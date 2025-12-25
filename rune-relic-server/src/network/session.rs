//! Match Session Management
//!
//! Manages the lifecycle of match sessions from matchmaking to completion.
//! Coordinates between connected clients and the deterministic game simulation.

use std::collections::BTreeMap;
use std::sync::Arc;
use std::time::{Duration, Instant};
use tokio::sync::{mpsc, RwLock, broadcast};

use crate::core::rng::derive_match_seed;
use crate::game::input::InputFrame;
use crate::game::state::{MatchState, PlayerId, MatchPhase};
use crate::game::tick::{tick, TickResult, MatchConfig};
use crate::proof::transcript::{MatchTranscript, MatchMetadata, MatchResult};
use crate::network::protocol::{
    ServerMessage, GameStateUpdate, PlayerStateUpdate, PlayerBuffs,
    MatchEvent, MatchEndInfo, PlayerPlacement, MatchMode,
    RuneUpdate, ShrineUpdate,
};

/// Unique session identifier.
pub type SessionId = [u8; 16];

/// Session state.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SessionState {
    /// Waiting for players to ready up.
    Lobby,
    /// Match countdown.
    Countdown,
    /// Match in progress.
    Playing,
    /// Match ended, processing results.
    Ended,
    /// Session closed.
    Closed,
}

/// Connection state for reconnection support.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ConnectionState {
    /// Player is connected.
    Connected,
    /// Player disconnected, waiting for reconnect.
    Disconnected {
        /// When disconnection occurred.
        since_tick: u32,
    },
}

/// Configuration for a match session.
#[derive(Debug, Clone)]
pub struct SessionConfig {
    /// Maximum players in match.
    pub max_players: usize,
    /// Minimum players to start.
    pub min_players: usize,
    /// Time to ready up after match found (seconds).
    pub ready_timeout: Duration,
    /// Countdown duration (seconds).
    pub countdown_duration: Duration,
    /// Match duration (ticks).
    pub match_duration_ticks: u32,
    /// Match mode.
    pub mode: MatchMode,
    /// Generate proof transcript.
    pub generate_proof: bool,
    /// Reconnect timeout in ticks (30 seconds at 60Hz = 1800 ticks).
    pub reconnect_timeout_ticks: u32,
}

impl Default for SessionConfig {
    fn default() -> Self {
        Self {
            max_players: 4,
            min_players: 2,
            ready_timeout: Duration::from_secs(30),
            countdown_duration: Duration::from_secs(3),
            match_duration_ticks: 5400, // 90 seconds @ 60Hz
            mode: MatchMode::Casual,
            generate_proof: false,
            reconnect_timeout_ticks: 1800, // 30 seconds @ 60Hz
        }
    }
}

/// A player connected to a session.
#[derive(Debug)]
pub struct SessionPlayer {
    /// Player identifier.
    pub player_id: PlayerId,
    /// Is player ready to start.
    pub ready: bool,
    /// Connection state (for reconnection support).
    pub connection_state: ConnectionState,
    /// Last input received.
    pub last_input: InputFrame,
    /// Last input tick.
    pub last_input_tick: u32,
    /// Round-trip time estimate (ms).
    pub rtt_ms: u32,
    /// Message channel to this player.
    pub sender: mpsc::Sender<ServerMessage>,
}

impl SessionPlayer {
    /// Check if player is connected.
    pub fn is_connected(&self) -> bool {
        matches!(self.connection_state, ConnectionState::Connected)
    }
}

/// A match session.
pub struct MatchSession {
    /// Unique session identifier.
    pub id: SessionId,
    /// Current state.
    pub state: SessionState,
    /// Session configuration.
    pub config: SessionConfig,
    /// Connected players.
    players: BTreeMap<PlayerId, SessionPlayer>,
    /// Game state (when playing).
    game_state: Option<MatchState>,
    /// Match configuration.
    match_config: MatchConfig,
    /// Proof transcript (if generating proofs).
    transcript: Option<MatchTranscript>,
    /// Block hash for seed derivation.
    block_hash: [u8; 32],
    /// When session was created.
    #[allow(dead_code)]
    created_at: Instant,
    /// When match started (if started).
    #[allow(dead_code)]
    started_at: Option<Instant>,
    /// Event broadcast channel.
    event_tx: broadcast::Sender<MatchEvent>,
}

impl MatchSession {
    /// Create a new session.
    pub fn new(id: SessionId, config: SessionConfig) -> Self {
        let (event_tx, _) = broadcast::channel(256);

        Self {
            id,
            state: SessionState::Lobby,
            config,
            players: BTreeMap::new(),
            game_state: None,
            match_config: MatchConfig::default(),
            transcript: None,
            block_hash: [0; 32],
            created_at: Instant::now(),
            started_at: None,
            event_tx,
        }
    }

    /// Add a player to the session.
    pub fn add_player(
        &mut self,
        player_id: PlayerId,
        sender: mpsc::Sender<ServerMessage>,
    ) -> Result<(), SessionError> {
        if self.state != SessionState::Lobby {
            return Err(SessionError::MatchInProgress);
        }

        if self.players.len() >= self.config.max_players {
            return Err(SessionError::SessionFull);
        }

        if self.players.contains_key(&player_id) {
            return Err(SessionError::AlreadyInSession);
        }

        self.players.insert(player_id, SessionPlayer {
            player_id,
            ready: false,
            connection_state: ConnectionState::Connected,
            last_input: InputFrame::new(),
            last_input_tick: 0,
            rtt_ms: 0,
            sender,
        });

        Ok(())
    }

    /// Remove a player from the session.
    pub fn remove_player(&mut self, player_id: &PlayerId) -> bool {
        if self.players.remove(player_id).is_some() {
            // If in lobby and no players left, close session
            if self.state == SessionState::Lobby && self.players.is_empty() {
                self.state = SessionState::Closed;
            }
            true
        } else {
            false
        }
    }

    /// Mark a player as disconnected (for reconnection support).
    /// Returns true if player was found and marked.
    pub fn mark_disconnected(&mut self, player_id: &PlayerId) -> bool {
        if let Some(player) = self.players.get_mut(player_id) {
            let current_tick = self.game_state.as_ref().map(|s| s.tick).unwrap_or(0);
            player.connection_state = ConnectionState::Disconnected { since_tick: current_tick };
            // Reset input to neutral
            player.last_input = InputFrame::new();
            true
        } else {
            false
        }
    }

    /// Reconnect a player with a new sender channel.
    /// Returns Some(current_tick) if reconnected, None if player not found or timed out.
    pub fn reconnect_player(
        &mut self,
        player_id: &PlayerId,
        sender: mpsc::Sender<ServerMessage>,
    ) -> Option<u32> {
        let current_tick = self.game_state.as_ref().map(|s| s.tick).unwrap_or(0);

        if let Some(player) = self.players.get_mut(player_id) {
            // Check if reconnect is within timeout
            if let ConnectionState::Disconnected { since_tick } = player.connection_state {
                let elapsed = current_tick.saturating_sub(since_tick);
                if elapsed > self.config.reconnect_timeout_ticks {
                    // Too late to reconnect
                    return None;
                }
            }

            player.connection_state = ConnectionState::Connected;
            player.sender = sender;
            Some(current_tick)
        } else {
            None
        }
    }

    /// Check if a player can reconnect (is disconnected but not timed out).
    pub fn can_reconnect(&self, player_id: &PlayerId) -> bool {
        let current_tick = self.game_state.as_ref().map(|s| s.tick).unwrap_or(0);

        if let Some(player) = self.players.get(player_id) {
            if let ConnectionState::Disconnected { since_tick } = player.connection_state {
                let elapsed = current_tick.saturating_sub(since_tick);
                return elapsed <= self.config.reconnect_timeout_ticks;
            }
        }
        false
    }

    /// Check and eliminate players who have been disconnected too long.
    /// Returns list of player IDs that were eliminated.
    pub fn check_reconnect_timeouts(&mut self) -> Vec<PlayerId> {
        let current_tick = self.game_state.as_ref().map(|s| s.tick).unwrap_or(0);
        let timeout = self.config.reconnect_timeout_ticks;

        let timed_out: Vec<PlayerId> = self.players.iter()
            .filter_map(|(id, player)| {
                if let ConnectionState::Disconnected { since_tick } = player.connection_state {
                    let elapsed = current_tick.saturating_sub(since_tick);
                    if elapsed > timeout {
                        return Some(*id);
                    }
                }
                None
            })
            .collect();

        // Mark timed-out players as eliminated in game state
        if let Some(ref mut state) = self.game_state {
            for player_id in &timed_out {
                if let Some(player) = state.players.get_mut(player_id) {
                    player.alive = false;
                }
            }
        }

        timed_out
    }

    /// Mark a player as ready.
    pub fn set_player_ready(&mut self, player_id: &PlayerId, ready: bool) -> bool {
        if let Some(player) = self.players.get_mut(player_id) {
            player.ready = ready;
            true
        } else {
            false
        }
    }

    /// Check if all players are ready.
    pub fn all_players_ready(&self) -> bool {
        self.players.len() >= self.config.min_players
            && self.players.values().all(|p| p.ready && p.is_connected())
    }

    /// Get player count.
    pub fn player_count(&self) -> usize {
        self.players.len()
    }

    /// Set block hash for seed derivation.
    pub fn set_block_hash(&mut self, block_hash: [u8; 32]) {
        self.block_hash = block_hash;
    }

    /// Start the match.
    pub fn start_match(&mut self) -> Result<MatchStartData, SessionError> {
        if self.state != SessionState::Lobby {
            return Err(SessionError::InvalidState);
        }

        if !self.all_players_ready() {
            return Err(SessionError::PlayersNotReady);
        }

        // Derive RNG seed from block hash
        let player_ids: Vec<[u8; 16]> = self.players.keys()
            .map(|id| *id.as_bytes())
            .collect();
        let rng_seed = derive_match_seed(&self.block_hash, &self.id, &player_ids);

        // Initialize game state
        let mut game_state = MatchState::new(self.id, rng_seed);

        // Add players to game state
        for player_id in self.players.keys() {
            game_state.add_player(*player_id);
        }

        // Create transcript if generating proofs
        if self.config.generate_proof {
            let metadata = MatchMetadata {
                match_id: self.id,
                block_hash: self.block_hash,
                player_ids: self.players.keys().map(|id| *id.as_bytes()).collect(),
                rng_seed,
                start_timestamp: std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .unwrap_or_default()
                    .as_secs(),
                config_hash: [0; 32], // TODO: hash config
            };
            self.transcript = Some(MatchTranscript::new(metadata));
        }

        // Collect initial positions
        let initial_players: Vec<_> = game_state.players.iter()
            .enumerate()
            .map(|(idx, (id, p))| {
                (
                    *id.as_bytes(),
                    [p.position.x, p.position.y],
                    idx as u8,
                )
            })
            .collect();

        self.game_state = Some(game_state);
        self.state = SessionState::Countdown;
        self.started_at = Some(Instant::now());

        Ok(MatchStartData {
            match_id: self.id,
            rng_seed,
            block_hash: self.block_hash,
            players: initial_players,
        })
    }

    /// Transition from countdown to playing.
    pub fn begin_playing(&mut self) {
        if self.state == SessionState::Countdown {
            self.state = SessionState::Playing;
            if let Some(ref mut state) = self.game_state {
                state.phase = MatchPhase::Playing;
            }
        }
    }

    /// Process a game input from a player.
    pub fn process_input(
        &mut self,
        player_id: &PlayerId,
        tick: u32,
        input: InputFrame,
    ) -> Result<(), SessionError> {
        if self.state != SessionState::Playing {
            return Err(SessionError::MatchNotInProgress);
        }

        // Update player's last input
        if let Some(player) = self.players.get_mut(player_id) {
            player.last_input = input;
            player.last_input_tick = tick;
        }

        Ok(())
    }

    /// Run a single game tick.
    pub fn run_tick(&mut self) -> Option<TickResult> {
        if self.state != SessionState::Playing {
            return None;
        }

        // Check for reconnect timeouts (eliminates players who have been disconnected too long)
        let _timed_out = self.check_reconnect_timeouts();

        let state = self.game_state.as_mut()?;

        // Collect inputs for all players
        // Disconnected players use neutral input (set in mark_disconnected)
        let mut inputs = BTreeMap::new();
        for (player_id, player) in &self.players {
            inputs.insert(*player_id, player.last_input);
        }

        // Run the tick
        let result = tick(state, &inputs, &self.match_config);

        // Record checkpoint in transcript
        if self.config.generate_proof {
            if let Some(ref mut transcript) = self.transcript {
                // Record checkpoint every 600 ticks
                if state.tick % 600 == 0 {
                    transcript.add_checkpoint(
                        state.tick,
                        state.compute_hash(),
                        state.rng.state(),
                    );
                }

                // Record events
                for event in &result.events {
                    transcript.record_event(event);
                }
            }
        }

        // Check if match ended
        if result.match_ended {
            self.state = SessionState::Ended;
        }

        Some(result)
    }

    /// Generate state update message.
    pub fn generate_state_update(&self) -> Option<GameStateUpdate> {
        let state = self.game_state.as_ref()?;

        let players: Vec<PlayerStateUpdate> = state.players.iter()
            .map(|(id, p)| PlayerStateUpdate {
                player_id: *id.as_bytes(),
                position: [p.position.x, p.position.y],
                velocity: [p.velocity.x, p.velocity.y],
                form: p.form as u8,
                score: p.score,
                alive: p.alive,
                radius: p.radius(),
                ability_cooldown: p.ability_cooldown,
                buffs: PlayerBuffs {
                    speed: p.speed_buff_ticks,
                    shield: p.shield_buff_ticks,
                    invulnerable: p.invulnerable_ticks,
                    shrine_buffs: p.shrine_buffs.iter()
                        .map(|shrine_type| *shrine_type as u8)
                        .collect(),
                },
            })
            .collect();

        // Collect active (uncollected) runes
        let runes: Vec<RuneUpdate> = state.runes.iter()
            .filter(|(_, r)| !r.collected)
            .map(|(_, r)| RuneUpdate {
                id: r.id,
                rune_type: r.rune_type as u8,
                position: [r.position.x, r.position.y],
                collected: r.collected,
            })
            .collect();

        // Collect shrine states
        let shrines: Vec<ShrineUpdate> = state.shrines.iter()
            .map(|s| ShrineUpdate {
                id: s.id as u32,
                shrine_type: s.shrine_type as u8,
                position: [s.position.x, s.position.y],
                active: s.active,
                controller: s.channeling_player.map(|p| *p.as_bytes()),
            })
            .collect();

        Some(GameStateUpdate {
            tick: state.tick,
            time_remaining: self.config.match_duration_ticks.saturating_sub(state.tick),
            players,
            runes: if runes.is_empty() { None } else { Some(runes) },
            shrines: if shrines.is_empty() { None } else { Some(shrines) },
            state_hash: state.compute_hash(),
        })
    }

    /// Finalize match and get results.
    pub fn finalize(&mut self) -> Option<MatchEndInfo> {
        if self.state != SessionState::Ended {
            return None;
        }

        let state = self.game_state.as_ref()?;
        let final_hash = state.compute_hash();

        // Build placements sorted by score
        let mut placements: Vec<_> = state.players.iter()
            .map(|(id, p)| PlayerPlacement {
                player_id: *id.as_bytes(),
                place: 0, // Set below
                score: p.score,
                eliminations: p.kills,
                runes_collected: p.runes_collected,
            })
            .collect();

        // Sort by score descending
        placements.sort_by(|a, b| b.score.cmp(&a.score));

        // Assign places
        for (i, p) in placements.iter_mut().enumerate() {
            p.place = (i + 1) as u8;
        }

        let winner_id = placements.first()
            .filter(|p| p.score > 0)
            .map(|p| p.player_id);

        // Finalize transcript
        let transcript_bytes = if self.config.generate_proof {
            if let Some(ref mut transcript) = self.transcript {
                let result = MatchResult {
                    end_tick: state.tick,
                    winner_id,
                    placements: placements.iter()
                        .map(|p| (p.player_id, p.place, p.score))
                        .collect(),
                    final_state_hash: final_hash,
                };
                transcript.finalize(result);
                Some(transcript.to_bytes())
            } else {
                None
            }
        } else {
            None
        };

        self.state = SessionState::Closed;

        Some(MatchEndInfo {
            match_id: self.id,
            end_tick: state.tick,
            winner_id,
            placements,
            final_state_hash: final_hash,
            transcript: transcript_bytes,
        })
    }

    /// Subscribe to match events.
    pub fn subscribe_events(&self) -> broadcast::Receiver<MatchEvent> {
        self.event_tx.subscribe()
    }

    /// Broadcast a message to all connected players.
    pub async fn broadcast(&self, message: ServerMessage) {
        for player in self.players.values() {
            if player.is_connected() {
                let _ = player.sender.send(message.clone()).await;
            }
        }
    }

    /// Get session state.
    pub fn get_state(&self) -> SessionState {
        self.state
    }

    /// Get current tick.
    pub fn current_tick(&self) -> u32 {
        self.game_state.as_ref().map(|s| s.tick).unwrap_or(0)
    }
}

/// Data returned when match starts.
#[derive(Debug, Clone)]
pub struct MatchStartData {
    /// Match identifier.
    pub match_id: SessionId,
    /// RNG seed.
    pub rng_seed: u64,
    /// Block hash used.
    pub block_hash: [u8; 32],
    /// Initial player data: (player_id, position, color_index).
    pub players: Vec<([u8; 16], [i32; 2], u8)>,
}

/// Session errors.
#[derive(Debug, Clone, thiserror::Error)]
pub enum SessionError {
    /// Session is full.
    #[error("Session is full")]
    SessionFull,

    /// Player already in session.
    #[error("Already in session")]
    AlreadyInSession,

    /// Match is in progress.
    #[error("Match in progress")]
    MatchInProgress,

    /// Match not in progress.
    #[error("Match not in progress")]
    MatchNotInProgress,

    /// Invalid session state.
    #[error("Invalid session state")]
    InvalidState,

    /// Players not ready.
    #[error("Players not ready")]
    PlayersNotReady,

    /// Player not found.
    #[error("Player not found")]
    PlayerNotFound,
}

// =============================================================================
// SESSION MANAGER
// =============================================================================

/// Manages all active sessions.
pub struct SessionManager {
    /// Active sessions.
    sessions: RwLock<BTreeMap<SessionId, Arc<RwLock<MatchSession>>>>,
    /// Player to session mapping.
    player_sessions: RwLock<BTreeMap<PlayerId, SessionId>>,
}

impl SessionManager {
    /// Create new session manager.
    pub fn new() -> Self {
        Self {
            sessions: RwLock::new(BTreeMap::new()),
            player_sessions: RwLock::new(BTreeMap::new()),
        }
    }

    /// Create a new session.
    pub async fn create_session(&self, config: SessionConfig) -> SessionId {
        let id = uuid::Uuid::new_v4().into_bytes();
        let session = MatchSession::new(id, config);

        let mut sessions = self.sessions.write().await;
        sessions.insert(id, Arc::new(RwLock::new(session)));

        id
    }

    /// Get a session by ID.
    pub async fn get_session(&self, id: &SessionId) -> Option<Arc<RwLock<MatchSession>>> {
        let sessions = self.sessions.read().await;
        sessions.get(id).cloned()
    }

    /// Get session for a player.
    pub async fn get_player_session(&self, player_id: &PlayerId) -> Option<Arc<RwLock<MatchSession>>> {
        let player_sessions = self.player_sessions.read().await;
        if let Some(session_id) = player_sessions.get(player_id) {
            self.get_session(session_id).await
        } else {
            None
        }
    }

    /// Register player in a session.
    pub async fn register_player(&self, player_id: PlayerId, session_id: SessionId) {
        let mut player_sessions = self.player_sessions.write().await;
        player_sessions.insert(player_id, session_id);
    }

    /// Unregister player from session.
    pub async fn unregister_player(&self, player_id: &PlayerId) {
        let mut player_sessions = self.player_sessions.write().await;
        player_sessions.remove(player_id);
    }

    /// Remove a session.
    pub async fn remove_session(&self, id: &SessionId) {
        let mut sessions = self.sessions.write().await;
        sessions.remove(id);
    }

    /// Get active session count.
    pub async fn session_count(&self) -> usize {
        let sessions = self.sessions.read().await;
        sessions.len()
    }

    /// Cleanup closed sessions.
    pub async fn cleanup(&self) {
        let mut sessions = self.sessions.write().await;
        let mut to_remove = Vec::new();

        for (id, session) in sessions.iter() {
            let s = session.read().await;
            if s.state == SessionState::Closed {
                to_remove.push(*id);
            }
        }

        for id in to_remove {
            sessions.remove(&id);
        }
    }
}

impl Default for SessionManager {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn create_test_session() -> MatchSession {
        MatchSession::new([0; 16], SessionConfig::default())
    }

    #[tokio::test]
    async fn test_add_remove_player() {
        let mut session = create_test_session();
        let player_id = PlayerId::new([1; 16]);
        let (tx, _rx) = mpsc::channel(10);

        session.add_player(player_id, tx).unwrap();
        assert_eq!(session.player_count(), 1);

        session.remove_player(&player_id);
        assert_eq!(session.player_count(), 0);
    }

    #[tokio::test]
    async fn test_session_full() {
        let config = SessionConfig {
            max_players: 2,
            ..Default::default()
        };
        let mut session = MatchSession::new([0; 16], config);

        for i in 0..2 {
            let player_id = PlayerId::new([i; 16]);
            let (tx, _rx) = mpsc::channel(10);
            session.add_player(player_id, tx).unwrap();
        }

        let extra_player = PlayerId::new([99; 16]);
        let (tx, _rx) = mpsc::channel(10);
        let result = session.add_player(extra_player, tx);
        assert!(matches!(result, Err(SessionError::SessionFull)));
    }

    #[tokio::test]
    async fn test_player_ready() {
        let mut session = create_test_session();
        let player1 = PlayerId::new([1; 16]);
        let player2 = PlayerId::new([2; 16]);
        let (tx1, _) = mpsc::channel(10);
        let (tx2, _) = mpsc::channel(10);

        session.add_player(player1, tx1).unwrap();
        session.add_player(player2, tx2).unwrap();

        assert!(!session.all_players_ready());

        session.set_player_ready(&player1, true);
        assert!(!session.all_players_ready());

        session.set_player_ready(&player2, true);
        assert!(session.all_players_ready());
    }

    #[tokio::test]
    async fn test_start_match() {
        let mut session = create_test_session();
        let player1 = PlayerId::new([1; 16]);
        let player2 = PlayerId::new([2; 16]);
        let (tx1, _) = mpsc::channel(10);
        let (tx2, _) = mpsc::channel(10);

        session.add_player(player1, tx1).unwrap();
        session.add_player(player2, tx2).unwrap();
        session.set_player_ready(&player1, true);
        session.set_player_ready(&player2, true);
        session.set_block_hash([42; 32]);

        let start_data = session.start_match().unwrap();
        assert_eq!(start_data.match_id, session.id);
        assert_eq!(start_data.players.len(), 2);
        assert_eq!(session.state, SessionState::Countdown);
    }

    #[tokio::test]
    async fn test_cannot_start_without_ready() {
        let mut session = create_test_session();
        let player1 = PlayerId::new([1; 16]);
        let player2 = PlayerId::new([2; 16]);
        let (tx1, _) = mpsc::channel(10);
        let (tx2, _) = mpsc::channel(10);

        session.add_player(player1, tx1).unwrap();
        session.add_player(player2, tx2).unwrap();

        let result = session.start_match();
        assert!(matches!(result, Err(SessionError::PlayersNotReady)));
    }

    #[tokio::test]
    async fn test_session_manager() {
        let manager = SessionManager::new();

        let session_id = manager.create_session(SessionConfig::default()).await;
        assert_eq!(manager.session_count().await, 1);

        let session = manager.get_session(&session_id).await;
        assert!(session.is_some());

        manager.remove_session(&session_id).await;
        assert_eq!(manager.session_count().await, 0);
    }

    #[tokio::test]
    async fn test_player_session_mapping() {
        let manager = SessionManager::new();
        let session_id = manager.create_session(SessionConfig::default()).await;
        let player_id = PlayerId::new([1; 16]);

        manager.register_player(player_id, session_id).await;

        let found = manager.get_player_session(&player_id).await;
        assert!(found.is_some());

        manager.unregister_player(&player_id).await;
        let not_found = manager.get_player_session(&player_id).await;
        assert!(not_found.is_none());
    }

    #[tokio::test]
    async fn test_run_tick() {
        let mut session = create_test_session();
        let player1 = PlayerId::new([1; 16]);
        let player2 = PlayerId::new([2; 16]);
        let (tx1, _) = mpsc::channel(10);
        let (tx2, _) = mpsc::channel(10);

        session.add_player(player1, tx1).unwrap();
        session.add_player(player2, tx2).unwrap();
        session.set_player_ready(&player1, true);
        session.set_player_ready(&player2, true);
        session.start_match().unwrap();
        session.begin_playing();

        let result = session.run_tick();
        assert!(result.is_some());
        assert_eq!(session.current_tick(), 1);
    }

    #[tokio::test]
    async fn test_generate_state_update() {
        let mut session = create_test_session();
        let player1 = PlayerId::new([1; 16]);
        let player2 = PlayerId::new([2; 16]);
        let (tx1, _) = mpsc::channel(10);
        let (tx2, _) = mpsc::channel(10);

        session.add_player(player1, tx1).unwrap();
        session.add_player(player2, tx2).unwrap();
        session.set_player_ready(&player1, true);
        session.set_player_ready(&player2, true);
        session.start_match().unwrap();

        let update = session.generate_state_update();
        assert!(update.is_some());
        let update = update.unwrap();
        assert_eq!(update.players.len(), 2);
    }

    #[tokio::test]
    async fn test_disconnect_marks_player() {
        let mut session = create_test_session();
        let player1 = PlayerId::new([1; 16]);
        let (tx1, _) = mpsc::channel(10);

        session.add_player(player1, tx1).unwrap();

        // Player starts connected
        assert!(session.players.get(&player1).unwrap().is_connected());

        // Mark disconnected
        assert!(session.mark_disconnected(&player1));
        assert!(!session.players.get(&player1).unwrap().is_connected());
    }

    #[tokio::test]
    async fn test_reconnect_restores_player() {
        let mut session = create_test_session();
        let player1 = PlayerId::new([1; 16]);
        let player2 = PlayerId::new([2; 16]);
        let (tx1, _) = mpsc::channel(10);
        let (tx2, _) = mpsc::channel(10);

        session.add_player(player1, tx1).unwrap();
        session.add_player(player2, tx2).unwrap();
        session.set_player_ready(&player1, true);
        session.set_player_ready(&player2, true);
        session.start_match().unwrap();
        session.begin_playing();

        // Mark disconnected
        session.mark_disconnected(&player1);
        assert!(!session.players.get(&player1).unwrap().is_connected());

        // Reconnect
        let (new_tx, _) = mpsc::channel(10);
        let result = session.reconnect_player(&player1, new_tx);
        assert!(result.is_some());
        assert!(session.players.get(&player1).unwrap().is_connected());
    }

    #[tokio::test]
    async fn test_reconnect_can_check() {
        let mut session = create_test_session();
        let player1 = PlayerId::new([1; 16]);
        let player2 = PlayerId::new([2; 16]);
        let (tx1, _) = mpsc::channel(10);
        let (tx2, _) = mpsc::channel(10);

        session.add_player(player1, tx1).unwrap();
        session.add_player(player2, tx2).unwrap();
        session.set_player_ready(&player1, true);
        session.set_player_ready(&player2, true);
        session.start_match().unwrap();
        session.begin_playing();

        // Connected player cannot reconnect (already connected)
        assert!(!session.can_reconnect(&player1));

        // Disconnected player can reconnect
        session.mark_disconnected(&player1);
        assert!(session.can_reconnect(&player1));
    }

    #[tokio::test]
    async fn test_idle_input_for_disconnected() {
        let mut session = create_test_session();
        let player1 = PlayerId::new([1; 16]);
        let player2 = PlayerId::new([2; 16]);
        let (tx1, _) = mpsc::channel(10);
        let (tx2, _) = mpsc::channel(10);

        session.add_player(player1, tx1).unwrap();
        session.add_player(player2, tx2).unwrap();
        session.set_player_ready(&player1, true);
        session.set_player_ready(&player2, true);
        session.start_match().unwrap();
        session.begin_playing();

        // Give player1 some input
        let input = InputFrame::with_movement(50, 50);
        session.process_input(&player1, 1, input).unwrap();

        // Now disconnect - input should be reset to neutral (-128 = no input)
        session.mark_disconnected(&player1);
        let player = session.players.get(&player1).unwrap();
        // InputFrame::new() uses NO_INPUT (-128) for neutral joystick state
        assert_eq!(player.last_input.move_x, InputFrame::NO_INPUT);
        assert_eq!(player.last_input.move_y, InputFrame::NO_INPUT);
    }
}

//! WebSocket Game Server
//!
//! Async WebSocket server for multiplayer connections.
//! Handles authentication, matchmaking, and game message routing.

use std::collections::BTreeMap;
use std::net::SocketAddr;
use std::sync::Arc;
use std::time::{Duration, Instant};
use tokio::net::{TcpListener, TcpStream};
use tokio::sync::{mpsc, RwLock, broadcast};
use tokio::time::interval;
use tokio_tungstenite::{accept_async, tungstenite::Message};
use futures_util::{SinkExt, StreamExt};
use tracing::{info, warn, error, debug, instrument};

use crate::game::state::PlayerId;
use crate::game::events::{GameEvent, GameEventData};
use crate::network::protocol::{
    ClientMessage, ServerMessage, AuthRequest, AuthResult, MatchmakingRequest,
    MatchmakingResponse, MatchmakingStatus, GameInput, MatchFoundInfo,
    MatchStartInfo, InitialPlayerInfo, MatchEvent, ErrorCode, MatchMode,
};
use crate::network::session::{
    SessionId, SessionState, SessionConfig, SessionManager, SessionError, MatchSession,
};

/// Convert a game event to a match event for client broadcasting.
fn convert_game_event_to_match_event(event: &GameEvent) -> MatchEvent {
    match &event.data {
        GameEventData::PlayerEliminated { victim_id, killer_id, placement } => {
            MatchEvent::PlayerEliminated {
                tick: event.tick,
                victim_id: *victim_id.as_bytes(),
                killer_id: killer_id.map(|id| *id.as_bytes()),
                victim_form: *placement, // Using placement as form for now
            }
        }
        GameEventData::RuneCollected { player_id, rune_id, rune_type, points, .. } => {
            MatchEvent::RuneCollected {
                tick: event.tick,
                player_id: *player_id.as_bytes(),
                rune_id: *rune_id,
                rune_type: *rune_type as u8,
                points: *points,
            }
        }
        GameEventData::FormEvolved { player_id, old_form, new_form } => {
            MatchEvent::PlayerEvolved {
                tick: event.tick,
                player_id: *player_id.as_bytes(),
                old_form: *old_form as u8,
                new_form: *new_form as u8,
            }
        }
        GameEventData::ShrineActivated { player_id, shrine_id } => {
            MatchEvent::ShrineCaptured {
                tick: event.tick,
                player_id: *player_id.as_bytes(),
                shrine_id: *shrine_id as u32,
                shrine_type: 0, // Generic shrine
            }
        }
        GameEventData::AbilityUsed { player_id, ability_type } => {
            MatchEvent::AbilityUsed {
                tick: event.tick,
                player_id: *player_id.as_bytes(),
                ability_type: *ability_type,
            }
        }
        // Events not sent to clients (internal)
        GameEventData::ShrineChannelStarted { .. } => MatchEvent::MatchStarted,
        GameEventData::ShrineChannelInterrupted { .. } => MatchEvent::MatchStarted,
        GameEventData::PhaseChanged { .. } => MatchEvent::MatchStarted,
        GameEventData::RuneSpawned { .. } => MatchEvent::MatchStarted,
        GameEventData::MatchEnded { .. } => MatchEvent::MatchStarted,
    }
}

/// Server configuration.
#[derive(Debug, Clone)]
pub struct ServerConfig {
    /// Bind address.
    pub bind_addr: SocketAddr,
    /// Maximum concurrent connections.
    pub max_connections: usize,
    /// Connection timeout.
    pub connection_timeout: Duration,
    /// Tick rate for game simulation (Hz).
    pub tick_rate: u32,
    /// Enable ranked mode with proofs.
    pub enable_ranked: bool,
    /// Server version string.
    pub version: String,
}

impl Default for ServerConfig {
    fn default() -> Self {
        Self {
            bind_addr: "0.0.0.0:8080".parse().unwrap(),
            max_connections: 1000,
            connection_timeout: Duration::from_secs(30),
            tick_rate: 60,
            enable_ranked: true,
            version: env!("CARGO_PKG_VERSION").to_string(),
        }
    }
}

/// Game server errors.
#[derive(Debug, thiserror::Error)]
pub enum GameServerError {
    /// Failed to bind to address.
    #[error("Failed to bind: {0}")]
    BindFailed(#[from] std::io::Error),

    /// WebSocket error.
    #[error("WebSocket error: {0}")]
    WebSocket(#[from] tokio_tungstenite::tungstenite::Error),

    /// Connection limit reached.
    #[error("Connection limit reached")]
    ConnectionLimitReached,

    /// Session error.
    #[error("Session error: {0}")]
    Session(#[from] SessionError),

    /// Internal error.
    #[error("Internal error: {0}")]
    Internal(String),
}

/// Connected client state.
struct ConnectedClient {
    /// Player identifier (after auth).
    player_id: Option<PlayerId>,
    /// Current session ID (if in match).
    session_id: Option<SessionId>,
    /// Is authenticated.
    authenticated: bool,
    /// Connection time.
    #[allow(dead_code)]
    connected_at: Instant,
    /// Last activity.
    last_activity: Instant,
    /// Message sender (for direct messaging to client).
    #[allow(dead_code)]
    sender: mpsc::Sender<ServerMessage>,
}

/// Matchmaking queue entry.
struct QueueEntry {
    player_id: PlayerId,
    mode: MatchMode,
    queued_at: Instant,
    sender: mpsc::Sender<ServerMessage>,
}

/// The game server.
pub struct GameServer {
    /// Server configuration.
    config: ServerConfig,
    /// Session manager.
    sessions: Arc<SessionManager>,
    /// Connected clients.
    clients: Arc<RwLock<BTreeMap<SocketAddr, ConnectedClient>>>,
    /// Matchmaking queue.
    matchmaking_queue: Arc<RwLock<Vec<QueueEntry>>>,
    /// Shutdown signal.
    shutdown_tx: broadcast::Sender<()>,
}

impl GameServer {
    /// Create a new game server.
    pub fn new(config: ServerConfig) -> Self {
        let (shutdown_tx, _) = broadcast::channel(1);

        Self {
            config,
            sessions: Arc::new(SessionManager::new()),
            clients: Arc::new(RwLock::new(BTreeMap::new())),
            matchmaking_queue: Arc::new(RwLock::new(Vec::new())),
            shutdown_tx,
        }
    }

    /// Run the server.
    #[instrument(skip(self))]
    pub async fn run(&self) -> Result<(), GameServerError> {
        let listener = TcpListener::bind(&self.config.bind_addr).await?;
        info!("Game server listening on {}", self.config.bind_addr);

        // Clone references for background tasks
        let matchmaking_queue = self.matchmaking_queue.clone();
        let matchmaking_sessions = self.sessions.clone();
        let matchmaking_clients = self.clients.clone();

        let cleanup_clients = self.clients.clone();
        let cleanup_sessions = self.sessions.clone();

        // Spawn matchmaking task
        let matchmaking_handle = tokio::spawn(async move {
            Self::run_matchmaking_loop(matchmaking_queue, matchmaking_sessions, matchmaking_clients).await;
        });

        // Spawn cleanup task
        let cleanup_handle = tokio::spawn(async move {
            Self::run_cleanup_loop(cleanup_clients, cleanup_sessions).await;
        });

        let mut shutdown_rx = self.shutdown_tx.subscribe();

        loop {
            tokio::select! {
                result = listener.accept() => {
                    match result {
                        Ok((stream, addr)) => {
                            let clients_count = self.clients.read().await.len();
                            if clients_count >= self.config.max_connections {
                                warn!("Connection limit reached, rejecting {}", addr);
                                continue;
                            }

                            info!("New connection from {}", addr);
                            self.handle_connection(stream, addr);
                        }
                        Err(e) => {
                            error!("Accept error: {}", e);
                        }
                    }
                }
                _ = shutdown_rx.recv() => {
                    info!("Shutdown signal received");
                    break;
                }
            }
        }

        // Wait for background tasks
        matchmaking_handle.abort();
        cleanup_handle.abort();

        Ok(())
    }

    /// Handle a new WebSocket connection.
    fn handle_connection(&self, stream: TcpStream, addr: SocketAddr) {
        let clients = self.clients.clone();
        let sessions = self.sessions.clone();
        let matchmaking_queue = self.matchmaking_queue.clone();
        let config = self.config.clone();
        let mut shutdown_rx = self.shutdown_tx.subscribe();

        tokio::spawn(async move {
            let ws_stream = match accept_async(stream).await {
                Ok(ws) => ws,
                Err(e) => {
                    error!("WebSocket handshake failed for {}: {}", addr, e);
                    return;
                }
            };

            let (mut ws_sender, mut ws_receiver) = ws_stream.split();
            let (msg_tx, mut msg_rx) = mpsc::channel::<ServerMessage>(64);

            // Register client
            {
                let mut clients = clients.write().await;
                clients.insert(addr, ConnectedClient {
                    player_id: None,
                    session_id: None,
                    authenticated: false,
                    connected_at: Instant::now(),
                    last_activity: Instant::now(),
                    sender: msg_tx.clone(),
                });
            }

            // Spawn message sender task
            let sender_task = tokio::spawn(async move {
                while let Some(msg) = msg_rx.recv().await {
                    let text = match msg.to_json() {
                        Ok(t) => t,
                        Err(e) => {
                            error!("Failed to serialize message: {}", e);
                            continue;
                        }
                    };
                    if ws_sender.send(Message::Text(text)).await.is_err() {
                        break;
                    }
                }
            });

            // Handle incoming messages
            loop {
                tokio::select! {
                    msg = ws_receiver.next() => {
                        match msg {
                            Some(Ok(Message::Text(text))) => {
                                let client_msg = match ClientMessage::from_json(&text) {
                                    Ok(m) => m,
                                    Err(e) => {
                                        debug!("Invalid message from {}: {}", addr, e);
                                        let _ = msg_tx.send(ServerMessage::Error(
                                            crate::network::protocol::ServerError {
                                                code: ErrorCode::InvalidInput,
                                                message: "Invalid message format".to_string(),
                                            }
                                        )).await;
                                        continue;
                                    }
                                };

                                // Update activity
                                {
                                    let mut clients = clients.write().await;
                                    if let Some(client) = clients.get_mut(&addr) {
                                        client.last_activity = Instant::now();
                                    }
                                }

                                // Handle message
                                Self::handle_client_message(
                                    addr,
                                    client_msg,
                                    &clients,
                                    &sessions,
                                    &matchmaking_queue,
                                    &config,
                                    &msg_tx,
                                ).await;
                            }
                            Some(Ok(Message::Binary(data))) => {
                                // Handle binary protocol
                                if let Ok(client_msg) = ClientMessage::from_bytes(&data) {
                                    Self::handle_client_message(
                                        addr,
                                        client_msg,
                                        &clients,
                                        &sessions,
                                        &matchmaking_queue,
                                        &config,
                                        &msg_tx,
                                    ).await;
                                }
                            }
                            Some(Ok(Message::Ping(_))) => {
                                let _ = msg_tx.send(ServerMessage::Pong {
                                    timestamp: 0,
                                    server_time: std::time::SystemTime::now()
                                        .duration_since(std::time::UNIX_EPOCH)
                                        .unwrap_or_default()
                                        .as_millis() as u64,
                                }).await;
                            }
                            Some(Ok(Message::Close(_))) | None => {
                                debug!("Client {} disconnected", addr);
                                break;
                            }
                            Some(Err(e)) => {
                                error!("WebSocket error for {}: {}", addr, e);
                                break;
                            }
                            _ => {}
                        }
                    }
                    _ = shutdown_rx.recv() => {
                        let _ = msg_tx.send(ServerMessage::Shutdown {
                            reason: "Server shutting down".to_string(),
                        }).await;
                        break;
                    }
                }
            }

            // Cleanup
            sender_task.abort();

            // Remove from matchmaking queue
            {
                let clients = clients.read().await;
                if let Some(client) = clients.get(&addr) {
                    if let Some(player_id) = client.player_id {
                        let mut queue = matchmaking_queue.write().await;
                        queue.retain(|e| e.player_id != player_id);
                    }
                }
            }

            // Remove client
            {
                let mut clients = clients.write().await;
                if let Some(client) = clients.remove(&addr) {
                    if let Some(player_id) = client.player_id {
                        sessions.unregister_player(&player_id).await;
                    }
                }
            }

            info!("Client {} cleaned up", addr);
        });
    }

    /// Handle a client message.
    async fn handle_client_message(
        addr: SocketAddr,
        msg: ClientMessage,
        clients: &Arc<RwLock<BTreeMap<SocketAddr, ConnectedClient>>>,
        sessions: &Arc<SessionManager>,
        matchmaking_queue: &Arc<RwLock<Vec<QueueEntry>>>,
        config: &ServerConfig,
        sender: &mpsc::Sender<ServerMessage>,
    ) {
        match msg {
            ClientMessage::Auth(auth) => {
                Self::handle_auth(addr, auth, clients, config, sender).await;
            }
            ClientMessage::Matchmaking(req) => {
                Self::handle_matchmaking(addr, req, clients, matchmaking_queue, sender).await;
            }
            ClientMessage::CancelMatchmaking => {
                Self::handle_cancel_matchmaking(addr, clients, matchmaking_queue, sender).await;
            }
            ClientMessage::Input(input) => {
                Self::handle_input(addr, input, clients, sessions, sender).await;
            }
            ClientMessage::Ready => {
                Self::handle_ready(addr, clients, sessions, config, sender).await;
            }
            ClientMessage::Ping { timestamp } => {
                let _ = sender.send(ServerMessage::Pong {
                    timestamp,
                    server_time: std::time::SystemTime::now()
                        .duration_since(std::time::UNIX_EPOCH)
                        .unwrap_or_default()
                        .as_millis() as u64,
                }).await;
            }
            ClientMessage::Leave => {
                Self::handle_leave(addr, clients, sessions, matchmaking_queue).await;
            }
            _ => {
                debug!("Unhandled message type from {}", addr);
            }
        }
    }

    /// Handle authentication.
    async fn handle_auth(
        addr: SocketAddr,
        auth: AuthRequest,
        clients: &Arc<RwLock<BTreeMap<SocketAddr, ConnectedClient>>>,
        config: &ServerConfig,
        sender: &mpsc::Sender<ServerMessage>,
    ) {
        // TODO: Implement proper authentication
        // For now, accept any valid player ID
        let player_id = PlayerId::new(auth.player_id);

        let mut clients = clients.write().await;
        if let Some(client) = clients.get_mut(&addr) {
            client.player_id = Some(player_id);
            client.authenticated = true;
        }

        let _ = sender.send(ServerMessage::AuthResult(AuthResult {
            success: true,
            session_id: Some(hex::encode(&auth.player_id[..8])),
            error: None,
            server_version: config.version.clone(),
        })).await;

        debug!("Client {} authenticated as {:?}", addr, &auth.player_id[..4]);
    }

    /// Handle matchmaking request.
    async fn handle_matchmaking(
        addr: SocketAddr,
        req: MatchmakingRequest,
        clients: &Arc<RwLock<BTreeMap<SocketAddr, ConnectedClient>>>,
        matchmaking_queue: &Arc<RwLock<Vec<QueueEntry>>>,
        sender: &mpsc::Sender<ServerMessage>,
    ) {
        let player_id = {
            let clients = clients.read().await;
            match clients.get(&addr) {
                Some(c) if c.authenticated => c.player_id,
                _ => {
                    let _ = sender.send(ServerMessage::Error(crate::network::protocol::ServerError {
                        code: ErrorCode::NotAuthenticated,
                        message: "Must authenticate first".to_string(),
                    })).await;
                    return;
                }
            }
        };

        let player_id = match player_id {
            Some(id) => id,
            None => return,
        };

        // Add to queue
        {
            let mut queue = matchmaking_queue.write().await;

            // Check if already in queue
            if queue.iter().any(|e| e.player_id == player_id) {
                let _ = sender.send(ServerMessage::Matchmaking(MatchmakingResponse {
                    status: MatchmakingStatus::Searching,
                    estimated_wait: Some(30),
                    players_found: 0,
                    players_needed: 4,
                })).await;
                return;
            }

            queue.push(QueueEntry {
                player_id,
                mode: req.mode,
                queued_at: Instant::now(),
                sender: sender.clone(),
            });
        }

        let _ = sender.send(ServerMessage::Matchmaking(MatchmakingResponse {
            status: MatchmakingStatus::Searching,
            estimated_wait: Some(30),
            players_found: 1,
            players_needed: 4,
        })).await;

        debug!("Player {:?} queued for matchmaking", &player_id.as_bytes()[..4]);
    }

    /// Handle cancel matchmaking.
    async fn handle_cancel_matchmaking(
        addr: SocketAddr,
        clients: &Arc<RwLock<BTreeMap<SocketAddr, ConnectedClient>>>,
        matchmaking_queue: &Arc<RwLock<Vec<QueueEntry>>>,
        sender: &mpsc::Sender<ServerMessage>,
    ) {
        let player_id = {
            let clients = clients.read().await;
            clients.get(&addr).and_then(|c| c.player_id)
        };

        if let Some(player_id) = player_id {
            let mut queue = matchmaking_queue.write().await;
            queue.retain(|e| e.player_id != player_id);
        }

        let _ = sender.send(ServerMessage::Matchmaking(MatchmakingResponse {
            status: MatchmakingStatus::Cancelled,
            estimated_wait: None,
            players_found: 0,
            players_needed: 0,
        })).await;
    }

    /// Handle player input.
    async fn handle_input(
        addr: SocketAddr,
        input: GameInput,
        clients: &Arc<RwLock<BTreeMap<SocketAddr, ConnectedClient>>>,
        sessions: &Arc<SessionManager>,
        sender: &mpsc::Sender<ServerMessage>,
    ) {
        let (player_id, session_id) = {
            let clients = clients.read().await;
            match clients.get(&addr) {
                Some(c) => (c.player_id, c.session_id),
                None => return,
            }
        };

        let player_id = match player_id {
            Some(id) => id,
            None => return,
        };

        let session_id = match session_id {
            Some(id) => id,
            None => return,
        };

        if let Some(session) = sessions.get_session(&session_id).await {
            let (process_result, server_tick) = {
                let mut session_guard = session.write().await;
                let result = session_guard.process_input(&player_id, input.tick, input.to_input_frame());
                let tick = session_guard.current_tick();
                (result, tick)
            };

            // Send input acknowledgment
            if process_result.is_ok() {
                let _ = sender.send(ServerMessage::InputAck {
                    tick: input.tick,
                    server_tick,
                }).await;
            }
        }
    }

    /// Handle player ready.
    async fn handle_ready(
        addr: SocketAddr,
        clients: &Arc<RwLock<BTreeMap<SocketAddr, ConnectedClient>>>,
        sessions: &Arc<SessionManager>,
        config: &ServerConfig,
        sender: &mpsc::Sender<ServerMessage>,
    ) {
        let (player_id, session_id) = {
            let clients = clients.read().await;
            match clients.get(&addr) {
                Some(c) => (c.player_id, c.session_id),
                None => return,
            }
        };

        let player_id = match player_id {
            Some(id) => id,
            None => return,
        };

        let session_id = match session_id {
            Some(id) => id,
            None => return,
        };

        let should_start = if let Some(session) = sessions.get_session(&session_id).await {
            let mut session_guard = session.write().await;
            session_guard.set_player_ready(&player_id, true);

            // Check if all players are ready and session is in Lobby state
            session_guard.all_players_ready() && session_guard.get_state() == SessionState::Lobby
        } else {
            false
        };

        debug!("Player {:?} marked ready", &player_id.as_bytes()[..4]);

        // If all players ready, start the match
        if should_start {
            if let Some(session) = sessions.get_session(&session_id).await {
                // Generate a block hash for seed derivation (in production, use real block hash)
                let block_hash: [u8; 32] = {
                    use std::time::{SystemTime, UNIX_EPOCH};
                    let mut hash = [0u8; 32];
                    let nanos = SystemTime::now()
                        .duration_since(UNIX_EPOCH)
                        .unwrap_or_default()
                        .as_nanos();
                    for (i, byte) in nanos.to_le_bytes().iter().enumerate() {
                        if i < 32 {
                            hash[i] = *byte;
                        }
                    }
                    hash
                };

                // Start the match
                let start_result = {
                    let mut session_guard = session.write().await;
                    session_guard.set_block_hash(block_hash);
                    session_guard.start_match()
                };

                match start_result {
                    Ok(start_data) => {
                        // Build MatchStartInfo
                        let match_start = MatchStartInfo {
                            match_id: start_data.match_id,
                            rng_seed: start_data.rng_seed,
                            start_tick: 0,
                            players: start_data.players.iter().map(|(id, pos, color)| {
                                InitialPlayerInfo {
                                    player_id: *id,
                                    position: *pos,
                                    color_index: *color,
                                }
                            }).collect(),
                            config_hash: [0; 32],
                            block_hash: start_data.block_hash,
                        };

                        // Broadcast match start to all players
                        {
                            let session_guard = session.read().await;
                            session_guard.broadcast(ServerMessage::MatchStart(match_start)).await;
                        }

                        info!("Match {:?} starting with {} players",
                            &session_id[..4], start_data.players.len());

                        // Spawn the game loop
                        let session_clone = session.clone();
                        let sessions_clone = sessions.clone();
                        let tick_rate = config.tick_rate;

                        tokio::spawn(async move {
                            Self::run_session_game_loop(session_clone, sessions_clone, tick_rate).await;
                        });
                    }
                    Err(e) => {
                        error!("Failed to start match: {:?}", e);
                        let _ = sender.send(ServerMessage::Error(crate::network::protocol::ServerError {
                            code: ErrorCode::InternalError,
                            message: format!("Failed to start match: {:?}", e),
                        })).await;
                    }
                }
            }
        }
    }

    /// Handle player leave.
    async fn handle_leave(
        addr: SocketAddr,
        clients: &Arc<RwLock<BTreeMap<SocketAddr, ConnectedClient>>>,
        sessions: &Arc<SessionManager>,
        matchmaking_queue: &Arc<RwLock<Vec<QueueEntry>>>,
    ) {
        let (player_id, session_id) = {
            let clients = clients.read().await;
            match clients.get(&addr) {
                Some(c) => (c.player_id, c.session_id),
                None => return,
            }
        };

        if let Some(player_id) = player_id {
            // Remove from matchmaking
            {
                let mut queue = matchmaking_queue.write().await;
                queue.retain(|e| e.player_id != player_id);
            }

            // Remove from session
            if let Some(session_id) = session_id {
                if let Some(session) = sessions.get_session(&session_id).await {
                    let mut session = session.write().await;
                    session.remove_player(&player_id);
                }
                sessions.unregister_player(&player_id).await;
            }
        }

        // Clear session from client
        {
            let mut clients = clients.write().await;
            if let Some(client) = clients.get_mut(&addr) {
                client.session_id = None;
            }
        }
    }

    /// Run the game loop for a session.
    /// Handles countdown, tick execution at 60Hz, state broadcasting, and match end.
    async fn run_session_game_loop(
        session: Arc<RwLock<MatchSession>>,
        sessions: Arc<SessionManager>,
        tick_rate: u32,
    ) {
        let session_id = session.read().await.id;
        let countdown_duration = session.read().await.config.countdown_duration;

        // Phase 1: Countdown
        let countdown_secs = countdown_duration.as_secs() as u32;
        for remaining in (1..=countdown_secs).rev() {
            // Broadcast countdown event
            {
                let s = session.read().await;
                s.broadcast(ServerMessage::Event(MatchEvent::Countdown { seconds: remaining })).await;
            }
            tokio::time::sleep(Duration::from_secs(1)).await;
        }

        // Transition to playing
        {
            let mut s = session.write().await;
            s.begin_playing();
            info!("Match {:?} started playing", &session_id[..4]);
        }

        // Broadcast match start (game is now running)
        {
            let s = session.read().await;
            s.broadcast(ServerMessage::Event(MatchEvent::MatchStarted)).await;
        }

        // Phase 2: Game tick loop at 60Hz
        let tick_duration = Duration::from_micros(1_000_000 / tick_rate as u64);
        let mut tick_interval = interval(tick_duration);
        tick_interval.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);

        loop {
            tick_interval.tick().await;

            let (match_ended, state_update, events) = {
                let mut s = session.write().await;

                // Check if session is still in playing state
                if s.get_state() != SessionState::Playing {
                    break;
                }

                // Run the game tick
                let tick_result = match s.run_tick() {
                    Some(result) => result,
                    None => break,
                };

                let match_ended = tick_result.match_ended;
                let events = tick_result.events.clone();

                // Generate state update
                let state_update = s.generate_state_update();

                (match_ended, state_update, events)
            };

            // Broadcast events
            {
                let s = session.read().await;
                for event in events {
                    let match_event = convert_game_event_to_match_event(&event);
                    s.broadcast(ServerMessage::Event(match_event)).await;
                }
            }

            // Broadcast state update (every tick or every N ticks for bandwidth)
            if let Some(update) = state_update {
                let s = session.read().await;
                s.broadcast(ServerMessage::State(update)).await;
            }

            // Check if match ended
            if match_ended {
                break;
            }
        }

        // Phase 3: Match end
        let end_info = {
            let mut s = session.write().await;
            s.finalize()
        };

        if let Some(end_info) = end_info {
            let s = session.read().await;
            s.broadcast(ServerMessage::MatchEnd(end_info)).await;
            info!("Match {:?} ended", &session_id[..4]);
        }

        // Cleanup session after a delay
        tokio::time::sleep(Duration::from_secs(5)).await;
        sessions.remove_session(&session_id).await;
    }

    /// Run matchmaking loop.
    async fn run_matchmaking_loop(
        queue: Arc<RwLock<Vec<QueueEntry>>>,
        sessions: Arc<SessionManager>,
        clients: Arc<RwLock<BTreeMap<SocketAddr, ConnectedClient>>>,
    ) {
        let mut interval = interval(Duration::from_secs(1));

        loop {
            interval.tick().await;

            let mut queue_guard = queue.write().await;

            // Get indices of casual players
            let casual_indices: Vec<usize> = queue_guard.iter()
                .enumerate()
                .filter(|(_, e)| e.mode == MatchMode::Casual)
                .map(|(i, _)| i)
                .collect();

            // Create matches for casual (need 2-4 players)
            if casual_indices.len() >= 2 {
                let match_size = casual_indices.len().min(4);
                let matched_indices: Vec<usize> = casual_indices[..match_size].to_vec();

                // Extract matched entries (remove from back to front to preserve indices)
                let mut matched_entries = Vec::new();
                let mut indices_to_remove = matched_indices.clone();
                indices_to_remove.sort_by(|a, b| b.cmp(a)); // Sort descending

                for idx in &indices_to_remove {
                    matched_entries.push(queue_guard.remove(*idx));
                }
                matched_entries.reverse(); // Restore original order

                // Create session
                let config = SessionConfig {
                    max_players: 4,
                    min_players: 2,
                    mode: MatchMode::Casual,
                    generate_proof: false,
                    ..Default::default()
                };

                let session_id = sessions.create_session(config).await;

                if let Some(session) = sessions.get_session(&session_id).await {
                    let mut session = session.write().await;

                    // Add players
                    for entry in &matched_entries {
                        let _ = session.add_player(entry.player_id, entry.sender.clone());
                        sessions.register_player(entry.player_id, session_id).await;

                        // Update client state
                        let mut clients_guard = clients.write().await;
                        for (_, client) in clients_guard.iter_mut() {
                            if client.player_id == Some(entry.player_id) {
                                client.session_id = Some(session_id);
                            }
                        }
                    }

                    // Notify players
                    let match_found = ServerMessage::MatchFound(MatchFoundInfo {
                        match_id: session_id,
                        player_ids: matched_entries.iter().map(|e| *e.player_id.as_bytes()).collect(),
                        mode: MatchMode::Casual,
                        ready_timeout: 30,
                    });

                    for entry in &matched_entries {
                        let _ = entry.sender.send(match_found.clone()).await;
                    }

                    info!("Created match {:?} with {} players",
                        &session_id[..4], matched_entries.len());
                }
            }

            // Timeout stale queue entries (> 2 minutes)
            let now = Instant::now();
            let timed_out: Vec<_> = queue_guard.iter()
                .filter(|e| now.duration_since(e.queued_at) > Duration::from_secs(120))
                .map(|e| (e.player_id, e.sender.clone()))
                .collect();

            for (player_id, sender) in timed_out {
                let _ = sender.send(ServerMessage::Matchmaking(MatchmakingResponse {
                    status: MatchmakingStatus::Failed,
                    estimated_wait: None,
                    players_found: 0,
                    players_needed: 0,
                })).await;
                queue_guard.retain(|e| e.player_id != player_id);
            }
        }
    }

    /// Run cleanup loop.
    async fn run_cleanup_loop(
        clients: Arc<RwLock<BTreeMap<SocketAddr, ConnectedClient>>>,
        sessions: Arc<SessionManager>,
    ) {
        let mut interval = interval(Duration::from_secs(60));

        loop {
            interval.tick().await;

            // Cleanup idle connections
            let now = Instant::now();
            let idle_timeout = Duration::from_secs(300); // 5 minutes

            let to_remove: Vec<_> = {
                let clients = clients.read().await;
                clients.iter()
                    .filter(|(_, c)| now.duration_since(c.last_activity) > idle_timeout)
                    .map(|(addr, _)| *addr)
                    .collect()
            };

            for addr in to_remove {
                let mut clients = clients.write().await;
                if let Some(client) = clients.remove(&addr) {
                    if let Some(player_id) = client.player_id {
                        sessions.unregister_player(&player_id).await;
                    }
                    info!("Removed idle client {}", addr);
                }
            }

            // Cleanup closed sessions
            sessions.cleanup().await;
        }
    }

    /// Shutdown the server.
    pub fn shutdown(&self) {
        let _ = self.shutdown_tx.send(());
    }

    /// Get active connection count.
    pub async fn connection_count(&self) -> usize {
        self.clients.read().await.len()
    }

    /// Get active session count.
    pub async fn session_count(&self) -> usize {
        self.sessions.session_count().await
    }

    /// Get matchmaking queue size.
    pub async fn queue_size(&self) -> usize {
        self.matchmaking_queue.read().await.len()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_server_config_default() {
        let config = ServerConfig::default();
        assert_eq!(config.tick_rate, 60);
        assert_eq!(config.max_connections, 1000);
        assert!(config.enable_ranked);
    }

    #[tokio::test]
    async fn test_server_creation() {
        let config = ServerConfig {
            bind_addr: "127.0.0.1:0".parse().unwrap(),
            ..Default::default()
        };
        let server = GameServer::new(config);

        assert_eq!(server.connection_count().await, 0);
        assert_eq!(server.session_count().await, 0);
        assert_eq!(server.queue_size().await, 0);
    }

    #[tokio::test]
    async fn test_server_shutdown() {
        let config = ServerConfig {
            bind_addr: "127.0.0.1:0".parse().unwrap(),
            ..Default::default()
        };
        let server = GameServer::new(config);
        server.shutdown();
        // Should not panic
    }
}

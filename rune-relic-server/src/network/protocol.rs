//! Protocol Messages
//!
//! Wire format for client-server communication over WebSocket.
//! All messages are serialized as JSON for debugging ease,
//! with optional binary (bincode) for production.

use serde::{Serialize, Deserialize};
use crate::core::vec2::FixedVec2;
use crate::game::input::InputFrame;

// =============================================================================
// CLIENT -> SERVER MESSAGES
// =============================================================================

/// Messages sent from client to server.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum ClientMessage {
    /// Authenticate with the server.
    Auth(AuthRequest),

    /// Request to join matchmaking.
    Matchmaking(MatchmakingRequest),

    /// Cancel matchmaking.
    CancelMatchmaking,

    /// Player input for current tick.
    Input(GameInput),

    /// Ready to start match.
    Ready,

    /// Request current match state (for reconnection).
    SyncRequest,

    /// Ping for latency measurement.
    Ping { timestamp: u64 },

    /// Player is leaving the match.
    Leave,
}

/// Authentication request.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AuthRequest {
    /// Player's unique identifier (hex string for JSON compatibility).
    pub player_id: String,
    /// Authentication token (JWT or session token).
    pub token: String,
    /// Client version for compatibility check.
    pub client_version: String,
}

impl AuthRequest {
    /// Parse player_id from hex string to bytes.
    pub fn player_id_bytes(&self) -> Option<[u8; 16]> {
        let bytes = hex::decode(&self.player_id).ok()?;
        if bytes.len() != 16 {
            return None;
        }
        let mut arr = [0u8; 16];
        arr.copy_from_slice(&bytes);
        Some(arr)
    }
}

/// Matchmaking request.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MatchmakingRequest {
    /// Preferred match mode.
    pub mode: MatchMode,
    /// Optional match ID for private matches.
    pub match_id: Option<[u8; 16]>,
    /// Player's commitment hash (for ranked).
    pub commitment: Option<[u8; 32]>,
}

/// Match modes.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum MatchMode {
    /// Quick play - casual matches.
    Casual,
    /// Ranked - with BitSage proofs.
    Ranked,
    /// Private match with friends.
    Private,
    /// Practice mode (solo).
    Practice,
}

/// Player input for a game tick.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct GameInput {
    /// Client tick number.
    pub tick: u32,
    /// Movement X direction (-127 to 127).
    pub move_x: i8,
    /// Movement Y direction (-127 to 127).
    pub move_y: i8,
    /// Action flags.
    pub flags: u8,
    /// Client timestamp for RTT calculation.
    pub timestamp: u64,
}

impl GameInput {
    /// Convert to InputFrame for game simulation.
    pub fn to_input_frame(&self) -> InputFrame {
        InputFrame {
            move_x: self.move_x,
            move_y: self.move_y,
            flags: self.flags,
        }
    }
}

// =============================================================================
// SERVER -> CLIENT MESSAGES
// =============================================================================

/// Messages sent from server to client.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum ServerMessage {
    /// Authentication result.
    AuthResult(AuthResult),

    /// Matchmaking status update.
    Matchmaking(MatchmakingResponse),

    /// Match found, prepare to start.
    MatchFound(MatchFoundInfo),

    /// Match is starting.
    MatchStart(MatchStartInfo),

    /// Game state update (every tick).
    State(GameStateUpdate),

    /// Game event notification.
    Event(MatchEvent),

    /// Match ended.
    MatchEnd(MatchEndInfo),

    /// Input acknowledgment.
    InputAck { tick: u32, server_tick: u32 },

    /// Pong response.
    Pong { timestamp: u64, server_time: u64 },

    /// Error message.
    Error(ServerError),

    /// Server is shutting down.
    Shutdown { reason: String },
}

/// Authentication result.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AuthResult {
    /// Whether auth succeeded.
    pub success: bool,
    /// Session ID if successful.
    pub session_id: Option<String>,
    /// Error message if failed.
    pub error: Option<String>,
    /// Server version.
    pub server_version: String,
}

/// Matchmaking status response.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MatchmakingResponse {
    /// Current status.
    pub status: MatchmakingStatus,
    /// Estimated wait time in seconds.
    pub estimated_wait: Option<u32>,
    /// Players found so far.
    pub players_found: u32,
    /// Players needed.
    pub players_needed: u32,
}

/// Matchmaking status.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum MatchmakingStatus {
    /// Searching for players.
    Searching,
    /// Match found, waiting for players to ready.
    Found,
    /// Match starting.
    Starting,
    /// Cancelled by player.
    Cancelled,
    /// Failed to find match.
    Failed,
}

/// Information about a found match.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MatchFoundInfo {
    /// Unique match identifier.
    pub match_id: [u8; 16],
    /// List of player IDs in the match.
    pub player_ids: Vec<[u8; 16]>,
    /// Match mode.
    pub mode: MatchMode,
    /// Time limit to ready up (seconds).
    pub ready_timeout: u32,
}

/// Information when match starts.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MatchStartInfo {
    /// Match identifier.
    pub match_id: [u8; 16],
    /// RNG seed (derived from block hash + player commitments).
    pub rng_seed: u64,
    /// Server tick when match starts.
    pub start_tick: u32,
    /// Initial player states.
    pub players: Vec<InitialPlayerInfo>,
    /// Match configuration hash.
    pub config_hash: [u8; 32],
    /// Block hash used for seed derivation.
    pub block_hash: [u8; 32],
}

/// Initial player information.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct InitialPlayerInfo {
    /// Player identifier.
    pub player_id: [u8; 16],
    /// Starting position.
    pub position: [i32; 2],
    /// Assigned color index.
    pub color_index: u8,
}

impl InitialPlayerInfo {
    /// Get position as FixedVec2.
    pub fn position_vec(&self) -> FixedVec2 {
        FixedVec2::new(self.position[0], self.position[1])
    }
}

/// Game state update (sent every tick or on demand).
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct GameStateUpdate {
    /// Current server tick.
    pub tick: u32,
    /// Time remaining in match (ticks).
    pub time_remaining: u32,
    /// Player states.
    pub players: Vec<PlayerStateUpdate>,
    /// Active runes (only changed ones).
    #[serde(skip_serializing_if = "Option::is_none")]
    pub runes: Option<Vec<RuneUpdate>>,
    /// Active shrines (only changed ones).
    #[serde(skip_serializing_if = "Option::is_none")]
    pub shrines: Option<Vec<ShrineUpdate>>,
    /// State hash for verification.
    pub state_hash: [u8; 32],
}

/// Player state in update.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PlayerStateUpdate {
    /// Player identifier.
    pub player_id: [u8; 16],
    /// Current position (Fixed as i32).
    pub position: [i32; 2],
    /// Current velocity (Fixed as i32).
    pub velocity: [i32; 2],
    /// Player form (evolution level).
    pub form: u8,
    /// Current score.
    pub score: u32,
    /// Is player alive.
    pub alive: bool,
    /// Spawn zone ID (-1 if none).
    pub spawn_zone_id: i32,
    /// Spawn zone shield active.
    pub spawn_zone_active: bool,
    /// Player radius (Fixed as i32).
    pub radius: i32,
    /// Ability cooldown remaining.
    pub ability_cooldown: i32,
    /// Active buffs.
    pub buffs: PlayerBuffs,
}

/// Active player buffs.
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct PlayerBuffs {
    /// Speed buff ticks remaining.
    pub speed: u32,
    /// Shield buff ticks remaining.
    pub shield: u32,
    /// Invulnerable ticks remaining.
    pub invulnerable: u32,
    /// Active shrine buffs.
    pub shrine_buffs: Vec<u8>,
}

/// Rune update.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RuneUpdate {
    /// Rune identifier.
    pub id: u32,
    /// Rune type.
    pub rune_type: u8,
    /// Position.
    pub position: [i32; 2],
    /// Whether collected.
    pub collected: bool,
}

/// Shrine update.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ShrineUpdate {
    /// Shrine identifier.
    pub id: u32,
    /// Shrine type.
    pub shrine_type: u8,
    /// Position.
    pub position: [i32; 2],
    /// Active (has power).
    pub active: bool,
    /// Controller player ID (if any).
    pub controller: Option<[u8; 16]>,
}

/// Game events.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "event", rename_all = "snake_case")]
pub enum MatchEvent {
    /// Player collected a rune.
    RuneCollected {
        tick: u32,
        player_id: [u8; 16],
        rune_id: u32,
        rune_type: u8,
        points: u32,
    },

    /// Rune spawned.
    RuneSpawned {
        tick: u32,
        rune_id: u32,
        rune_type: u8,
        position: [i32; 2],
    },

    /// Player evolved to new form.
    PlayerEvolved {
        tick: u32,
        player_id: [u8; 16],
        old_form: u8,
        new_form: u8,
    },

    /// Player eliminated another.
    PlayerEliminated {
        tick: u32,
        victim_id: [u8; 16],
        killer_id: Option<[u8; 16]>,
        victim_form: u8,
    },

    /// Player used ability.
    AbilityUsed {
        tick: u32,
        player_id: [u8; 16],
        ability_type: u8,
    },

    /// Player captured shrine.
    ShrineCaptured {
        tick: u32,
        player_id: [u8; 16],
        shrine_id: u32,
        shrine_type: u8,
    },

    /// Shrine power activated.
    ShrinePowerActivated {
        tick: u32,
        shrine_id: u32,
        shrine_type: u8,
    },

    /// Countdown milestone.
    Countdown {
        seconds: u32,
    },

    /// Match has started (game is now running).
    MatchStarted,
}

/// Match end information.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MatchEndInfo {
    /// Match identifier.
    pub match_id: [u8; 16],
    /// Final tick.
    pub end_tick: u32,
    /// Winner player ID (None if draw).
    pub winner_id: Option<[u8; 16]>,
    /// Final placements: (player_id, place, score).
    pub placements: Vec<PlayerPlacement>,
    /// Final state hash.
    pub final_state_hash: [u8; 32],
    /// Proof transcript (for ranked matches).
    #[serde(skip_serializing_if = "Option::is_none")]
    pub transcript: Option<Vec<u8>>,
}

/// Player placement at match end.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PlayerPlacement {
    /// Player identifier.
    pub player_id: [u8; 16],
    /// Final place (1-based).
    pub place: u8,
    /// Final score.
    pub score: u32,
    /// Eliminations.
    pub eliminations: u32,
    /// Runes collected.
    pub runes_collected: u32,
}

/// Server error.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ServerError {
    /// Error code.
    pub code: ErrorCode,
    /// Human-readable message.
    pub message: String,
}

/// Error codes.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ErrorCode {
    /// Authentication failed.
    AuthFailed,
    /// Not authenticated.
    NotAuthenticated,
    /// JWT token has expired.
    TokenExpired,
    /// Invalid JWT token (signature, format, claims).
    InvalidToken,
    /// Invalid input.
    InvalidInput,
    /// Match not found.
    MatchNotFound,
    /// Already in match.
    AlreadyInMatch,
    /// Not in match.
    NotInMatch,
    /// Rate limited.
    RateLimited,
    /// Server overloaded.
    ServerOverloaded,
    /// Version mismatch.
    VersionMismatch,
    /// Internal error.
    InternalError,
}

// =============================================================================
// SERIALIZATION HELPERS
// =============================================================================

impl ClientMessage {
    /// Serialize to JSON string.
    pub fn to_json(&self) -> Result<String, serde_json::Error> {
        serde_json::to_string(self)
    }

    /// Deserialize from JSON string.
    pub fn from_json(s: &str) -> Result<Self, serde_json::Error> {
        serde_json::from_str(s)
    }

    /// Serialize to binary.
    pub fn to_bytes(&self) -> Result<Vec<u8>, bincode::Error> {
        bincode::serialize(self)
    }

    /// Deserialize from binary.
    pub fn from_bytes(data: &[u8]) -> Result<Self, bincode::Error> {
        bincode::deserialize(data)
    }
}

impl ServerMessage {
    /// Serialize to JSON string.
    pub fn to_json(&self) -> Result<String, serde_json::Error> {
        serde_json::to_string(self)
    }

    /// Deserialize from JSON string.
    pub fn from_json(s: &str) -> Result<Self, serde_json::Error> {
        serde_json::from_str(s)
    }

    /// Serialize to binary.
    pub fn to_bytes(&self) -> Result<Vec<u8>, bincode::Error> {
        bincode::serialize(self)
    }

    /// Deserialize from binary.
    pub fn from_bytes(data: &[u8]) -> Result<Self, bincode::Error> {
        bincode::deserialize(data)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_client_message_json_roundtrip() {
        let msg = ClientMessage::Input(GameInput {
            tick: 100,
            move_x: 50,
            move_y: -30,
            flags: 0x01,
            timestamp: 1234567890,
        });

        let json = msg.to_json().unwrap();
        let parsed = ClientMessage::from_json(&json).unwrap();

        if let ClientMessage::Input(input) = parsed {
            assert_eq!(input.tick, 100);
            assert_eq!(input.move_x, 50);
            assert_eq!(input.move_y, -30);
        } else {
            panic!("Wrong message type");
        }
    }

    #[test]
    fn test_server_message_json_roundtrip() {
        let msg = ServerMessage::Event(MatchEvent::PlayerEvolved {
            tick: 500,
            player_id: [1; 16],
            old_form: 0,
            new_form: 1,
        });

        let json = msg.to_json().unwrap();
        let parsed = ServerMessage::from_json(&json).unwrap();

        if let ServerMessage::Event(MatchEvent::PlayerEvolved { tick, new_form, .. }) = parsed {
            assert_eq!(tick, 500);
            assert_eq!(new_form, 1);
        } else {
            panic!("Wrong message type");
        }
    }

    #[test]
    fn test_binary_serialization_input() {
        // Note: Binary serialization only works reliably for flat structs
        // Tagged enums (#[serde(tag = "type")]) are not supported by bincode
        // Use JSON for ClientMessage/ServerMessage, binary for GameInput
        let input = GameInput {
            tick: 100,
            move_x: 50,
            move_y: -30,
            flags: 0x01,
            timestamp: 123456,
        };

        let bytes = bincode::serialize(&input).unwrap();
        let parsed: GameInput = bincode::deserialize(&bytes).unwrap();

        assert_eq!(parsed.tick, 100);
        assert_eq!(parsed.move_x, 50);
        assert_eq!(parsed.timestamp, 123456);
    }

    #[test]
    fn test_game_input_to_frame() {
        let input = GameInput {
            tick: 1,
            move_x: 127,
            move_y: -127,
            flags: InputFrame::FLAG_ABILITY,
            timestamp: 0,
        };

        let frame = input.to_input_frame();
        assert_eq!(frame.move_x, 127);
        assert_eq!(frame.move_y, -127);
        assert!(frame.ability_pressed());
    }

    #[test]
    fn test_match_modes() {
        let modes = vec![
            MatchMode::Casual,
            MatchMode::Ranked,
            MatchMode::Private,
            MatchMode::Practice,
        ];

        for mode in modes {
            let msg = ClientMessage::Matchmaking(MatchmakingRequest {
                mode,
                match_id: None,
                commitment: None,
            });
            let json = msg.to_json().unwrap();
            let _parsed = ClientMessage::from_json(&json).unwrap();
        }
    }

    #[test]
    fn test_matchmaking_response() {
        let response = MatchmakingResponse {
            status: MatchmakingStatus::Searching,
            estimated_wait: Some(30),
            players_found: 2,
            players_needed: 4,
        };

        let msg = ServerMessage::Matchmaking(response);
        let json = msg.to_json().unwrap();
        assert!(json.contains("searching"));
    }

    #[test]
    fn test_error_codes() {
        let error = ServerError {
            code: ErrorCode::AuthFailed,
            message: "Invalid token".to_string(),
        };

        let msg = ServerMessage::Error(error);
        let json = msg.to_json().unwrap();
        assert!(json.contains("auth_failed"));
    }

    #[test]
    fn test_match_event_variants() {
        let events = vec![
            MatchEvent::RuneCollected {
                tick: 100,
                player_id: [1; 16],
                rune_id: 5,
                rune_type: 0,
                points: 10,
            },
            MatchEvent::RuneSpawned {
                tick: 110,
                rune_id: 6,
                rune_type: 2,
                position: [123, -456],
            },
            MatchEvent::PlayerEvolved {
                tick: 200,
                player_id: [2; 16],
                old_form: 0,
                new_form: 1,
            },
            MatchEvent::PlayerEliminated {
                tick: 300,
                victim_id: [3; 16],
                killer_id: Some([4; 16]),
                victim_form: 2,
            },
            MatchEvent::AbilityUsed {
                tick: 400,
                player_id: [5; 16],
                ability_type: 1,
            },
        ];

        for event in events {
            let msg = ServerMessage::Event(event);
            let json = msg.to_json().unwrap();
            let _ = ServerMessage::from_json(&json).unwrap();
        }
    }
}

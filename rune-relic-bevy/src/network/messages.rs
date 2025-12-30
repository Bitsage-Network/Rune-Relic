//! Protocol messages for client-server communication

use serde::{Deserialize, Serialize};

// =============================================================================
// CLIENT -> SERVER
// =============================================================================

#[derive(Debug, Clone, Serialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum ClientMessage {
    Auth {
        player_id: String,
        token: String,
        client_version: String,
    },
    Matchmaking {
        mode: String,
        match_id: Option<[u8; 16]>,
        commitment: Option<[u8; 32]>,
    },
    CancelMatchmaking,
    Ready,
    Input {
        tick: u32,
        move_x: i8,
        move_y: i8,
        flags: u8,
        timestamp: u64,
    },
    Ping {
        timestamp: u64,
    },
    Leave,
}

impl ClientMessage {
    pub fn auth(player_id: [u8; 16]) -> Self {
        Self::Auth {
            player_id: hex::encode(player_id),
            token: String::new(),
            client_version: "0.1.0".to_string(),
        }
    }

    pub fn matchmaking_casual() -> Self {
        Self::Matchmaking {
            mode: "casual".to_string(),
            match_id: None,
            commitment: None,
        }
    }

    pub fn input(tick: u32, move_x: f32, move_y: f32, ability: bool) -> Self {
        Self::Input {
            tick,
            move_x: (move_x * 127.0) as i8,
            move_y: (move_y * 127.0) as i8,
            flags: if ability { 1 } else { 0 },
            timestamp: std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap_or_default()
                .as_millis() as u64,
        }
    }

    pub fn ping() -> Self {
        Self::Ping {
            timestamp: std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap_or_default()
                .as_millis() as u64,
        }
    }
}

// =============================================================================
// SERVER -> CLIENT
// =============================================================================

#[derive(Debug, Clone, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum ServerMessage {
    AuthResult {
        success: bool,
        session_id: Option<String>,
        error: Option<String>,
        server_version: String,
    },
    Matchmaking {
        status: String,
        estimated_wait: Option<u32>,
        players_found: u32,
        players_needed: u32,
    },
    MatchFound {
        match_id: [u8; 16],
        player_ids: Vec<[u8; 16]>,
        mode: String,
        ready_timeout: u32,
    },
    MatchStart {
        match_id: [u8; 16],
        rng_seed: u64,
        start_tick: u32,
        players: Vec<PlayerInfo>,
        block_hash: [u8; 32],
    },
    State {
        tick: u32,
        time_remaining: u32,
        phase: String,
        arena_radius: f32,
        players: Vec<PlayerState>,
        runes: Option<Vec<RuneState>>,
        shrines: Option<Vec<ShrineState>>,
    },
    Event(GameEvent),
    MatchEnd {
        winner_id: Option<[u8; 16]>,
        placements: Vec<Placement>,
        duration_ticks: u32,
    },
    InputAck {
        tick: u32,
        server_tick: u32,
    },
    Pong {
        timestamp: u64,
        server_time: u64,
    },
    Error {
        code: String,
        message: String,
    },
    Shutdown {
        reason: String,
    },
}

#[derive(Debug, Clone, Deserialize)]
pub struct PlayerInfo {
    pub player_id: [u8; 16],
    pub position: [i32; 2],
    pub color_index: u8,
}

#[derive(Debug, Clone, Deserialize)]
pub struct PlayerState {
    pub id: [u8; 16],
    pub position: [i32; 2],
    pub velocity: [i32; 2],
    pub form: u8,
    pub score: u32,
    pub alive: bool,
    pub spawn_zone_id: i32,
    pub spawn_zone_active: bool,
    pub buffs: u8,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RuneState {
    pub id: u32,
    pub rune_type: u8,
    pub position: [i32; 2],
    pub collected: bool,
}

#[derive(Debug, Clone, Deserialize)]
pub struct ShrineState {
    pub id: u32,
    pub shrine_type: u8,
    pub position: [i32; 2],
    pub active: bool,
    pub controller: Option<[u8; 16]>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Placement {
    pub player_id: [u8; 16],
    pub place: u8,
    pub score: u32,
    pub eliminations: u32,
}

#[derive(Debug, Clone, Deserialize)]
#[serde(tag = "event", rename_all = "snake_case")]
pub enum GameEvent {
    Countdown { seconds: u32 },
    MatchStarted,
    RuneSpawned {
        tick: u32,
        rune_id: u32,
        rune_type: u8,
        position: [i32; 2],
    },
    RuneCollected {
        tick: u32,
        player_id: [u8; 16],
        rune_id: u32,
        rune_type: u8,
        points: u32,
    },
    PlayerEvolved {
        tick: u32,
        player_id: [u8; 16],
        old_form: u8,
        new_form: u8,
    },
    PlayerEliminated {
        tick: u32,
        victim_id: [u8; 16],
        killer_id: Option<[u8; 16]>,
        victim_form: u8,
    },
    ShrineCaptured {
        tick: u32,
        player_id: [u8; 16],
        shrine_id: u32,
        shrine_type: u8,
    },
}

// =============================================================================
// CONSTANTS
// =============================================================================

/// Fixed-point scale factor (Q16.16)
pub const FIXED_SCALE: f32 = 65536.0;

/// Convert fixed-point to float
pub fn fixed_to_float(fixed: i32) -> f32 {
    fixed as f32 / FIXED_SCALE
}

/// Convert float to fixed-point
pub fn float_to_fixed(float: f32) -> i32 {
    (float * FIXED_SCALE) as i32
}

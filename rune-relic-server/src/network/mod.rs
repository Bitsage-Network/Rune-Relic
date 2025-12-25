//! Network Layer
//!
//! WebSocket server for real-time multiplayer communication.
//! This layer is **non-deterministic** - all game logic runs through `game/`.

pub mod protocol;
pub mod session;
pub mod server;

pub use protocol::{
    ClientMessage, ServerMessage, MatchmakingRequest, MatchmakingResponse,
    GameInput, GameStateUpdate, MatchEvent,
};
pub use session::{MatchSession, SessionId, SessionState, SessionManager};
pub use server::{GameServer, ServerConfig, GameServerError};

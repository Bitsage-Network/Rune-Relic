//! Game Logic Module
//!
//! All game simulation code. 100% deterministic.
//!
//! ## Module Structure
//!
//! - `input`: Input capture, normalization, buffering
//! - `state`: Match state, player state, entities
//! - `tick`: Authoritative simulation loop
//! - `collision`: Collision detection and resolution
//! - `rune`: Rune spawning and collection
//! - `shrine`: Shrine activation mechanics
//! - `events`: Game events for replay/verification

pub mod input;
pub mod state;
pub mod tick;
pub mod collision;
pub mod map;
pub mod rune;
pub mod shrine;
pub mod ability;
pub mod events;

// Re-export key types
pub use input::{InputFrame, InputDelta, PlayerInputBuffer, MOVE_LUT};
pub use state::{MatchState, PlayerState, PlayerId, Form, MatchPhase};
pub use tick::TickResult;
pub use events::GameEvent;

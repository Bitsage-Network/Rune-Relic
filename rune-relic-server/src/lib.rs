//! # Rune Relic Game Server
//!
//! Deterministic game simulation for Rune Relic, designed for BitSage proof generation.
//!
//! ## Architecture
//!
//! ```text
//! ┌─────────────────────────────────────────────────────────────┐
//! │                    RUNE RELIC SERVER                         │
//! ├─────────────────────────────────────────────────────────────┤
//! │  core/           - Deterministic primitives                  │
//! │  ├── fixed.rs    - Q16.16 fixed-point arithmetic             │
//! │  ├── vec2.rs     - 2D vector with fixed-point                │
//! │  ├── rng.rs      - Deterministic Xorshift128+ PRNG           │
//! │  └── hash.rs     - State hashing for verification            │
//! │                                                              │
//! │  game/           - Game logic (deterministic)                │
//! │  ├── input.rs    - Input capture and normalization           │
//! │  ├── state.rs    - Match and player state                    │
//! │  ├── tick.rs     - Authoritative simulation loop             │
//! │  ├── collision.rs- Collision detection                       │
//! │  ├── rune.rs     - Rune spawning and collection              │
//! │  └── shrine.rs   - Shrine mechanics                          │
//! │                                                              │
//! │  network/        - Networking (non-deterministic)            │
//! │  ├── server.rs   - WebSocket server                          │
//! │  ├── protocol.rs - Message types                             │
//! │  └── session.rs  - Match session management                  │
//! └─────────────────────────────────────────────────────────────┘
//! ```
//!
//! ## Determinism Guarantee
//!
//! The `core/` and `game/` modules are **100% deterministic**:
//! - No floating-point arithmetic in game logic
//! - No HashMap (uses BTreeMap for sorted iteration)
//! - No system time dependencies
//! - All randomness from seeded Xorshift128+
//!
//! Given identical inputs and RNG seed, the simulation produces
//! **identical results** on any platform (x86, ARM, WASM, GPU).

#![warn(missing_docs)]
#![warn(clippy::all)]
#![deny(unsafe_code)]

pub mod core;
pub mod game;
pub mod network;
pub mod proof;

// Re-export commonly used types
pub use core::fixed::{Fixed, FIXED_ONE, FIXED_HALF, FIXED_SCALE};
pub use core::vec2::FixedVec2;
pub use core::rng::DeterministicRng;
pub use game::input::{InputFrame, InputDelta, PlayerInputBuffer};
pub use game::state::{MatchState, PlayerState, PlayerId};

/// Crate version
pub const VERSION: &str = env!("CARGO_PKG_VERSION");

/// Simulation tick rate (Hz)
pub const TICK_RATE: u32 = 60;

/// Match duration in ticks (90 seconds * 60 Hz)
pub const MATCH_DURATION_TICKS: u32 = 5400;

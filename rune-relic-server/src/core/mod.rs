//! Core deterministic primitives.
//!
//! All types in this module are designed for perfect cross-platform determinism.
//! They form the foundation for BitSage proof compatibility.

pub mod fixed;
pub mod vec2;
pub mod rng;
pub mod hash;

// Re-export core types
pub use fixed::{Fixed, FIXED_ONE, FIXED_HALF, FIXED_SCALE};
pub use vec2::FixedVec2;
pub use rng::DeterministicRng;
pub use hash::compute_state_hash;

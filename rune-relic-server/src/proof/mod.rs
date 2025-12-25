//! BitSage Proof System
//!
//! Provides verifiable match outcomes through:
//! - Match transcript recording
//! - Merkle tree commitments
//! - STWO-compatible public inputs (M31 field)
//! - Verification by deterministic replay
//!
//! ## Architecture
//!
//! ```text
//! ┌─────────────────────────────────────────────────────────────┐
//! │                    PROOF SYSTEM                             │
//! ├─────────────────────────────────────────────────────────────┤
//! │  transcript.rs   - Match transcript recording (~20KB/match) │
//! │  merkle.rs       - Binary Merkle tree for commitments       │
//! │  public_inputs.rs- M31 field encoding for STWO proofs       │
//! │  commitment.rs   - Pre-match commitment protocol            │
//! │  verify.rs       - Verification by replay                   │
//! └─────────────────────────────────────────────────────────────┘
//! ```

pub mod merkle;
pub mod transcript;
pub mod public_inputs;
pub mod commitment;
pub mod verify;

// Re-export key types
pub use merkle::{MerkleTree, MerkleProof};
pub use transcript::{
    MatchTranscript, MatchMetadata, MatchResult,
    InitialMatchState, InitialPlayerState,
    PlayerInputRecord, StateCheckpoint, TranscriptEvent,
};
pub use public_inputs::{ProofPublicInputs, M31};
pub use commitment::{MatchCommitment, CommitmentPreimage, MatchReveal, CommitmentError};
pub use verify::{
    verify_transcript, VerificationResult, VerificationError,
    CheckpointResult, ProofVerifier, ProofVerificationError,
};

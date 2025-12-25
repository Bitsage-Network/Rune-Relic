//! Verification API
//!
//! Verify matches by deterministic replay.
//! Interface for external STWO proof verification (BitSage/Obelysk).

use std::collections::BTreeMap;
use crate::core::hash::StateHash;
use crate::game::state::{MatchState, MatchPhase, PlayerId, PlayerState, Form};
use crate::game::input::InputFrame;
use crate::game::tick::{tick, MatchConfig};
use crate::proof::transcript::MatchTranscript;
use crate::proof::public_inputs::ProofPublicInputs;

/// Verification result.
#[derive(Debug)]
pub struct VerificationResult {
    /// Did verification pass?
    pub valid: bool,

    /// Final state hash (from replay).
    pub computed_final_hash: StateHash,

    /// Expected final hash (from transcript).
    pub expected_final_hash: StateHash,

    /// Checkpoint verification results.
    pub checkpoint_results: Vec<CheckpointResult>,

    /// Detailed error if verification failed.
    pub error: Option<VerificationError>,
}

/// Result of verifying a single checkpoint.
#[derive(Debug)]
pub struct CheckpointResult {
    /// Tick number.
    pub tick: u32,
    /// Expected hash from transcript.
    pub expected: StateHash,
    /// Computed hash from replay.
    pub computed: StateHash,
    /// Did this checkpoint match?
    pub valid: bool,
}

/// Errors that can occur during verification.
#[derive(Debug, Clone)]
pub enum VerificationError {
    /// Transcript version mismatch.
    VersionMismatch {
        /// Expected version.
        expected: u8,
        /// Actual version.
        got: u8,
    },

    /// Initial state hash mismatch.
    InitialStateMismatch {
        /// Expected hash.
        expected: StateHash,
        /// Computed hash.
        computed: StateHash,
    },

    /// Checkpoint hash mismatch.
    CheckpointMismatch {
        /// Tick where mismatch occurred.
        tick: u32,
        /// Expected hash.
        expected: StateHash,
        /// Computed hash.
        computed: StateHash,
    },

    /// Final state hash mismatch.
    FinalStateMismatch {
        /// Expected hash.
        expected: StateHash,
        /// Computed hash.
        computed: StateHash,
    },

    /// Player input buffer corrupted.
    InvalidInputBuffer {
        /// Player with invalid inputs.
        player_id: [u8; 16],
    },

    /// Match result mismatch.
    ResultMismatch,

    /// RNG seed derivation mismatch.
    SeedMismatch {
        /// Expected seed.
        expected: u64,
        /// Actual seed.
        got: u64,
    },

    /// Transcript is incomplete.
    IncompleteTranscript,
}

impl std::fmt::Display for VerificationError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::VersionMismatch { expected, got } => {
                write!(f, "Version mismatch: expected {}, got {}", expected, got)
            }
            Self::InitialStateMismatch { .. } => {
                write!(f, "Initial state hash mismatch")
            }
            Self::CheckpointMismatch { tick, .. } => {
                write!(f, "Checkpoint mismatch at tick {}", tick)
            }
            Self::FinalStateMismatch { .. } => {
                write!(f, "Final state hash mismatch")
            }
            Self::InvalidInputBuffer { player_id } => {
                write!(f, "Invalid input buffer for player {:02x?}", &player_id[..4])
            }
            Self::ResultMismatch => write!(f, "Match result mismatch"),
            Self::SeedMismatch { expected, got } => {
                write!(f, "RNG seed mismatch: expected {}, got {}", expected, got)
            }
            Self::IncompleteTranscript => write!(f, "Transcript is incomplete"),
        }
    }
}

impl std::error::Error for VerificationError {}

/// Verify a match transcript by full replay.
///
/// This is the authoritative verification method.
/// Replays the entire match and compares state hashes.
pub fn verify_transcript(transcript: &MatchTranscript) -> VerificationResult {
    // Check transcript is complete
    let result = match &transcript.result {
        Some(r) => r,
        None => {
            return VerificationResult {
                valid: false,
                computed_final_hash: [0; 32],
                expected_final_hash: [0; 32],
                checkpoint_results: vec![],
                error: Some(VerificationError::IncompleteTranscript),
            };
        }
    };

    // 1. Reconstruct initial state
    let mut state = reconstruct_initial_state(transcript);

    // 2. Verify initial state hash
    let initial_hash = state.compute_hash();
    if initial_hash != transcript.initial_state.state_hash {
        return VerificationResult {
            valid: false,
            computed_final_hash: initial_hash,
            expected_final_hash: transcript.initial_state.state_hash,
            checkpoint_results: vec![],
            error: Some(VerificationError::InitialStateMismatch {
                expected: transcript.initial_state.state_hash,
                computed: initial_hash,
            }),
        };
    }

    // 3. Build input lookup from transcript
    let player_inputs = build_input_lookup(transcript);

    // 4. Replay tick by tick with checkpoint verification
    let config = MatchConfig::default();
    let mut checkpoint_results = Vec::new();
    let mut checkpoint_idx = 0;

    // Start playing
    state.phase = MatchPhase::Playing;

    for tick_num in 1..=result.end_tick {
        // Get inputs for this tick
        let tick_inputs = get_inputs_at_tick(&player_inputs, tick_num);

        // Run tick
        let _tick_result = tick(&mut state, &tick_inputs, &config);

        // Check if we hit a checkpoint
        if checkpoint_idx < transcript.checkpoints.len()
            && transcript.checkpoints[checkpoint_idx].tick == state.tick
        {
            let checkpoint = &transcript.checkpoints[checkpoint_idx];
            let computed = state.compute_hash();
            let valid = computed == checkpoint.state_hash;

            checkpoint_results.push(CheckpointResult {
                tick: checkpoint.tick,
                expected: checkpoint.state_hash,
                computed,
                valid,
            });

            if !valid {
                return VerificationResult {
                    valid: false,
                    computed_final_hash: computed,
                    expected_final_hash: checkpoint.state_hash,
                    checkpoint_results,
                    error: Some(VerificationError::CheckpointMismatch {
                        tick: checkpoint.tick,
                        expected: checkpoint.state_hash,
                        computed,
                    }),
                };
            }

            checkpoint_idx += 1;
        }
    }

    // 5. Verify final state
    let final_hash = state.compute_hash();
    let valid = final_hash == result.final_state_hash;

    VerificationResult {
        valid,
        computed_final_hash: final_hash,
        expected_final_hash: result.final_state_hash,
        checkpoint_results,
        error: if valid {
            None
        } else {
            Some(VerificationError::FinalStateMismatch {
                expected: result.final_state_hash,
                computed: final_hash,
            })
        },
    }
}

/// Export public inputs for external prover (BitSage/STWO).
pub fn export_public_inputs(transcript: &MatchTranscript) -> ProofPublicInputs {
    ProofPublicInputs::from_transcript(transcript)
}

/// Interface for STWO proof verification.
///
/// This is called by on-chain or off-chain verifiers.
/// Implementations will connect to BitSage/Obelysk STWO GPU prover.
pub trait ProofVerifier {
    /// Verify a STWO proof against public inputs.
    fn verify_proof(
        &self,
        public_inputs: &ProofPublicInputs,
        proof: &[u8],
    ) -> Result<bool, ProofVerificationError>;
}

/// Errors during proof verification.
#[derive(Debug)]
pub enum ProofVerificationError {
    /// Proof format is invalid.
    InvalidProofFormat,
    /// Verification computation failed.
    VerificationFailed,
    /// Public inputs don't match proof.
    PublicInputMismatch,
    /// Prover connection failed.
    ProverConnectionFailed(String),
}

impl std::fmt::Display for ProofVerificationError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::InvalidProofFormat => write!(f, "Invalid proof format"),
            Self::VerificationFailed => write!(f, "Verification failed"),
            Self::PublicInputMismatch => write!(f, "Public input mismatch"),
            Self::ProverConnectionFailed(msg) => write!(f, "Prover connection failed: {}", msg),
        }
    }
}

impl std::error::Error for ProofVerificationError {}

/// Stub verifier for testing (always returns true for valid format).
///
/// Replace with real STWO/BitSage verifier when available.
pub struct StubProofVerifier;

impl ProofVerifier for StubProofVerifier {
    fn verify_proof(
        &self,
        _public_inputs: &ProofPublicInputs,
        proof: &[u8],
    ) -> Result<bool, ProofVerificationError> {
        // In real implementation, this would call STWO verifier
        if proof.is_empty() {
            return Err(ProofVerificationError::InvalidProofFormat);
        }

        // Stub: accept any non-empty proof
        Ok(true)
    }
}

/// Placeholder for BitSage/STWO verifier.
///
/// TODO: Implement when STWO GPU prover is integrated.
#[cfg(feature = "bitsage")]
pub struct BitSageProofVerifier {
    // Connection to BitSage node
    // endpoint: String,
}

#[cfg(feature = "bitsage")]
impl ProofVerifier for BitSageProofVerifier {
    fn verify_proof(
        &self,
        _public_inputs: &ProofPublicInputs,
        _proof: &[u8],
    ) -> Result<bool, ProofVerificationError> {
        // TODO: Implement actual STWO verification via BitSage
        Err(ProofVerificationError::ProverConnectionFailed(
            "BitSage integration not yet implemented".to_string()
        ))
    }
}

// =============================================================================
// Helper functions
// =============================================================================

/// Reconstruct initial match state from transcript.
fn reconstruct_initial_state(transcript: &MatchTranscript) -> MatchState {
    let mut state = MatchState::new(
        transcript.metadata.match_id,
        transcript.metadata.rng_seed,
    );

    // Set RNG state from initial snapshot
    state.rng.set_state(transcript.initial_state.rng_state);

    // Add players at their initial positions
    for player in &transcript.initial_state.players {
        let player_id = PlayerId::new(player.player_id);
        let mut player_state = PlayerState::new(player_id, player.position);
        player_state.form = Form::from_index(player.form).unwrap_or_default();
        state.players.insert(player_id, player_state);
        state.alive_count += 1;
    }

    state
}

/// Build input lookup from transcript records.
fn build_input_lookup(transcript: &MatchTranscript) -> BTreeMap<PlayerId, Vec<(u32, InputFrame)>> {
    let mut lookup = BTreeMap::new();

    for record in &transcript.player_inputs {
        let player_id = PlayerId::new(record.player_id);
        let mut frames: Vec<(u32, InputFrame)> = Vec::new();

        for delta in &record.deltas {
            frames.push((delta.tick, delta.frame));
        }

        lookup.insert(player_id, frames);
    }

    lookup
}

/// Get inputs for all players at a specific tick.
fn get_inputs_at_tick(
    lookup: &BTreeMap<PlayerId, Vec<(u32, InputFrame)>>,
    tick: u32,
) -> BTreeMap<PlayerId, InputFrame> {
    let mut inputs = BTreeMap::new();

    for (player_id, frames) in lookup {
        // Find the most recent input at or before this tick
        let frame = frames.iter()
            .rev()
            .find(|(t, _)| *t <= tick)
            .map(|(_, f)| *f)
            .unwrap_or_else(InputFrame::new);

        inputs.insert(*player_id, frame);
    }

    inputs
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::core::vec2::FixedVec2;
    use crate::proof::transcript::{
        MatchMetadata, MatchResult, InitialMatchState, InitialPlayerState, PlayerInputRecord,
    };
    use crate::game::input::InputDelta;

    fn create_minimal_transcript() -> MatchTranscript {
        // Create a minimal valid transcript for testing
        let metadata = MatchMetadata {
            match_id: [1; 16],
            block_hash: [2; 32],
            player_ids: vec![[3; 16]],
            rng_seed: 12345,
            start_timestamp: 1700000000,
            config_hash: [4; 32],
        };

        let mut transcript = MatchTranscript::new(metadata);

        // Set initial state
        transcript.initial_state = InitialMatchState {
            players: vec![InitialPlayerState {
                player_id: [3; 16],
                position: FixedVec2::ZERO,
                form: 0,
            }],
            rng_state: [100, 200],
            state_hash: [0; 32], // Will need to compute
        };

        // Add player inputs (idle)
        transcript.player_inputs.push(PlayerInputRecord {
            player_id: [3; 16],
            deltas: vec![InputDelta::new(0, InputFrame::new())],
            input_count: 1,
        });

        transcript
    }

    #[test]
    fn test_incomplete_transcript_fails() {
        let transcript = create_minimal_transcript();
        // Don't finalize - should fail

        let result = verify_transcript(&transcript);
        assert!(!result.valid);
        assert!(matches!(result.error, Some(VerificationError::IncompleteTranscript)));
    }

    #[test]
    fn test_export_public_inputs() {
        let mut transcript = create_minimal_transcript();
        transcript.finalize(MatchResult {
            end_tick: 100,
            winner_id: Some([3; 16]),
            placements: vec![([3; 16], 1, 100)],
            final_state_hash: [5; 32],
        });

        let inputs = export_public_inputs(&transcript);

        assert_eq!(inputs.player_count, 1);
        assert_eq!(inputs.duration_ticks, 100);
    }

    #[test]
    fn test_stub_verifier() {
        let verifier = StubProofVerifier;

        // Empty proof should fail
        let inputs = ProofPublicInputs::default();
        assert!(verifier.verify_proof(&inputs, &[]).is_err());

        // Non-empty proof should pass (stub behavior)
        assert!(verifier.verify_proof(&inputs, &[1, 2, 3]).unwrap());
    }

    #[test]
    fn test_input_lookup() {
        let mut transcript = create_minimal_transcript();

        transcript.player_inputs[0].deltas = vec![
            InputDelta::new(0, InputFrame::with_movement(10, 0)),
            InputDelta::new(50, InputFrame::with_movement(20, 0)),
            InputDelta::new(100, InputFrame::with_movement(30, 0)),
        ];

        let lookup = build_input_lookup(&transcript);
        let player_id = PlayerId::new([3; 16]);

        // Check inputs at various ticks
        let inputs_0 = get_inputs_at_tick(&lookup, 0);
        assert_eq!(inputs_0.get(&player_id).unwrap().move_x, 10);

        let inputs_25 = get_inputs_at_tick(&lookup, 25);
        assert_eq!(inputs_25.get(&player_id).unwrap().move_x, 10);

        let inputs_50 = get_inputs_at_tick(&lookup, 50);
        assert_eq!(inputs_50.get(&player_id).unwrap().move_x, 20);

        let inputs_75 = get_inputs_at_tick(&lookup, 75);
        assert_eq!(inputs_75.get(&player_id).unwrap().move_x, 20);

        let inputs_100 = get_inputs_at_tick(&lookup, 100);
        assert_eq!(inputs_100.get(&player_id).unwrap().move_x, 30);
    }
}

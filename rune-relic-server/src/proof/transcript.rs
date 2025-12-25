//! Match Transcript Recording
//!
//! Records all data needed to deterministically verify a match outcome.
//! Optimized for compact serialization (~20KB for a 90-second match).

use serde::{Serialize, Deserialize};
use crate::core::hash::StateHash;
use crate::core::vec2::FixedVec2;
use crate::game::input::InputDelta;
use crate::game::events::{GameEvent, GameEventData};

/// Current transcript version.
pub const TRANSCRIPT_VERSION: u8 = 1;

/// Checkpoint interval in ticks (every 10 seconds = 600 ticks at 60Hz).
pub const CHECKPOINT_INTERVAL: u32 = 600;

/// Complete match transcript for proof generation.
///
/// Contains all data needed to:
/// 1. Deterministically replay the match
/// 2. Generate STWO proof public inputs
/// 3. Verify match outcome on-chain
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct MatchTranscript {
    /// Version for forward compatibility.
    pub version: u8,

    /// Match metadata (public, can be committed before match starts).
    pub metadata: MatchMetadata,

    /// Initial match state (snapshot at tick 0).
    pub initial_state: InitialMatchState,

    /// All player input recordings (delta-compressed).
    pub player_inputs: Vec<PlayerInputRecord>,

    /// State hash checkpoints (every N ticks for partial verification).
    pub checkpoints: Vec<StateCheckpoint>,

    /// Final match result.
    pub result: Option<MatchResult>,

    /// Significant events (eliminations, evolutions, etc.).
    pub events: Vec<TranscriptEvent>,
}

/// Match metadata (public, can be committed before match starts).
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct MatchMetadata {
    /// Unique match identifier (UUID bytes).
    pub match_id: [u8; 16],

    /// Block hash used for RNG seed derivation.
    pub block_hash: [u8; 32],

    /// Sorted player IDs (deterministic ordering).
    pub player_ids: Vec<[u8; 16]>,

    /// RNG seed derived from block_hash + match_id + player_ids.
    pub rng_seed: u64,

    /// Unix timestamp when match started.
    pub start_timestamp: u64,

    /// Match configuration hash (for versioning rules).
    pub config_hash: StateHash,
}

/// Initial state snapshot at tick 0.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct InitialMatchState {
    /// Initial player states (positions, forms).
    pub players: Vec<InitialPlayerState>,

    /// Initial RNG state (after player spawning).
    pub rng_state: [u64; 2],

    /// Hash of initial state.
    pub state_hash: StateHash,
}

/// Minimal player state at match start.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct InitialPlayerState {
    /// Player identifier.
    pub player_id: [u8; 16],

    /// Initial position.
    pub position: FixedVec2,

    /// Initial form (as u8).
    pub form: u8,
}

/// Per-player input recording.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct PlayerInputRecord {
    /// Player identifier.
    pub player_id: [u8; 16],

    /// Delta-compressed inputs (only when input changes).
    pub deltas: Vec<InputDelta>,

    /// Total input count for validation.
    pub input_count: u32,
}

/// State checkpoint for partial verification.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct StateCheckpoint {
    /// Tick number.
    pub tick: u32,

    /// State hash at this tick.
    pub state_hash: StateHash,

    /// RNG state at this tick (for resumable verification).
    pub rng_state: [u64; 2],
}

/// Final match outcome.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct MatchResult {
    /// Final tick when match ended.
    pub end_tick: u32,

    /// Winner player ID (None if draw/timeout).
    pub winner_id: Option<[u8; 16]>,

    /// Final placements: (player_id, placement, score).
    pub placements: Vec<([u8; 16], u8, u32)>,

    /// Final state hash.
    pub final_state_hash: StateHash,
}

/// Transcript event (subset of GameEvent for compact storage).
#[derive(Clone, Debug, Serialize, Deserialize)]
pub enum TranscriptEvent {
    /// Player was eliminated.
    PlayerEliminated {
        /// Tick when elimination occurred.
        tick: u32,
        /// Eliminated player ID.
        victim_id: [u8; 16],
        /// Player who caused elimination (if any).
        killer_id: Option<[u8; 16]>,
        /// Final placement.
        placement: u8,
    },

    /// Player evolved to new form.
    FormEvolved {
        /// Tick when evolution occurred.
        tick: u32,
        /// Player who evolved.
        player_id: [u8; 16],
        /// New form (as u8).
        new_form: u8,
    },

    /// Player collected a rune.
    RuneCollected {
        /// Tick when collection occurred.
        tick: u32,
        /// Player who collected.
        player_id: [u8; 16],
        /// Rune identifier.
        rune_id: u32,
        /// Points gained.
        points: u32,
    },

    /// Shrine activated.
    ShrineActivated {
        /// Tick when activation occurred.
        tick: u32,
        /// Player who activated.
        player_id: [u8; 16],
        /// Shrine identifier.
        shrine_id: u8,
    },
}

impl MatchTranscript {
    /// Create a new transcript from match metadata.
    pub fn new(metadata: MatchMetadata) -> Self {
        Self {
            version: TRANSCRIPT_VERSION,
            metadata,
            initial_state: InitialMatchState {
                players: Vec::new(),
                rng_state: [0, 0],
                state_hash: [0; 32],
            },
            player_inputs: Vec::new(),
            checkpoints: Vec::new(),
            result: None,
            events: Vec::new(),
        }
    }

    /// Record initial state snapshot.
    pub fn set_initial_state(&mut self, state: InitialMatchState) {
        self.initial_state = state;
    }

    /// Add player input record.
    pub fn add_player_inputs(&mut self, record: PlayerInputRecord) {
        self.player_inputs.push(record);
    }

    /// Record a state checkpoint.
    pub fn add_checkpoint(&mut self, tick: u32, state_hash: StateHash, rng_state: [u64; 2]) {
        self.checkpoints.push(StateCheckpoint {
            tick,
            state_hash,
            rng_state,
        });
    }

    /// Record a game event.
    ///
    /// Only significant events are recorded (eliminations, evolutions, etc.).
    pub fn record_event(&mut self, event: &GameEvent) {
        if let Some(transcript_event) = TranscriptEvent::from_game_event(event) {
            self.events.push(transcript_event);
        }
    }

    /// Finalize the transcript with match result.
    pub fn finalize(&mut self, result: MatchResult) {
        self.result = Some(result);
    }

    /// Check if transcript is complete.
    pub fn is_complete(&self) -> bool {
        self.result.is_some()
    }

    /// Serialize to bytes using bincode.
    pub fn to_bytes(&self) -> Vec<u8> {
        bincode::serialize(self).expect("Transcript serialization should not fail")
    }

    /// Deserialize from bytes.
    pub fn from_bytes(data: &[u8]) -> Result<Self, TranscriptError> {
        bincode::deserialize(data).map_err(|e| TranscriptError::DeserializationFailed(e.to_string()))
    }

    /// Estimated size in bytes.
    pub fn estimated_size(&self) -> usize {
        // Rough estimate
        let base = 1 + 16 + 32 + (self.metadata.player_ids.len() * 16) + 8 + 8 + 32;
        let initial = self.initial_state.players.len() * 25 + 16 + 32;
        let inputs: usize = self.player_inputs.iter()
            .map(|r| 16 + r.deltas.len() * 8 + 4)
            .sum();
        let checkpoints = self.checkpoints.len() * 52;
        let events = self.events.len() * 40;
        let result = if self.result.is_some() { 100 } else { 0 };

        base + initial + inputs + checkpoints + events + result
    }

    /// Get player count.
    pub fn player_count(&self) -> usize {
        self.metadata.player_ids.len()
    }
}

impl TranscriptEvent {
    /// Convert a GameEvent to TranscriptEvent (if relevant).
    pub fn from_game_event(event: &GameEvent) -> Option<Self> {
        match &event.data {
            GameEventData::PlayerEliminated { victim_id, killer_id, placement } => {
                Some(TranscriptEvent::PlayerEliminated {
                    tick: event.tick,
                    victim_id: *victim_id.as_bytes(),
                    killer_id: killer_id.map(|k| *k.as_bytes()),
                    placement: *placement,
                })
            }
            GameEventData::FormEvolved { player_id, new_form, .. } => {
                Some(TranscriptEvent::FormEvolved {
                    tick: event.tick,
                    player_id: *player_id.as_bytes(),
                    new_form: *new_form as u8,
                })
            }
            GameEventData::RuneCollected { player_id, rune_id, points, .. } => {
                Some(TranscriptEvent::RuneCollected {
                    tick: event.tick,
                    player_id: *player_id.as_bytes(),
                    rune_id: *rune_id,
                    points: *points,
                })
            }
            GameEventData::ShrineActivated { player_id, shrine_id } => {
                Some(TranscriptEvent::ShrineActivated {
                    tick: event.tick,
                    player_id: *player_id.as_bytes(),
                    shrine_id: *shrine_id,
                })
            }
            // Other events are not recorded in transcript
            _ => None,
        }
    }
}

/// Errors that can occur with transcripts.
#[derive(Debug)]
pub enum TranscriptError {
    /// Deserialization failed.
    DeserializationFailed(String),
    /// Version mismatch.
    VersionMismatch { expected: u8, got: u8 },
    /// Incomplete transcript.
    Incomplete,
}

impl std::fmt::Display for TranscriptError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::DeserializationFailed(msg) => write!(f, "Deserialization failed: {}", msg),
            Self::VersionMismatch { expected, got } => {
                write!(f, "Version mismatch: expected {}, got {}", expected, got)
            }
            Self::Incomplete => write!(f, "Transcript is incomplete"),
        }
    }
}

impl std::error::Error for TranscriptError {}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::game::state::PlayerId;

    fn create_test_metadata() -> MatchMetadata {
        MatchMetadata {
            match_id: [1; 16],
            block_hash: [2; 32],
            player_ids: vec![[3; 16], [4; 16]],
            rng_seed: 12345,
            start_timestamp: 1700000000,
            config_hash: [5; 32],
        }
    }

    #[test]
    fn test_transcript_creation() {
        let metadata = create_test_metadata();
        let transcript = MatchTranscript::new(metadata.clone());

        assert_eq!(transcript.version, TRANSCRIPT_VERSION);
        assert_eq!(transcript.metadata.match_id, metadata.match_id);
        assert!(!transcript.is_complete());
    }

    #[test]
    fn test_transcript_serialization_roundtrip() {
        let metadata = create_test_metadata();
        let mut transcript = MatchTranscript::new(metadata);

        // Add some data
        transcript.add_checkpoint(600, [6; 32], [100, 200]);
        transcript.events.push(TranscriptEvent::FormEvolved {
            tick: 300,
            player_id: [3; 16],
            new_form: 2,
        });

        // Serialize and deserialize
        let bytes = transcript.to_bytes();
        let decoded = MatchTranscript::from_bytes(&bytes).unwrap();

        assert_eq!(decoded.version, transcript.version);
        assert_eq!(decoded.checkpoints.len(), 1);
        assert_eq!(decoded.events.len(), 1);
    }

    #[test]
    fn test_checkpoint_recording() {
        let metadata = create_test_metadata();
        let mut transcript = MatchTranscript::new(metadata);

        transcript.add_checkpoint(600, [1; 32], [10, 20]);
        transcript.add_checkpoint(1200, [2; 32], [30, 40]);
        transcript.add_checkpoint(1800, [3; 32], [50, 60]);

        assert_eq!(transcript.checkpoints.len(), 3);
        assert_eq!(transcript.checkpoints[0].tick, 600);
        assert_eq!(transcript.checkpoints[2].tick, 1800);
    }

    #[test]
    fn test_finalize() {
        let metadata = create_test_metadata();
        let mut transcript = MatchTranscript::new(metadata);

        assert!(!transcript.is_complete());

        transcript.finalize(MatchResult {
            end_tick: 5400,
            winner_id: Some([3; 16]),
            placements: vec![([3; 16], 1, 1000), ([4; 16], 2, 500)],
            final_state_hash: [7; 32],
        });

        assert!(transcript.is_complete());
    }

    #[test]
    fn test_estimated_size() {
        let metadata = create_test_metadata();
        let mut transcript = MatchTranscript::new(metadata);

        // Add typical match data
        for i in 0..9 {
            transcript.add_checkpoint(i * 600 + 600, [i as u8; 32], [i as u64, i as u64 + 1]);
        }

        for _ in 0..50 {
            transcript.events.push(TranscriptEvent::RuneCollected {
                tick: 100,
                player_id: [3; 16],
                rune_id: 1,
                points: 10,
            });
        }

        let size = transcript.estimated_size();
        // Should be well under 100KB
        assert!(size < 100_000, "Size {} should be under 100KB", size);
    }
}

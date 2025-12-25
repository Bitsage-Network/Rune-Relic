//! Match Commitment Protocol
//!
//! Commit to match parameters before the game starts.
//! Reveal and verify at match end to prevent manipulation.

use sha2::{Sha256, Digest};
use serde::{Serialize, Deserialize};
use crate::core::hash::StateHash;
use crate::core::rng::derive_match_seed;
use crate::proof::transcript::MatchTranscript;

/// Domain separator for commitments.
const COMMITMENT_DOMAIN: &[u8] = b"RUNE_RELIC_COMMIT_V1";

/// Pre-match commitment structure.
///
/// Players/server commit to this before match starts.
/// Cannot be changed after commitment is published.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct MatchCommitment {
    /// Commitment hash (published before match).
    pub commitment_hash: StateHash,

    /// Minimum block height for RNG block hash.
    pub block_height_min: u64,

    /// Maximum block height for match completion.
    pub block_height_max: u64,
}

/// Pre-image data for commitment (kept secret until reveal).
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct CommitmentPreimage {
    /// Match ID (chosen by matchmaker).
    pub match_id: [u8; 16],

    /// Player IDs (must be sorted for determinism).
    pub player_ids: Vec<[u8; 16]>,

    /// Match configuration hash.
    pub config_hash: StateHash,

    /// Random nonce from each player (prevents prediction).
    pub player_nonces: Vec<[u8; 32]>,

    /// Server nonce.
    pub server_nonce: [u8; 32],
}

impl MatchCommitment {
    /// Create commitment from preimage.
    pub fn from_preimage(preimage: &CommitmentPreimage, block_min: u64, block_max: u64) -> Self {
        let commitment_hash = compute_commitment_hash(preimage);
        Self {
            commitment_hash,
            block_height_min: block_min,
            block_height_max: block_max,
        }
    }

    /// Verify that a preimage matches this commitment.
    pub fn verify(&self, preimage: &CommitmentPreimage) -> bool {
        let computed = compute_commitment_hash(preimage);
        computed == self.commitment_hash
    }

    /// Check if a block height is in the valid range.
    pub fn is_block_in_range(&self, block_height: u64) -> bool {
        block_height >= self.block_height_min && block_height <= self.block_height_max
    }
}

/// Compute commitment hash from preimage.
fn compute_commitment_hash(preimage: &CommitmentPreimage) -> StateHash {
    let mut hasher = Sha256::new();
    hasher.update(COMMITMENT_DOMAIN);
    hasher.update(preimage.match_id);

    // Hash sorted player IDs
    for pid in &preimage.player_ids {
        hasher.update(pid);
    }

    hasher.update(preimage.config_hash);

    // Hash player nonces
    for nonce in &preimage.player_nonces {
        hasher.update(nonce);
    }

    hasher.update(preimage.server_nonce);

    hasher.finalize().into()
}

/// Reveal structure (published after match ends).
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct MatchReveal {
    /// The original preimage.
    pub preimage: CommitmentPreimage,

    /// Block hash used for RNG (from block in valid range).
    pub block_hash: [u8; 32],

    /// Block height where block_hash comes from.
    pub block_height: u64,

    /// Full match transcript.
    pub transcript: MatchTranscript,
}

impl MatchReveal {
    /// Create a new reveal.
    pub fn new(
        preimage: CommitmentPreimage,
        block_hash: [u8; 32],
        block_height: u64,
        transcript: MatchTranscript,
    ) -> Self {
        Self {
            preimage,
            block_hash,
            block_height,
            transcript,
        }
    }

    /// Verify reveal against commitment.
    pub fn verify(&self, commitment: &MatchCommitment) -> Result<(), CommitmentError> {
        // 1. Verify preimage matches commitment
        if !commitment.verify(&self.preimage) {
            return Err(CommitmentError::PreimageMismatch);
        }

        // 2. Verify block height is in valid range
        if !commitment.is_block_in_range(self.block_height) {
            return Err(CommitmentError::BlockOutOfRange {
                min: commitment.block_height_min,
                max: commitment.block_height_max,
                got: self.block_height,
            });
        }

        // 3. Verify transcript uses correct RNG seed
        let expected_seed = derive_match_seed(
            &self.block_hash,
            &self.preimage.match_id,
            &self.preimage.player_ids,
        );

        if self.transcript.metadata.rng_seed != expected_seed {
            return Err(CommitmentError::SeedMismatch {
                expected: expected_seed,
                got: self.transcript.metadata.rng_seed,
            });
        }

        // 4. Verify match ID matches
        if self.transcript.metadata.match_id != self.preimage.match_id {
            return Err(CommitmentError::MatchIdMismatch);
        }

        // 5. Verify player IDs match
        let mut transcript_players: Vec<[u8; 16]> = self.transcript.metadata.player_ids.clone();
        transcript_players.sort();
        let mut preimage_players = self.preimage.player_ids.clone();
        preimage_players.sort();

        if transcript_players != preimage_players {
            return Err(CommitmentError::PlayerIdsMismatch);
        }

        Ok(())
    }
}

/// Errors that can occur during commitment verification.
#[derive(Debug, Clone)]
pub enum CommitmentError {
    /// Preimage hash doesn't match commitment.
    PreimageMismatch,

    /// Block height is outside valid range.
    BlockOutOfRange {
        /// Minimum allowed block height.
        min: u64,
        /// Maximum allowed block height.
        max: u64,
        /// Actual block height provided.
        got: u64,
    },

    /// RNG seed doesn't match derived seed.
    SeedMismatch {
        /// Expected seed from derivation.
        expected: u64,
        /// Actual seed in transcript.
        got: u64,
    },

    /// Match ID in transcript doesn't match preimage.
    MatchIdMismatch,

    /// Player IDs in transcript don't match preimage.
    PlayerIdsMismatch,
}

impl std::fmt::Display for CommitmentError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::PreimageMismatch => write!(f, "Preimage hash doesn't match commitment"),
            Self::BlockOutOfRange { min, max, got } => {
                write!(f, "Block height {} is outside range [{}, {}]", got, min, max)
            }
            Self::SeedMismatch { expected, got } => {
                write!(f, "RNG seed mismatch: expected {}, got {}", expected, got)
            }
            Self::MatchIdMismatch => write!(f, "Match ID mismatch"),
            Self::PlayerIdsMismatch => write!(f, "Player IDs mismatch"),
        }
    }
}

impl std::error::Error for CommitmentError {}

/// Builder for creating commitments.
pub struct CommitmentBuilder {
    match_id: [u8; 16],
    player_ids: Vec<[u8; 16]>,
    config_hash: StateHash,
    player_nonces: Vec<[u8; 32]>,
    server_nonce: [u8; 32],
}

impl CommitmentBuilder {
    /// Create a new builder with match ID.
    pub fn new(match_id: [u8; 16]) -> Self {
        Self {
            match_id,
            player_ids: Vec::new(),
            config_hash: [0; 32],
            player_nonces: Vec::new(),
            server_nonce: [0; 32],
        }
    }

    /// Add a player with their nonce.
    pub fn add_player(mut self, player_id: [u8; 16], nonce: [u8; 32]) -> Self {
        self.player_ids.push(player_id);
        self.player_nonces.push(nonce);
        self
    }

    /// Set the configuration hash.
    pub fn config_hash(mut self, hash: StateHash) -> Self {
        self.config_hash = hash;
        self
    }

    /// Set the server nonce.
    pub fn server_nonce(mut self, nonce: [u8; 32]) -> Self {
        self.server_nonce = nonce;
        self
    }

    /// Build the preimage and commitment.
    pub fn build(mut self, block_min: u64, block_max: u64) -> (CommitmentPreimage, MatchCommitment) {
        // Sort player IDs for determinism
        let mut indexed: Vec<(usize, [u8; 16])> = self.player_ids.iter()
            .cloned()
            .enumerate()
            .collect();
        indexed.sort_by_key(|(_, id)| *id);

        let sorted_player_ids: Vec<[u8; 16]> = indexed.iter().map(|(_, id)| *id).collect();
        let sorted_nonces: Vec<[u8; 32]> = indexed.iter()
            .map(|(orig_idx, _)| self.player_nonces[*orig_idx])
            .collect();

        self.player_ids = sorted_player_ids;
        self.player_nonces = sorted_nonces;

        let preimage = CommitmentPreimage {
            match_id: self.match_id,
            player_ids: self.player_ids,
            config_hash: self.config_hash,
            player_nonces: self.player_nonces,
            server_nonce: self.server_nonce,
        };

        let commitment = MatchCommitment::from_preimage(&preimage, block_min, block_max);

        (preimage, commitment)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::proof::transcript::MatchMetadata;

    fn create_test_preimage() -> CommitmentPreimage {
        CommitmentPreimage {
            match_id: [1; 16],
            player_ids: vec![[2; 16], [3; 16]],
            config_hash: [4; 32],
            player_nonces: vec![[5; 32], [6; 32]],
            server_nonce: [7; 32],
        }
    }

    #[test]
    fn test_commitment_creation() {
        let preimage = create_test_preimage();
        let commitment = MatchCommitment::from_preimage(&preimage, 100, 200);

        assert!(commitment.verify(&preimage));
        assert!(commitment.is_block_in_range(100));
        assert!(commitment.is_block_in_range(150));
        assert!(commitment.is_block_in_range(200));
        assert!(!commitment.is_block_in_range(99));
        assert!(!commitment.is_block_in_range(201));
    }

    #[test]
    fn test_commitment_determinism() {
        let preimage = create_test_preimage();

        let commitment1 = MatchCommitment::from_preimage(&preimage, 100, 200);
        let commitment2 = MatchCommitment::from_preimage(&preimage, 100, 200);

        assert_eq!(commitment1.commitment_hash, commitment2.commitment_hash);
    }

    #[test]
    fn test_wrong_preimage_fails() {
        let preimage = create_test_preimage();
        let commitment = MatchCommitment::from_preimage(&preimage, 100, 200);

        // Modify preimage
        let mut wrong = preimage.clone();
        wrong.match_id[0] = 0xFF;

        assert!(!commitment.verify(&wrong));
    }

    #[test]
    fn test_commitment_builder() {
        let (preimage, commitment) = CommitmentBuilder::new([1; 16])
            .add_player([3; 16], [10; 32])  // Add out of order
            .add_player([2; 16], [20; 32])
            .config_hash([4; 32])
            .server_nonce([7; 32])
            .build(100, 200);

        // Players should be sorted
        assert!(preimage.player_ids[0] < preimage.player_ids[1]);
        assert!(commitment.verify(&preimage));
    }

    #[test]
    fn test_reveal_verification() {
        let preimage = create_test_preimage();
        let commitment = MatchCommitment::from_preimage(&preimage, 100, 200);

        // Create matching transcript
        let block_hash = [8; 32];
        let rng_seed = derive_match_seed(&block_hash, &preimage.match_id, &preimage.player_ids);

        let metadata = MatchMetadata {
            match_id: preimage.match_id,
            block_hash,
            player_ids: preimage.player_ids.clone(),
            rng_seed,
            start_timestamp: 1700000000,
            config_hash: preimage.config_hash,
        };

        let transcript = MatchTranscript::new(metadata);

        let reveal = MatchReveal::new(preimage, block_hash, 150, transcript);

        assert!(reveal.verify(&commitment).is_ok());
    }

    #[test]
    fn test_reveal_wrong_seed_fails() {
        let preimage = create_test_preimage();
        let commitment = MatchCommitment::from_preimage(&preimage, 100, 200);

        // Create transcript with wrong seed
        let metadata = MatchMetadata {
            match_id: preimage.match_id,
            block_hash: [8; 32],
            player_ids: preimage.player_ids.clone(),
            rng_seed: 99999, // Wrong seed
            start_timestamp: 1700000000,
            config_hash: preimage.config_hash,
        };

        let transcript = MatchTranscript::new(metadata);
        let reveal = MatchReveal::new(preimage, [8; 32], 150, transcript);

        assert!(matches!(reveal.verify(&commitment), Err(CommitmentError::SeedMismatch { .. })));
    }

    #[test]
    fn test_reveal_wrong_block_height_fails() {
        let preimage = create_test_preimage();
        let commitment = MatchCommitment::from_preimage(&preimage, 100, 200);

        let block_hash = [8; 32];
        let rng_seed = derive_match_seed(&block_hash, &preimage.match_id, &preimage.player_ids);

        let metadata = MatchMetadata {
            match_id: preimage.match_id,
            block_hash,
            player_ids: preimage.player_ids.clone(),
            rng_seed,
            start_timestamp: 1700000000,
            config_hash: preimage.config_hash,
        };

        let transcript = MatchTranscript::new(metadata);
        let reveal = MatchReveal::new(preimage, block_hash, 50, transcript); // Block 50 is too early

        assert!(matches!(reveal.verify(&commitment), Err(CommitmentError::BlockOutOfRange { .. })));
    }
}

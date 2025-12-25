//! Proof Public Inputs
//!
//! Converts match transcript data into M31 field elements for STWO proofs.
//! Uses Merkle commitments for large data to keep public input size bounded.

use crate::core::hash::{StateHash, M31_PRIME};
use crate::proof::transcript::MatchTranscript;
use crate::proof::merkle::MerkleTree;

/// M31 field element (u32 < 2^31 - 1).
pub type M31 = u32;

/// Total number of M31 field elements in public inputs.
pub const PUBLIC_INPUT_ELEMENT_COUNT: usize = 66;

/// Public inputs for STWO proof verification.
///
/// This structure is what goes on-chain or to the verifier.
/// Kept small via Merkle commitments for variable-size data.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ProofPublicInputs {
    /// Match ID (encoded as 4 M31 elements).
    pub match_id: [M31; 4],

    /// Block hash commitment (8 M31 elements, 256 bits).
    pub block_hash: [M31; 8],

    /// Player count.
    pub player_count: M31,

    /// Match duration in ticks.
    pub duration_ticks: M31,

    /// Winner player ID (4 M31 elements, or zeros if no winner).
    pub winner_id: [M31; 4],

    /// Final placements Merkle root (32 bytes as 8 M31).
    pub placements_root: [M31; 8],

    /// Input commitments Merkle root.
    pub inputs_root: [M31; 8],

    /// Initial state hash (8 M31 elements).
    pub initial_state_hash: [M31; 8],

    /// Final state hash (8 M31 elements).
    pub final_state_hash: [M31; 8],

    /// Checkpoint Merkle root (for intermediate state verification).
    pub checkpoints_root: [M31; 8],

    /// Events Merkle root (for replay verification).
    pub events_root: [M31; 8],
}

impl Default for ProofPublicInputs {
    fn default() -> Self {
        Self {
            match_id: [0; 4],
            block_hash: [0; 8],
            player_count: 0,
            duration_ticks: 0,
            winner_id: [0; 4],
            placements_root: [0; 8],
            inputs_root: [0; 8],
            initial_state_hash: [0; 8],
            final_state_hash: [0; 8],
            checkpoints_root: [0; 8],
            events_root: [0; 8],
        }
    }
}

impl ProofPublicInputs {
    /// Generate public inputs from a transcript.
    pub fn from_transcript(transcript: &MatchTranscript) -> Self {
        let result = transcript.result.as_ref();

        // Build Merkle trees for variable-size data
        let placements_root = if let Some(r) = result {
            let placement_bytes: Vec<Vec<u8>> = r.placements.iter()
                .map(|(id, placement, score)| {
                    let mut bytes = Vec::with_capacity(21);
                    bytes.extend_from_slice(id);
                    bytes.push(*placement);
                    bytes.extend_from_slice(&score.to_le_bytes());
                    bytes
                })
                .collect();
            let mut tree = MerkleTree::from_leaves(&placement_bytes);
            hash_to_m31(&tree.root())
        } else {
            [0; 8]
        };

        let inputs_root = {
            let input_hashes: Vec<Vec<u8>> = transcript.player_inputs.iter()
                .map(|record| {
                    // Hash each player's input deltas
                    let mut bytes = Vec::new();
                    bytes.extend_from_slice(&record.player_id);
                    for delta in &record.deltas {
                        bytes.extend_from_slice(&delta.tick.to_le_bytes());
                        bytes.push(delta.frame.move_x as u8);
                        bytes.push(delta.frame.move_y as u8);
                        bytes.push(delta.frame.flags);
                    }
                    bytes
                })
                .collect();
            let mut tree = MerkleTree::from_leaves(&input_hashes);
            hash_to_m31(&tree.root())
        };

        let checkpoints_root = {
            let checkpoint_bytes: Vec<Vec<u8>> = transcript.checkpoints.iter()
                .map(|cp| {
                    let mut bytes = Vec::with_capacity(52);
                    bytes.extend_from_slice(&cp.tick.to_le_bytes());
                    bytes.extend_from_slice(&cp.state_hash);
                    bytes.extend_from_slice(&cp.rng_state[0].to_le_bytes());
                    bytes.extend_from_slice(&cp.rng_state[1].to_le_bytes());
                    bytes
                })
                .collect();
            let mut tree = MerkleTree::from_leaves(&checkpoint_bytes);
            hash_to_m31(&tree.root())
        };

        let events_root = {
            let event_bytes: Vec<Vec<u8>> = transcript.events.iter()
                .map(|event| bincode::serialize(event).unwrap_or_default())
                .collect();
            let mut tree = MerkleTree::from_leaves(&event_bytes);
            hash_to_m31(&tree.root())
        };

        Self {
            match_id: uuid_to_m31(&transcript.metadata.match_id),
            block_hash: hash_to_m31(&transcript.metadata.block_hash),
            player_count: transcript.player_count() as M31,
            duration_ticks: result.map(|r| r.end_tick).unwrap_or(0),
            winner_id: result
                .and_then(|r| r.winner_id)
                .map(|id| uuid_to_m31(&id))
                .unwrap_or([0; 4]),
            placements_root,
            inputs_root,
            initial_state_hash: hash_to_m31(&transcript.initial_state.state_hash),
            final_state_hash: result
                .map(|r| hash_to_m31(&r.final_state_hash))
                .unwrap_or([0; 8]),
            checkpoints_root,
            events_root,
        }
    }

    /// Encode to flat M31 array (for STWO).
    pub fn to_m31_array(&self) -> [M31; PUBLIC_INPUT_ELEMENT_COUNT] {
        let mut arr = [0u32; PUBLIC_INPUT_ELEMENT_COUNT];
        let mut idx = 0;

        // match_id: 4 elements
        for &v in &self.match_id {
            arr[idx] = v;
            idx += 1;
        }

        // block_hash: 8 elements
        for &v in &self.block_hash {
            arr[idx] = v;
            idx += 1;
        }

        // player_count: 1 element
        arr[idx] = self.player_count;
        idx += 1;

        // duration_ticks: 1 element
        arr[idx] = self.duration_ticks;
        idx += 1;

        // winner_id: 4 elements
        for &v in &self.winner_id {
            arr[idx] = v;
            idx += 1;
        }

        // placements_root: 8 elements
        for &v in &self.placements_root {
            arr[idx] = v;
            idx += 1;
        }

        // inputs_root: 8 elements
        for &v in &self.inputs_root {
            arr[idx] = v;
            idx += 1;
        }

        // initial_state_hash: 8 elements
        for &v in &self.initial_state_hash {
            arr[idx] = v;
            idx += 1;
        }

        // final_state_hash: 8 elements
        for &v in &self.final_state_hash {
            arr[idx] = v;
            idx += 1;
        }

        // checkpoints_root: 8 elements
        for &v in &self.checkpoints_root {
            arr[idx] = v;
            idx += 1;
        }

        // events_root: 8 elements
        for &v in &self.events_root {
            arr[idx] = v;
            idx += 1;
        }

        debug_assert_eq!(idx, PUBLIC_INPUT_ELEMENT_COUNT);
        arr
    }

    /// Decode from flat M31 array.
    pub fn from_m31_array(arr: &[M31; PUBLIC_INPUT_ELEMENT_COUNT]) -> Self {
        let mut idx = 0;

        let mut match_id = [0u32; 4];
        for i in 0..4 {
            match_id[i] = arr[idx];
            idx += 1;
        }

        let mut block_hash = [0u32; 8];
        for i in 0..8 {
            block_hash[i] = arr[idx];
            idx += 1;
        }

        let player_count = arr[idx];
        idx += 1;

        let duration_ticks = arr[idx];
        idx += 1;

        let mut winner_id = [0u32; 4];
        for i in 0..4 {
            winner_id[i] = arr[idx];
            idx += 1;
        }

        let mut placements_root = [0u32; 8];
        for i in 0..8 {
            placements_root[i] = arr[idx];
            idx += 1;
        }

        let mut inputs_root = [0u32; 8];
        for i in 0..8 {
            inputs_root[i] = arr[idx];
            idx += 1;
        }

        let mut initial_state_hash = [0u32; 8];
        for i in 0..8 {
            initial_state_hash[i] = arr[idx];
            idx += 1;
        }

        let mut final_state_hash = [0u32; 8];
        for i in 0..8 {
            final_state_hash[i] = arr[idx];
            idx += 1;
        }

        let mut checkpoints_root = [0u32; 8];
        for i in 0..8 {
            checkpoints_root[i] = arr[idx];
            idx += 1;
        }

        let mut events_root = [0u32; 8];
        for i in 0..8 {
            events_root[i] = arr[idx];
            idx += 1;
        }

        Self {
            match_id,
            block_hash,
            player_count,
            duration_ticks,
            winner_id,
            placements_root,
            inputs_root,
            initial_state_hash,
            final_state_hash,
            checkpoints_root,
            events_root,
        }
    }

    /// Serialize to bytes (for on-chain submission).
    pub fn to_bytes(&self) -> Vec<u8> {
        let arr = self.to_m31_array();
        let mut bytes = Vec::with_capacity(PUBLIC_INPUT_ELEMENT_COUNT * 4);
        for val in arr {
            bytes.extend_from_slice(&val.to_le_bytes());
        }
        bytes
    }

    /// Deserialize from bytes.
    pub fn from_bytes(data: &[u8]) -> Option<Self> {
        if data.len() != PUBLIC_INPUT_ELEMENT_COUNT * 4 {
            return None;
        }

        let mut arr = [0u32; PUBLIC_INPUT_ELEMENT_COUNT];
        for (i, chunk) in data.chunks_exact(4).enumerate() {
            arr[i] = u32::from_le_bytes([chunk[0], chunk[1], chunk[2], chunk[3]]);
        }

        Some(Self::from_m31_array(&arr))
    }
}

/// Encode a 32-byte hash to 8 M31 elements.
///
/// Splits hash into 8 chunks of 4 bytes, takes lower 31 bits of each.
pub fn hash_to_m31(hash: &StateHash) -> [M31; 8] {
    let mut result = [0u32; 8];
    for i in 0..8 {
        let chunk = u32::from_le_bytes([
            hash[i * 4],
            hash[i * 4 + 1],
            hash[i * 4 + 2],
            hash[i * 4 + 3],
        ]);
        // Take lower 31 bits to fit in M31
        result[i] = chunk % M31_PRIME;
    }
    result
}

/// Decode 8 M31 elements back to approximate hash.
///
/// Note: This is lossy due to modular reduction.
pub fn m31_to_hash(encoded: &[M31; 8]) -> StateHash {
    let mut hash = [0u8; 32];
    for i in 0..8 {
        let bytes = encoded[i].to_le_bytes();
        hash[i * 4] = bytes[0];
        hash[i * 4 + 1] = bytes[1];
        hash[i * 4 + 2] = bytes[2];
        hash[i * 4 + 3] = bytes[3];
    }
    hash
}

/// Encode a UUID (16 bytes) to 4 M31 elements.
pub fn uuid_to_m31(uuid: &[u8; 16]) -> [M31; 4] {
    let mut result = [0u32; 4];
    for i in 0..4 {
        let chunk = u32::from_le_bytes([
            uuid[i * 4],
            uuid[i * 4 + 1],
            uuid[i * 4 + 2],
            uuid[i * 4 + 3],
        ]);
        result[i] = chunk % M31_PRIME;
    }
    result
}

/// Decode 4 M31 elements back to UUID bytes.
///
/// Note: This is lossy due to modular reduction.
pub fn m31_to_uuid(encoded: &[M31; 4]) -> [u8; 16] {
    let mut uuid = [0u8; 16];
    for i in 0..4 {
        let bytes = encoded[i].to_le_bytes();
        uuid[i * 4] = bytes[0];
        uuid[i * 4 + 1] = bytes[1];
        uuid[i * 4 + 2] = bytes[2];
        uuid[i * 4 + 3] = bytes[3];
    }
    uuid
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::proof::transcript::{MatchMetadata, MatchResult};

    fn create_test_transcript() -> MatchTranscript {
        let metadata = MatchMetadata {
            match_id: [1; 16],
            block_hash: [2; 32],
            player_ids: vec![[3; 16], [4; 16]],
            rng_seed: 12345,
            start_timestamp: 1700000000,
            config_hash: [5; 32],
        };

        let mut transcript = MatchTranscript::new(metadata);

        // Add checkpoints
        transcript.add_checkpoint(600, [6; 32], [100, 200]);

        // Finalize
        transcript.finalize(MatchResult {
            end_tick: 5400,
            winner_id: Some([3; 16]),
            placements: vec![([3; 16], 1, 1000), ([4; 16], 2, 500)],
            final_state_hash: [7; 32],
        });

        transcript
    }

    #[test]
    fn test_m31_encoding_bounds() {
        // All encoded values should be < M31_PRIME
        let hash: StateHash = [0xFF; 32];
        let encoded = hash_to_m31(&hash);

        for val in encoded {
            assert!(val < M31_PRIME, "Value {} should be < M31_PRIME", val);
        }
    }

    #[test]
    fn test_uuid_to_m31() {
        let uuid: [u8; 16] = [0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0,
                              0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88];
        let encoded = uuid_to_m31(&uuid);

        for val in encoded {
            assert!(val < M31_PRIME);
        }
    }

    #[test]
    fn test_public_inputs_from_transcript() {
        let transcript = create_test_transcript();
        let inputs = ProofPublicInputs::from_transcript(&transcript);

        assert_eq!(inputs.player_count, 2);
        assert_eq!(inputs.duration_ticks, 5400);
        assert_ne!(inputs.winner_id, [0; 4]); // Winner was set
    }

    #[test]
    fn test_m31_array_roundtrip() {
        let transcript = create_test_transcript();
        let inputs = ProofPublicInputs::from_transcript(&transcript);

        let arr = inputs.to_m31_array();
        let decoded = ProofPublicInputs::from_m31_array(&arr);

        assert_eq!(inputs, decoded);
    }

    #[test]
    fn test_bytes_roundtrip() {
        let transcript = create_test_transcript();
        let inputs = ProofPublicInputs::from_transcript(&transcript);

        let bytes = inputs.to_bytes();
        let decoded = ProofPublicInputs::from_bytes(&bytes).unwrap();

        assert_eq!(inputs, decoded);
    }

    #[test]
    fn test_public_inputs_size() {
        let transcript = create_test_transcript();
        let inputs = ProofPublicInputs::from_transcript(&transcript);

        let bytes = inputs.to_bytes();
        assert_eq!(bytes.len(), PUBLIC_INPUT_ELEMENT_COUNT * 4);
        assert_eq!(bytes.len(), 264); // 66 * 4 = 264 bytes
    }
}

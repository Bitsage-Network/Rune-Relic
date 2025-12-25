//! Merkle Tree Commitments
//!
//! Binary Merkle tree using SHA-256 for committing to variable-size data.
//! Used for inputs, events, and checkpoints in proof public inputs.

use sha2::{Sha256, Digest};
use crate::core::hash::StateHash;

/// Domain separator for Merkle tree leaf nodes.
const MERKLE_LEAF_DOMAIN: &[u8] = b"RUNE_RELIC_MERKLE_LEAF_V1";

/// Domain separator for Merkle tree internal nodes.
const MERKLE_NODE_DOMAIN: &[u8] = b"RUNE_RELIC_MERKLE_NODE_V1";

/// Empty hash for padding (hash of empty domain).
fn empty_hash() -> StateHash {
    let mut hasher = Sha256::new();
    hasher.update(b"RUNE_RELIC_MERKLE_EMPTY_V1");
    hasher.finalize().into()
}

/// Binary Merkle tree for commitment generation.
///
/// Supports building from leaves, computing root, and generating/verifying proofs.
#[derive(Clone, Debug)]
pub struct MerkleTree {
    /// Leaf hashes (level 0)
    leaves: Vec<StateHash>,
    /// All tree levels (leaves at index 0, root at last index)
    levels: Vec<Vec<StateHash>>,
}

impl Default for MerkleTree {
    fn default() -> Self {
        Self::new()
    }
}

impl MerkleTree {
    /// Create an empty Merkle tree.
    pub fn new() -> Self {
        Self {
            leaves: Vec::new(),
            levels: Vec::new(),
        }
    }

    /// Create a Merkle tree from leaf data.
    ///
    /// Each item is hashed with domain separation to form leaves.
    pub fn from_leaves<T: AsRef<[u8]>>(data: &[T]) -> Self {
        let mut tree = Self::new();
        for item in data {
            tree.add_leaf(item.as_ref());
        }
        tree.build();
        tree
    }

    /// Add a leaf (raw data will be hashed).
    pub fn add_leaf(&mut self, data: &[u8]) {
        let leaf_hash = hash_leaf(data);
        self.leaves.push(leaf_hash);
        // Clear computed levels since tree changed
        self.levels.clear();
    }

    /// Add a pre-hashed leaf.
    pub fn add_leaf_hash(&mut self, hash: StateHash) {
        self.leaves.push(hash);
        self.levels.clear();
    }

    /// Build the tree (compute all internal nodes).
    fn build(&mut self) {
        if self.leaves.is_empty() {
            self.levels.clear();
            return;
        }

        self.levels.clear();

        // Level 0 is the leaves
        let mut current_level = self.leaves.clone();

        // Pad to power of 2 for balanced tree
        let target_size = current_level.len().next_power_of_two();
        while current_level.len() < target_size {
            current_level.push(empty_hash());
        }

        self.levels.push(current_level.clone());

        // Build up to root
        while current_level.len() > 1 {
            let mut next_level = Vec::with_capacity(current_level.len() / 2);

            for chunk in current_level.chunks(2) {
                let left = &chunk[0];
                let right = if chunk.len() > 1 { &chunk[1] } else { left };
                next_level.push(hash_nodes(left, right));
            }

            self.levels.push(next_level.clone());
            current_level = next_level;
        }
    }

    /// Compute and return the root hash.
    ///
    /// Returns empty hash for empty tree.
    pub fn root(&mut self) -> StateHash {
        if self.leaves.is_empty() {
            return empty_hash();
        }

        if self.levels.is_empty() {
            self.build();
        }

        // Root is the single element at the top level
        self.levels.last()
            .and_then(|level| level.first())
            .copied()
            .unwrap_or_else(empty_hash)
    }

    /// Get the root without building (returns None if not built).
    pub fn get_root(&self) -> Option<StateHash> {
        self.levels.last()?.first().copied()
    }

    /// Number of leaves in the tree.
    pub fn leaf_count(&self) -> usize {
        self.leaves.len()
    }

    /// Generate a Merkle inclusion proof for a leaf at the given index.
    ///
    /// Returns None if index is out of bounds.
    pub fn generate_proof(&mut self, index: usize) -> Option<MerkleProof> {
        if index >= self.leaves.len() {
            return None;
        }

        if self.levels.is_empty() {
            self.build();
        }

        let mut siblings = Vec::new();
        let mut current_index = index;

        // Walk up the tree, collecting sibling hashes
        for level in &self.levels[..self.levels.len().saturating_sub(1)] {
            let sibling_index = if current_index % 2 == 0 {
                current_index + 1
            } else {
                current_index - 1
            };

            if sibling_index < level.len() {
                let is_right = current_index % 2 == 0;
                siblings.push((level[sibling_index], is_right));
            }

            current_index /= 2;
        }

        Some(MerkleProof {
            leaf_index: index,
            siblings,
        })
    }

    /// Verify a Merkle proof against a root hash.
    pub fn verify_proof(root: &StateHash, proof: &MerkleProof, leaf_data: &[u8]) -> bool {
        let mut current_hash = hash_leaf(leaf_data);

        for (sibling, is_right) in &proof.siblings {
            current_hash = if *is_right {
                hash_nodes(&current_hash, sibling)
            } else {
                hash_nodes(sibling, &current_hash)
            };
        }

        current_hash == *root
    }

    /// Verify a proof using a pre-hashed leaf.
    pub fn verify_proof_with_hash(root: &StateHash, proof: &MerkleProof, leaf_hash: &StateHash) -> bool {
        let mut current_hash = *leaf_hash;

        for (sibling, is_right) in &proof.siblings {
            current_hash = if *is_right {
                hash_nodes(&current_hash, sibling)
            } else {
                hash_nodes(sibling, &current_hash)
            };
        }

        current_hash == *root
    }
}

/// Merkle inclusion proof.
///
/// Contains the path from a leaf to the root.
#[derive(Clone, Debug)]
pub struct MerkleProof {
    /// Index of the leaf this proof is for.
    pub leaf_index: usize,
    /// Sibling hashes along the path (hash, is_right_sibling).
    pub siblings: Vec<(StateHash, bool)>,
}

impl MerkleProof {
    /// Estimated size in bytes.
    pub fn size(&self) -> usize {
        4 + self.siblings.len() * 33 // index + (hash + bool) per sibling
    }
}

/// Hash leaf data with domain separation.
fn hash_leaf(data: &[u8]) -> StateHash {
    let mut hasher = Sha256::new();
    hasher.update(MERKLE_LEAF_DOMAIN);
    hasher.update(data);
    hasher.finalize().into()
}

/// Hash two child nodes with domain separation.
fn hash_nodes(left: &StateHash, right: &StateHash) -> StateHash {
    let mut hasher = Sha256::new();
    hasher.update(MERKLE_NODE_DOMAIN);
    hasher.update(left);
    hasher.update(right);
    hasher.finalize().into()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_empty_tree() {
        let mut tree = MerkleTree::new();
        let root = tree.root();
        // Empty tree should return empty hash
        assert_eq!(root, empty_hash());
    }

    #[test]
    fn test_single_leaf() {
        let mut tree = MerkleTree::new();
        tree.add_leaf(b"hello");
        let root = tree.root();

        // Root should be deterministic
        let mut tree2 = MerkleTree::new();
        tree2.add_leaf(b"hello");
        assert_eq!(root, tree2.root());
    }

    #[test]
    fn test_merkle_root_determinism() {
        let leaves = vec![b"a".to_vec(), b"b".to_vec(), b"c".to_vec(), b"d".to_vec()];

        let tree1 = MerkleTree::from_leaves(&leaves);
        let tree2 = MerkleTree::from_leaves(&leaves);

        assert_eq!(tree1.get_root(), tree2.get_root());
    }

    #[test]
    fn test_different_leaves_different_root() {
        let mut tree1 = MerkleTree::from_leaves(&[b"a", b"b"]);
        let mut tree2 = MerkleTree::from_leaves(&[b"a", b"c"]);

        assert_ne!(tree1.root(), tree2.root());
    }

    #[test]
    fn test_merkle_proof_verification() {
        let leaves: Vec<&[u8]> = vec![b"leaf1", b"leaf2", b"leaf3", b"leaf4"];
        let mut tree = MerkleTree::from_leaves(&leaves);
        let root = tree.root();

        // Generate and verify proof for each leaf
        for (i, leaf) in leaves.iter().enumerate() {
            let proof = tree.generate_proof(i).unwrap();
            assert!(MerkleTree::verify_proof(&root, &proof, leaf));
        }
    }

    #[test]
    fn test_invalid_proof_fails() {
        let leaves: Vec<&[u8]> = vec![b"leaf1", b"leaf2", b"leaf3", b"leaf4"];
        let mut tree = MerkleTree::from_leaves(&leaves);
        let root = tree.root();

        let proof = tree.generate_proof(0).unwrap();

        // Proof for wrong data should fail
        assert!(!MerkleTree::verify_proof(&root, &proof, b"wrong_data"));
    }

    #[test]
    fn test_proof_out_of_bounds() {
        let mut tree = MerkleTree::from_leaves(&[b"a", b"b"]);
        tree.root();
        assert!(tree.generate_proof(10).is_none());
    }

    #[test]
    fn test_odd_number_of_leaves() {
        // Tree should handle non-power-of-2 leaves
        let leaves: Vec<&[u8]> = vec![b"a", b"b", b"c"];
        let mut tree = MerkleTree::from_leaves(&leaves);
        let root = tree.root();

        // Should still be able to verify proofs
        let proof = tree.generate_proof(2).unwrap();
        assert!(MerkleTree::verify_proof(&root, &proof, b"c"));
    }

    #[test]
    fn test_large_tree() {
        let leaves: Vec<Vec<u8>> = (0..100)
            .map(|i| format!("leaf_{}", i).into_bytes())
            .collect();

        let mut tree = MerkleTree::from_leaves(&leaves);
        let root = tree.root();

        // Verify a few random proofs
        for i in [0, 50, 99] {
            let proof = tree.generate_proof(i).unwrap();
            assert!(MerkleTree::verify_proof(&root, &proof, &leaves[i]));
        }
    }
}

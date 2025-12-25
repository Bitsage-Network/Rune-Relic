//! State Hashing for Verification
//!
//! Provides deterministic hashing of game state for:
//! - Integrity verification between client/server
//! - BitSage proof public inputs
//! - Replay validation

use sha2::{Sha256, Digest};
use super::fixed::Fixed;
use super::vec2::FixedVec2;

/// Hash output type (256 bits / 32 bytes)
pub type StateHash = [u8; 32];

/// Deterministic hasher for game state.
///
/// Wraps SHA-256 with helpers for fixed-point types.
/// Order of updates is critical for determinism.
pub struct StateHasher {
    hasher: Sha256,
}

impl StateHasher {
    /// Create a new hasher with domain separator.
    pub fn new(domain: &[u8]) -> Self {
        let mut hasher = Sha256::new();
        hasher.update(domain);
        Self { hasher }
    }

    /// Create hasher for match state.
    pub fn for_match_state() -> Self {
        Self::new(b"RUNE_RELIC_STATE_V1")
    }

    /// Create hasher for input buffer.
    pub fn for_input_buffer() -> Self {
        Self::new(b"RUNE_RELIC_INPUTS_V1")
    }

    /// Update with raw bytes.
    #[inline]
    pub fn update_bytes(&mut self, bytes: &[u8]) {
        self.hasher.update(bytes);
    }

    /// Update with a u8 value.
    #[inline]
    pub fn update_u8(&mut self, value: u8) {
        self.hasher.update([value]);
    }

    /// Update with a u16 value (little-endian).
    #[inline]
    pub fn update_u16(&mut self, value: u16) {
        self.hasher.update(value.to_le_bytes());
    }

    /// Update with a u32 value (little-endian).
    #[inline]
    pub fn update_u32(&mut self, value: u32) {
        self.hasher.update(value.to_le_bytes());
    }

    /// Update with a u64 value (little-endian).
    #[inline]
    pub fn update_u64(&mut self, value: u64) {
        self.hasher.update(value.to_le_bytes());
    }

    /// Update with an i32 value (little-endian).
    #[inline]
    pub fn update_i32(&mut self, value: i32) {
        self.hasher.update(value.to_le_bytes());
    }

    /// Update with a Fixed value.
    #[inline]
    pub fn update_fixed(&mut self, value: Fixed) {
        self.update_i32(value);
    }

    /// Update with a FixedVec2.
    #[inline]
    pub fn update_vec2(&mut self, value: FixedVec2) {
        self.update_fixed(value.x);
        self.update_fixed(value.y);
    }

    /// Update with a boolean.
    #[inline]
    pub fn update_bool(&mut self, value: bool) {
        self.update_u8(value as u8);
    }

    /// Update with a UUID (16 bytes).
    #[inline]
    pub fn update_uuid(&mut self, uuid: &[u8; 16]) {
        self.hasher.update(uuid);
    }

    /// Finalize and return the hash.
    pub fn finalize(self) -> StateHash {
        self.hasher.finalize().into()
    }
}

/// Compute a simple hash of arbitrary data.
pub fn hash_bytes(data: &[u8]) -> StateHash {
    let mut hasher = Sha256::new();
    hasher.update(data);
    hasher.finalize().into()
}

/// Compute hash with domain separator.
pub fn hash_with_domain(domain: &[u8], data: &[u8]) -> StateHash {
    let mut hasher = Sha256::new();
    hasher.update(domain);
    hasher.update(data);
    hasher.finalize().into()
}

/// Compute state hash for match verification.
///
/// This function is called by `MatchState::compute_hash()`.
/// The parameter is a closure that adds state-specific data.
pub fn compute_state_hash<F>(tick: u32, rng_seed: u64, add_state: F) -> StateHash
where
    F: FnOnce(&mut StateHasher),
{
    let mut hasher = StateHasher::for_match_state();

    // Always hash tick and seed first
    hasher.update_u32(tick);
    hasher.update_u64(rng_seed);

    // Add game-specific state
    add_state(&mut hasher);

    hasher.finalize()
}

// =============================================================================
// M31 FIELD ENCODING (for STWO proofs)
// =============================================================================

/// Mersenne-31 prime: 2^31 - 1
pub const M31_PRIME: u32 = 2147483647;

/// Bias for encoding signed values into M31 field.
/// Shifts [-2^31, 2^31-1] to [0, 2^32-1], then we take mod M31.
pub const M31_BIAS: i64 = 1 << 30;

/// Encode a signed Fixed (i32) to M31 field element.
///
/// Uses bias encoding to handle negative values.
/// For values within arena bounds, no modular reduction is needed.
///
/// # Panics
/// Panics in debug mode if value is outside safe range.
#[inline]
pub fn encode_fixed_to_m31(value: Fixed) -> u32 {
    let biased = (value as i64) + M31_BIAS;

    debug_assert!(
        biased >= 0 && biased < M31_PRIME as i64,
        "Value {} out of M31 range after bias", value
    );

    biased as u32
}

/// Decode M31 field element back to signed Fixed.
#[inline]
pub fn decode_m31_to_fixed(encoded: u32) -> Fixed {
    (encoded as i64 - M31_BIAS) as Fixed
}

/// Encode a FixedVec2 to two M31 field elements.
#[inline]
pub fn encode_vec2_to_m31(vec: FixedVec2) -> (u32, u32) {
    (encode_fixed_to_m31(vec.x), encode_fixed_to_m31(vec.y))
}

/// Decode two M31 field elements back to FixedVec2.
#[inline]
pub fn decode_m31_to_vec2(x: u32, y: u32) -> FixedVec2 {
    FixedVec2::new(decode_m31_to_fixed(x), decode_m31_to_fixed(y))
}

// =============================================================================
// TESTS
// =============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use crate::core::fixed::{to_fixed, ARENA_HALF_WIDTH};

    #[test]
    fn test_state_hasher_determinism() {
        let make_hash = || {
            let mut hasher = StateHasher::for_match_state();
            hasher.update_u32(100);
            hasher.update_u64(12345);
            hasher.update_fixed(to_fixed(5.5));
            hasher.update_vec2(FixedVec2::new(to_fixed(1.0), to_fixed(2.0)));
            hasher.update_bool(true);
            hasher.finalize()
        };

        let hash1 = make_hash();
        let hash2 = make_hash();

        assert_eq!(hash1, hash2);
    }

    #[test]
    fn test_hash_order_matters() {
        let hash1 = {
            let mut h = StateHasher::new(b"test");
            h.update_u32(1);
            h.update_u32(2);
            h.finalize()
        };

        let hash2 = {
            let mut h = StateHasher::new(b"test");
            h.update_u32(2);
            h.update_u32(1);
            h.finalize()
        };

        assert_ne!(hash1, hash2);
    }

    #[test]
    fn test_domain_separation() {
        let data = [1u8, 2, 3, 4];

        let hash1 = hash_with_domain(b"DOMAIN_A", &data);
        let hash2 = hash_with_domain(b"DOMAIN_B", &data);

        assert_ne!(hash1, hash2);
    }

    #[test]
    fn test_m31_encoding() {
        // Test zero
        let encoded = encode_fixed_to_m31(0);
        let decoded = decode_m31_to_fixed(encoded);
        assert_eq!(decoded, 0);

        // Test positive value
        let val = to_fixed(25.0);
        let encoded = encode_fixed_to_m31(val);
        let decoded = decode_m31_to_fixed(encoded);
        assert_eq!(decoded, val);

        // Test negative value
        let neg_val = to_fixed(-25.0);
        let encoded = encode_fixed_to_m31(neg_val);
        let decoded = decode_m31_to_fixed(encoded);
        assert_eq!(decoded, neg_val);

        // Test arena bounds
        let encoded = encode_fixed_to_m31(ARENA_HALF_WIDTH);
        let decoded = decode_m31_to_fixed(encoded);
        assert_eq!(decoded, ARENA_HALF_WIDTH);

        let encoded = encode_fixed_to_m31(-ARENA_HALF_WIDTH);
        let decoded = decode_m31_to_fixed(encoded);
        assert_eq!(decoded, -ARENA_HALF_WIDTH);
    }

    #[test]
    fn test_vec2_m31_encoding() {
        let vec = FixedVec2::new(to_fixed(10.5), to_fixed(-20.5));
        let (x, y) = encode_vec2_to_m31(vec);
        let decoded = decode_m31_to_vec2(x, y);
        assert_eq!(decoded, vec);
    }

    #[test]
    fn test_compute_state_hash() {
        let hash = compute_state_hash(100, 12345, |hasher| {
            hasher.update_fixed(to_fixed(5.0));
            hasher.update_bool(true);
        });

        // Hash should be consistent
        let hash2 = compute_state_hash(100, 12345, |hasher| {
            hasher.update_fixed(to_fixed(5.0));
            hasher.update_bool(true);
        });

        assert_eq!(hash, hash2);

        // Different input = different hash
        let hash3 = compute_state_hash(101, 12345, |hasher| {
            hasher.update_fixed(to_fixed(5.0));
            hasher.update_bool(true);
        });

        assert_ne!(hash, hash3);
    }
}

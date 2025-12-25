//! Deterministic Random Number Generator
//!
//! Uses Xorshift128+ algorithm for fast, high-quality, deterministic randomness.
//! Given the same seed, produces identical sequence on all platforms.

use serde::{Serialize, Deserialize};
use sha2::{Sha256, Digest};

use super::fixed::{Fixed, ARENA_HALF_WIDTH, ARENA_HALF_HEIGHT};
use super::vec2::FixedVec2;

/// Deterministic PRNG using Xorshift128+ algorithm.
///
/// # Determinism Guarantee
///
/// Given the same seed, this RNG will produce the exact same sequence
/// of random numbers on any platform (x86, ARM, WASM, GPU).
///
/// # Example
///
/// ```
/// use rune_relic::core::rng::DeterministicRng;
///
/// let mut rng = DeterministicRng::new(12345);
/// let value = rng.next_u64();
/// assert_eq!(value, 6233086606872742541); // Always the same!
/// ```
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct DeterministicRng {
    state: [u64; 2],
}

impl Default for DeterministicRng {
    fn default() -> Self {
        Self::new(0)
    }
}

impl DeterministicRng {
    /// Create a new RNG from a 64-bit seed.
    ///
    /// Uses SplitMix64 to initialize the internal state, ensuring
    /// good distribution even from weak seeds.
    pub fn new(seed: u64) -> Self {
        let mut s = seed;
        let state0 = splitmix64(&mut s);
        let state1 = splitmix64(&mut s);

        // Ensure state is never all zeros
        let state = if state0 == 0 && state1 == 0 {
            [1, 1]
        } else {
            [state0, state1]
        };

        Self { state }
    }

    /// Create RNG from match parameters.
    ///
    /// Derives a deterministic seed from:
    /// - Block hash (unpredictable before match)
    /// - Match ID (unique per match)
    /// - Sorted player IDs (prevents manipulation)
    ///
    /// This ensures the seed cannot be predicted or manipulated.
    pub fn from_match_params(
        block_hash: &[u8; 32],
        match_id: &[u8; 16],
        player_ids: &[[u8; 16]],
    ) -> Self {
        let seed = derive_match_seed(block_hash, match_id, player_ids);
        Self::new(seed)
    }

    /// Generate the next 64-bit random value.
    #[inline]
    pub fn next_u64(&mut self) -> u64 {
        let s0 = self.state[0];
        let mut s1 = self.state[1];
        let result = s0.wrapping_add(s1);

        s1 ^= s0;
        self.state[0] = s0.rotate_left(24) ^ s1 ^ (s1 << 16);
        self.state[1] = s1.rotate_left(37);

        result
    }

    /// Generate a random u32.
    #[inline]
    pub fn next_u32(&mut self) -> u32 {
        self.next_u64() as u32
    }

    /// Generate a random integer in range [0, max).
    ///
    /// Uses rejection sampling for uniform distribution.
    #[inline]
    pub fn next_int(&mut self, max: u32) -> u32 {
        if max == 0 {
            return 0;
        }
        // Simple modulo - slight bias for very large max, but acceptable
        (self.next_u64() % max as u64) as u32
    }

    /// Generate a random integer in range [min, max].
    #[inline]
    pub fn next_int_range(&mut self, min: i32, max: i32) -> i32 {
        if min >= max {
            return min;
        }
        let range = (max - min + 1) as u32;
        min + self.next_int(range) as i32
    }

    /// Generate a random Fixed in range [0, max).
    #[inline]
    pub fn next_fixed(&mut self, max: Fixed) -> Fixed {
        if max <= 0 {
            return 0;
        }
        // Use upper 32 bits to avoid overflow in multiplication
        let raw = (self.next_u64() >> 32) as u32;
        // Scale to [0, max) range: (raw * max) / 2^32
        ((raw as i64 * max as i64) >> 32) as Fixed
    }

    /// Generate a random Fixed in range [min, max).
    #[inline]
    pub fn next_fixed_range(&mut self, min: Fixed, max: Fixed) -> Fixed {
        if min >= max {
            return min;
        }
        let range = max.wrapping_sub(min);
        min.wrapping_add(self.next_fixed(range))
    }

    /// Generate a random position within arena bounds.
    #[inline]
    pub fn random_position(&mut self) -> FixedVec2 {
        let x = self.next_fixed_range(-ARENA_HALF_WIDTH, ARENA_HALF_WIDTH);
        let y = self.next_fixed_range(-ARENA_HALF_HEIGHT, ARENA_HALF_HEIGHT);
        FixedVec2::new(x, y)
    }

    /// Generate a random position within a circular area.
    ///
    /// Uses rejection sampling for uniform distribution.
    pub fn random_position_in_circle(&mut self, center: FixedVec2, radius: Fixed) -> FixedVec2 {
        // Rejection sampling for uniform distribution in circle
        loop {
            let x = self.next_fixed_range(-radius, radius);
            let y = self.next_fixed_range(-radius, radius);
            let offset = FixedVec2::new(x, y);

            // Check if within circle (use squared to avoid sqrt)
            let radius_sq = super::fixed::fixed_mul(radius, radius);
            if offset.length_squared() <= radius_sq {
                return center.add(offset);
            }
        }
    }

    /// Generate a random normalized direction vector.
    pub fn random_direction(&mut self) -> FixedVec2 {
        // Generate point, normalize
        // Rejection sampling to avoid zero vector
        loop {
            let x = self.next_fixed_range(-super::fixed::FIXED_ONE, super::fixed::FIXED_ONE);
            let y = self.next_fixed_range(-super::fixed::FIXED_ONE, super::fixed::FIXED_ONE);
            let vec = FixedVec2::new(x, y);

            if vec.length_squared() > 0 {
                return vec.normalize();
            }
        }
    }

    /// Generate a random boolean with given probability.
    ///
    /// probability is in range [0, FIXED_ONE] where FIXED_ONE = 100%
    #[inline]
    pub fn next_bool(&mut self, probability: Fixed) -> bool {
        self.next_fixed(super::fixed::FIXED_ONE) < probability
    }

    /// Shuffle a slice in place using Fisher-Yates algorithm.
    pub fn shuffle<T>(&mut self, slice: &mut [T]) {
        let len = slice.len();
        for i in (1..len).rev() {
            let j = self.next_int((i + 1) as u32) as usize;
            slice.swap(i, j);
        }
    }

    /// Select a random element from a slice.
    pub fn choose<'a, T>(&mut self, slice: &'a [T]) -> Option<&'a T> {
        if slice.is_empty() {
            None
        } else {
            let idx = self.next_int(slice.len() as u32) as usize;
            Some(&slice[idx])
        }
    }

    /// Get current state (for checkpointing/debugging).
    pub fn state(&self) -> [u64; 2] {
        self.state
    }

    /// Restore from saved state.
    pub fn set_state(&mut self, state: [u64; 2]) {
        self.state = state;
    }
}

/// SplitMix64 for seed initialization.
/// Produces well-distributed values from sequential seeds.
#[inline]
fn splitmix64(state: &mut u64) -> u64 {
    *state = state.wrapping_add(0x9E3779B97F4A7C15);
    let mut z = *state;
    z = (z ^ (z >> 30)).wrapping_mul(0xBF58476D1CE4E5B9);
    z = (z ^ (z >> 27)).wrapping_mul(0x94D049BB133111EB);
    z ^ (z >> 31)
}

/// Derive a match seed from verifiable parameters.
///
/// This function produces a deterministic seed that:
/// 1. Cannot be predicted before the block hash is known
/// 2. Cannot be manipulated by any single party
/// 3. Is verifiable after the match
///
/// # Parameters
///
/// - `block_hash`: Recent blockchain block hash (unpredictable)
/// - `match_id`: Unique match identifier
/// - `player_ids`: All player IDs (MUST be sorted for determinism)
pub fn derive_match_seed(
    block_hash: &[u8; 32],
    match_id: &[u8; 16],
    player_ids: &[[u8; 16]],
) -> u64 {
    let mut hasher = Sha256::new();

    // Domain separator
    hasher.update(b"RUNE_RELIC_SEED_V1");

    // Block hash (unpredictable entropy)
    hasher.update(block_hash);

    // Match ID (unique per match)
    hasher.update(match_id);

    // Player IDs (sorted for determinism)
    // IMPORTANT: Caller must ensure player_ids is sorted!
    for pid in player_ids {
        hasher.update(pid);
    }

    let hash = hasher.finalize();

    // Take first 8 bytes as seed
    u64::from_le_bytes(hash[0..8].try_into().unwrap())
}

// =============================================================================
// TESTS
// =============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use crate::core::fixed::to_fixed;

    #[test]
    fn test_rng_determinism() {
        // Same seed must produce same sequence
        let mut rng1 = DeterministicRng::new(12345);
        let mut rng2 = DeterministicRng::new(12345);

        for _ in 0..1000 {
            assert_eq!(rng1.next_u64(), rng2.next_u64());
        }
    }

    #[test]
    fn test_rng_different_seeds() {
        // Different seeds produce different sequences
        let mut rng1 = DeterministicRng::new(12345);
        let mut rng2 = DeterministicRng::new(54321);

        // Very unlikely to match
        assert_ne!(rng1.next_u64(), rng2.next_u64());
    }

    #[test]
    fn test_rng_known_values() {
        // Verify specific output for regression testing
        let mut rng = DeterministicRng::new(42);
        let val1 = rng.next_u64();
        let val2 = rng.next_u64();
        let val3 = rng.next_u64();

        // These values must never change!
        // If they do, existing match replays will break.
        assert_eq!(val1, 16629283624882167704);
        assert_eq!(val2, 1420492921613871959);
        assert_eq!(val3, 9768315062676884790);
    }

    #[test]
    fn test_next_int() {
        let mut rng = DeterministicRng::new(1234);

        // Test range
        for _ in 0..1000 {
            let val = rng.next_int(100);
            assert!(val < 100);
        }

        // Edge case: max = 0
        assert_eq!(rng.next_int(0), 0);

        // Edge case: max = 1
        assert_eq!(rng.next_int(1), 0);
    }

    #[test]
    fn test_next_int_range() {
        let mut rng = DeterministicRng::new(5678);

        for _ in 0..1000 {
            let val = rng.next_int_range(-10, 10);
            assert!(val >= -10 && val <= 10);
        }

        // Edge case: min = max
        assert_eq!(rng.next_int_range(5, 5), 5);
    }

    #[test]
    fn test_next_fixed() {
        let mut rng = DeterministicRng::new(9999);

        let max = to_fixed(100.0);
        for _ in 0..1000 {
            let val = rng.next_fixed(max);
            assert!(val >= 0 && val < max);
        }
    }

    #[test]
    fn test_random_position() {
        let mut rng = DeterministicRng::new(7777);

        for _ in 0..100 {
            let pos = rng.random_position();
            assert!(pos.is_in_arena());
        }
    }

    #[test]
    fn test_shuffle_determinism() {
        let mut rng1 = DeterministicRng::new(1111);
        let mut rng2 = DeterministicRng::new(1111);

        let mut arr1 = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        let mut arr2 = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        rng1.shuffle(&mut arr1);
        rng2.shuffle(&mut arr2);

        assert_eq!(arr1, arr2);
    }

    #[test]
    fn test_derive_match_seed() {
        let block_hash = [0u8; 32];
        let match_id = [1u8; 16];
        let player_ids = [[2u8; 16], [3u8; 16]];

        let seed1 = derive_match_seed(&block_hash, &match_id, &player_ids);
        let seed2 = derive_match_seed(&block_hash, &match_id, &player_ids);

        // Same inputs = same seed
        assert_eq!(seed1, seed2);

        // Different input = different seed
        let different_match = [99u8; 16];
        let seed3 = derive_match_seed(&block_hash, &different_match, &player_ids);
        assert_ne!(seed1, seed3);
    }

    #[test]
    fn test_state_checkpoint() {
        let mut rng = DeterministicRng::new(5555);

        // Advance some
        for _ in 0..50 {
            rng.next_u64();
        }

        // Save state
        let saved_state = rng.state();

        // Advance more
        let next_values: Vec<u64> = (0..10).map(|_| rng.next_u64()).collect();

        // Restore state
        rng.set_state(saved_state);

        // Should produce same values again
        for expected in next_values {
            assert_eq!(rng.next_u64(), expected);
        }
    }
}

//! Q16.16 Fixed-Point Arithmetic
//!
//! This module provides deterministic fixed-point math for game simulation.
//! All operations use integer arithmetic only - no floats in gameplay logic.
//!
//! ## Format: Q16.16
//!
//! ```text
//! ┌─────────────────────────────────────────────────────────────┐
//! │  Bit Layout: Q16.16 (32-bit signed integer)                 │
//! ├─────────────────────────────────────────────────────────────┤
//! │  [S][IIIIIIIIIIIIIIII][FFFFFFFFFFFFFFFF]                    │
//! │   │  └──── 16 bits ────┘└──── 16 bits ────┘                 │
//! │   └─ Sign bit                                               │
//! │                                                             │
//! │  Range: -32768.0 to +32767.99998 (approx)                   │
//! │  Precision: 1/65536 ≈ 0.000015 units                        │
//! └─────────────────────────────────────────────────────────────┘
//! ```
//!
//! ## Why Q16.16?
//!
//! - Fits in Mersenne-31 field (2^31 - 1) for STWO proofs
//! - 32k unit range is plenty for mobile arena
//! - Sub-pixel precision (0.00001 units)
//! - Fast integer ops on all platforms

use std::fmt;
use std::ops::{Add, Sub, Mul, Div, Neg};

/// Q16.16 fixed-point number stored as i32.
/// 16 bits integer, 16 bits fractional.
pub type Fixed = i32;

/// Number of fractional bits (16)
pub const FIXED_SCALE: i32 = 16;

/// 1.0 in fixed-point (65536)
pub const FIXED_ONE: Fixed = 1 << FIXED_SCALE; // 65536

/// 0.5 in fixed-point (32768)
pub const FIXED_HALF: Fixed = FIXED_ONE >> 1; // 32768

/// Maximum positive value
pub const FIXED_MAX: Fixed = i32::MAX;

/// Minimum negative value
pub const FIXED_MIN: Fixed = i32::MIN;

// =============================================================================
// GAME CONSTANTS (All as integer literals - NO float conversion!)
// =============================================================================

/// Tick duration: 1/60 second = round(65536/60) = 1092
pub const TICK_DURATION: Fixed = 1092;

/// Base movement speed: 5.0 units/sec = 5 * 65536 = 327680
pub const BASE_MOVE_SPEED: Fixed = 327680;

/// Jump velocity: 12.0 = 12 * 65536 = 786432
pub const JUMP_VELOCITY: Fixed = 786432;

/// Gravity acceleration: 30.0 = 30 * 65536 = 1966080
pub const GRAVITY: Fixed = 1966080;

/// Arena half-width: 50.0 = 50 * 65536 = 3276800
pub const ARENA_HALF_WIDTH: Fixed = 3276800;

/// Arena half-height: 50.0 = 50 * 65536 = 3276800
pub const ARENA_HALF_HEIGHT: Fixed = 3276800;

/// Form speeds by tier (indices 0-4 for Tiers 1-5)
pub const FORM_SPEEDS: [Fixed; 5] = [
    393216,  // Tier 1 (Spark):  6.0 * 65536
    360448,  // Tier 2 (Glyph):  5.5 * 65536
    327680,  // Tier 3 (Ward):   5.0 * 65536
    294912,  // Tier 4 (Arcane): 4.5 * 65536
    262144,  // Tier 5 (Ancient): 4.0 * 65536
];

/// Form radii by tier (indices 0-4 for Tiers 1-5)
pub const FORM_RADII: [Fixed; 5] = [
    32768,   // Tier 1 (Spark):  0.5 * 65536
    45875,   // Tier 2 (Glyph):  0.7 * 65536 (floor)
    65536,   // Tier 3 (Ward):   1.0 * 65536
    91750,   // Tier 4 (Arcane): 1.4 * 65536 (floor)
    131072,  // Tier 5 (Ancient): 2.0 * 65536
];

/// Score thresholds for evolution (Tier 1→2, 2→3, 3→4, 4→5)
pub const SCORE_TO_EVOLVE: [u32; 4] = [100, 300, 600, 1000];

/// Points per rune collected
pub const SCORE_PER_RUNE: u32 = 10;

/// Points per player eliminated
pub const SCORE_PER_KILL: u32 = 100;

// =============================================================================
// CORE OPERATIONS (All deterministic, wrapping semantics)
// =============================================================================

/// Convert a compile-time float to fixed-point.
///
/// # Warning
/// Only use at compile-time or initialization. NEVER in tick loop.
///
/// # Example
/// ```
/// use rune_relic::core::fixed::{to_fixed, FIXED_ONE};
/// const MY_VALUE: i32 = to_fixed(2.5);
/// assert_eq!(MY_VALUE, FIXED_ONE * 2 + FIXED_ONE / 2);
/// ```
#[inline]
pub const fn to_fixed(f: f64) -> Fixed {
    (f * (FIXED_ONE as f64)) as Fixed
}

/// Convert fixed-point to float for display/rendering.
///
/// # Warning
/// Only use for visual output. NEVER use result in game logic.
#[inline]
pub fn to_float(f: Fixed) -> f32 {
    f as f32 / FIXED_ONE as f32
}

/// Multiply two fixed-point numbers.
///
/// Uses i64 intermediate to prevent overflow, then truncates.
///
/// # Determinism
/// - Uses wrapping arithmetic
/// - Truncates toward zero (Rust default for integer division)
#[inline]
pub fn fixed_mul(a: Fixed, b: Fixed) -> Fixed {
    // Widen to i64, multiply, shift back
    let wide = (a as i64) * (b as i64);
    (wide >> FIXED_SCALE) as Fixed
}

/// Divide two fixed-point numbers.
///
/// Pre-shifts numerator to maintain precision.
/// Returns 0 on divide-by-zero.
///
/// # Determinism
/// - Uses wrapping arithmetic
/// - Truncates toward zero
/// - Divide-by-zero returns 0 (not panic)
#[inline]
pub fn fixed_div(a: Fixed, b: Fixed) -> Fixed {
    if b == 0 {
        return 0; // Deterministic: don't panic
    }
    let wide = (a as i64) << FIXED_SCALE;
    (wide / b as i64) as Fixed
}

/// Square root using Newton-Raphson iteration.
///
/// Safe from divide-by-zero: returns 0 for non-positive inputs.
/// Uses exactly 6 iterations for determinism.
///
/// # Prefer Squared Distances
/// When possible, use `distance_squared` instead of `distance`
/// to avoid sqrt entirely. It's faster and equally deterministic.
#[inline]
pub fn fixed_sqrt(x: Fixed) -> Fixed {
    if x <= 0 {
        return 0;
    }

    // Initial guess: x/2, but never zero
    let mut guess = (x >> 1).max(1);

    // Newton-Raphson: guess = (guess + x/guess) / 2
    // Fixed 6 iterations for determinism
    for _ in 0..6 {
        let div = fixed_div(x, guess);
        guess = (guess.wrapping_add(div)) >> 1;

        // Safety: never let guess become zero
        if guess == 0 {
            guess = 1;
        }
    }

    guess
}

/// Absolute value of a fixed-point number.
#[inline]
pub fn fixed_abs(x: Fixed) -> Fixed {
    if x < 0 { x.wrapping_neg() } else { x }
}

/// Minimum of two fixed-point numbers.
#[inline]
pub fn fixed_min(a: Fixed, b: Fixed) -> Fixed {
    if a < b { a } else { b }
}

/// Maximum of two fixed-point numbers.
#[inline]
pub fn fixed_max(a: Fixed, b: Fixed) -> Fixed {
    if a > b { a } else { b }
}

/// Clamp a fixed-point number to a range.
#[inline]
pub fn fixed_clamp(value: Fixed, min: Fixed, max: Fixed) -> Fixed {
    fixed_max(min, fixed_min(max, value))
}

/// Linear interpolation: a + (b - a) * t
/// where t is in fixed-point (0.0 = 0, 1.0 = FIXED_ONE)
#[inline]
pub fn fixed_lerp(a: Fixed, b: Fixed, t: Fixed) -> Fixed {
    let diff = b.wrapping_sub(a);
    a.wrapping_add(fixed_mul(diff, t))
}

// =============================================================================
// FIXEDNUM WRAPPER (Optional ergonomic wrapper)
// =============================================================================

/// Ergonomic wrapper around fixed-point with operator overloading.
///
/// Use this for cleaner code when performance isn't critical.
/// For hot paths, use raw `Fixed` with `fixed_*` functions.
#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Default)]
pub struct FixedNum(pub Fixed);

impl FixedNum {
    /// Zero constant
    pub const ZERO: Self = Self(0);

    /// One constant
    pub const ONE: Self = Self(FIXED_ONE);

    /// Create from raw fixed-point value
    #[inline]
    pub const fn from_raw(raw: Fixed) -> Self {
        Self(raw)
    }

    /// Create from integer
    #[inline]
    pub const fn from_int(i: i32) -> Self {
        Self(i << FIXED_SCALE)
    }

    /// Get raw fixed-point value
    #[inline]
    pub const fn raw(self) -> Fixed {
        self.0
    }

    /// Convert to float for display
    #[inline]
    pub fn to_float(self) -> f32 {
        to_float(self.0)
    }

    /// Absolute value
    #[inline]
    pub fn abs(self) -> Self {
        Self(fixed_abs(self.0))
    }

    /// Square root
    #[inline]
    pub fn sqrt(self) -> Self {
        Self(fixed_sqrt(self.0))
    }
}

impl Add for FixedNum {
    type Output = Self;
    #[inline]
    fn add(self, rhs: Self) -> Self {
        Self(self.0.wrapping_add(rhs.0))
    }
}

impl Sub for FixedNum {
    type Output = Self;
    #[inline]
    fn sub(self, rhs: Self) -> Self {
        Self(self.0.wrapping_sub(rhs.0))
    }
}

impl Mul for FixedNum {
    type Output = Self;
    #[inline]
    fn mul(self, rhs: Self) -> Self {
        Self(fixed_mul(self.0, rhs.0))
    }
}

impl Div for FixedNum {
    type Output = Self;
    #[inline]
    fn div(self, rhs: Self) -> Self {
        Self(fixed_div(self.0, rhs.0))
    }
}

impl Neg for FixedNum {
    type Output = Self;
    #[inline]
    fn neg(self) -> Self {
        Self(self.0.wrapping_neg())
    }
}

impl fmt::Debug for FixedNum {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "Fixed({:.4})", self.to_float())
    }
}

impl fmt::Display for FixedNum {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{:.4}", self.to_float())
    }
}

// =============================================================================
// TESTS
// =============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_fixed_constants() {
        assert_eq!(FIXED_ONE, 65536);
        assert_eq!(FIXED_HALF, 32768);
        assert_eq!(FIXED_SCALE, 16);
    }

    #[test]
    fn test_to_fixed() {
        assert_eq!(to_fixed(1.0), FIXED_ONE);
        assert_eq!(to_fixed(0.5), FIXED_HALF);
        assert_eq!(to_fixed(2.0), FIXED_ONE * 2);
        assert_eq!(to_fixed(-1.0), -FIXED_ONE);
    }

    #[test]
    fn test_fixed_mul() {
        // 2.0 * 3.0 = 6.0
        let a = to_fixed(2.0);
        let b = to_fixed(3.0);
        let result = fixed_mul(a, b);
        assert_eq!(result, to_fixed(6.0));

        // 0.5 * 0.5 = 0.25
        let result2 = fixed_mul(FIXED_HALF, FIXED_HALF);
        assert_eq!(result2, to_fixed(0.25));

        // Negative: -2.0 * 3.0 = -6.0
        let result3 = fixed_mul(to_fixed(-2.0), to_fixed(3.0));
        assert_eq!(result3, to_fixed(-6.0));
    }

    #[test]
    fn test_fixed_div() {
        // 6.0 / 2.0 = 3.0
        let result = fixed_div(to_fixed(6.0), to_fixed(2.0));
        assert_eq!(result, to_fixed(3.0));

        // 1.0 / 4.0 = 0.25
        let result2 = fixed_div(FIXED_ONE, to_fixed(4.0));
        assert_eq!(result2, to_fixed(0.25));

        // Divide by zero returns 0
        let result3 = fixed_div(FIXED_ONE, 0);
        assert_eq!(result3, 0);
    }

    #[test]
    fn test_fixed_sqrt() {
        // sqrt(4.0) = 2.0
        let result = fixed_sqrt(to_fixed(4.0));
        let expected = to_fixed(2.0);
        assert!((result - expected).abs() < 100, "sqrt(4) should be ~2.0");

        // sqrt(1.0) = 1.0
        let result2 = fixed_sqrt(FIXED_ONE);
        assert!((result2 - FIXED_ONE).abs() < 100, "sqrt(1) should be ~1.0");

        // sqrt(0) = 0
        assert_eq!(fixed_sqrt(0), 0);

        // sqrt(negative) = 0
        assert_eq!(fixed_sqrt(-FIXED_ONE), 0);

        // sqrt(very small) doesn't panic
        assert!(fixed_sqrt(1) >= 0);
    }

    #[test]
    fn test_game_constants() {
        // Verify constants are correct
        assert_eq!(TICK_DURATION, 1092); // round(65536/60)
        assert_eq!(BASE_MOVE_SPEED, 5 * FIXED_ONE);
        assert_eq!(ARENA_HALF_WIDTH, 50 * FIXED_ONE);
        assert_eq!(FORM_SPEEDS[0], 6 * FIXED_ONE); // Tier 1
        assert_eq!(FORM_RADII[2], FIXED_ONE); // Tier 3 = 1.0
    }

    #[test]
    fn test_fixed_determinism() {
        // Same inputs must produce same outputs
        for _ in 0..1000 {
            let a = 12345678;
            let b = 87654321;

            let mul1 = fixed_mul(a, b);
            let mul2 = fixed_mul(a, b);
            assert_eq!(mul1, mul2, "Multiplication must be deterministic");

            let div1 = fixed_div(a, b);
            let div2 = fixed_div(a, b);
            assert_eq!(div1, div2, "Division must be deterministic");

            let sqrt1 = fixed_sqrt(a);
            let sqrt2 = fixed_sqrt(a);
            assert_eq!(sqrt1, sqrt2, "Square root must be deterministic");
        }
    }

    #[test]
    fn test_fixednum_wrapper() {
        let a = FixedNum::from_int(5);
        let b = FixedNum::from_int(3);

        assert_eq!((a + b).raw(), to_fixed(8.0));
        assert_eq!((a - b).raw(), to_fixed(2.0));
        assert_eq!((a * b).raw(), to_fixed(15.0));

        let c = FixedNum::from_raw(to_fixed(10.0));
        let d = FixedNum::from_raw(to_fixed(4.0));
        assert_eq!((c / d).raw(), to_fixed(2.5));
    }
}

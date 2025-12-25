//! Fixed-Point 2D Vector
//!
//! Deterministic 2D vector operations for game physics.
//! All operations use fixed-point arithmetic.

use std::fmt;
use std::ops::{Add, Sub, Neg};
use serde::{Serialize, Deserialize};

use super::fixed::{
    Fixed, FIXED_ONE, FIXED_SCALE,
    fixed_mul, fixed_div, fixed_sqrt, fixed_clamp,
    ARENA_HALF_WIDTH, ARENA_HALF_HEIGHT,
};

/// 2D vector with fixed-point components.
#[derive(Clone, Copy, PartialEq, Eq, Hash, Default, Serialize, Deserialize)]
pub struct FixedVec2 {
    /// X component (Q16.16 fixed-point)
    pub x: Fixed,
    /// Y component (Q16.16 fixed-point)
    pub y: Fixed,
}

impl FixedVec2 {
    /// Zero vector
    pub const ZERO: Self = Self { x: 0, y: 0 };

    /// Unit vector pointing right (+X)
    pub const RIGHT: Self = Self { x: FIXED_ONE, y: 0 };

    /// Unit vector pointing up (+Y)
    pub const UP: Self = Self { x: 0, y: FIXED_ONE };

    /// Unit vector pointing left (-X)
    pub const LEFT: Self = Self { x: -FIXED_ONE, y: 0 };

    /// Unit vector pointing down (-Y)
    pub const DOWN: Self = Self { x: 0, y: -FIXED_ONE };

    /// Create a new vector from fixed-point components.
    #[inline]
    pub const fn new(x: Fixed, y: Fixed) -> Self {
        Self { x, y }
    }

    /// Create a vector from integer components.
    #[inline]
    pub const fn from_ints(x: i32, y: i32) -> Self {
        Self {
            x: x << FIXED_SCALE,
            y: y << FIXED_SCALE,
        }
    }

    /// Add another vector.
    #[inline]
    pub fn add(self, other: Self) -> Self {
        Self {
            x: self.x.wrapping_add(other.x),
            y: self.y.wrapping_add(other.y),
        }
    }

    /// Subtract another vector.
    #[inline]
    pub fn sub(self, other: Self) -> Self {
        Self {
            x: self.x.wrapping_sub(other.x),
            y: self.y.wrapping_sub(other.y),
        }
    }

    /// Scale by a fixed-point scalar.
    #[inline]
    pub fn scale(self, scalar: Fixed) -> Self {
        Self {
            x: fixed_mul(self.x, scalar),
            y: fixed_mul(self.y, scalar),
        }
    }

    /// Scale by an integer scalar (faster than fixed multiply).
    #[inline]
    pub fn scale_int(self, scalar: i32) -> Self {
        Self {
            x: self.x.wrapping_mul(scalar),
            y: self.y.wrapping_mul(scalar),
        }
    }

    /// Divide by a fixed-point scalar.
    #[inline]
    pub fn div_scalar(self, scalar: Fixed) -> Self {
        Self {
            x: fixed_div(self.x, scalar),
            y: fixed_div(self.y, scalar),
        }
    }

    /// Squared length (avoids sqrt - prefer this for comparisons).
    #[inline]
    pub fn length_squared(self) -> Fixed {
        fixed_mul(self.x, self.x)
            .wrapping_add(fixed_mul(self.y, self.y))
    }

    /// Length (magnitude). Prefer `length_squared` when possible.
    #[inline]
    pub fn length(self) -> Fixed {
        fixed_sqrt(self.length_squared())
    }

    /// Squared distance to another point.
    #[inline]
    pub fn distance_squared(self, other: Self) -> Fixed {
        let dx = self.x.wrapping_sub(other.x);
        let dy = self.y.wrapping_sub(other.y);
        fixed_mul(dx, dx).wrapping_add(fixed_mul(dy, dy))
    }

    /// Distance to another point. Prefer `distance_squared` when possible.
    #[inline]
    pub fn distance(self, other: Self) -> Fixed {
        fixed_sqrt(self.distance_squared(other))
    }

    /// Normalize to unit length.
    /// Returns ZERO if length is zero.
    #[inline]
    pub fn normalize(self) -> Self {
        let len = self.length();
        if len == 0 {
            return Self::ZERO;
        }
        self.div_scalar(len)
    }

    /// Dot product with another vector.
    #[inline]
    pub fn dot(self, other: Self) -> Fixed {
        fixed_mul(self.x, other.x)
            .wrapping_add(fixed_mul(self.y, other.y))
    }

    /// 2D cross product (returns scalar z-component).
    /// Positive if other is counter-clockwise from self.
    #[inline]
    pub fn cross(self, other: Self) -> Fixed {
        fixed_mul(self.x, other.y)
            .wrapping_sub(fixed_mul(self.y, other.x))
    }

    /// Clamp both components to a range.
    #[inline]
    pub fn clamp(self, min: Fixed, max: Fixed) -> Self {
        Self {
            x: fixed_clamp(self.x, min, max),
            y: fixed_clamp(self.y, min, max),
        }
    }

    /// Clamp to arena bounds.
    #[inline]
    pub fn clamp_to_arena(self) -> Self {
        Self {
            x: fixed_clamp(self.x, -ARENA_HALF_WIDTH, ARENA_HALF_WIDTH),
            y: fixed_clamp(self.y, -ARENA_HALF_HEIGHT, ARENA_HALF_HEIGHT),
        }
    }

    /// Check if position is within arena bounds.
    #[inline]
    pub fn is_in_arena(self) -> bool {
        self.x >= -ARENA_HALF_WIDTH
            && self.x <= ARENA_HALF_WIDTH
            && self.y >= -ARENA_HALF_HEIGHT
            && self.y <= ARENA_HALF_HEIGHT
    }

    /// Linear interpolation between two vectors.
    /// t = 0 returns self, t = FIXED_ONE returns other.
    #[inline]
    pub fn lerp(self, other: Self, t: Fixed) -> Self {
        let dx = other.x.wrapping_sub(self.x);
        let dy = other.y.wrapping_sub(self.y);
        Self {
            x: self.x.wrapping_add(fixed_mul(dx, t)),
            y: self.y.wrapping_add(fixed_mul(dy, t)),
        }
    }

    /// Rotate 90 degrees counter-clockwise.
    #[inline]
    pub fn perpendicular(self) -> Self {
        Self {
            x: self.y.wrapping_neg(),
            y: self.x,
        }
    }

    /// Negate both components.
    #[inline]
    pub fn negate(self) -> Self {
        Self {
            x: self.x.wrapping_neg(),
            y: self.y.wrapping_neg(),
        }
    }

    /// Convert to float tuple for rendering.
    #[inline]
    pub fn to_floats(self) -> (f32, f32) {
        (
            self.x as f32 / FIXED_ONE as f32,
            self.y as f32 / FIXED_ONE as f32,
        )
    }
}

// Operator overloads for ergonomics
impl Add for FixedVec2 {
    type Output = Self;
    #[inline]
    fn add(self, rhs: Self) -> Self {
        self.add(rhs)
    }
}

impl Sub for FixedVec2 {
    type Output = Self;
    #[inline]
    fn sub(self, rhs: Self) -> Self {
        self.sub(rhs)
    }
}

impl Neg for FixedVec2 {
    type Output = Self;
    #[inline]
    fn neg(self) -> Self {
        self.negate()
    }
}

impl fmt::Debug for FixedVec2 {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let (fx, fy) = self.to_floats();
        write!(f, "Vec2({:.3}, {:.3})", fx, fy)
    }
}

impl fmt::Display for FixedVec2 {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let (fx, fy) = self.to_floats();
        write!(f, "({:.3}, {:.3})", fx, fy)
    }
}

// =============================================================================
// TESTS
// =============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use crate::core::fixed::to_fixed;

    #[test]
    fn test_vec2_constants() {
        assert_eq!(FixedVec2::ZERO.x, 0);
        assert_eq!(FixedVec2::ZERO.y, 0);
        assert_eq!(FixedVec2::RIGHT.x, FIXED_ONE);
        assert_eq!(FixedVec2::UP.y, FIXED_ONE);
    }

    #[test]
    fn test_vec2_add() {
        let a = FixedVec2::new(to_fixed(3.0), to_fixed(4.0));
        let b = FixedVec2::new(to_fixed(1.0), to_fixed(2.0));
        let result = a + b;
        assert_eq!(result.x, to_fixed(4.0));
        assert_eq!(result.y, to_fixed(6.0));
    }

    #[test]
    fn test_vec2_sub() {
        let a = FixedVec2::new(to_fixed(5.0), to_fixed(7.0));
        let b = FixedVec2::new(to_fixed(2.0), to_fixed(3.0));
        let result = a - b;
        assert_eq!(result.x, to_fixed(3.0));
        assert_eq!(result.y, to_fixed(4.0));
    }

    #[test]
    fn test_vec2_scale() {
        let v = FixedVec2::new(to_fixed(2.0), to_fixed(3.0));
        let result = v.scale(to_fixed(2.0));
        assert_eq!(result.x, to_fixed(4.0));
        assert_eq!(result.y, to_fixed(6.0));
    }

    #[test]
    fn test_vec2_length() {
        // 3-4-5 triangle
        let v = FixedVec2::new(to_fixed(3.0), to_fixed(4.0));
        let len_sq = v.length_squared();
        assert_eq!(len_sq, to_fixed(25.0));

        let len = v.length();
        let expected = to_fixed(5.0);
        // Allow small error due to sqrt approximation
        assert!((len - expected).abs() < 200, "Length should be ~5.0");
    }

    #[test]
    fn test_vec2_distance() {
        let a = FixedVec2::new(to_fixed(0.0), to_fixed(0.0));
        let b = FixedVec2::new(to_fixed(3.0), to_fixed(4.0));
        let dist_sq = a.distance_squared(b);
        assert_eq!(dist_sq, to_fixed(25.0));
    }

    #[test]
    fn test_vec2_normalize() {
        let v = FixedVec2::new(to_fixed(3.0), to_fixed(4.0));
        let norm = v.normalize();

        // Should have length ~1.0
        let len = norm.length();
        assert!((len - FIXED_ONE).abs() < 200, "Normalized length should be ~1.0");

        // Zero vector normalizes to zero
        let zero_norm = FixedVec2::ZERO.normalize();
        assert_eq!(zero_norm, FixedVec2::ZERO);
    }

    #[test]
    fn test_vec2_dot() {
        let a = FixedVec2::new(to_fixed(2.0), to_fixed(3.0));
        let b = FixedVec2::new(to_fixed(4.0), to_fixed(5.0));
        let dot = a.dot(b);
        // 2*4 + 3*5 = 8 + 15 = 23
        assert_eq!(dot, to_fixed(23.0));
    }

    #[test]
    fn test_vec2_clamp_to_arena() {
        // Inside bounds - unchanged
        let inside = FixedVec2::new(to_fixed(10.0), to_fixed(20.0));
        assert_eq!(inside.clamp_to_arena(), inside);

        // Outside bounds - clamped
        let outside = FixedVec2::new(to_fixed(100.0), to_fixed(-100.0));
        let clamped = outside.clamp_to_arena();
        assert_eq!(clamped.x, ARENA_HALF_WIDTH);
        assert_eq!(clamped.y, -ARENA_HALF_HEIGHT);
    }

    #[test]
    fn test_vec2_determinism() {
        let a = FixedVec2::new(12345678, 87654321);
        let b = FixedVec2::new(11111111, 22222222);

        for _ in 0..1000 {
            let add1 = a + b;
            let add2 = a + b;
            assert_eq!(add1, add2);

            let len1 = a.length();
            let len2 = a.length();
            assert_eq!(len1, len2);
        }
    }
}

//! Input Capture and Normalization
//!
//! Handles player input with deterministic normalization.
//! Uses lookup table (MOVE_LUT) for exact i8 to Fixed conversion.

use serde::{Serialize, Deserialize};
use crate::core::fixed::Fixed;
use crate::core::vec2::FixedVec2;
use crate::game::state::PlayerId;

// =============================================================================
// MOVE LOOKUP TABLE (Critical for Determinism)
// =============================================================================

/// Lookup table for converting i8 move input to Fixed.
///
/// # Why a Lookup Table?
///
/// Converting i8 [-127..+127] to Fixed [-1.0..+1.0] requires:
/// `value * 65536 / 127 = value * 516.0...`
///
/// 516.0 is not an integer, so we use floor division:
/// `(value * 65536) / 127`
///
/// This lookup table precomputes all 256 possible values for
/// deterministic, fast conversion.
///
/// # Special Values
///
/// - Index 128 (-128 as i8) = 0 (represents "no input" / joystick released)
pub static MOVE_LUT: [Fixed; 256] = {
    let mut lut = [0i32; 256];
    let mut i = 0i32;
    while i < 256 {
        // Treat as signed: 0..127 = positive, 128..255 = negative (-128..-1)
        let signed = if i < 128 { i } else { i - 256 };

        // -128 is reserved for "no input" -> map to 0
        if signed == -128 {
            lut[i as usize] = 0;
        } else {
            // Scale [-127..+127] to [-65536..+65536] (FIXED_ONE)
            // Floor division: (signed * 65536) / 127
            lut[i as usize] = (signed * 65536) / 127;
        }
        i += 1;
    }
    lut
};

/// Convert i8 move input to Fixed using lookup table.
#[inline]
pub fn move_to_fixed(input: i8) -> Fixed {
    MOVE_LUT[(input as u8) as usize]
}

// =============================================================================
// INPUT TYPES
// =============================================================================

/// Raw input state for a single frame.
///
/// This is the minimal input that affects game state.
/// NO tick field - tick is stored separately for compression.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[repr(C)]
pub struct InputFrame {
    /// Movement X direction: -127 (left) to +127 (right)
    /// -128 = joystick released / no input
    pub move_x: i8,

    /// Movement Y direction: -127 (down) to +127 (up)
    /// -128 = joystick released / no input
    pub move_y: i8,

    /// Action flags (packed bits):
    /// - Bit 0: Jump pressed this frame
    /// - Bit 1: Ability activated this frame
    /// - Bit 2-7: Reserved
    pub flags: u8,
}

impl InputFrame {
    /// Size in bytes
    pub const SIZE: usize = 3;

    /// Special value indicating no input (joystick released)
    pub const NO_INPUT: i8 = -128;

    /// Jump flag bit
    pub const FLAG_JUMP: u8 = 0x01;

    /// Ability flag bit
    pub const FLAG_ABILITY: u8 = 0x02;

    /// Create a new empty input frame.
    pub const fn new() -> Self {
        Self {
            move_x: Self::NO_INPUT,
            move_y: Self::NO_INPUT,
            flags: 0,
        }
    }

    /// Create input with movement direction.
    pub const fn with_movement(move_x: i8, move_y: i8) -> Self {
        Self {
            move_x,
            move_y,
            flags: 0,
        }
    }

    /// Get movement as normalized FixedVec2.
    ///
    /// Uses MOVE_LUT for deterministic conversion.
    #[inline]
    pub fn move_direction(&self) -> FixedVec2 {
        FixedVec2 {
            x: move_to_fixed(self.move_x),
            y: move_to_fixed(self.move_y),
        }
    }

    /// Check if jump was pressed this frame.
    #[inline]
    pub fn jump_pressed(&self) -> bool {
        self.flags & Self::FLAG_JUMP != 0
    }

    /// Check if ability was activated this frame.
    #[inline]
    pub fn ability_pressed(&self) -> bool {
        self.flags & Self::FLAG_ABILITY != 0
    }

    /// Check if this is an idle frame (no input).
    #[inline]
    pub fn is_idle(&self) -> bool {
        self.move_x == Self::NO_INPUT
            && self.move_y == Self::NO_INPUT
            && self.flags == 0
    }

    /// Check if input has any movement.
    #[inline]
    pub fn has_movement(&self) -> bool {
        self.move_x != Self::NO_INPUT || self.move_y != Self::NO_INPUT
    }

    /// Set jump flag.
    #[inline]
    pub fn set_jump(&mut self, pressed: bool) {
        if pressed {
            self.flags |= Self::FLAG_JUMP;
        } else {
            self.flags &= !Self::FLAG_JUMP;
        }
    }

    /// Set ability flag.
    #[inline]
    pub fn set_ability(&mut self, pressed: bool) {
        if pressed {
            self.flags |= Self::FLAG_ABILITY;
        } else {
            self.flags &= !Self::FLAG_ABILITY;
        }
    }
}

/// Input with tick for network transmission.
///
/// Used when sending inputs over the network, where ordering matters.
#[derive(Clone, Copy, Debug, Serialize, Deserialize)]
#[repr(C, packed)]
pub struct NetworkInput {
    /// Tick when this input was captured
    pub tick: u32,
    /// The input frame
    pub frame: InputFrame,
    /// Padding for alignment
    pub _pad: u8,
}

impl NetworkInput {
    /// Size in bytes
    pub const SIZE: usize = 8;

    /// Create from tick and frame.
    pub fn new(tick: u32, frame: InputFrame) -> Self {
        Self {
            tick,
            frame,
            _pad: 0,
        }
    }
}

/// Delta-compressed input for proof generation.
///
/// Only stored when input CHANGES (not every tick).
/// This significantly reduces proof size.
#[derive(Clone, Copy, Debug, Serialize, Deserialize)]
pub struct InputDelta {
    /// Tick when this input state began
    pub tick: u32,
    /// The new input state
    pub frame: InputFrame,
}

impl InputDelta {
    /// Size in bytes (approximate)
    pub const SIZE: usize = 8;

    /// Create new delta entry.
    pub fn new(tick: u32, frame: InputFrame) -> Self {
        Self { tick, frame }
    }
}

// =============================================================================
// INPUT BUFFER
// =============================================================================

/// Complete input recording for one player in one match.
///
/// Used for:
/// - Replay playback
/// - Anti-cheat analysis
/// - BitSage proof generation
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct PlayerInputBuffer {
    /// Player identifier
    pub player_id: PlayerId,

    /// Match identifier
    pub match_id: [u8; 16],

    /// RNG seed used for this match
    pub rng_seed: u64,

    /// Starting tick (usually 0)
    pub start_tick: u32,

    /// Ending tick (usually 5400 for 90s @ 60Hz)
    pub end_tick: u32,

    /// Delta-compressed input data.
    /// Only stores ticks where input CHANGED.
    deltas: Vec<InputDelta>,

    /// Last recorded input (for delta comparison)
    #[serde(skip)]
    last_frame: InputFrame,
}

impl PlayerInputBuffer {
    /// Create a new input buffer for a player.
    pub fn new(player_id: PlayerId, match_id: [u8; 16], rng_seed: u64) -> Self {
        Self {
            player_id,
            match_id,
            rng_seed,
            start_tick: 0,
            end_tick: 0,
            deltas: Vec::with_capacity(512), // ~5 changes/sec * 90 sec
            last_frame: InputFrame::new(),
        }
    }

    /// Record input for a tick.
    ///
    /// Only stores if input changed from previous frame.
    pub fn record(&mut self, tick: u32, frame: InputFrame) {
        // Update end tick
        self.end_tick = tick;

        // Only store if changed
        if frame != self.last_frame {
            self.deltas.push(InputDelta::new(tick, frame));
            self.last_frame = frame;
        }
    }

    /// Get input at a specific tick.
    ///
    /// Uses binary search for efficiency.
    pub fn get_input_at(&self, tick: u32) -> InputFrame {
        if self.deltas.is_empty() {
            return InputFrame::new();
        }

        // Binary search for the last delta at or before this tick
        let idx = self.deltas.partition_point(|d| d.tick <= tick);

        if idx == 0 {
            // Before first delta - return idle
            InputFrame::new()
        } else {
            // Return the most recent delta before this tick
            self.deltas[idx - 1].frame
        }
    }

    /// Get all deltas (for serialization/proof).
    pub fn deltas(&self) -> &[InputDelta] {
        &self.deltas
    }

    /// Number of delta entries.
    pub fn delta_count(&self) -> usize {
        self.deltas.len()
    }

    /// Estimated size in bytes.
    ///
    /// Typical match: ~450 deltas * 8 bytes = 3.6 KB per player
    pub fn estimated_size(&self) -> usize {
        48 + (self.deltas.len() * InputDelta::SIZE)
    }

    /// Finalize the buffer (call at match end).
    pub fn finalize(&mut self, end_tick: u32) {
        self.end_tick = end_tick;
    }

    /// Create iterator over all inputs for replay.
    pub fn replay_iter(&self) -> ReplayIterator<'_> {
        ReplayIterator {
            buffer: self,
            current_tick: self.start_tick,
            delta_idx: 0,
            current_frame: InputFrame::new(),
        }
    }
}

/// Iterator for replaying inputs tick-by-tick.
pub struct ReplayIterator<'a> {
    buffer: &'a PlayerInputBuffer,
    current_tick: u32,
    delta_idx: usize,
    current_frame: InputFrame,
}

impl<'a> Iterator for ReplayIterator<'a> {
    type Item = (u32, InputFrame);

    fn next(&mut self) -> Option<Self::Item> {
        if self.current_tick > self.buffer.end_tick {
            return None;
        }

        // Check if we need to update current frame
        while self.delta_idx < self.buffer.deltas.len() {
            let delta = &self.buffer.deltas[self.delta_idx];
            if delta.tick <= self.current_tick {
                self.current_frame = delta.frame;
                self.delta_idx += 1;
            } else {
                break;
            }
        }

        let result = (self.current_tick, self.current_frame);
        self.current_tick += 1;
        Some(result)
    }
}

// =============================================================================
// TESTS
// =============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use crate::core::fixed::FIXED_ONE;

    #[test]
    fn test_move_lut_values() {
        // Check key values
        assert_eq!(MOVE_LUT[0], 0); // 0 as u8 = 0 as i8 = 0
        assert_eq!(MOVE_LUT[127], 65536); // 127 -> +1.0
        assert_eq!(MOVE_LUT[129], -65536); // 129 as u8 = -127 as i8 -> -1.0
        assert_eq!(MOVE_LUT[128], 0); // 128 as u8 = -128 as i8 -> no input

        // Check symmetry
        for i in 1..=127 {
            let pos = MOVE_LUT[i as usize];
            let neg = MOVE_LUT[(256 - i) as usize];
            assert_eq!(pos, -neg, "LUT should be symmetric for {}", i);
        }
    }

    #[test]
    fn test_move_to_fixed() {
        assert_eq!(move_to_fixed(0), 0);
        assert_eq!(move_to_fixed(127), FIXED_ONE);
        assert_eq!(move_to_fixed(-127), -FIXED_ONE);
        assert_eq!(move_to_fixed(-128), 0); // No input
    }

    #[test]
    fn test_input_frame_flags() {
        let mut frame = InputFrame::new();
        assert!(!frame.jump_pressed());
        assert!(!frame.ability_pressed());

        frame.set_jump(true);
        assert!(frame.jump_pressed());
        assert!(!frame.ability_pressed());

        frame.set_ability(true);
        assert!(frame.jump_pressed());
        assert!(frame.ability_pressed());

        frame.set_jump(false);
        assert!(!frame.jump_pressed());
        assert!(frame.ability_pressed());
    }

    #[test]
    fn test_input_frame_movement() {
        let frame = InputFrame::with_movement(127, -127);
        let dir = frame.move_direction();

        assert_eq!(dir.x, FIXED_ONE);
        assert_eq!(dir.y, -FIXED_ONE);
    }

    #[test]
    fn test_input_buffer_delta_compression() {
        let player_id = PlayerId::new([0u8; 16]);
        let mut buffer = PlayerInputBuffer::new(player_id, [0u8; 16], 12345);

        // Record same input multiple times
        let frame = InputFrame::with_movement(100, 50);
        buffer.record(0, frame);
        buffer.record(1, frame);
        buffer.record(2, frame);
        buffer.record(3, frame);

        // Should only have 1 delta (input didn't change)
        assert_eq!(buffer.delta_count(), 1);

        // Change input
        let frame2 = InputFrame::with_movement(-100, -50);
        buffer.record(4, frame2);

        // Now should have 2 deltas
        assert_eq!(buffer.delta_count(), 2);
    }

    #[test]
    fn test_input_buffer_get_at() {
        let player_id = PlayerId::new([0u8; 16]);
        let mut buffer = PlayerInputBuffer::new(player_id, [0u8; 16], 12345);

        let frame1 = InputFrame::with_movement(50, 0);
        let frame2 = InputFrame::with_movement(-50, 0);
        let frame3 = InputFrame::with_movement(0, 100);

        buffer.record(10, frame1);
        buffer.record(20, frame2);
        buffer.record(30, frame3);

        // Before first delta
        assert!(buffer.get_input_at(5).is_idle());

        // At first delta
        assert_eq!(buffer.get_input_at(10), frame1);

        // Between deltas
        assert_eq!(buffer.get_input_at(15), frame1);
        assert_eq!(buffer.get_input_at(25), frame2);

        // At and after last delta
        assert_eq!(buffer.get_input_at(30), frame3);
        assert_eq!(buffer.get_input_at(100), frame3);
    }

    #[test]
    fn test_replay_iterator() {
        let player_id = PlayerId::new([0u8; 16]);
        let mut buffer = PlayerInputBuffer::new(player_id, [0u8; 16], 12345);

        buffer.record(0, InputFrame::with_movement(10, 0));
        buffer.record(3, InputFrame::with_movement(20, 0));
        buffer.finalize(5);

        let frames: Vec<_> = buffer.replay_iter().collect();

        assert_eq!(frames.len(), 6); // Ticks 0-5
        assert_eq!(frames[0].1.move_x, 10);
        assert_eq!(frames[1].1.move_x, 10);
        assert_eq!(frames[2].1.move_x, 10);
        assert_eq!(frames[3].1.move_x, 20);
        assert_eq!(frames[4].1.move_x, 20);
        assert_eq!(frames[5].1.move_x, 20);
    }

    #[test]
    fn test_input_buffer_size_estimate() {
        let player_id = PlayerId::new([0u8; 16]);
        let mut buffer = PlayerInputBuffer::new(player_id, [0u8; 16], 12345);

        // Simulate 90 seconds at ~5 changes/second
        for i in 0..450 {
            let move_val = ((i % 255) as i32 - 127) as i8;
            let frame = InputFrame::with_movement(move_val, 0);
            buffer.record((i * 12) as u32, frame); // Every 12 ticks = 5/sec
        }

        // Should be under 5KB
        assert!(buffer.estimated_size() < 5000);
    }
}

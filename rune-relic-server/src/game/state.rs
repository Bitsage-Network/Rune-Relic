//! Game State Definitions
//!
//! All state types for match simulation.
//! Uses BTreeMap for deterministic iteration order.

use std::collections::BTreeMap;
use serde::{Serialize, Deserialize};

use crate::core::fixed::{
    Fixed, FIXED_ONE,
    FORM_SPEEDS, FORM_RADII, SCORE_TO_EVOLVE,
    ARENA_HALF_WIDTH, ARENA_HALF_HEIGHT,
};
use crate::core::vec2::FixedVec2;
use crate::core::rng::DeterministicRng;
use crate::core::hash::{StateHash, StateHasher, compute_state_hash};
use crate::game::events::GameEvent;

// =============================================================================
// PLAYER ID
// =============================================================================

/// Unique player identifier (UUID as bytes).
///
/// Implements Ord for deterministic BTreeMap ordering.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
#[derive(Default)]
pub struct PlayerId(pub [u8; 16]);

impl PlayerId {
    /// Create from raw bytes.
    pub const fn new(bytes: [u8; 16]) -> Self {
        Self(bytes)
    }

    /// Create from UUID string.
    pub fn from_uuid_str(s: &str) -> Option<Self> {
        uuid::Uuid::parse_str(s)
            .ok()
            .map(|u| Self(*u.as_bytes()))
    }

    /// Convert to UUID string.
    pub fn to_uuid_string(&self) -> String {
        uuid::Uuid::from_bytes(self.0).to_string()
    }

    /// Get raw bytes.
    pub fn as_bytes(&self) -> &[u8; 16] {
        &self.0
    }
}


// =============================================================================
// PLAYER FORM (Evolution Tier)
// =============================================================================

/// Player evolution form (Tier 1-5).
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
#[repr(u8)]
#[derive(Default)]
pub enum Form {
    /// Tier 1: Spark - Smallest, fastest
    #[default]
    Spark = 0,
    /// Tier 2: Glyph
    Glyph = 1,
    /// Tier 3: Ward
    Ward = 2,
    /// Tier 4: Arcane
    Arcane = 3,
    /// Tier 5: Ancient - Largest, slowest
    Ancient = 4,
}

impl Form {
    /// Get movement speed for this form.
    #[inline]
    pub fn speed(self) -> Fixed {
        FORM_SPEEDS[self as usize]
    }

    /// Get collision radius for this form.
    #[inline]
    pub fn radius(self) -> Fixed {
        FORM_RADII[self as usize]
    }

    /// Get next form (if not max).
    pub fn next(self) -> Option<Form> {
        match self {
            Form::Spark => Some(Form::Glyph),
            Form::Glyph => Some(Form::Ward),
            Form::Ward => Some(Form::Arcane),
            Form::Arcane => Some(Form::Ancient),
            Form::Ancient => None,
        }
    }

    /// Check if this form can eat another form.
    #[inline]
    pub fn can_eat(self, other: Form) -> bool {
        self as u8 > other as u8
    }

    /// Get form from index (0-4).
    pub fn from_index(index: u8) -> Option<Form> {
        match index {
            0 => Some(Form::Spark),
            1 => Some(Form::Glyph),
            2 => Some(Form::Ward),
            3 => Some(Form::Arcane),
            4 => Some(Form::Ancient),
            _ => None,
        }
    }
}


// =============================================================================
// PLAYER STATE
// =============================================================================

/// State of a single player in the match.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct PlayerState {
    /// Unique player ID
    pub id: PlayerId,

    /// Current position in arena
    pub position: FixedVec2,

    /// Current velocity
    pub velocity: FixedVec2,

    /// Current evolution form
    pub form: Form,

    /// Accumulated score
    pub score: u32,

    /// Is player still alive?
    pub alive: bool,

    /// Final placement (1st, 2nd, etc.) - set when eliminated
    pub placement: Option<u8>,

    /// Tick when player was eliminated
    pub eliminated_tick: Option<u32>,

    /// ID of player who eliminated this player
    pub eliminated_by: Option<PlayerId>,

    /// Ability cooldown remaining (0 = ready)
    pub ability_cooldown: Fixed,

    /// Tick of last jump (for jump cooldown)
    pub last_jump_tick: u32,

    /// Number of players eliminated by this player
    pub kills: u32,

    /// Number of runes collected
    pub runes_collected: u32,

    // =========================================================================
    // Health & Buff System (Phase 1 & 2)
    // =========================================================================

    /// Current health (0 to FIXED_ONE, where FIXED_ONE = 100%)
    pub health: Fixed,

    /// Maximum health
    pub max_health: Fixed,

    /// Speed buff from rune (remaining ticks, 1.4x speed)
    pub speed_buff_ticks: u32,

    /// Shield buff from rune (remaining ticks, wins ties)
    pub shield_buff_ticks: u32,

    // =========================================================================
    // Ability System (Phase 4)
    // =========================================================================

    /// Invulnerability ticks (from Phase Shift ability)
    pub invulnerable_ticks: u32,

    /// Active dash velocity (from Dash ability)
    pub dash_velocity: Option<FixedVec2>,

    // =========================================================================
    // Shrine Buffs (Phase 3)
    // =========================================================================

    /// Active shrine buffs
    pub shrine_buffs: Vec<ShrineType>,

    /// Shrine buff remaining ticks (parallel to shrine_buffs)
    pub shrine_buff_ticks: Vec<u32>,
}

impl PlayerState {
    /// Create a new player at spawn position.
    pub fn new(id: PlayerId, position: FixedVec2) -> Self {
        Self {
            id,
            position,
            velocity: FixedVec2::ZERO,
            form: Form::Spark,
            score: 0,
            alive: true,
            placement: None,
            eliminated_tick: None,
            eliminated_by: None,
            ability_cooldown: 0,
            last_jump_tick: 0,
            kills: 0,
            runes_collected: 0,
            // Health & buffs
            health: FIXED_ONE,
            max_health: FIXED_ONE,
            speed_buff_ticks: 0,
            shield_buff_ticks: 0,
            // Ability state
            invulnerable_ticks: 0,
            dash_velocity: None,
            // Shrine buffs
            shrine_buffs: Vec::new(),
            shrine_buff_ticks: Vec::new(),
        }
    }

    /// Get current movement speed based on form.
    #[inline]
    pub fn speed(&self) -> Fixed {
        self.form.speed()
    }

    /// Get current collision radius based on form.
    #[inline]
    pub fn radius(&self) -> Fixed {
        self.form.radius()
    }

    /// Check if player can evolve based on score.
    pub fn can_evolve(&self) -> bool {
        if self.form == Form::Ancient {
            return false;
        }
        let threshold = SCORE_TO_EVOLVE[self.form as usize];
        self.score >= threshold
    }

    /// Evolve to next form if possible.
    pub fn try_evolve(&mut self) -> bool {
        if let Some(next_form) = self.form.next() {
            if self.can_evolve() {
                self.form = next_form;
                return true;
            }
        }
        false
    }

    /// Check if ability is ready.
    #[inline]
    pub fn ability_ready(&self) -> bool {
        self.ability_cooldown <= 0
    }

    /// Check if player can jump.
    pub fn can_jump(&self, current_tick: u32) -> bool {
        // Simple cooldown: 30 ticks (0.5 seconds) between jumps
        current_tick.saturating_sub(self.last_jump_tick) >= 30
    }

    /// Add score and check for evolution.
    pub fn add_score(&mut self, amount: u32) -> bool {
        self.score = self.score.saturating_add(amount);
        self.try_evolve()
    }

    /// Hash this player's state for verification.
    pub fn hash_into(&self, hasher: &mut StateHasher) {
        hasher.update_uuid(&self.id.0);
        hasher.update_vec2(self.position);
        hasher.update_vec2(self.velocity);
        hasher.update_u8(self.form as u8);
        hasher.update_u32(self.score);
        hasher.update_bool(self.alive);
        hasher.update_u32(self.kills);
        // Health & buff system
        hasher.update_fixed(self.health);
        hasher.update_u32(self.speed_buff_ticks);
        hasher.update_u32(self.shield_buff_ticks);
        hasher.update_u32(self.invulnerable_ticks);
        // Shrine buffs
        for (i, shrine_type) in self.shrine_buffs.iter().enumerate() {
            hasher.update_u8(*shrine_type as u8);
            if let Some(ticks) = self.shrine_buff_ticks.get(i) {
                hasher.update_u32(*ticks);
            }
        }
    }

    /// Check if player has a specific shrine buff active.
    pub fn has_shrine_buff(&self, buff_type: ShrineType) -> bool {
        for (i, st) in self.shrine_buffs.iter().enumerate() {
            if *st == buff_type && self.shrine_buff_ticks.get(i).is_some_and(|t| *t > 0) {
                return true;
            }
        }
        false
    }

    /// Add a shrine buff.
    pub fn add_shrine_buff(&mut self, buff_type: ShrineType, duration_ticks: u32) {
        // Check if we already have this buff type - refresh duration
        for (i, st) in self.shrine_buffs.iter().enumerate() {
            if *st == buff_type {
                if let Some(ticks) = self.shrine_buff_ticks.get_mut(i) {
                    *ticks = duration_ticks;
                }
                return;
            }
        }
        // Add new buff
        self.shrine_buffs.push(buff_type);
        self.shrine_buff_ticks.push(duration_ticks);
    }

    /// Update shrine buff timers (called each tick).
    pub fn update_shrine_buffs(&mut self) {
        // Decay timers
        for ticks in &mut self.shrine_buff_ticks {
            *ticks = ticks.saturating_sub(1);
        }
        // Remove expired buffs
        let mut i = 0;
        while i < self.shrine_buffs.len() {
            if self.shrine_buff_ticks.get(i).is_none_or(|t| *t == 0) {
                self.shrine_buffs.swap_remove(i);
                self.shrine_buff_ticks.swap_remove(i);
            } else {
                i += 1;
            }
        }
    }
}

// =============================================================================
// RUNE STATE
// =============================================================================

/// Type of rune collectible.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[repr(u8)]
pub enum RuneType {
    Wisdom = 0,   // Blue - basic points
    Power = 1,    // Red - more points
    Speed = 2,    // Yellow - speed buff
    Shield = 3,   // Green - shield buff
    Arcane = 4,   // Purple - rare, many points
    Chaos = 5,    // Rainbow - random effect
}

impl RuneType {
    /// Get point value for this rune type.
    pub fn value(self) -> u32 {
        match self {
            RuneType::Wisdom => 10,
            RuneType::Power => 15,
            RuneType::Speed => 12,
            RuneType::Shield => 8,
            RuneType::Arcane => 25,
            RuneType::Chaos => 50,
        }
    }

    /// Get from index.
    pub fn from_index(index: u8) -> Option<Self> {
        match index {
            0 => Some(RuneType::Wisdom),
            1 => Some(RuneType::Power),
            2 => Some(RuneType::Speed),
            3 => Some(RuneType::Shield),
            4 => Some(RuneType::Arcane),
            5 => Some(RuneType::Chaos),
            _ => None,
        }
    }
}

/// State of a rune collectible.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct RuneState {
    /// Unique rune ID (monotonic counter)
    pub id: u32,

    /// Position in arena
    pub position: FixedVec2,

    /// Type of rune
    pub rune_type: RuneType,

    /// Has this rune been collected?
    pub collected: bool,

    /// Tick when collected (if collected)
    pub collected_tick: Option<u32>,

    /// Who collected it (if collected)
    pub collected_by: Option<PlayerId>,
}

impl RuneState {
    /// Rune collision radius
    pub const RADIUS: Fixed = 19660; // ~0.3 * 65536

    /// Create a new rune.
    pub fn new(id: u32, position: FixedVec2, rune_type: RuneType) -> Self {
        Self {
            id,
            position,
            rune_type,
            collected: false,
            collected_tick: None,
            collected_by: None,
        }
    }

    /// Get point value.
    pub fn value(&self) -> u32 {
        self.rune_type.value()
    }
}

// =============================================================================
// SHRINE STATE
// =============================================================================

/// Type of shrine.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[repr(u8)]
pub enum ShrineType {
    Wisdom = 0,  // XP boost
    Power = 1,   // Damage boost
    Speed = 2,   // Speed boost
    Shield = 3,  // Damage reduction
}

/// State of a shrine.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ShrineState {
    /// Shrine ID
    pub id: u8,

    /// Position in arena
    pub position: FixedVec2,

    /// Type of shrine
    pub shrine_type: ShrineType,

    /// Is shrine currently active?
    pub active: bool,

    /// Player currently channeling (if any)
    pub channeling_player: Option<PlayerId>,

    /// Channel progress (0 to FIXED_ONE)
    pub channel_progress: Fixed,

    /// Cooldown remaining after use
    pub cooldown: Fixed,
}

impl ShrineState {
    /// Shrine interaction radius
    pub const RADIUS: Fixed = 196608; // 3.0 * 65536

    /// Channel time required (5 seconds = 300 ticks)
    pub const CHANNEL_TICKS: u32 = 300;

    /// Cooldown after use (60 seconds = 3600 ticks)
    pub const COOLDOWN_TICKS: u32 = 3600;

    /// Create a new shrine.
    pub fn new(id: u8, position: FixedVec2, shrine_type: ShrineType) -> Self {
        Self {
            id,
            position,
            shrine_type,
            active: true,
            channeling_player: None,
            channel_progress: 0,
            cooldown: 0,
        }
    }
}

// =============================================================================
// ACTIVE ABILITY EFFECT
// =============================================================================

/// Type of ability effect.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[repr(u8)]
pub enum AbilityType {
    /// Spark: Quick Dash
    Dash = 0,
    /// Glyph: Phase Shift (invulnerability)
    PhaseShift = 1,
    /// Ward: Repel (push enemies)
    Repel = 2,
    /// Arcane: Gravity Well (slow field)
    GravityWell = 3,
    /// Ancient: Consume (extended range)
    Consume = 4,
}

/// Active ability effect on the field (e.g., gravity well).
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ActiveAbilityEffect {
    /// Type of ability
    pub ability_type: AbilityType,
    /// Player who created this effect
    pub source_player: PlayerId,
    /// Position of the effect
    pub position: FixedVec2,
    /// Remaining duration in ticks
    pub remaining_ticks: u32,
    /// Effect radius
    pub radius: Fixed,
}

// =============================================================================
// MATCH PHASE
// =============================================================================

/// Current phase of the match.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[derive(Default)]
pub enum MatchPhase {
    /// Waiting for players
    #[default]
    Waiting,
    /// Countdown before start
    Countdown { ticks_remaining: u32 },
    /// Active gameplay
    Playing,
    /// Match ended, showing results
    Ended,
}


// =============================================================================
// MATCH STATE
// =============================================================================

/// Complete state of a match.
///
/// Uses BTreeMap for deterministic iteration order.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct MatchState {
    /// Match identifier
    pub match_id: [u8; 16],

    /// Current tick (0 to 5400 for 90 seconds)
    pub tick: u32,

    /// Current match phase
    pub phase: MatchPhase,

    /// RNG seed (for verification)
    pub rng_seed: u64,

    /// Deterministic RNG state
    #[serde(skip)]
    pub rng: DeterministicRng,

    /// All players (BTreeMap for deterministic iteration)
    pub players: BTreeMap<PlayerId, PlayerState>,

    /// All runes (BTreeMap for deterministic iteration)
    pub runes: BTreeMap<u32, RuneState>,

    /// All shrines
    pub shrines: Vec<ShrineState>,

    /// Next rune ID (monotonic counter)
    pub next_rune_id: u32,

    /// Number of players still alive
    pub alive_count: u32,

    /// Next placement number (for elimination order)
    pub next_placement: u8,

    /// Events generated this tick (cleared each tick)
    #[serde(skip)]
    pub pending_events: Vec<GameEvent>,

    /// Arena shrink progress (0 = full size, FIXED_ONE = minimum)
    pub arena_shrink: Fixed,

    /// Active ability effects on the field (gravity wells, etc.)
    pub active_abilities: Vec<ActiveAbilityEffect>,
}

impl MatchState {
    /// Create a new match state.
    pub fn new(match_id: [u8; 16], rng_seed: u64) -> Self {
        Self {
            match_id,
            tick: 0,
            phase: MatchPhase::Waiting,
            rng_seed,
            rng: DeterministicRng::new(rng_seed),
            players: BTreeMap::new(),
            runes: BTreeMap::new(),
            shrines: Vec::new(),
            next_rune_id: 0,
            alive_count: 0,
            next_placement: 0,
            pending_events: Vec::new(),
            arena_shrink: 0,
            active_abilities: Vec::new(),
        }
    }

    /// Add a player to the match.
    pub fn add_player(&mut self, id: PlayerId) {
        let spawn_pos = self.rng.random_position();
        let player = PlayerState::new(id, spawn_pos);
        self.players.insert(id, player);
        self.alive_count += 1;
    }

    /// Get a player by ID.
    pub fn get_player(&self, id: &PlayerId) -> Option<&PlayerState> {
        self.players.get(id)
    }

    /// Get a player mutably by ID.
    pub fn get_player_mut(&mut self, id: &PlayerId) -> Option<&mut PlayerState> {
        self.players.get_mut(id)
    }

    /// Spawn a new rune.
    pub fn spawn_rune(&mut self, position: FixedVec2, rune_type: RuneType) -> u32 {
        let id = self.next_rune_id;
        self.next_rune_id += 1;
        let rune = RuneState::new(id, position, rune_type);
        self.runes.insert(id, rune);
        id
    }

    /// Get current arena bounds (accounting for shrink).
    pub fn current_arena_bounds(&self) -> (Fixed, Fixed) {
        // Shrink from full size to 50% over time
        let shrink_factor = FIXED_ONE - (self.arena_shrink >> 1);
        let half_width = crate::core::fixed::fixed_mul(ARENA_HALF_WIDTH, shrink_factor);
        let half_height = crate::core::fixed::fixed_mul(ARENA_HALF_HEIGHT, shrink_factor);
        (half_width, half_height)
    }

    /// Check if a position is within current arena bounds.
    pub fn is_in_bounds(&self, pos: FixedVec2) -> bool {
        let (hw, hh) = self.current_arena_bounds();
        pos.x >= -hw && pos.x <= hw && pos.y >= -hh && pos.y <= hh
    }

    /// Eliminate a player.
    pub fn eliminate_player(&mut self, victim_id: &PlayerId, killer_id: Option<&PlayerId>) {
        // First check if victim is alive
        let victim_alive = self.players.get(victim_id).map(|p| p.alive).unwrap_or(false);
        if !victim_alive {
            return;
        }

        // Calculate placement before mutating
        let player_count = self.players.len() as u8;
        let placement = player_count - self.next_placement;

        // Update victim
        if let Some(victim) = self.players.get_mut(victim_id) {
            victim.alive = false;
            victim.eliminated_tick = Some(self.tick);
            victim.eliminated_by = killer_id.copied();
            victim.placement = Some(placement);
        }

        // Update alive count and placement counter
        self.alive_count = self.alive_count.saturating_sub(1);
        self.next_placement += 1;

        // Credit kill to killer (separate borrow)
        if let Some(kid) = killer_id {
            if let Some(killer) = self.players.get_mut(kid) {
                killer.kills += 1;
                killer.add_score(crate::core::fixed::SCORE_PER_KILL);
            }
        }
    }

    /// Get final placements (sorted by placement).
    pub fn get_placements(&self) -> Vec<(PlayerId, u8, u32)> {
        let mut results: Vec<_> = self.players.values()
            .map(|p| (p.id, p.placement.unwrap_or(0), p.score))
            .collect();

        // Sort by placement (1st, 2nd, etc.)
        results.sort_by_key(|(_, placement, _)| *placement);
        results
    }

    /// Check if match has ended.
    pub fn is_ended(&self) -> bool {
        matches!(self.phase, MatchPhase::Ended)
    }

    /// Get count of alive players.
    pub fn alive_player_count(&self) -> u32 {
        self.alive_count
    }

    /// Compute hash of current state for verification.
    pub fn compute_hash(&self) -> StateHash {
        compute_state_hash(self.tick, self.rng_seed, |hasher| {
            // Hash all players in sorted order (BTreeMap guarantees this)
            for player in self.players.values() {
                player.hash_into(hasher);
            }

            // Hash rune states
            for (rune_id, rune) in &self.runes {
                hasher.update_u32(*rune_id);
                hasher.update_vec2(rune.position);
                hasher.update_u8(rune.rune_type as u8);
                hasher.update_bool(rune.collected);
            }

            // Hash shrine states
            for shrine in &self.shrines {
                hasher.update_u8(shrine.id);
                hasher.update_bool(shrine.active);
                hasher.update_fixed(shrine.channel_progress);
                hasher.update_fixed(shrine.cooldown);
            }

            // Hash active ability effects
            for ability in &self.active_abilities {
                hasher.update_u8(ability.ability_type as u8);
                hasher.update_vec2(ability.position);
                hasher.update_u32(ability.remaining_ticks);
            }

            // Hash arena state
            hasher.update_fixed(self.arena_shrink);
            hasher.update_u32(self.alive_count);
        })
    }

    /// Take pending events (consumes them).
    pub fn take_events(&mut self) -> Vec<GameEvent> {
        std::mem::take(&mut self.pending_events)
    }

    /// Push a game event.
    pub fn push_event(&mut self, event: GameEvent) {
        self.pending_events.push(event);
    }
}

// =============================================================================
// TESTS
// =============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_player_id_ordering() {
        let id1 = PlayerId::new([0; 16]);
        let id2 = PlayerId::new([1; 16]);
        let id3 = PlayerId::new([0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);

        assert!(id1 < id2);
        assert!(id1 < id3);
        assert!(id3 < id2);
    }

    #[test]
    fn test_form_progression() {
        assert_eq!(Form::Spark.next(), Some(Form::Glyph));
        assert_eq!(Form::Glyph.next(), Some(Form::Ward));
        assert_eq!(Form::Ward.next(), Some(Form::Arcane));
        assert_eq!(Form::Arcane.next(), Some(Form::Ancient));
        assert_eq!(Form::Ancient.next(), None);
    }

    #[test]
    fn test_form_can_eat() {
        assert!(!Form::Spark.can_eat(Form::Spark));
        assert!(!Form::Spark.can_eat(Form::Glyph));
        assert!(Form::Glyph.can_eat(Form::Spark));
        assert!(Form::Ancient.can_eat(Form::Arcane));
    }

    #[test]
    fn test_player_evolution() {
        let id = PlayerId::new([0; 16]);
        let mut player = PlayerState::new(id, FixedVec2::ZERO);

        assert_eq!(player.form, Form::Spark);
        assert!(!player.can_evolve());

        // Add score to trigger evolution
        player.score = 100;
        assert!(player.can_evolve());
        assert!(player.try_evolve());
        assert_eq!(player.form, Form::Glyph);

        // Can't evolve again without more score
        assert!(!player.can_evolve());
    }

    #[test]
    fn test_match_state_determinism() {
        let match_id = [0u8; 16];
        let seed = 12345u64;

        let mut state1 = MatchState::new(match_id, seed);
        let mut state2 = MatchState::new(match_id, seed);

        // Add same players
        for i in 0..4 {
            let id = PlayerId::new([i; 16]);
            state1.add_player(id);
            state2.add_player(id);
        }

        // Positions should be identical (same RNG seed)
        for id in state1.players.keys() {
            let pos1 = state1.players[id].position;
            let pos2 = state2.players[id].position;
            assert_eq!(pos1, pos2, "Spawn positions should be deterministic");
        }

        // Hashes should be identical
        assert_eq!(state1.compute_hash(), state2.compute_hash());
    }

    #[test]
    fn test_btreemap_iteration_order() {
        let mut state = MatchState::new([0; 16], 12345);

        // Add players in random order
        let ids = [
            PlayerId::new([5; 16]),
            PlayerId::new([1; 16]),
            PlayerId::new([9; 16]),
            PlayerId::new([3; 16]),
        ];

        for id in &ids {
            state.add_player(*id);
        }

        // Iteration should be sorted
        let iterated: Vec<_> = state.players.keys().collect();
        let mut sorted = iterated.clone();
        sorted.sort();

        assert_eq!(iterated, sorted, "BTreeMap should iterate in sorted order");
    }
}

//! Core 2D game components - True Slither.io style combat
//!
//! Rules:
//! - You die only if YOUR HEAD hits THEIR BODY
//! - They die only if THEIR HEAD hits YOUR BODY
//! - Head-to-head = no death
//! - Grace zone: first N segments are non-lethal

use bevy::prelude::*;

// ============================================================================
// TEAMS
// ============================================================================

/// Team affiliation for collision rules
#[derive(Debug, Clone, Copy, PartialEq, Eq, Component)]
pub enum Team {
    Player,
    Enemy,
}

// ============================================================================
// MOVEMENT
// ============================================================================

/// 2D velocity for all moving entities
#[derive(Component, Default)]
pub struct Velocity(pub Vec2);

/// Movement speed multiplier
#[derive(Component)]
pub struct MoveSpeed(pub f32);

impl Default for MoveSpeed {
    fn default() -> Self {
        Self(150.0)
    }
}

// ============================================================================
// COLLISION
// ============================================================================

/// Circle collider for 2D collision detection
#[derive(Component)]
pub struct Radius(pub f32);

impl Radius {
    pub fn new(radius: f32) -> Self {
        Self(radius)
    }

    /// Check if two circles overlap
    pub fn overlaps(&self, self_pos: Vec2, other: &Radius, other_pos: Vec2) -> bool {
        let distance = self_pos.distance(other_pos);
        distance < self.0 + other.0
    }
}

// ============================================================================
// SNAKE HEAD (both players and enemies are "snakes")
// ============================================================================

/// Marks an entity as a snake head - the deadly part that can kill others
/// but also the vulnerable part that can be killed by hitting bodies
#[derive(Component)]
pub struct SnakeHead {
    pub team: Team,
}

impl SnakeHead {
    pub fn player() -> Self {
        Self { team: Team::Player }
    }

    pub fn enemy() -> Self {
        Self { team: Team::Enemy }
    }
}

// ============================================================================
// PLAYER
// ============================================================================

/// Player marker component
#[derive(Component)]
pub struct Player;

/// Local player marker (the one we control)
#[derive(Component)]
pub struct LocalPlayer;

// ============================================================================
// BODY SEGMENT SYSTEM (the "trail" that kills enemy heads)
// ============================================================================

/// Component for entities that emit body segments (both player and enemies)
#[derive(Component)]
pub struct BodyEmitter {
    pub team: Team,             // whose body is this
    pub spawn_interval: f32,    // seconds between spawns (e.g. 0.08)
    pub timer: f32,             // current timer
    pub segment_radius: f32,    // radius of each segment (e.g. 7.0)
    pub max_segments: usize,    // cap to avoid perf issues
    pub grace_segments: u32,    // first N segments are non-lethal
    pub color: Color,           // segment color
    pub segments_spawned: u32,  // counter for indexing
}

impl BodyEmitter {
    pub fn player(color: Color) -> Self {
        Self {
            team: Team::Player,
            spawn_interval: 0.08,
            timer: 0.0,
            segment_radius: 7.0,
            max_segments: 320,
            grace_segments: 8,
            color,
            segments_spawned: 0,
        }
    }

    pub fn enemy(color: Color) -> Self {
        Self {
            team: Team::Enemy,
            spawn_interval: 0.10,
            timer: 0.0,
            segment_radius: 6.0,
            max_segments: 40,  // enemies have shorter bodies
            grace_segments: 6,
            color,
            segments_spawned: 0,
        }
    }

    pub fn with_interval(mut self, interval: f32) -> Self {
        self.spawn_interval = interval;
        self
    }

    pub fn with_radius(mut self, radius: f32) -> Self {
        self.segment_radius = radius;
        self
    }

    pub fn with_max_segments(mut self, max: usize) -> Self {
        self.max_segments = max;
        self
    }

    pub fn with_grace(mut self, grace: u32) -> Self {
        self.grace_segments = grace;
        self
    }
}

/// A body segment - lethal to enemy heads (unless in grace zone)
#[derive(Component)]
pub struct BodySegment {
    pub owner: Entity,
    pub team: Team,
    pub index: u32,  // 0 = newest (closest to head), higher = older
}

// ============================================================================
// ENEMY
// ============================================================================

/// Enemy marker with type info
#[derive(Component)]
pub struct Enemy {
    pub enemy_type: super::EnemyType,
}

/// Enemy chases this target entity
#[derive(Component)]
pub struct ChaseTarget(pub Entity);

// ============================================================================
// OBSTACLES
// ============================================================================

/// Static obstacle that kills player on contact
#[derive(Component)]
pub struct Obstacle;

// ============================================================================
// ESSENCE (replaces XP gems)
// ============================================================================

/// Essence dropped when enemies die or player dies
#[derive(Component)]
pub struct Essence {
    pub value: u32,
}

impl Essence {
    pub fn new(value: u32) -> Self {
        Self { value }
    }
}

/// Entity is being magnetized toward target
#[derive(Component)]
pub struct Magnetized {
    pub target: Entity,
    pub speed: f32,
}

// ============================================================================
// VISUAL EFFECTS
// ============================================================================

/// Death particle burst
#[derive(Component)]
pub struct DeathParticle {
    pub velocity: Vec2,
    pub lifetime: f32,
}

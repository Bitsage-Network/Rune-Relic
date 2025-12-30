//! Body segment system - True Slither.io style
//!
//! Both players and enemies emit body segments.
//! Heads die when hitting enemy body segments (outside grace zone).

use bevy::prelude::*;
use std::collections::{HashMap, VecDeque};

use super::{
    BodyEmitter, BodySegment, Velocity, Radius,
    Team, LocalPlayer, Essence,
};

// ============================================================================
// BODY STATE RESOURCE
// ============================================================================

/// Tracks body segments per owner for efficient management
#[derive(Resource, Default)]
pub struct BodyState {
    /// Maps owner entity to their body segment entities (oldest first)
    pub segments_by_owner: HashMap<Entity, VecDeque<Entity>>,
}

impl BodyState {
    /// Add a segment to an owner's body
    pub fn add_segment(&mut self, owner: Entity, segment: Entity) {
        self.segments_by_owner
            .entry(owner)
            .or_default()
            .push_back(segment);
    }

    /// Remove oldest segment if over cap, returns the entity to despawn
    pub fn trim_oldest(&mut self, owner: Entity, max_segments: usize) -> Option<Entity> {
        if let Some(segments) = self.segments_by_owner.get_mut(&owner) {
            if segments.len() > max_segments {
                return segments.pop_front();
            }
        }
        None
    }

    /// Get all segments for an owner (for death conversion)
    pub fn take_all_segments(&mut self, owner: Entity) -> Vec<Entity> {
        self.segments_by_owner
            .remove(&owner)
            .map(|dq| dq.into_iter().collect())
            .unwrap_or_default()
    }

    /// Get segment count for an owner
    pub fn segment_count(&self, owner: Entity) -> usize {
        self.segments_by_owner
            .get(&owner)
            .map(|s| s.len())
            .unwrap_or(0)
    }

    /// Update segment indices after spawning new segment
    /// (newest segment = index 0, older segments get higher index)
    pub fn reindex_segments(&self, owner: Entity, segment_query: &mut Query<&mut BodySegment>) {
        if let Some(segments) = self.segments_by_owner.get(&owner) {
            let count = segments.len() as u32;
            for (i, &segment_entity) in segments.iter().rev().enumerate() {
                if let Ok(mut segment) = segment_query.get_mut(segment_entity) {
                    segment.index = i as u32;
                }
            }
        }
    }
}

// ============================================================================
// BODY SEGMENT SPAWNING (for both players and enemies)
// ============================================================================

/// Spawn body segments behind all moving entities with BodyEmitter
pub fn spawn_body_segments(
    mut commands: Commands,
    time: Res<Time>,
    mut body_state: ResMut<BodyState>,
    mut emitter_query: Query<(Entity, &Transform, &Velocity, &mut BodyEmitter)>,
    mut segment_query: Query<&mut BodySegment>,
) {
    let dt = time.delta_secs();

    for (owner, transform, velocity, mut emitter) in emitter_query.iter_mut() {
        // Only emit when moving
        if velocity.0.length_squared() < 100.0 {
            continue;
        }

        emitter.timer += dt;

        // Spawn segments at interval
        while emitter.timer >= emitter.spawn_interval {
            emitter.timer -= emitter.spawn_interval;

            let pos = transform.translation.truncate();

            // Spawn slightly behind based on velocity direction
            let offset = -velocity.0.normalize_or_zero() * emitter.segment_radius * 0.5;
            let spawn_pos = pos + offset;

            // Create body segment entity
            let segment_entity = commands.spawn((
                BodySegment {
                    owner,
                    team: emitter.team,
                    index: 0,  // Will be reindexed
                },
                Radius::new(emitter.segment_radius),
                Sprite {
                    color: emitter.color.with_alpha(0.8),
                    custom_size: Some(Vec2::splat(emitter.segment_radius * 2.0)),
                    ..default()
                },
                Transform::from_translation(spawn_pos.extend(1.0)), // Z=1 for body layer
            )).id();

            // Track in body state
            body_state.add_segment(owner, segment_entity);

            // Reindex all segments (newest = 0)
            body_state.reindex_segments(owner, &mut segment_query);

            emitter.segments_spawned += 1;

            // Trim oldest if over cap
            if let Some(old_segment) = body_state.trim_oldest(owner, emitter.max_segments) {
                commands.entity(old_segment).despawn();
            }
        }
    }
}

// ============================================================================
// CONVERT BODY TO ESSENCE ON DEATH
// ============================================================================

/// When an entity dies, convert their body segments to essence pickups
pub fn convert_body_to_essence(
    commands: &mut Commands,
    body_state: &mut BodyState,
    owner: Entity,
    segment_query: &Query<&Transform, With<BodySegment>>,
) {
    let segments = body_state.take_all_segments(owner);

    for segment_entity in segments {
        // Get segment position before despawning
        if let Ok(transform) = segment_query.get(segment_entity) {
            let pos = transform.translation.truncate();

            // Spawn essence at segment position
            commands.spawn((
                Essence::new(1),
                Radius::new(5.0),
                Sprite {
                    color: Color::srgb(1.0, 0.6, 0.3), // Orange essence
                    custom_size: Some(Vec2::splat(10.0)),
                    ..default()
                },
                Transform::from_translation(pos.extend(2.0)),
            ));
        }

        // Despawn the segment
        commands.entity(segment_entity).despawn();
    }
}

/// Spawn essence pickup at position (for enemy death)
pub fn spawn_essence(commands: &mut Commands, position: Vec2, value: u32) {
    // Spawn 1-3 essence pickups with slight spread
    let count = (value / 2).clamp(1, 3);

    for i in 0..count {
        let angle = (i as f32 / count as f32) * std::f32::consts::TAU;
        let offset = Vec2::new(angle.cos(), angle.sin()) * 10.0;
        let spawn_pos = position + offset;

        commands.spawn((
            Essence::new(value / count),
            Radius::new(6.0),
            Sprite {
                color: Color::srgb(0.4, 1.0, 0.8), // Cyan-green glow
                custom_size: Some(Vec2::splat(12.0)),
                ..default()
            },
            Transform::from_translation(spawn_pos.extend(2.0)),
        ));
    }
}

// ============================================================================
// ESSENCE COLLECTION
// ============================================================================

/// Base pickup radius for essence
const ESSENCE_PICKUP_RADIUS: f32 = 25.0;
/// Magnetism activation radius
const ESSENCE_MAGNET_RADIUS: f32 = 80.0;
/// Magnetism speed
const ESSENCE_MAGNET_SPEED: f32 = 350.0;

/// Magnetize essence toward player when in range
pub fn essence_magnetism(
    mut commands: Commands,
    player_query: Query<(Entity, &Transform), With<LocalPlayer>>,
    essence_query: Query<(Entity, &Transform), (With<Essence>, Without<super::Magnetized>)>,
) {
    let Ok((player_entity, player_transform)) = player_query.get_single() else {
        return;
    };

    let player_pos = player_transform.translation.truncate();

    for (essence_entity, essence_transform) in essence_query.iter() {
        let essence_pos = essence_transform.translation.truncate();
        let distance = player_pos.distance(essence_pos);

        if distance < ESSENCE_MAGNET_RADIUS {
            commands.entity(essence_entity).insert(super::Magnetized {
                target: player_entity,
                speed: ESSENCE_MAGNET_SPEED,
            });
        }
    }
}

/// Move magnetized essence toward their target
pub fn move_magnetized_essence(
    time: Res<Time>,
    mut query: Query<(&mut Transform, &super::Magnetized), With<Essence>>,
    target_query: Query<&Transform, Without<Essence>>,
) {
    let dt = time.delta_secs();

    for (mut essence_transform, magnetized) in query.iter_mut() {
        let Ok(target_transform) = target_query.get(magnetized.target) else {
            continue;
        };

        let essence_pos = essence_transform.translation.truncate();
        let target_pos = target_transform.translation.truncate();

        let direction = (target_pos - essence_pos).normalize_or_zero();
        let movement = direction * magnetized.speed * dt;

        essence_transform.translation.x += movement.x;
        essence_transform.translation.y += movement.y;
    }
}

/// Collect essence on contact - grow body capacity
pub fn collect_essence(
    mut commands: Commands,
    player_query: Query<(&Transform, &Radius), With<LocalPlayer>>,
    essence_query: Query<(Entity, &Transform, &Essence)>,
    mut emitter_query: Query<&mut BodyEmitter, With<LocalPlayer>>,
) {
    let Ok((player_transform, player_radius)) = player_query.get_single() else {
        return;
    };

    let player_pos = player_transform.translation.truncate();

    for (essence_entity, essence_transform, essence) in essence_query.iter() {
        let essence_pos = essence_transform.translation.truncate();
        let distance = player_pos.distance(essence_pos);

        if distance < ESSENCE_PICKUP_RADIUS + player_radius.0 {
            // Collect essence
            commands.entity(essence_entity).despawn();

            // Grow body capacity
            if let Ok(mut emitter) = emitter_query.get_single_mut() {
                // Each essence adds to max segments (growth!)
                emitter.max_segments += essence.value as usize;

                // Slightly increase segment radius for visual growth
                emitter.segment_radius = (emitter.segment_radius + 0.02).min(15.0);
            }
        }
    }
}

//! Combat systems - True Slither.io style collision
//!
//! Rules:
//! - You die only if YOUR HEAD hits THEIR BODY (outside grace zone)
//! - They die only if THEIR HEAD hits YOUR BODY (outside grace zone)
//! - Head-to-head = no death
//! - Body-to-body = no death

use bevy::prelude::*;

use super::{
    Radius, SnakeHead, BodySegment, BodyEmitter, Team,
    Player, LocalPlayer, Enemy,
    EnemyDeathEvent, PlayerDeathEvent,
    trail::{BodyState, convert_body_to_essence, spawn_essence},
};

// ============================================================================
// TRUE SLITHER.IO COLLISION
// ============================================================================

/// Check player head vs enemy body segments
/// Player dies if their head hits an enemy body segment (outside grace zone)
pub fn player_head_vs_enemy_body(
    player_query: Query<(Entity, &Transform, &Radius), (With<SnakeHead>, With<LocalPlayer>)>,
    enemy_emitter_query: Query<&BodyEmitter, With<Enemy>>,
    body_query: Query<(&Transform, &Radius, &BodySegment)>,
    mut death_events: EventWriter<PlayerDeathEvent>,
) {
    let Ok((player_entity, player_transform, player_radius)) = player_query.get_single() else {
        return;
    };

    let player_pos = player_transform.translation.truncate();

    // Check against all enemy body segments
    for (body_transform, body_radius, body_segment) in body_query.iter() {
        // Only check enemy bodies
        if body_segment.team != Team::Enemy {
            continue;
        }

        // Get the owner's grace segment count
        let grace_segments = enemy_emitter_query
            .get(body_segment.owner)
            .map(|e| e.grace_segments)
            .unwrap_or(6);

        // Skip grace zone segments (closest to head)
        if body_segment.index < grace_segments {
            continue;
        }

        let body_pos = body_transform.translation.truncate();

        // Check collision
        if player_radius.overlaps(player_pos, body_radius, body_pos) {
            // Player head hit enemy body - player dies!
            death_events.send(PlayerDeathEvent {
                position: player_pos,
                player_entity,
            });
            info!("Player died - head hit enemy body!");
            return;
        }
    }
}

/// Check enemy heads vs player body segments
/// Enemy dies if their head hits a player body segment (outside grace zone)
pub fn enemy_head_vs_player_body(
    mut commands: Commands,
    player_query: Query<Entity, With<LocalPlayer>>,
    player_emitter_query: Query<&BodyEmitter, With<LocalPlayer>>,
    enemy_query: Query<(Entity, &Transform, &Radius, &Enemy), With<SnakeHead>>,
    body_query: Query<(&Transform, &Radius, &BodySegment)>,
    mut body_state: ResMut<BodyState>,
    segment_query: Query<&Transform, With<BodySegment>>,
    mut death_events: EventWriter<EnemyDeathEvent>,
) {
    let Ok(player_entity) = player_query.get_single() else {
        return;
    };

    // Get player grace segment count
    let player_grace = player_emitter_query
        .get(player_entity)
        .map(|e| e.grace_segments)
        .unwrap_or(8);

    for (enemy_entity, enemy_transform, enemy_radius, enemy) in enemy_query.iter() {
        let enemy_pos = enemy_transform.translation.truncate();

        // Check against player body segments
        for (body_transform, body_radius, body_segment) in body_query.iter() {
            // Only check player bodies
            if body_segment.team != Team::Player {
                continue;
            }

            // Skip grace zone segments
            if body_segment.index < player_grace {
                continue;
            }

            let body_pos = body_transform.translation.truncate();

            // Check collision
            if enemy_radius.overlaps(enemy_pos, body_radius, body_pos) {
                // Enemy head hit player body - enemy dies!
                let enemy_type = enemy.enemy_type;

                // Convert enemy body to essence
                convert_body_to_essence(
                    &mut commands,
                    &mut *body_state,
                    enemy_entity,
                    &segment_query,
                );

                // Spawn extra essence at head position
                spawn_essence(&mut commands, enemy_pos, enemy_type.essence_value());

                // Despawn enemy head
                commands.entity(enemy_entity).despawn();

                // Send death event for particles
                death_events.send(EnemyDeathEvent {
                    position: enemy_pos,
                    enemy_type,
                });

                info!("Enemy {:?} died - head hit player body!", enemy_type);
                break;
            }
        }
    }
}

// ============================================================================
// HANDLE PLAYER DEATH
// ============================================================================

/// Process player death - convert body to essence and transition to game over
pub fn handle_player_death(
    mut commands: Commands,
    mut death_events: EventReader<PlayerDeathEvent>,
    mut body_state: ResMut<BodyState>,
    segment_query: Query<&Transform, With<BodySegment>>,
    mut next_state: ResMut<NextState<crate::AppState>>,
) {
    for event in death_events.read() {
        info!("Processing player death at {:?}", event.position);

        // Convert player body to essence pickups
        convert_body_to_essence(
            &mut commands,
            &mut *body_state,
            event.player_entity,
            &segment_query,
        );

        // Despawn player
        commands.entity(event.player_entity).despawn_recursive();

        // Spawn death burst particles
        spawn_death_burst(&mut commands, event.position);

        // Go to game over state
        next_state.set(crate::AppState::GameOver);
    }
}

/// Spawn death burst particles
fn spawn_death_burst(commands: &mut Commands, position: Vec2) {
    let particle_count = 12;

    for i in 0..particle_count {
        let angle = (i as f32 / particle_count as f32) * std::f32::consts::TAU;
        let speed = 150.0 + rand::random::<f32>() * 100.0;
        let velocity = Vec2::new(angle.cos(), angle.sin()) * speed;

        commands.spawn((
            super::DeathParticle {
                velocity,
                lifetime: 0.6 + rand::random::<f32>() * 0.3,
            },
            Sprite {
                color: Color::srgb(1.0, 0.4, 0.2), // Orange-red
                custom_size: Some(Vec2::splat(8.0 + rand::random::<f32>() * 6.0)),
                ..default()
            },
            Transform::from_translation(position.extend(15.0)),
        ));
    }
}

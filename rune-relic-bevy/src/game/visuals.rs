//! Visual effects - camera and particles

use bevy::prelude::*;

use super::{DeathParticle, LocalPlayer, EnemyDeathEvent};

// ============================================================================
// CAMERA FOLLOW
// ============================================================================

/// Camera smoothly follows the local player
pub fn camera_follow(
    time: Res<Time>,
    player_query: Query<&Transform, With<LocalPlayer>>,
    mut camera_query: Query<&mut Transform, (With<Camera2d>, Without<LocalPlayer>)>,
) {
    let Ok(player_transform) = player_query.get_single() else {
        return;
    };

    let Ok(mut camera_transform) = camera_query.get_single_mut() else {
        return;
    };

    let target = player_transform.translation.truncate();
    let current = camera_transform.translation.truncate();

    // Smooth lerp with damping
    let lerp_speed = 5.0;
    let new_pos = current.lerp(target, lerp_speed * time.delta_secs());

    camera_transform.translation.x = new_pos.x;
    camera_transform.translation.y = new_pos.y;
}

// ============================================================================
// DEATH PARTICLES
// ============================================================================

/// Spawn death burst particles when enemies die
pub fn spawn_death_particles(
    mut commands: Commands,
    mut death_events: EventReader<EnemyDeathEvent>,
) {
    for event in death_events.read() {
        let enemy_color = event.enemy_type.color();

        // Spawn burst of particles
        let particle_count = 8;
        for i in 0..particle_count {
            let angle = (i as f32 / particle_count as f32) * std::f32::consts::TAU;
            let speed = 100.0 + rand::random::<f32>() * 50.0;
            let velocity = Vec2::new(angle.cos(), angle.sin()) * speed;

            commands.spawn((
                DeathParticle {
                    velocity,
                    lifetime: 0.5 + rand::random::<f32>() * 0.2,
                },
                Sprite {
                    color: enemy_color,
                    custom_size: Some(Vec2::splat(6.0 + rand::random::<f32>() * 4.0)),
                    ..default()
                },
                Transform::from_translation(event.position.extend(12.0)),
            ));
        }
    }
}

/// Update death particles (move, fade, shrink)
pub fn update_death_particles(
    mut commands: Commands,
    time: Res<Time>,
    mut query: Query<(Entity, &mut DeathParticle, &mut Transform, &mut Sprite)>,
) {
    let dt = time.delta_secs();

    for (entity, mut particle, mut transform, mut sprite) in query.iter_mut() {
        // Move particle
        transform.translation.x += particle.velocity.x * dt;
        transform.translation.y += particle.velocity.y * dt;

        // Slow down
        particle.velocity *= 0.95;

        // Tick lifetime
        particle.lifetime -= dt;

        if particle.lifetime <= 0.0 {
            commands.entity(entity).despawn();
            continue;
        }

        // Fade and shrink
        let progress = 1.0 - (particle.lifetime / 0.7).min(1.0);
        sprite.color = sprite.color.with_alpha(1.0 - progress);

        if let Some(size) = &mut sprite.custom_size {
            *size *= 0.98;
        }
    }
}

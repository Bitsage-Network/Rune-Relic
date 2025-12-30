//! Player systems for true Slither.io style combat

use bevy::prelude::*;

use super::{
    PlayerForm, Velocity, MoveSpeed, Radius,
    Player, LocalPlayer, SnakeHead, BodyEmitter,
};

// ============================================================================
// PLAYER SPAWNING
// ============================================================================

/// Spawn the player entity at the given position
pub fn spawn_player(commands: &mut Commands, position: Vec2) {
    let form = PlayerForm::default();

    commands.spawn((
        // Core identity
        Player,
        LocalPlayer,
        SnakeHead::player(),  // Player is a snake head
        form,
        Name::new("Player"),

        // Transform and rendering
        Sprite {
            color: form.color(),
            custom_size: Some(Vec2::splat(form.radius() * 2.0)),
            ..default()
        },
        Transform::from_translation(position.extend(10.0)), // Z=10 for head layer

        // Physics
        Velocity::default(),
        MoveSpeed(form.speed()),
        Radius::new(form.radius()),

        // Body emission (the trail that kills enemies)
        BodyEmitter::player(form.color())
            .with_interval(0.08)
            .with_radius(7.0)
            .with_max_segments(60)  // Start smaller, grows with essence
            .with_grace(8),         // First 8 segments are non-lethal
    ));

    info!("Player spawned at {:?}", position);
}

// ============================================================================
// PLAYER INPUT - Smooth turning like Slither.io
// ============================================================================

/// Handle WASD input with smooth turning
pub fn player_input(
    time: Res<Time>,
    keyboard: Res<ButtonInput<KeyCode>>,
    mut query: Query<(&mut Velocity, &MoveSpeed), With<LocalPlayer>>,
) {
    let Ok((mut velocity, speed)) = query.get_single_mut() else {
        return;
    };

    let mut direction = Vec2::ZERO;

    // WASD movement
    if keyboard.pressed(KeyCode::KeyW) || keyboard.pressed(KeyCode::ArrowUp) {
        direction.y += 1.0;
    }
    if keyboard.pressed(KeyCode::KeyS) || keyboard.pressed(KeyCode::ArrowDown) {
        direction.y -= 1.0;
    }
    if keyboard.pressed(KeyCode::KeyA) || keyboard.pressed(KeyCode::ArrowLeft) {
        direction.x -= 1.0;
    }
    if keyboard.pressed(KeyCode::KeyD) || keyboard.pressed(KeyCode::ArrowRight) {
        direction.x += 1.0;
    }

    // Normalize diagonal movement and apply speed
    if direction != Vec2::ZERO {
        direction = direction.normalize();
    }

    // Smooth velocity transition (not instant, more Slither-like)
    let target_velocity = direction * speed.0;
    let lerp_speed = 8.0;
    let dt = time.delta_secs();

    velocity.0 = velocity.0.lerp(target_velocity, lerp_speed * dt);
}

// ============================================================================
// PHYSICS
// ============================================================================

/// Apply velocity to transform (fixed timestep)
pub fn apply_velocity(
    time: Res<Time>,
    mut query: Query<(&mut Transform, &Velocity)>,
) {
    let dt = time.delta_secs();

    for (mut transform, velocity) in query.iter_mut() {
        transform.translation.x += velocity.0.x * dt;
        transform.translation.y += velocity.0.y * dt;
    }
}

/// Keep player within arena bounds (soft clamp, not death)
pub fn clamp_to_arena(
    mut query: Query<&mut Transform, With<Player>>,
) {
    const ARENA_RADIUS: f32 = 900.0;

    for mut transform in query.iter_mut() {
        let pos = transform.translation.truncate();
        let distance = pos.length();

        if distance > ARENA_RADIUS {
            let clamped = pos.normalize() * ARENA_RADIUS;
            transform.translation.x = clamped.x;
            transform.translation.y = clamped.y;
        }
    }
}

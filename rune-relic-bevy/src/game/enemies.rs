//! Enemy types, wave spawning, and AI for true Slither.io style combat
//!
//! Enemies are now snakes with heads and bodies, just like the player.
//! They die when their HEAD hits player BODY (outside grace zone).
//! Player dies when player HEAD hits enemy BODY (outside grace zone).

use bevy::prelude::*;
use rand::Rng;

use super::{
    Velocity, MoveSpeed, Radius,
    Enemy, ChaseTarget, Player, SnakeHead, BodyEmitter,
};

// ============================================================================
// ENEMY TYPES
// ============================================================================

/// Enemy type definitions with stats
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EnemyType {
    Shard,     // Fast, short body - chaser
    Orb,       // Medium speed/body - basic
    Spike,     // Slow, long body - tanky
    Prism,     // Medium, erratic movement - flanker
    Monolith,  // Boss - very long body
}

impl EnemyType {
    pub fn speed(&self) -> f32 {
        match self {
            EnemyType::Shard => 120.0,   // Fast but slightly slower than player
            EnemyType::Orb => 90.0,
            EnemyType::Spike => 60.0,
            EnemyType::Prism => 100.0,
            EnemyType::Monolith => 50.0,
        }
    }

    pub fn head_radius(&self) -> f32 {
        match self {
            EnemyType::Shard => 10.0,
            EnemyType::Orb => 14.0,
            EnemyType::Spike => 18.0,
            EnemyType::Prism => 16.0,
            EnemyType::Monolith => 30.0,
        }
    }

    pub fn body_radius(&self) -> f32 {
        match self {
            EnemyType::Shard => 5.0,
            EnemyType::Orb => 7.0,
            EnemyType::Spike => 9.0,
            EnemyType::Prism => 7.0,
            EnemyType::Monolith => 15.0,
        }
    }

    pub fn max_body_segments(&self) -> usize {
        match self {
            EnemyType::Shard => 15,
            EnemyType::Orb => 25,
            EnemyType::Spike => 40,
            EnemyType::Prism => 20,
            EnemyType::Monolith => 80,
        }
    }

    /// Essence value dropped on death
    pub fn essence_value(&self) -> u32 {
        match self {
            EnemyType::Shard => 2,
            EnemyType::Orb => 4,
            EnemyType::Spike => 8,
            EnemyType::Prism => 6,
            EnemyType::Monolith => 30,
        }
    }

    pub fn color(&self) -> Color {
        match self {
            EnemyType::Shard => Color::srgb(2.0, 0.4, 0.4),     // Red glow
            EnemyType::Orb => Color::srgb(1.5, 0.8, 1.8),       // Pink glow
            EnemyType::Spike => Color::srgb(2.0, 1.2, 0.4),     // Orange glow
            EnemyType::Prism => Color::srgb(0.8, 1.8, 0.8),     // Green glow
            EnemyType::Monolith => Color::srgb(1.2, 1.2, 2.0),  // White-blue glow
        }
    }
}

// ============================================================================
// WAVE STATE
// ============================================================================

/// Wave spawning state
#[derive(Resource)]
pub struct WaveState {
    pub current_wave: u32,
    pub wave_timer: Timer,
    pub spawn_timer: Timer,
    pub enemies_this_wave: u32,
    pub enemies_spawned: u32,
    pub max_enemies: u32,  // Cap for performance
}

impl Default for WaveState {
    fn default() -> Self {
        Self {
            current_wave: 1,
            wave_timer: Timer::from_seconds(30.0, TimerMode::Repeating),
            spawn_timer: Timer::from_seconds(2.0, TimerMode::Repeating),  // Slower spawns
            enemies_this_wave: 5,  // Start with fewer enemies
            enemies_spawned: 0,
            max_enemies: 30,  // Lower cap since enemies have bodies now
        }
    }
}

impl WaveState {
    /// Get enemy composition for current wave
    pub fn wave_composition(&self) -> Vec<(EnemyType, u32)> {
        let wave = self.current_wave;

        match wave {
            1 => vec![(EnemyType::Shard, 5)],
            2 => vec![(EnemyType::Shard, 6), (EnemyType::Orb, 2)],
            3 => vec![(EnemyType::Shard, 8), (EnemyType::Orb, 3)],
            4 => vec![(EnemyType::Shard, 8), (EnemyType::Orb, 4), (EnemyType::Spike, 1)],
            5 => vec![(EnemyType::Shard, 10), (EnemyType::Orb, 5), (EnemyType::Spike, 2), (EnemyType::Prism, 1)],
            6..=9 => vec![
                (EnemyType::Shard, 8 + wave / 2),
                (EnemyType::Orb, 4 + wave / 3),
                (EnemyType::Spike, 1 + wave / 4),
                (EnemyType::Prism, wave / 3),
            ],
            10 => vec![
                (EnemyType::Shard, 12),
                (EnemyType::Orb, 8),
                (EnemyType::Spike, 4),
                (EnemyType::Prism, 3),
                (EnemyType::Monolith, 1), // Boss!
            ],
            _ => vec![
                (EnemyType::Shard, 10 + wave / 2),
                (EnemyType::Orb, 6 + wave / 3),
                (EnemyType::Spike, 3 + wave / 4),
                (EnemyType::Prism, 2 + wave / 5),
            ],
        }
    }

    /// Pick a random enemy type based on wave composition
    pub fn pick_enemy_type(&self) -> EnemyType {
        let composition = self.wave_composition();
        let total: u32 = composition.iter().map(|(_, count)| count).sum();

        if total == 0 {
            return EnemyType::Shard;
        }

        let mut roll = rand::thread_rng().gen_range(0..total);

        for (enemy_type, count) in composition {
            if roll < count {
                return enemy_type;
            }
            roll -= count;
        }

        EnemyType::Shard
    }
}

// ============================================================================
// SPAWNING
// ============================================================================

/// Spawn a single enemy snake at position
pub fn spawn_enemy(
    commands: &mut Commands,
    position: Vec2,
    enemy_type: EnemyType,
    target: Entity,
) {
    commands.spawn((
        // Identity
        Enemy { enemy_type },
        SnakeHead::enemy(),  // Enemy is a snake head
        ChaseTarget(target),
        Name::new(format!("{:?}", enemy_type)),

        // Transform and rendering
        Sprite {
            color: enemy_type.color(),
            custom_size: Some(Vec2::splat(enemy_type.head_radius() * 2.0)),
            ..default()
        },
        Transform::from_translation(position.extend(5.0)), // Z=5 for enemy head layer

        // Physics
        Velocity::default(),
        MoveSpeed(enemy_type.speed()),
        Radius::new(enemy_type.head_radius()),

        // Body emission (the hazard for player)
        BodyEmitter::enemy(enemy_type.color())
            .with_interval(0.10)
            .with_radius(enemy_type.body_radius())
            .with_max_segments(enemy_type.max_body_segments())
            .with_grace(6),  // First 6 segments are non-lethal
    ));
}

/// Wave spawner system
pub fn wave_spawner(
    mut commands: Commands,
    time: Res<Time>,
    mut wave_state: ResMut<WaveState>,
    player_query: Query<(Entity, &Transform), With<Player>>,
    enemy_count: Query<&Enemy>,
) {
    let Ok((player_entity, player_transform)) = player_query.get_single() else {
        return;
    };

    // Check enemy cap
    let current_enemies = enemy_count.iter().count() as u32;
    if current_enemies >= wave_state.max_enemies {
        return;
    }

    // Tick timers
    wave_state.wave_timer.tick(time.delta());
    wave_state.spawn_timer.tick(time.delta());

    // Check for new wave
    if wave_state.wave_timer.just_finished() {
        wave_state.current_wave += 1;
        wave_state.enemies_spawned = 0;

        // Calculate enemies for this wave
        let composition = wave_state.wave_composition();
        wave_state.enemies_this_wave = composition.iter().map(|(_, count)| count).sum();

        // Speed up spawning as waves progress
        let spawn_interval = (2.0 - wave_state.current_wave as f32 * 0.1).max(0.8);
        wave_state.spawn_timer = Timer::from_seconds(spawn_interval, TimerMode::Repeating);

        info!("Wave {} started! {} enemies incoming", wave_state.current_wave, wave_state.enemies_this_wave);
    }

    // Spawn enemies on timer
    if wave_state.spawn_timer.just_finished() && wave_state.enemies_spawned < wave_state.enemies_this_wave {
        let player_pos = player_transform.translation.truncate();

        // Spawn off-screen at random angle
        let angle = rand::thread_rng().gen_range(0.0..std::f32::consts::TAU);
        let distance = 500.0 + rand::thread_rng().gen_range(0.0..150.0);
        let spawn_pos = player_pos + Vec2::new(angle.cos(), angle.sin()) * distance;

        let enemy_type = wave_state.pick_enemy_type();
        spawn_enemy(&mut commands, spawn_pos, enemy_type, player_entity);

        wave_state.enemies_spawned += 1;
    }
}

// ============================================================================
// ENEMY AI - Simple chase with slight wander
// ============================================================================

/// Simple chase AI - enemies move toward target with slight randomness
pub fn enemy_ai(
    time: Res<Time>,
    mut enemy_query: Query<(&mut Velocity, &MoveSpeed, &Transform, &ChaseTarget, &Enemy)>,
    target_query: Query<&Transform, Without<Enemy>>,
) {
    let dt = time.delta_secs();

    for (mut velocity, speed, enemy_transform, chase_target, enemy) in enemy_query.iter_mut() {
        // Get target position
        let Ok(target_transform) = target_query.get(chase_target.0) else {
            continue;
        };

        let enemy_pos = enemy_transform.translation.truncate();
        let target_pos = target_transform.translation.truncate();

        // Calculate direction to target
        let to_target = target_pos - enemy_pos;
        let distance = to_target.length();

        // Add some randomness/wander to movement (makes it feel more organic)
        let wander = Vec2::new(
            (enemy_pos.x * 0.01 + time.elapsed_secs() * 2.0).sin() * 0.3,
            (enemy_pos.y * 0.01 + time.elapsed_secs() * 2.0).cos() * 0.3,
        );

        // Blend chase direction with wander
        let direction = if distance > 10.0 {
            (to_target.normalize_or_zero() + wander).normalize_or_zero()
        } else {
            wander.normalize_or_zero()
        };

        // Smooth velocity (snake-like turning)
        let target_velocity = direction * speed.0;
        let lerp_speed = 4.0;  // Slower turning than player

        velocity.0 = velocity.0.lerp(target_velocity, lerp_speed * dt);
    }
}

/// Keep enemies within arena bounds
pub fn clamp_enemies_to_arena(
    mut query: Query<&mut Transform, With<Enemy>>,
) {
    const ARENA_RADIUS: f32 = 950.0;

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

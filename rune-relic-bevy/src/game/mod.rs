//! Game module - True Slither.io style snake combat
//!
//! Rules:
//! - You die only if YOUR HEAD hits THEIR BODY
//! - They die only if THEIR HEAD hits YOUR BODY
//! - Head-to-head = no death
//! - Grace zone: first N segments are non-lethal

use bevy::prelude::*;
use crate::AppState;

pub mod components;
pub mod player;
pub mod enemies;
pub mod combat;
pub mod trail;
pub mod visuals;

pub use components::*;
pub use player::*;
pub use enemies::*;
pub use combat::*;
pub use trail::BodyState;
pub use visuals::*;

// ============================================================================
// PRESERVED - Core game identity
// ============================================================================

/// Player evolution forms - from Spark to Ancient
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default, Component)]
pub enum PlayerForm {
    #[default]
    Spark,
    Glyph,
    Ward,
    Arcane,
    Ancient,
}

impl PlayerForm {
    /// Visual radius at this form
    pub fn radius(&self) -> f32 {
        match self {
            PlayerForm::Spark => 16.0,
            PlayerForm::Glyph => 20.0,
            PlayerForm::Ward => 25.0,
            PlayerForm::Arcane => 32.0,
            PlayerForm::Ancient => 40.0,
        }
    }

    /// Movement speed at this form
    pub fn speed(&self) -> f32 {
        match self {
            PlayerForm::Spark => 180.0,
            PlayerForm::Glyph => 170.0,
            PlayerForm::Ward => 160.0,
            PlayerForm::Arcane => 150.0,
            PlayerForm::Ancient => 140.0,
        }
    }

    /// Color for this form (HDR for bloom)
    pub fn color(&self) -> Color {
        match self {
            PlayerForm::Spark => Color::srgb(0.6, 1.2, 2.0),    // Cyan glow
            PlayerForm::Glyph => Color::srgb(0.8, 1.8, 1.0),    // Green glow
            PlayerForm::Ward => Color::srgb(2.0, 1.6, 0.4),     // Gold glow
            PlayerForm::Arcane => Color::srgb(1.6, 0.6, 2.0),   // Purple glow
            PlayerForm::Ancient => Color::srgb(2.0, 0.8, 0.4),  // Orange glow
        }
    }
}

// ============================================================================
// GAME PLUGIN
// ============================================================================

pub struct GamePlugin;

impl Plugin for GamePlugin {
    fn build(&self, app: &mut App) {
        app
            // Resources
            .init_resource::<GameTime>()
            .init_resource::<WaveState>()
            .init_resource::<BodyState>()

            // Events
            .add_event::<EnemyDeathEvent>()
            .add_event::<PlayerDeathEvent>()

            // Game setup/cleanup
            .add_systems(OnEnter(AppState::Playing), setup_game)
            .add_systems(OnExit(AppState::Playing), cleanup_game)

            // Core gameplay (fixed timestep for consistency)
            .add_systems(FixedUpdate, (
                // Player systems
                player_input,
                apply_velocity,
                clamp_to_arena,

                // Enemy systems
                enemy_ai,
                clamp_enemies_to_arena,
                wave_spawner,

                // Body segment spawning (for both player and enemies)
                trail::spawn_body_segments,

                // TRUE SLITHER.IO COLLISION
                // Player head vs enemy body = player dies
                player_head_vs_enemy_body,
                // Enemy head vs player body = enemy dies
                enemy_head_vs_player_body,

                // Handle deaths
                handle_player_death,

                // Essence collection
                trail::essence_magnetism,
                trail::move_magnetized_essence,
                trail::collect_essence,
            ).chain().run_if(in_state(AppState::Playing)))

            // Visual updates (variable timestep)
            .add_systems(Update, (
                camera_follow,
                spawn_death_particles,
                update_death_particles,
                update_hud,
            ).run_if(in_state(AppState::Playing)))

            // Game over state
            .add_systems(OnEnter(AppState::GameOver), setup_game_over)
            .add_systems(Update, handle_game_over_input.run_if(in_state(AppState::GameOver)))
            .add_systems(OnExit(AppState::GameOver), cleanup_game_over);
    }
}

// ============================================================================
// RESOURCES
// ============================================================================

/// Game timer
#[derive(Resource, Default)]
pub struct GameTime {
    pub elapsed: f32,
}

// ============================================================================
// EVENTS
// ============================================================================

#[derive(Event)]
pub struct EnemyDeathEvent {
    pub position: Vec2,
    pub enemy_type: EnemyType,
}

#[derive(Event)]
pub struct PlayerDeathEvent {
    pub position: Vec2,
    pub player_entity: Entity,
}

// ============================================================================
// GAME SETUP
// ============================================================================

fn setup_game(
    mut commands: Commands,
    mut game_time: ResMut<GameTime>,
    mut wave_state: ResMut<WaveState>,
    mut body_state: ResMut<BodyState>,
) {
    info!("Setting up True Slither.io style game...");

    // Reset game state
    *game_time = GameTime::default();
    *wave_state = WaveState::default();
    *body_state = BodyState::default();

    // Spawn player at center
    spawn_player(&mut commands, Vec2::ZERO);

    // Spawn arena background
    commands.spawn((
        Sprite {
            color: Color::srgba(0.08, 0.08, 0.12, 0.8),
            custom_size: Some(Vec2::splat(2000.0)),
            ..default()
        },
        Transform::from_translation(Vec3::new(0.0, 0.0, -100.0)),
        ArenaBackground,
    ));

    // Spawn arena boundary visualization
    spawn_arena_boundary(&mut commands);

    info!("Game ready! Cut off enemies with your body!");
}

/// Spawn arena boundary ring (visual only, no collision death)
fn spawn_arena_boundary(commands: &mut Commands) {
    const ARENA_RADIUS: f32 = 900.0;
    const RING_SEGMENTS: usize = 64;

    for i in 0..RING_SEGMENTS {
        let angle = (i as f32 / RING_SEGMENTS as f32) * std::f32::consts::TAU;
        let pos = Vec2::new(angle.cos(), angle.sin()) * ARENA_RADIUS;

        commands.spawn((
            Sprite {
                color: Color::srgba(0.3, 0.4, 0.6, 0.5),
                custom_size: Some(Vec2::splat(20.0)),
                ..default()
            },
            Transform::from_translation(pos.extend(-50.0)),
            ArenaBackground,
        ));
    }
}

fn cleanup_game(
    mut commands: Commands,
    entities: Query<Entity, Or<(
        With<Player>,
        With<Enemy>,
        With<BodySegment>,
        With<Essence>,
        With<DeathParticle>,
        With<ArenaBackground>,
    )>>,
) {
    for entity in entities.iter() {
        commands.entity(entity).despawn_recursive();
    }
    info!("Game cleaned up");
}

/// Marker for arena background
#[derive(Component)]
pub struct ArenaBackground;

// ============================================================================
// HUD
// ============================================================================

fn update_hud(
    time: Res<Time>,
    mut game_time: ResMut<GameTime>,
    body_state: Res<BodyState>,
    player_query: Query<Entity, With<LocalPlayer>>,
    wave_state: Res<WaveState>,
) {
    game_time.elapsed += time.delta_secs();

    // HUD updates - track state for UI
    if let Ok(player_entity) = player_query.get_single() {
        let _body_length = body_state.segment_count(player_entity);
        let _wave = wave_state.current_wave;
        // UI module reads these directly
    }
}

// ============================================================================
// GAME OVER
// ============================================================================

#[derive(Component)]
struct GameOverUI;

fn setup_game_over(mut commands: Commands, game_time: Res<GameTime>) {
    info!("Game Over! Survived {:.1}s", game_time.elapsed);

    commands.spawn((
        Node {
            width: Val::Percent(100.0),
            height: Val::Percent(100.0),
            flex_direction: FlexDirection::Column,
            justify_content: JustifyContent::Center,
            align_items: AlignItems::Center,
            ..default()
        },
        BackgroundColor(Color::srgba(0.0, 0.0, 0.0, 0.85)),
        GameOverUI,
    )).with_children(|parent| {
        parent.spawn((
            Text::new("GAME OVER"),
            TextFont {
                font_size: 64.0,
                ..default()
            },
            TextColor(Color::srgb(1.0, 0.3, 0.3)),
            Node {
                margin: UiRect::bottom(Val::Px(20.0)),
                ..default()
            },
        ));

        parent.spawn((
            Text::new(format!("Survived: {:.1}s", game_time.elapsed)),
            TextFont {
                font_size: 32.0,
                ..default()
            },
            TextColor(Color::WHITE),
            Node {
                margin: UiRect::bottom(Val::Px(20.0)),
                ..default()
            },
        ));

        parent.spawn((
            Text::new("Your HEAD hit an enemy BODY!"),
            TextFont {
                font_size: 20.0,
                ..default()
            },
            TextColor(Color::srgb(0.8, 0.6, 0.6)),
            Node {
                margin: UiRect::bottom(Val::Px(40.0)),
                ..default()
            },
        ));

        parent.spawn((
            Text::new("Press SPACE to restart or ESC for menu"),
            TextFont {
                font_size: 20.0,
                ..default()
            },
            TextColor(Color::srgb(0.6, 0.6, 0.7)),
        ));
    });
}

fn handle_game_over_input(
    keyboard: Res<ButtonInput<KeyCode>>,
    mut next_state: ResMut<NextState<AppState>>,
) {
    if keyboard.just_pressed(KeyCode::Space) {
        next_state.set(AppState::Playing);
    } else if keyboard.just_pressed(KeyCode::Escape) {
        next_state.set(AppState::MainMenu);
    }
}

fn cleanup_game_over(
    mut commands: Commands,
    query: Query<Entity, With<GameOverUI>>,
) {
    for entity in query.iter() {
        commands.entity(entity).despawn_recursive();
    }
}

//! Rune Relic 2D - Vampire Survivors-style Game
//!
//! A top-down survivor game where players collect XP gems,
//! auto-attack enemies, and evolve through forms.

mod network;
mod game;
mod ui;

use bevy::prelude::*;
use bevy::window::WindowMode;
use bevy::core_pipeline::bloom::Bloom;
use bevy::core_pipeline::tonemapping::Tonemapping;
use bevy::render::camera::ScalingMode;

use game::GamePlugin;
use network::NetworkPlugin;
use ui::UiPlugin;

/// Game states
#[derive(States, Debug, Clone, Copy, Eq, PartialEq, Hash, Default)]
pub enum AppState {
    #[default]
    MainMenu,
    Connecting,
    Matchmaking,
    ReadyCheck,
    Playing,
    LevelUp,  // Pause state for upgrade selection
    GameOver,
}

fn main() {
    App::new()
        // Bevy defaults with custom window
        .add_plugins(DefaultPlugins.set(WindowPlugin {
            primary_window: Some(Window {
                title: "Rune Relic".into(),
                resolution: (1280., 720.).into(),
                mode: WindowMode::Windowed,
                ..default()
            }),
            ..default()
        }))
        // Async runtime for networking
        .add_plugins(bevy_tokio_tasks::TokioTasksPlugin::default())
        // Game state
        .init_state::<AppState>()
        // Our plugins
        .add_plugins((
            NetworkPlugin,
            GamePlugin,
            UiPlugin,
        ))
        // Startup
        .add_systems(Startup, setup_2d_camera)
        .run();
}

/// 2D Camera setup with bloom for glowing vector graphics
fn setup_2d_camera(mut commands: Commands) {
    commands.spawn((
        Camera2d,
        Camera {
            hdr: true,  // Required for bloom
            clear_color: ClearColorConfig::Custom(Color::srgb(0.02, 0.02, 0.05)), // Dark blue-black
            ..default()
        },
        OrthographicProjection {
            // Show approximately 800 units of game world vertically
            scaling_mode: ScalingMode::FixedVertical { viewport_height: 800.0 },
            near: -1000.0,
            far: 1000.0,
            ..OrthographicProjection::default_2d()
        },
        Tonemapping::TonyMcMapface,
        Bloom {
            intensity: 0.3,
            low_frequency_boost: 0.6,
            low_frequency_boost_curvature: 0.9,
            high_pass_frequency: 1.0,
            prefilter: bevy::core_pipeline::bloom::BloomPrefilter {
                threshold: 0.8,
                threshold_softness: 0.3,
            },
            ..default()
        },
    ));

    info!("Rune Relic 2D initialized!");
}

//! UI module - menus and HUD for true Slither.io style game

use bevy::prelude::*;
use crate::AppState;
use crate::network::{NetworkEvent, ClientMessage, OutgoingChannel};
use crate::game::{GameTime, WaveState, BodyState, BodyEmitter, LocalPlayer};

pub struct UiPlugin;

impl Plugin for UiPlugin {
    fn build(&self, app: &mut App) {
        app
            .add_systems(OnEnter(AppState::MainMenu), setup_main_menu)
            .add_systems(OnExit(AppState::MainMenu), cleanup_main_menu)
            .add_systems(OnEnter(AppState::Connecting), setup_connecting_ui)
            .add_systems(OnExit(AppState::Connecting), cleanup_connecting_ui)
            .add_systems(OnEnter(AppState::Matchmaking), (setup_matchmaking_ui, send_matchmaking_request))
            .add_systems(OnExit(AppState::Matchmaking), cleanup_matchmaking_ui)
            .add_systems(OnEnter(AppState::Playing), setup_hud)
            .add_systems(OnExit(AppState::Playing), cleanup_hud)
            .add_systems(Update, (
                handle_menu_buttons.run_if(in_state(AppState::MainMenu)),
                update_hud.run_if(in_state(AppState::Playing)),
            ));
    }
}

/// Marker for main menu UI
#[derive(Component)]
struct MainMenuUI;

/// Marker for connecting UI
#[derive(Component)]
struct ConnectingUI;

/// Marker for matchmaking UI
#[derive(Component)]
struct MatchmakingUI;

/// Marker for HUD UI
#[derive(Component)]
struct HudUI;

/// Button actions
#[derive(Component)]
enum MenuButton {
    Play,
    Practice,
    Quit,
}

/// Marker for wave text
#[derive(Component)]
struct WaveText;

/// Marker for trail length text
#[derive(Component)]
struct TrailText;

/// Marker for shield text
#[derive(Component)]
struct ShieldText;

/// Marker for time text
#[derive(Component)]
struct TimeText;

/// Setup main menu UI
fn setup_main_menu(mut commands: Commands) {
    info!("Setting up main menu...");

    // Root container
    commands.spawn((
        Node {
            width: Val::Percent(100.0),
            height: Val::Percent(100.0),
            flex_direction: FlexDirection::Column,
            justify_content: JustifyContent::Center,
            align_items: AlignItems::Center,
            ..default()
        },
        BackgroundColor(Color::srgba(0.02, 0.02, 0.05, 0.98)),
        MainMenuUI,
    )).with_children(|parent| {
        // Title
        parent.spawn((
            Text::new("RUNE RELIC"),
            TextFont {
                font_size: 72.0,
                ..default()
            },
            TextColor(Color::srgb(0.4, 0.8, 1.2)),
            Node {
                margin: UiRect::bottom(Val::Px(10.0)),
                ..default()
            },
        ));

        // Subtitle
        parent.spawn((
            Text::new("Survive the Waves"),
            TextFont {
                font_size: 24.0,
                ..default()
            },
            TextColor(Color::srgb(0.5, 0.6, 0.8)),
            Node {
                margin: UiRect::bottom(Val::Px(40.0)),
                ..default()
            },
        ));

        // Play Online button
        spawn_menu_button(parent, "PLAY ONLINE", MenuButton::Play);

        // Practice button
        spawn_menu_button(parent, "PRACTICE", MenuButton::Practice);

        // Quit button
        spawn_menu_button(parent, "QUIT", MenuButton::Quit);

        // Instructions
        parent.spawn((
            Text::new("WASD to move | Enemies die on your trail | Avoid head-on collisions"),
            TextFont {
                font_size: 16.0,
                ..default()
            },
            TextColor(Color::srgb(0.4, 0.4, 0.5)),
            Node {
                margin: UiRect::top(Val::Px(40.0)),
                ..default()
            },
        ));
    });
}

/// Spawn a menu button
fn spawn_menu_button(parent: &mut ChildBuilder, text: &str, action: MenuButton) {
    parent.spawn((
        Button,
        Node {
            width: Val::Px(280.0),
            height: Val::Px(60.0),
            margin: UiRect::all(Val::Px(10.0)),
            justify_content: JustifyContent::Center,
            align_items: AlignItems::Center,
            border: UiRect::all(Val::Px(2.0)),
            ..default()
        },
        BorderColor(Color::srgb(0.3, 0.5, 0.8)),
        BackgroundColor(Color::srgb(0.15, 0.2, 0.35)),
        action,
    )).with_children(|parent| {
        parent.spawn((
            Text::new(text),
            TextFont {
                font_size: 26.0,
                ..default()
            },
            TextColor(Color::WHITE),
        ));
    });
}

/// Handle menu button clicks
fn handle_menu_buttons(
    mut interaction_query: Query<
        (&Interaction, &MenuButton, &mut BackgroundColor, &mut BorderColor),
        Changed<Interaction>,
    >,
    mut app_state: ResMut<NextState<AppState>>,
    mut network_events: EventWriter<NetworkEvent>,
) {
    for (interaction, button, mut bg_color, mut border_color) in interaction_query.iter_mut() {
        match *interaction {
            Interaction::Pressed => {
                match button {
                    MenuButton::Play => {
                        info!("Play Online clicked!");
                        network_events.send(NetworkEvent::Connect("ws://127.0.0.1:8080".to_string()));
                        app_state.set(AppState::Connecting);
                    }
                    MenuButton::Practice => {
                        info!("Practice clicked!");
                        app_state.set(AppState::Playing);
                    }
                    MenuButton::Quit => {
                        info!("Quit clicked!");
                        std::process::exit(0);
                    }
                }
                *bg_color = BackgroundColor(Color::srgb(0.3, 0.5, 0.8));
            }
            Interaction::Hovered => {
                *bg_color = BackgroundColor(Color::srgb(0.2, 0.35, 0.6));
                *border_color = BorderColor(Color::srgb(0.5, 0.7, 1.0));
            }
            Interaction::None => {
                *bg_color = BackgroundColor(Color::srgb(0.15, 0.2, 0.35));
                *border_color = BorderColor(Color::srgb(0.3, 0.5, 0.8));
            }
        }
    }
}

/// Cleanup main menu
fn cleanup_main_menu(
    mut commands: Commands,
    query: Query<Entity, With<MainMenuUI>>,
) {
    for entity in query.iter() {
        commands.entity(entity).despawn_recursive();
    }
}

/// Setup connecting UI
fn setup_connecting_ui(mut commands: Commands) {
    commands.spawn((
        Node {
            width: Val::Percent(100.0),
            height: Val::Percent(100.0),
            flex_direction: FlexDirection::Column,
            justify_content: JustifyContent::Center,
            align_items: AlignItems::Center,
            ..default()
        },
        BackgroundColor(Color::srgba(0.02, 0.02, 0.05, 0.98)),
        ConnectingUI,
    )).with_children(|parent| {
        parent.spawn((
            Text::new("Connecting to server..."),
            TextFont {
                font_size: 32.0,
                ..default()
            },
            TextColor(Color::srgb(0.4, 0.7, 1.0)),
        ));
    });
}

fn cleanup_connecting_ui(
    mut commands: Commands,
    query: Query<Entity, With<ConnectingUI>>,
) {
    for entity in query.iter() {
        commands.entity(entity).despawn_recursive();
    }
}

/// Setup matchmaking UI
fn setup_matchmaking_ui(mut commands: Commands) {
    commands.spawn((
        Node {
            width: Val::Percent(100.0),
            height: Val::Percent(100.0),
            flex_direction: FlexDirection::Column,
            justify_content: JustifyContent::Center,
            align_items: AlignItems::Center,
            ..default()
        },
        BackgroundColor(Color::srgba(0.02, 0.02, 0.05, 0.98)),
        MatchmakingUI,
    )).with_children(|parent| {
        parent.spawn((
            Text::new("Finding match..."),
            TextFont {
                font_size: 36.0,
                ..default()
            },
            TextColor(Color::srgb(0.4, 0.7, 1.0)),
        ));
    });
}

fn send_matchmaking_request(
    outgoing: Option<Res<OutgoingChannel>>,
) {
    if let Some(channel) = outgoing {
        let matchmaking_msg = ClientMessage::Matchmaking {
            mode: "casual".to_string(),
            match_id: None,
            commitment: None,
        };

        let json = serde_json::to_string(&matchmaking_msg).unwrap_or_default();

        if let Err(e) = channel.sender.try_send(json) {
            error!("Failed to send matchmaking request: {}", e);
        }
    }
}

fn cleanup_matchmaking_ui(
    mut commands: Commands,
    query: Query<Entity, With<MatchmakingUI>>,
) {
    for entity in query.iter() {
        commands.entity(entity).despawn_recursive();
    }
}

/// Setup in-game HUD for Slither.io style gameplay
fn setup_hud(mut commands: Commands) {
    // Top bar with wave, trail length, time
    commands.spawn((
        Node {
            width: Val::Percent(100.0),
            height: Val::Px(60.0),
            position_type: PositionType::Absolute,
            top: Val::Px(0.0),
            left: Val::Px(0.0),
            padding: UiRect::all(Val::Px(10.0)),
            flex_direction: FlexDirection::Row,
            justify_content: JustifyContent::SpaceBetween,
            align_items: AlignItems::Center,
            ..default()
        },
        BackgroundColor(Color::srgba(0.0, 0.0, 0.0, 0.6)),
        HudUI,
    )).with_children(|parent| {
        // Wave display (left)
        parent.spawn((
            Text::new("Wave 1"),
            TextFont {
                font_size: 24.0,
                ..default()
            },
            TextColor(Color::srgb(1.0, 0.6, 0.3)),
            WaveText,
        ));

        // Trail length (center)
        parent.spawn((
            Text::new("Trail: 0"),
            TextFont {
                font_size: 24.0,
                ..default()
            },
            TextColor(Color::srgb(0.4, 1.0, 0.8)),
            TrailText,
        ));

        // Time display (right)
        parent.spawn((
            Text::new("0:00"),
            TextFont {
                font_size: 24.0,
                ..default()
            },
            TextColor(Color::WHITE),
            TimeText,
        ));
    });

    // Shield display (bottom left)
    commands.spawn((
        Node {
            position_type: PositionType::Absolute,
            bottom: Val::Px(20.0),
            left: Val::Px(20.0),
            ..default()
        },
        HudUI,
    )).with_children(|parent| {
        parent.spawn((
            Text::new("Shield: 1"),
            TextFont {
                font_size: 20.0,
                ..default()
            },
            TextColor(Color::srgb(0.5, 0.8, 1.0)),
            ShieldText,
        ));
    });
}

/// Update HUD values
fn update_hud(
    game_time: Res<GameTime>,
    wave_state: Res<WaveState>,
    body_state: Res<BodyState>,
    player_query: Query<(Entity, Option<&BodyEmitter>), With<LocalPlayer>>,
    mut wave_text: Query<&mut Text, (With<WaveText>, Without<TrailText>, Without<TimeText>, Without<ShieldText>)>,
    mut trail_text: Query<&mut Text, (With<TrailText>, Without<WaveText>, Without<TimeText>, Without<ShieldText>)>,
    mut time_text: Query<&mut Text, (With<TimeText>, Without<WaveText>, Without<TrailText>, Without<ShieldText>)>,
    mut shield_text: Query<&mut Text, (With<ShieldText>, Without<WaveText>, Without<TrailText>, Without<TimeText>)>,
) {
    // Update wave text
    if let Ok(mut text) = wave_text.get_single_mut() {
        **text = format!("Wave {}", wave_state.current_wave);
    }

    // Update time text (elapsed time)
    if let Ok(mut text) = time_text.get_single_mut() {
        let elapsed = game_time.elapsed;
        let minutes = (elapsed / 60.0) as u32;
        let seconds = (elapsed % 60.0) as u32;
        **text = format!("{}:{:02}", minutes, seconds);
    }

    // Update body length and max capacity if player exists
    if let Ok((player_entity, emitter)) = player_query.get_single() {
        // Update body length
        if let Ok(mut text) = trail_text.get_single_mut() {
            let body_length = body_state.segment_count(player_entity);
            **text = format!("Body: {}", body_length);
        }

        // Update max body capacity (replaces shield)
        if let Ok(mut text) = shield_text.get_single_mut() {
            let max_segments = emitter.map(|e| e.max_segments).unwrap_or(60);
            **text = format!("Max: {}", max_segments);
        }
    }
}

/// Cleanup HUD
fn cleanup_hud(
    mut commands: Commands,
    query: Query<Entity, With<HudUI>>,
) {
    for entity in query.iter() {
        commands.entity(entity).despawn_recursive();
    }
}


//! Network module - WebSocket connection to game server

mod messages;
mod client;

pub use messages::*;
pub use client::*;

use bevy::prelude::*;

use crate::AppState;

pub struct NetworkPlugin;

impl Plugin for NetworkPlugin {
    fn build(&self, app: &mut App) {
        app
            .init_resource::<NetworkState>()
            .init_resource::<ServerMessages>()
            .add_event::<NetworkEvent>()
            .add_event::<SendMessage>()
            // Connection systems
            .add_systems(OnEnter(AppState::Connecting), connect_to_server)
            // Auth sent after a brief delay to ensure connection is established
            .add_systems(Update, (
                check_and_send_auth.run_if(in_state(AppState::Connecting)),
                poll_incoming_messages,
                handle_network_events,
                process_server_messages,
                send_queued_messages,
            ));
    }
}

/// Track if we've sent auth
#[derive(Resource, Default)]
pub struct AuthState {
    pub auth_sent: bool,
    pub connect_timer: f32,
}

/// Network connection state
#[derive(Resource, Default)]
pub struct NetworkState {
    pub connected: bool,
    pub authenticated: bool,
    pub in_match: bool,
    pub player_id: Option<[u8; 16]>,
    pub session_id: Option<String>,
    pub ping_ms: u32,
}

/// Queue of messages received from server
#[derive(Resource, Default)]
pub struct ServerMessages {
    pub messages: Vec<ServerMessage>,
}

/// Events for network state changes
#[derive(Event)]
pub enum NetworkEvent {
    Connect(String),
    Disconnect,
    Connected,
    Disconnected,
    AuthSuccess { session_id: String },
    AuthFailed { error: String },
    MatchFound { match_id: [u8; 16], players: Vec<[u8; 16]> },
    MatchStart { rng_seed: u64 },
    MatchEnd { winner: Option<[u8; 16]> },
    Error(String),
}

/// Event to send a message to the server
#[derive(Event)]
pub struct SendMessage(pub ClientMessage);

/// Check if we should send auth (with slight delay for connection)
fn check_and_send_auth(
    time: Res<Time>,
    mut auth_state: Local<AuthState>,
    outgoing: Option<Res<OutgoingChannel>>,
    mut state: ResMut<NetworkState>,
) {
    // Wait a bit for connection to establish
    auth_state.connect_timer += time.delta_secs();

    if auth_state.connect_timer > 0.5 && !auth_state.auth_sent {
        if let Some(channel) = outgoing {
            // Generate a random player ID
            let player_id: [u8; 16] = rand::random();
            let player_id_hex = hex::encode(player_id);

            let auth_msg = ClientMessage::Auth {
                player_id: player_id_hex.clone(),
                token: String::new(),
                client_version: "0.1.0".to_string(),
            };

            let json = serde_json::to_string(&auth_msg).unwrap_or_default();

            if let Err(e) = channel.sender.try_send(json) {
                error!("Failed to queue auth message: {}", e);
            } else {
                info!("Auth message sent with player_id: {}", player_id_hex);
                state.player_id = Some(player_id);
                auth_state.auth_sent = true;
            }
        }
    }
}

/// Handle network events (connect, disconnect, etc.)
fn handle_network_events(
    mut events: EventReader<NetworkEvent>,
    mut state: ResMut<NetworkState>,
    mut app_state: ResMut<NextState<AppState>>,
) {
    for event in events.read() {
        match event {
            NetworkEvent::Connect(url) => {
                info!("Connecting to {}", url);
            }
            NetworkEvent::Disconnect => {
                info!("Disconnect requested");
                state.connected = false;
            }
            NetworkEvent::Connected => {
                info!("Connected to server");
                state.connected = true;
            }
            NetworkEvent::Disconnected => {
                info!("Disconnected from server");
                state.connected = false;
                state.authenticated = false;
                state.in_match = false;
                app_state.set(AppState::MainMenu);
            }
            NetworkEvent::AuthSuccess { session_id } => {
                info!("Authenticated! Session: {}", session_id);
                state.authenticated = true;
                state.session_id = Some(session_id.clone());
                // Transition to matchmaking
                app_state.set(AppState::Matchmaking);
            }
            NetworkEvent::AuthFailed { error } => {
                error!("Auth failed: {}", error);
                state.authenticated = false;
                app_state.set(AppState::MainMenu);
            }
            NetworkEvent::MatchFound { match_id, players } => {
                info!("Match found! {} players", players.len());
                app_state.set(AppState::ReadyCheck);
            }
            NetworkEvent::MatchStart { rng_seed } => {
                info!("Match starting! Seed: {}", rng_seed);
                state.in_match = true;
                app_state.set(AppState::Playing);
            }
            NetworkEvent::MatchEnd { winner } => {
                info!("Match ended!");
                state.in_match = false;
                app_state.set(AppState::GameOver);
            }
            NetworkEvent::Error(msg) => {
                error!("Network error: {}", msg);
            }
        }
    }
}

/// Process messages from server
fn process_server_messages(
    mut messages: ResMut<ServerMessages>,
    mut events: EventWriter<NetworkEvent>,
    mut state: ResMut<NetworkState>,
    mut app_state: ResMut<NextState<AppState>>,
    outgoing: Option<Res<OutgoingChannel>>,
) {
    for msg in messages.messages.drain(..) {
        match msg {
            ServerMessage::AuthResult { success, session_id, error, .. } => {
                if success {
                    if let Some(sid) = session_id {
                        events.send(NetworkEvent::AuthSuccess { session_id: sid });
                    }
                } else {
                    events.send(NetworkEvent::AuthFailed {
                        error: error.unwrap_or_else(|| "Unknown error".to_string())
                    });
                }
            }
            ServerMessage::Matchmaking { status, estimated_wait, players_found, players_needed } => {
                info!("Matchmaking status: {} - found {}/{}", status, players_found, players_needed);
            }
            ServerMessage::MatchFound { match_id, player_ids, ready_timeout, .. } => {
                info!("Match found! {} players, timeout: {}s", player_ids.len(), ready_timeout);

                // Auto-send ready (for testing)
                if let Some(channel) = &outgoing {
                    let ready_msg = ClientMessage::Ready;
                    let json = serde_json::to_string(&ready_msg).unwrap_or_default();
                    let _ = channel.sender.try_send(json);
                    info!("Sent Ready message");
                }

                events.send(NetworkEvent::MatchFound {
                    match_id,
                    players: player_ids
                });
            }
            ServerMessage::MatchStart { rng_seed, .. } => {
                events.send(NetworkEvent::MatchStart { rng_seed });
            }
            ServerMessage::MatchEnd { winner_id, .. } => {
                events.send(NetworkEvent::MatchEnd { winner: winner_id });
            }
            ServerMessage::Pong { timestamp, server_time } => {
                let now = std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .unwrap_or_default()
                    .as_millis() as u64;
                state.ping_ms = (now - timestamp) as u32;
            }
            ServerMessage::State { tick, time_remaining, players, runes, .. } => {
                // Game state update - would update game entities here
                // For now just log occasionally
                if tick % 60 == 0 {
                    let rune_count = runes.as_ref().map(|list| list.len()).unwrap_or(0);
                    info!("Game tick {} - {} players, {} runes, time: {}s",
                        tick, players.len(), rune_count, time_remaining / 60);
                }
            }
            ServerMessage::Event(game_event) => {
                info!("Game event: {:?}", game_event);
                // Handle countdown events
                if let GameEvent::Countdown { seconds } = game_event {
                    info!("Match starting in {} seconds!", seconds);
                }
            }
            _ => {}
        }
    }
}

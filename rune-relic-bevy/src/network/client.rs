//! WebSocket client for connecting to game server

use bevy::prelude::*;
use bevy_tokio_tasks::TokioTasksRuntime;
use tokio::sync::mpsc;
use tokio_tungstenite::{connect_async, tungstenite::Message};
use futures_util::{StreamExt, SinkExt};
use std::sync::{Arc, Mutex};

use super::{ClientMessage, ServerMessage, ServerMessages};

/// Thread-safe channel for sending messages to server
#[derive(Resource)]
pub struct OutgoingChannel {
    pub sender: mpsc::Sender<String>,
}

/// Thread-safe queue for receiving messages from server
#[derive(Resource, Default)]
pub struct IncomingMessages {
    pub queue: Arc<Mutex<Vec<ServerMessage>>>,
}

/// System to initiate connection when entering Connecting state
pub fn connect_to_server(
    runtime: Res<TokioTasksRuntime>,
    mut commands: Commands,
) {
    info!("Initiating connection to server...");

    // Create channels for communication
    let (outgoing_tx, mut outgoing_rx) = mpsc::channel::<String>(100);
    let incoming_queue = Arc::new(Mutex::new(Vec::<ServerMessage>::new()));
    let incoming_queue_clone = incoming_queue.clone();

    // Insert resources
    commands.insert_resource(OutgoingChannel { sender: outgoing_tx });
    commands.insert_resource(IncomingMessages { queue: incoming_queue });

    // Spawn the WebSocket task
    runtime.spawn_background_task(move |_ctx| async move {
        let url = "ws://127.0.0.1:8080";
        info!("Connecting to {}...", url);

        match connect_async(url).await {
            Ok((ws_stream, _)) => {
                info!("WebSocket connected!");

                let (mut write, mut read) = ws_stream.split();
                let queue_for_reader = incoming_queue_clone.clone();

                // Spawn reader task
                let reader_handle = tokio::spawn(async move {
                    while let Some(msg_result) = read.next().await {
                        match msg_result {
                            Ok(Message::Text(text)) => {
                                match serde_json::from_str::<ServerMessage>(&text) {
                                    Ok(server_msg) => {
                                        info!("Received: {:?}", server_msg);
                                        if let Ok(mut queue) = queue_for_reader.lock() {
                                            queue.push(server_msg);
                                        }
                                    }
                                    Err(e) => {
                                        warn!("Failed to parse server message: {} - {}", e, text);
                                    }
                                }
                            }
                            Ok(Message::Close(_)) => {
                                info!("Server closed connection");
                                break;
                            }
                            Ok(Message::Ping(data)) => {
                                info!("Received ping from server");
                            }
                            Err(e) => {
                                error!("WebSocket read error: {}", e);
                                break;
                            }
                            _ => {}
                        }
                    }
                    info!("Reader task ended");
                });

                // Writer loop - send messages from channel
                while let Some(json) = outgoing_rx.recv().await {
                    info!("Sending: {}", json);
                    if let Err(e) = write.send(Message::Text(json)).await {
                        error!("Failed to send message: {}", e);
                        break;
                    }
                }

                info!("Writer loop ended");
                reader_handle.abort();
            }
            Err(e) => {
                error!("Failed to connect to server: {}", e);
            }
        }
    });
}

/// System to poll incoming messages and process them
pub fn poll_incoming_messages(
    incoming: Option<Res<IncomingMessages>>,
    mut server_messages: ResMut<ServerMessages>,
) {
    if let Some(incoming) = incoming {
        if let Ok(mut queue) = incoming.queue.lock() {
            for msg in queue.drain(..) {
                server_messages.messages.push(msg);
            }
        }
    }
}

/// System to send queued messages through the channel
pub fn send_queued_messages(
    mut events: EventReader<super::SendMessage>,
    outgoing: Option<Res<OutgoingChannel>>,
) {
    if let Some(channel) = outgoing {
        for super::SendMessage(msg) in events.read() {
            let json = serde_json::to_string(msg).unwrap_or_default();
            if let Err(e) = channel.sender.try_send(json) {
                error!("Failed to queue message: {}", e);
            }
        }
    }
}

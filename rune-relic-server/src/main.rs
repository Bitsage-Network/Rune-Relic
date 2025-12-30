//! Rune Relic Game Server
//!
//! Authoritative game server for Rune Relic.
//! Runs deterministic simulation that can be verified by BitSage.

use tracing::{info, Level};
use tracing_subscriber::FmtSubscriber;

use rune_relic::{
    TICK_RATE, MATCH_DURATION_TICKS, VERSION,
    network::server::{GameServer, ServerConfig},
};

#[tokio::main]
async fn main() {
    // Initialize logging
    let subscriber = FmtSubscriber::builder()
        .with_max_level(Level::INFO)
        .finish();
    tracing::subscriber::set_global_default(subscriber)
        .expect("Failed to set tracing subscriber");

    info!("Rune Relic Server v{}", VERSION);
    info!("Tick Rate: {} Hz", TICK_RATE);
    info!("Match Duration: {} ticks ({} seconds)", MATCH_DURATION_TICKS, MATCH_DURATION_TICKS / TICK_RATE);

    // Create server config
    let config = ServerConfig::default();
    info!("Starting WebSocket server on {}", config.bind_addr);

    // Create and run server
    let server = GameServer::new(config);

    if let Err(e) = server.run().await {
        tracing::error!("Server error: {}", e);
    }
}

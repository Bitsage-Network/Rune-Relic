//! Rune Relic Game Server
//!
//! Authoritative game server for Rune Relic.
//! Runs deterministic simulation that can be verified by BitSage.

use std::collections::BTreeMap;
use tracing::{info, Level};
use tracing_subscriber::FmtSubscriber;

use rune_relic::{
    TICK_RATE, MATCH_DURATION_TICKS, VERSION,
    game::{
        state::{MatchState, MatchPhase, PlayerId},
        input::InputFrame,
        tick::{tick, MatchConfig, replay_match},
    },
};

fn main() {
    // Initialize logging
    let subscriber = FmtSubscriber::builder()
        .with_max_level(Level::INFO)
        .finish();
    tracing::subscriber::set_global_default(subscriber)
        .expect("Failed to set tracing subscriber");

    info!("Rune Relic Server v{}", VERSION);
    info!("Tick Rate: {} Hz", TICK_RATE);
    info!("Match Duration: {} ticks ({} seconds)", MATCH_DURATION_TICKS, MATCH_DURATION_TICKS / TICK_RATE);

    // Demo: Run a test match
    demo_match();
}

/// Demo function to test the simulation.
fn demo_match() {
    info!("=== Starting Demo Match ===");

    // Create match
    let match_id = [1u8; 16];
    let rng_seed = 12345u64;
    let mut state = MatchState::new(match_id, rng_seed);

    info!("Match ID: {:?}", hex::encode(match_id));
    info!("RNG Seed: {}", rng_seed);

    // Add players
    let player_ids: Vec<PlayerId> = (0..4)
        .map(|i| PlayerId::new([i; 16]))
        .collect();

    for id in &player_ids {
        state.add_player(*id);
        let player = state.get_player(id).unwrap();
        let (x, y) = player.position.to_floats();
        info!("Added player {} at ({:.2}, {:.2})", hex::encode(&id.0[..4]), x, y);
    }

    // Start match
    state.phase = MatchPhase::Playing;

    // Simulate match with random-ish inputs
    let config = MatchConfig::default();
    let mut input_buffer: BTreeMap<PlayerId, InputFrame> = BTreeMap::new();

    info!("Running {} ticks...", MATCH_DURATION_TICKS);

    let mut total_events = 0;
    let mut last_report_tick = 0;

    for t in 0..MATCH_DURATION_TICKS {
        // Generate inputs (simulate joystick)
        for (i, id) in player_ids.iter().enumerate() {
            let angle = (t as i32 * (i as i32 + 1) * 7) % 360;
            let move_x = ((angle % 127) - 63) as i8;
            let move_y = (((angle + 90) % 127) - 63) as i8;
            input_buffer.insert(*id, InputFrame::with_movement(move_x, move_y));
        }

        // Run tick
        let result = tick(&mut state, &input_buffer, &config);
        total_events += result.events.len();

        // Report every 10 seconds
        if t - last_report_tick >= 600 {
            let alive = state.alive_player_count();
            let runes = state.runes.len();
            info!("Tick {}: {} alive, {} runes, {} events so far", t, alive, runes, total_events);
            last_report_tick = t;
        }

        // Log important events
        for event in &result.events {
            match &event.data {
                rune_relic::game::events::GameEventData::PlayerEliminated { victim_id, placement, .. } => {
                    info!("Player {} eliminated (placement: {})",
                          hex::encode(&victim_id.0[..4]), placement);
                }
                rune_relic::game::events::GameEventData::FormEvolved { player_id, new_form, .. } => {
                    info!("Player {} evolved to {:?}",
                          hex::encode(&player_id.0[..4]), new_form);
                }
                rune_relic::game::events::GameEventData::MatchEnded { winner_id: Some(winner), .. } => {
                    info!("Match ended! Winner: {}", hex::encode(&winner.0[..4]));
                }
                _ => {}
            }
        }

        if result.match_ended {
            info!("Match ended at tick {}", t);
            break;
        }
    }

    // Print final results
    info!("=== Match Results ===");
    let hash = state.compute_hash();
    info!("Final State Hash: {}", hex::encode(hash));

    let placements = state.get_placements();
    for (id, placement, score) in placements {
        info!("#{}: Player {} - Score: {}", placement, hex::encode(&id.0[..4]), score);
    }

    info!("Total events: {}", total_events);

    // Verify determinism by replaying
    info!("=== Verifying Determinism ===");
    let mut replay_state = MatchState::new(match_id, rng_seed);
    for id in &player_ids {
        replay_state.add_player(*id);
    }

    // Create input recordings (simplified - just replaying same pattern)
    let mut replay_inputs: BTreeMap<PlayerId, Vec<InputFrame>> = BTreeMap::new();
    for (i, id) in player_ids.iter().enumerate() {
        let frames: Vec<InputFrame> = (0..MATCH_DURATION_TICKS)
            .map(|t| {
                let angle = (t as i32 * (i as i32 + 1) * 7) % 360;
                let move_x = ((angle % 127) - 63) as i8;
                let move_y = (((angle + 90) % 127) - 63) as i8;
                InputFrame::with_movement(move_x, move_y)
            })
            .collect();
        replay_inputs.insert(*id, frames);
    }

    let (replay_final, _) = replay_match(replay_state, &replay_inputs, MATCH_DURATION_TICKS);
    let replay_hash = replay_final.compute_hash();

    info!("Replay State Hash: {}", hex::encode(replay_hash));

    if hash == replay_hash {
        info!("DETERMINISM VERIFIED: Hashes match!");
    } else {
        info!("DETERMINISM FAILURE: Hashes differ!");
    }
}

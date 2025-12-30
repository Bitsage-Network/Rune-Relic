//! Rune Spawning and Collection
//!
//! Deterministic rune spawning based on RNG.

use crate::game::state::{MatchState, RuneType, PlayerId};
use crate::game::events::GameEvent;

/// Configuration for rune spawning.
pub struct RuneSpawnConfig {
    /// Initial runes spawned when match begins
    pub initial_spawn_count: u32,
    /// Minimum ticks between spawn waves
    pub spawn_interval: u32,
    /// Number of runes per wave
    pub spawn_count: u32,
    /// Maximum runes on field at once
    pub max_runes: u32,
    /// Spawn weighting for hubs
    pub weight_hubs: u32,
    /// Spawn weighting for corridors
    pub weight_corridors: u32,
    /// Spawn weighting for spawn zones
    pub weight_spawns: u32,
}

impl Default for RuneSpawnConfig {
    fn default() -> Self {
        Self {
            initial_spawn_count: 6000,
            spawn_interval: 60, // Every second
            spawn_count: 25,
            max_runes: 7000,
            weight_hubs: 60,
            weight_corridors: 30,
            weight_spawns: 10,
        }
    }
}

/// Spawn runes based on current tick.
pub fn maybe_spawn_runes(state: &mut MatchState, config: &RuneSpawnConfig) {
    // Only spawn during playing phase
    if !matches!(state.phase, crate::game::state::MatchPhase::Playing) {
        return;
    }

    // Initial spawn on first tick
    if state.tick == 1 && state.runes.is_empty() {
        spawn_runes(state, config, config.initial_spawn_count, false, None);
    }

    // Check spawn interval
    if state.tick.is_multiple_of(config.spawn_interval) {
        spawn_runes(state, config, config.spawn_count, true, Some(0));
    }
}

fn spawn_runes(
    state: &mut MatchState,
    config: &RuneSpawnConfig,
    count: u32,
    emit_events: bool,
    weight_spawns_override: Option<u32>,
) {
    // Count uncollected runes
    let uncollected_count = state.runes.values().filter(|r| !r.collected).count();
    if uncollected_count >= config.max_runes as usize {
        return;
    }

    // Determine spawn count
    let spawn_count = count.min(config.max_runes - uncollected_count as u32);

    // Spawn runes
    let weight_spawns = weight_spawns_override.unwrap_or(config.weight_spawns);

    for _ in 0..spawn_count {
        let position = state.map.random_pellet_position(
            &mut state.rng,
            config.weight_hubs,
            config.weight_corridors,
            weight_spawns,
        );
        let rune_type = random_rune_type(&mut state.rng);
        let rune_id = state.spawn_rune(position, rune_type);

        if emit_events {
            state.push_event(GameEvent::rune_spawned(
                state.tick,
                rune_id,
                rune_type,
                position,
            ));
        }
    }
}

/// Get a random rune type with weighted distribution.
fn random_rune_type(rng: &mut crate::core::rng::DeterministicRng) -> RuneType {
    // Weighted distribution:
    // Wisdom: 60%, Power: 20%, Speed: 10%, Shield: 5%, Arcane: 4%, Chaos: 1%
    let roll = rng.next_int(100);

    if roll < 60 {
        RuneType::Wisdom
    } else if roll < 80 {
        RuneType::Power
    } else if roll < 90 {
        RuneType::Speed
    } else if roll < 95 {
        RuneType::Shield
    } else if roll < 99 {
        RuneType::Arcane
    } else {
        RuneType::Chaos
    }
}

/// Buff duration for Speed and Shield runes (5 seconds at 60 Hz)
const RUNE_BUFF_DURATION: u32 = 300;

/// Process rune collection with special effects.
pub fn collect_rune(state: &mut MatchState, player_id: PlayerId, rune_id: u32) -> Option<GameEvent> {
    // Get rune
    let rune = state.runes.get_mut(&rune_id)?;
    if rune.collected {
        return None;
    }

    // Mark as collected
    rune.collected = true;
    rune.collected_tick = Some(state.tick);
    rune.collected_by = Some(player_id);

    let rune_type = rune.rune_type;
    let mut points = rune.value();

    // Update player
    let player = state.players.get_mut(&player_id)?;
    if !player.alive {
        return None;
    }

    // Apply wisdom multiplier if player has shrine buff
    if player.has_shrine_buff(crate::game::state::ShrineType::Wisdom) {
        points *= 2;
    }

    // Apply rune-specific effects
    match rune_type {
        RuneType::Speed => {
            player.speed_buff_ticks = RUNE_BUFF_DURATION;
        }
        RuneType::Shield => {
            player.shield_buff_ticks = RUNE_BUFF_DURATION;
        }
        RuneType::Chaos => {
            // Chaos rune: random effect (handled separately to avoid borrow issues)
        }
        RuneType::Wisdom | RuneType::Power | RuneType::Arcane => {
            // These just give points (already handled)
        }
    }

    let old_form = player.form;
    player.runes_collected += 1;
    let evolved = player.add_score(points);
    let new_score = player.score;
    let new_form = player.form;

    // Generate collection event
    let event = GameEvent::rune_collected(
        state.tick,
        player_id,
        rune_id,
        rune_type,
        points,
        new_score,
    );

    // If player evolved, also generate evolution event
    if evolved {
        state.push_event(GameEvent::form_evolved(
            state.tick,
            player_id,
            old_form,
            new_form,
        ));
    }

    // Handle chaos rune effect after other borrows released
    if rune_type == RuneType::Chaos {
        apply_chaos_effect(state, player_id);
    }

    Some(event)
}

/// Apply random chaos rune effect.
fn apply_chaos_effect(state: &mut MatchState, player_id: PlayerId) {
    let effect = state.rng.next_int(4);

    if let Some(player) = state.players.get_mut(&player_id) {
        match effect {
            0 => {
                // Extended speed boost (10 seconds)
                player.speed_buff_ticks = 600;
            }
            1 => {
                // Extended shield (10 seconds)
                player.shield_buff_ticks = 600;
            }
            2 => {
                // Instant ability reset
                player.ability_cooldown = 0;
            }
            3 => {
                // Bonus points (double the chaos base value)
                player.add_score(50);
            }
            _ => unreachable!(),
        }
    }
}

/// Remove collected runes (garbage collection).
pub fn cleanup_collected_runes(state: &mut MatchState, max_age_ticks: u32) {
    let current_tick = state.tick;

    state.runes.retain(|_, rune| {
        if let Some(collected_tick) = rune.collected_tick {
            // Keep for a bit for visual feedback, then remove
            current_tick - collected_tick < max_age_ticks
        } else {
            true // Keep uncollected runes
        }
    });
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::core::fixed::SCORE_PER_RUNE;

    #[test]
    fn test_rune_spawn_determinism() {
        let mut state1 = MatchState::new([0; 16], 12345);
        let mut state2 = MatchState::new([0; 16], 12345);

        state1.phase = crate::game::state::MatchPhase::Playing;
        state2.phase = crate::game::state::MatchPhase::Playing;

        let config = RuneSpawnConfig::default();

        // Simulate 5 spawn waves
        for tick in 0..300 {
            state1.tick = tick;
            state2.tick = tick;
            maybe_spawn_runes(&mut state1, &config);
            maybe_spawn_runes(&mut state2, &config);
        }

        // Should have same runes
        assert_eq!(state1.runes.len(), state2.runes.len());

        for (id, rune1) in &state1.runes {
            let rune2 = state2.runes.get(id).unwrap();
            assert_eq!(rune1.position, rune2.position);
            assert_eq!(rune1.rune_type, rune2.rune_type);
        }
    }

    #[test]
    fn test_rune_collection() {
        let mut state = MatchState::new([0; 16], 12345);
        state.phase = crate::game::state::MatchPhase::Playing;

        // Add player
        let player_id = PlayerId::new([1; 16]);
        state.add_player(player_id);

        // Spawn a rune at player position
        let player_pos = state.players.get(&player_id).unwrap().position;
        state.spawn_rune(player_pos, RuneType::Wisdom);

        let rune_id = 0;

        // Collect it
        let event = collect_rune(&mut state, player_id, rune_id);
        assert!(event.is_some());

        // Rune should be collected
        assert!(state.runes.get(&rune_id).unwrap().collected);

        // Player should have score
        assert_eq!(state.players.get(&player_id).unwrap().score, SCORE_PER_RUNE);
    }
}

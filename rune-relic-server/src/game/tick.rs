//! Authoritative Simulation Tick
//!
//! The core game loop that must be 100% deterministic.
//! This is what BitSage will verify.

use std::collections::BTreeMap;

use crate::core::fixed::{
    Fixed, FIXED_ONE,
    fixed_mul, JUMP_VELOCITY,
};
use crate::core::vec2::FixedVec2;
use crate::MATCH_DURATION_TICKS;
use crate::game::input::InputFrame;
use crate::game::state::{MatchState, MatchPhase, PlayerId};
use crate::game::collision::{
    check_all_player_collisions,
    check_all_rune_collisions,
};
use crate::game::rune::{maybe_spawn_runes, collect_rune, RuneSpawnConfig};
use crate::game::shrine::{ShrineConfig, process_shrines, spawn_shrines};
use crate::game::ability::{activate_ability, process_active_abilities};
use crate::game::events::GameEvent;

/// Result of a tick.
#[derive(Debug)]
#[derive(Default)]
pub struct TickResult {
    /// Events generated this tick
    pub events: Vec<GameEvent>,
    /// Whether match ended this tick
    pub match_ended: bool,
    /// Winner (if match ended with winner)
    pub winner: Option<PlayerId>,
}


/// Configuration for match simulation.
pub struct MatchConfig {
    /// Rune spawn configuration
    pub rune_spawn: RuneSpawnConfig,
    /// Shrine configuration
    pub shrine: ShrineConfig,
    /// Ticks before arena starts shrinking
    pub shrink_start_tick: u32,
    /// Rate of arena shrink per tick (Fixed)
    pub shrink_rate: Fixed,
    /// Damage per tick when outside zone
    pub zone_damage_rate: Fixed,
}

impl Default for MatchConfig {
    fn default() -> Self {
        Self {
            rune_spawn: RuneSpawnConfig::default(),
            shrine: ShrineConfig::default(),
            shrink_start_tick: 1800, // Start shrinking at 30 seconds
            shrink_rate: 18,          // Slow shrink: ~0.0003 per tick
            zone_damage_rate: 3276,   // ~0.05 per tick = ~3 per second
        }
    }
}

/// Run one simulation tick.
///
/// # Arguments
///
/// * `state` - The match state (will be mutated)
/// * `inputs` - Player inputs for this tick (BTreeMap for deterministic order!)
/// * `config` - Match configuration
///
/// # Determinism
///
/// This function is 100% deterministic:
/// - Uses BTreeMap for iteration order
/// - Uses fixed-point math only
/// - Uses deterministic RNG (state.rng)
/// - No system calls, no floating point
pub fn tick(
    state: &mut MatchState,
    inputs: &BTreeMap<PlayerId, InputFrame>,
    config: &MatchConfig,
) -> TickResult {
    let mut result = TickResult::default();

    // Phase-specific logic
    match state.phase {
        MatchPhase::Waiting => {
            // Nothing to do while waiting
            return result;
        }
        MatchPhase::Countdown { ticks_remaining } => {
            if ticks_remaining == 0 {
                state.phase = MatchPhase::Playing;
                // Initialize shrines when match starts
                spawn_shrines(state);
            } else {
                state.phase = MatchPhase::Countdown {
                    ticks_remaining: ticks_remaining - 1,
                };
            }
            return result;
        }
        MatchPhase::Ended => {
            // Match is over
            result.match_ended = true;
            return result;
        }
        MatchPhase::Playing => {
            // Continue with main simulation
        }
    }

    // 0. Advance tick counter
    state.tick += 1;

    // 1. Apply player inputs
    apply_inputs(state, inputs);

    // 2. Update physics
    update_physics(state);

    // 3. Update arena shrink
    update_arena_shrink(state, config);

    // 4. Check player-vs-player collisions
    process_player_collisions(state, &mut result);

    // 5. Check player-vs-rune collisions
    process_rune_collisions(state, &mut result);

    // 6. Check zone damage (players outside shrinking arena)
    process_zone_damage(state, config, &mut result);

    // 7. Spawn new runes
    maybe_spawn_runes(state, &config.rune_spawn);

    // 8. Process shrine mechanics
    process_shrines(state, &config.shrine);

    // 9. Process active ability effects (gravity wells, etc.)
    process_active_abilities(state);

    // 10. Check end conditions
    check_end_conditions(state, &mut result);

    // Collect events
    result.events = state.take_events();

    result
}

/// Apply player inputs to their states.
fn apply_inputs(state: &mut MatchState, inputs: &BTreeMap<PlayerId, InputFrame>) {
    // Collect ability activations (to avoid borrow issues)
    let mut ability_activations: Vec<PlayerId> = Vec::new();

    // BTreeMap iterates in sorted key order - DETERMINISTIC
    for (player_id, input) in inputs {
        if let Some(player) = state.players.get_mut(player_id) {
            if !player.alive {
                continue;
            }

            // Movement
            let move_dir = input.move_direction();
            let speed = player.speed();

            // Normalize movement if diagonal (prevent faster diagonal movement)
            let move_len_sq = move_dir.length_squared();
            let velocity = if move_len_sq > FIXED_ONE {
                // Diagonal - normalize then scale
                let normalized = move_dir.normalize();
                normalized.scale(speed)
            } else if move_len_sq > 0 {
                // Partial movement - scale by input magnitude
                move_dir.scale(speed)
            } else {
                FixedVec2::ZERO
            };

            player.velocity = velocity;

            // Jump
            if input.jump_pressed() && player.can_jump(state.tick) {
                player.velocity.y = player.velocity.y.wrapping_add(JUMP_VELOCITY);
                player.last_jump_tick = state.tick;
            }

            // Check for ability activation (process after loop to avoid borrow issues)
            if input.ability_pressed() && player.ability_ready() {
                ability_activations.push(*player_id);
            }
        }
    }

    // Process ability activations
    for player_id in ability_activations {
        if let Some(event) = activate_ability(state, player_id) {
            state.push_event(event);
        }
    }
}

/// Update physics for all players.
fn update_physics(state: &mut MatchState) {
    // Tick duration: 1/60 second as Fixed
    const TICK_DT: Fixed = 1092; // round(65536 / 60)

    // Speed buff multiplier (1.4x = 91750 in fixed point)
    const SPEED_BUFF_MULT: Fixed = 91750;

    // Shrine speed buff multiplier (1.2x = 78643 in fixed point)
    const SHRINE_SPEED_MULT: Fixed = 78643;

    // Get arena bounds before iteration
    let (hw, hh) = state.current_arena_bounds();

    // BTreeMap values_mut iterates in sorted order
    for player in state.players.values_mut() {
        if !player.alive {
            continue;
        }

        // Apply speed buff multiplier to velocity
        let mut velocity = player.velocity;
        if player.speed_buff_ticks > 0 {
            velocity.x = fixed_mul(velocity.x, SPEED_BUFF_MULT);
            velocity.y = fixed_mul(velocity.y, SPEED_BUFF_MULT);
        }
        if player.has_shrine_buff(crate::game::state::ShrineType::Speed) {
            velocity.x = fixed_mul(velocity.x, SHRINE_SPEED_MULT);
            velocity.y = fixed_mul(velocity.y, SHRINE_SPEED_MULT);
        }

        // Apply dash velocity if active
        if let Some(dash_vel) = player.dash_velocity {
            velocity = velocity.add(dash_vel);
            player.dash_velocity = None; // Dash lasts only 1 tick application
        }

        // Integration: position += velocity * dt
        let dx = fixed_mul(velocity.x, TICK_DT);
        let dy = fixed_mul(velocity.y, TICK_DT);

        player.position.x = player.position.x.wrapping_add(dx);
        player.position.y = player.position.y.wrapping_add(dy);

        // Clamp to arena bounds
        player.position.x = player.position.x.max(-hw).min(hw);
        player.position.y = player.position.y.max(-hh).min(hh);

        // Decay ability cooldown
        if player.ability_cooldown > 0 {
            player.ability_cooldown = player.ability_cooldown.saturating_sub(FIXED_ONE);
        }

        // Decay buff timers
        if player.speed_buff_ticks > 0 {
            player.speed_buff_ticks -= 1;
        }
        if player.shield_buff_ticks > 0 {
            player.shield_buff_ticks -= 1;
        }
        if player.invulnerable_ticks > 0 {
            player.invulnerable_ticks -= 1;
        }

        // Update shrine buffs
        player.update_shrine_buffs();

        // Apply friction (slow down when no input)
        // This creates the "floaty" feel
        let friction = FIXED_ONE - 3276; // ~0.95 friction
        player.velocity.x = fixed_mul(player.velocity.x, friction);
        player.velocity.y = fixed_mul(player.velocity.y, friction);
    }
}

/// Update arena shrink.
fn update_arena_shrink(state: &mut MatchState, config: &MatchConfig) {
    if state.tick < config.shrink_start_tick {
        return;
    }

    // Increase shrink progress
    state.arena_shrink = (state.arena_shrink + config.shrink_rate).min(FIXED_ONE);
}

/// Process player-vs-player collisions.
fn process_player_collisions(state: &mut MatchState, _result: &mut TickResult) {
    let collisions = check_all_player_collisions(state);

    for collision in collisions {
        // Get loser's placement before elimination
        let placement = state.players.len() as u8 - state.next_placement;

        // Eliminate loser
        state.eliminate_player(&collision.loser, Some(&collision.winner));

        // Generate event
        let event = GameEvent::player_eliminated(
            state.tick,
            collision.loser,
            Some(collision.winner),
            placement,
        );
        state.push_event(event);
    }
}

/// Process player-vs-rune collisions.
fn process_rune_collisions(state: &mut MatchState, _result: &mut TickResult) {
    let collisions = check_all_rune_collisions(state);

    for collision in collisions {
        if let Some(event) = collect_rune(state, collision.player_id, collision.rune_id) {
            state.push_event(event);
        }
    }
}

/// Process zone damage for players outside bounds.
/// Implements gradual damage instead of instant elimination.
fn process_zone_damage(state: &mut MatchState, config: &MatchConfig, _result: &mut TickResult) {
    let (hw, hh) = state.current_arena_bounds();

    // Health regeneration rate when inside bounds
    const REGEN_RATE: Fixed = 328; // ~0.005 per tick = ~0.3 per second

    // Collect players to eliminate after damage
    let mut to_eliminate: Vec<PlayerId> = Vec::new();

    for (player_id, player) in state.players.iter_mut() {
        if !player.alive {
            continue;
        }

        // Check if outside bounds
        let outside_x = player.position.x < -hw || player.position.x > hw;
        let outside_y = player.position.y < -hh || player.position.y > hh;

        if outside_x || outside_y {
            // Calculate distance outside bounds for damage scaling
            let dist_outside_x = if player.position.x < -hw {
                (-hw).saturating_sub(player.position.x).abs()
            } else if player.position.x > hw {
                player.position.x.saturating_sub(hw).abs()
            } else {
                0
            };

            let dist_outside_y = if player.position.y < -hh {
                (-hh).saturating_sub(player.position.y).abs()
            } else if player.position.y > hh {
                player.position.y.saturating_sub(hh).abs()
            } else {
                0
            };

            // Base damage + distance factor
            let base_damage = config.zone_damage_rate;
            let dist_factor = fixed_mul(
                dist_outside_x.saturating_add(dist_outside_y),
                655, // ~0.01 multiplier per unit distance
            );
            let damage = base_damage.saturating_add(dist_factor);

            // Apply shield damage reduction (from rune or shrine buff)
            let has_shield = player.shield_buff_ticks > 0
                || player.has_shrine_buff(crate::game::state::ShrineType::Shield);
            let shield_mult = if has_shield { 32768 } else { FIXED_ONE }; // 0.5x or 1.0x
            let final_damage = fixed_mul(damage, shield_mult);

            // Apply damage
            player.health = player.health.saturating_sub(final_damage);

            // Check for elimination
            if player.health <= 0 {
                to_eliminate.push(*player_id);
            }
        } else {
            // Inside bounds - regenerate health slowly
            player.health = player.health.saturating_add(REGEN_RATE).min(player.max_health);
        }
    }

    // Eliminate players with zero health
    for player_id in to_eliminate {
        let event = GameEvent::player_eliminated(
            state.tick,
            player_id,
            None, // No killer - zone death
            (state.players.len() as u8).saturating_sub(state.next_placement),
        );
        state.eliminate_player(&player_id, None);
        state.push_event(event);
    }
}

/// Check if match should end.
fn check_end_conditions(state: &mut MatchState, result: &mut TickResult) {
    // End if time expired
    if state.tick >= MATCH_DURATION_TICKS {
        end_match(state, result);
        return;
    }

    // End if only 1 (or 0) players alive
    if state.alive_count <= 1 {
        end_match(state, result);
    }
}

/// End the match and determine winner.
fn end_match(state: &mut MatchState, result: &mut TickResult) {
    state.phase = MatchPhase::Ended;
    result.match_ended = true;

    // Find winner (last alive or highest score if tied)
    let winner = state
        .players
        .iter()
        .filter(|(_, p)| p.alive)
        .max_by_key(|(id, p)| (p.score, *id))  // Tie-break by ID
        .map(|(id, _)| *id);

    // Assign 1st place to winner
    if let Some(winner_id) = winner {
        if let Some(player) = state.players.get_mut(&winner_id) {
            player.placement = Some(1);
        }
    }

    result.winner = winner;

    // Generate match end event
    state.push_event(GameEvent::match_ended(state.tick, winner));
}

/// Replay a match from recorded inputs.
///
/// Returns final state hash and events.
pub fn replay_match(
    initial_state: MatchState,
    player_inputs: &BTreeMap<PlayerId, Vec<InputFrame>>,
    tick_count: u32,
) -> (MatchState, Vec<GameEvent>) {
    let mut state = initial_state;
    let mut all_events = Vec::new();
    let config = MatchConfig::default();

    // Start match
    state.phase = MatchPhase::Playing;

    for t in 0..tick_count {
        // Get inputs for this tick
        let mut tick_inputs = BTreeMap::new();
        for (player_id, frames) in player_inputs {
            if let Some(frame) = frames.get(t as usize) {
                tick_inputs.insert(*player_id, *frame);
            } else {
                tick_inputs.insert(*player_id, InputFrame::new());
            }
        }

        // Run tick
        let result = tick(&mut state, &tick_inputs, &config);
        all_events.extend(result.events);

        if result.match_ended {
            break;
        }
    }

    (state, all_events)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_tick_determinism() {
        let config = MatchConfig::default();

        // Create two identical states
        let mut state1 = MatchState::new([0; 16], 12345);
        let mut state2 = MatchState::new([0; 16], 12345);

        // Add same players
        for i in 0..4 {
            let id = PlayerId::new([i; 16]);
            state1.add_player(id);
            state2.add_player(id);
        }

        state1.phase = MatchPhase::Playing;
        state2.phase = MatchPhase::Playing;

        // Same inputs
        let mut inputs = BTreeMap::new();
        for i in 0..4 {
            let id = PlayerId::new([i; 16]);
            inputs.insert(id, InputFrame::with_movement(50, 0));
        }

        // Run 100 ticks
        for _ in 0..100 {
            tick(&mut state1, &inputs, &config);
            tick(&mut state2, &inputs, &config);
        }

        // States should be identical
        assert_eq!(state1.tick, state2.tick);
        assert_eq!(state1.compute_hash(), state2.compute_hash());

        for (id, player1) in &state1.players {
            let player2 = state2.players.get(id).unwrap();
            assert_eq!(player1.position, player2.position);
            assert_eq!(player1.score, player2.score);
        }
    }

    #[test]
    fn test_player_movement() {
        let mut state = MatchState::new([0; 16], 12345);
        let id = PlayerId::new([1; 16]);

        // Manually set spawn position for predictable test
        state.players.insert(id, crate::game::state::PlayerState::new(id, FixedVec2::ZERO));
        state.alive_count = 1;
        state.phase = MatchPhase::Playing;

        let config = MatchConfig::default();
        let mut inputs = BTreeMap::new();
        inputs.insert(id, InputFrame::with_movement(127, 0)); // Full right

        // Run tick
        tick(&mut state, &inputs, &config);

        // Player should have moved right
        let player = state.players.get(&id).unwrap();
        assert!(player.position.x > 0, "Player should move right");
    }

    #[test]
    fn test_match_ends_on_one_alive() {
        let mut state = MatchState::new([0; 16], 12345);
        let config = MatchConfig::default();

        // Add 2 players
        let id1 = PlayerId::new([1; 16]);
        let id2 = PlayerId::new([2; 16]);
        state.add_player(id1);
        state.add_player(id2);
        state.phase = MatchPhase::Playing;

        // Eliminate one player
        state.eliminate_player(&id2, Some(&id1));

        let inputs = BTreeMap::new();
        let result = tick(&mut state, &inputs, &config);

        assert!(result.match_ended);
        assert_eq!(result.winner, Some(id1));
    }

    #[test]
    fn test_replay_determinism() {
        let state1 = MatchState::new([0; 16], 99999);
        let state2 = MatchState::new([0; 16], 99999);

        // Add players to both
        let mut state1 = state1;
        let mut state2 = state2;

        for i in 0..4 {
            let id = PlayerId::new([i; 16]);
            state1.add_player(id);
            state2.add_player(id);
        }

        // Create input recordings
        let mut inputs: BTreeMap<PlayerId, Vec<InputFrame>> = BTreeMap::new();
        for i in 0..4 {
            let id = PlayerId::new([i; 16]);
            let frames: Vec<InputFrame> = (0..100)
                .map(|t| InputFrame::with_movement((((t as i32) * (i as i32)) % 127) as i8, 0))
                .collect();
            inputs.insert(id, frames);
        }

        // Replay both
        let (final1, events1) = replay_match(state1, &inputs, 100);
        let (final2, events2) = replay_match(state2, &inputs, 100);

        // Should be identical
        assert_eq!(final1.compute_hash(), final2.compute_hash());
        assert_eq!(events1.len(), events2.len());
    }
}

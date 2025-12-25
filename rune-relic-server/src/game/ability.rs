//! Form-Specific Abilities
//!
//! Each evolution form has a unique ability with different cooldowns.

use crate::core::fixed::{Fixed, FIXED_ONE, fixed_mul};
use crate::core::vec2::FixedVec2;
use crate::game::state::{MatchState, PlayerId, Form, AbilityType, ActiveAbilityEffect};
use crate::game::events::GameEvent;

/// Ability cooldowns by form (in ticks at 60Hz).
pub const ABILITY_COOLDOWNS: [u32; 5] = [
    180,  // Spark: 3 seconds
    300,  // Glyph: 5 seconds
    360,  // Ward: 6 seconds
    420,  // Arcane: 7 seconds
    480,  // Ancient: 8 seconds
];

/// Dash speed for Spark ability.
const DASH_SPEED: Fixed = 983040; // 15.0 * 65536

/// Invulnerability duration for Glyph ability.
const PHASE_SHIFT_TICKS: u32 = 20; // ~0.33 seconds

/// Repel radius for Ward ability.
const REPEL_RADIUS: Fixed = 327680; // 5.0 * 65536

/// Repel force for Ward ability.
const REPEL_FORCE: Fixed = 524288; // 8.0 * 65536

/// Gravity well radius for Arcane ability.
const GRAVITY_WELL_RADIUS: Fixed = 458752; // 7.0 * 65536

/// Gravity well duration for Arcane ability.
const GRAVITY_WELL_TICKS: u32 = 180; // 3 seconds

/// Consume extended radius multiplier for Ancient ability.
const CONSUME_RADIUS_MULT: Fixed = 98304; // 1.5 * 65536

/// Get ability type for a form.
pub fn ability_for_form(form: Form) -> AbilityType {
    match form {
        Form::Spark => AbilityType::Dash,
        Form::Glyph => AbilityType::PhaseShift,
        Form::Ward => AbilityType::Repel,
        Form::Arcane => AbilityType::GravityWell,
        Form::Ancient => AbilityType::Consume,
    }
}

/// Activate a player's ability.
/// Returns an event if ability was successfully activated.
pub fn activate_ability(state: &mut MatchState, player_id: PlayerId) -> Option<GameEvent> {
    // Check if player exists and can use ability
    let (form, position, velocity, can_activate) = {
        let player = state.players.get(&player_id)?;
        if !player.alive || !player.ability_ready() {
            return None;
        }
        (player.form, player.position, player.velocity, true)
    };

    if !can_activate {
        return None;
    }

    let ability_type = ability_for_form(form);
    let cooldown = (ABILITY_COOLDOWNS[form as usize] as i64 * FIXED_ONE as i64) as Fixed;

    // Apply ability effect
    match ability_type {
        AbilityType::Dash => {
            activate_dash(state, player_id, velocity);
        }
        AbilityType::PhaseShift => {
            activate_phase_shift(state, player_id);
        }
        AbilityType::Repel => {
            activate_repel(state, player_id, position);
        }
        AbilityType::GravityWell => {
            activate_gravity_well(state, player_id, position);
        }
        AbilityType::Consume => {
            activate_consume(state, player_id);
        }
    }

    // Set cooldown
    if let Some(player) = state.players.get_mut(&player_id) {
        player.ability_cooldown = cooldown;
    }

    Some(GameEvent::ability_used(
        state.tick,
        player_id,
        ability_type as u8,
    ))
}

/// Spark ability: Quick dash in movement direction.
fn activate_dash(state: &mut MatchState, player_id: PlayerId, current_velocity: FixedVec2) {
    if let Some(player) = state.players.get_mut(&player_id) {
        let direction = if current_velocity.length_squared() > 0 {
            current_velocity.normalize()
        } else {
            // Default to right if no velocity
            FixedVec2::new(FIXED_ONE, 0)
        };
        player.dash_velocity = Some(direction.scale(DASH_SPEED));
    }
}

/// Glyph ability: Brief invulnerability.
fn activate_phase_shift(state: &mut MatchState, player_id: PlayerId) {
    if let Some(player) = state.players.get_mut(&player_id) {
        player.invulnerable_ticks = PHASE_SHIFT_TICKS;
    }
}

/// Ward ability: Push nearby enemies away.
fn activate_repel(state: &mut MatchState, player_id: PlayerId, position: FixedVec2) {
    let radius_sq = fixed_mul(REPEL_RADIUS, REPEL_RADIUS);

    // Collect players to push
    let players_to_push: Vec<(PlayerId, FixedVec2)> = state.players
        .iter()
        .filter(|(id, p)| **id != player_id && p.alive)
        .filter(|(_, p)| position.distance_squared(p.position) < radius_sq)
        .map(|(id, p)| (*id, p.position))
        .collect();

    // Apply push force
    for (other_id, other_pos) in players_to_push {
        if let Some(other) = state.players.get_mut(&other_id) {
            let diff = other_pos.sub(position);
            if diff.length_squared() > 0 {
                let direction = diff.normalize();
                let push = direction.scale(REPEL_FORCE);
                other.velocity = other.velocity.add(push);
            }
        }
    }
}

/// Arcane ability: Create a gravity well that slows enemies.
fn activate_gravity_well(state: &mut MatchState, player_id: PlayerId, position: FixedVec2) {
    let effect = ActiveAbilityEffect {
        ability_type: AbilityType::GravityWell,
        source_player: player_id,
        position,
        remaining_ticks: GRAVITY_WELL_TICKS,
        radius: GRAVITY_WELL_RADIUS,
    };
    state.active_abilities.push(effect);
}

/// Ancient ability: Temporarily extend elimination radius.
fn activate_consume(state: &mut MatchState, player_id: PlayerId) {
    // Mark with a special ability effect to track consume state
    let position = state.players.get(&player_id)
        .map(|p| p.position)
        .unwrap_or(FixedVec2::ZERO);

    let effect = ActiveAbilityEffect {
        ability_type: AbilityType::Consume,
        source_player: player_id,
        position,
        remaining_ticks: 60, // 1 second of extended range
        radius: CONSUME_RADIUS_MULT,
    };
    state.active_abilities.push(effect);
}

/// Process active ability effects each tick.
pub fn process_active_abilities(state: &mut MatchState) {
    let gravity_wells: Vec<(FixedVec2, Fixed, PlayerId)> = state.active_abilities
        .iter()
        .filter(|e| e.ability_type == AbilityType::GravityWell)
        .map(|e| (e.position, e.radius, e.source_player))
        .collect();

    // Apply gravity well slow to players
    for (well_pos, well_radius, source_id) in gravity_wells {
        let radius_sq = fixed_mul(well_radius, well_radius);

        for (player_id, player) in state.players.iter_mut() {
            if *player_id == source_id || !player.alive {
                continue;
            }

            let dist_sq = well_pos.distance_squared(player.position);
            if dist_sq < radius_sq {
                // Apply 50% slow
                player.velocity = player.velocity.scale(32768); // 0.5 * 65536
            }
        }
    }

    // Decay ability timers
    state.active_abilities.retain_mut(|effect| {
        effect.remaining_ticks = effect.remaining_ticks.saturating_sub(1);
        effect.remaining_ticks > 0
    });
}

/// Check if a player has extended consume radius active.
pub fn has_consume_active(state: &MatchState, player_id: PlayerId) -> bool {
    state.active_abilities.iter().any(|e| {
        e.ability_type == AbilityType::Consume && e.source_player == player_id
    })
}

/// Get consume radius multiplier if active.
pub fn get_consume_radius_multiplier(state: &MatchState, player_id: PlayerId) -> Fixed {
    if has_consume_active(state, player_id) {
        CONSUME_RADIUS_MULT
    } else {
        FIXED_ONE
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::game::state::PlayerState;

    #[test]
    fn test_ability_for_form() {
        assert_eq!(ability_for_form(Form::Spark), AbilityType::Dash);
        assert_eq!(ability_for_form(Form::Glyph), AbilityType::PhaseShift);
        assert_eq!(ability_for_form(Form::Ward), AbilityType::Repel);
        assert_eq!(ability_for_form(Form::Arcane), AbilityType::GravityWell);
        assert_eq!(ability_for_form(Form::Ancient), AbilityType::Consume);
    }

    #[test]
    fn test_dash_ability() {
        let mut state = MatchState::new([0; 16], 12345);
        state.phase = crate::game::state::MatchPhase::Playing;

        let player_id = PlayerId::new([1; 16]);
        let mut player = PlayerState::new(player_id, FixedVec2::ZERO);
        player.velocity = FixedVec2::new(FIXED_ONE, 0); // Moving right
        state.players.insert(player_id, player);
        state.alive_count = 1;

        // Activate ability
        let event = activate_ability(&mut state, player_id);
        assert!(event.is_some());

        // Check dash velocity was set
        let player = state.players.get(&player_id).unwrap();
        assert!(player.dash_velocity.is_some());
        assert!(player.ability_cooldown > 0);
    }

    #[test]
    fn test_phase_shift_invulnerability() {
        let mut state = MatchState::new([0; 16], 12345);
        state.phase = crate::game::state::MatchPhase::Playing;

        let player_id = PlayerId::new([1; 16]);
        let mut player = PlayerState::new(player_id, FixedVec2::ZERO);
        player.form = Form::Glyph; // Has Phase Shift
        state.players.insert(player_id, player);
        state.alive_count = 1;

        // Activate ability
        let event = activate_ability(&mut state, player_id);
        assert!(event.is_some());

        // Check invulnerability
        let player = state.players.get(&player_id).unwrap();
        assert_eq!(player.invulnerable_ticks, PHASE_SHIFT_TICKS);
    }

    #[test]
    fn test_gravity_well_creation() {
        let mut state = MatchState::new([0; 16], 12345);
        state.phase = crate::game::state::MatchPhase::Playing;

        let player_id = PlayerId::new([1; 16]);
        let mut player = PlayerState::new(player_id, FixedVec2::ZERO);
        player.form = Form::Arcane; // Has Gravity Well
        state.players.insert(player_id, player);
        state.alive_count = 1;

        // Activate ability
        let event = activate_ability(&mut state, player_id);
        assert!(event.is_some());

        // Check gravity well was created
        assert_eq!(state.active_abilities.len(), 1);
        assert_eq!(state.active_abilities[0].ability_type, AbilityType::GravityWell);
    }

    #[test]
    fn test_ability_cooldown() {
        let mut state = MatchState::new([0; 16], 12345);
        state.phase = crate::game::state::MatchPhase::Playing;

        let player_id = PlayerId::new([1; 16]);
        let player = PlayerState::new(player_id, FixedVec2::ZERO);
        state.players.insert(player_id, player);
        state.alive_count = 1;

        // First activation should succeed
        let event1 = activate_ability(&mut state, player_id);
        assert!(event1.is_some());

        // Second activation should fail (cooldown)
        let event2 = activate_ability(&mut state, player_id);
        assert!(event2.is_none());
    }
}

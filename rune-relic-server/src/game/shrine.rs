//! Shrine Mechanics
//!
//! Shrine spawning, channeling, and buff application.
//! Shrines are fixed positions on the map that grant temporary buffs.

use crate::core::fixed::{Fixed, FIXED_ONE};
use crate::core::vec2::FixedVec2;
use crate::game::state::{MatchState, PlayerId, ShrineState, ShrineType};
use crate::game::events::GameEvent;
use crate::game::collision::circles_overlap;

/// Configuration for shrine mechanics.
pub struct ShrineConfig {
    /// Channel progress per tick (FIXED_ONE / CHANNEL_TICKS)
    pub channel_rate: Fixed,
    /// Buff duration in ticks (30 seconds)
    pub buff_duration: u32,
}

impl Default for ShrineConfig {
    fn default() -> Self {
        // Channel rate: complete in 5 seconds = 300 ticks
        // FIXED_ONE / 300 = 65536 / 300 = 218
        Self {
            channel_rate: 218,
            buff_duration: 1800, // 30 seconds at 60 Hz
        }
    }
}

/// Fixed shrine positions in arena (as Fixed values).
/// Positions are at ~35 units from center in corners.
const SHRINE_POSITIONS: [(Fixed, Fixed, ShrineType); 4] = [
    (2293760, 2293760, ShrineType::Wisdom),    // 35.0, 35.0 - top-right
    (-2293760, 2293760, ShrineType::Power),    // -35.0, 35.0 - top-left
    (-2293760, -2293760, ShrineType::Speed),   // -35.0, -35.0 - bottom-left
    (2293760, -2293760, ShrineType::Shield),   // 35.0, -35.0 - bottom-right
];

/// Initialize shrines at fixed positions (called at match start).
pub fn spawn_shrines(state: &mut MatchState) {
    for (i, (x, y, shrine_type)) in SHRINE_POSITIONS.iter().enumerate() {
        let position = FixedVec2::new(*x, *y);
        let shrine = ShrineState::new(i as u8, position, *shrine_type);
        state.shrines.push(shrine);
    }
}

/// Shrine action to apply after processing.
enum ShrineAction {
    StartChannel { shrine_id: u8, player_id: PlayerId },
    ContinueChannel { shrine_id: u8, progress: Fixed },
    CompleteChannel { shrine_id: u8, player_id: PlayerId, shrine_type: ShrineType },
    InterruptChannel { shrine_id: u8, player_id: PlayerId },
    StartNewChannel { shrine_id: u8, old_player: PlayerId, new_player: PlayerId },
}

/// Process shrine interactions each tick.
pub fn process_shrines(state: &mut MatchState, config: &ShrineConfig) {
    let tick = state.tick;
    let buff_duration = config.buff_duration;

    // Collect actions to perform
    let mut actions: Vec<ShrineAction> = Vec::new();
    let mut events: Vec<GameEvent> = Vec::new();

    // Step 1: Collect player positions for shrine checks
    let players: Vec<(PlayerId, FixedVec2, Fixed, bool)> = state.players
        .iter()
        .map(|(id, p)| (*id, p.position, p.radius(), p.alive))
        .collect();

    // Step 2: Process each shrine
    for shrine in &state.shrines {
        // Handle cooldown decay
        if !shrine.active {
            if shrine.cooldown > 0 {
                // Cooldown is handled in apply phase
            }
            continue;
        }

        // Find player on this shrine
        let mut player_on_shrine: Option<PlayerId> = None;
        for (player_id, pos, radius, alive) in &players {
            if !alive {
                continue;
            }
            if is_player_on_shrine(*pos, *radius, shrine) {
                player_on_shrine = Some(*player_id);
                break;
            }
        }

        match (shrine.channeling_player, player_on_shrine) {
            (None, Some(new_player)) => {
                actions.push(ShrineAction::StartChannel {
                    shrine_id: shrine.id,
                    player_id: new_player,
                });
                events.push(GameEvent::shrine_channel_started(tick, new_player, shrine.id));
            }
            (Some(current), Some(new_player)) if current == new_player => {
                let new_progress = shrine.channel_progress.saturating_add(config.channel_rate);
                if new_progress >= FIXED_ONE {
                    actions.push(ShrineAction::CompleteChannel {
                        shrine_id: shrine.id,
                        player_id: current,
                        shrine_type: shrine.shrine_type,
                    });
                    events.push(GameEvent::shrine_activated(tick, current, shrine.id));
                } else {
                    actions.push(ShrineAction::ContinueChannel {
                        shrine_id: shrine.id,
                        progress: new_progress,
                    });
                }
            }
            (Some(current), None) => {
                actions.push(ShrineAction::InterruptChannel {
                    shrine_id: shrine.id,
                    player_id: current,
                });
                events.push(GameEvent::shrine_channel_interrupted(tick, current, shrine.id));
            }
            (Some(current), Some(new_player)) if current != new_player => {
                actions.push(ShrineAction::StartNewChannel {
                    shrine_id: shrine.id,
                    old_player: current,
                    new_player,
                });
                events.push(GameEvent::shrine_channel_interrupted(tick, current, shrine.id));
                events.push(GameEvent::shrine_channel_started(tick, new_player, shrine.id));
            }
            (None, None) => {
                // Nothing to do
            }
            _ => {}
        }
    }

    // Step 3: Decay cooldowns for inactive shrines
    for shrine in &mut state.shrines {
        if !shrine.active && shrine.cooldown > 0 {
            shrine.cooldown = shrine.cooldown.saturating_sub(FIXED_ONE);
            if shrine.cooldown <= 0 {
                shrine.active = true;
            }
        }
    }

    // Step 4: Apply actions
    for action in actions {
        match action {
            ShrineAction::StartChannel { shrine_id, player_id } => {
                if let Some(shrine) = state.shrines.iter_mut().find(|s| s.id == shrine_id) {
                    shrine.channeling_player = Some(player_id);
                    shrine.channel_progress = config.channel_rate;
                }
            }
            ShrineAction::ContinueChannel { shrine_id, progress } => {
                if let Some(shrine) = state.shrines.iter_mut().find(|s| s.id == shrine_id) {
                    shrine.channel_progress = progress;
                }
            }
            ShrineAction::CompleteChannel { shrine_id, player_id, shrine_type } => {
                if let Some(shrine) = state.shrines.iter_mut().find(|s| s.id == shrine_id) {
                    shrine.active = false;
                    shrine.cooldown = (ShrineState::COOLDOWN_TICKS as i64 * FIXED_ONE as i64 / 60) as Fixed;
                    shrine.channeling_player = None;
                    shrine.channel_progress = 0;
                }
                // Apply buff to player
                if let Some(player) = state.players.get_mut(&player_id) {
                    player.add_shrine_buff(shrine_type, buff_duration);
                }
            }
            ShrineAction::InterruptChannel { shrine_id, player_id: _ } => {
                if let Some(shrine) = state.shrines.iter_mut().find(|s| s.id == shrine_id) {
                    shrine.channeling_player = None;
                    shrine.channel_progress = 0;
                }
            }
            ShrineAction::StartNewChannel { shrine_id, old_player: _, new_player } => {
                if let Some(shrine) = state.shrines.iter_mut().find(|s| s.id == shrine_id) {
                    shrine.channeling_player = Some(new_player);
                    shrine.channel_progress = config.channel_rate;
                }
            }
        }
    }

    // Step 5: Push events
    for event in events {
        state.push_event(event);
    }
}

/// Check if a player is on a shrine.
fn is_player_on_shrine(player_pos: FixedVec2, player_radius: Fixed, shrine: &ShrineState) -> bool {
    circles_overlap(player_pos, player_radius, shrine.position, ShrineState::RADIUS)
}

/// Get speed multiplier from shrine buff.
/// Returns FIXED_ONE if no speed buff, 1.2x if has speed shrine buff.
pub fn get_speed_multiplier(player: &crate::game::state::PlayerState) -> Fixed {
    if player.has_shrine_buff(ShrineType::Speed) {
        78643 // 1.2 * 65536
    } else {
        FIXED_ONE
    }
}

/// Get wisdom (XP/score) multiplier from shrine buff.
/// Returns 1 if no wisdom buff, 2 if has wisdom shrine buff.
pub fn get_wisdom_multiplier(player: &crate::game::state::PlayerState) -> u32 {
    if player.has_shrine_buff(ShrineType::Wisdom) {
        2
    } else {
        1
    }
}

/// Get shield damage reduction from shrine buff.
/// Returns FIXED_ONE if no shield buff, 0.5x if has shield shrine buff.
pub fn get_shield_multiplier(player: &crate::game::state::PlayerState) -> Fixed {
    if player.has_shrine_buff(ShrineType::Shield) {
        32768 // 0.5 * 65536
    } else {
        FIXED_ONE
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_shrine_spawn_positions() {
        let mut state = MatchState::new([0; 16], 12345);
        spawn_shrines(&mut state);

        assert_eq!(state.shrines.len(), 4);

        // Check types
        assert!(state.shrines.iter().any(|s| s.shrine_type == ShrineType::Wisdom));
        assert!(state.shrines.iter().any(|s| s.shrine_type == ShrineType::Power));
        assert!(state.shrines.iter().any(|s| s.shrine_type == ShrineType::Speed));
        assert!(state.shrines.iter().any(|s| s.shrine_type == ShrineType::Shield));

        // All should be active
        assert!(state.shrines.iter().all(|s| s.active));
    }

    #[test]
    fn test_shrine_channel_progress() {
        let mut state = MatchState::new([0; 16], 12345);
        state.phase = crate::game::state::MatchPhase::Playing;
        spawn_shrines(&mut state);

        // Add player at shrine position
        let player_id = PlayerId::new([1; 16]);
        state.players.insert(
            player_id,
            crate::game::state::PlayerState::new(player_id, FixedVec2::new(2293760, 2293760))
        );
        state.alive_count = 1;

        let config = ShrineConfig::default();

        // Process one tick
        process_shrines(&mut state, &config);

        // Should have started channeling
        let shrine = &state.shrines[0];
        assert_eq!(shrine.channeling_player, Some(player_id));
        assert!(shrine.channel_progress > 0);
    }

    #[test]
    fn test_shrine_buff_application() {
        let player_id = PlayerId::new([1; 16]);
        let mut player = crate::game::state::PlayerState::new(player_id, FixedVec2::ZERO);

        assert!(!player.has_shrine_buff(ShrineType::Speed));

        player.add_shrine_buff(ShrineType::Speed, 1800);
        assert!(player.has_shrine_buff(ShrineType::Speed));

        // Check multiplier
        assert_eq!(get_speed_multiplier(&player), 78643);
    }
}

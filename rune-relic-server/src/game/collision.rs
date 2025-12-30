//! Collision Detection
//!
//! Deterministic collision detection for players and entities.

use crate::core::fixed::{Fixed, fixed_mul};
use crate::core::vec2::FixedVec2;
use crate::game::state::{PlayerId, PlayerState, RuneState, MatchState};

/// Check if two circles overlap.
#[inline]
pub fn circles_overlap(
    pos_a: FixedVec2,
    radius_a: Fixed,
    pos_b: FixedVec2,
    radius_b: Fixed,
) -> bool {
    let combined_radius = radius_a + radius_b;
    let combined_radius_sq = fixed_mul(combined_radius, combined_radius);
    pos_a.distance_squared(pos_b) <= combined_radius_sq
}

/// Result of a player-vs-player collision.
#[derive(Debug)]
pub struct PlayerCollision {
    /// The larger player (winner)
    pub winner: PlayerId,
    /// The smaller player (loser, to be eliminated)
    pub loser: PlayerId,
}

/// Check collision between two players.
///
/// Returns Some(collision) if they collide and one can eat the other.
/// Uses player ID for tie-breaking when forms are equal.
/// Respects invulnerability and shield buffs.
pub fn check_player_collision(a: &PlayerState, b: &PlayerState) -> Option<PlayerCollision> {
    // Skip if either is dead
    if !a.alive || !b.alive {
        return None;
    }

    // Skip if either player is invulnerable (Phase Shift ability)
    if a.invulnerable_ticks > 0 || b.invulnerable_ticks > 0 {
        return None;
    }

    // Check if circles overlap
    if !circles_overlap(a.position, a.radius(), b.position, b.radius()) {
        return None;
    }

    // Determine winner based on form
    let (winner, loser) = if a.form > b.form {
        (a.id, b.id)
    } else if b.form > a.form {
        (b.id, a.id)
    } else {
        // Same form - check shield buffs first
        let a_has_shield = a.shield_buff_ticks > 0
            || a.has_shrine_buff(crate::game::state::ShrineType::Shield);
        let b_has_shield = b.shield_buff_ticks > 0
            || b.has_shrine_buff(crate::game::state::ShrineType::Shield);

        if a_has_shield && !b_has_shield {
            // A has shield advantage
            (a.id, b.id)
        } else if b_has_shield && !a_has_shield {
            // B has shield advantage
            (b.id, a.id)
        } else {
            // Both have shields or neither - tie-break by player ID (lower wins)
            // This is deterministic because PlayerId implements Ord
            if a.id < b.id {
                (a.id, b.id)
            } else {
                (b.id, a.id)
            }
        }
    };

    Some(PlayerCollision { winner, loser })
}

/// Check all player-vs-player collisions in deterministic order.
///
/// Returns list of collisions to process.
pub fn check_all_player_collisions(state: &MatchState) -> Vec<PlayerCollision> {
    let mut collisions = Vec::new();

    // Get sorted list of player IDs (BTreeMap keys are already sorted)
    let player_ids: Vec<PlayerId> = state.players.keys().cloned().collect();

    // Check all pairs (i, j) where i < j
    for i in 0..player_ids.len() {
        for j in (i + 1)..player_ids.len() {
            let id_a = &player_ids[i];
            let id_b = &player_ids[j];

            if let (Some(player_a), Some(player_b)) =
                (state.players.get(id_a), state.players.get(id_b))
            {
                if let Some(collision) = check_player_collision(player_a, player_b) {
                    collisions.push(collision);
                }
            }
        }
    }

    collisions
}

/// Result of a player-vs-rune collision.
#[derive(Debug)]
pub struct RuneCollision {
    pub player_id: PlayerId,
    pub rune_id: u32,
}

/// Check if a player collides with a rune.
pub fn check_rune_collision(player: &PlayerState, rune: &RuneState) -> bool {
    if !player.alive || rune.collected {
        return false;
    }

    circles_overlap(
        player.position,
        player.radius(),
        rune.position,
        RuneState::RADIUS,
    )
}

/// Check all player-vs-rune collisions in deterministic order.
pub fn check_all_rune_collisions(state: &MatchState) -> Vec<RuneCollision> {
    let mut collisions = Vec::new();

    // Iterate players in sorted order (BTreeMap)
    for (player_id, player) in &state.players {
        if !player.alive {
            continue;
        }

        // Iterate runes in sorted order (BTreeMap)
        for (rune_id, rune) in &state.runes {
            if rune.collected {
                continue;
            }

            if check_rune_collision(player, rune) {
                collisions.push(RuneCollision {
                    player_id: *player_id,
                    rune_id: *rune_id,
                });
            }
        }
    }

    collisions
}

/// Check if player is outside arena bounds.
pub fn check_bounds_collision(state: &MatchState, player: &PlayerState) -> bool {
    if !player.alive {
        return false;
    }

    !state.is_in_bounds(player)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::core::fixed::to_fixed;
    use crate::game::state::Form;

    #[test]
    fn test_circles_overlap() {
        let pos_a = FixedVec2::new(0, 0);
        let pos_b = FixedVec2::new(to_fixed(1.0), 0);
        let radius = to_fixed(0.6);

        // Should overlap (distance 1.0, combined radius 1.2)
        assert!(circles_overlap(pos_a, radius, pos_b, radius));

        // Should not overlap
        let pos_c = FixedVec2::new(to_fixed(2.0), 0);
        assert!(!circles_overlap(pos_a, radius, pos_c, radius));
    }

    #[test]
    fn test_player_collision_form_wins() {
        let id1 = PlayerId::new([1; 16]);
        let id2 = PlayerId::new([2; 16]);

        let mut player1 = PlayerState::new(id1, FixedVec2::new(0, 0));
        let mut player2 = PlayerState::new(id2, FixedVec2::new(to_fixed(0.5), 0));

        // Same form - id1 < id2, so id1 wins
        let collision = check_player_collision(&player1, &player2).unwrap();
        assert_eq!(collision.winner, id1);
        assert_eq!(collision.loser, id2);

        // Player2 evolves - now player2 wins
        player2.form = Form::Glyph;
        let collision = check_player_collision(&player1, &player2).unwrap();
        assert_eq!(collision.winner, id2);
        assert_eq!(collision.loser, id1);
    }

    #[test]
    fn test_player_collision_dead_skipped() {
        let id1 = PlayerId::new([1; 16]);
        let id2 = PlayerId::new([2; 16]);

        let player1 = PlayerState::new(id1, FixedVec2::ZERO);
        let mut player2 = PlayerState::new(id2, FixedVec2::ZERO);
        player2.alive = false;

        assert!(check_player_collision(&player1, &player2).is_none());
    }

    #[test]
    fn test_rune_collision() {
        let id = PlayerId::new([1; 16]);
        let player = PlayerState::new(id, FixedVec2::ZERO);
        let rune = RuneState::new(0, FixedVec2::new(to_fixed(0.3), 0), crate::game::state::RuneType::Wisdom);

        assert!(check_rune_collision(&player, &rune));

        // Farther rune
        let far_rune = RuneState::new(1, FixedVec2::new(to_fixed(5.0), 0), crate::game::state::RuneType::Wisdom);
        assert!(!check_rune_collision(&player, &far_rune));
    }
}

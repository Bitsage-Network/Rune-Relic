//! Game Events
//!
//! Events generated during simulation for replay and verification.

use serde::{Serialize, Deserialize};
use crate::core::vec2::FixedVec2;
use crate::game::state::{PlayerId, Form, RuneType};

/// Priority for event processing order.
///
/// Lower value = processed first.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[repr(u8)]
pub enum EventPriority {
    /// Player deaths processed first
    PlayerElimination = 0,
    /// Then pickups
    RuneCollection = 1,
    /// Then evolutions
    FormEvolution = 2,
    /// Then shrine events
    ShrineActivation = 3,
    /// Then abilities
    AbilityEffect = 4,
    /// Lowest priority
    Other = 255,
}

/// Game event data.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub enum GameEventData {
    /// Player was eliminated
    PlayerEliminated {
        victim_id: PlayerId,
        killer_id: Option<PlayerId>,
        placement: u8,
    },

    /// Player collected a rune
    RuneCollected {
        player_id: PlayerId,
        rune_id: u32,
        rune_type: RuneType,
        points: u32,
        new_score: u32,
    },

    /// Player evolved to new form
    FormEvolved {
        player_id: PlayerId,
        old_form: Form,
        new_form: Form,
    },

    /// Shrine activation started
    ShrineChannelStarted {
        player_id: PlayerId,
        shrine_id: u8,
    },

    /// Shrine activation completed
    ShrineActivated {
        player_id: PlayerId,
        shrine_id: u8,
    },

    /// Shrine channel interrupted
    ShrineChannelInterrupted {
        player_id: PlayerId,
        shrine_id: u8,
    },

    /// Player used ability
    AbilityUsed {
        player_id: PlayerId,
        ability_type: u8,
    },

    /// Match phase changed
    PhaseChanged {
        old_phase: String,
        new_phase: String,
    },

    /// Rune spawned
    RuneSpawned {
        rune_id: u32,
        rune_type: RuneType,
        position: FixedVec2,
    },

    /// Match ended
    MatchEnded {
        winner_id: Option<PlayerId>,
        duration_ticks: u32,
    },
}

/// A game event with timing and priority.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct GameEvent {
    /// Tick when event occurred
    pub tick: u32,

    /// Processing priority
    pub priority: EventPriority,

    /// Player involved (for tie-breaking)
    pub player_id: Option<PlayerId>,

    /// Event data
    pub data: GameEventData,
}

impl GameEvent {
    /// Create a new event.
    pub fn new(tick: u32, priority: EventPriority, data: GameEventData) -> Self {
        let player_id = match &data {
            GameEventData::PlayerEliminated { victim_id, .. } => Some(*victim_id),
            GameEventData::RuneCollected { player_id, .. } => Some(*player_id),
            GameEventData::FormEvolved { player_id, .. } => Some(*player_id),
            GameEventData::ShrineChannelStarted { player_id, .. } => Some(*player_id),
            GameEventData::ShrineActivated { player_id, .. } => Some(*player_id),
            GameEventData::ShrineChannelInterrupted { player_id, .. } => Some(*player_id),
            GameEventData::AbilityUsed { player_id, .. } => Some(*player_id),
            GameEventData::MatchEnded { winner_id, .. } => *winner_id,
            _ => None,
        };

        Self {
            tick,
            priority,
            player_id,
            data,
        }
    }

    /// Create player eliminated event.
    pub fn player_eliminated(
        tick: u32,
        victim_id: PlayerId,
        killer_id: Option<PlayerId>,
        placement: u8,
    ) -> Self {
        Self::new(
            tick,
            EventPriority::PlayerElimination,
            GameEventData::PlayerEliminated {
                victim_id,
                killer_id,
                placement,
            },
        )
    }

    /// Create rune collected event.
    pub fn rune_collected(
        tick: u32,
        player_id: PlayerId,
        rune_id: u32,
        rune_type: RuneType,
        points: u32,
        new_score: u32,
    ) -> Self {
        Self::new(
            tick,
            EventPriority::RuneCollection,
            GameEventData::RuneCollected {
                player_id,
                rune_id,
                rune_type,
                points,
                new_score,
            },
        )
    }

    /// Create form evolved event.
    pub fn form_evolved(tick: u32, player_id: PlayerId, old_form: Form, new_form: Form) -> Self {
        Self::new(
            tick,
            EventPriority::FormEvolution,
            GameEventData::FormEvolved {
                player_id,
                old_form,
                new_form,
            },
        )
    }

    /// Create match ended event.
    pub fn match_ended(tick: u32, winner_id: Option<PlayerId>) -> Self {
        Self::new(
            tick,
            EventPriority::Other,
            GameEventData::MatchEnded {
                winner_id,
                duration_ticks: tick,
            },
        )
    }

    /// Create shrine channel started event.
    pub fn shrine_channel_started(tick: u32, player_id: PlayerId, shrine_id: u8) -> Self {
        Self::new(
            tick,
            EventPriority::ShrineActivation,
            GameEventData::ShrineChannelStarted { player_id, shrine_id },
        )
    }

    /// Create shrine activated event.
    pub fn shrine_activated(tick: u32, player_id: PlayerId, shrine_id: u8) -> Self {
        Self::new(
            tick,
            EventPriority::ShrineActivation,
            GameEventData::ShrineActivated { player_id, shrine_id },
        )
    }

    /// Create shrine channel interrupted event.
    pub fn shrine_channel_interrupted(tick: u32, player_id: PlayerId, shrine_id: u8) -> Self {
        Self::new(
            tick,
            EventPriority::ShrineActivation,
            GameEventData::ShrineChannelInterrupted { player_id, shrine_id },
        )
    }

    /// Create ability used event.
    pub fn ability_used(tick: u32, player_id: PlayerId, ability_type: u8) -> Self {
        Self::new(
            tick,
            EventPriority::AbilityEffect,
            GameEventData::AbilityUsed { player_id, ability_type },
        )
    }
}

impl PartialEq for GameEvent {
    fn eq(&self, other: &Self) -> bool {
        self.tick == other.tick
            && self.priority == other.priority
            && self.player_id == other.player_id
    }
}

impl Eq for GameEvent {}

impl PartialOrd for GameEvent {
    fn partial_cmp(&self, other: &Self) -> Option<std::cmp::Ordering> {
        Some(self.cmp(other))
    }
}

impl Ord for GameEvent {
    fn cmp(&self, other: &Self) -> std::cmp::Ordering {
        // Sort by: tick, then priority, then player_id
        self.tick
            .cmp(&other.tick)
            .then(self.priority.cmp(&other.priority))
            .then(self.player_id.cmp(&other.player_id))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_event_ordering() {
        let id1 = PlayerId::new([1; 16]);
        let id2 = PlayerId::new([2; 16]);

        let event1 = GameEvent::player_eliminated(10, id1, None, 5);
        let event2 = GameEvent::rune_collected(10, id1, 0, RuneType::Wisdom, 10, 100);
        let event3 = GameEvent::player_eliminated(10, id2, None, 4);

        // Same tick, but elimination < collection
        assert!(event1 < event2);

        // Same tick and priority, but id1 < id2
        assert!(event1 < event3);
    }
}

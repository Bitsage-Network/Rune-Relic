//! Arcane Circuit Map Geometry
//!
//! Deterministic geometry helpers for hubs, corridors, and spawn alcoves.

use crate::core::fixed::{Fixed, FIXED_ONE, fixed_mul, fixed_div, fixed_abs, to_fixed};
use crate::core::vec2::FixedVec2;
use crate::core::rng::DeterministicRng;
use crate::game::state::RuneState;

const CORRIDOR_WIDTH: Fixed = to_fixed(7.0);
const CORRIDOR_HALF_WIDTH: Fixed = to_fixed(3.5);
const SPAWN_OFFSET: Fixed = to_fixed(20.0);
const SPAWN_RADIUS: Fixed = to_fixed(5.0);

#[derive(Clone, Copy, Debug)]
pub struct Hub {
    pub center: FixedVec2,
    pub radius: Fixed,
}

#[derive(Clone, Copy, Debug)]
pub struct Corridor {
    pub start: FixedVec2,
    pub end: FixedVec2,
    pub half_width: Fixed,
}

#[derive(Clone, Copy, Debug)]
pub struct SpawnZone {
    pub id: u8,
    pub anchor: FixedVec2,
    pub center: FixedVec2,
    pub radius: Fixed,
    pub corridor: Corridor,
}

#[derive(Clone, Debug)]
pub struct ArcaneCircuitMap {
    hubs: Vec<Hub>,
    corridors: Vec<Corridor>,
    spawn_zones: Vec<SpawnZone>,
    hub_weights: Vec<u32>,
    corridor_weights: Vec<u32>,
}

impl ArcaneCircuitMap {
    pub fn new() -> Self {
        let hubs = vec![
            Hub { center: FixedVec2::from_ints(0, 0), radius: to_fixed(35.0) },
            Hub { center: FixedVec2::from_ints(0, 90), radius: to_fixed(35.0) },
            Hub { center: FixedVec2::from_ints(0, -90), radius: to_fixed(35.0) },
            Hub { center: FixedVec2::from_ints(140, 0), radius: to_fixed(35.0) },
            Hub { center: FixedVec2::from_ints(-140, 0), radius: to_fixed(35.0) },
            Hub { center: FixedVec2::from_ints(70, 45), radius: to_fixed(18.0) },
            Hub { center: FixedVec2::from_ints(-70, 45), radius: to_fixed(18.0) },
            Hub { center: FixedVec2::from_ints(70, -45), radius: to_fixed(18.0) },
            Hub { center: FixedVec2::from_ints(-70, -45), radius: to_fixed(18.0) },
        ];

        let mut corridors = vec![
            // Inner spokes
            Corridor::new(FixedVec2::from_ints(0, 0), FixedVec2::from_ints(70, 45)),
            Corridor::new(FixedVec2::from_ints(0, 0), FixedVec2::from_ints(-70, 45)),
            Corridor::new(FixedVec2::from_ints(0, 0), FixedVec2::from_ints(70, -45)),
            Corridor::new(FixedVec2::from_ints(0, 0), FixedVec2::from_ints(-70, -45)),

            // Junction connectors
            Corridor::new(FixedVec2::from_ints(70, 45), FixedVec2::from_ints(0, 90)),
            Corridor::new(FixedVec2::from_ints(70, 45), FixedVec2::from_ints(140, 0)),
            Corridor::new(FixedVec2::from_ints(-70, 45), FixedVec2::from_ints(0, 90)),
            Corridor::new(FixedVec2::from_ints(-70, 45), FixedVec2::from_ints(-140, 0)),
            Corridor::new(FixedVec2::from_ints(70, -45), FixedVec2::from_ints(0, -90)),
            Corridor::new(FixedVec2::from_ints(70, -45), FixedVec2::from_ints(140, 0)),
            Corridor::new(FixedVec2::from_ints(-70, -45), FixedVec2::from_ints(0, -90)),
            Corridor::new(FixedVec2::from_ints(-70, -45), FixedVec2::from_ints(-140, 0)),

            // Outer ring (3-segment polyline per side)
            Corridor::new(FixedVec2::from_ints(0, 90), FixedVec2::from_ints(0, 140)),
            Corridor::new(FixedVec2::from_ints(0, 140), FixedVec2::from_ints(140, 140)),
            Corridor::new(FixedVec2::from_ints(140, 140), FixedVec2::from_ints(140, 0)),

            Corridor::new(FixedVec2::from_ints(140, 0), FixedVec2::from_ints(140, -140)),
            Corridor::new(FixedVec2::from_ints(140, -140), FixedVec2::from_ints(0, -140)),
            Corridor::new(FixedVec2::from_ints(0, -140), FixedVec2::from_ints(0, -90)),

            Corridor::new(FixedVec2::from_ints(0, -90), FixedVec2::from_ints(0, -140)),
            Corridor::new(FixedVec2::from_ints(0, -140), FixedVec2::from_ints(-140, -140)),
            Corridor::new(FixedVec2::from_ints(-140, -140), FixedVec2::from_ints(-140, 0)),

            Corridor::new(FixedVec2::from_ints(-140, 0), FixedVec2::from_ints(-140, 140)),
            Corridor::new(FixedVec2::from_ints(-140, 140), FixedVec2::from_ints(0, 140)),
            Corridor::new(FixedVec2::from_ints(0, 140), FixedVec2::from_ints(0, 90)),
        ];

        let spawn_anchors = [
            FixedVec2::from_ints(-20, 115),
            FixedVec2::from_ints(20, 115),
            FixedVec2::from_ints(-20, -115),
            FixedVec2::from_ints(20, -115),
            FixedVec2::from_ints(165, 20),
            FixedVec2::from_ints(165, -20),
            FixedVec2::from_ints(-165, 20),
            FixedVec2::from_ints(-165, -20),
            FixedVec2::from_ints(110, 80),
            FixedVec2::from_ints(120, 70),
            FixedVec2::from_ints(-110, 80),
            FixedVec2::from_ints(-120, 70),
            FixedVec2::from_ints(110, -80),
            FixedVec2::from_ints(120, -70),
            FixedVec2::from_ints(-110, -80),
            FixedVec2::from_ints(-120, -70),
        ];

        let mut spawn_zones = Vec::new();
        for (id, anchor) in spawn_anchors.iter().enumerate() {
            let dir = anchor.normalize();
            let center = anchor.add(dir.scale(SPAWN_OFFSET));
            let corridor = Corridor::new(*anchor, center);
            spawn_zones.push(SpawnZone {
                id: id as u8,
                anchor: *anchor,
                center,
                radius: SPAWN_RADIUS,
                corridor,
            });
        }

        let hub_weights = hubs
            .iter()
            .map(|hub| fixed_abs(fixed_mul(hub.radius, hub.radius)))
            .map(|w| w.max(1) as u32)
            .collect();

        let corridor_weights = corridors
            .iter()
            .map(|corridor| fixed_abs(corridor.length()))
            .map(|w| w.max(1) as u32)
            .collect();

        Self {
            hubs,
            corridors,
            spawn_zones,
            hub_weights,
            corridor_weights,
        }
    }

    pub fn hubs(&self) -> &[Hub] {
        &self.hubs
    }

    pub fn corridors(&self) -> &[Corridor] {
        &self.corridors
    }

    pub fn spawn_zones(&self) -> &[SpawnZone] {
        &self.spawn_zones
    }

    pub fn spawn_zone(&self, id: u8) -> Option<&SpawnZone> {
        self.spawn_zones.iter().find(|zone| zone.id == id)
    }

    pub fn contains_player_position(
        &self,
        position: FixedVec2,
        radius: Fixed,
        spawn_zone_id: Option<u8>,
        spawn_zone_active: bool,
    ) -> bool {
        if self.contains_in_hub(position, radius) || self.contains_in_corridor(position, radius) {
            return true;
        }

        if spawn_zone_active {
            if let Some(zone_id) = spawn_zone_id {
                if let Some(zone) = self.spawn_zones.iter().find(|z| z.id == zone_id) {
                    return zone.contains(position, radius);
                }
            }
        }

        false
    }

    pub fn spawn_zone_contains(&self, zone_id: u8, position: FixedVec2, radius: Fixed) -> bool {
        self.spawn_zones
            .iter()
            .find(|zone| zone.id == zone_id)
            .is_some_and(|zone| zone.contains(position, radius))
    }

    pub fn random_pellet_position(
        &self,
        rng: &mut DeterministicRng,
        weight_hubs: u32,
        weight_corridors: u32,
        weight_spawns: u32,
    ) -> FixedVec2 {
        let total = weight_hubs + weight_corridors + weight_spawns;
        if total == 0 {
            return FixedVec2::ZERO;
        }

        let roll = rng.next_int(total) as u32;
        if roll < weight_hubs {
            self.random_point_in_hub(rng)
        } else if roll < weight_hubs + weight_corridors {
            self.random_point_in_corridor(rng)
        } else {
            self.random_point_in_spawn_zone(rng)
        }
    }

    pub fn random_point_in_hub(&self, rng: &mut DeterministicRng) -> FixedVec2 {
        let idx = pick_weighted_index(&self.hub_weights, rng);
        let hub = self.hubs.get(idx).unwrap_or(&self.hubs[0]);
        let radius = hub.radius.saturating_sub(RuneState::RADIUS);
        rng.random_position_in_circle(hub.center, radius.max(0))
    }

    pub fn random_point_in_corridor(&self, rng: &mut DeterministicRng) -> FixedVec2 {
        let idx = pick_weighted_index(&self.corridor_weights, rng);
        let corridor = self.corridors.get(idx).unwrap_or(&self.corridors[0]);
        corridor.random_point(rng, RuneState::RADIUS)
    }

    pub fn random_point_in_spawn_zone(&self, rng: &mut DeterministicRng) -> FixedVec2 {
        let idx = rng.next_int(self.spawn_zones.len() as u32) as usize;
        let zone = &self.spawn_zones[idx];
        let radius = zone.radius.saturating_sub(RuneState::RADIUS);
        rng.random_position_in_circle(zone.center, radius.max(0))
    }

    fn contains_in_hub(&self, position: FixedVec2, radius: Fixed) -> bool {
        for hub in &self.hubs {
            if hub.contains(position, radius) {
                return true;
            }
        }
        false
    }

    fn contains_in_corridor(&self, position: FixedVec2, radius: Fixed) -> bool {
        for corridor in &self.corridors {
            if corridor.contains(position, radius) {
                return true;
            }
        }
        false
    }
}

impl Default for ArcaneCircuitMap {
    fn default() -> Self {
        Self::new()
    }
}

impl Hub {
    fn contains(&self, position: FixedVec2, radius: Fixed) -> bool {
        if self.radius <= radius {
            return false;
        }
        let allowed = self.radius.saturating_sub(radius);
        let allowed_sq = fixed_mul(allowed, allowed);
        position.distance_squared(self.center) <= allowed_sq
    }
}

impl Corridor {
    fn new(start: FixedVec2, end: FixedVec2) -> Self {
        Self {
            start,
            end,
            half_width: CORRIDOR_HALF_WIDTH,
        }
    }

    fn length(&self) -> Fixed {
        self.start.distance(self.end)
    }

    fn contains(&self, position: FixedVec2, radius: Fixed) -> bool {
        if self.half_width <= radius {
            return false;
        }
        let allowed = self.half_width.saturating_sub(radius);
        let allowed_sq = fixed_mul(allowed, allowed);
        distance_squared_to_segment(position, self.start, self.end) <= allowed_sq
    }

    fn random_point(&self, rng: &mut DeterministicRng, margin: Fixed) -> FixedVec2 {
        let half_width = self.half_width.saturating_sub(margin).max(0);
        let t = rng.next_fixed(FIXED_ONE);
        let offset = if half_width > 0 {
            rng.next_fixed_range(-half_width, half_width)
        } else {
            0
        };

        let ab = self.end.sub(self.start);
        let dir = ab.normalize();
        let perp = FixedVec2::new(-dir.y, dir.x);
        let center = self.start.add(ab.scale(t));
        center.add(perp.scale(offset))
    }
}

impl SpawnZone {
    fn contains(&self, position: FixedVec2, radius: Fixed) -> bool {
        if self.radius > radius {
            let allowed = self.radius.saturating_sub(radius);
            let allowed_sq = fixed_mul(allowed, allowed);
            if position.distance_squared(self.center) <= allowed_sq {
                return true;
            }
        }

        self.corridor.contains(position, radius)
    }
}

fn distance_squared_to_segment(point: FixedVec2, start: FixedVec2, end: FixedVec2) -> Fixed {
    let ab = end.sub(start);
    let ab_len_sq = ab.dot(ab);
    if ab_len_sq == 0 {
        return point.distance_squared(start);
    }

    let ap = point.sub(start);
    let t = fixed_div(ap.dot(ab), ab_len_sq);
    let t_clamped = if t < 0 { 0 } else if t > FIXED_ONE { FIXED_ONE } else { t };
    let closest = start.add(ab.scale(t_clamped));
    point.distance_squared(closest)
}

fn pick_weighted_index(weights: &[u32], rng: &mut DeterministicRng) -> usize {
    if weights.is_empty() {
        return 0;
    }
    let total: u32 = weights.iter().sum();
    if total == 0 {
        return 0;
    }

    let mut roll = rng.next_int(total);
    for (idx, weight) in weights.iter().enumerate() {
        if roll < *weight {
            return idx;
        }
        roll -= *weight;
    }
    weights.len() - 1
}

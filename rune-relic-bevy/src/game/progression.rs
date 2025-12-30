//! Progression systems - XP, levels, upgrades

use bevy::prelude::*;

use super::{
    CircleCollider, Player, LocalPlayer, XpGem, Magnetized,
    PlayerForm, LevelUpEvent, WeaponType, Weapon,
};
use crate::AppState;

// ============================================================================
// PLAYER PROGRESSION RESOURCE
// ============================================================================

/// Tracks player progression state
#[derive(Resource)]
pub struct PlayerProgression {
    pub experience: u32,
    pub level: u32,
    pub xp_to_next_level: u32,
    pub form: PlayerForm,
    pub weapons: Vec<(WeaponType, u32)>, // (type, level)
    pub passive_bonuses: PassiveBonuses,
}

impl Default for PlayerProgression {
    fn default() -> Self {
        Self {
            experience: 0,
            level: 1,
            xp_to_next_level: 10,
            form: PlayerForm::default(),
            weapons: vec![(WeaponType::RuneBolt, 1)],
            passive_bonuses: PassiveBonuses::default(),
        }
    }
}

impl PlayerProgression {
    /// Calculate XP needed for a given level
    fn xp_for_level(level: u32) -> u32 {
        // 10, 15, 22, 30, 40, 52, 66, 82, 100...
        (10.0 * (level as f32).powf(1.3)) as u32
    }

    /// Add XP and check for level up
    pub fn add_xp(&mut self, amount: u32) -> bool {
        self.experience += amount;

        if self.experience >= self.xp_to_next_level {
            self.level += 1;
            self.experience -= self.xp_to_next_level;
            self.xp_to_next_level = Self::xp_for_level(self.level);

            // Check for form evolution
            let new_form = PlayerForm::from_level(self.level);
            if new_form != self.form {
                info!("Player evolved to {:?}!", new_form);
                self.form = new_form;
            }

            return true;
        }

        false
    }

    /// XP progress as 0.0 to 1.0
    pub fn xp_progress(&self) -> f32 {
        self.experience as f32 / self.xp_to_next_level as f32
    }
}

/// Passive stat bonuses from upgrades
#[derive(Default, Clone)]
pub struct PassiveBonuses {
    pub move_speed_mult: f32,      // 1.0 = normal
    pub damage_mult: f32,          // 1.0 = normal
    pub pickup_radius_mult: f32,   // 1.0 = normal
    pub cooldown_reduction: f32,   // 0.0 = no reduction
    pub max_health_bonus: f32,     // Added to base health
    pub regen_per_second: f32,     // Health regen
}

impl PassiveBonuses {
    pub fn new() -> Self {
        Self {
            move_speed_mult: 1.0,
            damage_mult: 1.0,
            pickup_radius_mult: 1.0,
            cooldown_reduction: 0.0,
            max_health_bonus: 0.0,
            regen_per_second: 0.0,
        }
    }
}

// ============================================================================
// UPGRADE OPTIONS
// ============================================================================

/// Available upgrades when leveling up
#[derive(Debug, Clone)]
pub enum UpgradeOption {
    NewWeapon(WeaponType),
    WeaponUpgrade(WeaponType),
    PassiveBonus(PassiveUpgrade),
}

#[derive(Debug, Clone, Copy)]
pub enum PassiveUpgrade {
    MoveSpeed,     // +10% move speed
    Damage,        // +15% damage
    PickupRadius,  // +20% pickup radius
    Cooldown,      // -10% cooldowns
    MaxHealth,     // +20 max health
    Regen,         // +0.5 hp/s regen
}

impl PassiveUpgrade {
    pub fn apply(&self, bonuses: &mut PassiveBonuses) {
        match self {
            PassiveUpgrade::MoveSpeed => bonuses.move_speed_mult += 0.10,
            PassiveUpgrade::Damage => bonuses.damage_mult += 0.15,
            PassiveUpgrade::PickupRadius => bonuses.pickup_radius_mult += 0.20,
            PassiveUpgrade::Cooldown => bonuses.cooldown_reduction += 0.10,
            PassiveUpgrade::MaxHealth => bonuses.max_health_bonus += 20.0,
            PassiveUpgrade::Regen => bonuses.regen_per_second += 0.5,
        }
    }

    pub fn name(&self) -> &'static str {
        match self {
            PassiveUpgrade::MoveSpeed => "Swift Runes",
            PassiveUpgrade::Damage => "Power Surge",
            PassiveUpgrade::PickupRadius => "Magnetic Field",
            PassiveUpgrade::Cooldown => "Quick Cast",
            PassiveUpgrade::MaxHealth => "Vitality",
            PassiveUpgrade::Regen => "Regeneration",
        }
    }

    pub fn description(&self) -> &'static str {
        match self {
            PassiveUpgrade::MoveSpeed => "+10% movement speed",
            PassiveUpgrade::Damage => "+15% damage",
            PassiveUpgrade::PickupRadius => "+20% pickup radius",
            PassiveUpgrade::Cooldown => "-10% cooldowns",
            PassiveUpgrade::MaxHealth => "+20 max health",
            PassiveUpgrade::Regen => "+0.5 HP/s regeneration",
        }
    }
}

// ============================================================================
// XP GEM SYSTEMS
// ============================================================================

/// Base pickup radius
const BASE_PICKUP_RADIUS: f32 = 30.0;
/// Magnetism activation radius
const MAGNET_RADIUS: f32 = 100.0;
/// Magnetism speed
const MAGNET_SPEED: f32 = 300.0;

/// Magnetize XP gems toward player when in range
pub fn xp_gem_magnetism(
    mut commands: Commands,
    progression: Res<PlayerProgression>,
    player_query: Query<(Entity, &Transform), With<LocalPlayer>>,
    gem_query: Query<(Entity, &Transform), (With<XpGem>, Without<Magnetized>)>,
) {
    let Ok((player_entity, player_transform)) = player_query.get_single() else {
        return;
    };

    let player_pos = player_transform.translation.truncate();
    let magnet_radius = MAGNET_RADIUS * progression.passive_bonuses.pickup_radius_mult;

    for (gem_entity, gem_transform) in gem_query.iter() {
        let gem_pos = gem_transform.translation.truncate();
        let distance = player_pos.distance(gem_pos);

        if distance < magnet_radius {
            commands.entity(gem_entity).insert(Magnetized {
                target: player_entity,
                speed: MAGNET_SPEED,
            });
        }
    }
}

/// Move magnetized gems toward their target
pub fn move_magnetized(
    time: Res<Time>,
    mut gem_query: Query<(&mut Transform, &Magnetized)>,
    target_query: Query<&Transform, Without<Magnetized>>,
) {
    let dt = time.delta_secs();

    for (mut gem_transform, magnetized) in gem_query.iter_mut() {
        let Ok(target_transform) = target_query.get(magnetized.target) else {
            continue;
        };

        let gem_pos = gem_transform.translation.truncate();
        let target_pos = target_transform.translation.truncate();

        let direction = (target_pos - gem_pos).normalize_or_zero();
        let movement = direction * magnetized.speed * dt;

        gem_transform.translation.x += movement.x;
        gem_transform.translation.y += movement.y;
    }
}

/// Collect XP gems on contact
pub fn xp_gem_collection(
    mut commands: Commands,
    mut progression: ResMut<PlayerProgression>,
    mut level_events: EventWriter<LevelUpEvent>,
    mut next_state: ResMut<NextState<AppState>>,
    player_query: Query<&Transform, With<LocalPlayer>>,
    gem_query: Query<(Entity, &Transform, &XpGem)>,
) {
    let Ok(player_transform) = player_query.get_single() else {
        return;
    };

    let player_pos = player_transform.translation.truncate();
    let pickup_radius = BASE_PICKUP_RADIUS * progression.passive_bonuses.pickup_radius_mult;

    for (gem_entity, gem_transform, gem) in gem_query.iter() {
        let gem_pos = gem_transform.translation.truncate();
        let distance = player_pos.distance(gem_pos);

        if distance < pickup_radius {
            // Collect gem
            let leveled_up = progression.add_xp(gem.value);

            // Despawn gem
            commands.entity(gem_entity).despawn();

            // Trigger level up if applicable
            if leveled_up {
                level_events.send(LevelUpEvent {
                    new_level: progression.level,
                });

                // Transition to level up state
                next_state.set(AppState::LevelUp);
            }
        }
    }
}

// ============================================================================
// FORM EVOLUTION
// ============================================================================

/// Update player visuals when form changes
pub fn update_player_form(
    progression: Res<PlayerProgression>,
    mut player_query: Query<(&mut Sprite, &mut CircleCollider, &PlayerForm), With<LocalPlayer>>,
) {
    if !progression.is_changed() {
        return;
    }

    for (mut sprite, mut collider, current_form) in player_query.iter_mut() {
        if *current_form != progression.form {
            // Update visuals
            sprite.color = progression.form.color();
            sprite.custom_size = Some(Vec2::splat(progression.form.radius() * 2.0));

            // Update collider
            collider.radius = progression.form.radius();

            info!("Player form updated to {:?}", progression.form);
        }
    }
}

// Boss Encounter System
// 7 elemental bosses that rotate daily

import type { RuneElement, OwnedRune, RuneRarity } from './runes';
import { getSpecies, generateStats, ELEMENT_COLORS, ELEMENT_SYMBOLS } from './runes';

export interface Boss {
  id: string;
  name: string;
  title: string;
  element: RuneElement;
  speciesIds: [number, number, number]; // Team of 3 final-form runes
  statMultiplier: number;
  rewards: {
    sage: number;
    variantChance: number; // 0-100
    variant: 'shiny' | 'corrupted' | 'purified';
    bonusSage?: number; // Extra for special days
    guaranteedRarity?: RuneRarity;
  };
  description: string;
}

// The 7 Elemental Bosses
export const BOSSES: Boss[] = [
  {
    id: 'boss_fire',
    name: 'Inferno Rex',
    title: 'The Burning King',
    element: 'fire',
    speciesIds: [3, 3, 2], // 2 Infernos + 1 Blaze
    statMultiplier: 1.5,
    rewards: {
      sage: 150,
      variantChance: 25,
      variant: 'shiny',
    },
    description: 'The flames of rage incarnate. His power grows with each blow.',
  },
  {
    id: 'boss_water',
    name: 'Tsunami Lord',
    title: 'The Tidal Emperor',
    element: 'water',
    speciesIds: [6, 6, 5], // 2 Tsunamis + 1 Tide
    statMultiplier: 1.5,
    rewards: {
      sage: 200,
      variantChance: 15,
      variant: 'shiny',
      bonusSage: 50,
    },
    description: 'The depths bow to his command. His waves ignore all defenses.',
  },
  {
    id: 'boss_earth',
    name: 'Mountain King',
    title: 'The Immovable',
    element: 'earth',
    speciesIds: [9, 9, 8], // 2 Mountains + 1 Boulder
    statMultiplier: 1.5,
    rewards: {
      sage: 150,
      variantChance: 30,
      variant: 'corrupted',
    },
    description: 'An ancient fortress given form. Break through his walls if you can.',
  },
  {
    id: 'boss_air',
    name: 'Tempest Queen',
    title: 'The Storm Sovereign',
    element: 'air',
    speciesIds: [12, 12, 11], // 2 Tempests + 1 Gust
    statMultiplier: 1.5,
    rewards: {
      sage: 150,
      variantChance: 20,
      variant: 'shiny',
    },
    description: 'Chaos rides on every wind. Her speed defies prediction.',
  },
  {
    id: 'boss_light',
    name: 'Solar Warden',
    title: 'The Radiant Judge',
    element: 'light',
    speciesIds: [15, 15, 14], // 2 Solars + 1 Radiant
    statMultiplier: 1.5,
    rewards: {
      sage: 150,
      variantChance: 25,
      variant: 'purified',
    },
    description: 'Divine light that sears the darkness. His judgment is absolute.',
  },
  {
    id: 'boss_void',
    name: 'Abyss Herald',
    title: 'The Endless Dark',
    element: 'void',
    speciesIds: [18, 18, 17], // 2 Abyss + 1 Null
    statMultiplier: 1.5,
    rewards: {
      sage: 150,
      variantChance: 35,
      variant: 'corrupted',
    },
    description: 'From the void, he drains all hope. What he touches fades to nothing.',
  },
  {
    id: 'boss_arcane',
    name: 'The Primordial',
    title: 'Ancient One',
    element: 'arcane',
    speciesIds: [21, 21, 20], // 2 Relics + 1 Sigil
    statMultiplier: 1.75, // Hardest boss
    rewards: {
      sage: 250,
      variantChance: 20,
      variant: 'shiny',
      guaranteedRarity: 'legendary',
    },
    description: 'Before elements, there was only raw power. Face the origin itself.',
  },
];

// Day of week to boss mapping (0 = Sunday, 1 = Monday, etc.)
const DAY_TO_BOSS: Record<number, string> = {
  1: 'boss_fire',    // Monday
  2: 'boss_water',   // Tuesday
  3: 'boss_earth',   // Wednesday
  4: 'boss_air',     // Thursday
  5: 'boss_light',   // Friday
  6: 'boss_void',    // Saturday
  0: 'boss_arcane',  // Sunday (special)
};

// Get today's boss
export function getTodaysBoss(): Boss {
  const dayOfWeek = new Date().getDay();
  const bossId = DAY_TO_BOSS[dayOfWeek];
  return BOSSES.find(b => b.id === bossId)!;
}

// Get boss by ID
export function getBoss(id: string): Boss | undefined {
  return BOSSES.find(b => b.id === id);
}

// Generate a boss team with boosted stats
export function generateBossTeam(boss: Boss): OwnedRune[] {
  return boss.speciesIds.map((speciesId, index) => {
    const species = getSpecies(speciesId);
    if (!species) throw new Error(`Unknown species: ${speciesId}`);

    // Boss runes are always legendary with multiplied stats
    const baseStats = generateStats(species, 'legendary');

    return {
      id: `boss_rune_${boss.id}_${index}`,
      speciesId,
      rarity: 'legendary' as RuneRarity,
      stats: {
        power: Math.floor(baseStats.power * boss.statMultiplier),
        guard: Math.floor(baseStats.guard * boss.statMultiplier),
        speed: Math.floor(baseStats.speed * boss.statMultiplier),
      },
      variant: 'normal',
      wins: 0,
      catches: 0,
      caughtAt: 0,
    };
  });
}

// Calculate time until next boss reset (midnight local)
export function getTimeUntilBossReset(): { hours: number; minutes: number } {
  const now = new Date();
  const midnight = new Date(now);
  midnight.setDate(midnight.getDate() + 1);
  midnight.setHours(0, 0, 0, 0);

  const diff = midnight.getTime() - now.getTime();
  const hours = Math.floor(diff / (1000 * 60 * 60));
  const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));

  return { hours, minutes };
}

// Get boss element color
export function getBossColor(boss: Boss): string {
  return ELEMENT_COLORS[boss.element];
}

// Get boss element symbol
export function getBossSymbol(boss: Boss): string {
  return ELEMENT_SYMBOLS[boss.element];
}

// Calculate reward rune from boss victory
export function generateBossReward(boss: Boss): OwnedRune | null {
  const roll = Math.random() * 100;

  // Determine species - random from boss's element final forms
  const finalFormIds: Record<RuneElement, number> = {
    fire: 3,
    water: 6,
    earth: 9,
    air: 12,
    light: 15,
    void: 18,
    arcane: 21,
  };

  const speciesId = finalFormIds[boss.element];
  const species = getSpecies(speciesId);
  if (!species) return null;

  // Determine rarity
  const rarity = boss.rewards.guaranteedRarity ||
    (roll < 10 ? 'legendary' : roll < 35 ? 'epic' : 'rare');

  // Determine variant
  const variant = roll < boss.rewards.variantChance
    ? boss.rewards.variant
    : 'normal';

  const baseStats = generateStats(species, rarity);

  return {
    id: `rune_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
    speciesId,
    rarity,
    stats: baseStats,
    variant,
    wins: 0,
    catches: 1,
    caughtAt: Date.now(),
  };
}

// Battle Moves System

import type { RuneElement } from './runes';

export interface Move {
  id: string;
  name: string;
  element: RuneElement;
  power: number;        // Base damage
  accuracy: number;     // 0-100 hit chance
  energyCost: number;   // 0 = free, 1-3 for special moves
  effect?: MoveEffect;
  description: string;
}

export type MoveEffect =
  | { type: 'burn'; chance: number }      // Damage over time
  | { type: 'heal'; amount: number }      // Heal self
  | { type: 'buff'; stat: 'power' | 'guard' | 'speed'; amount: number }
  | { type: 'debuff'; stat: 'power' | 'guard' | 'speed'; amount: number }
  | { type: 'drain'; percent: number }    // Heal % of damage dealt
  | { type: 'priority' }                  // Always goes first
  | { type: 'recoil'; percent: number }   // Take % of damage dealt
  | { type: 'flinch'; chance: number };   // Target skips turn

// Universal basic attack (all runes have this)
export const BASIC_ATTACK: Move = {
  id: 'basic',
  name: 'Strike',
  element: 'arcane',
  power: 40,
  accuracy: 100,
  energyCost: 0,
  description: 'A basic attack.',
};

// Signature moves by species ID
export const SIGNATURE_MOVES: Record<number, Move> = {
  // FIRE
  1: { // Ember
    id: 'ignite',
    name: 'Ignite',
    element: 'fire',
    power: 50,
    accuracy: 95,
    energyCost: 1,
    effect: { type: 'burn', chance: 30 },
    description: 'A fiery strike that may burn.',
  },
  2: { // Blaze
    id: 'wildfire',
    name: 'Wildfire',
    element: 'fire',
    power: 70,
    accuracy: 90,
    energyCost: 2,
    effect: { type: 'burn', chance: 50 },
    description: 'Intense flames that often burn.',
  },
  3: { // Inferno
    id: 'pyroclasm',
    name: 'Pyroclasm',
    element: 'fire',
    power: 100,
    accuracy: 85,
    energyCost: 3,
    effect: { type: 'recoil', percent: 20 },
    description: 'Devastating fire. Hurts self too.',
  },

  // WATER
  4: { // Droplet
    id: 'splash',
    name: 'Splash',
    element: 'water',
    power: 45,
    accuracy: 100,
    energyCost: 1,
    description: 'A reliable water attack.',
  },
  5: { // Tide
    id: 'wave',
    name: 'Wave',
    element: 'water',
    power: 65,
    accuracy: 95,
    energyCost: 2,
    effect: { type: 'debuff', stat: 'speed', amount: 10 },
    description: 'Slows the target down.',
  },
  6: { // Tsunami
    id: 'deluge',
    name: 'Deluge',
    element: 'water',
    power: 90,
    accuracy: 85,
    energyCost: 3,
    description: 'Overwhelming water attack.',
  },

  // EARTH
  7: { // Pebble
    id: 'tumble',
    name: 'Tumble',
    element: 'earth',
    power: 45,
    accuracy: 95,
    energyCost: 1,
    description: 'A rolling stone attack.',
  },
  8: { // Boulder
    id: 'crush',
    name: 'Crush',
    element: 'earth',
    power: 75,
    accuracy: 90,
    energyCost: 2,
    effect: { type: 'flinch', chance: 30 },
    description: 'Heavy hit that may stun.',
  },
  9: { // Mountain
    id: 'earthquake',
    name: 'Earthquake',
    element: 'earth',
    power: 100,
    accuracy: 100,
    energyCost: 3,
    description: 'Massive seismic attack.',
  },

  // AIR
  10: { // Breeze
    id: 'whisper',
    name: 'Whisper',
    element: 'air',
    power: 40,
    accuracy: 100,
    energyCost: 1,
    effect: { type: 'priority' },
    description: 'Swift strike. Always first.',
  },
  11: { // Gust
    id: 'gale',
    name: 'Gale',
    element: 'air',
    power: 60,
    accuracy: 95,
    energyCost: 2,
    effect: { type: 'buff', stat: 'speed', amount: 15 },
    description: 'Boosts your speed.',
  },
  12: { // Tempest
    id: 'hurricane',
    name: 'Hurricane',
    element: 'air',
    power: 90,
    accuracy: 80,
    energyCost: 3,
    effect: { type: 'flinch', chance: 40 },
    description: 'Chaotic winds that may stun.',
  },

  // LIGHT
  13: { // Spark
    id: 'flash',
    name: 'Flash',
    element: 'light',
    power: 45,
    accuracy: 100,
    energyCost: 1,
    effect: { type: 'debuff', stat: 'guard', amount: 5 },
    description: 'Lowers target defense.',
  },
  14: { // Radiant
    id: 'beam',
    name: 'Beam',
    element: 'light',
    power: 60,
    accuracy: 95,
    energyCost: 2,
    effect: { type: 'heal', amount: 15 },
    description: 'Heals self slightly.',
  },
  15: { // Solar
    id: 'sunburst',
    name: 'Sunburst',
    element: 'light',
    power: 95,
    accuracy: 90,
    energyCost: 3,
    description: 'Brilliant light attack.',
  },

  // VOID
  16: { // Shadow
    id: 'fade',
    name: 'Fade',
    element: 'void',
    power: 50,
    accuracy: 100,
    energyCost: 1,
    effect: { type: 'buff', stat: 'speed', amount: 10 },
    description: 'Sneaky strike. Boosts speed.',
  },
  17: { // Null
    id: 'negate',
    name: 'Negate',
    element: 'void',
    power: 55,
    accuracy: 95,
    energyCost: 2,
    effect: { type: 'debuff', stat: 'power', amount: 15 },
    description: 'Weakens target attack.',
  },
  18: { // Abyss
    id: 'consume',
    name: 'Consume',
    element: 'void',
    power: 85,
    accuracy: 90,
    energyCost: 3,
    effect: { type: 'drain', percent: 30 },
    description: 'Drains HP from target.',
  },

  // ARCANE
  19: { // Glyph
    id: 'scribe',
    name: 'Scribe',
    element: 'arcane',
    power: 50,
    accuracy: 100,
    energyCost: 1,
    description: 'Mystical scratch attack.',
  },
  20: { // Sigil
    id: 'bind',
    name: 'Bind',
    element: 'arcane',
    power: 65,
    accuracy: 90,
    energyCost: 2,
    effect: { type: 'debuff', stat: 'speed', amount: 20 },
    description: 'Seals target movement.',
  },
  21: { // Relic
    id: 'primordial',
    name: 'Primordial',
    element: 'arcane',
    power: 110,
    accuracy: 85,
    energyCost: 3,
    description: 'Ancient devastating power.',
  },
};

// Get moves for a rune
export function getMovesForRune(speciesId: number): Move[] {
  const signature = SIGNATURE_MOVES[speciesId];
  return signature ? [BASIC_ATTACK, signature] : [BASIC_ATTACK];
}

// Calculate damage
export function calculateDamage(
  move: Move,
  attackerPower: number,
  defenderGuard: number,
  elementMultiplier: number,
  randomFactor: number = 0.9 + Math.random() * 0.2 // 0.9-1.1
): number {
  const baseDamage = (move.power * (attackerPower / 50)) * elementMultiplier;
  const defense = 1 - (defenderGuard / 200); // Guard reduces damage up to 50%
  return Math.floor(baseDamage * defense * randomFactor);
}

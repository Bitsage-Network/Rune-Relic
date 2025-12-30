// The 21 Base Runes of Rune Relic

export type RuneElement = 'fire' | 'water' | 'earth' | 'air' | 'light' | 'void' | 'arcane';

export type RuneRarity = 'common' | 'rare' | 'epic' | 'legendary';

export interface RuneSpecies {
  id: number;
  name: string;
  element: RuneElement;
  trait: string;
  traitDescription: string;
  signature: string;
  baseStats: {
    power: [number, number];  // min, max range
    guard: [number, number];
    speed: [number, number];
  };
  evolvesFrom?: number;  // id of pre-evolution
  evolvesTo?: number;    // id of evolution
}

export interface OwnedRune {
  id: string;           // unique instance id
  speciesId: number;    // which of the 21
  nickname?: string;
  rarity: RuneRarity;
  stats: {
    power: number;
    guard: number;
    speed: number;
  };
  variant?: 'normal' | 'shiny' | 'corrupted' | 'purified';
  wins: number;
  catches: number;      // mastery tracking
  caughtAt: number;
}

// Element colors for UI
export const ELEMENT_COLORS: Record<RuneElement, string> = {
  fire: '#ff4d4d',
  water: '#4da6ff',
  earth: '#8b6914',
  air: '#b8d4e3',
  light: '#ffd700',
  void: '#6b2d8b',
  arcane: '#00ffaa',
};

// Element symbols
export const ELEMENT_SYMBOLS: Record<RuneElement, string> = {
  fire: '🔥',
  water: '💧',
  earth: '🌍',
  air: '💨',
  light: '✨',
  void: '🌑',
  arcane: '⚡',
};

// Element advantage chart: attacker -> defender -> multiplier
export function getElementMultiplier(attacker: RuneElement, defender: RuneElement): number {
  const advantages: Record<RuneElement, RuneElement[]> = {
    fire: ['air'],
    water: ['fire'],
    earth: ['water'],
    air: ['earth'],
    light: ['void'],
    void: ['light'],
    arcane: [],  // neutral
  };

  if (advantages[attacker].includes(defender)) return 1.25;
  if (advantages[defender].includes(attacker)) return 0.8;
  return 1.0;
}

// THE 21 RUNES
export const RUNE_SPECIES: RuneSpecies[] = [
  // FIRE (1-3)
  {
    id: 1,
    name: 'Ember',
    element: 'fire',
    trait: 'Quick Strike',
    traitDescription: 'First hit deals +20% damage',
    signature: 'Ignite',
    baseStats: { power: [35, 50], guard: [20, 35], speed: [40, 55] },
    evolvesTo: 2,
  },
  {
    id: 2,
    name: 'Blaze',
    element: 'fire',
    trait: 'Burning',
    traitDescription: 'Deals damage over 2 rounds',
    signature: 'Wildfire',
    baseStats: { power: [50, 70], guard: [30, 45], speed: [45, 60] },
    evolvesFrom: 1,
    evolvesTo: 3,
  },
  {
    id: 3,
    name: 'Inferno',
    element: 'fire',
    trait: 'Rage',
    traitDescription: 'Power increases when losing',
    signature: 'Pyroclasm',
    baseStats: { power: [70, 90], guard: [35, 50], speed: [50, 65] },
    evolvesFrom: 2,
  },

  // WATER (4-6)
  {
    id: 4,
    name: 'Droplet',
    element: 'water',
    trait: 'Adaptive',
    traitDescription: 'Copies enemy element',
    signature: 'Splash',
    baseStats: { power: [30, 45], guard: [35, 50], speed: [35, 50] },
    evolvesTo: 5,
  },
  {
    id: 5,
    name: 'Tide',
    element: 'water',
    trait: 'Flow',
    traitDescription: 'Swaps position with ally',
    signature: 'Wave',
    baseStats: { power: [45, 65], guard: [45, 60], speed: [40, 55] },
    evolvesFrom: 4,
    evolvesTo: 6,
  },
  {
    id: 6,
    name: 'Tsunami',
    element: 'water',
    trait: 'Overwhelming',
    traitDescription: 'Ignores 50% defense',
    signature: 'Deluge',
    baseStats: { power: [65, 85], guard: [50, 70], speed: [45, 60] },
    evolvesFrom: 5,
  },

  // EARTH (7-9)
  {
    id: 7,
    name: 'Pebble',
    element: 'earth',
    trait: 'Sturdy',
    traitDescription: 'Survives one KO hit with 1 HP',
    signature: 'Tumble',
    baseStats: { power: [25, 40], guard: [45, 60], speed: [25, 40] },
    evolvesTo: 8,
  },
  {
    id: 8,
    name: 'Boulder',
    element: 'earth',
    trait: 'Heavy',
    traitDescription: "Can't be swapped out",
    signature: 'Crush',
    baseStats: { power: [45, 60], guard: [60, 75], speed: [20, 35] },
    evolvesFrom: 7,
    evolvesTo: 9,
  },
  {
    id: 9,
    name: 'Mountain',
    element: 'earth',
    trait: 'Fortress',
    traitDescription: '+50% defense, -20% speed',
    signature: 'Earthquake',
    baseStats: { power: [60, 80], guard: [75, 95], speed: [15, 30] },
    evolvesFrom: 8,
  },

  // AIR (10-12)
  {
    id: 10,
    name: 'Breeze',
    element: 'air',
    trait: 'Evasive',
    traitDescription: '20% chance to dodge attacks',
    signature: 'Whisper',
    baseStats: { power: [30, 45], guard: [25, 40], speed: [50, 65] },
    evolvesTo: 11,
  },
  {
    id: 11,
    name: 'Gust',
    element: 'air',
    trait: 'Swift',
    traitDescription: 'Always attacks first',
    signature: 'Gale',
    baseStats: { power: [40, 55], guard: [30, 45], speed: [65, 80] },
    evolvesFrom: 10,
    evolvesTo: 12,
  },
  {
    id: 12,
    name: 'Tempest',
    element: 'air',
    trait: 'Chaos',
    traitDescription: 'Randomizes enemy turn order',
    signature: 'Hurricane',
    baseStats: { power: [55, 75], guard: [35, 50], speed: [75, 95] },
    evolvesFrom: 11,
  },

  // LIGHT (13-15)
  {
    id: 13,
    name: 'Spark',
    element: 'light',
    trait: 'Illuminate',
    traitDescription: "Reveals enemy's next pick",
    signature: 'Flash',
    baseStats: { power: [30, 45], guard: [30, 45], speed: [40, 55] },
    evolvesTo: 14,
  },
  {
    id: 14,
    name: 'Radiant',
    element: 'light',
    trait: 'Blessed',
    traitDescription: 'Heals 10% after each round',
    signature: 'Beam',
    baseStats: { power: [45, 60], guard: [45, 60], speed: [45, 60] },
    evolvesFrom: 13,
    evolvesTo: 15,
  },
  {
    id: 15,
    name: 'Solar',
    element: 'light',
    trait: 'Judgment',
    traitDescription: 'Critical hits vs Void runes',
    signature: 'Sunburst',
    baseStats: { power: [65, 85], guard: [55, 75], speed: [50, 65] },
    evolvesFrom: 14,
  },

  // VOID (16-18)
  {
    id: 16,
    name: 'Shadow',
    element: 'void',
    trait: 'Stealth',
    traitDescription: 'Hidden until attacks',
    signature: 'Fade',
    baseStats: { power: [35, 50], guard: [25, 40], speed: [45, 60] },
    evolvesTo: 17,
  },
  {
    id: 17,
    name: 'Null',
    element: 'void',
    trait: 'Silence',
    traitDescription: "Disables enemy's trait",
    signature: 'Negate',
    baseStats: { power: [50, 65], guard: [35, 50], speed: [50, 65] },
    evolvesFrom: 16,
    evolvesTo: 18,
  },
  {
    id: 18,
    name: 'Abyss',
    element: 'void',
    trait: 'Drain',
    traitDescription: 'Steals 15% of damage dealt',
    signature: 'Consume',
    baseStats: { power: [70, 90], guard: [40, 55], speed: [55, 70] },
    evolvesFrom: 17,
  },

  // ARCANE (19-21)
  {
    id: 19,
    name: 'Glyph',
    element: 'arcane',
    trait: 'Inscribed',
    traitDescription: 'Trait changes each battle',
    signature: 'Scribe',
    baseStats: { power: [35, 55], guard: [35, 55], speed: [35, 55] },
    evolvesTo: 20,
  },
  {
    id: 20,
    name: 'Sigil',
    element: 'arcane',
    trait: 'Sealed',
    traitDescription: 'Unlocks power after 10 wins',
    signature: 'Bind',
    baseStats: { power: [50, 70], guard: [50, 70], speed: [50, 70] },
    evolvesFrom: 19,
    evolvesTo: 21,
  },
  {
    id: 21,
    name: 'Relic',
    element: 'arcane',
    trait: 'Ancient',
    traitDescription: 'Combines two random elements',
    signature: 'Primordial',
    baseStats: { power: [75, 95], guard: [75, 95], speed: [75, 95] },
    evolvesFrom: 20,
  },
];

// Helper to get species by ID
export function getSpecies(id: number): RuneSpecies | undefined {
  return RUNE_SPECIES.find(s => s.id === id);
}

// Generate random stats based on rarity
export function generateStats(species: RuneSpecies, rarity: RuneRarity): OwnedRune['stats'] {
  const rarityBonus: Record<RuneRarity, number> = {
    common: 0,
    rare: 10,
    epic: 20,
    legendary: 30,
  };

  const bonus = rarityBonus[rarity];

  const randomInRange = (range: [number, number]) => {
    const [min, max] = range;
    const base = min + Math.random() * (max - min);
    return Math.min(100, Math.floor(base + bonus * Math.random()));
  };

  return {
    power: randomInRange(species.baseStats.power),
    guard: randomInRange(species.baseStats.guard),
    speed: randomInRange(species.baseStats.speed),
  };
}

// Determine rarity from catch
export function rollRarity(): RuneRarity {
  const roll = Math.random() * 100;
  if (roll < 3) return 'legendary';
  if (roll < 15) return 'epic';
  if (roll < 40) return 'rare';
  return 'common';
}

// Create a new owned rune instance
export function createOwnedRune(speciesId: number, rarity?: RuneRarity): OwnedRune {
  const species = getSpecies(speciesId);
  if (!species) throw new Error(`Unknown species: ${speciesId}`);

  const actualRarity = rarity || rollRarity();

  return {
    id: `rune_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
    speciesId,
    rarity: actualRarity,
    stats: generateStats(species, actualRarity),
    variant: 'normal',
    wins: 0,
    catches: 1,
    caughtAt: Date.now(),
  };
}

// Rarity colors
export const RARITY_COLORS: Record<RuneRarity, string> = {
  common: '#9e9e9e',
  rare: '#2196f3',
  epic: '#9c27b0',
  legendary: '#ff9800',
};

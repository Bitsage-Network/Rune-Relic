// Mini-game types and shared utilities

export type MiniGameType = 'trace' | 'clash' | 'memory' | 'forge';

export interface MiniGameResult {
  success: boolean;
  score: number; // 0-100, affects bonus
  bonus: {
    rarityBoost: number;    // trace bonus
    sageBonus: number;      // clash bonus
    xpBonus: number;        // memory bonus
    shinyChance: number;    // forge bonus
  };
}

export interface MiniGameProps {
  speciesId: number;
  element: string;
  difficulty: number; // 0-1
  onComplete: (result: MiniGameResult) => void;
  onCancel: () => void;
}

export const GAME_INFO: Record<MiniGameType, {
  name: string;
  icon: string;
  description: string;
  bonusType: string;
}> = {
  trace: {
    name: 'Rune Tracing',
    icon: '✨',
    description: 'Trace the glowing pattern',
    bonusType: '+Rarity',
  },
  clash: {
    name: 'Element Clash',
    icon: '⚔️',
    description: 'Win the element battle',
    bonusType: '+SAGE',
  },
  memory: {
    name: 'Memory Match',
    icon: '🎴',
    description: 'Match the pairs quickly',
    bonusType: '+XP',
  },
  forge: {
    name: 'Forge Sequence',
    icon: '🔨',
    description: 'Follow the sequence',
    bonusType: '+Shiny',
  },
};

// Calculate final bonus based on game type and score
export function calculateBonus(game: MiniGameType, score: number): MiniGameResult['bonus'] {
  const base = {
    rarityBoost: 0,
    sageBonus: 0,
    xpBonus: 0,
    shinyChance: 0,
  };

  const multiplier = score / 100;

  switch (game) {
    case 'trace':
      base.rarityBoost = Math.floor(20 * multiplier); // Up to 20% better rarity
      break;
    case 'clash':
      base.sageBonus = Math.floor(25 * multiplier); // Up to 25 bonus SAGE
      break;
    case 'memory':
      base.xpBonus = Math.floor(50 * multiplier); // Up to 50% XP boost
      break;
    case 'forge':
      base.shinyChance = Math.floor(15 * multiplier); // Up to 15% shiny chance
      break;
  }

  return base;
}

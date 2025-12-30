// Rune Relic AI SAGE Edition - Type Definitions

export type RuneElement = 'fire' | 'water' | 'earth' | 'air' | 'void' | 'light';

export interface Rune {
  id: string;
  name: string;
  element: RuneElement;
  power: number;
  rarity: 'common' | 'rare' | 'epic' | 'legendary' | 'mythic';
  imageUrl?: string;
  lora?: string; // Custom LoRA style used
  createdAt: number;
}

export interface WizardPersonality {
  name: string;
  trait: 'wise' | 'mischievous' | 'fierce' | 'calm' | 'mysterious';
  greeting: string;
  battleCry: string;
}

export interface SageWizard {
  id: string;
  name: string;
  personality: WizardPersonality;
  level: number;
  experience: number;
  imageUrl?: string;
  messages: WizardMessage[];
}

export interface WizardMessage {
  id: string;
  role: 'user' | 'wizard';
  content: string;
  timestamp: number;
}

export interface Player {
  id: string;
  telegramId: number;
  username: string;
  sage: number; // SAGE token balance
  runes: Rune[];
  wizard?: SageWizard;
  wins: number;
  losses: number;
  streak: number;
  lastMine: number;
  miningPower: number;
}

export interface Battle {
  id: string;
  player1: string;
  player2: string;
  rune1: Rune;
  rune2: Rune;
  stake: number;
  winner?: string;
  status: 'pending' | 'active' | 'completed';
  zkProof?: string;
  timestamp: number;
}

export interface GenerationRequest {
  element: RuneElement;
  prompt?: string;
  lora?: string;
  cost: number;
}

export const ELEMENT_COLORS: Record<RuneElement, string> = {
  fire: '#ff4500',
  water: '#00bfff',
  earth: '#8b4513',
  air: '#e0e0e0',
  void: '#4b0082',
  light: '#ffd700',
};

export const RARITY_COLORS: Record<Rune['rarity'], string> = {
  common: '#9e9e9e',
  rare: '#2196f3',
  epic: '#9c27b0',
  legendary: '#ff9800',
  mythic: '#e91e63',
};

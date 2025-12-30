// Daily Challenge System
// 3 random challenges that reset at midnight

import type { RuneElement } from './runes';
import { ELEMENT_SYMBOLS } from './runes';

export type ChallengeType = 'catch' | 'battle' | 'minigame' | 'element' | 'perfect';

export interface DailyChallenge {
  id: string;
  type: ChallengeType;
  description: string;
  target: number;
  current: number;
  element?: RuneElement;
  reward: number; // SAGE
  completed: boolean;
  claimed: boolean;
}

interface ChallengeTemplate {
  type: ChallengeType;
  descriptions: string[];
  targets: number[];
  rewards: number[];
  elements?: RuneElement[];
}

// Challenge templates for generation
const CHALLENGE_TEMPLATES: ChallengeTemplate[] = [
  {
    type: 'catch',
    descriptions: ['Catch {n} rune', 'Catch {n} runes', 'Capture {n} wild runes'],
    targets: [1, 2, 3],
    rewards: [25, 40, 60],
  },
  {
    type: 'battle',
    descriptions: ['Win {n} battle', 'Win {n} battles', 'Achieve {n} victories'],
    targets: [1, 2],
    rewards: [35, 55],
  },
  {
    type: 'minigame',
    descriptions: ['Complete {n} mini-games', 'Play {n} mini-games', 'Finish {n} catch games'],
    targets: [2, 3, 4],
    rewards: [20, 30, 45],
  },
  {
    type: 'element',
    descriptions: ['Catch a {e} rune', 'Capture a wild {e} rune'],
    targets: [1],
    rewards: [35, 40],
    elements: ['fire', 'water', 'earth', 'air', 'light', 'void'],
  },
  {
    type: 'perfect',
    descriptions: ['Score 85+ on a mini-game', 'Get a high score (85+)', 'Master a mini-game (85+)'],
    targets: [1],
    rewards: [50],
  },
];

// Streak milestone rewards
export const STREAK_REWARDS: Record<number, { sage: number; energy?: number; description: string }> = {
  1: { sage: 10, description: 'First step!' },
  3: { sage: 50, energy: 2, description: '3-day streak!' },
  5: { sage: 100, description: '5-day streak!' },
  7: { sage: 250, energy: 3, description: 'Weekly champion!' },
};

// Generate a random challenge
function generateChallenge(usedTypes: Set<ChallengeType>): DailyChallenge {
  // Filter out already used types (for variety)
  let availableTemplates = CHALLENGE_TEMPLATES.filter(t => !usedTypes.has(t.type));
  if (availableTemplates.length === 0) {
    availableTemplates = CHALLENGE_TEMPLATES;
  }

  const template = availableTemplates[Math.floor(Math.random() * availableTemplates.length)];
  const targetIndex = Math.floor(Math.random() * template.targets.length);
  const target = template.targets[targetIndex];
  const reward = template.rewards[Math.min(targetIndex, template.rewards.length - 1)];

  let description = template.descriptions[Math.floor(Math.random() * template.descriptions.length)];
  description = description.replace('{n}', target.toString());

  let element: RuneElement | undefined;
  if (template.elements) {
    element = template.elements[Math.floor(Math.random() * template.elements.length)];
    description = description.replace('{e}', element.charAt(0).toUpperCase() + element.slice(1));
  }

  usedTypes.add(template.type);

  return {
    id: `challenge_${Date.now()}_${Math.random().toString(36).substr(2, 6)}`,
    type: template.type,
    description,
    target,
    current: 0,
    element,
    reward,
    completed: false,
    claimed: false,
  };
}

// Generate 3 daily challenges
export function generateDailyChallenges(): DailyChallenge[] {
  const usedTypes = new Set<ChallengeType>();
  return [
    generateChallenge(usedTypes),
    generateChallenge(usedTypes),
    generateChallenge(usedTypes),
  ];
}

// Get challenge icon
export function getChallengeIcon(challenge: DailyChallenge): string {
  switch (challenge.type) {
    case 'catch':
      return '🎯';
    case 'battle':
      return '⚔️';
    case 'minigame':
      return '🎮';
    case 'element':
      return challenge.element ? ELEMENT_SYMBOLS[challenge.element] : '🔮';
    case 'perfect':
      return '⭐';
    default:
      return '📋';
  }
}

// Check if all challenges are completed
export function allChallengesCompleted(challenges: DailyChallenge[]): boolean {
  return challenges.every(c => c.completed);
}

// Check if all challenges are claimed
export function allChallengesClaimed(challenges: DailyChallenge[]): boolean {
  return challenges.every(c => c.claimed);
}

// Calculate total unclaimed rewards
export function getUnclaimedRewards(challenges: DailyChallenge[]): number {
  return challenges
    .filter(c => c.completed && !c.claimed)
    .reduce((sum, c) => sum + c.reward, 0);
}

// Get the next streak milestone
export function getNextStreakMilestone(currentStreak: number): number | null {
  const milestones = [1, 3, 5, 7];
  return milestones.find(m => m > currentStreak) || null;
}

// Check if streak milestone was just reached
export function getStreakReward(streak: number): typeof STREAK_REWARDS[number] | null {
  return STREAK_REWARDS[streak] || null;
}

// Calculate time until daily reset (midnight local)
export function getTimeUntilDailyReset(): { hours: number; minutes: number } {
  const now = new Date();
  const midnight = new Date(now);
  midnight.setDate(midnight.getDate() + 1);
  midnight.setHours(0, 0, 0, 0);

  const diff = midnight.getTime() - now.getTime();
  const hours = Math.floor(diff / (1000 * 60 * 60));
  const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));

  return { hours, minutes };
}

// Format time remaining
export function formatTimeRemaining(hours: number, minutes: number): string {
  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }
  return `${minutes}m`;
}

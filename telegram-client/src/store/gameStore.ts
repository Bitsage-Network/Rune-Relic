// Rune Relic Game State - Pokemon-style Collection Game

import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import {
  RUNE_SPECIES,
  createOwnedRune,
  getSpecies,
  rollRarity,
  getElementMultiplier
} from '../data/runes';
import type { OwnedRune, RuneElement } from '../data/runes';
import { getTodaysBoss, generateBossTeam, generateBossReward } from '../data/bosses';
import type { Boss } from '../data/bosses';
import { generateDailyChallenges, allChallengesCompleted, getStreakReward } from '../data/challenges';
import type { DailyChallenge, ChallengeType } from '../data/challenges';

const MAX_ENERGY = 5;
const ENERGY_REGEN_MS = 10 * 60 * 1000; // 10 minutes

interface EncounterCard {
  speciesId: number;
  catchDifficulty: number; // 0-1, lower is easier
}

interface BattleState {
  phase: 'select' | 'reveal' | 'result';
  playerTeam: string[]; // rune ids
  enemyTeam: OwnedRune[];
  currentRound: number;
  playerScore: number;
  enemyScore: number;
  roundResults: Array<{
    playerRune: OwnedRune;
    enemyRune: OwnedRune;
    winner: 'player' | 'enemy' | 'tie';
  }>;
}

interface GameState {
  // Player
  username: string;
  sage: number;

  // Collection
  ownedRunes: OwnedRune[];
  seenSpecies: number[]; // species IDs seen (silhouettes)
  caughtSpecies: number[]; // species IDs caught at least once

  // Energy system
  energy: number;
  lastEnergyRegen: number;

  // Encounters
  currentEncounters: EncounterCard[] | null;
  catchingRune: EncounterCard | null;

  // Battle
  battle: BattleState | null;
  wins: number;
  losses: number;

  // Boss Encounters
  bossEnergy: number; // 0 or 1
  lastBossReset: number;
  bossesDefeated: string[]; // boss IDs defeated at least once
  currentBossFight: Boss | null;
  bossBattleTeam: OwnedRune[] | null;

  // Daily Challenges
  dailyChallenges: DailyChallenge[];
  lastDailyReset: number;
  dailyStreak: number;
  streakRewardClaimed: boolean;

  // UI
  currentPage: 'home' | 'dex' | 'encounter' | 'battle' | 'lab' | 'collection' | 'boss';

  // Actions
  setUsername: (name: string) => void;
  setPage: (page: GameState['currentPage']) => void;

  // Energy
  regenerateEnergy: () => void;
  spendEnergy: () => boolean;

  // SAGE
  addSage: (amount: number) => void;
  spendSage: (amount: number) => boolean;

  // Encounters
  startEncounter: () => EncounterCard[] | null;
  selectEncounter: (card: EncounterCard) => void;
  catchRune: (success: boolean) => OwnedRune | null;
  clearEncounter: () => void;

  // Collection
  getRune: (id: string) => OwnedRune | undefined;
  releaseRune: (id: string) => void;

  // Battle
  startBattle: (teamIds: string[]) => void;
  resolveBattle: () => void;
  endBattle: () => void;

  // Fusion
  fuseRunes: (rune1Id: string, rune2Id: string) => OwnedRune | null;

  // Boss Encounters
  checkDailyReset: () => void;
  startBossFight: (teamIds: string[]) => boolean;
  completeBossFight: (won: boolean) => OwnedRune | null;
  cancelBossFight: () => void;

  // Daily Challenges
  updateChallengeProgress: (type: ChallengeType, amount: number, element?: RuneElement) => void;
  claimChallengeReward: (challengeId: string) => number;
  claimAllChallengeRewards: () => number;
  claimStreakReward: () => { sage: number; energy?: number } | null;
}

export const useGameStore = create<GameState>()(
  persist(
    (set, get) => ({
      // Initial state
      username: 'Seeker',
      sage: 100,
      ownedRunes: [],
      seenSpecies: [],
      caughtSpecies: [],
      energy: MAX_ENERGY,
      lastEnergyRegen: Date.now(),
      currentEncounters: null,
      catchingRune: null,
      battle: null,
      wins: 0,
      losses: 0,
      currentPage: 'home',

      // Boss state
      bossEnergy: 1,
      lastBossReset: Date.now(),
      bossesDefeated: [],
      currentBossFight: null,
      bossBattleTeam: null,

      // Daily challenges state
      dailyChallenges: generateDailyChallenges(),
      lastDailyReset: Date.now(),
      dailyStreak: 0,
      streakRewardClaimed: false,

      setUsername: (name) => set({ username: name }),
      setPage: (page) => set({ currentPage: page }),

      // Energy regeneration
      regenerateEnergy: () => {
        const state = get();
        const now = Date.now();
        const elapsed = now - state.lastEnergyRegen;
        const regenCount = Math.floor(elapsed / ENERGY_REGEN_MS);

        if (regenCount > 0 && state.energy < MAX_ENERGY) {
          const newEnergy = Math.min(MAX_ENERGY, state.energy + regenCount);
          set({
            energy: newEnergy,
            lastEnergyRegen: now - (elapsed % ENERGY_REGEN_MS)
          });
        }
      },

      spendEnergy: () => {
        const state = get();
        state.regenerateEnergy();

        if (state.energy > 0) {
          set({ energy: state.energy - 1 });
          return true;
        }
        return false;
      },

      // SAGE
      addSage: (amount) => set((s) => ({ sage: s.sage + amount })),

      spendSage: (amount) => {
        const state = get();
        if (state.sage >= amount) {
          set({ sage: state.sage - amount });
          return true;
        }
        return false;
      },

      // Encounters - the catch loop!
      startEncounter: () => {
        const state = get();

        if (!state.spendEnergy()) {
          return null;
        }

        // Generate 3 random encounters
        // Weighted towards base forms, rarer for evolved
        const encounters: EncounterCard[] = [];
        const baseSpecies = RUNE_SPECIES.filter(s => !s.evolvesFrom);
        const evolvedSpecies = RUNE_SPECIES.filter(s => s.evolvesFrom);

        for (let i = 0; i < 3; i++) {
          // 70% base, 30% evolved
          const pool = Math.random() < 0.7 ? baseSpecies : evolvedSpecies;
          const species = pool[Math.floor(Math.random() * pool.length)];

          encounters.push({
            speciesId: species.id,
            catchDifficulty: species.evolvesFrom ? 0.6 : 0.4, // evolved harder
          });
        }

        // Mark as seen
        const newSeen = [...new Set([...state.seenSpecies, ...encounters.map(e => e.speciesId)])];

        set({ currentEncounters: encounters, seenSpecies: newSeen });
        return encounters;
      },

      selectEncounter: (card) => {
        set({ catchingRune: card, currentEncounters: null });
      },

      catchRune: (success) => {
        const state = get();
        if (!state.catchingRune) return null;

        // Always count mini-game completion for challenges
        state.updateChallengeProgress('minigame', 1);

        if (!success) {
          set({ catchingRune: null });
          return null;
        }

        // Create the rune!
        const rarity = rollRarity();
        const newRune = createOwnedRune(state.catchingRune.speciesId, rarity);
        const species = getSpecies(state.catchingRune.speciesId);

        // Update caught species
        const newCaught = [...new Set([...state.caughtSpecies, state.catchingRune.speciesId])];

        set({
          ownedRunes: [...state.ownedRunes, newRune],
          caughtSpecies: newCaught,
          catchingRune: null,
        });

        // Update challenge progress
        state.updateChallengeProgress('catch', 1);
        if (species) {
          state.updateChallengeProgress('element', 1, species.element);
        }

        return newRune;
      },

      clearEncounter: () => set({ currentEncounters: null, catchingRune: null }),

      // Collection
      getRune: (id) => get().ownedRunes.find(r => r.id === id),

      releaseRune: (id) => {
        const state = get();
        const rune = state.getRune(id);
        if (rune) {
          // Give some essence back
          const essenceReward = { common: 5, rare: 15, epic: 40, legendary: 100 }[rune.rarity];
          set({
            ownedRunes: state.ownedRunes.filter(r => r.id !== id),
            sage: state.sage + essenceReward,
          });
        }
      },

      // Battle system
      startBattle: (teamIds) => {
        const state = get();
        const playerTeam = teamIds.filter(id => state.getRune(id));

        if (playerTeam.length !== 3) return;

        // Generate enemy team (random runes at similar power)
        const avgPower = playerTeam.reduce((sum, id) => {
          const rune = state.getRune(id);
          return sum + (rune?.stats.power || 50);
        }, 0) / 3;

        const enemyTeam: OwnedRune[] = [];
        for (let i = 0; i < 3; i++) {
          const species = RUNE_SPECIES[Math.floor(Math.random() * RUNE_SPECIES.length)];
          const enemy = createOwnedRune(species.id, rollRarity());
          // Adjust to be somewhat balanced
          const scaleFactor = avgPower / enemy.stats.power;
          enemy.stats.power = Math.floor(enemy.stats.power * (0.8 + scaleFactor * 0.4));
          enemyTeam.push(enemy);
        }

        set({
          battle: {
            phase: 'select',
            playerTeam,
            enemyTeam,
            currentRound: 0,
            playerScore: 0,
            enemyScore: 0,
            roundResults: [],
          }
        });
      },

      resolveBattle: () => {
        const state = get();
        if (!state.battle) return;

        const results: BattleState['roundResults'] = [];
        let playerScore = 0;
        let enemyScore = 0;

        // Resolve all 3 rounds
        for (let i = 0; i < 3; i++) {
          const playerRune = state.getRune(state.battle.playerTeam[i]);
          const enemyRune = state.battle.enemyTeam[i];

          if (!playerRune) continue;

          // Simple combat: compare power with element bonus
          const playerSpecies = getSpecies(playerRune.speciesId);
          const enemySpecies = getSpecies(enemyRune.speciesId);

          if (!playerSpecies || !enemySpecies) continue;

          // Element multiplier
          const playerMult = getElementMultiplier(playerSpecies.element, enemySpecies.element);
          const enemyMult = getElementMultiplier(enemySpecies.element, playerSpecies.element);

          const playerPower = playerRune.stats.power * playerMult;
          const enemyPower = enemyRune.stats.power * enemyMult;

          // Add some randomness (±10%)
          const playerFinal = playerPower * (0.9 + Math.random() * 0.2);
          const enemyFinal = enemyPower * (0.9 + Math.random() * 0.2);

          let winner: 'player' | 'enemy' | 'tie';
          if (Math.abs(playerFinal - enemyFinal) < 5) {
            winner = 'tie';
          } else if (playerFinal > enemyFinal) {
            winner = 'player';
            playerScore++;
          } else {
            winner = 'enemy';
            enemyScore++;
          }

          results.push({ playerRune, enemyRune, winner });
        }

        set({
          battle: {
            ...state.battle,
            phase: 'result',
            playerScore,
            enemyScore,
            roundResults: results,
          }
        });
      },

      endBattle: () => {
        const state = get();
        if (!state.battle) return;

        const won = state.battle.playerScore > state.battle.enemyScore;

        // Rewards
        if (won) {
          set({
            wins: state.wins + 1,
            sage: state.sage + 25,
          });
          // Increment wins on used runes
          const updatedRunes = state.ownedRunes.map(r => {
            if (state.battle!.playerTeam.includes(r.id)) {
              return { ...r, wins: r.wins + 1 };
            }
            return r;
          });
          set({ ownedRunes: updatedRunes });

          // Update challenge progress for battle wins
          state.updateChallengeProgress('battle', 1);
        } else {
          set({
            losses: state.losses + 1,
            sage: state.sage + 5, // consolation
          });
        }

        set({ battle: null });
      },

      // Fusion
      fuseRunes: (rune1Id, rune2Id) => {
        const state = get();
        const rune1 = state.getRune(rune1Id);
        const rune2 = state.getRune(rune2Id);

        if (!rune1 || !rune2) return null;
        if (!state.spendSage(50)) return null; // Fusion costs SAGE

        const species1 = getSpecies(rune1.speciesId);
        const species2 = getSpecies(rune2.speciesId);

        if (!species1 || !species2) return null;

        let newSpeciesId: number;

        // Same species + has evolution = evolve
        if (rune1.speciesId === rune2.speciesId && species1.evolvesTo) {
          newSpeciesId = species1.evolvesTo;
        }
        // Same element = random of that element (possibly evolved)
        else if (species1.element === species2.element) {
          const sameElement = RUNE_SPECIES.filter(s => s.element === species1.element);
          newSpeciesId = sameElement[Math.floor(Math.random() * sameElement.length)].id;
        }
        // Different elements = random arcane or either element
        else {
          const options = RUNE_SPECIES.filter(s =>
            s.element === species1.element ||
            s.element === species2.element ||
            s.element === 'arcane'
          );
          newSpeciesId = options[Math.floor(Math.random() * options.length)].id;
        }

        // Better rarity chance from fusion
        const fusedRarity = Math.random() < 0.3 ? 'epic' : Math.random() < 0.6 ? 'rare' : 'common';
        const newRune = createOwnedRune(newSpeciesId, fusedRarity as any);

        // Remove used runes, add new one
        set({
          ownedRunes: [
            ...state.ownedRunes.filter(r => r.id !== rune1Id && r.id !== rune2Id),
            newRune
          ],
          caughtSpecies: [...new Set([...state.caughtSpecies, newSpeciesId])],
        });

        return newRune;
      },

      // ==================== BOSS ENCOUNTERS ====================

      checkDailyReset: () => {
        const state = get();
        const now = new Date();
        const lastReset = new Date(state.lastDailyReset);

        // Check if it's a new day
        if (now.toDateString() !== lastReset.toDateString()) {
          // Check if all challenges were completed yesterday
          const allComplete = allChallengesCompleted(state.dailyChallenges);

          set({
            // Reset boss energy
            bossEnergy: 1,
            lastBossReset: now.getTime(),

            // Reset challenges
            dailyChallenges: generateDailyChallenges(),
            lastDailyReset: now.getTime(),

            // Update streak
            dailyStreak: allComplete ? state.dailyStreak + 1 : 0,
            streakRewardClaimed: false,

            // Clear any ongoing boss fight
            currentBossFight: null,
            bossBattleTeam: null,
          });
        }
      },

      startBossFight: (teamIds) => {
        const state = get();
        state.checkDailyReset();

        if (state.bossEnergy < 1) return false;
        if (teamIds.length !== 3) return false;

        const playerTeam = teamIds.map(id => state.getRune(id)).filter(Boolean) as OwnedRune[];
        if (playerTeam.length !== 3) return false;

        const boss = getTodaysBoss();
        const bossTeam = generateBossTeam(boss);

        set({
          bossEnergy: 0,
          currentBossFight: boss,
          bossBattleTeam: bossTeam,
        });

        return true;
      },

      completeBossFight: (won) => {
        const state = get();
        if (!state.currentBossFight) return null;

        const boss = state.currentBossFight;
        let rewardRune: OwnedRune | null = null;

        if (won) {
          // Add SAGE reward
          const sageReward = boss.rewards.sage + (boss.rewards.bonusSage || 0);
          const isFirstClear = !state.bossesDefeated.includes(boss.id);
          const firstClearBonus = isFirstClear ? 50 : 0;

          // Generate reward rune
          rewardRune = generateBossReward(boss);

          set({
            sage: state.sage + sageReward + firstClearBonus,
            bossesDefeated: isFirstClear
              ? [...state.bossesDefeated, boss.id]
              : state.bossesDefeated,
            ownedRunes: rewardRune
              ? [...state.ownedRunes, rewardRune]
              : state.ownedRunes,
            caughtSpecies: rewardRune
              ? [...new Set([...state.caughtSpecies, rewardRune.speciesId])]
              : state.caughtSpecies,
            currentBossFight: null,
            bossBattleTeam: null,
          });

          // Update challenge progress
          state.updateChallengeProgress('battle', 1);
        } else {
          // Consolation reward
          set({
            sage: state.sage + 20,
            currentBossFight: null,
            bossBattleTeam: null,
          });
        }

        return rewardRune;
      },

      cancelBossFight: () => {
        set({
          currentBossFight: null,
          bossBattleTeam: null,
        });
      },

      // ==================== DAILY CHALLENGES ====================

      updateChallengeProgress: (type, amount, element) => {
        const state = get();
        state.checkDailyReset();

        const updatedChallenges = state.dailyChallenges.map(challenge => {
          if (challenge.completed) return challenge;

          let matches = false;

          if (challenge.type === type) {
            if (type === 'element' && element) {
              matches = challenge.element === element;
            } else {
              matches = true;
            }
          }

          if (matches) {
            const newCurrent = Math.min(challenge.current + amount, challenge.target);
            return {
              ...challenge,
              current: newCurrent,
              completed: newCurrent >= challenge.target,
            };
          }

          return challenge;
        });

        set({ dailyChallenges: updatedChallenges });
      },

      claimChallengeReward: (challengeId) => {
        const state = get();
        const challenge = state.dailyChallenges.find(c => c.id === challengeId);

        if (!challenge || !challenge.completed || challenge.claimed) return 0;

        const updatedChallenges = state.dailyChallenges.map(c =>
          c.id === challengeId ? { ...c, claimed: true } : c
        );

        set({
          dailyChallenges: updatedChallenges,
          sage: state.sage + challenge.reward,
        });

        return challenge.reward;
      },

      claimAllChallengeRewards: () => {
        const state = get();
        let totalReward = 0;

        const updatedChallenges = state.dailyChallenges.map(challenge => {
          if (challenge.completed && !challenge.claimed) {
            totalReward += challenge.reward;
            return { ...challenge, claimed: true };
          }
          return challenge;
        });

        if (totalReward > 0) {
          set({
            dailyChallenges: updatedChallenges,
            sage: state.sage + totalReward,
          });
        }

        return totalReward;
      },

      claimStreakReward: () => {
        const state = get();

        if (state.streakRewardClaimed) return null;
        if (!allChallengesCompleted(state.dailyChallenges)) return null;

        const reward = getStreakReward(state.dailyStreak + 1); // +1 because streak increments on next day
        if (!reward) return null;

        set({
          streakRewardClaimed: true,
          sage: state.sage + reward.sage,
          energy: reward.energy ? Math.min(MAX_ENERGY, state.energy + reward.energy) : state.energy,
        });

        return reward;
      },
    }),
    {
      name: 'rune-relic-v2',
      partialize: (state) => ({
        username: state.username,
        sage: state.sage,
        ownedRunes: state.ownedRunes,
        seenSpecies: state.seenSpecies,
        caughtSpecies: state.caughtSpecies,
        energy: state.energy,
        lastEnergyRegen: state.lastEnergyRegen,
        wins: state.wins,
        losses: state.losses,
        // Boss state
        bossEnergy: state.bossEnergy,
        lastBossReset: state.lastBossReset,
        bossesDefeated: state.bossesDefeated,
        // Daily challenges
        dailyChallenges: state.dailyChallenges,
        lastDailyReset: state.lastDailyReset,
        dailyStreak: state.dailyStreak,
        streakRewardClaimed: state.streakRewardClaimed,
      }),
    }
  )
);

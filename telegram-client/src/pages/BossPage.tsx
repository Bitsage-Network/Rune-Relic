// Boss Encounter Page - Daily boss battles with special rewards

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Skull, Swords, Clock, Trophy, Zap, Check, X, Star } from 'lucide-react';
import { useGameStore } from '../store/gameStore';
import { getTodaysBoss, getTimeUntilBossReset, getBossColor, getBossSymbol } from '../data/bosses';
import { getSpecies, ELEMENT_COLORS, ELEMENT_SYMBOLS, RARITY_COLORS } from '../data/runes';
import type { OwnedRune } from '../data/runes';

type Phase = 'preview' | 'team-select' | 'battle' | 'result';

export function BossPage() {
  const {
    ownedRunes,
    bossEnergy,
    bossesDefeated,
    currentBossFight,
    bossBattleTeam,
    startBossFight,
    completeBossFight,
    cancelBossFight,
    checkDailyReset,
  } = useGameStore();

  const [phase, setPhase] = useState<Phase>(currentBossFight ? 'battle' : 'preview');
  const [selectedTeam, setSelectedTeam] = useState<string[]>([]);
  const [battleResult, setBattleResult] = useState<'victory' | 'defeat' | null>(null);
  const [rewardRune, setRewardRune] = useState<OwnedRune | null>(null);
  const [timeUntilReset, setTimeUntilReset] = useState(getTimeUntilBossReset());

  const boss = getTodaysBoss();
  const bossColor = getBossColor(boss);
  const bossSymbol = getBossSymbol(boss);
  const isDefeated = bossesDefeated.includes(boss.id);

  // Check daily reset on mount
  useEffect(() => {
    checkDailyReset();
  }, [checkDailyReset]);

  // Update countdown timer
  useEffect(() => {
    const timer = setInterval(() => {
      setTimeUntilReset(getTimeUntilBossReset());
    }, 60000); // Update every minute

    return () => clearInterval(timer);
  }, []);

  const handleStartBattle = () => {
    if (selectedTeam.length !== 3) return;

    const success = startBossFight(selectedTeam);
    if (success) {
      setPhase('battle');
      // Simulate battle (in a real game, this would be the full battle system)
      simulateBattle();
    }
  };

  const simulateBattle = () => {
    // Simple simulation - compare total power
    const playerPower = selectedTeam.reduce((sum, id) => {
      const rune = ownedRunes.find(r => r.id === id);
      return sum + (rune?.stats.power || 0);
    }, 0);

    const bossPower = bossBattleTeam?.reduce((sum, r) => sum + r.stats.power, 0) || 300;

    // Player has ~40% base win rate, modified by power difference
    const winChance = 0.4 + ((playerPower - bossPower) / bossPower) * 0.3;
    const won = Math.random() < winChance;

    setTimeout(() => {
      setBattleResult(won ? 'victory' : 'defeat');
      const reward = completeBossFight(won);
      if (reward) {
        setRewardRune(reward);
      }
      setPhase('result');
    }, 2000);
  };

  const handleBack = () => {
    setPhase('preview');
    setSelectedTeam([]);
    setBattleResult(null);
    setRewardRune(null);
    cancelBossFight();
  };

  const toggleRune = (runeId: string) => {
    if (selectedTeam.includes(runeId)) {
      setSelectedTeam(selectedTeam.filter(id => id !== runeId));
    } else if (selectedTeam.length < 3) {
      setSelectedTeam([...selectedTeam, runeId]);
    }
  };

  return (
    <div className="page boss-page">
      <h2 className="page-title">
        <Skull size={24} />
        Boss Battle
      </h2>

      <AnimatePresence mode="wait">
        {/* PREVIEW - Show today's boss */}
        {phase === 'preview' && (
          <motion.div
            key="preview"
            className="boss-preview"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            {/* Timer */}
            <div className="boss-timer">
              <Clock size={16} />
              <span>Resets in {timeUntilReset.hours}h {timeUntilReset.minutes}m</span>
            </div>

            {/* Boss Card */}
            <motion.div
              className="boss-card"
              style={{
                borderColor: bossColor,
                boxShadow: `0 0 30px ${bossColor}40`,
              }}
              initial={{ scale: 0.9, y: 20 }}
              animate={{ scale: 1, y: 0 }}
            >
              <div className="boss-element" style={{ color: bossColor }}>
                {bossSymbol}
              </div>
              <h3 className="boss-name">{boss.name}</h3>
              <p className="boss-title">{boss.title}</p>
              <p className="boss-description">{boss.description}</p>

              <div className="boss-difficulty">
                {[1, 2, 3].map(i => (
                  <Star
                    key={i}
                    size={16}
                    fill={i <= (boss.statMultiplier > 1.5 ? 3 : 2) ? bossColor : 'transparent'}
                    color={bossColor}
                  />
                ))}
              </div>

              {isDefeated && (
                <div className="boss-defeated-badge">
                  <Trophy size={14} /> Defeated
                </div>
              )}
            </motion.div>

            {/* Rewards Preview */}
            <div className="boss-rewards">
              <h4>Rewards</h4>
              <div className="reward-list">
                <span className="reward-item sage">
                  +{boss.rewards.sage} SAGE
                </span>
                <span className="reward-item variant">
                  {boss.rewards.variantChance}% {boss.rewards.variant} chance
                </span>
                {boss.rewards.guaranteedRarity && (
                  <span className="reward-item rarity" style={{ color: RARITY_COLORS[boss.rewards.guaranteedRarity] }}>
                    Guaranteed {boss.rewards.guaranteedRarity}
                  </span>
                )}
                {!isDefeated && (
                  <span className="reward-item first-clear">
                    +50 SAGE (First Clear)
                  </span>
                )}
              </div>
            </div>

            {/* Boss Energy */}
            <div className="boss-energy">
              <Zap size={20} color={bossEnergy > 0 ? '#ffd700' : '#666'} />
              <span>{bossEnergy}/1 Boss Energy</span>
            </div>

            {/* Challenge Button */}
            <motion.button
              className="challenge-boss-btn"
              style={{ background: bossEnergy > 0 ? `linear-gradient(135deg, ${bossColor}, ${bossColor}cc)` : undefined }}
              onClick={() => setPhase('team-select')}
              disabled={bossEnergy < 1 || ownedRunes.length < 3}
              whileTap={{ scale: 0.95 }}
            >
              <Swords size={20} />
              {bossEnergy > 0 ? 'Challenge Boss' : 'No Energy (Try Tomorrow)'}
            </motion.button>

            {ownedRunes.length < 3 && (
              <p className="boss-hint">You need at least 3 runes to challenge the boss</p>
            )}
          </motion.div>
        )}

        {/* TEAM SELECT */}
        {phase === 'team-select' && (
          <motion.div
            key="team-select"
            className="boss-team-select"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <p className="instruction">Select 3 runes to battle {boss.name}</p>

            {/* Selected Team */}
            <div className="selected-team">
              {[0, 1, 2].map(i => {
                const runeId = selectedTeam[i];
                const rune = runeId ? ownedRunes.find(r => r.id === runeId) : null;
                const species = rune ? getSpecies(rune.speciesId) : null;

                return (
                  <motion.div
                    key={i}
                    className={`team-slot ${rune ? 'filled' : ''}`}
                    style={species ? { borderColor: ELEMENT_COLORS[species.element] } : undefined}
                    onClick={() => runeId && toggleRune(runeId)}
                    whileTap={{ scale: 0.95 }}
                  >
                    {species ? (
                      <>
                        <span style={{ color: ELEMENT_COLORS[species.element] }}>
                          {ELEMENT_SYMBOLS[species.element]}
                        </span>
                        <small>{species.name}</small>
                      </>
                    ) : (
                      <span>?</span>
                    )}
                  </motion.div>
                );
              })}
            </div>

            {/* Rune Selection Grid */}
            <div className="rune-select-grid">
              {ownedRunes.map(rune => {
                const species = getSpecies(rune.speciesId);
                if (!species) return null;
                const isSelected = selectedTeam.includes(rune.id);

                return (
                  <motion.button
                    key={rune.id}
                    className={`rune-select-card ${isSelected ? 'selected' : ''}`}
                    style={{ borderColor: ELEMENT_COLORS[species.element] }}
                    onClick={() => toggleRune(rune.id)}
                    whileTap={{ scale: 0.95 }}
                  >
                    <div style={{ color: ELEMENT_COLORS[species.element] }}>
                      {ELEMENT_SYMBOLS[species.element]}
                    </div>
                    <span className="rune-select-name">{species.name}</span>
                    <span className="rune-select-power">{rune.stats.power}</span>
                    {isSelected && <Check size={14} className="check-icon" />}
                  </motion.button>
                );
              })}
            </div>

            {/* Action Buttons */}
            <motion.button
              className="challenge-boss-btn"
              style={{ background: `linear-gradient(135deg, ${bossColor}, ${bossColor}cc)` }}
              onClick={handleStartBattle}
              disabled={selectedTeam.length !== 3}
              whileTap={{ scale: 0.95 }}
            >
              <Swords size={20} />
              Fight!
            </motion.button>

            <button className="cancel-btn" onClick={handleBack}>
              <X size={16} /> Cancel
            </button>
          </motion.div>
        )}

        {/* BATTLE */}
        {phase === 'battle' && (
          <motion.div
            key="battle"
            className="boss-battle"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <div className="battle-animation">
              <motion.div
                className="boss-fight-icon"
                animate={{
                  scale: [1, 1.2, 1],
                  rotate: [0, 5, -5, 0],
                }}
                transition={{ duration: 0.5, repeat: Infinity }}
              >
                <Swords size={64} color={bossColor} />
              </motion.div>
              <h3>Fighting {boss.name}...</h3>
              <p>Your team vs the {boss.element} boss</p>
            </div>
          </motion.div>
        )}

        {/* RESULT */}
        {phase === 'result' && battleResult && (
          <motion.div
            key="result"
            className={`boss-result ${battleResult}`}
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
          >
            {battleResult === 'victory' ? (
              <>
                <motion.div
                  initial={{ scale: 0 }}
                  animate={{ scale: [0, 1.3, 1] }}
                  transition={{ duration: 0.5 }}
                >
                  <Trophy size={64} className="trophy-icon" />
                </motion.div>
                <h2>Victory!</h2>
                <p>You defeated {boss.name}!</p>

                <div className="boss-rewards-earned">
                  <span className="reward-sage">+{boss.rewards.sage + (boss.rewards.bonusSage || 0)} SAGE</span>
                  {!bossesDefeated.includes(boss.id) && (
                    <span className="reward-first">+50 First Clear Bonus!</span>
                  )}
                </div>

                {rewardRune && (() => {
                  const species = getSpecies(rewardRune.speciesId);
                  if (!species) return null;

                  return (
                    <motion.div
                      className="reward-rune-card"
                      style={{
                        borderColor: ELEMENT_COLORS[species.element],
                        boxShadow: `0 0 20px ${ELEMENT_COLORS[species.element]}60`,
                      }}
                      initial={{ y: 20, opacity: 0 }}
                      animate={{ y: 0, opacity: 1 }}
                      transition={{ delay: 0.3 }}
                    >
                      <div className="reward-rarity" style={{ color: RARITY_COLORS[rewardRune.rarity] }}>
                        {rewardRune.rarity.toUpperCase()}
                        {rewardRune.variant !== 'normal' && ` (${rewardRune.variant})`}
                      </div>
                      <div className="reward-symbol" style={{ color: ELEMENT_COLORS[species.element] }}>
                        {ELEMENT_SYMBOLS[species.element]}
                      </div>
                      <div className="reward-name">{species.name}</div>
                    </motion.div>
                  );
                })()}
              </>
            ) : (
              <>
                <motion.div
                  initial={{ scale: 0 }}
                  animate={{ scale: 1 }}
                >
                  <X size={64} className="defeat-icon" />
                </motion.div>
                <h2>Defeated...</h2>
                <p>{boss.name} was too strong!</p>
                <p className="consolation">+20 SAGE (Consolation)</p>
              </>
            )}

            <motion.button
              className="done-btn"
              onClick={handleBack}
              whileTap={{ scale: 0.95 }}
            >
              Continue
            </motion.button>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

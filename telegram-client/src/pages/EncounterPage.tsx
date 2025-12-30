// Encounter Page - Find and catch runes with mini-games!

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Zap, X, Check } from 'lucide-react';
import { useGameStore } from '../store/gameStore';
import { getSpecies, ELEMENT_COLORS, ELEMENT_SYMBOLS, RARITY_COLORS } from '../data/runes';
import {
  GameSelector,
  RuneTracing,
  ElementClash,
  MemoryMatch,
  ForgeSequence,
} from '../components/minigames';
import type { MiniGameType, MiniGameResult } from '../components/minigames';
import type { OwnedRune } from '../data/runes';

type Phase = 'idle' | 'encounters' | 'select-game' | 'playing' | 'result';

export function EncounterPage() {
  const {
    energy, currentEncounters, catchingRune,
    startEncounter, selectEncounter, catchRune, clearEncounter
  } = useGameStore();

  const [phase, setPhase] = useState<Phase>(
    currentEncounters ? 'encounters' : catchingRune ? 'select-game' : 'idle'
  );
  const [selectedGame, setSelectedGame] = useState<MiniGameType | null>(null);
  const [catchResult, setCatchResult] = useState<OwnedRune | null>(null);
  const [bonusResult, setBonusResult] = useState<MiniGameResult['bonus'] | null>(null);
  const [catchFailed, setCatchFailed] = useState(false);

  // Sync phase with store state (only for external state changes)
  useEffect(() => {
    if (currentEncounters) {
      setPhase('encounters');
    } else if (catchingRune && !selectedGame) {
      setPhase('select-game');
    }
    // Don't auto-reset to idle - let the handlers manage that
  }, [currentEncounters, catchingRune, selectedGame]);

  const handleExplore = () => {
    const encounters = startEncounter();
    if (encounters) {
      setPhase('encounters');
    }
  };

  const handleSelectRune = (card: typeof currentEncounters extends (infer T)[] | null ? T : never) => {
    if (!card) return;
    selectEncounter(card);
    setPhase('select-game');
    setSelectedGame(null);
  };

  const handleSelectGame = (game: MiniGameType) => {
    setSelectedGame(game);
    setPhase('playing');
  };

  const handleGameComplete = (result: MiniGameResult) => {
    setBonusResult(result.bonus);

    if (result.success) {
      // Apply bonuses to catch
      const caught = catchRune(true);
      if (caught) {
        // Apply rarity boost if applicable
        if (result.bonus.rarityBoost > 0) {
          // Rarity was already determined, but we can show the bonus
        }
        setCatchResult(caught);
        setPhase('result');
      }
    } else {
      // Failed the mini-game
      catchRune(false);
      setCatchFailed(true);
      setTimeout(() => {
        setCatchFailed(false);
        setSelectedGame(null);
        setPhase('idle');
      }, 2000);
    }
  };

  const handleCancel = () => {
    clearEncounter();
    setSelectedGame(null);
    setCatchFailed(false);
    setPhase('idle');
  };

  const handleDone = () => {
    setCatchResult(null);
    setBonusResult(null);
    setSelectedGame(null);
    clearEncounter();
    setPhase('idle');
  };

  const catchingSpecies = catchingRune ? getSpecies(catchingRune.speciesId) : null;

  return (
    <div className="page encounter-page">
      <h2 className="page-title">Explore</h2>

      {/* Energy display */}
      <div className="energy-display">
        <Zap size={20} />
        <span>{energy} / 5 Energy</span>
      </div>

      <AnimatePresence mode="wait">
        {/* IDLE - Start exploring */}
        {phase === 'idle' && !catchFailed && (
          <motion.div
            key="idle"
            className="encounter-idle"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <div className="idle-visual">
              <motion.div
                className="explore-icon"
                animate={{ y: [0, -10, 0] }}
                transition={{ duration: 2, repeat: Infinity }}
              >
                🔮
              </motion.div>
            </div>
            <p>Spend energy to encounter wild runes!</p>
            <motion.button
              className="explore-btn"
              onClick={handleExplore}
              disabled={energy <= 0}
              whileTap={{ scale: 0.95 }}
            >
              <Zap size={24} />
              {energy > 0 ? 'Explore (1 Energy)' : 'No Energy'}
            </motion.button>

            {energy <= 0 && (
              <p className="energy-hint">Energy regenerates every 10 minutes</p>
            )}
          </motion.div>
        )}

        {/* ENCOUNTERS - Pick one of 3 */}
        {phase === 'encounters' && currentEncounters && (
          <motion.div
            key="encounters"
            className="encounter-cards"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -20 }}
          >
            <p>A wild rune appeared! Choose one:</p>
            <div className="cards-row">
              {currentEncounters.map((card, i) => {
                const species = getSpecies(card.speciesId);
                if (!species) return null;

                return (
                  <motion.button
                    key={i}
                    className="encounter-card"
                    onClick={() => handleSelectRune(card)}
                    style={{ borderColor: ELEMENT_COLORS[species.element] }}
                    initial={{ opacity: 0, y: 20, rotateY: 180 }}
                    animate={{
                      opacity: 1,
                      y: 0,
                      rotateY: 0,
                      transition: { delay: i * 0.15 }
                    }}
                    whileHover={{ scale: 1.05, y: -5 }}
                    whileTap={{ scale: 0.95 }}
                  >
                    <div
                      className="card-glow"
                      style={{ background: `${ELEMENT_COLORS[species.element]}30` }}
                    />
                    <div
                      className="card-symbol"
                      style={{ color: ELEMENT_COLORS[species.element] }}
                    >
                      {ELEMENT_SYMBOLS[species.element]}
                    </div>
                    <div className="card-name">{species.name}</div>
                    <div className="card-element">{species.element}</div>
                    <div className="card-difficulty">
                      {card.catchDifficulty > 0.5 ? '⭐⭐ Hard' : '⭐ Normal'}
                    </div>
                  </motion.button>
                );
              })}
            </div>

            <button className="cancel-btn" onClick={handleCancel}>
              <X size={16} /> Run Away
            </button>
          </motion.div>
        )}

        {/* SELECT GAME - Choose mini-game */}
        {phase === 'select-game' && catchingSpecies && (
          <motion.div
            key="select-game"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <GameSelector
              element={catchingSpecies.element}
              speciesName={catchingSpecies.name}
              onSelect={handleSelectGame}
              onCancel={handleCancel}
            />
          </motion.div>
        )}

        {/* PLAYING - Active mini-game */}
        {phase === 'playing' && catchingSpecies && selectedGame && (
          <motion.div
            key="playing"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            {selectedGame === 'trace' && (
              <RuneTracing
                speciesId={catchingRune!.speciesId}
                element={catchingSpecies.element}
                difficulty={catchingRune!.catchDifficulty}
                onComplete={handleGameComplete}
                onCancel={handleCancel}
              />
            )}
            {selectedGame === 'clash' && (
              <ElementClash
                speciesId={catchingRune!.speciesId}
                element={catchingSpecies.element}
                difficulty={catchingRune!.catchDifficulty}
                onComplete={handleGameComplete}
                onCancel={handleCancel}
              />
            )}
            {selectedGame === 'memory' && (
              <MemoryMatch
                speciesId={catchingRune!.speciesId}
                element={catchingSpecies.element}
                difficulty={catchingRune!.catchDifficulty}
                onComplete={handleGameComplete}
                onCancel={handleCancel}
              />
            )}
            {selectedGame === 'forge' && (
              <ForgeSequence
                speciesId={catchingRune!.speciesId}
                element={catchingSpecies.element}
                difficulty={catchingRune!.catchDifficulty}
                onComplete={handleGameComplete}
                onCancel={handleCancel}
              />
            )}
          </motion.div>
        )}

        {/* FAILED */}
        {catchFailed && (
          <motion.div
            key="failed"
            className="catch-failed"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0 }}
          >
            <X size={64} className="fail-icon" />
            <h3>It got away!</h3>
            <p>Better luck next time</p>
          </motion.div>
        )}

        {/* RESULT - Caught! */}
        {phase === 'result' && catchResult && (
          <motion.div
            key="result"
            className="catch-result"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
          >
            <motion.div
              initial={{ scale: 0 }}
              animate={{ scale: [0, 1.3, 1] }}
              transition={{ duration: 0.5 }}
            >
              <Check size={64} className="success-icon" />
            </motion.div>
            <h3>Caught!</h3>

            {(() => {
              const species = getSpecies(catchResult.speciesId);
              if (!species) return null;

              return (
                <motion.div
                  className="result-card"
                  style={{
                    borderColor: ELEMENT_COLORS[species.element],
                    boxShadow: `0 0 30px ${ELEMENT_COLORS[species.element]}60`,
                  }}
                  initial={{ y: 20 }}
                  animate={{ y: 0 }}
                >
                  <div className="result-rarity" style={{ color: RARITY_COLORS[catchResult.rarity] }}>
                    {catchResult.rarity.toUpperCase()}
                  </div>
                  <div
                    className="result-symbol"
                    style={{ color: ELEMENT_COLORS[species.element] }}
                  >
                    {ELEMENT_SYMBOLS[species.element]}
                  </div>
                  <div className="result-name">{species.name}</div>
                  <div className="result-stats">
                    <span>Power: {catchResult.stats.power}</span>
                    <span>Guard: {catchResult.stats.guard}</span>
                    <span>Speed: {catchResult.stats.speed}</span>
                  </div>
                  <div className="result-trait">
                    <strong>{species.trait}</strong>
                    <small>{species.traitDescription}</small>
                  </div>

                  {/* Show bonuses */}
                  {bonusResult && (
                    <div className="bonus-display">
                      {bonusResult.rarityBoost > 0 && (
                        <span className="bonus">+{bonusResult.rarityBoost}% Rarity</span>
                      )}
                      {bonusResult.sageBonus > 0 && (
                        <span className="bonus">+{bonusResult.sageBonus} SAGE</span>
                      )}
                      {bonusResult.xpBonus > 0 && (
                        <span className="bonus">+{bonusResult.xpBonus}% XP</span>
                      )}
                      {bonusResult.shinyChance > 0 && (
                        <span className="bonus">+{bonusResult.shinyChance}% Shiny</span>
                      )}
                    </div>
                  )}
                </motion.div>
              );
            })()}

            <motion.button
              className="done-btn"
              onClick={handleDone}
              whileTap={{ scale: 0.95 }}
            >
              Awesome!
            </motion.button>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

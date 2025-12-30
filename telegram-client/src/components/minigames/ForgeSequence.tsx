// Forge Sequence Mini-Game
// Simon Says style - tap the symbols in order

import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import type { MiniGameProps } from './types';
import { calculateBonus } from './types';
import { ELEMENT_COLORS, ELEMENT_SYMBOLS } from '../../data/runes';
import type { RuneElement } from '../../data/runes';

const FORGE_SYMBOLS = ['fire', 'water', 'earth', 'air', 'light', 'void'] as RuneElement[];

export function ForgeSequence({ element, onComplete, onCancel }: MiniGameProps) {
  const [phase, setPhase] = useState<'showing' | 'input' | 'success' | 'fail'>('showing');
  const [sequence, setSequence] = useState<RuneElement[]>([]);
  const [playerInput, setPlayerInput] = useState<RuneElement[]>([]);
  const [currentShowIndex, setCurrentShowIndex] = useState(-1);
  const [round, setRound] = useState(1);
  const [highlightedSymbol, setHighlightedSymbol] = useState<RuneElement | null>(null);
  const [completed, setCompleted] = useState(false);

  const maxRounds = 5;
  const baseSequenceLength = 3;

  // Generate sequence for current round
  const generateSequence = useCallback(() => {
    const length = baseSequenceLength + round - 1;
    const newSequence: RuneElement[] = [];

    // Include the target element at least once
    newSequence.push(element as RuneElement);

    // Fill rest randomly
    for (let i = 1; i < length; i++) {
      newSequence.push(FORGE_SYMBOLS[Math.floor(Math.random() * FORGE_SYMBOLS.length)]);
    }

    // Shuffle
    for (let i = newSequence.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [newSequence[i], newSequence[j]] = [newSequence[j], newSequence[i]];
    }

    return newSequence;
  }, [round, element]);

  // Start new round
  useEffect(() => {
    if (completed) return;

    const newSeq = generateSequence();
    setSequence(newSeq);
    setPlayerInput([]);
    setPhase('showing');
    setCurrentShowIndex(-1);

    // Show sequence with delays
    let showIndex = 0;
    const showInterval = setInterval(() => {
      if (showIndex < newSeq.length) {
        setCurrentShowIndex(showIndex);
        setHighlightedSymbol(newSeq[showIndex]);

        setTimeout(() => {
          setHighlightedSymbol(null);
        }, 400);

        showIndex++;
      } else {
        clearInterval(showInterval);
        setCurrentShowIndex(-1);
        setPhase('input');
      }
    }, 600);

    return () => clearInterval(showInterval);
  }, [round, completed, generateSequence]);

  const handleSymbolTap = (symbol: RuneElement) => {
    if (phase !== 'input' || completed) return;

    // Flash the tapped symbol
    setHighlightedSymbol(symbol);
    setTimeout(() => setHighlightedSymbol(null), 200);

    const newInput = [...playerInput, symbol];
    setPlayerInput(newInput);

    const currentIndex = newInput.length - 1;

    // Check if correct
    if (symbol !== sequence[currentIndex]) {
      // Wrong!
      setPhase('fail');
      handleComplete(false);
      return;
    }

    // Check if sequence complete
    if (newInput.length === sequence.length) {
      setPhase('success');

      if (round >= maxRounds) {
        // All rounds complete!
        handleComplete(true);
      } else {
        // Next round after delay
        setTimeout(() => {
          setRound(r => r + 1);
        }, 1000);
      }
    }
  };

  const handleComplete = (won: boolean) => {
    if (completed) return;
    setCompleted(true);

    const roundBonus = round / maxRounds;
    const score = won
      ? 70 + Math.floor(roundBonus * 30)
      : Math.floor(((round - 1) / maxRounds) * 60);

    setTimeout(() => {
      onComplete({
        success: won,
        score,
        bonus: calculateBonus('forge', score),
      });
    }, 1000);
  };

  const targetColor = ELEMENT_COLORS[element as RuneElement] || '#00ffaa';

  return (
    <motion.div
      className="minigame forge-game"
      initial={{ opacity: 0, scale: 0.9 }}
      animate={{ opacity: 1, scale: 1 }}
    >
      <div className="game-header">
        <h3>Forge Sequence</h3>
        <div className="round-info">
          Round {round}/{maxRounds}
        </div>
      </div>

      {/* Progress indicator */}
      <div className="sequence-progress">
        {sequence.map((_, i) => (
          <motion.div
            key={i}
            className={`seq-dot ${i < playerInput.length ? 'filled' : ''}`}
            style={{
              background: i < playerInput.length ? targetColor : '#333',
            }}
            animate={{
              scale: i === currentShowIndex ? 1.3 : 1,
            }}
          />
        ))}
      </div>

      {/* Phase indicator */}
      <AnimatePresence mode="wait">
        {phase === 'showing' && (
          <motion.p
            key="showing"
            className="phase-text"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            Watch the sequence...
          </motion.p>
        )}
        {phase === 'input' && (
          <motion.p
            key="input"
            className="phase-text"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            Your turn! Tap the symbols
          </motion.p>
        )}
        {phase === 'success' && (
          <motion.p
            key="success"
            className="phase-text success"
            initial={{ opacity: 0, scale: 0.5 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0 }}
          >
            {round >= maxRounds ? '🎉 Perfect Forge!' : '✓ Correct!'}
          </motion.p>
        )}
        {phase === 'fail' && (
          <motion.p
            key="fail"
            className="phase-text fail"
            initial={{ opacity: 0, scale: 0.5 }}
            animate={{ opacity: 1, scale: 1 }}
          >
            ✗ Wrong sequence!
          </motion.p>
        )}
      </AnimatePresence>

      {/* Symbol grid */}
      <div className="forge-grid">
        {FORGE_SYMBOLS.map(symbol => (
          <motion.button
            key={symbol}
            className={`forge-symbol ${highlightedSymbol === symbol ? 'highlighted' : ''}`}
            style={{
              borderColor: ELEMENT_COLORS[symbol],
              background: highlightedSymbol === symbol
                ? `${ELEMENT_COLORS[symbol]}60`
                : `${ELEMENT_COLORS[symbol]}20`,
              boxShadow: highlightedSymbol === symbol
                ? `0 0 30px ${ELEMENT_COLORS[symbol]}`
                : 'none',
            }}
            onClick={() => handleSymbolTap(symbol)}
            disabled={phase !== 'input'}
            whileTap={{ scale: 0.9 }}
            animate={{
              scale: highlightedSymbol === symbol ? 1.1 : 1,
            }}
          >
            <span style={{ color: ELEMENT_COLORS[symbol] }}>
              {ELEMENT_SYMBOLS[symbol]}
            </span>
          </motion.button>
        ))}
      </div>

      {/* Forge animation */}
      <div className="forge-furnace" style={{ borderColor: targetColor }}>
        <motion.div
          className="forge-flame"
          animate={{
            scale: phase === 'success' ? [1, 1.3, 1] : 1,
            opacity: phase === 'fail' ? 0.3 : 1,
          }}
          style={{ background: `${targetColor}80` }}
        >
          {ELEMENT_SYMBOLS[element as RuneElement]}
        </motion.div>
      </div>

      {phase !== 'showing' && !completed && (
        <button className="cancel-btn" onClick={onCancel}>
          Give Up
        </button>
      )}
    </motion.div>
  );
}

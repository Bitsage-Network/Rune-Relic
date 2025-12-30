// Element Clash Mini-Game
// Rock-paper-scissors style element battle

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import type { MiniGameProps } from './types';
import { calculateBonus } from './types';
import { ELEMENT_COLORS, ELEMENT_SYMBOLS, getElementMultiplier } from '../../data/runes';
import type { RuneElement } from '../../data/runes';

const ELEMENTS: RuneElement[] = ['fire', 'water', 'earth', 'air', 'light', 'void'];

export function ElementClash({ element, difficulty, onComplete, onCancel }: MiniGameProps) {
  const [phase, setPhase] = useState<'choose' | 'battle' | 'result'>('choose');
  const [playerChoice, setPlayerChoice] = useState<RuneElement | null>(null);
  const [enemyChoice, setEnemyChoice] = useState<RuneElement>(element as RuneElement);
  const [round, setRound] = useState(1);
  const [playerWins, setPlayerWins] = useState(0);
  const [enemyWins, setEnemyWins] = useState(0);
  const [roundResult, setRoundResult] = useState<'win' | 'lose' | 'tie' | null>(null);
  const [, setTimeLeft] = useState(5);

  const totalRounds = 3;
  const winsNeeded = 2;

  // Timer for choosing
  useEffect(() => {
    if (phase !== 'choose') return;

    const timer = setInterval(() => {
      setTimeLeft(t => {
        if (t <= 1) {
          // Auto-pick random element
          const randomElement = ELEMENTS[Math.floor(Math.random() * ELEMENTS.length)];
          handleChoose(randomElement);
          return 5;
        }
        return t - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [phase, round]);

  const handleChoose = (chosen: RuneElement) => {
    setPlayerChoice(chosen);
    setPhase('battle');

    // Enemy picks (biased towards their element, but can adapt)
    const enemyOptions = [...ELEMENTS];
    // Higher difficulty = smarter enemy
    const smart = Math.random() < difficulty;
    let enemyPick: RuneElement;

    if (smart) {
      // Pick something that beats player's likely choice
      const counters = ELEMENTS.filter(e => getElementMultiplier(e, chosen) > 1);
      enemyPick = counters.length > 0
        ? counters[Math.floor(Math.random() * counters.length)]
        : element as RuneElement;
    } else {
      // Random or prefer own element
      enemyPick = Math.random() < 0.4
        ? element as RuneElement
        : enemyOptions[Math.floor(Math.random() * enemyOptions.length)];
    }

    setEnemyChoice(enemyPick);

    // Calculate result
    setTimeout(() => {
      const playerMult = getElementMultiplier(chosen, enemyPick);
      const enemyMult = getElementMultiplier(enemyPick, chosen);

      let result: 'win' | 'lose' | 'tie';
      if (playerMult > enemyMult) {
        result = 'win';
        setPlayerWins(w => w + 1);
      } else if (enemyMult > playerMult) {
        result = 'lose';
        setEnemyWins(w => w + 1);
      } else {
        result = 'tie';
      }

      setRoundResult(result);

      // Check if game over
      setTimeout(() => {
        const newPlayerWins = playerWins + (result === 'win' ? 1 : 0);
        const newEnemyWins = enemyWins + (result === 'lose' ? 1 : 0);

        if (newPlayerWins >= winsNeeded || newEnemyWins >= winsNeeded || round >= totalRounds) {
          setPhase('result');
          const won = newPlayerWins > newEnemyWins;
          const score = won
            ? 60 + Math.floor((newPlayerWins / totalRounds) * 40)
            : Math.floor((newPlayerWins / totalRounds) * 40);

          setTimeout(() => {
            onComplete({
              success: won,
              score,
              bonus: calculateBonus('clash', score),
            });
          }, 1500);
        } else {
          // Next round
          setRound(r => r + 1);
          setPhase('choose');
          setPlayerChoice(null);
          setRoundResult(null);
          setTimeLeft(5);
        }
      }, 1500);
    }, 1000);
  };

  return (
    <motion.div
      className="minigame clash-game"
      initial={{ opacity: 0, scale: 0.9 }}
      animate={{ opacity: 1, scale: 1 }}
    >
      <div className="game-header">
        <h3>Element Clash</h3>
        <div className="round-info">
          Round {round}/{totalRounds}
        </div>
      </div>

      <div className="score-display">
        <div className="score player">
          <span>You</span>
          <div className="wins">{playerWins}</div>
        </div>
        <div className="vs">VS</div>
        <div className="score enemy">
          <span>Wild</span>
          <div className="wins">{enemyWins}</div>
        </div>
      </div>

      <AnimatePresence mode="wait">
        {phase === 'choose' && (
          <motion.div
            key="choose"
            className="choose-phase"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <div className="timer-bar">
              <motion.div
                className="timer-fill"
                initial={{ width: '100%' }}
                animate={{ width: '0%' }}
                transition={{ duration: 5, ease: 'linear' }}
              />
            </div>

            <p>Choose your element!</p>

            <div className="element-grid">
              {ELEMENTS.map(el => (
                <motion.button
                  key={el}
                  className="element-btn"
                  style={{
                    borderColor: ELEMENT_COLORS[el],
                    background: `${ELEMENT_COLORS[el]}20`,
                  }}
                  onClick={() => handleChoose(el)}
                  whileHover={{ scale: 1.1 }}
                  whileTap={{ scale: 0.95 }}
                >
                  <span className="symbol">{ELEMENT_SYMBOLS[el]}</span>
                  <span className="name">{el}</span>
                </motion.button>
              ))}
            </div>
          </motion.div>
        )}

        {phase === 'battle' && (
          <motion.div
            key="battle"
            className="battle-phase"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <div className="clash-arena">
              <motion.div
                className="fighter player"
                initial={{ x: -100 }}
                animate={{ x: 0 }}
                style={{ borderColor: ELEMENT_COLORS[playerChoice!] }}
              >
                <span className="symbol">{ELEMENT_SYMBOLS[playerChoice!]}</span>
                <span className="label">{playerChoice}</span>
              </motion.div>

              <motion.div
                className="clash-effect"
                initial={{ scale: 0 }}
                animate={{ scale: [0, 1.5, 1] }}
                transition={{ delay: 0.5 }}
              >
                {roundResult === 'win' && '💥'}
                {roundResult === 'lose' && '💨'}
                {roundResult === 'tie' && '⚡'}
                {!roundResult && '⚔️'}
              </motion.div>

              <motion.div
                className="fighter enemy"
                initial={{ x: 100 }}
                animate={{ x: 0 }}
                style={{ borderColor: ELEMENT_COLORS[enemyChoice] }}
              >
                <span className="symbol">{ELEMENT_SYMBOLS[enemyChoice]}</span>
                <span className="label">{enemyChoice}</span>
              </motion.div>
            </div>

            {roundResult && (
              <motion.div
                className={`round-result ${roundResult}`}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
              >
                {roundResult === 'win' && 'Super Effective!'}
                {roundResult === 'lose' && 'Not Very Effective...'}
                {roundResult === 'tie' && 'Draw!'}
              </motion.div>
            )}
          </motion.div>
        )}

        {phase === 'result' && (
          <motion.div
            key="result"
            className="result-phase"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
          >
            <div className={`final-result ${playerWins > enemyWins ? 'win' : 'lose'}`}>
              {playerWins > enemyWins ? '🎉 Victory!' : '😢 Defeated'}
            </div>
            <p>{playerWins} - {enemyWins}</p>
          </motion.div>
        )}
      </AnimatePresence>

      {phase === 'choose' && (
        <button className="cancel-btn" onClick={onCancel}>
          Flee
        </button>
      )}
    </motion.div>
  );
}

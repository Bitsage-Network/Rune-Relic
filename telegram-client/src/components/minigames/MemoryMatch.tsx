// Memory Match Mini-Game
// Flip cards to find matching pairs

import { useState, useEffect, useCallback, useRef } from 'react';
import { motion } from 'framer-motion';
import type { MiniGameProps } from './types';
import { calculateBonus } from './types';
import { ELEMENT_COLORS, ELEMENT_SYMBOLS } from '../../data/runes';
import type { RuneElement } from '../../data/runes';

interface Card {
  id: number;
  element: RuneElement;
  isFlipped: boolean;
  isMatched: boolean;
}

const ELEMENTS: RuneElement[] = ['fire', 'water', 'earth', 'air', 'light', 'void'];

function shuffleArray<T>(array: T[]): T[] {
  const shuffled = [...array];
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
  }
  return shuffled;
}

export function MemoryMatch({ element, onComplete, onCancel }: MiniGameProps) {
  const [cards, setCards] = useState<Card[]>([]);
  const [flippedIds, setFlippedIds] = useState<number[]>([]);
  const [matchedPairs, setMatchedPairs] = useState(0);
  const [moves, setMoves] = useState(0);
  const [timeLeft, setTimeLeft] = useState(30);
  const [isLocked, setIsLocked] = useState(true); // Start locked for initial reveal
  const [completed, setCompleted] = useState(false);
  const [showingInitial, setShowingInitial] = useState(true);
  const [matchedAnimation, setMatchedAnimation] = useState<number[]>([]);

  const totalPairs = 6;
  const cardsRef = useRef<Card[]>([]);

  // Initialize cards
  useEffect(() => {
    const elements = shuffleArray(ELEMENTS).slice(0, totalPairs);
    const cardPairs: Card[] = [];

    elements.forEach((el, i) => {
      cardPairs.push({ id: i * 2, element: el, isFlipped: false, isMatched: false });
      cardPairs.push({ id: i * 2 + 1, element: el, isFlipped: false, isMatched: false });
    });

    const shuffledCards = shuffleArray(cardPairs);
    setCards(shuffledCards);
    cardsRef.current = shuffledCards;

    // Brief reveal at start - show all cards face up
    const revealTimer = setTimeout(() => {
      setCards(prev => {
        const updated = prev.map(c => ({ ...c, isFlipped: true }));
        cardsRef.current = updated;
        return updated;
      });

      // Hide cards after 1.5 seconds
      setTimeout(() => {
        setCards(prev => {
          const updated = prev.map(c => ({ ...c, isFlipped: false }));
          cardsRef.current = updated;
          return updated;
        });
        setShowingInitial(false);
        setIsLocked(false);
      }, 1500);
    }, 300);

    return () => clearTimeout(revealTimer);
  }, []);

  // Timer - only start after initial reveal
  useEffect(() => {
    if (completed || showingInitial) return;

    const timer = setInterval(() => {
      setTimeLeft(t => {
        if (t <= 1) {
          clearInterval(timer);
          handleComplete(false);
          return 0;
        }
        return t - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [completed, showingInitial]);

  const handleComplete = useCallback((won: boolean) => {
    if (completed) return;
    setCompleted(true);
    setIsLocked(true);

    const timeBonus = timeLeft / 30;
    const moveEfficiency = Math.max(0, 1 - (moves - totalPairs) / (totalPairs * 2));
    const score = won
      ? Math.floor(40 + timeBonus * 30 + moveEfficiency * 30)
      : Math.floor((matchedPairs / totalPairs) * 40);

    setTimeout(() => {
      onComplete({
        success: won,
        score,
        bonus: calculateBonus('memory', score),
      });
    }, 800);
  }, [completed, timeLeft, moves, matchedPairs, onComplete]);

  // Check for win
  useEffect(() => {
    if (matchedPairs === totalPairs && !completed && !showingInitial) {
      handleComplete(true);
    }
  }, [matchedPairs, completed, showingInitial, handleComplete]);

  const handleCardClick = (cardId: number) => {
    if (isLocked || completed || showingInitial) return;

    const currentCards = cardsRef.current;
    const card = currentCards.find(c => c.id === cardId);
    if (!card || card.isFlipped || card.isMatched) return;

    // Don't allow clicking the same card twice
    if (flippedIds.includes(cardId)) return;

    // Flip the card
    setCards(prev => {
      const updated = prev.map(c =>
        c.id === cardId ? { ...c, isFlipped: true } : c
      );
      cardsRef.current = updated;
      return updated;
    });

    const newFlipped = [...flippedIds, cardId];
    setFlippedIds(newFlipped);

    if (newFlipped.length === 2) {
      setMoves(m => m + 1);
      setIsLocked(true);

      const [firstId, secondId] = newFlipped;
      const card1 = currentCards.find(c => c.id === firstId)!;
      const card2 = currentCards.find(c => c.id === secondId)!;

      if (card1.element === card2.element) {
        // Match found!
        setMatchedAnimation([firstId, secondId]);

        setTimeout(() => {
          setCards(prev => {
            const updated = prev.map(c =>
              c.id === firstId || c.id === secondId
                ? { ...c, isMatched: true, isFlipped: true }
                : c
            );
            cardsRef.current = updated;
            return updated;
          });
          setMatchedPairs(m => m + 1);
          setFlippedIds([]);
          setMatchedAnimation([]);
          setIsLocked(false);
        }, 600);
      } else {
        // No match - flip back
        setTimeout(() => {
          setCards(prev => {
            const updated = prev.map(c =>
              c.id === firstId || c.id === secondId
                ? { ...c, isFlipped: false }
                : c
            );
            cardsRef.current = updated;
            return updated;
          });
          setFlippedIds([]);
          setIsLocked(false);
        }, 1000);
      }
    }
  };

  const targetColor = ELEMENT_COLORS[element as RuneElement] || '#00ffaa';

  return (
    <motion.div
      className="minigame memory-game"
      initial={{ opacity: 0, scale: 0.9 }}
      animate={{ opacity: 1, scale: 1 }}
    >
      <div className="game-header">
        <h3>Memory Match</h3>
        <div className="stats">
          <span className="timer" style={{ color: timeLeft <= 5 ? '#ff4444' : targetColor }}>
            {timeLeft}s
          </span>
          <span className="moves">{moves} moves</span>
        </div>
      </div>

      <div className="progress-bar">
        <motion.div
          className="progress-fill"
          style={{ background: targetColor }}
          animate={{ width: `${(matchedPairs / totalPairs) * 100}%` }}
        />
      </div>
      <p className="progress-text">{matchedPairs}/{totalPairs} pairs</p>

      {showingInitial && (
        <p className="memory-hint">Memorize the cards!</p>
      )}

      <div className="memory-card-grid">
        {cards.map(card => {
          const isFlippedOrMatched = card.isFlipped || card.isMatched;
          const isMatchAnimating = matchedAnimation.includes(card.id);

          return (
            <motion.div
              key={card.id}
              className={`memory-card-wrapper ${card.isMatched ? 'matched' : ''}`}
              animate={{
                scale: isMatchAnimating ? [1, 1.2, 1] : 1,
              }}
              transition={{ duration: 0.3 }}
            >
              <motion.button
                className={`memory-card ${isFlippedOrMatched ? 'flipped' : ''}`}
                onClick={() => handleCardClick(card.id)}
                disabled={isLocked || card.isMatched}
                whileTap={!isLocked && !card.isMatched ? { scale: 0.95 } : undefined}
              >
                <motion.div
                  className="memory-card-inner"
                  animate={{ rotateY: isFlippedOrMatched ? 180 : 0 }}
                  transition={{ duration: 0.4, ease: 'easeInOut' }}
                  style={{ transformStyle: 'preserve-3d' }}
                >
                  {/* Card Back */}
                  <div className="memory-card-face memory-card-back">
                    <span>?</span>
                  </div>

                  {/* Card Front */}
                  <div
                    className="memory-card-face memory-card-front"
                    style={{
                      background: `linear-gradient(135deg, ${ELEMENT_COLORS[card.element]}40, ${ELEMENT_COLORS[card.element]}20)`,
                      borderColor: ELEMENT_COLORS[card.element],
                      boxShadow: isMatchAnimating ? `0 0 20px ${ELEMENT_COLORS[card.element]}` : 'none',
                    }}
                  >
                    <span style={{ color: ELEMENT_COLORS[card.element] }}>
                      {ELEMENT_SYMBOLS[card.element]}
                    </span>
                  </div>
                </motion.div>
              </motion.button>
            </motion.div>
          );
        })}
      </div>

      <button className="cancel-btn" onClick={onCancel} disabled={isLocked && !showingInitial}>
        Give Up
      </button>
    </motion.div>
  );
}

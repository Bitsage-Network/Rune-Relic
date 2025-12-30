// Mini-Game Selector
// Let players choose which game to play

import { motion } from 'framer-motion';
import { GAME_INFO } from './types';
import type { MiniGameType } from './types';
import { ELEMENT_COLORS } from '../../data/runes';
import type { RuneElement } from '../../data/runes';

interface GameSelectorProps {
  element: string;
  speciesName: string;
  onSelect: (game: MiniGameType) => void;
  onCancel: () => void;
}

export function GameSelector({ element, speciesName, onSelect, onCancel }: GameSelectorProps) {
  const color = ELEMENT_COLORS[element as RuneElement] || '#00ffaa';

  const games: MiniGameType[] = ['trace', 'clash', 'memory', 'forge'];

  return (
    <motion.div
      className="game-selector"
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
    >
      <h3>Capture {speciesName}!</h3>
      <p className="selector-subtitle">Choose your method:</p>

      <div className="game-options">
        {games.map((gameType, i) => {
          const info = GAME_INFO[gameType];
          return (
            <motion.button
              key={gameType}
              className="game-option"
              onClick={() => onSelect(gameType)}
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0, transition: { delay: i * 0.1 } }}
              whileHover={{ scale: 1.02, x: 5 }}
              whileTap={{ scale: 0.98 }}
              style={{ borderColor: color }}
            >
              <div className="game-icon">{info.icon}</div>
              <div className="game-info">
                <div className="game-name">{info.name}</div>
                <div className="game-desc">{info.description}</div>
              </div>
              <div className="game-bonus" style={{ color }}>
                {info.bonusType}
              </div>
            </motion.button>
          );
        })}
      </div>

      <button className="cancel-btn" onClick={onCancel}>
        Let it go...
      </button>
    </motion.div>
  );
}

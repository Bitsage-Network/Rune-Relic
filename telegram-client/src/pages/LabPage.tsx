// Lab Page - Fusion crafting

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { FlaskConical, Plus, Sparkles, Coins } from 'lucide-react';
import { useGameStore } from '../store/gameStore';
import { getSpecies, ELEMENT_COLORS, ELEMENT_SYMBOLS, RARITY_COLORS } from '../data/runes';
import type { OwnedRune } from '../data/runes';

export function LabPage() {
  const { ownedRunes, sage, fuseRunes, setPage } = useGameStore();
  const [selected, setSelected] = useState<[string | null, string | null]>([null, null]);
  const [result, setResult] = useState<OwnedRune | null>(null);
  const [fusing, setFusing] = useState(false);

  const handleSelect = (id: string) => {
    if (selected[0] === id) {
      setSelected([null, selected[1]]);
    } else if (selected[1] === id) {
      setSelected([selected[0], null]);
    } else if (!selected[0]) {
      setSelected([id, selected[1]]);
    } else if (!selected[1]) {
      setSelected([selected[0], id]);
    }
  };

  const handleFuse = async () => {
    if (!selected[0] || !selected[1]) return;

    setFusing(true);
    await new Promise(r => setTimeout(r, 1500));

    const newRune = fuseRunes(selected[0], selected[1]);
    if (newRune) {
      setResult(newRune);
    }
    setFusing(false);
    setSelected([null, null]);
  };

  const handleDone = () => {
    setResult(null);
  };

  // Predict fusion result
  const getPrediction = () => {
    if (!selected[0] || !selected[1]) return null;

    const rune1 = ownedRunes.find(r => r.id === selected[0]);
    const rune2 = ownedRunes.find(r => r.id === selected[1]);
    if (!rune1 || !rune2) return null;

    const species1 = getSpecies(rune1.speciesId);
    const species2 = getSpecies(rune2.speciesId);
    if (!species1 || !species2) return null;

    // Same species with evolution
    if (rune1.speciesId === rune2.speciesId && species1.evolvesTo) {
      return `Evolves to ${getSpecies(species1.evolvesTo)?.name}!`;
    }
    // Same element
    if (species1.element === species2.element) {
      return `Creates a ${species1.element} rune`;
    }
    // Different
    return `Creates a ${species1.element}/${species2.element}/arcane rune`;
  };

  if (ownedRunes.length < 2) {
    return (
      <div className="page lab-page">
        <h2 className="page-title">
          <FlaskConical size={24} /> Fusion Lab
        </h2>
        <div className="not-enough">
          <p>You need at least 2 runes to fuse!</p>
          <button onClick={() => setPage('encounter')}>Go Catch Some</button>
        </div>
      </div>
    );
  }

  return (
    <div className="page lab-page">
      <h2 className="page-title">
        <FlaskConical size={24} /> Fusion Lab
      </h2>

      <AnimatePresence mode="wait">
        {/* Fusion Result */}
        {result && (
          <motion.div
            key="result"
            className="fusion-result"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0 }}
          >
            <Sparkles size={48} className="sparkle-icon" />
            <h3>Fusion Complete!</h3>

            {(() => {
              const species = getSpecies(result.speciesId);
              if (!species) return null;

              return (
                <div
                  className="result-card"
                  style={{
                    borderColor: ELEMENT_COLORS[species.element],
                    boxShadow: `0 0 30px ${ELEMENT_COLORS[species.element]}60`,
                  }}
                >
                  <div className="result-rarity" style={{ color: RARITY_COLORS[result.rarity] }}>
                    {result.rarity.toUpperCase()}
                  </div>
                  <div style={{ color: ELEMENT_COLORS[species.element], fontSize: 48 }}>
                    {ELEMENT_SYMBOLS[species.element]}
                  </div>
                  <div className="result-name">{species.name}</div>
                  <div className="result-stats">
                    <span>Power: {result.stats.power}</span>
                    <span>Guard: {result.stats.guard}</span>
                    <span>Speed: {result.stats.speed}</span>
                  </div>
                </div>
              );
            })()}

            <motion.button
              className="done-btn"
              onClick={handleDone}
              whileTap={{ scale: 0.95 }}
            >
              Nice!
            </motion.button>
          </motion.div>
        )}

        {/* Fusing animation */}
        {fusing && !result && (
          <motion.div
            key="fusing"
            className="fusing"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <FlaskConical size={64} className="flask-icon spinning" />
            <p>Fusing...</p>
          </motion.div>
        )}

        {/* Selection */}
        {!fusing && !result && (
          <motion.div
            key="select"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <p className="instruction">Select 2 runes to fuse</p>

            {/* Fusion slots */}
            <div className="fusion-slots">
              {[0, 1].map(i => {
                const runeId = selected[i];
                const rune = runeId ? ownedRunes.find(r => r.id === runeId) : null;
                const species = rune ? getSpecies(rune.speciesId) : null;

                return (
                  <div
                    key={i}
                    className={`fusion-slot ${rune ? 'filled' : 'empty'}`}
                    style={species ? { borderColor: ELEMENT_COLORS[species.element] } : undefined}
                    onClick={() => runeId && handleSelect(runeId)}
                  >
                    {species ? (
                      <>
                        <span style={{ color: ELEMENT_COLORS[species.element], fontSize: 32 }}>
                          {ELEMENT_SYMBOLS[species.element]}
                        </span>
                        <small>{species.name}</small>
                      </>
                    ) : (
                      <Plus size={24} />
                    )}
                  </div>
                );
              })}
            </div>

            {/* Prediction */}
            {selected[0] && selected[1] && (
              <motion.div
                className="prediction"
                initial={{ opacity: 0, y: -10 }}
                animate={{ opacity: 1, y: 0 }}
              >
                <Sparkles size={16} />
                {getPrediction()}
              </motion.div>
            )}

            {/* Cost */}
            <div className="fusion-cost">
              <Coins size={16} />
              <span>Cost: 50 SAGE</span>
              <span className="balance">(You have: {sage})</span>
            </div>

            {/* Rune Grid */}
            <div className="rune-select-grid">
              {ownedRunes.map(rune => {
                const species = getSpecies(rune.speciesId);
                if (!species) return null;

                const isSelected = selected.includes(rune.id);

                return (
                  <motion.button
                    key={rune.id}
                    className={`rune-select-card ${isSelected ? 'selected' : ''}`}
                    onClick={() => handleSelect(rune.id)}
                    style={{
                      borderColor: isSelected ? ELEMENT_COLORS[species.element] : '#444',
                    }}
                    whileTap={{ scale: 0.95 }}
                  >
                    <div style={{ color: ELEMENT_COLORS[species.element] }}>
                      {ELEMENT_SYMBOLS[species.element]}
                    </div>
                    <div className="rune-select-name">{species.name}</div>
                    <div className="rune-select-power">{rune.stats.power}</div>
                  </motion.button>
                );
              })}
            </div>

            <motion.button
              className="fuse-btn"
              onClick={handleFuse}
              disabled={!selected[0] || !selected[1] || sage < 50}
              whileTap={{ scale: 0.95 }}
            >
              <FlaskConical size={20} />
              Fuse Runes
            </motion.button>

            {sage < 50 && (
              <p className="not-enough-sage">Not enough SAGE!</p>
            )}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

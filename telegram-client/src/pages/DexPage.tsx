// RuneDex Page - Collection tracker

import { motion } from 'framer-motion';
import { useGameStore } from '../store/gameStore';
import { RUNE_SPECIES, ELEMENT_COLORS, ELEMENT_SYMBOLS } from '../data/runes';

export function DexPage() {
  const { caughtSpecies, seenSpecies, ownedRunes } = useGameStore();

  // Group by element
  const elements = ['fire', 'water', 'earth', 'air', 'light', 'void', 'arcane'] as const;

  const getRuneStatus = (id: number): 'caught' | 'seen' | 'unknown' => {
    if (caughtSpecies.includes(id)) return 'caught';
    if (seenSpecies.includes(id)) return 'seen';
    return 'unknown';
  };

  const getOwnedCount = (speciesId: number) => {
    return ownedRunes.filter(r => r.speciesId === speciesId).length;
  };

  return (
    <div className="page dex-page">
      <h2 className="page-title">RuneDex</h2>

      {/* Progress */}
      <div className="dex-stats">
        <span className="caught">{caughtSpecies.length} Caught</span>
        <span className="seen">{seenSpecies.length} Seen</span>
        <span className="total">21 Total</span>
      </div>

      {/* Milestones */}
      <div className="milestones">
        <div className={`milestone ${caughtSpecies.length >= 7 ? 'complete' : ''}`}>
          <span>7</span>
          <small>+1 Energy</small>
        </div>
        <div className={`milestone ${caughtSpecies.length >= 14 ? 'complete' : ''}`}>
          <span>14</span>
          <small>Rare Catalyst</small>
        </div>
        <div className={`milestone ${caughtSpecies.length >= 21 ? 'complete' : ''}`}>
          <span>21</span>
          <small>Legendary</small>
        </div>
      </div>

      {/* Rune Grid by Element */}
      {elements.map(element => {
        const runesOfElement = RUNE_SPECIES.filter(s => s.element === element);

        return (
          <motion.div
            key={element}
            className="element-section"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
          >
            <h3 style={{ color: ELEMENT_COLORS[element] }}>
              {ELEMENT_SYMBOLS[element]} {element.toUpperCase()}
            </h3>

            <div className="dex-grid">
              {runesOfElement.map(species => {
                const status = getRuneStatus(species.id);
                const owned = getOwnedCount(species.id);

                return (
                  <motion.div
                    key={species.id}
                    className={`dex-entry ${status}`}
                    style={{
                      borderColor: status === 'caught' ? ELEMENT_COLORS[element] : undefined,
                      boxShadow: status === 'caught' ? `0 0 10px ${ELEMENT_COLORS[element]}40` : undefined,
                    }}
                    whileHover={{ scale: 1.05 }}
                  >
                    <div className="entry-number">#{species.id}</div>

                    {status === 'unknown' ? (
                      <div className="entry-unknown">?</div>
                    ) : (
                      <>
                        <div
                          className="entry-symbol"
                          style={{ color: status === 'caught' ? ELEMENT_COLORS[element] : '#666' }}
                        >
                          {ELEMENT_SYMBOLS[element]}
                        </div>
                        <div className="entry-name">
                          {status === 'caught' ? species.name : '???'}
                        </div>
                        {status === 'caught' && owned > 0 && (
                          <div className="entry-owned">×{owned}</div>
                        )}
                      </>
                    )}
                  </motion.div>
                );
              })}
            </div>
          </motion.div>
        );
      })}
    </div>
  );
}

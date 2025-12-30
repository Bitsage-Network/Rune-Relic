// Collection Page - View owned runes

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { BookOpen, Trash2, X } from 'lucide-react';
import { useGameStore } from '../store/gameStore';
import { getSpecies, ELEMENT_COLORS, ELEMENT_SYMBOLS, RARITY_COLORS } from '../data/runes';
import type { OwnedRune } from '../data/runes';

export function CollectionPage() {
  const { ownedRunes, releaseRune, setPage } = useGameStore();
  const [selectedRune, setSelectedRune] = useState<OwnedRune | null>(null);
  const [confirmRelease, setConfirmRelease] = useState(false);

  if (ownedRunes.length === 0) {
    return (
      <div className="page collection-page">
        <h2 className="page-title">
          <BookOpen size={24} /> Collection
        </h2>
        <div className="not-enough">
          <p>No runes yet!</p>
          <button onClick={() => setPage('encounter')}>Go Catch Some</button>
        </div>
      </div>
    );
  }

  return (
    <div className="page collection-page">
      <h2 className="page-title">
        <BookOpen size={24} /> Collection
      </h2>

      <p className="collection-count">{ownedRunes.length} Runes</p>

      {/* Rune Grid */}
      <div className="collection-grid">
        {ownedRunes.map(rune => {
          const species = getSpecies(rune.speciesId);
          if (!species) return null;

          return (
            <motion.div
              key={rune.id}
              className="collection-card"
              style={{
                borderColor: ELEMENT_COLORS[species.element],
                boxShadow: `0 0 10px ${ELEMENT_COLORS[species.element]}30`,
              }}
              onClick={() => setSelectedRune(rune)}
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.98 }}
            >
              <div
                className="rarity-badge"
                style={{ background: RARITY_COLORS[rune.rarity] }}
              >
                {rune.rarity[0].toUpperCase()}
              </div>
              <div
                className="card-symbol"
                style={{ color: ELEMENT_COLORS[species.element] }}
              >
                {ELEMENT_SYMBOLS[species.element]}
              </div>
              <div className="card-name">{species.name}</div>
              <div className="card-power">{rune.stats.power}</div>
            </motion.div>
          );
        })}
      </div>

      {/* Detail Modal */}
      <AnimatePresence>
        {selectedRune && (
          <motion.div
            className="modal-overlay"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={() => { setSelectedRune(null); setConfirmRelease(false); }}
          >
            <motion.div
              className="rune-detail-modal"
              initial={{ scale: 0.8, y: 50 }}
              animate={{ scale: 1, y: 0 }}
              exit={{ scale: 0.8, y: 50 }}
              onClick={e => e.stopPropagation()}
            >
              {(() => {
                const species = getSpecies(selectedRune.speciesId);
                if (!species) return null;

                return (
                  <>
                    <button className="close-btn" onClick={() => { setSelectedRune(null); setConfirmRelease(false); }}>
                      <X size={24} />
                    </button>

                    <div
                      className="detail-header"
                      style={{ borderColor: ELEMENT_COLORS[species.element] }}
                    >
                      <div
                        className="detail-symbol"
                        style={{ color: ELEMENT_COLORS[species.element] }}
                      >
                        {ELEMENT_SYMBOLS[species.element]}
                      </div>
                      <div className="detail-name">{species.name}</div>
                      <div
                        className="detail-rarity"
                        style={{ color: RARITY_COLORS[selectedRune.rarity] }}
                      >
                        {selectedRune.rarity.toUpperCase()}
                      </div>
                    </div>

                    <div className="detail-stats">
                      <div className="stat">
                        <span className="stat-label">Power</span>
                        <span className="stat-value">{selectedRune.stats.power}</span>
                      </div>
                      <div className="stat">
                        <span className="stat-label">Guard</span>
                        <span className="stat-value">{selectedRune.stats.guard}</span>
                      </div>
                      <div className="stat">
                        <span className="stat-label">Speed</span>
                        <span className="stat-value">{selectedRune.stats.speed}</span>
                      </div>
                    </div>

                    <div className="detail-trait">
                      <strong>{species.trait}</strong>
                      <p>{species.traitDescription}</p>
                    </div>

                    <div className="detail-signature">
                      Signature: <strong>{species.signature}</strong>
                    </div>

                    <div className="detail-wins">
                      Wins: {selectedRune.wins}
                    </div>

                    {!confirmRelease ? (
                      <button
                        className="release-btn"
                        onClick={() => setConfirmRelease(true)}
                      >
                        <Trash2 size={16} /> Release
                      </button>
                    ) : (
                      <div className="confirm-release">
                        <p>Release for {
                          { common: 5, rare: 15, epic: 40, legendary: 100 }[selectedRune.rarity]
                        } SAGE?</p>
                        <div className="confirm-buttons">
                          <button
                            className="confirm-yes"
                            onClick={() => {
                              releaseRune(selectedRune.id);
                              setSelectedRune(null);
                              setConfirmRelease(false);
                            }}
                          >
                            Yes
                          </button>
                          <button
                            className="confirm-no"
                            onClick={() => setConfirmRelease(false)}
                          >
                            No
                          </button>
                        </div>
                      </div>
                    )}
                  </>
                );
              })()}
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

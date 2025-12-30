// Home Page - Dashboard

import { useEffect } from 'react';
import { motion } from 'framer-motion';
import { Zap, Trophy, Coins, BookOpen, Swords, FlaskConical, Search, Skull } from 'lucide-react';
import { useGameStore } from '../store/gameStore';
import { getSpecies, ELEMENT_COLORS } from '../data/runes';
import { getTodaysBoss, getBossColor, getBossSymbol } from '../data/bosses';
import { DailyChallenges } from '../components/DailyChallenges';

export function HomePage() {
  const {
    username, sage, energy, wins, losses,
    ownedRunes, caughtSpecies, seenSpecies,
    bossEnergy, checkDailyReset,
    regenerateEnergy, setPage
  } = useGameStore();

  const boss = getTodaysBoss();
  const bossColor = getBossColor(boss);
  const bossSymbol = getBossSymbol(boss);

  useEffect(() => {
    regenerateEnergy();
    checkDailyReset();
  }, [regenerateEnergy, checkDailyReset]);

  const winRate = wins + losses > 0 ? Math.round((wins / (wins + losses)) * 100) : 0;

  return (
    <div className="page home-page">
      {/* Header */}
      <motion.div
        className="welcome-header"
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
      >
        <h1>Welcome, {username}</h1>
        <p className="subtitle">Rune Seeker</p>
      </motion.div>

      {/* Quick Stats */}
      <div className="stats-grid">
        <div className="stat-card">
          <Coins className="stat-icon gold" />
          <div className="stat-value">{sage}</div>
          <div className="stat-label">SAGE</div>
        </div>

        <div className="stat-card">
          <Zap className="stat-icon blue" />
          <div className="stat-value">{energy}/5</div>
          <div className="stat-label">Energy</div>
        </div>

        <div className="stat-card">
          <BookOpen className="stat-icon purple" />
          <div className="stat-value">{caughtSpecies.length}/21</div>
          <div className="stat-label">RuneDex</div>
        </div>

        <div className="stat-card">
          <Trophy className="stat-icon gold" />
          <div className="stat-value">{winRate}%</div>
          <div className="stat-label">Win Rate</div>
        </div>
      </div>

      {/* Progress Bar */}
      <motion.div
        className="dex-progress"
        initial={{ opacity: 0, scaleX: 0 }}
        animate={{ opacity: 1, scaleX: 1 }}
      >
        <div className="progress-header">
          <span>RuneDex Progress</span>
          <span>{caughtSpecies.length} caught • {seenSpecies.length} seen</span>
        </div>
        <div className="progress-bar">
          <div
            className="progress-fill caught"
            style={{ width: `${(caughtSpecies.length / 21) * 100}%` }}
          />
          <div
            className="progress-fill seen"
            style={{ width: `${(seenSpecies.length / 21) * 100}%` }}
          />
        </div>
      </motion.div>

      {/* Daily Challenges */}
      <DailyChallenges />

      {/* Boss Banner */}
      <motion.button
        className="boss-banner"
        style={{
          borderColor: bossColor,
          background: `linear-gradient(135deg, ${bossColor}20, ${bossColor}10)`,
        }}
        onClick={() => setPage('boss')}
        whileTap={{ scale: 0.98 }}
        initial={{ opacity: 0, y: 10 }}
        animate={{ opacity: 1, y: 0 }}
      >
        <div className="boss-banner-icon" style={{ color: bossColor }}>
          {bossSymbol}
        </div>
        <div className="boss-banner-info">
          <span className="boss-banner-title">{boss.name}</span>
          <span className="boss-banner-subtitle">Today's Boss</span>
        </div>
        <div className="boss-banner-energy">
          <Skull size={16} color={bossEnergy > 0 ? bossColor : '#666'} />
          <span style={{ color: bossEnergy > 0 ? bossColor : '#666' }}>
            {bossEnergy > 0 ? 'Ready!' : 'Done'}
          </span>
        </div>
      </motion.button>

      {/* Quick Actions */}
      <div className="action-grid">
        <motion.button
          className="action-card primary"
          onClick={() => setPage('encounter')}
          whileTap={{ scale: 0.95 }}
        >
          <Search size={32} />
          <span>Explore</span>
          <small>{energy} energy</small>
        </motion.button>

        <motion.button
          className="action-card"
          onClick={() => setPage('battle')}
          whileTap={{ scale: 0.95 }}
          disabled={ownedRunes.length < 3}
        >
          <Swords size={32} />
          <span>Battle</span>
          <small>{ownedRunes.length < 3 ? 'Need 3 runes' : 'PvE Arena'}</small>
        </motion.button>

        <motion.button
          className="action-card"
          onClick={() => setPage('lab')}
          whileTap={{ scale: 0.95 }}
          disabled={ownedRunes.length < 2}
        >
          <FlaskConical size={32} />
          <span>Lab</span>
          <small>Fuse runes</small>
        </motion.button>

        <motion.button
          className="action-card"
          onClick={() => setPage('dex')}
          whileTap={{ scale: 0.95 }}
        >
          <BookOpen size={32} />
          <span>RuneDex</span>
          <small>21 runes</small>
        </motion.button>
      </div>

      {/* Collection preview */}
      {ownedRunes.length > 0 && (
        <motion.div
          className="collection-preview"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
        >
          <div className="preview-header">
            <span>Your Runes ({ownedRunes.length})</span>
            <button onClick={() => setPage('collection')}>View All →</button>
          </div>
          <div className="preview-runes">
            {ownedRunes.slice(0, 4).map(rune => {
              const species = getSpecies(rune.speciesId);
              return (
                <div
                  key={rune.id}
                  className="preview-rune"
                  style={{ borderColor: ELEMENT_COLORS[species?.element || 'arcane'] }}
                >
                  <span className="rune-name">{species?.name}</span>
                  <span className="rune-power">{rune.stats.power}</span>
                </div>
              );
            })}
          </div>
        </motion.div>
      )}
    </div>
  );
}

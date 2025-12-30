// Battle Page - Turn-based 3v3 Combat

import { useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Swords, Heart, Zap, ArrowLeftRight, Trophy, X, Flame } from 'lucide-react';
import { useGameStore } from '../store/gameStore';
import { getSpecies, ELEMENT_COLORS, ELEMENT_SYMBOLS, getElementMultiplier, RUNE_SPECIES, createOwnedRune, rollRarity } from '../data/runes';
import { getMovesForRune, calculateDamage } from '../data/moves';
import type { OwnedRune } from '../data/runes';
import type { Move, MoveEffect } from '../data/moves';

// Battle-specific rune state
interface BattleRune {
  rune: OwnedRune;
  currentHp: number;
  maxHp: number;
  isFainted: boolean;
  isBurned: boolean;
  statMods: {
    power: number;
    guard: number;
    speed: number;
  };
}

type BattlePhase = 'team-select' | 'battle' | 'action-select' | 'animating' | 'enemy-turn' | 'victory' | 'defeat';

interface BattleLog {
  text: string;
  type: 'info' | 'damage' | 'heal' | 'effect' | 'faint';
}

export function BattlePage() {
  const { ownedRunes, wins, losses, setPage } = useGameStore();

  // Team selection
  const [selectedTeam, setSelectedTeam] = useState<string[]>([]);

  // Battle state
  const [phase, setPhase] = useState<BattlePhase>('team-select');
  const [playerTeam, setPlayerTeam] = useState<BattleRune[]>([]);
  const [enemyTeam, setEnemyTeam] = useState<BattleRune[]>([]);
  const [activePlayerIdx, setActivePlayerIdx] = useState(0);
  const [activeEnemyIdx, setActiveEnemyIdx] = useState(0);
  const [battleEnergy, setBattleEnergy] = useState(3);
  const [battleLog, setBattleLog] = useState<BattleLog[]>([]);
  const [showSwitchMenu, setShowSwitchMenu] = useState(false);

  // Get active runes
  const activePlayer = playerTeam[activePlayerIdx];
  const activeEnemy = enemyTeam[activeEnemyIdx];

  // Initialize battle rune from owned rune
  const createBattleRune = (rune: OwnedRune): BattleRune => {
    const maxHp = rune.stats.guard * 3 + 50;
    return {
      rune,
      currentHp: maxHp,
      maxHp,
      isFainted: false,
      isBurned: false,
      statMods: { power: 0, guard: 0, speed: 0 },
    };
  };

  // Add to battle log
  const log = useCallback((text: string, type: BattleLog['type'] = 'info') => {
    setBattleLog(prev => [...prev.slice(-4), { text, type }]);
  }, []);

  // Toggle rune selection for team
  const toggleRune = (id: string) => {
    if (selectedTeam.includes(id)) {
      setSelectedTeam(selectedTeam.filter(r => r !== id));
    } else if (selectedTeam.length < 3) {
      setSelectedTeam([...selectedTeam, id]);
    }
  };

  // Start the battle
  const handleStartBattle = () => {
    if (selectedTeam.length !== 3) return;

    // Create player team
    const pTeam = selectedTeam.map(id => {
      const rune = ownedRunes.find(r => r.id === id)!;
      return createBattleRune(rune);
    });

    // Generate enemy team
    const avgPower = pTeam.reduce((sum, br) => sum + br.rune.stats.power, 0) / 3;
    const eTeam: BattleRune[] = [];
    for (let i = 0; i < 3; i++) {
      const species = RUNE_SPECIES[Math.floor(Math.random() * RUNE_SPECIES.length)];
      const enemy = createOwnedRune(species.id, rollRarity());
      // Scale enemy power
      const scale = avgPower / enemy.stats.power;
      enemy.stats.power = Math.floor(enemy.stats.power * (0.85 + scale * 0.3));
      enemy.stats.guard = Math.floor(enemy.stats.guard * (0.85 + scale * 0.3));
      eTeam.push(createBattleRune(enemy));
    }

    setPlayerTeam(pTeam);
    setEnemyTeam(eTeam);
    setActivePlayerIdx(0);
    setActiveEnemyIdx(0);
    setBattleEnergy(3);
    setBattleLog([]);
    setPhase('action-select');

    const playerSpecies = getSpecies(pTeam[0].rune.speciesId);
    const enemySpecies = getSpecies(eTeam[0].rune.speciesId);
    log(`Battle Start! ${playerSpecies?.name} vs ${enemySpecies?.name}!`, 'info');
  };

  // Execute a move
  const executeMove = useCallback((move: Move, attacker: BattleRune, defender: BattleRune) => {
    const attackerSpecies = getSpecies(attacker.rune.speciesId)!;
    const defenderSpecies = getSpecies(defender.rune.speciesId)!;

    // Check accuracy
    if (Math.random() * 100 > move.accuracy) {
      log(`${attackerSpecies.name}'s ${move.name} missed!`, 'info');
      return { hit: false, damage: 0 };
    }

    // Calculate element multiplier
    const elemMult = getElementMultiplier(move.element, defenderSpecies.element);

    // Calculate damage
    const power = attacker.rune.stats.power + attacker.statMods.power;
    const guard = defender.rune.stats.guard + defender.statMods.guard;
    const damage = calculateDamage(move, power, guard, elemMult);

    // Apply damage
    defender.currentHp = Math.max(0, defender.currentHp - damage);

    // Log damage
    let dmgText = `${attackerSpecies.name} used ${move.name}! ${damage} damage!`;
    if (elemMult > 1) dmgText += ' Super effective!';
    if (elemMult < 1) dmgText += ' Not very effective...';
    log(dmgText, 'damage');

    // Apply effects
    if (move.effect) {
      applyEffect(move.effect, attacker, defender, attackerSpecies.name, defenderSpecies.name);
    }

    // Check faint
    if (defender.currentHp <= 0) {
      defender.isFainted = true;
      log(`${defenderSpecies.name} fainted!`, 'faint');
    }

    return { hit: true, damage };
  }, [log]);

  // Apply move effects
  const applyEffect = (effect: MoveEffect, attacker: BattleRune, defender: BattleRune, atkName: string, defName: string) => {
    switch (effect.type) {
      case 'burn':
        if (Math.random() * 100 < effect.chance && !defender.isBurned) {
          defender.isBurned = true;
          log(`${defName} was burned!`, 'effect');
        }
        break;
      case 'heal':
        const healAmt = Math.floor(attacker.maxHp * (effect.amount / 100));
        attacker.currentHp = Math.min(attacker.maxHp, attacker.currentHp + healAmt);
        log(`${atkName} healed ${healAmt} HP!`, 'heal');
        break;
      case 'buff':
        attacker.statMods[effect.stat] += effect.amount;
        log(`${atkName}'s ${effect.stat} rose!`, 'effect');
        break;
      case 'debuff':
        defender.statMods[effect.stat] -= effect.amount;
        log(`${defName}'s ${effect.stat} fell!`, 'effect');
        break;
      case 'drain':
        const drainAmt = Math.floor(attacker.maxHp * (effect.percent / 100));
        attacker.currentHp = Math.min(attacker.maxHp, attacker.currentHp + drainAmt);
        log(`${atkName} drained HP!`, 'heal');
        break;
      case 'recoil':
        const recoilDmg = Math.floor(attacker.maxHp * (effect.percent / 100));
        attacker.currentHp = Math.max(1, attacker.currentHp - recoilDmg);
        log(`${atkName} took recoil damage!`, 'damage');
        break;
    }
  };

  // Apply burn damage at end of turn
  const applyBurnDamage = (rune: BattleRune) => {
    if (rune.isBurned && !rune.isFainted) {
      const burnDmg = Math.floor(rune.maxHp * 0.1);
      rune.currentHp = Math.max(0, rune.currentHp - burnDmg);
      const species = getSpecies(rune.rune.speciesId);
      log(`${species?.name} took ${burnDmg} burn damage!`, 'damage');
      if (rune.currentHp <= 0) {
        rune.isFainted = true;
        log(`${species?.name} fainted!`, 'faint');
      }
    }
  };

  // Check battle end
  const checkBattleEnd = useCallback(() => {
    const playerAlive = playerTeam.filter(r => !r.isFainted).length;
    const enemyAlive = enemyTeam.filter(r => !r.isFainted).length;

    if (enemyAlive === 0) {
      setPhase('victory');
      useGameStore.setState(s => ({ wins: s.wins + 1, sage: s.sage + 30 }));
      return true;
    }
    if (playerAlive === 0) {
      setPhase('defeat');
      useGameStore.setState(s => ({ losses: s.losses + 1, sage: s.sage + 5 }));
      return true;
    }
    return false;
  }, [playerTeam, enemyTeam]);

  // Find next alive rune
  const findNextAlive = (team: BattleRune[], currentIdx: number): number => {
    for (let i = 0; i < team.length; i++) {
      const idx = (currentIdx + 1 + i) % team.length;
      if (!team[idx].isFainted) return idx;
    }
    return -1;
  };

  // Player selects a move
  const handlePlayerMove = async (move: Move) => {
    if (phase !== 'action-select' || !activePlayer || !activeEnemy) return;

    // Check energy cost
    if (move.energyCost > battleEnergy) {
      log('Not enough energy!', 'info');
      return;
    }

    setBattleEnergy(e => e - move.energyCost);
    setPhase('animating');

    // Determine turn order by speed
    const playerSpeed = activePlayer.rune.stats.speed + activePlayer.statMods.speed;
    const enemySpeed = activeEnemy.rune.stats.speed + activeEnemy.statMods.speed;
    const playerFirst = move.effect?.type === 'priority' || playerSpeed >= enemySpeed;

    if (playerFirst) {
      // Player attacks first
      executeMove(move, activePlayer, activeEnemy);
      setPlayerTeam([...playerTeam]);
      setEnemyTeam([...enemyTeam]);

      await new Promise(r => setTimeout(r, 1000));

      if (!checkBattleEnd() && !activeEnemy.isFainted) {
        // Enemy attacks
        await enemyTurn();
      }
    } else {
      // Enemy attacks first
      await enemyTurn();
      await new Promise(r => setTimeout(r, 500));

      if (!checkBattleEnd() && !activePlayer.isFainted) {
        executeMove(move, activePlayer, activeEnemy);
        setPlayerTeam([...playerTeam]);
        setEnemyTeam([...enemyTeam]);
      }
    }

    await new Promise(r => setTimeout(r, 500));

    // Apply burn damage
    applyBurnDamage(activePlayer);
    applyBurnDamage(activeEnemy);
    setPlayerTeam([...playerTeam]);
    setEnemyTeam([...enemyTeam]);

    await new Promise(r => setTimeout(r, 500));

    // Check for faints and auto-switch
    if (!checkBattleEnd()) {
      if (activeEnemy.isFainted) {
        const nextEnemy = findNextAlive(enemyTeam, activeEnemyIdx);
        if (nextEnemy >= 0) {
          setActiveEnemyIdx(nextEnemy);
          const nextSpecies = getSpecies(enemyTeam[nextEnemy].rune.speciesId);
          log(`Enemy sends out ${nextSpecies?.name}!`, 'info');
        }
      }
      if (activePlayer.isFainted) {
        // Force player to switch
        const nextPlayer = findNextAlive(playerTeam, activePlayerIdx);
        if (nextPlayer >= 0) {
          setShowSwitchMenu(true);
        }
      } else {
        // Regen some energy
        setBattleEnergy(e => Math.min(3, e + 1));
        setPhase('action-select');
      }
    }
  };

  // Enemy AI turn
  const enemyTurn = async () => {
    if (!activeEnemy || activeEnemy.isFainted) return;

    const moves = getMovesForRune(activeEnemy.rune.speciesId);
    // Simple AI: use signature move if available and HP is good, otherwise basic
    const move = moves.length > 1 && activeEnemy.currentHp > activeEnemy.maxHp * 0.3
      ? moves[1]
      : moves[0];

    executeMove(move, activeEnemy, activePlayer);
    setPlayerTeam([...playerTeam]);
    setEnemyTeam([...enemyTeam]);
  };

  // Switch rune
  const handleSwitch = (idx: number) => {
    if (playerTeam[idx].isFainted || idx === activePlayerIdx) return;

    const oldSpecies = getSpecies(playerTeam[activePlayerIdx].rune.speciesId);
    const newSpecies = getSpecies(playerTeam[idx].rune.speciesId);
    log(`${oldSpecies?.name} switched out for ${newSpecies?.name}!`, 'info');

    setActivePlayerIdx(idx);
    setShowSwitchMenu(false);

    // If switching voluntarily (not forced), enemy gets a free hit
    if (phase === 'action-select') {
      setPhase('animating');
      setTimeout(async () => {
        await enemyTurn();
        applyBurnDamage(playerTeam[idx]);
        setPlayerTeam([...playerTeam]);
        if (!checkBattleEnd()) {
          setBattleEnergy(e => Math.min(3, e + 1));
          setPhase('action-select');
        }
      }, 500);
    } else {
      // Forced switch after faint
      setBattleEnergy(e => Math.min(3, e + 1));
      setPhase('action-select');
    }
  };

  // Reset battle
  const handleEndBattle = () => {
    setPhase('team-select');
    setSelectedTeam([]);
    setPlayerTeam([]);
    setEnemyTeam([]);
    setBattleLog([]);
  };

  // Not enough runes
  if (ownedRunes.length < 3) {
    return (
      <div className="page battle-page">
        <h2 className="page-title"><Swords size={24} /> Battle Arena</h2>
        <div className="not-enough">
          <p>You need at least 3 runes to battle!</p>
          <button onClick={() => setPage('encounter')}>Go Catch Some</button>
        </div>
      </div>
    );
  }

  return (
    <div className="page battle-page">
      <h2 className="page-title"><Swords size={24} /> Battle Arena</h2>

      <AnimatePresence mode="wait">
        {/* TEAM SELECTION */}
        {phase === 'team-select' && (
          <motion.div
            key="select"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="team-select-phase"
          >
            <div className="battle-record">
              <Trophy size={16} /> {wins}W / {losses}L
            </div>

            <p className="instruction">Select 3 runes for battle</p>

            <div className="selected-team">
              {[0, 1, 2].map(i => {
                const runeId = selectedTeam[i];
                const rune = runeId ? ownedRunes.find(r => r.id === runeId) : null;
                const species = rune ? getSpecies(rune.speciesId) : null;

                return (
                  <div
                    key={i}
                    className={`team-slot ${rune ? 'filled' : 'empty'}`}
                    style={species ? { borderColor: ELEMENT_COLORS[species.element] } : undefined}
                    onClick={() => runeId && toggleRune(runeId)}
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
                  </div>
                );
              })}
            </div>

            <div className="rune-select-grid">
              {ownedRunes.map(rune => {
                const species = getSpecies(rune.speciesId);
                if (!species) return null;
                const isSelected = selectedTeam.includes(rune.id);

                return (
                  <motion.button
                    key={rune.id}
                    className={`rune-select-card ${isSelected ? 'selected' : ''}`}
                    onClick={() => toggleRune(rune.id)}
                    style={{
                      borderColor: isSelected ? ELEMENT_COLORS[species.element] : '#444',
                      opacity: !isSelected && selectedTeam.length >= 3 ? 0.5 : 1,
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
              className="battle-start-btn"
              onClick={handleStartBattle}
              disabled={selectedTeam.length !== 3}
              whileTap={{ scale: 0.95 }}
            >
              <Swords size={20} />
              {selectedTeam.length === 3 ? 'Start Battle!' : `Select ${3 - selectedTeam.length} more`}
            </motion.button>
          </motion.div>
        )}

        {/* BATTLE PHASE */}
        {(phase === 'action-select' || phase === 'animating' || phase === 'enemy-turn') && activePlayer && activeEnemy && (
          <motion.div
            key="battle"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="battle-phase"
          >
            {/* Battle Field */}
            <div className="battle-field">
              {/* Enemy Side */}
              <div className="enemy-side">
                <RuneDisplay
                  battleRune={activeEnemy}
                  isEnemy
                  isActive={phase === 'enemy-turn'}
                />
                <div className="bench">
                  {enemyTeam.map((br, i) => i !== activeEnemyIdx && (
                    <div
                      key={i}
                      className={`bench-rune ${br.isFainted ? 'fainted' : ''}`}
                      style={{ borderColor: ELEMENT_COLORS[getSpecies(br.rune.speciesId)?.element || 'arcane'] }}
                    >
                      {br.isFainted ? <X size={12} /> : ELEMENT_SYMBOLS[getSpecies(br.rune.speciesId)?.element || 'arcane']}
                    </div>
                  ))}
                </div>
              </div>

              {/* VS */}
              <div className="vs-divider">
                <Swords size={24} />
              </div>

              {/* Player Side */}
              <div className="player-side">
                <RuneDisplay
                  battleRune={activePlayer}
                  isActive={phase === 'action-select'}
                />
                <div className="bench">
                  {playerTeam.map((br, i) => i !== activePlayerIdx && (
                    <div
                      key={i}
                      className={`bench-rune ${br.isFainted ? 'fainted' : ''}`}
                      style={{ borderColor: ELEMENT_COLORS[getSpecies(br.rune.speciesId)?.element || 'arcane'] }}
                      onClick={() => !br.isFainted && phase === 'action-select' && setShowSwitchMenu(true)}
                    >
                      {br.isFainted ? <X size={12} /> : ELEMENT_SYMBOLS[getSpecies(br.rune.speciesId)?.element || 'arcane']}
                    </div>
                  ))}
                </div>
              </div>
            </div>

            {/* Battle Log */}
            <div className="battle-log">
              {battleLog.slice(-3).map((entry, i) => (
                <motion.div
                  key={i}
                  className={`log-entry ${entry.type}`}
                  initial={{ opacity: 0, x: -10 }}
                  animate={{ opacity: 1, x: 0 }}
                >
                  {entry.text}
                </motion.div>
              ))}
            </div>

            {/* Energy Display */}
            <div className="battle-energy">
              <Zap size={16} />
              {[...Array(3)].map((_, i) => (
                <div key={i} className={`energy-pip ${i < battleEnergy ? 'full' : ''}`} />
              ))}
            </div>

            {/* Action Buttons */}
            {phase === 'action-select' && !showSwitchMenu && (
              <div className="action-buttons">
                {getMovesForRune(activePlayer.rune.speciesId).map(move => {
                  const canUse = move.energyCost <= battleEnergy;
                  return (
                    <motion.button
                      key={move.id}
                      className={`move-btn ${!canUse ? 'disabled' : ''}`}
                      style={{ borderColor: ELEMENT_COLORS[move.element] }}
                      onClick={() => canUse && handlePlayerMove(move)}
                      whileTap={canUse ? { scale: 0.95 } : undefined}
                      disabled={!canUse}
                    >
                      <span className="move-name">{move.name}</span>
                      <span className="move-info">
                        <span style={{ color: ELEMENT_COLORS[move.element] }}>{ELEMENT_SYMBOLS[move.element]}</span>
                        <span>{move.power}</span>
                        {move.energyCost > 0 && <span className="cost">⚡{move.energyCost}</span>}
                      </span>
                    </motion.button>
                  );
                })}
                <motion.button
                  className="switch-btn"
                  onClick={() => setShowSwitchMenu(true)}
                  whileTap={{ scale: 0.95 }}
                >
                  <ArrowLeftRight size={16} /> Switch
                </motion.button>
              </div>
            )}

            {/* Switch Menu */}
            {showSwitchMenu && (
              <div className="switch-menu">
                <p>Switch to:</p>
                <div className="switch-options">
                  {playerTeam.map((br, i) => {
                    if (i === activePlayerIdx) return null;
                    const species = getSpecies(br.rune.speciesId);
                    return (
                      <motion.button
                        key={i}
                        className={`switch-option ${br.isFainted ? 'fainted' : ''}`}
                        style={{ borderColor: ELEMENT_COLORS[species?.element || 'arcane'] }}
                        onClick={() => !br.isFainted && handleSwitch(i)}
                        disabled={br.isFainted}
                        whileTap={{ scale: 0.95 }}
                      >
                        <span style={{ color: ELEMENT_COLORS[species?.element || 'arcane'] }}>
                          {ELEMENT_SYMBOLS[species?.element || 'arcane']}
                        </span>
                        <span>{species?.name}</span>
                        <span className="hp-text">{br.currentHp}/{br.maxHp}</span>
                      </motion.button>
                    );
                  })}
                </div>
                {phase === 'action-select' && (
                  <button className="cancel-switch" onClick={() => setShowSwitchMenu(false)}>
                    Cancel
                  </button>
                )}
              </div>
            )}

            {phase === 'animating' && (
              <div className="animating-indicator">
                <motion.div
                  animate={{ rotate: 360 }}
                  transition={{ duration: 1, repeat: Infinity, ease: 'linear' }}
                >
                  ⚔️
                </motion.div>
              </div>
            )}
          </motion.div>
        )}

        {/* VICTORY */}
        {phase === 'victory' && (
          <motion.div
            key="victory"
            className="battle-result victory"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
          >
            <Trophy size={64} className="trophy-icon" />
            <h2>Victory!</h2>
            <p className="reward">+30 SAGE</p>
            <motion.button
              className="continue-btn"
              onClick={handleEndBattle}
              whileTap={{ scale: 0.95 }}
            >
              Continue
            </motion.button>
          </motion.div>
        )}

        {/* DEFEAT */}
        {phase === 'defeat' && (
          <motion.div
            key="defeat"
            className="battle-result defeat"
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
          >
            <X size={64} className="defeat-icon" />
            <h2>Defeat...</h2>
            <p className="reward">+5 SAGE</p>
            <motion.button
              className="continue-btn"
              onClick={handleEndBattle}
              whileTap={{ scale: 0.95 }}
            >
              Try Again
            </motion.button>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// Rune display component
function RuneDisplay({ battleRune, isEnemy = false, isActive = false }: {
  battleRune: BattleRune;
  isEnemy?: boolean;
  isActive?: boolean;
}) {
  const species = getSpecies(battleRune.rune.speciesId);
  if (!species) return null;

  const hpPercent = (battleRune.currentHp / battleRune.maxHp) * 100;
  const hpColor = hpPercent > 50 ? '#00d084' : hpPercent > 25 ? '#ffaa00' : '#ff4444';

  return (
    <motion.div
      className={`rune-display ${isEnemy ? 'enemy' : 'player'} ${isActive ? 'active' : ''}`}
      animate={isActive ? { scale: [1, 1.05, 1] } : {}}
      transition={{ duration: 0.5 }}
    >
      <div className="rune-info-bar">
        <span className="rune-name">{species.name}</span>
        {battleRune.isBurned && <Flame size={12} className="burn-icon" />}
      </div>

      <div className="hp-bar-container">
        <motion.div
          className="hp-bar"
          style={{ background: hpColor }}
          animate={{ width: `${hpPercent}%` }}
        />
      </div>
      <div className="hp-text">
        <Heart size={12} /> {battleRune.currentHp}/{battleRune.maxHp}
      </div>

      <motion.div
        className="rune-sprite"
        style={{
          color: ELEMENT_COLORS[species.element],
          textShadow: `0 0 20px ${ELEMENT_COLORS[species.element]}`,
        }}
        animate={isActive ? { y: [0, -5, 0] } : {}}
        transition={{ duration: 0.5, repeat: isActive ? Infinity : 0 }}
      >
        {ELEMENT_SYMBOLS[species.element]}
      </motion.div>
    </motion.div>
  );
}

// Daily Challenges Component
// Shows 3 daily challenges with progress and streak info

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Target, Flame, Clock, Gift, Check, Coins } from 'lucide-react';
import { useGameStore } from '../store/gameStore';
import {
  getChallengeIcon,
  allChallengesCompleted,
  getUnclaimedRewards,
  getNextStreakMilestone,
  getTimeUntilDailyReset,
  formatTimeRemaining,
  STREAK_REWARDS,
} from '../data/challenges';

export function DailyChallenges() {
  const {
    dailyChallenges,
    dailyStreak,
    streakRewardClaimed,
    checkDailyReset,
    claimAllChallengeRewards,
    claimStreakReward,
  } = useGameStore();

  const [showRewardAnimation, setShowRewardAnimation] = useState(false);
  const [claimedAmount, setClaimedAmount] = useState(0);
  const [timeUntilReset, setTimeUntilReset] = useState(getTimeUntilDailyReset());

  // Check daily reset on mount
  useEffect(() => {
    checkDailyReset();
  }, [checkDailyReset]);

  // Update timer
  useEffect(() => {
    const timer = setInterval(() => {
      setTimeUntilReset(getTimeUntilDailyReset());
    }, 60000);
    return () => clearInterval(timer);
  }, []);

  const allComplete = allChallengesCompleted(dailyChallenges);
  const unclaimedRewards = getUnclaimedRewards(dailyChallenges);
  const nextMilestone = getNextStreakMilestone(dailyStreak);

  const handleClaimAll = () => {
    const amount = claimAllChallengeRewards();
    if (amount > 0) {
      setClaimedAmount(amount);
      setShowRewardAnimation(true);
      setTimeout(() => setShowRewardAnimation(false), 2000);
    }
  };

  const handleClaimStreak = () => {
    const reward = claimStreakReward();
    if (reward) {
      setClaimedAmount(reward.sage);
      setShowRewardAnimation(true);
      setTimeout(() => setShowRewardAnimation(false), 2000);
    }
  };

  return (
    <div className="daily-challenges">
      {/* Header */}
      <div className="challenges-header">
        <div className="challenges-title">
          <Target size={18} />
          <span>Daily Challenges</span>
        </div>
        <div className="challenges-timer">
          <Clock size={14} />
          <span>{formatTimeRemaining(timeUntilReset.hours, timeUntilReset.minutes)}</span>
        </div>
      </div>

      {/* Streak Display */}
      <div className="streak-display">
        <div className="streak-flame">
          <Flame size={20} color={dailyStreak > 0 ? '#ff6b35' : '#666'} />
          <span className="streak-count">{dailyStreak}</span>
        </div>
        <div className="streak-milestones">
          {[1, 3, 5, 7].map(milestone => (
            <div
              key={milestone}
              className={`milestone-dot ${dailyStreak >= milestone ? 'reached' : ''} ${milestone === nextMilestone ? 'next' : ''}`}
              title={STREAK_REWARDS[milestone]?.description}
            >
              {milestone}
            </div>
          ))}
        </div>
        {allComplete && !streakRewardClaimed && (
          <motion.button
            className="streak-claim-btn"
            onClick={handleClaimStreak}
            whileTap={{ scale: 0.95 }}
            initial={{ scale: 0 }}
            animate={{ scale: 1 }}
          >
            <Gift size={14} /> Claim Streak
          </motion.button>
        )}
      </div>

      {/* Challenge Cards */}
      <div className="challenge-cards">
        {dailyChallenges.map((challenge, index) => (
          <motion.div
            key={challenge.id}
            className={`challenge-card ${challenge.completed ? 'completed' : ''} ${challenge.claimed ? 'claimed' : ''}`}
            initial={{ opacity: 0, x: -20 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: index * 0.1 }}
          >
            <div className="challenge-icon">
              {getChallengeIcon(challenge)}
            </div>
            <div className="challenge-content">
              <div className="challenge-desc">{challenge.description}</div>
              <div className="challenge-progress-bar">
                <motion.div
                  className="challenge-progress-fill"
                  initial={{ width: 0 }}
                  animate={{ width: `${(challenge.current / challenge.target) * 100}%` }}
                />
              </div>
              <div className="challenge-progress-text">
                {challenge.current}/{challenge.target}
              </div>
            </div>
            <div className="challenge-reward">
              {challenge.claimed ? (
                <Check size={18} className="claimed-check" />
              ) : (
                <span className="reward-amount">+{challenge.reward}</span>
              )}
            </div>
          </motion.div>
        ))}
      </div>

      {/* Claim All Button */}
      {unclaimedRewards > 0 && (
        <motion.button
          className="claim-all-btn"
          onClick={handleClaimAll}
          whileTap={{ scale: 0.95 }}
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
        >
          <Coins size={18} />
          Claim All (+{unclaimedRewards} SAGE)
        </motion.button>
      )}

      {/* Reward Animation */}
      <AnimatePresence>
        {showRewardAnimation && (
          <motion.div
            className="reward-popup"
            initial={{ opacity: 0, scale: 0.5, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.5, y: -20 }}
          >
            <Coins size={24} />
            <span>+{claimedAmount} SAGE</span>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

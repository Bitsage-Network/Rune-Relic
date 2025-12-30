// Rune Tracing Mini-Game
// Draw/trace the glowing pattern to catch the rune

import { useState, useRef, useEffect, useCallback } from 'react';
import { motion } from 'framer-motion';
import type { MiniGameProps } from './types';
import { calculateBonus } from './types';
import { ELEMENT_COLORS } from '../../data/runes';
import type { RuneElement } from '../../data/runes';

// Rune patterns - simple geometric shapes for tracing
const PATTERNS: Record<string, { points: [number, number][]; name: string }> = {
  fire: { points: [[50, 80], [50, 20], [20, 50], [50, 20], [80, 50]], name: 'Flame' },
  water: { points: [[20, 40], [50, 70], [80, 40], [50, 10], [20, 40]], name: 'Wave' },
  earth: { points: [[20, 20], [80, 20], [80, 80], [20, 80], [20, 20]], name: 'Stone' },
  air: { points: [[50, 20], [80, 50], [60, 80], [40, 80], [20, 50], [50, 20]], name: 'Wind' },
  light: { points: [[50, 10], [60, 40], [90, 50], [60, 60], [50, 90], [40, 60], [10, 50], [40, 40], [50, 10]], name: 'Star' },
  void: { points: [[50, 20], [70, 35], [70, 65], [50, 80], [30, 65], [30, 35], [50, 20]], name: 'Void' },
  arcane: { points: [[50, 10], [65, 30], [90, 35], [70, 55], [75, 80], [50, 65], [25, 80], [30, 55], [10, 35], [35, 30], [50, 10]], name: 'Arcane' },
};

export function RuneTracing({ element, onComplete, onCancel }: MiniGameProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [isDrawing, setIsDrawing] = useState(false);
  const [drawnPoints, setDrawnPoints] = useState<[number, number][]>([]);
  const [currentPointIndex, setCurrentPointIndex] = useState(0);
  const [showHint, setShowHint] = useState(true);
  const [timeLeft, setTimeLeft] = useState(10);
  const [completed, setCompleted] = useState(false);

  const pattern = PATTERNS[element as keyof typeof PATTERNS] || PATTERNS.arcane;
  const color = ELEMENT_COLORS[element as RuneElement] || '#00ffaa';

  // Timer
  useEffect(() => {
    if (completed) return;

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

    // Hide hint after 2 seconds
    const hintTimer = setTimeout(() => setShowHint(false), 2000);

    return () => {
      clearInterval(timer);
      clearTimeout(hintTimer);
    };
  }, [completed]);

  // Draw the pattern guide
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const scale = canvas.width / 100;

    ctx.clearRect(0, 0, canvas.width, canvas.height);

    // Draw guide pattern (faded)
    if (showHint) {
      ctx.strokeStyle = `${color}40`;
      ctx.lineWidth = 20 * scale;
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.beginPath();
      pattern.points.forEach((p, i) => {
        if (i === 0) ctx.moveTo(p[0] * scale, p[1] * scale);
        else ctx.lineTo(p[0] * scale, p[1] * scale);
      });
      ctx.stroke();
    }

    // Draw checkpoints
    pattern.points.forEach((p, i) => {
      const isReached = i < currentPointIndex;
      const isCurrent = i === currentPointIndex;

      ctx.beginPath();
      ctx.arc(p[0] * scale, p[1] * scale, (isCurrent ? 15 : 10) * scale, 0, Math.PI * 2);
      ctx.fillStyle = isReached ? color : isCurrent ? `${color}80` : `${color}30`;
      ctx.fill();

      if (isCurrent) {
        ctx.strokeStyle = color;
        ctx.lineWidth = 2;
        ctx.stroke();
      }
    });

    // Draw user's path
    if (drawnPoints.length > 1) {
      ctx.strokeStyle = color;
      ctx.lineWidth = 8 * scale;
      ctx.lineCap = 'round';
      ctx.beginPath();
      drawnPoints.forEach((p, i) => {
        if (i === 0) ctx.moveTo(p[0], p[1]);
        else ctx.lineTo(p[0], p[1]);
      });
      ctx.stroke();
    }
  }, [pattern, color, showHint, currentPointIndex, drawnPoints]);

  const getCanvasPoint = useCallback((e: React.TouchEvent | React.MouseEvent): [number, number] => {
    const canvas = canvasRef.current;
    if (!canvas) return [0, 0];

    const rect = canvas.getBoundingClientRect();
    const clientX = 'touches' in e ? e.touches[0].clientX : e.clientX;
    const clientY = 'touches' in e ? e.touches[0].clientY : e.clientY;

    return [
      clientX - rect.left,
      clientY - rect.top,
    ];
  }, []);

  const checkPointHit = useCallback((point: [number, number]) => {
    const canvas = canvasRef.current;
    if (!canvas || currentPointIndex >= pattern.points.length) return;

    const scale = canvas.width / 100;
    const target = pattern.points[currentPointIndex];
    const targetX = target[0] * scale;
    const targetY = target[1] * scale;

    const distance = Math.sqrt(
      Math.pow(point[0] - targetX, 2) + Math.pow(point[1] - targetY, 2)
    );

    const hitRadius = 25 * scale;

    if (distance < hitRadius) {
      const newIndex = currentPointIndex + 1;
      setCurrentPointIndex(newIndex);

      if (newIndex >= pattern.points.length) {
        handleComplete(true);
      }
    }
  }, [currentPointIndex, pattern]);

  const handleComplete = (success: boolean) => {
    if (completed) return;
    setCompleted(true);

    const score = success
      ? Math.floor(50 + (timeLeft / 10) * 50) // 50-100 based on time
      : Math.floor((currentPointIndex / pattern.points.length) * 30); // 0-30 for partial

    setTimeout(() => {
      onComplete({
        success,
        score,
        bonus: calculateBonus('trace', score),
      });
    }, 500);
  };

  const handleStart = (e: React.TouchEvent | React.MouseEvent) => {
    e.preventDefault();
    setIsDrawing(true);
    const point = getCanvasPoint(e);
    setDrawnPoints([point]);
    checkPointHit(point);
  };

  const handleMove = (e: React.TouchEvent | React.MouseEvent) => {
    if (!isDrawing) return;
    e.preventDefault();
    const point = getCanvasPoint(e);
    setDrawnPoints(prev => [...prev, point]);
    checkPointHit(point);
  };

  const handleEnd = () => {
    setIsDrawing(false);
  };

  return (
    <motion.div
      className="minigame trace-game"
      initial={{ opacity: 0, scale: 0.9 }}
      animate={{ opacity: 1, scale: 1 }}
    >
      <div className="game-header">
        <h3>Trace the {pattern.name}</h3>
        <div className="timer" style={{ color: timeLeft <= 3 ? '#ff4444' : color }}>
          {timeLeft}s
        </div>
      </div>

      <div className="progress-dots">
        {pattern.points.map((_, i) => (
          <div
            key={i}
            className={`dot ${i < currentPointIndex ? 'complete' : ''}`}
            style={{ background: i < currentPointIndex ? color : '#333' }}
          />
        ))}
      </div>

      <canvas
        ref={canvasRef}
        width={280}
        height={280}
        className="trace-canvas"
        onTouchStart={handleStart}
        onTouchMove={handleMove}
        onTouchEnd={handleEnd}
        onMouseDown={handleStart}
        onMouseMove={handleMove}
        onMouseUp={handleEnd}
        onMouseLeave={handleEnd}
      />

      <p className="hint">
        {showHint ? 'Follow the pattern!' : 'Connect all the points!'}
      </p>

      <button className="cancel-btn" onClick={onCancel}>
        Give Up
      </button>
    </motion.div>
  );
}

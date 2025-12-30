// Rune Relic - Pokemon-style Collection Game for Telegram

import { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { initTelegramApp, getTelegramUser } from './lib/telegram';
import { useGameStore } from './store/gameStore';
import { NavBar } from './components/NavBar';
import { HomePage } from './pages/HomePage';
import { DexPage } from './pages/DexPage';
import { EncounterPage } from './pages/EncounterPage';
import { BattlePage } from './pages/BattlePage';
import { LabPage } from './pages/LabPage';
import { CollectionPage } from './pages/CollectionPage';
import { BossPage } from './pages/BossPage';
import './App.css';

function App() {
  const [isLoading, setIsLoading] = useState(true);
  const { currentPage, setUsername, regenerateEnergy } = useGameStore();

  useEffect(() => {
    async function init() {
      await initTelegramApp();

      const user = getTelegramUser();
      if (user) {
        setUsername(user.firstName);
      }

      regenerateEnergy();

      await new Promise((r) => setTimeout(r, 800));
      setIsLoading(false);
    }

    init();
  }, [setUsername, regenerateEnergy]);

  if (isLoading) {
    return <LoadingScreen />;
  }

  return (
    <div className="app">
      <div className="app-content">
        <AnimatePresence mode="wait">
          <motion.div
            key={currentPage}
            initial={{ opacity: 0, x: 20 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: -20 }}
            transition={{ duration: 0.15 }}
            className="page-container"
          >
            {currentPage === 'home' && <HomePage />}
            {currentPage === 'dex' && <DexPage />}
            {currentPage === 'encounter' && <EncounterPage />}
            {currentPage === 'battle' && <BattlePage />}
            {currentPage === 'lab' && <LabPage />}
            {currentPage === 'collection' && <CollectionPage />}
            {currentPage === 'boss' && <BossPage />}
          </motion.div>
        </AnimatePresence>
      </div>
      <NavBar />
    </div>
  );
}

function LoadingScreen() {
  return (
    <div className="loading-screen">
      <motion.div
        className="loading-content"
        initial={{ opacity: 0, scale: 0.8 }}
        animate={{ opacity: 1, scale: 1 }}
      >
        <motion.div
          className="loading-rune"
          animate={{ rotate: 360 }}
          transition={{ duration: 2, repeat: Infinity, ease: 'linear' }}
        >
          ⚡
        </motion.div>
        <h1>RUNE RELIC</h1>
        <p>Catch 'em all!</p>
        <motion.div
          className="loading-bar"
          initial={{ width: 0 }}
          animate={{ width: '100%' }}
          transition={{ duration: 0.6 }}
        />
      </motion.div>
    </div>
  );
}

export default App;

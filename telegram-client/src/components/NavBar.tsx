// Bottom Navigation Bar

import { motion } from 'framer-motion';
import { Home, BookOpen, Search, Swords, FlaskConical } from 'lucide-react';
import { useGameStore } from '../store/gameStore';
import { hapticFeedback } from '../lib/telegram';

type Page = 'home' | 'dex' | 'encounter' | 'battle' | 'lab';

const navItems: { page: Page; icon: typeof Home; label: string }[] = [
  { page: 'home', icon: Home, label: 'Home' },
  { page: 'encounter', icon: Search, label: 'Explore' },
  { page: 'dex', icon: BookOpen, label: 'Dex' },
  { page: 'battle', icon: Swords, label: 'Battle' },
  { page: 'lab', icon: FlaskConical, label: 'Lab' },
];

export function NavBar() {
  const { currentPage, setPage } = useGameStore();

  const handleNavClick = (page: Page) => {
    hapticFeedback('light');
    setPage(page);
  };

  return (
    <nav className="nav-bar">
      {navItems.map(({ page, icon: Icon, label }) => (
        <motion.button
          key={page}
          className={`nav-item ${currentPage === page ? 'active' : ''}`}
          onClick={() => handleNavClick(page)}
          whileTap={{ scale: 0.9 }}
        >
          <Icon size={22} />
          <span>{label}</span>
        </motion.button>
      ))}
    </nav>
  );
}

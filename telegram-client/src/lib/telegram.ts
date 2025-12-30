// Telegram WebApp Integration for Rune Relic

import { init, miniApp, viewport, backButton } from '@telegram-apps/sdk';

export interface TelegramUser {
  id: number;
  firstName: string;
  lastName?: string;
  username?: string;
  languageCode?: string;
  photoUrl?: string;
}

let initialized = false;

export async function initTelegramApp(): Promise<boolean> {
  if (initialized) return true;

  try {
    // Initialize the SDK
    init();

    // Mount mini app components
    if (miniApp.mount.isAvailable()) {
      await miniApp.mount();
    }

    // Expand to full height
    if (viewport.mount.isAvailable()) {
      await viewport.mount();
      if (viewport.expand.isAvailable()) {
        viewport.expand();
      }
    }

    // Set up back button
    if (backButton.mount.isAvailable()) {
      await backButton.mount();
    }

    initialized = true;
    console.log('Telegram Mini App initialized');
    return true;
  } catch (error) {
    console.warn('Running outside Telegram, using mock mode:', error);
    initialized = true;
    return false;
  }
}

export function getTelegramUser(): TelegramUser | null {
  try {
    // Try to get user from launch params
    const urlParams = new URLSearchParams(window.location.search);
    const initData = urlParams.get('tgWebAppData');

    if (initData) {
      const params = new URLSearchParams(initData);
      const userStr = params.get('user');
      if (userStr) {
        const user = JSON.parse(decodeURIComponent(userStr));
        return {
          id: user.id,
          firstName: user.first_name,
          lastName: user.last_name,
          username: user.username,
          languageCode: user.language_code,
          photoUrl: user.photo_url,
        };
      }
    }

    // Mock user for development
    return {
      id: 12345678,
      firstName: 'SAGE',
      lastName: 'Wizard',
      username: 'sage_wizard',
      languageCode: 'en',
    };
  } catch (error) {
    console.warn('Failed to get Telegram user:', error);
    return null;
  }
}

export function showMainButton(_text: string, _onClick: () => void): void {
  try {
    if (miniApp.setHeaderColor.isAvailable()) {
      miniApp.setHeaderColor('#1a1a2e');
    }
  } catch (error) {
    console.warn('Main button not available:', error);
  }
}

export function hideMainButton(): void {
  // Handled by SDK
}

export function hapticFeedback(type: 'light' | 'medium' | 'heavy' | 'success' | 'error'): void {
  try {
    // Telegram haptic feedback
    if (window.Telegram?.WebApp?.HapticFeedback) {
      switch (type) {
        case 'light':
        case 'medium':
        case 'heavy':
          window.Telegram.WebApp.HapticFeedback.impactOccurred(type);
          break;
        case 'success':
          window.Telegram.WebApp.HapticFeedback.notificationOccurred('success');
          break;
        case 'error':
          window.Telegram.WebApp.HapticFeedback.notificationOccurred('error');
          break;
      }
    }
  } catch {
    // Haptic not available
  }
}

export function getThemeColors() {
  // Return default theme colors - Telegram theme integration can be added later
  return {
    bg: '#1a1a2e',
    text: '#ffffff',
    hint: '#888888',
    link: '#6c5ce7',
    button: '#6c5ce7',
    buttonText: '#ffffff',
  };
}

// Extend window for Telegram WebApp
declare global {
  interface Window {
    Telegram?: {
      WebApp?: {
        HapticFeedback?: {
          impactOccurred: (style: 'light' | 'medium' | 'heavy') => void;
          notificationOccurred: (type: 'error' | 'success' | 'warning') => void;
        };
      };
    };
  }
}

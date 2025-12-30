using UnityEngine;

namespace RuneRelic.UI.Core
{
    /// <summary>
    /// Defines the visual theme for the game UI.
    /// Create ScriptableObject assets for different themes.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "RuneRelic/UI Theme")]
    public class UITheme : ScriptableObject
    {
        [Header("Primary Colors")]
        public Color primaryColor = new Color(0.2f, 0.6f, 1f);      // Main accent
        public Color secondaryColor = new Color(0.8f, 0.4f, 1f);    // Secondary accent
        public Color tertiaryColor = new Color(0.4f, 1f, 0.8f);     // Highlights

        [Header("Background Colors")]
        public Color backgroundDark = new Color(0.05f, 0.05f, 0.1f);
        public Color backgroundMedium = new Color(0.1f, 0.1f, 0.15f);
        public Color backgroundLight = new Color(0.15f, 0.15f, 0.2f);
        public Color panelColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        [Header("Text Colors")]
        public Color textPrimary = Color.white;
        public Color textSecondary = new Color(0.7f, 0.7f, 0.8f);
        public Color textMuted = new Color(0.5f, 0.5f, 0.6f);
        public Color textHighlight = new Color(1f, 0.9f, 0.3f);

        [Header("State Colors")]
        public Color successColor = new Color(0.3f, 1f, 0.5f);
        public Color warningColor = new Color(1f, 0.8f, 0.3f);
        public Color dangerColor = new Color(1f, 0.3f, 0.3f);
        public Color infoColor = new Color(0.3f, 0.7f, 1f);

        [Header("Form Colors")]
        public Color sparkColor = new Color(0.9f, 0.95f, 1f);
        public Color glyphColor = new Color(0.6f, 0.8f, 1f);
        public Color wardColor = new Color(0.4f, 0.9f, 0.6f);
        public Color arcaneColor = new Color(0.7f, 0.4f, 1f);
        public Color ancientColor = new Color(1f, 0.8f, 0.3f);

        [Header("Rune Colors")]
        public Color wisdomRuneColor = new Color(0.3f, 0.5f, 1f);
        public Color powerRuneColor = new Color(1f, 0.3f, 0.3f);
        public Color speedRuneColor = new Color(1f, 1f, 0.3f);
        public Color shieldRuneColor = new Color(0.3f, 1f, 0.5f);
        public Color arcaneRuneColor = new Color(0.7f, 0.3f, 1f);
        public Color chaosRuneColor = Color.white;

        [Header("Button Styles")]
        public Color buttonNormal = new Color(0.2f, 0.2f, 0.3f);
        public Color buttonHover = new Color(0.3f, 0.3f, 0.4f);
        public Color buttonPressed = new Color(0.15f, 0.15f, 0.25f);
        public Color buttonDisabled = new Color(0.15f, 0.15f, 0.2f);

        [Header("Border & Glow")]
        public Color borderColor = new Color(0.3f, 0.3f, 0.4f);
        public Color glowColor = new Color(0.4f, 0.6f, 1f, 0.5f);
        public float borderWidth = 2f;
        public float cornerRadius = 8f;

        [Header("Fonts")]
        public Font headerFont;
        public Font bodyFont;
        public Font monoFont;

        [Header("Font Sizes")]
        public int fontSizeTitle = 48;
        public int fontSizeHeader = 32;
        public int fontSizeSubheader = 24;
        public int fontSizeBody = 18;
        public int fontSizeSmall = 14;
        public int fontSizeCaption = 12;

        [Header("Spacing")]
        public float paddingSmall = 8f;
        public float paddingMedium = 16f;
        public float paddingLarge = 24f;
        public float marginSmall = 4f;
        public float marginMedium = 8f;
        public float marginLarge = 16f;

        [Header("Animation")]
        public float transitionDuration = 0.2f;
        public float hoverScale = 1.05f;
        public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        /// <summary>
        /// Get color for a specific form tier.
        /// </summary>
        public Color GetFormColor(int formIndex)
        {
            return formIndex switch
            {
                0 => sparkColor,
                1 => glyphColor,
                2 => wardColor,
                3 => arcaneColor,
                4 => ancientColor,
                _ => textPrimary
            };
        }

        /// <summary>
        /// Get color for a specific rune type.
        /// </summary>
        public Color GetRuneColor(int runeType)
        {
            return runeType switch
            {
                0 => wisdomRuneColor,
                1 => powerRuneColor,
                2 => speedRuneColor,
                3 => shieldRuneColor,
                4 => arcaneRuneColor,
                5 => chaosRuneColor,
                _ => textPrimary
            };
        }
    }
}

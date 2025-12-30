using UnityEngine;
using UnityEngine.UI;
using RuneRelic.Game;
using RuneRelic.Utils;

namespace RuneRelic.UI
{
    /// <summary>
    /// In-game heads-up display showing score, form, time, buffs, and ability cooldown.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Score & Form")]
        [SerializeField] private Text scoreText;
        [SerializeField] private Text formText;
        [SerializeField] private Image formIcon;
        [SerializeField] private Slider evolutionProgress;

        [Header("Match Info")]
        [SerializeField] private Text timeText;
        [SerializeField] private Text playersAliveText;
        [SerializeField] private Text placementText;

        [Header("Buffs")]
        [SerializeField] private GameObject speedBuffIndicator;
        [SerializeField] private GameObject shieldBuffIndicator;
        [SerializeField] private GameObject invulnerableIndicator;
        [SerializeField] private Text speedBuffTimer;
        [SerializeField] private Text shieldBuffTimer;

        [Header("Ability")]
        [SerializeField] private Image abilityCooldownFill;
        [SerializeField] private Text abilityNameText;

        [Header("Countdown")]
        [SerializeField] private GameObject countdownPanel;
        [SerializeField] private Text countdownText;

        [Header("Kill Feed")]
        [SerializeField] private Transform killFeedContainer;
        [SerializeField] private GameObject killFeedEntryPrefab;
        [SerializeField] private int maxKillFeedEntries = 5;

        // State
        private byte[] _localPlayerId;
        private string _localPlayerIdHex;

        private void Start()
        {
            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.OnCountdown += HandleCountdown;
                gameManager.OnMatchStarted += HandleMatchStarted;
                gameManager.OnPlayerEvolved += HandlePlayerEvolved;
                gameManager.OnPlayerEliminated += HandlePlayerEliminated;
            }

            // Hide countdown initially
            if (countdownPanel != null)
                countdownPanel.SetActive(false);

            // Hide buff indicators
            SetBuffIndicator(speedBuffIndicator, false);
            SetBuffIndicator(shieldBuffIndicator, false);
            SetBuffIndicator(invulnerableIndicator, false);
        }

        private void OnDestroy()
        {
            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.OnCountdown -= HandleCountdown;
                gameManager.OnMatchStarted -= HandleMatchStarted;
                gameManager.OnPlayerEvolved -= HandlePlayerEvolved;
                gameManager.OnPlayerEliminated -= HandlePlayerEliminated;
            }
        }

        private void Update()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null || !gameManager.IsInMatch) return;

            var matchState = gameManager.CurrentMatch;
            if (matchState == null) return;

            UpdateLocalPlayerUI(matchState);
            UpdateMatchInfo(matchState);
        }

        public void SetLocalPlayerId(byte[] playerId)
        {
            _localPlayerId = playerId;
            _localPlayerIdHex = BytesToHex(playerId);
        }

        private void UpdateLocalPlayerUI(MatchState matchState)
        {
            if (string.IsNullOrEmpty(_localPlayerIdHex)) return;

            if (!matchState.Players.TryGetValue(_localPlayerIdHex, out var playerState))
                return;

            // Score
            if (scoreText != null)
                scoreText.text = playerState.Score.ToString();

            // Form
            if (formText != null)
                formText.text = Constants.FORM_NAMES[(int)playerState.Form];

            // Evolution progress
            if (evolutionProgress != null)
            {
                int currentThreshold = Constants.EVOLUTION_THRESHOLDS[(int)playerState.Form];
                int nextThreshold = (int)playerState.Form < 4
                    ? Constants.EVOLUTION_THRESHOLDS[(int)playerState.Form + 1]
                    : Constants.EVOLUTION_THRESHOLDS[4];

                float progress = (float)(playerState.Score - currentThreshold) / (nextThreshold - currentThreshold);
                evolutionProgress.value = Mathf.Clamp01(progress);
                evolutionProgress.gameObject.SetActive((int)playerState.Form < 4);
            }

            // Ability
            if (abilityCooldownFill != null)
            {
                float maxCooldown = Constants.ABILITY_COOLDOWNS[(int)playerState.Form];
                abilityCooldownFill.fillAmount = 1f - (playerState.AbilityCooldown / maxCooldown);
            }

            if (abilityNameText != null)
                abilityNameText.text = Constants.ABILITY_NAMES[(int)playerState.Form];

            // Buffs
            UpdateBuffs(playerState);
        }

        private void UpdateBuffs(PlayerState playerState)
        {
            // Speed buff
            bool hasSpeed = playerState.HasSpeedBuff;
            SetBuffIndicator(speedBuffIndicator, hasSpeed);
            if (hasSpeed && speedBuffTimer != null)
            {
                float seconds = playerState.SpeedBuffTicks / (float)Constants.TICK_RATE;
                speedBuffTimer.text = $"{seconds:F1}s";
            }

            // Shield buff
            bool hasShield = playerState.HasShieldBuff;
            SetBuffIndicator(shieldBuffIndicator, hasShield);
            if (hasShield && shieldBuffTimer != null)
            {
                float seconds = playerState.ShieldBuffTicks / (float)Constants.TICK_RATE;
                shieldBuffTimer.text = $"{seconds:F1}s";
            }

            // Invulnerable
            SetBuffIndicator(invulnerableIndicator, playerState.IsInvulnerable);
        }

        private void UpdateMatchInfo(MatchState matchState)
        {
            // Time remaining
            if (timeText != null)
            {
                float seconds = matchState.GetTimeRemainingSeconds();
                int mins = (int)(seconds / 60);
                int secs = (int)(seconds % 60);
                timeText.text = $"{mins}:{secs:D2}";

                // Flash when low time
                if (seconds <= 10)
                {
                    timeText.color = Color.Lerp(Color.white, Color.red, Mathf.PingPong(Time.time * 2, 1));
                }
                else
                {
                    timeText.color = Color.white;
                }
            }

            // Players alive
            if (playersAliveText != null)
            {
                int alive = 0;
                foreach (var player in matchState.Players.Values)
                {
                    if (player.Alive) alive++;
                }
                playersAliveText.text = $"{alive}/{matchState.Players.Count}";
            }

            // Placement (only show if eliminated)
            if (placementText != null && !string.IsNullOrEmpty(_localPlayerIdHex))
            {
                if (matchState.Players.TryGetValue(_localPlayerIdHex, out var localPlayer))
                {
                    if (!localPlayer.Alive)
                    {
                        // Count players still alive to determine placement
                        int alive = 0;
                        foreach (var player in matchState.Players.Values)
                        {
                            if (player.Alive) alive++;
                        }
                        int placement = alive + 1;
                        placementText.text = GetPlacementString(placement);
                        placementText.gameObject.SetActive(true);
                    }
                    else
                    {
                        placementText.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void HandleCountdown(uint seconds)
        {
            if (countdownPanel != null)
                countdownPanel.SetActive(true);

            if (countdownText != null)
            {
                if (seconds > 0)
                    countdownText.text = seconds.ToString();
                else
                    countdownText.text = "GO!";
            }
        }

        private void HandleMatchStarted()
        {
            // Hide countdown after short delay
            Invoke(nameof(HideCountdown), 0.5f);
        }

        private void HideCountdown()
        {
            if (countdownPanel != null)
                countdownPanel.SetActive(false);
        }

        private void HandlePlayerEvolved(string playerId, int oldForm, int newForm)
        {
            if (playerId == _localPlayerIdHex)
            {
                // Show evolution notification
                ShowNotification($"Evolved to {Constants.FORM_NAMES[newForm]}!");
            }
            else
            {
                // Add to kill feed style notification
                AddKillFeedEntry($"Player evolved to {Constants.FORM_NAMES[newForm]}");
            }
        }

        private void HandlePlayerEliminated(string victimId)
        {
            if (victimId == _localPlayerIdHex)
            {
                ShowNotification("ELIMINATED!");
            }
            else
            {
                AddKillFeedEntry("Player eliminated");
            }
        }

        private void ShowNotification(string message)
        {
            // Could show a centered notification
            Debug.Log($"[HUD] Notification: {message}");
        }

        private void AddKillFeedEntry(string message)
        {
            if (killFeedContainer == null) return;

            GameObject entry;
            if (killFeedEntryPrefab != null)
            {
                entry = Instantiate(killFeedEntryPrefab, killFeedContainer);
            }
            else
            {
                entry = new GameObject("KillFeedEntry");
                entry.transform.SetParent(killFeedContainer);
                var text = entry.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 14;
                text.color = Color.white;
            }

            var entryText = entry.GetComponentInChildren<Text>();
            if (entryText != null)
                entryText.text = message;

            // Remove old entries
            while (killFeedContainer.childCount > maxKillFeedEntries)
            {
                Destroy(killFeedContainer.GetChild(0).gameObject);
            }

            // Auto-destroy after delay
            Destroy(entry, 5f);
        }

        private void SetBuffIndicator(GameObject indicator, bool active)
        {
            if (indicator != null)
                indicator.SetActive(active);
        }

        private static string GetPlacementString(int placement)
        {
            return placement switch
            {
                1 => "1st",
                2 => "2nd",
                3 => "3rd",
                _ => $"{placement}th"
            };
        }

        private static string BytesToHex(byte[] bytes)
        {
            if (bytes == null) return "";
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }
}

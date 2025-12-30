using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using RuneRelic.Game;
using RuneRelic.Network;
using RuneRelic.Network.Messages;
using RuneRelic.Utils;

namespace RuneRelic.UI
{
    /// <summary>
    /// Match end screen showing final placements, stats, and options.
    /// </summary>
    public class MatchEndUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject matchEndPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;

        [Header("Results")]
        [SerializeField] private Text resultText;
        [SerializeField] private Text placementText;
        [SerializeField] private Text finalScoreText;
        [SerializeField] private Text finalFormText;

        [Header("Stats")]
        [SerializeField] private Text runesCollectedText;
        [SerializeField] private Text eliminationsText;
        [SerializeField] private Text survivalTimeText;
        [SerializeField] private Text damageDealtText;

        [Header("Leaderboard")]
        [SerializeField] private Transform leaderboardContainer;
        [SerializeField] private GameObject leaderboardEntryPrefab;

        [Header("Buttons")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Settings")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        // State
        private byte[] _localPlayerId;
        private string _localPlayerIdHex;

        private void Start()
        {
            // Hide panel initially
            if (matchEndPanel != null)
                matchEndPanel.SetActive(false);

            // Subscribe to match end event
            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.OnMatchEnded += HandleMatchEnded;
            }

            // Setup buttons
            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        private void OnDestroy()
        {
            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.OnMatchEnded -= HandleMatchEnded;
            }
        }

        public void SetLocalPlayerId(byte[] playerId)
        {
            _localPlayerId = playerId;
            _localPlayerIdHex = BytesToHex(playerId);
        }

        private void HandleMatchEnded(MatchEndInfo info)
        {
            // Show match end panel
            if (matchEndPanel != null)
                matchEndPanel.SetActive(true);

            // Determine if local player won
            string winnerId = info.winner_id != null ? BytesToHex(info.winner_id) : null;
            bool isWinner = winnerId == _localPlayerIdHex;

            // Show victory or defeat panel
            if (victoryPanel != null)
                victoryPanel.SetActive(isWinner);
            if (defeatPanel != null)
                defeatPanel.SetActive(!isWinner);

            // Set result text
            if (resultText != null)
            {
                resultText.text = isWinner ? "VICTORY!" : "DEFEATED";
                resultText.color = isWinner ? Color.yellow : Color.red;
            }

            // Find local player placement
            int localPlacement = 0;
            PlayerPlacement localStats = null;

            for (int i = 0; i < info.placements.Count; i++)
            {
                var placement = info.placements[i];
                string placementId = BytesToHex(placement.player_id);

                if (placementId == _localPlayerIdHex)
                {
                    localPlacement = i + 1;
                    localStats = placement;
                    break;
                }
            }

            // Set placement text
            if (placementText != null)
            {
                placementText.text = GetPlacementString(localPlacement);
            }

            // Set stats
            if (localStats != null)
            {
                if (finalScoreText != null)
                    finalScoreText.text = localStats.final_score.ToString();

                if (finalFormText != null)
                    finalFormText.text = Constants.FORM_NAMES[localStats.final_form];

                if (runesCollectedText != null)
                    runesCollectedText.text = localStats.runes_collected.ToString();

                if (eliminationsText != null)
                    eliminationsText.text = localStats.eliminations.ToString();

                if (survivalTimeText != null)
                {
                    float seconds = localStats.survival_ticks / (float)Constants.TICK_RATE;
                    survivalTimeText.text = FormatTime(seconds);
                }

                if (damageDealtText != null)
                    damageDealtText.text = localStats.damage_dealt.ToString();
            }

            // Build leaderboard
            BuildLeaderboard(info.placements);
        }

        private void BuildLeaderboard(List<PlayerPlacement> placements)
        {
            if (leaderboardContainer == null) return;

            // Clear existing entries
            foreach (Transform child in leaderboardContainer)
            {
                Destroy(child.gameObject);
            }

            // Create entries for each player
            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                GameObject entry;

                if (leaderboardEntryPrefab != null)
                {
                    entry = Instantiate(leaderboardEntryPrefab, leaderboardContainer);
                }
                else
                {
                    // Create simple text entry
                    entry = new GameObject($"LeaderboardEntry_{i}");
                    entry.transform.SetParent(leaderboardContainer);

                    var text = entry.AddComponent<Text>();
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontSize = 16;
                    text.alignment = TextAnchor.MiddleLeft;

                    var rectTransform = entry.GetComponent<RectTransform>();
                    rectTransform.sizeDelta = new Vector2(400, 30);
                }

                // Set entry content
                string playerId = BytesToHex(placement.player_id);
                bool isLocal = playerId == _localPlayerIdHex;

                var entryText = entry.GetComponentInChildren<Text>();
                if (entryText != null)
                {
                    string formName = Constants.FORM_NAMES[placement.final_form];
                    entryText.text = $"{i + 1}. P{GetPlayerNumber(playerId)} - {placement.final_score} pts ({formName})";
                    entryText.color = isLocal ? Color.yellow : Color.white;
                }

                // Highlight local player
                var background = entry.GetComponent<Image>();
                if (background != null && isLocal)
                {
                    background.color = new Color(1f, 1f, 0f, 0.2f);
                }
            }
        }

        private async void OnPlayAgainClicked()
        {
            var client = GameClient.Instance;
            if (client != null && client.IsConnected)
            {
                // Leave current match and queue again
                await client.LeaveMatch();
                await client.RequestMatchmaking(MatchMode.Casual);
            }

            // Hide match end panel
            if (matchEndPanel != null)
                matchEndPanel.SetActive(false);
        }

        private async void OnMainMenuClicked()
        {
            var client = GameClient.Instance;
            if (client != null)
            {
                await client.LeaveMatch();
            }

            // Load main menu scene
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private static string GetPlacementString(int placement)
        {
            return placement switch
            {
                1 => "1st Place",
                2 => "2nd Place",
                3 => "3rd Place",
                4 => "4th Place",
                _ => $"{placement}th Place"
            };
        }

        private static string FormatTime(float seconds)
        {
            int mins = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            return $"{mins}:{secs:D2}";
        }

        private static int GetPlayerNumber(string playerId)
        {
            // Simple hash to get consistent player number
            if (string.IsNullOrEmpty(playerId)) return 0;
            int hash = playerId.GetHashCode();
            return (Mathf.Abs(hash) % 4) + 1;
        }

        private static string BytesToHex(byte[] bytes)
        {
            if (bytes == null) return "";
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }
}

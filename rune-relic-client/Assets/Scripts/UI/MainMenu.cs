using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using RuneRelic.Network;
using RuneRelic.Utils;

namespace RuneRelic.UI
{
    /// <summary>
    /// Main menu UI controller.
    /// Handles connection, authentication, and matchmaking.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject connectingPanel;
        [SerializeField] private GameObject matchmakingPanel;
        [SerializeField] private GameObject matchFoundPanel;

        [Header("Main Panel")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button practiceButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text statusText;

        [Header("Matchmaking Panel")]
        [SerializeField] private Text queueStatusText;
        [SerializeField] private Text playersFoundText;
        [SerializeField] private Button cancelButton;

        [Header("Match Found Panel")]
        [SerializeField] private Text matchFoundText;
        [SerializeField] private Button readyButton;
        [SerializeField] private Text countdownText;

        [Header("Settings")]
        [SerializeField] private string serverUrl = Constants.DEFAULT_SERVER_URL;
        [SerializeField] private string gameSceneName = "Game";

        private byte[] _playerId;
        private float _readyTimeout;
        private MatchMode _requestedMode = MatchMode.Casual;

        private void Start()
        {
            // Generate random player ID for dev
            _playerId = Guid.NewGuid().ToByteArray();

            // Find buttons if not assigned
            FindButtonsIfNeeded();

            // Setup buttons
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);
            if (practiceButton != null)
                practiceButton.onClick.AddListener(OnPracticeClicked);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClicked);
            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyClicked);
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);

            // Ensure GameClient exists
            EnsureGameClient();

            // Subscribe to network events
            var client = GameClient.Instance;
            if (client != null)
            {
                client.OnConnected += HandleConnected;
                client.OnDisconnected += HandleDisconnected;
                client.OnAuthResult += HandleAuthResult;
                client.OnMatchmakingUpdate += HandleMatchmakingUpdate;
                client.OnMatchFound += HandleMatchFound;
                client.OnMatchStart += HandleMatchStart;
                client.OnMatchEvent += HandleMatchEvent;
                client.OnError += HandleError;
            }
            else
            {
                Debug.LogError("[MainMenu] Failed to create GameClient!");
            }

            ShowPanel(mainPanel);
            UpdateStatus("Offline");
        }

        private void EnsureGameClient()
        {
            if (GameClient.Instance == null)
            {
                Debug.Log("[MainMenu] Creating GameClient...");
                var go = new GameObject("GameClient");
                go.AddComponent<GameClient>();
            }
        }

        private void FindButtonsIfNeeded()
        {
            // Find buttons by name if not assigned
            if (playButton == null)
            {
                var go = GameObject.Find("PlayButton");
                if (go != null) playButton = go.GetComponent<Button>();
            }
            if (cancelButton == null)
            {
                var go = GameObject.Find("CancelButton");
                if (go != null) cancelButton = go.GetComponent<Button>();
            }
            if (practiceButton == null)
            {
                var go = GameObject.Find("PracticeButton");
                if (go != null) practiceButton = go.GetComponent<Button>();
            }
            if (readyButton == null)
            {
                var go = GameObject.Find("ReadyButton");
                if (go != null) readyButton = go.GetComponent<Button>();
            }
            if (quitButton == null)
            {
                var go = GameObject.Find("QuitButton");
                if (go != null) quitButton = go.GetComponent<Button>();
            }
            if (statusText == null)
            {
                var go = GameObject.Find("ConnectionStatus");
                if (go != null) statusText = go.GetComponent<Text>();
            }

            Debug.Log($"[MainMenu] Buttons found - Play: {playButton != null}, Practice: {practiceButton != null}, Quit: {quitButton != null}, Status: {statusText != null}");
        }

        private void OnDestroy()
        {
            var client = GameClient.Instance;
            if (client != null)
            {
                client.OnConnected -= HandleConnected;
                client.OnDisconnected -= HandleDisconnected;
                client.OnAuthResult -= HandleAuthResult;
                client.OnMatchmakingUpdate -= HandleMatchmakingUpdate;
                client.OnMatchFound -= HandleMatchFound;
                client.OnMatchStart -= HandleMatchStart;
                client.OnMatchEvent -= HandleMatchEvent;
                client.OnError -= HandleError;
            }
        }

        // =====================================================================
        // Button Handlers
        // =====================================================================

        private async void OnPlayClicked()
        {
            _requestedMode = MatchMode.Casual;
            var client = GameClient.Instance;
            if (client == null)
            {
                Debug.LogError("[MainMenu] GameClient not found!");
                return;
            }

            ShowPanel(connectingPanel);
            UpdateStatus("Connecting...");

            // Connect if not connected
            if (!client.IsConnected)
            {
                await client.Connect(serverUrl);
            }
            else
            {
                // Already connected, start matchmaking
                StartMatchmaking();
            }
        }

        private async void OnPracticeClicked()
        {
            _requestedMode = MatchMode.Practice;
            var client = GameClient.Instance;
            if (client == null)
            {
                Debug.LogError("[MainMenu] GameClient not found!");
                return;
            }

            ShowPanel(connectingPanel);
            UpdateStatus("Connecting...");

            if (!client.IsConnected)
            {
                await client.Connect(serverUrl);
            }
            else
            {
                StartMatchmaking();
            }
        }

        private async void OnCancelClicked()
        {
            var client = GameClient.Instance;
            if (client != null)
            {
                await client.CancelMatchmaking();
            }

            ShowPanel(mainPanel);
            UpdateStatus("Ready to play");
        }

        private async void OnReadyClicked()
        {
            var client = GameClient.Instance;
            if (client != null)
            {
                await client.SendReady();
            }

            if (readyButton != null)
                readyButton.interactable = false;

            if (matchFoundText != null)
                matchFoundText.text = "Waiting for others...";
        }

        private void OnQuitClicked()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        // =====================================================================
        // Network Event Handlers
        // =====================================================================

        private async void HandleConnected()
        {
            UpdateStatus("Connected! Authenticating...");

            var client = GameClient.Instance;
            if (client != null)
            {
                await client.Authenticate(_playerId);
            }
        }

        private void HandleDisconnected()
        {
            ShowPanel(mainPanel);
            UpdateStatus("Disconnected");
        }

        private async void HandleAuthResult(Network.Messages.AuthResult result)
        {
            if (result.success)
            {
                UpdateStatus($"Authenticated! Server: {result.server_version}");
                StartMatchmaking();
            }
            else
            {
                ShowPanel(mainPanel);
                UpdateStatus($"Auth failed: {result.error}");
            }
        }

        private async void StartMatchmaking()
        {
            ShowPanel(matchmakingPanel);
            UpdateStatus("Finding match...");

            if (playersFoundText != null)
                playersFoundText.text = "0/4";

            var client = GameClient.Instance;
            if (client != null)
            {
                await client.RequestMatchmaking(_requestedMode);
            }
        }

        private void HandleMatchmakingUpdate(Network.Messages.MatchmakingResponse response)
        {
            if (queueStatusText != null)
                queueStatusText.text = $"Status: {response.status}";

            if (playersFoundText != null)
                playersFoundText.text = $"{response.players_found}/{response.players_needed}";
        }

        private void HandleMatchFound(Network.Messages.MatchFoundInfo info)
        {
            ShowPanel(matchFoundPanel);
            _readyTimeout = info.ready_timeout;

            if (matchFoundText != null)
                matchFoundText.text = $"Match Found! {info.player_ids.Count} players";

            if (readyButton != null)
                readyButton.interactable = true;

            if (countdownText != null)
                countdownText.text = $"{info.ready_timeout}s to ready";

            // Set local player ID in game manager
            var gameManager = Game.GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.SetLocalPlayerId(_playerId);
            }

            if (readyButton == null)
            {
                AutoReady();
            }
        }

        private async void AutoReady()
        {
            var client = GameClient.Instance;
            if (client != null)
            {
                await client.SendReady();
            }
        }

        private void HandleMatchStart(Network.Messages.MatchStartInfo info)
        {
            Debug.Log("[MainMenu] Match starting, loading game scene...");

            // Load game scene
            SceneManager.LoadScene(gameSceneName);
        }

        private void HandleMatchEvent(Network.Messages.MatchEvent evt)
        {
            if (evt.type == "countdown" && countdownText != null)
            {
                countdownText.text = evt.seconds.ToString();
            }
        }

        private void HandleError(string error)
        {
            UpdateStatus($"Error: {error}");
        }

        // =====================================================================
        // UI Helpers
        // =====================================================================

        private void ShowPanel(GameObject panel)
        {
            if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
            if (connectingPanel != null) connectingPanel.SetActive(panel == connectingPanel);
            if (matchmakingPanel != null) matchmakingPanel.SetActive(panel == matchmakingPanel);
            if (matchFoundPanel != null) matchFoundPanel.SetActive(panel == matchFoundPanel);
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;

            Debug.Log($"[MainMenu] {message}");
        }
    }
}

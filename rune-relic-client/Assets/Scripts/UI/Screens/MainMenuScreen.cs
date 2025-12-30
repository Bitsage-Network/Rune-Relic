using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RuneRelic.UI.Core;
using RuneRelic.UI.Components;
using RuneRelic.Network;
using RuneRelic.Audio;

namespace RuneRelic.UI.Screens
{
    /// <summary>
    /// Main menu screen with play, settings, and quit options.
    /// </summary>
    public class MainMenuScreen : UIScreen
    {
        [Header("Branding")]
        [SerializeField] private Image logoImage;
        [SerializeField] private Text versionText;

        [Header("Buttons")]
        [SerializeField] private StyledButton playButton;
        [SerializeField] private StyledButton practiceButton;
        [SerializeField] private StyledButton settingsButton;
        [SerializeField] private StyledButton quitButton;

        [Header("Connection Status")]
        [SerializeField] private Image connectionIndicator;
        [SerializeField] private Text connectionText;
        [SerializeField] private Text pingText;

        [Header("Animation")]
        [SerializeField] private CanvasGroup buttonsGroup;
        [SerializeField] private float buttonStaggerDelay = 0.1f;

        [Header("Background")]
        [SerializeField] private RawImage backgroundVideo;
        [SerializeField] private ParticleSystem ambientParticles;

        private bool _isConnecting;

        protected override void OnInitialize()
        {
            // Setup button listeners
            if (playButton != null)
                playButton.Button.onClick.AddListener(OnPlayClicked);

            if (practiceButton != null)
                practiceButton.Button.onClick.AddListener(OnPracticeClicked);

            if (settingsButton != null)
                settingsButton.Button.onClick.AddListener(OnSettingsClicked);

            if (quitButton != null)
                quitButton.Button.onClick.AddListener(OnQuitClicked);

            // Set version
            if (versionText != null)
            {
                versionText.text = $"v{Application.version}";
            }

            // Subscribe to network events
            var client = GameClient.Instance;
            if (client != null)
            {
                client.OnConnected += HandleConnected;
                client.OnDisconnected += HandleDisconnected;
                client.OnError += HandleError;
            }
        }

        private void OnDestroy()
        {
            var client = GameClient.Instance;
            if (client != null)
            {
                client.OnConnected -= HandleConnected;
                client.OnDisconnected -= HandleDisconnected;
                client.OnError -= HandleError;
            }
        }

        public override void OnShow()
        {
            base.OnShow();

            // Play menu music
            var audio = AudioManager.Instance;
            if (audio != null)
            {
                audio.PlayMenuMusic();
            }

            // Start ambient particles
            if (ambientParticles != null)
            {
                ambientParticles.Play();
            }

            // Animate buttons in
            StartCoroutine(AnimateButtonsIn());

            // Update connection status
            UpdateConnectionStatus();
        }

        private void Update()
        {
            // Update ping display
            var client = GameClient.Instance;
            if (client != null && client.IsConnected && pingText != null)
            {
                pingText.text = $"{client.Ping}ms";
            }
        }

        private IEnumerator AnimateButtonsIn()
        {
            if (buttonsGroup == null) yield break;

            // Get all buttons
            var buttons = buttonsGroup.GetComponentsInChildren<StyledButton>();

            // Hide initially
            foreach (var btn in buttons)
            {
                var cg = btn.GetComponent<CanvasGroup>();
                if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0;

                var rt = btn.GetComponent<RectTransform>();
                rt.anchoredPosition += new Vector2(-50, 0);
            }

            yield return new WaitForSeconds(0.2f);

            // Stagger animate each button
            foreach (var btn in buttons)
            {
                StartCoroutine(AnimateButtonIn(btn));
                yield return new WaitForSeconds(buttonStaggerDelay);
            }
        }

        private IEnumerator AnimateButtonIn(StyledButton button)
        {
            var cg = button.GetComponent<CanvasGroup>();
            var rt = button.GetComponent<RectTransform>();
            Vector2 targetPos = rt.anchoredPosition + new Vector2(50, 0);

            float elapsed = 0;
            float duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);

                cg.alpha = t;
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, t);

                yield return null;
            }

            cg.alpha = 1;
            rt.anchoredPosition = targetPos;
        }

        private async void OnPlayClicked()
        {
            if (_isConnecting) return;

            var client = GameClient.Instance;
            if (client == null)
            {
                UIManager.Instance?.ShowNotification("Game client not found!", NotificationType.Error);
                return;
            }

            // Connect if needed
            if (!client.IsConnected)
            {
                _isConnecting = true;
                SetButtonsInteractable(false);
                UpdateConnectionStatus("Connecting...");

                await client.Connect(Utils.Constants.DEFAULT_SERVER_URL);

                if (!client.IsConnected)
                {
                    _isConnecting = false;
                    SetButtonsInteractable(true);
                    UIManager.Instance?.ShowNotification("Failed to connect to server", NotificationType.Error);
                    return;
                }

                // Authenticate
                UpdateConnectionStatus("Authenticating...");
                byte[] playerId = System.Guid.NewGuid().ToByteArray();
                await client.Authenticate(playerId);
            }

            // Start matchmaking
            _isConnecting = false;
            await client.RequestMatchmaking(Utils.MatchMode.Casual);
            UIManager.Instance?.ShowScreen("Matchmaking");
        }

        private async void OnPracticeClicked()
        {
            var client = GameClient.Instance;
            if (client == null) return;

            if (!client.IsConnected)
            {
                await client.Connect(Utils.Constants.DEFAULT_SERVER_URL);
                byte[] playerId = System.Guid.NewGuid().ToByteArray();
                await client.Authenticate(playerId);
            }

            await client.RequestMatchmaking(Utils.MatchMode.Practice);
            UIManager.Instance?.ShowScreen("Matchmaking");
        }

        private void OnSettingsClicked()
        {
            UIManager.Instance?.ShowScreen("Settings");
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleConnected()
        {
            _isConnecting = false;
            UpdateConnectionStatus();
            SetButtonsInteractable(true);
        }

        private void HandleDisconnected()
        {
            _isConnecting = false;
            UpdateConnectionStatus();
            SetButtonsInteractable(true);
        }

        private void HandleError(string error)
        {
            UIManager.Instance?.ShowNotification($"Error: {error}", NotificationType.Error);
            _isConnecting = false;
            SetButtonsInteractable(true);
        }

        private void UpdateConnectionStatus(string overrideText = null)
        {
            var client = GameClient.Instance;
            bool connected = client != null && client.IsConnected;

            if (connectionIndicator != null)
            {
                UITheme theme = UIManager.Instance?.Theme;
                connectionIndicator.color = connected
                    ? (theme?.successColor ?? Color.green)
                    : (theme?.dangerColor ?? Color.red);
            }

            if (connectionText != null)
            {
                if (!string.IsNullOrEmpty(overrideText))
                {
                    connectionText.text = overrideText;
                }
                else
                {
                    connectionText.text = connected ? "Connected" : "Offline";
                }
            }

            if (pingText != null)
            {
                pingText.gameObject.SetActive(connected);
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (playButton != null) playButton.SetInteractable(interactable);
            if (practiceButton != null) practiceButton.SetInteractable(interactable);
        }

        public override void ApplyTheme(UITheme theme)
        {
            if (theme == null) return;

            if (versionText != null)
                versionText.color = theme.textMuted;

            if (connectionText != null)
                connectionText.color = theme.textSecondary;

            if (pingText != null)
                pingText.color = theme.textMuted;
        }
    }
}

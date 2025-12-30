using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RuneRelic.UI.Core;
using RuneRelic.UI.Components;
using RuneRelic.Network;
using RuneRelic.Utils;

namespace RuneRelic.UI.Screens
{
    /// <summary>
    /// Matchmaking queue screen with player count and cancel option.
    /// </summary>
    public class MatchmakingScreen : UIScreen
    {
        [Header("Queue Status")]
        [SerializeField] private Text statusText;
        [SerializeField] private Text queueTimeText;
        [SerializeField] private Text playersInQueueText;

        [Header("Player Slots")]
        [SerializeField] private PlayerSlot[] playerSlots;

        [Header("Animation")]
        [SerializeField] private Image[] searchingDots;
        [SerializeField] private float dotAnimSpeed = 0.5f;
        [SerializeField] private RectTransform spinnerRing;
        [SerializeField] private float spinnerSpeed = 60f;

        [Header("Buttons")]
        [SerializeField] private StyledButton cancelButton;

        [Header("Match Found")]
        [SerializeField] private CanvasGroup matchFoundOverlay;
        [SerializeField] private Text matchFoundText;
        [SerializeField] private StyledButton readyButton;
        [SerializeField] private Text readyCountdownText;
        [SerializeField] private PlayerSlot[] matchPlayerSlots;

        private float _queueStartTime;
        private int _playersFound;
        private int _playersNeeded = 4;
        private bool _matchFound;
        private float _readyTimeout;
        private Coroutine _dotAnimation;

        protected override void OnInitialize()
        {
            if (cancelButton != null)
            {
                cancelButton.Button.onClick.AddListener(OnCancelClicked);
            }

            if (readyButton != null)
            {
                readyButton.Button.onClick.AddListener(OnReadyClicked);
            }

            // Subscribe to network events
            var client = GameClient.Instance;
            if (client != null)
            {
                client.OnMatchmakingUpdate += HandleMatchmakingUpdate;
                client.OnMatchFound += HandleMatchFound;
            }
        }

        private void OnDestroy()
        {
            var client = GameClient.Instance;
            if (client != null)
            {
                client.OnMatchmakingUpdate -= HandleMatchmakingUpdate;
                client.OnMatchFound -= HandleMatchFound;
            }
        }

        public override void OnShow()
        {
            base.OnShow();

            _queueStartTime = Time.time;
            _playersFound = 0;
            _matchFound = false;

            UpdateQueueDisplay();
            ResetPlayerSlots();

            if (matchFoundOverlay != null)
            {
                matchFoundOverlay.alpha = 0f;
                matchFoundOverlay.gameObject.SetActive(false);
            }

            _dotAnimation = StartCoroutine(AnimateSearchingDots());
        }

        public override void OnHide()
        {
            base.OnHide();

            if (_dotAnimation != null)
            {
                StopCoroutine(_dotAnimation);
            }
        }

        private void Update()
        {
            // Update queue time
            if (!_matchFound)
            {
                float elapsed = Time.time - _queueStartTime;
                if (queueTimeText != null)
                {
                    int mins = (int)(elapsed / 60);
                    int secs = (int)(elapsed % 60);
                    queueTimeText.text = $"{mins}:{secs:D2}";
                }

                // Rotate spinner
                if (spinnerRing != null)
                {
                    spinnerRing.Rotate(0, 0, -spinnerSpeed * Time.deltaTime);
                }
            }

            // Ready countdown
            if (_matchFound && _readyTimeout > 0)
            {
                _readyTimeout -= Time.deltaTime;
                if (readyCountdownText != null)
                {
                    readyCountdownText.text = $"{Mathf.CeilToInt(_readyTimeout)}s";
                }
            }
        }

        private void HandleMatchmakingUpdate(Network.Messages.MatchmakingResponse response)
        {
            _playersFound = (int)response.players_found;
            _playersNeeded = (int)response.players_needed;

            if (statusText != null)
            {
                statusText.text = response.status;
            }

            UpdateQueueDisplay();
            UpdatePlayerSlots();
        }

        private void HandleMatchFound(Network.Messages.MatchFoundInfo info)
        {
            _matchFound = true;
            _readyTimeout = info.ready_timeout;

            StartCoroutine(ShowMatchFoundOverlay(info));
        }

        private void UpdateQueueDisplay()
        {
            if (playersInQueueText != null)
            {
                playersInQueueText.text = $"{_playersFound}/{_playersNeeded}";
            }
        }

        private void ResetPlayerSlots()
        {
            if (playerSlots == null) return;

            for (int i = 0; i < playerSlots.Length; i++)
            {
                if (playerSlots[i] != null)
                {
                    playerSlots[i].SetState(PlayerSlotState.Empty);
                }
            }
        }

        private void UpdatePlayerSlots()
        {
            if (playerSlots == null) return;

            for (int i = 0; i < playerSlots.Length; i++)
            {
                if (playerSlots[i] != null)
                {
                    if (i < _playersFound)
                    {
                        playerSlots[i].SetState(PlayerSlotState.Found);
                    }
                    else if (i < _playersNeeded)
                    {
                        playerSlots[i].SetState(PlayerSlotState.Searching);
                    }
                    else
                    {
                        playerSlots[i].SetState(PlayerSlotState.Empty);
                    }
                }
            }
        }

        private IEnumerator ShowMatchFoundOverlay(Network.Messages.MatchFoundInfo info)
        {
            if (matchFoundOverlay == null) yield break;

            matchFoundOverlay.gameObject.SetActive(true);

            // Fade in
            float elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                matchFoundOverlay.alpha = elapsed / 0.3f;
                yield return null;
            }
            matchFoundOverlay.alpha = 1f;

            // Update match player slots
            if (matchPlayerSlots != null)
            {
                for (int i = 0; i < matchPlayerSlots.Length; i++)
                {
                    if (matchPlayerSlots[i] != null && i < info.player_ids.Count)
                    {
                        matchPlayerSlots[i].SetState(PlayerSlotState.Found);
                        matchPlayerSlots[i].SetPlayerIndex(i);
                    }
                }
            }

            if (matchFoundText != null)
            {
                matchFoundText.text = $"Match Found!\n{info.player_ids.Count} Players";
            }

            if (readyButton != null)
            {
                readyButton.SetInteractable(true);
            }
        }

        private async void OnCancelClicked()
        {
            var client = GameClient.Instance;
            if (client != null)
            {
                await client.CancelMatchmaking();
            }

            UIManager.Instance?.ShowScreen("MainMenu");
        }

        private async void OnReadyClicked()
        {
            var client = GameClient.Instance;
            if (client != null)
            {
                await client.SendReady();
            }

            if (readyButton != null)
            {
                readyButton.SetInteractable(false);
            }
        }

        private IEnumerator AnimateSearchingDots()
        {
            if (searchingDots == null || searchingDots.Length == 0)
                yield break;

            int activeDot = 0;

            while (true)
            {
                for (int i = 0; i < searchingDots.Length; i++)
                {
                    if (searchingDots[i] != null)
                    {
                        float alpha = i == activeDot ? 1f : 0.3f;
                        searchingDots[i].color = new Color(1, 1, 1, alpha);
                    }
                }

                activeDot = (activeDot + 1) % searchingDots.Length;
                yield return new WaitForSeconds(dotAnimSpeed);
            }
        }

        public override void ApplyTheme(UITheme theme)
        {
            if (theme == null) return;

            if (statusText != null)
                statusText.color = theme.textPrimary;

            if (queueTimeText != null)
                queueTimeText.color = theme.textSecondary;

            if (playersInQueueText != null)
                playersInQueueText.color = theme.primaryColor;
        }
    }

    /// <summary>
    /// Individual player slot in matchmaking UI.
    /// </summary>
    [System.Serializable]
    public class PlayerSlot : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private Text playerText;
        [SerializeField] private GameObject searchingIndicator;

        private PlayerSlotState _state;
        private int _playerIndex;

        public void SetState(PlayerSlotState state)
        {
            _state = state;

            UITheme theme = UIManager.Instance?.Theme;

            switch (state)
            {
                case PlayerSlotState.Empty:
                    if (backgroundImage != null)
                        backgroundImage.color = theme?.backgroundDark ?? new Color(0.1f, 0.1f, 0.1f, 0.5f);
                    if (borderImage != null)
                        borderImage.color = theme?.borderColor ?? new Color(0.3f, 0.3f, 0.3f);
                    if (searchingIndicator != null)
                        searchingIndicator.SetActive(false);
                    if (iconImage != null)
                        iconImage.gameObject.SetActive(false);
                    if (playerText != null)
                        playerText.text = "";
                    break;

                case PlayerSlotState.Searching:
                    if (backgroundImage != null)
                        backgroundImage.color = theme?.backgroundMedium ?? new Color(0.15f, 0.15f, 0.15f, 0.7f);
                    if (borderImage != null)
                        borderImage.color = theme?.primaryColor ?? Color.cyan;
                    if (searchingIndicator != null)
                        searchingIndicator.SetActive(true);
                    if (iconImage != null)
                        iconImage.gameObject.SetActive(false);
                    if (playerText != null)
                        playerText.text = "Searching...";
                    break;

                case PlayerSlotState.Found:
                    if (backgroundImage != null)
                        backgroundImage.color = theme?.successColor ?? Color.green;
                    if (borderImage != null)
                        borderImage.color = theme?.successColor ?? Color.green;
                    if (searchingIndicator != null)
                        searchingIndicator.SetActive(false);
                    if (iconImage != null)
                        iconImage.gameObject.SetActive(true);
                    if (playerText != null)
                        playerText.text = $"Player {_playerIndex + 1}";
                    break;

                case PlayerSlotState.Ready:
                    if (backgroundImage != null)
                        backgroundImage.color = theme?.primaryColor ?? Color.cyan;
                    if (borderImage != null)
                        borderImage.color = theme?.primaryColor ?? Color.cyan;
                    if (playerText != null)
                        playerText.text = "READY";
                    break;
            }
        }

        public void SetPlayerIndex(int index)
        {
            _playerIndex = index;

            if (_state == PlayerSlotState.Found && playerText != null)
            {
                playerText.text = $"Player {index + 1}";
            }
        }
    }

    public enum PlayerSlotState
    {
        Empty,
        Searching,
        Found,
        Ready
    }
}

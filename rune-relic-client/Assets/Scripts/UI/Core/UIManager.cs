using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RuneRelic.UI.Core
{
    /// <summary>
    /// Central UI manager handling screens, transitions, and notifications.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Theme")]
        [SerializeField] private UITheme theme;

        [Header("Screen Management")]
        [SerializeField] private CanvasGroup screenContainer;
        [SerializeField] private List<UIScreen> screens = new List<UIScreen>();

        [Header("Overlays")]
        [SerializeField] private CanvasGroup loadingOverlay;
        [SerializeField] private CanvasGroup transitionOverlay;
        [SerializeField] private CanvasGroup notificationContainer;

        [Header("Transition Settings")]
        [SerializeField] private float transitionDuration = 0.3f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Notification Prefab")]
        [SerializeField] private GameObject notificationPrefab;
        [SerializeField] private int maxNotifications = 5;

        // State
        private UIScreen _currentScreen;
        private UIScreen _previousScreen;
        private bool _isTransitioning;
        private Queue<NotificationData> _notificationQueue = new Queue<NotificationData>();
        private List<GameObject> _activeNotifications = new List<GameObject>();

        public UITheme Theme => theme;
        public UIScreen CurrentScreen => _currentScreen;
        public bool IsTransitioning => _isTransitioning;

        // Events
        public event Action<UIScreen, UIScreen> OnScreenChanged;
        public event Action OnTransitionStarted;
        public event Action OnTransitionCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeScreens();
        }

        private void InitializeScreens()
        {
            foreach (var screen in screens)
            {
                if (screen != null)
                {
                    screen.gameObject.SetActive(false);
                    screen.Initialize(this);
                }
            }
        }

        // =====================================================================
        // Screen Management
        // =====================================================================

        /// <summary>
        /// Show a screen by name with optional transition.
        /// </summary>
        public void ShowScreen(string screenName, bool animate = true)
        {
            var screen = screens.Find(s => s.ScreenName == screenName);
            if (screen != null)
            {
                ShowScreen(screen, animate);
            }
            else
            {
                Debug.LogWarning($"[UIManager] Screen not found: {screenName}");
            }
        }

        /// <summary>
        /// Show a screen with optional transition.
        /// </summary>
        public void ShowScreen(UIScreen screen, bool animate = true)
        {
            if (_isTransitioning || screen == _currentScreen)
                return;

            StartCoroutine(TransitionToScreen(screen, animate));
        }

        /// <summary>
        /// Go back to the previous screen.
        /// </summary>
        public void GoBack(bool animate = true)
        {
            if (_previousScreen != null)
            {
                ShowScreen(_previousScreen, animate);
            }
        }

        private IEnumerator TransitionToScreen(UIScreen newScreen, bool animate)
        {
            _isTransitioning = true;
            OnTransitionStarted?.Invoke();

            UIScreen oldScreen = _currentScreen;
            _previousScreen = oldScreen;

            float duration = animate ? transitionDuration : 0f;

            // Fade out current screen
            if (oldScreen != null && animate)
            {
                yield return StartCoroutine(FadeScreen(oldScreen, 1f, 0f, duration));
                oldScreen.OnHide();
                oldScreen.gameObject.SetActive(false);
            }
            else if (oldScreen != null)
            {
                oldScreen.OnHide();
                oldScreen.gameObject.SetActive(false);
            }

            // Fade in new screen
            _currentScreen = newScreen;
            newScreen.gameObject.SetActive(true);
            newScreen.OnShow();

            if (animate)
            {
                var canvasGroup = newScreen.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = 0f;

                yield return StartCoroutine(FadeScreen(newScreen, 0f, 1f, duration));
            }

            _isTransitioning = false;
            OnTransitionCompleted?.Invoke();
            OnScreenChanged?.Invoke(oldScreen, newScreen);
        }

        private IEnumerator FadeScreen(UIScreen screen, float from, float to, float duration)
        {
            var canvasGroup = screen.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = screen.gameObject.AddComponent<CanvasGroup>();
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = transitionCurve.Evaluate(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        // =====================================================================
        // Loading Overlay
        // =====================================================================

        public void ShowLoading(string message = "Loading...")
        {
            if (loadingOverlay == null) return;

            loadingOverlay.gameObject.SetActive(true);
            StartCoroutine(FadeCanvasGroup(loadingOverlay, 0f, 1f, 0.2f));

            var text = loadingOverlay.GetComponentInChildren<Text>();
            if (text != null)
                text.text = message;
        }

        public void HideLoading()
        {
            if (loadingOverlay == null) return;

            StartCoroutine(FadeAndDisable(loadingOverlay, 0.2f));
        }

        public void UpdateLoadingProgress(float progress, string message = null)
        {
            if (loadingOverlay == null) return;

            var slider = loadingOverlay.GetComponentInChildren<Slider>();
            if (slider != null)
                slider.value = progress;

            if (message != null)
            {
                var text = loadingOverlay.GetComponentInChildren<Text>();
                if (text != null)
                    text.text = message;
            }
        }

        // =====================================================================
        // Notifications
        // =====================================================================

        /// <summary>
        /// Show a notification toast.
        /// </summary>
        public void ShowNotification(string message, NotificationType type = NotificationType.Info, float duration = 3f)
        {
            var data = new NotificationData
            {
                Message = message,
                Type = type,
                Duration = duration
            };

            if (_activeNotifications.Count >= maxNotifications)
            {
                _notificationQueue.Enqueue(data);
            }
            else
            {
                CreateNotification(data);
            }
        }

        private void CreateNotification(NotificationData data)
        {
            if (notificationContainer == null) return;

            GameObject notifObj;
            if (notificationPrefab != null)
            {
                notifObj = Instantiate(notificationPrefab, notificationContainer.transform);
            }
            else
            {
                notifObj = CreateDefaultNotification(data);
            }

            var notification = notifObj.GetComponent<UINotification>();
            if (notification == null)
            {
                notification = notifObj.AddComponent<UINotification>();
            }

            notification.Initialize(data, theme, OnNotificationDismissed);
            _activeNotifications.Add(notifObj);
        }

        private GameObject CreateDefaultNotification(NotificationData data)
        {
            GameObject notif = new GameObject("Notification");
            notif.transform.SetParent(notificationContainer.transform);

            var rect = notif.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 60);

            var image = notif.AddComponent<Image>();
            image.color = theme != null ? theme.panelColor : new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(notif.transform);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 8);
            textRect.offsetMax = new Vector2(-16, -8);

            var text = textObj.AddComponent<Text>();
            text.text = data.Message;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = GetNotificationColor(data.Type);

            return notif;
        }

        private Color GetNotificationColor(NotificationType type)
        {
            if (theme == null) return Color.white;

            return type switch
            {
                NotificationType.Success => theme.successColor,
                NotificationType.Warning => theme.warningColor,
                NotificationType.Error => theme.dangerColor,
                _ => theme.textPrimary
            };
        }

        private void OnNotificationDismissed(GameObject notification)
        {
            _activeNotifications.Remove(notification);
            Destroy(notification);

            if (_notificationQueue.Count > 0)
            {
                CreateNotification(_notificationQueue.Dequeue());
            }
        }

        // =====================================================================
        // Utilities
        // =====================================================================

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            group.alpha = to;
        }

        private IEnumerator FadeAndDisable(CanvasGroup group, float duration)
        {
            yield return StartCoroutine(FadeCanvasGroup(group, 1f, 0f, duration));
            group.gameObject.SetActive(false);
        }

        /// <summary>
        /// Register a screen dynamically.
        /// </summary>
        public void RegisterScreen(UIScreen screen)
        {
            if (!screens.Contains(screen))
            {
                screens.Add(screen);
                screen.Initialize(this);
            }
        }

        /// <summary>
        /// Get screen by name.
        /// </summary>
        public UIScreen GetScreen(string screenName)
        {
            return screens.Find(s => s.ScreenName == screenName);
        }
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public struct NotificationData
    {
        public string Message;
        public NotificationType Type;
        public float Duration;
    }
}

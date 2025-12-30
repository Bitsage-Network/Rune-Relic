using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RuneRelic.UI.Core
{
    /// <summary>
    /// Individual notification toast component.
    /// </summary>
    public class UINotification : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Text messageText;
        [SerializeField] private Image background;
        [SerializeField] private Image iconImage;
        [SerializeField] private Button closeButton;

        [Header("Icons")]
        [SerializeField] private Sprite infoIcon;
        [SerializeField] private Sprite successIcon;
        [SerializeField] private Sprite warningIcon;
        [SerializeField] private Sprite errorIcon;

        private NotificationData _data;
        private Action<GameObject> _onDismiss;
        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;

        public void Initialize(NotificationData data, UITheme theme, Action<GameObject> onDismiss)
        {
            _data = data;
            _onDismiss = onDismiss;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _rectTransform = GetComponent<RectTransform>();

            // Find components if not assigned
            if (messageText == null)
                messageText = GetComponentInChildren<Text>();

            if (background == null)
                background = GetComponent<Image>();

            // Set message
            if (messageText != null)
                messageText.text = data.Message;

            // Apply theme colors
            ApplyStyle(theme, data.Type);

            // Setup close button
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Dismiss);
            }

            // Animate in
            StartCoroutine(AnimateIn());

            // Auto dismiss
            if (data.Duration > 0)
            {
                StartCoroutine(AutoDismiss(data.Duration));
            }
        }

        private void ApplyStyle(UITheme theme, NotificationType type)
        {
            Color accentColor = Color.white;
            Color bgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            if (theme != null)
            {
                bgColor = theme.panelColor;

                accentColor = type switch
                {
                    NotificationType.Success => theme.successColor,
                    NotificationType.Warning => theme.warningColor,
                    NotificationType.Error => theme.dangerColor,
                    _ => theme.infoColor
                };
            }

            if (background != null)
            {
                background.color = bgColor;
            }

            if (messageText != null)
            {
                messageText.color = accentColor;
            }

            // Set icon
            if (iconImage != null)
            {
                Sprite icon = type switch
                {
                    NotificationType.Success => successIcon,
                    NotificationType.Warning => warningIcon,
                    NotificationType.Error => errorIcon,
                    _ => infoIcon
                };

                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.color = accentColor;
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }
        }

        private IEnumerator AnimateIn()
        {
            // Slide in from right
            Vector2 startPos = _rectTransform.anchoredPosition;
            startPos.x += 400;
            _rectTransform.anchoredPosition = startPos;
            _canvasGroup.alpha = 0;

            float elapsed = 0;
            float duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);

                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, startPos - new Vector2(400, 0), t);
                _canvasGroup.alpha = t;

                yield return null;
            }

            _canvasGroup.alpha = 1;
        }

        private IEnumerator AnimateOut()
        {
            Vector2 startPos = _rectTransform.anchoredPosition;
            float elapsed = 0;
            float duration = 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                _rectTransform.anchoredPosition = startPos + new Vector2(400 * t, 0);
                _canvasGroup.alpha = 1 - t;

                yield return null;
            }

            _onDismiss?.Invoke(gameObject);
        }

        private IEnumerator AutoDismiss(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Dismiss();
        }

        public void Dismiss()
        {
            StopAllCoroutines();
            StartCoroutine(AnimateOut());
        }
    }
}

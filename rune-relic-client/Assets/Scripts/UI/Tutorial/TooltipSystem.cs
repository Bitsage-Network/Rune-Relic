using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RuneRelic.UI.Core;

namespace RuneRelic.UI.Tutorial
{
    /// <summary>
    /// Shows contextual tooltips on hover.
    /// </summary>
    public class TooltipSystem : MonoBehaviour
    {
        public static TooltipSystem Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image iconImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float showDelay = 0.5f;
        [SerializeField] private float fadeSpeed = 10f;
        [SerializeField] private Vector2 offset = new Vector2(10, -10);
        [SerializeField] private float maxWidth = 300f;

        private bool _isShowing;
        private float _showTimer;
        private TooltipData _currentData;
        private RectTransform _canvasRect;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (tooltipPanel != null)
            {
                tooltipPanel.gameObject.SetActive(false);
            }

            if (canvasGroup == null && tooltipPanel != null)
            {
                canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
            }

            // Get canvas for bounds checking
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _canvasRect = canvas.GetComponent<RectTransform>();
            }
        }

        private void Update()
        {
            if (_currentData != null && !_isShowing)
            {
                _showTimer += Time.unscaledDeltaTime;
                if (_showTimer >= showDelay)
                {
                    Show();
                }
            }

            if (_isShowing)
            {
                UpdatePosition();

                // Fade in
                if (canvasGroup != null && canvasGroup.alpha < 1)
                {
                    canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1, fadeSpeed * Time.unscaledDeltaTime);
                }
            }
        }

        /// <summary>
        /// Request to show a tooltip (with delay).
        /// </summary>
        public void RequestShow(TooltipData data)
        {
            _currentData = data;
            _showTimer = 0;
        }

        /// <summary>
        /// Show tooltip immediately.
        /// </summary>
        public void ShowImmediate(TooltipData data)
        {
            _currentData = data;
            Show();
        }

        /// <summary>
        /// Hide the tooltip.
        /// </summary>
        public void Hide()
        {
            _currentData = null;
            _isShowing = false;
            _showTimer = 0;

            if (tooltipPanel != null)
            {
                tooltipPanel.gameObject.SetActive(false);
            }
        }

        private void Show()
        {
            if (_currentData == null) return;

            _isShowing = true;

            // Update content
            if (titleText != null)
            {
                titleText.text = _currentData.Title;
                titleText.gameObject.SetActive(!string.IsNullOrEmpty(_currentData.Title));
            }

            if (descriptionText != null)
            {
                descriptionText.text = _currentData.Description;
            }

            if (iconImage != null)
            {
                if (_currentData.Icon != null)
                {
                    iconImage.sprite = _currentData.Icon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            // Apply theme colors
            ApplyTheme();

            // Show panel
            if (tooltipPanel != null)
            {
                tooltipPanel.gameObject.SetActive(true);

                // Reset alpha for fade
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0;
                }
            }

            // Force layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);

            // Constrain width
            if (tooltipPanel.sizeDelta.x > maxWidth)
            {
                tooltipPanel.sizeDelta = new Vector2(maxWidth, tooltipPanel.sizeDelta.y);
                LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);
            }

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (tooltipPanel == null) return;

            Vector2 mousePos = Input.mousePosition;
            Vector2 tooltipPos = mousePos + offset;

            // Keep within screen bounds
            Vector2 tooltipSize = tooltipPanel.sizeDelta;

            if (tooltipPos.x + tooltipSize.x > Screen.width)
            {
                tooltipPos.x = mousePos.x - tooltipSize.x - offset.x;
            }

            if (tooltipPos.y - tooltipSize.y < 0)
            {
                tooltipPos.y = mousePos.y + tooltipSize.y - offset.y;
            }

            tooltipPanel.position = tooltipPos;
        }

        private void ApplyTheme()
        {
            UITheme theme = UIManager.Instance?.Theme;
            if (theme == null) return;

            var bg = tooltipPanel.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = theme.panelColor;
            }

            if (titleText != null)
            {
                titleText.color = theme.textHighlight;
            }

            if (descriptionText != null)
            {
                descriptionText.color = theme.textPrimary;
            }
        }
    }

    [System.Serializable]
    public class TooltipData
    {
        public string Title;
        public string Description;
        public Sprite Icon;

        public TooltipData(string description)
        {
            Description = description;
        }

        public TooltipData(string title, string description)
        {
            Title = title;
            Description = description;
        }

        public TooltipData(string title, string description, Sprite icon)
        {
            Title = title;
            Description = description;
            Icon = icon;
        }
    }

    /// <summary>
    /// Attach to UI elements to show tooltips on hover.
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string title;
        [TextArea(2, 5)]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private bool showImmediately = false;

        private TooltipData _data;

        private void Start()
        {
            _data = new TooltipData(title, description, icon);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (TooltipSystem.Instance == null) return;

            if (showImmediately)
            {
                TooltipSystem.Instance.ShowImmediate(_data);
            }
            else
            {
                TooltipSystem.Instance.RequestShow(_data);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipSystem.Instance?.Hide();
        }

        /// <summary>
        /// Update tooltip content at runtime.
        /// </summary>
        public void SetContent(string newTitle, string newDescription, Sprite newIcon = null)
        {
            title = newTitle;
            description = newDescription;
            icon = newIcon;
            _data = new TooltipData(title, description, icon);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using RuneRelic.UI.Core;

namespace RuneRelic.UI.Components
{
    /// <summary>
    /// Animated progress bar with customizable appearance.
    /// </summary>
    public class ProgressBar : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text valueText;
        [SerializeField] private Text labelText;

        [Header("Style")]
        [SerializeField] private ProgressBarStyle style = ProgressBarStyle.Default;
        [SerializeField] private bool useThemeColors = true;
        [SerializeField] private Gradient customGradient;

        [Header("Animation")]
        [SerializeField] private bool animateValue = true;
        [SerializeField] private float animationSpeed = 5f;
        [SerializeField] private bool pulseWhenFull = true;

        [Header("Display")]
        [SerializeField] private bool showValue = true;
        [SerializeField] private string valueFormat = "{0:P0}";
        [SerializeField] private bool showLabel = false;
        [SerializeField] private string label = "Progress";

        private float _targetValue;
        private float _currentValue;
        private float _pulseTimer;

        public float Value
        {
            get => _targetValue;
            set => SetValue(value);
        }

        private void Start()
        {
            ApplyStyle();
            UpdateDisplay();

            if (labelText != null)
            {
                labelText.gameObject.SetActive(showLabel);
                labelText.text = label;
            }
        }

        private void Update()
        {
            if (animateValue && Mathf.Abs(_currentValue - _targetValue) > 0.001f)
            {
                _currentValue = Mathf.Lerp(_currentValue, _targetValue, Time.deltaTime * animationSpeed);
                UpdateFill();
            }

            if (pulseWhenFull && _currentValue >= 0.99f)
            {
                _pulseTimer += Time.deltaTime * 2f;
                float pulse = 1f + Mathf.Sin(_pulseTimer) * 0.05f;

                if (fillImage != null)
                {
                    Color color = fillImage.color;
                    color.a = 0.8f + Mathf.Sin(_pulseTimer) * 0.2f;
                    fillImage.color = color;
                }
            }
        }

        public void SetValue(float value, bool instant = false)
        {
            _targetValue = Mathf.Clamp01(value);

            if (instant || !animateValue)
            {
                _currentValue = _targetValue;
                UpdateFill();
            }
        }

        public void SetLabel(string newLabel)
        {
            label = newLabel;
            if (labelText != null)
            {
                labelText.text = label;
            }
        }

        private void UpdateFill()
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = _currentValue;

                // Apply gradient color
                if (customGradient != null && !useThemeColors)
                {
                    fillImage.color = customGradient.Evaluate(_currentValue);
                }
            }

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (valueText != null)
            {
                valueText.gameObject.SetActive(showValue);
                valueText.text = string.Format(valueFormat, _currentValue);
            }
        }

        public void ApplyStyle()
        {
            UITheme theme = UIManager.Instance?.Theme;

            Color fillColor = Color.white;
            Color bgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            if (useThemeColors && theme != null)
            {
                bgColor = theme.backgroundDark;

                fillColor = style switch
                {
                    ProgressBarStyle.Health => theme.dangerColor,
                    ProgressBarStyle.Experience => theme.primaryColor,
                    ProgressBarStyle.Ability => theme.secondaryColor,
                    ProgressBarStyle.Timer => theme.warningColor,
                    _ => theme.primaryColor
                };
            }

            if (fillImage != null)
            {
                fillImage.color = fillColor;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = bgColor;
            }

            if (valueText != null && theme != null)
            {
                valueText.color = theme.textPrimary;
            }

            if (labelText != null && theme != null)
            {
                labelText.color = theme.textSecondary;
            }
        }
    }

    public enum ProgressBarStyle
    {
        Default,
        Health,
        Experience,
        Ability,
        Timer
    }
}

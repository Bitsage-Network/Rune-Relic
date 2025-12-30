using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RuneRelic.UI.Core;
using RuneRelic.Audio;

namespace RuneRelic.UI.Components
{
    /// <summary>
    /// Styled button with animations and sound effects.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class StyledButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Style")]
        [SerializeField] private ButtonStyle style = ButtonStyle.Primary;
        [SerializeField] private bool useThemeColors = true;

        [Header("Animation")]
        [SerializeField] private bool animateScale = true;
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float pressScale = 0.95f;
        [SerializeField] private float animationDuration = 0.1f;

        [Header("Audio")]
        [SerializeField] private bool playClickSound = true;
        [SerializeField] private bool playHoverSound = true;
        [SerializeField] private AudioClip customClickSound;
        [SerializeField] private AudioClip customHoverSound;

        [Header("Glow Effect")]
        [SerializeField] private bool showGlow = false;
        [SerializeField] private Image glowImage;

        private Button _button;
        private Image _image;
        private Text _text;
        private RectTransform _rectTransform;
        private Vector3 _originalScale;
        private Color _normalColor;
        private Color _hoverColor;
        private Color _pressedColor;
        private Color _disabledColor;
        private Coroutine _scaleCoroutine;
        private Coroutine _colorCoroutine;

        public Button Button => _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _image = GetComponent<Image>();
            _text = GetComponentInChildren<Text>();
            _rectTransform = GetComponent<RectTransform>();
            _originalScale = _rectTransform.localScale;

            _button.onClick.AddListener(OnClick);

            ApplyStyle();
        }

        private void OnEnable()
        {
            _rectTransform.localScale = _originalScale;
        }

        public void ApplyStyle()
        {
            UITheme theme = UIManager.Instance?.Theme;

            if (useThemeColors && theme != null)
            {
                switch (style)
                {
                    case ButtonStyle.Primary:
                        _normalColor = theme.primaryColor;
                        _hoverColor = Color.Lerp(theme.primaryColor, Color.white, 0.2f);
                        _pressedColor = Color.Lerp(theme.primaryColor, Color.black, 0.2f);
                        _disabledColor = Color.Lerp(theme.primaryColor, theme.backgroundDark, 0.5f);
                        break;

                    case ButtonStyle.Secondary:
                        _normalColor = theme.secondaryColor;
                        _hoverColor = Color.Lerp(theme.secondaryColor, Color.white, 0.2f);
                        _pressedColor = Color.Lerp(theme.secondaryColor, Color.black, 0.2f);
                        _disabledColor = Color.Lerp(theme.secondaryColor, theme.backgroundDark, 0.5f);
                        break;

                    case ButtonStyle.Ghost:
                        _normalColor = new Color(0, 0, 0, 0);
                        _hoverColor = new Color(theme.textPrimary.r, theme.textPrimary.g, theme.textPrimary.b, 0.1f);
                        _pressedColor = new Color(theme.textPrimary.r, theme.textPrimary.g, theme.textPrimary.b, 0.2f);
                        _disabledColor = new Color(0, 0, 0, 0);
                        break;

                    case ButtonStyle.Danger:
                        _normalColor = theme.dangerColor;
                        _hoverColor = Color.Lerp(theme.dangerColor, Color.white, 0.2f);
                        _pressedColor = Color.Lerp(theme.dangerColor, Color.black, 0.2f);
                        _disabledColor = Color.Lerp(theme.dangerColor, theme.backgroundDark, 0.5f);
                        break;

                    case ButtonStyle.Success:
                        _normalColor = theme.successColor;
                        _hoverColor = Color.Lerp(theme.successColor, Color.white, 0.2f);
                        _pressedColor = Color.Lerp(theme.successColor, Color.black, 0.2f);
                        _disabledColor = Color.Lerp(theme.successColor, theme.backgroundDark, 0.5f);
                        break;

                    default:
                        _normalColor = theme.buttonNormal;
                        _hoverColor = theme.buttonHover;
                        _pressedColor = theme.buttonPressed;
                        _disabledColor = theme.buttonDisabled;
                        break;
                }

                if (_text != null)
                {
                    _text.color = style == ButtonStyle.Ghost ? theme.textPrimary : Color.white;
                }
            }

            if (_image != null)
            {
                _image.color = _button.interactable ? _normalColor : _disabledColor;
            }

            UpdateGlow(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_button.interactable) return;

            if (animateScale)
            {
                AnimateScale(hoverScale);
            }

            AnimateColor(_hoverColor);
            UpdateGlow(true);

            if (playHoverSound)
            {
                // Play hover sound
                var audio = AudioManager.Instance;
                if (audio != null && customHoverSound != null)
                {
                    audio.PlaySFX(customHoverSound, 0.5f);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_button.interactable) return;

            if (animateScale)
            {
                AnimateScale(1f);
            }

            AnimateColor(_normalColor);
            UpdateGlow(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_button.interactable) return;

            if (animateScale)
            {
                AnimateScale(pressScale);
            }

            AnimateColor(_pressedColor);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_button.interactable) return;

            if (animateScale)
            {
                AnimateScale(hoverScale);
            }

            AnimateColor(_hoverColor);
        }

        private void OnClick()
        {
            if (playClickSound)
            {
                var audio = AudioManager.Instance;
                if (audio != null)
                {
                    if (customClickSound != null)
                    {
                        audio.PlaySFX(customClickSound);
                    }
                    else
                    {
                        audio.PlayButtonClick();
                    }
                }
            }
        }

        private void AnimateScale(float targetScale)
        {
            if (_scaleCoroutine != null)
            {
                StopCoroutine(_scaleCoroutine);
            }
            _scaleCoroutine = StartCoroutine(ScaleAnimation(targetScale));
        }

        private System.Collections.IEnumerator ScaleAnimation(float targetScale)
        {
            Vector3 target = _originalScale * targetScale;
            Vector3 start = _rectTransform.localScale;
            float elapsed = 0;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / animationDuration;
                _rectTransform.localScale = Vector3.Lerp(start, target, t);
                yield return null;
            }

            _rectTransform.localScale = target;
        }

        private void AnimateColor(Color targetColor)
        {
            if (_image == null) return;

            if (_colorCoroutine != null)
            {
                StopCoroutine(_colorCoroutine);
            }
            _colorCoroutine = StartCoroutine(ColorAnimation(targetColor));
        }

        private System.Collections.IEnumerator ColorAnimation(Color targetColor)
        {
            Color start = _image.color;
            float elapsed = 0;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / animationDuration;
                _image.color = Color.Lerp(start, targetColor, t);
                yield return null;
            }

            _image.color = targetColor;
        }

        private void UpdateGlow(bool show)
        {
            if (glowImage == null || !showGlow) return;

            glowImage.gameObject.SetActive(show);

            if (show)
            {
                UITheme theme = UIManager.Instance?.Theme;
                if (theme != null)
                {
                    glowImage.color = theme.glowColor;
                }
            }
        }

        public void SetInteractable(bool interactable)
        {
            _button.interactable = interactable;

            if (_image != null)
            {
                _image.color = interactable ? _normalColor : _disabledColor;
            }
        }
    }

    public enum ButtonStyle
    {
        Default,
        Primary,
        Secondary,
        Ghost,
        Danger,
        Success
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RuneRelic.UI.Core;
using RuneRelic.UI.Components;

namespace RuneRelic.UI.Screens
{
    /// <summary>
    /// Loading screen with progress bar and tips.
    /// </summary>
    public class LoadingScreen : UIScreen
    {
        [Header("Loading UI")]
        [SerializeField] private ProgressBar progressBar;
        [SerializeField] private Text statusText;
        [SerializeField] private Text tipText;
        [SerializeField] private Image backgroundImage;

        [Header("Animation")]
        [SerializeField] private Image spinnerImage;
        [SerializeField] private float spinnerSpeed = 180f;
        [SerializeField] private CanvasGroup contentGroup;

        [Header("Tips")]
        [SerializeField] private string[] loadingTips;
        [SerializeField] private float tipChangeInterval = 3f;

        [Header("Backgrounds")]
        [SerializeField] private Sprite[] backgroundSprites;

        private float _tipTimer;
        private int _currentTipIndex;
        private Coroutine _fakeProgressCoroutine;

        private static readonly string[] DefaultTips = {
            "Larger forms are slower but can consume smaller players.",
            "Collect runes to increase your score and evolve.",
            "Channel shrines for powerful temporary buffs.",
            "Use your ability wisely - it has a cooldown!",
            "The arena shrinks over time - stay inside the boundary!",
            "Ancient form can consume any smaller player.",
            "Chaos runes give massive points and random buffs.",
            "Shield buff protects you from one hit.",
            "Speed buff lets you escape or chase effectively."
        };

        protected override void OnInitialize()
        {
            if (loadingTips == null || loadingTips.Length == 0)
            {
                loadingTips = DefaultTips;
            }

            // Random background
            if (backgroundImage != null && backgroundSprites != null && backgroundSprites.Length > 0)
            {
                backgroundImage.sprite = backgroundSprites[Random.Range(0, backgroundSprites.Length)];
            }
        }

        public override void OnShow()
        {
            base.OnShow();

            // Reset state
            SetProgress(0f, "Loading...");
            ShowRandomTip();

            // Fade in content
            if (contentGroup != null)
            {
                StartCoroutine(FadeContent(0f, 1f, 0.3f));
            }
        }

        public override void OnHide()
        {
            base.OnHide();

            if (_fakeProgressCoroutine != null)
            {
                StopCoroutine(_fakeProgressCoroutine);
                _fakeProgressCoroutine = null;
            }
        }

        private void Update()
        {
            // Rotate spinner
            if (spinnerImage != null)
            {
                spinnerImage.transform.Rotate(0, 0, -spinnerSpeed * Time.deltaTime);
            }

            // Cycle tips
            _tipTimer += Time.deltaTime;
            if (_tipTimer >= tipChangeInterval)
            {
                _tipTimer = 0f;
                ShowNextTip();
            }
        }

        /// <summary>
        /// Set loading progress and status message.
        /// </summary>
        public void SetProgress(float progress, string status = null)
        {
            if (progressBar != null)
            {
                progressBar.SetValue(progress);
            }

            if (status != null && statusText != null)
            {
                statusText.text = status;
            }
        }

        /// <summary>
        /// Start fake progress animation.
        /// </summary>
        public void StartFakeProgress(float duration, string[] stages = null)
        {
            if (_fakeProgressCoroutine != null)
            {
                StopCoroutine(_fakeProgressCoroutine);
            }
            _fakeProgressCoroutine = StartCoroutine(FakeProgressRoutine(duration, stages));
        }

        private IEnumerator FakeProgressRoutine(float duration, string[] stages)
        {
            string[] defaultStages = {
                "Connecting to server...",
                "Loading assets...",
                "Preparing arena...",
                "Initializing game...",
                "Ready!"
            };

            stages = stages ?? defaultStages;

            float elapsed = 0f;
            int currentStage = 0;
            float stageInterval = duration / stages.Length;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                // Update stage
                int newStage = Mathf.Min((int)(progress * stages.Length), stages.Length - 1);
                if (newStage != currentStage)
                {
                    currentStage = newStage;
                }

                SetProgress(progress, stages[currentStage]);
                yield return null;
            }

            SetProgress(1f, stages[stages.Length - 1]);
        }

        private void ShowRandomTip()
        {
            if (loadingTips == null || loadingTips.Length == 0) return;

            _currentTipIndex = Random.Range(0, loadingTips.Length);
            UpdateTipDisplay();
        }

        private void ShowNextTip()
        {
            if (loadingTips == null || loadingTips.Length == 0) return;

            _currentTipIndex = (_currentTipIndex + 1) % loadingTips.Length;
            StartCoroutine(AnimateTipChange());
        }

        private IEnumerator AnimateTipChange()
        {
            if (tipText == null) yield break;

            // Fade out
            Color color = tipText.color;
            float elapsed = 0f;
            float duration = 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                color.a = 1f - (elapsed / duration);
                tipText.color = color;
                yield return null;
            }

            UpdateTipDisplay();

            // Fade in
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                color.a = elapsed / duration;
                tipText.color = color;
                yield return null;
            }

            color.a = 1f;
            tipText.color = color;
        }

        private void UpdateTipDisplay()
        {
            if (tipText != null && loadingTips != null && loadingTips.Length > 0)
            {
                tipText.text = $"TIP: {loadingTips[_currentTipIndex]}";
            }
        }

        private IEnumerator FadeContent(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                contentGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            contentGroup.alpha = to;
        }

        public override void ApplyTheme(UITheme theme)
        {
            if (theme == null) return;

            if (statusText != null)
                statusText.color = theme.textPrimary;

            if (tipText != null)
                tipText.color = theme.textSecondary;

            if (spinnerImage != null)
                spinnerImage.color = theme.primaryColor;
        }
    }
}

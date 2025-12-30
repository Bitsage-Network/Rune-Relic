using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RuneRelic.UI.Core;

namespace RuneRelic.UI.Tutorial
{
    /// <summary>
    /// Manages the tutorial and onboarding experience.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private CanvasGroup tutorialOverlay;
        [SerializeField] private RectTransform tutorialPanel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Text progressText;
        [SerializeField] private Image[] progressDots;

        [Header("Highlight")]
        [SerializeField] private RectTransform highlightFrame;
        [SerializeField] private Image highlightMask;

        [Header("Arrow")]
        [SerializeField] private RectTransform arrowIndicator;
        [SerializeField] private float arrowBobSpeed = 2f;
        [SerializeField] private float arrowBobAmount = 10f;

        [Header("Settings")]
        [SerializeField] private float stepTransitionDuration = 0.3f;
        [SerializeField] private bool autoAdvance = false;
        [SerializeField] private float autoAdvanceDelay = 3f;

        // Tutorial steps
        private List<TutorialStep> _steps = new List<TutorialStep>();
        private int _currentStepIndex = -1;
        private bool _isActive;
        private Coroutine _autoAdvanceCoroutine;

        // Events
        public event Action OnTutorialStarted;
        public event Action OnTutorialCompleted;
        public event Action<int> OnStepChanged;

        public bool IsActive => _isActive;
        public int CurrentStep => _currentStepIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Setup buttons
            if (nextButton != null)
                nextButton.onClick.AddListener(NextStep);
            if (skipButton != null)
                skipButton.onClick.AddListener(SkipTutorial);
            if (previousButton != null)
                previousButton.onClick.AddListener(PreviousStep);

            // Hide initially
            if (tutorialOverlay != null)
            {
                tutorialOverlay.alpha = 0;
                tutorialOverlay.gameObject.SetActive(false);
            }

            // Initialize default tutorial
            InitializeDefaultTutorial();
        }

        private void Update()
        {
            if (!_isActive) return;

            // Animate arrow
            if (arrowIndicator != null && arrowIndicator.gameObject.activeSelf)
            {
                float bob = Mathf.Sin(Time.time * arrowBobSpeed) * arrowBobAmount;
                Vector2 pos = arrowIndicator.anchoredPosition;
                pos.y = bob;
                arrowIndicator.anchoredPosition = pos;
            }

            // Handle input
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                NextStep();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                SkipTutorial();
            }
        }

        // =====================================================================
        // Tutorial Control
        // =====================================================================

        /// <summary>
        /// Start the tutorial from the beginning.
        /// </summary>
        public void StartTutorial()
        {
            if (_steps.Count == 0) return;

            _isActive = true;
            _currentStepIndex = -1;

            if (tutorialOverlay != null)
            {
                tutorialOverlay.gameObject.SetActive(true);
            }

            OnTutorialStarted?.Invoke();
            NextStep();
        }

        /// <summary>
        /// Start tutorial from a specific step.
        /// </summary>
        public void StartFromStep(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= _steps.Count) return;

            _isActive = true;
            _currentStepIndex = stepIndex - 1;

            if (tutorialOverlay != null)
            {
                tutorialOverlay.gameObject.SetActive(true);
            }

            NextStep();
        }

        /// <summary>
        /// Move to the next tutorial step.
        /// </summary>
        public void NextStep()
        {
            if (!_isActive) return;

            if (_autoAdvanceCoroutine != null)
            {
                StopCoroutine(_autoAdvanceCoroutine);
            }

            _currentStepIndex++;

            if (_currentStepIndex >= _steps.Count)
            {
                CompleteTutorial();
                return;
            }

            StartCoroutine(TransitionToStep(_currentStepIndex));
        }

        /// <summary>
        /// Move to the previous tutorial step.
        /// </summary>
        public void PreviousStep()
        {
            if (!_isActive || _currentStepIndex <= 0) return;

            if (_autoAdvanceCoroutine != null)
            {
                StopCoroutine(_autoAdvanceCoroutine);
            }

            _currentStepIndex--;
            StartCoroutine(TransitionToStep(_currentStepIndex));
        }

        /// <summary>
        /// Skip the tutorial entirely.
        /// </summary>
        public void SkipTutorial()
        {
            if (_autoAdvanceCoroutine != null)
            {
                StopCoroutine(_autoAdvanceCoroutine);
            }

            CompleteTutorial();
        }

        private void CompleteTutorial()
        {
            _isActive = false;
            _currentStepIndex = -1;

            StartCoroutine(FadeOut());

            // Mark as completed
            PlayerPrefs.SetInt("TutorialCompleted", 1);
            PlayerPrefs.Save();

            OnTutorialCompleted?.Invoke();
        }

        // =====================================================================
        // Step Display
        // =====================================================================

        private IEnumerator TransitionToStep(int stepIndex)
        {
            var step = _steps[stepIndex];

            // Fade out
            yield return StartCoroutine(FadePanel(1f, 0f, stepTransitionDuration * 0.5f));

            // Update content
            UpdateStepContent(step);

            // Fade in
            yield return StartCoroutine(FadePanel(0f, 1f, stepTransitionDuration * 0.5f));

            OnStepChanged?.Invoke(stepIndex);

            // Auto advance if enabled
            if (autoAdvance && step.AutoAdvance)
            {
                _autoAdvanceCoroutine = StartCoroutine(AutoAdvanceAfter(step.AutoAdvanceDelay > 0 ? step.AutoAdvanceDelay : autoAdvanceDelay));
            }
        }

        private void UpdateStepContent(TutorialStep step)
        {
            if (titleText != null)
                titleText.text = step.Title;

            if (descriptionText != null)
                descriptionText.text = step.Description;

            if (iconImage != null)
            {
                if (step.Icon != null)
                {
                    iconImage.sprite = step.Icon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            // Update progress
            UpdateProgress();

            // Update navigation buttons
            if (previousButton != null)
                previousButton.gameObject.SetActive(_currentStepIndex > 0);

            if (nextButton != null)
            {
                var buttonText = nextButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = _currentStepIndex >= _steps.Count - 1 ? "Finish" : "Next";
                }
            }

            // Position highlight
            if (step.HighlightTarget != null && highlightFrame != null)
            {
                PositionHighlight(step.HighlightTarget);
                highlightFrame.gameObject.SetActive(true);
            }
            else if (highlightFrame != null)
            {
                highlightFrame.gameObject.SetActive(false);
            }

            // Position arrow
            if (step.ArrowDirection != ArrowDirection.None && arrowIndicator != null)
            {
                PositionArrow(step.ArrowDirection, step.ArrowTarget);
                arrowIndicator.gameObject.SetActive(true);
            }
            else if (arrowIndicator != null)
            {
                arrowIndicator.gameObject.SetActive(false);
            }
        }

        private void UpdateProgress()
        {
            if (progressText != null)
            {
                progressText.text = $"{_currentStepIndex + 1}/{_steps.Count}";
            }

            if (progressDots != null)
            {
                for (int i = 0; i < progressDots.Length; i++)
                {
                    if (progressDots[i] != null)
                    {
                        bool isActive = i <= _currentStepIndex;
                        bool isCurrent = i == _currentStepIndex;

                        progressDots[i].color = isCurrent
                            ? Color.white
                            : (isActive ? new Color(1, 1, 1, 0.7f) : new Color(1, 1, 1, 0.3f));

                        progressDots[i].transform.localScale = isCurrent
                            ? Vector3.one * 1.2f
                            : Vector3.one;
                    }
                }
            }
        }

        private void PositionHighlight(RectTransform target)
        {
            if (highlightFrame == null || target == null) return;

            // Get target's screen position
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Vector2 min = corners[0];
            Vector2 max = corners[2];
            Vector2 size = max - min;
            Vector2 center = min + size * 0.5f;

            highlightFrame.position = center;
            highlightFrame.sizeDelta = size + new Vector2(20, 20); // Padding
        }

        private void PositionArrow(ArrowDirection direction, RectTransform target)
        {
            if (arrowIndicator == null) return;

            Vector3 position = target != null ? target.position : tutorialPanel.position;

            float rotation = direction switch
            {
                ArrowDirection.Up => 0,
                ArrowDirection.Down => 180,
                ArrowDirection.Left => 90,
                ArrowDirection.Right => -90,
                _ => 0
            };

            arrowIndicator.position = position;
            arrowIndicator.rotation = Quaternion.Euler(0, 0, rotation);
        }

        // =====================================================================
        // Animation
        // =====================================================================

        private IEnumerator FadePanel(float from, float to, float duration)
        {
            if (tutorialOverlay == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                tutorialOverlay.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            tutorialOverlay.alpha = to;
        }

        private IEnumerator FadeOut()
        {
            yield return StartCoroutine(FadePanel(1f, 0f, stepTransitionDuration));

            if (tutorialOverlay != null)
            {
                tutorialOverlay.gameObject.SetActive(false);
            }
        }

        private IEnumerator AutoAdvanceAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            NextStep();
        }

        // =====================================================================
        // Tutorial Steps
        // =====================================================================

        /// <summary>
        /// Add a tutorial step.
        /// </summary>
        public void AddStep(TutorialStep step)
        {
            _steps.Add(step);
        }

        /// <summary>
        /// Clear all tutorial steps.
        /// </summary>
        public void ClearSteps()
        {
            _steps.Clear();
        }

        private void InitializeDefaultTutorial()
        {
            _steps = new List<TutorialStep>
            {
                new TutorialStep
                {
                    Title = "Welcome to Rune Relic!",
                    Description = "Battle against other players in this fast-paced arena game. Collect runes, evolve your form, and be the last one standing!",
                    AutoAdvance = false
                },
                new TutorialStep
                {
                    Title = "Movement",
                    Description = "Use WASD or arrow keys to move around the arena. Your character will automatically face the direction you're moving.",
                    AutoAdvance = false
                },
                new TutorialStep
                {
                    Title = "Collecting Runes",
                    Description = "Collect glowing runes scattered around the arena to gain points. Different runes give different amounts of points and special effects!",
                    AutoAdvance = false
                },
                new TutorialStep
                {
                    Title = "Evolution",
                    Description = "As you collect points, you'll evolve into more powerful forms. Each form is larger, slower, but has unique abilities!",
                    AutoAdvance = false
                },
                new TutorialStep
                {
                    Title = "Abilities",
                    Description = "Press SPACE or LEFT CLICK to use your form's special ability. Abilities have cooldowns, so use them wisely!",
                    AutoAdvance = false
                },
                new TutorialStep
                {
                    Title = "Shrines",
                    Description = "Channel shrines by standing in them to gain powerful buffs. But watch out - channeling takes time and leaves you vulnerable!",
                    AutoAdvance = false
                },
                new TutorialStep
                {
                    Title = "Combat",
                    Description = "Larger players can consume smaller ones! Avoid players bigger than you, and chase down smaller players to eliminate them.",
                    AutoAdvance = false
                },
                new TutorialStep
                {
                    Title = "The Storm",
                    Description = "The arena shrinks over time! Stay inside the safe zone or take damage. Watch the minimap to track the shrinking boundary.",
                    AutoAdvance = false
                },
                new TutorialStep
                {
                    Title = "You're Ready!",
                    Description = "Good luck out there! Remember: collect runes, evolve, use abilities, and be the last one standing!",
                    AutoAdvance = false
                }
            };
        }

        /// <summary>
        /// Check if tutorial has been completed before.
        /// </summary>
        public bool HasCompletedTutorial()
        {
            return PlayerPrefs.GetInt("TutorialCompleted", 0) == 1;
        }

        /// <summary>
        /// Reset tutorial completion status.
        /// </summary>
        public void ResetTutorialProgress()
        {
            PlayerPrefs.SetInt("TutorialCompleted", 0);
            PlayerPrefs.Save();
        }
    }

    [Serializable]
    public class TutorialStep
    {
        public string Title;
        [TextArea(2, 5)]
        public string Description;
        public Sprite Icon;
        public RectTransform HighlightTarget;
        public ArrowDirection ArrowDirection = ArrowDirection.None;
        public RectTransform ArrowTarget;
        public bool AutoAdvance = false;
        public float AutoAdvanceDelay = 3f;
    }

    public enum ArrowDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }
}

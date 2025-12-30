using UnityEngine;
using UnityEngine.UI;
using RuneRelic.UI.Core;
using RuneRelic.Utils;

namespace RuneRelic.UI.Components
{
    /// <summary>
    /// Displays current form with evolution progress.
    /// </summary>
    public class FormIndicator : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Image formIcon;
        [SerializeField] private Image[] formTierIcons;
        [SerializeField] private Text formNameText;
        [SerializeField] private Text abilityNameText;
        [SerializeField] private ProgressBar evolutionProgress;
        [SerializeField] private Image progressGlow;

        [Header("Form Icons")]
        [SerializeField] private Sprite[] formSprites;

        [Header("Animation")]
        [SerializeField] private float evolutionAnimDuration = 1f;
        [SerializeField] private ParticleSystem evolutionParticles;

        private int _currentForm;
        private uint _currentScore;
        private Coroutine _evolutionAnimation;

        private void Start()
        {
            UpdateDisplay(0, 0);
        }

        public void UpdateDisplay(int form, uint score)
        {
            bool evolved = form > _currentForm;
            _currentForm = form;
            _currentScore = score;

            // Update form icon
            if (formIcon != null && formSprites != null && form < formSprites.Length)
            {
                formIcon.sprite = formSprites[form];
            }

            // Update tier indicators
            if (formTierIcons != null)
            {
                for (int i = 0; i < formTierIcons.Length; i++)
                {
                    if (formTierIcons[i] != null)
                    {
                        formTierIcons[i].gameObject.SetActive(i <= form);

                        UITheme theme = UIManager.Instance?.Theme;
                        if (theme != null)
                        {
                            formTierIcons[i].color = i == form
                                ? theme.GetFormColor(form)
                                : theme.textMuted;
                        }
                    }
                }
            }

            // Update names
            if (formNameText != null)
            {
                formNameText.text = Constants.FORM_NAMES[form];

                UITheme theme = UIManager.Instance?.Theme;
                if (theme != null)
                {
                    formNameText.color = theme.GetFormColor(form);
                }
            }

            if (abilityNameText != null)
            {
                abilityNameText.text = Constants.ABILITY_NAMES[form];
            }

            // Update evolution progress
            UpdateProgress(form, score);

            // Play evolution effect
            if (evolved)
            {
                PlayEvolutionEffect();
            }
        }

        private void UpdateProgress(int form, uint score)
        {
            if (evolutionProgress == null) return;

            // Max form - no more evolution
            if (form >= 4)
            {
                evolutionProgress.SetValue(1f);
                evolutionProgress.SetLabel("MAX");

                if (progressGlow != null)
                {
                    progressGlow.gameObject.SetActive(true);
                    progressGlow.color = UIManager.Instance?.Theme?.ancientColor ?? Color.yellow;
                }
                return;
            }

            if (progressGlow != null)
            {
                progressGlow.gameObject.SetActive(false);
            }

            int currentThreshold = Constants.EVOLUTION_THRESHOLDS[form];
            int nextThreshold = Constants.EVOLUTION_THRESHOLDS[form + 1];

            float progress = (float)(score - currentThreshold) / (nextThreshold - currentThreshold);
            evolutionProgress.SetValue(Mathf.Clamp01(progress));

            int remaining = nextThreshold - (int)score;
            evolutionProgress.SetLabel($"{remaining} to evolve");
        }

        private void PlayEvolutionEffect()
        {
            if (_evolutionAnimation != null)
            {
                StopCoroutine(_evolutionAnimation);
            }
            _evolutionAnimation = StartCoroutine(EvolutionAnimation());
        }

        private System.Collections.IEnumerator EvolutionAnimation()
        {
            // Play particles
            if (evolutionParticles != null)
            {
                evolutionParticles.Play();
            }

            // Scale pulse animation
            Vector3 originalScale = transform.localScale;
            float elapsed = 0;

            while (elapsed < evolutionAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / evolutionAnimDuration;

                // Pulse out then back
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.2f;
                transform.localScale = originalScale * scale;

                // Flash the icon
                if (formIcon != null)
                {
                    float flash = Mathf.Sin(t * Mathf.PI * 4) * 0.5f + 0.5f;
                    formIcon.color = Color.Lerp(Color.white, Color.yellow, flash);
                }

                yield return null;
            }

            transform.localScale = originalScale;
            if (formIcon != null)
            {
                formIcon.color = Color.white;
            }
        }
    }
}

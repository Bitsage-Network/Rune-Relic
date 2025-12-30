using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RuneRelic.VFX
{
    /// <summary>
    /// Screen-space visual effects like shake, flash, and vignette.
    /// </summary>
    public class ScreenEffects : MonoBehaviour
    {
        public static ScreenEffects Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Image flashOverlay;
        [SerializeField] private Image vignetteOverlay;
        [SerializeField] private Image damageOverlay;

        [Header("Screen Shake")]
        [SerializeField] private float defaultShakeIntensity = 0.1f;
        [SerializeField] private float defaultShakeDuration = 0.2f;
        [SerializeField] private float shakeDecay = 5f;

        [Header("Flash")]
        [SerializeField] private float defaultFlashDuration = 0.1f;
        [SerializeField] private Color defaultFlashColor = Color.white;

        [Header("Vignette")]
        [SerializeField] private float lowHealthThreshold = 0.3f;
        [SerializeField] private Color lowHealthVignetteColor = new Color(1, 0, 0, 0.3f);
        [SerializeField] private float vignettePulseSpeed = 2f;

        [Header("Damage")]
        [SerializeField] private Color damageColor = new Color(1, 0, 0, 0.5f);
        [SerializeField] private float damageFadeSpeed = 3f;

        // State
        private Vector3 _originalCameraPosition;
        private Coroutine _shakeCoroutine;
        private Coroutine _flashCoroutine;
        private float _currentDamageAlpha;
        private float _currentHealth = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                _originalCameraPosition = targetCamera.transform.localPosition;
            }

            // Initialize overlays
            if (flashOverlay != null)
            {
                flashOverlay.color = new Color(1, 1, 1, 0);
                flashOverlay.gameObject.SetActive(true);
            }

            if (vignetteOverlay != null)
            {
                vignetteOverlay.color = new Color(0, 0, 0, 0);
                vignetteOverlay.gameObject.SetActive(true);
            }

            if (damageOverlay != null)
            {
                damageOverlay.color = new Color(damageColor.r, damageColor.g, damageColor.b, 0);
                damageOverlay.gameObject.SetActive(true);
            }
        }

        private void Update()
        {
            UpdateVignette();
            UpdateDamageOverlay();
        }

        // =====================================================================
        // Screen Shake
        // =====================================================================

        /// <summary>
        /// Trigger screen shake effect.
        /// </summary>
        public void Shake(float intensity = -1, float duration = -1)
        {
            if (targetCamera == null) return;

            if (intensity < 0) intensity = defaultShakeIntensity;
            if (duration < 0) duration = defaultShakeDuration;

            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
            }

            _shakeCoroutine = StartCoroutine(ShakeCoroutine(intensity, duration));
        }

        /// <summary>
        /// Shake on hit (intensity based on damage).
        /// </summary>
        public void ShakeOnHit(float damagePercent)
        {
            float intensity = Mathf.Lerp(0.05f, 0.3f, damagePercent);
            Shake(intensity, 0.15f);
        }

        /// <summary>
        /// Shake on elimination (big shake).
        /// </summary>
        public void ShakeOnElimination()
        {
            Shake(0.4f, 0.4f);
        }

        /// <summary>
        /// Shake on evolution (medium shake).
        /// </summary>
        public void ShakeOnEvolution()
        {
            Shake(0.2f, 0.3f);
        }

        private IEnumerator ShakeCoroutine(float intensity, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float currentIntensity = intensity * (1 - elapsed / duration);

                Vector3 offset = new Vector3(
                    Random.Range(-1f, 1f) * currentIntensity,
                    Random.Range(-1f, 1f) * currentIntensity,
                    0
                );

                targetCamera.transform.localPosition = _originalCameraPosition + offset;
                yield return null;
            }

            targetCamera.transform.localPosition = _originalCameraPosition;
            _shakeCoroutine = null;
        }

        // =====================================================================
        // Flash Effect
        // =====================================================================

        /// <summary>
        /// Flash the screen with a color.
        /// </summary>
        public void Flash(Color? color = null, float duration = -1)
        {
            if (flashOverlay == null) return;

            Color flashColor = color ?? defaultFlashColor;
            if (duration < 0) duration = defaultFlashDuration;

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }

            _flashCoroutine = StartCoroutine(FlashCoroutine(flashColor, duration));
        }

        /// <summary>
        /// Flash on evolution.
        /// </summary>
        public void FlashOnEvolution(int newForm)
        {
            Color formColor = newForm switch
            {
                1 => new Color(0.6f, 0.8f, 1f, 0.5f),
                2 => new Color(0.4f, 0.9f, 0.6f, 0.5f),
                3 => new Color(0.7f, 0.4f, 1f, 0.5f),
                4 => new Color(1f, 0.8f, 0.3f, 0.7f),
                _ => new Color(1f, 1f, 1f, 0.3f)
            };

            Flash(formColor, 0.3f);
        }

        /// <summary>
        /// Flash on rune collect.
        /// </summary>
        public void FlashOnRuneCollect(Color runeColor)
        {
            Color flashColor = new Color(runeColor.r, runeColor.g, runeColor.b, 0.2f);
            Flash(flashColor, 0.1f);
        }

        private IEnumerator FlashCoroutine(Color color, float duration)
        {
            flashOverlay.color = color;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = color.a * (1 - elapsed / duration);
                flashOverlay.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            flashOverlay.color = new Color(color.r, color.g, color.b, 0);
            _flashCoroutine = null;
        }

        // =====================================================================
        // Vignette (Low Health Warning)
        // =====================================================================

        /// <summary>
        /// Set current health for vignette effect.
        /// </summary>
        public void SetHealth(float healthPercent)
        {
            _currentHealth = Mathf.Clamp01(healthPercent);
        }

        private void UpdateVignette()
        {
            if (vignetteOverlay == null) return;

            if (_currentHealth < lowHealthThreshold)
            {
                // Pulsing vignette when low health
                float pulse = (Mathf.Sin(Time.time * vignettePulseSpeed) + 1) * 0.5f;
                float intensity = (1 - _currentHealth / lowHealthThreshold) * pulse;

                vignetteOverlay.color = new Color(
                    lowHealthVignetteColor.r,
                    lowHealthVignetteColor.g,
                    lowHealthVignetteColor.b,
                    lowHealthVignetteColor.a * intensity
                );
            }
            else
            {
                vignetteOverlay.color = new Color(0, 0, 0, 0);
            }
        }

        // =====================================================================
        // Damage Overlay
        // =====================================================================

        /// <summary>
        /// Show damage indicator.
        /// </summary>
        public void ShowDamage(float damagePercent = 0.3f)
        {
            _currentDamageAlpha = Mathf.Max(_currentDamageAlpha, damagePercent);
        }

        private void UpdateDamageOverlay()
        {
            if (damageOverlay == null) return;

            if (_currentDamageAlpha > 0)
            {
                _currentDamageAlpha -= Time.deltaTime * damageFadeSpeed;
                _currentDamageAlpha = Mathf.Max(0, _currentDamageAlpha);

                damageOverlay.color = new Color(
                    damageColor.r,
                    damageColor.g,
                    damageColor.b,
                    damageColor.a * _currentDamageAlpha
                );
            }
        }

        // =====================================================================
        // Combined Effects
        // =====================================================================

        /// <summary>
        /// Play hit feedback (shake + damage overlay).
        /// </summary>
        public void PlayHitFeedback(float damagePercent)
        {
            ShakeOnHit(damagePercent);
            ShowDamage(damagePercent);
        }

        /// <summary>
        /// Play elimination feedback (big shake + flash).
        /// </summary>
        public void PlayEliminationFeedback(bool isLocalPlayer)
        {
            ShakeOnElimination();

            if (isLocalPlayer)
            {
                Flash(new Color(1, 0, 0, 0.5f), 0.5f);
            }
        }

        /// <summary>
        /// Play evolution feedback (shake + flash).
        /// </summary>
        public void PlayEvolutionFeedback(int newForm)
        {
            ShakeOnEvolution();
            FlashOnEvolution(newForm);
        }

        /// <summary>
        /// Play victory effect.
        /// </summary>
        public void PlayVictoryEffect()
        {
            Flash(new Color(1f, 0.9f, 0.3f, 0.5f), 1f);
        }
    }
}

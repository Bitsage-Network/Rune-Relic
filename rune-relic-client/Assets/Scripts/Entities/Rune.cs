using UnityEngine;
using RuneRelic.Utils;

namespace RuneRelic.Entities
{
    /// <summary>
    /// Rune entity that floats and rotates. Handles visual representation and collection effects.
    /// </summary>
    public class Rune : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.3f;
        [SerializeField] private float baseHeight = 0.5f;

        [Header("Effects")]
        [SerializeField] private ParticleSystem idleParticles;
        [SerializeField] private ParticleSystem collectionEffect;
        [SerializeField] private Light glowLight;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip collectionSound;

        // State
        private uint _id;
        private RuneType _runeType;
        private Vector3 _startPosition;
        private bool _collected;
        private Renderer _renderer;

        // Rune colors
        private static readonly Color[] RuneColors = {
            new Color(0.3f, 0.5f, 1f),      // Wisdom - Blue
            new Color(1f, 0.3f, 0.3f),      // Power - Red
            new Color(1f, 1f, 0.3f),        // Speed - Yellow
            new Color(0.3f, 1f, 0.5f),      // Shield - Green
            new Color(0.7f, 0.3f, 1f),      // Arcane - Purple
            new Color(1f, 1f, 1f)           // Chaos - White (rainbow shader)
        };

        // Rune glow intensities
        private static readonly float[] RuneGlowIntensities = {
            1f,     // Wisdom
            1.2f,   // Power
            1f,     // Speed
            0.8f,   // Shield
            1.5f,   // Arcane
            2f      // Chaos
        };

        public uint Id => _id;
        public RuneType Type => _runeType;

        public void Initialize(uint id, RuneType runeType, Vector3 position)
        {
            _id = id;
            _runeType = runeType;
            _startPosition = position;
            _startPosition.y = baseHeight;
            transform.position = _startPosition;

            // Get renderer
            _renderer = GetComponent<Renderer>();
            if (_renderer == null)
                _renderer = GetComponentInChildren<Renderer>();

            // Set color based on type
            SetColor(RuneColors[(int)runeType]);

            // Set glow intensity
            if (glowLight != null)
            {
                glowLight.color = RuneColors[(int)runeType];
                glowLight.intensity = RuneGlowIntensities[(int)runeType];
            }

            // Configure particles
            if (idleParticles != null)
            {
                var main = idleParticles.main;
                main.startColor = RuneColors[(int)runeType];
                idleParticles.Play();
            }
        }

        private void Update()
        {
            if (_collected) return;

            // Rotate
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // Bob up and down
            float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(
                _startPosition.x,
                _startPosition.y + yOffset,
                _startPosition.z
            );

            // Chaos rune: cycle colors
            if (_runeType == RuneType.Chaos && _renderer != null)
            {
                float hue = Mathf.Repeat(Time.time * 0.5f, 1f);
                Color rainbowColor = Color.HSVToRGB(hue, 1f, 1f);
                _renderer.material.color = rainbowColor;
                _renderer.material.SetColor("_EmissionColor", rainbowColor * 2f);
            }
        }

        /// <summary>
        /// Play collection effect and mark as collected.
        /// </summary>
        public void Collect()
        {
            if (_collected) return;
            _collected = true;

            // Stop idle particles
            if (idleParticles != null)
                idleParticles.Stop();

            // Play collection effect
            if (collectionEffect != null)
            {
                collectionEffect.Play();
            }

            // Play sound
            if (audioSource != null && collectionSound != null)
            {
                audioSource.PlayOneShot(collectionSound);
            }

            // Hide renderer immediately
            if (_renderer != null)
                _renderer.enabled = false;

            // Disable glow
            if (glowLight != null)
                glowLight.enabled = false;

            // Destroy after effects finish
            float destroyDelay = collectionEffect != null ? collectionEffect.main.duration : 0.5f;
            Destroy(gameObject, destroyDelay);
        }

        private void SetColor(Color color)
        {
            if (_renderer != null && _renderer.material != null)
            {
                _renderer.material.color = color;

                // Set emission for glow effect
                if (_renderer.material.HasProperty("_EmissionColor"))
                {
                    _renderer.material.EnableKeyword("_EMISSION");
                    _renderer.material.SetColor("_EmissionColor", color * 0.5f);
                }
            }
        }

        /// <summary>
        /// Get the point value for this rune type.
        /// </summary>
        public int GetPoints()
        {
            return Constants.RUNE_POINTS[(int)_runeType];
        }

        /// <summary>
        /// Create a placeholder rune mesh (for when no prefab is provided).
        /// </summary>
        public static GameObject CreatePlaceholder(RuneType runeType)
        {
            GameObject rune = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // Scale to diamond shape
            rune.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
            rune.transform.rotation = Quaternion.Euler(45f, 0f, 45f);

            // Remove collider (server handles collision)
            var collider = rune.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            // Set material
            var renderer = rune.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = RuneColors[(int)runeType];

                // Enable emission
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", RuneColors[(int)runeType] * 0.5f);
            }

            return rune;
        }
    }
}

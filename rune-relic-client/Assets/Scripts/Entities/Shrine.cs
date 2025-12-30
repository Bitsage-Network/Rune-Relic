using UnityEngine;
using RuneRelic.Utils;

namespace RuneRelic.Entities
{
    /// <summary>
    /// Shrine entity that provides buffs when channeled.
    /// Handles visual representation and activation effects.
    /// </summary>
    public class Shrine : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private float pulseSpeed = 1f;
        [SerializeField] private float pulseIntensity = 0.3f;
        [SerializeField] private float captureRadius = 3f;

        [Header("Components")]
        [SerializeField] private GameObject baseObject;
        [SerializeField] private GameObject beamObject;
        [SerializeField] private GameObject captureZoneVisual;
        [SerializeField] private ParticleSystem activeParticles;
        [SerializeField] private ParticleSystem captureEffect;
        [SerializeField] private Light shrineLight;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip channelLoopSound;
        [SerializeField] private AudioClip captureSound;

        // State
        private uint _id;
        private ShrineType _shrineType;
        private bool _isActive;
        private bool _isBeingChanneled;
        private byte[] _controllerId;
        private float _channelProgress;
        private Renderer _baseRenderer;

        // Shrine colors
        private static readonly Color[] ShrineColors = {
            new Color(0.3f, 0.5f, 1f),      // Wisdom - Blue
            new Color(1f, 0.3f, 0.3f),      // Power - Red
            new Color(1f, 1f, 0.3f),        // Speed - Yellow
            new Color(0.3f, 1f, 0.5f)       // Shield - Green
        };

        public uint Id => _id;
        public ShrineType Type => _shrineType;
        public bool IsActive => _isActive;

        public void Initialize(uint id, ShrineType shrineType, Vector3 position)
        {
            _id = id;
            _shrineType = shrineType;
            transform.position = position;

            // Get renderer
            if (baseObject != null)
                _baseRenderer = baseObject.GetComponent<Renderer>();
            else
                _baseRenderer = GetComponent<Renderer>();

            // Set color
            Color shrineColor = ShrineColors[(int)shrineType];
            SetColor(shrineColor);

            // Configure light
            if (shrineLight != null)
            {
                shrineLight.color = shrineColor;
                shrineLight.intensity = 1f;
            }

            // Configure particles
            if (activeParticles != null)
            {
                var main = activeParticles.main;
                main.startColor = shrineColor;
            }

            // Set capture zone size
            if (captureZoneVisual != null)
            {
                captureZoneVisual.transform.localScale = new Vector3(captureRadius * 2, 0.1f, captureRadius * 2);

                var zoneRenderer = captureZoneVisual.GetComponent<Renderer>();
                if (zoneRenderer != null)
                {
                    Color zoneColor = shrineColor;
                    zoneColor.a = 0.2f;
                    zoneRenderer.material.color = zoneColor;
                }
            }

            // Hide beam initially
            if (beamObject != null)
                beamObject.SetActive(false);

            SetActive(false);
        }

        private void Update()
        {
            // Pulse effect when active
            if (_isActive && shrineLight != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
                shrineLight.intensity = pulse;
            }

            // Update channel progress visual
            if (_isBeingChanneled && beamObject != null)
            {
                // Scale beam based on progress
                float beamHeight = Mathf.Lerp(1f, 10f, _channelProgress);
                beamObject.transform.localScale = new Vector3(0.5f, beamHeight, 0.5f);
            }
        }

        /// <summary>
        /// Update shrine state from server.
        /// </summary>
        public void UpdateState(bool isActive, byte[] controllerId, float channelProgress)
        {
            bool wasActive = _isActive;
            _isActive = isActive;
            _controllerId = controllerId;
            _channelProgress = channelProgress;

            // State changed
            if (isActive != wasActive)
            {
                SetActive(isActive);
            }

            // Update channeling visual
            bool shouldShowBeam = channelProgress > 0 && channelProgress < 1f;
            if (_isBeingChanneled != shouldShowBeam)
            {
                _isBeingChanneled = shouldShowBeam;
                if (beamObject != null)
                    beamObject.SetActive(shouldShowBeam);

                // Start/stop channel audio
                if (audioSource != null && channelLoopSound != null)
                {
                    if (shouldShowBeam)
                    {
                        audioSource.clip = channelLoopSound;
                        audioSource.loop = true;
                        audioSource.Play();
                    }
                    else
                    {
                        audioSource.Stop();
                    }
                }
            }

            // Update controller color on capture zone
            UpdateControllerVisual();
        }

        /// <summary>
        /// Set shrine as active or inactive (on cooldown).
        /// </summary>
        public void SetActive(bool active)
        {
            _isActive = active;

            // Update particles
            if (activeParticles != null)
            {
                if (active)
                    activeParticles.Play();
                else
                    activeParticles.Stop();
            }

            // Update light intensity
            if (shrineLight != null)
            {
                shrineLight.intensity = active ? 1f : 0.3f;
            }

            // Update base material
            if (_baseRenderer != null)
            {
                Color color = ShrineColors[(int)_shrineType];
                if (!active)
                    color *= 0.5f; // Dim when inactive
                _baseRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Play capture effect when shrine is captured.
        /// </summary>
        public void PlayCaptureEffect()
        {
            if (captureEffect != null)
                captureEffect.Play();

            if (audioSource != null && captureSound != null)
                audioSource.PlayOneShot(captureSound);
        }

        private void UpdateControllerVisual()
        {
            if (captureZoneVisual == null) return;

            var renderer = captureZoneVisual.GetComponent<Renderer>();
            if (renderer == null) return;

            Color color = ShrineColors[(int)_shrineType];

            if (_controllerId != null && _controllerId.Length > 0)
            {
                // Tint with controller's player color
                // For now, just make it more opaque
                color.a = 0.4f;
            }
            else
            {
                color.a = 0.2f;
            }

            renderer.material.color = color;
        }

        private void SetColor(Color color)
        {
            if (_baseRenderer != null && _baseRenderer.material != null)
            {
                _baseRenderer.material.color = color;

                // Set emission
                if (_baseRenderer.material.HasProperty("_EmissionColor"))
                {
                    _baseRenderer.material.EnableKeyword("_EMISSION");
                    _baseRenderer.material.SetColor("_EmissionColor", color * 0.3f);
                }
            }
        }

        /// <summary>
        /// Create a placeholder shrine mesh (for when no prefab is provided).
        /// </summary>
        public static GameObject CreatePlaceholder(ShrineType shrineType)
        {
            GameObject shrine = new GameObject($"Shrine_{shrineType}");

            // Base cylinder
            GameObject baseCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseCylinder.transform.SetParent(shrine.transform);
            baseCylinder.transform.localPosition = Vector3.zero;
            baseCylinder.transform.localScale = new Vector3(2f, 0.5f, 2f);

            // Remove collider
            var collider = baseCylinder.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            // Set material
            var renderer = baseCylinder.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = ShrineColors[(int)shrineType];
            }

            // Capture zone circle
            GameObject captureZone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            captureZone.name = "CaptureZone";
            captureZone.transform.SetParent(shrine.transform);
            captureZone.transform.localPosition = new Vector3(0, -0.2f, 0);
            captureZone.transform.localScale = new Vector3(6f, 0.05f, 6f);

            var zoneCollider = captureZone.GetComponent<Collider>();
            if (zoneCollider != null)
                Destroy(zoneCollider);

            var zoneRenderer = captureZone.GetComponent<Renderer>();
            if (zoneRenderer != null)
            {
                zoneRenderer.material = new Material(Shader.Find("Standard"));
                Color zoneColor = ShrineColors[(int)shrineType];
                zoneColor.a = 0.2f;
                zoneRenderer.material.color = zoneColor;

                // Make transparent
                zoneRenderer.material.SetFloat("_Mode", 3);
                zoneRenderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                zoneRenderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                zoneRenderer.material.SetInt("_ZWrite", 0);
                zoneRenderer.material.DisableKeyword("_ALPHATEST_ON");
                zoneRenderer.material.EnableKeyword("_ALPHABLEND_ON");
                zoneRenderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                zoneRenderer.material.renderQueue = 3000;
            }

            // Add Shrine component
            var shrineComponent = shrine.AddComponent<Shrine>();
            shrineComponent.captureZoneVisual = captureZone;

            return shrine;
        }
    }
}

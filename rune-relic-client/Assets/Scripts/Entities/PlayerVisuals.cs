using UnityEngine;
using RuneRelic.Network.Messages;
using RuneRelic.Utils;

namespace RuneRelic.Game
{
    /// <summary>
    /// Visual representation of a player. Handles mesh, materials, and effects.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        [Header("Form Prefabs")]
        [SerializeField] private GameObject[] formMeshes;  // 5 prefabs for each form

        [Header("Effects")]
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private ParticleSystem speedBuffEffect;
        [SerializeField] private ParticleSystem shieldBuffEffect;
        [SerializeField] private ParticleSystem invulnerableEffect;
        [SerializeField] private ParticleSystem evolutionEffect;
        [SerializeField] private ParticleSystem eliminationEffect;

        [Header("UI")]
        [SerializeField] private TextMesh nameTag;
        [SerializeField] private Transform healthBar;

        // State
        private byte[] _playerId;
        private int _colorIndex;
        private Form _currentForm = Form.Spark;
        private bool _alive = true;

        // Interpolation
        private Vector3 _previousPosition;
        private Vector3 _targetPosition;
        private Renderer _renderer;

        // Colors per player slot
        private static readonly Color[] PlayerColors = {
            new Color(0.2f, 0.6f, 1f),    // Blue
            new Color(1f, 0.3f, 0.3f),    // Red
            new Color(0.3f, 1f, 0.3f),    // Green
            new Color(1f, 1f, 0.3f)       // Yellow
        };

        public void Initialize(byte[] playerId, int colorIndex)
        {
            _playerId = playerId;
            _colorIndex = colorIndex;
            _previousPosition = transform.position;
            _targetPosition = transform.position;

            // Get renderer
            _renderer = GetComponent<Renderer>();
            if (_renderer == null)
                _renderer = GetComponentInChildren<Renderer>();

            // Set player color
            SetColor(PlayerColors[colorIndex % PlayerColors.Length]);

            // Set initial form size
            UpdateFormVisuals();

            // Set name tag
            if (nameTag != null)
            {
                nameTag.text = $"P{colorIndex + 1}";
            }
        }

        /// <summary>
        /// Update visual state from server data.
        /// </summary>
        public void UpdateFromState(PlayerStateUpdate state)
        {
            _previousPosition = _targetPosition;
            _targetPosition = FixedPoint.ToVector3(state.position);

            _alive = state.alive;

            // Update form if changed
            Form newForm = (Form)state.form;
            if (newForm != _currentForm)
            {
                SetForm(newForm);
            }

            // Update buffs
            UpdateBuffEffects(state.buffs);

            // Hide if eliminated
            if (!_alive)
            {
                SetEliminated();
            }
        }

        /// <summary>
        /// Interpolate position between previous and target.
        /// </summary>
        public void Interpolate(float t)
        {
            if (!_alive) return;

            Vector3 interpolated = Vector3.Lerp(_previousPosition, _targetPosition, t);
            transform.position = interpolated;

            // Rotate to face movement direction
            Vector3 direction = _targetPosition - _previousPosition;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }

        /// <summary>
        /// Change player form (evolution).
        /// </summary>
        public void SetForm(Form form)
        {
            Form oldForm = _currentForm;
            _currentForm = form;

            UpdateFormVisuals();

            // Play evolution effect
            if (form > oldForm && evolutionEffect != null)
            {
                evolutionEffect.Play();
            }
        }

        /// <summary>
        /// Mark player as eliminated.
        /// </summary>
        public void SetEliminated()
        {
            _alive = false;

            // Play elimination effect
            if (eliminationEffect != null)
            {
                eliminationEffect.Play();
            }

            // Fade out or disable
            if (_renderer != null)
            {
                Color color = _renderer.material.color;
                color.a = 0.3f;
                _renderer.material.color = color;
            }

            // Disable trail
            if (trail != null)
            {
                trail.enabled = false;
            }
        }

        /// <summary>
        /// Play ability activation effect.
        /// </summary>
        public void PlayAbilityEffect(int abilityType)
        {
            // TODO: Different effects per ability type
            // 0 = Dash, 1 = Phase Shift, 2 = Repel, 3 = Gravity Well, 4 = Consume
        }

        private void UpdateFormVisuals()
        {
            float radius = Constants.FORM_RADII[(int)_currentForm];
            float scale = radius * 2f;

            transform.localScale = new Vector3(scale, scale, scale);

            // Swap mesh if prefabs available
            if (formMeshes != null && formMeshes.Length > (int)_currentForm)
            {
                // Disable all, enable current
                for (int i = 0; i < formMeshes.Length; i++)
                {
                    if (formMeshes[i] != null)
                        formMeshes[i].SetActive(i == (int)_currentForm);
                }
            }

            // Update trail width
            if (trail != null)
            {
                trail.startWidth = radius * 0.5f;
                trail.endWidth = 0f;
            }
        }

        private void UpdateBuffEffects(PlayerBuffs buffs)
        {
            if (buffs == null) return;

            // Speed buff
            if (speedBuffEffect != null)
            {
                if (buffs.speed > 0 && !speedBuffEffect.isPlaying)
                    speedBuffEffect.Play();
                else if (buffs.speed == 0 && speedBuffEffect.isPlaying)
                    speedBuffEffect.Stop();
            }

            // Shield buff
            if (shieldBuffEffect != null)
            {
                if (buffs.shield > 0 && !shieldBuffEffect.isPlaying)
                    shieldBuffEffect.Play();
                else if (buffs.shield == 0 && shieldBuffEffect.isPlaying)
                    shieldBuffEffect.Stop();
            }

            // Invulnerable
            if (invulnerableEffect != null)
            {
                if (buffs.invulnerable > 0 && !invulnerableEffect.isPlaying)
                    invulnerableEffect.Play();
                else if (buffs.invulnerable == 0 && invulnerableEffect.isPlaying)
                    invulnerableEffect.Stop();
            }
        }

        private void SetColor(Color color)
        {
            if (_renderer != null && _renderer.material != null)
            {
                _renderer.material.color = color;
            }

            // Also set trail color
            if (trail != null)
            {
                trail.startColor = color;
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }
    }
}

using UnityEngine;
using RuneRelic.Network;
using RuneRelic.Network.Messages;
using RuneRelic.Utils;

namespace RuneRelic.Game
{
    /// <summary>
    /// Controls local player input and sends it to the server.
    /// Implements client-side prediction for responsive movement.
    /// </summary>
    public class PlayerController : MonoBehaviour, ILocalPlayerController
    {
        [Header("Input Settings")]
        [SerializeField] private float inputSendRate = 60f;  // Match server tick rate
        [SerializeField] private float deadzone = 0.1f;
        [SerializeField] private bool useMapBounds = true;

        private byte[] _playerId;
        private float _lastInputTime;
        private float _inputInterval;

        // Client-side prediction
        private Vector3 _predictedPosition;
        private float _predictedSpeed;
        private float _predictedRadius;
        private int _spawnZoneId = -1;
        private bool _spawnZoneActive;
        private float _abilityCooldown;

        // Input state
        private float _horizontalInput;
        private float _verticalInput;
        private bool _abilityPressed;

        public void Initialize(byte[] playerId)
        {
            _playerId = playerId;
            _inputInterval = 1f / inputSendRate;
            _predictedPosition = transform.position;

            // Get initial speed from form
            _predictedSpeed = Constants.FORM_SPEEDS[0];
            _predictedRadius = Constants.FORM_RADII[0];

            if (ArcaneCircuitMapLogic.TryGetSpawnZoneId(transform.position, _predictedRadius, out var zoneId))
            {
                _spawnZoneId = zoneId;
                _spawnZoneActive = true;
            }
        }

        private void Update()
        {
            if (GameClient.Instance?.CurrentState != GameState.Playing)
                return;

            // Capture input
            CaptureInput();

            // Apply client-side prediction
            ApplyPrediction();

            // Send input at fixed rate
            if (Time.time - _lastInputTime >= _inputInterval)
            {
                SendInput();
                _lastInputTime = Time.time;
            }
        }

        private void CaptureInput()
        {
            // Get raw input
            _horizontalInput = Input.GetAxisRaw("Horizontal");
            _verticalInput = Input.GetAxisRaw("Vertical");

            // Apply deadzone
            if (Mathf.Abs(_horizontalInput) < deadzone) _horizontalInput = 0;
            if (Mathf.Abs(_verticalInput) < deadzone) _verticalInput = 0;

            // Normalize diagonal movement
            Vector2 input = new Vector2(_horizontalInput, _verticalInput);
            if (input.magnitude > 1f)
            {
                input.Normalize();
                _horizontalInput = input.x;
                _verticalInput = input.y;
            }

            // Ability input (space or left mouse)
            _abilityPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
        }

        private void ApplyPrediction()
        {
            // Simple prediction: move in input direction at form speed
            if (Mathf.Abs(_horizontalInput) > 0.01f || Mathf.Abs(_verticalInput) > 0.01f)
            {
                Vector3 movement = new Vector3(_horizontalInput, 0, _verticalInput);
                movement *= _predictedSpeed * Time.deltaTime;

                Vector3 nextPosition = _predictedPosition + movement;

                if (!useMapBounds || ArcaneCircuitMapLogic.IsInsideMap(nextPosition, _predictedRadius, _spawnZoneId, _spawnZoneActive))
                {
                    _predictedPosition = nextPosition;
                }

                if (_spawnZoneActive && _spawnZoneId >= 0)
                {
                    if (!ArcaneCircuitMapLogic.IsInsideSpawnZone(_predictedPosition, _predictedRadius, _spawnZoneId))
                    {
                        _spawnZoneActive = false;
                    }
                }

                // Apply to transform
                transform.position = _predictedPosition;
            }
        }

        private async void SendInput()
        {
            var client = GameClient.Instance;
            if (client == null || !client.IsConnected) return;

            uint tick = GameManager.Instance?.GetClientTick() ?? 0;

            var input = GameInput.FromAxes(tick, _horizontalInput, _verticalInput, _abilityPressed);
            await client.SendInput(input);

            // Reset one-shot inputs
            _abilityPressed = false;
        }

        /// <summary>
        /// Called when server confirms position. Reconcile if needed.
        /// </summary>
        public void ReconcileWithServer(Vector3 serverPosition)
        {
            float distance = Vector3.Distance(_predictedPosition, serverPosition);

            // If prediction is too far off, snap to server position
            if (distance > 1f)
            {
                _predictedPosition = serverPosition;
                transform.position = serverPosition;
            }
            else if (distance > 0.1f)
            {
                // Smoothly correct small errors
                _predictedPosition = Vector3.Lerp(_predictedPosition, serverPosition, 0.3f);
            }
        }

        /// <summary>
        /// Update predicted speed based on form and buffs.
        /// </summary>
        public void UpdateSpeed(Form form, bool hasSpeedBuff, bool hasShrineSpeed)
        {
            float baseSpeed = Constants.FORM_SPEEDS[(int)form];
            _predictedRadius = Constants.FORM_RADII[(int)form];

            if (hasSpeedBuff) baseSpeed *= 1.4f;
            if (hasShrineSpeed) baseSpeed *= 1.2f;

            _predictedSpeed = baseSpeed;
        }

        public void UpdateRadius(float radius)
        {
            _predictedRadius = radius;
        }

        public void SetSpawnZone(int spawnZoneId, bool active)
        {
            _spawnZoneId = spawnZoneId;
            _spawnZoneActive = active;
        }

        public void UpdateAbilityCooldown(float cooldownSeconds)
        {
            _abilityCooldown = cooldownSeconds;
        }
    }
}

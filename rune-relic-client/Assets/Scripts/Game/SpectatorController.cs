using UnityEngine;
using RuneRelic.Network;
using RuneRelic.UI.Core;

namespace RuneRelic.Game
{
    /// <summary>
    /// Spectator camera controller for eliminated players.
    /// Allows cycling between remaining players or free camera.
    /// </summary>
    public class SpectatorController : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private float followHeight = 15f;
        [SerializeField] private float followDistance = 10f;
        [SerializeField] private float followSmoothness = 5f;
        [SerializeField] private float freeCamSpeed = 20f;
        [SerializeField] private float freeCamRotationSpeed = 100f;
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 30f;

        [Header("UI")]
        [SerializeField] private GameObject spectatorUI;
        [SerializeField] private UnityEngine.UI.Text spectatingText;
        [SerializeField] private UnityEngine.UI.Text controlsHintText;

        private Camera _camera;
        private Transform _followTarget;
        private int _currentTargetIndex;
        private SpectatorMode _mode = SpectatorMode.Follow;
        private float _currentZoom;
        private Vector3 _freeCamPosition;
        private Vector2 _freeCamRotation;
        private bool _isActive;

        private string[] _alivePlayerIds;

        public bool IsActive => _isActive;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            _currentZoom = followHeight;

            if (spectatorUI != null)
            {
                spectatorUI.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            HandleInput();

            switch (_mode)
            {
                case SpectatorMode.Follow:
                    UpdateFollowCamera();
                    break;
                case SpectatorMode.Free:
                    UpdateFreeCamera();
                    break;
                case SpectatorMode.Overview:
                    UpdateOverviewCamera();
                    break;
            }
        }

        /// <summary>
        /// Activate spectator mode.
        /// </summary>
        public void Activate()
        {
            _isActive = true;

            if (spectatorUI != null)
            {
                spectatorUI.SetActive(true);
            }

            // Find first alive player to follow
            UpdateAlivePlayerList();
            CycleTarget(0);

            UpdateUI();
            UIManager.Instance?.ShowNotification("You are now spectating", NotificationType.Info);
        }

        /// <summary>
        /// Deactivate spectator mode.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;

            if (spectatorUI != null)
            {
                spectatorUI.SetActive(false);
            }
        }

        private void HandleInput()
        {
            // Cycle targets
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                CycleTarget(1);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                CycleTarget(-1);
            }

            // Toggle mode
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                CycleMode();
            }

            // Free camera toggle
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (_mode == SpectatorMode.Free)
                {
                    SetMode(SpectatorMode.Follow);
                }
                else
                {
                    SetMode(SpectatorMode.Free);
                    _freeCamPosition = transform.position;
                    _freeCamRotation = new Vector2(transform.eulerAngles.x, transform.eulerAngles.y);
                }
            }

            // Overview toggle
            if (Input.GetKeyDown(KeyCode.O))
            {
                SetMode(SpectatorMode.Overview);
            }

            // Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _currentZoom = Mathf.Clamp(_currentZoom - scroll * zoomSpeed, minZoom, maxZoom);
            }
        }

        private void CycleTarget(int direction)
        {
            UpdateAlivePlayerList();

            if (_alivePlayerIds == null || _alivePlayerIds.Length == 0)
            {
                _followTarget = null;
                return;
            }

            _currentTargetIndex = (_currentTargetIndex + direction + _alivePlayerIds.Length) % _alivePlayerIds.Length;

            // Find the player visual for this ID
            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                string targetId = _alivePlayerIds[_currentTargetIndex];
                // Get player visual by ID (would need to expose this in GameManager)
                // For now, find by name
                var playerObj = GameObject.Find($"Player_{targetId.Substring(0, 8)}");
                if (playerObj != null)
                {
                    _followTarget = playerObj.transform;
                }
            }

            _mode = SpectatorMode.Follow;
            UpdateUI();
        }

        private void CycleMode()
        {
            int nextMode = ((int)_mode + 1) % 3;
            SetMode((SpectatorMode)nextMode);
        }

        private void SetMode(SpectatorMode mode)
        {
            _mode = mode;

            if (mode == SpectatorMode.Free)
            {
                _freeCamPosition = transform.position;
                _freeCamRotation = new Vector2(transform.eulerAngles.x, transform.eulerAngles.y);
            }

            UpdateUI();
        }

        private void UpdateFollowCamera()
        {
            if (_followTarget == null)
            {
                CycleTarget(0);
                return;
            }

            Vector3 targetPosition = _followTarget.position;
            Vector3 desiredPosition = targetPosition + new Vector3(0, _currentZoom, -followDistance);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSmoothness * Time.deltaTime);
            transform.LookAt(targetPosition);
        }

        private void UpdateFreeCamera()
        {
            // WASD movement
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            float upDown = 0f;

            if (Input.GetKey(KeyCode.Space)) upDown = 1f;
            if (Input.GetKey(KeyCode.LeftControl)) upDown = -1f;

            Vector3 movement = new Vector3(horizontal, upDown, vertical);
            movement = transform.TransformDirection(movement);
            _freeCamPosition += movement * freeCamSpeed * Time.deltaTime;

            // Mouse rotation (when right click held)
            if (Input.GetMouseButton(1))
            {
                _freeCamRotation.y += Input.GetAxis("Mouse X") * freeCamRotationSpeed * Time.deltaTime;
                _freeCamRotation.x -= Input.GetAxis("Mouse Y") * freeCamRotationSpeed * Time.deltaTime;
                _freeCamRotation.x = Mathf.Clamp(_freeCamRotation.x, -80f, 80f);
            }

            transform.position = _freeCamPosition;
            transform.rotation = Quaternion.Euler(_freeCamRotation.x, _freeCamRotation.y, 0);
        }

        private void UpdateOverviewCamera()
        {
            // Top-down view of entire arena
            Vector3 centerPosition = Vector3.zero;
            float arenaSize = Mathf.Max(Utils.Constants.ARENA_WIDTH, Utils.Constants.ARENA_HEIGHT);
            float overviewHeight = arenaSize * 0.7f;

            Vector3 targetPosition = new Vector3(centerPosition.x, overviewHeight, centerPosition.z);

            transform.position = Vector3.Lerp(transform.position, targetPosition, followSmoothness * Time.deltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(90, 0, 0), followSmoothness * Time.deltaTime);
        }

        private void UpdateAlivePlayerList()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.CurrentMatch == null)
            {
                _alivePlayerIds = new string[0];
                return;
            }

            var aliveList = new System.Collections.Generic.List<string>();
            foreach (var kvp in gameManager.CurrentMatch.Players)
            {
                if (kvp.Value.Alive)
                {
                    aliveList.Add(kvp.Key);
                }
            }

            _alivePlayerIds = aliveList.ToArray();
        }

        private void UpdateUI()
        {
            if (spectatingText != null)
            {
                switch (_mode)
                {
                    case SpectatorMode.Follow:
                        if (_alivePlayerIds != null && _alivePlayerIds.Length > 0)
                        {
                            spectatingText.text = $"Spectating: Player {_currentTargetIndex + 1}/{_alivePlayerIds.Length}";
                        }
                        else
                        {
                            spectatingText.text = "No players alive";
                        }
                        break;
                    case SpectatorMode.Free:
                        spectatingText.text = "Free Camera";
                        break;
                    case SpectatorMode.Overview:
                        spectatingText.text = "Overview";
                        break;
                }
            }

            if (controlsHintText != null)
            {
                controlsHintText.text = _mode switch
                {
                    SpectatorMode.Follow => "A/D: Cycle players | Tab: Change mode | F: Free cam | O: Overview",
                    SpectatorMode.Free => "WASD: Move | Right-click: Look | Space/Ctrl: Up/Down | Tab: Change mode",
                    SpectatorMode.Overview => "Scroll: Zoom | Tab: Change mode",
                    _ => ""
                };
            }
        }
    }

    public enum SpectatorMode
    {
        Follow,
        Free,
        Overview
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using RuneRelic.Network;
using RuneRelic.Network.Messages;
using RuneRelic.Utils;

namespace RuneRelic.Game
{
    public enum BotTargetMode
    {
        RunesThenShrines,
        RunesOnly,
        ShrinesOnly,
        RandomNodes
    }

    public enum BotNavMode
    {
        NavGraph,
        NavMesh
    }

    /// <summary>
    /// Lightweight bot controller for local testing and tuning.
    /// </summary>
    public class BotController : MonoBehaviour, ILocalPlayerController
    {
        private enum BotIntent
        {
            None,
            Runes,
            Shrine,
            Chase,
            Bait,
            Wander
        }

        [Header("Bot")]
        [SerializeField] private BotTargetMode targetMode = BotTargetMode.RunesThenShrines;
        [SerializeField] private BotNavMode navigationMode = BotNavMode.NavGraph;
        [SerializeField] private bool fallbackToNavGraph = true;
        [SerializeField] private float targetRefreshInterval = 0.75f;
        [SerializeField] private float waypointReachThreshold = 2f;
        [SerializeField] private float finalTargetThreshold = 1.5f;
        [SerializeField] private float stuckTimeout = 1.5f;
        [SerializeField] private float minProgressDistance = 0.25f;

        [Header("Steering")]
        [SerializeField] private float steeringAngleStep = 12f;
        [SerializeField] private int steeringChecks = 6;
        [SerializeField] private float directionSmoothing = 8f;

        [Header("Threat Avoidance")]
        [SerializeField] private float threatAwarenessRadius = 12f;
        [SerializeField] private float threatDangerRadius = 6f;
        [SerializeField] private float threatLeadTime = 0.35f;
        [SerializeField] private float threatAvoidWeight = 1.4f;
        [SerializeField] private bool avoidLargerOnly = true;

        [Header("Combat")]
        [SerializeField] private bool chaseSmallerPlayers = true;
        [SerializeField] private float chaseRadius = 14f;
        [SerializeField] private float chaseSizeRatio = 0.9f;
        [SerializeField] private float chaseLeadTime = 0.25f;
        [SerializeField] private bool denyShrines = true;
        [SerializeField] private float shrineDenyRadius = 18f;
        [SerializeField] private float shrineBaitRadius = 6f;
        [SerializeField] private float shrineOrbitRadius = 4f;
        [SerializeField] private float shrineOrbitSpeed = 1.25f;

        [Header("Ability")]
        [SerializeField] private bool useAbilityOnPanic = true;
        [SerializeField] private float abilityTriggerCooldown = 0.25f;

        [Header("Input Settings")]
        [SerializeField] private float inputSendRate = 60f;
        [SerializeField] private bool useMapBounds = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool showDebugGizmosAlways = true;
        [SerializeField] private bool showThreatGizmos = true;
        [SerializeField] private bool showPathGizmos = true;
        [SerializeField] private Color debugTargetColor = new Color(0.2f, 0.9f, 1f, 0.9f);
        [SerializeField] private Color debugPathColor = new Color(0.3f, 1f, 0.5f, 0.8f);
        [SerializeField] private Color debugThreatColor = new Color(1f, 0.4f, 0.2f, 0.8f);
        [SerializeField] private Color debugDangerColor = new Color(1f, 0.1f, 0.1f, 0.9f);
        [SerializeField] private float debugNodeSize = 0.4f;
        [SerializeField] private float debugTargetSize = 0.6f;
        [SerializeField] private float debugThreatConeAngle = 35f;

        private byte[] _playerId;
        private string _playerIdHex;
        private float _lastInputTime;
        private float _inputInterval;

        private Vector3 _predictedPosition;
        private float _predictedSpeed;
        private float _predictedRadius;
        private int _spawnZoneId = -1;
        private bool _spawnZoneActive;
        private float _abilityCooldown;
        private float _abilityLockoutUntil;
        private bool _abilityPressed;

        private ArcaneCircuitNavGraph _navGraph;
        private readonly List<Vector2> _navPath = new List<Vector2>();
        private readonly List<Vector3> _waypoints = new List<Vector3>();
        private int _waypointIndex;

        private Vector3 _currentTarget;
        private bool _hasTarget;
        private float _lastTargetTime;

        private Vector3 _currentDirection = Vector3.forward;
        private Vector3 _lastProgressPosition;
        private float _lastProgressTime;
        private float _lastThreatTime;
        private Vector3 _lastAvoidVector;
        private bool _lastPanic;
        private float _orbitPhase;
        private BotIntent _currentIntent = BotIntent.None;

        private float _horizontalInput;
        private float _verticalInput;

        public void Initialize(byte[] playerId)
        {
            _playerId = playerId;
            _playerIdHex = BytesToHex(playerId);
            _inputInterval = 1f / inputSendRate;
            _predictedPosition = transform.position;
            _predictedSpeed = Constants.FORM_SPEEDS[0];
            _predictedRadius = Constants.FORM_RADII[0];
            _lastProgressPosition = _predictedPosition;
            _lastProgressTime = Time.time;
            _orbitPhase = ComputePhase(playerId);

            if (ArcaneCircuitMapLogic.TryGetSpawnZoneId(transform.position, _predictedRadius, out var zoneId))
            {
                _spawnZoneId = zoneId;
                _spawnZoneActive = true;
            }
        }

        private void Update()
        {
            if (GameClient.Instance?.CurrentState != GameState.Playing)
            {
                return;
            }

            if (!IsAlive())
            {
                _horizontalInput = 0f;
                _verticalInput = 0f;
                return;
            }

            ResolveNavGraph();
            UpdateTarget();
            FollowPath();
            ApplyPrediction();
            if (_hasTarget)
            {
                UpdateProgress(_predictedPosition);
            }
            UpdateAbilityIntent();

            if (Time.time - _lastInputTime >= _inputInterval)
            {
                SendInput();
                _lastInputTime = Time.time;
            }
        }

        private bool IsAlive()
        {
            var match = GameManager.Instance?.CurrentMatch;
            if (match == null || string.IsNullOrEmpty(_playerIdHex))
            {
                return false;
            }

            if (match.Players.TryGetValue(_playerIdHex, out var state))
            {
                return state.Alive;
            }

            return false;
        }

        private void ResolveNavGraph()
        {
            if (_navGraph != null)
            {
                return;
            }

            var builder = FindObjectOfType<ArcaneCircuitMapBuilder>();
            if (builder != null && builder.NavGraph != null)
            {
                _navGraph = builder.NavGraph;
                return;
            }

            _navGraph = ArcaneCircuitNavGraph.BuildDefault();
        }

        private void UpdateTarget()
        {
            if (_spawnZoneActive && _spawnZoneId >= 0)
            {
                var anchor = ArcaneCircuitMapData.SpawnZones[_spawnZoneId].Anchor;
                Vector3 anchorTarget = new Vector3(anchor.x, _predictedPosition.y, anchor.y);
                if (!_hasTarget || Vector3.Distance(_currentTarget, anchorTarget) > 0.1f)
                {
                    SetTarget(anchorTarget);
                    _currentIntent = BotIntent.Wander;
                }
                return;
            }

            if (_hasTarget && _lastThreatTime > 0f && Time.time - _lastThreatTime < targetRefreshInterval * 0.5f)
            {
                return;
            }

            if (_hasTarget && Time.time - _lastTargetTime < targetRefreshInterval)
            {
                return;
            }

            if (TrySelectCombatTarget(out var combatTarget, out var combatIntent))
            {
                _currentIntent = combatIntent;
                SetTarget(combatTarget);
                return;
            }

            if (TrySelectTarget(out var target, out var intent))
            {
                _currentIntent = intent;
                SetTarget(target);
                return;
            }

            _currentIntent = BotIntent.Wander;
            SetTarget(GetFallbackTarget());
        }

        private bool TrySelectTarget(out Vector3 target, out BotIntent intent)
        {
            target = Vector3.zero;
            var manager = GameManager.Instance;
            if (manager == null)
            {
                intent = BotIntent.None;
                return false;
            }

            switch (targetMode)
            {
                case BotTargetMode.RunesOnly:
                    intent = BotIntent.Runes;
                    return manager.TryGetClosestRune(_predictedPosition, out target);
                case BotTargetMode.ShrinesOnly:
                    intent = BotIntent.Shrine;
                    return manager.TryGetClosestShrine(_predictedPosition, out target);
                case BotTargetMode.RunesThenShrines:
                    if (manager.TryGetClosestRune(_predictedPosition, out target))
                    {
                        intent = BotIntent.Runes;
                        return true;
                    }
                    intent = BotIntent.Shrine;
                    return manager.TryGetClosestShrine(_predictedPosition, out target);
                case BotTargetMode.RandomNodes:
                    target = GetFallbackTarget();
                    intent = BotIntent.Wander;
                    return true;
            }

            intent = BotIntent.None;
            return false;
        }

        private bool TrySelectCombatTarget(out Vector3 target, out BotIntent intent)
        {
            target = Vector3.zero;
            intent = BotIntent.None;

            if (chaseSmallerPlayers && TryFindChaseTarget(out target))
            {
                intent = BotIntent.Chase;
                return true;
            }

            if (denyShrines && TryFindShrineTarget(out target, out bool bait))
            {
                intent = bait ? BotIntent.Bait : BotIntent.Shrine;
                return true;
            }

            return false;
        }

        private bool TryFindChaseTarget(out Vector3 target)
        {
            target = Vector3.zero;
            var match = GameManager.Instance?.CurrentMatch;
            if (match == null || string.IsNullOrEmpty(_playerIdHex))
            {
                return false;
            }

            float radius = Mathf.Max(0.1f, chaseRadius);
            float radiusSq = radius * radius;
            float sizeThreshold = _predictedRadius * Mathf.Clamp(chaseSizeRatio, 0.1f, 1f);
            float bestDistSq = float.MaxValue;

            foreach (var kvp in match.Players)
            {
                if (kvp.Key == _playerIdHex)
                {
                    continue;
                }

                var state = kvp.Value;
                if (!state.Alive)
                {
                    continue;
                }

                if (state.Radius >= sizeThreshold)
                {
                    continue;
                }

                Vector3 predicted = state.TargetPosition + state.Velocity * chaseLeadTime;
                predicted.y = _predictedPosition.y;
                float distSq = (predicted - _predictedPosition).sqrMagnitude;
                if (distSq > radiusSq)
                {
                    continue;
                }

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    target = predicted;
                }
            }

            return bestDistSq < float.MaxValue;
        }

        private bool TryFindShrineTarget(out Vector3 target, out bool bait)
        {
            target = Vector3.zero;
            bait = false;

            var manager = GameManager.Instance;
            if (manager == null)
            {
                return false;
            }

            if (!manager.TryGetClosestShrine(_predictedPosition, out var shrinePos))
            {
                return false;
            }

            float dist = Vector3.Distance(_predictedPosition, shrinePos);
            if (dist > shrineDenyRadius)
            {
                return false;
            }

            if (dist <= shrineBaitRadius && HasShrineAdvantage(shrinePos))
            {
                bait = true;
                target = GetShrineOrbitPoint(shrinePos);
                return true;
            }

            target = shrinePos;
            return true;
        }

        private bool HasShrineAdvantage(Vector3 shrinePos)
        {
            var match = GameManager.Instance?.CurrentMatch;
            if (match == null)
            {
                return false;
            }

            float checkRadius = shrineBaitRadius * 1.5f;
            float checkRadiusSq = checkRadius * checkRadius;
            bool enemyNearby = false;

            foreach (var kvp in match.Players)
            {
                if (kvp.Key == _playerIdHex)
                {
                    continue;
                }

                var state = kvp.Value;
                if (!state.Alive)
                {
                    continue;
                }

                Vector3 pos = state.TargetPosition;
                pos.y = shrinePos.y;
                float distSq = (pos - shrinePos).sqrMagnitude;
                if (distSq > checkRadiusSq)
                {
                    continue;
                }

                enemyNearby = true;
                if (state.Radius >= _predictedRadius * Mathf.Clamp(chaseSizeRatio, 0.1f, 1f))
                {
                    return false;
                }
            }

            return enemyNearby;
        }

        private Vector3 GetShrineOrbitPoint(Vector3 shrinePos)
        {
            float angle = Time.time * shrineOrbitSpeed + _orbitPhase;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * shrineOrbitRadius;
            return shrinePos + offset;
        }

        private Vector3 GetFallbackTarget()
        {
            if (_navGraph != null && _navGraph.Nodes.Count > 0)
            {
                int index = Random.Range(0, _navGraph.Nodes.Count);
                Vector2 node = _navGraph.Nodes[index].Position;
                return new Vector3(node.x, _predictedPosition.y, node.y);
            }

            Vector2 offset = Random.insideUnitCircle * 10f;
            return new Vector3(_predictedPosition.x + offset.x, _predictedPosition.y, _predictedPosition.z + offset.y);
        }

        private void SetTarget(Vector3 target)
        {
            _currentTarget = target;
            _hasTarget = true;
            _lastTargetTime = Time.time;
            BuildPath(target);
        }

        private void BuildPath(Vector3 target)
        {
            _waypoints.Clear();
            _waypointIndex = 0;

            bool built = false;
            if (navigationMode == BotNavMode.NavMesh)
            {
                built = TryBuildNavMeshPath(_predictedPosition, target);
                if (!built && fallbackToNavGraph)
                {
                    built = TryBuildNavGraphPath(_predictedPosition, target);
                }
            }
            else
            {
                built = TryBuildNavGraphPath(_predictedPosition, target);
            }

            if (!built)
            {
                _waypoints.Add(target);
            }
        }

        private bool TryBuildNavMeshPath(Vector3 start, Vector3 target)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start, target, NavMesh.AllAreas, path))
            {
                return false;
            }

            if (path.corners == null || path.corners.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < path.corners.Length; i++)
            {
                AppendWaypoint(path.corners[i], 0.5f);
            }

            AppendWaypoint(target, 0.25f);
            return _waypoints.Count > 0;
        }

        private bool TryBuildNavGraphPath(Vector3 start, Vector3 target)
        {
            if (_navGraph == null)
            {
                return false;
            }

            _navPath.Clear();
            Vector2 start2d = new Vector2(start.x, start.z);
            Vector2 end2d = new Vector2(target.x, target.z);
            if (!_navGraph.TryFindPath(start2d, end2d, _navPath))
            {
                return false;
            }

            for (int i = 0; i < _navPath.Count; i++)
            {
                Vector2 node = _navPath[i];
                AppendWaypoint(new Vector3(node.x, start.y, node.y), 0.5f);
            }

            AppendWaypoint(target, 0.25f);
            return _waypoints.Count > 0;
        }

        private void AppendWaypoint(Vector3 waypoint, float minSpacing)
        {
            if (_waypoints.Count == 0)
            {
                _waypoints.Add(waypoint);
                return;
            }

            if (Vector3.Distance(_waypoints[_waypoints.Count - 1], waypoint) >= minSpacing)
            {
                _waypoints.Add(waypoint);
            }
        }

        private void FollowPath()
        {
            if (!_hasTarget || _waypoints.Count == 0)
            {
                _horizontalInput = 0f;
                _verticalInput = 0f;
                return;
            }

            Vector3 position = _predictedPosition;
            float threshold = _waypointIndex >= _waypoints.Count - 1 ? finalTargetThreshold : waypointReachThreshold;

            while (_waypointIndex < _waypoints.Count &&
                   Vector3.Distance(position, _waypoints[_waypointIndex]) <= threshold)
            {
                _waypointIndex++;
                if (_waypointIndex >= _waypoints.Count)
                {
                    _hasTarget = false;
                    _horizontalInput = 0f;
                    _verticalInput = 0f;
                    return;
                }
            }

            Vector3 target = _waypoints[_waypointIndex];
            Vector3 desired = target - position;
            desired.y = 0f;

            if (desired.sqrMagnitude < 0.0001f)
            {
                _horizontalInput = 0f;
                _verticalInput = 0f;
                return;
            }

            Vector3 desiredDir = desired.normalized;
            bool panic;
            Vector3 avoid = GetAvoidVector(position, out panic);
            if (avoid.sqrMagnitude > 0.0001f)
            {
                _lastThreatTime = Time.time;
                if (panic)
                {
                    desiredDir = avoid.normalized;
                }
                else
                {
                    desiredDir = (desiredDir + avoid * threatAvoidWeight).normalized;
                }
            }

            Vector3 safeDir = FindSafeDirection(position, desiredDir);
            if (safeDir.sqrMagnitude < 0.0001f)
            {
                safeDir = FindSafeDirection(position, Quaternion.Euler(0f, 45f, 0f) * desiredDir);
            }

            if (safeDir.sqrMagnitude < 0.0001f)
            {
                _horizontalInput = 0f;
                _verticalInput = 0f;
                return;
            }

            _currentDirection = Vector3.Slerp(_currentDirection, safeDir, Time.deltaTime * directionSmoothing);
            _currentDirection.y = 0f;

            Vector2 input = new Vector2(_currentDirection.x, _currentDirection.z);
            if (input.magnitude > 1f)
            {
                input.Normalize();
            }

            _horizontalInput = input.x;
            _verticalInput = input.y;
        }

        private Vector3 GetAvoidVector(Vector3 position, out bool panic)
        {
            panic = false;
            var match = GameManager.Instance?.CurrentMatch;
            if (match == null)
            {
                _lastPanic = false;
                _lastAvoidVector = Vector3.zero;
                return Vector3.zero;
            }

            float awareness = Mathf.Max(0.5f, threatAwarenessRadius);
            float danger = Mathf.Max(0.1f, threatDangerRadius);
            float awarenessSq = awareness * awareness;
            float dangerSq = danger * danger;
            Vector3 sum = Vector3.zero;

            foreach (var kvp in match.Players)
            {
                if (kvp.Key == _playerIdHex)
                {
                    continue;
                }

                var state = kvp.Value;
                if (!state.Alive)
                {
                    continue;
                }

                if (avoidLargerOnly && state.Radius <= _predictedRadius * 1.05f)
                {
                    continue;
                }

                Vector3 otherPos = state.TargetPosition;
                otherPos.y = position.y;
                Vector3 predicted = otherPos + state.Velocity * threatLeadTime;
                predicted.y = position.y;

                Vector3 delta = position - predicted;
                float distSq = delta.sqrMagnitude;
                if (distSq > awarenessSq)
                {
                    continue;
                }

                float dist = Mathf.Sqrt(distSq);
                float weight = 1f - Mathf.Clamp01(dist / awareness);
                if (delta.sqrMagnitude > 0.0001f)
                {
                    sum += delta.normalized * weight;
                }

                if (distSq <= dangerSq)
                {
                    panic = true;
                }
            }

            _lastPanic = panic;
            _lastAvoidVector = sum;
            return sum;
        }

        private void UpdateProgress(Vector3 position)
        {
            if (Vector3.Distance(position, _lastProgressPosition) >= minProgressDistance)
            {
                _lastProgressPosition = position;
                _lastProgressTime = Time.time;
                return;
            }

            if (Time.time - _lastProgressTime >= stuckTimeout)
            {
                _hasTarget = false;
                _waypoints.Clear();
                _waypointIndex = 0;
                _lastProgressTime = Time.time;
            }
        }

        private void UpdateAbilityIntent()
        {
            _abilityPressed = false;

            if (!useAbilityOnPanic)
            {
                return;
            }

            if (_abilityCooldown > 0f || Time.time < _abilityLockoutUntil)
            {
                return;
            }

            if (_lastPanic)
            {
                _abilityPressed = true;
                _abilityLockoutUntil = Time.time + Mathf.Max(0.05f, abilityTriggerCooldown);
            }
        }

        private Vector3 FindSafeDirection(Vector3 position, Vector3 desiredDir)
        {
            if (!useMapBounds)
            {
                return desiredDir;
            }

            if (IsDirectionSafe(position, desiredDir))
            {
                return desiredDir;
            }

            int maxChecks = Mathf.Max(1, steeringChecks);
            for (int i = 1; i <= maxChecks; i++)
            {
                float angle = steeringAngleStep * i;
                Vector3 left = Quaternion.AngleAxis(angle, Vector3.up) * desiredDir;
                if (IsDirectionSafe(position, left))
                {
                    return left;
                }

                Vector3 right = Quaternion.AngleAxis(-angle, Vector3.up) * desiredDir;
                if (IsDirectionSafe(position, right))
                {
                    return right;
                }
            }

            return Vector3.zero;
        }

        private bool IsDirectionSafe(Vector3 position, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float step = _predictedSpeed * Time.deltaTime;
            Vector3 nextPosition = position + direction.normalized * step;
            return ArcaneCircuitMapLogic.IsInsideMap(nextPosition, _predictedRadius, _spawnZoneId, _spawnZoneActive);
        }

        private void ApplyPrediction()
        {
            if (Mathf.Abs(_horizontalInput) < 0.01f && Mathf.Abs(_verticalInput) < 0.01f)
            {
                return;
            }

            Vector3 movement = new Vector3(_horizontalInput, 0f, _verticalInput);
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

            transform.position = _predictedPosition;
        }

        private async void SendInput()
        {
            var client = GameClient.Instance;
            if (client == null || !client.IsConnected)
            {
                return;
            }

            uint tick = GameManager.Instance?.GetClientTick() ?? 0;
            var input = GameInput.FromAxes(tick, _horizontalInput, _verticalInput, _abilityPressed);
            await client.SendInput(input);

            _abilityPressed = false;
        }

        public void UpdateSpeed(Form form, bool hasSpeedBuff, bool hasShrineSpeed)
        {
            float baseSpeed = Constants.FORM_SPEEDS[(int)form];
            _predictedRadius = Constants.FORM_RADII[(int)form];

            if (hasSpeedBuff)
            {
                baseSpeed *= 1.4f;
            }

            if (hasShrineSpeed)
            {
                baseSpeed *= 1.2f;
            }

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

        private static string BytesToHex(byte[] bytes)
        {
            if (bytes == null)
            {
                return "";
            }

            return System.BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        private static float ComputePhase(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return Random.value * Mathf.PI * 2f;
            }

            int hash = 17;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash = (hash * 31) + bytes[i];
            }

            float phase = Mathf.Abs(hash % 360) * Mathf.Deg2Rad;
            return phase;
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !showDebugGizmosAlways)
            {
                return;
            }

            DrawDebugGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos || showDebugGizmosAlways)
            {
                return;
            }

            DrawDebugGizmos();
        }

        private void DrawDebugGizmos()
        {
            Vector3 position = Application.isPlaying ? _predictedPosition : transform.position;

            if (showThreatGizmos)
            {
                Gizmos.color = debugThreatColor;
                DrawWireCircle(position, threatAwarenessRadius);

                Gizmos.color = debugDangerColor;
                DrawWireCircle(position, threatDangerRadius);

                if (_lastAvoidVector.sqrMagnitude > 0.001f)
                {
                    Gizmos.color = debugThreatColor;
                    Vector3 dir = _lastAvoidVector.normalized;
                    Gizmos.DrawLine(position, position + dir * (threatAwarenessRadius * 0.6f));

                    float halfAngle = debugThreatConeAngle;
                    Vector3 left = Quaternion.AngleAxis(halfAngle, Vector3.up) * dir;
                    Vector3 right = Quaternion.AngleAxis(-halfAngle, Vector3.up) * dir;
                    Gizmos.DrawLine(position, position + left * (threatAwarenessRadius * 0.45f));
                    Gizmos.DrawLine(position, position + right * (threatAwarenessRadius * 0.45f));
                }
            }

            if (showPathGizmos && _waypoints.Count > 0)
            {
                Gizmos.color = debugPathColor;
                Vector3 prev = position;
                for (int i = _waypointIndex; i < _waypoints.Count; i++)
                {
                    Vector3 point = _waypoints[i];
                    Gizmos.DrawLine(prev, point);
                    Gizmos.DrawSphere(point, debugNodeSize);
                    prev = point;
                }
            }

            if (_hasTarget)
            {
                Gizmos.color = debugTargetColor;
                Gizmos.DrawLine(position, _currentTarget);
                Gizmos.DrawSphere(_currentTarget, debugTargetSize);
            }
        }

        private void DrawWireCircle(Vector3 center, float radius)
        {
            float clampedRadius = Mathf.Max(0.1f, radius);
            int segments = 32;
            float step = Mathf.PI * 2f / segments;
            Vector3 prev = center + new Vector3(clampedRadius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * clampedRadius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}

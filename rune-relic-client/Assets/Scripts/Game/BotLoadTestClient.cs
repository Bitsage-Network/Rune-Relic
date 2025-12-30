using System;
using System.Collections.Generic;
using UnityEngine;
using RuneRelic.Network;
using RuneRelic.Network.Messages;
using RuneRelic.Utils;

namespace RuneRelic.Game
{
    public sealed class BotLoadTestClient : IDisposable
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

        private sealed class BotPlayerState
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Radius;
            public bool Alive;
        }

        private sealed class BotShrineState
        {
            public Vector3 Position;
            public bool Active;
        }

        private readonly string _serverUrl;
        private readonly MatchMode _matchMode;
        private readonly BotBehaviorSettings _settings;
        private readonly ArcaneCircuitNavGraph _navGraph;
        private readonly float _readyDelay;
        private readonly bool _autoRequeue;
        private readonly bool _logEvents;
        private readonly Action<string> _logSink;
        private readonly string _name;

        private WebSocketClient _socket;
        private bool _connected;
        private bool _authenticated;
        private bool _inMatch;
        private bool _playing;
        private float _readyAt;

        private readonly Queue<string> _incomingMessages = new Queue<string>();
        private readonly object _messageLock = new object();

        private byte[] _playerId;
        private string _playerIdHex;

        private readonly Dictionary<string, BotPlayerState> _players = new Dictionary<string, BotPlayerState>();
        private readonly Dictionary<uint, Vector3> _runes = new Dictionary<uint, Vector3>();
        private readonly Dictionary<uint, BotShrineState> _shrines = new Dictionary<uint, BotShrineState>();

        private uint _clientTick;
        private float _tickAccumulator;
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

        private readonly List<Vector2> _navPath = new List<Vector2>();
        private readonly List<Vector3> _waypoints = new List<Vector3>();
        private int _waypointIndex;

        private Vector3 _currentTarget;
        private bool _hasTarget;
        private float _lastTargetTime;
        private float _lastThreatTime;
        private Vector3 _lastAvoidVector;
        private bool _lastPanic;
        private float _orbitPhase;
        private BotIntent _currentIntent = BotIntent.None;

        private Vector3 _currentDirection = Vector3.forward;
        private Vector3 _lastProgressPosition;
        private float _lastProgressTime;

        private float _horizontalInput;
        private float _verticalInput;

        public BotLoadTestClient(
            string serverUrl,
            MatchMode matchMode,
            BotBehaviorSettings settings,
            ArcaneCircuitNavGraph navGraph,
            float readyDelay,
            bool autoRequeue,
            bool logEvents,
            Action<string> logSink,
            string name)
        {
            _serverUrl = serverUrl;
            _matchMode = matchMode;
            _settings = settings;
            _navGraph = navGraph ?? ArcaneCircuitNavGraph.BuildDefault();
            _readyDelay = readyDelay;
            _autoRequeue = autoRequeue;
            _logEvents = logEvents;
            _logSink = logSink;
            _name = name;
            _inputInterval = 1f / Mathf.Max(1f, settings.InputSendRate);
            _playerId = Guid.NewGuid().ToByteArray();
            _playerIdHex = BytesToHex(_playerId);
            _orbitPhase = ComputePhase(_playerId);
        }

        public async void Connect()
        {
            if (_socket != null)
            {
                return;
            }

            _socket = new WebSocketClient(_serverUrl);
            _socket.OnOpen += HandleOpen;
            _socket.OnClose += HandleClose;
            _socket.OnError += HandleError;
            _socket.OnMessage += HandleMessage;

            try
            {
                await _socket.Connect();
            }
            catch (Exception ex)
            {
                ReportError($"[BotLoadTestClient:{_name}] Connect failed: {ex.Message}");
            }
        }

        public void Tick(float deltaTime)
        {
            if (_socket == null)
            {
                return;
            }

            _socket.DispatchMessages();
            ProcessIncomingMessages();

            if (_readyAt > 0f && Time.time >= _readyAt)
            {
                _readyAt = 0f;
                _ = Send(new Ready());
            }

            if (!_playing)
            {
                return;
            }

            AdvanceTick(deltaTime);
            UpdateTarget();
            FollowPath(deltaTime);
            ApplyPrediction(deltaTime);
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

        public void Dispose()
        {
            if (_socket == null)
            {
                return;
            }

            _socket.OnOpen -= HandleOpen;
            _socket.OnClose -= HandleClose;
            _socket.OnError -= HandleError;
            _socket.OnMessage -= HandleMessage;
            _ = _socket.Close();
            _socket.Dispose();
            _socket = null;
        }

        private void HandleOpen()
        {
            _connected = true;
            _ = Send(new AuthRequest(_playerId));
        }

        private void HandleClose()
        {
            _connected = false;
            _authenticated = false;
            _inMatch = false;
            _playing = false;
        }

        private void HandleError(string error)
        {
            ReportError($"[BotLoadTestClient:{_name}] Socket error: {error}");
        }

        private void HandleMessage(string json)
        {
            lock (_messageLock)
            {
                _incomingMessages.Enqueue(json);
            }
        }

        private void ProcessIncomingMessages()
        {
            lock (_messageLock)
            {
                while (_incomingMessages.Count > 0)
                {
                    string json = _incomingMessages.Dequeue();
                    ParseAndDispatchMessage(json);
                }
            }
        }

        private void ParseAndDispatchMessage(string json)
        {
            try
            {
                var baseMsg = JsonUtility.FromJson<ServerMessage>(json);
                switch (baseMsg.type)
                {
                    case "auth_result":
                        HandleAuthResult(JsonUtility.FromJson<AuthResult>(json));
                        break;
                    case "matchmaking":
                        // Ignore for bots
                        break;
                    case "match_found":
                        HandleMatchFound(JsonUtility.FromJson<MatchFoundInfo>(json));
                        break;
                    case "match_start":
                        HandleMatchStart(JsonUtility.FromJson<MatchStartInfo>(json));
                        break;
                    case "state":
                        HandleStateUpdate(JsonUtility.FromJson<GameStateUpdate>(json));
                        break;
                    case "event":
                        HandleMatchEvent(JsonUtility.FromJson<MatchEvent>(json));
                        break;
                    case "match_end":
                        HandleMatchEnd(JsonUtility.FromJson<MatchEndInfo>(json));
                        break;
                }
            }
            catch (Exception ex)
            {
                ReportError($"[BotLoadTestClient:{_name}] Parse failed: {ex.Message}");
            }
        }

        private void HandleAuthResult(AuthResult result)
        {
            _authenticated = result.success;
            if (!_authenticated)
            {
                ReportError($"[BotLoadTestClient:{_name}] Auth failed.");
                return;
            }

            _ = Send(new MatchmakingRequest(_matchMode));
        }

        private void HandleMatchFound(MatchFoundInfo info)
        {
            _inMatch = true;
            _readyAt = Time.time + Mathf.Max(0f, _readyDelay);
        }

        private void HandleMatchStart(MatchStartInfo info)
        {
            _inMatch = true;
            _players.Clear();
            _runes.Clear();
            _shrines.Clear();
            _clientTick = 0;
            _tickAccumulator = 0f;
            _lastTargetTime = 0f;
            _lastThreatTime = 0f;
            _hasTarget = false;
            _waypoints.Clear();
            _waypointIndex = 0;

            foreach (var player in info.players)
            {
                string id = BytesToHex(player.player_id);
                var state = new BotPlayerState
                {
                    Position = FixedPoint.ToVector3(player.position),
                    Velocity = Vector3.zero,
                    Radius = Constants.FORM_RADII[0],
                    Alive = true
                };
                _players[id] = state;

                if (id == _playerIdHex)
                {
                    _predictedPosition = state.Position;
                    _predictedSpeed = Constants.FORM_SPEEDS[0];
                    _predictedRadius = state.Radius;
                    _lastProgressPosition = _predictedPosition;
                    _lastProgressTime = Time.time;
                }
            }
        }

        private void HandleStateUpdate(GameStateUpdate update)
        {
            if (update.players != null)
            {
                foreach (var playerUpdate in update.players)
                {
                    string id = BytesToHex(playerUpdate.player_id);
                    if (!_players.TryGetValue(id, out var state))
                    {
                        state = new BotPlayerState();
                        _players[id] = state;
                    }

                    state.Position = FixedPoint.ToVector3(playerUpdate.position);
                    state.Velocity = FixedPoint.VelocityToVector3(playerUpdate.velocity);
                    state.Radius = FixedPoint.ToFloat(playerUpdate.radius);
                    state.Alive = playerUpdate.alive;

                    if (id == _playerIdHex)
                    {
                        _spawnZoneId = playerUpdate.spawn_zone_id;
                        _spawnZoneActive = playerUpdate.spawn_zone_active;
                        _abilityCooldown = FixedPoint.ToFloat(playerUpdate.ability_cooldown);
                        _predictedRadius = state.Radius;
                        _predictedPosition = Vector3.Lerp(_predictedPosition, state.Position, 0.6f);

                        float baseSpeed = Constants.FORM_SPEEDS[Mathf.Clamp(playerUpdate.form, 0, Constants.FORM_SPEEDS.Length - 1)];
                        if (playerUpdate.buffs != null)
                        {
                            if (playerUpdate.buffs.speed > 0)
                            {
                                baseSpeed *= 1.4f;
                            }
                            if (playerUpdate.buffs.shrine_buffs != null &&
                                playerUpdate.buffs.shrine_buffs.Contains((int)ShrineType.Speed))
                            {
                                baseSpeed *= 1.2f;
                            }
                        }
                        _predictedSpeed = baseSpeed;
                    }
                }
            }

            if (update.runes != null)
            {
                foreach (var rune in update.runes)
                {
                    if (rune.collected)
                    {
                        _runes.Remove(rune.id);
                        continue;
                    }

                    _runes[rune.id] = FixedPoint.ToVector3(rune.position);
                }
            }

            if (update.shrines != null)
            {
                foreach (var shrine in update.shrines)
                {
                    if (!_shrines.TryGetValue(shrine.id, out var state))
                    {
                        state = new BotShrineState();
                        _shrines[shrine.id] = state;
                    }

                    state.Position = FixedPoint.ToVector3(shrine.position);
                    state.Active = shrine.active;
                }
            }
        }

        private void HandleMatchEvent(MatchEvent evt)
        {
            string eventType = ResolveEventType(evt);
            if (eventType == "match_started")
            {
                _playing = true;
                return;
            }

            if (eventType == "rune_spawned")
            {
                if (evt.position != null && evt.position.Length >= 2)
                {
                    _runes[evt.rune_id] = FixedPoint.ToVector3(evt.position);
                }
                return;
            }

            if (eventType == "rune_collected")
            {
                _runes.Remove(evt.rune_id);
                return;
            }

            if (eventType == "player_eliminated")
            {
                string victim = BytesToHex(evt.victim_id);
                if (_players.TryGetValue(victim, out var state))
                {
                    state.Alive = false;
                }
            }
        }

        private void HandleMatchEnd(MatchEndInfo info)
        {
            _playing = false;
            _inMatch = false;

            if (_autoRequeue && _authenticated)
            {
                _ = Send(new MatchmakingRequest(_matchMode));
            }
        }

        private void AdvanceTick(float deltaTime)
        {
            _tickAccumulator += deltaTime;
            while (_tickAccumulator >= Constants.TICK_DURATION)
            {
                _tickAccumulator -= Constants.TICK_DURATION;
                _clientTick++;
            }
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

            if (_hasTarget && _lastThreatTime > 0f && Time.time - _lastThreatTime < _settings.TargetRefreshInterval * 0.5f)
            {
                return;
            }

            if (_hasTarget && Time.time - _lastTargetTime < _settings.TargetRefreshInterval)
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

        private void SetTarget(Vector3 target)
        {
            _currentTarget = target;
            _hasTarget = true;
            _lastTargetTime = Time.time;
            BuildPath(target);
        }

        private bool TrySelectTarget(out Vector3 target, out BotIntent intent)
        {
            target = Vector3.zero;
            intent = BotIntent.None;

            switch (_settings.TargetMode)
            {
                case BotTargetMode.RunesOnly:
                    intent = BotIntent.Runes;
                    return TryGetClosestRune(_predictedPosition, out target);
                case BotTargetMode.ShrinesOnly:
                    intent = BotIntent.Shrine;
                    return TryGetClosestShrine(_predictedPosition, out target);
                case BotTargetMode.RunesThenShrines:
                    if (TryGetClosestRune(_predictedPosition, out target))
                    {
                        intent = BotIntent.Runes;
                        return true;
                    }
                    intent = BotIntent.Shrine;
                    return TryGetClosestShrine(_predictedPosition, out target);
                case BotTargetMode.RandomNodes:
                    intent = BotIntent.Wander;
                    target = GetFallbackTarget();
                    return true;
            }

            return false;
        }

        private bool TrySelectCombatTarget(out Vector3 target, out BotIntent intent)
        {
            target = Vector3.zero;
            intent = BotIntent.None;

            if (_settings.ChaseSmallerPlayers && TryFindChaseTarget(out target))
            {
                intent = BotIntent.Chase;
                return true;
            }

            if (_settings.DenyShrines && TryFindShrineTarget(out target, out bool bait))
            {
                intent = bait ? BotIntent.Bait : BotIntent.Shrine;
                return true;
            }

            return false;
        }

        private bool TryFindChaseTarget(out Vector3 target)
        {
            target = Vector3.zero;
            float radius = Mathf.Max(0.1f, _settings.ChaseRadius);
            float radiusSq = radius * radius;
            float sizeThreshold = _predictedRadius * Mathf.Clamp(_settings.ChaseSizeRatio, 0.1f, 1f);
            float bestDistSq = float.MaxValue;

            foreach (var kvp in _players)
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

                Vector3 predicted = state.Position + state.Velocity * _settings.ChaseLeadTime;
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

            if (!TryGetClosestShrine(_predictedPosition, out var shrinePos))
            {
                return false;
            }

            float dist = Vector3.Distance(_predictedPosition, shrinePos);
            if (dist > _settings.ShrineDenyRadius)
            {
                return false;
            }

            if (dist <= _settings.ShrineBaitRadius && HasShrineAdvantage(shrinePos))
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
            float checkRadius = _settings.ShrineBaitRadius * 1.5f;
            float checkRadiusSq = checkRadius * checkRadius;
            bool enemyNearby = false;

            foreach (var kvp in _players)
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

                Vector3 pos = state.Position;
                pos.y = shrinePos.y;
                float distSq = (pos - shrinePos).sqrMagnitude;
                if (distSq > checkRadiusSq)
                {
                    continue;
                }

                enemyNearby = true;
                if (state.Radius >= _predictedRadius * Mathf.Clamp(_settings.ChaseSizeRatio, 0.1f, 1f))
                {
                    return false;
                }
            }

            return enemyNearby;
        }

        private Vector3 GetShrineOrbitPoint(Vector3 shrinePos)
        {
            float angle = Time.time * _settings.ShrineOrbitSpeed + _orbitPhase;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _settings.ShrineOrbitRadius;
            return shrinePos + offset;
        }

        private Vector3 GetFallbackTarget()
        {
            if (_navGraph != null && _navGraph.Nodes.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, _navGraph.Nodes.Count);
                Vector2 node = _navGraph.Nodes[index].Position;
                return new Vector3(node.x, _predictedPosition.y, node.y);
            }

            Vector2 offset = UnityEngine.Random.insideUnitCircle * 10f;
            return new Vector3(_predictedPosition.x + offset.x, _predictedPosition.y, _predictedPosition.z + offset.y);
        }

        private void BuildPath(Vector3 target)
        {
            _waypoints.Clear();
            _waypointIndex = 0;
            _navPath.Clear();

            Vector2 start2d = new Vector2(_predictedPosition.x, _predictedPosition.z);
            Vector2 end2d = new Vector2(target.x, target.z);
            if (_navGraph != null && _navGraph.TryFindPath(start2d, end2d, _navPath))
            {
                for (int i = 0; i < _navPath.Count; i++)
                {
                    Vector2 node = _navPath[i];
                    AppendWaypoint(new Vector3(node.x, _predictedPosition.y, node.y), 0.5f);
                }
            }

            AppendWaypoint(target, 0.25f);
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

        private void FollowPath(float deltaTime)
        {
            if (!_hasTarget || _waypoints.Count == 0)
            {
                _horizontalInput = 0f;
                _verticalInput = 0f;
                return;
            }

            float threshold = _waypointIndex >= _waypoints.Count - 1 ? 1.5f : 2f;
            while (_waypointIndex < _waypoints.Count &&
                   Vector3.Distance(_predictedPosition, _waypoints[_waypointIndex]) <= threshold)
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
            Vector3 desired = target - _predictedPosition;
            desired.y = 0f;

            if (desired.sqrMagnitude < 0.0001f)
            {
                _horizontalInput = 0f;
                _verticalInput = 0f;
                return;
            }

            Vector3 desiredDir = desired.normalized;
            bool panic;
            Vector3 avoid = GetAvoidVector(_predictedPosition, out panic);
            if (avoid.sqrMagnitude > 0.0001f)
            {
                _lastThreatTime = Time.time;
                if (panic)
                {
                    desiredDir = avoid.normalized;
                }
                else
                {
                    desiredDir = (desiredDir + avoid * _settings.ThreatAvoidWeight).normalized;
                }
            }

            Vector3 safeDir = FindSafeDirection(_predictedPosition, desiredDir, deltaTime);
            if (safeDir.sqrMagnitude < 0.0001f)
            {
                safeDir = FindSafeDirection(_predictedPosition, Quaternion.Euler(0f, 45f, 0f) * desiredDir, deltaTime);
            }

            if (safeDir.sqrMagnitude < 0.0001f)
            {
                _horizontalInput = 0f;
                _verticalInput = 0f;
                return;
            }

            _currentDirection = Vector3.Slerp(_currentDirection, safeDir, deltaTime * _settings.DirectionSmoothing);
            _currentDirection.y = 0f;

            Vector2 input = new Vector2(_currentDirection.x, _currentDirection.z);
            if (input.magnitude > 1f)
            {
                input.Normalize();
            }

            _horizontalInput = input.x;
            _verticalInput = input.y;
        }

        private Vector3 FindSafeDirection(Vector3 position, Vector3 desiredDir, float deltaTime)
        {
            if (IsDirectionSafe(position, desiredDir, deltaTime))
            {
                return desiredDir;
            }

            int maxChecks = Mathf.Max(1, _settings.SteeringChecks);
            for (int i = 1; i <= maxChecks; i++)
            {
                float angle = _settings.SteeringAngleStep * i;
                Vector3 left = Quaternion.AngleAxis(angle, Vector3.up) * desiredDir;
                if (IsDirectionSafe(position, left, deltaTime))
                {
                    return left;
                }

                Vector3 right = Quaternion.AngleAxis(-angle, Vector3.up) * desiredDir;
                if (IsDirectionSafe(position, right, deltaTime))
                {
                    return right;
                }
            }

            return Vector3.zero;
        }

        private bool IsDirectionSafe(Vector3 position, Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float step = _predictedSpeed * deltaTime;
            Vector3 nextPosition = position + direction.normalized * step;
            return ArcaneCircuitMapLogic.IsInsideMap(nextPosition, _predictedRadius, _spawnZoneId, _spawnZoneActive);
        }

        private Vector3 GetAvoidVector(Vector3 position, out bool panic)
        {
            panic = false;
            float awareness = Mathf.Max(0.5f, _settings.ThreatAwarenessRadius);
            float danger = Mathf.Max(0.1f, _settings.ThreatDangerRadius);
            float awarenessSq = awareness * awareness;
            float dangerSq = danger * danger;
            Vector3 sum = Vector3.zero;

            foreach (var kvp in _players)
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

                if (_settings.AvoidLargerOnly && state.Radius <= _predictedRadius * 1.05f)
                {
                    continue;
                }

                Vector3 predicted = state.Position + state.Velocity * _settings.ThreatLeadTime;
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

        private void ApplyPrediction(float deltaTime)
        {
            if (Mathf.Abs(_horizontalInput) < 0.01f && Mathf.Abs(_verticalInput) < 0.01f)
            {
                return;
            }

            Vector3 movement = new Vector3(_horizontalInput, 0f, _verticalInput);
            movement *= _predictedSpeed * deltaTime;

            Vector3 nextPosition = _predictedPosition + movement;
            if (ArcaneCircuitMapLogic.IsInsideMap(nextPosition, _predictedRadius, _spawnZoneId, _spawnZoneActive))
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
        }

        private void UpdateProgress(Vector3 position)
        {
            if (Vector3.Distance(position, _lastProgressPosition) >= 0.25f)
            {
                _lastProgressPosition = position;
                _lastProgressTime = Time.time;
                return;
            }

            if (Time.time - _lastProgressTime >= 1.5f)
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

            if (!_settings.UseAbilityOnPanic)
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
                _abilityLockoutUntil = Time.time + Mathf.Max(0.05f, _settings.AbilityTriggerCooldown);
            }
        }

        private void SendInput()
        {
            var input = GameInput.FromAxes(_clientTick, _horizontalInput, _verticalInput, _abilityPressed);
            _ = Send(input);
            _abilityPressed = false;
        }

        private bool TryGetClosestRune(Vector3 fromPosition, out Vector3 runePosition)
        {
            runePosition = Vector3.zero;
            float bestDistSq = float.MaxValue;

            foreach (var rune in _runes.Values)
            {
                Vector3 pos = rune;
                pos.y = fromPosition.y;
                float distSq = (pos - fromPosition).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    runePosition = pos;
                }
            }

            return bestDistSq < float.MaxValue;
        }

        private bool TryGetClosestShrine(Vector3 fromPosition, out Vector3 shrinePosition)
        {
            shrinePosition = Vector3.zero;
            float bestDistSq = float.MaxValue;

            foreach (var shrine in _shrines.Values)
            {
                Vector3 pos = shrine.Position;
                pos.y = fromPosition.y;
                float distSq = (pos - fromPosition).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    shrinePosition = pos;
                }
            }

            return bestDistSq < float.MaxValue;
        }

        private async System.Threading.Tasks.Task Send(ClientMessage message)
        {
            if (_socket == null || !_connected)
            {
                return;
            }

            string json = JsonUtility.ToJson(message);
            await _socket.Send(json);
        }

        private static string ResolveEventType(MatchEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.@event))
            {
                return evt.@event;
            }

            if (!string.IsNullOrEmpty(evt.event_type))
            {
                return evt.event_type;
            }

            if (!string.IsNullOrEmpty(evt.type) && evt.type != "event")
            {
                return evt.type;
            }

            return evt.type;
        }

        private static string BytesToHex(byte[] bytes)
        {
            if (bytes == null)
            {
                return "";
            }

            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        private static float ComputePhase(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return UnityEngine.Random.value * Mathf.PI * 2f;
            }

            int hash = 17;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash = (hash * 31) + bytes[i];
            }

            return Mathf.Abs(hash % 360) * Mathf.Deg2Rad;
        }

        private void ReportError(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            _logSink?.Invoke(message);
            Debug.LogError(message);
        }

        private void ReportInfo(string message)
        {
            if (!_logEvents || string.IsNullOrEmpty(message))
            {
                return;
            }

            Debug.Log(message);
        }
    }
}

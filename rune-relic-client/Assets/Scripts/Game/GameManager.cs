using System;
using System.Collections.Generic;
using UnityEngine;
using RuneRelic.Network;
using RuneRelic.Network.Messages;
using RuneRelic.Utils;

namespace RuneRelic.Game
{
    /// <summary>
    /// Main game controller. Manages game state, player spawning, and match flow.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject runePrefab;
        [SerializeField] private GameObject shrinePrefab;
        [SerializeField] private Transform playersContainer;
        [SerializeField] private Transform runesContainer;
        [SerializeField] private Transform shrinesContainer;

        [Header("Arena")]
        [SerializeField] private GameObject arenaFloor;
        [SerializeField] private GameObject arenaBoundary;

        [Header("Local Player")]
        [SerializeField] private bool useBotForLocalPlayer = true;

        // State
        private MatchState _matchState;
        private Dictionary<string, PlayerVisual> _playerVisuals = new Dictionary<string, PlayerVisual>();
        private Dictionary<uint, GameObject> _runeObjects = new Dictionary<uint, GameObject>();
        private Dictionary<uint, GameObject> _shrineObjects = new Dictionary<uint, GameObject>();

        // Local player
        private byte[] _localPlayerId;
        private ILocalPlayerController _localPlayerController;
        private uint _clientTick;
        private float _tickAccumulator;

        // Events
        public event Action<uint> OnCountdown;
        public event Action OnMatchStarted;
        public event Action<MatchEndInfo> OnMatchEnded;
        public event Action<string, int, int> OnPlayerEvolved;  // playerId, oldForm, newForm
        public event Action<string> OnPlayerEliminated;

        public MatchState CurrentMatch => _matchState;
        public bool IsInMatch => _matchState != null && GameClient.Instance?.CurrentState == GameState.Playing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Subscribe to network events
            var client = GameClient.Instance;
            if (client != null)
            {
                client.OnMatchStart += HandleMatchStart;
                client.OnStateUpdate += HandleStateUpdate;
                client.OnMatchEvent += HandleMatchEvent;
                client.OnMatchEnd += HandleMatchEnd;
                client.OnInputAck += HandleInputAck;
            }
        }

        private void OnDestroy()
        {
            var client = GameClient.Instance;
            if (client != null)
            {
                client.OnMatchStart -= HandleMatchStart;
                client.OnStateUpdate -= HandleStateUpdate;
                client.OnMatchEvent -= HandleMatchEvent;
                client.OnMatchEnd -= HandleMatchEnd;
                client.OnInputAck -= HandleInputAck;
            }
        }

        private void Update()
        {
            if (!IsInMatch) return;

            // Advance client tick
            _tickAccumulator += Time.deltaTime;
            while (_tickAccumulator >= Constants.TICK_DURATION)
            {
                _tickAccumulator -= Constants.TICK_DURATION;
                _clientTick++;
            }

            // Update interpolation for all players
            UpdatePlayerInterpolation();
        }

        // =====================================================================
        // Match Lifecycle
        // =====================================================================

        private void HandleMatchStart(MatchStartInfo info)
        {
            Debug.Log($"[GameManager] Match starting! Players: {info.players.Count}");

            // Initialize match state
            _matchState = new MatchState(info);
            _clientTick = 0;
            _tickAccumulator = 0;

            if (_localPlayerId == null)
            {
                var client = GameClient.Instance;
                if (client != null && client.LocalPlayerId != null)
                {
                    _localPlayerId = client.LocalPlayerId;
                }
            }

            // Clear existing objects
            ClearGameObjects();

            // Spawn players
            foreach (var playerInfo in info.players)
            {
                SpawnPlayer(playerInfo);
            }

            // Setup arena
            SetupArena();
        }

        private void HandleStateUpdate(GameStateUpdate update)
        {
            if (_matchState == null) return;

            // Update match state with server data
            _matchState.ApplyUpdate(update);

            // Update player visuals
            foreach (var playerState in update.players)
            {
                string playerId = BytesToHex(playerState.player_id);
                if (_playerVisuals.TryGetValue(playerId, out var visual))
                {
                    visual.UpdateFromState(playerState);
                }

                if (_localPlayerController != null && _localPlayerId != null && playerId == BytesToHex(_localPlayerId))
                {
                    bool hasSpeedBuff = playerState.buffs != null && playerState.buffs.speed > 0;
                    bool hasShrineSpeed = playerState.buffs != null
                        && playerState.buffs.shrine_buffs != null
                        && playerState.buffs.shrine_buffs.Contains((int)ShrineType.Speed);

                    _localPlayerController.UpdateSpeed((Form)playerState.form, hasSpeedBuff, hasShrineSpeed);
                    _localPlayerController.UpdateRadius(FixedPoint.ToFloat(playerState.radius));
                    _localPlayerController.SetSpawnZone(playerState.spawn_zone_id, playerState.spawn_zone_active);
                    _localPlayerController.UpdateAbilityCooldown(FixedPoint.ToFloat(playerState.ability_cooldown));
                }
            }

            // Update runes
            if (update.runes != null)
            {
                UpdateRunes(update.runes);
            }

            // Update shrines
            if (update.shrines != null)
            {
                UpdateShrines(update.shrines);
            }
        }

        private void HandleMatchEvent(MatchEvent evt)
        {
            switch (evt.type)
            {
                case "countdown":
                    OnCountdown?.Invoke(evt.seconds);
                    break;

                case "match_started":
                    OnMatchStarted?.Invoke();
                    break;

                case "rune_spawned":
                    HandleRuneSpawned(evt);
                    break;

                case "rune_collected":
                    HandleRuneCollected(evt);
                    break;

                case "player_evolved":
                    HandlePlayerEvolved(evt);
                    break;

                case "player_eliminated":
                    HandlePlayerEliminated(evt);
                    break;

                case "ability_used":
                    HandleAbilityUsed(evt);
                    break;

                case "shrine_captured":
                    HandleShrineCaptured(evt);
                    break;
            }
        }

        private void HandleMatchEnd(MatchEndInfo info)
        {
            Debug.Log($"[GameManager] Match ended! Winner: {(info.winner_id != null ? BytesToHex(info.winner_id) : "None")}");
            OnMatchEnded?.Invoke(info);
        }

        private void HandleInputAck(InputAck ack)
        {
            // Could use this for client-side prediction reconciliation
            // For now, just track server tick
            if (_matchState != null)
            {
                _matchState.LastServerTick = ack.server_tick;
            }
        }

        // =====================================================================
        // Player Management
        // =====================================================================

        private void SpawnPlayer(InitialPlayerInfo info)
        {
            string playerId = BytesToHex(info.player_id);
            Vector3 position = FixedPoint.ToVector3(info.position);

            GameObject playerObj;
            if (playerPrefab != null)
            {
                playerObj = Instantiate(playerPrefab, position, Quaternion.identity, playersContainer);
            }
            else
            {
                // Create placeholder capsule
                playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerObj.transform.position = position;
                playerObj.transform.SetParent(playersContainer);
            }

            playerObj.name = $"Player_{playerId.Substring(0, 8)}";

            // Add or get PlayerVisual component
            var visual = playerObj.GetComponent<PlayerVisual>();
            if (visual == null)
            {
                visual = playerObj.AddComponent<PlayerVisual>();
            }
            visual.Initialize(info.player_id, info.color_index);

            _playerVisuals[playerId] = visual;

            // Check if this is local player
            if (_localPlayerId != null && BytesToHex(_localPlayerId) == playerId)
            {
                if (useBotForLocalPlayer)
                {
                    var botController = playerObj.AddComponent<BotController>();
                    botController.Initialize(info.player_id);
                    _localPlayerController = botController;
                }
                else
                {
                    var playerController = playerObj.AddComponent<PlayerController>();
                    playerController.Initialize(info.player_id);
                    _localPlayerController = playerController;
                }

                // Add camera follow
                var cam = Camera.main;
                if (cam != null)
                {
                    var follow = cam.gameObject.AddComponent<CameraFollow>();
                    follow.target = playerObj.transform;
                }
            }
        }

        private void UpdatePlayerInterpolation()
        {
            float t = _matchState?.GetInterpolationT() ?? 0f;

            foreach (var visual in _playerVisuals.Values)
            {
                visual.Interpolate(t);
            }
        }

        // =====================================================================
        // Entity Management
        // =====================================================================

        private void UpdateRunes(List<RuneUpdate> runes)
        {
            foreach (var rune in runes)
            {
                if (rune.collected)
                {
                    // Remove collected rune
                    if (_runeObjects.TryGetValue(rune.id, out var obj))
                    {
                        Destroy(obj);
                        _runeObjects.Remove(rune.id);
                    }
                }
                else if (!_runeObjects.ContainsKey(rune.id))
                {
                    // Spawn new rune
                    SpawnRune(rune);
                }
            }
        }

        private void SpawnRune(RuneUpdate rune)
        {
            Vector3 position = FixedPoint.ToVector3(rune.position);

            GameObject runeObj;
            if (runePrefab != null)
            {
                runeObj = Instantiate(runePrefab, position, Quaternion.identity, runesContainer);
            }
            else
            {
                // Placeholder sphere
                runeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                runeObj.transform.position = position + Vector3.up * 0.5f;
                runeObj.transform.localScale = Vector3.one * 0.6f;
                runeObj.transform.SetParent(runesContainer);

                // Color by type
                var renderer = runeObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = GetRuneColor((RuneType)rune.rune_type);
                }
            }

            runeObj.name = $"Rune_{rune.id}_{Constants.RUNE_NAMES[rune.rune_type]}";
            _runeObjects[rune.id] = runeObj;
        }

        private void UpdateShrines(List<ShrineUpdate> shrines)
        {
            foreach (var shrine in shrines)
            {
                if (!_shrineObjects.ContainsKey(shrine.id))
                {
                    SpawnShrine(shrine);
                }
                else
                {
                    // Update existing shrine state
                    // TODO: Update active/controller visuals
                }
            }
        }

        private void SpawnShrine(ShrineUpdate shrine)
        {
            Vector3 position = FixedPoint.ToVector3(shrine.position);

            GameObject shrineObj;
            if (shrinePrefab != null)
            {
                shrineObj = Instantiate(shrinePrefab, position, Quaternion.identity, shrinesContainer);
            }
            else
            {
                // Placeholder cylinder
                shrineObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shrineObj.transform.position = position;
                shrineObj.transform.localScale = new Vector3(2f, 0.5f, 2f);
                shrineObj.transform.SetParent(shrinesContainer);

                var renderer = shrineObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = GetShrineColor((ShrineType)shrine.shrine_type);
                }
            }

            shrineObj.name = $"Shrine_{shrine.id}_{Constants.SHRINE_NAMES[shrine.shrine_type]}";
            _shrineObjects[shrine.id] = shrineObj;
        }

        // =====================================================================
        // Event Handlers
        // =====================================================================

        private void HandleRuneCollected(MatchEvent evt)
        {
            if (_runeObjects.TryGetValue(evt.rune_id, out var obj))
            {
                // Play collection effect, then destroy
                Destroy(obj, 0.1f);
                _runeObjects.Remove(evt.rune_id);
            }
        }

        private void HandleRuneSpawned(MatchEvent evt)
        {
            if (evt.position == null || evt.position.Length < 2)
            {
                return;
            }

            if (_runeObjects.ContainsKey(evt.rune_id))
            {
                return;
            }

            var rune = new RuneUpdate
            {
                id = evt.rune_id,
                rune_type = evt.rune_type,
                position = evt.position,
                collected = false
            };
            SpawnRune(rune);
        }

        private void HandlePlayerEvolved(MatchEvent evt)
        {
            string playerId = BytesToHex(evt.player_id);
            if (_playerVisuals.TryGetValue(playerId, out var visual))
            {
                visual.SetForm((Form)evt.new_form);
            }
            OnPlayerEvolved?.Invoke(playerId, evt.old_form, evt.new_form);
        }

        private void HandlePlayerEliminated(MatchEvent evt)
        {
            string victimId = BytesToHex(evt.victim_id);
            if (_playerVisuals.TryGetValue(victimId, out var visual))
            {
                visual.SetEliminated();
            }
            OnPlayerEliminated?.Invoke(victimId);
        }

        private void HandleAbilityUsed(MatchEvent evt)
        {
            string playerId = BytesToHex(evt.player_id);
            if (_playerVisuals.TryGetValue(playerId, out var visual))
            {
                visual.PlayAbilityEffect(evt.ability_type);
            }
        }

        private void HandleShrineCaptured(MatchEvent evt)
        {
            // Play capture effect on shrine
            if (_shrineObjects.TryGetValue(evt.shrine_id, out var obj))
            {
                // TODO: Visual feedback
            }
        }

        // =====================================================================
        // Setup & Cleanup
        // =====================================================================

        private void SetupArena()
        {
            if (arenaFloor != null)
            {
                arenaFloor.transform.localScale = new Vector3(
                    Constants.ARENA_WIDTH / 10f,
                    1f,
                    Constants.ARENA_HEIGHT / 10f
                );
            }
        }

        private void ClearGameObjects()
        {
            foreach (var visual in _playerVisuals.Values)
            {
                if (visual != null)
                    Destroy(visual.gameObject);
            }
            _playerVisuals.Clear();

            foreach (var obj in _runeObjects.Values)
            {
                if (obj != null)
                    Destroy(obj);
            }
            _runeObjects.Clear();

            foreach (var obj in _shrineObjects.Values)
            {
                if (obj != null)
                    Destroy(obj);
            }
            _shrineObjects.Clear();
        }

        // =====================================================================
        // Public API
        // =====================================================================

        public void SetLocalPlayerId(byte[] playerId)
        {
            _localPlayerId = playerId;
        }

        public uint GetClientTick() => _clientTick;

        public bool TryGetClosestRune(Vector3 fromPosition, out Vector3 runePosition)
        {
            runePosition = Vector3.zero;
            float bestDistSq = float.MaxValue;

            foreach (var obj in _runeObjects.Values)
            {
                if (obj == null)
                {
                    continue;
                }

                Vector3 pos = obj.transform.position;
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

        public bool TryGetClosestShrine(Vector3 fromPosition, out Vector3 shrinePosition)
        {
            shrinePosition = Vector3.zero;
            float bestDistSq = float.MaxValue;

            foreach (var obj in _shrineObjects.Values)
            {
                if (obj == null)
                {
                    continue;
                }

                Vector3 pos = obj.transform.position;
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

        // =====================================================================
        // Utilities
        // =====================================================================

        private static string BytesToHex(byte[] bytes)
        {
            if (bytes == null) return "";
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        private static Color GetRuneColor(RuneType type)
        {
            return type switch
            {
                RuneType.Wisdom => Color.blue,
                RuneType.Power => Color.red,
                RuneType.Speed => Color.yellow,
                RuneType.Shield => Color.green,
                RuneType.Arcane => new Color(0.5f, 0f, 0.5f), // Purple
                RuneType.Chaos => Color.white, // Rainbow effect in shader
                _ => Color.gray
            };
        }

        private static Color GetShrineColor(ShrineType type)
        {
            return type switch
            {
                ShrineType.Wisdom => new Color(0.3f, 0.3f, 0.8f),
                ShrineType.Power => new Color(0.8f, 0.3f, 0.3f),
                ShrineType.Speed => new Color(0.8f, 0.8f, 0.3f),
                ShrineType.Shield => new Color(0.3f, 0.8f, 0.3f),
                _ => Color.gray
            };
        }
    }

    /// <summary>
    /// Simple camera follow script.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0, 15, -10);
        public float smoothSpeed = 5f;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.LookAt(target);
        }
    }
}

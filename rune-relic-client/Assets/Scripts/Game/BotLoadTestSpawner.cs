using System;
using System.Collections.Generic;
using UnityEngine;
using RuneRelic.Utils;

namespace RuneRelic.Game
{
    /// <summary>
    /// Spawns headless bot clients for server load testing.
    /// </summary>
    public class BotLoadTestSpawner : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private string serverUrl = Constants.DEFAULT_SERVER_URL;
        [SerializeField] private MatchMode matchMode = MatchMode.Casual;
        [SerializeField] private int botCount = 8;
        [SerializeField] private float spawnInterval = 0.25f;
        [SerializeField] private bool spawnOnStart = false;
        [SerializeField] private float readyDelay = 0.1f;
        [SerializeField] private bool autoRequeue = true;
        [SerializeField] private bool logBotEvents = false;

        [Header("Behavior")]
        [SerializeField] private BotTargetMode targetMode = BotTargetMode.RunesThenShrines;
        [SerializeField] private float targetRefreshInterval = 0.75f;
        [SerializeField] private bool chaseSmallerPlayers = true;
        [SerializeField] private float chaseRadius = 14f;
        [SerializeField] private float chaseSizeRatio = 0.9f;
        [SerializeField] private float chaseLeadTime = 0.25f;
        [SerializeField] private bool denyShrines = true;
        [SerializeField] private float shrineDenyRadius = 18f;
        [SerializeField] private float shrineBaitRadius = 6f;
        [SerializeField] private float shrineOrbitRadius = 4f;
        [SerializeField] private float shrineOrbitSpeed = 1.25f;
        [SerializeField] private float threatAwarenessRadius = 12f;
        [SerializeField] private float threatDangerRadius = 6f;
        [SerializeField] private float threatLeadTime = 0.35f;
        [SerializeField] private float threatAvoidWeight = 1.4f;
        [SerializeField] private bool avoidLargerOnly = true;
        [SerializeField] private bool useAbilityOnPanic = true;
        [SerializeField] private float abilityTriggerCooldown = 0.25f;

        [Header("Steering")]
        [SerializeField] private float steeringAngleStep = 12f;
        [SerializeField] private int steeringChecks = 6;
        [SerializeField] private float directionSmoothing = 8f;

        [Header("Input")]
        [SerializeField] private float inputSendRate = 60f;

        private readonly List<BotLoadTestClient> _bots = new List<BotLoadTestClient>();
        private readonly Queue<string> _pendingLogs = new Queue<string>();
        private readonly object _logLock = new object();
        private ArcaneCircuitNavGraph _navGraph;
        private float _spawnTimer;
        private int _spawned;
        private bool _spawning;

        public event Action<string> OnBotLog;
        public int ActiveBotCount => _bots.Count;
        public int TargetBotCount => botCount;
        public bool IsSpawning => _spawning;

        private void Start()
        {
            if (spawnOnStart)
            {
                StartSpawning();
            }
        }

        private void Update()
        {
            if (_spawning)
            {
                SpawnBotsOverTime();
            }

            for (int i = 0; i < _bots.Count; i++)
            {
                _bots[i].Tick(Time.deltaTime);
            }

            FlushLogs();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _bots.Count; i++)
            {
                _bots[i].Dispose();
            }
            _bots.Clear();
        }

        public void StartSpawning()
        {
            StopAllBots();

            _navGraph = ArcaneCircuitNavGraph.BuildDefault();
            _spawned = 0;
            _spawnTimer = 0f;
            _spawning = true;
        }

        public void StopAllBots()
        {
            _spawning = false;
            _spawned = 0;
            _spawnTimer = 0f;

            for (int i = 0; i < _bots.Count; i++)
            {
                _bots[i].Dispose();
            }
            _bots.Clear();
        }

        private void SpawnBotsOverTime()
        {
            _spawnTimer += Time.deltaTime;

            while (_spawned < botCount && _spawnTimer >= spawnInterval)
            {
                _spawnTimer -= Mathf.Max(0.01f, spawnInterval);
                SpawnBot(_spawned);
                _spawned++;
            }

            if (_spawned >= botCount)
            {
                _spawning = false;
            }
        }

        private void SpawnBot(int index)
        {
            var settings = new BotBehaviorSettings
            {
                TargetMode = targetMode,
                TargetRefreshInterval = targetRefreshInterval,
                ChaseSmallerPlayers = chaseSmallerPlayers,
                ChaseRadius = chaseRadius,
                ChaseSizeRatio = chaseSizeRatio,
                ChaseLeadTime = chaseLeadTime,
                DenyShrines = denyShrines,
                ShrineDenyRadius = shrineDenyRadius,
                ShrineBaitRadius = shrineBaitRadius,
                ShrineOrbitRadius = shrineOrbitRadius,
                ShrineOrbitSpeed = shrineOrbitSpeed,
                ThreatAwarenessRadius = threatAwarenessRadius,
                ThreatDangerRadius = threatDangerRadius,
                ThreatLeadTime = threatLeadTime,
                ThreatAvoidWeight = threatAvoidWeight,
                AvoidLargerOnly = avoidLargerOnly,
                UseAbilityOnPanic = useAbilityOnPanic,
                AbilityTriggerCooldown = abilityTriggerCooldown,
                SteeringAngleStep = steeringAngleStep,
                SteeringChecks = steeringChecks,
                DirectionSmoothing = directionSmoothing,
                InputSendRate = inputSendRate
            };

            var bot = new BotLoadTestClient(
                serverUrl,
                matchMode,
                settings,
                _navGraph,
                readyDelay,
                autoRequeue,
                logBotEvents,
                EnqueueLog,
                $"Bot-{index + 1}");

            _bots.Add(bot);
            bot.Connect();
        }

        private void EnqueueLog(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            lock (_logLock)
            {
                _pendingLogs.Enqueue(message);
            }
        }

        private void FlushLogs()
        {
            if (_pendingLogs.Count == 0)
            {
                return;
            }

            List<string> logs = null;
            lock (_logLock)
            {
                if (_pendingLogs.Count == 0)
                {
                    return;
                }

                logs = new List<string>(_pendingLogs.Count);
                while (_pendingLogs.Count > 0)
                {
                    logs.Add(_pendingLogs.Dequeue());
                }
            }

            if (logs == null || logs.Count == 0)
            {
                return;
            }

            for (int i = 0; i < logs.Count; i++)
            {
                OnBotLog?.Invoke(logs[i]);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using RuneRelic.Entities;
using RuneRelic.Network.Messages;
using RuneRelic.Utils;

namespace RuneRelic.Game
{
    /// <summary>
    /// Manages game entities (runes, shrines) with object pooling for performance.
    /// </summary>
    public class EntityManager : MonoBehaviour
    {
        public static EntityManager Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject[] runePrefabs;     // 6 types
        [SerializeField] private GameObject[] shrinePrefabs;   // 4 types

        [Header("Containers")]
        [SerializeField] private Transform runesContainer;
        [SerializeField] private Transform shrinesContainer;

        [Header("Pooling")]
        [SerializeField] private int initialPoolSize = 20;
        [SerializeField] private int maxPoolSize = 100;

        // Active entities
        private readonly Dictionary<uint, Rune> _activeRunes = new Dictionary<uint, Rune>();
        private readonly Dictionary<uint, Shrine> _activeShrines = new Dictionary<uint, Shrine>();

        // Object pools
        private readonly Dictionary<RuneType, Queue<Rune>> _runePools = new Dictionary<RuneType, Queue<Rune>>();
        private readonly Dictionary<ShrineType, Queue<Shrine>> _shrinePools = new Dictionary<ShrineType, Queue<Shrine>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Create containers if not assigned
            if (runesContainer == null)
            {
                runesContainer = new GameObject("Runes").transform;
                runesContainer.SetParent(transform);
            }

            if (shrinesContainer == null)
            {
                shrinesContainer = new GameObject("Shrines").transform;
                shrinesContainer.SetParent(transform);
            }

            // Initialize pools
            InitializePools();
        }

        private void InitializePools()
        {
            // Initialize rune pools for each type
            for (int i = 0; i < 6; i++)
            {
                RuneType type = (RuneType)i;
                _runePools[type] = new Queue<Rune>();

                // Pre-warm pool
                for (int j = 0; j < initialPoolSize / 6; j++)
                {
                    var rune = CreateRune(type);
                    rune.gameObject.SetActive(false);
                    _runePools[type].Enqueue(rune);
                }
            }

            // Initialize shrine pools for each type
            for (int i = 0; i < 4; i++)
            {
                ShrineType type = (ShrineType)i;
                _shrinePools[type] = new Queue<Shrine>();
            }
        }

        // =====================================================================
        // Rune Management
        // =====================================================================

        /// <summary>
        /// Spawn or update runes from server state.
        /// </summary>
        public void UpdateRunes(List<RuneUpdate> runeUpdates)
        {
            HashSet<uint> activeIds = new HashSet<uint>();

            foreach (var update in runeUpdates)
            {
                activeIds.Add(update.id);

                if (update.collected)
                {
                    // Collect and return to pool
                    if (_activeRunes.TryGetValue(update.id, out var rune))
                    {
                        CollectRune(update.id);
                    }
                }
                else if (!_activeRunes.ContainsKey(update.id))
                {
                    // Spawn new rune
                    SpawnRune(update);
                }
            }

            // Remove runes no longer in update
            var toRemove = new List<uint>();
            foreach (var id in _activeRunes.Keys)
            {
                if (!activeIds.Contains(id))
                    toRemove.Add(id);
            }

            foreach (var id in toRemove)
            {
                ReturnRuneToPool(id);
            }
        }

        /// <summary>
        /// Spawn a rune from the pool.
        /// </summary>
        public Rune SpawnRune(RuneUpdate update)
        {
            RuneType runeType = (RuneType)update.rune_type;
            Vector3 position = FixedPoint.ToVector3(update.position);

            Rune rune = GetRuneFromPool(runeType);
            rune.Initialize(update.id, runeType, position);
            rune.gameObject.SetActive(true);

            _activeRunes[update.id] = rune;
            return rune;
        }

        /// <summary>
        /// Collect a rune (play effect and return to pool).
        /// </summary>
        public void CollectRune(uint runeId)
        {
            if (!_activeRunes.TryGetValue(runeId, out var rune))
                return;

            rune.Collect();
            _activeRunes.Remove(runeId);

            // Return to pool after effect (Collect handles destruction)
        }

        private Rune GetRuneFromPool(RuneType type)
        {
            if (_runePools[type].Count > 0)
            {
                return _runePools[type].Dequeue();
            }

            return CreateRune(type);
        }

        private void ReturnRuneToPool(uint runeId)
        {
            if (!_activeRunes.TryGetValue(runeId, out var rune))
                return;

            _activeRunes.Remove(runeId);

            RuneType type = rune.Type;
            if (_runePools[type].Count < maxPoolSize / 6)
            {
                rune.gameObject.SetActive(false);
                _runePools[type].Enqueue(rune);
            }
            else
            {
                Destroy(rune.gameObject);
            }
        }

        private Rune CreateRune(RuneType type)
        {
            GameObject runeObj;

            if (runePrefabs != null && runePrefabs.Length > (int)type && runePrefabs[(int)type] != null)
            {
                runeObj = Instantiate(runePrefabs[(int)type], runesContainer);
            }
            else
            {
                runeObj = Rune.CreatePlaceholder(type);
                runeObj.transform.SetParent(runesContainer);
            }

            var rune = runeObj.GetComponent<Rune>();
            if (rune == null)
            {
                rune = runeObj.AddComponent<Rune>();
            }

            return rune;
        }

        // =====================================================================
        // Shrine Management
        // =====================================================================

        /// <summary>
        /// Spawn or update shrines from server state.
        /// </summary>
        public void UpdateShrines(List<ShrineUpdate> shrineUpdates)
        {
            foreach (var update in shrineUpdates)
            {
                if (_activeShrines.TryGetValue(update.id, out var shrine))
                {
                    // Update existing shrine
                    shrine.UpdateState(update.active, update.controller_id, update.channel_progress);
                }
                else
                {
                    // Spawn new shrine
                    SpawnShrine(update);
                }
            }
        }

        /// <summary>
        /// Spawn a shrine.
        /// </summary>
        public Shrine SpawnShrine(ShrineUpdate update)
        {
            ShrineType shrineType = (ShrineType)update.shrine_type;
            Vector3 position = FixedPoint.ToVector3(update.position);

            Shrine shrine = GetShrineFromPool(shrineType);
            shrine.Initialize(update.id, shrineType, position);
            shrine.gameObject.SetActive(true);
            shrine.UpdateState(update.active, update.controller_id, update.channel_progress);

            _activeShrines[update.id] = shrine;
            return shrine;
        }

        /// <summary>
        /// Play capture effect on a shrine.
        /// </summary>
        public void PlayShrineCaptureEffect(uint shrineId)
        {
            if (_activeShrines.TryGetValue(shrineId, out var shrine))
            {
                shrine.PlayCaptureEffect();
            }
        }

        private Shrine GetShrineFromPool(ShrineType type)
        {
            if (_shrinePools[type].Count > 0)
            {
                return _shrinePools[type].Dequeue();
            }

            return CreateShrine(type);
        }

        private Shrine CreateShrine(ShrineType type)
        {
            GameObject shrineObj;

            if (shrinePrefabs != null && shrinePrefabs.Length > (int)type && shrinePrefabs[(int)type] != null)
            {
                shrineObj = Instantiate(shrinePrefabs[(int)type], shrinesContainer);
            }
            else
            {
                shrineObj = Shrine.CreatePlaceholder(type);
                shrineObj.transform.SetParent(shrinesContainer);
            }

            var shrine = shrineObj.GetComponent<Shrine>();
            if (shrine == null)
            {
                shrine = shrineObj.AddComponent<Shrine>();
            }

            return shrine;
        }

        // =====================================================================
        // Cleanup
        // =====================================================================

        /// <summary>
        /// Clear all entities (on match end).
        /// </summary>
        public void ClearAll()
        {
            // Destroy active runes
            foreach (var rune in _activeRunes.Values)
            {
                if (rune != null)
                    Destroy(rune.gameObject);
            }
            _activeRunes.Clear();

            // Destroy active shrines
            foreach (var shrine in _activeShrines.Values)
            {
                if (shrine != null)
                    Destroy(shrine.gameObject);
            }
            _activeShrines.Clear();

            // Clear pools
            foreach (var pool in _runePools.Values)
            {
                while (pool.Count > 0)
                {
                    var rune = pool.Dequeue();
                    if (rune != null)
                        Destroy(rune.gameObject);
                }
            }

            foreach (var pool in _shrinePools.Values)
            {
                while (pool.Count > 0)
                {
                    var shrine = pool.Dequeue();
                    if (shrine != null)
                        Destroy(shrine.gameObject);
                }
            }
        }

        /// <summary>
        /// Get rune by ID.
        /// </summary>
        public Rune GetRune(uint id)
        {
            _activeRunes.TryGetValue(id, out var rune);
            return rune;
        }

        /// <summary>
        /// Get shrine by ID.
        /// </summary>
        public Shrine GetShrine(uint id)
        {
            _activeShrines.TryGetValue(id, out var shrine);
            return shrine;
        }

        /// <summary>
        /// Get all active rune count.
        /// </summary>
        public int ActiveRuneCount => _activeRunes.Count;

        /// <summary>
        /// Get all active shrine count.
        /// </summary>
        public int ActiveShrineCount => _activeShrines.Count;
    }
}

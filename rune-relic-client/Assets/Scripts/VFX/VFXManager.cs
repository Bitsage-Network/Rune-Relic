using System.Collections.Generic;
using UnityEngine;
using RuneRelic.Utils;

namespace RuneRelic.VFX
{
    /// <summary>
    /// Manages visual effects with object pooling.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Effect Prefabs")]
        [SerializeField] private GameObject runeCollectVFX;
        [SerializeField] private GameObject evolutionVFX;
        [SerializeField] private GameObject eliminationVFX;
        [SerializeField] private GameObject abilityDashVFX;
        [SerializeField] private GameObject abilityPhaseVFX;
        [SerializeField] private GameObject abilityRepelVFX;
        [SerializeField] private GameObject abilityGravityVFX;
        [SerializeField] private GameObject abilityConsumeVFX;
        [SerializeField] private GameObject shrineChannelVFX;
        [SerializeField] private GameObject shrineCaptureVFX;
        [SerializeField] private GameObject hitVFX;
        [SerializeField] private GameObject spawnVFX;

        [Header("Pool Settings")]
        [SerializeField] private int poolSizePerEffect = 5;
        [SerializeField] private Transform poolContainer;

        // Pools
        private readonly Dictionary<VFXType, Queue<ParticleSystem>> _pools =
            new Dictionary<VFXType, Queue<ParticleSystem>>();

        private readonly Dictionary<VFXType, GameObject> _prefabs =
            new Dictionary<VFXType, GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (poolContainer == null)
            {
                poolContainer = new GameObject("VFXPool").transform;
                poolContainer.SetParent(transform);
            }

            InitializePrefabs();
            InitializePools();
        }

        private void InitializePrefabs()
        {
            _prefabs[VFXType.RuneCollect] = runeCollectVFX;
            _prefabs[VFXType.Evolution] = evolutionVFX;
            _prefabs[VFXType.Elimination] = eliminationVFX;
            _prefabs[VFXType.AbilityDash] = abilityDashVFX;
            _prefabs[VFXType.AbilityPhase] = abilityPhaseVFX;
            _prefabs[VFXType.AbilityRepel] = abilityRepelVFX;
            _prefabs[VFXType.AbilityGravity] = abilityGravityVFX;
            _prefabs[VFXType.AbilityConsume] = abilityConsumeVFX;
            _prefabs[VFXType.ShrineChannel] = shrineChannelVFX;
            _prefabs[VFXType.ShrineCapture] = shrineCaptureVFX;
            _prefabs[VFXType.Hit] = hitVFX;
            _prefabs[VFXType.Spawn] = spawnVFX;
        }

        private void InitializePools()
        {
            foreach (VFXType type in System.Enum.GetValues(typeof(VFXType)))
            {
                _pools[type] = new Queue<ParticleSystem>();

                if (_prefabs.TryGetValue(type, out var prefab) && prefab != null)
                {
                    for (int i = 0; i < poolSizePerEffect; i++)
                    {
                        var instance = CreateInstance(type, prefab);
                        if (instance != null)
                        {
                            _pools[type].Enqueue(instance);
                        }
                    }
                }
            }
        }

        private ParticleSystem CreateInstance(VFXType type, GameObject prefab)
        {
            var obj = Instantiate(prefab, poolContainer);
            obj.name = $"VFX_{type}";
            obj.SetActive(false);

            var ps = obj.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                ps = obj.AddComponent<ParticleSystem>();
            }

            return ps;
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Play a VFX at a position.
        /// </summary>
        public void Play(VFXType type, Vector3 position, Color? color = null, float scale = 1f)
        {
            var ps = GetFromPool(type);
            if (ps == null)
            {
                // Create fallback if no prefab
                ps = CreateFallbackVFX(type);
            }

            if (ps == null) return;

            ps.transform.position = position;
            ps.transform.localScale = Vector3.one * scale;

            if (color.HasValue)
            {
                var main = ps.main;
                main.startColor = color.Value;
            }

            ps.gameObject.SetActive(true);
            ps.Play();

            // Return to pool after duration
            float duration = ps.main.duration + ps.main.startLifetime.constantMax;
            StartCoroutine(ReturnToPoolAfter(type, ps, duration));
        }

        /// <summary>
        /// Play a VFX attached to a transform.
        /// </summary>
        public ParticleSystem PlayAttached(VFXType type, Transform parent, Color? color = null)
        {
            var ps = GetFromPool(type);
            if (ps == null) return null;

            ps.transform.SetParent(parent);
            ps.transform.localPosition = Vector3.zero;
            ps.transform.localRotation = Quaternion.identity;

            if (color.HasValue)
            {
                var main = ps.main;
                main.startColor = color.Value;
            }

            ps.gameObject.SetActive(true);
            ps.Play();

            return ps;
        }

        /// <summary>
        /// Stop and return an attached VFX.
        /// </summary>
        public void StopAttached(VFXType type, ParticleSystem ps)
        {
            if (ps == null) return;

            ps.Stop();
            ps.transform.SetParent(poolContainer);
            StartCoroutine(ReturnToPoolAfter(type, ps, 1f));
        }

        /// <summary>
        /// Play rune collection effect.
        /// </summary>
        public void PlayRuneCollect(Vector3 position, RuneType runeType)
        {
            Color color = GetRuneColor(runeType);
            Play(VFXType.RuneCollect, position, color, 1f);
        }

        /// <summary>
        /// Play evolution effect.
        /// </summary>
        public void PlayEvolution(Vector3 position, int newForm)
        {
            Color color = GetFormColor(newForm);
            Play(VFXType.Evolution, position, color, 1f + newForm * 0.3f);
        }

        /// <summary>
        /// Play elimination effect.
        /// </summary>
        public void PlayElimination(Vector3 position, int form)
        {
            float scale = 1f + form * 0.3f;
            Play(VFXType.Elimination, position, null, scale);
        }

        /// <summary>
        /// Play ability effect.
        /// </summary>
        public void PlayAbility(int abilityType, Vector3 position, Color playerColor)
        {
            VFXType vfxType = abilityType switch
            {
                0 => VFXType.AbilityDash,
                1 => VFXType.AbilityPhase,
                2 => VFXType.AbilityRepel,
                3 => VFXType.AbilityGravity,
                4 => VFXType.AbilityConsume,
                _ => VFXType.Hit
            };

            Play(vfxType, position, playerColor, 1f);
        }

        // =====================================================================
        // Pool Management
        // =====================================================================

        private ParticleSystem GetFromPool(VFXType type)
        {
            if (!_pools.TryGetValue(type, out var pool) || pool.Count == 0)
            {
                // Try to create new instance
                if (_prefabs.TryGetValue(type, out var prefab) && prefab != null)
                {
                    return CreateInstance(type, prefab);
                }
                return null;
            }

            return pool.Dequeue();
        }

        private System.Collections.IEnumerator ReturnToPoolAfter(VFXType type, ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (ps != null)
            {
                ps.Stop();
                ps.Clear();
                ps.gameObject.SetActive(false);
                ps.transform.SetParent(poolContainer);

                if (_pools.TryGetValue(type, out var pool))
                {
                    pool.Enqueue(ps);
                }
            }
        }

        private ParticleSystem CreateFallbackVFX(VFXType type)
        {
            GameObject obj = new GameObject($"VFX_{type}_Fallback");
            obj.transform.SetParent(poolContainer);

            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.5f;
            main.startSpeed = 3f;
            main.startSize = 0.2f;
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }
            );
            colorOverLifetime.color = gradient;

            return ps;
        }

        // =====================================================================
        // Color Helpers
        // =====================================================================

        private Color GetRuneColor(RuneType type)
        {
            return type switch
            {
                RuneType.Wisdom => new Color(0.3f, 0.5f, 1f),
                RuneType.Power => new Color(1f, 0.3f, 0.3f),
                RuneType.Speed => new Color(1f, 1f, 0.3f),
                RuneType.Shield => new Color(0.3f, 1f, 0.5f),
                RuneType.Arcane => new Color(0.7f, 0.3f, 1f),
                RuneType.Chaos => Color.white,
                _ => Color.white
            };
        }

        private Color GetFormColor(int form)
        {
            return form switch
            {
                0 => new Color(0.9f, 0.95f, 1f),
                1 => new Color(0.6f, 0.8f, 1f),
                2 => new Color(0.4f, 0.9f, 0.6f),
                3 => new Color(0.7f, 0.4f, 1f),
                4 => new Color(1f, 0.8f, 0.3f),
                _ => Color.white
            };
        }
    }

    public enum VFXType
    {
        RuneCollect,
        Evolution,
        Elimination,
        AbilityDash,
        AbilityPhase,
        AbilityRepel,
        AbilityGravity,
        AbilityConsume,
        ShrineChannel,
        ShrineCapture,
        Hit,
        Spawn
    }
}

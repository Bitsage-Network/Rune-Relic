using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RuneRelic.Game;
using RuneRelic.UI.Core;
using RuneRelic.Utils;

namespace RuneRelic.UI.Components
{
    /// <summary>
    /// In-game minimap showing player positions, runes, and shrines.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RectTransform mapContainer;
        [SerializeField] private RawImage mapBackground;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image shrinkZoneImage;

        [Header("Icon Prefabs")]
        [SerializeField] private GameObject playerIconPrefab;
        [SerializeField] private GameObject runeIconPrefab;
        [SerializeField] private GameObject shrineIconPrefab;

        [Header("Settings")]
        [SerializeField] private float mapSize = 200f;
        [SerializeField] private float iconSize = 12f;
        [SerializeField] private float localPlayerIconSize = 16f;
        [SerializeField] private bool rotateWithPlayer = false;
        [SerializeField] private float updateInterval = 0.1f;
        [SerializeField] private bool generateMapTexture = true;
        [SerializeField] private int mapTextureResolution = 512;
        [SerializeField] private Color mapFillColor = new Color(0.08f, 0.1f, 0.14f, 1f);
        [SerializeField] private Color mapOutsideColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private bool useMinimapMaterial = false;
        [SerializeField] private Material minimapMaterial;
        [SerializeField] private Color minimapOutlineColor = new Color(0.4f, 0.9f, 1f, 1f);
        [SerializeField] private float minimapOutlineWidth = 1f;
        [SerializeField] private Color minimapGlowColor = new Color(0.4f, 0.9f, 1f, 0.6f);
        [SerializeField] private float minimapGlowWidth = 4f;
        [SerializeField] private float minimapGlowIntensity = 1f;
        [SerializeField] private RawImage navOverlay;
        [SerializeField] private bool drawNavOverlay = true;
        [SerializeField] private Color navLineColor = new Color(0.4f, 0.8f, 1f, 0.65f);
        [SerializeField] private float navLineThickness = 2f;

        [Header("Colors")]
        [SerializeField] private Color localPlayerColor = Color.cyan;
        [SerializeField] private Color enemyPlayerColor = Color.red;
        [SerializeField] private Color runeColor = Color.yellow;
        [SerializeField] private Color shrineColor = Color.magenta;
        [SerializeField] private Color shrinkZoneColor = new Color(1f, 0f, 0f, 0.3f);

        // Icon pools
        private readonly Dictionary<string, RectTransform> _playerIcons = new Dictionary<string, RectTransform>();
        private readonly Dictionary<uint, RectTransform> _runeIcons = new Dictionary<uint, RectTransform>();
        private readonly Dictionary<uint, RectTransform> _shrineIcons = new Dictionary<uint, RectTransform>();

        private byte[] _localPlayerId;
        private string _localPlayerIdHex;
        private float _lastUpdateTime;
        private float _currentArenaWidth;
        private float _currentArenaHeight;
        private Material _minimapMaterialInstance;

        private void Start()
        {
            _currentArenaWidth = Constants.ARENA_WIDTH;
            _currentArenaHeight = Constants.ARENA_HEIGHT;

            if (shrinkZoneImage != null)
            {
                shrinkZoneImage.color = shrinkZoneColor;
                shrinkZoneImage.gameObject.SetActive(false);
            }

            ApplyTheme();

            if (generateMapTexture)
            {
                BuildMinimapTextures();
            }
            else
            {
                ApplyMinimapMaterial();
            }
        }

        private void OnDestroy()
        {
            if (_minimapMaterialInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_minimapMaterialInstance);
            }
            else
            {
                DestroyImmediate(_minimapMaterialInstance);
            }
        }

        private void Update()
        {
            if (Time.time - _lastUpdateTime < updateInterval)
                return;

            _lastUpdateTime = Time.time;
            UpdateMinimap();
        }

        public void SetLocalPlayerId(byte[] playerId)
        {
            _localPlayerId = playerId;
            _localPlayerIdHex = BytesToHex(playerId);
        }

        public void SetArenaSize(float width, float height)
        {
            _currentArenaWidth = width;
            _currentArenaHeight = height;
            UpdateShrinkZone();
        }

        private void UpdateMinimap()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null || !gameManager.IsInMatch) return;

            var matchState = gameManager.CurrentMatch;
            if (matchState == null) return;

            UpdatePlayers(matchState);
        }

        private void UpdatePlayers(MatchState matchState)
        {
            HashSet<string> activeIds = new HashSet<string>();

            foreach (var kvp in matchState.Players)
            {
                string playerId = kvp.Key;
                var playerState = kvp.Value;
                activeIds.Add(playerId);

                if (!playerState.Alive) continue;

                // Get or create icon
                if (!_playerIcons.TryGetValue(playerId, out var icon))
                {
                    icon = CreatePlayerIcon(playerId);
                    _playerIcons[playerId] = icon;
                }

                // Update position
                Vector2 mapPos = WorldToMapPosition(playerState.TargetPosition);
                icon.anchoredPosition = mapPos;

                // Update appearance
                bool isLocal = playerId == _localPlayerIdHex;
                UpdatePlayerIcon(icon, isLocal, playerState);
            }

            // Remove dead players
            var toRemove = new List<string>();
            foreach (var kvp in _playerIcons)
            {
                if (!activeIds.Contains(kvp.Key) ||
                    (matchState.Players.TryGetValue(kvp.Key, out var state) && !state.Alive))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                if (_playerIcons.TryGetValue(id, out var icon))
                {
                    Destroy(icon.gameObject);
                    _playerIcons.Remove(id);
                }
            }
        }

        public void UpdateRunes(Dictionary<uint, Vector3> runes)
        {
            HashSet<uint> activeIds = new HashSet<uint>();

            foreach (var kvp in runes)
            {
                uint runeId = kvp.Key;
                Vector3 worldPos = kvp.Value;
                activeIds.Add(runeId);

                if (!_runeIcons.TryGetValue(runeId, out var icon))
                {
                    icon = CreateRuneIcon();
                    _runeIcons[runeId] = icon;
                }

                icon.anchoredPosition = WorldToMapPosition(worldPos);
            }

            // Remove collected runes
            var toRemove = new List<uint>();
            foreach (var id in _runeIcons.Keys)
            {
                if (!activeIds.Contains(id))
                    toRemove.Add(id);
            }

            foreach (var id in toRemove)
            {
                if (_runeIcons.TryGetValue(id, out var icon))
                {
                    Destroy(icon.gameObject);
                    _runeIcons.Remove(id);
                }
            }
        }

        public void UpdateShrines(Dictionary<uint, Vector3> shrines)
        {
            foreach (var kvp in shrines)
            {
                uint shrineId = kvp.Key;
                Vector3 worldPos = kvp.Value;

                if (!_shrineIcons.TryGetValue(shrineId, out var icon))
                {
                    icon = CreateShrineIcon();
                    _shrineIcons[shrineId] = icon;
                }

                icon.anchoredPosition = WorldToMapPosition(worldPos);
            }
        }

        private void UpdateShrinkZone()
        {
            if (shrinkZoneImage == null) return;

            if (_currentArenaWidth < Constants.ARENA_WIDTH)
            {
                shrinkZoneImage.gameObject.SetActive(true);

                // Scale the danger zone indicator
                float scaleX = _currentArenaWidth / Constants.ARENA_WIDTH;
                float scaleY = _currentArenaHeight / Constants.ARENA_HEIGHT;
                shrinkZoneImage.rectTransform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
            else
            {
                shrinkZoneImage.gameObject.SetActive(false);
            }
        }

        private Vector2 WorldToMapPosition(Vector3 worldPos)
        {
            // Convert world position to map position
            float normalizedX = (worldPos.x / Constants.ARENA_WIDTH) + 0.5f;
            float normalizedZ = (worldPos.z / Constants.ARENA_HEIGHT) + 0.5f;

            float mapX = (normalizedX - 0.5f) * mapSize;
            float mapY = (normalizedZ - 0.5f) * mapSize;

            return new Vector2(mapX, mapY);
        }

        private RectTransform CreatePlayerIcon(string playerId)
        {
            GameObject iconObj;

            if (playerIconPrefab != null)
            {
                iconObj = Instantiate(playerIconPrefab, mapContainer);
            }
            else
            {
                iconObj = CreateDefaultIcon();
            }

            iconObj.name = $"PlayerIcon_{playerId.Substring(0, 8)}";

            var rect = iconObj.GetComponent<RectTransform>();
            bool isLocal = playerId == _localPlayerIdHex;
            float size = isLocal ? localPlayerIconSize : iconSize;
            rect.sizeDelta = new Vector2(size, size);

            var image = iconObj.GetComponent<Image>();
            if (image != null)
            {
                image.color = isLocal ? localPlayerColor : enemyPlayerColor;
            }

            return rect;
        }

        private void UpdatePlayerIcon(RectTransform icon, bool isLocal, PlayerState state)
        {
            // Scale based on form
            float formScale = 1f + (int)state.Form * 0.15f;
            float baseSize = isLocal ? localPlayerIconSize : iconSize;
            icon.sizeDelta = new Vector2(baseSize * formScale, baseSize * formScale);

            // Pulse effect for local player
            if (isLocal)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.1f;
                icon.localScale = Vector3.one * pulse;
            }
        }

        private RectTransform CreateRuneIcon()
        {
            GameObject iconObj;

            if (runeIconPrefab != null)
            {
                iconObj = Instantiate(runeIconPrefab, mapContainer);
            }
            else
            {
                iconObj = CreateDefaultIcon();
                var image = iconObj.GetComponent<Image>();
                if (image != null)
                {
                    image.color = runeColor;
                }
            }

            var rect = iconObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(iconSize * 0.6f, iconSize * 0.6f);

            return rect;
        }

        private RectTransform CreateShrineIcon()
        {
            GameObject iconObj;

            if (shrineIconPrefab != null)
            {
                iconObj = Instantiate(shrineIconPrefab, mapContainer);
            }
            else
            {
                iconObj = CreateDefaultIcon();
                var image = iconObj.GetComponent<Image>();
                if (image != null)
                {
                    image.color = shrineColor;
                }
            }

            var rect = iconObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(iconSize * 1.5f, iconSize * 1.5f);

            return rect;
        }

        private GameObject CreateDefaultIcon()
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(mapContainer);

            var rect = iconObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(iconSize, iconSize);

            var image = iconObj.AddComponent<Image>();
            image.sprite = null; // Use solid color
            image.color = Color.white;

            return iconObj;
        }

        private void ApplyTheme()
        {
            UITheme theme = UIManager.Instance?.Theme;
            if (theme == null) return;

            if (borderImage != null)
            {
                borderImage.color = theme.borderColor;
            }

            localPlayerColor = theme.primaryColor;
            enemyPlayerColor = theme.dangerColor;
        }

        private void BuildMinimapTextures()
        {
            if (mapBackground == null)
            {
                return;
            }

            int resolution = Mathf.Clamp(mapTextureResolution, 64, 2048);
            var mask = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color insideColor = mapFillColor;
            Color outsideColor = mapOutsideColor;
            if (useMinimapMaterial)
            {
                insideColor = new Color(1f, 1f, 1f, 1f);
                outsideColor = new Color(0f, 0f, 0f, 0f);
            }

            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                float v = (y + 0.5f) / resolution;
                float worldZ = (v - 0.5f) * Constants.ARENA_HEIGHT;

                for (int x = 0; x < resolution; x++)
                {
                    float u = (x + 0.5f) / resolution;
                    float worldX = (u - 0.5f) * Constants.ARENA_WIDTH;

                    bool isInside = ArcaneCircuitMapLogic.IsInsidePlayableArea(new Vector2(worldX, worldZ), 0f);
                    pixels[y * resolution + x] = isInside ? insideColor : outsideColor;
                }
            }

            mask.SetPixels(pixels);
            mask.Apply();
            mapBackground.texture = mask;

            if (navOverlay != null && drawNavOverlay)
            {
                navOverlay.texture = BuildNavOverlay(resolution);
                navOverlay.color = Color.white;
            }

            ApplyMinimapMaterial();
        }

        private void ApplyMinimapMaterial()
        {
            if (mapBackground == null)
            {
                return;
            }

            if (!useMinimapMaterial || minimapMaterial == null)
            {
                if (_minimapMaterialInstance != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(_minimapMaterialInstance);
                    }
                    else
                    {
                        DestroyImmediate(_minimapMaterialInstance);
                    }

                    _minimapMaterialInstance = null;
                }

                mapBackground.material = null;
                return;
            }

            if (_minimapMaterialInstance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_minimapMaterialInstance);
                }
                else
                {
                    DestroyImmediate(_minimapMaterialInstance);
                }
            }

            _minimapMaterialInstance = new Material(minimapMaterial);
            _minimapMaterialInstance.SetColor("_TintColor", mapFillColor);
            _minimapMaterialInstance.SetColor("_OutlineColor", minimapOutlineColor);
            _minimapMaterialInstance.SetColor("_GlowColor", minimapGlowColor);
            _minimapMaterialInstance.SetFloat("_OutlineWidth", minimapOutlineWidth);
            _minimapMaterialInstance.SetFloat("_GlowWidth", minimapGlowWidth);
            _minimapMaterialInstance.SetFloat("_GlowIntensity", minimapGlowIntensity);

            mapBackground.material = _minimapMaterialInstance;
            mapBackground.color = Color.white;
        }

        private Texture2D BuildNavOverlay(int resolution)
        {
            var overlay = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[resolution * resolution];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0f, 0f, 0f, 0f);
            }

            foreach (var corridor in ArcaneCircuitMapData.AllCorridors)
            {
                DrawLineWorld(corridor.Start, corridor.End, navLineColor, navLineThickness, pixels, resolution);
            }

            foreach (var hub in ArcaneCircuitMapData.LargeHubs)
            {
                DrawCircleWorld(hub, ArcaneCircuitMapData.LargeHubRadius, navLineColor, navLineThickness, pixels, resolution);
            }

            foreach (var hub in ArcaneCircuitMapData.SmallHubs)
            {
                DrawCircleWorld(hub, ArcaneCircuitMapData.SmallHubRadius, navLineColor, navLineThickness, pixels, resolution);
            }

            foreach (var zone in ArcaneCircuitMapData.SpawnZones)
            {
                DrawCircleWorld(zone.Center, ArcaneCircuitMapData.SpawnRadius, navLineColor, navLineThickness, pixels, resolution);
            }

            overlay.SetPixels(pixels);
            overlay.Apply();
            return overlay;
        }

        private void DrawLineWorld(
            Vector2 start,
            Vector2 end,
            Color color,
            float thickness,
            Color[] pixels,
            int resolution)
        {
            float pixelsPerUnitX = resolution / Constants.ARENA_WIDTH;
            float pixelsPerUnitY = resolution / Constants.ARENA_HEIGHT;
            float pixelsPerUnit = Mathf.Max(pixelsPerUnitX, pixelsPerUnitY);

            float distance = Vector2.Distance(start, end);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance * pixelsPerUnit));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 pos = Vector2.Lerp(start, end, t);
                SetPixelWorld(pos, color, thickness, pixels, resolution);
            }
        }

        private void DrawCircleWorld(
            Vector2 center,
            float radius,
            Color color,
            float thickness,
            Color[] pixels,
            int resolution)
        {
            int segments = Mathf.Max(12, Mathf.RoundToInt(radius * 6f));
            float step = Mathf.PI * 2f / segments;

            Vector2 prev = center + new Vector2(Mathf.Cos(0f), Mathf.Sin(0f)) * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                DrawLineWorld(prev, next, color, thickness, pixels, resolution);
                prev = next;
            }
        }

        private void SetPixelWorld(Vector2 worldPos, Color color, float thickness, Color[] pixels, int resolution)
        {
            Vector2 uv = WorldToUv(worldPos);
            int px = Mathf.RoundToInt(uv.x * (resolution - 1));
            int py = Mathf.RoundToInt(uv.y * (resolution - 1));
            DrawPixel(px, py, color, thickness, pixels, resolution);
        }

        private Vector2 WorldToUv(Vector2 worldPos)
        {
            float u = (worldPos.x / Constants.ARENA_WIDTH) + 0.5f;
            float v = (worldPos.y / Constants.ARENA_HEIGHT) + 0.5f;
            return new Vector2(u, v);
        }

        private void DrawPixel(int x, int y, Color color, float thickness, Color[] pixels, int resolution)
        {
            int radius = Mathf.Max(1, Mathf.RoundToInt(thickness));
            int rSquared = radius * radius;

            for (int dy = -radius; dy <= radius; dy++)
            {
                int py = y + dy;
                if (py < 0 || py >= resolution)
                {
                    continue;
                }

                for (int dx = -radius; dx <= radius; dx++)
                {
                    int px = x + dx;
                    if (px < 0 || px >= resolution)
                    {
                        continue;
                    }

                    if (dx * dx + dy * dy > rSquared)
                    {
                        continue;
                    }

                    int index = py * resolution + px;
                    pixels[index] = Color.Lerp(pixels[index], color, color.a);
                }
            }
        }

        public void ClearAll()
        {
            foreach (var icon in _playerIcons.Values)
            {
                if (icon != null) Destroy(icon.gameObject);
            }
            _playerIcons.Clear();

            foreach (var icon in _runeIcons.Values)
            {
                if (icon != null) Destroy(icon.gameObject);
            }
            _runeIcons.Clear();

            foreach (var icon in _shrineIcons.Values)
            {
                if (icon != null) Destroy(icon.gameObject);
            }
            _shrineIcons.Clear();
        }

        private static string BytesToHex(byte[] bytes)
        {
            if (bytes == null) return "";
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }
}

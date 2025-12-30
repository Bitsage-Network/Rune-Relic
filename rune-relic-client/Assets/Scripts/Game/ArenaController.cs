using UnityEngine;
using RuneRelic.Utils;

namespace RuneRelic.Game
{
    /// <summary>
    /// Controls the arena visuals including floor, boundaries, and shrinking zone.
    /// </summary>
    public class ArenaController : MonoBehaviour
    {
        [Header("Arena Objects")]
        [SerializeField] private Transform arenaFloor;
        [SerializeField] private Transform arenaBoundary;
        [SerializeField] private Transform shrinkingZone;
        [SerializeField] private LineRenderer boundaryLine;

        [Header("Materials")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material boundaryMaterial;
        [SerializeField] private Material dangerZoneMaterial;

        [Header("Settings")]
        [SerializeField] private Color safeZoneColor = new Color(0.2f, 0.4f, 0.2f, 0.5f);
        [SerializeField] private Color dangerZoneColor = new Color(0.6f, 0.1f, 0.1f, 0.5f);
        [SerializeField] private float boundaryHeight = 2f;
        [SerializeField] private int boundarySegments = 64;

        // State
        private float _currentWidth;
        private float _currentHeight;
        private float _targetWidth;
        private float _targetHeight;
        private float _shrinkProgress;
        private bool _isShrinking;

        private void Start()
        {
            // Initialize at full size
            _currentWidth = Constants.ARENA_WIDTH;
            _currentHeight = Constants.ARENA_HEIGHT;
            _targetWidth = _currentWidth;
            _targetHeight = _currentHeight;

            SetupArena();
        }

        private void Update()
        {
            // Smoothly shrink arena
            if (_isShrinking)
            {
                _currentWidth = Mathf.Lerp(_currentWidth, _targetWidth, Time.deltaTime * 2f);
                _currentHeight = Mathf.Lerp(_currentHeight, _targetHeight, Time.deltaTime * 2f);
                UpdateBoundaryVisual();

                // Check if shrink complete
                if (Mathf.Abs(_currentWidth - _targetWidth) < 0.1f)
                {
                    _currentWidth = _targetWidth;
                    _currentHeight = _targetHeight;
                    _isShrinking = false;
                }
            }

            // Pulse danger zone when shrinking
            if (_isShrinking && dangerZoneMaterial != null)
            {
                float pulse = Mathf.PingPong(Time.time * 2f, 1f);
                Color pulsedColor = Color.Lerp(dangerZoneColor, Color.red, pulse * 0.3f);
                dangerZoneMaterial.color = pulsedColor;
            }
        }

        /// <summary>
        /// Initialize arena setup.
        /// </summary>
        public void SetupArena()
        {
            // Setup floor
            if (arenaFloor != null)
            {
                arenaFloor.localScale = new Vector3(
                    Constants.ARENA_WIDTH / 10f,
                    1f,
                    Constants.ARENA_HEIGHT / 10f
                );
                arenaFloor.position = Vector3.zero;
            }
            else
            {
                CreateFloor();
            }

            // Setup boundary
            if (boundaryLine != null)
            {
                SetupBoundaryLine();
            }
            else
            {
                CreateBoundaryLine();
            }

            // Setup shrinking zone indicator
            if (shrinkingZone == null)
            {
                CreateShrinkingZone();
            }
        }

        /// <summary>
        /// Update arena size (from server shrink event).
        /// </summary>
        public void SetArenaSize(float width, float height, float progress)
        {
            _targetWidth = width;
            _targetHeight = height;
            _shrinkProgress = progress;
            _isShrinking = true;

            // Update shrinking zone to show new boundary
            UpdateShrinkingZone();
        }

        /// <summary>
        /// Check if a position is within the current arena bounds.
        /// </summary>
        public bool IsInBounds(Vector3 position)
        {
            float halfWidth = _currentWidth / 2f;
            float halfHeight = _currentHeight / 2f;

            return position.x >= -halfWidth && position.x <= halfWidth &&
                   position.z >= -halfHeight && position.z <= halfHeight;
        }

        /// <summary>
        /// Get distance to nearest boundary edge.
        /// </summary>
        public float GetDistanceToBoundary(Vector3 position)
        {
            float halfWidth = _currentWidth / 2f;
            float halfHeight = _currentHeight / 2f;

            float distX = Mathf.Min(halfWidth - Mathf.Abs(position.x));
            float distZ = Mathf.Min(halfHeight - Mathf.Abs(position.z));

            return Mathf.Min(distX, distZ);
        }

        private void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ArenaFloor";
            floor.transform.SetParent(transform);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(
                Constants.ARENA_WIDTH / 10f,
                1f,
                Constants.ARENA_HEIGHT / 10f
            );

            arenaFloor = floor.transform;

            // Set floor material
            var renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (floorMaterial != null)
                {
                    renderer.material = floorMaterial;
                }
                else
                {
                    renderer.material.color = new Color(0.15f, 0.15f, 0.2f);
                }
            }
        }

        private void CreateBoundaryLine()
        {
            GameObject boundaryObj = new GameObject("BoundaryLine");
            boundaryObj.transform.SetParent(transform);

            boundaryLine = boundaryObj.AddComponent<LineRenderer>();
            SetupBoundaryLine();
        }

        private void SetupBoundaryLine()
        {
            if (boundaryLine == null) return;

            boundaryLine.positionCount = boundarySegments + 1;
            boundaryLine.loop = true;
            boundaryLine.startWidth = 0.2f;
            boundaryLine.endWidth = 0.2f;
            boundaryLine.useWorldSpace = true;

            if (boundaryMaterial != null)
            {
                boundaryLine.material = boundaryMaterial;
            }
            else
            {
                boundaryLine.material = new Material(Shader.Find("Sprites/Default"));
                boundaryLine.startColor = Color.cyan;
                boundaryLine.endColor = Color.cyan;
            }

            UpdateBoundaryVisual();
        }

        private void UpdateBoundaryVisual()
        {
            if (boundaryLine == null) return;

            float halfWidth = _currentWidth / 2f;
            float halfHeight = _currentHeight / 2f;

            // Create rectangular boundary
            Vector3[] points = new Vector3[boundarySegments + 1];

            int segmentsPerSide = boundarySegments / 4;

            for (int i = 0; i <= segmentsPerSide; i++)
            {
                float t = (float)i / segmentsPerSide;
                points[i] = new Vector3(-halfWidth + t * _currentWidth, 0.1f, -halfHeight);
            }

            for (int i = 0; i <= segmentsPerSide; i++)
            {
                float t = (float)i / segmentsPerSide;
                points[segmentsPerSide + i] = new Vector3(halfWidth, 0.1f, -halfHeight + t * _currentHeight);
            }

            for (int i = 0; i <= segmentsPerSide; i++)
            {
                float t = (float)i / segmentsPerSide;
                points[2 * segmentsPerSide + i] = new Vector3(halfWidth - t * _currentWidth, 0.1f, halfHeight);
            }

            for (int i = 0; i < segmentsPerSide; i++)
            {
                float t = (float)i / segmentsPerSide;
                points[3 * segmentsPerSide + i] = new Vector3(-halfWidth, 0.1f, halfHeight - t * _currentHeight);
            }

            // Close the loop
            points[boundarySegments] = points[0];

            boundaryLine.SetPositions(points);

            // Change color when shrinking
            if (_isShrinking)
            {
                float pulse = Mathf.PingPong(Time.time * 3f, 1f);
                boundaryLine.startColor = Color.Lerp(Color.cyan, Color.red, pulse);
                boundaryLine.endColor = boundaryLine.startColor;
            }
            else
            {
                boundaryLine.startColor = Color.cyan;
                boundaryLine.endColor = Color.cyan;
            }
        }

        private void CreateShrinkingZone()
        {
            // Create transparent plane showing the danger zone
            GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Quad);
            zone.name = "ShrinkingZone";
            zone.transform.SetParent(transform);
            zone.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            zone.transform.position = new Vector3(0, 0.05f, 0);

            // Remove collider
            var collider = zone.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            shrinkingZone = zone.transform;

            // Set material
            var renderer = zone.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (dangerZoneMaterial != null)
                {
                    renderer.material = dangerZoneMaterial;
                }
                else
                {
                    renderer.material = new Material(Shader.Find("Standard"));
                    renderer.material.SetFloat("_Mode", 3);
                    renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    renderer.material.SetInt("_ZWrite", 0);
                    renderer.material.EnableKeyword("_ALPHABLEND_ON");
                    renderer.material.renderQueue = 3000;
                    renderer.material.color = dangerZoneColor;
                }
            }

            zone.SetActive(false);
        }

        private void UpdateShrinkingZone()
        {
            if (shrinkingZone == null) return;

            if (_isShrinking && _targetWidth < _currentWidth)
            {
                shrinkingZone.gameObject.SetActive(true);
                shrinkingZone.localScale = new Vector3(
                    Constants.ARENA_WIDTH,
                    Constants.ARENA_HEIGHT,
                    1f
                );
            }
            else
            {
                shrinkingZone.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Get current arena dimensions.
        /// </summary>
        public Vector2 GetCurrentSize()
        {
            return new Vector2(_currentWidth, _currentHeight);
        }
    }
}

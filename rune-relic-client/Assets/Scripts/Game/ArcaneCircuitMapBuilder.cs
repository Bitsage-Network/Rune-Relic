using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RuneRelic.Game
{
    /// <summary>
    /// Procedural builder for Arcane Circuit walls and optional floor meshes.
    /// </summary>
    public class ArcaneCircuitMapBuilder : MonoBehaviour
    {
        [Header("Build")]
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private bool buildFloor = false;
        [SerializeField] private bool useMergedFloor = true;
        [SerializeField] private bool buildWallMeshes = true;
        [SerializeField] private bool buildWallColliders = true;
        [SerializeField] private bool buildNavMesh = false;
        [SerializeField] private bool buildNavGraph = true;

        [Header("Floor")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private float floorHeight = 0.02f;
        [SerializeField] private int hubFloorSegments = 48;
        [SerializeField] private float floorCellSize = 1f;
        [SerializeField] private bool useTiledFloorUVs = false;
        [SerializeField] private float floorUvScale = 10f;

        [Header("Walls")]
        [SerializeField] private Material wallMaterial;
        [SerializeField] private float wallHeight = 2.5f;
        [SerializeField] private float wallThickness = 0.6f;
        [SerializeField] private int hubWallSegments = 64;
        [SerializeField] private float wallOpeningMargin = 0.5f;
        [SerializeField] private int wallLayer = 0;

        private readonly List<GameObject> _generated = new List<GameObject>();

        public ArcaneCircuitNavGraph NavGraph { get; private set; }

        private void Start()
        {
            if (buildOnStart)
            {
                Build();
            }
        }

        public void Build()
        {
            Clear();

            if (buildFloor)
            {
                BuildFloorMeshes();
            }

            BuildWalls();
            TryBuildNavMesh();
            TryBuildNavGraph();
        }

        public void Clear()
        {
            for (int i = 0; i < _generated.Count; i++)
            {
                var obj = _generated[i];
                if (obj == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(obj);
                }
                else
                {
                    DestroyImmediate(obj);
                }
            }

            _generated.Clear();
            NavGraph = null;
        }

        private void BuildFloorMeshes()
        {
            if (useMergedFloor)
            {
                BuildMergedFloor();
                return;
            }

            Transform floorRoot = CreateRoot("ArcaneCircuitFloor");

            foreach (var hub in ArcaneCircuitMapData.LargeHubs)
            {
                CreateDisc(hub, ArcaneCircuitMapData.LargeHubRadius, hubFloorSegments, floorRoot);
            }

            foreach (var hub in ArcaneCircuitMapData.SmallHubs)
            {
                CreateDisc(hub, ArcaneCircuitMapData.SmallHubRadius, hubFloorSegments, floorRoot);
            }

            foreach (var corridor in ArcaneCircuitMapData.AllCorridors)
            {
                CreateCorridorFloor(corridor, floorRoot);
            }
        }

        private void BuildMergedFloor()
        {
            Transform floorRoot = CreateRoot("ArcaneCircuitFloor");
            var mesh = BuildMergedFloorMesh();

            var obj = new GameObject("Floor");
            obj.transform.SetParent(floorRoot, false);
            obj.transform.position = new Vector3(0f, floorHeight, 0f);

            var filter = obj.AddComponent<MeshFilter>();
            var renderer = obj.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            if (floorMaterial != null)
            {
                renderer.sharedMaterial = floorMaterial;
            }

            _generated.Add(obj);
        }

        private Mesh BuildMergedFloorMesh()
        {
            float cellSize = Mathf.Max(0.25f, floorCellSize);
            GetMapBounds(out float minX, out float maxX, out float minZ, out float maxZ);

            int cols = Mathf.CeilToInt((maxX - minX) / cellSize);
            int rows = Mathf.CeilToInt((maxZ - minZ) / cellSize);
            cols = Mathf.Max(1, cols);
            rows = Mathf.Max(1, rows);

            bool[,] samples = new bool[cols + 1, rows + 1];
            for (int x = 0; x <= cols; x++)
            {
                float worldX = minX + x * cellSize;
                for (int z = 0; z <= rows; z++)
                {
                    float worldZ = minZ + z * cellSize;
                    samples[x, z] = ArcaneCircuitMapLogic.IsInsidePlayableArea(new Vector2(worldX, worldZ), 0f);
                }
            }

            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var polygonA = new List<Vector3>(6);
            var polygonB = new List<Vector3>(6);

            for (int x = 0; x < cols; x++)
            {
                float x0 = minX + x * cellSize;
                float x1 = x0 + cellSize;

                for (int z = 0; z < rows; z++)
                {
                    float z0 = minZ + z * cellSize;
                    float z1 = z0 + cellSize;

                    bool bl = samples[x, z];
                    bool br = samples[x + 1, z];
                    bool tr = samples[x + 1, z + 1];
                    bool tl = samples[x, z + 1];

                    int mask = (bl ? 1 : 0) | (br ? 2 : 0) | (tr ? 4 : 0) | (tl ? 8 : 0);
                    if (mask == 0)
                    {
                        continue;
                    }

                    Vector3 p0 = new Vector3(x0, 0f, z0);
                    Vector3 p1 = new Vector3(x1, 0f, z0);
                    Vector3 p2 = new Vector3(x1, 0f, z1);
                    Vector3 p3 = new Vector3(x0, 0f, z1);
                    Vector3 e0 = (p0 + p1) * 0.5f;
                    Vector3 e1 = (p1 + p2) * 0.5f;
                    Vector3 e2 = (p2 + p3) * 0.5f;
                    Vector3 e3 = (p3 + p0) * 0.5f;

                    bool centerInside = false;
                    if (mask == 5 || mask == 10)
                    {
                        float centerX = (x0 + x1) * 0.5f;
                        float centerZ = (z0 + z1) * 0.5f;
                        centerInside = ArcaneCircuitMapLogic.IsInsidePlayableArea(new Vector2(centerX, centerZ), 0f);
                    }

                    AddMarchingSquaresCell(mask, p0, p1, p2, p3, e0, e1, e2, e3,
                        centerInside, polygonA, polygonB, vertices, indices);
                }
            }

            var uvs = BuildFloorUvs(vertices);

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private List<Vector2> BuildFloorUvs(List<Vector3> vertices)
        {
            var uvs = new List<Vector2>(vertices.Count);
            if (vertices.Count == 0)
            {
                return uvs;
            }

            if (useTiledFloorUVs)
            {
                float scale = Mathf.Max(0.01f, floorUvScale);
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector3 position = vertices[i];
                    uvs.Add(new Vector2(position.x / scale, position.z / scale));
                }

                return uvs;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 position = vertices[i];
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minZ = Mathf.Min(minZ, position.z);
                maxZ = Mathf.Max(maxZ, position.z);
            }

            float width = Mathf.Max(0.01f, maxX - minX);
            float height = Mathf.Max(0.01f, maxZ - minZ);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 position = vertices[i];
                float u = (position.x - minX) / width;
                float v = (position.z - minZ) / height;
                uvs.Add(new Vector2(u, v));
            }

            return uvs;
        }

        private void AddMarchingSquaresCell(
            int mask,
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            Vector3 e0,
            Vector3 e1,
            Vector3 e2,
            Vector3 e3,
            bool centerInside,
            List<Vector3> polygonA,
            List<Vector3> polygonB,
            List<Vector3> vertices,
            List<int> indices)
        {
            polygonA.Clear();
            polygonB.Clear();

            switch (mask)
            {
                case 1:
                    polygonA.Add(p0);
                    polygonA.Add(e0);
                    polygonA.Add(e3);
                    break;
                case 2:
                    polygonA.Add(p1);
                    polygonA.Add(e1);
                    polygonA.Add(e0);
                    break;
                case 3:
                    polygonA.Add(p0);
                    polygonA.Add(p1);
                    polygonA.Add(e1);
                    polygonA.Add(e3);
                    break;
                case 4:
                    polygonA.Add(p2);
                    polygonA.Add(e2);
                    polygonA.Add(e1);
                    break;
                case 5:
                    if (centerInside)
                    {
                        polygonA.Add(p0);
                        polygonA.Add(e0);
                        polygonA.Add(e1);
                        polygonA.Add(p2);
                        polygonA.Add(e2);
                        polygonA.Add(e3);
                    }
                    else
                    {
                        polygonA.Add(p0);
                        polygonA.Add(e0);
                        polygonA.Add(e3);
                        polygonB.Add(p2);
                        polygonB.Add(e2);
                        polygonB.Add(e1);
                    }
                    break;
                case 6:
                    polygonA.Add(p1);
                    polygonA.Add(p2);
                    polygonA.Add(e2);
                    polygonA.Add(e0);
                    break;
                case 7:
                    polygonA.Add(p0);
                    polygonA.Add(p1);
                    polygonA.Add(p2);
                    polygonA.Add(e2);
                    polygonA.Add(e3);
                    break;
                case 8:
                    polygonA.Add(p3);
                    polygonA.Add(e3);
                    polygonA.Add(e2);
                    break;
                case 9:
                    polygonA.Add(p3);
                    polygonA.Add(p0);
                    polygonA.Add(e0);
                    polygonA.Add(e2);
                    break;
                case 10:
                    if (centerInside)
                    {
                        polygonA.Add(p1);
                        polygonA.Add(e1);
                        polygonA.Add(e2);
                        polygonA.Add(p3);
                        polygonA.Add(e3);
                        polygonA.Add(e0);
                    }
                    else
                    {
                        polygonA.Add(p1);
                        polygonA.Add(e1);
                        polygonA.Add(e0);
                        polygonB.Add(p3);
                        polygonB.Add(e3);
                        polygonB.Add(e2);
                    }
                    break;
                case 11:
                    polygonA.Add(p0);
                    polygonA.Add(p1);
                    polygonA.Add(e1);
                    polygonA.Add(e2);
                    polygonA.Add(p3);
                    break;
                case 12:
                    polygonA.Add(p2);
                    polygonA.Add(p3);
                    polygonA.Add(e3);
                    polygonA.Add(e1);
                    break;
                case 13:
                    polygonA.Add(p0);
                    polygonA.Add(e0);
                    polygonA.Add(e1);
                    polygonA.Add(p2);
                    polygonA.Add(p3);
                    break;
                case 14:
                    polygonA.Add(p1);
                    polygonA.Add(p2);
                    polygonA.Add(p3);
                    polygonA.Add(e3);
                    polygonA.Add(e0);
                    break;
                case 15:
                    polygonA.Add(p0);
                    polygonA.Add(p1);
                    polygonA.Add(p2);
                    polygonA.Add(p3);
                    break;
            }

            if (polygonA.Count >= 3)
            {
                AddPolygon(polygonA, vertices, indices);
            }

            if (polygonB.Count >= 3)
            {
                AddPolygon(polygonB, vertices, indices);
            }
        }

        private void AddPolygon(List<Vector3> polygon, List<Vector3> vertices, List<int> indices)
        {
            EnsureClockwise(polygon);

            switch (polygon.Count)
            {
                case 3:
                    AddTriangle(polygon[0], polygon[1], polygon[2], vertices, indices);
                    return;
                case 4:
                    AddQuadBestDiagonal(polygon, vertices, indices);
                    return;
            }

            var earIndices = new List<int>(polygon.Count);
            for (int i = 0; i < polygon.Count; i++)
            {
                earIndices.Add(i);
            }

            while (earIndices.Count > 2)
            {
                int best = 0;
                float bestArea = -1f;

                for (int i = 0; i < earIndices.Count; i++)
                {
                    int prevIndex = earIndices[(i - 1 + earIndices.Count) % earIndices.Count];
                    int currIndex = earIndices[i];
                    int nextIndex = earIndices[(i + 1) % earIndices.Count];

                    float area = TriangleArea(polygon[prevIndex], polygon[currIndex], polygon[nextIndex]);
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = i;
                    }
                }

                int prev = earIndices[(best - 1 + earIndices.Count) % earIndices.Count];
                int curr = earIndices[best];
                int next = earIndices[(best + 1) % earIndices.Count];

                AddTriangle(polygon[prev], polygon[curr], polygon[next], vertices, indices);
                earIndices.RemoveAt(best);
            }
        }

        private void AddQuadBestDiagonal(List<Vector3> polygon, List<Vector3> vertices, List<int> indices)
        {
            Vector3 a = polygon[0];
            Vector3 b = polygon[1];
            Vector3 c = polygon[2];
            Vector3 d = polygon[3];

            float minAreaAC = Mathf.Min(TriangleArea(a, b, c), TriangleArea(a, c, d));
            float minAreaBD = Mathf.Min(TriangleArea(b, c, d), TriangleArea(b, d, a));

            if (minAreaAC >= minAreaBD)
            {
                AddTriangle(a, b, c, vertices, indices);
                AddTriangle(a, c, d, vertices, indices);
            }
            else
            {
                AddTriangle(b, c, d, vertices, indices);
                AddTriangle(b, d, a, vertices, indices);
            }
        }

        private void AddTriangle(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            List<Vector3> vertices,
            List<int> indices)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);

            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
        }

        private void EnsureClockwise(List<Vector3> polygon)
        {
            if (SignedArea(polygon) > 0f)
            {
                polygon.Reverse();
            }
        }

        private float SignedArea(List<Vector3> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 a = polygon[i];
                Vector3 b = polygon[(i + 1) % polygon.Count];
                area += (a.x * b.z) - (b.x * a.z);
            }

            return area * 0.5f;
        }

        private float TriangleArea(Vector3 a, Vector3 b, Vector3 c)
        {
            float abx = b.x - a.x;
            float abz = b.z - a.z;
            float acx = c.x - a.x;
            float acz = c.z - a.z;
            return Mathf.Abs(abx * acz - abz * acx) * 0.5f;
        }

        private void GetMapBounds(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            minZ = float.MaxValue;
            maxZ = float.MinValue;

            float corridorHalf = ArcaneCircuitMapData.CorridorWidth * 0.5f;

            foreach (var hub in ArcaneCircuitMapData.LargeHubs)
            {
                UpdateBounds(hub, ArcaneCircuitMapData.LargeHubRadius, ref minX, ref maxX, ref minZ, ref maxZ);
            }
            foreach (var hub in ArcaneCircuitMapData.SmallHubs)
            {
                UpdateBounds(hub, ArcaneCircuitMapData.SmallHubRadius, ref minX, ref maxX, ref minZ, ref maxZ);
            }
            foreach (var corridor in ArcaneCircuitMapData.AllCorridors)
            {
                UpdateBounds(corridor.Start, corridorHalf, ref minX, ref maxX, ref minZ, ref maxZ);
                UpdateBounds(corridor.End, corridorHalf, ref minX, ref maxX, ref minZ, ref maxZ);
            }
            foreach (var zone in ArcaneCircuitMapData.SpawnZones)
            {
                UpdateBounds(zone.Center, ArcaneCircuitMapData.SpawnRadius, ref minX, ref maxX, ref minZ, ref maxZ);
            }

            float padding = 2f;
            minX -= padding;
            maxX += padding;
            minZ -= padding;
            maxZ += padding;
        }

        private void UpdateBounds(Vector2 center, float radius, ref float minX, ref float maxX, ref float minZ, ref float maxZ)
        {
            minX = Mathf.Min(minX, center.x - radius);
            maxX = Mathf.Max(maxX, center.x + radius);
            minZ = Mathf.Min(minZ, center.y - radius);
            maxZ = Mathf.Max(maxZ, center.y + radius);
        }

        private void BuildWalls()
        {
            Transform wallRoot = CreateRoot("ArcaneCircuitWalls");

            foreach (var corridor in ArcaneCircuitMapData.AllCorridors)
            {
                BuildCorridorWalls(corridor, wallRoot);
            }

            foreach (var hub in ArcaneCircuitMapData.LargeHubs)
            {
                BuildHubWalls(hub, ArcaneCircuitMapData.LargeHubRadius, wallRoot);
            }

            foreach (var hub in ArcaneCircuitMapData.SmallHubs)
            {
                BuildHubWalls(hub, ArcaneCircuitMapData.SmallHubRadius, wallRoot);
            }
        }

        private void TryBuildNavMesh()
        {
            if (!buildNavMesh)
            {
                return;
            }

            var surfaceType = System.Type.GetType("UnityEngine.AI.NavMeshSurface, Unity.AI.Navigation");
            if (surfaceType == null)
            {
                Debug.LogWarning("[ArcaneCircuitMapBuilder] NavMeshSurface type not found. Install Unity AI Navigation package.");
                return;
            }

            var surface = GetComponent(surfaceType);
            if (surface == null)
            {
                surface = gameObject.AddComponent(surfaceType);
            }

            var buildMethod = surfaceType.GetMethod("BuildNavMesh", BindingFlags.Instance | BindingFlags.Public);
            buildMethod?.Invoke(surface, null);
        }

        private void TryBuildNavGraph()
        {
            if (!buildNavGraph)
            {
                return;
            }

            NavGraph = ArcaneCircuitNavGraph.BuildDefault();
        }

        private Transform CreateRoot(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            _generated.Add(root.gameObject);
            return root;
        }

        private void CreateDisc(Vector2 center, float radius, int segments, Transform parent)
        {
            var mesh = BuildDiscMesh(radius, Mathf.Max(12, segments));
            var obj = new GameObject($"Hub_{center.x}_{center.y}");
            obj.transform.SetParent(parent, false);
            obj.transform.position = new Vector3(center.x, floorHeight, center.y);

            var filter = obj.AddComponent<MeshFilter>();
            var renderer = obj.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            if (floorMaterial != null)
            {
                renderer.sharedMaterial = floorMaterial;
            }

            _generated.Add(obj);
        }

        private void CreateCorridorFloor(ArcaneCircuitSegment corridor, Transform parent)
        {
            Vector2 start = corridor.Start;
            Vector2 end = corridor.End;
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            var obj = new GameObject("Corridor");
            obj.transform.SetParent(parent, false);
            obj.transform.position = new Vector3((start.x + end.x) * 0.5f, floorHeight, (start.y + end.y) * 0.5f);
            obj.transform.rotation = Quaternion.LookRotation(new Vector3(delta.x, 0f, delta.y));
            obj.transform.localScale = new Vector3(ArcaneCircuitMapData.CorridorWidth, 1f, length);

            var filter = obj.AddComponent<MeshFilter>();
            var renderer = obj.AddComponent<MeshRenderer>();
            filter.sharedMesh = BuildQuadMesh();
            if (floorMaterial != null)
            {
                renderer.sharedMaterial = floorMaterial;
            }

            _generated.Add(obj);
        }

        private void BuildCorridorWalls(ArcaneCircuitSegment corridor, Transform parent)
        {
            if (!TryTrimForHubs(corridor, out var trimmed))
            {
                return;
            }

            Vector2 start = trimmed.Start;
            Vector2 end = trimmed.End;
            Vector2 dir = (end - start).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float halfWidth = ArcaneCircuitMapData.CorridorWidth * 0.5f;

            Vector2 leftStart = start + perp * halfWidth;
            Vector2 leftEnd = end + perp * halfWidth;
            Vector2 rightStart = start - perp * halfWidth;
            Vector2 rightEnd = end - perp * halfWidth;

            CreateWallSegment(leftStart, leftEnd, parent);
            CreateWallSegment(rightStart, rightEnd, parent);
        }

        private void BuildHubWalls(Vector2 center, float radius, Transform parent)
        {
            int segments = Mathf.Max(12, hubWallSegments);
            float step = Mathf.PI * 2f / segments;
            float openMargin = wallOpeningMargin;

            for (int i = 0; i < segments; i++)
            {
                float angle0 = step * i;
                float angle1 = step * (i + 1);
                Vector2 p0 = center + new Vector2(Mathf.Cos(angle0), Mathf.Sin(angle0)) * radius;
                Vector2 p1 = center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * radius;
                Vector2 mid = (p0 + p1) * 0.5f;

                if (ArcaneCircuitMapLogic.IsInsideAnyCorridor(mid, 0f, openMargin))
                {
                    continue;
                }

                CreateWallSegment(p0, p1, parent);
            }
        }

        private void CreateWallSegment(Vector2 start, Vector2 end, Transform parent)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Wall";
            obj.layer = wallLayer;
            obj.transform.SetParent(parent, false);
            obj.transform.position = new Vector3((start.x + end.x) * 0.5f, wallHeight * 0.5f, (start.y + end.y) * 0.5f);
            obj.transform.rotation = Quaternion.LookRotation(new Vector3(delta.x, 0f, delta.y));
            obj.transform.localScale = new Vector3(wallThickness, wallHeight, length);

            var renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (buildWallMeshes && wallMaterial != null)
                {
                    renderer.sharedMaterial = wallMaterial;
                }
                else if (!buildWallMeshes)
                {
                    renderer.enabled = false;
                }
            }

            if (!buildWallColliders)
            {
                var collider = obj.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(collider);
                    }
                    else
                    {
                        DestroyImmediate(collider);
                    }
                }
            }

            _generated.Add(obj);
        }

        private static Mesh BuildQuadMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildDiscMesh(float radius, int segments)
        {
            var mesh = new Mesh();
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = step * i;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int triIndex = i * 3;
                triangles[triIndex] = 0;
                triangles[triIndex + 1] = next + 1;
                triangles[triIndex + 2] = i + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        private static bool TryTrimForHubs(ArcaneCircuitSegment corridor, out ArcaneCircuitSegment trimmed)
        {
            Vector2 start = corridor.Start;
            Vector2 end = corridor.End;
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                trimmed = corridor;
                return false;
            }

            float startTrim = 0f;
            float endTrim = 0f;
            if (ArcaneCircuitMapData.TryGetHubRadius(start, out var startRadius))
            {
                startTrim = startRadius;
            }
            if (ArcaneCircuitMapData.TryGetHubRadius(end, out var endRadius))
            {
                endTrim = endRadius;
            }

            float usable = length - startTrim - endTrim;
            if (usable <= 0.1f)
            {
                trimmed = corridor;
                return false;
            }

            Vector2 dir = delta / length;
            Vector2 trimmedStart = start + dir * startTrim;
            Vector2 trimmedEnd = end - dir * endTrim;
            trimmed = new ArcaneCircuitSegment(trimmedStart, trimmedEnd);
            return true;
        }
    }
}

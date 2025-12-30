using UnityEngine;

namespace RuneRelic.Game
{
    public readonly struct ArcaneCircuitSegment
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;

        public ArcaneCircuitSegment(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;
        }
    }

    public readonly struct ArcaneCircuitSpawnZone
    {
        public readonly int Id;
        public readonly Vector2 Anchor;
        public readonly Vector2 Center;

        public ArcaneCircuitSpawnZone(int id, Vector2 anchor, Vector2 center)
        {
            Id = id;
            Anchor = anchor;
            Center = center;
        }
    }

    public static class ArcaneCircuitMapData
    {
        public const float LargeHubRadius = 35f;
        public const float SmallHubRadius = 18f;
        public const float CorridorWidth = 7f;
        public const float SpawnOffset = 20f;
        public const float SpawnRadius = 5f;

        public static readonly Vector2[] LargeHubs =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 90f),
            new Vector2(0f, -90f),
            new Vector2(140f, 0f),
            new Vector2(-140f, 0f),
        };

        public static readonly Vector2[] SmallHubs =
        {
            new Vector2(70f, 45f),
            new Vector2(-70f, 45f),
            new Vector2(70f, -45f),
            new Vector2(-70f, -45f),
        };

        public static readonly ArcaneCircuitSegment[] CoreCorridors =
        {
            // Inner spokes
            new ArcaneCircuitSegment(new Vector2(0f, 0f), new Vector2(70f, 45f)),
            new ArcaneCircuitSegment(new Vector2(0f, 0f), new Vector2(-70f, 45f)),
            new ArcaneCircuitSegment(new Vector2(0f, 0f), new Vector2(70f, -45f)),
            new ArcaneCircuitSegment(new Vector2(0f, 0f), new Vector2(-70f, -45f)),

            // Junction connectors
            new ArcaneCircuitSegment(new Vector2(70f, 45f), new Vector2(0f, 90f)),
            new ArcaneCircuitSegment(new Vector2(70f, 45f), new Vector2(140f, 0f)),
            new ArcaneCircuitSegment(new Vector2(-70f, 45f), new Vector2(0f, 90f)),
            new ArcaneCircuitSegment(new Vector2(-70f, 45f), new Vector2(-140f, 0f)),
            new ArcaneCircuitSegment(new Vector2(70f, -45f), new Vector2(0f, -90f)),
            new ArcaneCircuitSegment(new Vector2(70f, -45f), new Vector2(140f, 0f)),
            new ArcaneCircuitSegment(new Vector2(-70f, -45f), new Vector2(0f, -90f)),
            new ArcaneCircuitSegment(new Vector2(-70f, -45f), new Vector2(-140f, 0f)),

            // Outer ring (3-segment polyline per side)
            new ArcaneCircuitSegment(new Vector2(0f, 90f), new Vector2(0f, 140f)),
            new ArcaneCircuitSegment(new Vector2(0f, 140f), new Vector2(140f, 140f)),
            new ArcaneCircuitSegment(new Vector2(140f, 140f), new Vector2(140f, 0f)),

            new ArcaneCircuitSegment(new Vector2(140f, 0f), new Vector2(140f, -140f)),
            new ArcaneCircuitSegment(new Vector2(140f, -140f), new Vector2(0f, -140f)),
            new ArcaneCircuitSegment(new Vector2(0f, -140f), new Vector2(0f, -90f)),

            new ArcaneCircuitSegment(new Vector2(0f, -90f), new Vector2(0f, -140f)),
            new ArcaneCircuitSegment(new Vector2(0f, -140f), new Vector2(-140f, -140f)),
            new ArcaneCircuitSegment(new Vector2(-140f, -140f), new Vector2(-140f, 0f)),

            new ArcaneCircuitSegment(new Vector2(-140f, 0f), new Vector2(-140f, 140f)),
            new ArcaneCircuitSegment(new Vector2(-140f, 140f), new Vector2(0f, 140f)),
            new ArcaneCircuitSegment(new Vector2(0f, 140f), new Vector2(0f, 90f)),
        };

        public static readonly Vector2[] SpawnAnchors =
        {
            new Vector2(-20f, 115f),
            new Vector2(20f, 115f),
            new Vector2(-20f, -115f),
            new Vector2(20f, -115f),
            new Vector2(165f, 20f),
            new Vector2(165f, -20f),
            new Vector2(-165f, 20f),
            new Vector2(-165f, -20f),
            new Vector2(110f, 80f),
            new Vector2(120f, 70f),
            new Vector2(-110f, 80f),
            new Vector2(-120f, 70f),
            new Vector2(110f, -80f),
            new Vector2(120f, -70f),
            new Vector2(-110f, -80f),
            new Vector2(-120f, -70f),
        };

        public static readonly ArcaneCircuitSpawnZone[] SpawnZones = BuildSpawnZones();
        public static readonly ArcaneCircuitSegment[] SpawnCorridors = BuildSpawnCorridors();
        public static readonly ArcaneCircuitSegment[] AllCorridors = BuildAllCorridors();

        public static bool TryGetHubRadius(Vector2 position, out float radius)
        {
            const float epsilon = 0.01f;

            foreach (var hub in LargeHubs)
            {
                if ((position - hub).sqrMagnitude <= epsilon)
                {
                    radius = LargeHubRadius;
                    return true;
                }
            }

            foreach (var hub in SmallHubs)
            {
                if ((position - hub).sqrMagnitude <= epsilon)
                {
                    radius = SmallHubRadius;
                    return true;
                }
            }

            radius = 0f;
            return false;
        }

        private static ArcaneCircuitSpawnZone[] BuildSpawnZones()
        {
            var zones = new ArcaneCircuitSpawnZone[SpawnAnchors.Length];
            for (int i = 0; i < SpawnAnchors.Length; i++)
            {
                Vector2 anchor = SpawnAnchors[i];
                Vector2 dir = anchor.normalized;
                Vector2 center = anchor + dir * SpawnOffset;
                zones[i] = new ArcaneCircuitSpawnZone(i, anchor, center);
            }
            return zones;
        }

        private static ArcaneCircuitSegment[] BuildSpawnCorridors()
        {
            var corridors = new ArcaneCircuitSegment[SpawnZones.Length];
            for (int i = 0; i < SpawnZones.Length; i++)
            {
                var zone = SpawnZones[i];
                corridors[i] = new ArcaneCircuitSegment(zone.Anchor, zone.Center);
            }
            return corridors;
        }

        private static ArcaneCircuitSegment[] BuildAllCorridors()
        {
            var corridors = new ArcaneCircuitSegment[CoreCorridors.Length + SpawnCorridors.Length];
            for (int i = 0; i < CoreCorridors.Length; i++)
            {
                corridors[i] = CoreCorridors[i];
            }

            for (int i = 0; i < SpawnCorridors.Length; i++)
            {
                corridors[CoreCorridors.Length + i] = SpawnCorridors[i];
            }

            return corridors;
        }
    }
}

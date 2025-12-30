using UnityEngine;

namespace RuneRelic.Game
{
    public static class ArcaneCircuitMapLogic
    {
        public static bool IsInsideMap(Vector3 position, float radius)
        {
            return IsInsideMap(ToVec2(position), radius, -1, false);
        }

        public static bool IsInsideMap(Vector3 position, float radius, int spawnZoneId, bool spawnZoneActive)
        {
            return IsInsideMap(ToVec2(position), radius, spawnZoneId, spawnZoneActive);
        }

        public static bool IsInsideMap(Vector2 position, float radius, int spawnZoneId, bool spawnZoneActive)
        {
            if (IsInsideAnyHub(position, radius) || IsInsideAnyCorridor(position, radius, 0f))
            {
                return true;
            }

            if (spawnZoneActive && spawnZoneId >= 0)
            {
                if (IsInsideSpawnZone(position, radius, spawnZoneId))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsInsidePlayableArea(Vector2 position, float radius)
        {
            if (IsInsideAnyHub(position, radius) || IsInsideAnyCorridor(position, radius, 0f))
            {
                return true;
            }

            return IsInsideAnySpawnZone(position, radius);
        }

        public static bool IsInsideSpawnZone(Vector3 position, float radius, int spawnZoneId)
        {
            return IsInsideSpawnZone(ToVec2(position), radius, spawnZoneId);
        }

        public static bool IsInsideSpawnZone(Vector2 position, float radius, int spawnZoneId)
        {
            if (spawnZoneId < 0 || spawnZoneId >= ArcaneCircuitMapData.SpawnZones.Length)
            {
                return false;
            }

            var zone = ArcaneCircuitMapData.SpawnZones[spawnZoneId];
            if (IsInsideCircle(position, zone.Center, ArcaneCircuitMapData.SpawnRadius, radius))
            {
                return true;
            }

            return IsInsideCorridor(position, radius, zone.Anchor, zone.Center, 0f);
        }

        public static bool TryGetSpawnZoneId(Vector3 position, float radius, out int spawnZoneId)
        {
            return TryGetSpawnZoneId(ToVec2(position), radius, out spawnZoneId);
        }

        public static bool TryGetSpawnZoneId(Vector2 position, float radius, out int spawnZoneId)
        {
            for (int i = 0; i < ArcaneCircuitMapData.SpawnZones.Length; i++)
            {
                if (IsInsideSpawnZone(position, radius, i))
                {
                    spawnZoneId = i;
                    return true;
                }
            }

            spawnZoneId = -1;
            return false;
        }

        public static bool IsInsideAnyCorridor(Vector2 position, float radius, float extraMargin)
        {
            foreach (var corridor in ArcaneCircuitMapData.AllCorridors)
            {
                if (IsInsideCorridor(position, radius, corridor.Start, corridor.End, extraMargin))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInsideAnySpawnZone(Vector2 position, float radius)
        {
            for (int i = 0; i < ArcaneCircuitMapData.SpawnZones.Length; i++)
            {
                if (IsInsideSpawnZone(position, radius, i))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInsideAnyHub(Vector2 position, float radius)
        {
            foreach (var hub in ArcaneCircuitMapData.LargeHubs)
            {
                if (IsInsideCircle(position, hub, ArcaneCircuitMapData.LargeHubRadius, radius))
                {
                    return true;
                }
            }

            foreach (var hub in ArcaneCircuitMapData.SmallHubs)
            {
                if (IsInsideCircle(position, hub, ArcaneCircuitMapData.SmallHubRadius, radius))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInsideCircle(Vector2 position, Vector2 center, float circleRadius, float radius)
        {
            if (circleRadius <= radius)
            {
                return false;
            }

            float allowed = circleRadius - radius;
            float allowedSq = allowed * allowed;
            return (position - center).sqrMagnitude <= allowedSq;
        }

        private static bool IsInsideCorridor(
            Vector2 position,
            float radius,
            Vector2 start,
            Vector2 end,
            float extraMargin)
        {
            float halfWidth = (ArcaneCircuitMapData.CorridorWidth * 0.5f) + extraMargin;
            if (halfWidth <= radius)
            {
                return false;
            }

            float allowed = halfWidth - radius;
            float allowedSq = allowed * allowed;
            float distSq = DistanceSquaredToSegment(position, start, end);

            return distSq <= allowedSq;
        }

        private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 ab = end - start;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq <= 0.0001f)
            {
                return (point - start).sqrMagnitude;
            }

            float t = Vector2.Dot(point - start, ab) / abLenSq;
            t = Mathf.Clamp01(t);
            Vector2 closest = start + ab * t;
            return (point - closest).sqrMagnitude;
        }

        private static Vector2 ToVec2(Vector3 position)
        {
            return new Vector2(position.x, position.z);
        }
    }
}

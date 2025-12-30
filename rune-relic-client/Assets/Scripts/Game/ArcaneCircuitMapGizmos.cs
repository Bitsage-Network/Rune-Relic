using UnityEngine;

namespace RuneRelic.Game
{
    /// <summary>
    /// Editor gizmo helper for Arcane Circuit layout visualization.
    /// </summary>
    public class ArcaneCircuitMapGizmos : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private int circleSegments = 48;

        [Header("Colors")]
        [SerializeField] private Color largeHubColor = new Color(0.2f, 0.8f, 0.9f, 0.6f);
        [SerializeField] private Color smallHubColor = new Color(0.6f, 0.4f, 0.9f, 0.6f);
        [SerializeField] private Color corridorColor = new Color(0.2f, 0.9f, 0.4f, 0.6f);
        [SerializeField] private Color spawnColor = new Color(1f, 0.8f, 0.2f, 0.6f);

        private void OnDrawGizmos()
        {
            DrawHubs(ArcaneCircuitMapData.LargeHubs, ArcaneCircuitMapData.LargeHubRadius, largeHubColor);
            DrawHubs(ArcaneCircuitMapData.SmallHubs, ArcaneCircuitMapData.SmallHubRadius, smallHubColor);
            DrawCorridors(ArcaneCircuitMapData.AllCorridors, corridorColor);
            DrawSpawnZones();
        }

        private void DrawHubs(Vector2[] hubs, float radius, Color color)
        {
            Gizmos.color = color;
            foreach (var hub in hubs)
            {
                DrawCircle(ToWorld(hub), radius);
            }
        }

        private void DrawCorridors(ArcaneCircuitSegment[] corridors, Color color)
        {
            Gizmos.color = color;
            foreach (var segment in corridors)
            {
                Vector3 start = ToWorld(segment.Start);
                Vector3 end = ToWorld(segment.End);
                Vector3 dir = (end - start).normalized;
                Vector3 perp = new Vector3(-dir.z, 0f, dir.x) * (ArcaneCircuitMapData.CorridorWidth * 0.5f);

                Gizmos.DrawLine(start + perp, end + perp);
                Gizmos.DrawLine(start - perp, end - perp);
            }
        }

        private void DrawSpawnZones()
        {
            Gizmos.color = spawnColor;
            foreach (var zone in ArcaneCircuitMapData.SpawnZones)
            {
                Gizmos.DrawLine(ToWorld(zone.Anchor), ToWorld(zone.Center));
                DrawCircle(ToWorld(zone.Center), ArcaneCircuitMapData.SpawnRadius);
            }
        }

        private void DrawCircle(Vector3 center, float radius)
        {
            if (circleSegments < 3)
            {
                return;
            }

            float step = 360f / circleSegments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= circleSegments; i++)
            {
                float angle = step * i * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private static Vector3 ToWorld(Vector2 point)
        {
            return new Vector3(point.x, 0f, point.y);
        }
    }
}

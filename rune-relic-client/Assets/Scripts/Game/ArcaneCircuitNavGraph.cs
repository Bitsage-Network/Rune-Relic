using System.Collections.Generic;
using UnityEngine;

namespace RuneRelic.Game
{
    /// <summary>
    /// Lightweight navigation graph for bots and heuristics.
    /// </summary>
    public sealed class ArcaneCircuitNavGraph
    {
        public sealed class Node
        {
            public int Id { get; }
            public Vector2 Position { get; }
            public List<int> Neighbors { get; }

            public Node(int id, Vector2 position)
            {
                Id = id;
                Position = position;
                Neighbors = new List<int>();
            }
        }

        private readonly List<Node> _nodes = new List<Node>();
        private readonly Dictionary<Vector2, int> _nodeLookup = new Dictionary<Vector2, int>();

        public IReadOnlyList<Node> Nodes => _nodes;

        public static ArcaneCircuitNavGraph BuildDefault()
        {
            var graph = new ArcaneCircuitNavGraph();
            graph.BuildFromMap();
            return graph;
        }

        private void BuildFromMap()
        {
            foreach (var segment in ArcaneCircuitMapData.AllCorridors)
            {
                int startId = GetOrCreateNode(segment.Start);
                int endId = GetOrCreateNode(segment.End);
                AddEdge(startId, endId);
            }
        }

        private int GetOrCreateNode(Vector2 position)
        {
            if (_nodeLookup.TryGetValue(position, out int id))
            {
                return id;
            }

            id = _nodes.Count;
            _nodeLookup[position] = id;
            _nodes.Add(new Node(id, position));
            return id;
        }

        private void AddEdge(int a, int b)
        {
            if (a == b)
            {
                return;
            }

            var nodeA = _nodes[a];
            var nodeB = _nodes[b];

            if (!nodeA.Neighbors.Contains(b))
            {
                nodeA.Neighbors.Add(b);
            }

            if (!nodeB.Neighbors.Contains(a))
            {
                nodeB.Neighbors.Add(a);
            }
        }

        public int FindClosestNode(Vector2 position)
        {
            if (_nodes.Count == 0)
            {
                return -1;
            }

            float bestDistance = float.MaxValue;
            int bestId = -1;

            for (int i = 0; i < _nodes.Count; i++)
            {
                float dist = (position - _nodes[i].Position).sqrMagnitude;
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestId = i;
                }
            }

            return bestId;
        }

        public bool TryFindPath(Vector2 start, Vector2 goal, List<Vector2> path)
        {
            if (path == null)
            {
                return false;
            }

            path.Clear();

            int startId = FindClosestNode(start);
            int goalId = FindClosestNode(goal);
            if (startId < 0 || goalId < 0)
            {
                return false;
            }

            return TryFindPath(startId, goalId, path);
        }

        public bool TryFindPath(int startId, int goalId, List<Vector2> path)
        {
            if (path == null)
            {
                return false;
            }

            path.Clear();

            if (startId == goalId)
            {
                path.Add(_nodes[startId].Position);
                return true;
            }

            var openSet = new List<int> { startId };
            var openLookup = new HashSet<int> { startId };
            var cameFrom = new Dictionary<int, int>();
            var gScore = new Dictionary<int, float> { [startId] = 0f };
            var fScore = new Dictionary<int, float> { [startId] = Heuristic(startId, goalId) };

            while (openSet.Count > 0)
            {
                int current = GetLowestScore(openSet, fScore);
                if (current == goalId)
                {
                    ReconstructPath(current, cameFrom, path);
                    return true;
                }

                openSet.Remove(current);
                openLookup.Remove(current);

                var node = _nodes[current];
                foreach (int neighbor in node.Neighbors)
                {
                    float tentative = gScore[current] + Vector2.Distance(node.Position, _nodes[neighbor].Position);
                    if (!gScore.TryGetValue(neighbor, out float existing) || tentative < existing)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentative;
                        fScore[neighbor] = tentative + Heuristic(neighbor, goalId);

                        if (!openLookup.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                            openLookup.Add(neighbor);
                        }
                    }
                }
            }

            return false;
        }

        private float Heuristic(int nodeId, int goalId)
        {
            return Vector2.Distance(_nodes[nodeId].Position, _nodes[goalId].Position);
        }

        private int GetLowestScore(List<int> openSet, Dictionary<int, float> scores)
        {
            int bestId = openSet[0];
            float bestScore = scores.TryGetValue(bestId, out float score) ? score : float.MaxValue;

            for (int i = 1; i < openSet.Count; i++)
            {
                int id = openSet[i];
                float candidate = scores.TryGetValue(id, out float value) ? value : float.MaxValue;
                if (candidate < bestScore)
                {
                    bestScore = candidate;
                    bestId = id;
                }
            }

            return bestId;
        }

        private void ReconstructPath(int current, Dictionary<int, int> cameFrom, List<Vector2> path)
        {
            var reversed = new List<Vector2>();
            reversed.Add(_nodes[current].Position);

            while (cameFrom.TryGetValue(current, out int prev))
            {
                current = prev;
                reversed.Add(_nodes[current].Position);
            }

            for (int i = reversed.Count - 1; i >= 0; i--)
            {
                path.Add(reversed[i]);
            }
        }
    }
}

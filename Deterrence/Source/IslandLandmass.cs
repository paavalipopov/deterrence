using System;
using System.Collections.Generic;

namespace Deterrence
{
    internal static class IslandLandmass
    {
        // The base game's Island feature uses the same upper size limit.
        internal const int MaxWorldTiles = 350;
        internal const int DefaultMaxWorldTiles = 180;

        internal static HashSet<T> FindEnclosed<T>(T start, Func<T, bool> isWater, Func<T, bool> isWorldEdge,
            Action<T, List<T>> getNeighbors)
        {
            if (isWater(start)) return null;
            var visited = new HashSet<T> { start };
            var pending = new Queue<T>();
            var neighbors = new List<T>(6);
            bool touchesWater = false;
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                T tile = pending.Dequeue();
                if (isWorldEdge(tile)) return null;
                neighbors.Clear();
                getNeighbors(tile, neighbors);
                foreach (T neighbor in neighbors)
                {
                    if (isWater(neighbor))
                    {
                        touchesWater = true;
                        continue;
                    }
                    if (!visited.Add(neighbor)) continue;
                    if (visited.Count > MaxWorldTiles) return null;
                    pending.Enqueue(neighbor);
                }
            }
            return touchesWater ? visited : null;
        }
    }
}

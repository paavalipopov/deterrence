using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld.Planet;
using Verse;

namespace Deterrence
{
    internal static class IslandGeography
    {
        // World terrain is fixed during ordinary play. Keep results separate by world.
        private static readonly ConditionalWeakTable<WorldGrid, Dictionary<PlanetTile, HashSet<PlanetTile>>> Results =
            new ConditionalWeakTable<WorldGrid, Dictionary<PlanetTile, HashSet<PlanetTile>>>();

        internal static double FactorFor(Map map, DeterrenceSettings settings)
            => !settings.IslandWealthScaling || settings.IslandMultiplier == 1 ? 1
                : SourceMath.IslandFactor(Qualifies(map, settings, out _, out _), settings.IslandMultiplier);

        internal static bool Qualifies(Map map, DeterrenceSettings settings, out int size, out bool hasOtherSettlement)
        {
            var land = GetIsland(map);
            size = land?.Count ?? 0;
            hasOtherSettlement = false;
            if (land == null || size > settings.IslandMaxTiles) return false;
            if (settings.IslandRequiresNoOtherSettlements)
            {
                foreach (var settlement in Find.WorldObjects.Settlements)
                {
                    if (settlement == map.Parent || !land.Contains(settlement.Tile)) continue;
                    hasOtherSettlement = true;
                    return false;
                }
            }
            return true;
        }

        private static HashSet<PlanetTile> GetIsland(Map map)
        {
            if (map == null || map.IsPocketMap || Find.World == null) return null;
            var grid = Find.WorldGrid;
            var tile = map.Tile;
            if (grid == null || !tile.Valid || !grid.InBounds(tile) || !(grid[tile] is SurfaceTile)) return null;
            var cached = Results.GetValue(grid, _ => new Dictionary<PlanetTile, HashSet<PlanetTile>>());
            if (cached.TryGetValue(tile, out var island)) return island;
            island = IslandLandmass.FindEnclosed(tile, neighbor =>
            {
                var terrain = grid[neighbor];
                return terrain.WaterCovered || terrain.PrimaryBiome?.isWaterBiome == true;
            }, grid.IsOnEdge, grid.GetTileNeighbors);
            if (island == null) cached[tile] = null;
            else
                foreach (var member in island) cached[member] = island;
            return island;
        }
    }
}

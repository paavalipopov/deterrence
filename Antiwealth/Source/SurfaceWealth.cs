using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace Antiwealth
{
    public static class SurfaceWealth
    {
        private static HashSet<TerrainDef> floors;
        private static HashSet<ThingDef> walls;
        private static readonly FieldInfo cachedValues = typeof(WealthWatcher).GetField("cachedTerrainMarketValue", BindingFlags.NonPublic | BindingFlags.Static);

        public static double Count(Map map)
        {
            if (floors == null)
            {
                floors = new HashSet<TerrainDef>(DefDatabase<TerrainDef>.AllDefsListForReading
                    .Where(t => t.smoothedTerrain != null).Select(t => t.smoothedTerrain));
                walls = new HashSet<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(t => t.building?.smoothedThing != null).Select(t => t.building.smoothedThing));
                foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                    if (def.building?.unsmoothedThing != null) walls.Add(def);
            }
            var values = cachedValues?.GetValue(null) as float[];
            double value = 0;
            // Match vanilla: known top terrain, plus foundations; only player-owned buildings.
            for (int index = 0; index < map.cellIndices.NumGridCells; index++)
            {
                if (!map.fogGrid.IsFogged(index)) value += FloorValue(map.terrainGrid.TopTerrainAt(index), values);
                value += FloorValue(map.terrainGrid.FoundationAt(index), values);
            }
            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                if (thing.Faction == Faction.OfPlayer && walls.Contains(thing.def))
                    value += CreditMath.Positive(thing.GetStatValue(StatDefOf.MarketValueIgnoreHp));
            // Buildings and floor wealth contribute at half value to the threat-wealth input.
            value *= 0.5;
            if (Find.World != null)
                foreach (var pocket in Find.World.pocketMaps)
                    if (pocket.HasMap && pocket.sourceMap == map) value += Count(pocket.Map);
            return value;
        }

        private static double FloorValue(TerrainDef terrain, float[] values)
        {
            if (terrain == null || !floors.Contains(terrain)) return 0;
            return CreditMath.Positive(values != null && terrain.index < values.Length
                ? values[terrain.index] : terrain.GetStatValueAbstract(StatDefOf.MarketValue));
        }
    }
}

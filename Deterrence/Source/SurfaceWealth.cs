using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace Deterrence
{
    public static class SurfaceWealth
    {
        // Smoothed-floor value by terrain index, built once. Other terrain is 0.
        private static float[] floorValues;
        private static HashSet<ThingDef> walls;
        private static readonly FieldInfo cachedValues = typeof(WealthWatcher).GetField("cachedTerrainMarketValue", BindingFlags.NonPublic | BindingFlags.Static);

        public static double Count(Map map)
        {
            if (floorValues == null) BuildTables();
            var top = map.terrainGrid.topGrid;
            var foundations = map.terrainGrid.foundationGrid;
            double value = 0;
            // Match vanilla: known top terrain, plus foundations; only player-owned buildings.
            for (int index = 0; index < top.Length; index++)
            {
                var terrain = top[index];
                if (terrain != null && floorValues[terrain.index] > 0 && !map.fogGrid.IsFogged(index))
                    value += floorValues[terrain.index];
                var foundation = foundations?[index];
                if (foundation != null) value += floorValues[foundation.index];
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

        private static void BuildTables()
        {
            var values = cachedValues?.GetValue(null) as float[];
            var terrains = DefDatabase<TerrainDef>.AllDefsListForReading;
            var table = new float[terrains.Count == 0 ? 0 : terrains.Max(t => t.index) + 1];
            foreach (var floor in terrains.Where(t => t.smoothedTerrain != null).Select(t => t.smoothedTerrain).Distinct())
                table[floor.index] = (float)CreditMath.Positive(values != null && floor.index < values.Length
                    ? values[floor.index] : floor.GetStatValueAbstract(StatDefOf.MarketValue));
            walls = new HashSet<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading
                .Where(t => t.building?.smoothedThing != null).Select(t => t.building.smoothedThing));
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                if (def.building?.unsmoothedThing != null) walls.Add(def);
            floorValues = table;
        }
    }
}

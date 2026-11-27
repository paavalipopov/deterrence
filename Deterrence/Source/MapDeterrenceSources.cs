using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Deterrence
{
    // One map sample is shared by Global and every faction ledger observing the map.
    internal sealed class MapDeterrenceSources
    {
        internal readonly Map Map;
        internal readonly int Population;
        private List<Thing> corpses;
        private List<Thing> organs;
        private List<GameCondition> conditions;
        private int? hiveCount;
        private readonly IEnumerable<Thing> trackedOrgans;
        private static List<ThingDef> hiveDefs;

        // Hive definitions, including modded subclasses, found once after defs load.
        private static List<ThingDef> HiveDefs => hiveDefs ?? (hiveDefs = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(def => def.thingClass != null && typeof(Hive).IsAssignableFrom(def.thingClass)).ToList());

        internal MapDeterrenceSources(Map map, IEnumerable<Thing> trackedOrgans)
        {
            Map = map;
            Population = map.mapPawns.FreeColonistsCount;
            this.trackedOrgans = trackedOrgans;
        }

        internal List<Thing> Corpses => corpses ?? (corpses = Collect(ThingRequestGroup.Corpse));
        internal List<Thing> Organs => organs ?? (organs = trackedOrgans
            .Where(thing => DeterrenceGameComponent.IsKeptOrgan(thing, Map)).OrderBy(thing => thing.thingIDNumber).ToList());

        internal List<GameCondition> Conditions
        {
            get
            {
                if (conditions != null) return conditions;
                conditions = new List<GameCondition>();
                Map.GameConditionManager.GetAllGameConditionsAffectingMap(Map, conditions);
                return conditions;
            }
        }

        internal int HiveCount
        {
            get
            {
                if (!hiveCount.HasValue)
                    hiveCount = HiveDefs.Sum(def => Map.listerThings.ThingsOfDef(def).Count(thing => thing.Faction != null
                        && thing.Faction.HostileTo(Faction.OfPlayer)));
                return hiveCount.Value;
            }
        }

        private List<Thing> Collect(ThingRequestGroup group)
        {
            var result = new List<Thing>();
            ThingOwnerUtility.GetAllThingsRecursively(Map, ThingRequest.ForGroup(group), result, false, holder => true, true);
            return result;
        }
    }
}

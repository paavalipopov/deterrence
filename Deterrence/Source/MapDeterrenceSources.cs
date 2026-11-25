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

        internal MapDeterrenceSources(Map map)
        {
            Map = map;
            Population = map.mapPawns.FreeColonistsCount;
        }

        internal List<Thing> Corpses => corpses ?? (corpses = Collect(ThingRequestGroup.Corpse));
        internal List<Thing> Organs => organs ?? (organs = Collect(ThingRequestGroup.HaulableEver)
            .Where(thing => DeterrenceGameComponent.IsKeptOrgan(thing, Map)).ToList());

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
                    hiveCount = Map.listerThings.AllThings.Count(thing => thing is Hive && thing.Faction != null
                        && thing.Faction.HostileTo(Faction.OfPlayer));
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

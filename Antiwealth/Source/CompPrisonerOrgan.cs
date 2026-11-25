using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Antiwealth
{
    // A saved provenance flag, with no ticking or global stack patches.
    public class CompPrisonerOrgan : ThingComp
    {
        public bool HarvestedLocallyFromPrisoner;

        public override void PostExposeData()
            => Scribe_Values.Look(ref HarvestedLocallyFromPrisoner, "antiwealthPrisonerHarvest", false);

        public override bool AllowStackWith(Thing other)
            => HarvestedLocallyFromPrisoner == (other.TryGetComp<CompPrisonerOrgan>()?.HarvestedLocallyFromPrisoner ?? false);

        public override void PostSplitOff(Thing piece)
        {
            var provenance = piece.TryGetComp<CompPrisonerOrgan>();
            if (provenance != null) provenance.HarvestedLocallyFromPrisoner = HarvestedLocallyFromPrisoner;
        }

        public override string CompInspectStringExtra()
            => HarvestedLocallyFromPrisoner ? "Prisoner organ: harvested locally" : null;
    }

    [StaticConstructorOnStartup]
    public static class OrganTrackingSetup
    {
        static OrganTrackingSetup()
            => InstallOn(DefDatabase<BodyPartDef>.AllDefsListForReading.Select(p => p.spawnThingOnRemoved));

        public static void InstallOn(IEnumerable<ThingDef> definitions)
        {
            foreach (var def in definitions.Where(d => d != null).Distinct())
            {
                if (def.thingClass == null || !typeof(ThingWithComps).IsAssignableFrom(def.thingClass)) continue;
                if (def.comps == null) def.comps = new List<CompProperties>();
                if (!def.comps.Any(c => c.compClass == typeof(CompPrisonerOrgan)))
                    def.comps.Add(new CompProperties(typeof(CompPrisonerOrgan)));
            }
        }
    }
}

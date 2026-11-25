using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Antiwealth
{
    public class RitualReputation : RitualAttachableOutcomeEffectWorker
    {
        public override void Apply(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual,
            RitualOutcomePossibility outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
        {
            extraOutcomeDesc = null;
            var map = jobRitual?.Map;
            if (map == null || !map.IsPlayerHome || Current.Game == null) return;
            if (outcome?.Positive != true) return;
            double amount = Current.Game.GetComponent<AntiwealthGameComponent>().RegisterRitual(map, jobRitual.RitualLabel);
            if (amount > 0)
                extraOutcomeDesc = "Reputation gained " + amount.ToString("N0") + " deterrence.";
        }
    }
}

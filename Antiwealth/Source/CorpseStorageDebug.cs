using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace Antiwealth
{
    internal static class CorpseStorageDebug
    {
        [DebugAction("Deterrence", "Inspect corpse storage rules", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ChooseCorpse()
        {
            DebugTools.curTool = new DebugTool("Select a humanlike corpse", () =>
            {
                var map = Find.CurrentMap;
                var corpse = UI.MouseCell().GetThingList(map).OfType<Corpse>().FirstOrDefault(c => c.InnerPawn?.RaceProps.Humanlike == true);
                if (corpse == null) return;
                DebugTools.curTool = new DebugTool("Select destination", () =>
                {
                    if (Find.CurrentMap != map) return;
                    var cell = UI.MouseCell();
                    var destination = cell.GetThingList(map).OfType<IHaulDestination>().FirstOrDefault()
                        ?? map.zoneManager.ZoneAt(cell) as IHaulDestination;
                    var settings = (destination as IStoreSettingsParent)?.GetStoreSettings();
                    if (settings == null) return;
                    var record = BurialRecord.ForCorpse(corpse.InnerPawn);
                    var relation = BurialRegistry.Current.Relation(record.FactionId);
                    var rules = CorpseStorageRules.For(settings);
                    var grave = destination as Building_Grave;
                    string reason = grave?.AssignedPawn != null ? "grave assigned to " + grave.AssignedPawn.LabelShort
                        : rules.Status != -1 && (rules.Status & (int)record.Status) == 0 ? CorpseFilterMenu.Titles[0]
                        : rules.Diplomacy != -1 && (!relation.HasValue || (rules.Diplomacy & (int)relation.Value) == 0) ? CorpseFilterMenu.Titles[1]
                        : rules.Factions != null && !rules.Factions.ContainsKey(record.FactionId) ? CorpseFilterMenu.Titles[2] : "custom filters allow";
                    Log.Message("[Deterrence] " + corpse.Label + " → " + destination + ": faction #" + record.FactionId
                        + ", diplomacy " + (relation == BurialStatus.Friendly ? "Allied" : relation?.ToString() ?? "none")
                        + ", status " + record.Status + (record.Inferred ? " (inferred)" : " (recorded)")
                        + ", " + reason + ", destination accepts = " + destination.Accepts(corpse));
                    DebugTools.curTool = null;
                });
            });
        }
    }
}

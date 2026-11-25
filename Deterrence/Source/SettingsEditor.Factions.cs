using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deterrence
{
    public partial class SettingsEditor
    {
        private string factionKey;
        private DeterrenceSettings inheritedSettings;

        private Rect DrawFactionScope(Rect rect, DeterrenceSettings global)
        {
            var faction = factionKey == null ? null : DefDatabase<FactionDef>.GetNamedSilentFail(factionKey);
            var profile = factionKey == null ? null : global.OverrideFor(factionKey);
            float resetWidth = factionKey == null ? 0 : Math.Min(174, rect.width * 0.35f);
            float pickerWidth = rect.width - resetWidth;
            var picker = new Rect(rect.x, rect.y, pickerWidth, 30);
            string title = factionKey == null ? "Global" : faction?.LabelCap.ToString() ?? factionKey;
            if (Widgets.ButtonText(picker, ("Settings for " + title).Truncate(pickerWidth - 16)))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("Global", () => SelectFaction(null)) };
                foreach (var def in RaidFactionTypes(global))
                {
                    string name = def.defName;
                    string label = def.LabelCap.ToString();
                    if (global.OverrideFor(name) != null) label += " (custom)";
                    options.Add(new FloatMenuOption(label, () => SelectFaction(name)));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(picker,
                "Global is the default. A faction's edits apply to its ordinary enemy raids. Reset to Global removes all its overrides, including overrides on other tabs. Use Apply & save to refresh the colony.");
            if (factionKey != null && profile != null && Widgets.ButtonText(new Rect(rect.x + pickerWidth + 5, rect.y, resetWidth - 5, 30), "Reset to Global"))
            {
                global.ResetFaction(factionKey);
                SelectFaction(factionKey);
                profile = null;
            }
            if (factionKey != null)
            {
                string status = profile == null ? "Inherits Global. Edit any control to override it for this faction."
                    : profile.Count + " overridden settings. Other values follow Global.";
                Widgets.Label(new Rect(rect.x, rect.y + 31, rect.width, 26), status);
            }
            float height = factionKey == null ? 38 : 62;
            return new Rect(rect.x, rect.y + height, rect.width, Math.Max(0, rect.height - height));
        }

        private static IEnumerable<FactionDef> RaidFactionTypes(DeterrenceSettings global)
            => DefDatabase<FactionDef>.AllDefsListForReading.Where(def => !def.isPlayer && !def.raidsForbidden
                && (def.pawnGroupMakers?.Any(maker => maker.kindDef == PawnGroupKindDefOf.Combat) == true
                    || global.OverrideFor(def.defName) != null))
                .OrderBy(def => def.LabelCap.ToString()).ThenBy(def => def.defName);

        private void SelectFaction(string key)
        {
            factionKey = key;
            buffers.Clear();
            formulaScroll = curvesScroll = compareScroll = miscScroll = listScroll = Vector2.zero;
            SelectSource(null);
        }

        private void SaveFactionEdits(DeterrenceSettings global, FactionOverride profile,
            Dictionary<string, float> before, DeterrenceSettings effective)
        {
            var after = FactionOverride.Capture(effective);
            if (after.All(pair => before.TryGetValue(pair.Key, out float value) && value == pair.Value)) return;
            bool isNew = profile == null;
            if (isNew) profile = new FactionOverride { FactionKey = factionKey };
            if (!profile.StoreEdits(FactionOverride.Capture(global), before, after)) return;
            if (profile.Count == 0) global.ResetFaction(factionKey);
            else if (isNew) global.FactionOverrides.Add(profile);
        }

        private void ClearFactionRuleEdits(string ruleKey, bool includeSwitch, bool includeScaling = false)
        {
            var profile = inheritedSettings?.OverrideFor(factionKey);
            if (profile == null) return;
            profile.ClearRuleFields(ruleKey, includeSwitch, includeScaling);
            if (profile.Count == 0) inheritedSettings.ResetFaction(factionKey);
        }
    }
}

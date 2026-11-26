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
            string title = factionKey == null ? "Global" : faction == null ? factionKey : FactionTypeLabel(faction);
            if (Widgets.ButtonText(picker, ("Settings for " + title).Truncate(pickerWidth - 16)))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("Global", () => SelectFaction(null)) };
                foreach (var entry in RaidFactionTypes(global).Select(def => new { Def = def, Label = FactionTypeLabel(def) })
                    .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase))
                {
                    string name = entry.Def.defName;
                    string label = entry.Label;
                    if (global.OverrideFor(name) != null) label += " (custom)";
                    options.Add(new FloatMenuOption(label, () => SelectFaction(name)));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(picker,
                title + "\nGlobal is the default. Settings apply to ordinary enemy raids from every faction of the selected type. Reset to Global clears that type's overrides on all tabs. Use Apply & save to refresh the colony.");
            if (factionKey != null && profile != null && Widgets.ButtonText(new Rect(rect.x + pickerWidth + 5, rect.y, resetWidth - 5, 30), "Reset to Global"))
            {
                global.ResetFaction(factionKey);
                SelectFaction(factionKey);
                profile = null;
            }
            if (factionKey != null)
            {
                string status = profile == null ? "Inherits Global. Edit any control to override it for this faction type."
                    : profile.Count + " overridden settings. Other values follow Global.";
                Widgets.Label(new Rect(rect.x, rect.y + 31, rect.width, 26), status);
            }
            float height = factionKey == null ? 38 : 62;
            return new Rect(rect.x, rect.y + height, rect.width, Math.Max(0, rect.height - height));
        }

        private static IEnumerable<FactionDef> RaidFactionTypes(DeterrenceSettings global)
            => DefDatabase<FactionDef>.AllDefsListForReading.Where(def => !def.isPlayer && !def.raidsForbidden
                && (def.pawnGroupMakers?.Any(maker => maker.kindDef == PawnGroupKindDefOf.Combat) == true
                    || global.OverrideFor(def.defName) != null));

        private static string FactionTypeLabel(FactionDef def)
        {
            string type = def.LabelCap.ToString();
            var factions = Current.Game?.World?.factionManager?.AllFactionsListForReading;
            if (factions == null) return type;
            string names = string.Join(", ", factions.Where(f => f.def == def && !f.Hidden && !f.defeated)
                .Select(f => f.Name).Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase));
            return FormatFactionLabel(names, type);
        }

        internal static string FormatFactionLabel(string name, string type)
            => string.IsNullOrWhiteSpace(name) || string.Equals(name, type, StringComparison.CurrentCultureIgnoreCase)
                ? type : name + " (" + type + ")";

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

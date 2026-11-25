using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deterrence
{
    internal static class CorpseFilterMenu
    {
        internal static readonly string[] Titles = { "Allowed status at death", "Allowed faction status", "Allowed factions" };
        private static readonly string[] Tips = {
            "The pawn’s individual role when they died. Older bodies use a saved estimate from their faction.",
            "The faction’s current relationship to your colony. Your own faction and factionless pawns pass Any.",
            "The faction the pawn belonged to at death. Any also allows newly generated factions."
        };
        private sealed class Foldouts { public int Open; public Foldouts() { } }
        private static readonly ConditionalWeakTable<StorageSettings, Foldouts> foldouts = new ConditionalWeakTable<StorageSettings, Foldouts>();
        private static readonly AccessTools.FieldRef<ThingFilter, Action> callback =
            AccessTools.FieldRefAccess<ThingFilter, Action>("settingsChangedCallback");
        private static readonly Action<Listing_Lines> endLine = AccessTools.MethodDelegate<Action<Listing_Lines>>(
            AccessTools.Method(typeof(Listing_Lines), "EndLine"));
        private static readonly Func<Listing_TreeThingFilter, TreeNode_ThingCategory, bool> visible =
            AccessTools.MethodDelegate<Func<Listing_TreeThingFilter, TreeNode_ThingCategory, bool>>(
                AccessTools.Method(typeof(Listing_TreeThingFilter), "Visible", new[] { typeof(TreeNode_ThingCategory) }));

        private static StorageSettings Owner(ThingFilter filter)
        {
            var settings = callback(filter)?.Target as StorageSettings;
            return settings?.owner != null && settings.filter == filter ? settings : null;
        }

        private static List<KeyValuePair<int, string>> Options(int branch, CorpseStorageRules rules)
        {
            if (branch < 2) return new List<KeyValuePair<int, string>> {
                new KeyValuePair<int, string>(1, "Hostile"), new KeyValuePair<int, string>(2, "Neutral"),
                new KeyValuePair<int, string>(4, branch == 0 ? "Friendly" : "Allied")
            };
            var names = new Dictionary<int, string>(BurialRegistry.Current.KnownNames);
            if (rules.Factions != null)
                foreach (var pair in rules.Factions) names[pair.Key] = pair.Value;
            foreach (var faction in Find.FactionManager.AllFactionsListForReading)
                if (!faction.Hidden || faction.IsPlayer || names.ContainsKey(faction.loadID))
                    names[faction.loadID] = faction.Name ?? faction.def.LabelCap.ToString();
            names.Remove(BurialRecord.Factionless);
            var options = names.GroupBy(p => p.Value ?? "Unknown faction", StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
                .SelectMany(group => group.OrderBy(p => p.Key).Select(p => new KeyValuePair<int,string>(
                    p.Key, group.Count() > 1 ? group.Key + " (#" + p.Key + ")" : group.Key))).ToList();
            options.Add(new KeyValuePair<int, string>(BurialRecord.Factionless, "Factionless"));
            return options;
        }
        private static Rect Row(Listing_TreeThingFilter tree, int indent)
        {
            float x = indent * tree.nestIndentWidth;
            var rect = new Rect(x, tree.CurHeight, tree.ColumnWidth - x, tree.lineHeight);
            endLine(tree);
            return rect;
        }
        private static string Summary(CorpseStorageRules rules, int branch)
        {
            if (branch == 2)
            {
                var ids = rules.Factions;
                return ids == null ? "Any" : ids.Count == 0 ? "None" : ids.Count == 1 ? ids.Values.First() : ids.Count + " selected";
            }
            int mask = branch == 0 ? rules.Status : rules.Diplomacy;
            switch (mask)
            {
                case -1: return "Any";
                case 0: return "None";
                case 1: return "Hostile";
                case 2: return "Neutral";
                case 4: return branch == 0 ? "Friendly" : "Allied";
                case 7: return "3 selected";
                default: return "2 selected";
            }
        }

        [HarmonyPatch(typeof(Listing_TreeThingFilter), "ThisOrDescendantsVisibleAndMatchesSearch")]
        private static class Search
        {
            private static void Postfix(Listing_TreeThingFilter __instance, TreeNode_ThingCategory node,
                ThingFilter ___filter, QuickSearchFilter ___searchFilter, ref bool __result)
            {
                if (__result || node.catDef != ThingCategoryDefOf.CorpsesHumanlike || ___searchFilter?.Active != true) return;
                var store = Owner(___filter);
                if (store == null || BurialRegistry.Current == null || !visible(__instance, node)) return;
                var rules = CorpseStorageRules.For(store);
                for (int i = 0; i < Titles.Length; i++)
                    if (___searchFilter.Matches(Titles[i]) || Options(i, rules).Any(p => ___searchFilter.Matches(p.Value)))
                    { __result = true; return; }
            }
        }
        [HarmonyPatch(typeof(Listing_TreeThingFilter), "DoCategoryChildren")]
        private static class Children
        {
            private static void Postfix(Listing_TreeThingFilter __instance, TreeNode_ThingCategory node, int indentLevel,
                bool subtreeMatchedSearch, ThingFilter ___filter, QuickSearchFilter ___searchFilter, ref int ___matchCount)
            {
                if (node.catDef != ThingCategoryDefOf.CorpsesHumanlike) return;
                var store = Owner(___filter);
                if (store == null || BurialRegistry.Current == null) return;
                var rules = CorpseStorageRules.For(store);
                var state = foldouts.GetOrCreateValue(store);
                bool search = ___searchFilter?.Active == true && !subtreeMatchedSearch;
                for (int i = 0; i < Titles.Length; i++)
                {
                    bool titleMatch = search && ___searchFilter.Matches(Titles[i]);
                    bool open = (state.Open & (1 << i)) != 0;
                    var choices = open || search ? Options(i, rules) : null;
                    if (search && !titleMatch && !choices.Any(p => ___searchFilter.Matches(p.Value))) continue;
                    if (search) ___matchCount++;
                    Rect row = Row(__instance, indentLevel);
                    var arrow = new Rect(row.x, row.center.y - 7, 14, 14);
                    GUI.DrawTexture(arrow, open || search ? TexButton.Collapse : TexButton.Reveal);
                    string label = Titles[i] + " · " + Summary(rules, i);
                    Widgets.Label(new Rect(row.x + 18, row.y, row.width - 18, row.height), label.Truncate(row.width - 18));
                    TooltipHandler.TipRegion(row, label + "\n" + Tips[i]);
                    if (Widgets.ButtonInvisible(row)) state.Open ^= 1 << i;
                    if (!open && !search) continue;
                    Rect actions = Row(__instance, indentLevel + 1);
                    float width = Mathf.Min(70, (actions.width - 4) / 2);
                    bool any = Widgets.ButtonText(new Rect(actions.x, actions.y, width, actions.height), "Any");
                    bool none = Widgets.ButtonText(new Rect(actions.x + width + 4, actions.y, width, actions.height), "None");
                    if (any || none)
                    {
                        if (i == 2) rules.Factions = any ? null : new Dictionary<int, string>();
                        else if (i == 0) rules.Status = any ? -1 : 0;
                        else rules.Diplomacy = any ? -1 : 0;
                        store.owner.Notify_SettingsChanged();
                    }
                    foreach (var option in choices)
                    {
                        if (search && !titleMatch && !___searchFilter.Matches(option.Value)) continue;
                        int mask = i == 0 ? rules.Status : rules.Diplomacy;
                        bool allowed = i == 2 ? rules.Factions == null || rules.Factions.ContainsKey(option.Key)
                            : (mask & option.Key) != 0;
                        bool next = allowed;
                        Rect choice = Row(__instance, indentLevel + 1);
                        Widgets.CheckboxLabeled(choice, option.Value.Truncate(Mathf.Max(1, choice.width - 26)), ref next);
                        TooltipHandler.TipRegion(choice, option.Value);
                        if (next == allowed) continue;
                        if (i == 2)
                        {
                            if (rules.Factions == null) rules.Factions = choices.ToDictionary(p => p.Key, p => p.Value);
                            if (next) rules.Factions[option.Key] = option.Value;
                            else rules.Factions.Remove(option.Key);
                        }
                        else
                        {
                            mask = next ? mask | option.Key : (mask & 7) & ~option.Key;
                            if (i == 0) rules.Status = mask;
                            else rules.Diplomacy = mask;
                        }
                        store.owner.Notify_SettingsChanged();
                    }
                }
            }
        }
    }
}

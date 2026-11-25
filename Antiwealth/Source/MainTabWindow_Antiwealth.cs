using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Antiwealth
{
    public class MainTabWindow_Antiwealth : MainTabWindow
    {
        private Vector2 scroll;
        private bool showHistory;
        private readonly WealthHistoryView historyView = new WealthHistoryView();
        private readonly MeterPie meterPie = new MeterPie();
        private readonly HashSet<string> expanded = new HashSet<string>();
        private List<MeterNode> nodes = new List<MeterNode>();
        private List<MeterRow> visibleRows = new List<MeterRow>();
        private bool rowsDirty = true;
        private int cachedMap = -1, cachedRevision = -1;
        private AntiwealthGameComponent cachedLedger;
        public override Vector2 InitialSize => new Vector2(900, 650);

        public MainTabWindow_Antiwealth() { doCloseX = true; }

        public override void PostClose()
        {
            meterPie.Release();
            base.PostClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Map map = Find.CurrentMap;
            var ledger = Current.Game?.GetComponent<AntiwealthGameComponent>();
            if (map == null || ledger == null) { Widgets.Label(inRect, "Select a colony map."); return; }
            Text.Font = GameFont.Medium;
            var name = AntiwealthMod.Settings.FactionOverridesEnabled ? "Deterrence (Global)" : "Deterrence";
            Widgets.Label(new Rect(0, 0, inRect.width - 290, 32), (name + " - " + map.Parent.LabelCap).Truncate(inRect.width - 290));
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(inRect.width - 280, 0, 115, 30), "Meters")) showHistory = false;
            if (Widgets.ButtonText(new Rect(inRect.width - 160, 0, 115, 30), "History")) showHistory = true;
            if (showHistory) { historyView.Draw(new Rect(0, 42, inRect.width, inRect.height - 42), map, ledger); return; }

            float vanilla = map.wealthWatcher.WealthItems + 0.5f * map.wealthWatcher.WealthBuildings + map.wealthWatcher.WealthPawns;
            bool applies = map.IsPlayerHome && !map.IsPocketMap && !Find.Storyteller.difficulty.fixedWealthMode;
            double surfaceDiscount = applies ? ledger.SurfaceDiscountFor(map) : 0;
            var settings = AntiwealthMod.Settings;
            bool pieBesideHeader = inRect.width >= 780;
            const float pieColumn = 252;
            var header = HelpUI.SingleColumn(new Listing_Standard());
            header.Begin(new Rect(0, 40, inRect.width - (pieBesideHeader ? pieColumn + 14 : 0), inRect.height - 40));
            HelpUI.ThreatEquation(header, settings);
            header.Label("Base threat: " + Money(CreditMath.EffectiveWealth(vanilla, surfaceDiscount))
                + "     Deterrence: " + Money(ledger.CreditFor(map))
                + "     Threat: " + Money(map.PlayerWealthForStoryteller));
            string notice = Find.Storyteller.difficulty.fixedWealthMode ? "Wealth-independent mode: deterrence is tracked only."
                : !map.IsPlayerHome || map.IsPocketMap ? "Adjustments apply on home maps."
                : !AntiwealthMod.Settings.Enabled ? "Deterrence subtraction is off. Terrain and Misc settings still apply."
                : "Population and other threat factors still apply.";
            double factor = SourceMath.BiomeMultiplier(map.Biome.settlementSelectionWeight,
                settings.BiomeExponent, settings.BiomeWealthScaling && applies);
            double island = applies ? IslandGeography.FactorFor(map, settings) : 1;
            Rect factors = header.GetRect(2 * Text.LineHeight);
            float slot = Mathf.Min(160, factors.width / 2);
            HelpUI.MeterFactor(new Rect(factors.x, factors.y, slot, factors.height), "FactorBiome", "Biome factor", factor);
            HelpUI.MeterFactor(new Rect(factors.x + slot, factors.y, slot, factors.height), "FactorIsland", "Island factor", island);
            if (surfaceDiscount > 0)
            {
                string discount = "Surface wealth discount: " + Money(surfaceDiscount);
                float remaining = factors.width - 2 * slot;
                if (Text.CalcSize(discount).x + 2 <= remaining)
                    Widgets.Label(new Rect(factors.x + 2 * slot, factors.center.y - Text.LineHeight / 2, remaining, Text.LineHeight), discount);
                else header.Label(discount);
            }
            header.Label(notice);
            float tableTop = 40 + header.CurHeight + 4;
            header.End();

            if (cachedLedger != ledger || cachedMap != map.uniqueID || cachedRevision != ledger.LedgerRevision)
            {
                cachedLedger = ledger;
                cachedMap = map.uniqueID; cachedRevision = ledger.LedgerRevision;
                nodes = MeterTree.Build(MeterGrouping.Build(ledger.Entries, map.uniqueID), ledger.OrganStockFor(map.uniqueID),
                    AntiwealthGameComponent.Now, e => e is LedgerEntry entry ? entry.Shares : Enumerable.Empty<ShareValues>());
                meterPie.Update(nodes);
                rowsDirty = true;
            }
            if (pieBesideHeader)
                meterPie.Draw(new Rect(inRect.width - pieColumn, 40, pieColumn, tableTop - 44));
            else
            {
                meterPie.Draw(new Rect(0, tableTop, Mathf.Min(pieColumn, inRect.width), 78));
                tableTop += 84;
            }
            if (rowsDirty) { visibleRows = MeterTree.Visible(nodes, expanded); rowsDirty = false; }
            var rows = visibleRows;
            float width = inRect.width - 20;
            DrawRow(new Rect(0, tableTop, width, 28), "Meter", "State", "Deterrence", "Change", 0);
            Widgets.DrawLineHorizontal(0, tableTop + 29, width);
            Rect outer = new Rect(0, tableTop + 36, inRect.width, Math.Max(0, inRect.height - tableTop - 78));
            Rect view = new Rect(0, 0, width, Math.Max(outer.height, rows.Count * 40 + 8));
            Widgets.BeginScrollView(outer, ref scroll, view);
            int firstVisible = Math.Max(0, (int)(scroll.y/40) - 1);
            int lastVisible = Math.Min(rows.Count, (int)Math.Ceiling((scroll.y + outer.height)/40) + 1);
            for (int i = firstVisible; i < lastVisible; i++)
            {
                var visible = rows[i]; var node = visible.Node;
                Rect row = new Rect(0, i * 40, width, 38);
                if (visible.Depth == 0)
                {
                    Widgets.DrawLightHighlight(row);
                    Widgets.DrawBoxSolid(new Rect(row.x, row.y + 3, 3, row.height - 6), MeterPie.ColorFor(node.Key));
                }
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                float indent = visible.Depth * 20;
                if (node.Children.Count > 0 && Widgets.ButtonText(new Rect(row.x + indent + 3, row.y + 5, 24, 27),
                    expanded.Contains(node.Key) ? "-" : "+")) Toggle(node.Key);
                var target = node.TargetId >= 0 ? ledger.TargetFor(node.TargetId) : null;
                if (target != null && !target.Destroyed && target.MapHeld == map && target.SpawnedOrAnyParentSpawned)
                {
                    var visibleTarget = ThingOwnerUtility.GetFirstSpawnedParentThing(target) ?? target;
                    Rect jump = new Rect(row.xMax - 27, row.y + 6, 24, 24);
                    TooltipHandler.TipRegion(jump, "Show on map");
                    if (Widgets.ButtonImage(jump, TexButton.NextBig))
                    {
                        CameraJumper.TryJumpAndSelect(visibleTarget);
                        Find.MainTabsRoot.SetCurrentTab(null);
                    }
                    if (Mouse.IsOver(jump) && Event.current.type == EventType.Repaint)
                        GenDraw.DrawArrowPointingAt(visibleTarget.DrawPos);
                }
                DrawRow(row, node.Label, node.State, Money(node.Credit), node.Change, indent + 31);
                Rect labelHit = new Rect(row.x + indent + 30, row.y, Math.Max(0, row.width * 0.45f - indent - 30), row.height);
                TooltipHandler.TipRegion(new Rect(row.x, row.y, row.width - 30, row.height), node.Label + "\n" + node.Tip);
                if (node.Children.Count > 0 && Widgets.ButtonInvisible(labelHit)) Toggle(node.Key);
            }
            Widgets.EndScrollView();
            Widgets.Label(new Rect(0, inRect.height - 29, inRect.width - 130, 28), "Expand a meter for its contributors. Updates hourly.");
            var branches = MeterTree.All(nodes).Where(n => n.Children.Count > 0).ToList();
            if (branches.Count > 0)
            {
                bool all = branches.All(n => expanded.Contains(n.Key));
                if (Widgets.ButtonText(new Rect(inRect.width - 120, inRect.height - 32, 120, 28), all ? "Collapse all" : "Expand all"))
                {
                    if (all) expanded.Clear();
                    else foreach (var branch in branches) expanded.Add(branch.Key);
                    rowsDirty = true;
                }
            }
        }

        private void Toggle(string key) { if (!expanded.Add(key)) expanded.Remove(key); rowsDirty = true; }

        private static void DrawRow(Rect rect, string label, string state, string credit, string change, float indent)
        {
            float first = rect.width * 0.45f, second = rect.width * 0.15f, third = rect.width * 0.17f;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + indent + 4, rect.y, Math.Max(0, first - indent - 8), rect.height),
                (label ?? "").Truncate(Math.Max(0, first - indent - 8)));
            Widgets.Label(new Rect(rect.x + first, rect.y, second - 4, rect.height), state ?? "");
            Widgets.Label(new Rect(rect.x + first + second, rect.y, third - 4, rect.height), credit ?? "");
            Widgets.Label(new Rect(rect.x + first + second + third, rect.y, rect.width - first - second - third - 34, rect.height), change ?? "");
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static string Money(double value) => CreditMath.Positive(value).ToString("N0");
    }
}

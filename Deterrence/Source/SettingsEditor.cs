using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace Deterrence
{
    public partial class SettingsEditor
    {
        private sealed class Source
        {
            public EventRule Rule;
            public string Name;
            public string Unit;
            public bool Installed = true;
            public string Group, Requirement;
            public int Order;
            public bool Extra;
        }

        private int tab;
        private string search = "";
        private string selected;
        private bool showOtherConditions, showLabInputs;
        private bool showPopulation, showPreview;
        private Vector2 listScroll, detailScroll, formulaScroll, curvesScroll, compareScroll, miscScroll;
        private float detailHeight = 850, generalHeight = 1000, curvesHeight = 800, miscHeight = 500;
        private float plottedBiomeExponent = float.NaN;
        private float plottedIslandFactor = float.NaN;
        private List<SimpleCurveDrawInfo> terrainCurves;
        private const int DefaultExampleColonists = 5;
        private int previewPopulation = DefaultExampleColonists;
        private bool showColonyControls;
        private int ExampleColonists => showColonyControls ? previewPopulation : DefaultExampleColonists;
        private float labDays = 6, labHiveStart = 3, labHiveEnd = 11, labBump = 2000, labInterval = 3;
        private int labCount = 3;
        private string labSignature;
        private List<SimpleCurveDrawInfo> labCurves;
        private float previewDays = 10;
        private float previewPower = 100;
        private float previewGear = 1500;
        private float previewOrganValue = 1000;
        private float previewExposure = 1;
        private List<SimpleCurveDrawInfo> previewCurves;
        private string previewSignature;
        private float previewEnd;
        private readonly Dictionary<string, string> buffers = new Dictionary<string, string>();
        private static readonly string[] tabs = Enum.GetNames(typeof(SettingsSection));

        public void Draw(Rect rect, DeterrenceMod owner, DeterrenceSettings settings)
        {
            settings.DiscoverRules();
            showColonyControls = settings.ShowAdvancedColonyScaling;
            float tabWidth = (rect.width - 126) / 4;
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect button = new Rect(rect.x + (i % 4) * tabWidth, rect.y + (i / 4) * 36, tabWidth - 4, 32);
                if (Widgets.ButtonText(button, tabs[i]))
                {
                    if (i == (int)SettingsSection.Presets && tab != i) presetsNeedRefresh = true;
                    tab = i; SelectSource(null); search = ""; listScroll = Vector2.zero;
                }
                if (i == tab) Widgets.DrawHighlightSelected(button);
            }
            if (Widgets.ButtonText(new Rect(rect.xMax - 120, rect.y, 120, 68), "Apply & save")) owner.WriteSettings();
            Rect content = new Rect(rect.x, rect.y + 82, rect.width, rect.height - 82);
            if (tab == (int)SettingsSection.Presets)
            {
                DrawPresets(content, owner, settings);
                return;
            }
            if (settings.FactionOverridesEnabled) content = DrawFactionScope(content, settings);
            else if (factionKey != null) SelectFaction(null);

            var overrides = factionKey == null ? null : settings.OverrideFor(factionKey);
            var effective = factionKey == null ? settings : overrides?.Resolve(settings) ?? settings.CopyForFaction();
            inheritedSettings = factionKey == null ? null : settings;
            var before = factionKey == null ? null : FactionOverride.Capture(effective);
            if (tab == (int)SettingsSection.Formula) DrawFormula(content, effective, settings);
            else if (tab == (int)SettingsSection.Curves) DrawCurves(content, effective);
            else if (tab == (int)SettingsSection.Summary) DrawSummary(content, effective);
            else if (tab == (int)SettingsSection.Misc) DrawMisc(content, effective);
            else DrawSources(content, effective);
            effective.Sanitize();
            if (before != null) SaveFactionEdits(settings, overrides, before, effective);
        }

        private void DrawFormula(Rect rect, DeterrenceSettings settings, DeterrenceSettings global)
        {
            Rect view = new Rect(0, 0, rect.width - 20, Math.Max(rect.height, generalHeight));
            Widgets.BeginScrollView(rect, ref formulaScroll, view);
            var listing = HelpUI.SingleColumn(new Listing_Standard()); listing.Begin(view);
            HelpUI.ThreatEquation(listing, settings, centered: true);
            HelpUI.Checkbox(listing, "Enable deterrence", ref settings.Enabled,
                "Subtracts Remains, Reputation, and Hazards from threat wealth. The meters keep tracking in either mode.", true);
            listing.Gap(6);
            HelpUI.Checkbox(listing, "Enable biome factor", ref settings.BiomeWealthScaling,
                "Multiplies threat wealth by settlement weight raised to Biome strength.", true);
            if (settings.BiomeWealthScaling)
            {
                HelpUI.Equation(listing, "TerrainBiome", "Biome factor = settlement weight raised to biome strength",
                    "w is the biome settlement weight from 0 to 1. s is Biome strength. At zero strength every biome has a factor of 1.");
                var biome = Find.CurrentMap?.Biome;
                string weight = "w is the biome’s settlement weight. Higher weights make world generation more likely to choose this biome for settlements. The formula limits w to the range 0 to 1.";
                if (biome != null) weight += " Here (" + biome.LabelCap + "), w = "
                    + Math.Min(1, CreditMath.Positive(biome.settlementSelectionWeight, 1)).ToString("0.###") + ".";
                listing.Label(weight);
                GlobalSlider(listing, "Biome strength", ref settings.BiomeExponent, 0, SourceMath.MaxBiomeExponent, 0.05f, symbol: "s");
            }
            listing.Gap(6);
            HelpUI.Checkbox(listing, "Enable island factor", ref settings.IslandWealthScaling,
                "Multiplies threat wealth on qualifying water-enclosed landmasses.", true);
            if (settings.IslandWealthScaling)
            {
                HelpUI.Equation(listing, "TerrainIsland", "Island factor = m on a qualifying island, 1 elsewhere",
                    "m is the Island multiplier. The tile limit and settlement rule below determine which islands qualify.");
                GlobalSlider(listing, "Island multiplier", ref settings.IslandMultiplier, 0, 1, 0.05f, symbol: "m");
                float maxTiles = settings.IslandMaxTiles;
                GlobalSlider(listing, "Maximum island tiles", ref maxTiles, 1, IslandLandmass.MaxWorldTiles, 1);
                settings.IslandMaxTiles = Mathf.RoundToInt(maxTiles);
                HelpUI.Checkbox(listing, "Require sole settlement", ref settings.IslandRequiresNoOtherSettlements,
                    "The colony must be the only settlement on the island. Settlements from all factions count.");
            }
            if (settings.BiomeWealthScaling || settings.IslandWealthScaling) DrawTerrainPreview(listing, settings);
            if (factionKey == null)
            {
                listing.Gap(6);
                HelpUI.Checkbox(listing, "Apply to quests", ref global.AdjustQuests,
                    "Quests and quest sites take threat points from the combined threat wealth of your colonies. Their rewards scale with those points. When on, the formula above also lowers quest threats and rewards. When off, quests use the game's own threat wealth.", true);
            }
            listing.GapLine();
            SectionTitle(listing, "Settings guide");
            listing.Label("Remains: bodies and locally harvested prisoner organs kept at the colony.");
            listing.Label("Reputation: one-time gains from hostile deaths, prisoner treatment, and ritual rewards.");
            listing.Label("Hazards: pollution, regional devices, toxic weather, hives, and outdoor extremes.");
            listing.Label("Curves: change growth and fading.");
            listing.Label("Summary: compare contributions by source and review the current formula.");
            listing.Label("Misc: separate game-wealth adjustments, including smoothed surfaces.");
            listing.Label("Presets: save or restore a named set of settings.");
            listing.GapLine();
            SectionTitle(listing, "Options");
            HelpUI.Checkbox(listing, "Colony size scaling", ref global.ShowAdvancedColonyScaling,
                "Shows controls for colony size scaling, sample colonist inputs, and extra Summary columns. Existing scaling applies with the controls hidden.");
            if (factionKey == null)
                HelpUI.Checkbox(listing, "Faction overrides", ref global.FactionOverridesEnabled,
                    "Shows a faction selector on settings tabs. Overrides apply after the game chooses a faction for an ordinary enemy raid.");
            if (Current.Game != null && listing.ButtonText("Open meters")) OpenMeters();
            generalHeight = listing.CurHeight + 20; listing.End(); Widgets.EndScrollView();
        }

        private void DrawTerrainPreview(Listing_Standard listing, DeterrenceSettings settings)
        {
            var map = Find.CurrentMap;
            if (map != null && map.IsPlayerHome)
            {
                var biome = map.Biome;
                double weight = Math.Max(0, Math.Min(1, biome.settlementSelectionWeight));
                double biomeFactor = SourceMath.BiomeMultiplier(weight, settings.BiomeExponent, settings.BiomeWealthScaling);
                listing.Label("Biome factor (" + biome.LabelCap + "): " + biomeFactor.ToString("0.###"));
                if (settings.IslandWealthScaling)
                {
                    bool qualifies = IslandGeography.Qualifies(map, settings, out int tiles, out bool occupied);
                    string reason = qualifies ? tiles + " island tiles"
                        : occupied ? "another settlement shares the island"
                        : tiles > 0 ? tiles + " tiles, above the configured limit" : "outside a qualifying island";
                    listing.Label("Island factor: " + SourceMath.IslandFactor(qualifies, settings.IslandMultiplier)
                        .ToString("0.###") + " (" + reason + ")");
                }
            }
            float exponent = settings.BiomeWealthScaling ? settings.BiomeExponent : 0;
            float island = settings.IslandWealthScaling ? settings.IslandMultiplier : 1;
            if (terrainCurves == null || plottedBiomeExponent != exponent || plottedIslandFactor != island)
            {
                plottedBiomeExponent = exponent;
                plottedIslandFactor = island;
                terrainCurves = new List<SimpleCurveDrawInfo> { DeterrenceGraphs.Series("Biome factor",
                    DeterrenceGraphs.AdjustedColor, Enumerable.Range(0,101).Select(i => new CurvePoint(i,
                        (float)(100 * SourceMath.BiomeMultiplier(i/100.0, exponent, true))))),
                    DeterrenceGraphs.Series("Island factor (qualifying island)", DeterrenceGraphs.CreditColor,
                        new[] { new CurvePoint(0, 100 * island), new CurvePoint(100, 100 * island) }) };
                foreach (var series in terrainCurves) series.valueFormat = "{0:0.#}%";
            }
            listing.Label("Terrain factors (%)");
            DeterrenceGraphs.Draw(listing.GetRect(210), terrainCurves, 0, 100, "Biome settlement weight (%)", 100);
        }

        private void DrawMisc(Rect rect, DeterrenceSettings settings)
        {
            Rect view = new Rect(0, 0, rect.width - 20, Math.Max(rect.height, miscHeight));
            Widgets.BeginScrollView(rect, ref miscScroll, view);
            var listing = HelpUI.SingleColumn(new Listing_Standard()); listing.Begin(view);
            SectionTitle(listing, "Base threat");
            HelpUI.Equation(listing, "Surface", "Surface contribution = counted value × multiplier",
                "Counted value is the contribution of smoothed floors and walls to the game's base threat wealth.");
            GlobalSlider(listing, "Smoothed surface wealth", ref settings.SmoothedSurfaceMultiplier, 0, 1, 0.05f);
            listing.Label("Apply & save refreshes the map estimate.");
            if (Find.CurrentMap != null && Current.Game != null)
            {
                double discount = Current.Game.GetComponent<DeterrenceGameComponent>()
                    .SurfaceDiscountFor(Find.CurrentMap, settings.SmoothedSurfaceMultiplier);
                listing.Label("Current map surface discount: " + F(discount) + " (last refresh).");
            }
            miscHeight = listing.CurHeight + 20; listing.End(); Widgets.EndScrollView();
        }

        private static void OpenMeters()
        {
            // Close the settings dialogs so the map and its locator arrows are accessible.
            Find.WindowStack.TryRemove(typeof(RimWorld.Dialog_ModSettings), true);
            Find.WindowStack.TryRemove(typeof(RimWorld.Dialog_Options), false);
            Find.MainTabsRoot.SetCurrentTab(DefDatabase<RimWorld.MainButtonDef>.GetNamed("Deterrence"));
        }

        private string Buffer(string key, string value) { if (!buffers.TryGetValue(key, out string result)) { buffers[key] = value; return value; } return result; }
        private static string F(double value) => value.ToString(value > 1000000 ? "0.##E+0" : value < 100 ? "0.##" : "N0");
        private static void DrawPreviewRow(Rect rect, string[] values)
        {
            float column = rect.width / values.Length;
            for (int i = 0; i < values.Length; i++) Widgets.Label(new Rect(rect.x + i * column, rect.y, column - 4, rect.height), values[i]);
        }
        private static void DrawComparisonRow(Rect rect, string name, string[] values)
        {
            float left = rect.width * (values.Length == 1 ? 0.65f : 0.38f);
            string[] lines = name.Split('\n');
            Widgets.Label(new Rect(rect.x + 4, rect.y, left - 8, rect.height),
                string.Join("\n", lines.Select(line => line.Truncate(left - 8))));
            TooltipHandler.TipRegion(new Rect(rect.x, rect.y, left, rect.height), name);
            DrawPreviewRow(new Rect(rect.x + left, rect.y, rect.width - left, rect.height), values);
        }
        private static bool Foldout(Listing_Standard listing, string label, ref bool open)
        {
            if (listing.ButtonText((open ? "- " : "+ ") + label)) open = !open;
            return open;
        }

    }
}

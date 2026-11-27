using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Deterrence
{
    public class DeterrenceSettings : ModSettings
    {
        // Retain the saved key; this now controls deterrence alone, not the other factors.
        public bool Enabled = true;
        public bool Corpses = true;
        public bool Conditions = true;
        public bool Reputation = true;
        // Quests and quest sites read world threat wealth; this decides whether the formula applies there.
        public bool AdjustQuests = true;
        // Arrival frequency and raider retreat. Both are global and off until enabled.
        public bool ArrivalFrequency;
        public float RaidSkipStrength = 0.5f;
        public float VisitorSkipStrength = 0.5f;
        public float SkipExponent = 2;
        public bool RaiderRetreat;
        public float RetreatStrength = 0.5f;
        public float OwnDeadWeight = 1;
        public float RecoveryExponent = 0.5f;
        public bool FadeByHistory;
        public float FadeReferenceArea = 20000;
        private int schemaVersion = 6;
        public bool IncludeHumanGear = true;
        public float HumanGearFraction = 1;
        public float HeatThreshold = 40;
        public float ColdThreshold = -10;
        public bool BiomeWealthScaling = true;
        public float SmoothedSurfaceMultiplier;
        public float BiomeExponent = 0.5f;
        private const float DefaultIslandMultiplier = 0.2f;
        public float IslandMultiplier = DefaultIslandMultiplier;
        public bool IslandWealthScaling = true;
        public int IslandMaxTiles = IslandLandmass.DefaultMaxWorldTiles;
        public bool IslandRequiresNoOtherSettlements = true;
        public bool ShowAdvancedColonyScaling;
        public bool GrowthByRate;
        public float GrowthShape = 1;
        public float SlowdownDays = 10;
        internal HazardGrowth Growth => new HazardGrowth(GrowthByRate, SlowdownScale, SlowdownDays, GrowthShape);
        // Remains are a stock, so they always use the exposure-scale form of the hazard curve.
        internal HazardGrowth RemainsGrowth => new HazardGrowth(false, SlowdownScale, SlowdownDays, GrowthShape);
        internal double FadeDuration(double balance, double area, bool hazard)
            => CreditMath.Duration(hazard && FadeByHistory ? area : balance, RecoveryDays,
                hazard && FadeByHistory ? FadeReferenceArea : RecoveryReference, RecoveryExponent);
        public float SlowdownScale = 20000;
        public float RecoveryDays = 10;
        public float RecoveryReference = 10000;
        public float CorpsePerCombatPower = 5;
        public float FalloutPerDay = 4000;
        private const float DefaultTemperatureRate = 1000;
        public float TemperaturePerDay = DefaultTemperatureRate;
        public List<EventRule> Rules = new List<EventRule>();
        private Dictionary<string, EventRule> ruleIndex;
        public bool FactionOverridesEnabled;
        public List<FactionOverride> FactionOverrides = new List<FactionOverride>();
        public List<SettingsPreset> Presets = new List<SettingsPreset>();
        public string ActivePresetFile;

        internal DeterrenceSettings CopyForFaction()
        {
            var copy = (DeterrenceSettings)MemberwiseClone();
            copy.Rules = Rules.Select(rule => new EventRule(rule.Key, rule.BaseAmount, rule.Enabled)
            {
                PopulationExponent = rule.PopulationExponent,
                ReferencePopulation = rule.ReferencePopulation
            }).ToList();
            copy.ruleIndex = null;
            copy.FactionOverrides = new List<FactionOverride>();
            copy.FactionOverridesEnabled = false;
            copy.Presets = new List<SettingsPreset>();
            copy.ActivePresetFile = null;
            return copy;
        }

        internal DeterrenceSettings CopyForPreset()
        {
            var copy = CopyForFaction();
            copy.FactionOverridesEnabled = FactionOverridesEnabled;
            copy.FactionOverrides = FactionOverrides.Select(profile => profile.Copy()).ToList();
            return copy;
        }

        internal void LoadPreset(DeterrenceSettings snapshot)
        {
            var copy = snapshot.CopyForPreset();
            FactionOverride.CopyGlobalFields(copy, this);
            CorpsePerCombatPower = copy.CorpsePerCombatPower;
            FalloutPerDay = copy.FalloutPerDay;
            TemperaturePerDay = copy.TemperaturePerDay;
            ShowAdvancedColonyScaling = copy.ShowAdvancedColonyScaling;
            AdjustQuests = copy.AdjustQuests;
            ArrivalFrequency = copy.ArrivalFrequency;
            RaidSkipStrength = copy.RaidSkipStrength;
            VisitorSkipStrength = copy.VisitorSkipStrength;
            SkipExponent = copy.SkipExponent;
            RaiderRetreat = copy.RaiderRetreat;
            RetreatStrength = copy.RetreatStrength;
            OwnDeadWeight = copy.OwnDeadWeight;
            FactionOverridesEnabled = copy.FactionOverridesEnabled;
            FactionOverrides = copy.FactionOverrides;
            Rules = copy.Rules;
            ruleIndex = null;
            Sanitize();
        }

        internal FactionOverride OverrideFor(string factionKey)
            => FactionOverrides.FirstOrDefault(profile => profile.FactionKey == factionKey);

        internal void ResetFaction(string factionKey) => FactionOverrides.RemoveAll(profile => profile.FactionKey == factionKey);

        // Legacy fields above remain readable so 0.1 settings seed the new independent controls.
        public EventRule Rule(string key, float fallback = 0, bool enabled = true)
        {
            if (!RuleGrouping.IsSupported(key)) return new EventRule(key, 0, false);
            if (Rules == null) Rules = new List<EventRule>();
            if (ruleIndex == null)
            {
                ruleIndex = new Dictionary<string, EventRule>();
                Rules.RemoveAll(r => r == null || string.IsNullOrEmpty(r.Key));
                foreach (var rule in Rules.ToArray())
                {
                    if (ruleIndex.ContainsKey(rule.Key)) Rules.Remove(rule);
                    else ruleIndex.Add(rule.Key, rule);
                }
            }
            if (!ruleIndex.TryGetValue(key, out var result))
            {
                result = new EventRule(key, fallback, enabled);
                Rules.Add(result);
                ruleIndex.Add(key, result);
            }
            return result;
        }

        public void EnsureBuiltins()
        {
            Rule("condition:ToxicFallout", FalloutPerDay);
            Rule("condition:PsychicDroner", 1000);
            Rule("condition:ToxicSpewer", 1000);
            Rule("remains:human", CorpsePerCombatPower);
            Rule("remains:mech", CorpsePerCombatPower);
            Rule("remains:animal", 1);
            Rule("organs:local", 0.75f);
            Rule("pollution:ground", 4000);
            Rule("infestation:hives", 2000);
            Rule("climate:heat", DefaultTemperatureRate);
            Rule("climate:cold", DefaultTemperatureRate);
            Rule("bump:human", 5);
            Rule("bump:mech", 5);
            Rule("bump:animal", 3);
            Rule("bump:organ", 1000);
            Rule("bump:failedSurgery", 333);
            Rule("bump:amputation", 500);
            Rule("bump:execution", 2000);
            Rule("bump:ritual", 5000);
            foreach (var key in ruleIndex.Keys.Where(key => !RuleGrouping.IsSupported(key)).ToArray()) ruleIndex.Remove(key);
            Rules.RemoveAll(r => !RuleGrouping.IsSupported(r.Key));
        }

        public void DiscoverRules()
        {
            EnsureBuiltins();
            foreach (var custom in DefDatabase<DeterrenceConditionDef>.AllDefsListForReading)
                if (!string.IsNullOrEmpty(custom.conditionDefName) && RuleGrouping.IsSupported("condition:" + custom.conditionDefName))
                    Rule("condition:" + custom.conditionDefName, custom.initialDailyGain);
            foreach (var condition in DefDatabase<GameConditionDef>.AllDefsListForReading)
            {
                string key = "condition:" + condition.defName;
                if (!RuleGrouping.IsSupported(key)) continue;
                float amount = condition.defName == "ToxicRain" || condition.defName == "AcidRain" ? FalloutPerDay : 0;
                Rule(key, amount, amount > 0);
            }
        }

        public static string RemainsKey(Pawn pawn)
            => pawn.RaceProps.IsMechanoid ? "remains:mech"
                : pawn.RaceProps.Humanlike ? "remains:human" : "remains:animal";

        public float LegacyDefault(string key)
        {
            if (!RuleGrouping.IsSupported(key)) return 0;
            switch (key)
            {
                case "condition:ToxicFallout": case "condition:ToxicRain": case "condition:AcidRain": return FalloutPerDay;
                case "pollution:ground": return 4000;
                case "infestation:hives": return 2000;
                case "climate:heat": case "climate:cold": return DefaultTemperatureRate;
                case "bump:human": case "bump:mech": return 5;
                case "bump:animal": return 3;
                case "condition:PsychicDroner": case "condition:ToxicSpewer": return 1000;
                case "bump:organ": return 1000;
                case "bump:failedSurgery": return 333;
                case "bump:amputation": return 500;
                case "bump:execution": return 2000;
                case "bump:ritual": return 5000;
                case "organs:local": return 0.75f;
                case "remains:animal": return 1;
                default:
                    if (key.StartsWith("remains:", StringComparison.Ordinal)) return CorpsePerCombatPower;
                    var custom = DefDatabase<DeterrenceConditionDef>.AllDefsListForReading
                        .FirstOrDefault(d => "condition:" + d.conditionDefName == key);
                    return custom == null ? 0 : custom.initialDailyGain;
            }
        }

        public override void ExposeData()
        {
            ExposeProfileData();
            Scribe_Collections.Look(ref Presets, "presets", LookMode.Deep);
            Scribe_Values.Look(ref ActivePresetFile, "activePresetFile");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                Presets = (Presets ?? new List<SettingsPreset>())
                    .Where(preset => preset != null && !string.IsNullOrWhiteSpace(preset.Name))
                    .GroupBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First()).ToList();
        }

        internal void ExposeProfileData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref Corpses, "corpses", true);
            Scribe_Values.Look(ref Conditions, "conditions", true);
            Scribe_Values.Look(ref Reputation, "reputation", true);
            Scribe_Values.Look(ref AdjustQuests, "adjustQuests", true);
            Scribe_Values.Look(ref ArrivalFrequency, "arrivalFrequency");
            Scribe_Values.Look(ref RaidSkipStrength, "raidSkipStrength", 0.5f);
            Scribe_Values.Look(ref VisitorSkipStrength, "visitorSkipStrength", 0.5f);
            Scribe_Values.Look(ref SkipExponent, "skipExponent", 2);
            Scribe_Values.Look(ref RaiderRetreat, "raiderRetreat");
            Scribe_Values.Look(ref RetreatStrength, "retreatStrength", 0.5f);
            Scribe_Values.Look(ref OwnDeadWeight, "ownDeadWeight", 1);
            Scribe_Values.Look(ref FadeByHistory, "fadeByHistory");
            Scribe_Values.Look(ref FadeReferenceArea, "fadeReferenceArea", 20000);
            Scribe_Values.Look(ref RecoveryExponent, "recoveryExponent", 0.5f);
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref IncludeHumanGear, "includeHumanGear", true);
            Scribe_Values.Look(ref HumanGearFraction, "humanGearFraction", 1);
            Scribe_Values.Look(ref HeatThreshold, "heatThreshold", 40);
            Scribe_Values.Look(ref ColdThreshold, "coldThreshold", -10);
            Scribe_Values.Look(ref BiomeWealthScaling, "biomeWealthScaling", true);
            Scribe_Values.Look(ref SmoothedSurfaceMultiplier, "smoothedSurfaceMultiplier", 0f);
            Scribe_Values.Look(ref BiomeExponent, "biomeExponent", 0.5f);
            Scribe_Values.Look(ref IslandMultiplier, "islandMultiplier", DefaultIslandMultiplier);
            Scribe_Values.Look(ref IslandWealthScaling, "islandWealthScaling", true);
            Scribe_Values.Look(ref IslandMaxTiles, "islandMaxTiles", IslandLandmass.DefaultMaxWorldTiles);
            Scribe_Values.Look(ref IslandRequiresNoOtherSettlements, "islandRequiresNoOtherSettlements", true);
            Scribe_Values.Look(ref ShowAdvancedColonyScaling, "showAdvancedColonyScaling");
            var legacy = AccumulationCurve.Logarithmic;
            if (Scribe.mode == LoadSaveMode.LoadingVars) Scribe_Values.Look(ref legacy, "curve", AccumulationCurve.Logarithmic);
            Scribe_Values.Look(ref GrowthShape, "growthShape", legacy == AccumulationCurve.SquareRoot ? 0.5f : 1f);
            Scribe_Values.Look(ref GrowthByRate, "growthByRate");
            Scribe_Values.Look(ref SlowdownDays, "slowdownDays", 10);
            Scribe_Values.Look(ref SlowdownScale, "slowdownScale", 20000);
            Scribe_Values.Look(ref RecoveryDays, "recoveryDays", 10);
            Scribe_Values.Look(ref RecoveryReference, "recoveryReference", 10000);
            Scribe_Values.Look(ref CorpsePerCombatPower, "corpsePerCombatPower", 10);
            Scribe_Values.Look(ref FalloutPerDay, "falloutPerDay", 4000);
            Scribe_Values.Look(ref TemperaturePerDay, "temperaturePerDay", DefaultTemperatureRate);
            Scribe_Collections.Look(ref Rules, "eventRules", LookMode.Deep);
            Scribe_Values.Look(ref FactionOverridesEnabled, "factionOverridesEnabled");
            Scribe_Collections.Look(ref FactionOverrides, "factionOverrides", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) ruleIndex = null;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                FactionOverrides = (FactionOverrides ?? new List<FactionOverride>())
                    .Where(profile => profile != null && !string.IsNullOrEmpty(profile.FactionKey))
                    .GroupBy(profile => profile.FactionKey).Select(group => group.Last()).ToList();
                MigrateLegacySettings();
                Sanitize();
            }
        }

        internal void MigrateLegacySettings()
        {
            if (schemaVersion < 5) Rule("remains:animal", CorpsePerCombatPower);
            EnsureBuiltins();
            if (schemaVersion < 5)
            {
                foreach (var kind in new[] { "human", "mech", "animal" })
                {
                    var body = Rule("remains:" + kind, CorpsePerCombatPower);
                    var bump = Rule("bump:" + kind, 5);
                    bump.BaseAmount = body.BaseAmount / 2;
                    bump.PopulationExponent = body.PopulationExponent;
                    bump.ReferencePopulation = body.ReferencePopulation;
                    bump.Enabled = body.Enabled;
                    body.BaseAmount /= 2;
                }
                Rule("climate:heat").Enabled = true;
                Rule("climate:cold").Enabled = true;
                CorpsePerCombatPower /= 2;
                schemaVersion = 5;
            }
            if (schemaVersion < 6)
            {
                // An old disabled master switch meant every wealth adjustment was off.
                if (!Enabled) { BiomeWealthScaling = false; IslandWealthScaling = false; SmoothedSurfaceMultiplier = 1; }
                schemaVersion = 6;
            }
        }

        public void Sanitize()
        {
            SlowdownScale = Mathf.Clamp((float)CreditMath.Positive(SlowdownScale, 20000), 100, 1000000);
            RecoveryDays = Mathf.Clamp((float)CreditMath.Positive(RecoveryDays, 10), 1, 60);
            RecoveryReference = Mathf.Clamp((float)CreditMath.Positive(RecoveryReference, 10000), 100, 1000000);
            CorpsePerCombatPower = ClampRate(CorpsePerCombatPower, 100);
            FalloutPerDay = ClampRate(FalloutPerDay);
            TemperaturePerDay = ClampRate(TemperaturePerDay);
            HumanGearFraction = Mathf.Clamp01((float)CreditMath.Positive(HumanGearFraction, 1));
            HeatThreshold = CreditMath.Finite(HeatThreshold) ? Mathf.Clamp(HeatThreshold, -100, 150) : 40;
            ColdThreshold = CreditMath.Finite(ColdThreshold) ? Mathf.Clamp(ColdThreshold, -150, 100) : -10;
            if (ColdThreshold > HeatThreshold) ColdThreshold = HeatThreshold;
            BiomeExponent = Mathf.Clamp((float)CreditMath.Positive(BiomeExponent, 0.5), 0, SourceMath.MaxBiomeExponent);
            IslandMultiplier = Mathf.Clamp01((float)CreditMath.Positive(IslandMultiplier, DefaultIslandMultiplier));
            IslandMaxTiles = Mathf.Clamp(IslandMaxTiles, 1, IslandLandmass.MaxWorldTiles);
            SmoothedSurfaceMultiplier = Mathf.Clamp01((float)CreditMath.Positive(SmoothedSurfaceMultiplier));
            RecoveryExponent = Mathf.Clamp01((float)CreditMath.Positive(RecoveryExponent, 0.5));
            GrowthShape = Mathf.Clamp01((float)CreditMath.Positive(GrowthShape, 1));
            SlowdownDays = Mathf.Clamp((float)CreditMath.Positive(SlowdownDays, 10), 0.1f, 120);
            FadeReferenceArea = Mathf.Clamp((float)CreditMath.Positive(FadeReferenceArea, 20000), 100, 1000000);
            RaidSkipStrength = Mathf.Clamp01((float)CreditMath.Positive(RaidSkipStrength));
            VisitorSkipStrength = Mathf.Clamp01((float)CreditMath.Positive(VisitorSkipStrength));
            SkipExponent = Mathf.Clamp((float)CreditMath.Positive(SkipExponent, 2), 0.25f, 4);
            RetreatStrength = Mathf.Clamp01((float)CreditMath.Positive(RetreatStrength));
            OwnDeadWeight = Mathf.Clamp((float)CreditMath.Positive(OwnDeadWeight, 1), 1, 5);
            EnsureBuiltins();
            foreach (var rule in Rules)
            {
                rule.SanitizeValues();
                if (rule.Key == "organs:local")
                {
                    rule.BaseAmount = Mathf.Clamp(rule.BaseAmount, 0, 5);
                    rule.PopulationExponent = 0;
                }
            }
        }

        private static float ClampRate(float value, float max = 20000)
            => Mathf.Clamp((float)CreditMath.Positive(value), 0, max);
    }
}

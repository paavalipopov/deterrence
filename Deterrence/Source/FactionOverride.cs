using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace Deterrence
{
    // A saved profile records only edited fields. A rule can override its amount, switch,
    // and colony scaling independently, just like fields on Formula or Curves.
    public sealed class FactionOverride : IExposable
    {
        public string FactionKey;
        public string Id = Guid.NewGuid().ToString("N");
        private Dictionary<string, float> values = new Dictionary<string, float>();
        private List<string> saveKeys;
        private List<float> saveValues;

        private static readonly string[] WealthKeys = {
            nameof(DeterrenceSettings.Enabled), nameof(DeterrenceSettings.BiomeWealthScaling),
            nameof(DeterrenceSettings.BiomeExponent), nameof(DeterrenceSettings.IslandMultiplier),
            nameof(DeterrenceSettings.IslandWealthScaling), nameof(DeterrenceSettings.IslandMaxTiles),
            nameof(DeterrenceSettings.IslandRequiresNoOtherSettlements),
            nameof(DeterrenceSettings.SmoothedSurfaceMultiplier)
        };

        private static readonly Dictionary<string, FieldInfo> Fields = WealthKeys.Concat(new[] {
            nameof(DeterrenceSettings.Corpses), nameof(DeterrenceSettings.Conditions), nameof(DeterrenceSettings.Reputation),
            nameof(DeterrenceSettings.IncludeHumanGear), nameof(DeterrenceSettings.HumanGearFraction),
            nameof(DeterrenceSettings.HeatThreshold), nameof(DeterrenceSettings.ColdThreshold),
            nameof(DeterrenceSettings.GrowthByRate), nameof(DeterrenceSettings.GrowthShape),
            nameof(DeterrenceSettings.SlowdownDays), nameof(DeterrenceSettings.SlowdownScale),
            nameof(DeterrenceSettings.FadeByHistory), nameof(DeterrenceSettings.FadeReferenceArea), nameof(DeterrenceSettings.RecoveryExponent),
            nameof(DeterrenceSettings.RecoveryDays), nameof(DeterrenceSettings.RecoveryReference)
        }).ToDictionary(name => name, name => typeof(DeterrenceSettings).GetField(name));

        public int Count => values.Count;
        internal bool HasLedgerOverrides => values.Keys.Any(key => !WealthKeys.Contains(key));
        internal IReadOnlyDictionary<string, float> Values => values;
        internal void ReplaceValues(Dictionary<string, float> fields) => values = fields;
        internal static Type SettingType(string key) => key == "Curve" ? typeof(AccumulationCurve)
            : Fields.TryGetValue(key, out var field) ? field.FieldType : null;

        internal static void UpgradeCurveFields(Dictionary<string, float> fields)
        {
            if (fields.TryGetValue("Curve", out float curve) && !fields.ContainsKey("GrowthShape"))
                fields["GrowthShape"] = curve == (int)AccumulationCurve.SquareRoot ? 0.5f : 1;
            fields.Remove("Curve");
            fields.Remove("RecoveryCurve");
        }

        internal FactionOverride Copy() => new FactionOverride
        {
            FactionKey = FactionKey,
            Id = Id,
            values = new Dictionary<string, float>(values)
        };

        internal static void CopyGlobalFields(DeterrenceSettings source, DeterrenceSettings target)
        {
            foreach (var field in Fields.Values) field.SetValue(target, field.GetValue(source));
        }

        internal void ClearRuleFields(string ruleKey, bool includeSwitch, bool includeScaling)
        {
            values.Remove("amount/" + ruleKey);
            if (includeScaling)
            {
                values.Remove("exponent/" + ruleKey);
                values.Remove("population/" + ruleKey);
            }
            if (includeSwitch) values.Remove("enabled/" + ruleKey);
        }

        internal static Dictionary<string, float> Capture(DeterrenceSettings settings)
        {
            var result = Fields.ToDictionary(pair => pair.Key, pair => Convert.ToSingle(pair.Value.GetValue(settings)));
            foreach (var rule in settings.Rules)
            {
                result["enabled/" + rule.Key] = rule.Enabled ? 1 : 0;
                result["amount/" + rule.Key] = rule.BaseAmount;
                result["exponent/" + rule.Key] = rule.PopulationExponent;
                result["population/" + rule.Key] = rule.ReferencePopulation;
            }
            return result;
        }

        internal DeterrenceSettings Resolve(DeterrenceSettings global)
        {
            var result = global.CopyForFaction();
            ApplyValues(result, values);
            return result;
        }

        internal static void ApplyValues(DeterrenceSettings result, IReadOnlyDictionary<string, float> fields)
        {
            foreach (var pair in fields)
            {
                if (Fields.TryGetValue(pair.Key, out var field))
                {
                    object value = field.FieldType == typeof(bool) ? (object)(pair.Value != 0)
                        : field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, (int)pair.Value)
                        : field.FieldType == typeof(int) ? (object)(int)pair.Value
                        : pair.Value;
                    field.SetValue(result, value);
                    continue;
                }
                int separator = pair.Key.IndexOf('/');
                if (separator < 0) continue;
                string ruleKey = pair.Key.Substring(separator + 1);
                if (!RuleGrouping.IsSupported(ruleKey)) continue;
                var rule = result.Rule(ruleKey, result.LegacyDefault(ruleKey));
                switch (pair.Key.Substring(0, separator))
                {
                    case "enabled": rule.Enabled = pair.Value != 0; break;
                    case "amount": rule.BaseAmount = pair.Value; break;
                    case "exponent": rule.PopulationExponent = pair.Value; break;
                    case "population": rule.ReferencePopulation = (int)pair.Value; break;
                }
            }
            result.Sanitize();
        }

        internal bool StoreEdits(Dictionary<string, float> inherited, Dictionary<string, float> before,
            Dictionary<string, float> after)
        {
            bool changed = false;
            foreach (var pair in after)
            {
                if (before.TryGetValue(pair.Key, out float previous) && previous == pair.Value) continue;
                if (inherited.TryGetValue(pair.Key, out float defaultValue) && defaultValue == pair.Value)
                    changed |= values.Remove(pair.Key);
                else
                {
                    values[pair.Key] = pair.Value;
                    changed = true;
                }
            }
            return changed;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref FactionKey, "factionDef");
            Scribe_Values.Look(ref Id, "id");
            Scribe_Collections.Look(ref values, "values", LookMode.Value, LookMode.Value, ref saveKeys, ref saveValues);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (string.IsNullOrEmpty(Id)) Id = Guid.NewGuid().ToString("N");
            values = values ?? new Dictionary<string, float>();
            UpgradeCurveFields(values);
            foreach (string key in values.Keys.Where(key => !CreditMath.Finite(values[key])).ToArray()) values.Remove(key);
        }
    }
}

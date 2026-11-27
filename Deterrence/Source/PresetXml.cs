using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Deterrence
{
    internal static class PresetXml
    {
        internal static string Encode(SettingsPreset preset)
        {
            var snapshot = preset.Snapshot;
            var global = new XElement("Global", WriteValues(FactionOverride.Capture(snapshot)));
            global.Add(new XElement("FactionOverridesEnabled", snapshot.FactionOverridesEnabled),
                new XElement("ShowAdvancedColonyScaling", snapshot.ShowAdvancedColonyScaling),
                new XElement("AdjustQuests", snapshot.AdjustQuests),
                new XElement("DeterArrivals", snapshot.DeterArrivals),
                new XElement("RaidSkipStrength", snapshot.RaidSkipStrength),
                new XElement("SkipExponent", snapshot.SkipExponent),
                new XElement("RetreatFactor", snapshot.RetreatFactor),
                new XElement("OwnDeadWeight", snapshot.OwnDeadWeight),
                new XElement("CorpsePerCombatPower", snapshot.CorpsePerCombatPower),
                new XElement("FalloutPerDay", snapshot.FalloutPerDay),
                new XElement("TemperaturePerDay", snapshot.TemperaturePerDay));
            return new XElement("DeterrencePreset", new XElement("Name", preset.Name),
                new XElement("Description", preset.Description ?? ""), global, new XElement("Factions",
                    snapshot.FactionOverrides.Select(profile => new XElement("Faction",
                        new XAttribute("key", profile.FactionKey), new XAttribute("id", profile.Id),
                        WriteValues(profile.Values))))).ToString() + Environment.NewLine;
        }

        internal static SettingsPreset Decode(string text, string fileName)
        {
            using (var input = new StringReader(text))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 1000000 }))
            {
                var root = XElement.Load(reader);
                if (root.Name != "DeterrencePreset") throw new FormatException("Expected a DeterrencePreset root.");
                var global = root.Element("Global");
                var snapshot = new DeterrenceSettings();
                if (global != null)
                {
                    snapshot.FactionOverridesEnabled = ReadOptional(global, "FactionOverridesEnabled", 0) != 0;
                    snapshot.ShowAdvancedColonyScaling = ReadOptional(global, "ShowAdvancedColonyScaling", 0) != 0;
                    snapshot.AdjustQuests = ReadOptional(global, "AdjustQuests", 1) != 0;
                    snapshot.DeterArrivals = ReadOptional(global, "DeterArrivals", 0) != 0;
                    snapshot.RaidSkipStrength = ReadOptional(global, "RaidSkipStrength", snapshot.RaidSkipStrength);
                    snapshot.SkipExponent = ReadOptional(global, "SkipExponent", snapshot.SkipExponent);
                    snapshot.RetreatFactor = ReadOptional(global, "RetreatFactor", snapshot.RetreatFactor);
                    snapshot.OwnDeadWeight = ReadOptional(global, "OwnDeadWeight", snapshot.OwnDeadWeight);
                    snapshot.CorpsePerCombatPower = ReadOptional(global, "CorpsePerCombatPower", snapshot.CorpsePerCombatPower);
                    snapshot.FalloutPerDay = ReadOptional(global, "FalloutPerDay", snapshot.FalloutPerDay);
                    snapshot.TemperaturePerDay = ReadOptional(global, "TemperaturePerDay", snapshot.TemperaturePerDay);
                    FactionOverride.ApplyValues(snapshot, ReadValues(global));
                }
                snapshot.Sanitize();
                var seen = new HashSet<string>();
                foreach (var faction in root.Element("Factions")?.Elements("Faction") ?? Enumerable.Empty<XElement>())
                {
                    string key = ((string)faction.Attribute("key"))?.Trim();
                    if (string.IsNullOrEmpty(key) || !seen.Add(key)) throw new FormatException("Faction keys must be unique.");
                    string id = (string)faction.Attribute("id");
                    var profile = new FactionOverride { FactionKey = key, Id = string.IsNullOrWhiteSpace(id) ? fileName + ":" + key : id };
                    profile.ReplaceValues(ReadValues(faction));
                    if (profile.Count > 0) snapshot.FactionOverrides.Add(profile);
                }
                string name = ((string)root.Element("Name"))?.Trim();
                var preset = new SettingsPreset
                {
                    Name = string.IsNullOrEmpty(name) ? fileName : name,
                    Description = (string)root.Element("Description") ?? ""
                };
                preset.SaveFrom(snapshot);
                return preset;
            }
        }

        private static IEnumerable<XElement> WriteValues(IReadOnlyDictionary<string, float> values)
        {
            foreach (var pair in values.Where(p => !p.Key.Contains("/")).OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                Type type = FactionOverride.SettingType(pair.Key);
                if (type != null) yield return new XElement(pair.Key, FormatValue(pair.Value, type));
            }
            var sources = values.Where(p => p.Key.Contains("/")).GroupBy(p => p.Key.Substring(p.Key.IndexOf('/') + 1));
            foreach (var source in sources.OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                yield return new XElement("Source", new XAttribute("key", source.Key), source.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p =>
                    {
                        string name = p.Key.Substring(0, p.Key.IndexOf('/'));
                        return new XAttribute(name, FormatValue(p.Value, name == "enabled" ? typeof(bool) : typeof(float)));
                    }));
            }
        }

        private static Dictionary<string, float> ReadValues(XElement parent)
        {
            var result = new Dictionary<string, float>();
            foreach (var element in parent.Elements())
            {
                string name = element.Name.LocalName;
                if (name == "Source")
                {
                    string key = (string)element.Attribute("key");
                    if (string.IsNullOrWhiteSpace(key)) throw new FormatException("Source is missing its key.");
                    if (!RuleGrouping.IsSupported(key)) continue;
                    foreach (string field in new[] { "amount", "enabled", "exponent", "population" })
                    {
                        var attribute = element.Attribute(field);
                        if (attribute != null) result[field + "/" + key] = ParseValue(attribute.Value, typeof(float));
                    }
                }
                else if (FactionOverride.SettingType(name) is Type type) result[name] = ParseValue(element.Value, type);
            }
            FactionOverride.UpgradeCurveFields(result);
            return result;
        }

        private static string FormatValue(float value, Type type)
        {
            if (type == typeof(bool)) return value == 0 ? "false" : "true";
            if (type.IsEnum) return Enum.ToObject(type, (int)value).ToString();
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static float ReadOptional(XElement parent, string name, float fallback)
            => parent.Element(name) is XElement element ? ParseValue(element.Value, typeof(float)) : fallback;

        private static float ParseValue(string text, Type type)
        {
            if (bool.TryParse(text, out bool toggle)) return toggle ? 1 : 0;
            if (type.IsEnum && Enum.TryParse(type, text, true, out object value)) return Convert.ToSingle(value);
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                && CreditMath.Finite(number) && (type != typeof(int) || Math.Abs(number) < int.MaxValue)) return number;
            throw new FormatException("Invalid setting value: " + text);
        }
    }
}

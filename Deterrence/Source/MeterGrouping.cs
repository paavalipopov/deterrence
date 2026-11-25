using System;
using System.Collections.Generic;
using System.Linq;

namespace Deterrence
{
    public enum MeterKind { HostileRemains, Violence, Environment }

    public class MeterGroup
    {
        public string Key, Label;
        public MeterKind Kind;
        public List<LedgerValues> Contributors = new List<LedgerValues>();
        public double Balance => Contributors.Sum(e => CreditMath.Positive(e.Balance));
        public int Present => Contributors.Count(e => e.Active);
        public int Fading => Contributors.Count(e => !e.Active && e.Balance > 0);
    }

    public static class MeterGrouping
    {
        public static bool IsRemains(LedgerValues entry)
            => entry.CorpseId >= 0 || entry.Key?.StartsWith("remains:", StringComparison.Ordinal) == true
                || entry.SourceKey == "organs:local" || entry.Key?.StartsWith("organs:", StringComparison.Ordinal) == true;

        public static string BodyKind(LedgerValues entry)
            => entry.SourceKey == "remains:human" ? "human"
                : entry.SourceKey == "remains:mech" ? "mech"
                : entry.SourceKey == "remains:animal" ? "animal"
                : entry.SourceKey == "organs:local" || entry.Key?.StartsWith("organs:", StringComparison.Ordinal) == true ? "organs" : "other";

        public static List<MeterGroup> Build(IEnumerable<LedgerValues> entries, int mapId)
        {
            var remains = new MeterGroup { Key = "hostile-remains", Label = "Remains", Kind = MeterKind.HostileRemains };
            var violence = new MeterGroup { Key = "violence", Label = "Reputation", Kind = MeterKind.Violence };
            var result = new List<MeterGroup> { remains, violence };
            foreach (var entry in entries.Where(e => e != null && e.MapId == mapId && (e.Active || e.Balance > 0)))
            {
                if (IsRemains(entry)) { remains.Contributors.Add(entry); continue; }
                if (entry.Category == "Reputation" || entry.Key?.StartsWith("reputation:violence:", StringComparison.Ordinal) == true)
                { violence.Contributors.Add(entry); continue; }
                string source = !string.IsNullOrEmpty(entry.SourceKey) ? entry.SourceKey : "legacy:" + entry.Label;
                string key = "environment:" + source;
                var group = result.FirstOrDefault(g => g.Key == key);
                if (group == null)
                {
                    group = new MeterGroup { Key = key, Label = EnvironmentName(source, entry.Label), Kind = MeterKind.Environment };
                    result.Add(group);
                }
                group.Contributors.Add(entry);
            }
            return result.Take(2).Concat(result.Skip(2).OrderBy(g => g.Label, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        private static string EnvironmentName(string source, string fallback)
        {
            switch (source)
            {
                case "pollution:ground": return "Pollution";
                case "infestation:hives": return "Infestation";
                case "climate:heat": return "Outdoor heat";
                case "climate:cold": return "Outdoor cold";
                case "condition:ToxicFallout": return "Toxic fallout";
                case "condition:ToxicRain": return "Toxic rain";
                case "condition:AcidRain": return "Acid rain";
                default: return fallback ?? "Environment";
            }
        }
    }
}

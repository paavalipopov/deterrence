using System;

namespace Deterrence
{
    public enum SettingsSection { Formula, Remains, Reputation, Hazards, Options, Curves, Summary, Presets }

    public static class RuleGrouping
    {
        public static bool IsIllness(string key, string category = null)
            => (key != null && key.StartsWith("illness:", StringComparison.OrdinalIgnoreCase))
                || string.Equals(category, "Illness", StringComparison.OrdinalIgnoreCase);

        public static bool IsRemovedSource(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (IsIllness(key) || key.StartsWith("terrain:", StringComparison.OrdinalIgnoreCase)
                || key == "habitat:biome") return true;
            return key == "condition:SolarFlare" || key == "condition:Eclipse"
                || key == "condition:PsychicDrone" || key == "condition:VolcanicWinter"
                || key == "condition:HeatWave" || key == "condition:ColdSnap";
        }

        public static bool IsSupported(string key) => !IsRemovedSource(key);
        public static bool IsBump(string key) => key != null && key.StartsWith("bump:", StringComparison.Ordinal);
        public static bool IsDeathBump(string key)
            => key == "bump:human" || key == "bump:mech" || key == "bump:animal";
        public static string ConditionMeter(int mapId, string defName) => "condition:" + mapId + ":" + defName;
        public static string ReputationMeter(int mapId) => "reputation:violence:" + mapId;

        public static SettingsSection SectionFor(string key)
        {
            if (key == null) return SettingsSection.Hazards;
            if (IsBump(key)) return SettingsSection.Reputation;
            if (key == "organs:local") return SettingsSection.Remains;
            if (key.StartsWith("remains:", StringComparison.Ordinal)) return SettingsSection.Remains;
            if (key.StartsWith("pollution:", StringComparison.Ordinal)
                || key.StartsWith("climate:", StringComparison.Ordinal)
                || key.StartsWith("terrain:", StringComparison.Ordinal)) return SettingsSection.Hazards;
            return SettingsSection.Hazards;
        }
    }
}

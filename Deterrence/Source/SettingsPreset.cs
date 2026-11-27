using System.Collections.Generic;
using System.IO;
using RimWorld;
using Verse;

namespace Deterrence
{
    public sealed class SettingsPreset : IExposable
    {
        public string Name;
        public string Description = "";
        private DeterrenceSettings snapshot = new DeterrenceSettings();
        internal DeterrenceSettings Snapshot => snapshot;
        internal string FilePath;
        internal string FileContents;
        internal bool IsBuiltIn { get; private set; }
        private string builtInId;
        internal string SelectionId => IsBuiltIn ? builtInId : Path.GetFileName(FilePath);

        internal static SettingsPreset CreateDefaults()
        {
            var preset = new SettingsPreset { Name = "Flat", IsBuiltIn = true, builtInId = "@defaults",
                Description = "Restores the mod defaults for your installed content and clears faction overrides, so every faction fears the same things." };
            preset.snapshot.DiscoverRules();
            return preset;
        }

        // Flat plus faction profiles for outsiders that fear different things.
        internal static SettingsPreset CreateSmart()
        {
            var preset = new SettingsPreset { Name = "Smart", IsBuiltIn = true, builtInId = "@smart",
                Description = "Flat, plus faction profiles. Waster pirates ignore pollution and toxic hazards. "
                    + "Insects ignore remains and reputation but still avoid extreme temperatures. Mechanoids ignore hazards." };
            var snapshot = preset.snapshot;
            snapshot.DiscoverRules();
            snapshot.FactionOverridesEnabled = true;
            var toxic = new Dictionary<string, float> { { "enabled/pollution:ground", 0 } };
            foreach (var condition in new[] { "ToxicFallout", "ToxicSpewer", "NoxiousHaze" })
                if (DefDatabase<GameConditionDef>.GetNamedSilentFail(condition) != null) toxic["enabled/condition:" + condition] = 0;
            AddProfile(snapshot, "PirateWaster", toxic);
            AddProfile(snapshot, "Insect", new Dictionary<string, float>
                { { nameof(DeterrenceSettings.Corpses), 0 }, { nameof(DeterrenceSettings.Reputation), 0 }, { "enabled/organs:local", 0 } });
            AddProfile(snapshot, "Mechanoid", new Dictionary<string, float> { { nameof(DeterrenceSettings.Conditions), 0 } });
            return preset;
        }

        // Stable IDs keep each profile's balances when the preset is loaded again.
        private static void AddProfile(DeterrenceSettings snapshot, string factionKey, Dictionary<string, float> values)
        {
            if (DefDatabase<FactionDef>.GetNamedSilentFail(factionKey) == null) return;
            var profile = new FactionOverride { FactionKey = factionKey, Id = "smart-" + factionKey };
            profile.ReplaceValues(values);
            snapshot.FactionOverrides.Add(profile);
        }

        internal void SaveFrom(DeterrenceSettings source) => snapshot = source.CopyForPreset();

        internal void LoadInto(DeterrenceSettings target) => target.LoadPreset(snapshot);

        public void ExposeData()
        {
            Scribe_Values.Look(ref Name, "name");
            snapshot.ExposeProfileData();
        }
    }
}

using System.IO;
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
        internal string SelectionId => IsBuiltIn ? "@defaults" : Path.GetFileName(FilePath);

        internal static SettingsPreset CreateDefaults()
        {
            var preset = new SettingsPreset { Name = "Defaults", IsBuiltIn = true,
                Description = "Restores the mod defaults for your installed content and clears faction overrides." };
            preset.snapshot.DiscoverRules();
            return preset;
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

using HarmonyLib;
using UnityEngine;
using Verse;

namespace Deterrence
{
    public class DeterrenceMod : Mod
    {
        public static DeterrenceSettings Settings = new DeterrenceSettings();
        internal static string EquationDirectory;
        private readonly SettingsEditor editor = new SettingsEditor();

        public DeterrenceMod(ModContentPack content) : base(content)
        {
            EquationDirectory = System.IO.Path.Combine(content.RootDir, "Textures/UI/Deterrence/Equations");
            new Harmony("pavel.deterrence").PatchAll();
            LegacyIdentity.ImportSettings(content.FolderName);
            Settings = GetSettings<DeterrenceSettings>();
            Settings.Sanitize();
            Log.Message("[Deterrence] Loaded 0.12.0.");
        }

        public override string SettingsCategory() => "Deterrence";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            editor.Draw(inRect, this, Settings);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            Current.Game?.GetComponent<DeterrenceGameComponent>()?.Refresh(true);
        }

        internal void SavePresetLibrary() => base.WriteSettings();
    }
}

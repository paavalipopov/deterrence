using HarmonyLib;
using UnityEngine;
using Verse;

namespace Antiwealth
{
    public class AntiwealthMod : Mod
    {
        public static AntiwealthSettings Settings = new AntiwealthSettings();
        internal static string EquationDirectory;
        private readonly SettingsEditor editor = new SettingsEditor();

        public AntiwealthMod(ModContentPack content) : base(content)
        {
            EquationDirectory = System.IO.Path.Combine(content.RootDir, "Textures/UI/Deterrence/Equations");
            Settings = GetSettings<AntiwealthSettings>();
            Settings.Sanitize();
            new Harmony("pavel.antiwealth").PatchAll();
            Log.Message("[Deterrence] Loaded 0.11.1.");
        }

        public override string SettingsCategory() => "Deterrence";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            editor.Draw(inRect, this, Settings);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            Current.Game?.GetComponent<AntiwealthGameComponent>()?.Refresh(true);
        }

        internal void SavePresetLibrary() => base.WriteSettings();
    }
}

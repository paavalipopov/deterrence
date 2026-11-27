using System;
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
            PatchEachClass(new Harmony("pavel.deterrence"));
            LegacyIdentity.ImportSettings(content.FolderName);
            Settings = GetSettings<DeterrenceSettings>();
            Settings.Sanitize();
            Log.Message("[Deterrence] Loaded 0.14.0.");
        }

        // Patch class by class, so one missing game method leaves the other patches active.
        private static void PatchEachClass(Harmony harmony)
        {
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(DeterrenceMod).Assembly))
            {
                try { harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception error) { Log.Error("[Deterrence] Could not apply patches in " + type.FullName + ": " + error); }
            }
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

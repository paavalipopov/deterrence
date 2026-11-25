using System;
using System.IO;
using HarmonyLib;
using Verse;

namespace Deterrence
{
    [HarmonyPatch(typeof(GenTypes), "GetTypeInAnyAssemblyInt")]
    internal static class LegacyIdentity
    {
        // Scribe and XML defs both resolve class names here, on a type-cache miss.
        private static void Prefix(ref string __0)
        {
            if (__0 == null || !__0.StartsWith("Antiwealth.", StringComparison.Ordinal)) return;
            string name = __0.Replace("Antiwealth", "Deterrence");
            if (typeof(DeterrenceMod).Assembly.GetType(name) != null) __0 = name;
        }

        internal static void ImportSettings(string folder)
        {
            string config = GenFilePaths.ConfigFolderPath;
            string target = Path.Combine(config, GenText.SanitizeFilename("Mod_" + folder + "_DeterrenceMod.xml"));
            if (File.Exists(target)) return;
            string old = Path.Combine(config, GenText.SanitizeFilename("Mod_" + folder + "_AntiwealthMod.xml"));
            if (!File.Exists(old)) old = Path.Combine(config, "Mod_Antiwealth_AntiwealthMod.xml");
            if (!File.Exists(old)) return;

            string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(old, temp);
                File.Move(temp, target);
            }
            catch (IOException error)
            {
                if (!File.Exists(target)) Log.Warning("[Deterrence] Could not import Antiwealth settings: " + error.Message);
            }
            catch (UnauthorizedAccessException error)
            {
                Log.Warning("[Deterrence] Could not import Antiwealth settings: " + error.Message);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}

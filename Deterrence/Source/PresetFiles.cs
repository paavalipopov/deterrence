using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Deterrence
{
    internal sealed class PresetFiles
    {
        private const int MaxFileBytes = 1000000;
        internal string Folder { get; }

        internal PresetFiles(string folder) => Folder = folder;

        internal List<SettingsPreset> ReadAll(List<string> errors)
        {
            var presets = new List<SettingsPreset>();
            if (!Directory.Exists(Folder)) return presets;
            foreach (string path in Directory.GetFiles(Folder, "*.xml"))
            {
                try { presets.Add(Read(path)); }
                catch (Exception error) when (IsFileError(error)) { errors.Add(Path.GetFileName(path)); }
            }
            return presets.OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(preset => preset.FilePath, StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal void ImportLegacy(SettingsPreset preset)
        {
            string fingerprint;
            using (var sha = SHA256.Create())
                fingerprint = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(preset.Name))).Replace("-", "");
            string path = Path.Combine(Folder, "legacy-" + fingerprint.Substring(0, 20) + ".xml");
            if (File.Exists(path)) Read(path);
            else Write(preset, path, false);
        }

        internal void Save(SettingsPreset preset, SettingsPreset previous = null)
        {
            if (preset.IsBuiltIn || previous?.IsBuiltIn == true) throw new IOException("Choose a name for your own preset.");
            if (previous != null)
            {
                CheckVersion(previous);
                Write(preset, previous.FilePath, true);
                return;
            }
            string safeName = string.Concat(preset.Name.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
            string path = Path.Combine(Folder, safeName + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".xml");
            Write(preset, path, false);
        }

        internal void Delete(SettingsPreset preset)
        {
            if (preset.IsBuiltIn) throw new IOException("The built-in preset is read-only.");
            CheckVersion(preset);
            File.Delete(preset.FilePath);
        }

        private static SettingsPreset Read(string path)
        {
            if (new FileInfo(path).Length > MaxFileBytes) throw new FormatException("Preset is too large.");
            string contents = File.ReadAllText(path);
            var preset = PresetXml.Decode(contents, Path.GetFileNameWithoutExtension(path));
            preset.FilePath = path;
            preset.FileContents = contents;
            return preset;
        }

        private void Write(SettingsPreset preset, string path, bool replace)
        {
            Directory.CreateDirectory(Folder);
            string contents = PresetXml.Encode(preset);
            string temporary = Path.Combine(Folder, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, contents);
                if (replace) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            preset.FilePath = path;
            preset.FileContents = contents;
        }

        private static void CheckVersion(SettingsPreset preset)
        {
            var file = new FileInfo(preset.FilePath);
            if (!file.Exists || file.Length > MaxFileBytes || File.ReadAllText(file.FullName) != preset.FileContents)
                throw new IOException("The preset file changed on disk. Refresh files and try again.");
        }

        internal static bool IsFileError(Exception error)
            => error is IOException || error is UnauthorizedAccessException || error is SecurityException
                || error is XmlException || error is FormatException || error is ArgumentException;
    }
}

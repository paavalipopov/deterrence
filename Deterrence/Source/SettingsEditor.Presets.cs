using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Deterrence
{
    public partial class SettingsEditor
    {
        private readonly List<SettingsPreset> filePresets = new List<SettingsPreset>();
        private PresetFiles presetFiles;
        private bool presetsNeedRefresh = true;
        private string presetNotice = "";
        private SettingsPreset selectedPreset;
        private Vector2 presetDetailsScroll, presetListScroll;
        private float presetDetailsHeight = 500;
        private SettingsPreset builtInDefaults, builtInSmart;

        private void DrawPresets(Rect rect, DeterrenceMod owner, DeterrenceSettings settings)
        {
            if (presetFiles == null)
                presetFiles = new PresetFiles(Path.Combine(GenFilePaths.ConfigFolderPath, "Deterrence", "Presets"));
            if (builtInDefaults == null) builtInDefaults = SettingsPreset.CreateDefaults();
            if (builtInSmart == null) builtInSmart = SettingsPreset.CreateSmart();
            if (presetsNeedRefresh) RefreshPresets(owner, settings);
            float width = rect.width * 0.61f;
            const float gap = 16;
            DrawPresetDetails(new Rect(rect.x, rect.y, width, rect.height), owner, settings);
            DrawPresetList(new Rect(rect.x + width + gap, rect.y, rect.width - width - gap, rect.height), owner, settings);
        }

        private void DrawPresetDetails(Rect rect, DeterrenceMod owner, DeterrenceSettings settings)
        {
            var view = new Rect(0, 0, rect.width - 20, Math.Max(rect.height, presetDetailsHeight));
            Widgets.BeginScrollView(rect, ref presetDetailsScroll, view);
            var listing = HelpUI.SingleColumn(new Listing_Standard());
            listing.Begin(view);
            SectionTitle(listing, "Current settings");
            var current = filePresets.FirstOrDefault(preset =>
                string.Equals(preset.SelectionId, settings.ActivePresetFile, StringComparison.OrdinalIgnoreCase));
            listing.Label("Current preset: " + (current?.Name ?? "Unnamed"));
            if (listing.ButtonText("Save current settings"))
                Find.WindowStack.Add(new PresetTextDialog("Save current settings", current?.IsBuiltIn == true ? "" : current?.Name ?? "", false,
                    name => RequestSavePreset(name, owner, settings)));

            var chosen = selectedPreset;
            if (chosen != null)
            {
                var row = listing.GetRect(32);
                if (ChoiceButton(row, 0, chosen.IsBuiltIn ? 1 : 2, "Load", false))
                    ConfirmPresetAction("Load “" + chosen.Name + "” into the current settings?", () =>
                    {
                        chosen.LoadInto(settings);
                        settings.ActivePresetFile = chosen.SelectionId;
                        SelectFaction(null);
                        owner.WriteSettings();
                        presetNotice = "Loaded " + chosen.Name + ".";
                    });
                if (!chosen.IsBuiltIn)
                {
                    if (ChoiceButton(row, 1, 2, "Rename", false))
                        Find.WindowStack.Add(new PresetTextDialog("Rename preset", chosen.Name, false, name =>
                        {
                            var error = CheckName(name, chosen);
                            if (error != null) return error;
                            return SavePresetFile(chosen.Snapshot, name, chosen.Description, chosen, owner, settings, false);
                        }));
                    row = listing.GetRect(32);
                    if (ChoiceButton(row, 0, 2, "Edit description", false))
                        Find.WindowStack.Add(new PresetTextDialog("Preset description", chosen.Description, true, description =>
                            SavePresetFile(chosen.Snapshot, chosen.Name, description, chosen, owner, settings, false)));
                    if (ChoiceButton(row, 1, 2, "Delete", false))
                        ConfirmPresetAction("Delete the preset “" + chosen.Name + "”?", () => DeletePreset(chosen, owner, settings));
                }
                listing.GapLine();
                SectionTitle(listing, chosen.Name);
                if (!string.IsNullOrWhiteSpace(chosen.Description)) listing.Label(chosen.Description);
                DrawPresetSummary(listing, chosen.Snapshot);
            }
            else listing.Label("Select a saved preset on the right to see its settings and description.");
            if (presetNotice.Length > 0) { listing.Gap(); listing.Label(presetNotice); }
            presetDetailsHeight = listing.CurHeight + 20;
            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawPresetList(Rect rect, DeterrenceMod owner, DeterrenceSettings settings)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y + 6, rect.width, 27), "Saved presets");
            Text.Font = GameFont.Small;
            var actions = new Rect(rect.x, rect.y + 39, rect.width, 30);
            if (ChoiceButton(actions, 0, 2, "Open folder", false))
            {
                try
                {
                    Directory.CreateDirectory(presetFiles.Folder);
                    Application.OpenURL(new Uri(presetFiles.Folder + Path.DirectorySeparatorChar).AbsoluteUri);
                }
                catch (Exception error) when (PresetFiles.IsFileError(error)) { presetNotice = error.Message; }
            }
            if (ChoiceButton(actions, 1, 2, "Refresh files", false))
            {
                presetNotice = "";
                RefreshPresets(owner, settings);
            }
            var outer = new Rect(rect.x, rect.y + 76, rect.width, Math.Max(0, rect.height - 76));
            var view = new Rect(0, 0, rect.width - 18, Math.Max(outer.height, filePresets.Count * 36f));
            Widgets.BeginScrollView(outer, ref presetListScroll, view);
            if (filePresets.Count == 0) Widgets.Label(new Rect(0, 0, view.width, 30), "No saved presets.");
            for (int i = 0; i < filePresets.Count; i++)
            {
                var preset = filePresets[i];
                var row = new Rect(0, i * 36, view.width, 32);
                if (Widgets.ButtonText(row, (preset.IsBuiltIn ? preset.Name + " (built-in)" : preset.Name).Truncate(view.width - 24)))
                {
                    selectedPreset = preset;
                    presetDetailsScroll = Vector2.zero;
                    presetNotice = "";
                }
                if (preset == selectedPreset) Widgets.DrawHighlightSelected(row);
                TooltipHandler.TipRegion(row, preset.IsBuiltIn ? preset.Description : preset.SelectionId);
            }
            Widgets.EndScrollView();
        }

        private void DrawPresetSummary(Listing_Standard listing, DeterrenceSettings snapshot)
        {
            HelpUI.ThreatEquation(listing, snapshot);
            var presetDefaults = builtInDefaults.Snapshot;
            var baseline = FactionOverride.Capture(presetDefaults);
            var changes = FactionOverride.Capture(snapshot)
                .Where(pair => !baseline.TryGetValue(pair.Key, out float value) || pair.Value != value).ToList();
            int sourceCount = changes.Where(pair => pair.Key.Contains("/"))
                .Select(pair => pair.Key.Substring(pair.Key.IndexOf('/') + 1)).Distinct().Count();
            int globalCount = changes.Count(pair => !pair.Key.Contains("/"));
            if (snapshot.BiomeExponent != presetDefaults.BiomeExponent)
                listing.Label("Biome strength: " + snapshot.BiomeExponent.ToString("0.##"));
            if (snapshot.IslandMultiplier != presetDefaults.IslandMultiplier || snapshot.IslandMaxTiles != presetDefaults.IslandMaxTiles)
                listing.Label("Island multiplier: " + snapshot.IslandMultiplier.ToString("0.##") + " • up to " + snapshot.IslandMaxTiles + " tiles");
            if (globalCount + sourceCount == 0) listing.Label("Default Global settings");
            else listing.Label(globalCount + " Global settings changed • " + sourceCount + " sources customized");
            var profiles = snapshot.FactionOverrides.Where(profile => profile.Count > 0).ToList();
            if (profiles.Count == 0) return;
            listing.Label("Faction overrides: " + profiles.Count + (snapshot.FactionOverridesEnabled ? "" : " saved, currently off"));
            foreach (var profile in profiles.Take(3))
            {
                var def = DefDatabase<FactionDef>.GetNamedSilentFail(profile.FactionKey);
                listing.Label("• " + (def?.LabelCap.ToString() ?? profile.FactionKey) + " (" + profile.Count + " settings)");
            }
            if (profiles.Count > 3) listing.Label("+" + (profiles.Count - 3) + " more");
        }

        private string RequestSavePreset(string name, DeterrenceMod owner, DeterrenceSettings settings)
        {
            if (string.Equals(name, builtInDefaults.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, builtInSmart.Name, StringComparison.OrdinalIgnoreCase)) return "Choose a new name for your preset.";
            var matches = filePresets.Where(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1) return "Several files use this name. Choose a unique name.";
            var previous = matches.FirstOrDefault();
            if (previous == null) return SavePresetFile(settings, name, "", null, owner, settings, true);
            ConfirmPresetAction("Replace the saved settings in “" + previous.Name + "”?", () =>
            {
                var error = SavePresetFile(settings, name, previous.Description, previous, owner, settings, true);
                if (error != null) presetNotice = error;
            });
            return null;
        }

        private string CheckName(string name, SettingsPreset except)
            => filePresets.Any(preset => preset != except && string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase))
                ? "A preset already uses that name." : null;

        private string SavePresetFile(DeterrenceSettings source, string name, string description, SettingsPreset previous,
            DeterrenceMod owner, DeterrenceSettings settings, bool makeCurrent)
        {
            var saved = new SettingsPreset { Name = name, Description = description };
            saved.SaveFrom(source);
            try { presetFiles.Save(saved, previous); }
            catch (Exception error) when (PresetFiles.IsFileError(error)) { return error.Message; }
            if (makeCurrent)
            {
                settings.ActivePresetFile = saved.SelectionId;
                owner.SavePresetLibrary();
            }
            selectedPreset = saved;
            presetNotice = "Saved " + saved.Name + ".";
            RefreshPresets(owner, settings);
            return null;
        }

        private void DeletePreset(SettingsPreset chosen, DeterrenceMod owner, DeterrenceSettings settings)
        {
            try { presetFiles.Delete(chosen); }
            catch (Exception error) when (PresetFiles.IsFileError(error)) { presetNotice = error.Message; return; }
            if (string.Equals(chosen.SelectionId, settings.ActivePresetFile, StringComparison.OrdinalIgnoreCase))
            {
                settings.ActivePresetFile = null;
                owner.SavePresetLibrary();
            }
            selectedPreset = null;
            presetNotice = "Deleted " + chosen.Name + ".";
            RefreshPresets(owner, settings);
        }

        private void RefreshPresets(DeterrenceMod owner, DeterrenceSettings settings)
        {
            presetsNeedRefresh = false;
            string selection = selectedPreset?.SelectionId ?? settings.ActivePresetFile ?? builtInDefaults.SelectionId;
            var unreadable = new List<string>();
            bool imported = false;
            foreach (var legacy in settings.Presets.ToArray())
            {
                try
                {
                    presetFiles.ImportLegacy(legacy);
                    settings.Presets.Remove(legacy);
                    imported = true;
                }
                catch (Exception error) when (PresetFiles.IsFileError(error)) { unreadable.Add(legacy.Name); }
            }
            if (imported) owner.SavePresetLibrary();
            try
            {
                filePresets.Clear();
                filePresets.Add(builtInDefaults);
                filePresets.Add(builtInSmart);
                selectedPreset = builtInDefaults;
                var loaded = presetFiles.ReadAll(unreadable);
                filePresets.AddRange(loaded);
                selectedPreset = filePresets.FirstOrDefault(preset => preset.SelectionId == selection) ?? builtInDefaults;
            }
            catch (Exception error) when (PresetFiles.IsFileError(error)) { presetNotice = error.Message; return; }
            if (unreadable.Count > 0)
                presetNotice = "Could not read or migrate: " + string.Join(", ", unreadable.Take(3))
                    + (unreadable.Count > 3 ? " and " + (unreadable.Count - 3) + " more." : ".");
        }

        private static void ConfirmPresetAction(string question, Action action)
            => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(question, action));
    }

    internal sealed class PresetTextDialog : Window
    {
        private readonly string title;
        private readonly bool multiline;
        private readonly Func<string, string> save;
        private string value;
        private string error;

        public PresetTextDialog(string title, string value, bool multiline, Func<string, string> save)
        {
            this.title = title;
            this.value = value ?? "";
            this.multiline = multiline;
            this.save = save;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(440, multiline ? 325 : 202);

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, rect.width - 28, 30), title);
            Text.Font = GameFont.Small;
            var input = new Rect(0, 43, rect.width, multiline ? 144 : 30);
            value = multiline ? Widgets.TextArea(input, value) : Widgets.TextField(input, value);
            if (error != null) Widgets.Label(new Rect(0, input.yMax + 6, rect.width, 34), error);
            float y = rect.yMax - 32;
            if (Widgets.ButtonText(new Rect(0, y, rect.width / 2 - 6, 32), "Cancel")) Close();
            if (Widgets.ButtonText(new Rect(rect.width / 2 + 6, y, rect.width / 2 - 6, 32), "Save"))
            {
                string text = value.Trim();
                if (!multiline && (text.Length == 0 || text.Length > 48)) error = "Enter a name of 1–48 characters.";
                else if (multiline && text.Length > 2000) error = "Use up to 2,000 characters.";
                else if ((error = save(text)) == null) Close();
            }
        }
    }
}

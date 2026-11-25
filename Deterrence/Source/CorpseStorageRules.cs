using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Deterrence
{
    internal enum BurialStatus { Hostile = 1, Neutral = 2, Friendly = 4 }

    internal sealed class CorpseStorageRules : IExposable
    {
        internal const int Any = -1;
        internal int Status = Any, Diplomacy = Any;
        // null accepts future factions too. An empty dictionary accepts nobody.
        internal Dictionary<int, string> Factions;
        internal bool Unrestricted => Status == Any && Diplomacy == Any && Factions == null;
        private static readonly ConditionalWeakTable<StorageSettings, CorpseStorageRules> byStore =
            new ConditionalWeakTable<StorageSettings, CorpseStorageRules>();

        public CorpseStorageRules() { }
        internal static CorpseStorageRules For(StorageSettings settings) => byStore.GetOrCreateValue(settings);
        internal static void Assign(StorageSettings settings, CorpseStorageRules rules)
        {
            byStore.Remove(settings);
            if (rules != null && !rules.Unrestricted) byStore.Add(settings, rules);
        }
        internal CorpseStorageRules Clone() => new CorpseStorageRules {
            Status = Status, Diplomacy = Diplomacy,
            Factions = Factions == null ? null : new Dictionary<int, string>(Factions)
        };

        public void ExposeData()
        {
            Scribe_Values.Look(ref Status, "status", Any);
            Scribe_Values.Look(ref Diplomacy, "diplomacy", Any);
            bool limited = Factions != null;
            Scribe_Values.Look(ref limited, "limitFactions");
            if (limited)
            {
                Scribe_Collections.Look(ref Factions, "factions", LookMode.Value, LookMode.Value);
                if (Factions == null) Factions = new Dictionary<int, string>();
            }
        }

        internal bool Allows(BurialRecord record, BurialStatus? relation)
        {
            return (Status == Any || (Status & (int)record.Status) != 0)
                && (Diplomacy == Any || (relation.HasValue && (Diplomacy & (int)relation.Value) != 0))
                && (Factions == null || Factions.ContainsKey(record.FactionId));
        }

        [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.AllowedToAccept), typeof(Thing))]
        private static class Acceptance
        {
            private static void Postfix(StorageSettings __instance, Thing t, ref bool __result)
            {
                if (!__result || !(t is Corpse corpse) || !byStore.TryGetValue(__instance, out var rules)
                    || rules.Unrestricted || corpse.InnerPawn?.RaceProps.Humanlike != true) return;
                var record = BurialRecord.ForCorpse(corpse.InnerPawn);
                __result = rules.Allows(record, rules.Diplomacy == Any ? null : BurialRegistry.Current.Relation(record.FactionId));
            }
        }
        [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.CopyFrom))]
        private static class Copy
        {
            private static void Prefix(StorageSettings __instance, StorageSettings other)
            {
                if (__instance != other) Assign(__instance, byStore.TryGetValue(other, out var rules) ? rules.Clone() : null);
            }
        }
        [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.SetFromPreset))]
        private static class Reset
        {
            private static void Prefix(StorageSettings __instance) => byStore.Remove(__instance);
        }
        [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.ExposeData))]
        private static class Save
        {
            private static void Postfix(StorageSettings __instance)
            {
                byStore.TryGetValue(__instance, out var rules);
                if (Scribe.mode == LoadSaveMode.Saving && (rules == null || rules.Unrestricted)) return;
                Scribe_Deep.Look(ref rules, "deterrenceCorpseStorage");
                if (Scribe.mode == LoadSaveMode.LoadingVars) Assign(__instance, rules);
            }
        }
    }
}

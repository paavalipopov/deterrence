using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Deterrence
{
    // Identity is saved with the pawn, including while a downed raider is being carried.
    internal sealed class BurialRecord : IExposable
    {
        internal const int Factionless = -1;
        internal int FactionId = Factionless;
        internal string FactionName;
        internal BurialStatus Status;
        internal bool Inferred, Downed;
        private static readonly ConditionalWeakTable<Pawn, BurialRecord> records = new ConditionalWeakTable<Pawn, BurialRecord>();
        public BurialRecord() { }
        private BurialRecord(Pawn pawn, BurialStatus status)
        {
            FactionId = pawn.Faction?.loadID ?? Factionless;
            if (pawn.Faction != null)
                FactionName = SettingsEditor.FormatFactionLabel(pawn.Faction.Name, pawn.Faction.def.LabelCap.ToString());
            Status = status;
        }
        public void ExposeData()
        {
            Scribe_Values.Look(ref FactionId, "faction", Factionless);
            Scribe_Values.Look(ref FactionName, "factionName");
            Scribe_Values.Look(ref Status, "status", BurialStatus.Neutral);
            Scribe_Values.Look(ref Inferred, "inferred");
            Scribe_Values.Look(ref Downed, "downed");
        }
        private static void Set(Pawn pawn, BurialRecord value)
        {
            records.Remove(pawn);
            if (value != null) records.Add(pawn, value);
        }
        internal static BurialRecord ForCorpse(Pawn pawn)
        {
            if (!records.TryGetValue(pawn, out var record) || record.Downed)
            {
                var status = pawn.Faction != null && pawn.Faction.IsPlayer ? BurialStatus.Friendly
                    : BurialRegistry.Current.Relation(pawn.Faction?.loadID ?? Factionless) ?? BurialStatus.Neutral;
                record = new BurialRecord(pawn, status) { Inferred = true };
                Set(pawn, record);
            }
            return record;
        }
        private static BurialStatus Classify(Pawn pawn)
        {
            var player = Faction.OfPlayer;
            var group = pawn.GetLord()?.LordJob;
            if (pawn.MentalState?.ForceHostileTo(player) == true || group is LordJob_PrisonBreak || group is LordJob_SlaveRebellion)
                return BurialStatus.Hostile;
            if (pawn.IsAttacking())
            {
                var target = pawn.CurJobDef == JobDefOf.Wait_Combat
                    ? (pawn.stances?.curStance as Stance_Busy)?.focusTarg.Thing : pawn.CurJob?.targetA.Thing;
                if (target != null && target.Faction == player) return BurialStatus.Hostile;
            }
            if (pawn.IsPrisonerOfColony) return BurialStatus.Neutral;
            if (pawn.Faction == player || pawn.IsSlaveOfColony || pawn.HostFaction == player || pawn.IsQuestLodger())
                return BurialStatus.Friendly;
            if (pawn.MapHeld?.IsPlayerHome == true)
            {
                if (group is LordJob_AssistColony) return BurialStatus.Friendly;
                if (group is LordJob_AssaultColony || group is LordJob_Siege) return BurialStatus.Hostile;
            }
            if (pawn.Downed && records.TryGetValue(pawn, out var earlier) && earlier.Downed
                && earlier.FactionId == (pawn.Faction?.loadID ?? Factionless)) return earlier.Status;
            return BurialStatus.Neutral;
        }

        [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
        private static class BeforeDowning
        {
            private static void Prefix(Pawn ___pawn)
            {
                if (___pawn.RaceProps.Humanlike && !___pawn.Dead)
                    Set(___pawn, new BurialRecord(___pawn, Classify(___pawn)) { Downed = true });
            }
        }
        internal static void ForgetDowning(Pawn pawn)
        {
            if (records.TryGetValue(pawn, out var record) && record.Downed) records.Remove(pawn);
        }
        [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeUndowned")]
        private static class Recovery { private static void Postfix(Pawn ___pawn) => ForgetDowning(___pawn); }
        [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_Resurrected))]
        private static class Resurrection { private static void Postfix(Pawn ___pawn) => records.Remove(___pawn); }
        [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
        private static class Custody
        {
            private static void Prefix(Pawn_GuestTracker __instance, out (Faction host, GuestStatus status) __state)
                => __state = (__instance.HostFaction, __instance.GuestStatus);
            private static void Postfix(Pawn_GuestTracker __instance, Pawn ___pawn, (Faction host, GuestStatus status) __state)
            {
                if (__instance.HostFaction != __state.host || __instance.GuestStatus != __state.status) ForgetDowning(___pawn);
            }
        }
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
        private static class Allegiance
        {
            private static void Prefix(Pawn __instance, out Faction __state) => __state = __instance.Faction;
            private static void Postfix(Pawn __instance, Faction __state)
            {
                if (__instance.Faction != __state) ForgetDowning(__instance);
            }
        }
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
        private static class Death
        {
            private static void Prefix(Pawn __instance, out BurialRecord __state)
            {
                __state = null;
                if (__instance.Dead || !__instance.RaceProps.Humanlike) return;
                records.TryGetValue(__instance, out __state);
                Set(__instance, new BurialRecord(__instance, Classify(__instance)));
            }
            private static void Finalizer(Pawn __instance, BurialRecord __state)
            {
                if (!__instance.Dead && __instance.RaceProps.Humanlike) Set(__instance, __state);
            }
        }
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExposeData))]
        private static class Save
        {
            private static void Postfix(Pawn __instance)
            {
                records.TryGetValue(__instance, out var record);
                if (record == null && Scribe.mode == LoadSaveMode.Saving) return;
                Scribe_Deep.Look(ref record, "deterrenceBurialRecord");
                if (Scribe.mode == LoadSaveMode.LoadingVars && record != null) Set(__instance, record);
            }
        }
        [HarmonyPatch(typeof(Corpse), nameof(Corpse.ExposeData))]
        private static class LoadCorpse
        {
            private static void Postfix(Corpse __instance)
            {
                if (Scribe.mode == LoadSaveMode.PostLoadInit) BurialRegistry.Current?.Observe(__instance.InnerPawn);
            }
        }
        [HarmonyPatch(typeof(Corpse), nameof(Corpse.InnerPawn), MethodType.Setter)]
        private static class NewCorpse
        {
            private static void Postfix(Pawn value) => BurialRegistry.Current?.Observe(value);
        }
        [HarmonyPatch(typeof(Corpse), nameof(Corpse.GetInspectString))]
        private static class Inspect
        {
            private static void Postfix(Corpse __instance, ref string __result)
            {
                var pawn = __instance.InnerPawn;
                if (pawn?.RaceProps.Humanlike != true || BurialRegistry.Current == null) return;
                var record = ForCorpse(pawn);
                __result = (__result + "\nStatus at death: " + record.Status +
                    (record.Inferred ? " (inferred from faction)" : "")).TrimStart('\n');
            }
        }
    }

    public sealed class BurialRegistry : GameComponent
    {
        private Dictionary<int, Faction> factions;
        private readonly HashSet<Pawn> pending = new HashSet<Pawn>();
        internal readonly Dictionary<int, string> KnownNames = new Dictionary<int, string>();
        private bool ready;
        public BurialRegistry(Game game) { }
        internal static BurialRegistry Current => Verse.Current.Game?.GetComponent<BurialRegistry>();

        public override void FinalizeInit()
        {
            factions = null;
            ready = true;
            foreach (var pawn in pending) Observe(pawn);
            pending.Clear();
        }
        internal void Observe(Pawn pawn)
        {
            if (pawn?.RaceProps.Humanlike != true) return;
            if (!ready) { pending.Add(pawn); return; }
            var record = BurialRecord.ForCorpse(pawn);
            if (record.FactionId != BurialRecord.Factionless) KnownNames[record.FactionId] = record.FactionName;
        }
        internal BurialStatus? Relation(int id)
        {
            if (id == BurialRecord.Factionless) return null;
            if (factions == null)
            {
                factions = new Dictionary<int, Faction>();
                foreach (var faction in Find.FactionManager.AllFactionsListForReading) factions[faction.loadID] = faction;
            }
            if (!factions.TryGetValue(id, out var f) || f.IsPlayer) return null;
            var relation = f.RelationWith(Faction.OfPlayer, true);
            if (relation == null) return null;
            return relation.kind == FactionRelationKind.Hostile ? BurialStatus.Hostile
                : relation.kind == FactionRelationKind.Ally ? BurialStatus.Friendly : BurialStatus.Neutral;
        }
        [HarmonyPatch(typeof(FactionManager), "RecacheFactions")]
        private static class Refresh
        {
            private static void Postfix() { var registry = Current; if (registry != null) registry.factions = null; }
        }
    }
}

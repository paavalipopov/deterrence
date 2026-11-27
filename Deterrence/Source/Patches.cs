using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Deterrence
{
    [HarmonyPatch(typeof(Map), nameof(Map.PlayerWealthForStoryteller), MethodType.Getter)]
    public static class ThreatWealthPatch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Map __instance, ref float __result)
        {
            if (!__instance.IsPlayerHome || __instance.IsPocketMap
                || Current.Game == null || Find.Storyteller == null || Find.Storyteller.difficulty.fixedWealthMode) return;
            if (WorldThreatWealthPatch.Depth > 0 && !DeterrenceMod.Settings.AdjustQuests) return;
            var ledger = Current.Game.GetComponent<DeterrenceGameComponent>();
            var profile = FactionRaidPoints.ProfileForEvaluation(__instance);
            var settings = profile?.Settings ?? DeterrenceMod.Settings;
            double factor = SourceMath.BiomeMultiplier(__instance.Biome.settlementSelectionWeight,
                settings.BiomeExponent, settings.BiomeWealthScaling);
            float gameThreatWealth = CreditMath.EffectiveWealth(__result,
                ledger?.SurfaceDiscountFor(__instance, settings.SmoothedSurfaceMultiplier) ?? 0);
            __result = (float)SourceMath.AdjustedThreatWealth(gameThreatWealth,
                profile?.Ledger.CreditFor(__instance) ?? ledger?.CreditFor(__instance) ?? 0, settings.Enabled, factor,
                IslandGeography.FactorFor(__instance, settings));
        }
    }

    // Quests and quest sites size their threats and rewards from the combined wealth of all colony maps.
    [HarmonyPatch(typeof(RimWorld.Planet.World), nameof(RimWorld.Planet.World.PlayerWealthForStoryteller), MethodType.Getter)]
    public static class WorldThreatWealthPatch
    {
        internal static int Depth;

        public static void Prefix() => Depth++;

        public static Exception Finalizer(Exception __exception)
        {
            Depth--;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class HostileDeathPatch
    {
        public class DeathState { public Map Map; public float Power; public double Gear; public bool Prisoner; }

        public static void Prefix(Pawn __instance, out DeathState __state)
        {
            __state = null;
            if (!__instance.Dead && __instance.MapHeld != null && (DeterrenceGameComponent.Eligible(__instance) || __instance.IsPrisonerOfColony))
                __state = new DeathState
                {
                    Map = __instance.MapHeld, Power = __instance.kindDef.combatPower, Prisoner = __instance.IsPrisonerOfColony,
                    Gear = Current.Game?.GetComponent<DeterrenceGameComponent>()?.GearForDeath(__instance) ?? 0
                };
        }

        public static void Postfix(Pawn __instance, DeathState __state)
        {
            if (__state == null || Current.Game == null) return;
            var ledger = Current.Game.GetComponent<DeterrenceGameComponent>();
            ledger?.RegisterDeath(__instance, __state.Map, __state.Power, __state.Gear, __state.Prisoner);
            ledger?.ForgetGear(__instance.thingIDNumber);
        }
    }

    [HarmonyPatch(typeof(Recipe_RemoveBodyPart), nameof(Recipe_RemoveBodyPart.ApplyOnPawn))]
    public static class PrisonerSurgeryPatch
    {
        public class SurgeryState
        {
            public SurgeryState Previous;
            public Pawn Victim;
            public BodyPartRecord Part;
            public Map Map;
            public Thing HarvestedOrgan;
            public bool Eligible, HadPart, Failed, Harvested;
        }
        [ThreadStatic] public static SurgeryState CurrentOperation;

        public static void Prefix(Pawn pawn, BodyPartRecord part, Pawn billDoer, out SurgeryState __state)
        {
            __state = new SurgeryState { Previous = CurrentOperation, Victim = pawn, Part = part, Map = pawn?.MapHeld };
            CurrentOperation = __state;
            __state.Eligible = pawn != null && part != null && pawn.IsPrisonerOfColony && pawn.RaceProps.Humanlike
                && billDoer?.Faction == Faction.OfPlayer && __state.Map != null && __state.Map.IsPlayerHome;
            __state.HadPart = __state.Eligible && !pawn.health.hediffSet.PartIsMissing(part);
        }

        public static void Postfix(SurgeryState __state)
        {
            if (__state == null || !__state.Eligible || !__state.HadPart) return;
            bool removed = __state.Victim.health.hediffSet.PartIsMissing(__state.Part);
            string rule = SurgeryOutcome.RuleFor(__state.Failed, __state.Harvested, removed);
            var ledger = Verse.Current.Game?.GetComponent<DeterrenceGameComponent>();
            if (rule == "bump:organ")
            {
                var provenance = __state.HarvestedOrgan?.TryGetComp<CompPrisonerOrgan>();
                if (provenance != null) provenance.HarvestedLocallyFromPrisoner = true;
                ledger?.TrackOrgan(__state.HarvestedOrgan);
            }
            if (rule != null) ledger?.RegisterSurgery(__state.Victim, __state.Map, rule);
            if (rule == "bump:organ") ledger?.Refresh();
        }

        public static Exception Finalizer(Exception __exception, SurgeryState __state)
        {
            if (__state != null) CurrentOperation = __state.Previous;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Recipe_Surgery), "CheckSurgeryFail")]
    public static class PrisonerSurgeryFailurePatch
    {
        public static void Postfix(Pawn patient, bool __result)
        {
            var operation = PrisonerSurgeryPatch.CurrentOperation;
            if (operation != null && operation.Victim == patient && __result) operation.Failed = true;
        }
    }

    [HarmonyPatch(typeof(MedicalRecipesUtility), nameof(MedicalRecipesUtility.SpawnNaturalPartIfClean))]
    public static class PrisonerOrganHarvestPatch
    {
        public static void Postfix(Pawn pawn, BodyPartRecord part, Thing __result)
        {
            var operation = PrisonerSurgeryPatch.CurrentOperation;
            if (__result != null && operation != null && operation.Victim == pawn && operation.Part == part)
            {
                operation.Harvested = true;
                operation.HarvestedOrgan = __result;
            }
        }
    }

    [HarmonyPatch(typeof(ExecutionUtility), nameof(ExecutionUtility.DoExecutionByCut))]
    public static class PrisonerExecutionPatch
    {
        public static void Prefix(Pawn executioner, Pawn victim, out Map __state)
        {
            __state = victim != null && !victim.Dead && victim.IsPrisonerOfColony
                && executioner?.Faction == Faction.OfPlayer ? victim.MapHeld : null;
        }
        public static void Postfix(Pawn victim, Map __state)
        {
            if (__state != null && victim.Dead)
                Current.Game?.GetComponent<DeterrenceGameComponent>()?.RegisterExecution(victim, __state);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class DownedGearPatch
    {
        public static void Prefix(Pawn ___pawn)
            => Current.Game?.GetComponent<DeterrenceGameComponent>()?.CaptureDownedGear(___pawn);
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeUndowned")]
    public static class UndownedGearPatch
    {
        public static void Postfix(Pawn ___pawn)
            => Current.Game?.GetComponent<DeterrenceGameComponent>()?.ForgetGear(___pawn.thingIDNumber);
    }
}

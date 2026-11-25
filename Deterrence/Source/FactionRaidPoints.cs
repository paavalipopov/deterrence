using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Deterrence
{
    internal static class FactionRaidPoints
    {
        private sealed class StorytellerBudget
        {
            internal float ExpectedPoints;
            internal int MapId;
        }

        internal sealed class RaidAttempt
        {
            internal RaidAttempt Previous;
            internal IncidentParms Parms;
            internal bool Applied;
            internal float OriginalPoints;
        }

        private sealed class Evaluation
        {
            internal Map Map;
            internal DeterrenceGameComponent.ResolvedFaction Profile;
            internal bool ReadWealth;
        }

        private static readonly ConditionalWeakTable<IncidentParms, StorytellerBudget> Budgets =
            new ConditionalWeakTable<IncidentParms, StorytellerBudget>();
        [ThreadStatic] private static RaidAttempt attempt;
        [ThreadStatic] private static Evaluation evaluation;

        internal static DeterrenceGameComponent.ResolvedFaction ProfileForEvaluation(Map map)
        {
            if (evaluation == null || evaluation.Map != map) return null;
            evaluation.ReadWealth = true;
            return evaluation.Profile;
        }

        internal static void Track(IncidentParms parms)
        {
            if (parms?.target is Map map && map.IsPlayerHome && !map.IsPocketMap && parms.points > 0
                && DeterrenceMod.Settings.FactionOverridesEnabled)
                Remember(parms, parms.points, map.uniqueID);
        }

        private static void Remember(IncidentParms parms, float points, int mapId)
        {
            var budget = Budgets.GetValue(parms, key => new StorytellerBudget());
            budget.ExpectedPoints = points;
            budget.MapId = mapId;
        }

        // Randy applies his random multiplier after obtaining default incident parameters.
        internal static void TrackRandomMainMultiplier(IncidentParms parms)
        {
            if (parms != null && Budgets.TryGetValue(parms, out var budget)) budget.ExpectedPoints = parms.points;
        }

        internal static void CopyBudget(IncidentParms source, IncidentParms copy)
        {
            if (copy != null && Budgets.TryGetValue(source, out var budget))
                Remember(copy, budget.ExpectedPoints, budget.MapId);
        }

        internal static void ExposeBudget(IncidentParms parms)
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (!Budgets.TryGetValue(parms, out var budget) || budget.ExpectedPoints != parms.points) return;
                Scribe_Values.Look(ref budget.ExpectedPoints, "deterrenceStorytellerPoints");
                Scribe_Values.Look(ref budget.MapId, "deterrenceStorytellerMap", -1);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                float points = 0;
                int mapId = -1;
                Scribe_Values.Look(ref points, "deterrenceStorytellerPoints");
                Scribe_Values.Look(ref mapId, "deterrenceStorytellerMap", -1);
                if (points > 0 && mapId >= 0) Remember(parms, points, mapId);
            }
        }

        internal static RaidAttempt BeginRaid(IncidentWorker_Raid worker, IncidentParms parms)
        {
            if (!(worker is IncidentWorker_RaidEnemy) || !DeterrenceMod.Settings.FactionOverridesEnabled
                || parms == null || parms.quest != null || parms.questScriptDef != null || !string.IsNullOrEmpty(parms.questTag)
                || !Budgets.TryGetValue(parms, out var budget) || budget.ExpectedPoints != parms.points
                || !(parms.target is Map map) || map.uniqueID != budget.MapId) return null;
            var frame = new RaidAttempt { Previous = attempt, Parms = parms, OriginalPoints = parms.points };
            attempt = frame;
            return frame;
        }

        internal static void EndRaid(RaidAttempt frame, bool succeeded)
        {
            if (frame == null) return;
            attempt = frame.Previous;
            if (!frame.Applied) return;
            if (succeeded) Budgets.Remove(frame.Parms);
            else frame.Parms.points = frame.OriginalPoints;
        }

        internal static void ApplyForResolvedFaction(IncidentParms parms)
        {
            if (attempt == null || attempt.Parms != parms || attempt.Applied || parms.points != attempt.OriginalPoints
                || !(parms.target is Map map) || !map.IsPlayerHome || map.IsPocketMap
                || Find.Storyteller?.difficulty.fixedWealthMode != false) return;
            var profile = Current.Game?.GetComponent<DeterrenceGameComponent>()?.FactionSettingsFor(parms.faction?.def?.defName);
            if (profile == null) return;

            float globalPoints = Evaluate(map, null, out bool readGlobal);
            float factionPoints = Evaluate(map, profile, out bool readFaction);
            if (!readGlobal || !readFaction) return;
            float points = SourceMath.AdjustRaidBudget(parms.points, globalPoints, factionPoints);
            if (points == parms.points) return;
            parms.points = points;
            attempt.Applied = true;
        }

        private static float Evaluate(Map map, DeterrenceGameComponent.ResolvedFaction profile, out bool readWealth)
        {
            var previous = evaluation;
            var context = new Evaluation { Map = map, Profile = profile };
            Rand.PushState();
            evaluation = context;
            try
            {
                return StorytellerUtility.DefaultThreatPointsNow(map);
            }
            finally
            {
                readWealth = context.ReadWealth;
                evaluation = previous;
                Rand.PopState();
            }
        }
    }

    [HarmonyPatch(typeof(StorytellerUtility), nameof(StorytellerUtility.DefaultParmsNow))]
    internal static class TrackDefaultStorytellerBudgetPatch
    {
        private static void Postfix(IncidentCategoryDef incCat, IncidentParms __result)
        {
            if (incCat?.needsParmsPoints == true) FactionRaidPoints.Track(__result);
        }
    }

    [HarmonyPatch(typeof(StorytellerComp_RandomMain), nameof(StorytellerComp_RandomMain.GenerateParms))]
    internal static class TrackRandomMainBudgetPatch
    {
        private static void Postfix(IncidentParms __result) => FactionRaidPoints.TrackRandomMainMultiplier(__result);
    }

    [HarmonyPatch(typeof(IncidentParms), nameof(IncidentParms.ShallowCopy))]
    internal static class CopyStorytellerBudgetPatch
    {
        private static void Postfix(IncidentParms __instance, IncidentParms __result)
            => FactionRaidPoints.CopyBudget(__instance, __result);
    }

    [HarmonyPatch(typeof(IncidentParms), nameof(IncidentParms.ExposeData))]
    internal static class SaveStorytellerBudgetPatch
    {
        private static void Postfix(IncidentParms __instance) => FactionRaidPoints.ExposeBudget(__instance);
    }

    [HarmonyPatch(typeof(IncidentWorker_Raid), "TryGenerateRaidInfo")]
    internal static class FactionRaidAttemptPatch
    {
        private static void Prefix(IncidentWorker_Raid __instance, IncidentParms parms, out FactionRaidPoints.RaidAttempt __state)
            => __state = FactionRaidPoints.BeginRaid(__instance, parms);

        private static Exception Finalizer(bool __result, Exception __exception, FactionRaidPoints.RaidAttempt __state)
        {
            FactionRaidPoints.EndRaid(__state, __exception == null && __result);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryResolveRaidFaction")]
    internal static class ApplyFactionRaidBudgetPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(IncidentParms parms, bool __result)
        {
            if (__result) FactionRaidPoints.ApplyForResolvedFaction(parms);
        }
    }
}

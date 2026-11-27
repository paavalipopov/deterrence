using System;
using System.Collections.Generic;
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

        internal sealed class Evaluation
        {
            internal Map Map;
            internal DeterrenceGameComponent.ResolvedFaction Profile;
            // The raiding faction whose own dead weigh more, when that weighting applies.
            internal Faction Faction;
            internal bool ReadWealth;
        }

        // Threats that are not raids still use the profile of the faction they belong to.
        private static readonly Dictionary<string, string> ThreatFactions = new Dictionary<string, string>
        {
            { "Infestation", "Insect" }, { "MechCluster", "Mechanoid" },
            { "DefoliatorShipPartCrash", "Mechanoid" }, { "PsychicEmanatorShipPartCrash", "Mechanoid" }
        };

        private static readonly ConditionalWeakTable<IncidentParms, StorytellerBudget> Budgets =
            new ConditionalWeakTable<IncidentParms, StorytellerBudget>();
        [ThreadStatic] private static RaidAttempt attempt;
        [ThreadStatic] private static Evaluation evaluation;

        internal static Evaluation ContextFor(Map map)
        {
            if (evaluation == null || evaluation.Map != map) return null;
            evaluation.ReadWealth = true;
            return evaluation;
        }

        internal static void Track(IncidentParms parms)
        {
            if (parms?.target is Map map && map.IsPlayerHome && !map.IsPocketMap && parms.points > 0)
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
            if (!(worker is IncidentWorker_RaidEnemy) || !IsStorytellerBudget(parms)
                || parms.quest != null || parms.questScriptDef != null || !string.IsNullOrEmpty(parms.questTag)) return null;
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

        // Points still match the storyteller's budget for this colony map.
        private static bool IsStorytellerBudget(IncidentParms parms)
            => parms != null && Budgets.TryGetValue(parms, out var budget) && budget.ExpectedPoints == parms.points
                && parms.target is Map map && map.uniqueID == budget.MapId;

        private static bool Adjustable(IncidentParms parms, out Map map)
        {
            map = parms?.target as Map;
            return map != null && map.IsPlayerHome && !map.IsPocketMap && Find.Storyteller?.difficulty.fixedWealthMode == false;
        }

        // Returns false when the resolved faction skips this raid.
        internal static bool ApplyForResolvedFaction(IncidentParms parms)
        {
            if (attempt == null || attempt.Parms != parms || attempt.Applied || parms.points != attempt.OriginalPoints
                || !Adjustable(parms, out var map)) return true;
            var global = DeterrenceMod.Settings;
            var profile = Current.Game?.GetComponent<DeterrenceGameComponent>()?.FactionSettingsFor(parms.faction?.def?.defName);
            var remembering = global.OwnDeadWeight > 1 ? parms.faction : null;

            if (profile != null || remembering != null)
            {
                float globalPoints = Evaluate(map, null, null, out bool readGlobal);
                float factionPoints = Evaluate(map, profile, remembering, out bool readFaction);
                float points = readGlobal && readFaction ? SourceMath.AdjustRaidBudget(parms.points, globalPoints, factionPoints) : parms.points;
                if (points != parms.points)
                {
                    parms.points = points;
                    attempt.Applied = true;
                }
            }

            // Human raiders think twice in proportion to the wealth deterred for their faction.
            if (global.RaidSkipStrength <= 0 || parms.faction?.def?.humanlikeFaction != true) return true;
            double chance = SourceMath.SkipChance(ThreatRatio(map, profile, remembering), global.RaidSkipStrength, global.SkipExponent);
            if (!Rand.ChanceSeeded((float)chance, Gen.HashCombineInt(Find.TickManager.TicksGame, map.uniqueID))) return true;
            if (Prefs.DevMode) Log.Message("[Deterrence] Skipped a raid by " + parms.faction.Name + " (" + chance.ToString("P0") + " chance).");
            return false;
        }

        // Returns the original points when they were changed, otherwise NaN.
        internal static float ApplyForThreatSource(IncidentWorker worker, IncidentParms parms)
        {
            if (worker is IncidentWorker_Raid || worker?.def == null || !ThreatFactions.TryGetValue(worker.def.defName, out string factionKey)
                || !IsStorytellerBudget(parms) || !Adjustable(parms, out var map)) return float.NaN;
            var profile = Current.Game?.GetComponent<DeterrenceGameComponent>()?.FactionSettingsFor(factionKey);
            if (profile == null) return float.NaN;
            float globalPoints = Evaluate(map, null, null, out bool readGlobal);
            float sourcePoints = Evaluate(map, profile, null, out bool readSource);
            if (!readGlobal || !readSource) return float.NaN;
            float points = SourceMath.AdjustRaidBudget(parms.points, globalPoints, sourcePoints);
            if (points == parms.points) return float.NaN;
            float original = parms.points;
            parms.points = points;
            return original;
        }

        // Threat / Base for the map, as seen by a faction profile and its own dead.
        internal static double ThreatRatio(Map map, DeterrenceGameComponent.ResolvedFaction profile, Faction faction)
        {
            var previous = evaluation;
            evaluation = new Evaluation { Map = map, Profile = profile, Faction = faction };
            float threat;
            try { threat = map.PlayerWealthForStoryteller; }
            finally { evaluation = previous; }

            float vanilla;
            ThreatWealthPatch.Bypass = true;
            try { vanilla = map.PlayerWealthForStoryteller; }
            finally { ThreatWealthPatch.Bypass = false; }

            var settings = profile?.Settings ?? DeterrenceMod.Settings;
            double surface = Current.Game?.GetComponent<DeterrenceGameComponent>()?.SurfaceDiscountFor(map, settings.SmoothedSurfaceMultiplier) ?? 0;
            return SourceMath.ThreatRatio(threat, CreditMath.EffectiveWealth(vanilla, surface));
        }

        private static float Evaluate(Map map, DeterrenceGameComponent.ResolvedFaction profile, Faction faction, out bool readWealth)
        {
            var previous = evaluation;
            var context = new Evaluation { Map = map, Profile = profile, Faction = faction };
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
        private static void Postfix(IncidentParms parms, ref bool __result)
        {
            if (__result && !FactionRaidPoints.ApplyForResolvedFaction(parms)) __result = false;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    internal static class ThreatSourceBudgetPatch
    {
        private static void Prefix(IncidentWorker __instance, IncidentParms parms, out float __state)
            => __state = FactionRaidPoints.ApplyForThreatSource(__instance, parms);

        // A failed attempt keeps the storyteller's budget for any retry.
        private static void Postfix(IncidentParms parms, bool __result, float __state)
        {
            if (!__result && !float.IsNaN(__state)) parms.points = __state;
        }
    }
}

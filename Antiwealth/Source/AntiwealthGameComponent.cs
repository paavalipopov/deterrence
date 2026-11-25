using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Antiwealth
{
    public class AntiwealthGameComponent : GameComponent
    {
        public const int SampleInterval = 2500;
        private readonly DeterrenceLedger globalLedger = new DeterrenceLedger(true);
        private List<DeterrenceLedger> factionLedgers = new List<DeterrenceLedger>();
        private readonly Dictionary<string, ResolvedFaction> factionRuntime = new Dictionary<string, ResolvedFaction>();

        internal sealed class ResolvedFaction
        {
            internal AntiwealthSettings Settings;
            internal DeterrenceLedger Ledger;
        }

        private int lastSampleTick = -1;
        private bool sampling;
        private List<GearSnapshot> gearSnapshots = new List<GearSnapshot>();
        private readonly Dictionary<int, GearSnapshot> gearByPawn = new Dictionary<int, GearSnapshot>();
        private List<WealthSample> wealthHistory = new List<WealthSample>();
        private readonly Dictionary<int, WealthSample> lastHistory = new Dictionary<int, WealthSample>();
        public int HistoryRevision { get; private set; }
        public int LedgerRevision { get; private set; }
        private readonly Dictionary<int, double> surfaceWealth = new Dictionary<int, double>();
        public Thing TargetFor(int thingId) => globalLedger.TargetFor(thingId);
        public double SurfaceDiscountFor(Map map)
            => SurfaceDiscountFor(map, AntiwealthMod.Settings.SmoothedSurfaceMultiplier);
        internal double SurfaceDiscountFor(Map map, float multiplier) => SourceMath.SurfaceDiscount(
            surfaceWealth.TryGetValue(map.uniqueID, out var value) ? value : 0, multiplier);
        public IEnumerable<OrganStockValues> OrganStockFor(int mapId) => globalLedger.OrganStockFor(mapId);
        private List<int> creditedDeaths = new List<int>();
        private readonly HashSet<int> deathIds = new HashSet<int>();

        public AntiwealthGameComponent(Game game) { }
        public IEnumerable<LedgerEntry> Entries => globalLedger.Entries;
        public static double Now => Find.TickManager.TicksGame / 60000.0;
        public IEnumerable<WealthSample> HistoryFor(Map map) => wealthHistory.Where(s => s.MapId == map.uniqueID);

        public double CreditFor(Map map) => globalLedger.CreditFor(map);

        internal ResolvedFaction FactionSettingsFor(string factionKey)
        {
            if (!AntiwealthMod.Settings.FactionOverridesEnabled || string.IsNullOrEmpty(factionKey)) return null;
            return factionRuntime.TryGetValue(factionKey, out var scope) ? scope : null;
        }

        private void ResolveFactions(AntiwealthSettings global, double day)
        {
            factionRuntime.Clear();
            var availableLedgers = factionLedgers.Where(ledger => ledger != null && !string.IsNullOrEmpty(ledger.ProfileId))
                .GroupBy(ledger => ledger.ProfileId).ToDictionary(group => group.Key, group => group.First());
            var observedLedgers = new List<DeterrenceLedger>();
            foreach (var profile in global.FactionOverrides)
            {
                if (profile.Count == 0 || DefDatabase<FactionDef>.GetNamedSilentFail(profile.FactionKey) == null) continue;
                var ledger = globalLedger;
                if (profile.HasLedgerOverrides)
                {
                    if (!availableLedgers.TryGetValue(profile.Id, out ledger)) ledger = globalLedger.Branch(profile.Id, day);
                    observedLedgers.Add(ledger);
                }
                factionRuntime.Add(profile.FactionKey, new ResolvedFaction { Settings = profile.Resolve(global), Ledger = ledger });
            }
            factionLedgers = observedLedgers;
        }

        public override void ExposeData()
        {
            globalLedger.ExposeEntries("antiwealthEntries");
            Scribe_Collections.Look(ref factionLedgers, "antiwealthFactionLedgers", LookMode.Deep);
            Scribe_Values.Look(ref lastSampleTick, "antiwealthLastSampleTick", -1);
            Scribe_Collections.Look(ref gearSnapshots, "antiwealthGearSnapshots", LookMode.Deep);
            Scribe_Collections.Look(ref wealthHistory, "antiwealthWealthHistory", LookMode.Deep);
            Scribe_Collections.Look(ref creditedDeaths, "antiwealthCreditedDeaths", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                factionLedgers = factionLedgers ?? new List<DeterrenceLedger>();
                LedgerRevision++;
                gearSnapshots = gearSnapshots ?? new List<GearSnapshot>();
                gearByPawn.Clear();
                gearSnapshots.RemoveAll(s => s == null);
                foreach (var snapshot in gearSnapshots.ToArray())
                {
                    if (gearByPawn.ContainsKey(snapshot.PawnId)) gearSnapshots.Remove(snapshot);
                    else gearByPawn.Add(snapshot.PawnId, snapshot);
                }
                wealthHistory = wealthHistory ?? new List<WealthSample>();
                wealthHistory.RemoveAll(s => s == null || !CreditMath.Finite(s.Day));
                lastHistory.Clear();
                foreach (var sample in wealthHistory.OrderBy(s => s.Day)) lastHistory[sample.MapId] = sample;
                deathIds.Clear();
                creditedDeaths = creditedDeaths ?? new List<int>();
                foreach (int id in creditedDeaths) deathIds.Add(id);
                HistoryRevision++;
            }
        }

        public override void FinalizeInit()
        {
            // Loading a save never awards extra time or resets an existing recovery curve.
            Refresh(true);
        }

        public override void GameComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (lastSampleTick < 0 || tick - lastSampleTick >= SampleInterval) Refresh();
        }

        public void Refresh(bool forceHistory = false)
        {
            if (sampling || Current.Game == null || Find.TickManager == null) return;
            sampling = true;
            try
            {
                var settings = AntiwealthMod.Settings;
                settings.Sanitize();
                double day = Now;
                var samples = Find.Maps.Where(map => map.IsPlayerHome && !map.Disposed && !map.IsPocketMap)
                    .Select(map => new MapDeterrenceSources(map)).ToArray();
                globalLedger.BeginSample(day);
                foreach (var sample in samples) globalLedger.ObserveMap(sample, day, settings);
                globalLedger.FinishSample(day, settings);
                ResolveFactions(settings, day);
                var scopes = factionRuntime.Values.Where(scope => scope.Ledger != globalLedger).ToArray();
                foreach (var scope in scopes) scope.Ledger.BeginSample(day);
                bool countSurfaces = settings.SmoothedSurfaceMultiplier < 1
                    || factionRuntime.Values.Any(scope => scope.Settings.SmoothedSurfaceMultiplier < 1);
                surfaceWealth.Clear();
                foreach (var sample in samples)
                {
                    foreach (var scope in scopes) scope.Ledger.ObserveMap(sample, day, scope.Settings);
                    if (countSurfaces) surfaceWealth[sample.Map.uniqueID] = SurfaceWealth.Count(sample.Map);
                }
                foreach (var scope in scopes) scope.Ledger.FinishSample(day, scope.Settings);
                LedgerRevision++;
                lastSampleTick = Find.TickManager.TicksGame;
                RecordHistory(day, forceHistory);
                CleanGearSnapshots();
            }
            finally { sampling = false; }
        }

        internal static bool IsBuried(Corpse corpse)
        {
            for (var holder = corpse.ParentHolder; holder != null; holder = holder.ParentHolder)
                if (holder is Building_Grave) return true;
            return false;
        }

        internal static bool IsKeptOrgan(Thing thing, Map map)
        {
            if (thing == null || thing.Destroyed || thing.MapHeld != map || !thing.SpawnedOrAnyParentSpawned
                || thing.TryGetComp<CompPrisonerOrgan>()?.HarvestedLocallyFromPrisoner != true) return false;
            for (var holder = thing.ParentHolder; holder != null; holder = holder.ParentHolder)
                if (holder is Pawn pawn && pawn.Faction != Faction.OfPlayer) return false;
            return true;
        }

        internal static double GearValue(Pawn pawn)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike) return 0;
            double value = 0;
            if (pawn.equipment != null)
                foreach (var item in pawn.equipment.AllEquipmentListForReading)
                    value += CreditMath.Positive(item.MarketValue) * item.stackCount;
            if (pawn.apparel != null)
                foreach (var item in pawn.apparel.WornApparel)
                    value += CreditMath.Positive(item.MarketValue) * item.stackCount;
            return value;
        }

        internal void CaptureDownedGear(Pawn pawn)
        {
            if (pawn?.MapHeld == null || !pawn.MapHeld.IsPlayerHome || !pawn.RaceProps.Humanlike || !Eligible(pawn)) return;
            if (gearByPawn.ContainsKey(pawn.thingIDNumber)) return;
            var snapshot = new GearSnapshot { PawnId = pawn.thingIDNumber, Value = GearValue(pawn) };
            gearSnapshots.Add(snapshot); gearByPawn.Add(snapshot.PawnId, snapshot);
        }

        internal double GearForDeath(Pawn pawn)
            => gearByPawn.TryGetValue(pawn.thingIDNumber, out var snapshot) ? snapshot.Value : GearValue(pawn);

        internal void ForgetGear(int pawnId)
        {
            if (!gearByPawn.TryGetValue(pawnId, out var snapshot)) return;
            gearByPawn.Remove(pawnId); gearSnapshots.Remove(snapshot);
        }

        private void CleanGearSnapshots()
        {
            if (gearSnapshots.Count == 0) return;
            var present = new HashSet<int>(Find.Maps.Where(m => m.IsPlayerHome)
                .SelectMany(m => m.mapPawns.AllPawns).Where(p => p.Downed && !p.Dead).Select(p => p.thingIDNumber));
            foreach (var snapshot in gearSnapshots.ToArray())
                if (!present.Contains(snapshot.PawnId)) ForgetGear(snapshot.PawnId);
        }

        private void RecordHistory(double day, bool force)
        {
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || map.Disposed || map.IsPocketMap) continue;
                lastHistory.TryGetValue(map.uniqueID, out var previous);
                if (previous != null && !HistoryMath.Due(previous.Day, day, force)) continue;
                var sample = new WealthSample
                {
                    MapId = map.uniqueID, Day = (float)day,
                    Economy = map.wealthWatcher.WealthTotal,
                    VanillaThreat = map.wealthWatcher.WealthItems + 0.5f * map.wealthWatcher.WealthBuildings + map.wealthWatcher.WealthPawns,
                    SurfaceDiscount = Find.Storyteller.difficulty.fixedWealthMode ? 0 : (float)SurfaceDiscountFor(map),
                    AdjustedThreat = map.PlayerWealthForStoryteller,
                    Antiwealth = (float)CreditFor(map)
                };
                if (previous != null && HistoryMath.SameTimestamp(sample.Day, previous.Day))
                {
                    previous.Economy = sample.Economy; previous.VanillaThreat = sample.VanillaThreat;
                    previous.SurfaceDiscount = sample.SurfaceDiscount;
                    previous.AdjustedThreat = sample.AdjustedThreat; previous.Antiwealth = sample.Antiwealth;
                }
                else { wealthHistory.Add(sample); lastHistory[map.uniqueID] = sample; }
                HistoryRevision++;
            }
        }

        internal static bool Eligible(Pawn pawn)
        {
            if (pawn == null || pawn.Faction == Faction.OfPlayer) return false;
            if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer)) return true;
            if (!pawn.RaceProps.Animal) return false;
            string mental = pawn.MentalStateDef?.defName;
            if (mental == "Manhunter" || mental == "ManhunterPermanent") return true;
            return pawn.health.hediffSet.hediffs.Any(h => h.def.defName == "Scaria");
        }

        private bool RecordAct(Map map, string note, string sourceKey, Func<AntiwealthSettings, double> amountFor,
            out double globalAmount)
        {
            double day = Now;
            var global = AntiwealthMod.Settings;
            globalAmount = amountFor(global);
            bool awarded = globalLedger.AddReputation(map, note, globalAmount, sourceKey, global, day);
            if (awarded) LedgerRevision++;
            else globalAmount = 0;
            foreach (var scope in factionRuntime.Values)
                if (scope.Ledger != globalLedger)
                    awarded |= scope.Ledger.AddReputation(map, note, amountFor(scope.Settings), sourceKey, scope.Settings, day);
            return awarded;
        }

        private bool RecordFixedAct(Map map, string note, string sourceKey, out double globalAmount)
        {
            int population = map.mapPawns.FreeColonistsCount;
            return RecordAct(map, note, sourceKey, settings =>
                settings.Rule(sourceKey, settings.LegacyDefault(sourceKey)).AmountFor(population), out globalAmount);
        }

        internal double RegisterRitual(Map map, string name)
        {
            RecordFixedAct(map, "Ritual: " + name, "bump:ritual", out double globalAmount);
            return globalAmount;
        }

        internal void RegisterSurgery(Pawn pawn, Map map, string sourceKey)
        {
            if (pawn == null || map == null || sourceKey == null) return;
            string label = sourceKey == "bump:organ" ? "Prisoner organ harvest"
                : sourceKey == "bump:failedSurgery" ? "Failed prisoner removal surgery" : "Prisoner amputation";
            RecordFixedAct(map, label + ": " + pawn.LabelShort, sourceKey, out _);
        }

        internal void RegisterExecution(Pawn pawn, Map map)
        {
            if (pawn == null || map == null || deathIds.Contains(pawn.thingIDNumber)) return;
            if (RecordFixedAct(map, "Prisoner execution: " + pawn.LabelShort, "bump:execution", out _))
            {
                deathIds.Add(pawn.thingIDNumber);
                creditedDeaths.Add(pawn.thingIDNumber);
            }
        }

        // Eligibility and original gear are captured before death clears mental/guest state.
        internal void RegisterDeath(Pawn pawn, Map map, float combatPower, double gearValue, bool wasPrisoner)
        {
            if (pawn == null || !pawn.Dead || map == null || !map.IsPlayerHome) return;
            string kind = AntiwealthSettings.RemainsKey(pawn).Substring(8);
            if (!wasPrisoner && !deathIds.Contains(pawn.thingIDNumber))
            {
                int population = map.mapPawns.FreeColonistsCount;
                string sourceKey = "bump:" + kind;
                if (RecordAct(map, "Hostile death: " + pawn.LabelShort, sourceKey, settings =>
                    SourceMath.DeathBump(combatPower, settings.Rule(sourceKey, settings.LegacyDefault(sourceKey)), population,
                        pawn.RaceProps.Humanlike && settings.IncludeHumanGear ? gearValue : 0, settings.HumanGearFraction), out _))
                {
                    deathIds.Add(pawn.thingIDNumber);
                    creditedDeaths.Add(pawn.thingIDNumber);
                }
            }
            double day = Now;
            globalLedger.RegisterBody(pawn, map, combatPower, kind, AntiwealthMod.Settings, day);
            foreach (var scope in factionRuntime.Values)
                if (scope.Ledger != globalLedger) scope.Ledger.RegisterBody(pawn, map, combatPower, kind, scope.Settings, day);
            LedgerRevision++;
        }
    }
}

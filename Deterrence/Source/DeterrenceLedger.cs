using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Deterrence
{
    internal sealed class DeterrenceLedger : IExposable
    {
        private List<LedgerEntry> entries = new List<LedgerEntry>();
        private readonly Dictionary<string, LedgerEntry> byKey = new Dictionary<string, LedgerEntry>();
        private readonly Dictionary<int, double> totals = new Dictionary<int, double>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly Dictionary<int, Thing> sourceTargets = new Dictionary<int, Thing>();
        private readonly Dictionary<int, List<OrganStockValues>> organStock = new Dictionary<int, List<OrganStockValues>>();
        private readonly bool trackDetails;
        public string ProfileId;

        public DeterrenceLedger() { }
        public DeterrenceLedger(bool trackDetails) => this.trackDetails = trackDetails;

        internal IEnumerable<LedgerEntry> Entries => entries;
        internal Thing TargetFor(int id) => sourceTargets.TryGetValue(id, out var thing) ? thing : null;
        internal IEnumerable<OrganStockValues> OrganStockFor(int mapId)
            => organStock.TryGetValue(mapId, out var stock) ? stock : Enumerable.Empty<OrganStockValues>();
        internal double CreditFor(Map map)
            => map != null && totals.TryGetValue(map.uniqueID, out double credit) ? credit : 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ProfileId, "profile");
            ExposeEntries("entries");
        }

        internal void ExposeEntries(string key)
        {
            Scribe_Collections.Look(ref entries, key, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) IndexEntries();
        }

        private void IndexEntries()
        {
            entries = entries ?? new List<LedgerEntry>();
            byKey.Clear();
            entries.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.Key));
            DiscardRemovedBalances();
            entries.RemoveAll(entry =>
            {
                if (byKey.ContainsKey(entry.Key)) return true;
                byKey.Add(entry.Key, entry);
                return false;
            });
            RebuildTotals();
        }

        internal DeterrenceLedger Branch(string profileId, double day)
        {
            var branch = new DeterrenceLedger { ProfileId = profileId,
                entries = entries.Select(entry => entry.CopyForFaction(day)).ToList() };
            branch.IndexEntries();
            return branch;
        }

        internal void BeginSample(double day)
        {
            foreach (var entry in entries) entry.Advance(day);
            seen.Clear();
            sourceTargets.Clear();
            organStock.Clear();
        }

        internal void ObserveMap(MapDeterrenceSources sample, double day, DeterrenceSettings settings)
        {
            if (settings.Corpses) ObserveCorpses(sample, day, settings);
            ObserveOrganStock(sample, day, settings);
            if (!settings.Conditions) return;
            ObserveConditions(sample, day, settings);
            ObservePollution(sample, day, settings);
            ObserveLocalEnvironment(sample, day, settings);
        }

        internal void FinishSample(double day, DeterrenceSettings settings)
        {
            EndUnseenSources(day, settings);
            entries.RemoveAll(entry => {
                if (entry.Active || entry.Balance > 0) return false;
                byKey.Remove(entry.Key);
                return true;
            });
            RebuildTotals();
        }

        internal void EndUnseenSources(double day, DeterrenceSettings settings)
        {
            // Stock is valued only while held. Its separate harvest reputation already fades.
            foreach (var entry in entries.Where(e => e.SourceKey == "organs:local" && !seen.Contains(e.Key)).ToArray())
            {
                entries.Remove(entry); byKey.Remove(entry.Key);
            }
            foreach (var entry in entries)
            {
                if (entry.Active && !seen.Contains(entry.Key))
                {
                    entry.Accrue(entry.LastRawRate, Math.Max(0, day - entry.LastSeenDay), settings.Growth);
                    bool useArea = settings.FadeByHistory && entry.HasArea && entry.LastRawRate > 0;
                    entry.End(day, settings.RecoveryDays, useArea ? settings.FadeReferenceArea : settings.RecoveryReference,
                        settings.RecoveryExponent, useArea: useArea);
                    entry.LastRawRate = 0;
                    entry.DailyGain = 0;
                    if (entry.CorpseId >= 0) { entry.Buried = false; entry.Quantity = 0; }
                }
            }
        }

        internal int DiscardRemovedBalances()
        {
            int count = 0;
            foreach (var entry in entries.ToArray())
            {
                if (entry.Key.StartsWith("condition:", StringComparison.Ordinal) && string.IsNullOrEmpty(entry.SourceKey))
                {
                    // Earlier saves used incident IDs, without persisting the condition definition.
                    var def = DefDatabase<GameConditionDef>.AllDefsListForReading.FirstOrDefault(d =>
                        string.Equals(entry.Label, d.LabelCap.ToString(), StringComparison.OrdinalIgnoreCase)
                        || (d.defName == "PsychicDrone" && entry.Label != null
                            && entry.Label.StartsWith(d.LabelCap.ToString(), StringComparison.OrdinalIgnoreCase)));
                    if (def != null) entry.SourceKey = "condition:" + def.defName;
                }
                if (RuleGrouping.IsIllness(entry.Key, entry.Category)
                    || RuleGrouping.IsRemovedSource(entry.Key) || RuleGrouping.IsRemovedSource(entry.SourceKey))
                {
                    entries.Remove(entry); byKey.Remove(entry.Key); count++; continue;
                }
                if (entry.Key.StartsWith("condition:", StringComparison.Ordinal)
                    && entry.SourceKey != null && entry.SourceKey.StartsWith("condition:", StringComparison.Ordinal))
                {
                    string canonical = RuleGrouping.ConditionMeter(entry.MapId, entry.SourceKey.Substring(10));
                    if (entry.Key != canonical && !entries.Any(e => e.Key == canonical))
                    {
                        bool indexed = byKey.Remove(entry.Key);
                        entry.Key = canonical;
                        if (indexed) byKey[canonical] = entry;
                    }
                }
            }
            if (count > 0) RebuildTotals();
            return count;
        }

        private LedgerEntry Observe(string key, string label, string category, Map map, double day)
        {
            if (!byKey.TryGetValue(key, out var entry))
            {
                entry = new LedgerEntry { Key = key, LastSeenDay = day };
                entries.Add(entry);
                byKey.Add(key, entry);
            }
            entry.Activate(day);
            entry.Label = label;
            entry.Category = category;
            entry.MapId = map.uniqueID;
            seen.Add(key);
            return entry;
        }

        private void ObserveOngoing(string key, string label, string category, Map map,
            double day, double rate, DeterrenceSettings settings, string sourceKey = null)
        {
            bool wasActive = byKey.TryGetValue(key, out var old) && old.Active;
            double elapsed = wasActive ? Math.Max(0, day - old.LastSeenDay) : 0;
            var entry = Observe(key, label, category, map, day);
            entry.SourceKey = sourceKey ?? key;
            var growth = settings.Growth;
            entry.Accrue(entry.LastRawRate > 0 ? entry.LastRawRate : rate, elapsed, growth);
            entry.LastRawRate = rate;
            entry.DailyGain = growth.DailyRate(rate, entry.Exposure, entry.RateClock);
            entry.LastSeenDay = day;
        }

        private void ObserveCorpses(MapDeterrenceSources sample, double day, DeterrenceSettings settings)
        {
            var map = sample.Map;
            foreach (var thing in sample.Corpses)
            {
                var corpse = thing as Corpse;
                if (corpse == null || corpse.Destroyed || corpse.InnerPawn == null || !corpse.SpawnedOrAnyParentSpawned) continue;
                string key = "remains:" + corpse.InnerPawn.thingIDNumber;
                bool known = byKey.TryGetValue(key, out var existing) && existing.CorpseId == corpse.thingIDNumber;
                if (!known && !DeterrenceGameComponent.EligibleRemains(corpse.InnerPawn)) continue;
                var rule = settings.Rule(DeterrenceSettings.RemainsKey(corpse.InnerPawn), settings.CorpsePerCombatPower);
                if (!rule.Enabled) continue;
                double power = known ? existing.CombatPower : Math.Max(1, corpse.InnerPawn.kindDef.combatPower);
                double credit = SourceMath.MaintainedRemains(power, rule, sample.Population);
                if (credit <= 0) continue;
                var entry = Observe(key, corpse.InnerPawn.LabelShort + " (remains)", "Remains", map, day);
                entry.CorpseId = corpse.thingIDNumber;
                if (trackDetails) sourceTargets[corpse.thingIDNumber] = corpse;
                entry.Buried = DeterrenceGameComponent.IsBuried(corpse);
                entry.Quantity = 1;
                entry.CombatPower = (float)power;
                entry.SourceKey = DeterrenceSettings.RemainsKey(corpse.InnerPawn);
                entry.Balance = credit;
                entry.DailyGain = 0;
                entry.LastSeenDay = day;
            }
        }

        private void ObserveOrganStock(MapDeterrenceSources sample, double day, DeterrenceSettings settings)
        {
            var map = sample.Map;
            var details = trackDetails ? new List<OrganStockValues>() : null;
            if (trackDetails) organStock[map.uniqueID] = details;
            var rule = settings.Rule("organs:local", 0.75f);
            if (!rule.Enabled || rule.BaseAmount <= 0) return;
            double credit = 0;
            int count = 0;
            var counted = new HashSet<int>();
            foreach (var item in sample.Organs)
            {
                if (!counted.Add(item.thingIDNumber)) continue;
                double value = SourceMath.KeptOrgans(item.MarketValue, item.stackCount, rule.BaseAmount);
                details?.Add(new OrganStockValues { Key = "item:" + item.thingIDNumber, Label = item.LabelCap.ToString(),
                    Count = item.stackCount, MarketValue = item.MarketValue * item.stackCount, Credit = value, TargetId = item.thingIDNumber });
                if (trackDetails) sourceTargets[item.thingIDNumber] = item;
                credit += value;
                count += item.stackCount;
            }
            if (credit <= 0) return;
            var entry = Observe("organs:" + map.uniqueID, "Kept prisoner organs (" + count + ")", "Organs", map, day);
            entry.SourceKey = "organs:local";
            entry.Quantity = count;
            entry.Balance = credit;
            entry.DailyGain = 0;
            entry.LastSeenDay = day;
        }

        private void ObservePollution(MapDeterrenceSources sample, double day, DeterrenceSettings settings)
        {
            var map = sample.Map;
            if (!ModsConfig.BiotechActive || map.pollutionGrid == null) return;
            double fraction = map.pollutionGrid.TotalPollutionPercent;
            double fullRate = settings.Rule("pollution:ground", 4000).AmountFor(sample.Population);
            double rate = SourceMath.Pollution(fullRate, fraction);
            if (rate <= 0) return;
            ObserveOngoing("pollution:" + map.uniqueID, "Polluted ground (" + fraction.ToString("P1") + ")",
                "Pollution", map, day, rate, settings, "pollution:ground");
        }

        private void ObserveConditions(MapDeterrenceSources sample, double day, DeterrenceSettings settings)
        {
            var map = sample.Map;
            var types = new HashSet<string>();
            foreach (var condition in sample.Conditions)
            {
                if (!types.Add(condition.def.defName)) continue;
                double rate = ConditionRate(condition.def.defName, settings, sample.Population);
                if (rate <= 0) continue;
                ObserveOngoing(RuleGrouping.ConditionMeter(map.uniqueID, condition.def.defName),
                    condition.LabelCap, "Environment", map, day, rate, settings, "condition:" + condition.def.defName);
            }
        }

        internal static double ConditionRate(string defName, DeterrenceSettings settings, int population = 5)
        {
            if (!RuleGrouping.IsSupported("condition:" + defName)) return 0;
            settings.EnsureBuiltins();
            string key = "condition:" + defName;
            float fallback = settings.LegacyDefault(key);
            return settings.Rule(key, fallback, fallback > 0).AmountFor(population);
        }

        private void ObserveLocalEnvironment(MapDeterrenceSources sample, double day, DeterrenceSettings settings)
        {
            var map = sample.Map;
            int population = sample.Population;
            ObserveExposure("climate:heat", "Extreme outdoor heat", map, day,
                SourceMath.HeatExposure(map.mapTemperature.OutdoorTemp, settings.HeatThreshold), population, settings);
            ObserveExposure("climate:cold", "Extreme outdoor cold", map, day,
                SourceMath.ColdExposure(map.mapTemperature.OutdoorTemp, settings.ColdThreshold), population, settings);
            var infestation = settings.Rule("infestation:hives", 2000);
            if (infestation.Enabled && infestation.BaseAmount > 0)
            {
                int hives = sample.HiveCount;
                ObserveExposure("infestation:hives", "Hostile infestation (" + hives + " hives)", map, day, hives, population, settings);
            }
        }

        private void ObserveExposure(string key, string label, Map map, double day, double exposure,
            int population, DeterrenceSettings settings)
        {
            var rule = settings.Rule(key);
            double rate = rule.AmountFor(population) * CreditMath.Positive(exposure);
            if (rate <= 0) return;
            ObserveOngoing(key + ":" + map.uniqueID, label, "Environment", map, day, rate, settings, key);
        }

        internal bool AddReputation(Map map, string note, double amount, string sourceKey, DeterrenceSettings settings, double day)
        {
            if (map == null || !map.IsPlayerHome || !settings.Reputation || amount <= 0) return false;
            string key = RuleGrouping.ReputationMeter(map.uniqueID);
            if (!byKey.TryGetValue(key, out var entry))
            {
                entry = new LedgerEntry { Key = key, Label = "Reputation", Category = "Reputation",
                    SourceKey = "reputation:violence", MapId = map.uniqueID };
                entries.Add(entry); byKey.Add(key, entry);
            }
            entry.Advance(day);
            if (trackDetails) ShareMath.Add(entry.Shares, entry.Balance,
                new ReputationShare { Key = "act:" + (entry.EventsRecorded + 1), Label = note, SourceKey = sourceKey, Amount = amount, Day = day },
                () => new ReputationShare { Key = "earlier", Label = "Earlier reputation" });
            if (!entry.Bump(day, amount, settings.RecoveryDays, settings.RecoveryReference,
                settings.RecoveryExponent)) return false;
            entry.EventsRecorded++;
            entry.LastEvent = note;
            entry.LastSeenDay = day;
            RebuildTotals();
            return true;
        }

        internal void RegisterBody(Pawn pawn, Map map, float combatPower, string kind, DeterrenceSettings settings, double day)
        {
            var corpse = pawn.Corpse;
            if (corpse == null || !settings.Corpses) return;
            var rule = settings.Rule("remains:" + kind, settings.CorpsePerCombatPower);
            if (!rule.Enabled) return;
            string key = "remains:" + pawn.thingIDNumber;
            if (!byKey.TryGetValue(key, out var entry))
            {
                entry = new LedgerEntry { Key = key };
                entries.Add(entry); byKey.Add(key, entry);
            }
            entry.Label = pawn.LabelShort + " (remains)";
            entry.Category = "Remains";
            entry.SourceKey = "remains:" + kind;
            entry.MapId = map.uniqueID;
            entry.CorpseId = corpse.thingIDNumber;
            if (trackDetails) sourceTargets[corpse.thingIDNumber] = corpse;
            entry.Buried = DeterrenceGameComponent.IsBuried(corpse);
            entry.Quantity = 1;
            entry.CombatPower = Math.Max(1, combatPower);
            entry.Active = true;
            entry.LastSeenDay = day;
            entry.OriginalGearValue = 0;
            entry.GearSnapshotKnown = false;
            entry.Balance = SourceMath.MaintainedRemains(entry.CombatPower, rule, map.mapPawns.FreeColonistsCount);
            RebuildTotals();
        }

        private void RebuildTotals()
        {
            totals.Clear();
            foreach (var entry in entries)
            {
                totals.TryGetValue(entry.MapId, out double value);
                totals[entry.MapId] = value + CreditMath.Positive(entry.Balance);
            }
        }
    }
}

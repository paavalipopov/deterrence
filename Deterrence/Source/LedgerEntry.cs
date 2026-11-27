using Verse;

namespace Deterrence
{
    public class LedgerEntry : LedgerValues, IExposable
    {
        public double LastRawRate;
        public System.Collections.Generic.List<ReputationShare> Shares = new System.Collections.Generic.List<ReputationShare>();

        internal LedgerEntry CopyForFaction(double day)
        {
            var copy = (LedgerEntry)MemberwiseClone();
            copy.Shares = new System.Collections.Generic.List<ReputationShare>();
            // Start future accumulation at the time of the split. Existing fades retain their clock.
            if (copy.Active) copy.LastSeenDay = day;
            return copy;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Key, "key");
            Scribe_Values.Look(ref Label, "label");
            Scribe_Values.Look(ref Category, "category");
            Scribe_Values.Look(ref SourceKey, "sourceKey");
            Scribe_Values.Look(ref Buried, "buried");
            Scribe_Values.Look(ref Quantity, "quantity");
            Scribe_Collections.Look(ref Shares, "reputationShares", LookMode.Deep);
            Scribe_Values.Look(ref EventsRecorded, "eventsRecorded");
            Scribe_Values.Look(ref LastEvent, "lastEvent");
            Scribe_Values.Look(ref MapId, "mapId");
            Scribe_Values.Look(ref CorpseId, "corpseId", -1);
            Scribe_Values.Look(ref FactionId, "factionId", -1);
            Scribe_Values.Look(ref RawCredit, "rawCredit");
            Scribe_Values.Look(ref CombatPower, "combatPower");
            Scribe_Values.Look(ref OriginalGearValue, "originalGearValue");
            Scribe_Values.Look(ref GearSnapshotKnown, "gearSnapshotKnown");
            Scribe_Values.Look(ref LastSeenDay, "lastSeenDay");
            Scribe_Values.Look(ref Exposure, "exposure");
            Scribe_Values.Look(ref RateClock, "rateClock");
            Scribe_Values.Look(ref ActiveArea, "activeArea");
            Scribe_Values.Look(ref HasArea, "hasActiveArea");
            Scribe_Values.Look(ref LastRawRate, "lastRawRate");
            Scribe_Values.Look(ref Balance, "balance");
            Scribe_Values.Look(ref Active, "active");
            Scribe_Values.Look(ref EndDay, "endDay");
            Scribe_Values.Look(ref DecayFrom, "decayFrom");
            Scribe_Values.Look(ref DecayDuration, "decayDuration");
            Scribe_Values.Look(ref DecayCurve, "decayCurve", FadeCurve.Cubic);
            // DailyGain is derived by the next observation, never used as an accrual source.
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Shares = Shares ?? new System.Collections.Generic.List<ReputationShare>();
                Shares.RemoveAll(s => s == null || !CreditMath.Finite(s.Amount) || s.Amount <= 0);
                Balance = CreditMath.Positive(Balance);
                RawCredit = CreditMath.Positive(RawCredit);
                Exposure = CreditMath.Positive(Exposure);
                RateClock = CreditMath.Positive(RateClock);
                ActiveArea = CreditMath.Positive(ActiveArea);
                LastRawRate = CreditMath.Positive(LastRawRate);
                DecayFrom = CreditMath.Positive(DecayFrom);
                DecayDuration = CreditMath.Positive(DecayDuration);
                LastSeenDay = CreditMath.Positive(LastSeenDay);
                EndDay = CreditMath.Positive(EndDay);
                OriginalGearValue = CreditMath.Positive(OriginalGearValue);
            }
        }
    }
}

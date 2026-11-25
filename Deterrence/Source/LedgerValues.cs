namespace Deterrence
{
    // Saved ledger values, also used by the UI grouping tests without game dependencies.
    public class LedgerValues : CreditState
    {
        public string Key, Label, Category, SourceKey;
        public int EventsRecorded;
        public string LastEvent;
        public int MapId;
        public int CorpseId = -1;
        public float CombatPower;
        public double OriginalGearValue;
        public bool GearSnapshotKnown;
        public double LastSeenDay, DailyGain;
        public bool Buried;
        public int Quantity;
    }

    public class OrganStockValues
    {
        public string Key, Label;
        public int Count;
        public int TargetId = -1;
        public double MarketValue, Credit;
    }
}

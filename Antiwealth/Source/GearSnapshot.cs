using Verse;

namespace Antiwealth
{
    public class GearSnapshot : IExposable
    {
        public int PawnId;
        public double Value;

        public void ExposeData()
        {
            Scribe_Values.Look(ref PawnId, "pawnId");
            Scribe_Values.Look(ref Value, "value");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Value = CreditMath.Positive(Value);
        }
    }
}

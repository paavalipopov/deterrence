using Verse;

namespace Antiwealth
{
    public class ReputationShare : ShareValues, IExposable
    {
        public void ExposeData()
        {
            Scribe_Values.Look(ref Key, "key");
            Scribe_Values.Look(ref Label, "label");
            Scribe_Values.Look(ref SourceKey, "sourceKey");
            Scribe_Values.Look(ref Amount, "amount");
            Scribe_Values.Look(ref Day, "day");
        }
    }
}

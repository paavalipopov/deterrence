using Verse;

namespace Deterrence
{
    public class WealthSample : WealthValues, IExposable
    {
        public void ExposeData()
        {
            Scribe_Values.Look(ref MapId, "mapId");
            Scribe_Values.Look(ref Day, "day");
            Scribe_Values.Look(ref Economy, "economy");
            Scribe_Values.Look(ref VanillaThreat, "vanillaThreat");
            Scribe_Values.Look(ref SurfaceDiscount, "surfaceDiscount");
            Scribe_Values.Look(ref AdjustedThreat, "adjustedThreat");
            Scribe_Values.Look(ref Deterrence, "antiwealth");
        }
    }
}

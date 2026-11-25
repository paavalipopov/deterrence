using Verse;

namespace Deterrence
{
    public class EventRule : RuleValues, IExposable
    {
        public EventRule() { }
        public EventRule(string key, float amount, bool enabled = true)
        {
            Key = key;
            BaseAmount = amount;
            Enabled = enabled;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Key, "key");
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref BaseAmount, "baseAmount");
            Scribe_Values.Look(ref PopulationExponent, "populationExponent");
            Scribe_Values.Look(ref ReferencePopulation, "referencePopulation", 5);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) SanitizeValues();
        }
    }
}

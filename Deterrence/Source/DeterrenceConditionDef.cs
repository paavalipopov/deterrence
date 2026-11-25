using Verse;

namespace Deterrence
{
    // Optional extension point for game conditions supplied by other mods.
    public class DeterrenceConditionDef : Def
    {
        public string conditionDefName;
        public float initialDailyGain;
    }
}

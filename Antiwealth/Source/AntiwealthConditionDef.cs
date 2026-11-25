using Verse;

namespace Antiwealth
{
    // Optional extension point for game conditions supplied by other mods.
    public class AntiwealthConditionDef : Def
    {
        public string conditionDefName;
        public float initialDailyGain;
    }
}

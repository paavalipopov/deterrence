namespace Antiwealth
{
    public static class SurgeryOutcome
    {
        // Exactly one outcome per attempted prisoner removal operation.
        public static string RuleFor(bool failed, bool harvested, bool removed)
            => failed ? "bump:failedSurgery" : !removed ? null
                : harvested ? "bump:organ" : "bump:amputation";
    }
}

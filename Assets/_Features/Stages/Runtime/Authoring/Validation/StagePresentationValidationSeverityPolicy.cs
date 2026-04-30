namespace Game.Feature.Stages
{
    internal static class StagePresentationValidationSeverityPolicy
    {
        public const StageValidationSeverity AlwaysError = StageValidationSeverity.Error;

        public static StageValidationSeverity ResolveSoftSeverity(bool strict)
        {
            return strict ? StageValidationSeverity.Error : StageValidationSeverity.Warning;
        }
    }
}

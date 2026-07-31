namespace Game.Feature.Stages
{
    public static class StageObjectiveAuthoringMetadataPolicy
    {
        public const string AuthoringLabelMissingCode = "objective.authoring-label-missing";

        public static bool IsAuthoringLabelMissing(string authoringLabel)
        {
            return string.IsNullOrWhiteSpace(authoringLabel);
        }
    }
}

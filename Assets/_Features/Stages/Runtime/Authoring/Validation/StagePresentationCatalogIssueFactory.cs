namespace Game.Feature.Stages
{
    internal static class StagePresentationCatalogIssueFactory
    {
        public static StageValidationIssue Create(
            StageValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context,
            string assetPath,
            StageValidationTiming timing,
            string stageId,
            string authoringAssetName,
            string outputAssetName,
            int entityId = 0,
            string stableGuid = "",
            string fieldName = "",
            string expectedValue = "",
            string actualValue = "",
            string presentationId = "")
        {
            return new StageValidationIssue(
                severity,
                code,
                message,
                context,
                assetPath,
                timing,
                stageId,
                authoringAssetName,
                outputAssetName,
                entityId,
                stableGuid,
                fieldName,
                expectedValue,
                actualValue,
                presentationId);
        }

        public static string GetAssetPath(UnityEngine.Object asset, StageCatalogValidationOptions options)
        {
            return (options?.ResolvedAssetMetadataProvider)?.GetAssetPath(asset) ?? string.Empty;
        }
    }
}

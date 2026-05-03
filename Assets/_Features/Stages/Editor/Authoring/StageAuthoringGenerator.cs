namespace Game.Feature.Stages.Editor
{
    public static class StageAuthoringGenerator
    {
        public static StageAuthoringGenerationReport Generate(
            StageAuthoringDefinition source,
            StageAuthoringGenerateOptions options)
        {
            return Generate(
                source,
                source != null ? source.GeneratedGameplayDefinition : null,
                source != null ? source.GeneratedPresentationDefinition : null,
                options);
        }

        public static StageAuthoringGenerationReport Generate(
            StageAuthoringDefinition source,
            StageDefinition gameplayOutput,
            StagePresentationDefinition presentationOutput,
            StageAuthoringGenerateOptions options)
        {
            options ??= StageAuthoringGenerateOptions.WriteAll;
            var plan = BuildPlan(source, gameplayOutput, presentationOutput, options);
            if (!plan.Report.HasErrors && !options.DryRun)
            {
                ApplyPlan(plan, options);
            }

            return plan.Report;
        }

        internal static StageAuthoringGenerationPlan BuildPlan(
            StageAuthoringDefinition source,
            StageDefinition gameplayOutput,
            StagePresentationDefinition presentationOutput,
            StageAuthoringGenerateOptions options)
        {
            return StageAuthoringPlanBuilder.BuildPlan(source, gameplayOutput, presentationOutput, options);
        }

        internal static void ApplyPlan(
            StageAuthoringGenerationPlan plan,
            StageAuthoringGenerateOptions options)
        {
            StageAuthoringGeneratedAssetWriter.ApplyPlan(plan, options);
        }

        internal static StageAuthoringBuildData BuildExpectedDataForComparison(
            StageAuthoringDefinition source,
            StagePresentationDefinition presentationOutput,
            StageAuthoringGenerationReport report)
        {
            return StageAuthoringPlanBuilder.BuildExpectedDataForComparison(source, presentationOutput, report);
        }

        internal static void ApplyGameplayOutput(
            StageDefinition stage,
            StageAuthoringBuildData buildData,
            bool recordUndo = true,
            bool markDirty = true)
        {
            StageAuthoringGeneratedAssetWriter.ApplyGameplayOutput(stage, buildData, recordUndo, markDirty);
        }

        internal static void ApplyPresentationOutput(
            StagePresentationDefinition presentation,
            StageAuthoringBuildData buildData,
            bool recordUndo = true,
            bool markDirty = true)
        {
            StageAuthoringGeneratedAssetWriter.ApplyPresentationOutput(presentation, buildData, recordUndo, markDirty);
        }

        internal static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

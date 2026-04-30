namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringPlanBuilder
    {
        public static StageAuthoringGenerationPlan BuildPlan(
            StageAuthoringDefinition source,
            StageDefinition gameplayOutput,
            StagePresentationDefinition presentationOutput,
            StageAuthoringGenerateOptions options)
        {
            options ??= StageAuthoringGenerateOptions.WriteAll;
            var report = new StageAuthoringGenerationReport();
            if (source == null)
            {
                report.Add(StageValidationSeverity.Error, "authoring.source.null", "StageAuthoringDefinition cannot be null.");
                return new StageAuthoringGenerationPlan(null, null, null, null, report, options);
            }

            gameplayOutput ??= source.GeneratedGameplayDefinition;
            presentationOutput ??= source.GeneratedPresentationDefinition;
            if ((options.WriteGameplay || options.ValidateAfterGenerate) && gameplayOutput == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.output.gameplay-null",
                    "Stage authoring generation requires a generated gameplay StageDefinition.",
                    source,
                    string.Empty);
            }

            if ((options.WritePresentationBindings || options.ValidateAfterGenerate) && presentationOutput == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.output.presentation-null",
                    "Stage authoring generation requires a generated StagePresentationDefinition.",
                    source,
                    string.Empty);
            }

            var allocation = StageAuthoringEntityIdAllocator.BuildAllocationPlan(source, report);
            if (report.HasErrors)
            {
                return new StageAuthoringGenerationPlan(source, gameplayOutput, presentationOutput, allocation, report, options);
            }

            var buildData = StageAuthoringSpawnProjector.Project(
                source,
                allocation.EntityIdsByStableGuid,
                report);
            StageAuthoringPresentationBindingProjector.Project(
                source,
                presentationOutput,
                buildData,
                report);
            StageAuthoringSpawnProjector.ValidatePlayerCount(source, buildData, report);
            if (report.HasErrors)
            {
                return new StageAuthoringGenerationPlan(
                    source,
                    gameplayOutput,
                    presentationOutput,
                    allocation,
                    buildData,
                    report,
                    options);
            }

            StageAuthoringGeneratedOutputValidator.ValidateGeneratedGameplay(buildData, report);
            StageAuthoringGeneratedOutputValidator.ValidateGeneratedPresentation(presentationOutput, buildData, report);

            for (var i = 0; i < buildData.Placements.Count; i++)
            {
                var placement = buildData.Placements[i];
                report.RecordEntityId(placement.StableGuid, buildData.EntityIdsByStableGuid[placement.StableGuid]);
            }

            return new StageAuthoringGenerationPlan(
                source,
                gameplayOutput,
                presentationOutput,
                allocation,
                buildData,
                report,
                options);
        }

        public static StageAuthoringBuildData BuildExpectedDataForComparison(
            StageAuthoringDefinition source,
            StagePresentationDefinition presentationOutput,
            StageAuthoringGenerationReport report)
        {
            var allocation = StageAuthoringEntityIdAllocator.BuildAllocationPlan(source, report);
            if (report.HasErrors)
            {
                return null;
            }

            var buildData = StageAuthoringSpawnProjector.Project(
                source,
                allocation.EntityIdsByStableGuid,
                report);
            StageAuthoringPresentationBindingProjector.Project(
                source,
                presentationOutput,
                buildData,
                report);
            StageAuthoringSpawnProjector.ValidatePlayerCount(source, buildData, report);
            return buildData;
        }
    }
}

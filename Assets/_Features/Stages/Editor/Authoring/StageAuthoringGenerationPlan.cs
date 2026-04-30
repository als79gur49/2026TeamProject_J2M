using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages.Editor
{
    internal sealed class StageAuthoringGameplayWritePayload
    {
        public StageAuthoringGameplayWritePayload(
            StageBoardDefinition board,
            StageObjectiveAuthoring objective,
            IReadOnlyList<StageZoneDefinition> zones)
        {
            Board = board;
            Objective = objective;
            Zones.AddRange(zones ?? Array.Empty<StageZoneDefinition>());
        }

        public StageBoardDefinition Board { get; }

        public StageObjectiveAuthoring Objective { get; }

        public List<StageZoneDefinition> Zones { get; } = new();

        public List<StageSpawnDefinition> PlayerSpawns { get; } = new();

        public List<StageSpawnDefinition> EnemySpawns { get; } = new();

        public List<StageSpawnDefinition> BoxSpawns { get; } = new();

        public List<StageSpawnDefinition> WallSpawns { get; } = new();
    }

    internal sealed class StageAuthoringPresentationBindingWritePayload
    {
        public List<EnemyPresentationBinding> EnemyPresentationBindings { get; } = new();

        public List<StaticEntityPresentationBinding> StaticEntityPresentationBindings { get; } = new();
    }

    internal sealed class StageAuthoringBuildData
    {
        public StageAuthoringBuildData(
            StageBoardDefinition board,
            StageObjectiveAuthoring objective,
            IReadOnlyList<StageZoneDefinition> zones,
            IReadOnlyDictionary<string, int> entityIdsByStableGuid)
        {
            GameplayPayload = new StageAuthoringGameplayWritePayload(board, objective, zones);
            PresentationBindingPayload = new StageAuthoringPresentationBindingWritePayload();
            EntityIdsByStableGuid = entityIdsByStableGuid;
        }

        public StageAuthoringGameplayWritePayload GameplayPayload { get; }

        public StageAuthoringPresentationBindingWritePayload PresentationBindingPayload { get; }

        public StageBoardDefinition Board => GameplayPayload.Board;

        public StageObjectiveAuthoring Objective => GameplayPayload.Objective;

        public List<StageZoneDefinition> Zones => GameplayPayload.Zones;

        public IReadOnlyDictionary<string, int> EntityIdsByStableGuid { get; }

        public List<StagePlacedEntityAuthoring> Placements { get; } = new();

        public int PlayerPlacementCount { get; set; }

        public List<StageSpawnDefinition> PlayerSpawns => GameplayPayload.PlayerSpawns;

        public List<StageSpawnDefinition> EnemySpawns => GameplayPayload.EnemySpawns;

        public List<StageSpawnDefinition> BoxSpawns => GameplayPayload.BoxSpawns;

        public List<StageSpawnDefinition> WallSpawns => GameplayPayload.WallSpawns;

        public List<EnemyPresentationBinding> EnemyPresentationBindings =>
            PresentationBindingPayload.EnemyPresentationBindings;

        public List<StaticEntityPresentationBinding> StaticEntityPresentationBindings =>
            PresentationBindingPayload.StaticEntityPresentationBindings;
    }

    internal sealed class StageAuthoringGenerationPlan
    {
        public StageAuthoringGenerationPlan(
            StageAuthoringDefinition source,
            StageDefinition gameplayOutput,
            StagePresentationDefinition presentationOutput,
            StageAuthoringEntityIdAllocation allocation,
            StageAuthoringGenerationReport report,
            StageAuthoringGenerateOptions options = null)
            : this(source, gameplayOutput, presentationOutput, allocation, null, report, options)
        {
        }

        public StageAuthoringGenerationPlan(
            StageAuthoringDefinition source,
            StageDefinition gameplayOutput,
            StagePresentationDefinition presentationOutput,
            StageAuthoringEntityIdAllocation allocation,
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report,
            StageAuthoringGenerateOptions options = null)
        {
            Source = source;
            GameplayOutput = gameplayOutput;
            PresentationOutput = presentationOutput;
            Allocation = allocation;
            BuildData = buildData;
            GameplayPayload = buildData?.GameplayPayload;
            PresentationBindingPayload = buildData?.PresentationBindingPayload;
            Report = report ?? new StageAuthoringGenerationReport();
            Options = CopyOptions(options);

            var runtimeAllocationPlan = allocation != null
                ? new StageAuthoringAllocationPlan(
                    allocation.EntityIdsByStableGuid,
                    allocation.Mappings,
                    Array.Empty<StageAuthoringIdMapping>(),
                    Array.Empty<StageAuthoringIdMapping>())
                : StageAuthoringAllocationPlan.Empty;
            ExpectedGameplaySnapshot = source != null
                ? StageAuthoringProjection.ProjectExpectedGameplay(source, runtimeAllocationPlan)
                : null;
            ExpectedPresentationSnapshot = source != null
                ? StageAuthoringProjection.ProjectExpectedPresentation(source, runtimeAllocationPlan)
                : null;
        }

        public StageAuthoringDefinition Source { get; }

        public StageDefinition GameplayOutput { get; }

        public StagePresentationDefinition PresentationOutput { get; }

        public StageAuthoringEntityIdAllocation Allocation { get; }

        public StageAuthoringBuildData BuildData { get; }

        public StageAuthoringGameplayWritePayload GameplayPayload { get; }

        public StageAuthoringPresentationBindingWritePayload PresentationBindingPayload { get; }

        public StageAuthoringGenerationReport Report { get; }

        public StageAuthoringGenerateOptions Options { get; }

        public StageAuthoringNormalizedGameplaySnapshot ExpectedGameplaySnapshot { get; }

        public StageAuthoringNormalizedPresentationSnapshot ExpectedPresentationSnapshot { get; }

        private static StageAuthoringGenerateOptions CopyOptions(StageAuthoringGenerateOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return new StageAuthoringGenerateOptions
            {
                DryRun = options.DryRun,
                WriteGameplay = options.WriteGameplay,
                WritePresentationBindings = options.WritePresentationBindings,
                ValidateAfterGenerate = options.ValidateAfterGenerate,
            };
        }
    }
}

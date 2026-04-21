using System;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public abstract class StageBackedGameplayShowcaseInstallerBase : GameplayShowcaseSceneInstallerBase
    {
        [SerializeField] private StageContentEntry stageContentEntry;
        [SerializeField] private StageDefinition stageDefinition;

        protected StageContentEntry StageContentEntry => stageContentEntry;

        protected StageDefinition StageDefinition => stageDefinition;

        protected sealed override InitialGameplayState BuildInitialGameplayState()
        {
            var resolvedStageDefinition = ResolveGameplayDefinition();
            var buildResult = StageRuntimeBuilder.Build(resolvedStageDefinition);
            var resolvedPresentation = ResolvePresentationData(resolvedStageDefinition);
            var compositionData = StageSceneCompositionAssembler.Compose(buildResult, resolvedPresentation);

            return new InitialGameplayState(
                compositionData.GameplayBuildResult.BoardBounds,
                compositionData.GameplayBuildResult.InitialTopology,
                compositionData.GameplayBuildResult.InitialEntities,
                compositionData.GameplayBuildResult.InitialTerrain,
                compositionData.GameplayBuildResult.PlayerEntityId,
                compositionData.GameplayBuildResult.ObjectiveRuntimeDefinition,
                compositionData.GameplayBuildResult.EnemyAiProfileOverrides,
                compositionData.PresentationData.EnemyPresentationCatalog,
                compositionData.PresentationData.EnemyPresentationBindings,
                compositionData.PresentationData.StaticEntityPresentationCatalog,
                compositionData.PresentationData.StaticEntityPresentationBindings);
        }

        private StageDefinition ResolveGameplayDefinition()
        {
            var definition = stageContentEntry != null
                ? stageContentEntry.GameplayDefinition
                : stageDefinition;
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} requires either a {nameof(StageContentEntry)} or {nameof(StageDefinition)} reference.");
            }

            return definition;
        }

        private StagePresentationResolvedData ResolvePresentationData(StageDefinition resolvedStageDefinition)
        {
            if (stageContentEntry != null && stageContentEntry.PresentationDefinition != null)
            {
                return StagePresentationAssembler.Resolve(stageContentEntry.PresentationDefinition);
            }

            return StagePresentationAssembler.ResolveLegacy(
                resolvedStageDefinition,
                ResolveEnemyPresentationCatalog(),
                ResolveStaticEntityPresentationCatalog());
        }
    }
}

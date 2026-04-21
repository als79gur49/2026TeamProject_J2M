using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public abstract class StageBackedGameplayShowcaseInstallerBase : GameplayShowcaseSceneInstallerBase
    {
        private static readonly StageRuntimeContentResolver RuntimeContentResolver = new();

        [Header("Stage Load")]
        [SerializeField] private ScriptableObjectStageCatalogProvider stageCatalogProvider;
        [SerializeField] private StageId defaultStageId = StageId.None;

        protected ScriptableObjectStageCatalogProvider StageCatalogProvider => stageCatalogProvider;

        protected StageId DefaultStageId => defaultStageId;

        protected sealed override InitialGameplayState BuildInitialGameplayState()
        {
            var resolved = RuntimeContentResolver.Resolve(CreateStageLoadRequest());
            var buildResult = StageRuntimeBuilder.Build(resolved.Entry.GameplayDefinition);
            var resolvedPresentation = StagePresentationAssembler.Resolve(resolved.Entry.PresentationDefinition);
            var compositionData = StageSceneCompositionAssembler.Compose(buildResult, resolvedPresentation);

            return new InitialGameplayState(
                compositionData.GameplayBuildResult.BoardBounds,
                compositionData.GameplayBuildResult.InitialTopology,
                compositionData.GameplayBuildResult.InitialEntities,
                compositionData.GameplayBuildResult.InitialTerrain,
                compositionData.GameplayBuildResult.PlayerEntityId,
                compositionData.GameplayBuildResult.ObjectiveRuntimeDefinition,
                compositionData.GameplayBuildResult.EnemyAiProfileOverrides,
                resolved.Entry,
                compositionData.PresentationData.EnemyPresentationCatalog,
                compositionData.PresentationData.EnemyPresentationBindings,
                compositionData.PresentationData.StaticEntityPresentationCatalog,
                compositionData.PresentationData.StaticEntityPresentationBindings);
        }

        private StageLoadRequest CreateStageLoadRequest()
        {
            return StageLoadRequest.CreateEditorDirectPlayFallback(
                stageCatalogProvider,
                defaultStageId,
                gameObject.scene.name);
        }
    }
}

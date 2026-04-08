using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public abstract class StageBackedGameplayShowcaseInstallerBase : GameplayShowcaseSceneInstallerBase
    {
        [SerializeField] private StageDefinition stageDefinition;

        protected StageDefinition StageDefinition => stageDefinition;

        protected sealed override InitialGameplayState BuildInitialGameplayState()
        {
            var buildResult = StageRuntimeBuilder.Build(stageDefinition);
            return new InitialGameplayState(
                buildResult.BoardBounds,
                buildResult.TraversalRules,
                buildResult.InitialTopology,
                buildResult.InitialEntities,
                buildResult.InitialTerrain,
                buildResult.PlayerEntityId,
                buildResult.EnemyAiProfileOverrides,
                buildResult.EnemyPresentationBindings);
        }
    }
}

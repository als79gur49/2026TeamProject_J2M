using System;
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
            if (buildResult.PlayerEntityId != PlayerEntityId)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} built player entity id {buildResult.PlayerEntityId} from stage '{stageDefinition?.name ?? "<null>"}', but the serialized playerEntityId is {PlayerEntityId}. The installer field still drives player-specific host wiring, so the values must match.");
            }

            return new InitialGameplayState(
                buildResult.BoardBounds,
                buildResult.InitialTopology,
                buildResult.InitialEntities,
                buildResult.InitialTerrain,
                buildResult.PlayerEntityId,
                buildResult.EnemyAiProfileOverrides);
        }
    }
}

using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public class StageBackedGameplaySceneInstaller : StageBackedGameplaySceneInstallerBase
    {
        [SerializeField] private GameplayEntityView playerViewPrefab;

        protected override void ConfigureRuntimeConfiguration(
            GameplaySceneHostConfiguration configuration,
            in InitialGameplayState initialState)
        {
            base.ConfigureRuntimeConfiguration(configuration, initialState);
            configuration.ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
        }

        protected override void ConfigureRuntimeConfigurationAfterTimingPresets(
            GameplaySceneHostConfiguration configuration,
            in InitialGameplayState initialState)
        {
            var playerFree2DLocomotion = configuration.PlayerFree2DLocomotion;
            playerFree2DLocomotion.ActionAssistSettleWindowCells = 0.3125f;
            playerFree2DLocomotion.CollisionRadiusCells = 0.25f;
            configuration.PlayerFree2DLocomotion = playerFree2DLocomotion;
        }

        protected override IGameplayEntityViewFactory CreateViewFactory(
            GameplayBoardRoot boardRoot,
            in InitialGameplayState initialState)
        {
            if (!AutoCreateViews || boardRoot == null)
            {
                return null;
            }

            return new DefaultGameplayEntityViewFactory(
                boardRoot.EntityRoot,
                CellSize,
                initialState.PlayerEntityId,
                playerViewPrefab,
                ResolveEnemyViewPrefabs(
                    initialState.EnemyPresentationCatalog,
                    initialState.EnemyPresentationBindings),
                ResolveStaticEntityViewPrefabs(
                    initialState.StaticEntityPresentationCatalog,
                    initialState.StaticEntityPresentationBindings),
                EnemyInactiveVisualSettings);
        }

        protected override GameplayEntityView ResolvePlayerViewPrefab()
        {
            return playerViewPrefab;
        }
    }
}

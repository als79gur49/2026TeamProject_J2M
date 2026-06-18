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

        protected override bool TryGetPlayerFree2DLocomotionOverride(
            in InitialGameplayState initialState,
            out PlayerFree2DLocomotionOverride value)
        {
            value = PlayerFree2DLocomotionOverride.CreateCollisionAndActionAssist(
                collisionRadiusCells: 0.25f,
                actionAssistSettleWindowCells: 0.3125f);
            return true;
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

using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public class CombinedGameplayShowcaseInstaller : StageBackedGameplayShowcaseInstallerBase
    {
        [SerializeField] private GameplayEntityView playerViewPrefab;

        protected override void ConfigureRuntimeConfiguration(
            GameplaySceneHostConfiguration configuration,
            in InitialGameplayState initialState)
        {
            base.ConfigureRuntimeConfiguration(configuration, initialState);
            configuration.EnablePlayerSameFaceContinuousLocomotion = true;
            configuration.EnableEnemySameFaceContinuousLocomotion = true;
        }

        protected override IGameplayEntityViewFactory CreateViewFactory(
            GameplayBoardRoot boardRoot,
            in InitialGameplayState initialState)
        {
            if (!AutoCreateViews || boardRoot == null)
            {
                return null;
            }

            return new GameplayBoxCapabilityLabelViewFactory(
                boardRoot.EntityRoot,
                CellSize,
                initialState.PlayerEntityId,
                playerViewPrefab,
                ResolveEnemyViewPrefabs(
                    initialState.EnemyPresentationCatalog,
                    initialState.EnemyPresentationBindings),
                ResolveStaticEntityViewPrefabs(
                    initialState.StaticEntityPresentationCatalog,
                    initialState.StaticEntityPresentationBindings));
        }

        protected override GameplayEntityView ResolvePlayerViewPrefab()
        {
            return playerViewPrefab;
        }
    }
}

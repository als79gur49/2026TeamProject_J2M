using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public class CombinedGameplayShowcaseInstaller : StageBackedGameplayShowcaseInstallerBase
    {
        [SerializeField] private GameplayEntityView playerViewPrefab;
        [SerializeField] private EnemyPresentationCatalog enemyPresentationCatalog;
        [SerializeField] private EnemyPresentationArchetypeCatalog enemyPresentationArchetypeCatalog;
        [SerializeField] private EnemyUnitArchetypeCatalog enemyUnitArchetypeCatalog;
        [SerializeField] private StaticEntityPresentationCatalog staticEntityPresentationCatalog;

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
                    initialState.EnemyPresentationCatalog ?? ResolveEnemyPresentationCatalog(),
                    initialState.EnemyPresentationBindings),
                ResolveStaticEntityViewPrefabs(
                    initialState.StaticEntityPresentationCatalog ?? ResolveStaticEntityPresentationCatalog(),
                    initialState.StaticEntityPresentationBindings));
        }

        protected override GameplayEntityView ResolvePlayerViewPrefab()
        {
            return playerViewPrefab;
        }

        protected override EnemyPresentationCatalog ResolveEnemyPresentationCatalog()
        {
            return enemyPresentationCatalog;
        }

        protected override EnemyUnitArchetypeCatalog ResolveEnemyUnitArchetypeCatalog()
        {
            return enemyUnitArchetypeCatalog;
        }

        protected override EnemyPresentationArchetypeCatalog ResolveEnemyPresentationArchetypeCatalog()
        {
            return enemyPresentationArchetypeCatalog;
        }

        protected override StaticEntityPresentationCatalog ResolveStaticEntityPresentationCatalog()
        {
            return staticEntityPresentationCatalog;
        }
    }
}

using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public class CombinedGameplayShowcaseInstaller : StageBackedGameplayShowcaseInstallerBase
    {
        [SerializeField] private GameplayEntityView playerViewPrefab;
        [SerializeField] private EnemyPresentationCatalog enemyPresentationCatalog;

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
                ResolveEnemyViewPrefabs(initialState.EnemyPresentationBindings));
        }

        protected override GameplayEntityView ResolvePlayerViewPrefab()
        {
            return playerViewPrefab;
        }

        protected override EnemyPresentationCatalog ResolveEnemyPresentationCatalog()
        {
            return enemyPresentationCatalog;
        }

        protected override GameplayShowcaseOverlayContent CreateShowcaseOverlayContent()
        {
            return new GameplayShowcaseOverlayContent(
                "Combined Gameplay Showcase",
                "Boxes + Enemy Variants",
                "Move: WASD   Push: E   Flip: Q",
                new[]
                {
                    "Floor charger starts in the traversal lane to demo Patrol -> Chase -> Charge immediately.",
                    "Front-face scout uses the non-attacking profile so surface-transition chase behavior stays visible.",
                    "Floor striker near spawn uses a 2-tick wind-up melee profile plus tuned wind-up/recover timing so those beats can be inspected without blocking the charger lane.",
                });
        }

        private IReadOnlyDictionary<int, GameplayEntityView> ResolveEnemyViewPrefabs(
            EnemyPresentationBinding[] enemyPresentationBindings)
        {
            return EnemyPresentationCatalogResolver.BuildEnemyViewPrefabs(
                ResolveEnemyPresentationCatalog(),
                enemyPresentationBindings,
                nameof(CombinedGameplayShowcaseInstaller));
        }
    }
}

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
                    "Floor striker near spawn uses a brief wind-up melee profile plus tuned wind-up/recover timing so those beats can be inspected in place.",
                    "Adjacent floor scout uses the non-attacking profile so move-only pursuit pacing can be compared against the striker lane.",
                    "Elevated floor scout circles the nearby push box with a wall-follow patrol profile and never enters chase or attack.",
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

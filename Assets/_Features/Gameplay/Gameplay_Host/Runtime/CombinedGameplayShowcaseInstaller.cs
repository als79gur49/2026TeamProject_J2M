using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public class CombinedGameplayShowcaseInstaller : StageBackedGameplayShowcaseInstallerBase
    {
        [SerializeField] private GameplayEntityView playerViewPrefab;

        protected override IGameplayEntityViewFactory CreateViewFactory(GameplayBoardRoot boardRoot)
        {
            if (!AutoCreateViews || boardRoot == null)
            {
                return null;
            }

            return playerViewPrefab != null
                ? new CombinedGameplayShowcasePlayerPrefabViewFactory(
                    boardRoot.EntityRoot,
                    PlayerEntityId,
                    playerViewPrefab,
                    CellSize)
                : new GameplayBoxCapabilityLabelViewFactory(boardRoot.EntityRoot, CellSize, PlayerEntityId);
        }

        protected override GameplayEntityView ResolvePlayerViewPrefab()
        {
            return playerViewPrefab;
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
                    "Floor striker near spawn uses a 2-tick wind-up melee profile so wind-up, execute, and recover beats can be inspected without blocking the charger lane.",
                });
        }
    }
}

namespace Game.Feature.Gameplay.Host
{
    public sealed class CubeSurfaceTraversalShowcaseInstaller : StageBackedGameplayShowcaseInstallerBase
    {
        protected override GameplayShowcaseOverlayContent CreateShowcaseOverlayContent()
        {
            return new GameplayShowcaseOverlayContent(
                "Cube Surface Traversal",
                "Walk the shared edge openings to watch the board roll across Floor, Front, Ceiling, and Back.",
                "Move: WASD   Push: E   Flip: Q",
                new[]
                {
                    "Start at the top opening on Floor and press Up to trigger the first topology rotation.",
                    "The corridor stays open across the visible faces while the rest of the perimeter remains sealed.",
                    "This scene isolates traversal, board rotation, and active-face visibility without box rules layered on top.",
                });
        }
    }
}

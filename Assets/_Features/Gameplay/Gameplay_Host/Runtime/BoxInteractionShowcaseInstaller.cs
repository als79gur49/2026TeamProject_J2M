namespace Game.Feature.Gameplay.Host
{
    public sealed class BoxInteractionShowcaseInstaller : StageBackedGameplayShowcaseInstallerBase
    {
        protected override GameplayShowcaseOverlayContent CreateShowcaseOverlayContent()
        {
            return new GameplayShowcaseOverlayContent(
                "Box Interaction Showcase",
                "Compare the box capability combinations lane by lane on a single active floor.",
                "Move: WASD   Push: E   Flip: Q",
                new[]
                {
                    "Left lanes cover Push, Push + Destroy, and Flip in isolation.",
                    "Right lanes combine Push + Flip and Push + Flip + Destroy for cross-mechanic checks.",
                    "Use the overlay labels as scenario notes; the board itself stays clear for 3D presentation.",
                });
        }
    }
}

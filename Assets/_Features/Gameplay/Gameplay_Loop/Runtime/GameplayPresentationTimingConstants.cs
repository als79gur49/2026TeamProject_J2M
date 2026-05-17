namespace Game.Feature.Gameplay.Loop
{
    public static class GameplayPresentationTimingConstants
    {
        // Stay: contact/recoil branch threshold.
        // DestroySelf: break/release onset threshold.
        public const float FlipImpactInteractionOnsetNormalizedTime = 0.62f;

        // Ordinary Flip box motion visual slam contact. This is the point where the
        // visual sampler reaches the floor, not the earlier interaction onset.
        public const float FlipVisualSlamContactNormalizedTime = 0.936f;
    }
}

using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    public static class FlipImpactContactVfxAnchorBuilder
    {
        public static bool TryBuild(
            in FlipImpactPresentationSignal signal,
            GameplayTimingProfile timingProfile,
            out FlipImpactContactVfxAnchor anchor)
        {
            anchor = default;

            if (timingProfile == null ||
                signal.BoxEntityId <= 0 ||
                !IsSupportedDisposition(signal.Disposition))
            {
                return false;
            }

            var timingSettings = GameplayMotionTimingResolver.CreateFlipImpactTimingSettings(timingProfile);
            anchor = new FlipImpactContactVfxAnchor(
                signal.SourceActionPlanId,
                signal.BoxEntityId,
                signal.ActorEntityId,
                signal.ImpactTargetEntityId,
                signal.SourceCell,
                signal.ImpactCell,
                signal.Topology,
                signal.SourceFacing,
                signal.ImpactFacing,
                signal.Disposition,
                timingSettings.ContactNormalizedTime);
            return true;
        }

        private static bool IsSupportedDisposition(FlipImpactPresentationDisposition disposition)
        {
            return disposition == FlipImpactPresentationDisposition.Stay ||
                   disposition == FlipImpactPresentationDisposition.DestroySelf;
        }
    }
}

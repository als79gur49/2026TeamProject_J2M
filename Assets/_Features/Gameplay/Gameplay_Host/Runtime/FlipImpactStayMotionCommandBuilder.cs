using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    internal static class FlipImpactStayMotionCommandBuilder
    {
        public static bool TryBuild(
            in FlipImpactPresentationSignal signal,
            int tickIndexFallback,
            GameplayTimingProfile timingProfile,
            GameplayMotionTimingResolver motionTimingResolver,
            GameplayPoseResolver poseResolver,
            GameplayCubeProjector projector,
            out FlipImpactStayMotionCommand command)
        {
            command = default;

            if (timingProfile == null ||
                motionTimingResolver == null ||
                poseResolver == null ||
                projector == null ||
                signal.Disposition != FlipImpactPresentationDisposition.Stay ||
                signal.BoxEntityId <= 0)
            {
                return false;
            }

            if (!poseResolver.TryResolveFlipImpactSignalLocalPoses(
                    projector,
                    signal,
                    out var sourceLocalPose,
                    out var impactLocalPose))
            {
                return false;
            }

            var timingSettings = motionTimingResolver.ResolveFlipImpactTimingSettings(timingProfile);
            var durationSeconds = motionTimingResolver.ResolveMotionDurationSeconds(
                signal.BoxEntityId,
                TickEntityMotionKind.Flip,
                timingProfile);
            var presentationSeed = signal.SourceActionPlanId > 0
                ? signal.SourceActionPlanId
                : tickIndexFallback;

            command = new FlipImpactStayMotionCommand(
                signal.SourceActionPlanId,
                signal.BoxEntityId,
                signal.ActorEntityId,
                signal.ImpactTargetEntityId,
                signal.SourceCell,
                signal.ImpactCell,
                signal.Topology,
                signal.SourceFacing,
                signal.ImpactFacing,
                sourceLocalPose.Position,
                sourceLocalPose.Rotation,
                impactLocalPose.Position,
                impactLocalPose.Rotation,
                durationSeconds,
                timingSettings.ContactNormalizedTime,
                timingSettings.StayPostContactHoldNormalizedDuration,
                timingSettings.StayReturnArcHeightMultiplier,
                timingProfile.FlipArcHeightInCells * projector.CellSize,
                presentationSeed);
            return true;
        }
    }
}

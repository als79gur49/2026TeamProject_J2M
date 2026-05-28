using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal static class FlipDestroySelfMotionVfxCommandBuilder
    {
        public static bool TryBuild(
            in FlipImpactPresentationSignal signal,
            GameplayTimingProfile timingProfile,
            GameplayMotionTimingResolver motionTimingResolver,
            GameplayPoseResolver poseResolver,
            GameplayCubeProjector projector,
            out FlipDestroySelfMotionVfxCommand command)
        {
            command = default;

            if (timingProfile == null ||
                motionTimingResolver == null ||
                poseResolver == null ||
                projector == null ||
                signal.Disposition != FlipImpactPresentationDisposition.DestroySelf ||
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

            var timingSettings = GameplayMotionTimingResolver.CreateFlipImpactTimingSettings(timingProfile);
            var flightDurationSeconds = motionTimingResolver.ResolveMotionDurationSeconds(
                signal.BoxEntityId,
                TickEntityMotionKind.Flip,
                timingProfile);
            var breakStartSeconds = Mathf.Clamp01(timingSettings.ContactNormalizedTime) * Mathf.Max(0.0001f, flightDurationSeconds);
            var fadeDurationSeconds = Mathf.Max(0f, Mathf.Max(0.0001f, flightDurationSeconds) - breakStartSeconds);
            var presentationSeed = signal.SourceActionPlanId > 0
                ? signal.SourceActionPlanId
                : signal.BoxEntityId;

            command = new FlipDestroySelfMotionVfxCommand(
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
                flightDurationSeconds,
                timingSettings.ContactNormalizedTime,
                breakStartSeconds,
                fadeDurationSeconds,
                timingProfile.FlipArcHeightInCells * projector.CellSize,
                presentationSeed);
            return true;
        }

        public static bool TryBuildDueContinuation(
            in FlipDueContactPresentationSignal signal,
            GameplayTimingProfile timingProfile,
            GameplayMotionTimingResolver motionTimingResolver,
            GameplayPoseResolver poseResolver,
            GameplayCubeProjector projector,
            out FlipDestroySelfMotionVfxCommand command)
        {
            command = default;

            if (timingProfile == null ||
                motionTimingResolver == null ||
                poseResolver == null ||
                projector == null ||
                signal.BoxDisposition != FlipBoxDisposition.DestroySelf ||
                signal.BoxEntityId <= 0)
            {
                return false;
            }

            if (!poseResolver.TryResolveFlipDueContactSignalLocalPoses(
                    projector,
                    signal,
                    out _,
                    out var contactLocalPose))
            {
                return false;
            }

            var timingSettings = GameplayMotionTimingResolver.CreateFlipImpactTimingSettings(timingProfile);
            var flightDurationSeconds = motionTimingResolver.ResolveMotionDurationSeconds(
                signal.BoxEntityId,
                TickEntityMotionKind.Flip,
                timingProfile);
            var breakStartSeconds = 0f;
            var fadeDurationSeconds = Mathf.Max(
                0.0001f,
                flightDurationSeconds * Mathf.Clamp01(timingSettings.DestroyBreakNormalizedDuration));
            var presentationSeed = signal.PresentationSeed != 0
                ? signal.PresentationSeed
                : signal.SourceActionPlanId > 0
                    ? signal.SourceActionPlanId
                    : signal.BoxEntityId;

            command = new FlipDestroySelfMotionVfxCommand(
                signal.SourceActionPlanId,
                signal.BoxEntityId,
                signal.ActorEntityId,
                signal.HitEntityId,
                signal.ContactCell,
                signal.ContactCell,
                signal.Topology,
                signal.ContactFacing,
                signal.ContactFacing,
                contactLocalPose.Position,
                contactLocalPose.Rotation,
                contactLocalPose.Position,
                contactLocalPose.Rotation,
                fadeDurationSeconds,
                contactNormalizedTime: 0f,
                breakStartSeconds: breakStartSeconds,
                fadeDurationSeconds: fadeDurationSeconds,
                arcHeight: 0f,
                presentationSeed: presentationSeed);
            return true;
        }
    }
}

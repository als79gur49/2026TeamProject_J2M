using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal static class ImpactTransientBreakVfxCommandBuilder
    {
        private const float BreakStartNormalizedTime = 0.62f;

        public static bool TryBuild(
            int tickIndex,
            in TickImpactTransientPresentationSignal signal,
            GameplayTimingProfile timingProfile,
            GameplayPoseResolver poseResolver,
            GameplayCubeProjector projector,
            out ParameterizedMotionVfxCommand command)
        {
            command = default;
            if (!IsImpactTransientBreakCandidate(signal) ||
                timingProfile == null ||
                poseResolver == null ||
                projector == null)
            {
                return false;
            }

            if (!poseResolver.TryResolveImpactTransientSignalLocalPoses(
                    projector,
                    signal,
                    out var sourceLocalPose,
                    out var impactLocalPose))
            {
                return false;
            }

            var durationSeconds = Mathf.Max(
                timingProfile.BoxDestroyEffectDurationSeconds,
                timingProfile.FlipMotionDurationSeconds);
            var breakStartSeconds = BreakStartNormalizedTime * Mathf.Max(0.0001f, durationSeconds);
            var fadeDurationSeconds = Mathf.Max(0f, durationSeconds - breakStartSeconds);
            var sequenceId = signal.PresentationSeed != 0
                ? signal.PresentationSeed
                : ComputeSequenceId(tickIndex, signal);

            command = new ParameterizedMotionVfxCommand(
                GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak),
                signal.EntityId,
                sequenceId,
                sequenceId,
                sourceLocalPose.Position,
                sourceLocalPose.Rotation,
                impactLocalPose.Position,
                impactLocalPose.Rotation,
                durationSeconds,
                timingProfile.FlipArcHeightInCells * projector.CellSize,
                breakStartSeconds,
                fadeDurationSeconds,
                ParameterizedMotionVfxFadeMode.ScaleAndAlpha,
                ParameterizedMotionVfxCloneMode.SourceViewCloneWithPrefabFallback,
                ParameterizedMotionVfxSamplerMode.FlipArc);
            return true;
        }

        public static bool IsImpactTransientBreakCandidate(in TickImpactTransientPresentationSignal signal)
        {
            return signal.EntityType == EntityType.Box &&
                   signal.EntityId > 0;
        }

        public static int ComputeSequenceId(
            int tickIndex,
            in TickImpactTransientPresentationSignal signal)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.EntityId;
                hash = (hash * 31) + (int)BoxVfxCue.ImpactTransientBreak;
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.ImpactCell.GetHashCode();
                hash = (hash * 31) + signal.Topology.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }
    }
}

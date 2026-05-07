using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal static class OutOfBoundsExitVfxCommandBuilder
    {
        public static bool TryBuild(
            int tickIndex,
            in TickEntityExitPresentationSignal signal,
            GameplayVfxCueId cueId,
            GameplayTimingProfile timingProfile,
            GameplayPoseResolver poseResolver,
            GameplayCubeProjector projector,
            out ParameterizedMotionVfxCommand command)
        {
            command = default;
            if (!IsOutOfBoundsExitCandidate(signal) ||
                timingProfile == null ||
                poseResolver == null ||
                projector == null)
            {
                return false;
            }

            if (!poseResolver.TryResolveEntityExitSignalLocalPose(projector, signal, out var sourceLocalPose))
            {
                return false;
            }

            var sequenceId = signal.PresentationSeed != 0
                ? signal.PresentationSeed
                : ComputeSequenceId(tickIndex, signal, cueId);
            var durationSeconds = timingProfile.ItemConsumeEffectDurationSeconds;
            command = new ParameterizedMotionVfxCommand(
                cueId,
                signal.ExitedEntityId,
                sequenceId,
                sequenceId,
                sourceLocalPose.Position,
                sourceLocalPose.Rotation,
                sourceLocalPose.Position,
                sourceLocalPose.Rotation,
                durationSeconds,
                arcHeight: 0f,
                breakStartSeconds: 0f,
                fadeDurationSeconds: durationSeconds,
                ParameterizedMotionVfxFadeMode.DestroyShrinkEase,
                ParameterizedMotionVfxCloneMode.SourceViewCloneWithPrefabFallback,
                ParameterizedMotionVfxSamplerMode.Linear);
            return true;
        }

        public static bool IsOutOfBoundsExitCandidate(in TickEntityExitPresentationSignal signal)
        {
            return signal.ExitedEntityId > 0 &&
                   signal.ExitCause == TickEntityExitCause.OutOfBounds &&
                   (signal.EntityType == EntityType.Box ||
                    signal.EntityType == EntityType.Unit);
        }

        public static bool TryResolveCue(
            in TickEntityExitPresentationSignal signal,
            out GameplayVfxCueId cueId)
        {
            if (signal.EntityType == EntityType.Box)
            {
                cueId = GameplayVfxCueId.From(BoxVfxCue.OutOfBoundsExit);
                return true;
            }

            if (signal.EntityType == EntityType.Unit)
            {
                cueId = GameplayVfxCueId.From(EnemyVfxCue.OutOfBoundsExit);
                return true;
            }

            cueId = default;
            return false;
        }

        public static int ComputeSequenceId(
            int tickIndex,
            in TickEntityExitPresentationSignal signal,
            GameplayVfxCueId cueId)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.ExitedEntityId;
                hash = (hash * 31) + cueId.GetHashCode();
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.Topology.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }
    }
}

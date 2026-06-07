using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal static class EntityExitBoxDestroyShrinkVfxCommandBuilder
    {
        public static bool TryBuild(
            int tickIndex,
            in TickEntityExitPresentationSignal signal,
            GameplayTimingProfile timingProfile,
            GameplayPoseResolver poseResolver,
            GameplayCubeProjector projector,
            out ParameterizedMotionVfxCommand command)
        {
            command = default;
            if (!IsBoxDestroyExitCandidate(signal) ||
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
                : ComputeSequenceId(tickIndex, signal);
            var durationSeconds = timingProfile.BoxDestroyEffectDurationSeconds;
            command = new ParameterizedMotionVfxCommand(
                GameplayVfxCueId.From(BoxVfxCue.DestroyShrink),
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
                ParameterizedMotionVfxCloneMode.SourceCloneMotion,
                ParameterizedMotionVfxSamplerMode.Linear);
            return true;
        }

        public static bool IsBoxDestroyExitCandidate(in TickEntityExitPresentationSignal signal)
        {
            return signal.EntityType == EntityType.Box &&
                   signal.ExitedEntityId > 0 &&
                   signal.ExitCause == TickEntityExitCause.BoxDestroy;
        }

        public static bool IsDuplicateOwnedExit(
            TickPresentationData presentationData,
            int entityId)
        {
            if (presentationData == null || entityId <= 0)
            {
                return false;
            }

            return BoxVfxExitSignalGuards.IsDuplicateOwnedExit(presentationData, entityId);
        }

        public static int ComputeSequenceId(
            int tickIndex,
            in TickEntityExitPresentationSignal signal)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.ExitedEntityId;
                hash = (hash * 31) + (int)BoxVfxCue.DestroyShrink;
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.Topology.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }
    }
}

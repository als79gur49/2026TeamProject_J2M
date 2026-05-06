using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Vfx;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal static class BoxSlideTrailVfxCommandBuilder
    {
        public const float DefaultFadeDurationSeconds = 0.20f;

        public static bool TryBuild(
            int tickIndex,
            TickEntityMotion motion,
            GameplayTimingProfile timingProfile,
            GameplayMotionTimingResolver motionTimingResolver,
            GameplayPoseResolver poseResolver,
            GameplayCubeProjector projector,
            CubeTopologyState fallbackTopology,
            out ParameterizedMotionVfxCommand command)
        {
            command = default;
            if (motion.MotionKind != TickEntityMotionKind.BoxSlide ||
                motion.EntityId <= 0 ||
                timingProfile == null ||
                motionTimingResolver == null ||
                poseResolver == null ||
                projector == null)
            {
                return false;
            }

            var sourceTopology = motion.SourceTopology ??
                                 motion.DestinationTopology ??
                                 fallbackTopology;
            var destinationTopology = motion.DestinationTopology ??
                                      motion.SourceTopology ??
                                      fallbackTopology;
            var sourceFacing = motion.SourceFacing ??
                               motion.DestinationFacing ??
                               Direction.Up;
            var destinationFacing = motion.DestinationFacing ??
                                    motion.SourceFacing ??
                                    Direction.Up;

            if (!poseResolver.TryResolveLocalPose(
                    projector,
                    motion.EntityId,
                    motion.SourceCell,
                    sourceTopology,
                    sourceFacing,
                    out var sourcePose) ||
                !poseResolver.TryResolveLocalPose(
                    projector,
                    motion.EntityId,
                    motion.DestinationCell,
                    destinationTopology,
                    destinationFacing,
                    out var destinationPose))
            {
                return false;
            }

            var sequenceId = ComputeSequenceId(tickIndex, motion);
            var durationSeconds = motionTimingResolver.ResolveGlobalMotionDurationSeconds(
                TickEntityMotionKind.BoxSlide,
                timingProfile);
            command = new ParameterizedMotionVfxCommand(
                GameplayVfxCueId.From(BoxVfxCue.SlideDustTrail),
                motion.EntityId,
                sequenceId,
                sequenceId,
                sourcePose.Position,
                sourcePose.Rotation,
                destinationPose.Position,
                destinationPose.Rotation,
                durationSeconds,
                arcHeight: 0f,
                breakStartSeconds: durationSeconds,
                fadeDurationSeconds: DefaultFadeDurationSeconds,
                ParameterizedMotionVfxFadeMode.AlphaOnly,
                ParameterizedMotionVfxCloneMode.PrefabOnly,
                ParameterizedMotionVfxSamplerMode.Linear);
            return true;
        }

        public static int ComputeSequenceId(int tickIndex, TickEntityMotion motion)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + motion.EntityId;
                hash = (hash * 31) + (int)BoxVfxCue.SlideDustTrail;
                hash = (hash * 31) + motion.SourceCell.GetHashCode();
                hash = (hash * 31) + motion.DestinationCell.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }

        public static BoxSlideTrailMotionInstanceKey CreateInstanceKey(
            int tickIndex,
            TickEntityMotion motion)
        {
            return new BoxSlideTrailMotionInstanceKey(
                tickIndex,
                motion.EntityId,
                motion.SourceCell,
                motion.DestinationCell);
        }
    }

    internal readonly struct BoxSlideTrailMotionInstanceKey : IEquatable<BoxSlideTrailMotionInstanceKey>
    {
        public BoxSlideTrailMotionInstanceKey(
            int tickIndex,
            int entityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell)
        {
            TickIndex = tickIndex;
            EntityId = entityId;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
        }

        public int TickIndex { get; }

        public int EntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public bool Equals(BoxSlideTrailMotionInstanceKey other)
        {
            return TickIndex == other.TickIndex &&
                   EntityId == other.EntityId &&
                   SourceCell.Equals(other.SourceCell) &&
                   DestinationCell.Equals(other.DestinationCell);
        }

        public override bool Equals(object obj)
        {
            return obj is BoxSlideTrailMotionInstanceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ EntityId;
                hash = (hash * 397) ^ SourceCell.GetHashCode();
                hash = (hash * 397) ^ DestinationCell.GetHashCode();
                return hash;
            }
        }
    }
}

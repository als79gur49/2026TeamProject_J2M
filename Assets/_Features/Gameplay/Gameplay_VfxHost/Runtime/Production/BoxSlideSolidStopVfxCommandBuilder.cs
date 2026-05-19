using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal static class BoxSlideSolidStopVfxCommandBuilder
    {
        public static bool IsCandidate(in BoxSlideStopPresentationSignal signal)
        {
            return signal.StopperKind == BoxSlideStopperKind.SolidEntity &&
                   signal.BoxEntityId > 0 &&
                   signal.StopperEntityId > 0 &&
                   signal.Cause == BoxSlideStopCause.SlidingContinuationBlocked;
        }

        public static bool TryBuild(
            int tickIndex,
            in BoxSlideStopPresentationSignal signal,
            GameplayCubeProjector projector,
            out GameplayVfxRequest request,
            out VfxResolvedAnchor anchor)
        {
            request = default;
            anchor = default;
            if (!IsCandidate(signal) || projector == null)
            {
                return false;
            }

            if (signal.SourceCell.face != signal.StopperCell.face)
            {
                return false;
            }

            var sequenceId = ComputeSequenceId(tickIndex, signal);
            request = new GameplayVfxRequest(
                tickIndex: tickIndex,
                sequenceId: sequenceId,
                presentationSeed: sequenceId,
                sourceEntityId: signal.BoxEntityId,
                cueId: GameplayVfxCueId.From(BoxVfxCue.BoxSlideSolidStop),
                anchor: VfxAnchor.ForCell(
                    signal.SourceCell,
                    signal.Topology,
                    VfxAnchorSlot.CellCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);

            if (!signal.Topology.IsFaceActive(signal.SourceCell.face) ||
                !projector.TryProjectSurfaceCell(signal.SourceCell, signal.Topology, out var sourcePose))
            {
                return false;
            }

            if (signal.Topology.IsFaceActive(signal.StopperCell.face) &&
                projector.TryProjectSurfaceCell(signal.StopperCell, signal.Topology, out var stopperPose))
            {
                var midpoint = Vector3.Lerp(sourcePose.LocalPosition, stopperPose.LocalPosition, 0.5f);
                var approach = stopperPose.LocalPosition - sourcePose.LocalPosition;
                var rotation = approach.sqrMagnitude > 0.000001f
                    ? Quaternion.LookRotation(approach.normalized, sourcePose.Normal)
                    : sourcePose.LocalRotation;
                anchor = VfxResolvedAnchor.ForCell(
                    signal.SourceCell,
                    signal.Topology,
                    VfxAnchorSlot.CellCenter,
                    midpoint,
                    rotation);
                return true;
            }

            anchor = VfxResolvedAnchor.ForCell(
                signal.SourceCell,
                signal.Topology,
                VfxAnchorSlot.CellCenter,
                sourcePose.LocalPosition,
                sourcePose.LocalRotation,
                usedFallback: true);
            return true;
        }

        public static int ComputeSequenceId(
            int tickIndex,
            in BoxSlideStopPresentationSignal signal)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.BoxEntityId;
                hash = (hash * 31) + signal.StopperEntityId;
                hash = (hash * 31) + (int)BoxVfxCue.BoxSlideSolidStop;
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.StopperCell.GetHashCode();
                hash = (hash * 31) + (int)signal.SlideDirection;
                hash = (hash * 31) + signal.Topology.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }
    }
}

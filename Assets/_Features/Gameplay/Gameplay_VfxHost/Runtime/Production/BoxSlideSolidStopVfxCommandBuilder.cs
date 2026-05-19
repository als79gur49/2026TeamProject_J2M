using Game.Feature.Gameplay.BoardState;
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
            var policy = VfxBindingRuntimePolicy.Optional(
                GameplayVfxCueId.From(BoxVfxCue.BoxSlideSolidStop),
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration);
            return TryBuild(
                tickIndex,
                signal,
                projector,
                policy,
                default,
                out request,
                out anchor);
        }

        public static bool TryBuild(
            int tickIndex,
            in BoxSlideStopPresentationSignal signal,
            GameplayCubeProjector projector,
            VfxBindingRuntimePolicy policy,
            GameplayVfxVisibilityContext visibilityContext,
            out GameplayVfxRequest request,
            out VfxResolvedAnchor anchor)
        {
            request = default;
            anchor = default;
            if (projector == null ||
                !TryCreateRequest(tickIndex, signal, out request))
            {
                return false;
            }

            if (!IsCellVisible(request, policy, visibilityContext))
            {
                return false;
            }

            var stopperRequest = CreateRequest(tickIndex, signal, request.SequenceId, signal.StopperCell);
            if (!IsCellVisible(stopperRequest, policy, visibilityContext))
            {
                return false;
            }

            if (!projector.TryProjectSurfaceCell(signal.SourceCell, signal.Topology, out var sourcePose))
            {
                return false;
            }

            if (projector.TryProjectSurfaceCell(signal.StopperCell, signal.Topology, out var stopperPose))
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

        public static bool TryCreateRequest(
            int tickIndex,
            in BoxSlideStopPresentationSignal signal,
            out GameplayVfxRequest request)
        {
            request = default;
            if (!IsCandidate(signal) ||
                signal.SourceCell.face != signal.StopperCell.face)
            {
                return false;
            }

            request = CreateRequest(
                tickIndex,
                signal,
                ComputeSequenceId(tickIndex, signal),
                signal.SourceCell);
            return true;
        }

        private static GameplayVfxRequest CreateRequest(
            int tickIndex,
            in BoxSlideStopPresentationSignal signal,
            int sequenceId,
            SurfaceCell anchorCell)
        {
            return new GameplayVfxRequest(
                tickIndex: tickIndex,
                sequenceId: sequenceId,
                presentationSeed: sequenceId,
                sourceEntityId: signal.BoxEntityId,
                cueId: GameplayVfxCueId.From(BoxVfxCue.BoxSlideSolidStop),
                anchor: VfxAnchor.ForCell(
                    anchorCell,
                    signal.Topology,
                    VfxAnchorSlot.CellCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: false,
                persistentKey: VfxPersistentKey.None);
        }

        private static bool IsCellVisible(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy,
            GameplayVfxVisibilityContext visibilityContext)
        {
            return GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                policy,
                visibilityContext).IsVisible;
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

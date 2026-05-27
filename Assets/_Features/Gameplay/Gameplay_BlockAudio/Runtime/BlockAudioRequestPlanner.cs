using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.BlockAudio
{
    public sealed class BlockAudioRequestPlanner
    {
        private const float FlipLandingNormalizedTime = 0.9f;
        private const float FlipImpactContactNormalizedTime =
            GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime;

        public IReadOnlyList<BlockAudioRequest> BuildRequests(
            TickResult result,
            GameplayTimingProfile timingProfile)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            var requests = new List<BlockAudioRequest>();
            BuildFlipLandingRequests(result, timingProfile, requests);
            BuildFlipDueContactRequests(result, requests);
            BuildBoxSlideStartedRequests(result, requests);
            BuildBoxSlideSolidStopRequests(result, requests);
            return requests;
        }

        private static void BuildFlipLandingRequests(
            TickResult result,
            GameplayTimingProfile timingProfile,
            ICollection<BlockAudioRequest> requests)
        {
            var delaySeconds = timingProfile.FlipMotionDurationSeconds * FlipLandingNormalizedTime;
            var motions = result.PresentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (motion.MotionKind != TickEntityMotionKind.Flip ||
                    motion.EntityId <= 0)
                {
                    continue;
                }

                requests.Add(new BlockAudioRequest(
                    BlockAudioCue.FlipLanding,
                    motion.EntityId,
                    ComputeFlipMotionSequenceId(result.TickIndex, motion),
                    delaySeconds,
                    new AudioPlaybackContext(
                        ownerEntityId: motion.EntityId,
                        debugTag: BlockAudioCueCatalog.Format(BlockAudioCue.FlipLanding))));
            }

            var flipImpactSignals = result.PresentationData.FlipImpactSignals;
            for (var i = 0; i < flipImpactSignals.Count; i++)
            {
                var signal = flipImpactSignals[i];
                if (signal.Disposition != FlipImpactPresentationDisposition.Stay ||
                    signal.BoxEntityId <= 0)
                {
                    continue;
                }

                requests.Add(new BlockAudioRequest(
                    BlockAudioCue.FlipLanding,
                    signal.BoxEntityId,
                    ComputeFlipImpactStaySequenceId(result.TickIndex, signal),
                    timingProfile.FlipMotionDurationSeconds * FlipImpactContactNormalizedTime,
                    new AudioPlaybackContext(
                        ownerEntityId: signal.BoxEntityId,
                        debugTag: BlockAudioCueCatalog.Format(BlockAudioCue.FlipLanding))));
            }
        }

        private static void BuildFlipDueContactRequests(
            TickResult result,
            ICollection<BlockAudioRequest> requests)
        {
            var signals = result.PresentationData.FlipDueContactSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.BoxEntityId <= 0 ||
                    signal.TimingMode != GameplayPresentationTimingMode.DueContactImmediate ||
                    signal.BoxDisposition == FlipBoxDisposition.Cancelled)
                {
                    continue;
                }

                requests.Add(new BlockAudioRequest(
                    BlockAudioCue.FlipLanding,
                    signal.BoxEntityId,
                    ComputeFlipDueContactSequenceId(result.TickIndex, signal),
                    delaySeconds: 0f,
                    new AudioPlaybackContext(
                        ownerEntityId: signal.BoxEntityId,
                        debugTag: BlockAudioCueCatalog.Format(BlockAudioCue.FlipLanding))));
            }
        }

        private static void BuildBoxSlideSolidStopRequests(
            TickResult result,
            ICollection<BlockAudioRequest> requests)
        {
            var signals = result.PresentationData.BoxSlideStopSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!IsBoxSlideCrashStopCandidate(signal))
                {
                    continue;
                }

                requests.Add(new BlockAudioRequest(
                    BlockAudioCue.BoxSlideSolidStop,
                    signal.BoxEntityId,
                    ComputeBoxSlideSolidStopSequenceId(result.TickIndex, signal),
                    delaySeconds: 0f,
                    new AudioPlaybackContext(
                        ownerEntityId: signal.BoxEntityId,
                        debugTag: BlockAudioCueCatalog.Format(BlockAudioCue.BoxSlideSolidStop))));
            }
        }

        private static bool IsBoxSlideCrashStopCandidate(in BoxSlideStopPresentationSignal signal)
        {
            if (signal.Cause != BoxSlideStopCause.SlidingContinuationBlocked ||
                signal.BoxEntityId <= 0)
            {
                return false;
            }

            return (signal.StopperKind == BoxSlideStopperKind.SolidEntity && signal.StopperEntityId > 0) ||
                   (signal.StopperKind == BoxSlideStopperKind.Barricade && signal.StopperTileId > 0);
        }

        private static void BuildBoxSlideStartedRequests(
            TickResult result,
            ICollection<BlockAudioRequest> requests)
        {
            var signals = result.PresentationData.BoxSlideStartSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.BoxEntityId <= 0 ||
                    signal.ActorEntityId <= 0)
                {
                    continue;
                }

                requests.Add(new BlockAudioRequest(
                    BlockAudioCue.BoxSlideStarted,
                    signal.BoxEntityId,
                    ComputeBoxSlideStartedSequenceId(result.TickIndex, signal),
                    delaySeconds: 0f,
                    new AudioPlaybackContext(
                        ownerEntityId: signal.BoxEntityId,
                        debugTag: BlockAudioCueCatalog.Format(BlockAudioCue.BoxSlideStarted))));
            }
        }

        private static int ComputeFlipMotionSequenceId(int tickIndex, in TickEntityMotion motion)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + motion.EntityId;
                hash = (hash * 31) + (int)BlockAudioCue.FlipLanding;
                hash = (hash * 31) + motion.SourceCell.GetHashCode();
                hash = (hash * 31) + motion.DestinationCell.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }

        private static int ComputeFlipImpactStaySequenceId(int tickIndex, in FlipImpactPresentationSignal signal)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.BoxEntityId;
                hash = (hash * 31) + signal.SourceActionPlanId;
                hash = (hash * 31) + (int)BlockAudioCue.FlipLanding;
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.ImpactCell.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }

        private static int ComputeFlipDueContactSequenceId(int tickIndex, in FlipDueContactPresentationSignal signal)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.BoxEntityId;
                hash = (hash * 31) + signal.SourceActionPlanId;
                hash = (hash * 31) + (int)BlockAudioCue.FlipLanding;
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.ContactCell.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }

        private static int ComputeBoxSlideSolidStopSequenceId(
            int tickIndex,
            in BoxSlideStopPresentationSignal signal)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.BoxEntityId;
                hash = (hash * 31) + signal.StopperEntityId;
                hash = (hash * 31) + signal.StopperTileId;
                hash = (hash * 31) + (int)BlockAudioCue.BoxSlideSolidStop;
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.StopperCell.GetHashCode();
                hash = (hash * 31) + (int)signal.SlideDirection;
                hash = (hash * 31) + signal.Topology.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }

        private static int ComputeBoxSlideStartedSequenceId(
            int tickIndex,
            in BoxSlideStartPresentationSignal signal)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.BoxEntityId;
                hash = (hash * 31) + signal.ActorEntityId;
                hash = (hash * 31) + (int)BlockAudioCue.BoxSlideStarted;
                hash = (hash * 31) + signal.SourceCell.GetHashCode();
                hash = (hash * 31) + signal.DestinationCell.GetHashCode();
                hash = (hash * 31) + signal.Topology.GetHashCode();
                return hash == 0 ? 1 : hash;
            }
        }
    }
}

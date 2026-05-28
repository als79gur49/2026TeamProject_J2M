using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.PlayerLocomotionAudio
{
    public sealed class PlayerLocomotionAudioRequestPlanner
    {
        public IReadOnlyList<PlayerLocomotionAudioRequest> BuildRequests(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var requests = new List<PlayerLocomotionAudioRequest>();
            var seenKeys = new HashSet<PlayerTopologyTransitionBlockedRequestKey>();
            var signals = result.PresentationData.PlayerTopologyTransitionBlockedSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0)
                {
                    continue;
                }

                if (!IsAudibleBottomToBackBlockedTransition(signal))
                {
                    continue;
                }

                var key = new PlayerTopologyTransitionBlockedRequestKey(signal);
                if (!seenKeys.Add(key))
                {
                    continue;
                }

                requests.Add(
                    new PlayerLocomotionAudioRequest(
                        PlayerLocomotionAudioCue.TopologyTransitionBlocked,
                        signal.EntityId,
                        ComputeTopologyTransitionBlockedSequenceId(result.TickIndex, signal),
                        delaySeconds: 0f,
                        new AudioPlaybackContext(
                            ownerEntityId: signal.EntityId,
                            debugTag: PlayerLocomotionAudioCueCatalog.Format(
                                PlayerLocomotionAudioCue.TopologyTransitionBlocked)),
                        signal.Direction,
                        signal.CandidateCell));
            }

            return requests;
        }

        private static bool IsAudibleBottomToBackBlockedTransition(
            in TickPlayerTopologyTransitionBlockedSignal signal)
        {
            return signal.PrimaryBlockerKind != TickTraversalBlockerKind.None &&
                   signal.PrimaryBlockerKind != TickTraversalBlockerKind.BoardEdge &&
                   signal.Direction == Direction.Down &&
                   signal.SourceTopology.BottomFace == FaceId.Floor &&
                   signal.OriginCell.face == FaceId.Floor &&
                   signal.RotationKind == CubeRotationKind.Backward &&
                   signal.RequiredTopology.BottomFace == FaceId.Back &&
                   signal.CandidateCell.face == FaceId.Back;
        }

        private static int ComputeTopologyTransitionBlockedSequenceId(
            int tickIndex,
            in TickPlayerTopologyTransitionBlockedSignal signal)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + signal.EntityId;
                hash = (hash * 31) + (int)PlayerLocomotionAudioCue.TopologyTransitionBlocked;
                hash = (hash * 31) + signal.OriginCell.GetHashCode();
                hash = (hash * 31) + signal.CandidateCell.GetHashCode();
                hash = (hash * 31) + (int)signal.RotationKind;
                return hash == 0 ? 1 : hash;
            }
        }

        private readonly struct PlayerTopologyTransitionBlockedRequestKey :
            IEquatable<PlayerTopologyTransitionBlockedRequestKey>
        {
            public PlayerTopologyTransitionBlockedRequestKey(
                in TickPlayerTopologyTransitionBlockedSignal signal)
            {
                EntityId = signal.EntityId;
                OriginCell = signal.OriginCell;
                CandidateCell = signal.CandidateCell;
                RotationKind = signal.RotationKind;
            }

            private int EntityId { get; }

            private SurfaceCell OriginCell { get; }

            private SurfaceCell CandidateCell { get; }

            private CubeRotationKind RotationKind { get; }

            public bool Equals(PlayerTopologyTransitionBlockedRequestKey other)
            {
                return EntityId == other.EntityId &&
                       OriginCell.Equals(other.OriginCell) &&
                       CandidateCell.Equals(other.CandidateCell) &&
                       RotationKind == other.RotationKind;
            }

            public override bool Equals(object obj)
            {
                return obj is PlayerTopologyTransitionBlockedRequestKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = EntityId;
                    hash = (hash * 397) ^ OriginCell.GetHashCode();
                    hash = (hash * 397) ^ CandidateCell.GetHashCode();
                    hash = (hash * 397) ^ (int)RotationKind;
                    return hash;
                }
            }
        }
    }
}

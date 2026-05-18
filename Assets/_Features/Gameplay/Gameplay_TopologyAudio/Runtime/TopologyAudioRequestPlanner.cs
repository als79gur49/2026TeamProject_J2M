using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.TopologyAudio
{
    public sealed class TopologyAudioRequestPlanner
    {
        public IReadOnlyList<TopologyAudioRequest> BuildRequests(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var topologyMotion = result.PresentationData.TopologyMotion;
            if (!topologyMotion.HasValue ||
                topologyMotion.Value.RotationKind == CubeRotationKind.None)
            {
                return Array.Empty<TopologyAudioRequest>();
            }

            return new[]
            {
                new TopologyAudioRequest(
                    TopologyAudioCue.MapRotateStarted,
                    ComputeMapRotateStartedSequenceId(result.TickIndex, topologyMotion.Value),
                    new AudioPlaybackContext(
                        debugTag: TopologyAudioCueCatalog.Format(TopologyAudioCue.MapRotateStarted))),
            };
        }

        private static int ComputeMapRotateStartedSequenceId(
            int tickIndex,
            in TickTopologyMotion topologyMotion)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + tickIndex;
                hash = (hash * 31) + (int)TopologyAudioCue.MapRotateStarted;
                hash = (hash * 31) + topologyMotion.SourceTopology.GetHashCode();
                hash = (hash * 31) + topologyMotion.DestinationTopology.GetHashCode();
                hash = (hash * 31) + (int)topologyMotion.RotationKind;
                return hash == 0 ? 1 : hash;
            }
        }
    }
}

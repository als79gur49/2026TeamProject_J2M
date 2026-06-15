using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;

namespace Game.Feature.Gameplay.Host
{
    internal enum TopologyPresentationExecutorMode
    {
        Disabled = 0,
        EnabledForTests = 1,
    }

    internal readonly struct TopologyTransitionPlaybackRequest : IEquatable<TopologyTransitionPlaybackRequest>
    {
        public TopologyTransitionPlaybackRequest(
            int tickIndex,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind,
            int sourceTickIndex,
            bool hasSourceMetadata,
            int sourceMetadataKey)
        {
            TickIndex = Math.Max(0, tickIndex);
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            RotationKind = rotationKind;
            SourceTickIndex = Math.Max(0, sourceTickIndex);
            HasSourceMetadata = hasSourceMetadata;
            SourceMetadataKey = Math.Max(0, sourceMetadataKey);
        }

        public int TickIndex { get; }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState DestinationTopology { get; }

        public CubeRotationKind RotationKind { get; }

        public int SourceTickIndex { get; }

        public bool HasSourceMetadata { get; }

        public int SourceMetadataKey { get; }

        public bool Equals(TopologyTransitionPlaybackRequest other)
        {
            return TickIndex == other.TickIndex &&
                   SourceTopology.Equals(other.SourceTopology) &&
                   DestinationTopology.Equals(other.DestinationTopology) &&
                   RotationKind == other.RotationKind &&
                   SourceTickIndex == other.SourceTickIndex &&
                   HasSourceMetadata == other.HasSourceMetadata &&
                   SourceMetadataKey == other.SourceMetadataKey;
        }

        public override bool Equals(object obj)
        {
            return obj is TopologyTransitionPlaybackRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = TickIndex;
                hash = (hash * 397) ^ SourceTopology.GetHashCode();
                hash = (hash * 397) ^ DestinationTopology.GetHashCode();
                hash = (hash * 397) ^ (int)RotationKind;
                hash = (hash * 397) ^ SourceTickIndex;
                hash = (hash * 397) ^ HasSourceMetadata.GetHashCode();
                hash = (hash * 397) ^ SourceMetadataKey;
                return hash;
            }
        }
    }

    internal readonly struct TopologyExecutorDiagnostics
    {
        public TopologyExecutorDiagnostics(
            int observedTrackCount,
            int routeCount,
            int ignoredCount,
            int invalidTrackCount,
            int missingPortCount)
        {
            ObservedTrackCount = Math.Max(0, observedTrackCount);
            RouteCount = Math.Max(0, routeCount);
            IgnoredCount = Math.Max(0, ignoredCount);
            InvalidTrackCount = Math.Max(0, invalidTrackCount);
            MissingPortCount = Math.Max(0, missingPortCount);
        }

        public int ObservedTrackCount { get; }

        public int RouteCount { get; }

        public int IgnoredCount { get; }

        public int InvalidTrackCount { get; }

        public int MissingPortCount { get; }
    }

    internal interface ITopologyTransitionPlaybackPort
    {
        bool IsTransitionActive { get; }

        void BeginOrRefreshTopologyTransition(TopologyTransitionPlaybackRequest request);

        void UpdatePresentation(float deltaTime);

        void ResetSession();

        void HardCleanup();
    }

    internal sealed class TopologyPresentationExecutor : IPresentationTopologyExecutor
    {
        private readonly ITopologyTransitionPlaybackPort _playbackPort;
        private readonly TopologyPresentationExecutorMode _mode;
        private bool _hasRoutedRequest;

        public TopologyPresentationExecutor(
            ITopologyTransitionPlaybackPort playbackPort = null,
            TopologyPresentationExecutorMode mode = TopologyPresentationExecutorMode.Disabled)
        {
            _playbackPort = playbackPort;
            _mode = mode;
        }

        public TopologyExecutorDiagnostics Diagnostics { get; private set; }

        public void Prepare(PresentationPlaybackPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }
        }

        public void Play(PresentationPlaybackPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var observedTrackCount = 0;
            var routeCount = 0;
            var ignoredCount = 0;
            var invalidTrackCount = 0;
            var missingPortCount = 0;

            for (var i = 0; i < plan.Tracks.Count; i++)
            {
                var track = plan.Tracks[i];
                if (!IsTopologyTransitionTrack(track))
                {
                    continue;
                }

                observedTrackCount++;
                if (!TryCreateRequest(plan.TickIndex, track, out var request))
                {
                    invalidTrackCount++;
                    ignoredCount++;
                    continue;
                }

                if (_mode != TopologyPresentationExecutorMode.EnabledForTests)
                {
                    ignoredCount++;
                    continue;
                }

                if (_playbackPort == null)
                {
                    missingPortCount++;
                    ignoredCount++;
                    continue;
                }

                _playbackPort.BeginOrRefreshTopologyTransition(request);
                _hasRoutedRequest = true;
                routeCount++;
            }

            Diagnostics = new TopologyExecutorDiagnostics(
                observedTrackCount,
                routeCount,
                ignoredCount,
                invalidTrackCount,
                missingPortCount);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (_mode == TopologyPresentationExecutorMode.EnabledForTests &&
                _hasRoutedRequest)
            {
                _playbackPort?.UpdatePresentation(deltaTime);
            }
        }

        public void ResetSession()
        {
            _hasRoutedRequest = false;
            Diagnostics = default;
            if (_mode == TopologyPresentationExecutorMode.EnabledForTests)
            {
                _playbackPort?.ResetSession();
            }
        }

        public void HardCleanup()
        {
            _hasRoutedRequest = false;
            Diagnostics = default;
            if (_mode == TopologyPresentationExecutorMode.EnabledForTests)
            {
                _playbackPort?.HardCleanup();
            }
        }

        private static bool IsTopologyTransitionTrack(in PresentationPlaybackTrack track)
        {
            var cue = track.Cue;
            return cue.Domain == PresentationDomain.Topology &&
                   cue.Key.Domain == PresentationDomain.Topology &&
                   cue.Key.LocalKey == (int)PresentationTopologyCueKey.Transition;
        }

        private static bool TryCreateRequest(
            int tickIndex,
            in PresentationPlaybackTrack track,
            out TopologyTransitionPlaybackRequest request)
        {
            var payload = track.Cue.TopologyPayload;
            if (payload.RotationKind == CubeRotationKind.None)
            {
                request = default;
                return false;
            }

            request = new TopologyTransitionPlaybackRequest(
                tickIndex,
                payload.SourceTopology,
                payload.DestinationTopology,
                payload.RotationKind,
                payload.SourceTickIndex,
                payload.HasSourceMetadata,
                payload.SourceMetadataKey);
            return true;
        }
    }

    internal sealed class GameplayTopologyTransitionPlaybackPort : ITopologyTransitionPlaybackPort
    {
        private readonly GameplayTopologyTransitionController _controller;
        private CubeTopologyState _committedTopology;

        public GameplayTopologyTransitionPlaybackPort(GameplayTopologyTransitionController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public bool IsTransitionActive => _controller.HasActiveBoardRotationTween;

        public void BeginOrRefreshTopologyTransition(TopologyTransitionPlaybackRequest request)
        {
            var topologyMotion = new TickTopologyMotion(
                request.SourceTopology,
                request.DestinationTopology,
                request.RotationKind);
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion,
                Array.Empty<TickVisibilityChange>());

            _committedTopology = request.DestinationTopology;
            _controller.RefreshTopologyTrack(presentationData, _committedTopology);
            _controller.RefreshBoardSurfaceTransition(presentationData, _committedTopology);
        }

        public void UpdatePresentation(float deltaTime)
        {
            _controller.UpdatePresentation(deltaTime, _committedTopology);
        }

        public void ResetSession()
        {
            _committedTopology = default;
            _controller.Reset();
        }

        public void HardCleanup()
        {
            ResetSession();
        }
    }
}

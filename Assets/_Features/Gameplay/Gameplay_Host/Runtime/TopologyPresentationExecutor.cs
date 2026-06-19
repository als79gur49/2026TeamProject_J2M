using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct TopologyProductionTelemetrySnapshot
    {
        public TopologyProductionTelemetrySnapshot(
            TopologyPresentationExecutionMode currentMode,
            bool isProductionDefaultOwner,
            TopologyPresentationExecutionMode productionDefaultMode,
            TopologyPresentationExecutionMode rollbackMode,
            int lastTickIndex,
            CubeTopologyState lastSourceTopology,
            CubeTopologyState lastDestinationTopology,
            CubeRotationKind lastRotationKind,
            int lastSourceTickIndex,
            bool lastHasSourceMetadata,
            int lastSourceMetadataKey,
            TopologyPresentationExecutionOwner lastExecutionOwner,
            int legacyOwnerAttemptCount,
            int legacyOwnerExecutedCount,
            int legacyOwnerSkippedByPolicyCount,
            int executorOwnerAttemptCount,
            int executorOwnerExecutedCount,
            int executorOwnerSkippedByPolicyCount,
            int duplicateOwnerAttemptCount,
            int observedTrackCount,
            int routeCount,
            int ignoredCount,
            int invalidTrackCount,
            int missingPortCount,
            bool hasBlockingPresentation,
            bool isTopologyTransitionActive,
            PresentationBlockingSnapshot blockingSnapshot)
        {
            CurrentMode = currentMode;
            IsProductionDefaultOwner = isProductionDefaultOwner;
            ProductionDefaultMode = productionDefaultMode;
            RollbackMode = rollbackMode;
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastSourceTopology = lastSourceTopology;
            LastDestinationTopology = lastDestinationTopology;
            LastRotationKind = lastRotationKind;
            LastSourceTickIndex = Math.Max(0, lastSourceTickIndex);
            LastHasSourceMetadata = lastHasSourceMetadata;
            LastSourceMetadataKey = Math.Max(0, lastSourceMetadataKey);
            LastExecutionOwner = lastExecutionOwner;
            LegacyOwnerAttemptCount = Math.Max(0, legacyOwnerAttemptCount);
            LegacyOwnerExecutedCount = Math.Max(0, legacyOwnerExecutedCount);
            LegacyOwnerSkippedByPolicyCount = Math.Max(0, legacyOwnerSkippedByPolicyCount);
            ExecutorOwnerAttemptCount = Math.Max(0, executorOwnerAttemptCount);
            ExecutorOwnerExecutedCount = Math.Max(0, executorOwnerExecutedCount);
            ExecutorOwnerSkippedByPolicyCount = Math.Max(0, executorOwnerSkippedByPolicyCount);
            DuplicateOwnerAttemptCount = Math.Max(0, duplicateOwnerAttemptCount);
            ObservedTrackCount = Math.Max(0, observedTrackCount);
            RouteCount = Math.Max(0, routeCount);
            IgnoredCount = Math.Max(0, ignoredCount);
            InvalidTrackCount = Math.Max(0, invalidTrackCount);
            MissingPortCount = Math.Max(0, missingPortCount);
            HasBlockingPresentation = hasBlockingPresentation;
            IsTopologyTransitionActive = isTopologyTransitionActive;
            BlockingSnapshot = blockingSnapshot;
        }

        public TopologyPresentationExecutionMode CurrentMode { get; }
        public bool IsProductionDefaultOwner { get; }
        public TopologyPresentationExecutionMode ProductionDefaultMode { get; }
        public TopologyPresentationExecutionMode RollbackMode { get; }
        public int LastTickIndex { get; }
        public CubeTopologyState LastSourceTopology { get; }
        public CubeTopologyState LastDestinationTopology { get; }
        public CubeRotationKind LastRotationKind { get; }
        public int LastSourceTickIndex { get; }
        public bool LastHasSourceMetadata { get; }
        public int LastSourceMetadataKey { get; }
        public TopologyPresentationExecutionOwner LastExecutionOwner { get; }
        public int LegacyOwnerAttemptCount { get; }
        public int LegacyOwnerExecutedCount { get; }
        public int LegacyOwnerSkippedByPolicyCount { get; }
        public int ExecutorOwnerAttemptCount { get; }
        public int ExecutorOwnerExecutedCount { get; }
        public int ExecutorOwnerSkippedByPolicyCount { get; }
        public int DuplicateOwnerAttemptCount { get; }
        public int ObservedTrackCount { get; }
        public int RouteCount { get; }
        public int IgnoredCount { get; }
        public int InvalidTrackCount { get; }
        public int MissingPortCount { get; }
        public bool HasBlockingPresentation { get; }
        public bool IsTopologyTransitionActive { get; }
        public PresentationBlockingSnapshot BlockingSnapshot { get; }
    }

    internal static class TopologyProductionTelemetryBuilder
    {
        public static TopologyExecutorDiagnostics ResolveExecutorDiagnostics(GameplayPresentationPipeline pipeline)
        {
            if (pipeline == null)
            {
                return default;
            }

            var executors = pipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is TopologyPresentationExecutor executor)
                {
                    return executor.Diagnostics;
                }
            }

            return default;
        }

        public static TopologyProductionTelemetrySnapshot Build(
            TopologyPresentationExecutionMode mode,
            TopologyPresentationOwnershipDiagnostics ownership,
            GameplayPresentationPipeline pipeline,
            bool hasBlockingPresentation,
            bool isTopologyTransitionActive,
            PresentationBlockingSnapshot presentationBlockingSnapshot,
            PresentationBlockingSnapshot topologyExecutionBlockingSnapshot)
        {
            var executor = ResolveExecutorDiagnostics(pipeline);
            var lastTickIndex = executor.LastTickIndex > 0
                ? executor.LastTickIndex
                : ownership.LastExecutionTickIndex;

            return new TopologyProductionTelemetrySnapshot(
                mode,
                mode == TopologyPresentationExecutionMode.LegacyCoordinator,
                TopologyPresentationExecutionMode.LegacyCoordinator,
                TopologyPresentationExecutionMode.LegacyCoordinator,
                lastTickIndex,
                executor.LastSourceTopology,
                executor.LastDestinationTopology,
                executor.LastRotationKind,
                executor.LastSourceTickIndex,
                executor.LastHasSourceMetadata || ownership.LastExecutionHasSourceMetadata,
                Math.Max(executor.LastSourceMetadataKey, ownership.LastExecutionSourceMetadataKey),
                ownership.LastExecutionOwner,
                ownership.LegacyAttemptCount,
                ownership.ExecutedByLegacyCount,
                ownership.SkippedLegacyBecauseExecutorOwnerCount,
                ownership.ExecutorAttemptCount,
                ownership.ExecutedByExecutorCount,
                ownership.SkippedExecutorBecauseLegacyOwnerCount,
                ownership.DuplicateAttemptCount,
                executor.ObservedTrackCount,
                executor.RouteCount,
                executor.IgnoredCount,
                executor.InvalidTrackCount,
                executor.MissingPortCount,
                hasBlockingPresentation,
                isTopologyTransitionActive,
                mode == TopologyPresentationExecutionMode.ExecutorBridge
                    ? topologyExecutionBlockingSnapshot
                    : presentationBlockingSnapshot);
        }
    }

    public enum TopologyPresentationExecutionMode
    {
        LegacyCoordinator = 0,
        ExecutorBridge = 1,
    }

    internal enum TopologyPresentationExecutionOwner
    {
        None = 0,
        LegacyCoordinator = 1,
        ExecutorBridge = 2,
    }

    internal readonly struct TopologyPresentationOwnershipDiagnostics
    {
        public TopologyPresentationOwnershipDiagnostics(
            TopologyPresentationExecutionMode mode,
            int legacyAttemptCount,
            int executorAttemptCount,
            int executedByLegacyCount,
            int executedByExecutorCount,
            int skippedLegacyBecauseExecutorOwnerCount,
            int skippedExecutorBecauseLegacyOwnerCount,
            int duplicateAttemptCount,
            int lastExecutionTickIndex,
            bool lastExecutionHasSourceMetadata,
            int lastExecutionSourceMetadataKey,
            TopologyPresentationExecutionOwner lastExecutionOwner)
        {
            Mode = mode;
            LegacyAttemptCount = Math.Max(0, legacyAttemptCount);
            ExecutorAttemptCount = Math.Max(0, executorAttemptCount);
            ExecutedByLegacyCount = Math.Max(0, executedByLegacyCount);
            ExecutedByExecutorCount = Math.Max(0, executedByExecutorCount);
            SkippedLegacyBecauseExecutorOwnerCount = Math.Max(0, skippedLegacyBecauseExecutorOwnerCount);
            SkippedExecutorBecauseLegacyOwnerCount = Math.Max(0, skippedExecutorBecauseLegacyOwnerCount);
            DuplicateAttemptCount = Math.Max(0, duplicateAttemptCount);
            LastExecutionTickIndex = Math.Max(0, lastExecutionTickIndex);
            LastExecutionHasSourceMetadata = lastExecutionHasSourceMetadata;
            LastExecutionSourceMetadataKey = Math.Max(0, lastExecutionSourceMetadataKey);
            LastExecutionOwner = lastExecutionOwner;
        }

        public TopologyPresentationExecutionMode Mode { get; }

        public int LegacyAttemptCount { get; }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByLegacyCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int SkippedLegacyBecauseExecutorOwnerCount { get; }

        public int SkippedExecutorBecauseLegacyOwnerCount { get; }

        public int DuplicateAttemptCount { get; }

        public int LastExecutionTickIndex { get; }

        public bool LastExecutionHasSourceMetadata { get; }

        public int LastExecutionSourceMetadataKey { get; }

        public TopologyPresentationExecutionOwner LastExecutionOwner { get; }
    }

    internal sealed class TopologyPresentationExecutionGuard
    {
        private readonly bool _throwOnDuplicate;
        private TopologyPresentationExecutionMode _mode;
        private int _legacyAttemptCount;
        private int _executorAttemptCount;
        private int _executedByLegacyCount;
        private int _executedByExecutorCount;
        private int _skippedLegacyBecauseExecutorOwnerCount;
        private int _skippedExecutorBecauseLegacyOwnerCount;
        private int _duplicateAttemptCount;
        private int _lastExecutionTickIndex;
        private int _lastExecutionSourceMetadataKey;
        private bool _hasLastExecution;
        private bool _lastExecutionHasSourceMetadata;
        private TopologyPresentationExecutionOwner _lastExecutionOwner;

        public TopologyPresentationExecutionGuard(
            TopologyPresentationExecutionMode mode = TopologyPresentationExecutionMode.LegacyCoordinator,
            bool throwOnDuplicate = false)
        {
            _mode = NormalizeMode(mode);
            _throwOnDuplicate = throwOnDuplicate;
        }

        public TopologyPresentationOwnershipDiagnostics Diagnostics =>
            new(
                _mode,
                _legacyAttemptCount,
                _executorAttemptCount,
                _executedByLegacyCount,
                _executedByExecutorCount,
                _skippedLegacyBecauseExecutorOwnerCount,
                _skippedExecutorBecauseLegacyOwnerCount,
                _duplicateAttemptCount,
                _lastExecutionTickIndex,
                _lastExecutionHasSourceMetadata,
                _lastExecutionSourceMetadataKey,
                _lastExecutionOwner);

        public void Configure(TopologyPresentationExecutionMode mode)
        {
            _mode = NormalizeMode(mode);
        }

        public void ResetSession()
        {
            _legacyAttemptCount = 0;
            _executorAttemptCount = 0;
            _executedByLegacyCount = 0;
            _executedByExecutorCount = 0;
            _skippedLegacyBecauseExecutorOwnerCount = 0;
            _skippedExecutorBecauseLegacyOwnerCount = 0;
            _duplicateAttemptCount = 0;
            _lastExecutionTickIndex = 0;
            _lastExecutionSourceMetadataKey = 0;
            _hasLastExecution = false;
            _lastExecutionHasSourceMetadata = false;
            _lastExecutionOwner = TopologyPresentationExecutionOwner.None;
        }

        public void RecordSkippedByPolicy(TopologyPresentationExecutionOwner skippedOwner)
        {
            RecordAttempt(skippedOwner);
            RecordPolicySkip(skippedOwner);
        }

        public bool TryBeginExecution(
            TopologyPresentationExecutionOwner owner,
            int tickIndex,
            bool hasSourceMetadata,
            int sourceMetadataKey)
        {
            if (owner == TopologyPresentationExecutionOwner.None)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), "Topology execution owner must be explicit.");
            }

            var normalizedTickIndex = Math.Max(0, tickIndex);
            var normalizedSourceMetadataKey = Math.Max(0, sourceMetadataKey);
            RecordAttempt(owner);

            if (_hasLastExecution &&
                _lastExecutionTickIndex == normalizedTickIndex &&
                _lastExecutionHasSourceMetadata == hasSourceMetadata &&
                _lastExecutionSourceMetadataKey == normalizedSourceMetadataKey)
            {
                _duplicateAttemptCount++;
                RecordPolicySkip(owner);
                if (_throwOnDuplicate)
                {
                    throw new InvalidOperationException(
                        "Duplicate topology presentation execution ownership was attempted for the same tick/source.");
                }

                return false;
            }

            if (!IsOwnerAllowed(owner))
            {
                RecordPolicySkip(owner);
                return false;
            }

            _hasLastExecution = true;
            _lastExecutionTickIndex = normalizedTickIndex;
            _lastExecutionHasSourceMetadata = hasSourceMetadata;
            _lastExecutionSourceMetadataKey = normalizedSourceMetadataKey;
            _lastExecutionOwner = owner;

            if (owner == TopologyPresentationExecutionOwner.LegacyCoordinator)
            {
                _executedByLegacyCount++;
            }
            else
            {
                _executedByExecutorCount++;
            }

            return true;
        }

        private static TopologyPresentationExecutionMode NormalizeMode(TopologyPresentationExecutionMode mode)
        {
            return Enum.IsDefined(typeof(TopologyPresentationExecutionMode), mode)
                ? mode
                : TopologyPresentationExecutionMode.LegacyCoordinator;
        }

        private bool IsOwnerAllowed(TopologyPresentationExecutionOwner owner)
        {
            return (_mode == TopologyPresentationExecutionMode.LegacyCoordinator &&
                    owner == TopologyPresentationExecutionOwner.LegacyCoordinator) ||
                   (_mode == TopologyPresentationExecutionMode.ExecutorBridge &&
                    owner == TopologyPresentationExecutionOwner.ExecutorBridge);
        }

        private void RecordAttempt(TopologyPresentationExecutionOwner owner)
        {
            if (owner == TopologyPresentationExecutionOwner.LegacyCoordinator)
            {
                _legacyAttemptCount++;
            }
            else if (owner == TopologyPresentationExecutionOwner.ExecutorBridge)
            {
                _executorAttemptCount++;
            }
        }

        private void RecordPolicySkip(TopologyPresentationExecutionOwner skippedOwner)
        {
            if (skippedOwner == TopologyPresentationExecutionOwner.LegacyCoordinator)
            {
                _skippedLegacyBecauseExecutorOwnerCount++;
            }
            else if (skippedOwner == TopologyPresentationExecutionOwner.ExecutorBridge)
            {
                _skippedExecutorBecauseLegacyOwnerCount++;
            }
        }
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
            int missingPortCount,
            int lastTickIndex = 0,
            CubeTopologyState lastSourceTopology = default,
            CubeTopologyState lastDestinationTopology = default,
            CubeRotationKind lastRotationKind = CubeRotationKind.None,
            int lastSourceTickIndex = 0,
            bool lastHasSourceMetadata = false,
            int lastSourceMetadataKey = 0)
        {
            ObservedTrackCount = Math.Max(0, observedTrackCount);
            RouteCount = Math.Max(0, routeCount);
            IgnoredCount = Math.Max(0, ignoredCount);
            InvalidTrackCount = Math.Max(0, invalidTrackCount);
            MissingPortCount = Math.Max(0, missingPortCount);
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastSourceTopology = lastSourceTopology;
            LastDestinationTopology = lastDestinationTopology;
            LastRotationKind = lastRotationKind;
            LastSourceTickIndex = Math.Max(0, lastSourceTickIndex);
            LastHasSourceMetadata = lastHasSourceMetadata;
            LastSourceMetadataKey = Math.Max(0, lastSourceMetadataKey);
        }

        public int ObservedTrackCount { get; }

        public int RouteCount { get; }

        public int IgnoredCount { get; }

        public int InvalidTrackCount { get; }

        public int MissingPortCount { get; }

        public int LastTickIndex { get; }

        public CubeTopologyState LastSourceTopology { get; }

        public CubeTopologyState LastDestinationTopology { get; }

        public CubeRotationKind LastRotationKind { get; }

        public int LastSourceTickIndex { get; }

        public bool LastHasSourceMetadata { get; }

        public int LastSourceMetadataKey { get; }
    }

    internal interface ITopologyTransitionPlaybackPort
    {
        bool IsTransitionActive { get; }

        void BeginOrRefreshTopologyTransition(TopologyTransitionPlaybackRequest request);

        void UpdatePresentation(float deltaTime);

        void ResetSession();

        void HardCleanup();
    }

    internal delegate GameplayPresentationPipeline TopologyExecutionPipelineFactory(
        TopologyPresentationExecutionMode mode,
        GameplayTopologyTransitionController controller,
        TopologyPresentationExecutionGuard executionGuard);

    internal sealed class TopologyPresentationExecutor : IPresentationTopologyExecutor
    {
        private readonly ITopologyTransitionPlaybackPort _playbackPort;
        private readonly TopologyPresentationExecutionMode _mode;
        private readonly TopologyPresentationExecutionGuard _executionGuard;
        private bool _hasRoutedRequest;

        public TopologyPresentationExecutor(
            ITopologyTransitionPlaybackPort playbackPort = null,
            TopologyPresentationExecutionMode mode = TopologyPresentationExecutionMode.LegacyCoordinator,
            TopologyPresentationExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
            _mode = mode;
            _executionGuard = executionGuard;
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
            var lastRequest = default(TopologyTransitionPlaybackRequest);
            var hasLastRequest = false;

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

                if (!TryClaimExecution(request))
                {
                    ignoredCount++;
                    continue;
                }

                lastRequest = request;
                hasLastRequest = true;

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
                missingPortCount,
                hasLastRequest ? lastRequest.TickIndex : 0,
                hasLastRequest ? lastRequest.SourceTopology : default,
                hasLastRequest ? lastRequest.DestinationTopology : default,
                hasLastRequest ? lastRequest.RotationKind : CubeRotationKind.None,
                hasLastRequest ? lastRequest.SourceTickIndex : 0,
                hasLastRequest && lastRequest.HasSourceMetadata,
                hasLastRequest ? lastRequest.SourceMetadataKey : 0);
        }

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (_mode == TopologyPresentationExecutionMode.ExecutorBridge &&
                _hasRoutedRequest)
            {
                _playbackPort?.UpdatePresentation(deltaTime);
            }
        }

        public void ResetSession()
        {
            _hasRoutedRequest = false;
            Diagnostics = default;
            if (_mode == TopologyPresentationExecutionMode.ExecutorBridge)
            {
                _playbackPort?.ResetSession();
            }
        }

        public void HardCleanup()
        {
            _hasRoutedRequest = false;
            Diagnostics = default;
            if (_mode == TopologyPresentationExecutionMode.ExecutorBridge)
            {
                _playbackPort?.HardCleanup();
            }
        }

        private bool TryClaimExecution(in TopologyTransitionPlaybackRequest request)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    TopologyPresentationExecutionOwner.ExecutorBridge,
                    request.SourceTickIndex,
                    request.HasSourceMetadata,
                    request.SourceMetadataKey);
            }

            return _mode == TopologyPresentationExecutionMode.ExecutorBridge;
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

    internal static class GameplayHostPresentationPipelineFactory
    {
        public static GameplayPresentationPipeline CreateTopologyExecutionPipeline(
            TopologyPresentationExecutionMode mode,
            GameplayTopologyTransitionController controller,
            TopologyPresentationExecutionGuard executionGuard)
        {
            if (mode != TopologyPresentationExecutionMode.ExecutorBridge)
            {
                return null;
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new TopologyCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                new IPresentationExecutor[]
                {
                    new TopologyPresentationExecutor(
                        new GameplayTopologyTransitionPlaybackPort(controller),
                        mode,
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreateDamageDeathVfxExecutionPipeline(
            DamageDeathVfxExecutionMode mode,
            IDamageDeathVfxPlaybackPort playbackPort,
            DamageDeathVfxExecutionGuard executionGuard)
        {
            if (mode != DamageDeathVfxExecutionMode.OrchestrationExecutor)
            {
                return null;
            }

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new VfxCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                new IPresentationExecutor[]
                {
                    new GameplayVfxPresentationExecutor(
                        playbackPort,
                        mode,
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreateBoxMotionExecutionPipeline(
            BoxMotionPresentationExecutionMode mode,
            IGameplayMotionPlaybackPort playbackPort,
            BoxMotionExecutionGuard executionGuard)
        {
            if (mode != BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor)
            {
                return null;
            }

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new MotionCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                new IPresentationExecutor[]
                {
                    new GameplayMotionPresentationExecutor(
                        playbackPort,
                        mode,
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreatePlayerActionAnimationExecutionPipeline(
            PlayerActionAnimationExecutionMode mode,
            IGameplayAnimationPlaybackPort playbackPort,
            PlayerActionAnimationExecutionGuard executionGuard)
        {
            if (mode != PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor)
            {
                return null;
            }

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new AnimationCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                new IPresentationExecutor[]
                {
                    new GameplayAnimationPresentationExecutor(
                        playbackPort,
                        mode,
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreateEnemyPresentationExecutionPipeline(
            EnemyPresentationExecutionMode mode,
            IGameplayEnemyPresentationPlaybackPort playbackPort,
            EnemyPresentationExecutionGuard executionGuard)
        {
            if (mode != EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor)
            {
                return null;
            }

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new EnemyPresentationCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                new IPresentationExecutor[]
                {
                    new GameplayEnemyPresentationExecutor(
                        playbackPort,
                        mode,
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreateCoreGameplaySfxExecutionPipeline(
            CoreGameplaySfxExecutionMode mode,
            IGameplaySfxPlaybackPort playbackPort,
            CoreGameplaySfxExecutionGuard executionGuard)
        {
            if (mode != CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor)
            {
                return null;
            }

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new SfxCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                new IPresentationExecutor[]
                {
                    new GameplaySfxPresentationExecutor(
                        playbackPort,
                        mode,
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreateActionAudioExecutionPipeline(
            ActionAudioExecutionMode mode,
            IGameplayActionAudioPlaybackPort playbackPort,
            ActionAudioExecutionGuard executionGuard)
        {
            if (mode != ActionAudioExecutionMode.OrchestrationActionAudioBridge)
            {
                return null;
            }

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new ActionAudioCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                new IPresentationExecutor[]
                {
                    new GameplayActionAudioPresentationExecutor(
                        playbackPort,
                        mode,
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreateEnemyAudioExecutionPipeline(
            EnemyAudioExecutionMode mode,
            IGameplayEnemyAudioPlaybackPort playbackPort,
            EnemyAudioExecutionGuard executionGuard)
        {
            if (mode != EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge)
            {
                return null;
            }

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new EnemyAudioCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                new IPresentationExecutor[]
                {
                    new GameplayEnemyAudioPresentationExecutor(
                        playbackPort,
                        mode,
                        executionGuard),
                });
        }
    }
}

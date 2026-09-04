using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct BoxMotionPreparation
    {
        private readonly IReadOnlyList<BoxMotionPlaybackKey> _playbackKeys;

        public BoxMotionPreparation(
            int tickIndex,
            int token,
            IReadOnlyList<BoxMotionPlaybackKey> playbackKeys)
        {
            TickIndex = Math.Max(0, tickIndex);
            Token = Math.Max(0, token);
            _playbackKeys = playbackKeys ?? Array.Empty<BoxMotionPlaybackKey>();
        }

        internal int TickIndex { get; }

        internal int Token { get; }

        internal IReadOnlyList<BoxMotionPlaybackKey> PlaybackKeys => _playbackKeys;
    }

    internal interface IBoxMotionRuntimeCleanupPort
    {
        void ResetSession(BoxMotionTelemetryCleanupReason reason);

        void HardCleanup(BoxMotionTelemetryCleanupReason reason);
    }

    internal sealed class BoxMotionPresentationLaneRuntime
    {
        private static readonly IReadOnlyList<BoxMotionPlaybackKey> EmptyPlaybackKeys =
            Array.Empty<BoxMotionPlaybackKey>();

        private readonly IBoxMotionRuntimeCleanupPort _cleanupPort;
        private readonly GameplayMotionTrackPlannerPlaybackPort _defaultPlaybackPort;
        private readonly BoxMotionExecutionGuard _executionGuard;
        private readonly BoxMotionExecutionPipelineFactory _executionPipelineFactory;
        private readonly GameplayPresentationTrackState _trackState;

        private GameplayPresentationPipeline _executionPipeline;
        private IGameplayMotionPlaybackPort _playbackPort;
        private bool _useDefaultPlaybackPort = true;
        private int _lastConsumedPreparationToken;
        private int _lastPreparationToken;
        private int _nextPreparationToken;

        public BoxMotionPresentationLaneRuntime(
            GameplayPresentationTrackState trackState,
            BoxMotionExecutionPipelineFactory executionPipelineFactory,
            GameplayMotionTrackPlannerPlaybackPort defaultPlaybackPort,
            IBoxMotionRuntimeCleanupPort cleanupPort,
            BoxMotionExecutionGuard executionGuard = null)
        {
            _trackState = trackState ?? throw new ArgumentNullException(nameof(trackState));
            _executionPipelineFactory = executionPipelineFactory ??
                                        GameplayHostPresentationPipelineFactory.CreateBoxMotionExecutionPipeline;
            _defaultPlaybackPort = defaultPlaybackPort;
            _cleanupPort = cleanupPort;
            _executionGuard = executionGuard ?? new BoxMotionExecutionGuard();
        }

        public BoxMotionOwnershipDiagnostics OwnershipDiagnostics =>
            _executionGuard.Diagnostics;

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public GameplayMotionExecutorDiagnostics ExecutorDiagnostics =>
            ResolveExecutorDiagnostics();

        public BoxMotionPresentationRuntimeDebugSnapshot RuntimeDebugSnapshot =>
            new(
                _trackState.LocalMotionTracks.Count,
                _trackState.OriginalViewMotionTracks.Count,
                _trackState.CompletedPresentationMotions.Count,
                _trackState.CompletedMotionTrackIds.Count,
                _trackState.CompletedOriginalViewMotionTrackIds.Count,
                _trackState.MotionVisualScaleEntityIds.Count,
                _trackState.FlipInteractionTracks.Count,
                _trackState.FlipInteractionResetRequests.Count,
                _trackState.CompletedFlipInteractionTrackIds.Count,
                _defaultPlaybackPort?.Diagnostics ?? default);

        public BoxMotionProductionTelemetrySnapshot ProductionTelemetrySnapshot =>
            BuildProductionTelemetrySnapshot();

        public void ConfigurePlaybackPort(
            IGameplayMotionPlaybackPort playbackPort,
            bool useDefaultPlaybackPort = true)
        {
            _playbackPort = playbackPort;
            _useDefaultPlaybackPort = useDefaultPlaybackPort;
            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public BoxMotionPreparation Prepare(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityPose> previousCommittedLocalTargetPoses,
            CubeTopologyState previousCommittedTopology,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var playbackKeys = BuildBoxMotionPlaybackKeys(result);
            if (ResolvePlaybackPort() is GameplayMotionTrackPlannerPlaybackPort adapter)
            {
                adapter.BeginTickContext(
                    result,
                    previousCommittedLocalTargetPoses,
                    previousCommittedTopology,
                    projector,
                    timingProfile);
            }

            var token = ++_nextPreparationToken;
            _lastPreparationToken = token;
            return new BoxMotionPreparation(
                result.TickIndex,
                token,
                playbackKeys);
        }

        public void PresentPrepared(
            TickResult result,
            BoxMotionPreparation preparation,
            float deltaSeconds)
        {
            if (result == null ||
                preparation.Token == 0 ||
                preparation.Token != _lastPreparationToken ||
                preparation.Token == _lastConsumedPreparationToken ||
                preparation.TickIndex != result.TickIndex)
            {
                return;
            }

            _lastConsumedPreparationToken = preparation.Token;
            _executionPipeline ??= CreateExecutionPipeline();
            _executionPipeline?.Present(result);
        }

        public void Update(float deltaSeconds)
        {
            _executionPipeline?.Update(deltaSeconds);
        }

        public void ResetSession(BoxMotionTelemetryCleanupReason cleanupReason = BoxMotionTelemetryCleanupReason.None)
        {
            ResetExecutionSession();
            _executionPipeline?.ResetSession();
            if (cleanupReason != BoxMotionTelemetryCleanupReason.None)
            {
                _cleanupPort?.ResetSession(cleanupReason);
            }
        }

        public void HardCleanup(BoxMotionTelemetryCleanupReason cleanupReason = BoxMotionTelemetryCleanupReason.None)
        {
            _executionPipeline?.HardCleanup();
            ResetExecutionSession();
            if (cleanupReason != BoxMotionTelemetryCleanupReason.None)
            {
                _cleanupPort?.HardCleanup(cleanupReason);
            }
        }

        private GameplayPresentationPipeline CreateExecutionPipeline()
        {
            return _executionPipelineFactory(
                ResolvePlaybackPort(),
                _executionGuard);
        }

        private IGameplayMotionPlaybackPort ResolvePlaybackPort()
        {
            return _playbackPort ??
                   (_useDefaultPlaybackPort ? _defaultPlaybackPort : null);
        }

        private void ResetExecutionSession()
        {
            _executionGuard.ResetSession();
            _lastPreparationToken = 0;
            _lastConsumedPreparationToken = 0;
        }

        private static IReadOnlyList<BoxMotionPlaybackKey> BuildBoxMotionPlaybackKeys(TickResult result)
        {
            if (result == null)
            {
                return EmptyPlaybackKeys;
            }

            var factFrame = new TickPresentationFactExtractor().Extract(result);
            if (factFrame.Facts.Count == 0)
            {
                return EmptyPlaybackKeys;
            }

            var keys = new List<BoxMotionPlaybackKey>();
            for (var i = 0; i < factFrame.Facts.Count; i++)
            {
                var fact = factFrame.Facts[i];
                if (!fact.MotionPayload.IsValid ||
                    !TryMapMotionFactKind(fact.MotionPayload.Kind, out var cueKey))
                {
                    continue;
                }

                var payload = fact.MotionPayload;
                keys.Add(new BoxMotionPlaybackKey(
                    fact.Source.TickIndex,
                    fact.Source.SemanticSource,
                    payload.EntityId,
                    cueKey,
                    payload.SourceCell,
                    payload.DestinationCell,
                    payload.SourceActionPlanId,
                    payload.SourceSequenceId));
            }

            return keys.Count > 0 ? keys : EmptyPlaybackKeys;
        }

        private static bool TryMapMotionFactKind(
            PresentationMotionFactKind factKind,
            out PresentationMotionCueKey cueKey)
        {
            switch (factKind)
            {
                case PresentationMotionFactKind.BoxSlide:
                    cueKey = PresentationMotionCueKey.BoxSlide;
                    return true;
                case PresentationMotionFactKind.BoxFlip:
                    cueKey = PresentationMotionCueKey.BoxFlip;
                    return true;
                case PresentationMotionFactKind.BoxFlipImpact:
                    cueKey = PresentationMotionCueKey.BoxFlipImpact;
                    return true;
                default:
                    cueKey = PresentationMotionCueKey.None;
                    return false;
            }
        }

        private GameplayMotionExecutorDiagnostics ResolveExecutorDiagnostics()
        {
            if (_executionPipeline == null)
            {
                return BuildEmptyExecutorDiagnostics();
            }

            var executors = _executionPipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplayMotionPresentationExecutor executor)
                {
                    return executor.Diagnostics.IsCurrentProductionOwner
                        ? executor.Diagnostics
                        : BuildEmptyExecutorDiagnostics();
                }
            }

            return BuildEmptyExecutorDiagnostics();
        }

        private static GameplayMotionExecutorDiagnostics BuildEmptyExecutorDiagnostics()
        {
            return new GameplayMotionExecutorDiagnostics(
                observedTrackCount: 0,
                targetMissingCount: 0,
                anchorMissingCount: 0,
                bindingMissingCount: 0,
                driverMissingCount: 0,
                duplicateRejectedCount: 0,
                playbackRequestedCount: 0,
                trackStartedCount: 0,
                missingPortCount: 0);
        }

        private BoxMotionProductionTelemetrySnapshot BuildProductionTelemetrySnapshot()
        {
            var ownership = _executionGuard.Diagnostics;
            var executor = ResolveExecutorDiagnostics();
            var runtime = RuntimeDebugSnapshot;
            var telemetry = _trackState.BoxMotionTelemetry;
            var cleanup = telemetry.CleanupDiagnostics;
            var plannedCount = executor.ObservedTrackCount;
            var requestedCount = executor.PlaybackRequestedCount;
            var startedCount = executor.TrackStartedCount;
            var activeTrackCount =
                runtime.ActiveLocalMotionTrackCount +
                runtime.ActiveOriginalViewMotionTrackCount +
                runtime.FlipInteractionTrackCount;
            var pendingTrackCount = Math.Max(0, requestedCount - startedCount);

            return new BoxMotionProductionTelemetrySnapshot(
                executor.LastTickIndex,
                executor.LastCueKey,
                executor.LastDedupeKey,
                executor.LastTargetEntityId,
                executor.LastMotionFactKind,
                executor.LastFailureReason,
                cleanup.LastCleanupReason,
                ownership.ExecutorAttemptCount,
                ownership.ExecutedByExecutorCount,
                ownership.DuplicateAttemptCount,
                Math.Max(executor.DuplicateRejectedCount, ownership.DuplicateAttemptCount),
                plannedCount,
                requestedCount,
                startedCount,
                telemetry.PlaybackTrackCompletedCount,
                telemetry.PlaybackTrackCanceledCount,
                telemetry.PlaybackTrackIgnoredCount,
                activeTrackCount,
                pendingTrackCount,
                runtime.CompletedPresentationMotionKeyCount,
                runtime.DefaultAdapterDiagnostics,
                cleanup,
                executor.TargetMissingCount,
                executor.AnchorMissingCount,
                executor.BindingMissingCount,
                executor.DriverMissingCount,
                executor.MissingPortCount,
                executor.UnsupportedSemanticCount,
                MergeBoxMotionSemanticDiagnostics(executor.SemanticDiagnostics, telemetry.SemanticDiagnostics));
        }

        private static IReadOnlyList<BoxMotionSemanticDiagnostics> MergeBoxMotionSemanticDiagnostics(
            IReadOnlyList<BoxMotionSemanticDiagnostics> executorDiagnostics,
            IReadOnlyList<BoxMotionSemanticDiagnostics> runtimeDiagnostics)
        {
            var result = new BoxMotionSemanticDiagnostics[3];
            result[0] = MergeBoxMotionSemanticDiagnostics(
                PresentationMotionFactKind.BoxSlide,
                PresentationMotionCueKey.BoxSlide,
                executorDiagnostics,
                runtimeDiagnostics);
            result[1] = MergeBoxMotionSemanticDiagnostics(
                PresentationMotionFactKind.BoxFlip,
                PresentationMotionCueKey.BoxFlip,
                executorDiagnostics,
                runtimeDiagnostics);
            result[2] = MergeBoxMotionSemanticDiagnostics(
                PresentationMotionFactKind.BoxFlipImpact,
                PresentationMotionCueKey.BoxFlipImpact,
                executorDiagnostics,
                runtimeDiagnostics);
            return result;
        }

        private static BoxMotionSemanticDiagnostics MergeBoxMotionSemanticDiagnostics(
            PresentationMotionFactKind semantic,
            PresentationMotionCueKey cueKey,
            IReadOnlyList<BoxMotionSemanticDiagnostics> executorDiagnostics,
            IReadOnlyList<BoxMotionSemanticDiagnostics> runtimeDiagnostics)
        {
            var executor = FindBoxMotionSemanticDiagnostics(executorDiagnostics, semantic);
            var runtime = FindBoxMotionSemanticDiagnostics(runtimeDiagnostics, semantic);
            var lastTickIndex = runtime.LastTickIndex > 0 ? runtime.LastTickIndex : executor.LastTickIndex;
            var lastEntityId = runtime.LastEntityId > 0 ? runtime.LastEntityId : executor.LastEntityId;
            var lastDedupeKey = runtime.LastDedupeKey != 0 ? runtime.LastDedupeKey : executor.LastDedupeKey;

            return new BoxMotionSemanticDiagnostics(
                semantic,
                cueKey,
                executor.PlannedCount,
                executor.RequestedCount,
                executor.StartedCount,
                runtime.CompletedCount,
                executor.DuplicateRejectedCount,
                executor.MissingDependencyCount + runtime.MissingDependencyCount,
                runtime.CleanupCount,
                lastTickIndex,
                lastEntityId,
                lastDedupeKey);
        }

        private static BoxMotionSemanticDiagnostics FindBoxMotionSemanticDiagnostics(
            IReadOnlyList<BoxMotionSemanticDiagnostics> diagnostics,
            PresentationMotionFactKind semantic)
        {
            if (diagnostics == null)
            {
                return default;
            }

            for (var i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Semantic == semantic)
                {
                    return diagnostics[i];
                }
            }

            return default;
        }
    }
}

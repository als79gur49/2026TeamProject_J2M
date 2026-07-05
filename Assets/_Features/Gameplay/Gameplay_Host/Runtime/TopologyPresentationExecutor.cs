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
            bool isProductionDefaultOwner,
            int lastTickIndex,
            CubeTopologyState lastSourceTopology,
            CubeTopologyState lastDestinationTopology,
            CubeRotationKind lastRotationKind,
            int lastSourceTickIndex,
            bool lastHasSourceMetadata,
            int lastSourceMetadataKey,
            int executorOwnerAttemptCount,
            int executorOwnerExecutedCount,
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
            IsProductionDefaultOwner = isProductionDefaultOwner;
            LastTickIndex = Math.Max(0, lastTickIndex);
            LastSourceTopology = lastSourceTopology;
            LastDestinationTopology = lastDestinationTopology;
            LastRotationKind = lastRotationKind;
            LastSourceTickIndex = Math.Max(0, lastSourceTickIndex);
            LastHasSourceMetadata = lastHasSourceMetadata;
            LastSourceMetadataKey = Math.Max(0, lastSourceMetadataKey);
            ExecutorOwnerAttemptCount = Math.Max(0, executorOwnerAttemptCount);
            ExecutorOwnerExecutedCount = Math.Max(0, executorOwnerExecutedCount);
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

        public bool IsProductionDefaultOwner { get; }
        public int LastTickIndex { get; }
        public CubeTopologyState LastSourceTopology { get; }
        public CubeTopologyState LastDestinationTopology { get; }
        public CubeRotationKind LastRotationKind { get; }
        public int LastSourceTickIndex { get; }
        public bool LastHasSourceMetadata { get; }
        public int LastSourceMetadataKey { get; }
        public int ExecutorOwnerAttemptCount { get; }
        public int ExecutorOwnerExecutedCount { get; }
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
                true,
                lastTickIndex,
                executor.LastSourceTopology,
                executor.LastDestinationTopology,
                executor.LastRotationKind,
                executor.LastSourceTickIndex,
                executor.LastHasSourceMetadata || ownership.LastExecutionHasSourceMetadata,
                Math.Max(executor.LastSourceMetadataKey, ownership.LastExecutionSourceMetadataKey),
                ownership.ExecutorAttemptCount,
                ownership.ExecutedByExecutorCount,
                ownership.DuplicateAttemptCount,
                executor.ObservedTrackCount,
                executor.RouteCount,
                executor.IgnoredCount,
                executor.InvalidTrackCount,
                executor.MissingPortCount,
                hasBlockingPresentation,
                isTopologyTransitionActive,
                topologyExecutionBlockingSnapshot);
        }
    }

    internal readonly struct TopologyPresentationOwnershipDiagnostics
    {
        public TopologyPresentationOwnershipDiagnostics(
            int executorAttemptCount,
            int executedByExecutorCount,
            int duplicateAttemptCount,
            int lastExecutionTickIndex,
            bool lastExecutionHasSourceMetadata,
            int lastExecutionSourceMetadataKey)
        {
            ExecutorAttemptCount = Math.Max(0, executorAttemptCount);
            ExecutedByExecutorCount = Math.Max(0, executedByExecutorCount);
            DuplicateAttemptCount = Math.Max(0, duplicateAttemptCount);
            LastExecutionTickIndex = Math.Max(0, lastExecutionTickIndex);
            LastExecutionHasSourceMetadata = lastExecutionHasSourceMetadata;
            LastExecutionSourceMetadataKey = Math.Max(0, lastExecutionSourceMetadataKey);
        }

        public int ExecutorAttemptCount { get; }

        public int ExecutedByExecutorCount { get; }

        public int DuplicateAttemptCount { get; }

        public int LastExecutionTickIndex { get; }

        public bool LastExecutionHasSourceMetadata { get; }

        public int LastExecutionSourceMetadataKey { get; }
    }

    internal sealed class TopologyPresentationExecutionGuard
    {
        private readonly bool _throwOnDuplicate;
        private int _executorAttemptCount;
        private int _executedByExecutorCount;
        private int _duplicateAttemptCount;
        private int _lastExecutionTickIndex;
        private int _lastExecutionSourceMetadataKey;
        private bool _hasLastExecution;
        private bool _lastExecutionHasSourceMetadata;

        public TopologyPresentationExecutionGuard(bool throwOnDuplicate = false)
        {
            _throwOnDuplicate = throwOnDuplicate;
        }

        public TopologyPresentationOwnershipDiagnostics Diagnostics =>
            new(
                _executorAttemptCount,
                _executedByExecutorCount,
                _duplicateAttemptCount,
                _lastExecutionTickIndex,
                _lastExecutionHasSourceMetadata,
                _lastExecutionSourceMetadataKey);

        public void ResetSession()
        {
            _executorAttemptCount = 0;
            _executedByExecutorCount = 0;
            _duplicateAttemptCount = 0;
            _lastExecutionTickIndex = 0;
            _lastExecutionSourceMetadataKey = 0;
            _hasLastExecution = false;
            _lastExecutionHasSourceMetadata = false;
        }

        public bool TryBeginExecution(
            int tickIndex,
            bool hasSourceMetadata,
            int sourceMetadataKey)
        {
            var normalizedTickIndex = Math.Max(0, tickIndex);
            var normalizedSourceMetadataKey = Math.Max(0, sourceMetadataKey);
            _executorAttemptCount++;

            if (_hasLastExecution &&
                _lastExecutionTickIndex == normalizedTickIndex &&
                _lastExecutionHasSourceMetadata == hasSourceMetadata &&
                _lastExecutionSourceMetadataKey == normalizedSourceMetadataKey)
            {
                _duplicateAttemptCount++;
                if (_throwOnDuplicate)
                {
                    throw new InvalidOperationException(
                        "Duplicate topology presentation execution ownership was attempted for the same tick/source.");
                }

                return false;
            }

            _hasLastExecution = true;
            _lastExecutionTickIndex = normalizedTickIndex;
            _lastExecutionHasSourceMetadata = hasSourceMetadata;
            _lastExecutionSourceMetadataKey = normalizedSourceMetadataKey;
            _executedByExecutorCount++;

            return true;
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

    internal interface ITopologyTransitionCleanupPort
    {
        void ResetTransition();
    }

    internal delegate GameplayPresentationPipeline TopologyExecutionPipelineFactory(
        ITopologyTransitionPlaybackPort playbackPort,
        TopologyPresentationExecutionGuard executionGuard);

    internal sealed class TopologyPresentationExecutor : IPresentationTopologyExecutor
    {
        private readonly ITopologyTransitionPlaybackPort _playbackPort;
        private readonly TopologyPresentationExecutionGuard _executionGuard;
        private bool _hasRoutedRequest;

        public TopologyPresentationExecutor(
            ITopologyTransitionPlaybackPort playbackPort = null,
            TopologyPresentationExecutionGuard executionGuard = null)
        {
            _playbackPort = playbackPort;
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

            if (_hasRoutedRequest)
            {
                _playbackPort?.UpdatePresentation(deltaTime);
            }
        }

        public void ResetSession()
        {
            _hasRoutedRequest = false;
            Diagnostics = default;
            _playbackPort?.ResetSession();
        }

        public void HardCleanup()
        {
            _hasRoutedRequest = false;
            Diagnostics = default;
            _playbackPort?.HardCleanup();
        }

        private bool TryClaimExecution(in TopologyTransitionPlaybackRequest request)
        {
            if (_executionGuard != null)
            {
                return _executionGuard.TryBeginExecution(
                    request.SourceTickIndex,
                    request.HasSourceMetadata,
                    request.SourceMetadataKey);
            }

            return true;
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
        }

        public void HardCleanup()
        {
            ResetSession();
        }
    }

    internal sealed class GameplayTopologyTransitionCleanupPort : ITopologyTransitionCleanupPort
    {
        private readonly GameplayTopologyTransitionController _controller;

        public GameplayTopologyTransitionCleanupPort(GameplayTopologyTransitionController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public void ResetTransition()
        {
            _controller.Reset();
        }
    }

    internal static class GameplayHostPresentationPipelineFactory
    {
        public static GameplayPresentationPipeline CreateTopologyExecutionPipeline(
            ITopologyTransitionPlaybackPort playbackPort,
            TopologyPresentationExecutionGuard executionGuard)
        {
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
                        playbackPort,
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreateDamageDeathVfxExecutionPipeline(
            IDamageDeathVfxPlaybackPort playbackPort,
            DamageDeathVfxExecutionGuard executionGuard)
        {
            return CreateDamageDeathVfxExecutionPipeline(
                playbackPort,
                executionGuard,
                null);
        }

        public static GameplayPresentationPipeline CreateDamageDeathVfxExecutionPipeline(
            IDamageDeathVfxPlaybackPort playbackPort,
            DamageDeathVfxExecutionGuard executionGuard,
            GameplayTimingProfile timingProfile)
        {
            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(timingProfile),
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
                        executionGuard,
                        timingProfile),
                });
        }

        public static GameplayPresentationPipeline CreateBoxMotionExecutionPipeline(
            IGameplayMotionPlaybackPort playbackPort,
            BoxMotionExecutionGuard executionGuard)
        {
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
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreatePlayerActionAnimationExecutionPipeline(
            PlayerActionAnimationExecutionMode mode,
            IGameplayAnimationPlaybackPort playbackPort,
            PlayerActionAnimationExecutionGuard executionGuard)
        {
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
            IGameplayEnemyPresentationPlaybackPort playbackPort,
            EnemyPresentationExecutionGuard executionGuard)
        {
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
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreateCoreGameplaySfxExecutionPipeline(
            IGameplaySfxPlaybackPort playbackPort,
            CoreGameplaySfxExecutionGuard executionGuard)
        {
            return CreateCoreGameplaySfxExecutionPipeline(
                playbackPort,
                executionGuard,
                null);
        }

        public static GameplayPresentationPipeline CreateCoreGameplaySfxExecutionPipeline(
            IGameplaySfxPlaybackPort playbackPort,
            CoreGameplaySfxExecutionGuard executionGuard,
            GameplayTimingProfile timingProfile)
        {
            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(timingProfile),
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
                        executionGuard),
                });
        }

        public static GameplayPresentationPipeline CreateActionAudioExecutionPipeline(
            IGameplayActionAudioPlaybackPort playbackPort)
        {
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
                    new GameplayActionAudioPresentationExecutor(playbackPort),
                });
        }

        public static GameplayPresentationPipeline CreateEnemyAudioExecutionPipeline(
            IGameplayEnemyAudioPlaybackPort playbackPort)
        {
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
                    new GameplayEnemyAudioPresentationExecutor(playbackPort),
                });
        }
    }
}

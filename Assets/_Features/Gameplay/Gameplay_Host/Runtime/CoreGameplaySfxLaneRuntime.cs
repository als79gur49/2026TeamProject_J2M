using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct CoreGameplaySfxCandidatePlan
    {
        private static readonly IReadOnlyList<GameplayAudioRequest> EmptyRequests = Array.Empty<GameplayAudioRequest>();
        private static readonly IReadOnlyList<CoreGameplaySfxPlaybackKey> EmptyPlaybackKeys =
            Array.Empty<CoreGameplaySfxPlaybackKey>();

        public CoreGameplaySfxCandidatePlan(
            IReadOnlyList<GameplayAudioRequest> requests,
            IReadOnlyList<CoreGameplaySfxPlaybackKey> playbackKeys,
            int tickIndex)
        {
            Requests = requests ?? EmptyRequests;
            PlaybackKeys = playbackKeys ?? EmptyPlaybackKeys;
            TickIndex = Math.Max(0, tickIndex);
        }

        public IReadOnlyList<GameplayAudioRequest> Requests { get; }

        public IReadOnlyList<CoreGameplaySfxPlaybackKey> PlaybackKeys { get; }

        public int TickIndex { get; }
    }

    internal readonly struct CoreGameplaySfxLaneDiagnostics
    {
        public CoreGameplaySfxLaneDiagnostics(
            CoreGameplaySfxExecutionMode executionMode,
            CoreGameplaySfxOwnershipDiagnostics ownership,
            GameplaySfxExecutorDiagnostics executor,
            PresentationBlockingSnapshot blockingSnapshot,
            int pendingRequestCount,
            int deferredRequestCount)
        {
            ExecutionMode = executionMode;
            Ownership = ownership;
            Executor = executor;
            BlockingSnapshot = blockingSnapshot;
            PendingRequestCount = Math.Max(0, pendingRequestCount);
            DeferredRequestCount = Math.Max(0, deferredRequestCount);
        }

        public CoreGameplaySfxExecutionMode ExecutionMode { get; }

        public CoreGameplaySfxOwnershipDiagnostics Ownership { get; }

        public GameplaySfxExecutorDiagnostics Executor { get; }

        public PresentationBlockingSnapshot BlockingSnapshot { get; }

        public int PendingRequestCount { get; }

        public int DeferredRequestCount { get; }
    }

    internal sealed class CoreGameplaySfxLaneRuntime
    {
        private static readonly IReadOnlyList<GameplayAudioRequest> EmptyGameplayAudioRequests =
            Array.Empty<GameplayAudioRequest>();
        private static readonly IReadOnlyList<CoreGameplaySfxPlaybackKey> EmptyPlaybackKeys =
            Array.Empty<CoreGameplaySfxPlaybackKey>();

        private readonly GameplayAudioRequestPlanner _requestPlanner;
        private readonly GameplayAudioPresentationController _legacyController;
        private readonly CoreGameplaySfxExecutionGuard _executionGuard;
        private readonly CoreGameplaySfxExecutionPipelineFactory _pipelineFactory;
        private readonly GameplaySfxPlaybackPortAdapter _playbackPortAdapter;
        private readonly HashSet<int> _playableEnemyDeathCueEntityIds = new();

        private GameplayPresentationPipeline _executionPipeline;
        private IGameplaySfxPlaybackPort _playbackPort;
        private bool _previousPlaybackGateBlocked;
        private bool _currentPlaybackGateBlocked;

        public CoreGameplaySfxLaneRuntime(
            GameplayPresentationStateStore stateStore,
            CoreGameplaySfxExecutionPipelineFactory pipelineFactory = null,
            GameplayAudioRequestPlanner requestPlanner = null,
            GameplayAudioPresentationController legacyController = null,
            CoreGameplaySfxExecutionGuard executionGuard = null,
            GameplaySfxPlaybackPortAdapter playbackPortAdapter = null)
        {
            if (stateStore == null)
            {
                throw new ArgumentNullException(nameof(stateStore));
            }

            _pipelineFactory = pipelineFactory ??
                               GameplayHostPresentationPipelineFactory.CreateCoreGameplaySfxExecutionPipeline;
            _requestPlanner = requestPlanner ?? new GameplayAudioRequestPlanner();
            _legacyController = legacyController ?? new GameplayAudioPresentationController(stateStore);
            _executionGuard = executionGuard ?? new CoreGameplaySfxExecutionGuard();
            _playbackPortAdapter = playbackPortAdapter ?? new GameplaySfxPlaybackPortAdapter(stateStore);
        }

        public CoreGameplaySfxExecutionMode ExecutionMode => _executionGuard.Diagnostics.Mode;

        public CoreGameplaySfxOwnershipDiagnostics OwnershipDiagnostics => _executionGuard.Diagnostics;

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public int PendingRequestCount => _legacyController.PendingRequestCount;

        public int DeferredRequestCount =>
            _legacyController.DeferredRequestCount + _playbackPortAdapter.DeferredRequestCount;

        public GameplaySfxExecutorDiagnostics ExecutorDiagnostics => ResolveExecutorDiagnostics();

        public CoreGameplaySfxLaneDiagnostics Diagnostics =>
            new(
                ExecutionMode,
                OwnershipDiagnostics,
                ExecutorDiagnostics,
                BlockingSnapshot,
                PendingRequestCount,
                DeferredRequestCount);

        public void ConfigureExecution(
            CoreGameplaySfxExecutionMode mode,
            IGameplaySfxPlaybackPort playbackPort = null)
        {
            _playbackPort = playbackPort;
            _executionGuard.Configure(CoreGameplaySfxExecutionPolicy.Normalize(mode));
            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public CoreGameplaySfxCandidatePlan BuildCandidatePlan(
            TickResult result,
            GameplayTimingProfile timingProfile)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var requests = CopyRequests(_requestPlanner.BuildRequests(result, timingProfile));
            var playbackKeys = CopyPlaybackKeys(BuildCoreGameplaySfxPlaybackKeys(result));
            return new CoreGameplaySfxCandidatePlan(requests, playbackKeys, result.TickIndex);
        }

        public void FinalizePlan(
            in CoreGameplaySfxCandidatePlan candidatePlan,
            bool isTopologyTransitionActive,
            IReadOnlyCollection<int> playableEnemyDeathCueEntityIds)
        {
            SetTopologyTransitionActive(isTopologyTransitionActive);
            CopyPlayableEnemyDeathCueEntityIds(playableEnemyDeathCueEntityIds);
            _playbackPortAdapter.ConfigureEnemyDeathCueSuppression(_playableEnemyDeathCueEntityIds);
            var filteredRequests = SuppressLethalEnemyDamageRequests(
                candidatePlan.Requests,
                _playableEnemyDeathCueEntityIds);

            if (UseProductionExecutor())
            {
                for (var i = 0; i < candidatePlan.PlaybackKeys.Count; i++)
                {
                    _executionGuard.RecordSkippedByPolicy(
                        CoreGameplaySfxExecutionOwner.LegacyGameplayAudioController);
                }

                _legacyController.ReplacePendingPlan(EmptyGameplayAudioRequests, candidatePlan.TickIndex);
                return;
            }

            _legacyController.ReplacePendingPlan(filteredRequests, candidatePlan.TickIndex);
            for (var i = 0; i < candidatePlan.PlaybackKeys.Count; i++)
            {
                _executionGuard.TryBeginExecution(
                    CoreGameplaySfxExecutionOwner.LegacyGameplayAudioController,
                    candidatePlan.PlaybackKeys[i]);
            }
        }

        public void PresentPrepared(TickResult result)
        {
            if (result == null ||
                !UseProductionExecutor())
            {
                return;
            }

            _executionPipeline ??= CreateExecutionPipeline();
            _executionPipeline?.Present(result);
        }

        public void CompletePrepared()
        {
            _legacyController.PlayPlannedAudio();
        }

        public void SetTopologyTransitionActive(bool isActive)
        {
            _previousPlaybackGateBlocked = _currentPlaybackGateBlocked;
            _currentPlaybackGateBlocked = isActive;
            var gateState = isActive
                ? GameplayAudioPlaybackGateState.TopologyLocked
                : GameplayAudioPlaybackGateState.Open;
            _legacyController.SetPlaybackGateState(gateState);
            _playbackPortAdapter.SetPlaybackGateState(gateState);
        }

        public void Update(float deltaTime)
        {
            var gameplayAudioDeltaTime = _previousPlaybackGateBlocked || _currentPlaybackGateBlocked
                ? 0f
                : deltaTime;
            _legacyController.Update(gameplayAudioDeltaTime);
            _playbackPortAdapter.Update();
            _executionPipeline?.Update(deltaTime);
        }

        public void AttachRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GameplayAudioMap gameplayAudioMap)
        {
            _legacyController.AttachRuntime(playbackPort, gameplayAudioMap);
            _playbackPortAdapter.AttachRuntime(playbackPort, gameplayAudioMap);
        }

        public void DetachRuntime()
        {
            _playbackPortAdapter.DetachRuntime();
            _legacyController.DetachRuntime();
        }

        public void ResetSession()
        {
            _legacyController.ResetSession();
            _playbackPortAdapter.ResetSession();
            ResetExecutionSession();
            _executionPipeline?.ResetSession();
        }

        public void HardCleanup()
        {
            _legacyController.ResetSession();
            _playbackPortAdapter.HardCleanup();
            _executionPipeline?.HardCleanup();
            ResetExecutionSession();
        }

        private void ResetExecutionSession()
        {
            _playableEnemyDeathCueEntityIds.Clear();
            _executionGuard.ResetSession();
        }

        private GameplayPresentationPipeline CreateExecutionPipeline()
        {
            return _pipelineFactory(
                ExecutionMode,
                ResolvePlaybackPort(),
                _executionGuard);
        }

        private IGameplaySfxPlaybackPort ResolvePlaybackPort()
        {
            return _playbackPort ?? _playbackPortAdapter;
        }

        private bool UseProductionExecutor()
        {
            return ExecutionMode == CoreGameplaySfxExecutionPolicy.ProductionDefault;
        }

        private GameplaySfxExecutorDiagnostics ResolveExecutorDiagnostics()
        {
            var ownershipDiagnostics = _executionGuard.Diagnostics;
            var adapterDiagnostics = _playbackPortAdapter.Diagnostics;
            if (_executionPipeline == null)
            {
                return BuildEmptyExecutorDiagnostics(ownershipDiagnostics, adapterDiagnostics);
            }

            var executors = _executionPipeline.Executors;
            for (var i = 0; i < executors.Count; i++)
            {
                if (executors[i] is GameplaySfxPresentationExecutor executor)
                {
                    return MergeCoreGameplaySfxDiagnostics(
                        executor.Diagnostics,
                        ownershipDiagnostics,
                        adapterDiagnostics);
                }
            }

            return BuildEmptyExecutorDiagnostics(ownershipDiagnostics, adapterDiagnostics);
        }

        private static GameplaySfxExecutorDiagnostics BuildEmptyExecutorDiagnostics(
            CoreGameplaySfxOwnershipDiagnostics ownershipDiagnostics,
            GameplaySfxPlaybackAdapterDiagnostics adapterDiagnostics)
        {
            return new GameplaySfxExecutorDiagnostics(
                ownershipDiagnostics.Mode,
                ownershipDiagnostics.Mode == CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor,
                ownershipDiagnostics.SkippedLegacyBecauseExecutorOwnerCount,
                observedCueCount: 0,
                semanticUnsupportedCount: 0,
                mapMissingCount: 0,
                bindingMissingCount: 0,
                targetMissingCount: 0,
                ownerViewMissingCount: 0,
                portMissingCount: 0,
                duplicateSuppressedCount: ownershipDiagnostics.DuplicateAttemptCount,
                legacyOwnerNoOpCount: ownershipDiagnostics.SkippedExecutorBecauseLegacyOwnerCount,
                requestPlannedCount: 0,
                playbackRequestedCount: 0,
                playbackSucceededCount: 0,
                playbackNoOpFallbackCount: 0,
                fallbackCount: 0,
                attachedLikePlaybackCount: adapterDiagnostics.AttachedLikePlaybackCount,
                twoDFallbackPlaybackCount: adapterDiagnostics.TwoDFallbackPlaybackCount,
                deferredDuringTopologyLockCount: adapterDiagnostics.DeferredDuringTopologyLockCount,
                deferredDrainCount: adapterDiagnostics.DeferredDrainCount,
                enemyDeathGenericCoreSfxSuppressedCount: adapterDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount,
                lethalEnemyDamageSuppressedByDeathCount: adapterDiagnostics.LethalEnemyDamageSuppressedByDeathCount,
                lastTickIndex: adapterDiagnostics.LastTickIndex,
                lastSemanticKey: adapterDiagnostics.LastSemanticKey,
                lastFallbackReason: adapterDiagnostics.LastFallbackReason,
                semanticDiagnostics: Array.Empty<GameplaySfxSemanticDiagnostics>());
        }

        private static GameplaySfxExecutorDiagnostics MergeCoreGameplaySfxDiagnostics(
            GameplaySfxExecutorDiagnostics executorDiagnostics,
            CoreGameplaySfxOwnershipDiagnostics ownershipDiagnostics,
            GameplaySfxPlaybackAdapterDiagnostics adapterDiagnostics)
        {
            var adapterFallbackCount =
                adapterDiagnostics.TwoDFallbackPlaybackCount +
                adapterDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount +
                adapterDiagnostics.LethalEnemyDamageSuppressedByDeathCount;
            var lastFallbackReason = adapterDiagnostics.LastFallbackReason != GameplaySfxFallbackReason.None
                ? adapterDiagnostics.LastFallbackReason
                : executorDiagnostics.LastFallbackReason;
            var lastSemanticKey = adapterDiagnostics.LastSemanticKey != PresentationSfxCueKey.None
                ? adapterDiagnostics.LastSemanticKey
                : executorDiagnostics.LastSemanticKey;
            var lastTickIndex = adapterDiagnostics.LastTickIndex > 0
                ? adapterDiagnostics.LastTickIndex
                : executorDiagnostics.LastTickIndex;

            return new GameplaySfxExecutorDiagnostics(
                ownershipDiagnostics.Mode,
                ownershipDiagnostics.Mode == CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor,
                ownershipDiagnostics.SkippedLegacyBecauseExecutorOwnerCount,
                executorDiagnostics.ObservedCueCount,
                executorDiagnostics.SemanticUnsupportedCount,
                executorDiagnostics.MapMissingCount,
                executorDiagnostics.BindingMissingCount,
                executorDiagnostics.TargetMissingCount,
                executorDiagnostics.OwnerViewMissingCount,
                executorDiagnostics.PortMissingCount,
                Math.Max(
                    executorDiagnostics.DuplicateSuppressedCount,
                    ownershipDiagnostics.DuplicateAttemptCount),
                executorDiagnostics.LegacyOwnerNoOpCount,
                executorDiagnostics.RequestPlannedCount,
                executorDiagnostics.PlaybackRequestedCount,
                executorDiagnostics.PlaybackSucceededCount,
                executorDiagnostics.PlaybackNoOpFallbackCount,
                Math.Max(executorDiagnostics.FallbackCount, adapterFallbackCount),
                adapterDiagnostics.AttachedLikePlaybackCount,
                adapterDiagnostics.TwoDFallbackPlaybackCount,
                adapterDiagnostics.DeferredDuringTopologyLockCount,
                adapterDiagnostics.DeferredDrainCount,
                adapterDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount,
                adapterDiagnostics.LethalEnemyDamageSuppressedByDeathCount,
                lastTickIndex,
                lastSemanticKey,
                lastFallbackReason,
                executorDiagnostics.SemanticDiagnostics);
        }

        private static IReadOnlyList<CoreGameplaySfxPlaybackKey> BuildCoreGameplaySfxPlaybackKeys(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new SfxCuePlanner(),
            }).Plan(factFrame);
            if (cueFrame.Cues.Count == 0)
            {
                return EmptyPlaybackKeys;
            }

            var keys = new List<CoreGameplaySfxPlaybackKey>(cueFrame.Cues.Count);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.Sfx ||
                    !cue.Key.TryGetSfxCueKey(out var cueKey) ||
                    cue.Target.Kind != PresentationTargetKind.Entity ||
                    cue.Target.EntityId <= 0)
                {
                    continue;
                }

                keys.Add(new CoreGameplaySfxPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    cue.Source.SourceEntityId,
                    cue.Target.EntityId,
                    cueKey));
            }

            return keys.Count == 0
                ? EmptyPlaybackKeys
                : keys;
        }

        private static IReadOnlyList<GameplayAudioRequest> SuppressLethalEnemyDamageRequests(
            IReadOnlyList<GameplayAudioRequest> gameplayAudioRequests,
            IReadOnlyCollection<int> playableDeathCueEntityIds)
        {
            if (gameplayAudioRequests.Count == 0 ||
                playableDeathCueEntityIds == null ||
                playableDeathCueEntityIds.Count == 0)
            {
                return gameplayAudioRequests;
            }

            List<GameplayAudioRequest> filteredRequests = null;
            for (var i = 0; i < gameplayAudioRequests.Count; i++)
            {
                var request = gameplayAudioRequests[i];
                if (ShouldSuppressLethalEnemyDamageRequest(request, playableDeathCueEntityIds))
                {
                    if (filteredRequests == null)
                    {
                        filteredRequests = new List<GameplayAudioRequest>(gameplayAudioRequests.Count);
                        for (var copyIndex = 0; copyIndex < i; copyIndex++)
                        {
                            filteredRequests.Add(gameplayAudioRequests[copyIndex]);
                        }
                    }

                    continue;
                }

                filteredRequests?.Add(request);
            }

            return filteredRequests ?? gameplayAudioRequests;
        }

        private static bool ShouldSuppressLethalEnemyDamageRequest(
            in GameplayAudioRequest request,
            IReadOnlyCollection<int> playableDeathCueEntityIds)
        {
            return request.SemanticId == GameplayAudioSemanticId.EnemyDamage &&
                   request.OwnerEntityId.HasValue &&
                   ContainsEntityId(playableDeathCueEntityIds, request.OwnerEntityId.Value);
        }

        private static bool ContainsEntityId(IReadOnlyCollection<int> entityIds, int entityId)
        {
            foreach (var candidate in entityIds)
            {
                if (candidate == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private void CopyPlayableEnemyDeathCueEntityIds(IReadOnlyCollection<int> entityIds)
        {
            _playableEnemyDeathCueEntityIds.Clear();
            if (entityIds == null ||
                entityIds.Count == 0)
            {
                return;
            }

            foreach (var entityId in entityIds)
            {
                if (entityId > 0)
                {
                    _playableEnemyDeathCueEntityIds.Add(entityId);
                }
            }
        }

        private static IReadOnlyList<GameplayAudioRequest> CopyRequests(IReadOnlyList<GameplayAudioRequest> requests)
        {
            if (requests == null ||
                requests.Count == 0)
            {
                return EmptyGameplayAudioRequests;
            }

            var copy = new GameplayAudioRequest[requests.Count];
            for (var i = 0; i < requests.Count; i++)
            {
                copy[i] = requests[i];
            }

            return copy;
        }

        private static IReadOnlyList<CoreGameplaySfxPlaybackKey> CopyPlaybackKeys(
            IReadOnlyList<CoreGameplaySfxPlaybackKey> playbackKeys)
        {
            if (playbackKeys == null ||
                playbackKeys.Count == 0)
            {
                return EmptyPlaybackKeys;
            }

            var copy = new CoreGameplaySfxPlaybackKey[playbackKeys.Count];
            for (var i = 0; i < playbackKeys.Count; i++)
            {
                copy[i] = playbackKeys[i];
            }

            return copy;
        }
    }
}

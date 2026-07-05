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
    internal readonly struct CoreGameplaySfxLaneDiagnostics
    {
        public CoreGameplaySfxLaneDiagnostics(
            CoreGameplaySfxOwnershipDiagnostics ownership,
            GameplaySfxExecutorDiagnostics executor,
            PresentationBlockingSnapshot blockingSnapshot,
            int pendingRequestCount,
            int deferredRequestCount)
        {
            Ownership = ownership;
            Executor = executor;
            BlockingSnapshot = blockingSnapshot;
            PendingRequestCount = Math.Max(0, pendingRequestCount);
            DeferredRequestCount = Math.Max(0, deferredRequestCount);
        }

        public CoreGameplaySfxOwnershipDiagnostics Ownership { get; }

        public GameplaySfxExecutorDiagnostics Executor { get; }

        public PresentationBlockingSnapshot BlockingSnapshot { get; }

        public int PendingRequestCount { get; }

        public int DeferredRequestCount { get; }
    }

    internal sealed class CoreGameplaySfxLaneRuntime
    {
        private readonly CoreGameplaySfxExecutionGuard _executionGuard;
        private readonly CoreGameplaySfxExecutionPipelineFactory _pipelineFactory;
        private readonly GameplaySfxPlaybackPortAdapter _playbackPortAdapter;
        private readonly HashSet<int> _playableEnemyDeathCueEntityIds = new();

        private GameplayPresentationPipeline _executionPipeline;
        private IGameplaySfxPlaybackPort _playbackPort;
        private GameplayTimingProfile _timingProfile;
        private bool _previousPlaybackGateBlocked;
        private bool _currentPlaybackGateBlocked;
        private bool _usesDefaultPipelineFactory;

        public CoreGameplaySfxLaneRuntime(
            GameplayPresentationStateStore stateStore,
            CoreGameplaySfxExecutionPipelineFactory pipelineFactory = null,
            CoreGameplaySfxExecutionGuard executionGuard = null,
            GameplaySfxPlaybackPortAdapter playbackPortAdapter = null)
        {
            if (stateStore == null)
            {
                throw new ArgumentNullException(nameof(stateStore));
            }

            _usesDefaultPipelineFactory = pipelineFactory == null;
            _pipelineFactory = pipelineFactory ?? CreateDefaultExecutionPipeline;
            _executionGuard = executionGuard ?? new CoreGameplaySfxExecutionGuard();
            _playbackPortAdapter = playbackPortAdapter ?? new GameplaySfxPlaybackPortAdapter(stateStore);
            _executionPipeline = CreateExecutionPipeline();
        }

        public CoreGameplaySfxOwnershipDiagnostics OwnershipDiagnostics => _executionGuard.Diagnostics;

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public int PendingRequestCount => 0;

        public int DeferredRequestCount => _playbackPortAdapter.DeferredRequestCount;

        public GameplaySfxExecutorDiagnostics ExecutorDiagnostics => ResolveExecutorDiagnostics();

        public CoreGameplaySfxLaneDiagnostics Diagnostics =>
            new(
                OwnershipDiagnostics,
                ExecutorDiagnostics,
                BlockingSnapshot,
                PendingRequestCount,
                DeferredRequestCount);

        public void ConfigurePlaybackPort(IGameplaySfxPlaybackPort playbackPort, bool useDefaultPlaybackPort = true)
        {
            _playbackPort = playbackPort;
            if (playbackPort == null && useDefaultPlaybackPort)
            {
                _playbackPort = _playbackPortAdapter;
            }

            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public void ConfigureTiming(GameplayTimingProfile timingProfile)
        {
            _timingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            if (!_usesDefaultPipelineFactory)
            {
                return;
            }

            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public void PrepareCurrentRoute(
            TickResult result,
            bool isTopologyTransitionActive,
            IReadOnlyCollection<int> playableEnemyDeathCueEntityIds)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            SetTopologyTransitionActive(isTopologyTransitionActive);
            CopyPlayableEnemyDeathCueEntityIds(playableEnemyDeathCueEntityIds);
            _playbackPortAdapter.ConfigureEnemyDeathCueSuppression(_playableEnemyDeathCueEntityIds);
        }

        public void PresentPrepared(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            _executionPipeline ??= CreateExecutionPipeline();
            _executionPipeline?.Present(result);
        }

        public void CompletePrepared()
        {
        }

        public void SetTopologyTransitionActive(bool isActive)
        {
            _previousPlaybackGateBlocked = _currentPlaybackGateBlocked;
            _currentPlaybackGateBlocked = isActive;
            var gateState = isActive
                ? GameplayAudioPlaybackGateState.TopologyLocked
                : GameplayAudioPlaybackGateState.Open;
            _playbackPortAdapter.SetPlaybackGateState(gateState);
        }

        public void Update(float deltaTime)
        {
            _playbackPortAdapter.Update(deltaTime);
            _executionPipeline?.Update(deltaTime);
        }

        public void AttachRuntime(
            IGameplayAudioPlaybackPort playbackPort,
            GameplayAudioMap gameplayAudioMap)
        {
            _playbackPortAdapter.AttachRuntime(playbackPort, gameplayAudioMap);
            if (_playbackPort == null)
            {
                _playbackPort = _playbackPortAdapter;
                _executionPipeline = CreateExecutionPipeline();
            }
        }

        public void DetachRuntime()
        {
            _playbackPortAdapter.DetachRuntime();
        }

        public void ResetSession()
        {
            _playbackPortAdapter.ResetSession();
            ResetExecutionSession();
            _executionPipeline?.ResetSession();
        }

        public void HardCleanup()
        {
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
                ResolvePlaybackPort(),
                _executionGuard);
        }

        private GameplayPresentationPipeline CreateDefaultExecutionPipeline(
            IGameplaySfxPlaybackPort playbackPort,
            CoreGameplaySfxExecutionGuard executionGuard)
        {
            return GameplayHostPresentationPipelineFactory.CreateCoreGameplaySfxExecutionPipeline(
                playbackPort,
                executionGuard,
                _timingProfile);
        }

        private IGameplaySfxPlaybackPort ResolvePlaybackPort()
        {
            return _playbackPort ?? _playbackPortAdapter;
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
                isProductionDefaultOwner: true,
                observedCueCount: 0,
                semanticUnsupportedCount: 0,
                mapMissingCount: 0,
                bindingMissingCount: 0,
                targetMissingCount: 0,
                ownerViewMissingCount: 0,
                portMissingCount: 0,
                duplicateSuppressedCount: ownershipDiagnostics.DuplicateAttemptCount,
                requestPlannedCount: 0,
                playbackRequestedCount: 0,
                playbackSucceededCount: 0,
                playbackNoOpSuppressedCount: 0,
                diagnosticCount: 0,
                attachedLikePlaybackCount: adapterDiagnostics.AttachedLikePlaybackCount,
                ownerMissingTwoDPlaybackCount: adapterDiagnostics.OwnerMissingTwoDPlaybackCount,
                deferredDuringTopologyLockCount: adapterDiagnostics.DeferredDuringTopologyLockCount,
                deferredDrainCount: adapterDiagnostics.DeferredDrainCount,
                enemyDeathGenericCoreSfxSuppressedCount: adapterDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount,
                lethalEnemyDamageSuppressedByDeathCount: adapterDiagnostics.LethalEnemyDamageSuppressedByDeathCount,
                lastTickIndex: adapterDiagnostics.LastTickIndex,
                lastSemanticKey: adapterDiagnostics.LastSemanticKey,
                lastDiagnosticReason: adapterDiagnostics.LastDiagnosticReason,
                semanticDiagnostics: Array.Empty<GameplaySfxSemanticDiagnostics>());
        }

        private static GameplaySfxExecutorDiagnostics MergeCoreGameplaySfxDiagnostics(
            GameplaySfxExecutorDiagnostics executorDiagnostics,
            CoreGameplaySfxOwnershipDiagnostics ownershipDiagnostics,
            GameplaySfxPlaybackAdapterDiagnostics adapterDiagnostics)
        {
            var adapterDiagnosticCount =
                adapterDiagnostics.OwnerMissingTwoDPlaybackCount +
                adapterDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount +
                adapterDiagnostics.LethalEnemyDamageSuppressedByDeathCount;
            var lastDiagnosticReason = adapterDiagnostics.LastDiagnosticReason != GameplaySfxDiagnosticReason.None
                ? adapterDiagnostics.LastDiagnosticReason
                : executorDiagnostics.LastDiagnosticReason;
            var lastSemanticKey = adapterDiagnostics.LastSemanticKey != PresentationSfxCueKey.None
                ? adapterDiagnostics.LastSemanticKey
                : executorDiagnostics.LastSemanticKey;
            var lastTickIndex = adapterDiagnostics.LastTickIndex > 0
                ? adapterDiagnostics.LastTickIndex
                : executorDiagnostics.LastTickIndex;

            return new GameplaySfxExecutorDiagnostics(
                isProductionDefaultOwner: true,
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
                executorDiagnostics.RequestPlannedCount,
                executorDiagnostics.PlaybackRequestedCount,
                executorDiagnostics.PlaybackSucceededCount,
                executorDiagnostics.PlaybackNoOpSuppressedCount,
                Math.Max(executorDiagnostics.DiagnosticCount, adapterDiagnosticCount),
                adapterDiagnostics.AttachedLikePlaybackCount,
                adapterDiagnostics.OwnerMissingTwoDPlaybackCount,
                adapterDiagnostics.DeferredDuringTopologyLockCount,
                adapterDiagnostics.DeferredDrainCount,
                adapterDiagnostics.EnemyDeathGenericCoreSfxSuppressedCount,
                adapterDiagnostics.LethalEnemyDamageSuppressedByDeathCount,
                lastTickIndex,
                lastSemanticKey,
                lastDiagnosticReason,
                executorDiagnostics.SemanticDiagnostics);
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

    }
}

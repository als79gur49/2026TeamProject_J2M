using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct EnemyOneShotAudioPlanSnapshot
    {
        private static readonly IReadOnlyCollection<int> EmptyPlayableDeathCueEntityIds = Array.Empty<int>();

        public EnemyOneShotAudioPlanSnapshot(
            IReadOnlyCollection<int> playableDeathCueEntityIds,
            int requestCount,
            int playbackKeyCount)
        {
            PlayableDeathCueEntityIds = playableDeathCueEntityIds ?? EmptyPlayableDeathCueEntityIds;
            RequestCount = Math.Max(0, requestCount);
            PlaybackKeyCount = Math.Max(0, playbackKeyCount);
        }

        public IReadOnlyCollection<int> PlayableDeathCueEntityIds { get; }

        public int RequestCount { get; }

        public int PlaybackKeyCount { get; }
    }

    internal readonly struct EnemyOneShotAudioLaneDiagnostics
    {
        public EnemyOneShotAudioLaneDiagnostics(
            EnemyAudioExecutionMode executionMode,
            EnemyAudioOwnershipDiagnostics ownership,
            GameplayEnemyAudioExecutorDiagnostics executor,
            PresentationBlockingSnapshot blockingSnapshot,
            EnemyAudioProductionTelemetrySnapshot productionTelemetry)
        {
            ExecutionMode = executionMode;
            Ownership = ownership;
            Executor = executor;
            BlockingSnapshot = blockingSnapshot;
            ProductionTelemetry = productionTelemetry;
        }

        public EnemyAudioExecutionMode ExecutionMode { get; }

        public EnemyAudioOwnershipDiagnostics Ownership { get; }

        public GameplayEnemyAudioExecutorDiagnostics Executor { get; }

        public PresentationBlockingSnapshot BlockingSnapshot { get; }

        public EnemyAudioProductionTelemetrySnapshot ProductionTelemetry { get; }
    }

    internal sealed class EnemyOneShotAudioLaneRuntime
    {
        private static readonly IReadOnlyCollection<int> EmptyPlayableDeathCueEntityIds = Array.Empty<int>();

        private readonly EnemyAudioRequestPlanner _requestPlanner;
        private readonly EnemyAudioPresentationController _legacyController;
        private readonly EnemyAudioExecutionGuard _executionGuard;
        private readonly EnemyAudioExecutionPipelineFactory _pipelineFactory;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayEnemyAudioPlaybackPortAdapter _playbackPortAdapter;

        private GameplayPresentationPipeline _executionPipeline;
        private IGameplayEnemyAudioPlaybackPort _playbackPort;
        private int _preparedTickIndex;
        private bool _previousPlaybackGateBlocked;
        private bool _currentPlaybackGateBlocked;

        public EnemyOneShotAudioLaneRuntime(
            GameplayPresentationStateStore stateStore,
            EnemyAudioExecutionPipelineFactory pipelineFactory = null,
            EnemyAudioRequestPlanner requestPlanner = null,
            EnemyAudioPresentationController legacyController = null,
            EnemyAudioExecutionGuard executionGuard = null)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _pipelineFactory = pipelineFactory ??
                               GameplayHostPresentationPipelineFactory.CreateEnemyAudioExecutionPipeline;
            _requestPlanner = requestPlanner ?? new EnemyAudioRequestPlanner();
            _legacyController = legacyController ?? new EnemyAudioPresentationController(_stateStore);
            _executionGuard = executionGuard ?? new EnemyAudioExecutionGuard();
            _playbackPortAdapter = new GameplayEnemyAudioPlaybackPortAdapter(_legacyController);
        }

        public EnemyAudioExecutionMode ExecutionMode => _executionGuard.Diagnostics.Mode;

        public EnemyAudioOwnershipDiagnostics OwnershipDiagnostics => _executionGuard.Diagnostics;

        public GameplayEnemyAudioExecutorDiagnostics ExecutorDiagnostics =>
            EnemyAudioProductionTelemetryBuilder.ResolveExecutorDiagnostics(_executionPipeline);

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public EnemyAudioProductionTelemetrySnapshot ProductionTelemetrySnapshot =>
            EnemyAudioProductionTelemetryBuilder.Build(
                ExecutionMode,
                _executionGuard.Diagnostics,
                _executionPipeline);

        public EnemyOneShotAudioLaneDiagnostics Diagnostics =>
            new(
                ExecutionMode,
                OwnershipDiagnostics,
                ExecutorDiagnostics,
                BlockingSnapshot,
                ProductionTelemetrySnapshot);

        public int DeferredRequestCount => _legacyController.DeferredRequestCount;

        public void ConfigureExecution(
            EnemyAudioExecutionMode mode,
            IGameplayEnemyAudioPlaybackPort playbackPort = null)
        {
            _playbackPort = playbackPort;
            _executionGuard.Configure(EnemyAudioExecutionPolicy.Normalize(mode));
            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public void ConfigureTiming(GameplayTimingProfile timingProfile)
        {
            _legacyController.ConfigureTiming(timingProfile);
        }

        public EnemyOneShotAudioPlanSnapshot RefreshPlan(
            TickResult result,
            GameplayTimingProfile timingProfile)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var enemyAudioRequests = _requestPlanner.BuildRequests(result, timingProfile);
            var playableDeathCueEntityIds = BuildPlayableEnemyDeathCueEntityIds(
                result.PresentationData,
                enemyAudioRequests);
            var enemyAudioKeys = BuildEnemyAudioPlaybackKeys(result);
            _preparedTickIndex = result.TickIndex;
            if (UseProductionExecutor())
            {
                for (var i = 0; i < enemyAudioKeys.Count; i++)
                {
                    _executionGuard.RecordSkippedByPolicy(
                        EnemyAudioExecutionOwner.LegacyEnemyAudioController);
                }

                _legacyController.ReplacePendingPlan(Array.Empty<EnemyAudioRequest>(), result.TickIndex);
            }
            else
            {
                _legacyController.ReplacePendingPlan(enemyAudioRequests, result.TickIndex);
                for (var i = 0; i < enemyAudioKeys.Count; i++)
                {
                    _executionGuard.TryBeginExecution(
                        EnemyAudioExecutionOwner.LegacyEnemyAudioController,
                        enemyAudioKeys[i]);
                }
            }

            return new EnemyOneShotAudioPlanSnapshot(
                playableDeathCueEntityIds,
                enemyAudioRequests.Count,
                enemyAudioKeys.Count);
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
            _legacyController.PlayPlannedAudio(_preparedTickIndex);
        }

        public void Update(
            int tickIndex,
            float deltaTime)
        {
            var gameplayAudioDeltaTime = _previousPlaybackGateBlocked || _currentPlaybackGateBlocked
                ? 0f
                : deltaTime;
            _legacyController.Update(tickIndex, gameplayAudioDeltaTime);
            _executionPipeline?.Update(deltaTime);
        }

        public void SetPlaybackGateState(GameplayAudioPlaybackGateState gateState)
        {
            _previousPlaybackGateBlocked = _currentPlaybackGateBlocked;
            _currentPlaybackGateBlocked = gateState.IsBlocked;
            _legacyController.SetPlaybackGateState(gateState);
        }

        public void AttachRuntime(IGameplayAudioPlaybackPort playbackPort)
        {
            _legacyController.AttachRuntime(playbackPort);
        }

        public void DetachRuntime()
        {
            _legacyController.DetachRuntime();
            _playbackPort?.HardCleanup();
        }

        public void ResetSession()
        {
            _legacyController.ResetSession();
            ResetExecutionSession();
            _executionPipeline?.ResetSession();
        }

        public void HardCleanup()
        {
            _legacyController.ResetSession();
            _executionPipeline?.HardCleanup();
            ResetExecutionSession();
        }

        private void ResetExecutionSession()
        {
            _executionGuard.ResetSession();
        }

        private GameplayPresentationPipeline CreateExecutionPipeline()
        {
            return _pipelineFactory(
                ExecutionMode,
                ResolvePlaybackPort(),
                _executionGuard);
        }

        private IGameplayEnemyAudioPlaybackPort ResolvePlaybackPort()
        {
            return _playbackPort ?? _playbackPortAdapter;
        }

        private bool UseProductionExecutor()
        {
            return ExecutionMode == EnemyAudioExecutionPolicy.ProductionDefault;
        }

        private static IReadOnlyList<EnemyAudioPlaybackKey> BuildEnemyAudioPlaybackKeys(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new EnemyAudioCuePlanner(),
            }).Plan(factFrame);
            if (cueFrame.Cues.Count == 0)
            {
                return Array.Empty<EnemyAudioPlaybackKey>();
            }

            var keys = new List<EnemyAudioPlaybackKey>(cueFrame.Cues.Count);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.EnemyAudio ||
                    !cue.Key.TryGetEnemyAudioCueKey(out var cueKey) ||
                    !cue.EnemyAudioPayload.IsValid)
                {
                    continue;
                }

                var payload = cue.EnemyAudioPayload;
                keys.Add(new EnemyAudioPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    payload.OwnerEntityId,
                    cueKey,
                    payload.OriginKind,
                    payload.Phase,
                    payload.SourceSequenceId,
                    payload.TargetEntityId,
                    payload.ImpactTick,
                    payload.ImpactId,
                    payload.PresentationKey,
                    i));
            }

            return keys.Count == 0
                ? Array.Empty<EnemyAudioPlaybackKey>()
                : keys;
        }

        private IReadOnlyCollection<int> BuildPlayableEnemyDeathCueEntityIds(
            TickPresentationData presentationData,
            IReadOnlyList<EnemyAudioRequest> enemyAudioRequests)
        {
            var deathExitEntityIds = BuildEnemyDeathExitEntityIds(presentationData);
            if (deathExitEntityIds.Count == 0)
            {
                return EmptyPlayableDeathCueEntityIds;
            }

            var playableDeathCueEntityIds = new List<int>();
            for (var i = 0; i < enemyAudioRequests.Count; i++)
            {
                var request = enemyAudioRequests[i];
                if (request.Cue != EnemyAudioCue.Death ||
                    !deathExitEntityIds.Contains(request.OwnerEntityId) ||
                    !HasPlayableEnemyDeathCue(request.OwnerEntityId))
                {
                    continue;
                }

                playableDeathCueEntityIds.Add(request.OwnerEntityId);
            }

            return playableDeathCueEntityIds.Count == 0
                ? EmptyPlayableDeathCueEntityIds
                : new ReadOnlyCollection<int>(playableDeathCueEntityIds);
        }

        private static HashSet<int> BuildEnemyDeathExitEntityIds(TickPresentationData presentationData)
        {
            var entityIds = new HashSet<int>();
            var exitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < exitSignals.Count; i++)
            {
                var signal = exitSignals[i];
                if (signal.ExitCause != TickEntityExitCause.EnemyDeath &&
                    signal.ExitCause != TickEntityExitCause.Killed)
                {
                    continue;
                }

                entityIds.Add(signal.ExitedEntityId);
            }

            return entityIds;
        }

        private bool HasPlayableEnemyDeathCue(int ownerEntityId)
        {
            if (!TryResolveActiveOwner(ownerEntityId, out var owner))
            {
                return false;
            }

            var authoring = EnemyAudioAuthoring.GetOptionalValidatedAuthoring(owner);
            return authoring != null &&
                   authoring.Profile.HasCue(EnemyAudioCue.Death);
        }

        private bool TryResolveActiveOwner(int ownerEntityId, out GameplayEntityView owner)
        {
            owner = null;
            if (!_stateStore.ViewsByEntityId.TryGetValue(ownerEntityId, out owner) ||
                owner == null ||
                !owner.gameObject.activeInHierarchy)
            {
                owner = null;
                return false;
            }

            return true;
        }
    }
}

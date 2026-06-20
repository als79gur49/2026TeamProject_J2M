using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct GameplayActionAudioLaneDiagnostics
    {
        public GameplayActionAudioLaneDiagnostics(
            ActionAudioExecutionMode executionMode,
            ActionAudioOwnershipDiagnostics ownership,
            GameplayActionAudioExecutorDiagnostics executor,
            PresentationBlockingSnapshot blockingSnapshot,
            ActionAudioProductionTelemetrySnapshot productionTelemetry)
        {
            ExecutionMode = executionMode;
            Ownership = ownership;
            Executor = executor;
            BlockingSnapshot = blockingSnapshot;
            ProductionTelemetry = productionTelemetry;
        }

        public ActionAudioExecutionMode ExecutionMode { get; }

        public ActionAudioOwnershipDiagnostics Ownership { get; }

        public GameplayActionAudioExecutorDiagnostics Executor { get; }

        public PresentationBlockingSnapshot BlockingSnapshot { get; }

        public ActionAudioProductionTelemetrySnapshot ProductionTelemetry { get; }
    }

    internal sealed class GameplayActionAudioLaneRuntime
    {
        private readonly GameplayActionAudioRequestPlanner _requestPlanner;
        private readonly GameplayActionAudioPresentationController _legacyController;
        private readonly ActionAudioExecutionGuard _executionGuard;
        private readonly ActionAudioExecutionPipelineFactory _pipelineFactory;
        private readonly GameplayPresentationStateStore _stateStore;
        private readonly GameplayActionAudioPlaybackPortAdapter _playbackPortAdapter;

        private GameplayPresentationPipeline _executionPipeline;
        private IGameplayActionAudioPlaybackPort _playbackPort;

        public GameplayActionAudioLaneRuntime(
            GameplayPresentationStateStore stateStore,
            ActionAudioExecutionPipelineFactory pipelineFactory = null,
            GameplayActionAudioRequestPlanner requestPlanner = null,
            GameplayActionAudioPresentationController legacyController = null,
            ActionAudioExecutionGuard executionGuard = null)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _pipelineFactory = pipelineFactory ??
                               GameplayHostPresentationPipelineFactory.CreateActionAudioExecutionPipeline;
            _requestPlanner = requestPlanner ?? new GameplayActionAudioRequestPlanner();
            _legacyController = legacyController ?? new GameplayActionAudioPresentationController(_stateStore);
            _executionGuard = executionGuard ?? new ActionAudioExecutionGuard();
            _playbackPortAdapter = new GameplayActionAudioPlaybackPortAdapter(_legacyController);
        }

        public ActionAudioExecutionMode ExecutionMode => _executionGuard.Diagnostics.Mode;

        public ActionAudioOwnershipDiagnostics OwnershipDiagnostics => _executionGuard.Diagnostics;

        public GameplayActionAudioExecutorDiagnostics ExecutorDiagnostics =>
            ActionAudioProductionTelemetryBuilder.ResolveExecutorDiagnostics(_executionPipeline);

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public ActionAudioProductionTelemetrySnapshot ProductionTelemetrySnapshot =>
            ActionAudioProductionTelemetryBuilder.Build(
                ExecutionMode,
                _executionGuard.Diagnostics,
                _executionPipeline);

        public GameplayActionAudioLaneDiagnostics Diagnostics =>
            new(
                ExecutionMode,
                OwnershipDiagnostics,
                ExecutorDiagnostics,
                BlockingSnapshot,
                ProductionTelemetrySnapshot);

        public int DeferredRequestCount => _legacyController.DeferredRequestCount;

        public void ConfigureExecution(
            ActionAudioExecutionMode mode,
            IGameplayActionAudioPlaybackPort playbackPort = null)
        {
            _playbackPort = playbackPort;
            _executionGuard.Configure(mode);
            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public void RefreshPlan(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var actionAudioRequests = _requestPlanner.BuildRequests(result);
            var actionAudioKeys = BuildActionAudioPlaybackKeys(result);
            if (UseProductionExecutor())
            {
                for (var i = 0; i < actionAudioKeys.Count; i++)
                {
                    _executionGuard.RecordSkippedByPolicy(
                        ActionAudioExecutionOwner.LegacyActionAudioController);
                }

                _legacyController.ReplacePendingPlan(
                    Array.Empty<GameplayActionAudioRequest>(),
                    result.TickIndex);
            }
            else
            {
                _legacyController.ReplacePendingPlan(actionAudioRequests, result.TickIndex);
                for (var i = 0; i < actionAudioKeys.Count; i++)
                {
                    _executionGuard.TryBeginExecution(
                        ActionAudioExecutionOwner.LegacyActionAudioController,
                        actionAudioKeys[i]);
                }
            }
        }

        public void PresentProduction(TickResult result)
        {
            if (result == null ||
                !UseProductionExecutor())
            {
                return;
            }

            _executionPipeline ??= CreateExecutionPipeline();
            _executionPipeline?.Present(result);
        }

        public void PlayLegacyPending(int tickIndex)
        {
            _legacyController.PlayPlannedAudio(tickIndex);
        }

        public void Update(
            int tickIndex,
            float gameplayAudioDeltaTime,
            float pipelineDeltaTime)
        {
            _ = tickIndex;
            _ = gameplayAudioDeltaTime;

            _legacyController.Update();
            _executionPipeline?.Update(pipelineDeltaTime);
        }

        public void SetPlaybackGateState(GameplayAudioPlaybackGateState gateState)
        {
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

        public void ObserveTopologyActiveState(bool isTopologyActive, int tickIndex)
        {
            _executionPipeline?.ObserveTopologyActiveState(isTopologyActive, tickIndex);
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

        private IGameplayActionAudioPlaybackPort ResolvePlaybackPort()
        {
            return _playbackPort ?? _playbackPortAdapter;
        }

        private bool UseProductionExecutor()
        {
            return ExecutionMode == ActionAudioExecutionDefaults.ProductionDefault;
        }

        private static IReadOnlyList<ActionAudioPlaybackKey> BuildActionAudioPlaybackKeys(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new ActionAudioCuePlanner(),
            }).Plan(factFrame);
            if (cueFrame.Cues.Count == 0)
            {
                return Array.Empty<ActionAudioPlaybackKey>();
            }

            var keys = new List<ActionAudioPlaybackKey>(cueFrame.Cues.Count);
            for (var i = 0; i < cueFrame.Cues.Count; i++)
            {
                var cue = cueFrame.Cues[i];
                if (cue.Domain != PresentationDomain.ActionAudio ||
                    !cue.Key.TryGetActionAudioCueKey(out _) ||
                    !TryMapActionAudioPayload(cue.ActionAudioPayload, out var action, out var moment))
                {
                    continue;
                }

                var payload = cue.ActionAudioPayload;
                keys.Add(new ActionAudioPlaybackKey(
                    cue.Source.TickIndex,
                    cue.Source.SemanticSource,
                    payload.OwnerEntityId,
                    action,
                    moment,
                    payload.SourceSequenceId,
                    payload.SourceActionPlanId,
                    payload.TargetEntityId,
                    i));
            }

            return keys.Count == 0
                ? Array.Empty<ActionAudioPlaybackKey>()
                : keys;
        }

        private static bool TryMapActionAudioPayload(
            PresentationActionAudioPayload payload,
            out GameplayActionKind action,
            out GameplayActionAudioMoment moment)
        {
            action = default;
            moment = default;
            if (!payload.IsValid ||
                !Enum.IsDefined(typeof(GameplayActionKind), payload.ActionKind))
            {
                return false;
            }

            action = (GameplayActionKind)payload.ActionKind;
            switch ((GameplayActionAudioMoment)payload.Moment)
            {
                case GameplayActionAudioMoment.Windup:
                case GameplayActionAudioMoment.AssistOutOfRange:
                case GameplayActionAudioMoment.NoTarget:
                case GameplayActionAudioMoment.Invalid:
                    moment = (GameplayActionAudioMoment)payload.Moment;
                    return true;
                default:
                    return false;
            }
        }
    }
}

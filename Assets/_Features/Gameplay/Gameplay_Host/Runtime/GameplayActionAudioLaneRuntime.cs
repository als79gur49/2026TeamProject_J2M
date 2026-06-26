using System;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct GameplayActionAudioLaneDiagnostics
    {
        public GameplayActionAudioLaneDiagnostics(
            GameplayActionAudioExecutorDiagnostics executor,
            PresentationBlockingSnapshot blockingSnapshot,
            ActionAudioProductionTelemetrySnapshot productionTelemetry)
        {
            Executor = executor;
            BlockingSnapshot = blockingSnapshot;
            ProductionTelemetry = productionTelemetry;
        }

        public GameplayActionAudioExecutorDiagnostics Executor { get; }

        public PresentationBlockingSnapshot BlockingSnapshot { get; }

        public ActionAudioProductionTelemetrySnapshot ProductionTelemetry { get; }
    }

    internal sealed class GameplayActionAudioLaneRuntime
    {
        private readonly ActionAudioExecutionPipelineFactory _pipelineFactory;
        private readonly GameplayActionAudioPresentationController _playbackController;
        private readonly GameplayActionAudioPlaybackPortAdapter _playbackPortAdapter;

        private GameplayPresentationPipeline _executionPipeline;

        public GameplayActionAudioLaneRuntime(
            GameplayPresentationStateStore stateStore,
            ActionAudioExecutionPipelineFactory pipelineFactory = null,
            GameplayActionAudioPresentationController playbackController = null)
        {
            if (stateStore == null)
            {
                throw new ArgumentNullException(nameof(stateStore));
            }

            _pipelineFactory = pipelineFactory ??
                               GameplayHostPresentationPipelineFactory.CreateActionAudioExecutionPipeline;
            _playbackController = playbackController ?? new GameplayActionAudioPresentationController(stateStore);
            _playbackPortAdapter = new GameplayActionAudioPlaybackPortAdapter(_playbackController);
            _executionPipeline = CreateExecutionPipeline();
        }

        public GameplayActionAudioExecutorDiagnostics ExecutorDiagnostics =>
            ActionAudioProductionTelemetryBuilder.ResolveExecutorDiagnostics(_executionPipeline);

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public ActionAudioProductionTelemetrySnapshot ProductionTelemetrySnapshot =>
            ActionAudioProductionTelemetryBuilder.Build(_executionPipeline);

        public GameplayActionAudioLaneDiagnostics Diagnostics =>
            new(
                ExecutorDiagnostics,
                BlockingSnapshot,
                ProductionTelemetrySnapshot);

        public void RefreshPlan(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
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

        public void Update(float deltaTime)
        {
            _executionPipeline?.Update(deltaTime);
        }

        public void AttachRuntime(IGameplayAudioPlaybackPort playbackPort)
        {
            _playbackController.AttachRuntime(playbackPort);
        }

        public void DetachRuntime()
        {
            _playbackController.DetachRuntime();
        }

        public void ResetSession()
        {
            _playbackController.ResetSession();
            _executionPipeline?.ResetSession();
        }

        public void HardCleanup()
        {
            _playbackController.ResetSession();
            _executionPipeline?.HardCleanup();
        }

        public void ObserveTopologyActiveState(bool isTopologyActive, int tickIndex)
        {
            _executionPipeline?.ObserveTopologyActiveState(isTopologyActive, tickIndex);
        }

        private GameplayPresentationPipeline CreateExecutionPipeline()
        {
            return _pipelineFactory(ResolvePlaybackPort());
        }

        private IGameplayActionAudioPlaybackPort ResolvePlaybackPort()
        {
            return _playbackPortAdapter;
        }
    }
}

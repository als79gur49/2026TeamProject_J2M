using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct TopologyPresentationLaneDiagnostics
    {
        public TopologyPresentationLaneDiagnostics(
            TopologyPresentationOwnershipDiagnostics ownership,
            TopologyExecutorDiagnostics executor,
            PresentationBlockingSnapshot blockingSnapshot)
        {
            Ownership = ownership;
            Executor = executor;
            BlockingSnapshot = blockingSnapshot;
        }

        public TopologyPresentationOwnershipDiagnostics Ownership { get; }

        public TopologyExecutorDiagnostics Executor { get; }

        public PresentationBlockingSnapshot BlockingSnapshot { get; }
    }

    internal sealed class TopologyPresentationLaneRuntime
    {
        private readonly TopologyPresentationExecutionGuard _executionGuard;
        private readonly TopologyExecutionPipelineFactory _pipelineFactory;
        private readonly ITopologyTransitionPlaybackPort _productionPlaybackPort;
        private readonly ITopologyTransitionCleanupPort _cleanupPort;

        private GameplayPresentationPipeline _executionPipeline;

        public TopologyPresentationLaneRuntime(
            TopologyExecutionPipelineFactory pipelineFactory,
            ITopologyTransitionPlaybackPort productionPlaybackPort,
            ITopologyTransitionCleanupPort cleanupPort,
            TopologyPresentationExecutionGuard executionGuard = null)
        {
            _pipelineFactory = pipelineFactory ??
                               GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline;
            _productionPlaybackPort = productionPlaybackPort;
            _cleanupPort = cleanupPort ?? throw new ArgumentNullException(nameof(cleanupPort));
            _executionGuard = executionGuard ?? new TopologyPresentationExecutionGuard();
            _executionPipeline = CreateExecutionPipeline();
        }

        public TopologyPresentationOwnershipDiagnostics OwnershipDiagnostics => _executionGuard.Diagnostics;

        public TopologyExecutorDiagnostics ExecutorDiagnostics =>
            TopologyProductionTelemetryBuilder.ResolveExecutorDiagnostics(_executionPipeline);

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public TopologyPresentationLaneDiagnostics Diagnostics =>
            new(
                OwnershipDiagnostics,
                ExecutorDiagnostics,
                BlockingSnapshot);

        public void Present(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _executionPipeline ??= CreateExecutionPipeline();
            _executionPipeline?.Present(result);
        }

        public void ObserveControllerActivity(bool isActive, int tickIndex)
        {
            _executionPipeline?.ObserveTopologyActiveState(isActive, tickIndex);
        }

        public void ResetSession()
        {
            _executionPipeline?.ResetSession();
            ResetExecutionSession();
            _cleanupPort.ResetTransition();
        }

        public void HardCleanup()
        {
            _executionPipeline?.HardCleanup();
            ResetExecutionSession();
            _cleanupPort.ResetTransition();
        }

        public TopologyProductionTelemetrySnapshot BuildProductionTelemetrySnapshot(
            bool hasBlockingPresentation,
            bool isTopologyTransitionActive,
            PresentationBlockingSnapshot presentationBlockingSnapshot)
        {
            return TopologyProductionTelemetryBuilder.Build(
                OwnershipDiagnostics,
                _executionPipeline,
                hasBlockingPresentation,
                isTopologyTransitionActive,
                presentationBlockingSnapshot,
                BlockingSnapshot);
        }

        private GameplayPresentationPipeline CreateExecutionPipeline()
        {
            return _pipelineFactory(
                _productionPlaybackPort,
                _executionGuard);
        }

        private void ResetExecutionSession()
        {
            _executionGuard.ResetSession();
        }
    }
}

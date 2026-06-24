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
            TopologyPresentationExecutionMode executionMode,
            TopologyPresentationOwnershipDiagnostics ownership,
            TopologyExecutorDiagnostics executor,
            PresentationBlockingSnapshot blockingSnapshot)
        {
            ExecutionMode = executionMode;
            Ownership = ownership;
            Executor = executor;
            BlockingSnapshot = blockingSnapshot;
        }

        public TopologyPresentationExecutionMode ExecutionMode { get; }

        public TopologyPresentationOwnershipDiagnostics Ownership { get; }

        public TopologyExecutorDiagnostics Executor { get; }

        public PresentationBlockingSnapshot BlockingSnapshot { get; }
    }

    internal sealed class TopologyPresentationLaneRuntime
    {
        private readonly TopologyPresentationExecutionGuard _executionGuard;
        private readonly TopologyExecutionPipelineFactory _pipelineFactory;
        private readonly ITopologyTransitionPlaybackPort _productionPlaybackPort;
        private readonly ITopologyLegacyTransitionPort _legacyTransitionPort;
        private readonly ITopologyTransitionCleanupPort _cleanupPort;

        private GameplayPresentationPipeline _executionPipeline;

        public TopologyPresentationLaneRuntime(
            TopologyExecutionPipelineFactory pipelineFactory,
            ITopologyTransitionPlaybackPort productionPlaybackPort,
            ITopologyLegacyTransitionPort legacyTransitionPort,
            ITopologyTransitionCleanupPort cleanupPort,
            TopologyPresentationExecutionGuard executionGuard = null)
        {
            _pipelineFactory = pipelineFactory ??
                               GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline;
            _productionPlaybackPort = productionPlaybackPort;
            _legacyTransitionPort = legacyTransitionPort ?? throw new ArgumentNullException(nameof(legacyTransitionPort));
            _cleanupPort = cleanupPort ?? throw new ArgumentNullException(nameof(cleanupPort));
            _executionGuard = executionGuard ?? new TopologyPresentationExecutionGuard();
        }

        public TopologyPresentationExecutionMode ExecutionMode => _executionGuard.Diagnostics.Mode;

        public TopologyPresentationOwnershipDiagnostics OwnershipDiagnostics => _executionGuard.Diagnostics;

        public TopologyExecutorDiagnostics ExecutorDiagnostics =>
            TopologyProductionTelemetryBuilder.ResolveExecutorDiagnostics(_executionPipeline);

        public PresentationBlockingSnapshot BlockingSnapshot =>
            _executionPipeline?.BlockingSnapshot ?? PresentationBlockingSnapshot.Empty;

        public TopologyPresentationLaneDiagnostics Diagnostics =>
            new(
                ExecutionMode,
                OwnershipDiagnostics,
                ExecutorDiagnostics,
                BlockingSnapshot);

        public void ConfigureExecution(TopologyPresentationExecutionMode mode)
        {
            _executionGuard.Configure(TopologyPresentationExecutionPolicy.Normalize(mode));
            ResetExecutionSession();
            _executionPipeline = CreateExecutionPipeline();
            _executionPipeline?.ResetSession();
        }

        public void Present(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (UseProductionExecutor())
            {
                if (IsTopologyTransitionPresentation(result.PresentationData.TopologyMotion))
                {
                    _executionGuard.RecordSkippedByPolicy(
                        TopologyPresentationExecutionOwner.LegacyCoordinator);
                }

                _executionPipeline ??= CreateExecutionPipeline();
                _executionPipeline?.Present(result);
                return;
            }

            PresentLegacy(result);
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
                ExecutionMode,
                OwnershipDiagnostics,
                _executionPipeline,
                hasBlockingPresentation,
                isTopologyTransitionActive,
                presentationBlockingSnapshot,
                BlockingSnapshot);
        }

        private void PresentLegacy(TickResult result)
        {
            if (IsTopologyTransitionPresentation(result.PresentationData.TopologyMotion) &&
                !_executionGuard.TryBeginExecution(
                    TopologyPresentationExecutionOwner.LegacyCoordinator,
                    result.TickIndex,
                    hasSourceMetadata: false,
                    sourceMetadataKey: 0))
            {
                return;
            }

            _legacyTransitionPort.PresentLegacyTopology(result.PresentationData, result.FinalTopology);
        }

        private GameplayPresentationPipeline CreateExecutionPipeline()
        {
            return _pipelineFactory(
                ExecutionMode,
                _productionPlaybackPort,
                _executionGuard);
        }

        private void ResetExecutionSession()
        {
            _executionGuard.ResetSession();
        }

        private bool UseProductionExecutor()
        {
            return ExecutionMode == TopologyPresentationExecutionDefaults.ProductionDefault;
        }

        private static bool IsTopologyTransitionPresentation(TickTopologyMotion? topologyMotion)
        {
            return topologyMotion.HasValue &&
                   topologyMotion.Value.RotationKind != CubeRotationKind.None;
        }
    }
}

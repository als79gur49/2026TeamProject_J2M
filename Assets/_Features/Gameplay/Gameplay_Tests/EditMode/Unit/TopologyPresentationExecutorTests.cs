using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TopologyPresentationExecutorTests
    {
        [Test]
        [Category("Core")]
        public void TopologyPresentationExecutor_CurrentRoute_MapsOneTopologyTrackToOnePortCall()
        {
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);
            var port = new RecordingTopologyTransitionPlaybackPort();
            var guard = new TopologyPresentationExecutionGuard();
            var executor = new TopologyPresentationExecutor(
                port,
                guard);
            var plan = CreateTopologyPlaybackPlan(
                sourceTopology,
                destinationTopology,
                CubeRotationKind.Forward,
                tickIndex: 42);

            executor.Play(plan);

            Assert.That(port.BeginOrRefreshCallCount, Is.EqualTo(1));
            Assert.That(port.LastRequest.TickIndex, Is.EqualTo(42));
            Assert.That(port.LastRequest.SourceTopology, Is.EqualTo(sourceTopology));
            Assert.That(port.LastRequest.DestinationTopology, Is.EqualTo(destinationTopology));
            Assert.That(port.LastRequest.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(port.LastRequest.SourceTickIndex, Is.EqualTo(42));
            Assert.That(executor.Diagnostics.ObservedTrackCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.RouteCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.IgnoredCount, Is.Zero);
            Assert.That(executor.Diagnostics.InvalidTrackCount, Is.Zero);
            Assert.That(executor.Diagnostics.MissingPortCount, Is.Zero);
            Assert.That(guard.Diagnostics.ExecutorAttemptCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.Zero);
            Assert.That(guard.Diagnostics.LastExecutionTickIndex, Is.EqualTo(42));
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentationExecutor_MissingPort_NoOpsWithoutFallback()
        {
            var guard = new TopologyPresentationExecutionGuard();
            var executor = new TopologyPresentationExecutor(
                playbackPort: null,
                guard);
            var plan = CreateTopologyPlaybackPlan(
                new CubeTopologyState(FaceId.Floor),
                new CubeTopologyState(FaceId.Front),
                CubeRotationKind.Forward,
                tickIndex: 42);

            executor.Play(plan);

            Assert.That(executor.Diagnostics.ObservedTrackCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.RouteCount, Is.Zero);
            Assert.That(executor.Diagnostics.IgnoredCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.InvalidTrackCount, Is.Zero);
            Assert.That(executor.Diagnostics.MissingPortCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.ExecutorAttemptCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.Zero);
            Assert.That(guard.Diagnostics.LastExecutionTickIndex, Is.EqualTo(42));
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentationExecutor_CurrentRoute_PreservesSymbolicTopologyCueSemanticsInRequest()
        {
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);
            var port = new RecordingTopologyTransitionPlaybackPort();
            var guard = new TopologyPresentationExecutionGuard();
            var executor = new TopologyPresentationExecutor(
                port,
                guard);
            var plan = CreateTopologyPlaybackPlan(
                sourceTopology,
                destinationTopology,
                CubeRotationKind.Forward,
                tickIndex: 42,
                hasSourceMetadata: true,
                sourceMetadataKey: 314);

            executor.Play(plan);

            var cue = plan.Tracks[0].Cue;
            Assert.That(cue.Source.SemanticSource, Is.EqualTo(PresentationSemanticSource.TopologyMotion));
            Assert.That(cue.Target.Kind, Is.EqualTo(PresentationTargetKind.Topology));
            Assert.That(cue.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.TopologyOrbit));
            Assert.That(plan.Barriers[0].Target.Kind, Is.EqualTo(PresentationTargetKind.Topology));
            Assert.That(plan.Barriers[0].OwnerDomain, Is.EqualTo(PresentationDomain.Topology));
            Assert.That(plan.Barriers[0].Blocking, Is.True);
            Assert.That(port.LastRequest.SourceTopology, Is.EqualTo(cue.TopologyPayload.SourceTopology));
            Assert.That(port.LastRequest.DestinationTopology, Is.EqualTo(cue.TopologyPayload.DestinationTopology));
            Assert.That(port.LastRequest.RotationKind, Is.EqualTo(cue.TopologyPayload.RotationKind));
            Assert.That(port.LastRequest.SourceTickIndex, Is.EqualTo(cue.TopologyPayload.SourceTickIndex));
            Assert.That(port.LastRequest.HasSourceMetadata, Is.EqualTo(cue.TopologyPayload.HasSourceMetadata));
            Assert.That(port.LastRequest.SourceMetadataKey, Is.EqualTo(cue.TopologyPayload.SourceMetadataKey));
            Assert.That(guard.Diagnostics.LastExecutionHasSourceMetadata, Is.True);
            Assert.That(guard.Diagnostics.LastExecutionSourceMetadataKey, Is.EqualTo(314));
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentationExecutionGuard_CurrentRoute_AllowsFirstAttempt()
        {
            var guard = new TopologyPresentationExecutionGuard();

            var executorAllowed = guard.TryBeginExecution(
                tickIndex: 9,
                hasSourceMetadata: false,
                sourceMetadataKey: 0);

            Assert.That(executorAllowed, Is.True);
            Assert.That(guard.Diagnostics.ExecutorAttemptCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentationExecutionGuard_DuplicateCurrentAttempt_IsRecordedAndBlocked()
        {
            var guard = new TopologyPresentationExecutionGuard();
            var firstAllowed = guard.TryBeginExecution(
                tickIndex: 12,
                hasSourceMetadata: false,
                sourceMetadataKey: 0);

            var executorAllowed = guard.TryBeginExecution(
                tickIndex: 12,
                hasSourceMetadata: false,
                sourceMetadataKey: 0);

            Assert.That(firstAllowed, Is.True);
            Assert.That(executorAllowed, Is.False);
            Assert.That(guard.Diagnostics.ExecutorAttemptCount, Is.EqualTo(2));
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentationExecutionGuard_DuplicateCurrentAttempt_CanFailFastForTests()
        {
            var guard = new TopologyPresentationExecutionGuard(throwOnDuplicate: true);

            guard.TryBeginExecution(
                tickIndex: 12,
                hasSourceMetadata: false,
                sourceMetadataKey: 0);

            Assert.Throws<InvalidOperationException>(() => guard.TryBeginExecution(
                tickIndex: 12,
                hasSourceMetadata: false,
                sourceMetadataKey: 0));
            Assert.That(guard.Diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
        }

        private static PresentationPlaybackPlan CreateTopologyPlaybackPlan(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind,
            int tickIndex = 7,
            bool hasSourceMetadata = false,
            int sourceMetadataKey = 0)
        {
            var cue = new PresentationCue(
                PresentationDomain.Topology,
                new PresentationCueKey(PresentationDomain.Topology, (int)PresentationTopologyCueKey.Transition),
                new PresentationSource(tickIndex, PresentationSemanticSource.TopologyMotion),
                PresentationTarget.Topology(),
                PresentationAnchor.ForTopologyOrbit(),
                PresentationPlaybackPolicyHint.Track(blocking: true),
                new PresentationTopologyTransitionPayload(
                    sourceTopology,
                    destinationTopology,
                    rotationKind,
                    tickIndex,
                    hasSourceMetadata,
                    sourceMetadataKey));
            var policy = PresentationPlaybackPolicy.FromHint(cue.PolicyHint);

            return new PresentationPlaybackPlan(
                tickIndex,
                Array.Empty<PresentationPlaybackCue>(),
                new[] { new PresentationPlaybackTrack(cue, policy) },
                new[]
                {
                    new PresentationPlaybackBarrier(
                        PresentationDomain.Topology,
                        cue.Source,
                        cue.Target,
                        (int)PresentationTopologyCueKey.Transition,
                        blocking: true),
                },
                new PresentationPlaybackDiagnostics(
                    extractedFactCount: 1,
                    plannedCueCount: 0,
                    plannedTrackCount: 1,
                    plannedBarrierCount: 1,
                    topologyCueCount: 1,
                    topologyTrackCount: 1,
                    topologyBarrierCount: 1,
                    blockingBarrierCount: 1,
                    routeCount: 0,
                    suppressedCount: 0,
                    deferredCount: 0,
                    canceledCount: 0,
                    missingBindingCount: 0,
                    noOpSchedulerAcceptCount: 0));
        }

        private sealed class RecordingTopologyTransitionPlaybackPort : ITopologyTransitionPlaybackPort
        {
            public int BeginOrRefreshCallCount { get; private set; }

            public int UpdatePresentationCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public bool IsTransitionActive { get; private set; }

            public TopologyTransitionPlaybackRequest LastRequest { get; private set; }

            public void BeginOrRefreshTopologyTransition(TopologyTransitionPlaybackRequest request)
            {
                BeginOrRefreshCallCount++;
                LastRequest = request;
            }

            public void UpdatePresentation(float deltaTime)
            {
                UpdatePresentationCallCount++;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                IsTransitionActive = false;
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                IsTransitionActive = false;
            }
        }
    }
}

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
        public void TopologyPresentationExecutor_DisabledMode_IgnoresTopologyTrackWithoutCallingPort()
        {
            var port = new RecordingTopologyTransitionPlaybackPort();
            var executor = new TopologyPresentationExecutor(
                port,
                TopologyPresentationExecutorMode.Disabled);
            var plan = CreateTopologyPlaybackPlan(
                new CubeTopologyState(FaceId.Floor),
                new CubeTopologyState(FaceId.Front),
                CubeRotationKind.Forward);

            executor.Play(plan);

            Assert.That(port.BeginOrRefreshCallCount, Is.Zero);
            Assert.That(port.IsTransitionActive, Is.False);
            Assert.That(executor.Diagnostics.ObservedTrackCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.RouteCount, Is.Zero);
            Assert.That(executor.Diagnostics.IgnoredCount, Is.EqualTo(1));
            Assert.That(executor.Diagnostics.InvalidTrackCount, Is.Zero);
            Assert.That(executor.Diagnostics.MissingPortCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentationExecutor_EnabledIsolatedMode_MapsOneTopologyTrackToOnePortCall()
        {
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);
            var port = new RecordingTopologyTransitionPlaybackPort();
            var executor = new TopologyPresentationExecutor(
                port,
                TopologyPresentationExecutorMode.EnabledForTests);
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
        }

        private static PresentationPlaybackPlan CreateTopologyPlaybackPlan(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind,
            int tickIndex = 7)
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
                    tickIndex));
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

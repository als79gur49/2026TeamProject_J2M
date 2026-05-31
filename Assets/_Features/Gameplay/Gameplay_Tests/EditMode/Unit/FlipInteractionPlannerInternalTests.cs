using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class FlipInteractionPlannerInternalTests
    {
        [Test]
        [Category("Extended")]
        public void FlipInteractionTrack_PhasesTransitionFromWindupToRecoveryToComplete()
        {
            var track = new FlipInteractionTrack(
                playerEntityId: 10,
                boxEntityId: 20,
                actionSequence: 3,
                direction: Direction.Right,
                windupDurationSeconds: 0.2f,
                followDurationSeconds: 0.3f,
                recoveryDurationSeconds: 0.4f,
                phase: FlipInteractionPhase.Windup);

            Assert.That(track.Phase, Is.EqualTo(FlipInteractionPhase.Windup));

            track.SetPhase(FlipInteractionPhase.AirborneFollow);
            Assert.That(track.Phase, Is.EqualTo(FlipInteractionPhase.AirborneFollow));

            track.SetPhase(FlipInteractionPhase.Recovery);
            Assert.That(track.Phase, Is.EqualTo(FlipInteractionPhase.Recovery));

            track.Advance(0.39f);
            Assert.That(track.Phase, Is.EqualTo(FlipInteractionPhase.Recovery));

            track.Advance(0.02f);
            Assert.That(track.Phase, Is.EqualTo(FlipInteractionPhase.Complete));
            Assert.That(track.IsComplete, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void FlipInteractionTrack_DestroySelfReleaseUsesBreakOnsetThresholdWhileBoxFlightMayContinue()
        {
            var settings = new FlipImpactTimingSettings(0.70f, 0.35f, 0.30f, 0.10f);
            var track = new FlipInteractionTrack(
                playerEntityId: 10,
                boxEntityId: 20,
                actionSequence: 3,
                direction: Direction.Right,
                windupDurationSeconds: 0.2f,
                followDurationSeconds: 1f,
                recoveryDurationSeconds: 0.4f,
                flipImpactTimingSettings: settings,
                flipOutcome: TickPlayerFlipOutcomeKind.DestroySelf,
                phase: FlipInteractionPhase.AirborneFollow);
            var pose = new Pose(Vector3.zero, Quaternion.identity);

            track.Advance(0.69f);
            var beforeContact = track.Sample(pose, pose);

            track.Advance(0.11f);
            var afterContact = track.Sample(pose, pose);

            Assert.That(track.FlipImpactTimingSettings.ContactNormalizedTime, Is.EqualTo(settings.ContactNormalizedTime));
            Assert.That(beforeContact.HandWeight, Is.EqualTo(1f));
            Assert.That(afterContact.HandWeight, Is.LessThan(1f));
            Assert.That(afterContact.BoxWeight, Is.LessThan(1f));
        }

        [Test]
        [Category("Extended")]
        public void FlipInteractionTrack_FollowThroughBuildsUpBeforeSettlingBackToFollowPose()
        {
            var settings = new FlipImpactTimingSettings(0.62f, 0.55f, 0.22f, 0.18f);
            var track = new FlipInteractionTrack(
                playerEntityId: 10,
                boxEntityId: 20,
                actionSequence: 3,
                direction: Direction.Right,
                windupDurationSeconds: 0.2f,
                followDurationSeconds: 1f,
                recoveryDurationSeconds: 0.4f,
                flipImpactTimingSettings: settings,
                flipOutcome: TickPlayerFlipOutcomeKind.FollowThrough,
                phase: FlipInteractionPhase.AirborneFollow);
            var pose = new Pose(Vector3.zero, Quaternion.identity);

            track.Advance(settings.ContactNormalizedTime * 0.9f);
            var nearContact = track.Sample(pose, pose);

            track.Advance(1f);
            var endOfFollow = track.Sample(pose, pose);

            Assert.That(
                nearContact.BoxLocalPositionOffset.magnitude,
                Is.GreaterThan(endOfFollow.BoxLocalPositionOffset.magnitude));
            Assert.That(
                Quaternion.Angle(nearContact.BoxLocalRotationOffset, Quaternion.identity),
                Is.GreaterThan(Quaternion.Angle(endOfFollow.BoxLocalRotationOffset, Quaternion.identity)));
            Assert.That(endOfFollow.HandWeight, Is.EqualTo(1f));
            Assert.That(endOfFollow.BoxWeight, Is.EqualTo(1f));
        }

        [Test]
        [Category("Extended")]
        public void FlipInteractionTrack_BlockedAirborneFollowQuicklyDropsTowardRecoveryPose()
        {
            var settings = new FlipImpactTimingSettings(0.62f, 0.55f, 0.22f, 0.18f);
            var track = new FlipInteractionTrack(
                playerEntityId: 10,
                boxEntityId: 20,
                actionSequence: 3,
                direction: Direction.Right,
                windupDurationSeconds: 0.2f,
                followDurationSeconds: 1f,
                recoveryDurationSeconds: 0.4f,
                flipImpactTimingSettings: settings,
                flipOutcome: TickPlayerFlipOutcomeKind.Blocked,
                phase: FlipInteractionPhase.AirborneFollow);
            var pose = new Pose(Vector3.zero, Quaternion.identity);

            var start = track.Sample(pose, pose);
            track.Advance(1f);
            var end = track.Sample(pose, pose);

            Assert.That(start.HandWeight, Is.EqualTo(1f));
            Assert.That(end.HandWeight, Is.LessThan(0.3f));
            Assert.That(end.BoxWeight, Is.LessThan(0.3f));
            Assert.That(end.BoxLocalPositionOffset.magnitude, Is.LessThan(start.BoxLocalPositionOffset.magnitude));
            Assert.That(
                Quaternion.Angle(end.BoxLocalRotationOffset, Quaternion.identity),
                Is.LessThan(Quaternion.Angle(start.BoxLocalRotationOffset, Quaternion.identity)));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTrackPlanner_FlipInteractionTrack_CancelAndTargetLossRemoveTrackSafely()
        {
            var rootObject = new GameObject("GameplayTrackPlanner_FlipInteractionTrack_CancelAndTargetLossRemoveTrackSafely");

            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlannerHarness(rootObject);
                stateStore.ViewsByEntityId[10] = CreateEntityView(rootObject.transform, 10);
                stateStore.ViewsByEntityId[20] = CreateEntityView(rootObject.transform, 20);

                planner.RefreshTracks(
                    CreateTickResult(
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            new[]
                            {
                                new TickPlayerActionPresentationSignal(
                                    10,
                                    PlayerActionKind.Flip,
                                    1,
                                    startedThisTick: true,
                                    completedThisTick: false,
                                    canceledThisTick: false,
                                    targetEntityId: 20,
                                    direction: Direction.Right),
                            })),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.FlipInteractionTracks.ContainsKey(10), Is.True);

                planner.RefreshTracks(
                    CreateTickResult(
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            new[]
                            {
                                new TickPlayerActionPresentationSignal(
                                    10,
                                    PlayerActionKind.None,
                                    0,
                                    startedThisTick: false,
                                    completedThisTick: false,
                                    canceledThisTick: true,
                                    direction: Direction.Right),
                            })),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.FlipInteractionTracks.ContainsKey(10), Is.False);
                Assert.That(trackState.FlipInteractionResetRequests.Count, Is.EqualTo(1));

                trackState.FlipInteractionResetRequests.Clear();

                planner.RefreshTracks(
                    CreateTickResult(
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            new[]
                            {
                                new TickPlayerActionPresentationSignal(
                                    10,
                                    PlayerActionKind.Flip,
                                    2,
                                    startedThisTick: true,
                                    completedThisTick: false,
                                    canceledThisTick: false,
                                    targetEntityId: 20,
                                    direction: Direction.Right),
                            })),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                stateStore.ViewsByEntityId.Remove(20);

                planner.RefreshTracks(
                    CreateTickResult(TickPresentationData.Empty),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.FlipInteractionTracks.ContainsKey(10), Is.False);
                Assert.That(trackState.FlipInteractionResetRequests.Count, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTrackPlanner_JumpWindupSignal_CreatesRotationTrackBetweenCurrentAndTargetFacing()
        {
            var rootObject = new GameObject("GameplayTrackPlanner_JumpWindupSignal_CreatesRotationTrack");
            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlannerHarness(rootObject);
                var sourceCell = new SurfaceCell(FaceId.Floor, 1, 1);
                var topology = new CubeTopologyState(FaceId.Floor);
                Assert.That(
                    projector.TryResolveEntityRotation(sourceCell, topology, Direction.Up, out var startRotation),
                    Is.True);
                Assert.That(
                    projector.TryResolveEntityRotation(sourceCell, topology, Direction.Right, out var endRotation),
                    Is.True);
                stateStore.CommittedLocalTargetPoses[40] = new GameplayEntityPose(Vector3.zero, startRotation);

                var presentationData = new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    new[]
                    {
                        new TickEnemyJumpPresentationSignal(
                            40,
                            sequence: 1,
                            phase: EnemyJumpPhase.Windup,
                            startedWindupThisTick: true,
                            startedAirborneThisTick: false,
                            landedThisTick: false,
                            retryThisTick: false,
                            sourceCell: sourceCell,
                            lockedTargetCell: new SurfaceCell(FaceId.Floor, 2, 1),
                            presentationTargetCell: new SurfaceCell(FaceId.Floor, 2, 1),
                            facing: Direction.Right,
                            windupTicks: 2),
                    },
                    Array.Empty<TickEntityExitPresentationSignal>());

                planner.RefreshTracks(
                    CreateTickResult(presentationData),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.JumpWindupRotationTracks.TryGetValue(40, out var track), Is.True);
                Assert.That(track.HasClips, Is.True);

                var totalAngle = Quaternion.Angle(startRotation, endRotation);
                var sampledRotation = track.SampleAndAdvance(
                    1f / timingProfile.SimulationTicksPerSecond,
                    startRotation);

                Assert.That(Quaternion.Angle(startRotation, sampledRotation), Is.GreaterThan(0.01f));
                Assert.That(Quaternion.Angle(startRotation, sampledRotation), Is.LessThan(totalAngle));
                Assert.That(Quaternion.Angle(sampledRotation, endRotation), Is.LessThan(totalAngle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTrackPlanner_EntityMotion_CreatesMotionTrackWithOperationId()
        {
            var rootObject = new GameObject("GameplayTrackPlanner_EntityMotion_CreatesMotionTrackWithOperationId");

            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlannerHarness(rootObject);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var motion = new TickEntityMotion(
                    entityId: 10,
                    motionKind: TickEntityMotionKind.Move,
                    sourceCell: sourceCell,
                    destinationCell: targetCell,
                    operationId: 137,
                    direction: Direction.Right);

                planner.RefreshTracks(
                    CreateTickResult(new TickPresentationData(new[] { motion })),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.LocalMotionTracks.TryGetValue(10, out var track), Is.True);
                Assert.That(track.HasClips, Is.True);
                Assert.That(track.LastOperationId, Is.EqualTo(137));
                Assert.That(stateStore.LastMotionTrackBuildDiagnostics.Count, Is.EqualTo(1));
                var diagnostic = stateStore.LastMotionTrackBuildDiagnostics[0];
                Assert.That(diagnostic.TrackCreated, Is.True);
                Assert.That(diagnostic.OperationId, Is.EqualTo(137));
                Assert.That(diagnostic.SourceCell, Is.EqualTo(sourceCell));
                Assert.That(diagnostic.TargetCell, Is.EqualTo(targetCell));
                Assert.That(diagnostic.Direction, Is.EqualTo(Direction.Right));
                Assert.That(diagnostic.DurationSeconds, Is.EqualTo(timingProfile.MoveMotionDurationSeconds));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static (GameplayTrackPlanner Planner, GameplayPresentationStateStore StateStore, GameplayPresentationTrackState TrackState, GameplayCubeProjector Projector, GameplayTimingProfile TimingProfile)
            CreatePlannerHarness(GameObject rootObject)
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            var poseResolver = new GameplayPoseResolver(stateStore, trackState);
            var animationSync = new GameplayAnimationSyncCoordinator();
            var motionTimingResolver = new GameplayMotionTimingResolver(stateStore, trackState);
            var committedFrameBuilder = new GameplayCommittedFrameBuilder(stateStore, poseResolver, animationSync);
            var entityApplier = new GameplayEntityPresentationApplier(
                stateStore,
                trackState,
                poseResolver,
                animationSync,
                motionTimingResolver,
                new DefaultEnemyVisualSemanticResolver(),
                committedFrameBuilder);
            var planner = new GameplayTrackPlanner(
                stateStore,
                trackState,
                motionTimingResolver,
                poseResolver,
                new GameplayExitPresentationController(
                    animationSync,
                    stateStore,
                    trackState),
                entityApplier);
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var projector = new GameplayCubeProjector(new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)), 1f);

            stateStore.ResetSession(new CubeTopologyState(FaceId.Floor));
            return (planner, stateStore, trackState, projector, timingProfile);
        }

        private static GameplayEntityView CreateEntityView(Transform parent, int entityId)
        {
            var viewObject = new GameObject($"EntityView_{entityId}");
            viewObject.transform.SetParent(parent, worldPositionStays: false);
            var view = viewObject.AddComponent<GameplayEntityView>();
            view.Initialize(entityId);
            return view;
        }

        private static TickResult CreateTickResult(TickPresentationData presentationData)
        {
            return new TickResult(
                tickIndex: 1,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
                TickTrace.Empty);
        }
    }
}

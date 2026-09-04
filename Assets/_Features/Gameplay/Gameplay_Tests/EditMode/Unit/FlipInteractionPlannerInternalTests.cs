using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
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
        public void FlipInteractionTrack_ProgressPreservesSourceTickAndActionPlanIdentity()
        {
            var track = new FlipInteractionTrack(
                playerEntityId: 10,
                boxEntityId: 20,
                actionSequence: 3,
                direction: Direction.Right,
                windupDurationSeconds: 0.2f,
                followDurationSeconds: 1f,
                recoveryDurationSeconds: 0.4f,
                phase: FlipInteractionPhase.AirborneFollow);

            track.CorrelateSourceActionPlan(sourceTickIndex: 41, sourceActionPlanId: 131);
            track.CorrelateSourceActionPlan(sourceTickIndex: 42, sourceActionPlanId: 131);
            track.Advance(0.5f);

            Assert.That(track.TryCaptureProgress(out var progress), Is.True);
            Assert.That(progress.TickIndex, Is.EqualTo(41));
            Assert.That(progress.EntityId, Is.EqualTo(20));
            Assert.That(progress.MotionKind, Is.EqualTo(TickEntityMotionKind.Flip));
            Assert.That(progress.SequenceOrActionPlanId, Is.EqualTo(131));
            Assert.That(progress.SourceKind, Is.EqualTo(MotionTrackProgressSourceKind.FlipInteraction));
        }

        [Test]
        [Category("Core")]
        public void GameplayTrackPlanner_HostileStayCompletedOnPriorTick_ReusedActionPlanStartsNewTrack()
        {
            var rootObject = new GameObject(nameof(GameplayTrackPlanner_HostileStayCompletedOnPriorTick_ReusedActionPlanStartsNewTrack));

            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlannerHarness(rootObject);
                const int boxEntityId = 20;
                const int actionPlanId = 131;
                const int priorTickIndex = 41;
                const int currentTickIndex = 42;
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 1, 1);
                var impactCell = new SurfaceCell(FaceId.Floor, 2, 1);
                var signal = new FlipImpactPresentationSignal(
                    actionPlanId,
                    boxEntityId,
                    impactTargetEntityId: 30,
                    actorEntityId: 10,
                    sourceCell,
                    impactCell,
                    topology,
                    Direction.Right,
                    Direction.Right,
                    FlipImpactPresentationDisposition.Stay,
                    hasLandingCell: true,
                    landingCell: sourceCell);
                trackState.CompletedPresentationMotions.RecordCompleted(
                    PresentationMotionInstanceKey.CreateFlipImpactStay(signal, priorTickIndex));
                var payload = new PresentationMotionPayload(
                    PresentationMotionFactKind.BoxFlipImpact,
                    boxEntityId,
                    sourceCell,
                    impactCell,
                    actorEntityId: 10,
                    PresentationMotionActionKind.Flip,
                    Direction.Right,
                    Direction.Right,
                    Direction.Right,
                    sourceSequenceId: actionPlanId,
                    sourceActionPlanId: actionPlanId,
                    impactTargetEntityId: 30,
                    flipDisposition: (int)FlipImpactPresentationDisposition.Stay,
                    hasLandingCell: true,
                    landingCell: sourceCell,
                    topology: topology,
                    hasTopology: true);
                var request = new GameplayMotionPlaybackRequest(
                    new BoxMotionPlaybackKey(
                        currentTickIndex,
                        PresentationSemanticSource.BoxFlipImpactMotion,
                        boxEntityId,
                        PresentationMotionCueKey.BoxFlipImpact,
                        sourceCell,
                        impactCell,
                        actionPlanId,
                        sourceSequenceId: actionPlanId),
                    PresentationMotionCueKey.BoxFlipImpact,
                    payload,
                    PresentationTarget.Entity(boxEntityId),
                    PresentationAnchor.ForEntityVisualRoot(boxEntityId));
                var result = new TickResult(
                    currentTickIndex,
                    Array.Empty<TickPhase>(),
                    Array.Empty<string>(),
                    MovementPhaseResult.Empty,
                    AttackPhaseResult.Empty,
                    new[] { CreateBoxEntity(boxEntityId, sourceCell) },
                    Array.Empty<string>(),
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                        Array.Empty<TickPlayerDamagePresentationSignal>(),
                        Array.Empty<TickPlayerDeathPresentationSignal>(),
                        Array.Empty<TickEnemyDamagePresentationSignal>(),
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        Array.Empty<TickEnemyJumpPresentationSignal>(),
                        Array.Empty<TickEntityExitPresentationSignal>(),
                        new[] { signal }),
                    string.Empty,
                    TickTrace.Empty);

                var started = planner.TryRequestBoxMotionPlayback(
                    request,
                    result,
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile,
                    out var resultKind);

                Assert.That(started, Is.True);
                Assert.That(resultKind, Is.EqualTo(GameplayMotionPlaybackResultKind.Started));
                Assert.That(trackState.OriginalViewMotionTracks.TryGetValue(boxEntityId, out var track), Is.True);
                Assert.That(track.InstanceKey.TickIndex, Is.EqualTo(currentTickIndex));
                Assert.That(track.InstanceKey.CorrelationId, Is.EqualTo(actionPlanId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
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
        [Category("Extended")]
        public void GameplayTrackPlanner_PlayerFlipResultTurnSignal_CreatesOwnedEntry()
        {
            var rootObject = new GameObject("GameplayTrackPlanner_PlayerFlipResultTurnSignal_CreatesOwnedEntry");
            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlannerHarness(rootObject);
                var entityId = 10;
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                stateStore.ViewsByEntityId[entityId] = CreateEntityView(rootObject.transform, entityId);

                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(
                            resultTurnSignals: new[]
                            {
                                CreateResultTurnSignal(entityId, actionSequence: 7, contactFacing: Direction.Left, resultFacing: Direction.Right),
                            }),
                        new[] { CreatePlayerEntity(entityId, cell, Direction.Left) }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.PlayerFlipResultTurnTracks.TryGetValue(entityId, out var entry), Is.True);
                Assert.That(entry.ActionSequence, Is.EqualTo(7));
                Assert.That(entry.StartTick, Is.EqualTo(11));
                Assert.That(entry.ContactFacing, Is.EqualTo(Direction.Left));
                Assert.That(entry.ResultFacing, Is.EqualTo(Direction.Right));
                Assert.That(entry.Track.HasClips, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTrackPlanner_PlayerFlipResultTurnTrack_SignalAbsentNextRefreshKeepsActiveTrack()
        {
            var rootObject = new GameObject("GameplayTrackPlanner_PlayerFlipResultTurnTrack_SignalAbsentNextRefreshKeepsActiveTrack");
            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlannerHarness(rootObject);
                var entityId = 10;
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                stateStore.ViewsByEntityId[entityId] = CreateEntityView(rootObject.transform, entityId);
                stateStore.CommittedLocalTargetPoses[entityId] = new GameplayEntityPose(
                    Vector3.zero,
                    ResolveRotation(projector, cell, Direction.Left));

                var signal = CreateResultTurnSignal(entityId, actionSequence: 7, contactFacing: Direction.Left, resultFacing: Direction.Right);
                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(resultTurnSignals: new[] { signal }),
                        new[] { CreatePlayerEntity(entityId, cell, Direction.Left) }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);
                Assert.That(trackState.PlayerFlipResultTurnTracks.TryGetValue(entityId, out var originalEntry), Is.True);

                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(),
                        new[] { CreatePlayerEntity(entityId, cell, Direction.Left) }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.PlayerFlipResultTurnTracks.TryGetValue(entityId, out var retainedEntry), Is.True);
                Assert.That(retainedEntry, Is.SameAs(originalEntry));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTrackPlanner_PlayerFlipResultTurnTrack_DuplicateSignalDoesNotRestartTrack()
        {
            var rootObject = new GameObject("GameplayTrackPlanner_PlayerFlipResultTurnTrack_DuplicateSignalDoesNotRestartTrack");
            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlannerHarness(rootObject);
                var entityId = 10;
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                stateStore.ViewsByEntityId[entityId] = CreateEntityView(rootObject.transform, entityId);
                var signal = CreateResultTurnSignal(entityId, actionSequence: 7, contactFacing: Direction.Left, resultFacing: Direction.Right);

                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(resultTurnSignals: new[] { signal }),
                        new[] { CreatePlayerEntity(entityId, cell, Direction.Left) }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);
                Assert.That(trackState.PlayerFlipResultTurnTracks.TryGetValue(entityId, out var originalEntry), Is.True);

                originalEntry.Track.SampleAndAdvance(
                    1f / timingProfile.SimulationTicksPerSecond,
                    ResolveRotation(projector, cell, Direction.Left));

                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(resultTurnSignals: new[] { signal }),
                        new[] { CreatePlayerEntity(entityId, cell, Direction.Left) }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.PlayerFlipResultTurnTracks.TryGetValue(entityId, out var duplicateEntry), Is.True);
                Assert.That(duplicateEntry, Is.SameAs(originalEntry));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTrackPlanner_PlayerFlipResultTurnTrack_OlderSignalDoesNotReplaceNewerTrack()
        {
            var rootObject = new GameObject("GameplayTrackPlanner_PlayerFlipResultTurnTrack_OlderSignalDoesNotReplaceNewerTrack");
            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlannerHarness(rootObject);
                var entityId = 10;
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                stateStore.ViewsByEntityId[entityId] = CreateEntityView(rootObject.transform, entityId);

                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(
                            resultTurnSignals: new[]
                            {
                                CreateResultTurnSignal(entityId, actionSequence: 7, contactFacing: Direction.Left, resultFacing: Direction.Right),
                            }),
                        new[] { CreatePlayerEntity(entityId, cell, Direction.Left) }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);
                Assert.That(trackState.PlayerFlipResultTurnTracks.TryGetValue(entityId, out var originalEntry), Is.True);

                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(
                            resultTurnSignals: new[]
                            {
                                CreateResultTurnSignal(entityId, actionSequence: 6, contactFacing: Direction.Up, resultFacing: Direction.Down),
                            }),
                        new[] { CreatePlayerEntity(entityId, cell, Direction.Left) }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.PlayerFlipResultTurnTracks.TryGetValue(entityId, out var retainedEntry), Is.True);
                Assert.That(retainedEntry, Is.SameAs(originalEntry));
                Assert.That(retainedEntry.ActionSequence, Is.EqualTo(7));
                Assert.That(retainedEntry.ContactFacing, Is.EqualTo(Direction.Left));
                Assert.That(retainedEntry.ResultFacing, Is.EqualTo(Direction.Right));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTrackPlanner_PlayerFlipResultTurnTrack_NewerActionCancelsOlderTrack()
        {
            var rootObject = new GameObject("GameplayTrackPlanner_PlayerFlipResultTurnTrack_NewerActionCancelsOlderTrack");
            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlannerHarness(rootObject);
                var entityId = 10;
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                stateStore.ViewsByEntityId[entityId] = CreateEntityView(rootObject.transform, entityId);
                stateStore.CommittedLocalTargetPoses[entityId] = new GameplayEntityPose(
                    Vector3.zero,
                    ResolveRotation(projector, cell, Direction.Right));

                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(
                            resultTurnSignals: new[]
                            {
                                CreateResultTurnSignal(entityId, actionSequence: 7, contactFacing: Direction.Left, resultFacing: Direction.Right),
                            }),
                        new[] { CreatePlayerEntity(entityId, cell, Direction.Right) }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);
                Assert.That(trackState.PlayerFlipResultTurnTracks.ContainsKey(entityId), Is.True);

                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(
                            resultTurnSignals: new[]
                            {
                                CreateResultTurnSignal(entityId, actionSequence: 6, contactFacing: Direction.Left, resultFacing: Direction.Right),
                            },
                            playerActionSignals: new[]
                            {
                                new TickPlayerActionPresentationSignal(
                                    entityId,
                                    PlayerActionKind.Flip,
                                    activeActionSequence: 8,
                                    startedThisTick: true,
                                    completedThisTick: false,
                                    canceledThisTick: false,
                                    direction: Direction.Up),
                            }),
                        new[] { CreatePlayerEntity(entityId, cell, Direction.Right) }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.PlayerFlipResultTurnTracks.ContainsKey(entityId), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTrackPlanner_PlayerFlipResultTurnTrack_EntityExitRemovesOnlyTargetTrack()
        {
            var rootObject = new GameObject("GameplayTrackPlanner_PlayerFlipResultTurnTrack_EntityExitRemovesOnlyTargetTrack");
            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlannerHarness(rootObject);
                var firstEntityId = 10;
                var secondEntityId = 11;
                var firstCell = new SurfaceCell(FaceId.Floor, 1, 1);
                var secondCell = new SurfaceCell(FaceId.Floor, 2, 1);
                stateStore.ViewsByEntityId[firstEntityId] = CreateEntityView(rootObject.transform, firstEntityId);
                stateStore.ViewsByEntityId[secondEntityId] = CreateEntityView(rootObject.transform, secondEntityId);

                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(
                            resultTurnSignals: new[]
                            {
                                CreateResultTurnSignal(firstEntityId, actionSequence: 7, contactFacing: Direction.Left, resultFacing: Direction.Right),
                                CreateResultTurnSignal(secondEntityId, actionSequence: 3, contactFacing: Direction.Up, resultFacing: Direction.Down),
                            }),
                        new[]
                        {
                            CreatePlayerEntity(firstEntityId, firstCell, Direction.Left),
                            CreatePlayerEntity(secondEntityId, secondCell, Direction.Up),
                        }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                planner.RefreshTracks(
                    CreateTickResult(
                        CreatePresentationData(
                            resultTurnSignals: new[]
                            {
                                CreateResultTurnSignal(firstEntityId, actionSequence: 8, contactFacing: Direction.Left, resultFacing: Direction.Right),
                            },
                            exitSignals: new[]
                            {
                                CreateExitSignal(firstEntityId, firstCell, Direction.Left),
                            }),
                        new[] { CreatePlayerEntity(secondEntityId, secondCell, Direction.Up) }),
                    stateStore.CommittedLocalTargetPoses,
                    stateStore.CommittedTopology,
                    projector,
                    timingProfile);

                Assert.That(trackState.PlayerFlipResultTurnTracks.ContainsKey(firstEntityId), Is.False);
                Assert.That(trackState.PlayerFlipResultTurnTracks.ContainsKey(secondEntityId), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayPresentationTrackState_PlayerFlipResultTurnTracks_UsesOwnedEntry()
        {
            var property = typeof(GameplayPresentationTrackState).GetProperty(nameof(GameplayPresentationTrackState.PlayerFlipResultTurnTracks));

            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(Dictionary<int, PlayerFlipResultTurnTrackEntry>)));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTrackPlanner_PlayerFlipResultTurnCancellation_DoesNotUseGeometryFreshness()
        {
            var source = File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTrackPlanner.cs");

            Assert.That(source, Does.Not.Contain("RemoveSupersededPlayerFlipResultTurnTracks"));
            Assert.That(source, Does.Not.Contain("TailEndValue"));
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
            return CreateTickResult(presentationData, Array.Empty<EntityState>());
        }

        private static TickResult CreateTickResult(
            TickPresentationData presentationData,
            IReadOnlyList<EntityState> finalEntities)
        {
            return new TickResult(
                tickIndex: 1,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
                TickTrace.Empty);
        }

        private static TickPresentationData CreatePresentationData(
            IEnumerable<TickPlayerFlipResultTurnSignal> resultTurnSignals = null,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals = null,
            IEnumerable<TickEntityExitPresentationSignal> exitSignals = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals ?? Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                exitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                playerFlipResultTurnSignals: resultTurnSignals ?? Array.Empty<TickPlayerFlipResultTurnSignal>());
        }

        private static TickPlayerFlipResultTurnSignal CreateResultTurnSignal(
            int entityId,
            int actionSequence,
            Direction contactFacing,
            Direction resultFacing)
        {
            return new TickPlayerFlipResultTurnSignal(
                entityId,
                actionSequence,
                Direction.Right,
                contactFacing,
                resultFacing,
                startTick: 11,
                PlayerFlipResultTurnStartReason.ImmediateFlip);
        }

        private static TickEntityExitPresentationSignal CreateExitSignal(
            int entityId,
            SurfaceCell sourceCell,
            Direction facing)
        {
            return new TickEntityExitPresentationSignal(
                entityId,
                TickEntityExitCause.OutOfBounds,
                sourceCell,
                new CubeTopologyState(FaceId.Floor),
                facing,
                EntityType.Unit);
        }

        private static EntityState CreatePlayerEntity(int entityId, SurfaceCell position, Direction facing)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateBoxEntity(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static Quaternion ResolveRotation(
            GameplayCubeProjector projector,
            SurfaceCell cell,
            Direction facing)
        {
            Assert.That(
                projector.TryResolveEntityRotation(cell, new CubeTopologyState(FaceId.Floor), facing, out var rotation),
                Is.True);
            return rotation;
        }
    }
}

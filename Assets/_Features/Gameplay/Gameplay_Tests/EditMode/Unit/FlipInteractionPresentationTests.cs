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
    public sealed class FlipInteractionPresentationTests
    {
        [Test]
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
        public void GameplayTrackPlanner_FlipInteractionTrack_CancelAndTargetLossRemoveTrackSafely()
        {
            var rootObject = new GameObject("GameplayTrackPlanner_FlipInteractionTrack_CancelAndTargetLossRemoveTrackSafely");

            try
            {
                var (planner, stateStore, trackState, projector, timingProfile) = CreatePlanner(rootObject);
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
        public void GameplayEntityPresentationApplier_FlipInteraction_DoesNotMoveGameplayEntityViewRoots()
        {
            var rootObject = new GameObject("GameplayEntityPresentationApplier_FlipInteraction_DoesNotMoveGameplayEntityViewRoots");

            try
            {
                var stateStore = new GameplayPresentationStateStore();
                var trackState = new GameplayPresentationTrackState();
                var poseResolver = new GameplayPoseResolver(stateStore, trackState);
                var animationSync = new GameplayAnimationSyncCoordinator();
                var motionTimingResolver = new GameplayMotionTimingResolver(stateStore, trackState);
                var committedFrameBuilder = new GameplayCommittedFrameBuilder(stateStore, poseResolver, animationSync);
                var applier = new GameplayEntityPresentationApplier(
                    stateStore,
                    trackState,
                    poseResolver,
                    animationSync,
                    motionTimingResolver,
                    new DefaultEnemyVisualSemanticResolver(),
                    committedFrameBuilder);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, viewFactory: null);
                var timingProfile = GameplayTimingProfile.CreateDefault();

                stateStore.ResetSession(new CubeTopologyState(FaceId.Floor));

                var playerView = CreateEntityView(rootObject.transform, 10);
                var boxView = CreateEntityView(rootObject.transform, 20);
                registry.Register(playerView);
                registry.Register(boxView);
                stateStore.ViewsByEntityId[10] = playerView;
                stateStore.ViewsByEntityId[20] = boxView;
                stateStore.CommittedLocalTargetPoses[10] = new GameplayEntityPose(new Vector3(1f, 2f, 3f), Quaternion.identity);
                stateStore.CommittedLocalTargetPoses[20] = new GameplayEntityPose(new Vector3(4f, 5f, 6f), Quaternion.identity);

                var handRestAnchor = new GameObject("HandRestAnchor").transform;
                handRestAnchor.SetParent(playerView.transform, worldPositionStays: false);
                handRestAnchor.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
                var handIkTarget = new GameObject("HandIkTarget").transform;
                handIkTarget.SetParent(playerView.transform, worldPositionStays: false);
                handIkTarget.localPosition = new Vector3(-0.1f, 0.2f, 0.3f);
                var playerDriver = playerView.gameObject.AddComponent<PlayerFlipInteractionDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(playerDriver, "handRestAnchor", handRestAnchor);
                PlayerViewPrefabTestUtility.SetSerializedField(playerDriver, "handIkTarget", handIkTarget);

                var boxVisualRoot = new GameObject("BoxVisualRoot").transform;
                boxVisualRoot.SetParent(boxView.ModelRoot, worldPositionStays: false);
                var gripPoint = new GameObject("GripPoint").transform;
                gripPoint.SetParent(boxVisualRoot, worldPositionStays: false);
                gripPoint.localPosition = new Vector3(0.25f, 0f, 0f);
                var boxDriver = boxView.gameObject.AddComponent<BoxFlipInteractionDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(boxDriver, "visualRoot", boxVisualRoot);
                PlayerViewPrefabTestUtility.SetSerializedField(boxDriver, "gripPoint", gripPoint);

                trackState.FlipInteractionTracks[10] = new FlipInteractionTrack(
                    playerEntityId: 10,
                    boxEntityId: 20,
                    actionSequence: 1,
                    direction: Direction.Right,
                    windupDurationSeconds: 0.2f,
                    followDurationSeconds: 0.2f,
                    recoveryDurationSeconds: 0.2f,
                    phase: FlipInteractionPhase.AirborneFollow);

                applier.Apply(
                    deltaTime: 0f,
                    hasActiveBoardRotationTween: false,
                    binder,
                    timingProfile);

                Assert.That(playerView.transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(boxView.transform.localPosition, Is.EqualTo(new Vector3(4f, 5f, 6f)));
                Assert.That(handIkTarget.position, Is.Not.EqualTo(handRestAnchor.position));
                Assert.That(boxVisualRoot.localPosition, Is.Not.EqualTo(Vector3.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static (GameplayTrackPlanner Planner, GameplayPresentationStateStore StateStore, GameplayPresentationTrackState TrackState, GameplayCubeProjector Projector, GameplayTimingProfile TimingProfile)
            CreatePlanner(GameObject rootObject)
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
                    stateStore,
                    trackState,
                    poseResolver,
                    new GameplayTransientEffectPresenter()),
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
                CleanupPhaseResult.Empty,
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
                TickTrace.Empty);
        }
    }
}

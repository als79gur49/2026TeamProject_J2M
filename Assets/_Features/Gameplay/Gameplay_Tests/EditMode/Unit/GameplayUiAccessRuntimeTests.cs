using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayUiAccessRuntimeTests
    {
        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_ExposesUiAccessQueriesWithCommittedHudState()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_ExposesUiAccessQueriesWithCommittedHudState");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));

                var session = host.UiAccess.QueryFacade.Session.Read();
                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var objectives = host.UiAccess.QueryFacade.Objectives.Read();

                Assert.That(session.NextTickIndex, Is.EqualTo(1));
                Assert.That(session.IsPaused, Is.False);
                Assert.That(session.CanAcceptGameplayCommands, Is.True);
                Assert.That(session.IsStageCleared, Is.False);

                Assert.That(playerHud.IsAvailable, Is.True);
                Assert.That(playerHud.PlayerEntityId, Is.EqualTo(10));
                Assert.That(playerHud.CurrentHp, Is.EqualTo(3));
                Assert.That(playerHud.Facing, Is.EqualTo(GameplayUiDirection.Right));
                Assert.That(playerHud.ActiveActionKind, Is.EqualTo(GameplayUiActionKind.None));
                Assert.That(playerHud.IsActionInRecoveryPhase, Is.False);
                Assert.That(playerHud.CanMoveThisTick, Is.True);
                Assert.That(playerHud.CanStartActionThisTick, Is.True);

                Assert.That(objectives.HasObjective, Is.False);
                Assert.That(objectives.IsCleared, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PauseRejectsActionableCommands_AllowsClear_AndClearsPendingUiInput()
        {
            var hostObject = new GameObject("GameplayUiAccess_PauseRejectsActionableCommands_AllowsClear_AndClearsPendingUiInput");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));

                var commandGateway = host.UiAccess.CommandGateway;
                var pauseService = host.UiAccess.PauseService;

                Assert.That(commandGateway.SetHeldMoveDirection(GameplayUiDirection.Right).Accepted, Is.True);

                pauseService.Pause();

                var rejectedMove = commandGateway.SetHeldMoveDirection(GameplayUiDirection.Up);
                var clearAcceptance = commandGateway.ClearHeldMoveDirection();
                var pausedSession = host.UiAccess.QueryFacade.Session.Read();

                Assert.That(rejectedMove.Accepted, Is.False);
                Assert.That(rejectedMove.RejectionReason, Is.EqualTo(GameplayCommandRejectionReason.Paused));
                Assert.That(clearAcceptance.Accepted, Is.True);
                Assert.That(pausedSession.IsPaused, Is.True);
                Assert.That(pausedSession.CanAcceptGameplayCommands, Is.False);
                Assert.That(host.InputHost.RunSingleTick(), Is.Null);

                pauseService.Resume();

                var resumedTick = host.InputHost.RunSingleTick();
                var snapshotAfter = GameplayCompositionRoot.CreateSnapshot(host.WorldState);

                Assert.That(resumedTick, Is.Not.Null);
                Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
                Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PresentationFeed_RotationTickPublishesTopologySliceAndStateChanges()
        {
            var hostObject = new GameObject("GameplayUiAccess_PresentationFeed_RotationTickPublishesTopologySliceAndStateChanges");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), facing: Direction.Up),
                }));

                var frames = new List<GameplayPresentationFrame>();
                var states = new List<GameplayPresentationState>();
                host.UiAccess.PresentationFeed.FramePublished += frames.Add;
                host.UiAccess.PresentationFeed.StateChanged += states.Add;

                var acceptance = host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Up);
                var tickResult = host.InputHost.RunSingleTick();

                Assert.That(acceptance.Accepted, Is.True);
                Assert.That(tickResult, Is.Not.Null);
                Assert.That(frames.Count, Is.EqualTo(1));
                Assert.That(frames[0].Topology.HasValue, Is.True);
                Assert.That(frames[0].Topology.Value.RotationKind, Is.EqualTo(GameplayUiRotationKind.Forward));
                Assert.That(frames[0].Topology.Value.DestinationTopology.BottomFace, Is.EqualTo(GameplayUiFace.Front));
                Assert.That(frames[0].FinalTopology.BottomFace, Is.EqualTo(GameplayUiFace.Front));
                Assert.That(host.UiAccess.PresentationFeed.CurrentState.IsTopologyTransitionActive, Is.True);
                Assert.That(states.Count, Is.GreaterThanOrEqualTo(1));

                host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);

                Assert.That(host.UiAccess.PresentationFeed.CurrentState.IsTopologyTransitionActive, Is.False);
                Assert.That(states.Count, Is.GreaterThanOrEqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PresentationFeed_MapsPlayerSlice_ForHeldMove()
        {
            var hostObject = new GameObject("GameplayUiAccess_PresentationFeed_MapsPlayerSlice_ForHeldMove");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Up),
                },
                boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0))));

                GameplayPresentationFrame capturedFrame = default;
                var frameCount = 0;
                host.UiAccess.PresentationFeed.FramePublished += frame =>
                {
                    capturedFrame = frame;
                    frameCount++;
                };

                var acceptance = host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Right);
                var tickResult = host.InputHost.RunSingleTick();

                Assert.That(acceptance.Accepted, Is.True);
                Assert.That(tickResult, Is.Not.Null);
                Assert.That(tickResult.PresentationData.PlayerLocomotionSignals.Count, Is.EqualTo(1));
                Assert.That(tickResult.PresentationData.PlayerLocomotionSignals[0].ShouldPlayWalkLoop, Is.True);
                Assert.That(tickResult.PresentationData.PlayerLocomotionSignals[0].MoveMotionGeneratedThisTick, Is.True);
                Assert.That(frameCount, Is.EqualTo(1));
                Assert.That(capturedFrame.Player.HasValue, Is.True);
                Assert.That(capturedFrame.Player.Value.PlayerEntityId, Is.EqualTo(10));
                Assert.That(capturedFrame.Player.Value.ActiveActionKind, Is.EqualTo(GameplayUiActionKind.None));
                Assert.That(capturedFrame.Player.Value.ActiveActionSequence, Is.EqualTo(0));
                Assert.That(capturedFrame.Player.Value.ShouldPlayWalkLoop, Is.True);
                Assert.That(capturedFrame.Player.Value.MoveMotionGeneratedThisTick, Is.True);
                Assert.That(capturedFrame.Player.Value.ActionDirection, Is.EqualTo(GameplayUiDirection.None));
                Assert.That(capturedFrame.Player.Value.TargetEntityId, Is.EqualTo(0));
                Assert.That(capturedFrame.Player.Value.StartedThisTick, Is.False);
                Assert.That(capturedFrame.Player.Value.ExecutedThisTick, Is.False);
                Assert.That(capturedFrame.Player.Value.IsRecoveryPhase, Is.False);
                Assert.That(capturedFrame.Player.Value.ResolutionKind, Is.EqualTo(GameplayUiActionResolutionKind.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        private static GameplaySceneHostConfiguration CreateConfiguration(
            EntityState[] initialEntities,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            BoardBounds? boardBounds = null)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = boardBounds ?? new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                InitialEntities = initialEntities,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                ObjectiveRuntimeDefinition = objectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled,
                PlayerEntityId = 10,
            };
        }

        private static EntityState CreatePlayerEntity(
            SurfaceCell position,
            Direction facing = Direction.Up)
        {
            return new EntityState
            {
                entityId = 10,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static EntityState CreateBoxEntity(
            SurfaceCell position,
            BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = 20,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Up,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = capabilities,
                aiMode = EnemyAiMode.None,
            };
        }
    }
}

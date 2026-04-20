using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class PlayerControlScenarioTests
    {
        [Test]
        [Category("Core")]
        public void PlayerControl_MoveCooldown_CannotBeBypassedByTapSpam()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(
                timingProfile,
                playerMoveCooldownTicks: 3);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                },
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                timingProfile,
                playerControlTiming);

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var secondTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var fourthTick = pipeline.RunTick(new TickInput(4, PlayerTickCommand.Move(Direction.Right)));
            var fifthTick = pipeline.RunTick(new TickInput(5, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(secondTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(thirdTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fourthTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    fifthTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.moveCooldownTicks, Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_MoveCooldown_OneTick_BlocksImmediateNextTick()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(
                timingProfile,
                playerMoveCooldownTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                },
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                timingProfile,
                playerControlTiming);

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var secondTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    firstTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(secondTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    thirdTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_LocomotionPresentationSignal_StaysTrueDuringCooldownGapAndDropsWhenBlocked()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(
                timingProfile,
                playerMoveCooldownTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateWall(entityId: 90, position: new Vector2Int(2, 0)),
                },
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                timingProfile,
                playerControlTiming);

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var secondTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));

            var firstSignal = firstTick.PresentationData.PlayerLocomotionSignals.Single();
            var secondSignal = secondTick.PresentationData.PlayerLocomotionSignals.Single();
            var thirdSignal = thirdTick.PresentationData.PlayerLocomotionSignals.Single();

            Assert.That(firstSignal.ShouldPlayWalkLoop, Is.True);
            Assert.That(firstSignal.MoveMotionGeneratedThisTick, Is.True);
            Assert.That(firstSignal.WaitingForNextMoveCadence, Is.False);

            Assert.That(secondSignal.ShouldPlayWalkLoop, Is.True);
            Assert.That(secondSignal.MoveMotionGeneratedThisTick, Is.False);
            Assert.That(secondSignal.WaitingForNextMoveCadence, Is.True);

            Assert.That(thirdSignal.ShouldPlayWalkLoop, Is.False);
            Assert.That(thirdSignal.MoveMotionGeneratedThisTick, Is.False);
            Assert.That(thirdSignal.WaitingForNextMoveCadence, Is.False);
            Assert.That(thirdTick.MovementPhaseResult.CommitEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_LocomotionPresentationSignal_InputReleaseDuringCooldown_DropsWalkLoop()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(
                timingProfile,
                playerMoveCooldownTicks: 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                },
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                timingProfile,
                playerControlTiming);

            var moveTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var releaseTick = pipeline.RunTick(new TickInput(2));

            Assert.That(moveTick.PresentationData.PlayerLocomotionSignals.Single().ShouldPlayWalkLoop, Is.True);
            Assert.That(releaseTick.PresentationData.PlayerLocomotionSignals.Single().ShouldPlayWalkLoop, Is.False);
            Assert.That(releaseTick.PresentationData.PlayerLocomotionSignals.Single().WaitingForNextMoveCadence, Is.False);
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_HoldAgainstSameBox_TriggersPushAtThreshold()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreatePushThresholdPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var secondTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(firstTick.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(secondTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(secondTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(secondTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(secondTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(thirdTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.False);
            Assert.That(thirdTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.True);
            Assert.That(thirdTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, Command: MovementCommandKind.Push),
                },
                thirdTick.MovementPhaseResult.SortedIntents.Select(intent => (intent.SourceId, intent.IntentId, intent.CommandKind)).ToArray());
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    thirdTick.MovementPhaseResult.CommitEvents,
                    "StateChanged",
                    "E=20",
                    "State=Sliding",
                    "Timer=12"),
                Is.True);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    thirdTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=20",
                    "To=(2,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.moveCooldownTicks, Is.Zero);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.executionAttempted, Is.True);
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_InputRelease_ResetsPushContact()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreatePushThresholdPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var releaseTick = pipeline.RunTick(new TickInput(2));
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var fourthTick = pipeline.RunTick(new TickInput(4, PlayerTickCommand.Move(Direction.Right)));
            var fifthTick = pipeline.RunTick(new TickInput(5, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(releaseTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(thirdTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fourthTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fifthTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_BufferedMove_DoesNotAccumulatePushContact()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreatePushThresholdPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var bufferedReleaseTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right, isMoveBuffered: true)));
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var fourthTick = pipeline.RunTick(new TickInput(4, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(bufferedReleaseTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(thirdTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fourthTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_DirectionChange_ResetsPushContact()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(-1, 0)),
                CreateWall(entityId: 91, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreatePushThresholdPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var directionChangeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Left)));
            var snapshotAfterDirectionChange = CreateSnapshot(worldState);
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var fourthTick = pipeline.RunTick(new TickInput(4, PlayerTickCommand.Move(Direction.Right)));
            var fifthTick = pipeline.RunTick(new TickInput(5, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    directionChangeTick.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=BlockedDestination",
                    "Cell=(-1,0)"),
                Is.True);
            Assert.That(snapshotAfterDirectionChange.TryGetEntity(10, out var playerAfterDirectionChange), Is.True);
            Assert.That(playerAfterDirectionChange.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(playerAfterDirectionChange.facing, Is.EqualTo(Direction.Left));
            Assert.That(thirdTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fourthTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fifthTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_BlockedMove_UpdatesFacingWithoutMoving()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateWall(entityId: 90, position: new Vector2Int(1, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=Expand",
                    "Source=10",
                    "Reason=BlockedDestination",
                    "Cell=(1,0)"),
                Is.True);
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(player.facing, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_FlipStartsActionAndRetainsPriorityOverPush()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var result = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Flip(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(result.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_PushAction_ExecutesAfterWindupInsteadOfThresholdTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10, pushContactThresholdTicks: 1),
                });

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var executeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(startTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().TargetEntityId, Is.EqualTo(20));
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().Direction, Is.EqualTo(Direction.Right));
            Assert.That(executeTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.False);
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.True);
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().TargetEntityId, Is.EqualTo(20));
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().Direction, Is.EqualTo(Direction.Right));
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(box.stateTimer, Is.EqualTo(11));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.executionAttempted, Is.True);
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_FlipAction_ExecutesAfterWindupInsteadOfSameTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var executeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(startTick.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(startTick.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().TargetEntityId, Is.EqualTo(20));
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().Direction, Is.EqualTo(Direction.Right));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 20, Kind: TickEntityMotionKind.Flip),
                },
                executeTick.PresentationData.EntityMotions.Select(motion => (motion.EntityId, motion.MotionKind)).ToArray());
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.False);
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.True);
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().TargetEntityId, Is.EqualTo(20));
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().Direction, Is.EqualTo(Direction.Right));
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(controlState.activeAction.executionAttempted, Is.True);
        }

        [Test]
        [Category("Core")]
        public void FlipImpactFailure_DestroySelfStillEntersRecoveryWithoutCommittedMoveTrack()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1, facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 30, position: new Vector2Int(-1, 0), hp: 3, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var executeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var snapshotAfter = CreateSnapshot(worldState);
            var signal = executeTick.PresentationData.PlayerActionSignals.Single();

            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, TargetId: 30, Position: new SurfaceCell(FaceId.Floor, -1, 0), Damage: 1),
                },
                executeTick.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.ImpactCell,
                        reservation.Damage))
                    .ToArray());
            Assert.That(signal.ExecutedThisTick, Is.True);
            Assert.That(signal.CanceledThisTick, Is.False);
            Assert.That(signal.IsRecoveryPhase, Is.True);
            Assert.That(signal.ActionPlanId, Is.GreaterThan(0));
            Assert.That(signal.FlipOutcome, Is.EqualTo(TickPlayerFlipOutcomeKind.DestroySelf));
            Assert.That(signal.HasFlipImpactContactTiming, Is.True);
            Assert.That(signal.FlipTargetBoxEntityId, Is.EqualTo(20));
            Assert.That(executeTick.PresentationData.EntityMotions, Is.Empty);
            Assert.That(executeTick.PresentationData.ImpactTransientSignals, Is.Empty);
            Assert.That(executeTick.PresentationData.FlipImpactSignals.Count, Is.EqualTo(1));
            Assert.That(snapshotAfter.TryGetEntity(20, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void FlipBlockedFailure_StillEntersRecoveryWithoutReturnTrack()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateWall(entityId: 30, position: new Vector2Int(-1, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var executeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var signal = executeTick.PresentationData.PlayerActionSignals.Single();

            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(executeTick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(signal.ExecutedThisTick, Is.True);
            Assert.That(signal.CanceledThisTick, Is.False);
            Assert.That(signal.IsRecoveryPhase, Is.True);
            Assert.That(executeTick.PresentationData.EntityMotions, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void FlipPreExecuteInvalidation_CancelsBeforeExecute()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            worldState.CreateWriteContext().MoveEntity(20, new SurfaceCell(FaceId.Floor, 2, 0));
            var cancelTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var signal = cancelTick.PresentationData.PlayerActionSignals.Single();
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(cancelTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(signal.ActiveActionKind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(signal.ExecutedThisTick, Is.False);
            Assert.That(signal.CanceledThisTick, Is.True);
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_MoveOccupancy_BlocksFlipStartUntilFirstUnlockedTick()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1, moveOccupancyTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(
                timingProfile,
                playerMoveCooldownTicks: 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Right),
                CreateBox(entityId: 20, position: new Vector2Int(2, 0), capabilities: BoxCapabilities.Flip),
            }, timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                timingProfile,
                playerControlTiming);

            var moveTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var lockedTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Flip(Direction.Right)));
            var unlockTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Flip(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    moveTick.MovementPhaseResult.CommitEvents,
                    "MoveCommitted",
                    "E=10",
                    "To=(1,0)",
                    "Facing=Right"),
                Is.True);
            Assert.That(CreateSnapshot(worldState).TryGetEntityExecutionLockState(10, out var executionLockState), Is.True);
            Assert.That(executionLockState.phase, Is.EqualTo(EntityExecutionPhase.Move));
            Assert.That(executionLockState.unlockTickExclusive, Is.EqualTo(3));
            Assert.That(lockedTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.False);
            Assert.That(lockedTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(unlockTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(unlockTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_CustomPushInputLock_IgnoresNewInputsUntilActionCompletes()
        {
            var timingProfile = CreateTimingProfile();
            var playerControlTiming = CreatePlayerControlTimingSnapshot(
                timingProfile,
                playerPushContactThresholdTicks: 1,
                playerPushExecuteDelayTicks: 2,
                playerPushInputLockDurationTicks: 4);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Right),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
                CreateWall(entityId: 91, position: new Vector2Int(-4, 0)),
            }, timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreatePlayerLogic(
                        10,
                        playerControlTiming),
                },
                timingProfile,
                playerControlTiming);

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var lockedWindupTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Flip(Direction.Left)));
            var executeTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Left)));
            var lockedRecoveryTick = pipeline.RunTick(new TickInput(4, PlayerTickCommand.Move(Direction.Left)));
            var lastLockedTick = pipeline.RunTick(new TickInput(5, PlayerTickCommand.Flip(Direction.Left)));
            var postLockTick = pipeline.RunTick(new TickInput(6, PlayerTickCommand.Move(Direction.Left)));

            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(lockedWindupTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(lockedWindupTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(lockedWindupTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(executeTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.True);
            Assert.That(lockedRecoveryTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(lockedRecoveryTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(lastLockedTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(lastLockedTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(postLockTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Move));
            Assert.That(postLockTick.PresentationData.PlayerActionSignals.Single().CompletedThisTick, Is.True);
            Assert.That(postLockTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.False);
            Assert.That(postLockTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.None));
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            GameplayTimingProfile timingProfile)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, timingProfile);
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            return worldState.CreateSnapshot();
        }

        private static GameplayTimingProfile CreateTimingProfile(
            int simulationTicksPerSecond = 60,
            int repeatedMoveIntervalTicks = 2,
            int moveOccupancyTicks = 12)
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: repeatedMoveIntervalTicks / (float)simulationTicksPerSecond,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 0.2f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: 0.2f,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8,
                moveOccupancyDurationSeconds: moveOccupancyTicks / (float)simulationTicksPerSecond);
        }

        private static PlayerLogic CreatePushThresholdPlayerLogic(int entityId)
        {
            return new PlayerLogic(
                entityId,
                pushContactThresholdTicks: 2,
                pushWindupTicks: 1,
                pushRecoveryTicks: 0,
                flipWindupTicks: 1,
                flipRecoveryTicks: 0);
        }

        private static PlayerLogic CreatePlayerLogic(
            int entityId,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming)
        {
            return new PlayerLogic(
                entityId,
                playerControlTiming.PushContactThresholdTicks,
                playerControlTiming.PushWindupTicks,
                playerControlTiming.PushRecoveryTicks,
                playerControlTiming.FlipWindupTicks,
                playerControlTiming.FlipRecoveryTicks);
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreatePlayerControlTimingSnapshot(
            GameplayTimingProfile timingProfile,
            int? playerMoveCooldownTicks = null,
            int? playerPushContactThresholdTicks = null,
            int playerPushExecuteDelayTicks = 1,
            int playerPushInputLockDurationTicks = 1,
            int playerFlipExecuteDelayTicks = 1,
            int playerFlipInputLockDurationTicks = 1)
        {
            return new PlayerControlTimingSettings
            {
                MoveCooldownSeconds = ResolveSeconds(
                    playerMoveCooldownTicks,
                    timingProfile.SimulationTicksPerSecond),
                PushContactThresholdSeconds = ResolveSeconds(
                    playerPushContactThresholdTicks,
                    timingProfile.SimulationTicksPerSecond),
                PushExecuteDelaySeconds = playerPushExecuteDelayTicks / (float)timingProfile.SimulationTicksPerSecond,
                PushInputLockDurationSeconds = playerPushInputLockDurationTicks / (float)timingProfile.SimulationTicksPerSecond,
                FlipExecuteDelaySeconds = playerFlipExecuteDelayTicks / (float)timingProfile.SimulationTicksPerSecond,
                FlipInputLockDurationSeconds = playerFlipInputLockDurationTicks / (float)timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static float ResolveSeconds(
            int? tickOverride,
            int simulationTicksPerSecond)
        {
            return tickOverride.HasValue
                ? tickOverride.Value / (float)simulationTicksPerSecond
                : -1f;
        }

        private static EntityState CreateUnit(
            int entityId,
            Vector2Int position,
            int hp = 3,
            int teamId = 1,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = facing,
            };
        }

        private static EntityState CreateBox(int entityId, Vector2Int position, BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boxCapabilities = capabilities,
            };
        }

        private static EntityState CreateWall(int entityId, Vector2Int position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
            };
        }
    }
}

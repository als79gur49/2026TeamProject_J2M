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
        public void PlayerControl_RetiredMoveCooldown_LegacyFallbackRemoved_HasNoPlayerState()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(timingProfile);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                },
                timingProfile);
            var pipeline = CreatePlayerControlPipeline(
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

            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(firstTick, 10);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(secondTick, 10);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(thirdTick, 10);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(fourthTick, 10);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(fifthTick, 10);
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.nextExplicitActionAllowedTick, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_RetiredMoveCooldown_OneTick_LegacyFallbackRemoved_DoesNotWriteActionGate()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(timingProfile);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                },
                timingProfile);
            var pipeline = CreatePlayerControlPipeline(
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

            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(firstTick, 10);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(secondTick, 10);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(thirdTick, 10);
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.nextExplicitActionAllowedTick, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_TopologyChangingBoundaryMove_RequiresCoveredLocomotionWithoutMoveCooldown()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(timingProfile);
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 1)),
                },
                boardBounds,
                timingProfile);
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                timingProfile,
                playerControlTiming);

            var boundaryTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var boundarySnapshot = CreateSnapshot(worldState);
            var followupTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));
            var followupSnapshot = CreateSnapshot(worldState);

            Assert.That(boundaryTick.PresentationData.TopologyMotion.HasValue, Is.True);
            Assert.That(boundarySnapshot.TryGetPlayerControlState(10, out var boundaryControlState), Is.True);
            Assert.That(boundaryControlState.nextExplicitActionAllowedTick, Is.Zero);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(followupTick, 10);
            Assert.That(followupSnapshot.TryGetPlayerControlState(10, out var followupControlState), Is.True);
            Assert.That(followupControlState.nextExplicitActionAllowedTick, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_LocomotionPresentationSignal_StaysTrueWithoutMoveCooldownAndDropsWhenBlocked()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(timingProfile);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateWall(entityId: 90, position: new Vector2Int(2, 0)),
                },
                timingProfile);
            var pipeline = CreatePlayerControlPipeline(
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
            Assert.That(firstSignal.MoveMotionGeneratedThisTick, Is.False);
            Assert.That(firstSignal.WaitingForNextMoveCadence, Is.False);

            Assert.That(secondSignal.ShouldPlayWalkLoop, Is.True);
            Assert.That(secondSignal.MoveMotionGeneratedThisTick, Is.False);
            Assert.That(secondSignal.WaitingForNextMoveCadence, Is.False);

            Assert.That(thirdSignal.ShouldPlayWalkLoop, Is.True);
            Assert.That(thirdSignal.MoveMotionGeneratedThisTick, Is.False);
            Assert.That(thirdSignal.WaitingForNextMoveCadence, Is.False);
            Assert.That(thirdTick.MovementPhaseResult.CommitEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_LocomotionPresentationSignal_InputReleaseWithoutMoveCooldown_DropsWalkLoop()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(timingProfile);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                },
                timingProfile);
            var pipeline = CreatePlayerControlPipeline(
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
        public void PlayerControl_ExplicitPush_StartsImmediatelyAgainstAdjacentPushBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(firstTick.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(firstTick.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(firstTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(firstTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(firstTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(firstTick.PresentationData.PlayerActionSignals.Single().TargetEntityId, Is.EqualTo(20));
            Assert.That(firstTick.PresentationData.PlayerActionSignals.Single().Direction, Is.EqualTo(Direction.Right));
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.nextExplicitActionAllowedTick, Is.Zero);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(20));
            Assert.That(controlState.activeAction.executionAttempted, Is.False);
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_ExplicitPushWithoutDirection_RemainsNoOp()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Create(Direction.None, pushPressed: true)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_ExplicitPushWithoutAdjacentPushTarget_RemainsNoOp()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateWall(entityId: 90, position: new Vector2Int(1, 0)),
            });
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(result.Trace.Text, Does.Not.Contain("ExplicitPushNotStartable"));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_ExplicitPushAgainstBlockedPushBox_EmitsPreMovementRejection()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(2, 0)),
            });
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(
                SemanticEventAssertions.ContainsEvent(
                    result.MovementPhaseResult.RejectedReasons,
                    "MovementRejected",
                    "Stage=PreMovement",
                    "Source=10",
                    "Reason=ExplicitPushNotStartable",
                    "Direction=Right",
                    "Target=20"),
                Is.True);
            Assert.That(
                result.Trace.Text,
                Does.Contain("MovementRejected|Stage=PreMovement|Source=10|Reason=ExplicitPushNotStartable|Direction=Right|Target=20"));
            Assert.That(result.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
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
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(result, 10);
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(player.facing, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_FlipStartsActionAgainstFlipCapableBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
            });
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
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
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
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
            Assert.That(executeTick.PresentationData.BoxSlideStartSignals.Single().BoxEntityId, Is.EqualTo(20));
            Assert.That(executeTick.PresentationData.BoxSlideStartSignals.Single().ActorEntityId, Is.EqualTo(10));
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(box.stateTimer, Is.EqualTo(DefaultBoxSlidePostCommitTimer));
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
            var pipeline = CreatePlayerControlPipeline(
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
            var pipeline = CreatePlayerControlPipeline(
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
        public void FlipLandingBlocked_RejectsBeforeActionStart_AndEmitsAudioOnlyAttempt()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateWall(entityId: 30, position: new Vector2Int(-1, 0)),
            });
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);
            var attemptSignal = result.PresentationData.PlayerActionAttemptSignals.Single();

            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
            Assert.That(result.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(attemptSignal.ActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(attemptSignal.FeedbackKind, Is.EqualTo(PlayerActionAttemptFeedbackKind.Invalid));
            Assert.That(attemptSignal.TargetEntityId, Is.EqualTo(20));
            Assert.That(attemptSignal.HasTarget, Is.True);
            Assert.That(attemptSignal.EmitsVisualFeedback, Is.False);
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
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
            var pipeline = CreatePlayerControlPipeline(
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
            Assert.That(cancelTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Move));
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(cancelTick, 10);
            Assert.That(signal.ActiveActionKind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(signal.ExecutedThisTick, Is.False);
            Assert.That(signal.CanceledThisTick, Is.True);
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(controlState.nextExplicitActionAllowedTick, Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void FlipPreExecuteLandingBlocked_CancelsBeforeExecute()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
            });
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            worldState.CreateWriteContext().SpawnEntity(CreateWall(entityId: 30, position: new Vector2Int(-1, 0)));
            var cancelTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var signal = cancelTick.PresentationData.PlayerActionSignals.Single();
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(cancelTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Move));
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(cancelTick, 10);
            Assert.That(cancelTick.PresentationData.EntityMotions, Is.Empty);
            Assert.That(cancelTick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(signal.ActiveActionKind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(signal.ExecutedThisTick, Is.False);
            Assert.That(signal.CanceledThisTick, Is.True);
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(controlState.nextExplicitActionAllowedTick, Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_MoveOccupancy_BlocksFlipStartUntilFirstUnlockedTick()
        {
            var timingProfile = CreateTimingProfile(repeatedMoveIntervalTicks: 1, moveOccupancyTicks: 1);
            var playerControlTiming = CreatePlayerControlTimingSnapshot(timingProfile);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Right),
                CreateBox(entityId: 20, position: new Vector2Int(2, 0), capabilities: BoxCapabilities.Flip),
            }, timingProfile);
            var pipeline = CreatePlayerControlPipeline(
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

            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(moveTick, 10);
            Assert.That(CreateSnapshot(worldState).TryGetEntityExecutionLockState(10, out _), Is.False);
            Assert.That(lockedTick.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(unlockTick.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
        }

        [Test]
        [Category("Core")]
        public void PlayerControl_CustomPushInputLock_IgnoresNewInputsUntilActionCompletes()
        {
            var timingProfile = CreateTimingProfile();
            var playerControlTiming = CreatePlayerControlTimingSnapshot(
                timingProfile,
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
            var pipeline = CreatePlayerControlPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreatePlayerLogic(
                        10,
                        playerControlTiming),
                },
                timingProfile,
                playerControlTiming);

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
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
            Assert.That(postLockTick.MovementPhaseResult.SortedIntents, Is.Empty);
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

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTimingProfile timingProfile)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                boardBounds,
                timingProfile);
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            return worldState.CreateSnapshot();
        }

        private static TickPipeline CreatePlayerControlPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerControlTiming = PlayerControlTimingSettings.CreateDefault()
                .CreateAuthoritativeSnapshot(
                    timingProfile.SimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds);

            return CreatePlayerControlPipeline(
                worldState,
                entityLogics,
                timingProfile,
                playerControlTiming);
        }

        private static TickPipeline CreatePlayerControlPipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            GameplayTimingProfile timingProfile,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                entityLogics,
                timingProfile,
                playerControlTiming,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline);
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

        private static PlayerLogic CreateImmediatePushPlayerLogic(int entityId)
        {
            return new PlayerLogic(
                entityId,
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
                playerControlTiming.PushWindupTicks,
                playerControlTiming.PushRecoveryTicks,
                playerControlTiming.FlipWindupTicks,
                playerControlTiming.FlipRecoveryTicks);
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreatePlayerControlTimingSnapshot(
            GameplayTimingProfile timingProfile,
            int playerPushExecuteDelayTicks = 1,
            int playerPushInputLockDurationTicks = 1,
            int playerFlipExecuteDelayTicks = 1,
            int playerFlipInputLockDurationTicks = 1)
        {
            return new PlayerControlTimingSettings
            {
                PushExecuteDelaySeconds = playerPushExecuteDelayTicks / (float)timingProfile.SimulationTicksPerSecond,
                PushInputLockDurationSeconds = playerPushInputLockDurationTicks / (float)timingProfile.SimulationTicksPerSecond,
                FlipExecuteDelaySeconds = playerFlipExecuteDelayTicks / (float)timingProfile.SimulationTicksPerSecond,
                FlipInputLockDurationSeconds = playerFlipInputLockDurationTicks / (float)timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static int DefaultBoxSlidePostCommitTimer =>
            GameplayTimingProfile.CreateDefault().BoxSlideStepIntervalTicks - 1;

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

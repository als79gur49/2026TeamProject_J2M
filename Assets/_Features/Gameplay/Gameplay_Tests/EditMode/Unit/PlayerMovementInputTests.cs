using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class PlayerMovementInputTests
    {
        [Test]
        public void PlayerLogic_MoveCommand_ProducesSingleRawMovementIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Move),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        public void PlayerLogic_FlipCommand_DoesNotProduceImmediateIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Flip(Direction.Right)),
                buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        public void PlayerLogic_ActivePushAction_ProducesSingleRawPushIntentOnExecuteTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    actionSequenceCounter = 2,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Push,
                        sequence = 2,
                        direction = Direction.Right,
                        targetEntityId = 30,
                        startTick = 0,
                        executeTick = 1,
                        recoveryEndTick = 1,
                    },
                });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Push),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        public void PlayerLogic_PrefabDerivedTimingSnapshot_OnlyExecutesOnSnapshotExecuteTick()
        {
            var playerPrefabObject = new GameObject("PlayerLogic_PrefabDerivedTimingSnapshot");

            try
            {
                var authoring = playerPrefabObject.AddComponent<PlayerActionTimingAuthoring>();
                var snapshot = authoring.CreateAuthoritativeSnapshot(60);
                var worldState = CreateWorldState(new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                });
                var writeContext = worldState.CreateWriteContext();
                writeContext.SetPlayerControlState(
                    10,
                    new PlayerControlState
                    {
                        actionSequenceCounter = 2,
                        activeAction = new PlayerActionRuntimeState
                        {
                            kind = PlayerActionKind.Push,
                            sequence = 2,
                            direction = Direction.Right,
                            targetEntityId = 30,
                            startTick = 1,
                            executeTick = 1 + snapshot.PushWindupTicks,
                            recoveryEndTick = 1 + snapshot.PushWindupTicks + snapshot.PushRecoveryTicks,
                        },
                    });
                var logic = new PlayerLogic(
                    entityId: 10,
                    pushContactThresholdTicks: GameplayTimingProfile.DefaultPlayerPushContactThresholdTicks,
                    pushWindupTicks: snapshot.PushWindupTicks,
                    pushRecoveryTicks: snapshot.PushRecoveryTicks,
                    flipWindupTicks: snapshot.FlipWindupTicks,
                    flipRecoveryTicks: snapshot.FlipRecoveryTicks);
                var beforeExecuteBuffer = new List<RawMovementIntent>();
                var executeBuffer = new List<RawMovementIntent>();

                logic.CollectMovementIntents(
                    worldState.CreateSnapshot(),
                    new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                    beforeExecuteBuffer);
                logic.CollectMovementIntents(
                    worldState.CreateSnapshot(),
                    new TickInput(2, PlayerTickCommand.Move(Direction.Right)),
                    executeBuffer);

                Assert.That(beforeExecuteBuffer, Is.Empty);
                Assert.That(executeBuffer.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
            }
            finally
            {
                Object.DestroyImmediate(playerPrefabObject);
            }
        }

        [Test]
        public void PlayerLogic_ImplementsMovementContractOnly()
        {
            var logic = new PlayerLogic(entityId: 10);

            Assert.That(logic, Is.InstanceOf<IMovementEntityLogic>());
            Assert.That(logic, Is.Not.InstanceOf<IAttackEntityLogic>());
        }

        [Test]
        public void PlayerLogic_ActiveFlipAction_ProducesSingleRawFlipIntentOnExecuteTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    actionSequenceCounter = 3,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Flip,
                        sequence = 3,
                        direction = Direction.Right,
                        targetEntityId = 30,
                        startTick = 0,
                        executeTick = 1,
                        recoveryEndTick = 1,
                    },
                });
            var logic = new PlayerLogic(entityId: 10);
            var movementBuffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                movementBuffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Flip),
                },
                movementBuffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        public void PlayerLogic_ActiveAction_BlocksOrdinaryMoveIntentBeforeExecuteTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    actionSequenceCounter = 1,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Push,
                        sequence = 1,
                        direction = Direction.Right,
                        targetEntityId = 30,
                        startTick = 1,
                        executeTick = 2,
                        recoveryEndTick = 2,
                    },
                });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        public void PlayerLogic_ActiveAction_BlocksFlipInputBeforeExecuteTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    actionSequenceCounter = 1,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Flip,
                        sequence = 1,
                        direction = Direction.Right,
                        targetEntityId = 30,
                        startTick = 1,
                        executeTick = 2,
                        recoveryEndTick = 2,
                    },
                });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Flip(Direction.Right)),
                buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        public void PlayerLogic_ActiveAction_BlocksOrdinaryMoveIntentDuringRecovery()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    actionSequenceCounter = 1,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Push,
                        sequence = 1,
                        direction = Direction.Right,
                        targetEntityId = 30,
                        startTick = 1,
                        executeTick = 2,
                        recoveryEndTick = 5,
                        executionAttempted = true,
                    },
                });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(4, PlayerTickCommand.Move(Direction.Up)),
                buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        public void PlayerLogic_NoMoveCommand_ProducesNoIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1),
                buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        public void PlayerLogic_DeadEntity_DoesNotProduceIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), hp: 0, markedForDeath: true),
            });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        public void PlayerLogic_ExposesControlledEntityId()
        {
            var logic = new PlayerLogic(entityId: 10);

            Assert.That(logic.ControlledEntityId, Is.EqualTo(10));
        }

        [Test]
        public void PlayerLogic_MoveCooldown_BlocksIntentUntilStateExpires()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    moveCooldownTicks = 2,
                });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        public void InputQuantizer_Vector2ToGridDirection_PicksDominantAxis()
        {
            var direction = GridMoveInputQuantizer.Quantize(new Vector2(0.8f, 0.2f), deadzone: 0.5f);

            Assert.That(direction, Is.EqualTo(Direction.Right));
        }

        [Test]
        public void InputQuantizer_DiagonalTie_ReturnsNone()
        {
            var direction = GridMoveInputQuantizer.Quantize(new Vector2(1f, 1f), deadzone: 0.5f);

            Assert.That(direction, Is.EqualTo(Direction.None));
        }

        [Test]
        public void InputQuantizer_BelowDeadzone_ReturnsNone()
        {
            var direction = GridMoveInputQuantizer.Quantize(new Vector2(0.2f, 0.1f), deadzone: 0.5f);

            Assert.That(direction, Is.EqualTo(Direction.None));
        }

        [Test]
        public void PlayerControlStateLogic_HoldAgainstSameBox_AccumulatesContactTicks()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
            });
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);
            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2, PlayerTickCommand.Move(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.pushContactTicks, Is.Zero);
            Assert.That(controlState.pushTargetEntityId, Is.Zero);
            Assert.That(controlState.pushDirection, Is.EqualTo(Direction.None));
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(20));
            Assert.That(controlState.activeAction.executeTick, Is.EqualTo(3));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick && transition.CurrentKind == PlayerActionKind.Push), Is.True);
        }

        [Test]
        public void PlayerControlQueries_StartAction_ZeroWindup_MarksExecutionAttemptedImmediately()
        {
            var startedState = PlayerControlQueries.StartAction(
                default,
                PlayerActionKind.Push,
                Direction.Right,
                targetEntityId: 20,
                startTick: 5,
                windupTicks: 0,
                recoveryTicks: 2);

            Assert.That(startedState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(startedState.activeAction.executeTick, Is.EqualTo(5));
            Assert.That(startedState.activeAction.recoveryEndTick, Is.EqualTo(7));
            Assert.That(startedState.activeAction.executionAttempted, Is.True);
        }

        [Test]
        public void PlayerControlStateLogic_InputRelease_ResetsContact()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
            });
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);
            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.pushContactTicks, Is.Zero);
            Assert.That(controlState.pushTargetEntityId, Is.Zero);
            Assert.That(controlState.pushDirection, Is.EqualTo(Direction.None));
        }

        [Test]
        public void PlayerControlStateLogic_TargetChange_ResetsAccumulation()
        {
            var firstWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
            });
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                firstWorld.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                firstWorld.CreateWriteContext(),
                updates,
                transitions);

            var secondWorld = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
            });
            secondWorld.CreateWriteContext().SetPlayerControlState(
                10,
                firstWorld.CreateSnapshot().TryGetPlayerControlState(10, out var priorState)
                    ? priorState
                    : default);

            logic.CommitPreMovementState(
                secondWorld.CreateSnapshot(),
                new TickInput(2, PlayerTickCommand.Move(Direction.Right)),
                secondWorld.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(secondWorld.CreateSnapshot().TryGetPlayerControlState(10, out var updatedState), Is.True);
            Assert.That(updatedState.pushContactTicks, Is.EqualTo(1));
            Assert.That(updatedState.pushTargetEntityId, Is.EqualTo(30));
        }

        [Test]
        public void PlayerControlStateLogic_ActiveAction_AdvancesExecutionAndCompletion()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            worldState.CreateWriteContext().SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    actionSequenceCounter = 4,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Push,
                        sequence = 4,
                        direction = Direction.Right,
                        targetEntityId = 20,
                        startTick = 1,
                        executeTick = 2,
                        recoveryEndTick = 2,
                    },
                });
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2, PlayerTickCommand.Move(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var executingState), Is.True);
            Assert.That(executingState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(executingState.activeAction.executionAttempted, Is.True);

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(3, PlayerTickCommand.Move(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.CompletedThisTick), Is.True);
        }

        [Test]
        public void PlayerControlStateLogic_ActivePushAction_IgnoresRecoveryInputsAndAllowsNextPushAfterCompletion()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
            });
            worldState.CreateWriteContext().SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    actionSequenceCounter = 1,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Push,
                        sequence = 1,
                        direction = Direction.Right,
                        targetEntityId = 20,
                        startTick = 1,
                        executeTick = 2,
                        recoveryEndTick = 5,
                        executionAttempted = true,
                    },
                });
            var logic = new PlayerControlStateLogic(
                entityId: 10,
                pushContactThresholdTicks: 1,
                pushWindupTicks: 1,
                pushRecoveryTicks: 3,
                flipWindupTicks: 1,
                flipRecoveryTicks: 3);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(3, PlayerTickCommand.Move(Direction.Up)),
                worldState.CreateWriteContext(),
                updates,
                transitions);
            AssertRecoveryStillActive(worldState, expectedKind: PlayerActionKind.Push, expectedSequence: 1);

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(4, PlayerTickCommand.Flip(Direction.Left)),
                worldState.CreateWriteContext(),
                updates,
                transitions);
            AssertRecoveryStillActive(worldState, expectedKind: PlayerActionKind.Push, expectedSequence: 1);

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(5, PlayerTickCommand.Move(Direction.Left)),
                worldState.CreateWriteContext(),
                updates,
                transitions);
            AssertRecoveryStillActive(worldState, expectedKind: PlayerActionKind.Push, expectedSequence: 1);

            updates.Clear();
            transitions.Clear();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(6),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var clearedState), Is.True);
            Assert.That(clearedState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.CompletedThisTick), Is.True);

            updates.Clear();
            transitions.Clear();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(7, PlayerTickCommand.Move(Direction.Left)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var restartedState), Is.True);
            Assert.That(restartedState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(restartedState.activeAction.sequence, Is.EqualTo(2));
            Assert.That(restartedState.activeAction.direction, Is.EqualTo(Direction.Left));
            Assert.That(restartedState.activeAction.targetEntityId, Is.EqualTo(30));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick && transition.CurrentKind == PlayerActionKind.Push), Is.True);
        }

        [Test]
        public void PlayerControlStateLogic_ActiveFlipAction_IgnoresRecoveryInputsAndAllowsNextFlipAfterCompletion()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
            });
            worldState.CreateWriteContext().SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    actionSequenceCounter = 1,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Flip,
                        sequence = 1,
                        direction = Direction.Right,
                        targetEntityId = 20,
                        startTick = 1,
                        executeTick = 2,
                        recoveryEndTick = 5,
                        executionAttempted = true,
                    },
                });
            var logic = new PlayerControlStateLogic(
                entityId: 10,
                pushContactThresholdTicks: 1,
                pushWindupTicks: 1,
                pushRecoveryTicks: 3,
                flipWindupTicks: 1,
                flipRecoveryTicks: 3);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(3, PlayerTickCommand.Move(Direction.Up)),
                worldState.CreateWriteContext(),
                updates,
                transitions);
            AssertRecoveryStillActive(worldState, expectedKind: PlayerActionKind.Flip, expectedSequence: 1);

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(4, PlayerTickCommand.Flip(Direction.Left)),
                worldState.CreateWriteContext(),
                updates,
                transitions);
            AssertRecoveryStillActive(worldState, expectedKind: PlayerActionKind.Flip, expectedSequence: 1);

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(5, PlayerTickCommand.Move(Direction.Left)),
                worldState.CreateWriteContext(),
                updates,
                transitions);
            AssertRecoveryStillActive(worldState, expectedKind: PlayerActionKind.Flip, expectedSequence: 1);

            updates.Clear();
            transitions.Clear();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(6),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var clearedState), Is.True);
            Assert.That(clearedState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.CompletedThisTick), Is.True);

            updates.Clear();
            transitions.Clear();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(7, PlayerTickCommand.Flip(Direction.Left)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var restartedState), Is.True);
            Assert.That(restartedState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(restartedState.activeAction.sequence, Is.EqualTo(2));
            Assert.That(restartedState.activeAction.direction, Is.EqualTo(Direction.Left));
            Assert.That(restartedState.activeAction.targetEntityId, Is.EqualTo(30));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick && transition.CurrentKind == PlayerActionKind.Flip), Is.True);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static EntityState CreateUnit(
            int entityId,
            Vector2Int position,
            int hp = 3,
            bool markedForDeath = false,
            Direction facing = Direction.Right)
        {
            return CreateUnit(entityId, SurfaceCell.FromPlanar(position), hp, markedForDeath, facing);
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int hp = 3,
            bool markedForDeath = false,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = hp > 0 ? EntityPhaseState.Idle : EntityPhaseState.Dead,
                facing = facing,
                markedForDeath = markedForDeath,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            Vector2Int position,
            BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            Direction facing = Direction.Right)
        {
            return CreateBox(entityId, SurfaceCell.FromPlanar(position), capabilities, facing);
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            Direction facing = Direction.Right)
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
                facing = facing,
                boxCapabilities = capabilities,
            };
        }

        private static void AssertRecoveryStillActive(
            WorldState worldState,
            PlayerActionKind expectedKind,
            int expectedSequence)
        {
            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.pushContactTicks, Is.Zero);
            Assert.That(controlState.pushTargetEntityId, Is.Zero);
            Assert.That(controlState.pushDirection, Is.EqualTo(Direction.None));
            Assert.That(controlState.activeAction.kind, Is.EqualTo(expectedKind));
            Assert.That(controlState.activeAction.sequence, Is.EqualTo(expectedSequence));
            Assert.That(controlState.activeAction.executionAttempted, Is.True);
        }
    }
}

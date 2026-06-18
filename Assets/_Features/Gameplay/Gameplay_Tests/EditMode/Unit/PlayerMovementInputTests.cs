using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
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
        [Category("Extended")]
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
        [Category("Extended")]
        public void PlayerLogic_OrdinaryMoveIntoPushBox_SuppressesMoveIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
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
        [Category("Extended")]
        public void PlayerLogic_OrdinaryMoveIntoItemPushBox_ProducesMoveIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(
                    entityId: 20,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    capabilities: BoxCapabilities.Push | BoxCapabilities.Item),
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
        [Category("Extended")]
        public void PlayerLogic_BufferedMoveCommand_DoesNotProduceImmediateIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right, isMoveBuffered: true)),
                buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
        public void PlayerLogic_ConfiguredTimingSnapshot_OnlyExecutesOnSnapshotExecuteTick()
        {
            var snapshot = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                60,
                GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds);
            var actionTicks = ResolvePushActionTickWindow(snapshot, startTick: 1);
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
                        executeTick = actionTicks.ExecuteTick,
                        recoveryEndTick = actionTicks.AfterRecoveryTick - 1,
                    },
                });
            var logic = new PlayerLogic(
                entityId: 10,
                pushWindupTicks: snapshot.PushWindupTicks,
                pushRecoveryTicks: snapshot.PushRecoveryTicks,
                flipWindupTicks: snapshot.FlipWindupTicks,
                flipRecoveryTicks: snapshot.FlipRecoveryTicks);
            var beforeExecuteBuffer = new List<RawMovementIntent>();
            var executeBuffer = new List<RawMovementIntent>();
            var afterRecoveryBuffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(actionTicks.BeforeExecuteTick, PlayerTickCommand.Move(Direction.Right)),
                beforeExecuteBuffer);
            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(actionTicks.ExecuteTick, PlayerTickCommand.Move(Direction.Right)),
                executeBuffer);
            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(actionTicks.AfterRecoveryTick, PlayerTickCommand.Move(Direction.Right)),
                afterRecoveryBuffer);

            Assert.That(beforeExecuteBuffer, Is.Empty);
            Assert.That(executeBuffer.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
            Assert.That(afterRecoveryBuffer, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void PlayerLogic_ImplementsMovementContractOnly()
        {
            var logic = new PlayerLogic(entityId: 10);

            Assert.That(logic, Is.InstanceOf<IMovementEntityLogic>());
            Assert.That(logic, Is.Not.InstanceOf<IAttackEntityLogic>());
        }

        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
        public void PlayerLogic_ExposesControlledEntityId()
        {
            var logic = new PlayerLogic(entityId: 10);

            Assert.That(logic.ControlledEntityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
        public void PlayerControlStateLogic_SameState_DoesNotWritePlayerControlState()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            worldState.CreateWriteContext().SetPlayerControlState(10, default);
            var snapshot = worldState.CreateSnapshot();
            var batch = new FinalizationBatch();
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                logic.CommitPreMovementState(
                    snapshot,
                    new TickInput(1),
                    new RecordingFinalizationContext(batch, snapshot, TickPhase.Plan),
                    updates,
                    transitions);
                counts = capture.Counts;
            }

            Assert.That(
                batch.Operations.Any(operation => operation.Kind == FinalizationOperationKind.SetPlayerControlState),
                Is.False);
            Assert.That(counts.PlayerControlStateSameStateSkippedCount, Is.EqualTo(1));
            Assert.That(counts.PlayerControlStateWrittenCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_StateChanged_WritesPlayerControlState()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            worldState.CreateWriteContext().SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    moveCooldownTicks = 2,
                    nextMoveAllowedTick = 5,
                });
            var snapshot = worldState.CreateSnapshot();
            var batch = new FinalizationBatch();
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                logic.CommitPreMovementState(
                    snapshot,
                    new TickInput(3),
                    new RecordingFinalizationContext(batch, snapshot, TickPhase.Plan),
                    updates,
                    transitions);
                counts = capture.Counts;
            }

            var operation = batch.Operations.Single(operation => operation.Kind == FinalizationOperationKind.SetPlayerControlState);
            Assert.That(operation.PlayerControlState.moveCooldownTicks, Is.EqualTo(1));
            Assert.That(operation.PlayerControlState.nextMoveAllowedTick, Is.EqualTo(5));
            Assert.That(counts.PlayerControlStateWrittenCount, Is.EqualTo(1));
            Assert.That(counts.PlayerControlStateSameStateSkippedCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_PushActionTransition_IsPreserved()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
            });
            var snapshot = worldState.CreateSnapshot();
            var batch = new FinalizationBatch();
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                snapshot,
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                new RecordingFinalizationContext(batch, snapshot, TickPhase.Plan),
                updates,
                transitions);

            var operation = batch.Operations.Single(operation => operation.Kind == FinalizationOperationKind.SetPlayerControlState);
            Assert.That(operation.PlayerControlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(operation.PlayerControlState.activeAction.targetEntityId, Is.EqualTo(20));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick && transition.CurrentKind == PlayerActionKind.Push), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_FlipActionTransition_IsPreserved()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
            });
            var snapshot = worldState.CreateSnapshot();
            var batch = new FinalizationBatch();
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                snapshot,
                new TickInput(1, PlayerTickCommand.Flip(Direction.Right)),
                new RecordingFinalizationContext(batch, snapshot, TickPhase.Plan),
                updates,
                transitions);

            var operation = batch.Operations.Single(operation => operation.Kind == FinalizationOperationKind.SetPlayerControlState);
            Assert.That(operation.PlayerControlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(operation.PlayerControlState.activeAction.targetEntityId, Is.EqualTo(20));
            Assert.That(transitions.Any(transition =>
                transition.EntityId == 10 &&
                transition.StartedThisTick &&
                transition.CurrentKind == PlayerActionKind.Flip &&
                transition.FlipResultTurnTransition.HasFlipResultTurn), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_SameState_SnapshotEquivalence()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var expectedState = new PlayerControlState
            {
                actionSequenceCounter = 3,
            };
            worldState.CreateWriteContext().SetPlayerControlState(10, expectedState);
            var beforeSnapshot = worldState.CreateSnapshot();
            var batch = new FinalizationBatch();
            var logic = new PlayerControlStateLogic(entityId: 10);

            logic.CommitPreMovementState(
                beforeSnapshot,
                new TickInput(1),
                new RecordingFinalizationContext(batch, beforeSnapshot, TickPhase.Plan),
                new List<string>(),
                new List<PlayerActionTransition>());
            batch.ApplyTo(worldState.CreateWriteContext(), delayedAttackEffectSink: null);

            var afterSnapshot = worldState.CreateSnapshot();
            Assert.That(afterSnapshot.TryGetPlayerControlState(10, out var actualState), Is.True);
            AssertPlayerControlStateEqual(expectedState, actualState);
            Assert.That(
                batch.Operations.Any(operation => operation.Kind == FinalizationOperationKind.SetPlayerControlState),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void InputQuantizer_Vector2ToGridDirection_PicksDominantAxis()
        {
            var direction = GridMoveInputQuantizer.Quantize(new Vector2(0.8f, 0.2f), deadzone: 0.5f);

            Assert.That(direction, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Extended")]
        public void InputQuantizer_DiagonalTie_ReturnsNone()
        {
            var direction = GridMoveInputQuantizer.Quantize(new Vector2(1f, 1f), deadzone: 0.5f);

            Assert.That(direction, Is.EqualTo(Direction.None));
        }

        [Test]
        [Category("Extended")]
        public void InputQuantizer_BelowDeadzone_ReturnsNone()
        {
            var direction = GridMoveInputQuantizer.Quantize(new Vector2(0.2f, 0.1f), deadzone: 0.5f);

            Assert.That(direction, Is.EqualTo(Direction.None));
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_PushPressedAgainstPushBox_StartsPushImmediately()
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
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.direction, Is.EqualTo(Direction.Right));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(20));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick && transition.CurrentKind == PlayerActionKind.Push), Is.True);
        }

        [Test]
        [Category("Core")]
        public void PlayerControlStateLogic_PushPressedAcrossBottomFrontBoundary_DoesNotStartOrReportAdjacentTarget()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)));
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Push(Direction.Up)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(updates, Has.None.Contains("ExplicitPushNotStartable"));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick), Is.False);
        }

        [Test]
        [Category("Core")]
        public void PlayerControlStateLogic_PushPressedAgainstPushLockedBox_DoesNotStartPush()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Destroy),
            });
            worldState.CreateWriteContext().SetBoxInteractionLockState(
                20,
                new BoxInteractionLockState(
                    100,
                    0,
                    expiresTickExclusive: 8,
                    blocksPush: true,
                    blocksFlip: false));
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(box.markedForDeath, Is.False);
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick), Is.False);
        }

        [Test]
        [Category("Core")]
        public void PlayerControlStateLogic_FlipPressedAgainstFlipLockedBox_DoesNotStartFlip()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
            });
            worldState.CreateWriteContext().SetBoxInteractionLockState(
                20,
                new BoxInteractionLockState(
                    100,
                    0,
                    expiresTickExclusive: 8,
                    blocksPush: false,
                    blocksFlip: true));
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Flip(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick), Is.False);
        }

        [Test]
        [Category("Core")]
        public void PlayerLogic_MoveCommand_WhileMoonBlockSpawnLockActive_StillProducesMoveIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 2, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
            });
            worldState.CreateWriteContext().SetBoxInteractionLockState(
                20,
                new BoxInteractionLockState(
                    100,
                    0,
                    expiresTickExclusive: 8,
                    blocksPush: true,
                    blocksFlip: true,
                    blocksDestroy: false,
                    sourceReason: BoxInteractionLockSourceReason.MoonBlockGeneratorSpawn));
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                buffer);

            Assert.That(buffer, Has.Count.EqualTo(1));
            Assert.That(buffer[0].SourceId, Is.EqualTo(10));
            Assert.That(buffer[0].Destination, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(buffer[0].CommandKind, Is.EqualTo(MovementCommandKind.Move));
        }

        [Test]
        [Category("Core")]
        public void PlayerControlStateLogic_PushOtherBox_WhileMoonBlockSpawnLockActive_StillStartsPush()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, -1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
            });
            worldState.CreateWriteContext().SetBoxInteractionLockState(
                20,
                new BoxInteractionLockState(
                    100,
                    0,
                    expiresTickExclusive: 8,
                    blocksPush: true,
                    blocksFlip: true,
                    blocksDestroy: false,
                    sourceReason: BoxInteractionLockSourceReason.MoonBlockGeneratorSpawn));
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(30));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick), Is.True);
        }

        [Test]
        [Category("Core")]
        public void PlayerControlStateLogic_PendingPush_TargetLockedBeforeExecute_CancelsAction()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
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
                        executeTick = 3,
                        recoveryEndTick = 3,
                        executionAttempted = false,
                    },
                });
            worldState.CreateWriteContext().SetBoxInteractionLockState(
                20,
                new BoxInteractionLockState(
                    100,
                    0,
                    expiresTickExclusive: 8,
                    blocksPush: true,
                    blocksFlip: false));
            var logic = new PlayerControlStateLogic(entityId: 10);

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2),
                worldState.CreateWriteContext(),
                new List<string>(),
                new List<PlayerActionTransition>());

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(controlState.nextMoveAllowedTick, Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void PlayerControlStateLogic_PendingPushAcrossBottomFrontBoundary_CancelsBeforeExecute()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)));
            worldState.CreateWriteContext().SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    actionSequenceCounter = 1,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Push,
                        sequence = 1,
                        direction = Direction.Up,
                        targetEntityId = 20,
                        startTick = 1,
                        executeTick = 3,
                        recoveryEndTick = 3,
                        executionAttempted = false,
                    },
                });
            var logic = new PlayerControlStateLogic(entityId: 10);

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2),
                worldState.CreateWriteContext(),
                new List<string>(),
                new List<PlayerActionTransition>());

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(controlState.nextMoveAllowedTick, Is.EqualTo(3));
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_MoveIntoUnit_DoesNotStartPushAction()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), hp: 3),
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

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_FlipInputAgainstUnit_DoesNotStartFlipAction()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Up),
                CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Floor, -1, 0), hp: 3),
            });
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Flip(Direction.Left)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_ExecutionLock_BlocksFlipStart()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
            });
            worldState.CreateWriteContext().SetEntityExecutionLockState(
                10,
                new EntityExecutionLockState
                {
                    phase = EntityExecutionPhase.Move,
                    sequence = 1,
                    unlockTickExclusive = 3,
                });
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2, PlayerTickCommand.Flip(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_FlipWindup_EntersMovementOwnedPhasedCarrier()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
            });
            var logic = new PlayerControlStateLogic(
                entityId: 10,
                pushWindupTicks: 1,
                pushRecoveryTicks: 0,
                flipWindupTicks: 2,
                flipRecoveryTicks: 0);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Flip(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(controlState.activeAction.executionAttempted, Is.False);
            Assert.That(snapshot.TryGetPhasedState(10, out var phasedState), Is.True);
            Assert.That(phasedState.ownerKind, Is.EqualTo(PhasedRuntimeStateOwnerKind.MovementPreMovement));
            Assert.That(phasedState.enteredTick, Is.EqualTo(1));
            Assert.That(snapshot.TryGetResolvedSpatialState(10, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Phased));
            Assert.That(snapshot.CanBeTargetedForNewSelection(10), Is.False);
            Assert.That(updates, Has.Some.Contains("PhaseEnter|Entity=10|Tick=1"));
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_FlipWindup_SustainsWithoutRewriting_AndClearsOnExecutionTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
            });
            var logic = new PlayerControlStateLogic(
                entityId: 10,
                pushWindupTicks: 1,
                pushRecoveryTicks: 0,
                flipWindupTicks: 2,
                flipRecoveryTicks: 0);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Flip(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            updates.Clear();
            transitions.Clear();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            var sustainedSnapshot = worldState.CreateSnapshot();
            Assert.That(sustainedSnapshot.TryGetPhasedState(10, out var sustainedState), Is.True);
            Assert.That(sustainedState.sequence, Is.EqualTo(1));
            Assert.That(sustainedState.enteredTick, Is.EqualTo(1));
            Assert.That(updates, Has.None.Contains("PhaseEnter|Entity=10"));
            Assert.That(updates, Has.None.Contains("PhaseExit|Entity=10"));

            updates.Clear();
            transitions.Clear();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(3),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            var clearedSnapshot = worldState.CreateSnapshot();
            Assert.That(clearedSnapshot.TryGetPhasedState(10, out _), Is.False);
            Assert.That(clearedSnapshot.TryGetPlayerControlState(10, out var executingState), Is.True);
            Assert.That(executingState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(executingState.activeAction.executionAttempted, Is.True);
            Assert.That(updates, Has.Some.Contains("PhaseExit|Entity=10|Tick=3"));
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_FlipWindup_RejectsForeignActivePhasedOwner()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
            });
            ((IPhasedStateCommitContext)worldState.CreateWriteContext()).SetPhasedState(
                10,
                PhasedRuntimeStateQueries.ForceDebug(default, tickIndex: 0));
            var logic = new PlayerControlStateLogic(entityId: 10);

            Assert.Throws<InvalidOperationException>(
                () => logic.CommitPreMovementState(
                    worldState.CreateSnapshot(),
                    new TickInput(1, PlayerTickCommand.Flip(Direction.Right)),
                    worldState.CreateWriteContext(),
                    new List<string>(),
                    new List<PlayerActionTransition>()));
        }

        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
        public void PlayerControlStateLogic_PushWithoutDirection_IsNoOp()
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
                new TickInput(1, PlayerTickCommand.Create(Direction.None, pushPressed: true)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_PushWithDirectionAndNoAdjacentPushableBox_IsNoOp()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 1, 0)),
            });
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick), Is.False);
            Assert.That(updates, Has.None.Contains("ExplicitPushNotStartable"));
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_PushAgainstAdjacentButNotStartablePushBox_RecordsPreMovementRejection()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new SurfaceCell(FaceId.Floor, 2, 0)),
            });
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick), Is.False);
            Assert.That(
                updates,
                Has.Some.EqualTo("MovementRejected|Stage=PreMovement|Source=10|Reason=ExplicitPushNotStartable|Direction=Right|Target=20"));
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_PushPressed_StartsAgainstCurrentTarget()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Push),
            });
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var updatedState), Is.True);
            Assert.That(updatedState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(updatedState.activeAction.targetEntityId, Is.EqualTo(30));
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_ActiveAction_AdvancesExecutionAndCompletion()
        {
            var worldState = CreateValidPendingPushActionWorldState(
                playerEntityId: 10,
                playerPosition: new SurfaceCell(FaceId.Floor, 0, 0),
                targetEntityId: 20,
                direction: Direction.Right);
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
        [Category("Extended")]
        public void PlayerControlStateLogic_BufferedMove_DoesNotStartPushWithoutFreshPress()
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
                new TickInput(2, PlayerTickCommand.Move(Direction.Right, isMoveBuffered: true)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(controlState.nextMoveAllowedTick, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_BufferedMove_WithoutTarget_RemainsIdle()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var logic = new PlayerControlStateLogic(entityId: 10);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2, PlayerTickCommand.Move(Direction.Right, isMoveBuffered: true)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlStateLogic_CanceledPendingAction_BlocksSameTickMoveFallback()
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
                        kind = PlayerActionKind.Flip,
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

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(controlState.nextMoveAllowedTick, Is.EqualTo(3));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.CanceledThisTick), Is.True);
        }

        [Test]
        [Category("Extended")]
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
                pushWindupTicks: 1,
                pushRecoveryTicks: 3,
                flipWindupTicks: 1,
                flipRecoveryTicks: 3);
            var updates = new List<string>();
            var transitions = new List<PlayerActionTransition>();

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(3, PlayerTickCommand.Push(Direction.Left)),
                worldState.CreateWriteContext(),
                updates,
                transitions);
            AssertRecoveryStillActive(worldState, expectedKind: PlayerActionKind.Push, expectedSequence: 1);

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(4, PlayerTickCommand.Push(Direction.Left)),
                worldState.CreateWriteContext(),
                updates,
                transitions);
            AssertRecoveryStillActive(worldState, expectedKind: PlayerActionKind.Push, expectedSequence: 1);

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(5, PlayerTickCommand.Push(Direction.Left)),
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
                new TickInput(7, PlayerTickCommand.Push(Direction.Left)),
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
        [Category("Extended")]
        public void PlayerControlStateLogic_ActiveFlipAction_IgnoresRecoveryInputsAndAllowsNextFlipAfterCompletion()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Flip),
                CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Floor, 0, 1), capabilities: BoxCapabilities.Flip),
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
                new TickInput(4, PlayerTickCommand.Flip(Direction.Up)),
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
                new TickInput(7, PlayerTickCommand.Flip(Direction.Up)),
                worldState.CreateWriteContext(),
                updates,
                transitions);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var restartedState), Is.True);
            Assert.That(restartedState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(restartedState.activeAction.sequence, Is.EqualTo(2));
            Assert.That(restartedState.activeAction.direction, Is.EqualTo(Direction.Up));
            Assert.That(restartedState.activeAction.targetEntityId, Is.EqualTo(30));
            Assert.That(transitions.Any(transition => transition.EntityId == 10 && transition.StartedThisTick && transition.CurrentKind == PlayerActionKind.Flip), Is.True);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities, BoardBounds boardBounds)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                boardBounds);
        }

        private static void AssertPlayerControlStateEqual(PlayerControlState expected, PlayerControlState actual)
        {
            Assert.That(actual.moveCooldownTicks, Is.EqualTo(expected.moveCooldownTicks));
            Assert.That(actual.nextMoveAllowedTick, Is.EqualTo(expected.nextMoveAllowedTick));
            Assert.That(actual.actionSequenceCounter, Is.EqualTo(expected.actionSequenceCounter));
            Assert.That(actual.activeAction.kind, Is.EqualTo(expected.activeAction.kind));
            Assert.That(actual.activeAction.sequence, Is.EqualTo(expected.activeAction.sequence));
            Assert.That(actual.activeAction.direction, Is.EqualTo(expected.activeAction.direction));
            Assert.That(actual.activeAction.targetEntityId, Is.EqualTo(expected.activeAction.targetEntityId));
            Assert.That(actual.activeAction.startTick, Is.EqualTo(expected.activeAction.startTick));
            Assert.That(actual.activeAction.executeTick, Is.EqualTo(expected.activeAction.executeTick));
            Assert.That(actual.activeAction.recoveryEndTick, Is.EqualTo(expected.activeAction.recoveryEndTick));
            Assert.That(actual.activeAction.executionAttempted, Is.EqualTo(expected.activeAction.executionAttempted));
            Assert.That(actual.queuedFree2DAction.kind, Is.EqualTo(expected.queuedFree2DAction.kind));
            Assert.That(actual.queuedFree2DAction.direction, Is.EqualTo(expected.queuedFree2DAction.direction));
            Assert.That(actual.queuedFree2DAction.requestedTick, Is.EqualTo(expected.queuedFree2DAction.requestedTick));
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

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
            };
        }

        // Pending-action fixtures must remain executable under CanPendingActionStillExecute.
        private static WorldState CreateValidPendingPushActionWorldState(
            int playerEntityId,
            SurfaceCell playerPosition,
            int targetEntityId,
            Direction direction)
        {
            return CreateWorldState(new[]
            {
                CreateUnit(playerEntityId, playerPosition),
                CreateBox(
                    targetEntityId,
                    playerPosition + ResolveDirectionDelta(direction),
                    capabilities: BoxCapabilities.Push),
            });
        }

        // Timing-sensitive assertions must derive ticks from the authoritative snapshot, not hardcoded literals.
        private static (int BeforeExecuteTick, int ExecuteTick, int AfterRecoveryTick) ResolvePushActionTickWindow(
            PlayerControlTimingAuthoritativeSnapshot snapshot,
            int startTick)
        {
            var executeTick = startTick + snapshot.PushWindupTicks;
            return (executeTick - 1, executeTick, executeTick + snapshot.PushRecoveryTicks + 1);
        }

        private static Vector2Int ResolveDirectionDelta(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Vector2Int.up;

                case Direction.Right:
                    return Vector2Int.right;

                case Direction.Down:
                    return Vector2Int.down;

                case Direction.Left:
                    return Vector2Int.left;

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported direction for push fixture.");
            }
        }

        private static void AssertRecoveryStillActive(
            WorldState worldState,
            PlayerActionKind expectedKind,
            int expectedSequence)
        {
            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(expectedKind));
            Assert.That(controlState.activeAction.sequence, Is.EqualTo(expectedSequence));
            Assert.That(controlState.activeAction.executionAttempted, Is.True);
        }
    }
}

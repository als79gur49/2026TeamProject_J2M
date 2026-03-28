using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
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
        public void PlayerLogic_FlipCommand_ProducesSingleRawMovementIntent()
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

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Flip),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        public void PlayerLogic_PushCommand_ProducesSingleRawMovementIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Push(Direction.Right)),
                buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Push),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        public void PlayerLogic_PushCommand_DoesNotProduceRawAttackIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawAttackIntent>();

            logic.CollectAttackIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Push(Direction.Left)),
                buffer);

            Assert.That(buffer, Is.Empty);
        }

        [Test]
        public void PlayerLogic_FlipInput_TakesPriorityOverPushForMovementIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var logic = new PlayerLogic(entityId: 10);
            var movementBuffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(
                    1,
                    PlayerTickCommand.Create(
                        Direction.Right,
                        pushPressed: true,
                        flipPressed: true)),
                movementBuffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Flip),
                },
                movementBuffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
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
        public void PlayerLogic_ControlsEntity_ForMovementPhaseOnly()
        {
            var logic = new PlayerLogic(entityId: 10);

            Assert.That(logic.ControlsEntity(10, TickPhase.Movement), Is.True);
            Assert.That(logic.ControlsEntity(10, TickPhase.Attack), Is.False);
            Assert.That(logic.ControlsEntity(10, TickPhase.Cleanup), Is.False);
            Assert.That(logic.ControlsEntity(20, TickPhase.Movement), Is.False);
            Assert.That(logic.ControlsEntity(20, TickPhase.Attack), Is.False);
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
        public void InputRepeatCooldown_InitialTap_IssuesImmediateMove()
        {
            var cooldown = new InputRepeatCooldown(
                initialMoveDelayTicks: 0,
                repeatedMoveIntervalTicks: 2,
                directionChangeConsumesDelay: false);

            var command = cooldown.BuildCommand(currentTick: 1, quantizedDirection: Direction.Right);

            AssertCommand(command, expectedDirection: Direction.Right);
        }

        [Test]
        public void InputRepeatCooldown_HoldSameDirection_RespectsRepeatInterval()
        {
            var cooldown = new InputRepeatCooldown(
                initialMoveDelayTicks: 0,
                repeatedMoveIntervalTicks: 2,
                directionChangeConsumesDelay: false);

            var firstTick = cooldown.BuildCommand(currentTick: 1, quantizedDirection: Direction.Right);
            var secondTick = cooldown.BuildCommand(currentTick: 2, quantizedDirection: Direction.Right);
            var thirdTick = cooldown.BuildCommand(currentTick: 3, quantizedDirection: Direction.Right);

            AssertCommand(firstTick, expectedDirection: Direction.Right);
            AssertCommand(secondTick, expectedDirection: Direction.None);
            AssertCommand(thirdTick, expectedDirection: Direction.Right);
        }

        [Test]
        public void InputRepeatCooldown_DirectionChange_IssuesImmediateMove()
        {
            var cooldown = new InputRepeatCooldown(
                initialMoveDelayTicks: 0,
                repeatedMoveIntervalTicks: 2,
                directionChangeConsumesDelay: false);

            var firstTick = cooldown.BuildCommand(currentTick: 1, quantizedDirection: Direction.Right);
            var secondTick = cooldown.BuildCommand(currentTick: 2, quantizedDirection: Direction.Up);

            AssertCommand(firstTick, expectedDirection: Direction.Right);
            AssertCommand(secondTick, expectedDirection: Direction.Up);
        }

        [Test]
        public void InputRepeatCooldown_InitialDelay_WaitsConfiguredTicks()
        {
            var cooldown = new InputRepeatCooldown(
                initialMoveDelayTicks: 2,
                repeatedMoveIntervalTicks: 2,
                directionChangeConsumesDelay: false);

            var firstTick = cooldown.BuildCommand(currentTick: 1, quantizedDirection: Direction.Right);
            var secondTick = cooldown.BuildCommand(currentTick: 2, quantizedDirection: Direction.Right);
            var thirdTick = cooldown.BuildCommand(currentTick: 3, quantizedDirection: Direction.Right);

            AssertCommand(firstTick, expectedDirection: Direction.None);
            AssertCommand(secondTick, expectedDirection: Direction.None);
            AssertCommand(thirdTick, expectedDirection: Direction.Right);
        }

        [Test]
        public void InputRepeatCooldown_NoneInput_ResetsHoldState()
        {
            var cooldown = new InputRepeatCooldown(
                initialMoveDelayTicks: 0,
                repeatedMoveIntervalTicks: 2,
                directionChangeConsumesDelay: false);

            var firstTick = cooldown.BuildCommand(currentTick: 1, quantizedDirection: Direction.Right);
            var resetTick = cooldown.BuildCommand(currentTick: 2, quantizedDirection: Direction.None);
            var resumedTick = cooldown.BuildCommand(currentTick: 3, quantizedDirection: Direction.Right);

            AssertCommand(firstTick, expectedDirection: Direction.Right);
            AssertCommand(resetTick, expectedDirection: Direction.None);
            AssertCommand(resumedTick, expectedDirection: Direction.Right);
        }

        [Test]
        public void InputRepeatCooldown_DirectionChange_WithDelay_RespectsInitialDelay()
        {
            var cooldown = new InputRepeatCooldown(
                initialMoveDelayTicks: 2,
                repeatedMoveIntervalTicks: 2,
                directionChangeConsumesDelay: true);

            var firstTick = cooldown.BuildCommand(currentTick: 1, quantizedDirection: Direction.Right);
            var secondTick = cooldown.BuildCommand(currentTick: 2, quantizedDirection: Direction.Right);
            var thirdTick = cooldown.BuildCommand(currentTick: 3, quantizedDirection: Direction.Right);
            var directionChangeTick = cooldown.BuildCommand(currentTick: 4, quantizedDirection: Direction.Up);
            var delayedDirectionTick = cooldown.BuildCommand(currentTick: 5, quantizedDirection: Direction.Up);
            var issuedDirectionTick = cooldown.BuildCommand(currentTick: 6, quantizedDirection: Direction.Up);

            AssertCommand(firstTick, expectedDirection: Direction.None);
            AssertCommand(secondTick, expectedDirection: Direction.None);
            AssertCommand(thirdTick, expectedDirection: Direction.Right);
            AssertCommand(directionChangeTick, expectedDirection: Direction.None);
            AssertCommand(delayedDirectionTick, expectedDirection: Direction.None);
            AssertCommand(issuedDirectionTick, expectedDirection: Direction.Up);
        }

        private static void AssertCommand(
            PlayerTickCommand command,
            Direction expectedDirection)
        {
            Assert.That(command.MoveDirection, Is.EqualTo(expectedDirection));
            Assert.That(command.PushPressed, Is.False);
            Assert.That(command.FlipPressed, Is.False);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static EntityState CreateUnit(
            int entityId,
            Vector2Int position,
            int hp = 3,
            bool markedForDeath = false)
        {
            return CreateUnit(entityId, SurfaceCell.FromPlanar(position), hp, markedForDeath);
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int hp = 3,
            bool markedForDeath = false)
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
                facing = Direction.Right,
                markedForDeath = markedForDeath,
            };
        }
    }
}

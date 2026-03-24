using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class PlayerMovementInputTests
    {
        [Test]
        public void PlayerLogic_MoveCommand_ProducesSingleRawMovementIntent()
        {
            var worldState = GameplayCompositionRoot.CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
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
        public void PlayerLogic_InteractSlideCommand_ProducesSingleRawMovementIntent()
        {
            var worldState = GameplayCompositionRoot.CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
            });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.InteractSlide(Direction.Right)),
                buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.InteractSlide),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        public void PlayerLogic_InteractFlipCommand_ProducesSingleRawMovementIntent()
        {
            var worldState = GameplayCompositionRoot.CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
            });
            var logic = new PlayerLogic(entityId: 10);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.InteractFlip(Direction.Left)),
                buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Destination: new Vector2Int(-1, 0), Command: MovementCommandKind.InteractFlip),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        public void PlayerLogic_NoMoveCommand_ProducesNoIntent()
        {
            var worldState = GameplayCompositionRoot.CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
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
            var worldState = GameplayCompositionRoot.CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), hp: 0, markedForDeath: true),
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
        public void InputRepeatCooldown_InitialTap_IssuesImmediateMove()
        {
            var cooldown = new InputRepeatCooldown(
                initialMoveDelayTicks: 0,
                repeatedMoveIntervalTicks: 2,
                directionChangeConsumesDelay: false);

            var command = cooldown.BuildCommand(currentTick: 1, quantizedDirection: Direction.Right);

            AssertCommand(command, PlayerPrimaryCommandKind.Move, Direction.Right);
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

            AssertCommand(firstTick, PlayerPrimaryCommandKind.Move, Direction.Right);
            AssertCommand(secondTick, PlayerPrimaryCommandKind.None, Direction.None);
            AssertCommand(thirdTick, PlayerPrimaryCommandKind.Move, Direction.Right);
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

            AssertCommand(firstTick, PlayerPrimaryCommandKind.Move, Direction.Right);
            AssertCommand(secondTick, PlayerPrimaryCommandKind.Move, Direction.Up);
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

            AssertCommand(firstTick, PlayerPrimaryCommandKind.None, Direction.None);
            AssertCommand(secondTick, PlayerPrimaryCommandKind.None, Direction.None);
            AssertCommand(thirdTick, PlayerPrimaryCommandKind.Move, Direction.Right);
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

            AssertCommand(firstTick, PlayerPrimaryCommandKind.Move, Direction.Right);
            AssertCommand(resetTick, PlayerPrimaryCommandKind.None, Direction.None);
            AssertCommand(resumedTick, PlayerPrimaryCommandKind.Move, Direction.Right);
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

            AssertCommand(firstTick, PlayerPrimaryCommandKind.None, Direction.None);
            AssertCommand(secondTick, PlayerPrimaryCommandKind.None, Direction.None);
            AssertCommand(thirdTick, PlayerPrimaryCommandKind.Move, Direction.Right);
            AssertCommand(directionChangeTick, PlayerPrimaryCommandKind.None, Direction.None);
            AssertCommand(delayedDirectionTick, PlayerPrimaryCommandKind.None, Direction.None);
            AssertCommand(issuedDirectionTick, PlayerPrimaryCommandKind.Move, Direction.Up);
        }

        private static void AssertCommand(
            PlayerTickCommand command,
            PlayerPrimaryCommandKind expectedKind,
            Direction expectedDirection)
        {
            Assert.That(command.PrimaryKind, Is.EqualTo(expectedKind));
            Assert.That(command.Direction, Is.EqualTo(expectedDirection));
        }

        private static EntityState CreateUnit(
            int entityId,
            Vector2Int position,
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

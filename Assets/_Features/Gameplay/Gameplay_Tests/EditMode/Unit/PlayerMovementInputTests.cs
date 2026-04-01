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
        public void PlayerLogic_ArmedPushState_ProducesSingleRawPushIntent()
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
                    pushContactTicks = GameplayTimingProfile.DefaultPlayerPushContactThresholdTicks,
                    pushTargetEntityId = 30,
                    pushDirection = Direction.Right,
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
        public void PlayerLogic_ImplementsMovementContractOnly()
        {
            var logic = new PlayerLogic(entityId: 10);

            Assert.That(logic, Is.InstanceOf<IMovementEntityLogic>());
            Assert.That(logic, Is.Not.InstanceOf<IAttackEntityLogic>());
        }

        [Test]
        public void PlayerLogic_FlipInput_TakesPriorityOverPushIntent()
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
                    pushContactTicks = GameplayTimingProfile.DefaultPlayerPushContactThresholdTicks,
                    pushTargetEntityId = 30,
                    pushDirection = Direction.Right,
                });
            var logic = new PlayerLogic(entityId: 10);
            var movementBuffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(
                worldState.CreateSnapshot(),
                new TickInput(
                    1,
                    PlayerTickCommand.Create(
                        Direction.Right,
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

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                worldState.CreateWriteContext(),
                updates);
            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2, PlayerTickCommand.Move(Direction.Right)),
                worldState.CreateWriteContext(),
                updates);

            Assert.That(worldState.CreateSnapshot().TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.pushContactTicks, Is.EqualTo(2));
            Assert.That(controlState.pushTargetEntityId, Is.EqualTo(20));
            Assert.That(controlState.pushDirection, Is.EqualTo(Direction.Right));
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

            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                worldState.CreateWriteContext(),
                updates);
            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2),
                worldState.CreateWriteContext(),
                updates);

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

            logic.CommitPreMovementState(
                firstWorld.CreateSnapshot(),
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                firstWorld.CreateWriteContext(),
                updates);

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
                updates);

            Assert.That(secondWorld.CreateSnapshot().TryGetPlayerControlState(10, out var updatedState), Is.True);
            Assert.That(updatedState.pushContactTicks, Is.EqualTo(1));
            Assert.That(updatedState.pushTargetEntityId, Is.EqualTo(30));
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
    }
}

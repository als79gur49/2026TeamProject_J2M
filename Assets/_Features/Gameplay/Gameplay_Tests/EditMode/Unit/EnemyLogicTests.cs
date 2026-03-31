using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyLogicTests
    {
        [Test]
        public void EnemyLogic_ImplementsMovementAndAttackContracts()
        {
            var logic = new EnemyLogic(entityId: 40);

            Assert.That(logic, Is.InstanceOf<IMovementEntityLogic>());
            Assert.That(logic, Is.InstanceOf<IAttackEntityLogic>());
            Assert.That(logic.ControlledEntityId, Is.EqualTo(40));
        }

        [Test]
        public void EnemyLogic_PatrolMode_ProducesForwardMovementIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Move),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        public void EnemyLogic_ChaseMode_ProducesMovementTowardNearestOpponent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 20, teamId: 1, position: new Vector2Int(0, 4), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Up),
            });
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Move),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        public void EnemyLogic_ChaseMode_FallsBackToSecondaryAxisWhenPrimaryStepIsBlocked()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(2, 1), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 30, teamId: 2, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawMovementIntent>();

            logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, Destination: new Vector2Int(0, 1), Command: MovementCommandKind.Move),
                },
                buffer.Select(intent => (intent.SourceId, intent.Destination, intent.CommandKind)).ToArray());
        }

        [Test]
        public void EnemyLogic_AttackMode_AdjacentOpponent_ProducesRawAttackIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Attack, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var buffer = new List<RawAttackIntent>();

            logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(1), buffer);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 40, TargetId: 10),
                },
                buffer.Select(intent => (intent.SourceId, intent.TargetId)).ToArray());
        }

        [Test]
        public void EnemyLogic_RecoverMode_DoesNotProduceMovementOrAttackIntent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(1, 0), aiMode: EnemyAiMode.None),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), aiMode: EnemyAiMode.Recover, facing: Direction.Right),
            });
            var logic = new EnemyLogic(entityId: 40);
            var movementBuffer = new List<RawMovementIntent>();
            var attackBuffer = new List<RawAttackIntent>();

            logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), movementBuffer);
            logic.CollectAttackIntents(worldState.CreateSnapshot(), new TickInput(1), attackBuffer);

            Assert.That(movementBuffer, Is.Empty);
            Assert.That(attackBuffer, Is.Empty);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            Vector2Int position,
            EnemyAiMode aiMode,
            Direction facing = Direction.Right,
            int hp = 3)
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
                stateTimer = 0,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
            };
        }
    }
}

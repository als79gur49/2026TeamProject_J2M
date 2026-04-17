using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineJumpLandingExactStackTests
    {
        [Test]
        [Category("Extended")]
        public void IsExclusiveLockedPlayerStack_PlayerOnly_ReturnsTrue()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, targetCell, teamId: 1),
            });
            PrimePlayerControlState(worldState, 10);

            Assert.That(
                TickPipeline.IsExclusiveLockedPlayerStack(worldState.CreateSnapshot(), targetCell, sourceEntityId: 40),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void IsExclusiveLockedPlayerStack_PlayerPlusHostile_ReturnsFalse()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, targetCell, teamId: 1),
                CreateUnit(20, targetCell, teamId: 2),
            });
            PrimePlayerControlState(worldState, 10);

            Assert.That(
                TickPipeline.IsExclusiveLockedPlayerStack(worldState.CreateSnapshot(), targetCell, sourceEntityId: 40),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void IsExclusiveLockedPlayerStack_PlayerPlusFriendly_ReturnsFalse()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, targetCell, teamId: 1),
                CreateUnit(20, targetCell, teamId: 1),
            });
            PrimePlayerControlState(worldState, 10);

            Assert.That(
                TickPipeline.IsExclusiveLockedPlayerStack(worldState.CreateSnapshot(), targetCell, sourceEntityId: 40),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void IsExclusiveLockedPlayerStack_PlayerUnitWithoutPlayerControlState_ReturnsFalse()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, targetCell, teamId: 1),
            });

            Assert.That(
                TickPipeline.IsExclusiveLockedPlayerStack(worldState.CreateSnapshot(), targetCell, sourceEntityId: 40),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void IsExclusiveLockedPlayerStack_MarkedForDeathPlayer_ReturnsFalse()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, targetCell, teamId: 1, hp: 0, markedForDeath: true),
            });
            PrimePlayerControlState(worldState, 10);

            Assert.That(
                TickPipeline.IsExclusiveLockedPlayerStack(worldState.CreateSnapshot(), targetCell, sourceEntityId: 40),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void IsExclusiveLockedPlayerStack_DetachedExtraOccupant_IsIgnored()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, targetCell, teamId: 1),
                CreateUnit(20, targetCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
            });
            PrimePlayerControlState(worldState, 10);

            Assert.That(
                TickPipeline.IsExclusiveLockedPlayerStack(worldState.CreateSnapshot(), targetCell, sourceEntityId: 40),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void IsExclusiveLockedPlayerStack_MultipleControlledPlayers_ReturnsFalse()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, targetCell, teamId: 1),
                CreateUnit(11, targetCell, teamId: 1),
            });
            PrimePlayerControlState(worldState, 10, 11);

            Assert.That(
                TickPipeline.IsExclusiveLockedPlayerStack(worldState.CreateSnapshot(), targetCell, sourceEntityId: 40),
                Is.False);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static void PrimePlayerControlState(WorldState worldState, params int[] entityIds)
        {
            var writeContext = worldState.CreateWriteContext();
            for (var i = 0; i < entityIds.Length; i++)
            {
                writeContext.SetPlayerControlState(entityIds[i], default);
            }
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int teamId,
            int hp = 3,
            bool markedForDeath = false,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp > 0 ? hp : 1,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = Direction.Right,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
                spawnTick = 0,
                aiMode = EnemyAiMode.None,
                aiStateTimer = 0,
            };
        }
    }
}

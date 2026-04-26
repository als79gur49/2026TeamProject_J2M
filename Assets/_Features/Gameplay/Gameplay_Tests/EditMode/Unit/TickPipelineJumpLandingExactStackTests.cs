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
        [Category("Core")]
        public void BoxCapabilities_FlagValues_AreStable()
        {
            Assert.That((int)BoxCapabilities.Push, Is.EqualTo(1));
            Assert.That((int)BoxCapabilities.Flip, Is.EqualTo(2));
            Assert.That((int)BoxCapabilities.Item, Is.EqualTo(4));
            Assert.That((int)BoxCapabilities.Destroy, Is.EqualTo(8));
            Assert.That((int)BoxCapabilities.JumpCrushable, Is.EqualTo(16));
        }

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
        [Category("Core")]
        public void EvaluateJumpCrushLandingCell_JumpCrushableBoxOnLockedTarget_AllowsAndReportsBox()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(40, sourceCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
                CreateBox(50, targetCell, BoxCapabilities.JumpCrushable),
            });
            var snapshot = worldState.CreateSnapshot();

            var evaluation = RuntimeSettlementLegalityPolicy.EvaluateJumpCrushLandingCell(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, 40, EntityType.Unit),
                    targetCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new JumpLandingEvidence(snapshot, targetCell));

            Assert.That(evaluation.LegalityResult.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(evaluation.CrushedBoxEntityId, Is.EqualTo(50));
        }

        [Test]
        [Category("Extended")]
        [TestCase((int)BoxCapabilities.None)]
        [TestCase((int)BoxCapabilities.Destroy)]
        [TestCase((int)BoxCapabilities.Item)]
        public void EvaluateJumpCrushLandingCell_NonJumpCrushableBoxOnLockedTarget_Blocks(int boxCapabilityValue)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(40, sourceCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
                CreateBox(50, targetCell, (BoxCapabilities)boxCapabilityValue),
            });
            var snapshot = worldState.CreateSnapshot();

            var evaluation = RuntimeSettlementLegalityPolicy.EvaluateJumpCrushLandingCell(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, 40, EntityType.Unit),
                    targetCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new JumpLandingEvidence(snapshot, targetCell));

            Assert.That(evaluation.LegalityResult.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(evaluation.CrushedBoxEntityId, Is.Zero);
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

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities)
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
                stateTimer = 0,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                boxCapabilities = capabilities,
                aiMode = EnemyAiMode.None,
                aiStateTimer = 0,
            };
        }
    }
}

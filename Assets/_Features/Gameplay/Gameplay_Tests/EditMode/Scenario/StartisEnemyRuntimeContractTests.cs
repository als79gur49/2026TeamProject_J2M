using System;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class StartisEnemyRuntimeContractTests
    {
        private const int PlayerId = 10;
        private const int EnemyId = 40;

        [Test]
        [Category("Extended")]
        public void Startis_AppliesPassiveContactDamage_OnSameSurfaceCell()
        {
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking(includePassiveContact: true);
            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, cell, UnitRole.Player),
                        CreateUnit(EnemyId, 2, cell, UnitRole.Enemy, EnemyAiMode.Patrol),
                    });
                var tick = CreatePipeline(worldState, profile).RunTick(new TickInput(1));

                var damage = tick.AttackPhaseResult.DamageResolutions.Single();
                Assert.That(damage.SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
                Assert.That(damage.TargetId, Is.EqualTo(PlayerId));
                Assert.That(damage.Accepted, Is.True);
                Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(2));
                AssertActionInactiveOrMissing(worldState);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Startis_PassiveContactDoesNotRequireEnemyActionState()
        {
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking(includePassiveContact: true);
            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, cell, UnitRole.Player),
                        CreateUnit(EnemyId, 2, cell, UnitRole.Enemy, EnemyAiMode.Patrol),
                    });
                var tick = CreatePipeline(worldState, profile).RunTick(new TickInput(1));

                AssertActionInactiveOrMissing(worldState);
                Assert.That(tick.AttackPhaseResult.DamageResolutions.Any(record => record.SourceKind == AttackSourceKind.PassiveContact), Is.True);
                Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(2));
                Assert.That(GetEntity(worldState, EnemyId).aiMode, Is.Not.EqualTo(EnemyAiMode.Attack));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Startis_DoesNotApplyPassiveContactAcrossFaces_WithSamePlanarCell()
        {
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking(includePassiveContact: true);
            try
            {
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 0, 0), UnitRole.Player),
                        CreateUnit(EnemyId, 2, new SurfaceCell(FaceId.Front, 0, 0), UnitRole.Enemy, EnemyAiMode.Patrol),
                    });
                var tick = CreatePipeline(worldState, profile).RunTick(new TickInput(1));

                Assert.That(tick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(3));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Startis_DoesNotMove_WhenOffBottomFace()
        {
            var profile = CreateForwardPassiveContactProfile();
            try
            {
                var offBottomCell = new SurfaceCell(FaceId.Ceiling, 0, 0);
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, offBottomCell, UnitRole.Player),
                        CreateUnit(EnemyId, 2, offBottomCell, UnitRole.Enemy, EnemyAiMode.Patrol),
                    },
                    topology: new CubeTopologyState(FaceId.Floor));
                var tick = CreatePipeline(worldState, profile).RunTick(new TickInput(1));

                Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
                Assert.That(tick.AttackPhaseResult.DamageResolutions, Is.Empty, "PassiveContact is suspended with enemy participation when the enemy is off-bottom.");
                Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(offBottomCell));
                Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(3));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Startis_ResumesMovementAndContact_WhenReturnedToBottomFace()
        {
            var profile = CreateForwardPassiveContactProfile();
            try
            {
                var cell = new SurfaceCell(FaceId.Ceiling, 0, 0);
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, cell, UnitRole.Player),
                        CreateUnit(EnemyId, 2, cell, UnitRole.Enemy, EnemyAiMode.Patrol),
                    },
                    topology: new CubeTopologyState(FaceId.Floor));
                var pipeline = CreatePipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Ceiling));
                var resumedTick = pipeline.RunTick(new TickInput(2));

                Assert.That(resumedTick.AttackPhaseResult.DamageResolutions.Any(record => record.SourceKind == AttackSourceKind.PassiveContact), Is.True);
                Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(2));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Startis_RespectsFaceAwareTerrainBlocker()
        {
            var floorDestination = new SurfaceCell(FaceId.Floor, 1, 0);
            var frontSamePlanar = new SurfaceCell(FaceId.Front, 1, 0);
            var floorBlockedWorld = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 2, 0), UnitRole.Player),
                    CreateUnit(EnemyId, 2, new SurfaceCell(FaceId.Floor, 0, 0), UnitRole.Enemy, EnemyAiMode.Chase),
                },
                terrainData: new GameplayTerrainData(
                    new[]
                    {
                        new TerrainCellState(floorDestination, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
                    }));
            Assert.That(floorBlockedWorld.CreateSnapshot().IsTerrainBlockedForUnit(floorDestination), Is.True);
            Assert.That(floorBlockedWorld.CreateSnapshot().IsTerrainBlockedForUnit(frontSamePlanar), Is.False);

            var frontBlockedWorld = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 2, 0), UnitRole.Player),
                    CreateUnit(EnemyId, 2, new SurfaceCell(FaceId.Floor, 0, 0), UnitRole.Enemy, EnemyAiMode.Chase),
                },
                terrainData: new GameplayTerrainData(
                    new[]
                    {
                        new TerrainCellState(frontSamePlanar, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
                    }));
            Assert.That(frontBlockedWorld.CreateSnapshot().IsTerrainBlockedForUnit(floorDestination), Is.False);
            Assert.That(frontBlockedWorld.CreateSnapshot().IsTerrainBlockedForUnit(frontSamePlanar), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Startis_DoesNotFlattenTerrainOrOccupancyAcrossFaces()
        {
            var profile = CreateForwardPassiveContactProfile();
            try
            {
                var terrain = new GameplayTerrainData(
                    new[]
                    {
                        new TerrainCellState(new SurfaceCell(FaceId.Front, 1, 0), TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
                    });
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 2, 0), UnitRole.Player),
                        CreateUnit(EnemyId, 2, new SurfaceCell(FaceId.Floor, 0, 0), UnitRole.Enemy, EnemyAiMode.Chase),
                        CreateUnit(50, 1, new SurfaceCell(FaceId.Front, 1, 0), UnitRole.Player),
                        CreateUnit(60, 0, new SurfaceCell(FaceId.Front, 2, 0), UnitRole.None, type: EntityType.Box),
                        CreateUnit(70, 1, new SurfaceCell(FaceId.Front, 3, 0), UnitRole.None, type: EntityType.Projectile),
                    },
                    terrainData: terrain);
                var floorDestination = new SurfaceCell(FaceId.Floor, 1, 0);
                var snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.IsTerrainBlockedForUnit(floorDestination), Is.False);
                Assert.That(snapshot.TryGetPlacementBlocker(EntityType.Unit, floorDestination, ignoredEntityId: EnemyId, out _), Is.False);

                CreatePipeline(worldState, profile).RunTick(new TickInput(1));

                Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(GetEntity(worldState, 50).position, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
                Assert.That(GetEntity(worldState, 60).position, Is.EqualTo(new SurfaceCell(FaceId.Front, 2, 0)));
                Assert.That(GetEntity(worldState, 70).position, Is.EqualTo(new SurfaceCell(FaceId.Front, 3, 0)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [TestCase("Dead")]
        [TestCase("MarkedForDeath")]
        [TestCase("Detached")]
        [Category("Extended")]
        public void Startis_ContactIgnoresInvalidReceiver_DeadMarkedOrNonOccupying(string invalidReceiverKind)
        {
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking(includePassiveContact: true);
            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);
                var receiver = CreateUnit(PlayerId, 1, cell, UnitRole.Player);
                if (invalidReceiverKind == "Dead")
                {
                    receiver.hp = 0;
                    receiver.maxHp = 3;
                }
                else if (invalidReceiverKind == "MarkedForDeath")
                {
                    receiver.markedForDeath = true;
                }
                else if (invalidReceiverKind == "Detached")
                {
                    receiver.boardPresence = EntityBoardPresence.Detached;
                }

                var worldState = CreateWorldState(
                    new[]
                    {
                        receiver,
                        CreateUnit(EnemyId, 2, cell, UnitRole.Enemy, EnemyAiMode.Patrol),
                    });
                var tick = CreatePipeline(worldState, profile).RunTick(new TickInput(1));

                Assert.That(
                    tick.AttackPhaseResult.DamageResolutions.Where(record => record.TargetId == PlayerId && record.Accepted),
                    Is.Empty);
                if (worldState.CreateSnapshot().TryGetEntity(PlayerId, out var remainingReceiver))
                {
                    Assert.That(remainingReceiver.hp, Is.EqualTo(receiver.hp));
                }
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static EnemyAiProfile CreateForwardPassiveContactProfile()
        {
            return EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                PatrolStrategyKind = PatrolStrategyKind.Forward,
                PatrolSettings = new PatrolSettings(PatrolBlockedMovementResponse.Stop),
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = true,
            });
        }

        private static TickPipeline CreatePipeline(WorldState worldState, EnemyAiProfile profile)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new EnemyLogic(EnemyId, profile),
                });
        }

        private static void RunTicks(TickPipeline pipeline, int startTick, int endTickInclusive)
        {
            for (var tickIndex = startTick; tickIndex <= endTickInclusive; tickIndex++)
            {
                pipeline.RunTick(new TickInput(tickIndex));
            }
        }

        private static WorldState CreateWorldState(
            EntityState[] initialEntities,
            GameplayTerrainData terrainData = null,
            CubeTopologyState? topology = null)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(4, 4)),
                terrainData ?? GameplayTerrainData.Empty,
                topology ?? new CubeTopologyState(FaceId.Floor));
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell cell,
            UnitRole unitRole,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Right,
            EntityType type = EntityType.Unit)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = type,
                unitRole = unitRole,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
            };
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static void AssertActionInactiveOrMissing(WorldState worldState)
        {
            if (!worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action))
            {
                return;
            }

            Assert.That(action.IsActive, Is.False, "PassiveContact must not require an active EnemyActionRuntimeState.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
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
        public void Startis_AppliesPassiveContact_OnSameSurfaceCell()
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

        [Test]
        [Category("Extended")]
        public void Startis_ForwardMovement_ActivatedBarricadeBlocksOwnCell()
        {
            var profile = CreateForwardPassiveContactProfile();
            try
            {
                var source = new SurfaceCell(FaceId.Floor, 0, 0);
                var destination = new SurfaceCell(FaceId.Floor, 1, 0);
                var barricade = CreateTileFeature(100, destination, TileFeatureKind.Barricade);
                var definitions = CreateActiveDefinitions(barricade);
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 3, 0), UnitRole.Player),
                        CreateUnit(EnemyId, 2, source, UnitRole.Enemy, EnemyAiMode.Patrol),
                    },
                    initialTileFeatures: new[] { barricade });

                var tick = CreatePipeline(worldState, profile, definitions).RunTick(new TickInput(1));

                Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(source));
                Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
                Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(destination, out _), Is.False);
                Assert.That(CountUnitsAt(worldState.CreateSnapshot(), destination, EnemyId), Is.Zero);
                Assert.That(
                    EnemyMovementStrategyShared.CanTraverseStep(
                        worldState.CreateSnapshot(),
                        GetEntity(worldState, EnemyId),
                        destination.PlanarPosition - source.PlanarPosition,
                        definitions),
                    Is.False);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Startis_ForwardMovement_InactiveBarricadeDoesNotBlock()
        {
            var profile = CreateForwardPassiveContactProfile();
            try
            {
                var source = new SurfaceCell(FaceId.Floor, 0, 0);
                var destination = new SurfaceCell(FaceId.Floor, 1, 0);
                var barricade = CreateTileFeature(101, destination, TileFeatureKind.Barricade);
                var definitions = CreateInactiveDefinitions(barricade);
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 3, 0), UnitRole.Player),
                        CreateUnit(EnemyId, 2, source, UnitRole.Enemy, EnemyAiMode.Patrol),
                    },
                    initialTileFeatures: new[] { barricade });

                var canTraverseInactiveBarricade = EnemyMovementStrategyShared.CanTraverseStep(
                    worldState.CreateSnapshot(),
                    GetEntity(worldState, EnemyId),
                    destination.PlanarPosition - source.PlanarPosition,
                    definitions);

                var tick = CreatePipeline(worldState, profile, definitions).RunTick(new TickInput(1));

                Assert.That(
                    canTraverseInactiveBarricade,
                    Is.True,
                    "Inactive Barricade must not make ordinary unit traversal return false.");
                Assert.That(
                    tick.MovementPhaseResult.RawIntents.Any(intent =>
                        intent.SourceId == EnemyId &&
                        intent.Destination == destination.PlanarPosition),
                    Is.True);
                Assert.That(
                    tick.MovementPhaseResult.RejectedReasons.Any(reason => reason.Contains("TileFeature", StringComparison.Ordinal)),
                    Is.False);
                Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(destination, out _), Is.False);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Startis_ForwardMovement_ActivatedDestroyTileDoesNotHardBlock()
        {
            var profile = CreateForwardPassiveContactProfile();
            try
            {
                var source = new SurfaceCell(FaceId.Floor, 0, 0);
                var destination = new SurfaceCell(FaceId.Floor, 1, 0);
                var destroyTile = CreateTileFeature(102, destination, TileFeatureKind.Destroy);
                var definitions = CreateActiveDefinitions(destroyTile);
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 3, 0), UnitRole.Player),
                        CreateUnit(EnemyId, 2, source, UnitRole.Enemy, EnemyAiMode.Patrol),
                    },
                    initialTileFeatures: new[] { destroyTile });
                var canTraverseDestroyTile = EnemyMovementStrategyShared.CanTraverseStep(
                    worldState.CreateSnapshot(),
                    GetEntity(worldState, EnemyId),
                    destination.PlanarPosition - source.PlanarPosition,
                    definitions);

                var tick = CreatePipeline(worldState, profile, definitions).RunTick(new TickInput(1));

                Assert.That(
                    canTraverseDestroyTile,
                    Is.True,
                    "DestroyTile must not make ordinary unit traversal return false as a hard TileFeature blocker.");
                Assert.That(
                    tick.MovementPhaseResult.RejectedReasons.Any(reason => reason.Contains("TileFeature", StringComparison.Ordinal)),
                    Is.False);
                Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(destination, out _), Is.False);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Startis_ForwardMovement_SamePlanarOtherFaceActivatedBarricadeIgnored()
        {
            var profile = CreateForwardPassiveContactProfile();
            try
            {
                var source = new SurfaceCell(FaceId.Floor, 0, 0);
                var destination = new SurfaceCell(FaceId.Floor, 1, 0);
                var otherFaceBarricade = CreateTileFeature(103, new SurfaceCell(FaceId.Front, 1, 0), TileFeatureKind.Barricade);
                var definitions = CreateActiveDefinitions(otherFaceBarricade);
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 3, 0), UnitRole.Player),
                        CreateUnit(EnemyId, 2, source, UnitRole.Enemy, EnemyAiMode.Patrol),
                    },
                    initialTileFeatures: new[] { otherFaceBarricade });

                var canTraverseFloorDestination = EnemyMovementStrategyShared.CanTraverseStep(
                    worldState.CreateSnapshot(),
                    GetEntity(worldState, EnemyId),
                    destination.PlanarPosition - source.PlanarPosition,
                    definitions);

                var tick = CreatePipeline(worldState, profile, definitions).RunTick(new TickInput(1));

                Assert.That(
                    canTraverseFloorDestination,
                    Is.True,
                    "Same planar other-face Barricade must not make Floor traversal return false.");
                Assert.That(
                    tick.MovementPhaseResult.RawIntents.Any(intent =>
                        intent.SourceId == EnemyId &&
                        intent.Destination == destination.PlanarPosition),
                    Is.True);
                Assert.That(
                    tick.MovementPhaseResult.RejectedReasons.Any(reason => reason.Contains("TileFeature", StringComparison.Ordinal)),
                    Is.False);
                Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(destination, out _), Is.False);
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
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = true,
            });
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            EnemyAiProfile profile,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);

            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                playerTiming,
                tileFeatureDefinitions: tileFeatureDefinitions);
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
            CubeTopologyState? topology = null,
            IEnumerable<TileFeatureState> initialTileFeatures = null)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(4, 4)),
                terrainData ?? GameplayTerrainData.Empty,
                topology ?? new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                initialTileFeatures);
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

        private static int CountUnitsAt(WorldSnapshot snapshot, SurfaceCell cell, int entityId)
        {
            var units = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, units);
            return units.Count(unit => unit.entityId == entityId);
        }

        private static void RunUntilEntityAt(
            TickPipeline pipeline,
            WorldState worldState,
            int startTick,
            SurfaceCell destination)
        {
            for (var tick = startTick; tick < startTick + 32; tick++)
            {
                pipeline.RunTick(new TickInput(tick));
                if (GetEntity(worldState, EnemyId).position == destination)
                {
                    return;
                }
            }

            Assert.Fail($"Startis did not reach {destination}.");
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition[] CreateActiveDefinitions(params TileFeatureState[] tileFeatures)
        {
            return CreateDefinitions(TileFeatureActivationRule.Always, tileFeatures);
        }

        private static TileFeatureRuntimeDefinition[] CreateInactiveDefinitions(params TileFeatureState[] tileFeatures)
        {
            return CreateDefinitions(TileFeatureActivationRule.InactiveFaceOnly, tileFeatures);
        }

        private static TileFeatureRuntimeDefinition[] CreateDefinitions(
            TileFeatureActivationRule activationRule,
            params TileFeatureState[] tileFeatures)
        {
            return tileFeatures
                .Select(tileFeature => new TileFeatureRuntimeDefinition(
                    tileFeature.TileId,
                    activationRule,
                    Direction2D.None,
                    TileFeatureBoxSelector.None,
                    boundEntityId: tileFeature.Charges,
                    presentationKey: string.Empty))
                .ToArray();
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

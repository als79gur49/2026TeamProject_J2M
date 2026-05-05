using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureRuntimeStateTests
    {
        private static readonly BoardBounds TestBounds = new(
            Vector2Int.zero,
            new Vector2Int(4, 4));

        [Test]
        [Category("Core")]
        public void WorldState_InitialTileFeatures_ArePreservedInSnapshot()
        {
            var tileFeature = CreateTileFeature(
                tileId: 10,
                cell: new SurfaceCell(FaceId.Front, 2, 3),
                kind: TileFeatureKind.Button,
                sourceEntityId: 20,
                ownerEntityId: 30,
                teamId: 2,
                lifetimeTicks: 40,
                charges: 5);

            var snapshot = CreateWorldState(Array.Empty<EntityState>(), new[] { tileFeature }).CreateSnapshot();

            Assert.That(snapshot.TryGetTileFeature(10, out var storedFeature), Is.True);
            Assert.That(storedFeature, Is.EqualTo(tileFeature));
        }

        [Test]
        [Category("Core")]
        public void WorldSnapshot_EnumerateTileFeaturesAt_OrdersByTileId()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var snapshot = CreateWorldState(
                Array.Empty<EntityState>(),
                new[]
                {
                    CreateTileFeature(tileId: 30, cell: cell, kind: TileFeatureKind.Slide),
                    CreateTileFeature(tileId: 10, cell: cell, kind: TileFeatureKind.Destroy),
                    CreateTileFeature(tileId: 20, cell: new SurfaceCell(FaceId.Floor, 2, 1), kind: TileFeatureKind.Exit),
                }).CreateSnapshot();
            var tileFeatures = new List<TileFeatureState>();

            snapshot.EnumerateTileFeaturesAt(cell, tileFeatures);

            CollectionAssert.AreEqual(new[] { 10, 30 }, tileFeatures.Select(feature => feature.TileId).ToArray());
        }

        [Test]
        [Category("Core")]
        public void WorldSnapshot_EnumerateTileFeaturesOrdered_UsesFaceXYThenTileId()
        {
            var snapshot = CreateWorldState(
                Array.Empty<EntityState>(),
                new[]
                {
                    CreateTileFeature(tileId: 40, cell: new SurfaceCell(FaceId.Front, 0, 0), kind: TileFeatureKind.Exit),
                    CreateTileFeature(tileId: 30, cell: new SurfaceCell(FaceId.Floor, 1, 0), kind: TileFeatureKind.Barricade),
                    CreateTileFeature(tileId: 20, cell: new SurfaceCell(FaceId.Floor, 0, 1), kind: TileFeatureKind.Destroy),
                    CreateTileFeature(tileId: 10, cell: new SurfaceCell(FaceId.Floor, 0, 0), kind: TileFeatureKind.Slide),
                    CreateTileFeature(tileId: 50, cell: new SurfaceCell(FaceId.Floor, 0, 0), kind: TileFeatureKind.Button),
                }).CreateSnapshot();
            var tileFeatures = new List<TileFeatureState>();

            snapshot.EnumerateTileFeaturesOrdered(tileFeatures);

            CollectionAssert.AreEqual(
                new[] { 10, 50, 20, 30, 40 },
                tileFeatures.Select(feature => feature.TileId).ToArray());
        }

        [Test]
        [Category("Core")]
        public void TileFeatureLayer_DoesNotPopulateOccupancyLayers()
        {
            var snapshot = CreateWorldState(
                Array.Empty<EntityState>(),
                new[] { CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Exit) })
                .CreateSnapshot();
            var occupancy = new List<SnapshotOccupancyEntry>();

            snapshot.EnumerateUnitOccupancyOrdered(occupancy);
            Assert.That(occupancy, Is.Empty);
            snapshot.EnumerateSolidOccupancyOrdered(occupancy);
            Assert.That(occupancy, Is.Empty);
            snapshot.EnumerateProjectileOccupancyOrdered(occupancy);
            Assert.That(occupancy, Is.Empty);
            Assert.That(snapshot.TryGetTileFeature(10, out _), Is.True);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureLayer_AllowsBoxUnitAndProjectileSameCell()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            AssertOccupantAndTileFeatureCanShareCell(CreateBox(10, cell), cell);
            AssertOccupantAndTileFeatureCanShareCell(CreateUnit(20, cell), cell);
            AssertOccupantAndTileFeatureCanShareCell(CreateProjectile(30, cell), cell);
        }

        [Test]
        [Category("Core")]
        public void WorldState_RejectsDuplicateTileId()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => CreateWorldState(
                    Array.Empty<EntityState>(),
                    new[]
                    {
                        CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button),
                        CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 2, 1), TileFeatureKind.Exit),
                    }));

            StringAssert.Contains("Duplicate TileFeature id 10", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void WorldState_RejectsNonPositiveTileId()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => CreateWorldState(
                    Array.Empty<EntityState>(),
                    new[] { CreateTileFeature(0, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button) }));

            StringAssert.Contains("must be positive", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void WorldState_RejectsTileFeatureOutsideBoardBounds()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => CreateWorldState(
                    Array.Empty<EntityState>(),
                    new[] { CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 5, 1), TileFeatureKind.Exit) }));

            StringAssert.Contains("outside the configured board bounds", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_IncludesEmptyTileFeaturesSection()
        {
            var snapshot = CreateWorldState(Array.Empty<EntityState>(), Array.Empty<TileFeatureState>()).CreateSnapshot();
            var dump = BuildCanonicalDump(snapshot);

            StringAssert.Contains("TileFeatures\n<empty>\nEntities", dump);
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_TileFeaturesAreOrderedByCellThenTileId()
        {
            var firstSnapshot = CreateWorldState(
                Array.Empty<EntityState>(),
                new[]
                {
                    CreateTileFeature(30, new SurfaceCell(FaceId.Floor, 1, 0), TileFeatureKind.Barricade),
                    CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Slide),
                    CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Destroy),
                }).CreateSnapshot();
            var secondSnapshot = CreateWorldState(
                Array.Empty<EntityState>(),
                new[]
                {
                    CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Destroy),
                    CreateTileFeature(30, new SurfaceCell(FaceId.Floor, 1, 0), TileFeatureKind.Barricade),
                    CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Slide),
                }).CreateSnapshot();
            var hashBuilder = new DeterminismHashBuilder();

            Assert.That(
                hashBuilder.Build(3, firstSnapshot, CreateTickResultData(firstSnapshot)),
                Is.EqualTo(hashBuilder.Build(3, secondSnapshot, CreateTickResultData(secondSnapshot))));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_TileFeatureFieldsAffectHash()
        {
            var baselineSnapshot = CreateWorldState(
                Array.Empty<EntityState>(),
                new[] { CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Button, charges: 1) })
                .CreateSnapshot();
            var changedSnapshot = CreateWorldState(
                Array.Empty<EntityState>(),
                new[] { CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Button, charges: 2) })
                .CreateSnapshot();
            var hashBuilder = new DeterminismHashBuilder();

            Assert.That(
                hashBuilder.Build(3, baselineSnapshot, CreateTickResultData(baselineSnapshot)),
                Is.Not.EqualTo(hashBuilder.Build(3, changedSnapshot, CreateTickResultData(changedSnapshot))));
        }

        private static void AssertOccupantAndTileFeatureCanShareCell(EntityState occupant, SurfaceCell cell)
        {
            var snapshot = CreateWorldState(
                new[] { occupant },
                new[] { CreateTileFeature(100 + occupant.entityId, cell, TileFeatureKind.Button) })
                .CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(occupant.entityId, out var storedOccupant), Is.True);
            Assert.That(storedOccupant.position, Is.EqualTo(cell));
            Assert.That(snapshot.TryGetTileFeature(100 + occupant.entityId, out var tileFeature), Is.True);
            Assert.That(tileFeature.Cell, Is.EqualTo(cell));
        }

        private static string BuildCanonicalDump(WorldSnapshot snapshot)
        {
            var method = typeof(DeterminismHashBuilder).GetMethod(
                "BuildCanonicalDump",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { 3, snapshot, CreateTickResultData(snapshot) });
        }

        private static TickResultData CreateTickResultData(WorldSnapshot snapshot)
        {
            var finalEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(finalEntities);
            return new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>());
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            IEnumerable<TileFeatureState> initialTileFeatures)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                TestBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                initialTileFeatures);
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            int sourceEntityId = 0,
            int ownerEntityId = 0,
            int teamId = 0,
            int lifetimeTicks = 0,
            int charges = 0)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId,
                ownerEntityId,
                teamId,
                lifetimeTicks,
                charges);
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
            };
        }

        private static EntityState CreateProjectile(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Projectile,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
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
                facing = Direction.Right,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            };
        }
    }
}

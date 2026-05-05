using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class ProjectedWorldTileFeatureOperationTests
    {
        private static readonly BoardBounds TestBounds = new(
            Vector2Int.zero,
            new Vector2Int(4, 4));

        [Test]
        [Category("Core")]
        public void ProjectedWorld_PreservesExistingTileFeatures()
        {
            var tileFeature = CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button);
            var projectedSnapshot = new ProjectedWorld(CreateSnapshot(new[] { tileFeature })).CreateSnapshot();

            Assert.That(projectedSnapshot.TryGetTileFeature(10, out var projectedFeature), Is.True);
            Assert.That(projectedFeature, Is.EqualTo(tileFeature));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_AddTileFeature_AppearsInProjectedSnapshot()
        {
            var addedFeature = CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 2, 1), TileFeatureKind.Slide);
            var projectedSnapshot = ProjectTileFeatures(
                CreateSnapshot(Array.Empty<TileFeatureState>()),
                TileFeatureOperation.Add(addedFeature));

            Assert.That(projectedSnapshot.TryGetTileFeature(20, out var projectedFeature), Is.True);
            Assert.That(projectedFeature, Is.EqualTo(addedFeature));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_UpdateTileFeature_ReplacesState()
        {
            var original = CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button, charges: 1);
            var updated = CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button, charges: 2);
            var projectedSnapshot = ProjectTileFeatures(
                CreateSnapshot(new[] { original }),
                TileFeatureOperation.Update(updated));

            Assert.That(projectedSnapshot.TryGetTileFeature(10, out var projectedFeature), Is.True);
            Assert.That(projectedFeature, Is.EqualTo(updated));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_UpdateTileFeatureChangedCell_UpdatesCellEnumerationIndex()
        {
            var oldCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var newCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var original = CreateTileFeature(10, oldCell, TileFeatureKind.Button);
            var updated = CreateTileFeature(10, newCell, TileFeatureKind.Button);
            var projectedSnapshot = ProjectTileFeatures(
                CreateSnapshot(new[] { original }),
                TileFeatureOperation.Update(updated));
            var tileFeatures = new List<TileFeatureState>();

            projectedSnapshot.EnumerateTileFeaturesAt(oldCell, tileFeatures);
            Assert.That(tileFeatures, Is.Empty);

            projectedSnapshot.EnumerateTileFeaturesAt(newCell, tileFeatures);
            CollectionAssert.AreEqual(new[] { 10 }, tileFeatures.Select(feature => feature.TileId).ToArray());
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_RemoveTileFeature_RemovesIdLookupAndCellLookup()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var projectedSnapshot = ProjectTileFeatures(
                CreateSnapshot(new[] { CreateTileFeature(10, cell, TileFeatureKind.Button) }),
                TileFeatureOperation.Remove(10));
            var tileFeatures = new List<TileFeatureState>();

            Assert.That(projectedSnapshot.TryGetTileFeature(10, out _), Is.False);
            projectedSnapshot.EnumerateTileFeaturesAt(cell, tileFeatures);
            Assert.That(tileFeatures, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_TileFeatureOperation_DoesNotAffectOccupancyLayers()
        {
            var unitCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var boxCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var projectileCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(10, unitCell),
                    CreateBox(20, boxCell),
                    CreateProjectile(30, projectileCell),
                },
                Array.Empty<TileFeatureState>());
            var projectedSnapshot = ProjectTileFeatures(
                worldState.CreateSnapshot(),
                TileFeatureOperation.Add(CreateTileFeature(100, unitCell, TileFeatureKind.Button)));

            Assert.That(projectedSnapshot.HasAnyUnitAt(unitCell), Is.True);
            Assert.That(projectedSnapshot.TryGetSolidOccupantAt(boxCell, out var box), Is.True);
            Assert.That(box.entityId, Is.EqualTo(20));
            Assert.That(projectedSnapshot.TryGetProjectileAt(projectileCell, out var projectile), Is.True);
            Assert.That(projectile.entityId, Is.EqualTo(30));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_TileFeatureOperation_DoesNotAffectTerrain()
        {
            var terrainCell = new TerrainCellState(
                new SurfaceCell(FaceId.Floor, 1, 1),
                TerrainKind.Generic,
                TerrainFlags.BlocksGroundTraversal);
            var terrainData = new GameplayTerrainData(new[] { terrainCell });
            var baseSnapshot = CreateWorldState(
                Array.Empty<EntityState>(),
                Array.Empty<TileFeatureState>(),
                terrainData)
                .CreateSnapshot();
            var projectedSnapshot = ProjectTileFeatures(
                baseSnapshot,
                TileFeatureOperation.Add(CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 2, 1), TileFeatureKind.Button)));

            Assert.That(projectedSnapshot.TryGetTerrain(terrainCell.Cell, out var projectedTerrain), Is.True);
            Assert.That(projectedTerrain, Is.EqualTo(terrainCell));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_AddExistingTileId_Rejects()
        {
            var projectedWorld = new ProjectedWorld(
                CreateSnapshot(new[] { CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button) }));

            var exception = Assert.Throws<InvalidOperationException>(
                () => projectedWorld.ApplyTileFeatureOperations(CreateBatch(
                    TileFeatureOperation.Add(CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 2, 1), TileFeatureKind.Slide)))));

            StringAssert.Contains("Cannot add existing TileFeature id 10", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_UpdateMissingTileId_Rejects()
        {
            var projectedWorld = new ProjectedWorld(CreateSnapshot(Array.Empty<TileFeatureState>()));

            var exception = Assert.Throws<InvalidOperationException>(
                () => projectedWorld.ApplyTileFeatureOperations(CreateBatch(
                    TileFeatureOperation.Update(CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button)))));

            StringAssert.Contains("Cannot update missing TileFeature id 10", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_RemoveMissingTileId_Rejects()
        {
            var projectedWorld = new ProjectedWorld(CreateSnapshot(Array.Empty<TileFeatureState>()));

            var exception = Assert.Throws<InvalidOperationException>(
                () => projectedWorld.ApplyTileFeatureOperations(CreateBatch(TileFeatureOperation.Remove(10))));

            StringAssert.Contains("Cannot remove missing TileFeature id 10", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_DuplicateTileIdOperationInSameBatch_Rejects()
        {
            var projectedWorld = new ProjectedWorld(CreateSnapshot(Array.Empty<TileFeatureState>()));

            var exception = Assert.Throws<InvalidOperationException>(
                () => projectedWorld.ApplyTileFeatureOperations(CreateBatch(
                    TileFeatureOperation.Add(CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button)),
                    TileFeatureOperation.Add(CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 2, 1), TileFeatureKind.Slide)))));

            StringAssert.Contains("Duplicate TileFeature operation for TileId 10", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_OutOfBoundsAddOrUpdate_Rejects()
        {
            var addWorld = new ProjectedWorld(CreateSnapshot(Array.Empty<TileFeatureState>()));
            var addException = Assert.Throws<InvalidOperationException>(
                () => addWorld.ApplyTileFeatureOperations(CreateBatch(
                    TileFeatureOperation.Add(CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 5, 1), TileFeatureKind.Button)))));

            var updateWorld = new ProjectedWorld(
                CreateSnapshot(new[] { CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button) }));
            var updateException = Assert.Throws<InvalidOperationException>(
                () => updateWorld.ApplyTileFeatureOperations(CreateBatch(
                    TileFeatureOperation.Update(CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 5, 1), TileFeatureKind.Button)))));

            StringAssert.Contains("outside the configured board bounds", addException.Message);
            StringAssert.Contains("outside the configured board bounds", updateException.Message);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_NonPositiveTileId_Rejects()
        {
            var projectedWorld = new ProjectedWorld(CreateSnapshot(Array.Empty<TileFeatureState>()));

            var exception = Assert.Throws<InvalidOperationException>(
                () => projectedWorld.ApplyTileFeatureOperations(CreateBatch(TileFeatureOperation.Remove(0))));

            StringAssert.Contains("must be positive", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_AddOrUpdateTileIdMismatch_Rejects()
        {
            var addWorld = new ProjectedWorld(CreateSnapshot(Array.Empty<TileFeatureState>()));
            var addException = Assert.Throws<InvalidOperationException>(
                () => addWorld.ApplyTileFeatureOperations(CreateBatch(
                    TileFeatureOperation.CreateUnchecked(
                        TileFeatureOperationKind.Add,
                        10,
                        CreateTileFeature(11, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button)))));

            var updateWorld = new ProjectedWorld(
                CreateSnapshot(new[] { CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button) }));
            var updateException = Assert.Throws<InvalidOperationException>(
                () => updateWorld.ApplyTileFeatureOperations(CreateBatch(
                    TileFeatureOperation.CreateUnchecked(
                        TileFeatureOperationKind.Update,
                        20,
                        CreateTileFeature(21, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button)))));

            StringAssert.Contains("must match state TileId", addException.Message);
            StringAssert.Contains("must match state TileId", updateException.Message);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_TileFeatureOrderedEnumeration_RemainsDeterministic()
        {
            var projectedSnapshot = ProjectTileFeatures(
                CreateSnapshot(new[]
                {
                    CreateTileFeature(40, new SurfaceCell(FaceId.Front, 0, 0), TileFeatureKind.Exit),
                    CreateTileFeature(30, new SurfaceCell(FaceId.Floor, 1, 0), TileFeatureKind.Barricade),
                }),
                TileFeatureOperation.Add(CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 0, 1), TileFeatureKind.Destroy)),
                TileFeatureOperation.Add(CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Slide)),
                TileFeatureOperation.Add(CreateTileFeature(50, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Button)));
            var tileFeatures = new List<TileFeatureState>();

            projectedSnapshot.EnumerateTileFeaturesOrdered(tileFeatures);

            CollectionAssert.AreEqual(
                new[] { 10, 50, 20, 30, 40 },
                tileFeatures.Select(feature => feature.TileId).ToArray());
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_TileFeatureHash_ChangesWhenOperationChangesState()
        {
            var baseSnapshot = CreateSnapshot(
                new[] { CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button, charges: 1) });
            var projectedSnapshot = ProjectTileFeatures(
                baseSnapshot,
                TileFeatureOperation.Update(CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button, charges: 2)));
            var hashBuilder = new DeterminismHashBuilder();

            Assert.That(
                hashBuilder.Build(3, baseSnapshot, CreateTickResultData(baseSnapshot)),
                Is.Not.EqualTo(hashBuilder.Build(3, projectedSnapshot, CreateTickResultData(projectedSnapshot))));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_EmptyTileFeatureOperationBatch_DoesNotDirtyProjectedWorld()
        {
            var projectedWorld = new ProjectedWorld(CreateSnapshot(Array.Empty<TileFeatureState>()));
            var emptyBatch = new TileFeatureOperationBatch();

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                var first = projectedWorld.CreateSnapshot();
                projectedWorld.ApplyTileFeatureOperations(emptyBatch);
                var second = projectedWorld.CreateSnapshot();

                Assert.That(second, Is.SameAs(first));
                counts = capture.Counts;
            }

            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Core")]
        public void FinalizationBatch_TileFeatureOperations_MatchProjectedWorldResult()
        {
            var initialTileFeatures = new[]
            {
                CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button, charges: 1),
                CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 2, 1), TileFeatureKind.Destroy),
            };
            var operations = new[]
            {
                TileFeatureOperation.Update(
                    CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 2), TileFeatureKind.Button, charges: 2)),
                TileFeatureOperation.Remove(20),
                TileFeatureOperation.Add(
                    CreateTileFeature(30, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Slide)),
            };
            var baseWorld = CreateWorldState(Array.Empty<EntityState>(), initialTileFeatures);
            var projectedWorld = new ProjectedWorld(baseWorld.CreateSnapshot());
            projectedWorld.ApplyTileFeatureOperations(CreateBatch(operations));
            var projectedSnapshot = projectedWorld.CreateSnapshot();
            var authoritativeWorld = CreateWorldState(Array.Empty<EntityState>(), initialTileFeatures);
            var batch = new FinalizationBatch();
            batch.ApplyTileFeatureOperations(CreateBatch(operations));

            batch.ApplyTo(authoritativeWorld.CreateWriteContext(), delayedAttackEffectSink: null);
            var authoritativeSnapshot = authoritativeWorld.CreateSnapshot();

            CollectionAssert.AreEqual(
                EnumerateTileFeatures(projectedSnapshot),
                EnumerateTileFeatures(authoritativeSnapshot));
        }

        [Test]
        [Category("Core")]
        public void FinalizationBatch_EmptyTileFeatureOperationBatch_DoesNotChangeWorldState()
        {
            var tileFeature = CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button);
            var worldState = CreateWorldState(Array.Empty<EntityState>(), new[] { tileFeature });
            var batch = new FinalizationBatch();

            batch.ApplyTileFeatureOperations(new TileFeatureOperationBatch());
            batch.ApplyTo(worldState.CreateWriteContext(), delayedAttackEffectSink: null);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(batch.TileFeatureOperations, Is.Empty);
            CollectionAssert.AreEqual(new[] { tileFeature }, EnumerateTileFeatures(snapshot));
        }

        private static WorldSnapshot ProjectTileFeatures(
            WorldSnapshot baseSnapshot,
            params TileFeatureOperation[] operations)
        {
            var projectedWorld = new ProjectedWorld(baseSnapshot);
            projectedWorld.ApplyTileFeatureOperations(CreateBatch(operations));
            return projectedWorld.CreateSnapshot();
        }

        private static TileFeatureOperationBatch CreateBatch(params TileFeatureOperation[] operations)
        {
            return new TileFeatureOperationBatch(operations);
        }

        private static TileFeatureState[] EnumerateTileFeatures(WorldSnapshot snapshot)
        {
            var tileFeatures = new List<TileFeatureState>();
            snapshot.EnumerateTileFeaturesOrdered(tileFeatures);
            return tileFeatures.ToArray();
        }

        private static WorldSnapshot CreateSnapshot(IEnumerable<TileFeatureState> initialTileFeatures)
        {
            return CreateWorldState(Array.Empty<EntityState>(), initialTileFeatures).CreateSnapshot();
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            IEnumerable<TileFeatureState> initialTileFeatures,
            GameplayTerrainData terrainData = null)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                TestBounds,
                terrainData ?? GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                initialTileFeatures);
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

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            int charges = 0)
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
                charges: charges);
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

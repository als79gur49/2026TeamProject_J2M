using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
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
        public void WorldWriteContext_AddTileFeature_AppearsInSnapshot()
        {
            var worldState = CreateWorldState(Array.Empty<EntityState>(), Array.Empty<TileFeatureState>());
            var writeContext = worldState.CreateWriteContext();
            var added = CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button);

            writeContext.AddTileFeature(added);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetTileFeature(10, out var stored), Is.True);
            Assert.That(stored, Is.EqualTo(added));
        }

        [Test]
        [Category("Core")]
        public void WorldWriteContext_UpdateTileFeature_ReplacesStateAndCellIndex()
        {
            var oldCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var newCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var original = CreateTileFeature(10, oldCell, TileFeatureKind.Button, charges: 1);
            var updated = CreateTileFeature(10, newCell, TileFeatureKind.Button, charges: 2);
            var worldState = CreateWorldState(Array.Empty<EntityState>(), new[] { original });
            var writeContext = worldState.CreateWriteContext();
            var tileFeatures = new List<TileFeatureState>();

            writeContext.UpdateTileFeature(updated);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetTileFeature(10, out var stored), Is.True);
            Assert.That(stored, Is.EqualTo(updated));
            snapshot.EnumerateTileFeaturesAt(oldCell, tileFeatures);
            Assert.That(tileFeatures, Is.Empty);
            snapshot.EnumerateTileFeaturesAt(newCell, tileFeatures);
            CollectionAssert.AreEqual(new[] { 10 }, tileFeatures.Select(feature => feature.TileId).ToArray());
        }

        [Test]
        [Category("Core")]
        public void WorldWriteContext_RemoveTileFeature_ClearsIdAndCellIndex()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(
                Array.Empty<EntityState>(),
                new[] { CreateTileFeature(10, cell, TileFeatureKind.Button) });
            var writeContext = worldState.CreateWriteContext();
            var tileFeatures = new List<TileFeatureState>();

            writeContext.RemoveTileFeature(10);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetTileFeature(10, out _), Is.False);
            snapshot.EnumerateTileFeaturesAt(cell, tileFeatures);
            Assert.That(tileFeatures, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void WorldWriteContext_TileFeatureMutationRejectsInvalidWrites()
        {
            var worldState = CreateWorldState(
                Array.Empty<EntityState>(),
                new[] { CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button) });
            var writeContext = worldState.CreateWriteContext();

            var duplicateAdd = Assert.Throws<InvalidOperationException>(
                () => writeContext.AddTileFeature(CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 2, 1), TileFeatureKind.Exit)));
            var missingUpdate = Assert.Throws<InvalidOperationException>(
                () => writeContext.UpdateTileFeature(CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 1, 1), TileFeatureKind.Button)));
            var missingRemove = Assert.Throws<InvalidOperationException>(
                () => writeContext.RemoveTileFeature(20));
            var nonPositive = Assert.Throws<InvalidOperationException>(
                () => writeContext.RemoveTileFeature(0));
            var outOfBounds = Assert.Throws<InvalidOperationException>(
                () => writeContext.AddTileFeature(CreateTileFeature(30, new SurfaceCell(FaceId.Floor, 5, 1), TileFeatureKind.Button)));

            StringAssert.Contains("Duplicate TileFeature id 10", duplicateAdd.Message);
            StringAssert.Contains("Cannot update missing TileFeature id 20", missingUpdate.Message);
            StringAssert.Contains("Cannot remove missing TileFeature id 20", missingRemove.Message);
            StringAssert.Contains("must be positive", nonPositive.Message);
            StringAssert.Contains("outside the configured board bounds", outOfBounds.Message);
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
            Assert.That(snapshot.TryGetTileFeature(10, out _), Is.True);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureLayer_AllowsBoxAndUnitSameCell()
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            AssertOccupantAndTileFeatureCanShareCell(CreateBox(10, cell), cell);
            AssertOccupantAndTileFeatureCanShareCell(CreateUnit(20, cell), cell);
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

        [Test]
        [Category("Core")]
        public void TileFeatureActivationQuery_BottomFaceOnly_UsesTopologyBottomFace()
        {
            var state = CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Button);
            var definition = CreateDefinition(10, TileFeatureActivationRule.BottomFaceOnly);

            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, new CubeTopologyState(FaceId.Floor)), Is.True);
            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, new CubeTopologyState(FaceId.Front)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureActivationQuery_FrontFaceOnly_UsesTopologyFrontFace()
        {
            var state = CreateTileFeature(10, new SurfaceCell(FaceId.Front, 0, 0), TileFeatureKind.Button);
            var definition = CreateDefinition(10, TileFeatureActivationRule.FrontFaceOnly);

            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, new CubeTopologyState(FaceId.Floor)), Is.True);
            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, new CubeTopologyState(FaceId.Front)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureActivationQuery_ActiveFaceOnly_UsesTopologyActiveFaces()
        {
            var state = CreateTileFeature(10, new SurfaceCell(FaceId.Front, 0, 0), TileFeatureKind.Button);
            var definition = CreateDefinition(10, TileFeatureActivationRule.ActiveFaceOnly);

            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, new CubeTopologyState(FaceId.Floor)), Is.True);
            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, new CubeTopologyState(FaceId.Ceiling)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureActivationQuery_InactiveFaceOnly_UsesTopologyInactiveFaces()
        {
            var state = CreateTileFeature(10, new SurfaceCell(FaceId.Back, 0, 0), TileFeatureKind.Button);
            var definition = CreateDefinition(10, TileFeatureActivationRule.InactiveFaceOnly);

            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, new CubeTopologyState(FaceId.Floor)), Is.True);
            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, new CubeTopologyState(FaceId.Ceiling)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void InactiveFaceOnlyFeature_ActiveButGeneralGameplayVfxSuppressed()
        {
            var cell = new SurfaceCell(FaceId.Back, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);
            var state = CreateTileFeature(10, cell, TileFeatureKind.Button);
            var definition = CreateDefinition(10, TileFeatureActivationRule.InactiveFaceOnly);
            var request = new GameplayVfxRequest(
                1,
                10,
                10,
                GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop),
                VfxAnchor.ForCell(cell, topology, VfxAnchorSlot.CellCenter),
                VfxTimingKind.ImmediateOnTickPresentation);

            var decision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                request,
                GameplayVfxVisibilityMode.DefaultGameplay,
                default);

            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, topology), Is.True);
            Assert.That(decision.IsVisible, Is.False);
            Assert.That(decision.BlockReason, Is.EqualTo(GameplayVfxVisibilityBlockReason.InactiveFace));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureActivationQuery_TopologyRotation_CanChangeActivationResult()
        {
            var state = CreateTileFeature(10, new SurfaceCell(FaceId.Back, 0, 0), TileFeatureKind.Button);
            var definition = CreateDefinition(10, TileFeatureActivationRule.ActiveFaceOnly);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, topology), Is.False);
            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, topology.Rotate(CubeRotationKind.Backward)), Is.True);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureActivationQuery_TileIdMismatch_ReturnsFalse()
        {
            var state = CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Button);
            var definition = CreateDefinition(20, TileFeatureActivationRule.Always);

            Assert.That(TileFeatureActivationQueries.IsActive(state, definition, new CubeTopologyState(FaceId.Floor)), Is.False);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureActivationQuery_DoesNotMutateWorldState()
        {
            var worldState = CreateWorldState(
                Array.Empty<EntityState>(),
                new[] { CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Button) });
            var beforeSnapshot = worldState.CreateSnapshot();
            Assert.That(beforeSnapshot.TryGetTileFeature(10, out var beforeFeature), Is.True);

            var definition = CreateDefinition(10, TileFeatureActivationRule.Always);
            Assert.That(TileFeatureActivationQueries.IsActive(beforeFeature, definition, beforeSnapshot.Topology), Is.True);

            var afterSnapshot = worldState.CreateSnapshot();
            Assert.That(afterSnapshot.TryGetTileFeature(10, out var afterFeature), Is.True);
            Assert.That(afterFeature, Is.EqualTo(beforeFeature));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureActivationQuery_DoesNotIncreaseSnapshotMaterializationBudget()
        {
            var state = CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Button);
            var definition = CreateDefinition(10, TileFeatureActivationRule.Always);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                Assert.That(TileFeatureActivationQueries.IsActive(state, definition, new CubeTopologyState(FaceId.Floor)), Is.True);
                counts = capture.Counts;
            }

            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(0));
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

        private static TileFeatureRuntimeDefinition CreateDefinition(
            int tileId,
            TileFeatureActivationRule activationRule)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: 0,
                presentationKey: string.Empty);
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

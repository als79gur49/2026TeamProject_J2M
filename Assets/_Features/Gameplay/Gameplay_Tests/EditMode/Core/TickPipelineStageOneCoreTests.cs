using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class TickPipelineStageOneCoreTests
    {
        [Test]
        [Category("Core")]
        public void IdAllocator_ResetForTick_RestartsCategorySequences()
        {
            var allocator = new IdAllocator();

            allocator.ResetForTick(3);

            Assert.That(allocator.AllocateIntentId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateIntentId(), Is.EqualTo(2));
            Assert.That(allocator.AllocateGroupId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateSpawnId(), Is.EqualTo(1));

            allocator.ResetForTick(4);

            Assert.That(allocator.AllocateIntentId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateGroupId(), Is.EqualTo(1));
            Assert.That(allocator.AllocateSpawnId(), Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void TickInputBuffer_RecordRejectsDuplicateTick()
        {
            var inputBuffer = new TickInputBuffer();

            inputBuffer.Record(new TickInput(3));

            Assert.That(
                () => inputBuffer.Record(new TickInput(3)),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        [Category("Core")]
        public void TickInputBuffer_ConsumeOrDefault_ReturnsRecordedInputOrDefaultTick()
        {
            var inputBuffer = new TickInputBuffer();

            inputBuffer.Record(new TickInput(5));

            Assert.That(inputBuffer.HasBufferedInput(5), Is.True);
            Assert.That(inputBuffer.ConsumeOrDefault(5).TickIndex, Is.EqualTo(5));
            Assert.That(inputBuffer.HasBufferedInput(5), Is.False);
            Assert.That(inputBuffer.ConsumeOrDefault(6).TickIndex, Is.EqualTo(6));
        }

        [Test]
        [Category("Core")]
        public void IntentComparer_ProvidesTotalOrder()
        {
            var first = new MoveIntent(1, 10);
            var second = new MoveIntent(1, 10);
            var third = new AttackIntent(1, 10, 20);
            first.AssignIntentId(1);
            second.AssignIntentId(2);
            third.AssignIntentId(3);

            var sortedIntents = new List<Intent>
            {
                third,
                second,
                first,
            };

            sortedIntents.Sort(IntentComparer.Instance);

            CollectionAssert.AreEqual(
                new Intent[] { first, second, third },
                sortedIntents);
        }

        [Test]
        [Category("Core")]
        public void ActionGroupComparer_ProvidesTotalOrder()
        {
            var second = CreateActionGroup(intentId: 10, sourceId: 1, priority: 10, groupId: 2);
            var first = CreateActionGroup(intentId: 10, sourceId: 1, priority: 10, groupId: 1);
            var third = CreateActionGroup(intentId: 1, sourceId: 2, priority: 10, groupId: 1);

            var sortedGroups = new List<ActionGroup>
            {
                third,
                second,
                first,
            };

            sortedGroups.Sort(ActionGroupComparer.Instance);

            CollectionAssert.AreEqual(
                new[] { first, second, third },
                sortedGroups);
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_EmptyFinalizationBatch_DoesNotDirtyProjectedWorld()
        {
            var projectedWorld = new ProjectedWorld(CreateSnapshot(Array.Empty<TileFeatureState>()));
            var emptyBatch = new FinalizationBatch();

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                var first = projectedWorld.CreateSnapshot();
                projectedWorld.ApplyBatch(emptyBatch);
                var second = projectedWorld.CreateSnapshot();

                Assert.That(second, Is.SameAs(first));
                counts = capture.Counts;
            }

            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_EntityFinalizationBatch_DirtiesProjectedWorld()
        {
            var startCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var baseSnapshot = CreateWorldState(
                new[] { CreateUnit(10, startCell) },
                Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var projectedWorld = new ProjectedWorld(baseSnapshot);
            var batch = new FinalizationBatch();
            batch.MoveEntity(10, destination);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                var first = projectedWorld.CreateSnapshot();
                projectedWorld.ApplyBatch(batch);
                var second = projectedWorld.CreateSnapshot();

                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(second.TryGetEntity(10, out var projectedUnit), Is.True);
                Assert.That(projectedUnit.position, Is.EqualTo(destination));
                counts = capture.Counts;
            }

            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(2));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_TileFeatureFinalizationBatch_DirtiesProjectedWorld()
        {
            var projectedWorld = new ProjectedWorld(CreateSnapshot(Array.Empty<TileFeatureState>()));
            var tileFeature = CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 2, 1), TileFeatureKind.Slide);
            var batch = new FinalizationBatch();
            batch.ApplyTileFeatureOperations(new TileFeatureOperationBatch(new[] { TileFeatureOperation.Add(tileFeature) }));

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                var first = projectedWorld.CreateSnapshot();
                projectedWorld.ApplyBatch(batch);
                var second = projectedWorld.CreateSnapshot();

                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(second.TryGetTileFeature(tileFeature.TileId, out var projectedFeature), Is.True);
                Assert.That(projectedFeature, Is.EqualTo(tileFeature));
                counts = capture.Counts;
            }

            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(2));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(0));
        }

        private static ActionGroup CreateActionGroup(int intentId, int sourceId, int priority, int groupId)
        {
            var actionGroup = new ActionGroup(intentId, sourceId, priority, ActionGroupKind.Move);
            actionGroup.AssignGroupId(groupId);
            return actionGroup;
        }

        private static WorldSnapshot CreateSnapshot(IEnumerable<TileFeatureState> initialTileFeatures)
        {
            return CreateWorldState(Array.Empty<EntityState>(), initialTileFeatures).CreateSnapshot();
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            IEnumerable<TileFeatureState> initialTileFeatures)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                new BoardBounds(UnityEngine.Vector2Int.zero, new UnityEngine.Vector2Int(4, 4)),
                TerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                initialTileFeatures);
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
    }
}

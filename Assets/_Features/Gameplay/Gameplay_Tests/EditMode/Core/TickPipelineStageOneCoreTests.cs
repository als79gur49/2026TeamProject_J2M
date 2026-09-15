using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Intents;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

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
        public void ActionGroupComparer_OtherwiseEqualSpawn_RawEntityTypeProvidesStableOrder()
        {
            var legacyWall = CreateActionGroup(intentId: 10, sourceId: 1, priority: 10, groupId: 1);
            var explicitWall = CreateActionGroup(intentId: 10, sourceId: 1, priority: 10, groupId: 1);
            var entity = CreateUnit(40, new SurfaceCell(FaceId.Floor, 1, 0));
            entity.type = EntityType.None;
            legacyWall.Spawns.Add(new SpawnAction(1, entity));
            entity.type = EntityType.Wall;
            explicitWall.Spawns.Add(new SpawnAction(1, entity));

            Assert.That(ActionGroupComparer.Instance.Compare(legacyWall, explicitWall), Is.LessThan(0));
            Assert.That(ActionGroupComparer.Instance.Compare(explicitWall, legacyWall), Is.GreaterThan(0));

            var sorted = new List<ActionGroup> { explicitWall, legacyWall };
            sorted.Sort(ActionGroupComparer.Instance);
            CollectionAssert.AreEqual(new[] { legacyWall, explicitWall }, sorted);
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
        public void ProjectedWorld_SeededBaseSnapshot_ReusesBaseSnapshotUntilBatchApplied()
        {
            var baseSnapshot = CreateSnapshot(Array.Empty<TileFeatureState>());
            var projectedWorld = new ProjectedWorld(baseSnapshot, seedBaseSnapshot: true);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                var first = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanAfterEnemyAi);
                var second = projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanPostPreMovement);

                Assert.That(first, Is.SameAs(baseSnapshot));
                Assert.That(second, Is.SameAs(baseSnapshot));
                counts = capture.Counts;
            }

            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(2));
            Assert.That(counts.GetCacheHitCount(ProjectedWorldSnapshotReason.PlanAfterEnemyAi), Is.EqualTo(1));
            Assert.That(counts.GetCacheHitCount(ProjectedWorldSnapshotReason.PlanPostPreMovement), Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_ReasonedApplyBatch_RecordsSourceCounts()
        {
            var projectedWorld = new ProjectedWorld(CreateSnapshot(Array.Empty<TileFeatureState>()));
            var emptyBatch = new FinalizationBatch();

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                projectedWorld.ApplyBatch(emptyBatch, ProjectedWorldBatchReason.PlanPreMovementState);
                counts = capture.Counts;
            }

            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(1));
            Assert.That(counts.GetApplyBatchCount(ProjectedWorldBatchReason.PlanPreMovementState), Is.EqualTo(1));
            Assert.That(counts.GetEmptyApplyBatchCount(ProjectedWorldBatchReason.PlanPreMovementState), Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void RunResolvePhase_RebuildsAllProjectionsFromMaterializedPlanSnapshot_WithoutPlanPrefixReplay()
        {
            var source = File.ReadAllText(Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"));
            var resolveStart = source.IndexOf(
                "private ResolvePhaseResult RunResolvePhase(",
                StringComparison.Ordinal);
            var finalizeStart = source.IndexOf(
                "private void RunFinalizePhase(",
                resolveStart,
                StringComparison.Ordinal);

            Assert.That(resolveStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(finalizeStart, Is.GreaterThan(resolveStart));
            var resolveSource = source.Substring(resolveStart, finalizeStart - resolveStart);

            Assert.That(
                CountOccurrences(resolveSource, "new ProjectedWorld(planSnapshot)"),
                Is.EqualTo(4),
                "initial, impact rematerialization, jump landing, and Unit tile-effect rebuilds must all seed from the terminal Plan snapshot");
            Assert.That(
                resolveSource,
                Does.Not.Contain("ApplyBatch(planPhaseResult.PlanFinalizationBatch)"),
                "planSnapshot already contains the Plan prefix; Resolve must overlay only Resolve-owned batches");
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

        [Test]
        [Category("Core")]
        public void CompositeDamageProjection_EmptyProjection_ReturnsBaseSnapshotWithoutMaterialization()
        {
            var baseSnapshot = CreateWorldState(
                    new[] { CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();

            SnapshotMaterializationCounts counts;
            WorldSnapshot projectedSnapshot;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                projectedSnapshot = InvokeCompositeDamageProjectionSnapshot(
                    baseSnapshot,
                    Array.Empty<ResolutionRecord>(),
                    new Dictionary<int, AttackActionPlanPayload>());
                counts = capture.Counts;
            }

            Assert.That(projectedSnapshot, Is.SameAs(baseSnapshot));
            Assert.That(counts.CompositeDamageProjectionReturnedBaseSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.CompositeDamageProjectionMaterializedSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.GetMaterializedSnapshotCount(ProjectedWorldSnapshotReason.DamageProjection), Is.EqualTo(0));
        }

        [Test]
        [Category("Core")]
        public void CompositeDamageProjection_EntityDamageOperation_MaterializesProjection()
        {
            var targetId = 20;
            var baseSnapshot = CreateWorldState(
                    new[] { CreateUnit(targetId, new SurfaceCell(FaceId.Floor, 0, 0)) },
                    Array.Empty<TileFeatureState>())
                .CreateSnapshot();
            var payloads = new Dictionary<int, AttackActionPlanPayload>
            {
                [1] = CreateAttackPayload(
                    actionPlanId: 1,
                    damageWrites: new[]
                    {
                        new DamageWritePayload(
                            targetId,
                            amount: 1,
                            hasPlayerDamageState: false,
                            playerDamageState: default,
                            AttackSourceKind.Combat),
                    }),
            };
            var resolutionRecords = new[]
            {
                new ResolutionRecord(
                    contestId: 1,
                    ContestKind.Damage,
                    accepted: true,
                    sourceId: 10,
                    priority: 10,
                    actionPlanId: 1,
                    affectedEntityId: targetId,
                    localActionIndex: 0),
            };

            SnapshotMaterializationCounts counts;
            WorldSnapshot projectedSnapshot;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                projectedSnapshot = InvokeCompositeDamageProjectionSnapshot(
                    baseSnapshot,
                    resolutionRecords,
                    payloads);
                counts = capture.Counts;
            }

            Assert.That(projectedSnapshot, Is.Not.SameAs(baseSnapshot));
            Assert.That(projectedSnapshot.TryGetEntity(targetId, out var projectedTarget), Is.True);
            Assert.That(projectedTarget.hp, Is.EqualTo(2));
            Assert.That(counts.CompositeDamageProjectionReturnedBaseSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.CompositeDamageProjectionMaterializedSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.CompositeDamageProjectionEntityOperationCount, Is.EqualTo(1));
            Assert.That(counts.GetMaterializedSnapshotCount(ProjectedWorldSnapshotReason.DamageProjection), Is.EqualTo(1));
        }

        private static ActionGroup CreateActionGroup(int intentId, int sourceId, int priority, int groupId)
        {
            var actionGroup = new ActionGroup(intentId, sourceId, priority, ActionGroupKind.Move);
            actionGroup.AssignGroupId(groupId);
            return actionGroup;
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
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

        private static AttackActionPlanPayload CreateAttackPayload(
            int actionPlanId,
            IReadOnlyList<DamageWritePayload> damageWrites)
        {
            return new AttackActionPlanPayload(
                actionPlanId,
                intentId: actionPlanId,
                sourceActorEntityId: 10,
                priority: 10,
                ResolvedActionSemanticKind.Attack,
                Array.Empty<StateChangeWritePayload>(),
                damageWrites,
                Array.Empty<SpawnWritePayload>(),
                Array.Empty<DestroyWritePayload>(),
                Array.Empty<DelayedEnqueueWritePayload>());
        }

        private static WorldSnapshot InvokeCompositeDamageProjectionSnapshot(
            WorldSnapshot baseSnapshot,
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            IReadOnlyDictionary<int, AttackActionPlanPayload> payloads)
        {
            var method = typeof(TickPipeline).GetMethod(
                "CreateCompositeDamageProjectionSnapshot",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            return (WorldSnapshot)method.Invoke(null, new object[] { baseSnapshot, resolutionRecords, payloads });
        }
    }
}

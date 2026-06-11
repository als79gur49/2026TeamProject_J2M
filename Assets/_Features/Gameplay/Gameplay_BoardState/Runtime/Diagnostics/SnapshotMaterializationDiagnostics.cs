using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class SnapshotMaterializationDiagnostics
    {
        [ThreadStatic]
        private static Capture _current;

        internal static bool IsEnabled => _current != null;

        internal static SnapshotMaterializationCounts Current => _current?.Counts ?? default;

        internal static SnapshotMaterializationScope BeginCapture()
        {
            var previous = _current;
            var capture = new Capture();
            _current = capture;
            return new SnapshotMaterializationScope(previous, capture);
        }

        internal static void RecordWorldStateCreateSnapshot()
        {
            _current?.RecordWorldStateCreateSnapshot();
        }

        internal static void RecordSnapshotOwnedTileFeatureCellIndexBuild(int cellCount)
        {
            _current?.RecordSnapshotOwnedTileFeatureCellIndexBuild(cellCount);
        }

        internal static void RecordSnapshotOwnedStackedUnitCellIndexBuild(int cellCount)
        {
            _current?.RecordSnapshotOwnedStackedUnitCellIndexBuild(cellCount);
        }

        internal static void RecordSnapshotReadonlyCellIndexSecondCopySkipped(int cellCount)
        {
            _current?.RecordSnapshotReadonlyCellIndexSecondCopySkipped(cellCount);
        }

        internal static void RecordProjectedWorldMaterializedSnapshot()
        {
            RecordProjectedWorldMaterializedSnapshot(ProjectedWorldSnapshotReason.Unspecified);
        }

        internal static void RecordProjectedWorldMaterializedSnapshot(ProjectedWorldSnapshotReason reason)
        {
            _current?.RecordProjectedWorldMaterializedSnapshot(
                reason,
                baseEntityCount: 0,
                overlayEntityOperationCount: 0,
                overlayTileFeatureOperationCount: 0,
                materializedEntityCount: 0);
        }

        internal static void RecordProjectedWorldMaterializedSnapshot(
            ProjectedWorldSnapshotReason reason,
            int baseEntityCount,
            int overlayEntityOperationCount,
            int overlayTileFeatureOperationCount,
            int materializedEntityCount)
        {
            _current?.RecordProjectedWorldMaterializedSnapshot(
                reason,
                baseEntityCount,
                overlayEntityOperationCount,
                overlayTileFeatureOperationCount,
                materializedEntityCount);
        }

        internal static void RecordProjectedWorldCacheHit()
        {
            RecordProjectedWorldCacheHit(ProjectedWorldSnapshotReason.Unspecified);
        }

        internal static void RecordProjectedWorldCacheHit(ProjectedWorldSnapshotReason reason)
        {
            _current?.RecordProjectedWorldCacheHit(reason);
        }

        internal static void RecordProjectedWorldApplyBatch(bool isEmpty)
        {
            RecordProjectedWorldApplyBatch(isEmpty, ProjectedWorldBatchReason.Unspecified);
        }

        internal static void RecordProjectedWorldApplyBatch(bool isEmpty, ProjectedWorldBatchReason reason)
        {
            _current?.RecordProjectedWorldApplyBatch(isEmpty, reason);
        }

        internal static void RecordFastBaseSnapshotImport(int entityCount, int tileFeatureCount)
        {
            _current?.RecordFastBaseSnapshotImport(entityCount, tileFeatureCount);
        }

        internal static void RecordSlowBaseSnapshotImport(int entityCount, int tileFeatureCount)
        {
            _current?.RecordSlowBaseSnapshotImport(entityCount, tileFeatureCount);
        }

        internal static void RecordFastImportOverlayApply(int entityOperationCount, int tileFeatureOperationCount)
        {
            _current?.RecordFastImportOverlayApply(entityOperationCount, tileFeatureOperationCount);
        }

        internal static void RecordCompositeDamageProjection(
            int entityOperationCount,
            int tileFeatureOperationCount,
            int delayedAttackEffectCount,
            int damageFactCount,
            bool returnedBaseSnapshot)
        {
            _current?.RecordCompositeDamageProjection(
                entityOperationCount,
                tileFeatureOperationCount,
                delayedAttackEffectCount,
                damageFactCount,
                returnedBaseSnapshot);
        }

        internal static void RecordOrderedEntitiesCacheHit(int entityCount)
        {
            _current?.RecordOrderedEntitiesCacheHit(entityCount);
        }

        internal static void RecordOrderedEntitiesCacheMiss(int entityCount)
        {
            _current?.RecordOrderedEntitiesCacheMiss(entityCount);
        }

        internal static void RecordOrderedEntitiesSort(int entityCount)
        {
            _current?.RecordOrderedEntitiesSort(entityCount);
        }

        internal static void RecordOrderedTileFeaturesCacheHit(int tileFeatureCount)
        {
            _current?.RecordOrderedTileFeaturesCacheHit(tileFeatureCount);
        }

        internal static void RecordOrderedTileFeaturesCacheMiss(int tileFeatureCount)
        {
            _current?.RecordOrderedTileFeaturesCacheMiss(tileFeatureCount);
        }

        internal static void RecordOrderedTileFeaturesSort(int tileFeatureCount)
        {
            _current?.RecordOrderedTileFeaturesSort(tileFeatureCount);
        }

        internal static void RecordPlayerControlStateWritten()
        {
            _current?.RecordPlayerControlStateWritten();
        }

        internal static void RecordPlayerControlStateSameStateSkipped()
        {
            _current?.RecordPlayerControlStateSameStateSkipped();
        }

        internal sealed class Capture
        {
            private int _worldStateCreateSnapshotCount;
            private int _snapshotOwnedTileFeatureCellIndexBuildCount;
            private int _snapshotOwnedStackedUnitCellIndexBuildCount;
            private int _snapshotReadonlyCellIndexSecondCopySkippedCount;
            private int _snapshotTileFeatureCellIndexCellCount;
            private int _snapshotStackedUnitCellIndexCellCount;
            private int _projectedWorldMaterializedSnapshotCount;
            private int _projectedWorldCacheHitCount;
            private int _projectedWorldApplyBatchCount;
            private int _projectedWorldEmptyApplyBatchCount;
            private int _projectedWorldMaterializedBaseEntityCount;
            private int _projectedWorldMaterializedOverlayEntityOperationCount;
            private int _projectedWorldMaterializedOverlayTileFeatureOperationCount;
            private int _projectedWorldMaterializedEntityCount;
            private int _fastBaseSnapshotImportCount;
            private int _slowBaseSnapshotImportCount;
            private int _fastImportedEntityCount;
            private int _fastImportedTileFeatureCount;
            private int _slowImportedEntityCount;
            private int _slowImportedTileFeatureCount;
            private int _fastImportOverlayApplyCount;
            private int _fastImportOverlayEntityOperationCount;
            private int _fastImportOverlayTileFeatureOperationCount;
            private int _compositeDamageProjectionCount;
            private int _compositeDamageProjectionEntityOperationCount;
            private int _compositeDamageProjectionTileFeatureOperationCount;
            private int _compositeDamageProjectionDelayedAttackEffectCount;
            private int _compositeDamageProjectionDamageFactCount;
            private int _compositeDamageProjectionReturnedBaseSnapshotCount;
            private int _compositeDamageProjectionMaterializedSnapshotCount;
            private int _orderedEntitiesCacheHitCount;
            private int _orderedEntitiesCacheMissCount;
            private int _orderedEntitiesSortCount;
            private int _orderedEntitiesEnumeratedCount;
            private int _orderedTileFeaturesCacheHitCount;
            private int _orderedTileFeaturesCacheMissCount;
            private int _orderedTileFeaturesSortCount;
            private int _orderedTileFeaturesEnumeratedCount;
            private int _playerControlStateWrittenCount;
            private int _playerControlStateSameStateSkippedCount;
            private readonly Dictionary<ProjectedWorldSnapshotReason, int> _materializedSnapshotCountsByReason = new();
            private readonly Dictionary<ProjectedWorldSnapshotReason, int> _cacheHitCountsByReason = new();
            private readonly Dictionary<ProjectedWorldBatchReason, int> _applyBatchCountsByReason = new();
            private readonly Dictionary<ProjectedWorldBatchReason, int> _emptyApplyBatchCountsByReason = new();

            public SnapshotMaterializationCounts Counts =>
                new(
                    _worldStateCreateSnapshotCount,
                    _snapshotOwnedTileFeatureCellIndexBuildCount,
                    _snapshotOwnedStackedUnitCellIndexBuildCount,
                    _snapshotReadonlyCellIndexSecondCopySkippedCount,
                    _snapshotTileFeatureCellIndexCellCount,
                    _snapshotStackedUnitCellIndexCellCount,
                    _projectedWorldMaterializedSnapshotCount,
                    _projectedWorldCacheHitCount,
                    _projectedWorldApplyBatchCount,
                    _projectedWorldEmptyApplyBatchCount,
                    _projectedWorldMaterializedBaseEntityCount,
                    _projectedWorldMaterializedOverlayEntityOperationCount,
                    _projectedWorldMaterializedOverlayTileFeatureOperationCount,
                    _projectedWorldMaterializedEntityCount,
                    _fastBaseSnapshotImportCount,
                    _slowBaseSnapshotImportCount,
                    _fastImportedEntityCount,
                    _fastImportedTileFeatureCount,
                    _slowImportedEntityCount,
                    _slowImportedTileFeatureCount,
                    _fastImportOverlayApplyCount,
                    _fastImportOverlayEntityOperationCount,
                    _fastImportOverlayTileFeatureOperationCount,
                    _compositeDamageProjectionCount,
                    _compositeDamageProjectionEntityOperationCount,
                    _compositeDamageProjectionTileFeatureOperationCount,
                    _compositeDamageProjectionDelayedAttackEffectCount,
                    _compositeDamageProjectionDamageFactCount,
                    _compositeDamageProjectionReturnedBaseSnapshotCount,
                    _compositeDamageProjectionMaterializedSnapshotCount,
                    _orderedEntitiesCacheHitCount,
                    _orderedEntitiesCacheMissCount,
                    _orderedEntitiesSortCount,
                    _orderedEntitiesEnumeratedCount,
                    _orderedTileFeaturesCacheHitCount,
                    _orderedTileFeaturesCacheMissCount,
                    _orderedTileFeaturesSortCount,
                    _orderedTileFeaturesEnumeratedCount,
                    _playerControlStateWrittenCount,
                    _playerControlStateSameStateSkippedCount,
                    _materializedSnapshotCountsByReason,
                    _cacheHitCountsByReason,
                    _applyBatchCountsByReason,
                    _emptyApplyBatchCountsByReason);

            public void RecordWorldStateCreateSnapshot()
            {
                _worldStateCreateSnapshotCount++;
            }

            public void RecordSnapshotOwnedTileFeatureCellIndexBuild(int cellCount)
            {
                _snapshotOwnedTileFeatureCellIndexBuildCount++;
                _snapshotTileFeatureCellIndexCellCount += cellCount;
            }

            public void RecordSnapshotOwnedStackedUnitCellIndexBuild(int cellCount)
            {
                _snapshotOwnedStackedUnitCellIndexBuildCount++;
                _snapshotStackedUnitCellIndexCellCount += cellCount;
            }

            public void RecordSnapshotReadonlyCellIndexSecondCopySkipped(int cellCount)
            {
                _snapshotReadonlyCellIndexSecondCopySkippedCount++;
            }

            public void RecordProjectedWorldMaterializedSnapshot(
                ProjectedWorldSnapshotReason reason,
                int baseEntityCount,
                int overlayEntityOperationCount,
                int overlayTileFeatureOperationCount,
                int materializedEntityCount)
            {
                _projectedWorldMaterializedSnapshotCount++;
                _projectedWorldMaterializedBaseEntityCount += baseEntityCount;
                _projectedWorldMaterializedOverlayEntityOperationCount += overlayEntityOperationCount;
                _projectedWorldMaterializedOverlayTileFeatureOperationCount += overlayTileFeatureOperationCount;
                _projectedWorldMaterializedEntityCount += materializedEntityCount;
                Increment(_materializedSnapshotCountsByReason, reason);
            }

            public void RecordProjectedWorldCacheHit(ProjectedWorldSnapshotReason reason)
            {
                _projectedWorldCacheHitCount++;
                Increment(_cacheHitCountsByReason, reason);
            }

            public void RecordProjectedWorldApplyBatch(bool isEmpty, ProjectedWorldBatchReason reason)
            {
                _projectedWorldApplyBatchCount++;
                Increment(_applyBatchCountsByReason, reason);
                if (isEmpty)
                {
                    _projectedWorldEmptyApplyBatchCount++;
                    Increment(_emptyApplyBatchCountsByReason, reason);
                }
            }

            public void RecordFastBaseSnapshotImport(int entityCount, int tileFeatureCount)
            {
                _fastBaseSnapshotImportCount++;
                _fastImportedEntityCount += entityCount;
                _fastImportedTileFeatureCount += tileFeatureCount;
            }

            public void RecordSlowBaseSnapshotImport(int entityCount, int tileFeatureCount)
            {
                _slowBaseSnapshotImportCount++;
                _slowImportedEntityCount += entityCount;
                _slowImportedTileFeatureCount += tileFeatureCount;
            }

            public void RecordFastImportOverlayApply(int entityOperationCount, int tileFeatureOperationCount)
            {
                _fastImportOverlayApplyCount++;
                _fastImportOverlayEntityOperationCount += entityOperationCount;
                _fastImportOverlayTileFeatureOperationCount += tileFeatureOperationCount;
            }

            public void RecordCompositeDamageProjection(
                int entityOperationCount,
                int tileFeatureOperationCount,
                int delayedAttackEffectCount,
                int damageFactCount,
                bool returnedBaseSnapshot)
            {
                _compositeDamageProjectionCount++;
                _compositeDamageProjectionEntityOperationCount += entityOperationCount;
                _compositeDamageProjectionTileFeatureOperationCount += tileFeatureOperationCount;
                _compositeDamageProjectionDelayedAttackEffectCount += delayedAttackEffectCount;
                _compositeDamageProjectionDamageFactCount += damageFactCount;
                if (returnedBaseSnapshot)
                {
                    _compositeDamageProjectionReturnedBaseSnapshotCount++;
                }
                else
                {
                    _compositeDamageProjectionMaterializedSnapshotCount++;
                }
            }

            public void RecordOrderedEntitiesCacheHit(int entityCount)
            {
                _orderedEntitiesCacheHitCount++;
                _orderedEntitiesEnumeratedCount += entityCount;
            }

            public void RecordOrderedEntitiesCacheMiss(int entityCount)
            {
                _orderedEntitiesCacheMissCount++;
                _orderedEntitiesEnumeratedCount += entityCount;
            }

            public void RecordOrderedEntitiesSort(int entityCount)
            {
                _orderedEntitiesSortCount++;
            }

            public void RecordOrderedTileFeaturesCacheHit(int tileFeatureCount)
            {
                _orderedTileFeaturesCacheHitCount++;
                _orderedTileFeaturesEnumeratedCount += tileFeatureCount;
            }

            public void RecordOrderedTileFeaturesCacheMiss(int tileFeatureCount)
            {
                _orderedTileFeaturesCacheMissCount++;
                _orderedTileFeaturesEnumeratedCount += tileFeatureCount;
            }

            public void RecordOrderedTileFeaturesSort(int tileFeatureCount)
            {
                _orderedTileFeaturesSortCount++;
            }

            public void RecordPlayerControlStateWritten()
            {
                _playerControlStateWrittenCount++;
            }

            public void RecordPlayerControlStateSameStateSkipped()
            {
                _playerControlStateSameStateSkippedCount++;
            }

            private static void Increment<T>(IDictionary<T, int> counts, T key)
            {
                counts.TryGetValue(key, out var count);
                counts[key] = count + 1;
            }
        }

        internal readonly struct SnapshotMaterializationScope : IDisposable
        {
            private readonly Capture _previous;
            private readonly Capture _capture;

            internal SnapshotMaterializationScope(Capture previous, Capture capture)
            {
                _previous = previous;
                _capture = capture;
            }

            public SnapshotMaterializationCounts Counts => _capture?.Counts ?? default;

            public void Dispose()
            {
                if (_current == _capture)
                {
                    _current = _previous;
                }
            }
        }
    }

    internal readonly struct SnapshotMaterializationCounts
    {
        public SnapshotMaterializationCounts(
            int worldStateCreateSnapshotCount,
            int projectedWorldMaterializedSnapshotCount,
            int projectedWorldCacheHitCount,
            int projectedWorldApplyBatchCount,
            int projectedWorldEmptyApplyBatchCount)
            : this(
                worldStateCreateSnapshotCount,
                0,
                0,
                0,
                0,
                0,
                projectedWorldMaterializedSnapshotCount,
                projectedWorldCacheHitCount,
                projectedWorldApplyBatchCount,
                projectedWorldEmptyApplyBatchCount,
                projectedWorldMaterializedBaseEntityCount: 0,
                projectedWorldMaterializedOverlayEntityOperationCount: 0,
                projectedWorldMaterializedOverlayTileFeatureOperationCount: 0,
                projectedWorldMaterializedEntityCount: 0,
                fastBaseSnapshotImportCount: 0,
                slowBaseSnapshotImportCount: 0,
                fastImportedEntityCount: 0,
                fastImportedTileFeatureCount: 0,
                slowImportedEntityCount: 0,
                slowImportedTileFeatureCount: 0,
                fastImportOverlayApplyCount: 0,
                fastImportOverlayEntityOperationCount: 0,
                fastImportOverlayTileFeatureOperationCount: 0,
                compositeDamageProjectionCount: 0,
                compositeDamageProjectionEntityOperationCount: 0,
                compositeDamageProjectionTileFeatureOperationCount: 0,
                compositeDamageProjectionDelayedAttackEffectCount: 0,
                compositeDamageProjectionDamageFactCount: 0,
                compositeDamageProjectionReturnedBaseSnapshotCount: 0,
                compositeDamageProjectionMaterializedSnapshotCount: 0,
                orderedEntitiesCacheHitCount: 0,
                orderedEntitiesCacheMissCount: 0,
                orderedEntitiesSortCount: 0,
                orderedEntitiesEnumeratedCount: 0,
                orderedTileFeaturesCacheHitCount: 0,
                orderedTileFeaturesCacheMissCount: 0,
                orderedTileFeaturesSortCount: 0,
                orderedTileFeaturesEnumeratedCount: 0,
                playerControlStateWrittenCount: 0,
                playerControlStateSameStateSkippedCount: 0,
                null,
                null,
                null,
                null)
        {
        }

        public SnapshotMaterializationCounts(
            int worldStateCreateSnapshotCount,
            int snapshotOwnedTileFeatureCellIndexBuildCount,
            int snapshotOwnedStackedUnitCellIndexBuildCount,
            int snapshotReadonlyCellIndexSecondCopySkippedCount,
            int snapshotTileFeatureCellIndexCellCount,
            int snapshotStackedUnitCellIndexCellCount,
            int projectedWorldMaterializedSnapshotCount,
            int projectedWorldCacheHitCount,
            int projectedWorldApplyBatchCount,
            int projectedWorldEmptyApplyBatchCount,
            int projectedWorldMaterializedBaseEntityCount,
            int projectedWorldMaterializedOverlayEntityOperationCount,
            int projectedWorldMaterializedOverlayTileFeatureOperationCount,
            int projectedWorldMaterializedEntityCount,
            int fastBaseSnapshotImportCount,
            int slowBaseSnapshotImportCount,
            int fastImportedEntityCount,
            int fastImportedTileFeatureCount,
            int slowImportedEntityCount,
            int slowImportedTileFeatureCount,
            int fastImportOverlayApplyCount,
            int fastImportOverlayEntityOperationCount,
            int fastImportOverlayTileFeatureOperationCount,
            int compositeDamageProjectionCount,
            int compositeDamageProjectionEntityOperationCount,
            int compositeDamageProjectionTileFeatureOperationCount,
            int compositeDamageProjectionDelayedAttackEffectCount,
            int compositeDamageProjectionDamageFactCount,
            int compositeDamageProjectionReturnedBaseSnapshotCount,
            int compositeDamageProjectionMaterializedSnapshotCount,
            int orderedEntitiesCacheHitCount,
            int orderedEntitiesCacheMissCount,
            int orderedEntitiesSortCount,
            int orderedEntitiesEnumeratedCount,
            int orderedTileFeaturesCacheHitCount,
            int orderedTileFeaturesCacheMissCount,
            int orderedTileFeaturesSortCount,
            int orderedTileFeaturesEnumeratedCount,
            int playerControlStateWrittenCount,
            int playerControlStateSameStateSkippedCount,
            IReadOnlyDictionary<ProjectedWorldSnapshotReason, int> materializedSnapshotCountsByReason,
            IReadOnlyDictionary<ProjectedWorldSnapshotReason, int> cacheHitCountsByReason,
            IReadOnlyDictionary<ProjectedWorldBatchReason, int> applyBatchCountsByReason,
            IReadOnlyDictionary<ProjectedWorldBatchReason, int> emptyApplyBatchCountsByReason)
        {
            WorldStateCreateSnapshotCount = worldStateCreateSnapshotCount;
            SnapshotOwnedTileFeatureCellIndexBuildCount = snapshotOwnedTileFeatureCellIndexBuildCount;
            SnapshotOwnedStackedUnitCellIndexBuildCount = snapshotOwnedStackedUnitCellIndexBuildCount;
            SnapshotReadonlyCellIndexSecondCopySkippedCount = snapshotReadonlyCellIndexSecondCopySkippedCount;
            SnapshotTileFeatureCellIndexCellCount = snapshotTileFeatureCellIndexCellCount;
            SnapshotStackedUnitCellIndexCellCount = snapshotStackedUnitCellIndexCellCount;
            ProjectedWorldMaterializedSnapshotCount = projectedWorldMaterializedSnapshotCount;
            ProjectedWorldCacheHitCount = projectedWorldCacheHitCount;
            ProjectedWorldApplyBatchCount = projectedWorldApplyBatchCount;
            ProjectedWorldEmptyApplyBatchCount = projectedWorldEmptyApplyBatchCount;
            ProjectedWorldMaterializedBaseEntityCount = projectedWorldMaterializedBaseEntityCount;
            ProjectedWorldMaterializedOverlayEntityOperationCount = projectedWorldMaterializedOverlayEntityOperationCount;
            ProjectedWorldMaterializedOverlayTileFeatureOperationCount = projectedWorldMaterializedOverlayTileFeatureOperationCount;
            ProjectedWorldMaterializedEntityCount = projectedWorldMaterializedEntityCount;
            FastBaseSnapshotImportCount = fastBaseSnapshotImportCount;
            SlowBaseSnapshotImportCount = slowBaseSnapshotImportCount;
            FastImportedEntityCount = fastImportedEntityCount;
            FastImportedTileFeatureCount = fastImportedTileFeatureCount;
            SlowImportedEntityCount = slowImportedEntityCount;
            SlowImportedTileFeatureCount = slowImportedTileFeatureCount;
            FastImportOverlayApplyCount = fastImportOverlayApplyCount;
            FastImportOverlayEntityOperationCount = fastImportOverlayEntityOperationCount;
            FastImportOverlayTileFeatureOperationCount = fastImportOverlayTileFeatureOperationCount;
            CompositeDamageProjectionCount = compositeDamageProjectionCount;
            CompositeDamageProjectionEntityOperationCount = compositeDamageProjectionEntityOperationCount;
            CompositeDamageProjectionTileFeatureOperationCount = compositeDamageProjectionTileFeatureOperationCount;
            CompositeDamageProjectionDelayedAttackEffectCount = compositeDamageProjectionDelayedAttackEffectCount;
            CompositeDamageProjectionDamageFactCount = compositeDamageProjectionDamageFactCount;
            CompositeDamageProjectionReturnedBaseSnapshotCount = compositeDamageProjectionReturnedBaseSnapshotCount;
            CompositeDamageProjectionMaterializedSnapshotCount = compositeDamageProjectionMaterializedSnapshotCount;
            OrderedEntitiesCacheHitCount = orderedEntitiesCacheHitCount;
            OrderedEntitiesCacheMissCount = orderedEntitiesCacheMissCount;
            OrderedEntitiesSortCount = orderedEntitiesSortCount;
            OrderedEntitiesEnumeratedCount = orderedEntitiesEnumeratedCount;
            OrderedTileFeaturesCacheHitCount = orderedTileFeaturesCacheHitCount;
            OrderedTileFeaturesCacheMissCount = orderedTileFeaturesCacheMissCount;
            OrderedTileFeaturesSortCount = orderedTileFeaturesSortCount;
            OrderedTileFeaturesEnumeratedCount = orderedTileFeaturesEnumeratedCount;
            PlayerControlStateWrittenCount = playerControlStateWrittenCount;
            PlayerControlStateSameStateSkippedCount = playerControlStateSameStateSkippedCount;
            MaterializedSnapshotCountsByReason = Clone(materializedSnapshotCountsByReason);
            CacheHitCountsByReason = Clone(cacheHitCountsByReason);
            ApplyBatchCountsByReason = Clone(applyBatchCountsByReason);
            EmptyApplyBatchCountsByReason = Clone(emptyApplyBatchCountsByReason);
        }

        public int WorldStateCreateSnapshotCount { get; }

        public int SnapshotOwnedTileFeatureCellIndexBuildCount { get; }

        public int SnapshotOwnedStackedUnitCellIndexBuildCount { get; }

        public int SnapshotReadonlyCellIndexSecondCopySkippedCount { get; }

        public int SnapshotTileFeatureCellIndexCellCount { get; }

        public int SnapshotStackedUnitCellIndexCellCount { get; }

        public int ProjectedWorldMaterializedSnapshotCount { get; }

        public int ProjectedWorldCacheHitCount { get; }

        public int ProjectedWorldApplyBatchCount { get; }

        public int ProjectedWorldEmptyApplyBatchCount { get; }

        public int ProjectedWorldMaterializedBaseEntityCount { get; }

        public int ProjectedWorldMaterializedOverlayEntityOperationCount { get; }

        public int ProjectedWorldMaterializedOverlayTileFeatureOperationCount { get; }

        public int ProjectedWorldMaterializedEntityCount { get; }

        public int FastBaseSnapshotImportCount { get; }

        public int SlowBaseSnapshotImportCount { get; }

        public int FastImportedEntityCount { get; }

        public int FastImportedTileFeatureCount { get; }

        public int SlowImportedEntityCount { get; }

        public int SlowImportedTileFeatureCount { get; }

        public int FastImportOverlayApplyCount { get; }

        public int FastImportOverlayEntityOperationCount { get; }

        public int FastImportOverlayTileFeatureOperationCount { get; }

        public int CompositeDamageProjectionCount { get; }

        public int CompositeDamageProjectionEntityOperationCount { get; }

        public int CompositeDamageProjectionTileFeatureOperationCount { get; }

        public int CompositeDamageProjectionDelayedAttackEffectCount { get; }

        public int CompositeDamageProjectionDamageFactCount { get; }

        public int CompositeDamageProjectionReturnedBaseSnapshotCount { get; }

        public int CompositeDamageProjectionMaterializedSnapshotCount { get; }

        public int OrderedEntitiesCacheHitCount { get; }

        public int OrderedEntitiesCacheMissCount { get; }

        public int OrderedEntitiesSortCount { get; }

        public int OrderedEntitiesEnumeratedCount { get; }

        public int OrderedTileFeaturesCacheHitCount { get; }

        public int OrderedTileFeaturesCacheMissCount { get; }

        public int OrderedTileFeaturesSortCount { get; }

        public int OrderedTileFeaturesEnumeratedCount { get; }

        public int PlayerControlStateWrittenCount { get; }

        public int PlayerControlStateSameStateSkippedCount { get; }

        public IReadOnlyDictionary<ProjectedWorldSnapshotReason, int> MaterializedSnapshotCountsByReason { get; }

        public IReadOnlyDictionary<ProjectedWorldSnapshotReason, int> CacheHitCountsByReason { get; }

        public IReadOnlyDictionary<ProjectedWorldBatchReason, int> ApplyBatchCountsByReason { get; }

        public IReadOnlyDictionary<ProjectedWorldBatchReason, int> EmptyApplyBatchCountsByReason { get; }

        public int GetMaterializedSnapshotCount(ProjectedWorldSnapshotReason reason)
        {
            return TryGetCount(MaterializedSnapshotCountsByReason, reason);
        }

        public int GetCacheHitCount(ProjectedWorldSnapshotReason reason)
        {
            return TryGetCount(CacheHitCountsByReason, reason);
        }

        public int GetApplyBatchCount(ProjectedWorldBatchReason reason)
        {
            return TryGetCount(ApplyBatchCountsByReason, reason);
        }

        public int GetEmptyApplyBatchCount(ProjectedWorldBatchReason reason)
        {
            return TryGetCount(EmptyApplyBatchCountsByReason, reason);
        }

        private static IReadOnlyDictionary<T, int> Clone<T>(IReadOnlyDictionary<T, int> source)
        {
            return source == null
                ? new Dictionary<T, int>()
                : new Dictionary<T, int>(source);
        }

        private static int TryGetCount<T>(IReadOnlyDictionary<T, int> counts, T key)
        {
            return counts != null && counts.TryGetValue(key, out var count) ? count : 0;
        }
    }

    internal enum ProjectedWorldBatchReason
    {
        Unspecified = 0,
        PlanBeforeMovementAi = 1,
        PlanKinematicClosure = 2,
        PlanGravityField = 3,
        PlanPreMovementState = 4,
        PlanPreMovementUtility = 5,
        PlanPlayerActionAttempt = 6,
        PlanPlayerFree2DLocomotion = 7,
        ResolvePlanFinalization = 100,
        ResolveMovementStage = 101,
        ResolveBeforeAttackAi = 102,
        ResolveEnemyActionBeforeAttack = 103,
        ResolveJumpLanding = 104,
        RetiredResolvePhaseRelocation = 105,
        ResolveTileEffectEntityOperations = 106,
        ResolveAttackStage = 107,
        ResolveEnemyActionAfterAttack = 108,
        ResolveAfterAttackAi = 109,
        ResolveUtility = 110,
        DamageProjection = 200,
    }

    internal enum ProjectedWorldSnapshotReason
    {
        Unspecified = 0,
        PlanAfterEnemyAi = 1,
        PlanAfterKinematicClosure = 2,
        PlanAfterGravityField = 3,
        PlanPreMovementUtilityInput = 4,
        PlanPostPreMovement = 5,
        PlanAfterPlayerActionAttempt = 6,
        PlanAfterPlayerFree2DLocomotion = 7,
        ResolvePostMovement = 100,
        ResolveEnemyActionBeforeAttackInput = 101,
        ResolveAttackSnapshot = 102,
        ResolveAttackRead = 103,
        ResolvePostAttack = 104,
        DamageProjection = 200,
    }
}

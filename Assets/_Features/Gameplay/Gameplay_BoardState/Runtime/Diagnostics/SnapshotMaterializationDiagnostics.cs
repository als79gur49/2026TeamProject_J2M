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

        internal static void RecordProjectedWorldMaterializedSnapshot()
        {
            RecordProjectedWorldMaterializedSnapshot(ProjectedWorldSnapshotReason.Unspecified);
        }

        internal static void RecordProjectedWorldMaterializedSnapshot(ProjectedWorldSnapshotReason reason)
        {
            _current?.RecordProjectedWorldMaterializedSnapshot(reason);
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

        internal sealed class Capture
        {
            private int _worldStateCreateSnapshotCount;
            private int _projectedWorldMaterializedSnapshotCount;
            private int _projectedWorldCacheHitCount;
            private int _projectedWorldApplyBatchCount;
            private int _projectedWorldEmptyApplyBatchCount;
            private readonly Dictionary<ProjectedWorldSnapshotReason, int> _materializedSnapshotCountsByReason = new();
            private readonly Dictionary<ProjectedWorldSnapshotReason, int> _cacheHitCountsByReason = new();
            private readonly Dictionary<ProjectedWorldBatchReason, int> _applyBatchCountsByReason = new();
            private readonly Dictionary<ProjectedWorldBatchReason, int> _emptyApplyBatchCountsByReason = new();

            public SnapshotMaterializationCounts Counts =>
                new(
                    _worldStateCreateSnapshotCount,
                    _projectedWorldMaterializedSnapshotCount,
                    _projectedWorldCacheHitCount,
                    _projectedWorldApplyBatchCount,
                    _projectedWorldEmptyApplyBatchCount,
                    _materializedSnapshotCountsByReason,
                    _cacheHitCountsByReason,
                    _applyBatchCountsByReason,
                    _emptyApplyBatchCountsByReason);

            public void RecordWorldStateCreateSnapshot()
            {
                _worldStateCreateSnapshotCount++;
            }

            public void RecordProjectedWorldMaterializedSnapshot(ProjectedWorldSnapshotReason reason)
            {
                _projectedWorldMaterializedSnapshotCount++;
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
                projectedWorldMaterializedSnapshotCount,
                projectedWorldCacheHitCount,
                projectedWorldApplyBatchCount,
                projectedWorldEmptyApplyBatchCount,
                null,
                null,
                null,
                null)
        {
        }

        public SnapshotMaterializationCounts(
            int worldStateCreateSnapshotCount,
            int projectedWorldMaterializedSnapshotCount,
            int projectedWorldCacheHitCount,
            int projectedWorldApplyBatchCount,
            int projectedWorldEmptyApplyBatchCount,
            IReadOnlyDictionary<ProjectedWorldSnapshotReason, int> materializedSnapshotCountsByReason,
            IReadOnlyDictionary<ProjectedWorldSnapshotReason, int> cacheHitCountsByReason,
            IReadOnlyDictionary<ProjectedWorldBatchReason, int> applyBatchCountsByReason,
            IReadOnlyDictionary<ProjectedWorldBatchReason, int> emptyApplyBatchCountsByReason)
        {
            WorldStateCreateSnapshotCount = worldStateCreateSnapshotCount;
            ProjectedWorldMaterializedSnapshotCount = projectedWorldMaterializedSnapshotCount;
            ProjectedWorldCacheHitCount = projectedWorldCacheHitCount;
            ProjectedWorldApplyBatchCount = projectedWorldApplyBatchCount;
            ProjectedWorldEmptyApplyBatchCount = projectedWorldEmptyApplyBatchCount;
            MaterializedSnapshotCountsByReason = Clone(materializedSnapshotCountsByReason);
            CacheHitCountsByReason = Clone(cacheHitCountsByReason);
            ApplyBatchCountsByReason = Clone(applyBatchCountsByReason);
            EmptyApplyBatchCountsByReason = Clone(emptyApplyBatchCountsByReason);
        }

        public int WorldStateCreateSnapshotCount { get; }

        public int ProjectedWorldMaterializedSnapshotCount { get; }

        public int ProjectedWorldCacheHitCount { get; }

        public int ProjectedWorldApplyBatchCount { get; }

        public int ProjectedWorldEmptyApplyBatchCount { get; }

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
        ResolvePhaseRelocation = 105,
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

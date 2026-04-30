using System;

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
            _current?.RecordProjectedWorldMaterializedSnapshot();
        }

        internal static void RecordProjectedWorldCacheHit()
        {
            _current?.RecordProjectedWorldCacheHit();
        }

        internal static void RecordProjectedWorldApplyBatch(bool isEmpty)
        {
            _current?.RecordProjectedWorldApplyBatch(isEmpty);
        }

        internal sealed class Capture
        {
            private int _worldStateCreateSnapshotCount;
            private int _projectedWorldMaterializedSnapshotCount;
            private int _projectedWorldCacheHitCount;
            private int _projectedWorldApplyBatchCount;
            private int _projectedWorldEmptyApplyBatchCount;

            public SnapshotMaterializationCounts Counts =>
                new(
                    _worldStateCreateSnapshotCount,
                    _projectedWorldMaterializedSnapshotCount,
                    _projectedWorldCacheHitCount,
                    _projectedWorldApplyBatchCount,
                    _projectedWorldEmptyApplyBatchCount);

            public void RecordWorldStateCreateSnapshot()
            {
                _worldStateCreateSnapshotCount++;
            }

            public void RecordProjectedWorldMaterializedSnapshot()
            {
                _projectedWorldMaterializedSnapshotCount++;
            }

            public void RecordProjectedWorldCacheHit()
            {
                _projectedWorldCacheHitCount++;
            }

            public void RecordProjectedWorldApplyBatch(bool isEmpty)
            {
                _projectedWorldApplyBatchCount++;
                if (isEmpty)
                {
                    _projectedWorldEmptyApplyBatchCount++;
                }
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
        {
            WorldStateCreateSnapshotCount = worldStateCreateSnapshotCount;
            ProjectedWorldMaterializedSnapshotCount = projectedWorldMaterializedSnapshotCount;
            ProjectedWorldCacheHitCount = projectedWorldCacheHitCount;
            ProjectedWorldApplyBatchCount = projectedWorldApplyBatchCount;
            ProjectedWorldEmptyApplyBatchCount = projectedWorldEmptyApplyBatchCount;
        }

        public int WorldStateCreateSnapshotCount { get; }

        public int ProjectedWorldMaterializedSnapshotCount { get; }

        public int ProjectedWorldCacheHitCount { get; }

        public int ProjectedWorldApplyBatchCount { get; }

        public int ProjectedWorldEmptyApplyBatchCount { get; }
    }
}

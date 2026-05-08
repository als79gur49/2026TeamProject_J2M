using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    internal enum TileFeatureOperationKind
    {
        Add = 0,
        Update = 1,
        Remove = 2,
    }

    internal readonly struct TileFeatureOperation
    {
        private TileFeatureOperation(
            TileFeatureOperationKind kind,
            int tileId,
            TileFeatureState state)
        {
            Kind = kind;
            TileId = tileId;
            State = state;
        }

        public TileFeatureOperationKind Kind { get; }

        public int TileId { get; }

        public TileFeatureState State { get; }

        public static TileFeatureOperation Add(TileFeatureState state)
        {
            return new TileFeatureOperation(TileFeatureOperationKind.Add, state.TileId, state);
        }

        public static TileFeatureOperation Update(TileFeatureState state)
        {
            return new TileFeatureOperation(TileFeatureOperationKind.Update, state.TileId, state);
        }

        public static TileFeatureOperation Remove(int tileId)
        {
            return new TileFeatureOperation(TileFeatureOperationKind.Remove, tileId, default);
        }

        internal static TileFeatureOperation CreateUnchecked(
            TileFeatureOperationKind kind,
            int tileId,
            TileFeatureState state)
        {
            return new TileFeatureOperation(kind, tileId, state);
        }
    }

    internal sealed class TileFeatureOperationBatch
    {
        private readonly List<TileFeatureOperation> _operations = new();

        public TileFeatureOperationBatch()
        {
        }

        public TileFeatureOperationBatch(IEnumerable<TileFeatureOperation> operations)
        {
            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            foreach (var operation in operations)
            {
                _operations.Add(operation);
            }
        }

        public IReadOnlyList<TileFeatureOperation> Operations => _operations;

        public bool IsEmpty => _operations.Count == 0;

        public void Add(TileFeatureOperation operation)
        {
            _operations.Add(operation);
        }
    }
}

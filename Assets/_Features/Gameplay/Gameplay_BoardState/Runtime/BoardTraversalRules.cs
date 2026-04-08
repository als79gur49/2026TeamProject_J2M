using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    [Serializable]
    public sealed class BoardTraversalRules
    {
        private static readonly int[] EmptyColumns = Array.Empty<int>();

        private readonly HashSet<int> _sharedEdgeTraversalColumns;
        private readonly int[] _sharedEdgeTraversalColumnsOrdered;

        public static BoardTraversalRules Empty { get; } = new(EmptyColumns);

        public BoardTraversalRules(IEnumerable<int> sharedEdgeTraversalColumns)
        {
            if (sharedEdgeTraversalColumns == null)
            {
                _sharedEdgeTraversalColumns = new HashSet<int>();
                _sharedEdgeTraversalColumnsOrdered = EmptyColumns;
                return;
            }

            _sharedEdgeTraversalColumns = new HashSet<int>();
            var orderedColumns = new List<int>();

            foreach (var column in sharedEdgeTraversalColumns)
            {
                if (_sharedEdgeTraversalColumns.Add(column))
                {
                    orderedColumns.Add(column);
                }
            }

            orderedColumns.Sort();
            _sharedEdgeTraversalColumnsOrdered = orderedColumns.Count > 0
                ? orderedColumns.ToArray()
                : EmptyColumns;
        }

        public IReadOnlyList<int> SharedEdgeTraversalColumns => _sharedEdgeTraversalColumnsOrdered;

        public bool AllowsSharedEdgeTraversal(int column)
        {
            return _sharedEdgeTraversalColumns.Contains(column);
        }
    }
}

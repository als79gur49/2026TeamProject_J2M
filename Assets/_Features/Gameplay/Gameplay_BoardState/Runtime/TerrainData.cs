using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public sealed class TerrainData
    {
        private static readonly TerrainData EmptyTerrain = new(Array.Empty<Vector2Int>());

        private readonly HashSet<Vector2Int> _unitBlockingCells;
        private readonly ReadOnlyCollection<Vector2Int> _orderedUnitBlockingCells;

        public TerrainData(IEnumerable<Vector2Int> unitBlockingCells)
        {
            if (unitBlockingCells == null)
            {
                throw new ArgumentNullException(nameof(unitBlockingCells));
            }

            var uniqueCells = new HashSet<Vector2Int>();

            foreach (var cell in unitBlockingCells)
            {
                uniqueCells.Add(cell);
            }

            var orderedCells = new List<Vector2Int>(uniqueCells);
            orderedCells.Sort(Vector2IntComparer.Instance);

            _unitBlockingCells = uniqueCells;
            _orderedUnitBlockingCells = new ReadOnlyCollection<Vector2Int>(orderedCells);
        }

        public static TerrainData Empty => EmptyTerrain;

        public bool BlocksUnitMovement(Vector2Int cell)
        {
            return _unitBlockingCells.Contains(cell);
        }

        internal IReadOnlyList<Vector2Int> OrderedUnitBlockingCells => _orderedUnitBlockingCells;

        private sealed class Vector2IntComparer : IComparer<Vector2Int>
        {
            internal static readonly Vector2IntComparer Instance = new();

            public int Compare(Vector2Int left, Vector2Int right)
            {
                var result = left.x.CompareTo(right.x);
                if (result != 0)
                {
                    return result;
                }

                return left.y.CompareTo(right.y);
            }
        }
    }
}

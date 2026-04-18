using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public sealed class TerrainData
    {
        private static readonly FaceId[] AllFaces =
        {
            FaceId.Floor,
            FaceId.Front,
            FaceId.Ceiling,
            FaceId.Back,
        };

        private static readonly TerrainData EmptyTerrain = new(Array.Empty<TerrainCellState>());

        private readonly Dictionary<SurfaceCell, TerrainCellState> _terrainByCell;
        private readonly ReadOnlyCollection<TerrainCellState> _orderedTerrainCells;
        private readonly ReadOnlyCollection<Vector2Int> _orderedUnitBlockingCells;

        public TerrainData(IEnumerable<Vector2Int> unitBlockingCells)
            : this(ExpandLegacyUnitBlockingCells(unitBlockingCells))
        {
        }

        public TerrainData(IEnumerable<TerrainCellState> terrainCells)
        {
            if (terrainCells == null)
            {
                throw new ArgumentNullException(nameof(terrainCells));
            }

            var terrainByCell = new Dictionary<SurfaceCell, TerrainCellState>();

            foreach (var terrainCell in terrainCells)
            {
                terrainByCell[terrainCell.Cell] = terrainCell;
            }

            var orderedTerrainCells = new List<TerrainCellState>(terrainByCell.Values);
            orderedTerrainCells.Sort(TerrainCellStateComparer.Instance);

            var unitBlockingCells = new HashSet<Vector2Int>();
            for (var i = 0; i < orderedTerrainCells.Count; i++)
            {
                if ((orderedTerrainCells[i].Flags & TerrainFlags.BlocksGroundTraversal) == 0)
                {
                    continue;
                }

                unitBlockingCells.Add(orderedTerrainCells[i].Cell.PlanarPosition);
            }

            var orderedUnitBlockingCells = new List<Vector2Int>(unitBlockingCells);
            orderedUnitBlockingCells.Sort(Vector2IntComparer.Instance);

            _terrainByCell = terrainByCell;
            _orderedTerrainCells = new ReadOnlyCollection<TerrainCellState>(orderedTerrainCells);
            _orderedUnitBlockingCells = new ReadOnlyCollection<Vector2Int>(orderedUnitBlockingCells);
        }

        public static TerrainData Empty => EmptyTerrain;

        public bool BlocksUnitMovement(Vector2Int cell)
        {
            for (var i = 0; i < AllFaces.Length; i++)
            {
                if (TryGetTerrain(SurfaceCell.FromPlanar(cell, AllFaces[i]), out var terrainCell) &&
                    (terrainCell.Flags & TerrainFlags.BlocksGroundTraversal) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetTerrain(SurfaceCell cell, out TerrainCellState terrainCell)
        {
            return _terrainByCell.TryGetValue(cell, out terrainCell);
        }

        internal IReadOnlyList<TerrainCellState> OrderedTerrainCells => _orderedTerrainCells;

        internal IReadOnlyList<Vector2Int> OrderedUnitBlockingCells => _orderedUnitBlockingCells;

        private static IEnumerable<TerrainCellState> ExpandLegacyUnitBlockingCells(IEnumerable<Vector2Int> unitBlockingCells)
        {
            if (unitBlockingCells == null)
            {
                throw new ArgumentNullException(nameof(unitBlockingCells));
            }

            foreach (var cell in unitBlockingCells)
            {
                for (var i = 0; i < AllFaces.Length; i++)
                {
                    yield return new TerrainCellState(
                        SurfaceCell.FromPlanar(cell, AllFaces[i]),
                        TerrainKind.Generic,
                        TerrainFlags.BlocksGroundTraversal);
                }
            }
        }

        private sealed class TerrainCellStateComparer : IComparer<TerrainCellState>
        {
            internal static readonly TerrainCellStateComparer Instance = new();

            public int Compare(TerrainCellState left, TerrainCellState right)
            {
                var result = SurfaceCellComparer.Instance.Compare(left.Cell, right.Cell);
                if (result != 0)
                {
                    return result;
                }

                result = left.Kind.CompareTo(right.Kind);
                if (result != 0)
                {
                    return result;
                }

                return left.Flags.CompareTo(right.Flags);
            }
        }

        internal sealed class SurfaceCellComparer : IComparer<SurfaceCell>
        {
            internal static readonly SurfaceCellComparer Instance = new();

            public int Compare(SurfaceCell left, SurfaceCell right)
            {
                var result = left.face.CompareTo(right.face);
                if (result != 0)
                {
                    return result;
                }

                result = left.x.CompareTo(right.x);
                if (result != 0)
                {
                    return result;
                }

                return left.y.CompareTo(right.y);
            }
        }

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

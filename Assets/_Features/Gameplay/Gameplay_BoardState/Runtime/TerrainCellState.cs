using System;

namespace Game.Feature.Gameplay.BoardState
{
    [Serializable]
    public readonly struct TerrainCellState : IEquatable<TerrainCellState>
    {
        public TerrainCellState(SurfaceCell cell, TerrainKind kind, TerrainFlags flags)
        {
            Cell = cell;
            Kind = kind;
            Flags = flags;
        }

        public SurfaceCell Cell { get; }

        public TerrainKind Kind { get; }

        public TerrainFlags Flags { get; }

        public bool Equals(TerrainCellState other)
        {
            return Cell.Equals(other.Cell) &&
                   Kind == other.Kind &&
                   Flags == other.Flags;
        }

        public override bool Equals(object obj)
        {
            return obj is TerrainCellState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Cell.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ (int)Flags;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Cell}|Kind={Kind}|Flags={Flags}";
        }
    }
}

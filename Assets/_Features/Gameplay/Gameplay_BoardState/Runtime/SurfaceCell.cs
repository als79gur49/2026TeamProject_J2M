using System;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    [Serializable]
    public struct SurfaceCell : IEquatable<SurfaceCell>
    {
        public FaceId face;
        public int x;
        public int y;

        public SurfaceCell(FaceId face, int x, int y)
        {
            this.face = face;
            this.x = x;
            this.y = y;
        }

        public Vector2Int PlanarPosition => new(x, y);

        public bool Equals(SurfaceCell other)
        {
            return face == other.face && x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is SurfaceCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)face;
                hash = (hash * 397) ^ x;
                hash = (hash * 397) ^ y;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{face}({x},{y})";
        }

        public static SurfaceCell FromPlanar(Vector2Int planarCell, FaceId face = FaceId.Floor)
        {
            return new SurfaceCell(face, planarCell.x, planarCell.y);
        }

        public static implicit operator SurfaceCell(Vector2Int planarCell)
        {
            return FromPlanar(planarCell);
        }

        public static implicit operator Vector2Int(SurfaceCell cell)
        {
            return new Vector2Int(cell.x, cell.y);
        }

        public static bool operator ==(SurfaceCell left, SurfaceCell right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SurfaceCell left, SurfaceCell right)
        {
            return !left.Equals(right);
        }

        public static bool operator ==(SurfaceCell left, Vector2Int right)
        {
            return left == FromPlanar(right);
        }

        public static bool operator !=(SurfaceCell left, Vector2Int right)
        {
            return !(left == right);
        }

        public static bool operator ==(Vector2Int left, SurfaceCell right)
        {
            return FromPlanar(left) == right;
        }

        public static bool operator !=(Vector2Int left, SurfaceCell right)
        {
            return !(left == right);
        }

        public static SurfaceCell operator +(SurfaceCell cell, Vector2Int delta)
        {
            return new SurfaceCell(cell.face, cell.x + delta.x, cell.y + delta.y);
        }

        public static SurfaceCell operator -(SurfaceCell cell, Vector2Int delta)
        {
            return new SurfaceCell(cell.face, cell.x - delta.x, cell.y - delta.y);
        }

        public static Vector2Int operator -(SurfaceCell left, SurfaceCell right)
        {
            ValidatePlanarOperation(left, right);
            return new Vector2Int(left.x - right.x, left.y - right.y);
        }

        public static Vector2Int operator -(Vector2Int left, SurfaceCell right)
        {
            return left - (Vector2Int)right;
        }

        private static void ValidatePlanarOperation(SurfaceCell left, SurfaceCell right)
        {
            if (left.face != right.face)
            {
                throw new InvalidOperationException(
                    $"Cannot perform planar arithmetic between {left.face} and {right.face}.");
            }
        }
    }
}

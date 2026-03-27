using System;
using UnityEngine;

namespace Game.Feature.Gameplay.BoardState
{
    public readonly struct BoardBounds
    {
        public static BoardBounds Unbounded => default;

        public BoardBounds(Vector2Int minInclusive, Vector2Int maxInclusive)
        {
            if (maxInclusive.x < minInclusive.x)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInclusive), "Board max X must be greater than or equal to min X.");
            }

            if (maxInclusive.y < minInclusive.y)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInclusive), "Board max Y must be greater than or equal to min Y.");
            }

            IsBounded = true;
            MinInclusive = minInclusive;
            MaxInclusive = maxInclusive;
        }

        public bool IsBounded { get; }

        public Vector2Int MinInclusive { get; }

        public Vector2Int MaxInclusive { get; }

        public bool Contains(Vector2Int cell)
        {
            if (!IsBounded)
            {
                return true;
            }

            return cell.x >= MinInclusive.x &&
                   cell.x <= MaxInclusive.x &&
                   cell.y >= MinInclusive.y &&
                   cell.y <= MaxInclusive.y;
        }

        public override string ToString()
        {
            if (!IsBounded)
            {
                return "Unbounded";
            }

            return $"Min=({MinInclusive.x},{MinInclusive.y})|Max=({MaxInclusive.x},{MaxInclusive.y})";
        }
    }
}

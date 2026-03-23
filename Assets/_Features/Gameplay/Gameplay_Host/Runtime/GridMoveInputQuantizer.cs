using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public static class GridMoveInputQuantizer
    {
        public static Direction Quantize(Vector2 rawInput, float deadzone)
        {
            if (deadzone < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deadzone), "Deadzone must be non-negative.");
            }

            if (rawInput.sqrMagnitude <= deadzone * deadzone)
            {
                return Direction.None;
            }

            var absX = Mathf.Abs(rawInput.x);
            var absY = Mathf.Abs(rawInput.y);

            if (Mathf.Approximately(absX, absY))
            {
                return Direction.None;
            }

            if (absX > absY)
            {
                return rawInput.x > 0f ? Direction.Right : Direction.Left;
            }

            return rawInput.y > 0f ? Direction.Up : Direction.Down;
        }
    }
}

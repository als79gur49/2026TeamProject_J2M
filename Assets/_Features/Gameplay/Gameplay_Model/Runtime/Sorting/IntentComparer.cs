using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Model.Intents;
using UnityEngine;

namespace Game.Feature.Gameplay.Model.Sorting
{
    public sealed class IntentComparer : IComparer<Intent>
    {
        public static readonly IntentComparer Instance = new();

        public int Compare(Intent left, Intent right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var result = right.Priority.CompareTo(left.Priority);
            if (result != 0)
            {
                return result;
            }

            result = left.SourceId.CompareTo(right.SourceId);
            if (result != 0)
            {
                return result;
            }

            result = left.GetTypeSortKey().CompareTo(right.GetTypeSortKey());
            if (result != 0)
            {
                return result;
            }

            var leftHasTargetCell = left.TryGetTargetCell(out var leftTargetCell);
            var rightHasTargetCell = right.TryGetTargetCell(out var rightTargetCell);
            result = rightHasTargetCell.CompareTo(leftHasTargetCell);
            if (result != 0)
            {
                return result;
            }

            if (leftHasTargetCell && rightHasTargetCell)
            {
                result = CompareCells(leftTargetCell, rightTargetCell);
                if (result != 0)
                {
                    return result;
                }
            }

            result = left.GetLocalSequence().CompareTo(right.GetLocalSequence());
            if (result != 0)
            {
                return result;
            }

            result = StringComparer.Ordinal.Compare(left.GetType().FullName, right.GetType().FullName);
            if (result != 0)
            {
                return result;
            }

            if (left.GetType() != right.GetType())
            {
                throw new InvalidOperationException(
                    "CompareSameType requires identical runtime intent types.");
            }

            result = left.CompareSameType(right);
            if (result != 0)
            {
                return result;
            }

            return left.IntentId.CompareTo(right.IntentId);
        }

        private static int CompareCells(Vector2Int left, Vector2Int right)
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

using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.Movement.Intents;

namespace Game.Feature.Gameplay.Movement.Sorting
{
    public sealed class IntentComparer : IComparer<Intent>
    {
        public static readonly IntentComparer Instance = new();

        public int Compare(Intent left, Intent right)
        {
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

            result = ((int)left.Phase).CompareTo((int)right.Phase);
            if (result != 0)
            {
                return result;
            }

            result = left.IntentId.CompareTo(right.IntentId);
            if (result != 0)
            {
                return result;
            }

            result = CompareTypeKey(left, right);
            if (result != 0)
            {
                return result;
            }

            if (left is AttackIntent leftAttackIntent && right is AttackIntent rightAttackIntent)
            {
                result = ((int)leftAttackIntent.InputKind).CompareTo((int)rightAttackIntent.InputKind);
                if (result != 0)
                {
                    return result;
                }

                result = leftAttackIntent.LocalSequence.CompareTo(rightAttackIntent.LocalSequence);
                if (result != 0)
                {
                    return result;
                }

                result = leftAttackIntent.TargetId.CompareTo(rightAttackIntent.TargetId);
                if (result != 0)
                {
                    return result;
                }

                if (leftAttackIntent.ImpactReservation.HasValue && rightAttackIntent.ImpactReservation.HasValue)
                {
                    return ImpactReservationComparer.Instance.Compare(
                        leftAttackIntent.ImpactReservation.Value,
                        rightAttackIntent.ImpactReservation.Value);
                }
            }

            return 0;
        }

        private static int CompareTypeKey(Intent left, Intent right)
        {
            return GetTypeSortKey(left).CompareTo(GetTypeSortKey(right));
        }

        private static int GetTypeSortKey(Intent intent)
        {
            return intent switch
            {
                AttackIntent => 1,
                MoveIntent => 0,
                _ => int.MaxValue,
            };
        }
    }
}

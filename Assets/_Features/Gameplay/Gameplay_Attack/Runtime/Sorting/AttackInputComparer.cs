using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Model.Sorting;

namespace Game.Feature.Gameplay.Attack.Sorting
{
    internal sealed class AttackInputComparer : IComparer<AttackIntent>
    {
        internal static readonly AttackInputComparer Instance = new();

        public int Compare(AttackIntent left, AttackIntent right)
        {
            return IntentComparer.Instance.Compare(left, right);
        }
    }
}

using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Chase/Axis Priority", fileName = "AxisPriorityChase")]
    public sealed class AxisPriorityChaseAsset : EnemyChaseStrategyAsset
    {
        [SerializeField] private ChaseAxisPriorityMode axisPriority = ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak;
        [SerializeField] private bool trySecondaryAxisWhenBlocked = true;
        [SerializeField] private int desiredChaseDistance;

        public override ChaseStrategyKind Kind => ChaseStrategyKind.AxisPriority;

        public override ChaseSettings Settings => new(
            axisPriority,
            trySecondaryAxisWhenBlocked,
            desiredChaseDistance);

        protected override IChaseStrategy ResolveStrategy()
        {
            return AxisPriorityChaseStrategy.Instance;
        }
    }
}

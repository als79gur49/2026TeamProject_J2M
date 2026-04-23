using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Patrol/Random Walk", fileName = "RandomWalkPatrol")]
    public sealed class RandomWalkPatrolAsset : EnemyPatrolStrategyAsset
    {
        [SerializeField] private int leashRadius = 2;
        [SerializeField] private int forwardWeight = 4;
        [SerializeField] private int sideWeight = 2;
        [SerializeField] private int backwardWeight = 1;
        [SerializeField] private bool preventImmediateBacktrack = true;

        public override PatrolStrategyKind Kind => PatrolStrategyKind.RandomWalk;

        public override PatrolSettings Settings => new(
            PatrolBlockedMovementResponse.Stop,
            leashRadius: leashRadius,
            forwardWeight: forwardWeight,
            sideWeight: sideWeight,
            backwardWeight: backwardWeight,
            preventImmediateBacktrack: preventImmediateBacktrack);

        protected override IPatrolStrategy ResolveStrategy()
        {
            return RandomWalkPatrolStrategy.Instance;
        }
    }
}

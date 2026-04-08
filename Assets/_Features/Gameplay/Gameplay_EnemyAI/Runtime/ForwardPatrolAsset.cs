using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Patrol/Forward", fileName = "ForwardPatrol")]
    public sealed class ForwardPatrolAsset : EnemyPatrolStrategyAsset
    {
        [SerializeField] private PatrolBlockedMovementResponse blockedMovementResponse = PatrolBlockedMovementResponse.Stop;

        public override PatrolStrategyKind Kind => PatrolStrategyKind.Forward;

        public override PatrolSettings Settings => new(blockedMovementResponse);

        protected override IPatrolStrategy ResolveStrategy()
        {
            return ForwardPatrolStrategy.Instance;
        }
    }
}

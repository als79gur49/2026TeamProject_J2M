using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Patrol/Stationary", fileName = "StationaryPatrol")]
    public sealed class StationaryPatrolAsset : EnemyPatrolStrategyAsset
    {
        public override PatrolStrategyKind Kind => PatrolStrategyKind.Stationary;

        public override PatrolSettings Settings => PatrolSettings.CreateDefault();

        protected override IPatrolStrategy ResolveStrategy()
        {
            return StationaryPatrolStrategy.Instance;
        }
    }
}

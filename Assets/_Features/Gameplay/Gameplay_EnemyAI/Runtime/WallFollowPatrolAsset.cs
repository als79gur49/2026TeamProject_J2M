using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Patrol/Wall Follow", fileName = "WallFollowPatrol")]
    public sealed class WallFollowPatrolAsset : EnemyPatrolStrategyAsset
    {
        [SerializeField] private PatrolBlockedMovementResponse blockedMovementResponse = PatrolBlockedMovementResponse.Stop;
        [SerializeField] private WallFollowTurnPreference turnPreference = WallFollowTurnPreference.Right;
        [SerializeField] private bool followWalls = true;
        [SerializeField] private bool followBoxes = true;

        public override PatrolStrategyKind Kind => PatrolStrategyKind.WallFollow;

        public override PatrolSettings Settings => new(
            blockedMovementResponse,
            turnPreference,
            followWalls,
            followBoxes);

        protected override IPatrolStrategy ResolveStrategy()
        {
            return WallFollowPatrolStrategy.Instance;
        }
    }
}

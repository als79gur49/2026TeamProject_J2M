using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Detection/Cross Line Of Sight Opponent", fileName = "CrossLineOfSightOpponentDetection")]
    public sealed class CrossLineOfSightOpponentDetectionAsset : EnemyDetectionStrategyAsset
    {
        [SerializeField] private int senseRange = 8;
        [SerializeField] private bool requireSameFace = true;
        [SerializeField] private bool canTargetMarkedForDeath;

        public override DetectionStrategyKind Kind => DetectionStrategyKind.CrossLineOfSightOpponent;

        public override DetectionSettings Settings => new(
            senseRange,
            requireSameFace,
            canTargetMarkedForDeath);

        protected override IDetectionStrategy ResolveStrategy()
        {
            return CrossLineOfSightOpponentDetectionStrategy.Instance;
        }
    }
}

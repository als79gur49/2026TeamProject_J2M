using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Detection/Nearest Opponent", fileName = "NearestOpponentDetection")]
    public sealed class NearestOpponentDetectionAsset : EnemyDetectionStrategyAsset
    {
        [SerializeField] private int senseRange = 8;
        [SerializeField] private bool requireSameFace = true;
        [SerializeField] private bool canTargetMarkedForDeath;

        public override DetectionStrategyKind Kind => DetectionStrategyKind.NearestOpponent;

        public override DetectionSettings Settings => new(
            senseRange,
            requireSameFace,
            canTargetMarkedForDeath);

        protected override IDetectionStrategy ResolveStrategy()
        {
            return NearestOpponentDetectionStrategy.Instance;
        }
    }
}

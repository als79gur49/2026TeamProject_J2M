using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Detection/None", fileName = "NoDetection")]
    public sealed class NoDetectionStrategyAsset : EnemyDetectionStrategyAsset
    {
        public override DetectionStrategyKind Kind => DetectionStrategyKind.None;

        public override DetectionSettings Settings => DetectionSettings.CreateDefaultMelee();

        protected override IDetectionStrategy ResolveStrategy()
        {
            return NoDetectionStrategy.Instance;
        }
    }
}

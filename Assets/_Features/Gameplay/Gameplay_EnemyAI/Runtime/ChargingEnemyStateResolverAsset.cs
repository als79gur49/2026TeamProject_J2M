using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Resolvers/Charging", fileName = "ChargingEnemyStateResolver")]
    public sealed class ChargingEnemyStateResolverAsset : EnemyStateResolverAsset
    {
        public override EnemyAiStateResolverKind Kind => EnemyAiStateResolverKind.Charge;

        protected override IEnemyAiStateResolver ResolveResolver()
        {
            return ChargingEnemyAiStateResolver.Instance;
        }
    }
}

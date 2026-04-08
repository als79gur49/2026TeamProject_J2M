using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Resolvers/Default", fileName = "DefaultEnemyStateResolver")]
    public sealed class DefaultEnemyStateResolverAsset : EnemyStateResolverAsset
    {
        public override EnemyAiStateResolverKind Kind => EnemyAiStateResolverKind.Default;

        protected override IEnemyAiStateResolver ResolveResolver()
        {
            return DefaultEnemyAiStateResolver.Instance;
        }
    }
}

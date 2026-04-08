using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Capabilities/Combat/Contact Damage", fileName = "ContactDamageCapability")]
    public sealed class ContactDamageCapabilityAsset : EnemyCombatCapabilityAsset
    {
        public override AttackDecisionStrategyKind Kind => AttackDecisionStrategyKind.ContactSameCell;

        public override AttackDecisionSettings AttackDecisionSettings => global::Game.Feature.Gameplay.Entities.AttackDecisionSettings.CreateDefaultMelee();

        public override EnemyAttackTimingAuthoringSettings AttackTimingSettings => global::Game.Feature.Gameplay.Entities.EnemyAttackTimingAuthoringSettings.CreateDefaultMelee();

        protected override IAttackDecisionStrategy ResolveStrategy()
        {
            return ContactSameCellAttackDecisionStrategy.Instance;
        }
    }
}

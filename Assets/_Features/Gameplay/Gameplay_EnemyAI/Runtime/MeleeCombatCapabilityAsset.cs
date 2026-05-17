using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(menuName = "Gameplay/AI/Capabilities/Combat/Melee", fileName = "MeleeCombatCapability")]
    public sealed class MeleeCombatCapabilityAsset : EnemyCombatCapabilityAsset
    {
        [SerializeField] private AttackDecisionSettings attackDecisionSettings = new(1);
        [SerializeField] private EnemyAttackTimingAuthoringSettings attackTimingSettings = new(0f);
        [SerializeField] private WindupMeleeSettings windupMeleeSettings = new(WindupMeleeSettings.DefaultVisualRangeSlackCells);

        public override AttackDecisionStrategyKind Kind => AttackDecisionStrategyKind.Melee;

        public override AttackDecisionSettings AttackDecisionSettings => attackDecisionSettings;

        public override EnemyAttackTimingAuthoringSettings AttackTimingSettings => attackTimingSettings;

        public override WindupMeleeSettings WindupMeleeSettings => windupMeleeSettings;

        protected override IAttackDecisionStrategy ResolveStrategy()
        {
            return MeleeAttackDecisionStrategy.Instance;
        }
    }
}

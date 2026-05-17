using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(
        menuName = "Gameplay/AI/Capabilities/Combat/Windup Forward Cell Projectile",
        fileName = "WindupForwardCellProjectileCapability")]
    public sealed class WindupForwardCellProjectileCapabilityAsset : EnemyCombatCapabilityAsset
    {
        [SerializeField] private AttackDecisionSettings attackDecisionSettings = new(1);
        [SerializeField] private EnemyAttackTimingAuthoringSettings attackTimingSettings = new(0f);
        [SerializeField] private WindupForwardCellProjectileSettings windupForwardCellProjectileSettings =
            WindupForwardCellProjectileSettings.CreateDefault();

        public override AttackDecisionStrategyKind Kind => AttackDecisionStrategyKind.WindupForwardCellProjectile;

        public override AttackDecisionSettings AttackDecisionSettings => attackDecisionSettings;

        public override EnemyAttackTimingAuthoringSettings AttackTimingSettings => attackTimingSettings;

        public override WindupForwardCellProjectileSettings WindupForwardCellProjectileSettings =>
            windupForwardCellProjectileSettings;

        protected override IAttackDecisionStrategy ResolveStrategy()
        {
            return WindupForwardCellProjectileAttackDecisionStrategy.Instance;
        }
    }
}

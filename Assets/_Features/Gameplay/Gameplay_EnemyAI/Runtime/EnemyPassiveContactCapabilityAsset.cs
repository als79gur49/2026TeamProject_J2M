using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(
        menuName = "Gameplay/AI/Capabilities/Passive Contact/Same Cell Damage",
        fileName = "EnemyPassiveContactCapability")]
    public class EnemyPassiveContactCapabilityAsset : EnemyCapabilityAsset
    {
        [SerializeField] private AttackDecisionSettings attackDecisionSettings = new(1);

        public sealed override EnemyCapabilityFamily Family => EnemyCapabilityFamily.PassiveContact;

        public virtual AttackDecisionStrategyKind Kind => AttackDecisionStrategyKind.ContactSameCell;

        public virtual AttackDecisionSettings AttackDecisionSettings => attackDecisionSettings;

        internal sealed override EnemyCapabilityRuntime Compile(int simulationTicksPerSecond)
        {
            return new EnemyPassiveContactCapabilityRuntime(
                Kind,
                AttackDecisionSettings,
                ContactSameCellAttackDecisionStrategy.Instance);
        }
    }
}

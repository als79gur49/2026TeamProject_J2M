using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    public abstract class EnemyStateResolverAsset : ScriptableObject
    {
        public abstract EnemyAiStateResolverKind Kind { get; }

        internal EnemyStateResolverRuntime Compile()
        {
            return new EnemyStateResolverRuntime(Kind, ResolveResolver());
        }

        protected abstract IEnemyAiStateResolver ResolveResolver();
    }

    public abstract class EnemyPatrolStrategyAsset : ScriptableObject
    {
        public abstract PatrolStrategyKind Kind { get; }

        public abstract PatrolSettings Settings { get; }

        internal EnemyPatrolRuntime Compile()
        {
            return new EnemyPatrolRuntime(Kind, Settings, ResolveStrategy());
        }

        protected abstract IPatrolStrategy ResolveStrategy();
    }

    public abstract class EnemyDetectionStrategyAsset : ScriptableObject
    {
        public abstract DetectionStrategyKind Kind { get; }

        public abstract DetectionSettings Settings { get; }

        internal EnemyDetectionRuntime Compile()
        {
            return new EnemyDetectionRuntime(Kind, Settings, ResolveStrategy());
        }

        protected abstract IDetectionStrategy ResolveStrategy();
    }

    public abstract class EnemyChaseStrategyAsset : ScriptableObject
    {
        public abstract ChaseStrategyKind Kind { get; }

        public abstract ChaseSettings Settings { get; }

        internal EnemyChaseRuntime Compile()
        {
            return new EnemyChaseRuntime(Kind, Settings, ResolveStrategy());
        }

        protected abstract IChaseStrategy ResolveStrategy();
    }

    public abstract class EnemyCapabilityAsset : ScriptableObject
    {
        public abstract EnemyCapabilityFamily Family { get; }

        internal abstract EnemyCapabilityRuntime Compile(int simulationTicksPerSecond);
    }

    public abstract class EnemyCombatCapabilityAsset : EnemyCapabilityAsset
    {
        public sealed override EnemyCapabilityFamily Family => EnemyCapabilityFamily.Combat;

        public abstract AttackDecisionStrategyKind Kind { get; }

        public abstract AttackDecisionSettings AttackDecisionSettings { get; }

        public abstract EnemyAttackTimingAuthoringSettings AttackTimingSettings { get; }

        internal sealed override EnemyCapabilityRuntime Compile(int simulationTicksPerSecond)
        {
            return new EnemyCombatCapabilityRuntime(
                Kind,
                AttackDecisionSettings,
                AttackTimingSettings.ToRuntimeSettings(simulationTicksPerSecond),
                ResolveStrategy());
        }

        protected abstract IAttackDecisionStrategy ResolveStrategy();
    }

    public abstract class EnemyMovementSkillCapabilityAsset : EnemyCapabilityAsset
    {
        public sealed override EnemyCapabilityFamily Family => EnemyCapabilityFamily.MovementSkill;

        public abstract MovementSkillStrategyKind Kind { get; }

        public abstract EnemyJumpTimingAuthoringSettings JumpTimingSettings { get; }

        internal sealed override EnemyCapabilityRuntime Compile(int simulationTicksPerSecond)
        {
            return new EnemyMovementSkillCapabilityRuntime(
                Kind,
                JumpTimingSettings.ToRuntimeSettings(simulationTicksPerSecond));
        }
    }
}

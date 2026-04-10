using System;

namespace Game.Feature.Gameplay.Entities
{
    public enum EnemyCapabilityFamily
    {
        Combat = 0,
        MovementSkill = 1,
        PassiveContact = 2,
    }

    public readonly struct EnemyCoreRuntime
    {
        public EnemyCoreRuntime(
            EnemyAiCommonSettings commonSettings,
            EnemyLocomotionTimingSettings locomotionTimingSettings)
        {
            CommonSettings = commonSettings;
            LocomotionTimingSettings = locomotionTimingSettings;
            Validate(nameof(EnemyCoreRuntime));
        }

        public EnemyAiCommonSettings CommonSettings { get; }

        public EnemyLocomotionTimingSettings LocomotionTimingSettings { get; }

        public void Validate(string paramName)
        {
            CommonSettings.Validate(paramName);
            LocomotionTimingSettings.Validate(paramName);
        }
    }

    public readonly struct EnemyStateResolverRuntime
    {
        public EnemyStateResolverRuntime(
            EnemyAiStateResolverKind kind,
            IEnemyAiStateResolver resolver)
        {
            Kind = kind;
            Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public EnemyAiStateResolverKind Kind { get; }

        public IEnemyAiStateResolver Resolver { get; }
    }

    public readonly struct EnemyPatrolRuntime
    {
        public EnemyPatrolRuntime(
            PatrolStrategyKind kind,
            PatrolSettings settings,
            IPatrolStrategy strategy)
        {
            Kind = kind;
            Settings = settings;
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        public PatrolStrategyKind Kind { get; }

        public PatrolSettings Settings { get; }

        public IPatrolStrategy Strategy { get; }
    }

    public readonly struct EnemyDetectionRuntime
    {
        public EnemyDetectionRuntime(
            DetectionStrategyKind kind,
            DetectionSettings settings,
            IDetectionStrategy strategy)
        {
            Kind = kind;
            Settings = settings;
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        public DetectionStrategyKind Kind { get; }

        public DetectionSettings Settings { get; }

        public IDetectionStrategy Strategy { get; }

        public void Validate(string paramName)
        {
            if (Kind != DetectionStrategyKind.None)
            {
                Settings.Validate(paramName);
            }
        }
    }

    public readonly struct EnemyChaseRuntime
    {
        public EnemyChaseRuntime(
            ChaseStrategyKind kind,
            ChaseSettings settings,
            IChaseStrategy strategy)
        {
            Kind = kind;
            Settings = settings;
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        public ChaseStrategyKind Kind { get; }

        public ChaseSettings Settings { get; }

        public IChaseStrategy Strategy { get; }

        public void Validate(string paramName)
        {
            Settings.Validate(paramName);
        }
    }

    public readonly struct EnemyBrainRuntime
    {
        public EnemyBrainRuntime(
            EnemyStateResolverRuntime stateResolver,
            EnemyPatrolRuntime patrol,
            EnemyDetectionRuntime detection,
            EnemyChaseRuntime chase)
        {
            StateResolver = stateResolver;
            Patrol = patrol;
            Detection = detection;
            Chase = chase;
            Validate(nameof(EnemyBrainRuntime));
        }

        public EnemyStateResolverRuntime StateResolver { get; }

        public EnemyPatrolRuntime Patrol { get; }

        public EnemyDetectionRuntime Detection { get; }

        public EnemyChaseRuntime Chase { get; }

        public void Validate(string paramName)
        {
            if (StateResolver.Resolver == null ||
                Patrol.Strategy == null ||
                Detection.Strategy == null ||
                Chase.Strategy == null)
            {
                throw new ArgumentException("Enemy brain runtime requires non-null strategy slots.", paramName);
            }

            Detection.Validate(paramName);
            Chase.Validate(paramName);
        }
    }

    public abstract class EnemyCapabilityRuntime
    {
        public abstract EnemyCapabilityFamily Family { get; }

        public abstract void Validate(string paramName);
    }

    public sealed class EnemyCombatCapabilityRuntime : EnemyCapabilityRuntime
    {
        public EnemyCombatCapabilityRuntime(
            AttackDecisionStrategyKind kind,
            AttackDecisionSettings attackDecisionSettings,
            EnemyAttackTimingSettings attackTimingSettings,
            IAttackDecisionStrategy attackDecisionStrategy)
        {
            if (kind == AttackDecisionStrategyKind.None)
            {
                throw new ArgumentException("Combat capability runtime requires a concrete combat kind.", nameof(kind));
            }

            if (kind == AttackDecisionStrategyKind.ContactSameCell)
            {
                throw new ArgumentException(
                    "ContactSameCell must compile as passive contact, not as a combat capability.",
                    nameof(kind));
            }

            Kind = kind;
            AttackDecisionSettings = attackDecisionSettings;
            AttackTimingSettings = attackTimingSettings;
            AttackDecisionStrategy = attackDecisionStrategy ?? throw new ArgumentNullException(nameof(attackDecisionStrategy));
            Validate(nameof(EnemyCombatCapabilityRuntime));
        }

        public override EnemyCapabilityFamily Family => EnemyCapabilityFamily.Combat;

        public AttackDecisionStrategyKind Kind { get; }

        public AttackDecisionSettings AttackDecisionSettings { get; }

        public EnemyAttackTimingSettings AttackTimingSettings { get; }

        public IAttackDecisionStrategy AttackDecisionStrategy { get; }

        public override void Validate(string paramName)
        {
            AttackDecisionSettings.Validate(paramName);
            AttackTimingSettings.Validate(paramName);
        }
    }

    public sealed class EnemyMovementSkillCapabilityRuntime : EnemyCapabilityRuntime
    {
        public EnemyMovementSkillCapabilityRuntime(
            MovementSkillStrategyKind kind,
            EnemyJumpTimingSettings jumpTimingSettings)
        {
            if (kind == MovementSkillStrategyKind.None)
            {
                throw new ArgumentException("Movement skill runtime requires a concrete movement skill kind.", nameof(kind));
            }

            Kind = kind;
            JumpTimingSettings = jumpTimingSettings;
            Validate(nameof(EnemyMovementSkillCapabilityRuntime));
        }

        public override EnemyCapabilityFamily Family => EnemyCapabilityFamily.MovementSkill;

        public MovementSkillStrategyKind Kind { get; }

        public EnemyJumpTimingSettings JumpTimingSettings { get; }

        public override void Validate(string paramName)
        {
            JumpTimingSettings.Validate(paramName);
        }
    }

    public sealed class EnemyPassiveContactCapabilityRuntime : EnemyCapabilityRuntime
    {
        public EnemyPassiveContactCapabilityRuntime(
            AttackDecisionStrategyKind kind,
            AttackDecisionSettings attackDecisionSettings,
            IAttackDecisionStrategy attackDecisionStrategy)
        {
            if (kind != AttackDecisionStrategyKind.ContactSameCell)
            {
                throw new ArgumentException(
                    "Passive contact runtime requires ContactSameCell semantics.",
                    nameof(kind));
            }

            Kind = kind;
            AttackDecisionSettings = attackDecisionSettings;
            AttackDecisionStrategy = attackDecisionStrategy ?? throw new ArgumentNullException(nameof(attackDecisionStrategy));
            Validate(nameof(EnemyPassiveContactCapabilityRuntime));
        }

        public override EnemyCapabilityFamily Family => EnemyCapabilityFamily.PassiveContact;

        public AttackDecisionStrategyKind Kind { get; }

        public AttackDecisionSettings AttackDecisionSettings { get; }

        public IAttackDecisionStrategy AttackDecisionStrategy { get; }

        public override void Validate(string paramName)
        {
            AttackDecisionSettings.Validate(paramName);
        }
    }

    public readonly struct EnemyCapabilityRuntimeSet
    {
        public EnemyCapabilityRuntimeSet(
            EnemyCombatCapabilityRuntime combat,
            EnemyMovementSkillCapabilityRuntime movementSkill,
            EnemyPassiveContactCapabilityRuntime passiveContact)
        {
            Combat = combat;
            MovementSkill = movementSkill;
            PassiveContact = passiveContact;
            Validate(nameof(EnemyCapabilityRuntimeSet));
        }

        public EnemyCombatCapabilityRuntime Combat { get; }

        public EnemyMovementSkillCapabilityRuntime MovementSkill { get; }

        public EnemyPassiveContactCapabilityRuntime PassiveContact { get; }

        public bool TryGetCombat(out EnemyCombatCapabilityRuntime combat)
        {
            combat = Combat;
            return combat != null;
        }

        public bool TryGetMovementSkill(out EnemyMovementSkillCapabilityRuntime movementSkill)
        {
            movementSkill = MovementSkill;
            return movementSkill != null;
        }

        public bool TryGetPassiveContact(out EnemyPassiveContactCapabilityRuntime passiveContact)
        {
            passiveContact = PassiveContact;
            return passiveContact != null;
        }

        public void Validate(string paramName)
        {
            Combat?.Validate(paramName);
            MovementSkill?.Validate(paramName);
            PassiveContact?.Validate(paramName);
        }
    }
}

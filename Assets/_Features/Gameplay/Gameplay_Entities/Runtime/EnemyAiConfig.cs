using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    public enum EnemyAiStateResolverKind
    {
        Default = 0,
        Charge = 1,
    }

    public enum PatrolStrategyKind
    {
        Forward = 0,
    }

    public enum DetectionStrategyKind
    {
        NearestOpponent = 0,
    }

    public enum ChaseStrategyKind
    {
        AxisPriority = 0,
    }

    public enum AttackDecisionStrategyKind
    {
        Melee = 0,
        None = 1,
    }

    [Serializable]
    public struct EnemyAiCommonSettings
    {
        [SerializeField] private int movementPriority;
        [SerializeField] private int attackPriority;
        [SerializeField] private int recoverTicks;

        public EnemyAiCommonSettings(
            int movementPriority,
            int attackPriority,
            int recoverTicks)
        {
            this.movementPriority = movementPriority;
            this.attackPriority = attackPriority;
            this.recoverTicks = recoverTicks;
        }

        public int MovementPriority => movementPriority;

        public int AttackPriority => attackPriority;

        public int RecoverTicks => recoverTicks;

        public void Validate(string paramName)
        {
            if (recoverTicks < 0)
            {
                throw new ArgumentException("Enemy AI common settings require a non-negative recover tick count.", paramName);
            }
        }

        public static EnemyAiCommonSettings CreateDefaultMelee()
        {
            return new EnemyAiCommonSettings(
                movementPriority: 50,
                attackPriority: 50,
                recoverTicks: 1);
        }
    }

    [Serializable]
    public struct EnemyAttackTimingSettings
    {
        [SerializeField] private int windupTicks;

        public EnemyAttackTimingSettings(int windupTicks)
        {
            this.windupTicks = windupTicks;
        }

        public int WindupTicks => windupTicks;

        public void Validate(string paramName)
        {
            if (windupTicks < 0)
            {
                throw new ArgumentException("Enemy attack timing settings require a non-negative wind-up tick count.", paramName);
            }
        }

        public static EnemyAttackTimingSettings CreateDefaultMelee()
        {
            return new EnemyAttackTimingSettings(windupTicks: 0);
        }
    }

    public readonly struct EnemyAiRuntimeDefinition
    {
        public EnemyAiRuntimeDefinition(
            EnemyAiCommonSettings commonSettings,
            PatrolSettings patrolSettings,
            DetectionSettings detectionSettings,
            ChaseSettings chaseSettings,
            AttackDecisionSettings attackDecisionSettings,
            EnemyAttackTimingSettings attackTimingSettings,
            IPatrolStrategy patrolStrategy,
            IDetectionStrategy detectionStrategy,
            IChaseStrategy chaseStrategy,
            IAttackDecisionStrategy attackDecisionStrategy,
            IEnemyAiStateResolver stateResolver)
        {
            CommonSettings = commonSettings;
            PatrolSettings = patrolSettings;
            DetectionSettings = detectionSettings;
            ChaseSettings = chaseSettings;
            AttackDecisionSettings = attackDecisionSettings;
            AttackTimingSettings = attackTimingSettings;
            PatrolStrategy = patrolStrategy;
            DetectionStrategy = detectionStrategy;
            ChaseStrategy = chaseStrategy;
            AttackDecisionStrategy = attackDecisionStrategy;
            StateResolver = stateResolver;

            Validate(nameof(EnemyAiRuntimeDefinition));
        }

        public EnemyAiCommonSettings CommonSettings { get; }

        public PatrolSettings PatrolSettings { get; }

        public DetectionSettings DetectionSettings { get; }

        public ChaseSettings ChaseSettings { get; }

        public AttackDecisionSettings AttackDecisionSettings { get; }

        public EnemyAttackTimingSettings AttackTimingSettings { get; }

        public IPatrolStrategy PatrolStrategy { get; }

        public IDetectionStrategy DetectionStrategy { get; }

        public IChaseStrategy ChaseStrategy { get; }

        public IAttackDecisionStrategy AttackDecisionStrategy { get; }

        public IEnemyAiStateResolver StateResolver { get; }

        public void Validate(string paramName)
        {
            CommonSettings.Validate(paramName);
            DetectionSettings.Validate(paramName);
            AttackDecisionSettings.Validate(paramName);
            AttackTimingSettings.Validate(paramName);

            if (PatrolStrategy == null ||
                DetectionStrategy == null ||
                ChaseStrategy == null ||
                AttackDecisionStrategy == null ||
                StateResolver == null)
            {
                throw new ArgumentException("Enemy AI runtime definitions require non-null strategies and state resolvers.", paramName);
            }
        }

        public static EnemyAiRuntimeDefinition CreateDefaultMelee()
        {
            return new EnemyAiRuntimeDefinition(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                EnemyAttackTimingSettings.CreateDefaultMelee(),
                ForwardPatrolStrategy.Instance,
                NearestOpponentDetectionStrategy.Instance,
                AxisPriorityChaseStrategy.Instance,
                MeleeAttackDecisionStrategy.Instance,
                DefaultEnemyAiStateResolver.Instance);
        }

        internal static EnemyAiRuntimeDefinition CreateFromProfile(EnemyAiProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return new EnemyAiRuntimeDefinition(
                profile.CommonSettings,
                profile.PatrolSettings,
                profile.DetectionSettings,
                profile.ChaseSettings,
                profile.AttackDecisionSettings,
                profile.AttackTimingSettings,
                ResolvePatrolStrategy(profile.PatrolStrategyKind),
                ResolveDetectionStrategy(profile.DetectionStrategyKind),
                ResolveChaseStrategy(profile.ChaseStrategyKind),
                ResolveAttackDecisionStrategy(profile.AttackDecisionStrategyKind),
                ResolveStateResolver(profile.StateResolverKind));
        }

        private static IPatrolStrategy ResolvePatrolStrategy(PatrolStrategyKind kind)
        {
            switch (kind)
            {
                case PatrolStrategyKind.Forward:
                    return ForwardPatrolStrategy.Instance;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown patrol strategy kind.");
            }
        }

        private static IDetectionStrategy ResolveDetectionStrategy(DetectionStrategyKind kind)
        {
            switch (kind)
            {
                case DetectionStrategyKind.NearestOpponent:
                    return NearestOpponentDetectionStrategy.Instance;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown detection strategy kind.");
            }
        }

        private static IChaseStrategy ResolveChaseStrategy(ChaseStrategyKind kind)
        {
            switch (kind)
            {
                case ChaseStrategyKind.AxisPriority:
                    return AxisPriorityChaseStrategy.Instance;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown chase strategy kind.");
            }
        }

        private static IAttackDecisionStrategy ResolveAttackDecisionStrategy(AttackDecisionStrategyKind kind)
        {
            switch (kind)
            {
                case AttackDecisionStrategyKind.Melee:
                    return MeleeAttackDecisionStrategy.Instance;

                case AttackDecisionStrategyKind.None:
                    return NoAttackDecisionStrategy.Instance;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown attack decision strategy kind.");
            }
        }

        private static IEnemyAiStateResolver ResolveStateResolver(EnemyAiStateResolverKind kind)
        {
            switch (kind)
            {
                case EnemyAiStateResolverKind.Default:
                    return DefaultEnemyAiStateResolver.Instance;

                case EnemyAiStateResolverKind.Charge:
                    return ChargingEnemyAiStateResolver.Instance;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown enemy AI state resolver kind.");
            }
        }
    }

    [Obsolete("Use EnemyAiProfile or EnemyAiRuntimeDefinition instead.")]
    public readonly struct EnemyAiConfig
    {
        public EnemyAiConfig(
            int senseRange,
            int attackRange,
            int movementPriority,
            int attackPriority,
            int recoverTicks)
        {
            if (senseRange <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(senseRange), "Enemy AI sense range must be positive.");
            }

            if (attackRange <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackRange), "Enemy AI attack range must be positive.");
            }

            if (recoverTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(recoverTicks), "Enemy AI recover ticks cannot be negative.");
            }

            SenseRange = senseRange;
            AttackRange = attackRange;
            MovementPriority = movementPriority;
            AttackPriority = attackPriority;
            RecoverTicks = recoverTicks;
        }

        public int SenseRange { get; }

        public int AttackRange { get; }

        public int MovementPriority { get; }

        public int AttackPriority { get; }

        public int RecoverTicks { get; }

        public EnemyAiRuntimeDefinition ToRuntimeDefinition()
        {
            return new EnemyAiRuntimeDefinition(
                new EnemyAiCommonSettings(MovementPriority, AttackPriority, RecoverTicks),
                PatrolSettings.CreateDefault(),
                new DetectionSettings(SenseRange, requireSameFace: true, canTargetMarkedForDeath: false),
                ChaseSettings.CreateDefault(),
                new AttackDecisionSettings(AttackRange),
                EnemyAttackTimingSettings.CreateDefaultMelee(),
                ForwardPatrolStrategy.Instance,
                NearestOpponentDetectionStrategy.Instance,
                AxisPriorityChaseStrategy.Instance,
                MeleeAttackDecisionStrategy.Instance,
                DefaultEnemyAiStateResolver.Instance);
        }

        public static EnemyAiConfig CreateDefaultMelee()
        {
            return new EnemyAiConfig(
                senseRange: 8,
                attackRange: 1,
                movementPriority: 50,
                attackPriority: 50,
                recoverTicks: 1);
        }
    }
}

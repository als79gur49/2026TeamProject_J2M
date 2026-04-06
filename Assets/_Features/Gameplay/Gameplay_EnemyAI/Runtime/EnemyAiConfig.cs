using System;
using Game.Feature.Gameplay.Loop;
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
        ContactSameCell = 2,
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
    public struct EnemyAiCommonAuthoringSettings
    {
        [SerializeField] private int movementPriority;
        [SerializeField] private int attackPriority;
        [SerializeField] private float recoverSeconds;

        public EnemyAiCommonAuthoringSettings(
            int movementPriority,
            int attackPriority,
            float recoverSeconds)
        {
            this.movementPriority = movementPriority;
            this.attackPriority = attackPriority;
            this.recoverSeconds = recoverSeconds;
        }

        public int MovementPriority => movementPriority;

        public int AttackPriority => attackPriority;

        public float RecoverSeconds => recoverSeconds;

        public void Validate(string paramName)
        {
            if (recoverSeconds < 0f)
            {
                throw new ArgumentException("Enemy AI common authoring settings require a non-negative recover duration.", paramName);
            }
        }

        public EnemyAiCommonSettings ToRuntimeSettings(int simulationTicksPerSecond)
        {
            Validate(nameof(EnemyAiCommonAuthoringSettings));

            return new EnemyAiCommonSettings(
                movementPriority,
                attackPriority,
                GameplayTimingProfile.SecondsToTicks(
                    recoverSeconds,
                    simulationTicksPerSecond,
                    allowZero: true));
        }

        public static EnemyAiCommonAuthoringSettings CreateDefaultMelee()
        {
            return FromRuntimeSettings(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        public static EnemyAiCommonAuthoringSettings FromRuntimeSettings(
            EnemyAiCommonSettings runtimeSettings,
            int simulationTicksPerSecond)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            return new EnemyAiCommonAuthoringSettings(
                runtimeSettings.MovementPriority,
                runtimeSettings.AttackPriority,
                runtimeSettings.RecoverTicks / (float)simulationTicksPerSecond);
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

    [Serializable]
    public struct EnemyAttackTimingAuthoringSettings
    {
        [SerializeField] private float windupSeconds;

        public EnemyAttackTimingAuthoringSettings(float windupSeconds)
        {
            this.windupSeconds = windupSeconds;
        }

        public float WindupSeconds => windupSeconds;

        public void Validate(string paramName)
        {
            if (windupSeconds < 0f)
            {
                throw new ArgumentException("Enemy attack timing authoring settings require a non-negative wind-up duration.", paramName);
            }
        }

        public EnemyAttackTimingSettings ToRuntimeSettings(int simulationTicksPerSecond)
        {
            Validate(nameof(EnemyAttackTimingAuthoringSettings));

            return new EnemyAttackTimingSettings(
                GameplayTimingProfile.SecondsToTicks(
                    windupSeconds,
                    simulationTicksPerSecond,
                    allowZero: true));
        }

        public static EnemyAttackTimingAuthoringSettings CreateDefaultMelee()
        {
            return FromRuntimeSettings(
                EnemyAttackTimingSettings.CreateDefaultMelee(),
                GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        public static EnemyAttackTimingAuthoringSettings FromRuntimeSettings(
            EnemyAttackTimingSettings runtimeSettings,
            int simulationTicksPerSecond)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            return new EnemyAttackTimingAuthoringSettings(
                runtimeSettings.WindupTicks / (float)simulationTicksPerSecond);
        }
    }

    [Serializable]
    public struct EnemyLocomotionTimingSettings
    {
        [SerializeField] private int moveCooldownTicks;

        public EnemyLocomotionTimingSettings(int moveCooldownTicks)
        {
            this.moveCooldownTicks = moveCooldownTicks;
        }

        public int MoveCooldownTicks => moveCooldownTicks;

        public void Validate(string paramName)
        {
            if (moveCooldownTicks < 0)
            {
                throw new ArgumentException("Enemy locomotion timing settings require a non-negative move cooldown tick count.", paramName);
            }
        }

        public static EnemyLocomotionTimingSettings CreateDefaultMelee()
        {
            return new EnemyLocomotionTimingSettings(moveCooldownTicks: 0);
        }
    }

    [Serializable]
    public struct EnemyLocomotionTimingAuthoringSettings
    {
        [SerializeField] private float moveCooldownSeconds;

        public EnemyLocomotionTimingAuthoringSettings(float moveCooldownSeconds)
        {
            this.moveCooldownSeconds = moveCooldownSeconds;
        }

        public float MoveCooldownSeconds => moveCooldownSeconds;

        public void Validate(string paramName)
        {
            if (moveCooldownSeconds < 0f)
            {
                throw new ArgumentException("Enemy locomotion timing authoring settings require a non-negative move cooldown duration.", paramName);
            }
        }

        public EnemyLocomotionTimingSettings ToRuntimeSettings(int simulationTicksPerSecond)
        {
            Validate(nameof(EnemyLocomotionTimingAuthoringSettings));

            return new EnemyLocomotionTimingSettings(
                GameplayTimingProfile.SecondsToTicks(
                    moveCooldownSeconds,
                    simulationTicksPerSecond,
                    allowZero: true));
        }

        public static EnemyLocomotionTimingAuthoringSettings CreateDefaultMelee()
        {
            return FromRuntimeSettings(
                EnemyLocomotionTimingSettings.CreateDefaultMelee(),
                GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        public static EnemyLocomotionTimingAuthoringSettings FromRuntimeSettings(
            EnemyLocomotionTimingSettings runtimeSettings,
            int simulationTicksPerSecond)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            return new EnemyLocomotionTimingAuthoringSettings(
                runtimeSettings.MoveCooldownTicks / (float)simulationTicksPerSecond);
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
            : this(
                commonSettings,
                patrolSettings,
                detectionSettings,
                chaseSettings,
                attackDecisionSettings,
                attackTimingSettings,
                EnemyLocomotionTimingSettings.CreateDefaultMelee(),
                patrolStrategy,
                detectionStrategy,
                chaseStrategy,
                attackDecisionStrategy,
                stateResolver)
        {
        }

        public EnemyAiRuntimeDefinition(
            EnemyAiCommonSettings commonSettings,
            PatrolSettings patrolSettings,
            DetectionSettings detectionSettings,
            ChaseSettings chaseSettings,
            AttackDecisionSettings attackDecisionSettings,
            EnemyAttackTimingSettings attackTimingSettings,
            EnemyLocomotionTimingSettings locomotionTimingSettings,
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
            LocomotionTimingSettings = locomotionTimingSettings;
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

        public EnemyLocomotionTimingSettings LocomotionTimingSettings { get; }

        public IPatrolStrategy PatrolStrategy { get; }

        public IDetectionStrategy DetectionStrategy { get; }

        public IChaseStrategy ChaseStrategy { get; }

        public IAttackDecisionStrategy AttackDecisionStrategy { get; }

        public IEnemyAiStateResolver StateResolver { get; }

        public void Validate(string paramName)
        {
            CommonSettings.Validate(paramName);
            ChaseSettings.Validate(paramName);
            DetectionSettings.Validate(paramName);
            AttackDecisionSettings.Validate(paramName);
            AttackTimingSettings.Validate(paramName);
            LocomotionTimingSettings.Validate(paramName);

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
                EnemyLocomotionTimingSettings.CreateDefaultMelee(),
                ForwardPatrolStrategy.Instance,
                NearestOpponentDetectionStrategy.Instance,
                AxisPriorityChaseStrategy.Instance,
                MeleeAttackDecisionStrategy.Instance,
                DefaultEnemyAiStateResolver.Instance);
        }

        internal static EnemyAiRuntimeDefinition CreateFromProfile(
            EnemyAiProfile profile,
            int simulationTicksPerSecond)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return new EnemyAiRuntimeDefinition(
                profile.CommonSettings.ToRuntimeSettings(simulationTicksPerSecond),
                profile.PatrolSettings,
                profile.DetectionSettings,
                profile.ChaseSettings,
                profile.AttackDecisionSettings,
                profile.AttackTimingSettings.ToRuntimeSettings(simulationTicksPerSecond),
                profile.LocomotionTimingSettings.ToRuntimeSettings(simulationTicksPerSecond),
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

                case AttackDecisionStrategyKind.ContactSameCell:
                    return ContactSameCellAttackDecisionStrategy.Instance;

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

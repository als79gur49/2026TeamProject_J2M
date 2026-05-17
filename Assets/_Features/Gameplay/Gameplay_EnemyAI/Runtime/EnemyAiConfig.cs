using System;
using Game.Feature.Gameplay.BoardState;
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
        WallFollow = 1,
        Stationary = 2,
        RandomWalk = 3,
    }

    public enum DetectionStrategyKind
    {
        NearestOpponent = 0,
        None = 1,
        CrossLineOfSightOpponent = 2,
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
        WindupForwardCellProjectile = 3,
    }

    public enum MovementSkillStrategyKind
    {
        None = 0,
        JumpToLockedTarget = 1,
        PhaseThroughLockedTarget = 2,
        GlideOverSolid = 3,
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
        [SerializeField] private int ordinaryKinematicMoveTicks;

        public EnemyLocomotionTimingSettings(int moveCooldownTicks)
            : this(moveCooldownTicks, ordinaryKinematicMoveTicks: 0)
        {
        }

        public EnemyLocomotionTimingSettings(int moveCooldownTicks, int ordinaryKinematicMoveTicks)
        {
            this.moveCooldownTicks = moveCooldownTicks;
            this.ordinaryKinematicMoveTicks = ordinaryKinematicMoveTicks;
        }

        public int MoveCooldownTicks => moveCooldownTicks;

        public int OrdinaryKinematicMoveTicks => ordinaryKinematicMoveTicks;

        public void Validate(string paramName)
        {
            if (moveCooldownTicks < 0)
            {
                throw new ArgumentException("Enemy locomotion timing settings require a non-negative move cooldown tick count.", paramName);
            }

            if (ordinaryKinematicMoveTicks < 0)
            {
                throw new ArgumentException("Enemy locomotion timing settings require a non-negative ordinary kinematic move tick count.", paramName);
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
        [SerializeField] private float ordinaryKinematicMoveDurationSeconds;

        public EnemyLocomotionTimingAuthoringSettings(float moveCooldownSeconds)
            : this(moveCooldownSeconds, ordinaryKinematicMoveDurationSeconds: 0f)
        {
        }

        public EnemyLocomotionTimingAuthoringSettings(
            float moveCooldownSeconds,
            float ordinaryKinematicMoveDurationSeconds)
        {
            this.moveCooldownSeconds = moveCooldownSeconds;
            this.ordinaryKinematicMoveDurationSeconds = ordinaryKinematicMoveDurationSeconds;
        }

        public float MoveCooldownSeconds => moveCooldownSeconds;

        public float OrdinaryKinematicMoveDurationSeconds => ordinaryKinematicMoveDurationSeconds;

        public void Validate(string paramName)
        {
            if (moveCooldownSeconds < 0f ||
                float.IsNaN(moveCooldownSeconds) ||
                float.IsInfinity(moveCooldownSeconds))
            {
                throw new ArgumentException("Enemy locomotion timing authoring settings require a non-negative move cooldown duration.", paramName);
            }

            if (ordinaryKinematicMoveDurationSeconds < 0f ||
                float.IsNaN(ordinaryKinematicMoveDurationSeconds) ||
                float.IsInfinity(ordinaryKinematicMoveDurationSeconds))
            {
                throw new ArgumentException("Enemy locomotion timing authoring settings require a non-negative ordinary kinematic move duration.", paramName);
            }
        }

        public EnemyLocomotionTimingSettings ToRuntimeSettings(int simulationTicksPerSecond)
        {
            Validate(nameof(EnemyLocomotionTimingAuthoringSettings));

            var moveCooldownTicks = GameplayTimingProfile.SecondsToTicks(
                moveCooldownSeconds,
                simulationTicksPerSecond,
                allowZero: true);
            var ordinaryDurationSeconds = ordinaryKinematicMoveDurationSeconds > 0f
                ? ordinaryKinematicMoveDurationSeconds
                : moveCooldownSeconds;
            var ordinaryKinematicMoveTicks = ordinaryDurationSeconds > 0f
                ? GameplayTimingProfile.SecondsToEvenCeilTicks(
                    ordinaryDurationSeconds,
                    simulationTicksPerSecond,
                    minimumTicks: 2)
                : 0;

            return new EnemyLocomotionTimingSettings(
                moveCooldownTicks,
                ordinaryKinematicMoveTicks);
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
                runtimeSettings.MoveCooldownTicks / (float)simulationTicksPerSecond,
                runtimeSettings.OrdinaryKinematicMoveTicks / (float)simulationTicksPerSecond);
        }
    }

    [Serializable]
    public struct EnemyChargeTimingSettings
    {
        [SerializeField] private int windupTicks;
        [SerializeField] private int activeStepCooldownTicks;
        [SerializeField] private int recoverTicks;

        public EnemyChargeTimingSettings(int windupTicks, int activeStepCooldownTicks, int recoverTicks)
        {
            this.windupTicks = windupTicks;
            this.activeStepCooldownTicks = activeStepCooldownTicks;
            this.recoverTicks = recoverTicks;
        }

        public int WindupTicks => windupTicks;

        public int ActiveStepCooldownTicks => activeStepCooldownTicks;

        public int RecoverTicks => recoverTicks;

        public void Validate(string paramName)
        {
            if (windupTicks < 0 || activeStepCooldownTicks < 0 || recoverTicks < 0)
            {
                throw new ArgumentException("Enemy charge timing settings require non-negative tick counts.", paramName);
            }
        }

        public static EnemyChargeTimingSettings CreateDefault()
        {
            return new EnemyChargeTimingSettings(windupTicks: 0, activeStepCooldownTicks: 0, recoverTicks: 0);
        }
    }

    [Serializable]
    public struct EnemyChargeTimingAuthoringSettings
    {
        [SerializeField] private float windupSeconds;
        [SerializeField] private float activeStepCooldownSeconds;
        [SerializeField] private float recoverSeconds;

        public EnemyChargeTimingAuthoringSettings(float windupSeconds, float activeStepCooldownSeconds, float recoverSeconds)
        {
            this.windupSeconds = windupSeconds;
            this.activeStepCooldownSeconds = activeStepCooldownSeconds;
            this.recoverSeconds = recoverSeconds;
        }

        public float WindupSeconds => windupSeconds;

        public float ActiveStepCooldownSeconds => activeStepCooldownSeconds;

        public float RecoverSeconds => recoverSeconds;

        public void Validate(string paramName)
        {
            if (windupSeconds < 0f || activeStepCooldownSeconds < 0f || recoverSeconds < 0f)
            {
                throw new ArgumentException("Enemy charge timing authoring settings require non-negative durations.", paramName);
            }
        }

        public EnemyChargeTimingSettings ToRuntimeSettings(int simulationTicksPerSecond)
        {
            Validate(nameof(EnemyChargeTimingAuthoringSettings));

            return new EnemyChargeTimingSettings(
                GameplayTimingProfile.SecondsToTicks(
                    windupSeconds,
                    simulationTicksPerSecond,
                    allowZero: true),
                GameplayTimingProfile.SecondsToTicks(
                    activeStepCooldownSeconds,
                    simulationTicksPerSecond,
                    allowZero: true),
                GameplayTimingProfile.SecondsToTicks(
                    recoverSeconds,
                    simulationTicksPerSecond,
                    allowZero: true));
        }

        public static EnemyChargeTimingAuthoringSettings CreateDefault()
        {
            return FromRuntimeSettings(
                EnemyChargeTimingSettings.CreateDefault(),
                GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        public static EnemyChargeTimingAuthoringSettings FromRuntimeSettings(
            EnemyChargeTimingSettings runtimeSettings,
            int simulationTicksPerSecond)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            return new EnemyChargeTimingAuthoringSettings(
                runtimeSettings.WindupTicks / (float)simulationTicksPerSecond,
                runtimeSettings.ActiveStepCooldownTicks / (float)simulationTicksPerSecond,
                runtimeSettings.RecoverTicks / (float)simulationTicksPerSecond);
        }
    }

    [Serializable]
    public struct EnemyJumpTimingSettings
    {
        [SerializeField] private int initialDelayTicks;
        [SerializeField] private int windupTicks;
        [SerializeField] private int airborneTicks;
        [SerializeField] private int cooldownTicks;

        public EnemyJumpTimingSettings(
            int windupTicks,
            int airborneTicks,
            int cooldownTicks)
            : this(
                initialDelayTicks: 0,
                windupTicks: windupTicks,
                airborneTicks: airborneTicks,
                cooldownTicks: cooldownTicks)
        {
        }

        public EnemyJumpTimingSettings(
            int initialDelayTicks,
            int windupTicks,
            int airborneTicks,
            int cooldownTicks)
        {
            this.initialDelayTicks = initialDelayTicks;
            this.windupTicks = windupTicks;
            this.airborneTicks = airborneTicks;
            this.cooldownTicks = cooldownTicks;
        }

        public int InitialDelayTicks => initialDelayTicks;

        public int WindupTicks => windupTicks;

        public int AirborneTicks => airborneTicks;

        public int CooldownTicks => cooldownTicks;

        public void Validate(string paramName)
        {
            if (initialDelayTicks < 0 || windupTicks < 0 || airborneTicks < 0 || cooldownTicks < 0)
            {
                throw new ArgumentException("Enemy jump timing settings require non-negative tick counts.", paramName);
            }
        }

        public static EnemyJumpTimingSettings CreateDefault()
        {
            return new EnemyJumpTimingSettings(initialDelayTicks: 0, windupTicks: 0, airborneTicks: 0, cooldownTicks: 0);
        }
    }

    [Serializable]
    public struct EnemyJumpTimingAuthoringSettings
    {
        [SerializeField] private float initialDelaySeconds;
        [SerializeField] private float windupSeconds;
        [SerializeField] private float airborneSeconds;
        [SerializeField] private float cooldownSeconds;

        public EnemyJumpTimingAuthoringSettings(
            float windupSeconds,
            float airborneSeconds,
            float cooldownSeconds)
            : this(
                initialDelaySeconds: 0f,
                windupSeconds: windupSeconds,
                airborneSeconds: airborneSeconds,
                cooldownSeconds: cooldownSeconds)
        {
        }

        public EnemyJumpTimingAuthoringSettings(
            float initialDelaySeconds,
            float windupSeconds,
            float airborneSeconds,
            float cooldownSeconds)
        {
            this.initialDelaySeconds = initialDelaySeconds;
            this.windupSeconds = windupSeconds;
            this.airborneSeconds = airborneSeconds;
            this.cooldownSeconds = cooldownSeconds;
        }

        public float InitialDelaySeconds => initialDelaySeconds;

        public float WindupSeconds => windupSeconds;

        public float AirborneSeconds => airborneSeconds;

        public float CooldownSeconds => cooldownSeconds;

        public void Validate(string paramName)
        {
            if (initialDelaySeconds < 0f || windupSeconds < 0f || airborneSeconds < 0f || cooldownSeconds < 0f)
            {
                throw new ArgumentException("Enemy jump timing authoring settings require non-negative durations.", paramName);
            }
        }

        public EnemyJumpTimingSettings ToRuntimeSettings(int simulationTicksPerSecond)
        {
            Validate(nameof(EnemyJumpTimingAuthoringSettings));

            return new EnemyJumpTimingSettings(
                GameplayTimingProfile.SecondsToTicks(
                    initialDelaySeconds,
                    simulationTicksPerSecond,
                    allowZero: true),
                GameplayTimingProfile.SecondsToTicks(
                    windupSeconds,
                    simulationTicksPerSecond,
                    allowZero: true),
                GameplayTimingProfile.SecondsToTicks(
                    airborneSeconds,
                    simulationTicksPerSecond,
                    allowZero: true),
                GameplayTimingProfile.SecondsToTicks(
                    cooldownSeconds,
                    simulationTicksPerSecond,
                    allowZero: true));
        }

        public static EnemyJumpTimingAuthoringSettings CreateDefault()
        {
            return FromRuntimeSettings(
                EnemyJumpTimingSettings.CreateDefault(),
                GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        public static EnemyJumpTimingAuthoringSettings FromRuntimeSettings(
            EnemyJumpTimingSettings runtimeSettings,
            int simulationTicksPerSecond)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            return new EnemyJumpTimingAuthoringSettings(
                runtimeSettings.InitialDelayTicks / (float)simulationTicksPerSecond,
                runtimeSettings.WindupTicks / (float)simulationTicksPerSecond,
                runtimeSettings.AirborneTicks / (float)simulationTicksPerSecond,
                runtimeSettings.CooldownTicks / (float)simulationTicksPerSecond);
        }
    }

    [Serializable]
    public readonly struct EnemyGlidePresentationSettings
    {
        public EnemyGlidePresentationSettings(int liftHeightUnits, int recoveryDipHeightUnits)
        {
            LiftHeightUnits = Mathf.Max(0, liftHeightUnits);
            RecoveryDipHeightUnits = Mathf.Max(0, recoveryDipHeightUnits);
        }

        public int LiftHeightUnits { get; }

        public int RecoveryDipHeightUnits { get; }

        public void Validate(string paramName)
        {
            if (LiftHeightUnits < 0)
            {
                throw new ArgumentException("Enemy glide presentation settings require a non-negative lift height.", paramName);
            }

            if (RecoveryDipHeightUnits < 0)
            {
                throw new ArgumentException("Enemy glide presentation settings require a non-negative recovery dip height.", paramName);
            }
        }

        public static EnemyGlidePresentationSettings CreateDefault()
        {
            return new EnemyGlidePresentationSettings(
                liftHeightUnits: KinematicFixed.UnitsPerCell / 4,
                recoveryDipHeightUnits: 0);
        }
    }

    [Serializable]
    public struct EnemyGlidePresentationAuthoringSettings
    {
        [SerializeField] private float liftHeightCells;
        [SerializeField] private float recoveryDipHeightCells;

        public EnemyGlidePresentationAuthoringSettings(float liftHeightCells, float recoveryDipHeightCells)
        {
            this.liftHeightCells = liftHeightCells;
            this.recoveryDipHeightCells = recoveryDipHeightCells;
        }

        public float LiftHeightCells => liftHeightCells;

        public float RecoveryDipHeightCells => recoveryDipHeightCells;

        public void Validate(string paramName)
        {
            if (liftHeightCells < 0f)
            {
                throw new ArgumentException("Enemy glide presentation settings require a non-negative lift height.", paramName);
            }

            if (recoveryDipHeightCells < 0f)
            {
                throw new ArgumentException("Enemy glide presentation settings require a non-negative recovery dip height.", paramName);
            }
        }

        public EnemyGlidePresentationSettings ToRuntimeSettings()
        {
            Validate(nameof(EnemyGlidePresentationAuthoringSettings));

            return new EnemyGlidePresentationSettings(
                Mathf.RoundToInt(liftHeightCells * KinematicFixed.UnitsPerCell),
                Mathf.RoundToInt(recoveryDipHeightCells * KinematicFixed.UnitsPerCell));
        }

        public static EnemyGlidePresentationAuthoringSettings CreateDefault()
        {
            return new EnemyGlidePresentationAuthoringSettings(0.25f, 0f);
        }

        public static EnemyGlidePresentationAuthoringSettings FromRuntimeSettings(
            EnemyGlidePresentationSettings runtimeSettings)
        {
            return new EnemyGlidePresentationAuthoringSettings(
                runtimeSettings.LiftHeightUnits / (float)KinematicFixed.UnitsPerCell,
                runtimeSettings.RecoveryDipHeightUnits / (float)KinematicFixed.UnitsPerCell);
        }
    }

    [Serializable]
    public struct EnemyGlideTimingSettings
    {
        [SerializeField] private int initialDelayTicks;
        [SerializeField] private int windupTicks;
        [SerializeField] private int durationTicks;
        [SerializeField] private int recoveryTicks;
        [SerializeField] private int cooldownTicks;

        public EnemyGlideTimingSettings(int durationTicks, int cooldownTicks)
            : this(
                initialDelayTicks: 0,
                windupTicks: 0,
                durationTicks: durationTicks,
                recoveryTicks: 0,
                cooldownTicks: cooldownTicks)
        {
        }

        public EnemyGlideTimingSettings(
            int windupTicks,
            int durationTicks,
            int recoveryTicks,
            int cooldownTicks)
            : this(
                initialDelayTicks: 0,
                windupTicks: windupTicks,
                durationTicks: durationTicks,
                recoveryTicks: recoveryTicks,
                cooldownTicks: cooldownTicks)
        {
        }

        public EnemyGlideTimingSettings(
            int initialDelayTicks,
            int windupTicks,
            int durationTicks,
            int recoveryTicks,
            int cooldownTicks)
        {
            this.initialDelayTicks = initialDelayTicks;
            this.windupTicks = windupTicks;
            this.durationTicks = durationTicks;
            this.recoveryTicks = recoveryTicks;
            this.cooldownTicks = cooldownTicks;
        }

        public int InitialDelayTicks => initialDelayTicks;

        public int WindupTicks => windupTicks;

        public int DurationTicks => durationTicks;

        public int RecoveryTicks => recoveryTicks;

        public int CooldownTicks => cooldownTicks;

        public void Validate(string paramName)
        {
            if (initialDelayTicks < 0)
            {
                throw new ArgumentException("Enemy glide timing settings require a non-negative initial delay tick count.", paramName);
            }

            if (windupTicks < 0)
            {
                throw new ArgumentException("Enemy glide timing settings require a non-negative windup tick count.", paramName);
            }

            if (durationTicks <= 0)
            {
                throw new ArgumentException("Enemy glide timing settings require a positive duration tick count.", paramName);
            }

            if (recoveryTicks < 0)
            {
                throw new ArgumentException("Enemy glide timing settings require a non-negative recovery tick count.", paramName);
            }

            if (cooldownTicks < 0)
            {
                throw new ArgumentException("Enemy glide timing settings require a non-negative cooldown tick count.", paramName);
            }
        }

        public static EnemyGlideTimingSettings CreateDefault()
        {
            return new EnemyGlideTimingSettings(durationTicks: 1, cooldownTicks: 0);
        }
    }

    [Serializable]
    public struct EnemyGlideTimingAuthoringSettings
    {
        [SerializeField] private float initialDelaySeconds;
        [SerializeField] private float windupSeconds;
        [SerializeField] private float durationSeconds;
        [SerializeField] private float recoverySeconds;
        [SerializeField] private float cooldownSeconds;

        public EnemyGlideTimingAuthoringSettings(float durationSeconds, float cooldownSeconds)
            : this(
                initialDelaySeconds: 0f,
                windupSeconds: 0f,
                durationSeconds: durationSeconds,
                recoverySeconds: 0f,
                cooldownSeconds: cooldownSeconds)
        {
        }

        public EnemyGlideTimingAuthoringSettings(
            float windupSeconds,
            float durationSeconds,
            float recoverySeconds,
            float cooldownSeconds)
            : this(
                initialDelaySeconds: 0f,
                windupSeconds: windupSeconds,
                durationSeconds: durationSeconds,
                recoverySeconds: recoverySeconds,
                cooldownSeconds: cooldownSeconds)
        {
        }

        public EnemyGlideTimingAuthoringSettings(
            float initialDelaySeconds,
            float windupSeconds,
            float durationSeconds,
            float recoverySeconds,
            float cooldownSeconds)
        {
            this.initialDelaySeconds = initialDelaySeconds;
            this.windupSeconds = windupSeconds;
            this.durationSeconds = durationSeconds;
            this.recoverySeconds = recoverySeconds;
            this.cooldownSeconds = cooldownSeconds;
        }

        public float InitialDelaySeconds => initialDelaySeconds;

        public float WindupSeconds => windupSeconds;

        public float DurationSeconds => durationSeconds;

        public float RecoverySeconds => recoverySeconds;

        public float CooldownSeconds => cooldownSeconds;

        public void Validate(string paramName)
        {
            if (initialDelaySeconds < 0f)
            {
                throw new ArgumentException("Enemy glide timing authoring settings require a non-negative initial delay duration.", paramName);
            }

            if (windupSeconds < 0f)
            {
                throw new ArgumentException("Enemy glide timing authoring settings require a non-negative windup duration.", paramName);
            }

            if (durationSeconds <= 0f)
            {
                throw new ArgumentException("Enemy glide timing authoring settings require a positive duration.", paramName);
            }

            if (recoverySeconds < 0f)
            {
                throw new ArgumentException("Enemy glide timing authoring settings require a non-negative recovery duration.", paramName);
            }

            if (cooldownSeconds < 0f)
            {
                throw new ArgumentException("Enemy glide timing authoring settings require a non-negative cooldown duration.", paramName);
            }
        }

        public EnemyGlideTimingSettings ToRuntimeSettings(int simulationTicksPerSecond)
        {
            Validate(nameof(EnemyGlideTimingAuthoringSettings));

            return new EnemyGlideTimingSettings(
                GameplayTimingProfile.SecondsToTicks(
                    initialDelaySeconds,
                    simulationTicksPerSecond,
                    allowZero: true),
                GameplayTimingProfile.SecondsToTicks(
                    windupSeconds,
                    simulationTicksPerSecond,
                    allowZero: true),
                GameplayTimingProfile.SecondsToTicks(durationSeconds, simulationTicksPerSecond),
                GameplayTimingProfile.SecondsToTicks(
                    recoverySeconds,
                    simulationTicksPerSecond,
                    allowZero: true),
                GameplayTimingProfile.SecondsToTicks(
                    cooldownSeconds,
                    simulationTicksPerSecond,
                    allowZero: true));
        }

        public static EnemyGlideTimingAuthoringSettings CreateDefault()
        {
            return new EnemyGlideTimingAuthoringSettings(durationSeconds: 3f, cooldownSeconds: 2f);
        }

        public static EnemyGlideTimingAuthoringSettings FromRuntimeSettings(
            EnemyGlideTimingSettings runtimeSettings,
            int simulationTicksPerSecond)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            return new EnemyGlideTimingAuthoringSettings(
                runtimeSettings.InitialDelayTicks / (float)simulationTicksPerSecond,
                runtimeSettings.WindupTicks / (float)simulationTicksPerSecond,
                runtimeSettings.DurationTicks / (float)simulationTicksPerSecond,
                runtimeSettings.RecoveryTicks / (float)simulationTicksPerSecond,
                runtimeSettings.CooldownTicks / (float)simulationTicksPerSecond);
        }
    }

    public readonly struct EnemyAiRuntimeDefinition
    {
        public EnemyAiRuntimeDefinition(
            EnemyCoreRuntime core,
            EnemyBrainRuntime brain,
            EnemyCapabilityRuntimeSet capabilities)
        {
            Core = core;
            Brain = brain;
            Capabilities = capabilities;
            Validate(nameof(EnemyAiRuntimeDefinition));
        }

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
                MovementSkillStrategyKind.None,
                EnemyJumpTimingSettings.CreateDefault(),
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
            : this(
                commonSettings,
                patrolSettings,
                detectionSettings,
                chaseSettings,
                attackDecisionSettings,
                attackTimingSettings,
                locomotionTimingSettings,
                MovementSkillStrategyKind.None,
                EnemyJumpTimingSettings.CreateDefault(),
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
            MovementSkillStrategyKind movementSkillStrategyKind,
            EnemyJumpTimingSettings jumpTimingSettings,
            IPatrolStrategy patrolStrategy,
            IDetectionStrategy detectionStrategy,
            IChaseStrategy chaseStrategy,
            IAttackDecisionStrategy attackDecisionStrategy,
            IEnemyAiStateResolver stateResolver)
            : this(
                new EnemyCoreRuntime(commonSettings, locomotionTimingSettings, EnemyChargeTimingSettings.CreateDefault()),
                new EnemyBrainRuntime(
                    new EnemyStateResolverRuntime(ResolveStateResolverKind(stateResolver), stateResolver),
                    new EnemyPatrolRuntime(ResolvePatrolStrategyKind(patrolStrategy), patrolSettings, patrolStrategy),
                    new EnemyDetectionRuntime(ResolveDetectionStrategyKind(detectionStrategy), detectionSettings, detectionStrategy),
                    new EnemyChaseRuntime(ResolveChaseStrategyKind(chaseStrategy), chaseSettings, chaseStrategy)),
                CreateCapabilities(
                    attackDecisionStrategy,
                    attackDecisionSettings,
                    attackTimingSettings,
                    movementSkillStrategyKind,
                    jumpTimingSettings))
        {
        }

        public EnemyCoreRuntime Core { get; }

        public EnemyBrainRuntime Brain { get; }

        public EnemyCapabilityRuntimeSet Capabilities { get; }

        public EnemyAiCommonSettings CommonSettings => Core.CommonSettings;

        public PatrolSettings PatrolSettings => Brain.Patrol.Settings;

        public DetectionSettings DetectionSettings => Brain.Detection.Settings;

        public ChaseSettings ChaseSettings => Brain.Chase.Settings;

        public AttackDecisionSettings AttackDecisionSettings => Capabilities.TryGetCombat(out var combat)
            ? combat.AttackDecisionSettings
            : global::Game.Feature.Gameplay.Entities.AttackDecisionSettings.CreateDefaultMelee();

        public EnemyAttackTimingSettings AttackTimingSettings => Capabilities.TryGetCombat(out var combat)
            ? combat.AttackTimingSettings
            : global::Game.Feature.Gameplay.Entities.EnemyAttackTimingSettings.CreateDefaultMelee();

        public EnemyLocomotionTimingSettings LocomotionTimingSettings => Core.LocomotionTimingSettings;

        public EnemyChargeTimingSettings ChargeTimingSettings => Core.ChargeTimingSettings;

        public MovementSkillStrategyKind MovementSkillStrategyKind => Capabilities.TryGetMovementSkill(out var movementSkill)
            ? movementSkill.Kind
            : global::Game.Feature.Gameplay.Entities.MovementSkillStrategyKind.None;

        public EnemyJumpTimingSettings JumpTimingSettings => Capabilities.TryGetMovementSkill(out var movementSkill)
            && (movementSkill.Kind == MovementSkillStrategyKind.JumpToLockedTarget ||
                movementSkill.Kind == MovementSkillStrategyKind.PhaseThroughLockedTarget)
            ? movementSkill.JumpTimingSettings
            : global::Game.Feature.Gameplay.Entities.EnemyJumpTimingSettings.CreateDefault();

        public EnemyGlideTimingSettings GlideTimingSettings => Capabilities.TryGetMovementSkill(out var movementSkill) &&
                                                               movementSkill.Kind == MovementSkillStrategyKind.GlideOverSolid
            ? movementSkill.GlideTimingSettings
            : global::Game.Feature.Gameplay.Entities.EnemyGlideTimingSettings.CreateDefault();

        public EnemyGlidePresentationSettings GlidePresentationSettings =>
            Capabilities.TryGetMovementSkill(out var movementSkill) &&
            movementSkill.Kind == MovementSkillStrategyKind.GlideOverSolid
                ? movementSkill.GlidePresentationSettings
                : global::Game.Feature.Gameplay.Entities.EnemyGlidePresentationSettings.CreateDefault();

        public IPatrolStrategy PatrolStrategy => Brain.Patrol.Strategy;

        public IDetectionStrategy DetectionStrategy => Brain.Detection.Strategy;

        public IChaseStrategy ChaseStrategy => Brain.Chase.Strategy;

        public IAttackDecisionStrategy AttackDecisionStrategy => Capabilities.TryGetCombat(out var combat)
            ? combat.AttackDecisionStrategy
            : NoAttackDecisionStrategy.Instance;

        public IEnemyAiStateResolver StateResolver => Brain.StateResolver.Resolver;

        public void Validate(string paramName)
        {
            Core.Validate(paramName);
            Brain.Validate(paramName);
            Capabilities.Validate(paramName);
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
                MovementSkillStrategyKind.None,
                EnemyJumpTimingSettings.CreateDefault(),
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
            return EnemyAiProfileCompiler.Compile(profile, simulationTicksPerSecond);
        }

        private static EnemyCapabilityRuntimeSet CreateCapabilities(
            IAttackDecisionStrategy attackDecisionStrategy,
            AttackDecisionSettings attackDecisionSettings,
            EnemyAttackTimingSettings attackTimingSettings,
            MovementSkillStrategyKind movementSkillStrategyKind,
            EnemyJumpTimingSettings jumpTimingSettings)
        {
            EnemyCombatCapabilityRuntime combat = null;
            var attackKind = ResolveAttackDecisionStrategyKind(attackDecisionStrategy);
            if (attackKind != AttackDecisionStrategyKind.None)
            {
                combat = new EnemyCombatCapabilityRuntime(
                    attackKind,
                    attackDecisionSettings,
                    attackTimingSettings,
                    attackDecisionStrategy,
                    WindupMeleeSettings.CreateDefault());
            }

            EnemyMovementSkillCapabilityRuntime movementSkill = null;
            if (movementSkillStrategyKind != MovementSkillStrategyKind.None)
            {
                movementSkill = new EnemyMovementSkillCapabilityRuntime(
                    movementSkillStrategyKind,
                    jumpTimingSettings);
            }

            return new EnemyCapabilityRuntimeSet(combat, movementSkill, passiveContact: null, utility: null, frontFaceSupport: null);
        }

        private static PatrolStrategyKind ResolvePatrolStrategyKind(IPatrolStrategy patrolStrategy)
        {
            return patrolStrategy switch
            {
                ForwardPatrolStrategy _ => PatrolStrategyKind.Forward,
                WallFollowPatrolStrategy _ => PatrolStrategyKind.WallFollow,
                StationaryPatrolStrategy _ => PatrolStrategyKind.Stationary,
                RandomWalkPatrolStrategy _ => PatrolStrategyKind.RandomWalk,
                null => throw new ArgumentNullException(nameof(patrolStrategy)),
                _ => throw new ArgumentOutOfRangeException(nameof(patrolStrategy), patrolStrategy, "Unknown patrol strategy implementation."),
            };
        }

        private static DetectionStrategyKind ResolveDetectionStrategyKind(IDetectionStrategy detectionStrategy)
        {
            return detectionStrategy switch
            {
                NearestOpponentDetectionStrategy _ => DetectionStrategyKind.NearestOpponent,
                NoDetectionStrategy _ => DetectionStrategyKind.None,
                CrossLineOfSightOpponentDetectionStrategy _ => DetectionStrategyKind.CrossLineOfSightOpponent,
                null => throw new ArgumentNullException(nameof(detectionStrategy)),
                _ => throw new ArgumentOutOfRangeException(nameof(detectionStrategy), detectionStrategy, "Unknown detection strategy implementation."),
            };
        }

        private static ChaseStrategyKind ResolveChaseStrategyKind(IChaseStrategy chaseStrategy)
        {
            return chaseStrategy switch
            {
                AxisPriorityChaseStrategy _ => ChaseStrategyKind.AxisPriority,
                null => throw new ArgumentNullException(nameof(chaseStrategy)),
                _ => throw new ArgumentOutOfRangeException(nameof(chaseStrategy), chaseStrategy, "Unknown chase strategy implementation."),
            };
        }

        private static AttackDecisionStrategyKind ResolveAttackDecisionStrategyKind(IAttackDecisionStrategy attackDecisionStrategy)
        {
            return attackDecisionStrategy switch
            {
                MeleeAttackDecisionStrategy _ => AttackDecisionStrategyKind.Melee,
                WindupForwardCellProjectileAttackDecisionStrategy _ => AttackDecisionStrategyKind.WindupForwardCellProjectile,
                NoAttackDecisionStrategy _ => AttackDecisionStrategyKind.None,
                ContactSameCellAttackDecisionStrategy _ => AttackDecisionStrategyKind.ContactSameCell,
                null => throw new ArgumentNullException(nameof(attackDecisionStrategy)),
                _ => throw new ArgumentOutOfRangeException(nameof(attackDecisionStrategy), attackDecisionStrategy, "Unknown attack decision strategy implementation."),
            };
        }

        private static EnemyAiStateResolverKind ResolveStateResolverKind(IEnemyAiStateResolver stateResolver)
        {
            return stateResolver switch
            {
                DefaultEnemyAiStateResolver _ => EnemyAiStateResolverKind.Default,
                ChargingEnemyAiStateResolver _ => EnemyAiStateResolverKind.Charge,
                null => throw new ArgumentNullException(nameof(stateResolver)),
                _ => throw new ArgumentOutOfRangeException(nameof(stateResolver), stateResolver, "Unknown enemy AI state resolver implementation."),
            };
        }
    }

}

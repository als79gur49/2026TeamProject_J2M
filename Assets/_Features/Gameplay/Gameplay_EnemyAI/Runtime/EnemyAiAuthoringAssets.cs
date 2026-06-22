using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Feature.Gameplay.Entities
{
    [Serializable]
    public struct EnemyUnitSpawnDefaults
    {
        [SerializeField] private int hp;
        [SerializeField] private EnemyAiMode initialAiMode;
        [SerializeField] private UnitMobilityKind unitMobilityKind;

        public int Hp => hp;

        public EnemyAiMode InitialAiMode => initialAiMode;

        public UnitMobilityKind UnitMobilityKind => unitMobilityKind;

        public void Validate(string paramName)
        {
            if (hp <= 0)
            {
                throw new ArgumentException("Enemy unit spawn defaults require positive HP.", paramName);
            }

            if (initialAiMode == EnemyAiMode.None ||
                initialAiMode == EnemyAiMode.Dead)
            {
                throw new ArgumentException("Enemy unit spawn defaults require a live initial AI mode.", paramName);
            }

            if (!Enum.IsDefined(typeof(UnitMobilityKind), unitMobilityKind))
            {
                throw new ArgumentException("Enemy unit spawn defaults require a valid unit mobility kind.", paramName);
            }
        }

        internal EnemyUnitSpawnDefaultsRuntime ToRuntime()
        {
            Validate(nameof(EnemyUnitSpawnDefaults));
            return new EnemyUnitSpawnDefaultsRuntime(hp, initialAiMode, unitMobilityKind);
        }

        public static EnemyUnitSpawnDefaults CreateDefault()
        {
            return new EnemyUnitSpawnDefaults
            {
                hp = 1,
                initialAiMode = EnemyAiMode.Patrol,
                unitMobilityKind = Game.Feature.Gameplay.BoardState.UnitMobilityKind.Ground,
            };
        }
    }

    public abstract class EnemyStateResolverAsset : ScriptableObject
    {
        public abstract EnemyAiStateResolverKind Kind { get; }

        public virtual bool RequiresChargeBehavior => false;

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

    public abstract class EnemyBehaviorModuleAsset : ScriptableObject
    {
        public abstract EnemyBehaviorModuleKey Key { get; }

        internal abstract EnemyBehaviorModuleRuntime Compile(in EnemyBehaviorModuleCompileContext context);
    }

    public abstract class EnemyCombatCapabilityAsset : EnemyCapabilityAsset
    {
        public sealed override EnemyCapabilityFamily Family => EnemyCapabilityFamily.Combat;

        public abstract AttackDecisionStrategyKind Kind { get; }

        public abstract AttackDecisionSettings AttackDecisionSettings { get; }

        public abstract EnemyAttackTimingAuthoringSettings AttackTimingSettings { get; }

        public virtual ProjectileWindupSettings ProjectileWindupSettings =>
            global::Game.Feature.Gameplay.Entities.ProjectileWindupSettings.CreateDefault();

        public virtual WindupForwardCellProjectileSettings WindupForwardCellProjectileSettings =>
            global::Game.Feature.Gameplay.Entities.WindupForwardCellProjectileSettings.CreateDefault();

        internal sealed override EnemyCapabilityRuntime Compile(int simulationTicksPerSecond)
        {
            return new EnemyCombatCapabilityRuntime(
                Kind,
                AttackDecisionSettings,
                AttackTimingSettings.ToRuntimeSettings(simulationTicksPerSecond),
                ResolveStrategy(),
                ProjectileWindupSettings,
                WindupForwardCellProjectileSettings);
        }

        protected abstract IAttackDecisionStrategy ResolveStrategy();
    }

    public abstract class EnemyMovementSkillCapabilityAsset : EnemyCapabilityAsset
    {
        public sealed override EnemyCapabilityFamily Family => EnemyCapabilityFamily.MovementSkill;

        public abstract MovementSkillStrategyKind Kind { get; }

        public virtual EnemyJumpTimingAuthoringSettings JumpTimingSettings =>
            EnemyJumpTimingAuthoringSettings.CreateDefault();

        public virtual EnemyGlideTimingAuthoringSettings GlideTimingSettings =>
            EnemyGlideTimingAuthoringSettings.CreateDefault();

        public virtual EnemyGlidePresentationAuthoringSettings GlidePresentationSettings =>
            EnemyGlidePresentationAuthoringSettings.CreateDefault();

        internal sealed override EnemyCapabilityRuntime Compile(int simulationTicksPerSecond)
        {
            if (Kind == MovementSkillStrategyKind.RetiredGlideOverSolid)
            {
                throw new ArgumentException(
                    "GlideOverSolid capability is retired; use EnemyGlideBehaviorModuleAsset.",
                    nameof(Kind));
            }

            return new EnemyMovementSkillCapabilityRuntime(
                Kind,
                JumpTimingSettings.ToRuntimeSettings(simulationTicksPerSecond));
        }
    }

    [Serializable]
    public sealed class EnemyGravityFieldAuraAuthoring
    {
        [SerializeField] private int radius = 1;
        [SerializeField] private float windupSeconds = 0.5f;
        [FormerlySerializedAs("durationSeconds")]
        [SerializeField] private float fieldDurationSeconds = 4f;
        [SerializeField] private float recoverSeconds = 0.5f;
        [SerializeField] private bool blocksPush = true;
        [SerializeField] private bool blocksFlip = true;
        [SerializeField] private bool blocksDestroy = true;
        [SerializeField] private bool suppressMovementDuringWindup = true;
        [SerializeField] private bool suppressMovementDuringRecover = true;

        public int Radius => radius;

        public float WindupSeconds => windupSeconds;

        public float FieldDurationSeconds => fieldDurationSeconds;

        public float DurationSeconds => fieldDurationSeconds;

        public float RecoverSeconds => recoverSeconds;

        public bool BlocksPush => blocksPush;

        public bool BlocksFlip => blocksFlip;

        public bool BlocksDestroy => blocksDestroy;

        public bool SuppressMovementDuringWindup => suppressMovementDuringWindup;

        public bool SuppressMovementDuringActive => false;

        public bool SuppressMovementDuringRecover => suppressMovementDuringRecover;

        internal EnemyGravityFieldAuraRuntime Compile(int simulationTicksPerSecond)
        {
            if (radius <= 0)
            {
                throw new ArgumentException("Enemy gravity field aura authoring requires a positive radius.", nameof(radius));
            }

            if (windupSeconds <= 0f)
            {
                throw new ArgumentException("Enemy gravity field aura authoring requires a positive windup duration.", nameof(windupSeconds));
            }

            if (fieldDurationSeconds <= 0f)
            {
                throw new ArgumentException("Enemy gravity field aura authoring requires a positive field duration.", nameof(fieldDurationSeconds));
            }

            if (recoverSeconds < 0f)
            {
                throw new ArgumentException("Enemy gravity field aura authoring requires a non-negative recovery duration.", nameof(recoverSeconds));
            }

            if (!blocksPush && !blocksFlip && !blocksDestroy)
            {
                throw new ArgumentException("Enemy gravity field aura authoring must block at least one box interaction.", nameof(blocksPush));
            }

            var windupTicks = GameplayTimingProfile.SecondsToTicks(windupSeconds, simulationTicksPerSecond);
            var fieldDurationTicks = GameplayTimingProfile.SecondsToTicks(fieldDurationSeconds, simulationTicksPerSecond);
            var recoveryTicks = GameplayTimingProfile.SecondsToTicks(
                recoverSeconds,
                simulationTicksPerSecond,
                allowZero: true);
            if (windupTicks <= 0)
            {
                throw new ArgumentException("Enemy gravity field aura windup must compile to a positive duration.", nameof(windupSeconds));
            }

            if (fieldDurationTicks <= 0)
            {
                throw new ArgumentException("Enemy gravity field aura field duration must compile to a positive duration.", nameof(fieldDurationSeconds));
            }

            return new EnemyGravityFieldAuraRuntime(
                radius,
                windupTicks,
                fieldDurationTicks,
                recoveryTicks,
                blocksPush,
                blocksFlip,
                blocksDestroy,
                suppressMovementDuringWindup,
                suppressMovementDuringRecover);
        }
    }

    [Serializable]
    public sealed class EnemyUtilityEffectAuthoring
    {
        [SerializeField] private EnemyUtilityEffectKind kind = EnemyUtilityEffectKind.GravityFieldAura;
        [SerializeField] private float initialDelaySeconds = 0f;
        [SerializeField] private float cooldownSeconds = 1f;
        [SerializeField] private EnemyGravityFieldAuraAuthoring gravityFieldAura = new();

        public EnemyUtilityEffectKind Kind => kind;

        public float InitialDelaySeconds => initialDelaySeconds;

        public float CooldownSeconds => cooldownSeconds;

        public EnemyGravityFieldAuraAuthoring GravityFieldAura => gravityFieldAura;

        internal EnemyUtilityEffectRuntime Compile(int simulationTicksPerSecond)
        {
            if (initialDelaySeconds < 0f)
            {
                throw new ArgumentException("Enemy utility effect authoring requires a non-negative initial delay.", nameof(initialDelaySeconds));
            }

            if (cooldownSeconds <= 0f)
            {
                throw new ArgumentException("Enemy utility effect authoring requires a positive cooldown.", nameof(cooldownSeconds));
            }

            return kind switch
            {
                EnemyUtilityEffectKind.RetiredSummonMinion => throw new ArgumentException(
                    "Utility Summon is retired; use EnemySummonBehaviorModuleAsset.",
                    nameof(kind)),
                EnemyUtilityEffectKind.GravityFieldAura => new EnemyUtilityEffectRuntime(
                    kind,
                    GameplayTimingProfile.SecondsToTicks(initialDelaySeconds, simulationTicksPerSecond, allowZero: true),
                    GameplayTimingProfile.SecondsToTicks(cooldownSeconds, simulationTicksPerSecond),
                    gravityFieldAura: (gravityFieldAura ?? throw new ArgumentException("Enemy gravity field aura utility effect requires authoring data.", nameof(gravityFieldAura))).Compile(simulationTicksPerSecond)),
                EnemyUtilityEffectKind.RetiredLockNearbyBoxes => throw new ArgumentException(
                    "Enemy utility effect kind 1 (LockNearbyBoxes) is retired and cannot compile to active runtime.",
                    nameof(kind)),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported enemy utility effect kind."),
            };
        }
    }

}

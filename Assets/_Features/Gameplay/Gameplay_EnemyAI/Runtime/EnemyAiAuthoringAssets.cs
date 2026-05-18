using System;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [Serializable]
    public struct EnemyUnitSpawnDefaults
    {
        [SerializeField] private int hp;
        [SerializeField] private EnemyAiMode initialAiMode;

        public int Hp => hp;

        public EnemyAiMode InitialAiMode => initialAiMode;

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
        }

        internal EnemyUnitSpawnDefaultsRuntime ToRuntime()
        {
            Validate(nameof(EnemyUnitSpawnDefaults));
            return new EnemyUnitSpawnDefaultsRuntime(hp, initialAiMode);
        }

        public static EnemyUnitSpawnDefaults CreateDefault()
        {
            return new EnemyUnitSpawnDefaults
            {
                hp = 1,
                initialAiMode = EnemyAiMode.Patrol,
            };
        }
    }

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

        public virtual WindupMeleeSettings WindupMeleeSettings =>
            global::Game.Feature.Gameplay.Entities.WindupMeleeSettings.CreateDefault();

        public virtual WindupForwardCellProjectileSettings WindupForwardCellProjectileSettings =>
            global::Game.Feature.Gameplay.Entities.WindupForwardCellProjectileSettings.CreateDefault();

        internal sealed override EnemyCapabilityRuntime Compile(int simulationTicksPerSecond)
        {
            return new EnemyCombatCapabilityRuntime(
                Kind,
                AttackDecisionSettings,
                AttackTimingSettings.ToRuntimeSettings(simulationTicksPerSecond),
                ResolveStrategy(),
                WindupMeleeSettings,
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
            return new EnemyMovementSkillCapabilityRuntime(
                Kind,
                JumpTimingSettings.ToRuntimeSettings(simulationTicksPerSecond),
                GlideTimingSettings.ToRuntimeSettings(simulationTicksPerSecond),
                GlidePresentationSettings.ToRuntimeSettings());
        }
    }

    [Serializable]
    public sealed class SummonMinionAuthoring
    {
        [SerializeField] private int spawnCountPerTrigger = 1;
        [SerializeField] private int maxAliveChildren = 3;
        [SerializeField] private SummonCandidatePattern candidatePattern = SummonCandidatePattern.OrthogonalAdjacent4;
        [SerializeField] private bool requireNoUnitAtSpawnCell = true;
        [SerializeField] private bool requireNoSolidAtSpawnCell = true;
        [SerializeField] private EnemyUnitArchetypeAsset summonedArchetype;
        [SerializeField] private bool overrideHp;
        [SerializeField] private int hpOverride = 1;
        [SerializeField] private float windupSeconds = 1.0f;
        [SerializeField] private bool suppressMovementDuringWindup = false;
        [SerializeField, Min(0f)] private float recoverySeconds = 0f;
        [SerializeField] private bool suppressMovementDuringRecover = false;

        public int SpawnCountPerTrigger => spawnCountPerTrigger;

        public int MaxAliveChildren => maxAliveChildren;

        public SummonCandidatePattern CandidatePattern => candidatePattern;

        public bool RequireNoUnitAtSpawnCell => requireNoUnitAtSpawnCell;

        public bool RequireNoSolidAtSpawnCell => requireNoSolidAtSpawnCell;

        public EnemyUnitArchetypeAsset SummonedArchetype => summonedArchetype;

        public bool OverrideHp => overrideHp;

        public int HpOverride => hpOverride;

        public float WindupSeconds => windupSeconds;

        public bool SuppressMovementDuringWindup => suppressMovementDuringWindup;

        public float RecoverySeconds => recoverySeconds;

        public bool SuppressMovementDuringRecover => suppressMovementDuringRecover;

        internal SummonMinionRuntime Compile()
        {
            return Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        internal SummonMinionRuntime Compile(int simulationTicksPerSecond)
        {
            if (spawnCountPerTrigger <= 0)
            {
                throw new ArgumentException("Summon minion authoring requires a positive spawn count.", nameof(spawnCountPerTrigger));
            }

            if (maxAliveChildren <= 0)
            {
                throw new ArgumentException("Summon minion authoring requires a positive max alive child count.", nameof(maxAliveChildren));
            }

            if (summonedArchetype == null)
            {
                throw new ArgumentException("Summon minion authoring requires a summoned archetype asset.", nameof(summonedArchetype));
            }

            summonedArchetype.ValidateConfiguration(nameof(summonedArchetype));

            if (overrideHp && hpOverride <= 0)
            {
                throw new ArgumentException("Summon minion authoring HP override must be positive when enabled.", nameof(hpOverride));
            }

            if (windupSeconds <= 0f)
            {
                throw new ArgumentException("Summon minion authoring requires a positive windup duration.", nameof(windupSeconds));
            }

            if (recoverySeconds < 0f)
            {
                throw new ArgumentException("Summon minion authoring requires a non-negative recovery duration.", nameof(recoverySeconds));
            }

            var windupTicks = GameplayTimingProfile.SecondsToTicks(windupSeconds, simulationTicksPerSecond);
            if (windupTicks <= 0)
            {
                throw new ArgumentException("Summon minion authoring windup must compile to a positive duration.", nameof(windupSeconds));
            }

            var recoveryTicks = GameplayTimingProfile.SecondsToTicks(
                recoverySeconds,
                simulationTicksPerSecond,
                allowZero: true);

            return new SummonMinionRuntime(
                spawnCountPerTrigger,
                candidatePattern,
                requireNoUnitAtSpawnCell,
                requireNoSolidAtSpawnCell,
                maxAliveChildren,
                summonedArchetype.ArchetypeId,
                overrideHp,
                hpOverride,
                windupTicks,
                suppressMovementDuringWindup,
                recoveryTicks,
                suppressMovementDuringRecover);
        }
    }

    [Serializable]
    public sealed class LockNearbyBoxesAuthoring
    {
        [SerializeField] private int radius = 1;
        [SerializeField] private float durationSeconds = 2f;
        [SerializeField, Min(0f)] private float activationDelaySeconds = 0f;
        [SerializeField] private bool blocksPush = true;
        [SerializeField] private bool blocksFlip = true;
        [SerializeField] private bool includeSourceCell;
        [SerializeField] private BoxLockTargetPattern targetPattern = BoxLockTargetPattern.ManhattanRadius;
        [SerializeField] private bool suppressMovementDuringWindup = false;
        [SerializeField, Min(0f)] private float recoverySeconds = 0f;
        [SerializeField] private bool suppressMovementDuringRecover = false;

        public int Radius => radius;

        public float DurationSeconds => durationSeconds;

        public float ActivationDelaySeconds => activationDelaySeconds;

        public bool BlocksPush => blocksPush;

        public bool BlocksFlip => blocksFlip;

        public bool IncludeSourceCell => includeSourceCell;

        public BoxLockTargetPattern TargetPattern => targetPattern;

        public bool SuppressMovementDuringWindup => suppressMovementDuringWindup;

        public float RecoverySeconds => recoverySeconds;

        public bool SuppressMovementDuringRecover => suppressMovementDuringRecover;

        internal LockNearbyBoxesRuntime Compile(int simulationTicksPerSecond)
        {
            if (radius <= 0)
            {
                throw new ArgumentException("Lock nearby boxes authoring requires a positive radius.", nameof(radius));
            }

            if (durationSeconds <= 0f)
            {
                throw new ArgumentException("Lock nearby boxes authoring requires a positive duration.", nameof(durationSeconds));
            }

            if (activationDelaySeconds < 0f)
            {
                throw new ArgumentException("Lock nearby boxes authoring requires a non-negative activation delay.", nameof(activationDelaySeconds));
            }

            if (recoverySeconds < 0f)
            {
                throw new ArgumentException("Lock nearby boxes authoring requires a non-negative recovery duration.", nameof(recoverySeconds));
            }

            if (!blocksPush && !blocksFlip)
            {
                throw new ArgumentException("Lock nearby boxes authoring must block push or flip.", nameof(blocksPush));
            }

            switch (targetPattern)
            {
                case BoxLockTargetPattern.OrthogonalAdjacent4:
                case BoxLockTargetPattern.ManhattanRadius:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(targetPattern), targetPattern, "Unsupported box lock target pattern.");
            }

            var durationTicks = GameplayTimingProfile.SecondsToTicks(durationSeconds, simulationTicksPerSecond);
            if (durationTicks <= 0)
            {
                throw new ArgumentException("Lock nearby boxes authoring must compile to a positive duration.", nameof(durationSeconds));
            }

            var activationDelayTicks = GameplayTimingProfile.SecondsToTicks(
                activationDelaySeconds,
                simulationTicksPerSecond,
                allowZero: true);
            var recoveryTicks = GameplayTimingProfile.SecondsToTicks(
                recoverySeconds,
                simulationTicksPerSecond,
                allowZero: true);

            return new LockNearbyBoxesRuntime(
                radius,
                durationTicks,
                activationDelayTicks,
                blocksPush,
                blocksFlip,
                includeSourceCell,
                targetPattern,
                suppressMovementDuringWindup,
                recoveryTicks,
                suppressMovementDuringRecover);
        }
    }

    [Serializable]
    public sealed class EnemyGravityFieldAuraAuthoring
    {
        [SerializeField] private int radius = 1;
        [SerializeField] private float windupSeconds = 0.5f;
        [SerializeField] private float durationSeconds = 4f;
        [SerializeField] private bool blocksPush = true;
        [SerializeField] private bool blocksFlip = true;
        [SerializeField] private bool blocksDestroy = true;
        [SerializeField] private bool suppressMovementDuringWindup = true;
        [SerializeField] private bool suppressMovementDuringActive;

        public int Radius => radius;

        public float WindupSeconds => windupSeconds;

        public float DurationSeconds => durationSeconds;

        public bool BlocksPush => blocksPush;

        public bool BlocksFlip => blocksFlip;

        public bool BlocksDestroy => blocksDestroy;

        public bool SuppressMovementDuringWindup => suppressMovementDuringWindup;

        public bool SuppressMovementDuringActive => suppressMovementDuringActive;

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

            if (durationSeconds <= 0f)
            {
                throw new ArgumentException("Enemy gravity field aura authoring requires a positive active duration.", nameof(durationSeconds));
            }

            if (!blocksPush && !blocksFlip && !blocksDestroy)
            {
                throw new ArgumentException("Enemy gravity field aura authoring must block at least one box interaction.", nameof(blocksPush));
            }

            var windupTicks = GameplayTimingProfile.SecondsToTicks(windupSeconds, simulationTicksPerSecond);
            var durationTicks = GameplayTimingProfile.SecondsToTicks(durationSeconds, simulationTicksPerSecond);
            if (windupTicks <= 0)
            {
                throw new ArgumentException("Enemy gravity field aura windup must compile to a positive duration.", nameof(windupSeconds));
            }

            if (durationTicks <= 0)
            {
                throw new ArgumentException("Enemy gravity field aura duration must compile to a positive duration.", nameof(durationSeconds));
            }

            return new EnemyGravityFieldAuraRuntime(
                radius,
                windupTicks,
                durationTicks,
                blocksPush,
                blocksFlip,
                blocksDestroy,
                suppressMovementDuringWindup,
                suppressMovementDuringActive);
        }
    }

    [Serializable]
    public sealed class EnemyUtilityEffectAuthoring
    {
        [SerializeField] private EnemyUtilityEffectKind kind = EnemyUtilityEffectKind.SummonMinion;
        [SerializeField] private float initialDelaySeconds = 0f;
        [SerializeField] private float cooldownSeconds = 1f;
        [SerializeField] private SummonMinionAuthoring summon = new();
        [SerializeField] private LockNearbyBoxesAuthoring lockNearbyBoxes = new();
        [SerializeField] private EnemyGravityFieldAuraAuthoring gravityFieldAura = new();

        public EnemyUtilityEffectKind Kind => kind;

        public float InitialDelaySeconds => initialDelaySeconds;

        public float CooldownSeconds => cooldownSeconds;

        public SummonMinionAuthoring Summon => summon;

        public LockNearbyBoxesAuthoring LockNearbyBoxes => lockNearbyBoxes;

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
                EnemyUtilityEffectKind.SummonMinion => new EnemyUtilityEffectRuntime(
                    kind,
                    GameplayTimingProfile.SecondsToTicks(initialDelaySeconds, simulationTicksPerSecond, allowZero: true),
                    GameplayTimingProfile.SecondsToTicks(cooldownSeconds, simulationTicksPerSecond),
                    summon: (summon ?? throw new ArgumentException("Summon utility effect requires summon authoring data.", nameof(summon))).Compile(simulationTicksPerSecond)),
                EnemyUtilityEffectKind.LockNearbyBoxes => new EnemyUtilityEffectRuntime(
                    kind,
                    GameplayTimingProfile.SecondsToTicks(initialDelaySeconds, simulationTicksPerSecond, allowZero: true),
                    GameplayTimingProfile.SecondsToTicks(cooldownSeconds, simulationTicksPerSecond),
                    lockNearbyBoxes: (lockNearbyBoxes ?? throw new ArgumentException("Lock nearby boxes utility effect requires authoring data.", nameof(lockNearbyBoxes))).Compile(simulationTicksPerSecond)),
                EnemyUtilityEffectKind.GravityFieldAura => new EnemyUtilityEffectRuntime(
                    kind,
                    GameplayTimingProfile.SecondsToTicks(initialDelaySeconds, simulationTicksPerSecond, allowZero: true),
                    GameplayTimingProfile.SecondsToTicks(cooldownSeconds, simulationTicksPerSecond),
                    gravityFieldAura: (gravityFieldAura ?? throw new ArgumentException("Enemy gravity field aura utility effect requires authoring data.", nameof(gravityFieldAura))).Compile(simulationTicksPerSecond)),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported enemy utility effect kind."),
            };
        }
    }

    [Serializable]
    public sealed class BoxSlideShieldAuthoring
    {
        [SerializeField] private int radius = 1;
        [SerializeField] private bool includeSourceCell;
        [SerializeField] private FrontFaceShieldTargetPattern targetPattern = FrontFaceShieldTargetPattern.ManhattanRadius;
        [SerializeField] private float initialDelaySeconds = 0f;
        [SerializeField] private float windupSeconds = 1.0f;
        [SerializeField] private float cooldownSeconds = 1.0f;

        public int Radius => radius;

        public bool IncludeSourceCell => includeSourceCell;

        public FrontFaceShieldTargetPattern TargetPattern => targetPattern;

        public float InitialDelaySeconds => initialDelaySeconds;

        public float WindupSeconds => windupSeconds;

        public float CooldownSeconds => cooldownSeconds;

        internal BoxSlideShieldRuntime Compile()
        {
            return Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        internal BoxSlideShieldRuntime Compile(int simulationTicksPerSecond)
        {
            if (radius <= 0)
            {
                throw new ArgumentException("Box slide shield authoring requires a positive radius.", nameof(radius));
            }

            switch (targetPattern)
            {
                case FrontFaceShieldTargetPattern.OrthogonalAdjacent4:
                case FrontFaceShieldTargetPattern.ManhattanRadius:
                case FrontFaceShieldTargetPattern.SquareRadius:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(targetPattern), targetPattern, "Unsupported front-face shield target pattern.");
            }

            if (initialDelaySeconds < 0f)
            {
                throw new ArgumentException("Box slide shield authoring requires a non-negative initial delay.", nameof(initialDelaySeconds));
            }

            if (windupSeconds <= 0f)
            {
                throw new ArgumentException("Box slide shield authoring requires a positive windup duration.", nameof(windupSeconds));
            }

            if (cooldownSeconds <= 0f)
            {
                throw new ArgumentException("Box slide shield authoring requires a positive cooldown.", nameof(cooldownSeconds));
            }

            var initialDelayTicks = GameplayTimingProfile.SecondsToTicks(
                initialDelaySeconds,
                simulationTicksPerSecond,
                allowZero: true);
            var windupTicks = GameplayTimingProfile.SecondsToTicks(windupSeconds, simulationTicksPerSecond);
            if (windupTicks <= 0)
            {
                throw new ArgumentException("Box slide shield authoring windup must compile to a positive duration.", nameof(windupSeconds));
            }

            var cooldownTicks = GameplayTimingProfile.SecondsToTicks(cooldownSeconds, simulationTicksPerSecond);
            if (cooldownTicks <= 0)
            {
                throw new ArgumentException("Box slide shield authoring cooldown must compile to a positive duration.", nameof(cooldownSeconds));
            }

            return new BoxSlideShieldRuntime(radius, includeSourceCell, targetPattern, initialDelayTicks, windupTicks, cooldownTicks);
        }
    }

    [Serializable]
    public sealed class EnemyFrontFaceSupportEffectAuthoring
    {
        [SerializeField] private EnemyFrontFaceSupportEffectKind kind = EnemyFrontFaceSupportEffectKind.BoxSlideShield;
        [SerializeField] private BoxSlideShieldAuthoring boxSlideShield = new();

        public EnemyFrontFaceSupportEffectKind Kind => kind;

        public BoxSlideShieldAuthoring BoxSlideShield => boxSlideShield;

        internal EnemyFrontFaceSupportEffectRuntime Compile(int simulationTicksPerSecond)
        {
            _ = simulationTicksPerSecond;

            return kind switch
            {
                EnemyFrontFaceSupportEffectKind.BoxSlideShield => new EnemyFrontFaceSupportEffectRuntime(
                    kind,
                    boxSlideShield: (boxSlideShield ?? throw new ArgumentException("Box slide shield support effect requires authoring data.", nameof(boxSlideShield))).Compile(simulationTicksPerSecond)),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported front-face support effect kind."),
            };
        }
    }

}

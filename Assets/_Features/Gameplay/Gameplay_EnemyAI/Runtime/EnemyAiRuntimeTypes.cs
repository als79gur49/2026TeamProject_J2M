using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [Serializable]
    public struct EnemyUnitArchetypeId : IEquatable<EnemyUnitArchetypeId>
    {
        private sealed class OrdinalComparerImpl : IEqualityComparer<EnemyUnitArchetypeId>, IComparer<EnemyUnitArchetypeId>
        {
            public bool Equals(EnemyUnitArchetypeId left, EnemyUnitArchetypeId right)
            {
                return left.Equals(right);
            }

            public int GetHashCode(EnemyUnitArchetypeId value)
            {
                return value.GetHashCode();
            }

            public int Compare(EnemyUnitArchetypeId left, EnemyUnitArchetypeId right)
            {
                return string.Compare(left.Value, right.Value, StringComparison.Ordinal);
            }
        }

        private static readonly OrdinalComparerImpl OrdinalComparerInstance = new();
        public static readonly EnemyUnitArchetypeId None = new(string.Empty);

        [SerializeField] private string value;

        public EnemyUnitArchetypeId(string value)
        {
            this.value = value ?? string.Empty;
        }

        public string Value => value ?? string.Empty;

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static IEqualityComparer<EnemyUnitArchetypeId> EqualityComparer => OrdinalComparerInstance;

        public static IComparer<EnemyUnitArchetypeId> OrderingComparer => OrdinalComparerInstance;

        public bool Equals(EnemyUnitArchetypeId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EnemyUnitArchetypeId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public void Validate(string paramName)
        {
            if (string.IsNullOrEmpty(Value))
            {
                throw new ArgumentException("Enemy unit archetype IDs must be non-empty.", paramName);
            }
        }
    }

    public enum EnemyCapabilityFamily
    {
        Combat = 0,
        MovementSkill = 1,
        PassiveContact = 2,
        Utility = 3,
        RetiredFrontFaceSupport = 4,
    }

    public enum EnemyBehaviorModuleKey
    {
        Charge = 1,
        Summon = 2,
        Glide = 3,
    }

    public enum EnemyUtilityEffectKind
    {
        RetiredSummonMinion = 0,
        RetiredLockNearbyBoxes = 1,
        GravityFieldAura = 2,
    }

    public enum SummonCandidatePattern
    {
        OrthogonalAdjacent4 = 0,
    }

    public enum BoxLockTargetPattern
    {
        OrthogonalAdjacent4 = 0,
        ManhattanRadius = 1,
        SquareRadius = 2,
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
            Validate(nameof(EnemyStateResolverRuntime));
        }

        public EnemyAiStateResolverKind Kind { get; }

        public IEnemyAiStateResolver Resolver { get; }

        public void Validate(string paramName)
        {
            if (Resolver == null)
            {
                throw new ArgumentException("Enemy state resolver runtime requires a non-null resolver.", paramName);
            }

            var resolverKind = Resolver switch
            {
                DefaultEnemyAiStateResolver _ => EnemyAiStateResolverKind.Default,
                ChargingEnemyAiStateResolver _ => EnemyAiStateResolverKind.Charge,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(Resolver),
                    Resolver,
                    "Unknown enemy state resolver implementation."),
            };

            if (Kind != resolverKind)
            {
                throw new ArgumentException(
                    $"Enemy state resolver runtime kind '{Kind}' must match resolver implementation '{resolverKind}' ({Resolver.GetType().Name}).",
                    paramName);
            }
        }
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
            Validate(nameof(EnemyPatrolRuntime));
        }

        public PatrolStrategyKind Kind { get; }

        public PatrolSettings Settings { get; }

        public IPatrolStrategy Strategy { get; }

        public void Validate(string paramName)
        {
            if (Strategy == null)
            {
                throw new ArgumentException("Enemy patrol runtime requires a non-null strategy.", paramName);
            }

            var strategyKind = Strategy switch
            {
                ForwardPatrolStrategy _ => PatrolStrategyKind.Forward,
                WallFollowPatrolStrategy _ => PatrolStrategyKind.WallFollow,
                StationaryPatrolStrategy _ => PatrolStrategyKind.Stationary,
                RandomWalkPatrolStrategy _ => PatrolStrategyKind.RandomWalk,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(Strategy),
                    Strategy,
                    "Unknown patrol strategy implementation."),
            };

            if (Kind != strategyKind)
            {
                throw new ArgumentException(
                    $"Enemy patrol runtime kind '{Kind}' must match strategy implementation '{strategyKind}'.",
                    paramName);
            }
        }
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
            Validate(nameof(EnemyDetectionRuntime));
        }

        public DetectionStrategyKind Kind { get; }

        public DetectionSettings Settings { get; }

        public IDetectionStrategy Strategy { get; }

        public void Validate(string paramName)
        {
            if (Strategy == null)
            {
                throw new ArgumentException("Enemy detection runtime requires a non-null strategy.", paramName);
            }

            var strategyKind = Strategy switch
            {
                NoDetectionStrategy _ => DetectionStrategyKind.None,
                NearestOpponentDetectionStrategy _ => DetectionStrategyKind.NearestOpponent,
                CrossLineOfSightOpponentDetectionStrategy _ => DetectionStrategyKind.CrossLineOfSightOpponent,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(Strategy),
                    Strategy,
                    "Unknown detection strategy implementation."),
            };

            if (Kind != strategyKind)
            {
                throw new ArgumentException(
                    $"Enemy detection runtime kind '{Kind}' must match strategy implementation '{strategyKind}' ({Strategy.GetType().Name}).",
                    paramName);
            }

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
            Validate(nameof(EnemyChaseRuntime));
        }

        public ChaseStrategyKind Kind { get; }

        public ChaseSettings Settings { get; }

        public IChaseStrategy Strategy { get; }

        public void Validate(string paramName)
        {
            if (Strategy == null)
            {
                throw new ArgumentException("Enemy chase runtime requires a non-null strategy.", paramName);
            }

            var strategyKind = Strategy switch
            {
                AxisPriorityChaseStrategy _ => ChaseStrategyKind.AxisPriority,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(Strategy),
                    Strategy,
                    "Unknown chase strategy implementation."),
            };

            if (Kind != strategyKind)
            {
                throw new ArgumentException(
                    $"Enemy chase runtime kind '{Kind}' must match strategy implementation '{strategyKind}' ({Strategy.GetType().Name}).",
                    paramName);
            }

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

            StateResolver.Validate(paramName);
            Patrol.Validate(paramName);
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
            IAttackDecisionStrategy attackDecisionStrategy,
            ProjectileWindupSettings? projectileWindupSettings = null,
            WindupForwardCellProjectileSettings? windupForwardCellProjectileSettings = null)
        {
            if (kind == AttackDecisionStrategyKind.None)
            {
                throw new ArgumentException("Combat capability runtime requires a concrete combat kind.", nameof(kind));
            }

            if (kind == AttackDecisionStrategyKind.RetiredMelee)
            {
                throw new ArgumentException(
                    "RetiredMelee is a serialized compatibility slot and cannot compile as a combat capability.",
                    nameof(kind));
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
            ProjectileWindupSettings = projectileWindupSettings ?? global::Game.Feature.Gameplay.Entities.ProjectileWindupSettings.CreateDefault();
            WindupForwardCellProjectileSettings = windupForwardCellProjectileSettings ??
                                                  global::Game.Feature.Gameplay.Entities.WindupForwardCellProjectileSettings.CreateDefault();
            AttackDecisionStrategy = attackDecisionStrategy ?? throw new ArgumentNullException(nameof(attackDecisionStrategy));
            Validate(nameof(EnemyCombatCapabilityRuntime));
        }

        public override EnemyCapabilityFamily Family => EnemyCapabilityFamily.Combat;

        public AttackDecisionStrategyKind Kind { get; }

        public AttackDecisionSettings AttackDecisionSettings { get; }

        public EnemyAttackTimingSettings AttackTimingSettings { get; }

        public ProjectileWindupSettings ProjectileWindupSettings { get; }

        public WindupForwardCellProjectileSettings WindupForwardCellProjectileSettings { get; }

        public IAttackDecisionStrategy AttackDecisionStrategy { get; }

        public override void Validate(string paramName)
        {
            if (AttackDecisionStrategy == null)
            {
                throw new ArgumentException("Enemy combat capability runtime requires a non-null attack decision strategy.", paramName);
            }

            var strategyKind = AttackDecisionStrategy switch
            {
                WindupForwardCellProjectileAttackDecisionStrategy _ => AttackDecisionStrategyKind.WindupForwardCellProjectile,
                ContactSameCellAttackDecisionStrategy _ => AttackDecisionStrategyKind.ContactSameCell,
                NoAttackDecisionStrategy _ => AttackDecisionStrategyKind.None,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(AttackDecisionStrategy),
                    AttackDecisionStrategy,
                    "Unknown combat attack decision strategy implementation."),
            };

            if (strategyKind == AttackDecisionStrategyKind.None)
            {
                throw new ArgumentException(
                    "Combat capability runtime requires a concrete attack decision strategy implementation.",
                    paramName);
            }

            if (strategyKind == AttackDecisionStrategyKind.ContactSameCell)
            {
                throw new ArgumentException(
                    "ContactSameCell must compile as passive contact, not as a combat capability.",
                    paramName);
            }

            if (Kind != strategyKind)
            {
                throw new ArgumentException(
                    $"Enemy combat capability runtime kind '{Kind}' must match attack decision strategy implementation '{strategyKind}' ({AttackDecisionStrategy.GetType().Name}).",
                    paramName);
            }

            AttackDecisionSettings.Validate(paramName);
            AttackTimingSettings.Validate(paramName);
            ProjectileWindupSettings.Validate(paramName);
            if (Kind == AttackDecisionStrategyKind.WindupForwardCellProjectile)
            {
                WindupForwardCellProjectileSettings.Validate(paramName);
            }
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

            if (kind == MovementSkillStrategyKind.RetiredPhaseThroughLockedTarget)
            {
                throw new ArgumentException(
                    "Movement skill 'RetiredPhaseThroughLockedTarget' is retired and cannot compile into active runtime behavior.",
                    nameof(kind));
            }

            if (kind == MovementSkillStrategyKind.RetiredGlideOverSolid)
            {
                throw new ArgumentException(
                    "GlideOverSolid capability is retired; use EnemyGlideBehaviorModuleAsset.",
                    nameof(kind));
            }

            Kind = kind;
            _jumpTimingSettings = jumpTimingSettings;
            Validate(nameof(EnemyMovementSkillCapabilityRuntime));
        }

        private readonly EnemyJumpTimingSettings _jumpTimingSettings;

        public override EnemyCapabilityFamily Family => EnemyCapabilityFamily.MovementSkill;

        public MovementSkillStrategyKind Kind { get; }

        public EnemyJumpTimingSettings JumpTimingSettings
        {
            get
            {
                if (Kind != MovementSkillStrategyKind.JumpToLockedTarget)
                {
                    throw new InvalidOperationException(
                        $"Movement skill '{Kind}' does not expose jump timing settings.");
                }

                return _jumpTimingSettings;
            }
        }

        public override void Validate(string paramName)
        {
            switch (Kind)
            {
                case MovementSkillStrategyKind.JumpToLockedTarget:
                    _jumpTimingSettings.Validate(paramName);
                    break;

                case MovementSkillStrategyKind.RetiredPhaseThroughLockedTarget:
                    throw new ArgumentException(
                        "Movement skill 'RetiredPhaseThroughLockedTarget' is retired and cannot compile into active runtime behavior.",
                        paramName);

                case MovementSkillStrategyKind.RetiredGlideOverSolid:
                    throw new ArgumentException(
                        "GlideOverSolid capability is retired; use EnemyGlideBehaviorModuleAsset.",
                        paramName);

                case MovementSkillStrategyKind.None:
                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown movement skill kind.");
            }
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
            if (AttackDecisionStrategy == null)
            {
                throw new ArgumentException("Enemy passive contact runtime requires a non-null attack decision strategy.", paramName);
            }

            if (AttackDecisionStrategy is not ContactSameCellAttackDecisionStrategy)
            {
                throw new ArgumentException(
                    $"Enemy passive contact runtime kind '{Kind}' must match attack decision strategy implementation '{AttackDecisionStrategy.GetType().Name}'.",
                    paramName);
            }

            AttackDecisionSettings.Validate(paramName);
        }
    }

    public readonly struct EnemyUnitSpawnDefaultsRuntime
    {
        public EnemyUnitSpawnDefaultsRuntime(
            int hp,
            EnemyAiMode initialAiMode,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            Hp = hp;
            InitialAiMode = initialAiMode;
            UnitMobilityKind = unitMobilityKind;
            Validate(nameof(EnemyUnitSpawnDefaultsRuntime));
        }

        public int Hp { get; }

        public EnemyAiMode InitialAiMode { get; }

        public UnitMobilityKind UnitMobilityKind { get; }

        public void Validate(string paramName)
        {
            if (Hp <= 0)
            {
                throw new ArgumentException("Enemy unit spawn defaults require positive HP.", paramName);
            }

            if (InitialAiMode == EnemyAiMode.None ||
                InitialAiMode == EnemyAiMode.Dead)
            {
                throw new ArgumentException("Enemy unit spawn defaults require a live initial AI mode.", paramName);
            }

            if (!Enum.IsDefined(typeof(UnitMobilityKind), UnitMobilityKind))
            {
                throw new ArgumentException("Enemy unit spawn defaults require a valid unit mobility kind.", paramName);
            }
        }
    }

    public readonly struct EnemyGravityFieldAuraRuntime
    {
        public EnemyGravityFieldAuraRuntime(
            int radius,
            int windupTicks,
            int fieldDurationTicks,
            int recoveryTicks,
            bool blocksPush,
            bool blocksFlip,
            bool blocksDestroy,
            bool suppressMovementDuringWindup = true,
            bool suppressMovementDuringRecover = true)
        {
            Radius = radius;
            WindupTicks = windupTicks;
            FieldDurationTicks = fieldDurationTicks;
            RecoveryTicks = recoveryTicks;
            BlocksPush = blocksPush;
            BlocksFlip = blocksFlip;
            BlocksDestroy = blocksDestroy;
            SuppressMovementDuringWindup = suppressMovementDuringWindup;
            SuppressMovementDuringRecover = suppressMovementDuringRecover;
            Validate(nameof(EnemyGravityFieldAuraRuntime));
        }

        public int Radius { get; }

        public int WindupTicks { get; }

        public int FieldDurationTicks { get; }

        public int DurationTicks => FieldDurationTicks;

        public int RecoveryTicks { get; }

        public bool BlocksPush { get; }

        public bool BlocksFlip { get; }

        public bool BlocksDestroy { get; }

        public bool SuppressMovementDuringWindup { get; }

        public bool SuppressMovementDuringActive => false;

        public bool SuppressMovementDuringRecover { get; }

        public void Validate(string paramName)
        {
            if (Radius <= 0)
            {
                throw new ArgumentException("Enemy gravity field aura runtime requires a positive radius.", paramName);
            }

            if (WindupTicks <= 0)
            {
                throw new ArgumentException("Enemy gravity field aura runtime requires a positive windup duration.", paramName);
            }

            if (FieldDurationTicks <= 0)
            {
                throw new ArgumentException("Enemy gravity field aura runtime requires a positive field duration.", paramName);
            }

            if (RecoveryTicks < 0)
            {
                throw new ArgumentException("Enemy gravity field aura runtime requires a non-negative recovery duration.", paramName);
            }

            if (!BlocksPush && !BlocksFlip && !BlocksDestroy)
            {
                throw new ArgumentException("Enemy gravity field aura runtime must block at least one box interaction.", paramName);
            }
        }
    }

    public sealed class EnemyUtilityEffectRuntime
    {
        public EnemyUtilityEffectRuntime(
            EnemyUtilityEffectKind kind,
            int initialDelayTicks,
            int cooldownTicks,
            EnemyGravityFieldAuraRuntime gravityFieldAura = default)
        {
            Kind = kind;
            InitialDelayTicks = initialDelayTicks;
            CooldownTicks = cooldownTicks;
            GravityFieldAura = gravityFieldAura;
            Validate(nameof(EnemyUtilityEffectRuntime));
        }

        public EnemyUtilityEffectKind Kind { get; }

        public int InitialDelayTicks { get; }

        public int CooldownTicks { get; }

        public EnemyGravityFieldAuraRuntime GravityFieldAura { get; }

        public void Validate(string paramName)
        {
            if (InitialDelayTicks < 0)
            {
                throw new ArgumentException("Enemy utility effect runtime requires a non-negative initial delay.", paramName);
            }

            if (CooldownTicks <= 0)
            {
                throw new ArgumentException("Enemy utility effect runtime requires a positive cooldown.", paramName);
            }

            switch (Kind)
            {
                case EnemyUtilityEffectKind.RetiredSummonMinion:
                    throw new ArgumentException(
                        "Utility Summon is retired; use EnemySummonBehaviorModuleAsset.",
                        paramName);

                case EnemyUtilityEffectKind.GravityFieldAura:
                    GravityFieldAura.Validate(paramName);
                    break;

                case EnemyUtilityEffectKind.RetiredLockNearbyBoxes:
                    throw new ArgumentException(
                        "Enemy utility effect kind 1 (LockNearbyBoxes) is retired and cannot compile to active runtime.",
                        paramName);

                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown enemy utility effect kind.");
            }
        }
    }

    public sealed class EnemyUtilityCapabilityRuntime : EnemyCapabilityRuntime
    {
        private readonly ReadOnlyCollection<EnemyUtilityEffectRuntime> _effects;

        public EnemyUtilityCapabilityRuntime(
            IEnumerable<EnemyUtilityEffectRuntime> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var compiledEffects = new List<EnemyUtilityEffectRuntime>();
            foreach (var effect in effects)
            {
                if (effect == null)
                {
                    throw new ArgumentException("Utility capability runtime cannot contain null effects.", nameof(effects));
                }

                compiledEffects.Add(effect);
            }

            _effects = new ReadOnlyCollection<EnemyUtilityEffectRuntime>(compiledEffects);
            Validate(nameof(EnemyUtilityCapabilityRuntime));
        }

        public override EnemyCapabilityFamily Family => EnemyCapabilityFamily.Utility;

        public IReadOnlyList<EnemyUtilityEffectRuntime> Effects => _effects;

        public override void Validate(string paramName)
        {
            for (var i = 0; i < _effects.Count; i++)
            {
                _effects[i].Validate(paramName);
            }
        }
    }

    public readonly struct EnemyCapabilityRuntimeSet
    {
        public EnemyCapabilityRuntimeSet(
            EnemyCombatCapabilityRuntime combat,
            EnemyMovementSkillCapabilityRuntime movementSkill,
            EnemyPassiveContactCapabilityRuntime passiveContact,
            EnemyUtilityCapabilityRuntime utility)
        {
            Combat = combat;
            MovementSkill = movementSkill;
            PassiveContact = passiveContact;
            Utility = utility;
            Validate(nameof(EnemyCapabilityRuntimeSet));
        }

        public EnemyCombatCapabilityRuntime Combat { get; }

        public EnemyMovementSkillCapabilityRuntime MovementSkill { get; }

        public EnemyPassiveContactCapabilityRuntime PassiveContact { get; }

        public EnemyUtilityCapabilityRuntime Utility { get; }

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

        public bool TryGetUtility(out EnemyUtilityCapabilityRuntime utility)
        {
            utility = Utility;
            return utility != null;
        }

        public void Validate(string paramName)
        {
            Combat?.Validate(paramName);
            MovementSkill?.Validate(paramName);
            PassiveContact?.Validate(paramName);
            Utility?.Validate(paramName);
        }
    }

    public readonly struct EnemyBehaviorModuleCompileContext
    {
        public EnemyBehaviorModuleCompileContext(
            string profileName,
            int simulationTicksPerSecond)
        {
            ProfileName = profileName ?? string.Empty;
            SimulationTicksPerSecond = simulationTicksPerSecond;
            Validate(nameof(EnemyBehaviorModuleCompileContext));
        }

        public string ProfileName { get; }

        public int SimulationTicksPerSecond { get; }

        public void Validate(string paramName)
        {
            if (SimulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(SimulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }
        }
    }

    public abstract class EnemyBehaviorModuleRuntime
    {
        protected EnemyBehaviorModuleRuntime(EnemyBehaviorModuleKey key)
        {
            Key = key;
        }

        public EnemyBehaviorModuleKey Key { get; }

        public virtual void Validate(string paramName)
        {
            if (!Enum.IsDefined(typeof(EnemyBehaviorModuleKey), Key))
            {
                throw new ArgumentException("Enemy behavior module runtime requires a valid key.", paramName);
            }
        }
    }

    public sealed class EnemyChargeBehaviorRuntime : EnemyBehaviorModuleRuntime
    {
        public EnemyChargeBehaviorRuntime(EnemyChargeTimingSettings timing)
            : base(EnemyBehaviorModuleKey.Charge)
        {
            Timing = timing;
            Validate(nameof(EnemyChargeBehaviorRuntime));
        }

        public EnemyChargeTimingSettings Timing { get; }

        public override void Validate(string paramName)
        {
            base.Validate(paramName);
            Timing.Validate(paramName);
        }
    }

    public sealed class EnemySummonBehaviorRuntime : EnemyBehaviorModuleRuntime
    {
        public EnemySummonBehaviorRuntime(
            int initialDelayTicks,
            int cooldownTicks,
            EnemySummonCompiledConfig summon)
            : base(EnemyBehaviorModuleKey.Summon)
        {
            InitialDelayTicks = initialDelayTicks;
            CooldownTicks = cooldownTicks;
            Summon = summon;
            Validate(nameof(EnemySummonBehaviorRuntime));
        }

        public int InitialDelayTicks { get; }

        public int CooldownTicks { get; }

        public EnemySummonCompiledConfig Summon { get; }

        public int SpawnCountPerTrigger => Summon.SpawnCountPerTrigger;

        public SummonCandidatePattern CandidatePattern => Summon.CandidatePattern;

        public bool RequireNoUnitAtSpawnCell => Summon.RequireNoUnitAtSpawnCell;

        public bool RequireNoSolidAtSpawnCell => Summon.RequireNoSolidAtSpawnCell;

        public int MaxAliveChildren => Summon.MaxAliveChildren;

        public EnemyUnitArchetypeId SummonedArchetypeId => Summon.SummonedArchetypeId;

        public bool OverrideHp => Summon.OverrideHp;

        public int HpOverride => Summon.HpOverride;

        public int WindupTicks => Summon.WindupTicks;

        public bool SuppressMovementDuringWindup => Summon.SuppressMovementDuringWindup;

        public int RecoveryTicks => Summon.RecoveryTicks;

        public bool SuppressMovementDuringRecover => Summon.SuppressMovementDuringRecover;

        public override void Validate(string paramName)
        {
            base.Validate(paramName);
            if (InitialDelayTicks < 0)
            {
                throw new ArgumentException("Enemy summon behavior runtime requires a non-negative initial delay.", paramName);
            }

            if (CooldownTicks <= 0)
            {
                throw new ArgumentException("Enemy summon behavior runtime requires a positive cooldown.", paramName);
            }

            Summon.Validate(paramName);
        }
    }

    public sealed class EnemyGlideBehaviorRuntime : EnemyBehaviorModuleRuntime
    {
        public EnemyGlideBehaviorRuntime(
            EnemyGlideTimingSettings timing,
            EnemyGlidePresentationSettings presentationSettings)
            : base(EnemyBehaviorModuleKey.Glide)
        {
            Timing = timing;
            PresentationSettings = presentationSettings;
            Validate(nameof(EnemyGlideBehaviorRuntime));
        }

        public EnemyGlideTimingSettings Timing { get; }

        public EnemyGlidePresentationSettings PresentationSettings { get; }

        public override void Validate(string paramName)
        {
            base.Validate(paramName);
            Timing.Validate(paramName);
            PresentationSettings.Validate(paramName);
        }
    }

    public readonly struct EnemyBehaviorRuntimeSet
    {
        public EnemyBehaviorRuntimeSet(
            EnemyChargeBehaviorRuntime charge,
            EnemySummonBehaviorRuntime summon = null,
            EnemyGlideBehaviorRuntime glide = null)
        {
            Charge = charge;
            Summon = summon;
            Glide = glide;
            Validate(nameof(EnemyBehaviorRuntimeSet));
        }

        public EnemyChargeBehaviorRuntime Charge { get; }

        public EnemySummonBehaviorRuntime Summon { get; }

        public EnemyGlideBehaviorRuntime Glide { get; }

        public bool HasCharge => Charge != null;

        public bool HasSummon => Summon != null;

        public bool HasGlide => Glide != null;

        public bool TryGetCharge(out EnemyChargeBehaviorRuntime charge)
        {
            charge = Charge;
            return charge != null;
        }

        public bool TryGetSummon(out EnemySummonBehaviorRuntime summon)
        {
            summon = Summon;
            return summon != null;
        }

        public bool TryGetGlide(out EnemyGlideBehaviorRuntime glide)
        {
            glide = Glide;
            return glide != null;
        }

        public void Validate(string paramName)
        {
            Charge?.Validate(paramName);
            Summon?.Validate(paramName);
            Glide?.Validate(paramName);
        }
    }

    public struct EnemyUtilityEffectState
    {
        public EnemyUtilityEffectKind effectKind;
        public int cooldownTicksRemaining;
        public EnemyUtilityEffectPhase phase;
        public int windupStartTick;
        public int windupEndTick;
        public int activeStartTick;
        public int activeEndTickExclusive;
        public SurfaceCell activeOriginCell;
        public int recoverStartTick;
        public int recoverEndTickExclusive;
        public int activationSequence;
        public int movementSuppressionUntilTickInclusive;
    }

    public enum EnemyUtilityEffectPhase
    {
        None = 0,
        Windup = 1,
        Recover = 2,
        Active = 3,
    }

    public enum EnemySummonBehaviorPhase
    {
        None = 0,
        Windup = 1,
        Recover = 2,
    }

    public struct EnemySummonBehaviorRuntimeState
    {
        public int cooldownTicksRemaining;
        public EnemySummonBehaviorPhase phase;
        public int windupStartTick;
        public int windupEndTick;
        public int recoverStartTick;
        public int recoverEndTickExclusive;
        public int activationSequence;
        public int movementSuppressionUntilTickInclusive;
    }

    public readonly struct EnemySummonBehaviorSnapshotEntry
    {
        public EnemySummonBehaviorSnapshotEntry(int entityId, EnemySummonBehaviorRuntimeState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public EnemySummonBehaviorRuntimeState State { get; }
    }

    public sealed class EnemyUtilityRuntimeState
    {
        private readonly ReadOnlyCollection<EnemyUtilityEffectState> _effectStates;

        public EnemyUtilityRuntimeState(IEnumerable<EnemyUtilityEffectState> effectStates)
        {
            if (effectStates == null)
            {
                throw new ArgumentNullException(nameof(effectStates));
            }

            var copiedStates = new List<EnemyUtilityEffectState>();
            foreach (var effectState in effectStates)
            {
                copiedStates.Add(effectState);
            }

            _effectStates = new ReadOnlyCollection<EnemyUtilityEffectState>(copiedStates);
        }

        public IReadOnlyList<EnemyUtilityEffectState> EffectStates => _effectStates;

        public bool HasEffectCount(int expectedCount)
        {
            return _effectStates.Count == Math.Max(0, expectedCount);
        }
    }

    public readonly struct EnemyUtilitySnapshotEntry
    {
        public EnemyUtilitySnapshotEntry(int entityId, EnemyUtilityRuntimeState state)
        {
            EntityId = entityId;
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public int EntityId { get; }

        public EnemyUtilityRuntimeState State { get; }
    }

    public readonly struct SummonedEntityState
    {
        public SummonedEntityState(int sourceEntityId, int sourceEffectIndex)
        {
            SourceEntityId = sourceEntityId;
            SourceEffectIndex = sourceEffectIndex;
        }

        public int SourceEntityId { get; }

        public int SourceEffectIndex { get; }
    }

    public readonly struct EnemyDefinitionBindingState
    {
        public EnemyDefinitionBindingState(EnemyUnitArchetypeId archetypeId)
        {
            ArchetypeId = archetypeId;
            Validate(nameof(EnemyDefinitionBindingState));
        }

        public EnemyUnitArchetypeId ArchetypeId { get; }

        public void Validate(string paramName)
        {
            ArchetypeId.Validate(paramName);
        }
    }

    public readonly struct EnemyDefinitionBindingSnapshotEntry
    {
        public EnemyDefinitionBindingSnapshotEntry(int entityId, EnemyDefinitionBindingState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public EnemyDefinitionBindingState State { get; }
    }

    public readonly struct SummonedEntitySnapshotEntry
    {
        public SummonedEntitySnapshotEntry(int entityId, SummonedEntityState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public SummonedEntityState State { get; }
    }

    internal static class EnemyUtilityStateQueries
    {
        public static EnemyUtilityRuntimeState CreateInitialState(EnemyUtilityCapabilityRuntime capability)
        {
            if (capability == null)
            {
                throw new ArgumentNullException(nameof(capability));
            }

            var effectStates = new EnemyUtilityEffectState[capability.Effects.Count];
            for (var i = 0; i < capability.Effects.Count; i++)
            {
                effectStates[i] = new EnemyUtilityEffectState
                {
                    effectKind = capability.Effects[i].Kind,
                    cooldownTicksRemaining = capability.Effects[i].InitialDelayTicks,
                };
            }

            return new EnemyUtilityRuntimeState(effectStates);
        }

        public static bool AreEqual(EnemyUtilityRuntimeState left, EnemyUtilityRuntimeState right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.EffectStates.Count != right.EffectStates.Count)
            {
                return false;
            }

            for (var i = 0; i < left.EffectStates.Count; i++)
            {
                if (left.EffectStates[i].effectKind != right.EffectStates[i].effectKind ||
                    left.EffectStates[i].cooldownTicksRemaining != right.EffectStates[i].cooldownTicksRemaining)
                {
                    return false;
                }

                if (left.EffectStates[i].phase != right.EffectStates[i].phase ||
                    left.EffectStates[i].windupStartTick != right.EffectStates[i].windupStartTick ||
                    left.EffectStates[i].windupEndTick != right.EffectStates[i].windupEndTick ||
                    left.EffectStates[i].activeStartTick != right.EffectStates[i].activeStartTick ||
                    left.EffectStates[i].activeEndTickExclusive != right.EffectStates[i].activeEndTickExclusive ||
                    !left.EffectStates[i].activeOriginCell.Equals(right.EffectStates[i].activeOriginCell) ||
                    left.EffectStates[i].recoverStartTick != right.EffectStates[i].recoverStartTick ||
                    left.EffectStates[i].recoverEndTickExclusive != right.EffectStates[i].recoverEndTickExclusive ||
                    left.EffectStates[i].activationSequence != right.EffectStates[i].activationSequence ||
                    left.EffectStates[i].movementSuppressionUntilTickInclusive != right.EffectStates[i].movementSuppressionUntilTickInclusive)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal readonly struct EnemyUtilityTriggerIntent
    {
        public EnemyUtilityTriggerIntent(
            int sourceEntityId,
            int effectIndex,
            EnemyUtilityEffectKind effectKind,
            int triggerTick,
            EnemyUtilityEffectRuntime effectRuntime,
            SurfaceCell originCell = default)
        {
            if (effectRuntime == null)
            {
                throw new ArgumentNullException(nameof(effectRuntime));
            }

            SourceEntityId = sourceEntityId;
            EffectIndex = effectIndex;
            EffectKind = effectKind;
            TriggerTick = triggerTick;
            EffectRuntime = effectRuntime;
            OriginCell = originCell;
        }

        public int SourceEntityId { get; }

        public int EffectIndex { get; }

        public EnemyUtilityEffectKind EffectKind { get; }

        public int TriggerTick { get; }

        public EnemyUtilityEffectRuntime EffectRuntime { get; }

        public SurfaceCell OriginCell { get; }
    }

    internal readonly struct EnemySummonBehaviorTriggerIntent
    {
        public EnemySummonBehaviorTriggerIntent(
            int sourceEntityId,
            int sourceEffectIndex,
            int triggerTick,
            SurfaceCell originCell,
            Direction sourceFacing,
            int sourceTeamId,
            EnemySummonCompiledConfig summon)
        {
            SourceEntityId = sourceEntityId;
            SourceEffectIndex = sourceEffectIndex;
            TriggerTick = triggerTick;
            OriginCell = originCell;
            SourceFacing = sourceFacing;
            SourceTeamId = sourceTeamId;
            Summon = summon;
        }

        public int SourceEntityId { get; }

        public int SourceEffectIndex { get; }

        public int TriggerTick { get; }

        public SurfaceCell OriginCell { get; }

        public Direction SourceFacing { get; }

        public int SourceTeamId { get; }

        public EnemySummonCompiledConfig Summon { get; }
    }

    internal sealed class EnemySummonBehaviorTriggerIntentComparer : IComparer<EnemySummonBehaviorTriggerIntent>
    {
        public static readonly EnemySummonBehaviorTriggerIntentComparer Instance = new();

        public int Compare(EnemySummonBehaviorTriggerIntent left, EnemySummonBehaviorTriggerIntent right)
        {
            var sourceComparison = left.SourceEntityId.CompareTo(right.SourceEntityId);
            if (sourceComparison != 0)
            {
                return sourceComparison;
            }

            var effectComparison = left.SourceEffectIndex.CompareTo(right.SourceEffectIndex);
            if (effectComparison != 0)
            {
                return effectComparison;
            }

            return left.TriggerTick.CompareTo(right.TriggerTick);
        }
    }

    internal sealed class EnemyUtilityTriggerIntentComparer : IComparer<EnemyUtilityTriggerIntent>
    {
        public static readonly EnemyUtilityTriggerIntentComparer Instance = new();

        public int Compare(EnemyUtilityTriggerIntent left, EnemyUtilityTriggerIntent right)
        {
            var sourceComparison = left.SourceEntityId.CompareTo(right.SourceEntityId);
            if (sourceComparison != 0)
            {
                return sourceComparison;
            }

            var effectComparison = left.EffectIndex.CompareTo(right.EffectIndex);
            if (effectComparison != 0)
            {
                return effectComparison;
            }

            return left.TriggerTick.CompareTo(right.TriggerTick);
        }
    }
}

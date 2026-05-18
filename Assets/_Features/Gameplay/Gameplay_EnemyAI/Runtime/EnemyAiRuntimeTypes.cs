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
        FrontFaceSupport = 4,
    }

    public enum EnemyUtilityEffectKind
    {
        SummonMinion = 0,
        LockNearbyBoxes = 1,
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

    public enum EnemyFrontFaceSupportEffectKind
    {
        BoxSlideShield = 0,
    }

    public enum FrontFaceShieldTargetPattern
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
            : this(
                commonSettings,
                locomotionTimingSettings,
                EnemyChargeTimingSettings.CreateDefault())
        {
        }

        public EnemyCoreRuntime(
            EnemyAiCommonSettings commonSettings,
            EnemyLocomotionTimingSettings locomotionTimingSettings,
            EnemyChargeTimingSettings chargeTimingSettings)
        {
            CommonSettings = commonSettings;
            LocomotionTimingSettings = locomotionTimingSettings;
            ChargeTimingSettings = chargeTimingSettings;
            Validate(nameof(EnemyCoreRuntime));
        }

        public EnemyAiCommonSettings CommonSettings { get; }

        public EnemyLocomotionTimingSettings LocomotionTimingSettings { get; }

        public EnemyChargeTimingSettings ChargeTimingSettings { get; }

        public void Validate(string paramName)
        {
            CommonSettings.Validate(paramName);
            LocomotionTimingSettings.Validate(paramName);
            ChargeTimingSettings.Validate(paramName);
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
            IAttackDecisionStrategy attackDecisionStrategy,
            WindupMeleeSettings? windupMeleeSettings = null,
            WindupForwardCellProjectileSettings? windupForwardCellProjectileSettings = null)
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
            WindupMeleeSettings = windupMeleeSettings ?? global::Game.Feature.Gameplay.Entities.WindupMeleeSettings.CreateDefault();
            WindupForwardCellProjectileSettings = windupForwardCellProjectileSettings ??
                                                  global::Game.Feature.Gameplay.Entities.WindupForwardCellProjectileSettings.CreateDefault();
            AttackDecisionStrategy = attackDecisionStrategy ?? throw new ArgumentNullException(nameof(attackDecisionStrategy));
            Validate(nameof(EnemyCombatCapabilityRuntime));
        }

        public override EnemyCapabilityFamily Family => EnemyCapabilityFamily.Combat;

        public AttackDecisionStrategyKind Kind { get; }

        public AttackDecisionSettings AttackDecisionSettings { get; }

        public EnemyAttackTimingSettings AttackTimingSettings { get; }

        public WindupMeleeSettings WindupMeleeSettings { get; }

        public WindupForwardCellProjectileSettings WindupForwardCellProjectileSettings { get; }

        public IAttackDecisionStrategy AttackDecisionStrategy { get; }

        public override void Validate(string paramName)
        {
            AttackDecisionSettings.Validate(paramName);
            AttackTimingSettings.Validate(paramName);
            WindupMeleeSettings.Validate(paramName);
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
            : this(
                kind,
                jumpTimingSettings,
                EnemyGlideTimingSettings.CreateDefault(),
                EnemyGlidePresentationSettings.CreateDefault())
        {
        }

        public EnemyMovementSkillCapabilityRuntime(
            MovementSkillStrategyKind kind,
            EnemyJumpTimingSettings jumpTimingSettings,
            EnemyGlideTimingSettings glideTimingSettings)
            : this(
                kind,
                jumpTimingSettings,
                glideTimingSettings,
                EnemyGlidePresentationSettings.CreateDefault())
        {
        }

        public EnemyMovementSkillCapabilityRuntime(
            MovementSkillStrategyKind kind,
            EnemyJumpTimingSettings jumpTimingSettings,
            EnemyGlideTimingSettings glideTimingSettings,
            EnemyGlidePresentationSettings glidePresentationSettings)
        {
            if (kind == MovementSkillStrategyKind.None)
            {
                throw new ArgumentException("Movement skill runtime requires a concrete movement skill kind.", nameof(kind));
            }

            Kind = kind;
            _jumpTimingSettings = jumpTimingSettings;
            _glideTimingSettings = glideTimingSettings;
            _glidePresentationSettings = glidePresentationSettings;
            Validate(nameof(EnemyMovementSkillCapabilityRuntime));
        }

        private readonly EnemyJumpTimingSettings _jumpTimingSettings;
        private readonly EnemyGlideTimingSettings _glideTimingSettings;
        private readonly EnemyGlidePresentationSettings _glidePresentationSettings;

        public override EnemyCapabilityFamily Family => EnemyCapabilityFamily.MovementSkill;

        public MovementSkillStrategyKind Kind { get; }

        public EnemyJumpTimingSettings JumpTimingSettings
        {
            get
            {
                if (Kind != MovementSkillStrategyKind.JumpToLockedTarget &&
                    Kind != MovementSkillStrategyKind.PhaseThroughLockedTarget)
                {
                    throw new InvalidOperationException(
                        $"Movement skill '{Kind}' does not expose jump timing settings.");
                }

                return _jumpTimingSettings;
            }
        }

        public EnemyGlideTimingSettings GlideTimingSettings
        {
            get
            {
                if (Kind != MovementSkillStrategyKind.GlideOverSolid)
                {
                    throw new InvalidOperationException(
                        $"Movement skill '{Kind}' does not expose glide timing settings.");
                }

                return _glideTimingSettings;
            }
        }

        public EnemyGlidePresentationSettings GlidePresentationSettings
        {
            get
            {
                if (Kind != MovementSkillStrategyKind.GlideOverSolid)
                {
                    throw new InvalidOperationException(
                        $"Movement skill '{Kind}' does not expose glide presentation settings.");
                }

                return _glidePresentationSettings;
            }
        }

        public override void Validate(string paramName)
        {
            switch (Kind)
            {
                case MovementSkillStrategyKind.JumpToLockedTarget:
                case MovementSkillStrategyKind.PhaseThroughLockedTarget:
                    _jumpTimingSettings.Validate(paramName);
                    break;

                case MovementSkillStrategyKind.GlideOverSolid:
                    _glideTimingSettings.Validate(paramName);
                    _glidePresentationSettings.Validate(paramName);
                    break;

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
            AttackDecisionSettings.Validate(paramName);
        }
    }

    public readonly struct EnemyUnitSpawnDefaultsRuntime
    {
        public EnemyUnitSpawnDefaultsRuntime(int hp, EnemyAiMode initialAiMode)
        {
            Hp = hp;
            InitialAiMode = initialAiMode;
            Validate(nameof(EnemyUnitSpawnDefaultsRuntime));
        }

        public int Hp { get; }

        public EnemyAiMode InitialAiMode { get; }

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
        }
    }

    public readonly struct SummonMinionRuntime
    {
        public SummonMinionRuntime(
            int spawnCountPerTrigger,
            SummonCandidatePattern candidatePattern,
            bool requireNoUnitAtSpawnCell,
            bool requireNoSolidAtSpawnCell,
            int maxAliveChildren,
            EnemyUnitArchetypeId summonedArchetypeId,
            bool overrideHp = false,
            int hpOverride = 1,
            int windupTicks = 1,
            bool suppressMovementDuringWindup = false,
            int recoveryTicks = 0,
            bool suppressMovementDuringRecover = false)
        {
            SpawnCountPerTrigger = spawnCountPerTrigger;
            CandidatePattern = candidatePattern;
            RequireNoUnitAtSpawnCell = requireNoUnitAtSpawnCell;
            RequireNoSolidAtSpawnCell = requireNoSolidAtSpawnCell;
            MaxAliveChildren = maxAliveChildren;
            SummonedArchetypeId = summonedArchetypeId;
            OverrideHp = overrideHp;
            HpOverride = hpOverride;
            WindupTicks = windupTicks;
            SuppressMovementDuringWindup = suppressMovementDuringWindup;
            RecoveryTicks = recoveryTicks;
            SuppressMovementDuringRecover = suppressMovementDuringRecover;
            Validate(nameof(SummonMinionRuntime));
        }

        public int SpawnCountPerTrigger { get; }

        public SummonCandidatePattern CandidatePattern { get; }

        public bool RequireNoUnitAtSpawnCell { get; }

        public bool RequireNoSolidAtSpawnCell { get; }

        public int MaxAliveChildren { get; }

        public EnemyUnitArchetypeId SummonedArchetypeId { get; }

        public bool OverrideHp { get; }

        public int HpOverride { get; }

        public int WindupTicks { get; }

        public bool SuppressMovementDuringWindup { get; }

        public int RecoveryTicks { get; }

        public bool SuppressMovementDuringRecover { get; }

        public void Validate(string paramName)
        {
            if (SpawnCountPerTrigger <= 0)
            {
                throw new ArgumentException("Summon minion runtime requires a positive spawn count.", paramName);
            }

            if (MaxAliveChildren <= 0)
            {
                throw new ArgumentException("Summon minion runtime requires a positive max alive child count.", paramName);
            }

            SummonedArchetypeId.Validate(paramName);
            if (OverrideHp && HpOverride <= 0)
            {
                throw new ArgumentException("Summon minion runtime HP override must be positive when enabled.", paramName);
            }

            if (WindupTicks <= 0)
            {
                throw new ArgumentException("Summon minion runtime requires a positive windup duration.", paramName);
            }

            if (RecoveryTicks < 0)
            {
                throw new ArgumentException("Summon minion runtime requires a non-negative recovery duration.", paramName);
            }
        }
    }

    internal static class EnemyUtilitySummonPolicy
    {
        public static bool IsMaxAliveReached(
            WorldSnapshot snapshot,
            IReadOnlyList<SummonedEntitySnapshotEntry> summonedEntries,
            int sourceEntityId,
            int effectIndex,
            in SummonMinionRuntime summonRuntime,
            int plannedChildren = 0)
        {
            return CountAliveChildren(snapshot, summonedEntries, sourceEntityId, effectIndex) + plannedChildren >=
                   summonRuntime.MaxAliveChildren;
        }

        public static int CountAliveChildren(
            WorldSnapshot snapshot,
            IReadOnlyList<SummonedEntitySnapshotEntry> summonedEntries,
            int sourceEntityId,
            int effectIndex)
        {
            var aliveCount = 0;
            for (var i = 0; i < summonedEntries.Count; i++)
            {
                var entry = summonedEntries[i];
                if (entry.State.SourceEntityId != sourceEntityId ||
                    entry.State.SourceEffectIndex != effectIndex ||
                    !snapshot.TryGetEntity(entry.EntityId, out var child) ||
                    child.hp <= 0 ||
                    child.markedForDeath ||
                    child.boardPresence != EntityBoardPresence.Occupying)
                {
                    continue;
                }

                aliveCount++;
            }

            return aliveCount;
        }
    }

    public readonly struct LockNearbyBoxesRuntime
    {
        public LockNearbyBoxesRuntime(
            int radius,
            int durationTicks,
            int activationDelayTicks,
            bool blocksPush,
            bool blocksFlip,
            bool includeSourceCell,
            BoxLockTargetPattern targetPattern,
            bool suppressMovementDuringWindup = false,
            int recoveryTicks = 0,
            bool suppressMovementDuringRecover = false)
        {
            Radius = radius;
            DurationTicks = durationTicks;
            ActivationDelayTicks = activationDelayTicks;
            BlocksPush = blocksPush;
            BlocksFlip = blocksFlip;
            IncludeSourceCell = includeSourceCell;
            TargetPattern = targetPattern;
            SuppressMovementDuringWindup = suppressMovementDuringWindup;
            RecoveryTicks = recoveryTicks;
            SuppressMovementDuringRecover = suppressMovementDuringRecover;
            Validate(nameof(LockNearbyBoxesRuntime));
        }

        public int Radius { get; }

        public int DurationTicks { get; }

        public int ActivationDelayTicks { get; }

        public bool BlocksPush { get; }

        public bool BlocksFlip { get; }

        public bool IncludeSourceCell { get; }

        public BoxLockTargetPattern TargetPattern { get; }

        public bool SuppressMovementDuringWindup { get; }

        public int RecoveryTicks { get; }

        public bool SuppressMovementDuringRecover { get; }

        public void Validate(string paramName)
        {
            if (Radius <= 0)
            {
                throw new ArgumentException("Lock nearby boxes runtime requires a positive radius.", paramName);
            }

            if (DurationTicks <= 0)
            {
                throw new ArgumentException("Lock nearby boxes runtime requires a positive duration.", paramName);
            }

            if (ActivationDelayTicks < 0)
            {
                throw new ArgumentException("Lock nearby boxes runtime requires a non-negative activation delay.", paramName);
            }

            if (RecoveryTicks < 0)
            {
                throw new ArgumentException("Lock nearby boxes runtime requires a non-negative recovery duration.", paramName);
            }

            if (!BlocksPush && !BlocksFlip)
            {
                throw new ArgumentException("Lock nearby boxes runtime must block push or flip.", paramName);
            }

            switch (TargetPattern)
            {
                case BoxLockTargetPattern.OrthogonalAdjacent4:
                case BoxLockTargetPattern.ManhattanRadius:
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(TargetPattern), TargetPattern, "Unsupported box lock target pattern.");
            }
        }
    }

    public readonly struct EnemyGravityFieldAuraRuntime
    {
        public EnemyGravityFieldAuraRuntime(
            int radius,
            int windupTicks,
            int durationTicks,
            bool blocksPush,
            bool blocksFlip,
            bool blocksDestroy,
            bool suppressMovementDuringWindup = true,
            bool suppressMovementDuringActive = false)
        {
            Radius = radius;
            WindupTicks = windupTicks;
            DurationTicks = durationTicks;
            BlocksPush = blocksPush;
            BlocksFlip = blocksFlip;
            BlocksDestroy = blocksDestroy;
            SuppressMovementDuringWindup = suppressMovementDuringWindup;
            SuppressMovementDuringActive = suppressMovementDuringActive;
            Validate(nameof(EnemyGravityFieldAuraRuntime));
        }

        public int Radius { get; }

        public int WindupTicks { get; }

        public int DurationTicks { get; }

        public bool BlocksPush { get; }

        public bool BlocksFlip { get; }

        public bool BlocksDestroy { get; }

        public bool SuppressMovementDuringWindup { get; }

        public bool SuppressMovementDuringActive { get; }

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

            if (DurationTicks <= 0)
            {
                throw new ArgumentException("Enemy gravity field aura runtime requires a positive active duration.", paramName);
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
            SummonMinionRuntime summon = default,
            LockNearbyBoxesRuntime lockNearbyBoxes = default,
            EnemyGravityFieldAuraRuntime gravityFieldAura = default)
        {
            Kind = kind;
            InitialDelayTicks = initialDelayTicks;
            CooldownTicks = cooldownTicks;
            Summon = summon;
            LockNearbyBoxes = lockNearbyBoxes;
            GravityFieldAura = gravityFieldAura;
            Validate(nameof(EnemyUtilityEffectRuntime));
        }

        public EnemyUtilityEffectKind Kind { get; }

        public int InitialDelayTicks { get; }

        public int CooldownTicks { get; }

        public SummonMinionRuntime Summon { get; }

        public LockNearbyBoxesRuntime LockNearbyBoxes { get; }

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
                case EnemyUtilityEffectKind.SummonMinion:
                    Summon.Validate(paramName);
                    break;

                case EnemyUtilityEffectKind.LockNearbyBoxes:
                    LockNearbyBoxes.Validate(paramName);
                    break;

                case EnemyUtilityEffectKind.GravityFieldAura:
                    GravityFieldAura.Validate(paramName);
                    break;

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

    public readonly struct BoxSlideShieldRuntime
    {
        public BoxSlideShieldRuntime(
            int radius,
            bool includeSourceCell,
            FrontFaceShieldTargetPattern targetPattern,
            int windupTicks = 1,
            int cooldownTicks = 1)
            : this(
                radius: radius,
                includeSourceCell: includeSourceCell,
                targetPattern: targetPattern,
                initialDelayTicks: 0,
                windupTicks: windupTicks,
                cooldownTicks: cooldownTicks)
        {
        }

        public BoxSlideShieldRuntime(
            int radius,
            bool includeSourceCell,
            FrontFaceShieldTargetPattern targetPattern,
            int initialDelayTicks,
            int windupTicks,
            int cooldownTicks)
        {
            Radius = radius;
            IncludeSourceCell = includeSourceCell;
            TargetPattern = targetPattern;
            InitialDelayTicks = initialDelayTicks;
            WindupTicks = windupTicks;
            CooldownTicks = cooldownTicks;
            Validate(nameof(BoxSlideShieldRuntime));
        }

        public int Radius { get; }

        public bool IncludeSourceCell { get; }

        public FrontFaceShieldTargetPattern TargetPattern { get; }

        public int InitialDelayTicks { get; }

        public int WindupTicks { get; }

        public int CooldownTicks { get; }

        public void Validate(string paramName)
        {
            if (Radius <= 0)
            {
                throw new ArgumentException("Box slide shield runtime requires a positive radius.", paramName);
            }

            if (InitialDelayTicks < 0)
            {
                throw new ArgumentException("Box slide shield runtime requires a non-negative initial delay.", paramName);
            }

            if (WindupTicks <= 0)
            {
                throw new ArgumentException("Box slide shield runtime requires a positive windup duration.", paramName);
            }

            if (CooldownTicks <= 0)
            {
                throw new ArgumentException("Box slide shield runtime requires a positive cooldown.", paramName);
            }

            switch (TargetPattern)
            {
                case FrontFaceShieldTargetPattern.OrthogonalAdjacent4:
                case FrontFaceShieldTargetPattern.ManhattanRadius:
                case FrontFaceShieldTargetPattern.SquareRadius:
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(TargetPattern), TargetPattern, "Unsupported front-face shield target pattern.");
            }
        }
    }

    public sealed class EnemyFrontFaceSupportEffectRuntime
    {
        public EnemyFrontFaceSupportEffectRuntime(
            EnemyFrontFaceSupportEffectKind kind,
            BoxSlideShieldRuntime boxSlideShield = default)
        {
            Kind = kind;
            BoxSlideShield = boxSlideShield;
            Validate(nameof(EnemyFrontFaceSupportEffectRuntime));
        }

        public EnemyFrontFaceSupportEffectKind Kind { get; }

        public BoxSlideShieldRuntime BoxSlideShield { get; }

        public void Validate(string paramName)
        {
            switch (Kind)
            {
                case EnemyFrontFaceSupportEffectKind.BoxSlideShield:
                    BoxSlideShield.Validate(paramName);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown front-face support effect kind.");
            }
        }
    }

    public sealed class EnemyFrontFaceSupportCapabilityRuntime : EnemyCapabilityRuntime
    {
        private readonly ReadOnlyCollection<EnemyFrontFaceSupportEffectRuntime> _effects;

        public EnemyFrontFaceSupportCapabilityRuntime(
            IEnumerable<EnemyFrontFaceSupportEffectRuntime> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            var compiledEffects = new List<EnemyFrontFaceSupportEffectRuntime>();
            foreach (var effect in effects)
            {
                if (effect == null)
                {
                    throw new ArgumentException("Front-face support capability runtime cannot contain null effects.", nameof(effects));
                }

                compiledEffects.Add(effect);
            }

            _effects = new ReadOnlyCollection<EnemyFrontFaceSupportEffectRuntime>(compiledEffects);
            Validate(nameof(EnemyFrontFaceSupportCapabilityRuntime));
        }

        public override EnemyCapabilityFamily Family => EnemyCapabilityFamily.FrontFaceSupport;

        public IReadOnlyList<EnemyFrontFaceSupportEffectRuntime> Effects => _effects;

        public override void Validate(string paramName)
        {
            for (var i = 0; i < _effects.Count; i++)
            {
                _effects[i].Validate(paramName);
            }
        }
    }

    public readonly struct FrontFaceSupportContributor
    {
        public FrontFaceSupportContributor(
            int sourceEntityId,
            SurfaceCell sourceCell,
            int effectIndex,
            EnemyFrontFaceSupportEffectRuntime effectRuntime)
        {
            SourceEntityId = sourceEntityId;
            SourceCell = sourceCell;
            EffectIndex = effectIndex;
            EffectRuntime = effectRuntime ?? throw new ArgumentNullException(nameof(effectRuntime));
        }

        public int SourceEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public int EffectIndex { get; }

        public EnemyFrontFaceSupportEffectRuntime EffectRuntime { get; }
    }

    internal sealed class FrontFaceSupportContributorComparer : IComparer<FrontFaceSupportContributor>
    {
        internal static readonly FrontFaceSupportContributorComparer Instance = new();

        public int Compare(FrontFaceSupportContributor left, FrontFaceSupportContributor right)
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

            var faceComparison = left.SourceCell.face.CompareTo(right.SourceCell.face);
            if (faceComparison != 0)
            {
                return faceComparison;
            }

            var xComparison = left.SourceCell.x.CompareTo(right.SourceCell.x);
            if (xComparison != 0)
            {
                return xComparison;
            }

            return left.SourceCell.y.CompareTo(right.SourceCell.y);
        }
    }

    public readonly struct EnemyCapabilityRuntimeSet
    {
        public EnemyCapabilityRuntimeSet(
            EnemyCombatCapabilityRuntime combat,
            EnemyMovementSkillCapabilityRuntime movementSkill,
            EnemyPassiveContactCapabilityRuntime passiveContact,
            EnemyUtilityCapabilityRuntime utility,
            EnemyFrontFaceSupportCapabilityRuntime frontFaceSupport)
        {
            Combat = combat;
            MovementSkill = movementSkill;
            PassiveContact = passiveContact;
            Utility = utility;
            FrontFaceSupport = frontFaceSupport;
            Validate(nameof(EnemyCapabilityRuntimeSet));
        }

        public EnemyCombatCapabilityRuntime Combat { get; }

        public EnemyMovementSkillCapabilityRuntime MovementSkill { get; }

        public EnemyPassiveContactCapabilityRuntime PassiveContact { get; }

        public EnemyUtilityCapabilityRuntime Utility { get; }

        public EnemyFrontFaceSupportCapabilityRuntime FrontFaceSupport { get; }

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

        public bool TryGetFrontFaceSupport(out EnemyFrontFaceSupportCapabilityRuntime frontFaceSupport)
        {
            frontFaceSupport = FrontFaceSupport;
            return frontFaceSupport != null;
        }

        public void Validate(string paramName)
        {
            Combat?.Validate(paramName);
            MovementSkill?.Validate(paramName);
            PassiveContact?.Validate(paramName);
            Utility?.Validate(paramName);
            FrontFaceSupport?.Validate(paramName);
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

    public enum EnemyFrontFaceSupportEffectPhase
    {
        None = 0,
        Windup = 1,
        Active = 2,
    }

    public struct EnemyFrontFaceSupportEffectState
    {
        public EnemyFrontFaceSupportEffectPhase phase;
        public int windupStartTick;
        public int windupEndTick;
        public int activationSequence;
        public int cooldownTicksRemaining;
        public int radius;
        public bool includeSourceCell;
        public FrontFaceShieldTargetPattern targetPattern;
    }

    public sealed class EnemyFrontFaceSupportRuntimeState
    {
        private readonly ReadOnlyCollection<EnemyFrontFaceSupportEffectState> _effectStates;

        public EnemyFrontFaceSupportRuntimeState(IEnumerable<EnemyFrontFaceSupportEffectState> effectStates)
        {
            if (effectStates == null)
            {
                throw new ArgumentNullException(nameof(effectStates));
            }

            var copiedStates = new List<EnemyFrontFaceSupportEffectState>();
            foreach (var effectState in effectStates)
            {
                copiedStates.Add(effectState);
            }

            _effectStates = new ReadOnlyCollection<EnemyFrontFaceSupportEffectState>(copiedStates);
        }

        public IReadOnlyList<EnemyFrontFaceSupportEffectState> EffectStates => _effectStates;

        public bool HasEffectCount(int expectedCount)
        {
            return _effectStates.Count == Math.Max(0, expectedCount);
        }
    }

    public readonly struct EnemyFrontFaceSupportSnapshotEntry
    {
        public EnemyFrontFaceSupportSnapshotEntry(int entityId, EnemyFrontFaceSupportRuntimeState state)
        {
            EntityId = entityId;
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public int EntityId { get; }

        public EnemyFrontFaceSupportRuntimeState State { get; }
    }

    internal static class EnemyFrontFaceSupportStateQueries
    {
        public static EnemyFrontFaceSupportRuntimeState CreateInitialState(EnemyFrontFaceSupportCapabilityRuntime capability)
        {
            if (capability == null)
            {
                throw new ArgumentNullException(nameof(capability));
            }

            var effectStates = new EnemyFrontFaceSupportEffectState[capability.Effects.Count];
            for (var i = 0; i < capability.Effects.Count; i++)
            {
                effectStates[i] = CreateInactiveEffectState(capability.Effects[i], previousActivationSequence: 0);
                if (capability.Effects[i].Kind == EnemyFrontFaceSupportEffectKind.BoxSlideShield)
                {
                    effectStates[i].cooldownTicksRemaining = capability.Effects[i].BoxSlideShield.InitialDelayTicks;
                }
            }

            return new EnemyFrontFaceSupportRuntimeState(effectStates);
        }

        public static EnemyFrontFaceSupportEffectState CreateInactiveEffectState(
            EnemyFrontFaceSupportEffectRuntime effectRuntime,
            int previousActivationSequence)
        {
            if (effectRuntime == null)
            {
                throw new ArgumentNullException(nameof(effectRuntime));
            }

            var state = new EnemyFrontFaceSupportEffectState
            {
                phase = EnemyFrontFaceSupportEffectPhase.None,
                activationSequence = Math.Max(0, previousActivationSequence),
            };

            if (effectRuntime.Kind == EnemyFrontFaceSupportEffectKind.BoxSlideShield)
            {
                state.radius = effectRuntime.BoxSlideShield.Radius;
                state.includeSourceCell = effectRuntime.BoxSlideShield.IncludeSourceCell;
                state.targetPattern = effectRuntime.BoxSlideShield.TargetPattern;
            }

            return state;
        }

        public static bool AreEqual(EnemyFrontFaceSupportRuntimeState left, EnemyFrontFaceSupportRuntimeState right)
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
                var leftState = left.EffectStates[i];
                var rightState = right.EffectStates[i];
                if (leftState.phase != rightState.phase ||
                    leftState.windupStartTick != rightState.windupStartTick ||
                    leftState.windupEndTick != rightState.windupEndTick ||
                    leftState.activationSequence != rightState.activationSequence ||
                    leftState.cooldownTicksRemaining != rightState.cooldownTicksRemaining ||
                    leftState.radius != rightState.radius ||
                    leftState.includeSourceCell != rightState.includeSourceCell ||
                    leftState.targetPattern != rightState.targetPattern)
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

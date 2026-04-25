using System;
using System.Collections.Generic;
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

    [CreateAssetMenu(menuName = "Gameplay/AI/Enemy Unit Archetype", fileName = "EnemyUnitArchetype")]
    public sealed class EnemyUnitArchetypeAsset : ScriptableObject
    {
        [SerializeField] private EnemyUnitArchetypeId archetypeId;
        [SerializeField] private EnemyAiProfile aiProfile;
        [SerializeField] private EnemyUnitSpawnDefaults spawnDefaults = EnemyUnitSpawnDefaults.CreateDefault();

        public EnemyUnitArchetypeId ArchetypeId => archetypeId;

        public EnemyAiProfile AiProfile => aiProfile;

        public EnemyUnitSpawnDefaults SpawnDefaults => spawnDefaults;

        internal void ValidateConfiguration(string paramName)
        {
            archetypeId.Validate(paramName);
            if (aiProfile == null)
            {
                throw new ArgumentException("Enemy unit archetype assets require a non-null AI profile.", paramName);
            }

            spawnDefaults.Validate(paramName);
        }
    }

    [CreateAssetMenu(menuName = "Gameplay/AI/Enemy Unit Archetype Catalog", fileName = "EnemyUnitArchetypeCatalog")]
    public sealed class EnemyUnitArchetypeCatalog : ScriptableObject
    {
        [SerializeField] private EnemyUnitArchetypeAsset[] entries = Array.Empty<EnemyUnitArchetypeAsset>();

        public IReadOnlyList<EnemyUnitArchetypeAsset> Entries => entries ?? Array.Empty<EnemyUnitArchetypeAsset>();
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

    [Serializable]
    public sealed class SummonMinionAuthoring
    {
        [SerializeField] private int spawnCountPerTrigger = 1;
        [SerializeField] private int maxAliveChildren = 3;
        [SerializeField] private SummonCandidatePattern candidatePattern = SummonCandidatePattern.OrthogonalAdjacent4;
        [SerializeField] private bool requireNoUnitAtSpawnCell = true;
        [SerializeField] private bool requireNoSolidAtSpawnCell = true;
        [SerializeField] private SummonedUnitDefinitionMode definitionMode = SummonedUnitDefinitionMode.DefaultEnemy;
        [SerializeField] private EnemyUnitArchetypeAsset summonedArchetype;
        [SerializeField] private bool overrideHp;
        [SerializeField] private int hpOverride = 1;
        [SerializeField] private int minionHp = 1;

        public int SpawnCountPerTrigger => spawnCountPerTrigger;

        public int MaxAliveChildren => maxAliveChildren;

        public SummonCandidatePattern CandidatePattern => candidatePattern;

        public bool RequireNoUnitAtSpawnCell => requireNoUnitAtSpawnCell;

        public bool RequireNoSolidAtSpawnCell => requireNoSolidAtSpawnCell;

        public SummonedUnitDefinitionMode DefinitionMode => definitionMode;

        public EnemyUnitArchetypeAsset SummonedArchetype => summonedArchetype;

        public bool OverrideHp => overrideHp;

        public int HpOverride => hpOverride;

        public int MinionHp => minionHp;

        internal SummonMinionRuntime Compile()
        {
            if (spawnCountPerTrigger <= 0)
            {
                throw new ArgumentException("Summon minion authoring requires a positive spawn count.", nameof(spawnCountPerTrigger));
            }

            if (maxAliveChildren <= 0)
            {
                throw new ArgumentException("Summon minion authoring requires a positive max alive child count.", nameof(maxAliveChildren));
            }

            if (definitionMode == SummonedUnitDefinitionMode.DefaultEnemy &&
                minionHp <= 0)
            {
                throw new ArgumentException("Summon minion authoring requires positive minion HP.", nameof(minionHp));
            }

            if (definitionMode == SummonedUnitDefinitionMode.Archetype)
            {
                if (summonedArchetype == null)
                {
                    throw new ArgumentException("Archetype summon authoring requires a summoned archetype asset.", nameof(summonedArchetype));
                }

                summonedArchetype.ValidateConfiguration(nameof(summonedArchetype));

                if (overrideHp && hpOverride <= 0)
                {
                    throw new ArgumentException("Archetype summon authoring HP override must be positive when enabled.", nameof(hpOverride));
                }
            }

            return new SummonMinionRuntime(
                spawnCountPerTrigger,
                candidatePattern,
                requireNoUnitAtSpawnCell,
                requireNoSolidAtSpawnCell,
                maxAliveChildren,
                minionHp,
                definitionMode,
                definitionMode == SummonedUnitDefinitionMode.Archetype
                    ? summonedArchetype.ArchetypeId
                    : EnemyUnitArchetypeId.None,
                overrideHp,
                hpOverride);
        }
    }

    [Serializable]
    public sealed class LockNearbyBoxesAuthoring
    {
        [SerializeField] private int radius = 1;
        [SerializeField] private float durationSeconds = 2f;
        [SerializeField] private bool blocksPush = true;
        [SerializeField] private bool blocksFlip = true;
        [SerializeField] private bool includeSourceCell;
        [SerializeField] private BoxLockTargetPattern targetPattern = BoxLockTargetPattern.ManhattanRadius;

        public int Radius => radius;

        public float DurationSeconds => durationSeconds;

        public bool BlocksPush => blocksPush;

        public bool BlocksFlip => blocksFlip;

        public bool IncludeSourceCell => includeSourceCell;

        public BoxLockTargetPattern TargetPattern => targetPattern;

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

            return new LockNearbyBoxesRuntime(
                radius,
                durationTicks,
                blocksPush,
                blocksFlip,
                includeSourceCell,
                targetPattern);
        }
    }

    [Serializable]
    public sealed class EnemyUtilityEffectAuthoring
    {
        [SerializeField] private EnemyUtilityEffectKind kind = EnemyUtilityEffectKind.SummonMinion;
        [SerializeField] private float initialDelaySeconds = 0f;
        [SerializeField] private float intervalSeconds = 1f;
        [SerializeField] private SummonMinionAuthoring summon = new();
        [SerializeField] private LockNearbyBoxesAuthoring lockNearbyBoxes = new();

        public EnemyUtilityEffectKind Kind => kind;

        public float InitialDelaySeconds => initialDelaySeconds;

        public float IntervalSeconds => intervalSeconds;

        public SummonMinionAuthoring Summon => summon;

        public LockNearbyBoxesAuthoring LockNearbyBoxes => lockNearbyBoxes;

        internal EnemyUtilityEffectRuntime Compile(int simulationTicksPerSecond)
        {
            if (initialDelaySeconds < 0f)
            {
                throw new ArgumentException("Enemy utility effect authoring requires a non-negative initial delay.", nameof(initialDelaySeconds));
            }

            if (intervalSeconds <= 0f)
            {
                throw new ArgumentException("Enemy utility effect authoring requires a positive interval.", nameof(intervalSeconds));
            }

            return kind switch
            {
                EnemyUtilityEffectKind.SummonMinion => new EnemyUtilityEffectRuntime(
                    kind,
                    GameplayTimingProfile.SecondsToTicks(initialDelaySeconds, simulationTicksPerSecond, allowZero: true),
                    GameplayTimingProfile.SecondsToTicks(intervalSeconds, simulationTicksPerSecond),
                    summon: (summon ?? throw new ArgumentException("Summon utility effect requires summon authoring data.", nameof(summon))).Compile()),
                EnemyUtilityEffectKind.LockNearbyBoxes => new EnemyUtilityEffectRuntime(
                    kind,
                    GameplayTimingProfile.SecondsToTicks(initialDelaySeconds, simulationTicksPerSecond, allowZero: true),
                    GameplayTimingProfile.SecondsToTicks(intervalSeconds, simulationTicksPerSecond),
                    lockNearbyBoxes: (lockNearbyBoxes ?? throw new ArgumentException("Lock nearby boxes utility effect requires authoring data.", nameof(lockNearbyBoxes))).Compile(simulationTicksPerSecond)),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported enemy utility effect kind."),
            };
        }
    }

}

using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [CreateAssetMenu(
        menuName = "Gameplay/AI/Behaviors/Summon Module",
        fileName = "EnemySummonBehaviorModule")]
    public sealed class EnemySummonBehaviorModuleAsset : EnemyBehaviorModuleAsset
    {
        [SerializeField] private float initialDelaySeconds = 0f;
        [SerializeField] private float cooldownSeconds = 1f;
        [SerializeField] private EnemySummonAuthoring summon = new();

        public override EnemyBehaviorModuleKey Key => EnemyBehaviorModuleKey.Summon;

        public float InitialDelaySeconds => initialDelaySeconds;

        public float CooldownSeconds => cooldownSeconds;

        public EnemySummonAuthoring Summon => summon;

        internal override EnemyBehaviorModuleRuntime Compile(in EnemyBehaviorModuleCompileContext context)
        {
            if (initialDelaySeconds < 0f)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{context.ProfileName}' summon behavior module '{name}' requires a non-negative initial delay.",
                    nameof(initialDelaySeconds));
            }

            if (cooldownSeconds <= 0f)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{context.ProfileName}' summon behavior module '{name}' requires a positive cooldown.",
                    nameof(cooldownSeconds));
            }

            if (summon == null)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{context.ProfileName}' summon behavior module '{name}' requires summon authoring data.",
                    nameof(summon));
            }

            return new EnemySummonBehaviorRuntime(
                GameplayTimingProfile.SecondsToTicks(
                    initialDelaySeconds,
                    context.SimulationTicksPerSecond,
                    allowZero: true),
                GameplayTimingProfile.SecondsToTicks(cooldownSeconds, context.SimulationTicksPerSecond),
                summon.Compile(context.SimulationTicksPerSecond));
        }
    }

    [Serializable]
    public sealed class EnemySummonAuthoring
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

        internal EnemySummonCompiledConfig Compile()
        {
            return Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);
        }

        internal EnemySummonCompiledConfig Compile(int simulationTicksPerSecond)
        {
            if (spawnCountPerTrigger <= 0)
            {
                throw new ArgumentException("Enemy summon authoring requires a positive spawn count.", nameof(spawnCountPerTrigger));
            }

            if (maxAliveChildren <= 0)
            {
                throw new ArgumentException("Enemy summon authoring requires a positive max alive child count.", nameof(maxAliveChildren));
            }

            if (summonedArchetype == null)
            {
                throw new ArgumentException("Enemy summon authoring requires a summoned archetype asset.", nameof(summonedArchetype));
            }

            summonedArchetype.ValidateConfiguration(nameof(summonedArchetype));

            if (overrideHp && hpOverride <= 0)
            {
                throw new ArgumentException("Enemy summon authoring HP override must be positive when enabled.", nameof(hpOverride));
            }

            if (windupSeconds <= 0f)
            {
                throw new ArgumentException("Enemy summon authoring requires a positive windup duration.", nameof(windupSeconds));
            }

            if (recoverySeconds < 0f)
            {
                throw new ArgumentException("Enemy summon authoring requires a non-negative recovery duration.", nameof(recoverySeconds));
            }

            var windupTicks = GameplayTimingProfile.SecondsToTicks(windupSeconds, simulationTicksPerSecond);
            if (windupTicks <= 0)
            {
                throw new ArgumentException("Enemy summon authoring windup must compile to a positive duration.", nameof(windupSeconds));
            }

            var recoveryTicks = GameplayTimingProfile.SecondsToTicks(
                recoverySeconds,
                simulationTicksPerSecond,
                allowZero: true);

            return new EnemySummonCompiledConfig(
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

    public readonly struct EnemySummonCompiledConfig
    {
        public EnemySummonCompiledConfig(
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
            Validate(nameof(EnemySummonCompiledConfig));
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
                throw new ArgumentException("Enemy summon compiled config requires a positive spawn count.", paramName);
            }

            if (MaxAliveChildren <= 0)
            {
                throw new ArgumentException("Enemy summon compiled config requires a positive max alive child count.", paramName);
            }

            SummonedArchetypeId.Validate(paramName);
            if (OverrideHp && HpOverride <= 0)
            {
                throw new ArgumentException("Enemy summon compiled config HP override must be positive when enabled.", paramName);
            }

            if (WindupTicks <= 0)
            {
                throw new ArgumentException("Enemy summon compiled config requires a positive windup duration.", paramName);
            }

            if (RecoveryTicks < 0)
            {
                throw new ArgumentException("Enemy summon compiled config requires a non-negative recovery duration.", paramName);
            }
        }
    }

    internal static class EnemySummonChildLimitPolicy
    {
        public static bool IsMaxAliveReached(
            WorldSnapshot snapshot,
            IReadOnlyList<SummonedEntitySnapshotEntry> summonedEntries,
            int sourceEntityId,
            int effectIndex,
            in EnemySummonCompiledConfig summonConfig,
            int plannedChildren = 0)
        {
            return CountAliveChildren(snapshot, summonedEntries, sourceEntityId, effectIndex) + plannedChildren >=
                   summonConfig.MaxAliveChildren;
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
}

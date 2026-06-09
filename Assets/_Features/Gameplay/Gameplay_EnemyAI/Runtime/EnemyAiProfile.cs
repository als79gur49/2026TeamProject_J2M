using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    /// <summary>
    /// Authoritative enemy AI profile.
    /// Locomotion cadence belongs here and resolves into runtime definitions and world-state counters.
    /// </summary>
    [CreateAssetMenu(menuName = "Gameplay/AI/Enemy AI Profile", fileName = "EnemyAiProfile")]
    public sealed class EnemyAiProfile : ScriptableObject
    {
        [SerializeField] private EnemyCoreAuthoring coreAuthoring;
        [SerializeField] private EnemyBrainAuthoring brainAuthoring;
        [SerializeField] private List<EnemyCapabilityAsset> capabilityAssets = new();

        internal EnemyCoreAuthoring CoreAuthoring => coreAuthoring;

        internal EnemyBrainAuthoring BrainAuthoring => brainAuthoring;

        internal IReadOnlyList<EnemyCapabilityAsset> CapabilityAssets => capabilityAssets;

        public EnemyAiStateResolverKind StateResolverKind => RequireStateResolverAsset().Kind;

        public PatrolStrategyKind PatrolStrategyKind => RequirePatrolStrategyAsset().Kind;

        public DetectionStrategyKind DetectionStrategyKind => RequireDetectionStrategyAsset().Kind;

        public ChaseStrategyKind ChaseStrategyKind => RequireChaseStrategyAsset().Kind;

        public AttackDecisionStrategyKind AttackDecisionStrategyKind =>
            GetCombatCapabilityAsset()?.Kind ?? global::Game.Feature.Gameplay.Entities.AttackDecisionStrategyKind.None;

        public MovementSkillStrategyKind MovementSkillStrategyKind =>
            GetMovementSkillCapabilityAsset()?.Kind ?? global::Game.Feature.Gameplay.Entities.MovementSkillStrategyKind.None;

        public EnemyAiCommonAuthoringSettings CommonSettings => RequireCoreAuthoring().CommonSettings;

        public PatrolSettings PatrolSettings => RequirePatrolStrategyAsset().Settings;

        public DetectionSettings DetectionSettings => RequireDetectionStrategyAsset().Settings;

        public ChaseSettings ChaseSettings => RequireChaseStrategyAsset().Settings;

        public AttackDecisionSettings AttackDecisionSettings =>
            GetCombatCapabilityAsset()?.AttackDecisionSettings ?? global::Game.Feature.Gameplay.Entities.AttackDecisionSettings.CreateAdjacentRange();

        public EnemyAttackTimingAuthoringSettings AttackTimingSettings =>
            GetCombatCapabilityAsset()?.AttackTimingSettings ?? global::Game.Feature.Gameplay.Entities.EnemyAttackTimingAuthoringSettings.CreateImmediate();

        public EnemyLocomotionTimingAuthoringSettings LocomotionTimingSettings => RequireCoreAuthoring().LocomotionTimingSettings;

        public EnemyChargeTimingAuthoringSettings ChargeTimingSettings => RequireCoreAuthoring().ChargeTimingSettings;

        public EnemyJumpTimingAuthoringSettings JumpTimingSettings =>
            GetMovementSkillCapabilityAsset()?.JumpTimingSettings ?? global::Game.Feature.Gameplay.Entities.EnemyJumpTimingAuthoringSettings.CreateDefault();

        public EnemyGlideTimingAuthoringSettings GlideTimingSettings =>
            GetMovementSkillCapabilityAsset()?.GlideTimingSettings ?? global::Game.Feature.Gameplay.Entities.EnemyGlideTimingAuthoringSettings.CreateDefault();

        public EnemyGlidePresentationAuthoringSettings GlidePresentationSettings =>
            GetMovementSkillCapabilityAsset()?.GlidePresentationSettings ??
            global::Game.Feature.Gameplay.Entities.EnemyGlidePresentationAuthoringSettings.CreateDefault();

        public EnemyAiRuntimeDefinition CreateRuntimeDefinition(int simulationTicksPerSecond)
        {
            return EnemyAiProfileCompiler.Compile(this, simulationTicksPerSecond);
        }

        private EnemyCombatCapabilityAsset GetCombatCapabilityAsset()
        {
            if (capabilityAssets == null)
            {
                return null;
            }

            for (var i = 0; i < capabilityAssets.Count; i++)
            {
                if (capabilityAssets[i] is EnemyCombatCapabilityAsset combatCapability)
                {
                    return combatCapability;
                }
            }

            return null;
        }

        private EnemyMovementSkillCapabilityAsset GetMovementSkillCapabilityAsset()
        {
            if (capabilityAssets == null)
            {
                return null;
            }

            for (var i = 0; i < capabilityAssets.Count; i++)
            {
                if (capabilityAssets[i] is EnemyMovementSkillCapabilityAsset movementSkillCapability)
                {
                    return movementSkillCapability;
                }
            }

            return null;
        }

        private EnemyCoreAuthoring RequireCoreAuthoring()
        {
            if (coreAuthoring != null)
            {
                return coreAuthoring;
            }

            throw new InvalidOperationException(
                $"Enemy AI profile '{name}' requires {nameof(EnemyCoreAuthoring)} authoring.");
        }

        private EnemyBrainAuthoring RequireBrainAuthoring()
        {
            if (brainAuthoring != null)
            {
                return brainAuthoring;
            }

            throw new InvalidOperationException(
                $"Enemy AI profile '{name}' requires {nameof(EnemyBrainAuthoring)} authoring.");
        }

        private EnemyStateResolverAsset RequireStateResolverAsset()
        {
            var stateResolver = RequireBrainAuthoring().StateResolver;
            if (stateResolver != null)
            {
                return stateResolver;
            }

            throw new InvalidOperationException(
                $"Enemy AI profile '{name}' requires a {nameof(EnemyStateResolverAsset)} on {nameof(EnemyBrainAuthoring)}.");
        }

        private EnemyPatrolStrategyAsset RequirePatrolStrategyAsset()
        {
            var patrolStrategy = RequireBrainAuthoring().PatrolStrategy;
            if (patrolStrategy != null)
            {
                return patrolStrategy;
            }

            throw new InvalidOperationException(
                $"Enemy AI profile '{name}' requires a {nameof(EnemyPatrolStrategyAsset)} on {nameof(EnemyBrainAuthoring)}.");
        }

        private EnemyDetectionStrategyAsset RequireDetectionStrategyAsset()
        {
            var detectionStrategy = RequireBrainAuthoring().DetectionStrategy;
            if (detectionStrategy != null)
            {
                return detectionStrategy;
            }

            throw new InvalidOperationException(
                $"Enemy AI profile '{name}' requires a {nameof(EnemyDetectionStrategyAsset)} on {nameof(EnemyBrainAuthoring)}.");
        }

        private EnemyChaseStrategyAsset RequireChaseStrategyAsset()
        {
            var chaseStrategy = RequireBrainAuthoring().ChaseStrategy;
            if (chaseStrategy != null)
            {
                return chaseStrategy;
            }

            throw new InvalidOperationException(
                $"Enemy AI profile '{name}' requires a {nameof(EnemyChaseStrategyAsset)} on {nameof(EnemyBrainAuthoring)}.");
        }
    }
}

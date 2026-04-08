using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Entities
{
    public static class EnemyAiProfileCompiler
    {
        public static EnemyAiRuntimeDefinition Compile(
            EnemyAiProfile profile,
            int simulationTicksPerSecond)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            return profile.UsesAuthoringConfiguration
                ? CompileAuthoring(profile, simulationTicksPerSecond)
                : CompileLegacy(profile, simulationTicksPerSecond);
        }

        private static EnemyAiRuntimeDefinition CompileAuthoring(
            EnemyAiProfile profile,
            int simulationTicksPerSecond)
        {
            if (profile.CoreAuthoring == null)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{profile.name}' uses hybrid authoring but is missing {nameof(EnemyCoreAuthoring)}.",
                    nameof(profile));
            }

            if (profile.BrainAuthoring == null)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{profile.name}' uses hybrid authoring but is missing {nameof(EnemyBrainAuthoring)}.",
                    nameof(profile));
            }

            var core = profile.CoreAuthoring.Compile(simulationTicksPerSecond);
            var brain = profile.BrainAuthoring.Compile();
            var capabilities = CompileCapabilities(profile.name, profile.CapabilityAssets, simulationTicksPerSecond);
            return new EnemyAiRuntimeDefinition(core, brain, capabilities);
        }

        private static EnemyAiRuntimeDefinition CompileLegacy(
            EnemyAiProfile profile,
            int simulationTicksPerSecond)
        {
            var core = new EnemyCoreRuntime(
                profile.LegacyCommonSettings.ToRuntimeSettings(simulationTicksPerSecond),
                profile.LegacyLocomotionTimingSettings.ToRuntimeSettings(simulationTicksPerSecond));
            var brain = new EnemyBrainRuntime(
                ResolveStateResolver(profile.LegacyStateResolverKind),
                ResolvePatrol(profile.LegacyPatrolStrategyKind, profile.LegacyPatrolSettings),
                ResolveDetection(profile.LegacyDetectionStrategyKind, profile.LegacyDetectionSettings),
                ResolveChase(profile.LegacyChaseStrategyKind, profile.LegacyChaseSettings));
            var capabilities = ResolveLegacyCapabilities(profile, simulationTicksPerSecond);
            return new EnemyAiRuntimeDefinition(core, brain, capabilities);
        }

        private static EnemyCapabilityRuntimeSet CompileCapabilities(
            string profileName,
            IReadOnlyList<EnemyCapabilityAsset> capabilityAssets,
            int simulationTicksPerSecond)
        {
            EnemyCombatCapabilityRuntime combat = null;
            EnemyMovementSkillCapabilityRuntime movementSkill = null;
            var capabilityCount = capabilityAssets?.Count ?? 0;

            for (var i = 0; i < capabilityCount; i++)
            {
                var capabilityAsset = capabilityAssets[i];
                if (capabilityAsset == null)
                {
                    throw new ArgumentException(
                        $"Enemy AI profile '{profileName}' contains a null capability entry.",
                        nameof(capabilityAssets));
                }

                var runtime = capabilityAsset.Compile(simulationTicksPerSecond);
                switch (runtime.Family)
                {
                    case EnemyCapabilityFamily.Combat:
                        if (combat != null)
                        {
                            throw new ArgumentException(
                                $"Enemy AI profile '{profileName}' declares multiple combat capabilities ('{combat.Kind}' and '{capabilityAsset.name}').",
                                nameof(capabilityAssets));
                        }

                        combat = runtime as EnemyCombatCapabilityRuntime
                            ?? throw new ArgumentException(
                                $"Enemy AI profile '{profileName}' compiled an invalid combat capability runtime from '{capabilityAsset.name}'.",
                                nameof(capabilityAssets));
                        break;

                    case EnemyCapabilityFamily.MovementSkill:
                        if (movementSkill != null)
                        {
                            throw new ArgumentException(
                                $"Enemy AI profile '{profileName}' declares multiple movement skill capabilities ('{movementSkill.Kind}' and '{capabilityAsset.name}').",
                                nameof(capabilityAssets));
                        }

                        movementSkill = runtime as EnemyMovementSkillCapabilityRuntime
                            ?? throw new ArgumentException(
                                $"Enemy AI profile '{profileName}' compiled an invalid movement capability runtime from '{capabilityAsset.name}'.",
                                nameof(capabilityAssets));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(runtime), runtime.Family, "Unknown enemy capability family.");
                }
            }

            return new EnemyCapabilityRuntimeSet(combat, movementSkill);
        }

        private static EnemyCapabilityRuntimeSet ResolveLegacyCapabilities(
            EnemyAiProfile profile,
            int simulationTicksPerSecond)
        {
            EnemyCombatCapabilityRuntime combat = null;
            if (profile.LegacyAttackDecisionStrategyKind != AttackDecisionStrategyKind.None)
            {
                combat = new EnemyCombatCapabilityRuntime(
                    profile.LegacyAttackDecisionStrategyKind,
                    profile.LegacyAttackDecisionSettings,
                    profile.LegacyAttackTimingSettings.ToRuntimeSettings(simulationTicksPerSecond),
                    ResolveAttackDecisionStrategy(profile.LegacyAttackDecisionStrategyKind));
            }

            EnemyMovementSkillCapabilityRuntime movementSkill = null;
            if (profile.LegacyMovementSkillStrategyKind != MovementSkillStrategyKind.None)
            {
                movementSkill = new EnemyMovementSkillCapabilityRuntime(
                    profile.LegacyMovementSkillStrategyKind,
                    profile.LegacyJumpTimingSettings.ToRuntimeSettings(simulationTicksPerSecond));
            }

            return new EnemyCapabilityRuntimeSet(combat, movementSkill);
        }

        private static EnemyStateResolverRuntime ResolveStateResolver(EnemyAiStateResolverKind kind)
        {
            return kind switch
            {
                EnemyAiStateResolverKind.Default => new EnemyStateResolverRuntime(kind, DefaultEnemyAiStateResolver.Instance),
                EnemyAiStateResolverKind.Charge => new EnemyStateResolverRuntime(kind, ChargingEnemyAiStateResolver.Instance),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown enemy AI state resolver kind."),
            };
        }

        private static EnemyPatrolRuntime ResolvePatrol(
            PatrolStrategyKind kind,
            PatrolSettings settings)
        {
            return kind switch
            {
                PatrolStrategyKind.Forward => new EnemyPatrolRuntime(kind, settings, ForwardPatrolStrategy.Instance),
                PatrolStrategyKind.WallFollow => new EnemyPatrolRuntime(kind, settings, WallFollowPatrolStrategy.Instance),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown patrol strategy kind."),
            };
        }

        private static EnemyDetectionRuntime ResolveDetection(
            DetectionStrategyKind kind,
            DetectionSettings settings)
        {
            return kind switch
            {
                DetectionStrategyKind.NearestOpponent => new EnemyDetectionRuntime(kind, settings, NearestOpponentDetectionStrategy.Instance),
                DetectionStrategyKind.None => new EnemyDetectionRuntime(kind, settings, NoDetectionStrategy.Instance),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown detection strategy kind."),
            };
        }

        private static EnemyChaseRuntime ResolveChase(
            ChaseStrategyKind kind,
            ChaseSettings settings)
        {
            return kind switch
            {
                ChaseStrategyKind.AxisPriority => new EnemyChaseRuntime(kind, settings, AxisPriorityChaseStrategy.Instance),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown chase strategy kind."),
            };
        }

        private static IAttackDecisionStrategy ResolveAttackDecisionStrategy(AttackDecisionStrategyKind kind)
        {
            return kind switch
            {
                AttackDecisionStrategyKind.Melee => MeleeAttackDecisionStrategy.Instance,
                AttackDecisionStrategyKind.ContactSameCell => ContactSameCellAttackDecisionStrategy.Instance,
                AttackDecisionStrategyKind.None => NoAttackDecisionStrategy.Instance,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown attack decision strategy kind."),
            };
        }
    }
}

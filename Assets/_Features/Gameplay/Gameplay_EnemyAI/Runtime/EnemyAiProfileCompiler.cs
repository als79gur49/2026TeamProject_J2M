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

            if (profile.CoreAuthoring == null)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{profile.name}' is missing {nameof(EnemyCoreAuthoring)} authoring.",
                    nameof(profile));
            }

            if (profile.BrainAuthoring == null)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{profile.name}' is missing {nameof(EnemyBrainAuthoring)} authoring.",
                    nameof(profile));
            }

            var core = profile.CoreAuthoring.Compile(simulationTicksPerSecond);
            var brain = profile.BrainAuthoring.Compile();
            var capabilities = CompileCapabilities(profile.name, profile.CapabilityAssets, simulationTicksPerSecond);
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
    }
}

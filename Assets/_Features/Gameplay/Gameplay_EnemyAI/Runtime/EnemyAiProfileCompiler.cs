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
            EnemyPassiveContactCapabilityRuntime passiveContact = null;
            EnemyUtilityCapabilityRuntime utility = null;
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

                    case EnemyCapabilityFamily.PassiveContact:
                        if (passiveContact != null)
                        {
                            throw new ArgumentException(
                                $"Enemy AI profile '{profileName}' declares multiple passive contact capabilities ('{passiveContact.Kind}' and '{capabilityAsset.name}').",
                                nameof(capabilityAssets));
                        }

                        passiveContact = runtime as EnemyPassiveContactCapabilityRuntime
                            ?? throw new ArgumentException(
                                $"Enemy AI profile '{profileName}' compiled an invalid passive contact capability runtime from '{capabilityAsset.name}'.",
                                nameof(capabilityAssets));
                        break;

                    case EnemyCapabilityFamily.Utility:
                        if (utility != null)
                        {
                            throw new ArgumentException(
                                $"Enemy AI profile '{profileName}' declares multiple utility capabilities ('{capabilityAsset.name}' and '{utility.GetType().Name}').",
                                nameof(capabilityAssets));
                        }

                        utility = runtime as EnemyUtilityCapabilityRuntime
                            ?? throw new ArgumentException(
                                $"Enemy AI profile '{profileName}' compiled an invalid utility capability runtime from '{capabilityAsset.name}'.",
                                nameof(capabilityAssets));
                        break;

                    case EnemyCapabilityFamily.RetiredFrontFaceSupport:
                        throw new ArgumentException(
                            $"Enemy AI profile '{profileName}' contains retired front-face support capability '{capabilityAsset.name}'.",
                            nameof(capabilityAssets));

                    default:
                        throw new ArgumentOutOfRangeException(nameof(runtime), runtime.Family, "Unknown enemy capability family.");
                }
            }

            return new EnemyCapabilityRuntimeSet(combat, movementSkill, passiveContact, utility);
        }
    }
}

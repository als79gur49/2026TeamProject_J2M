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
            var behaviors = CompileBehaviors(profile.name, profile.BehaviorModuleAssets, simulationTicksPerSecond);
            ValidateBehaviorRequirements(profile, behaviors);
            return new EnemyAiRuntimeDefinition(core, brain, capabilities, behaviors);
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

        private static EnemyBehaviorRuntimeSet CompileBehaviors(
            string profileName,
            IReadOnlyList<EnemyBehaviorModuleAsset> behaviorModuleAssets,
            int simulationTicksPerSecond)
        {
            EnemyChargeBehaviorRuntime charge = null;
            var behaviorCount = behaviorModuleAssets?.Count ?? 0;
            var context = new EnemyBehaviorModuleCompileContext(profileName, simulationTicksPerSecond);

            for (var i = 0; i < behaviorCount; i++)
            {
                var behaviorAsset = behaviorModuleAssets[i];
                if (behaviorAsset == null)
                {
                    throw new ArgumentException(
                        $"Enemy AI profile '{profileName}' contains a null behavior module entry.",
                        nameof(behaviorModuleAssets));
                }

                var runtime = behaviorAsset.Compile(context);
                if (runtime == null)
                {
                    throw new ArgumentException(
                        $"Enemy AI profile '{profileName}' behavior module '{behaviorAsset.name}' compiled a null runtime.",
                        nameof(behaviorModuleAssets));
                }

                switch (runtime.Key)
                {
                    case EnemyBehaviorModuleKey.Charge:
                        if (charge != null)
                        {
                            throw new ArgumentException(
                                $"Enemy AI profile '{profileName}' declares multiple behavior modules with key '{EnemyBehaviorModuleKey.Charge}' ('{charge.GetType().Name}' and '{behaviorAsset.name}').",
                                nameof(behaviorModuleAssets));
                        }

                        charge = runtime as EnemyChargeBehaviorRuntime
                            ?? throw new ArgumentException(
                                $"Enemy AI profile '{profileName}' compiled an invalid charge behavior module runtime from '{behaviorAsset.name}'.",
                                nameof(behaviorModuleAssets));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(runtime),
                            runtime.Key,
                            "Unknown enemy behavior module key.");
                }
            }

            return new EnemyBehaviorRuntimeSet(charge);
        }

        private static void ValidateBehaviorRequirements(
            EnemyAiProfile profile,
            in EnemyBehaviorRuntimeSet behaviors)
        {
            if (profile.BrainAuthoring.StateResolver != null &&
                profile.BrainAuthoring.StateResolver.RequiresChargeBehavior &&
                !behaviors.HasCharge)
            {
                throw new ArgumentException(
                    $"Enemy AI profile '{profile.name}' uses charge resolver '{profile.BrainAuthoring.StateResolver.name}' but does not declare a '{EnemyBehaviorModuleKey.Charge}' behavior module.",
                    nameof(profile));
            }
        }
    }
}

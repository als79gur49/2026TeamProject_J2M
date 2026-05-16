using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Vfx.Authoring
{
    public static class VfxBindingDiagnostics
    {
        public static VfxAuthoringValidationResult ValidateBinding(VfxBindingDefinitionAsset binding)
        {
            var messages = new List<VfxAuthoringValidationMessage>();
            AppendBindingMessages(binding, messages);
            return VfxAuthoringValidationResult.FromMessages(messages);
        }

        public static void AppendBindingMessages(
            VfxBindingDefinitionAsset binding,
            ICollection<VfxAuthoringValidationMessage> messages)
        {
            if (messages == null)
            {
                throw new ArgumentNullException(nameof(messages));
            }

            if (binding == null)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_BINDING_NULL",
                    "VFX binding asset entry cannot be null."));
                return;
            }

            var cueId = binding.CueId;
            if (cueId.IsNone)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_BINDING_CUE_NONE",
                    $"{binding.name} contains an empty VFX cue id.",
                    binding));
            }

            if (binding.InitialPoolSize < 0)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_BINDING_INITIAL_POOL_NEGATIVE",
                    $"{binding.name} cue '{cueId}' initial pool size cannot be negative.",
                    binding));
            }

            if (binding.Requirement == VfxBindingRequirement.Required &&
                binding.MissingAnchorPolicy == VfxMissingAnchorPolicy.SkipOptional)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_BINDING_REQUIRED_SKIP_OPTIONAL",
                    $"{binding.name} cue '{cueId}' is required but uses SkipOptional missing-anchor policy.",
                    binding));
            }

            try
            {
                binding.CreateRuntimePolicy().ValidateOrThrow();
            }
            catch (InvalidOperationException exception)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_BINDING_POLICY_INVALID",
                    $"{binding.name} cue '{cueId}' has invalid runtime policy: {exception.Message}",
                    binding));
            }

            var prefabValidation = VfxPrefabValidationDiagnostics.ValidatePrefab(binding.Prefab, binding);
            for (var i = 0; i < prefabValidation.Messages.Count; i++)
            {
                messages.Add(prefabValidation.Messages[i]);
            }

            var modelRootValidation = VfxPrefabValidationDiagnostics.ValidateModelRootContract(binding.Prefab, binding);
            for (var i = 0; i < modelRootValidation.Messages.Count; i++)
            {
                messages.Add(modelRootValidation.Messages[i]);
            }
        }
    }
}

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

            messages.Add(VfxAuthoringValidationResult.Info(
                "VFX_BINDING_VISIBILITY_MODE",
                $"{binding.name} cue '{cueId}' uses visibility mode '{binding.VisibilityMode}'.",
                binding));

            if (binding.VisibilityMode == GameplayVfxVisibilityMode.PresentationOnly &&
                !IsPresentationOnlyBindingAllowed(binding))
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_BINDING_PRESENTATION_ONLY_REQUIRES_ALLOWLIST",
                    $"{binding.name} cue '{cueId}' uses PresentationOnly visibility without an explicit presentation/topology helper allowlist marker.",
                    binding));
            }

            if (binding.AllowTopologyHelperExempt &&
                !GameplayVfxTopologyHelperExemptionPolicy.IsTopologyHelperCue(cueId))
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_BINDING_TOPOLOGY_HELPER_EXEMPT_REQUIRES_HELPER_CUE",
                    $"{binding.name} cue '{cueId}' enables topology helper exemption for a non-helper gameplay cue.",
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

            if (binding.VisualSourceMode == VfxVisualSourceMode.SourceCloneMotion &&
                binding.HostRequirement != GameplayVfxHostRequirement.CommonHostAllowed)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_BINDING_INVALID_PLAYBACK_MODE_POLICY",
                    $"{binding.name} cue '{cueId}' uses SourceCloneMotion without CommonHostAllowed host requirement.",
                    binding));
            }

            if (binding.VisualSourceMode == VfxVisualSourceMode.PrefabOnly &&
                binding.HostRequirement != GameplayVfxHostRequirement.ExplicitPrefabRequired)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_BINDING_INVALID_PLAYBACK_MODE_POLICY",
                    $"{binding.name} cue '{cueId}' uses PrefabOnly without ExplicitPrefabRequired host requirement.",
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

            var prefabOptionalForSourceClone =
                binding.VisualSourceMode == VfxVisualSourceMode.SourceCloneMotion &&
                binding.HostRequirement == GameplayVfxHostRequirement.CommonHostAllowed &&
                binding.Prefab == null;
            if (prefabOptionalForSourceClone)
            {
                return;
            }

            var prefabValidation = VfxPrefabValidationDiagnostics.ValidatePrefab(binding.Prefab, binding);
            for (var i = 0; i < prefabValidation.Messages.Count; i++)
            {
                messages.Add(prefabValidation.Messages[i]);
            }

            if (binding.Prefab != null)
            {
                var modelRootValidation = VfxPrefabValidationDiagnostics.ValidateModelRootContract(binding.Prefab, binding);
                for (var i = 0; i < modelRootValidation.Messages.Count; i++)
                {
                    messages.Add(modelRootValidation.Messages[i]);
                }
            }
        }

        private static bool IsPresentationOnlyBindingAllowed(VfxBindingDefinitionAsset binding)
        {
            var assetName = binding.name ?? string.Empty;
            return binding.AllowTopologyHelperExempt ||
                   assetName.IndexOf("Topology", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   assetName.IndexOf("PresentationOnly", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

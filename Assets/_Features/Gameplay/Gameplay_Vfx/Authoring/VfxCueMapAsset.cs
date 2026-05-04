using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Authoring
{
    [CreateAssetMenu(menuName = "Game/VFX/VFX Cue Map")]
    public sealed class VfxCueMapAsset : ScriptableObject
    {
        [SerializeField] private VfxBindingDefinitionAsset[] bindings = Array.Empty<VfxBindingDefinitionAsset>();

        private void OnValidate()
        {
            LogValidationMessages(ValidateAuthoring());
        }

        public VfxCueMap BuildRuntimeMap()
        {
            ValidateAuthoring().ThrowIfErrors();
            return new VfxCueMap(GetOrderedBindings().Select(binding => binding.BuildRuntimePolicy()));
        }

        public VfxAuthoringValidationResult ValidateAuthoring()
        {
            var messages = new List<VfxAuthoringValidationMessage>();
            AppendBindingArrayMessages(bindings, messages);
            return VfxAuthoringValidationResult.FromMessages(messages);
        }

        internal IEnumerable<VfxBindingDefinitionAsset> GetOrderedBindings()
        {
            return (bindings ?? Array.Empty<VfxBindingDefinitionAsset>())
                .Where(binding => binding != null)
                .OrderBy(binding => binding.CueId);
        }

        internal static void AppendBindingArrayMessages(
            IReadOnlyList<VfxBindingDefinitionAsset> bindings,
            ICollection<VfxAuthoringValidationMessage> messages,
            GameplayVfxFamily? requiredFamily = null,
            UnityEngine.Object ownerContext = null,
            string ownerName = null)
        {
            if (messages == null)
            {
                throw new ArgumentNullException(nameof(messages));
            }

            var seen = new HashSet<GameplayVfxCueId>();
            var entries = bindings ?? Array.Empty<VfxBindingDefinitionAsset>();
            for (var i = 0; i < entries.Count; i++)
            {
                var binding = entries[i];
                if (binding == null)
                {
                    messages.Add(VfxAuthoringValidationResult.Error(
                        "VFX_CUE_MAP_NULL_BINDING",
                        $"{ownerName ?? "VFX cue map"} contains a null binding entry at index {i}.",
                        ownerContext));
                    continue;
                }

                var cueId = binding.CueId;
                if (!cueId.IsNone && !seen.Add(cueId))
                {
                    messages.Add(VfxAuthoringValidationResult.Error(
                        "VFX_CUE_MAP_DUPLICATE_CUE",
                        $"{ownerName ?? "VFX cue map"} contains duplicate VFX cue '{cueId}'.",
                        binding));
                }

                if (requiredFamily.HasValue &&
                    !cueId.IsNone &&
                    cueId.Family != requiredFamily.Value)
                {
                    messages.Add(VfxAuthoringValidationResult.Error(
                        "VFX_PROFILE_CROSS_FAMILY_BINDING",
                        $"{ownerName ?? "VFX profile"} family '{requiredFamily.Value}' cannot contain cue '{cueId}'.",
                        binding));
                }

                VfxBindingDiagnostics.AppendBindingMessages(binding, messages);
            }
        }

        private void LogValidationMessages(VfxAuthoringValidationResult result)
        {
            for (var i = 0; i < result.Messages.Count; i++)
            {
                var message = result.Messages[i];
                if (message.Severity == VfxAuthoringValidationSeverity.Error)
                {
                    Debug.LogError(message.ToString(), message.Context != null ? message.Context : this);
                }
                else if (message.Severity == VfxAuthoringValidationSeverity.Warning)
                {
                    Debug.LogWarning(message.ToString(), message.Context != null ? message.Context : this);
                }
            }
        }
    }
}

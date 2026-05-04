using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Authoring
{
    [CreateAssetMenu(menuName = "Game/VFX/VFX Profile")]
    public sealed class VfxProfileAsset : ScriptableObject
    {
#pragma warning disable 0649
        [SerializeField] private GameplayVfxFamily family;
#pragma warning restore 0649
        [SerializeField] private VfxBindingDefinitionAsset[] bindings = Array.Empty<VfxBindingDefinitionAsset>();

        public GameplayVfxFamily Family => family;

        private void OnValidate()
        {
            LogValidationMessages(ValidateAuthoring());
        }

        public VfxProfile BuildRuntimeProfile()
        {
            ValidateAuthoring().ThrowIfErrors();
            return new VfxProfile(family, GetOrderedBindings().Select(binding => binding.BuildRuntimePolicy()));
        }

        public VfxAuthoringValidationResult ValidateAuthoring()
        {
            var messages = new List<VfxAuthoringValidationMessage>();
            if (family == GameplayVfxFamily.None)
            {
                messages.Add(VfxAuthoringValidationResult.Error(
                    "VFX_PROFILE_FAMILY_NONE",
                    $"{name} VFX profile family cannot be None.",
                    this));
            }

            VfxCueMapAsset.AppendBindingArrayMessages(
                bindings,
                messages,
                family == GameplayVfxFamily.None ? null : family,
                this,
                name);

            return VfxAuthoringValidationResult.FromMessages(messages);
        }

        private IEnumerable<VfxBindingDefinitionAsset> GetOrderedBindings()
        {
            return (bindings ?? Array.Empty<VfxBindingDefinitionAsset>())
                .Where(binding => binding != null)
                .OrderBy(binding => binding.CueId);
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

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay;
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

        public bool TryResolvePrefab(GameplayVfxCueId cueId, out GameObject prefab)
        {
            return TryResolvePrefab(cueId, VfxStyleKey.Default, out prefab);
        }

        public bool TryResolvePrefab(GameplayVfxCueId cueId, VfxStyleKey styleKey, out GameObject prefab)
        {
            if (cueId.Family != family)
            {
                prefab = null;
                return false;
            }

            foreach (var binding in GetOrderedBindings())
            {
                if (binding.CueId == cueId &&
                    binding.StyleKey == styleKey)
                {
                    prefab = binding.Prefab;
                    return prefab != null;
                }
            }

            prefab = null;
            return false;
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
                .OrderBy(binding => binding.BindingKey);
        }

        private void LogValidationMessages(VfxAuthoringValidationResult result)
        {
            for (var i = 0; i < result.Messages.Count; i++)
            {
                var message = result.Messages[i];
                if (message.Severity == VfxAuthoringValidationSeverity.Error)
                {
                    UnityEngine.Debug.LogError(message.ToString(), message.Context != null ? message.Context : this);
                }
                else if (message.Severity == VfxAuthoringValidationSeverity.Warning)
                {
                    UnityEngine.Debug.LogWarning(message.ToString(), message.Context != null ? message.Context : this);
                }
            }
        }
    }
}

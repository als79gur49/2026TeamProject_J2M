using System;
using System.Collections.Generic;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.ActionAudio
{
    [Serializable]
    public struct GameplayActionAudioEntry
    {
        public GameplayActionKind Action;
        public GameplayActionAudioMoment Moment;
        public AudioBinding Binding;
        public bool IsOptional;
    }

    [CreateAssetMenu(menuName = "Game/Audio/Gameplay Action Audio Profile")]
    public sealed class GameplayActionAudioProfile : ScriptableObject
    {
        private static readonly AudioCategory[] OneShotSfxCategories = { AudioCategory.Sfx };

        [SerializeField] private GameplayActionAudioEntry[] entries = Array.Empty<GameplayActionAudioEntry>();

        private void OnValidate()
        {
            var validationErrors = CollectValidationErrors();
            for (var i = 0; i < validationErrors.Count; i++)
            {
                UnityEngine.Debug.LogError(validationErrors[i], this);
            }
        }

        public bool TryResolve(
            GameplayActionKind action,
            GameplayActionAudioMoment moment,
            out AudioBinding binding)
        {
            binding = null;
            var found = false;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Action != action ||
                    entries[i].Moment != moment)
                {
                    continue;
                }

                if (found)
                {
                    throw new InvalidOperationException(
                        $"{name} contains duplicate gameplay action audio entry '{action}/{moment}'.");
                }

                found = true;
                binding = entries[i].Binding;
            }

            if (!found)
            {
                return false;
            }

            if (binding != null)
            {
                return true;
            }

            binding = null;
            return false;
        }

        public void ValidateOrThrow()
        {
            var validationErrors = CollectValidationErrors();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(validationErrors[0]);
            }
        }

        private List<string> CollectValidationErrors()
        {
            var validationErrors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var entryLabel = $"{entry.Action}/{entry.Moment}";
                if (!seen.Add(entryLabel))
                {
                    validationErrors.Add(
                        $"{name} contains duplicate gameplay action audio entry '{entryLabel}'.");
                }

                AudioBindingDiagnostics.AppendValidationErrors(
                    entry.Binding,
                    name,
                    $"entry '{entryLabel}'",
                    validationErrors,
                    new AudioBindingValidationOptions(
                        OneShotSfxCategories,
                        allowLoopingDefinitions: false,
                        allowNullBinding: entry.IsOptional));
            }

            return validationErrors;
        }
    }
}

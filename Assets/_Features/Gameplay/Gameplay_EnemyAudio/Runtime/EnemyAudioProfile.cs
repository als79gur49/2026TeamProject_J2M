using System;
using System.Collections.Generic;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.EnemyAudio
{
    [Serializable]
    public struct EnemyAudioEntry
    {
        public EnemyAudioCue Cue;
        public AudioBinding Binding;
        public bool IsOptional;
    }

    [CreateAssetMenu(menuName = "Game/Audio/Enemy Audio Profile")]
    public sealed class EnemyAudioProfile : ScriptableObject
    {
        private static readonly AudioCategory[] OneShotSfxCategories = { AudioCategory.Sfx };

        [SerializeField] private EnemyAudioEntry[] entries = Array.Empty<EnemyAudioEntry>();

        private void OnValidate()
        {
            var validationErrors = CollectValidationErrors();
            for (var i = 0; i < validationErrors.Count; i++)
            {
                UnityEngine.Debug.LogError(validationErrors[i], this);
            }
        }

        public bool HasCue(EnemyAudioCue cue)
        {
            if (cue == EnemyAudioCue.None)
            {
                return false;
            }

            return TryResolve(cue, out _);
        }

        public bool TryResolve(EnemyAudioCue cue, out AudioBinding binding)
        {
            binding = null;
            var found = false;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Cue != cue)
                {
                    continue;
                }

                if (found)
                {
                    throw new InvalidOperationException(
                        $"{name} contains duplicate enemy audio cue '{EnemyAudioCueCatalog.Format(cue)}'.");
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
            var seen = new HashSet<EnemyAudioCue>();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var cueLabel = entry.Cue == EnemyAudioCue.None
                    ? "<empty>"
                    : EnemyAudioCueCatalog.Format(entry.Cue);
                if (entry.Cue == EnemyAudioCue.None)
                {
                    validationErrors.Add($"{name} contains an empty enemy audio cue.");
                }
                else if (!seen.Add(entry.Cue))
                {
                    validationErrors.Add(
                        $"{name} contains duplicate enemy audio cue '{cueLabel}'.");
                }

                AudioBindingDiagnostics.AppendValidationErrors(
                    entry.Binding,
                    name,
                    $"cue '{cueLabel}'",
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

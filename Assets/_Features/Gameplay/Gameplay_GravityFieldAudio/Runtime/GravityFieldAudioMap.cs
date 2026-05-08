using System;
using System.Collections.Generic;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.GravityFieldAudio
{
    [CreateAssetMenu(menuName = "Game/Audio/Gravity Field Audio Map")]
    public sealed class GravityFieldAudioMap : ScriptableObject
    {
        private static readonly AudioCategory[] OneShotSfxCategories = { AudioCategory.Sfx };

        [Serializable]
        private struct Entry
        {
            public GravityFieldAudioCue Cue;
            public AudioBinding Binding;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private void OnValidate()
        {
            var validationErrors = CollectValidationErrors();
            for (var i = 0; i < validationErrors.Count; i++)
            {
                UnityEngine.Debug.LogError(validationErrors[i], this);
            }
        }

        public bool TryResolveOptional(GravityFieldAudioCue cue, out AudioBinding binding)
        {
            if (cue == GravityFieldAudioCue.None)
            {
                throw new ArgumentException("GravityField audio cue cannot be None.", nameof(cue));
            }

            var cueLabel = GravityFieldAudioCueCatalog.Format(cue);
            var found = false;
            binding = null;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Cue != cue)
                {
                    continue;
                }

                if (found)
                {
                    throw new InvalidOperationException(
                        $"{name} contains duplicate GravityField audio cue '{cueLabel}'.");
                }

                found = true;
                binding = entries[i].Binding;
            }

            if (!found)
            {
                return false;
            }

            AudioBindingDiagnostics.ValidateOrThrow(
                binding,
                name,
                $"cue '{cueLabel}'",
                CreateValidationOptions());
            return true;
        }

        public void ValidateOrThrow()
        {
            var validationErrors = CollectValidationErrors();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(validationErrors[0]);
            }
        }

        public void ValidateRequiredCuesOrThrow(IReadOnlyList<GravityFieldAudioCue> requiredCues)
        {
            if (requiredCues == null)
            {
                throw new ArgumentNullException(nameof(requiredCues));
            }

            ValidateOrThrow();
        }

        private static AudioBindingValidationOptions CreateValidationOptions()
        {
            return new AudioBindingValidationOptions(
                OneShotSfxCategories,
                allowLoopingDefinitions: false,
                allowNullBinding: false);
        }

        private List<string> CollectValidationErrors()
        {
            var validationErrors = new List<string>();
            var seen = new HashSet<GravityFieldAudioCue>();
            for (var i = 0; i < entries.Length; i++)
            {
                var cue = entries[i].Cue;
                var cueLabel = cue == GravityFieldAudioCue.None
                    ? "<empty>"
                    : GravityFieldAudioCueCatalog.Format(cue);
                if (cue == GravityFieldAudioCue.None)
                {
                    validationErrors.Add($"{name} contains an empty GravityField audio cue.");
                }
                else if (!seen.Add(cue))
                {
                    validationErrors.Add(
                        $"{name} contains duplicate GravityField audio cue '{cueLabel}'.");
                }

                AudioBindingDiagnostics.AppendValidationErrors(
                    entries[i].Binding,
                    name,
                    $"cue '{cueLabel}'",
                    validationErrors,
                    CreateValidationOptions());
            }

            return validationErrors;
        }
    }
}

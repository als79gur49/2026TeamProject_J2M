using System;
using System.Collections.Generic;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.TileFeatureAudio
{
    [CreateAssetMenu(menuName = "Game/Audio/Tile Feature Audio Map")]
    public sealed class TileFeatureAudioMap : ScriptableObject
    {
        private static readonly AudioCategory[] OneShotSfxCategories = { AudioCategory.Sfx };

        [Serializable]
        private struct Entry
        {
            public TileFeatureAudioCue Cue;
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

        public AudioBinding ResolveOrThrow(TileFeatureAudioCue cue)
        {
            if (cue == TileFeatureAudioCue.None)
            {
                throw new ArgumentException("Tile feature audio cue cannot be None.", nameof(cue));
            }

            var cueLabel = TileFeatureAudioCueCatalog.Format(cue);
            var found = false;
            AudioBinding resolved = null;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Cue != cue)
                {
                    continue;
                }

                if (found)
                {
                    throw new InvalidOperationException(
                        $"{name} contains duplicate tile feature audio cue '{cueLabel}'.");
                }

                found = true;
                resolved = entries[i].Binding;
            }

            if (!found)
            {
                throw new InvalidOperationException(
                    $"{name} is missing tile feature audio cue '{cueLabel}'.");
            }

            AudioBindingDiagnostics.ValidateOrThrow(
                resolved,
                name,
                $"cue '{cueLabel}'",
                CreateValidationOptions());
            return resolved;
        }

        public bool TryResolveOptional(TileFeatureAudioCue cue, out AudioBinding binding)
        {
            if (cue == TileFeatureAudioCue.None)
            {
                throw new ArgumentException("Tile feature audio cue cannot be None.", nameof(cue));
            }

            var cueLabel = TileFeatureAudioCueCatalog.Format(cue);
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
                        $"{name} contains duplicate tile feature audio cue '{cueLabel}'.");
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

        public void ValidateRequiredCuesOrThrow(IReadOnlyList<TileFeatureAudioCue> requiredCues)
        {
            if (requiredCues == null)
            {
                throw new ArgumentNullException(nameof(requiredCues));
            }

            ValidateOrThrow();

            var missingCueLabels = new List<string>();
            var seenRequired = new HashSet<TileFeatureAudioCue>();
            for (var i = 0; i < requiredCues.Count; i++)
            {
                var requiredCue = requiredCues[i];
                if (requiredCue == TileFeatureAudioCue.None ||
                    !seenRequired.Add(requiredCue))
                {
                    continue;
                }

                if (!ContainsCue(requiredCue))
                {
                    missingCueLabels.Add(TileFeatureAudioCueCatalog.Format(requiredCue));
                }
            }

            if (missingCueLabels.Count > 0)
            {
                throw new InvalidOperationException(
                    $"TileFeatureAudioMap '{name}' is missing required tile feature audio cues: {string.Join(", ", missingCueLabels)}.");
            }
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
            var seen = new HashSet<TileFeatureAudioCue>();
            for (var i = 0; i < entries.Length; i++)
            {
                var cue = entries[i].Cue;
                var cueLabel = cue == TileFeatureAudioCue.None
                    ? "<empty>"
                    : TileFeatureAudioCueCatalog.Format(cue);
                if (cue == TileFeatureAudioCue.None)
                {
                    validationErrors.Add($"{name} contains an empty tile feature audio cue.");
                }
                else if (!seen.Add(cue))
                {
                    validationErrors.Add(
                        $"{name} contains duplicate tile feature audio cue '{cueLabel}'.");
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

        private bool ContainsCue(TileFeatureAudioCue cue)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Cue == cue)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

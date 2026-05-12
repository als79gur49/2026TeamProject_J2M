using System;
using System.Collections.Generic;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.BlockAudio
{
    [CreateAssetMenu(menuName = "Game/Audio/Block Audio Map")]
    public sealed class BlockAudioMap : ScriptableObject
    {
        private static readonly AudioCategory[] OneShotSfxCategories = { AudioCategory.Sfx };

        [Serializable]
        private struct Entry
        {
            public BlockAudioCue Cue;
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

        public AudioBinding ResolveOrThrow(BlockAudioCue cue)
        {
            if (cue == BlockAudioCue.None)
            {
                throw new ArgumentException("Block audio cue cannot be None.", nameof(cue));
            }

            var cueLabel = BlockAudioCueCatalog.Format(cue);
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
                        $"{name} contains duplicate block audio cue '{cueLabel}'.");
                }

                found = true;
                resolved = entries[i].Binding;
            }

            if (!found)
            {
                throw new InvalidOperationException(
                    $"{name} is missing block audio cue '{cueLabel}'.");
            }

            AudioBindingDiagnostics.ValidateOrThrow(
                resolved,
                name,
                $"cue '{cueLabel}'",
                CreateValidationOptions());
            return resolved;
        }

        public void ValidateRequiredCuesOrThrow(IReadOnlyList<BlockAudioCue> requiredCues)
        {
            if (requiredCues == null)
            {
                throw new ArgumentNullException(nameof(requiredCues));
            }

            ValidateOrThrow();

            var missingCueLabels = new List<string>();
            var seenRequired = new HashSet<BlockAudioCue>();
            for (var i = 0; i < requiredCues.Count; i++)
            {
                var requiredCue = requiredCues[i];
                if (requiredCue == BlockAudioCue.None ||
                    !seenRequired.Add(requiredCue))
                {
                    continue;
                }

                if (!ContainsCue(requiredCue))
                {
                    missingCueLabels.Add(BlockAudioCueCatalog.Format(requiredCue));
                }
            }

            if (missingCueLabels.Count > 0)
            {
                throw new InvalidOperationException(
                    $"BlockAudioMap '{name}' is missing required block audio cues: {string.Join(", ", missingCueLabels)}.");
            }
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
            var seen = new HashSet<BlockAudioCue>();
            for (var i = 0; i < entries.Length; i++)
            {
                var cue = entries[i].Cue;
                var cueLabel = cue == BlockAudioCue.None
                    ? "<empty>"
                    : BlockAudioCueCatalog.Format(cue);
                if (cue == BlockAudioCue.None)
                {
                    validationErrors.Add($"{name} contains an empty block audio cue.");
                }
                else if (!seen.Add(cue))
                {
                    validationErrors.Add(
                        $"{name} contains duplicate block audio cue '{cueLabel}'.");
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

        private bool ContainsCue(BlockAudioCue cue)
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

        private static AudioBindingValidationOptions CreateValidationOptions()
        {
            return new AudioBindingValidationOptions(
                OneShotSfxCategories,
                allowLoopingDefinitions: false,
                allowNullBinding: false);
        }
    }
}

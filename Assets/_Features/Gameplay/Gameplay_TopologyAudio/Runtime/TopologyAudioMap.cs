using System;
using System.Collections.Generic;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.TopologyAudio
{
    [CreateAssetMenu(menuName = "Game/Audio/Topology Audio Map")]
    public sealed class TopologyAudioMap : ScriptableObject
    {
        private static readonly AudioCategory[] OneShotSfxCategories = { AudioCategory.Sfx };

        [Serializable]
        private struct Entry
        {
            public TopologyAudioCue Cue;
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

        public AudioBinding ResolveOrThrow(TopologyAudioCue cue)
        {
            if (cue == TopologyAudioCue.None)
            {
                throw new ArgumentException("Topology audio cue cannot be None.", nameof(cue));
            }

            var cueLabel = TopologyAudioCueCatalog.Format(cue);
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
                        $"{name} contains duplicate topology audio cue '{cueLabel}'.");
                }

                found = true;
                resolved = entries[i].Binding;
            }

            if (!found)
            {
                throw new InvalidOperationException(
                    $"{name} is missing topology audio cue '{cueLabel}'.");
            }

            AudioBindingDiagnostics.ValidateOrThrow(
                resolved,
                name,
                $"cue '{cueLabel}'",
                CreateValidationOptions());
            return resolved;
        }

        public void ValidateRequiredCuesOrThrow(IReadOnlyList<TopologyAudioCue> requiredCues)
        {
            if (requiredCues == null)
            {
                throw new ArgumentNullException(nameof(requiredCues));
            }

            ValidateOrThrow();

            var missingCueLabels = new List<string>();
            var seenRequired = new HashSet<TopologyAudioCue>();
            for (var i = 0; i < requiredCues.Count; i++)
            {
                var requiredCue = requiredCues[i];
                if (requiredCue == TopologyAudioCue.None ||
                    !seenRequired.Add(requiredCue))
                {
                    continue;
                }

                if (!ContainsCue(requiredCue))
                {
                    missingCueLabels.Add(TopologyAudioCueCatalog.Format(requiredCue));
                }
            }

            if (missingCueLabels.Count > 0)
            {
                throw new InvalidOperationException(
                    $"TopologyAudioMap '{name}' is missing required topology audio cues: {string.Join(", ", missingCueLabels)}.");
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
            var seen = new HashSet<TopologyAudioCue>();
            for (var i = 0; i < entries.Length; i++)
            {
                var cue = entries[i].Cue;
                var cueLabel = cue == TopologyAudioCue.None
                    ? "<empty>"
                    : TopologyAudioCueCatalog.Format(cue);
                if (cue == TopologyAudioCue.None)
                {
                    validationErrors.Add($"{name} contains an empty topology audio cue.");
                }
                else if (!seen.Add(cue))
                {
                    validationErrors.Add(
                        $"{name} contains duplicate topology audio cue '{cueLabel}'.");
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

        private bool ContainsCue(TopologyAudioCue cue)
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

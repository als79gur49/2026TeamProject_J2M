using System;
using System.Collections.Generic;
using Game.Feature.UI.Application;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [CreateAssetMenu(menuName = "Game/Audio/UI Audio Cue Map")]
    public sealed class UiAudioCueMap : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField] private UiAudioCueId cueId;
            [SerializeField] private AudioBinding binding;

            public UiAudioCueId CueId => cueId;

            public AudioBinding Binding => binding;
        }

        private static readonly AudioBindingValidationOptions BindingValidationOptions = new(
            new[] { AudioCategory.Ui },
            allowLoopingDefinitions: false,
            allowNullBinding: false);

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public AudioBinding ResolveBindingOrThrow(UiAudioCueId cueId)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].CueId == cueId)
                {
                    return entries[i].Binding;
                }
            }

            throw new InvalidOperationException(
                $"{GetOwnerDescription()} is missing an explicit binding for UiAudioCueId '{cueId}'.");
        }

        public void ValidateOrThrow(string ownerDescription = null)
        {
            var validationErrors = new List<string>();
            AppendValidationErrors(validationErrors, ownerDescription);
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(validationErrors[0]);
            }
        }

        internal void AppendValidationErrors(ICollection<string> validationErrors, string ownerDescription = null)
        {
            if (validationErrors == null)
            {
                throw new ArgumentNullException(nameof(validationErrors));
            }

            var effectiveOwnerDescription = string.IsNullOrWhiteSpace(ownerDescription)
                ? GetOwnerDescription()
                : ownerDescription;

            var seenCueIds = new HashSet<UiAudioCueId>();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (!seenCueIds.Add(entry.CueId))
                {
                    validationErrors.Add(
                        $"{effectiveOwnerDescription} contains duplicate cue entry '{entry.CueId}'.");
                    continue;
                }

                AudioBindingDiagnostics.AppendValidationErrors(
                    entry.Binding,
                    effectiveOwnerDescription,
                    $"cue '{entry.CueId}'",
                    validationErrors,
                    BindingValidationOptions);

                if (entry.Binding != null && entry.Binding.HasAttachmentSlot)
                {
                    validationErrors.Add(
                        $"{effectiveOwnerDescription} cue '{entry.CueId}' configures AudioAttachmentSlot, but UI SFX v1 uses Play2D only and attachment slots must remain empty.");
                }
            }

            var expectedCueIds = (UiAudioCueId[])Enum.GetValues(typeof(UiAudioCueId));
            for (var i = 0; i < expectedCueIds.Length; i++)
            {
                if (!seenCueIds.Contains(expectedCueIds[i]))
                {
                    validationErrors.Add(
                        $"{effectiveOwnerDescription} is missing an explicit binding for UiAudioCueId '{expectedCueIds[i]}'.");
                }
            }
        }

        private string GetOwnerDescription()
        {
            return $"UiAudioCueMap '{name}'";
        }
    }
}

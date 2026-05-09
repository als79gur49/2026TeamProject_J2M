using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.TileFeatureAudio
{
    [Serializable]
    public sealed class MoonBlockGeneratorBlockedAudioBinding
    {
        [SerializeField] private MoonBlockGeneratorBlockedReason reason;
        [SerializeField] private AudioBinding binding;

        public MoonBlockGeneratorBlockedReason Reason => reason;

        public AudioBinding Binding => binding;
    }

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
        [SerializeField] private MoonBlockGeneratorBlockedAudioBinding[] moonBlockGeneratorBlockedReasonBindings =
            Array.Empty<MoonBlockGeneratorBlockedAudioBinding>();

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

        public bool TryResolveMoonBlockGeneratorBlocked(
            in MoonBlockGeneratorBlockedPayload payload,
            out AudioBinding binding)
        {
            if (payload.Reason != MoonBlockGeneratorBlockedReason.None &&
                TryResolveMoonBlockGeneratorBlockedReason(payload.Reason, out binding))
            {
                return true;
            }

            return TryResolveOptional(TileFeatureAudioCue.MoonBlockGeneratorBlocked, out binding);
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

            var seenBlockedReasons = new HashSet<MoonBlockGeneratorBlockedReason>();
            for (var i = 0; i < moonBlockGeneratorBlockedReasonBindings.Length; i++)
            {
                var entry = moonBlockGeneratorBlockedReasonBindings[i];
                var reason = entry?.Reason ?? MoonBlockGeneratorBlockedReason.None;
                var reasonLabel = reason == MoonBlockGeneratorBlockedReason.None
                    ? "<empty>"
                    : reason.ToString();
                if (reason == MoonBlockGeneratorBlockedReason.None)
                {
                    validationErrors.Add($"{name} contains an empty MoonBlockGeneratorBlocked reason audio binding.");
                }
                else if (!seenBlockedReasons.Add(reason))
                {
                    validationErrors.Add(
                        $"{name} contains duplicate MoonBlockGeneratorBlocked reason audio binding '{reasonLabel}'.");
                }

                AudioBindingDiagnostics.AppendValidationErrors(
                    entry?.Binding,
                    name,
                    $"MoonBlockGeneratorBlocked reason '{reasonLabel}'",
                    validationErrors,
                    CreateValidationOptions());
            }

            return validationErrors;
        }

        private bool TryResolveMoonBlockGeneratorBlockedReason(
            MoonBlockGeneratorBlockedReason reason,
            out AudioBinding binding)
        {
            var found = false;
            binding = null;
            for (var i = 0; i < moonBlockGeneratorBlockedReasonBindings.Length; i++)
            {
                var entry = moonBlockGeneratorBlockedReasonBindings[i];
                if (entry == null ||
                    entry.Reason != reason)
                {
                    continue;
                }

                if (found)
                {
                    throw new InvalidOperationException(
                        $"{name} contains duplicate MoonBlockGeneratorBlocked reason audio binding '{reason}'.");
                }

                found = true;
                binding = entry.Binding;
            }

            if (!found)
            {
                return false;
            }

            AudioBindingDiagnostics.ValidateOrThrow(
                binding,
                name,
                $"MoonBlockGeneratorBlocked reason '{reason}'",
                CreateValidationOptions());
            return true;
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

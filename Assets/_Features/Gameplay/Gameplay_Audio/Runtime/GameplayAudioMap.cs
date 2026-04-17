using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Audio
{
    [CreateAssetMenu(menuName = "Game/Audio/Gameplay Audio Map")]
    public sealed class GameplayAudioMap : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string SemanticId;
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

        public AudioBinding ResolveOrThrow(string semanticId)
        {
            if (string.IsNullOrWhiteSpace(semanticId))
            {
                throw new ArgumentException("Gameplay audio semantic id cannot be empty.", nameof(semanticId));
            }

            var found = false;
            AudioBinding resolved = null;
            for (var i = 0; i < entries.Length; i++)
            {
                if (!string.Equals(entries[i].SemanticId, semanticId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found)
                {
                    throw new InvalidOperationException(
                        $"{name} contains duplicate gameplay audio semantic '{semanticId}'.");
                }

                found = true;
                resolved = entries[i].Binding;
            }

            if (!found)
            {
                throw new InvalidOperationException(
                    $"{name} is missing gameplay audio semantic '{semanticId}'.");
            }

            if (resolved == null)
            {
                throw new InvalidOperationException(
                    $"{name} semantic '{semanticId}' is missing an AudioBinding.");
            }

            resolved.ValidateOrThrow(name, semanticId);
            return resolved;
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
                var semanticId = entries[i].SemanticId ?? string.Empty;
                var semanticLabel = string.IsNullOrWhiteSpace(semanticId) ? "<empty>" : semanticId;
                if (string.IsNullOrWhiteSpace(semanticId))
                {
                    validationErrors.Add($"{name} contains an empty gameplay audio semantic.");
                }
                else if (!seen.Add(semanticId))
                {
                    validationErrors.Add(
                        $"{name} contains duplicate gameplay audio semantic '{semanticId}'.");
                }

                var binding = entries[i].Binding;
                if (binding == null)
                {
                    validationErrors.Add(
                        $"{name} semantic '{semanticLabel}' is missing an AudioBinding.");
                    continue;
                }

                binding.AppendValidationErrors(name, semanticLabel, validationErrors);
            }

            return validationErrors;
        }
    }
}

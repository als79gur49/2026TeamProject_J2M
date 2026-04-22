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
            public GameplayAudioSemanticId SemanticId;
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

        public AudioBinding ResolveOrThrow(GameplayAudioSemanticId semanticId)
        {
            if (semanticId == GameplayAudioSemanticId.None)
            {
                throw new ArgumentException("Gameplay audio semantic id cannot be None.", nameof(semanticId));
            }

            var semanticLabel = GameplayAudioSemanticCatalog.Format(semanticId);
            var found = false;
            AudioBinding resolved = null;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].SemanticId != semanticId)
                {
                    continue;
                }

                if (found)
                {
                    throw new InvalidOperationException(
                        $"{name} contains duplicate gameplay audio semantic '{semanticLabel}'.");
                }

                found = true;
                resolved = entries[i].Binding;
            }

            if (!found)
            {
                throw new InvalidOperationException(
                    $"{name} is missing gameplay audio semantic '{semanticLabel}'.");
            }

            if (resolved == null)
            {
                throw new InvalidOperationException(
                    $"{name} semantic '{semanticLabel}' is missing an AudioBinding.");
            }

            resolved.ValidateOrThrow(name, semanticLabel);
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

        public void ValidateRequiredSemanticsOrThrow(IReadOnlyList<GameplayAudioSemanticId> requiredSemanticIds)
        {
            if (requiredSemanticIds == null)
            {
                throw new ArgumentNullException(nameof(requiredSemanticIds));
            }

            ValidateOrThrow();

            var missingSemanticLabels = new List<string>();
            var seenRequired = new HashSet<GameplayAudioSemanticId>();
            for (var i = 0; i < requiredSemanticIds.Count; i++)
            {
                var requiredSemanticId = requiredSemanticIds[i];
                if (requiredSemanticId == GameplayAudioSemanticId.None ||
                    !seenRequired.Add(requiredSemanticId))
                {
                    continue;
                }

                if (!ContainsSemantic(requiredSemanticId))
                {
                    missingSemanticLabels.Add(GameplayAudioSemanticCatalog.Format(requiredSemanticId));
                }
            }

            if (missingSemanticLabels.Count > 0)
            {
                throw new InvalidOperationException(
                    $"GameplayAudioMap '{name}' is missing required gameplay audio semantics: {string.Join(", ", missingSemanticLabels)}.");
            }
        }

        private List<string> CollectValidationErrors()
        {
            var validationErrors = new List<string>();
            var seen = new HashSet<GameplayAudioSemanticId>();
            for (var i = 0; i < entries.Length; i++)
            {
                var semanticId = entries[i].SemanticId;
                var semanticLabel = semanticId == GameplayAudioSemanticId.None
                    ? "<empty>"
                    : GameplayAudioSemanticCatalog.Format(semanticId);
                if (semanticId == GameplayAudioSemanticId.None)
                {
                    validationErrors.Add($"{name} contains an empty gameplay audio semantic.");
                }
                else if (!seen.Add(semanticId))
                {
                    validationErrors.Add(
                        $"{name} contains duplicate gameplay audio semantic '{semanticLabel}'.");
                }

                AudioBindingDiagnostics.AppendValidationErrors(
                    entries[i].Binding,
                    name,
                    $"semantic '{semanticLabel}'",
                    validationErrors,
                    AudioBindingValidationOptions.Default);
            }

            return validationErrors;
        }

        private bool ContainsSemantic(GameplayAudioSemanticId semanticId)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].SemanticId == semanticId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

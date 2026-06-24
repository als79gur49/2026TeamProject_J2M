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
        [SerializeReference]
        public AudioBinding Binding;
        public bool IsOptional;
    }

    public enum GameplayActionAudioProfileDiagnosticSeverity
    {
        Error = 0,
        Warning = 1,
    }

    public readonly struct GameplayActionAudioProfileDiagnostic
    {
        public GameplayActionAudioProfileDiagnostic(
            GameplayActionAudioProfileDiagnosticSeverity severity,
            string message)
        {
            Severity = severity;
            Message = message;
        }

        public GameplayActionAudioProfileDiagnosticSeverity Severity { get; }

        public string Message { get; }
    }

    public enum GameplayActionAudioProfileResolveStatus
    {
        None = 0,
        EntryMissing = 1,
        OptionalBindingMissing = 2,
        BindingMissing = 3,
        Resolved = 4,
    }

    [CreateAssetMenu(menuName = "Game/Audio/Gameplay Action Audio Profile")]
    public sealed class GameplayActionAudioProfile : ScriptableObject
    {
        private static readonly AudioCategory[] OneShotSfxCategories = { AudioCategory.Sfx };

        [SerializeField] private GameplayActionAudioEntry[] entries = Array.Empty<GameplayActionAudioEntry>();

        private void OnValidate()
        {
            var diagnostics = CollectDiagnostics();
            for (var i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == GameplayActionAudioProfileDiagnosticSeverity.Error)
                {
                    UnityEngine.Debug.LogError(diagnostics[i].Message, this);
                }
                else
                {
                    UnityEngine.Debug.LogWarning(diagnostics[i].Message, this);
                }
            }
        }

        public bool ContainsEntry(
            GameplayActionKind action,
            GameplayActionAudioMoment moment)
        {
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
            }

            return found;
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

        public GameplayActionAudioProfileResolveStatus ResolveEntryStatus(
            GameplayActionKind action,
            GameplayActionAudioMoment moment,
            out AudioBinding binding)
        {
            binding = null;
            var found = false;
            var isOptional = false;
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
                isOptional = entries[i].IsOptional;
                binding = entries[i].Binding;
            }

            if (!found)
            {
                return GameplayActionAudioProfileResolveStatus.EntryMissing;
            }

            if (binding != null)
            {
                return GameplayActionAudioProfileResolveStatus.Resolved;
            }

            return isOptional
                ? GameplayActionAudioProfileResolveStatus.OptionalBindingMissing
                : GameplayActionAudioProfileResolveStatus.BindingMissing;
        }

        public void ValidateOrThrow()
        {
            var validationErrors = CollectValidationErrors();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(validationErrors[0]);
            }
        }

        public IReadOnlyList<GameplayActionAudioProfileDiagnostic> CollectDiagnostics()
        {
            var diagnostics = new List<GameplayActionAudioProfileDiagnostic>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var entryLabel = $"{entry.Action}/{entry.Moment}";
                if (!IsKnownMoment(entry.Moment))
                {
                    diagnostics.Add(new GameplayActionAudioProfileDiagnostic(
                        GameplayActionAudioProfileDiagnosticSeverity.Error,
                        $"{name} contains unsupported gameplay action audio moment value '{(int)entry.Moment}'."));
                }

                if (!seen.Add(entryLabel))
                {
                    diagnostics.Add(new GameplayActionAudioProfileDiagnostic(
                        GameplayActionAudioProfileDiagnosticSeverity.Error,
                        $"{name} contains duplicate gameplay action audio entry '{entryLabel}'."));
                }

                if (entry.Binding == null && entry.IsOptional)
                {
                    diagnostics.Add(new GameplayActionAudioProfileDiagnostic(
                        GameplayActionAudioProfileDiagnosticSeverity.Warning,
                        $"{name} optional entry '{entryLabel}' has no assigned AudioBinding."));
                }

                var validationErrors = new List<string>();
                AudioBindingDiagnostics.AppendValidationErrors(
                    entry.Binding,
                    name,
                    $"entry '{entryLabel}'",
                    validationErrors,
                    new AudioBindingValidationOptions(
                        OneShotSfxCategories,
                        allowLoopingDefinitions: false,
                        allowNullBinding: entry.IsOptional));

                for (var errorIndex = 0; errorIndex < validationErrors.Count; errorIndex++)
                {
                    diagnostics.Add(new GameplayActionAudioProfileDiagnostic(
                        GameplayActionAudioProfileDiagnosticSeverity.Error,
                        validationErrors[errorIndex]));
                }
            }

            return diagnostics;
        }

        private List<string> CollectValidationErrors()
        {
            var validationErrors = new List<string>();
            var diagnostics = CollectDiagnostics();
            for (var i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == GameplayActionAudioProfileDiagnosticSeverity.Error)
                {
                    validationErrors.Add(diagnostics[i].Message);
                }
            }

            return validationErrors;
        }

        private static bool IsKnownMoment(GameplayActionAudioMoment moment)
        {
            var moments = GameplayActionAudioMomentCatalog.OrderedMoments;
            for (var i = 0; i < moments.Length; i++)
            {
                if (moments[i] == moment)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

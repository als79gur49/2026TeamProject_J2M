using System;
using System.Collections.Generic;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum StageBgmSlotMode
    {
        None = 0,
        Profile = 1,
    }

    [Serializable]
    public sealed class StageBgmSlot
    {
        [SerializeField] private StageBgmSlotMode mode;
        [SerializeField] private BgmProfile profile;

        public StageBgmSlotMode Mode => mode;

        public BgmProfile Profile => profile;

        public void ValidateOrThrow(string ownerDescription)
        {
            var validationErrors = CollectValidationErrors(ownerDescription);
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(validationErrors[0]);
            }
        }

        internal IReadOnlyList<string> CollectValidationErrors(string ownerDescription)
        {
            var errors = new List<string>();
            ownerDescription = string.IsNullOrWhiteSpace(ownerDescription)
                ? nameof(StageBgmSlot)
                : ownerDescription;

            if (!Enum.IsDefined(typeof(StageBgmSlotMode), mode))
            {
                errors.Add($"{ownerDescription} uses unsupported StageBgmSlotMode '{mode}'.");
                return errors;
            }

            if (mode == StageBgmSlotMode.None && profile != null)
            {
                errors.Add($"{ownerDescription} cannot assign a BgmProfile when mode is None.");
            }

            if (mode == StageBgmSlotMode.Profile)
            {
                if (profile == null)
                {
                    errors.Add($"{ownerDescription} requires a BgmProfile when mode is Profile.");
                }
                else
                {
                    try
                    {
                        profile.ValidateOrThrow();
                    }
                    catch (Exception exception)
                    {
                        errors.Add($"{ownerDescription} profile is invalid: {exception.Message}");
                    }
                }
            }

            return errors;
        }
    }

    [Serializable]
    public sealed class StagePhaseBgmSlot
    {
        [SerializeField] private string phaseId = string.Empty;
        [SerializeField] private StageBgmSlot slot = new();

        public string PhaseId => phaseId?.Trim() ?? string.Empty;

        public StageBgmSlot Slot => slot;
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Audio Definition", fileName = "stage-audio")]
    public sealed class StageAudioDefinition : StageCompanionDefinitionBase
    {
        [SerializeField] private StageBgmSlot gameplayBgm = new();
        [SerializeField] private StageBgmSlot previewBgm = new();
        [SerializeField] private StageBgmSlot clearResultBgm = new();
        [SerializeField] private StageBgmSlot failureResultBgm = new();
        [SerializeField] private StagePhaseBgmSlot[] phaseBgms = Array.Empty<StagePhaseBgmSlot>();

        public StageBgmSlot GameplayBgm => gameplayBgm;

        public StageBgmSlot PreviewBgm => previewBgm;

        public StageBgmSlot ClearResultBgm => clearResultBgm;

        public StageBgmSlot FailureResultBgm => failureResultBgm;

        public IReadOnlyList<StagePhaseBgmSlot> PhaseBgms => phaseBgms ?? Array.Empty<StagePhaseBgmSlot>();

        public void ValidateOrThrow()
        {
            var validationErrors = CollectValidationErrors();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(validationErrors[0]);
            }
        }

        public IReadOnlyList<string> CollectValidationErrors()
        {
            var errors = new List<string>();
            AppendRequiredSlotErrors(gameplayBgm, "gameplayBgm", errors);
            AppendOptionalSlotErrors(previewBgm, "previewBgm", errors);
            AppendOptionalSlotErrors(clearResultBgm, "clearResultBgm", errors);
            AppendOptionalSlotErrors(failureResultBgm, "failureResultBgm", errors);
            AppendPhaseSlotErrors(errors);
            return errors;
        }

        private static void AppendRequiredSlotErrors(
            StageBgmSlot slot,
            string fieldName,
            ICollection<string> errors)
        {
            if (slot == null)
            {
                errors.Add($"StageAudioDefinition requires {fieldName} to be authored explicitly.");
                return;
            }

            AppendOptionalSlotErrors(slot, fieldName, errors);
        }

        private static void AppendOptionalSlotErrors(
            StageBgmSlot slot,
            string fieldName,
            ICollection<string> errors)
        {
            if (slot == null)
            {
                return;
            }

            var slotErrors = slot.CollectValidationErrors($"StageAudioDefinition.{fieldName}");
            for (var i = 0; i < slotErrors.Count; i++)
            {
                errors.Add(slotErrors[i]);
            }
        }

        private void AppendPhaseSlotErrors(ICollection<string> errors)
        {
            var phases = PhaseBgms;
            var seenPhaseIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                if (phase == null)
                {
                    errors.Add($"StageAudioDefinition.phaseBgms[{i}] cannot be null.");
                    continue;
                }

                var phaseId = phase.PhaseId;
                if (string.IsNullOrWhiteSpace(phaseId))
                {
                    errors.Add($"StageAudioDefinition.phaseBgms[{i}] requires a non-empty phaseId.");
                }
                else if (!seenPhaseIds.Add(phaseId))
                {
                    errors.Add($"StageAudioDefinition.phaseBgms contains duplicate phaseId '{phaseId}'.");
                }

                if (phase.Slot == null)
                {
                    errors.Add($"StageAudioDefinition.phaseBgms[{i}] requires a slot.");
                    continue;
                }

                var slotErrors = phase.Slot.CollectValidationErrors($"StageAudioDefinition.phaseBgms[{i}]");
                for (var errorIndex = 0; errorIndex < slotErrors.Count; errorIndex++)
                {
                    errors.Add(slotErrors[errorIndex]);
                }
            }
        }
    }
}

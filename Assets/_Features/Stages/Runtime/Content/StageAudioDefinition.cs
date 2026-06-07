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

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Audio Definition", fileName = "stage-audio")]
    public sealed class StageAudioDefinition : StageCompanionDefinitionBase
    {
        [SerializeField] private StageBgmSlot gameplayBgm = new();

        public StageBgmSlot GameplayBgm => gameplayBgm;

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
            AppendOwnerMetadataErrors(errors);
            AppendRequiredSlotErrors(gameplayBgm, "gameplayBgm", errors);
            return errors;
        }

        private void AppendOwnerMetadataErrors(ICollection<string> errors)
        {
            if (OwnerEntry == null)
            {
                errors.Add("StageAudioDefinition requires companion owner entry metadata.");
            }

            if (string.IsNullOrWhiteSpace(OwnerEntryGuid))
            {
                errors.Add("StageAudioDefinition requires companion owner entry guid metadata.");
            }
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
    }
}

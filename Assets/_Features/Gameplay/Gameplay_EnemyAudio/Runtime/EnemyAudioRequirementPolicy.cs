using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Gameplay.EnemyAudio
{
    [CreateAssetMenu(
        fileName = "EnemyAudioRequirementPolicy",
        menuName = "Game/Gameplay/Enemy Audio Requirement Policy")]
    public sealed class EnemyAudioRequirementPolicy : ScriptableObject
    {
        [SerializeField] private EnemyAudioCue[] requiredCues = Array.Empty<EnemyAudioCue>();
        [SerializeField] private EnemyAudioCue[] optionalCues = Array.Empty<EnemyAudioCue>();

        public IReadOnlyList<EnemyAudioCue> RequiredCues => requiredCues ?? Array.Empty<EnemyAudioCue>();

        public IReadOnlyList<EnemyAudioCue> OptionalCues => optionalCues ?? Array.Empty<EnemyAudioCue>();

        private void OnValidate()
        {
            var validationErrors = CollectValidationErrors();
            for (var i = 0; i < validationErrors.Count; i++)
            {
                UnityEngine.Debug.LogError(validationErrors[i], this);
            }
        }

        public EnemyAudioCueRequirement GetRequirement(EnemyAudioCue cue)
        {
            if (ContainsCue(requiredCues, cue))
            {
                return EnemyAudioCueRequirement.Required;
            }

            if (ContainsCue(optionalCues, cue))
            {
                return EnemyAudioCueRequirement.Optional;
            }

            return EnemyAudioCueRequirement.Disabled;
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
            var requiredSet = ValidateCueList(requiredCues, nameof(requiredCues), validationErrors);
            var optionalSet = ValidateCueList(optionalCues, nameof(optionalCues), validationErrors);

            foreach (var cue in requiredSet)
            {
                if (optionalSet.Contains(cue))
                {
                    validationErrors.Add(
                        $"{name} contains enemy audio cue '{EnemyAudioCueCatalog.Format(cue)}' in both required and optional cues.");
                }
            }

            return validationErrors;
        }

        private HashSet<EnemyAudioCue> ValidateCueList(
            IReadOnlyList<EnemyAudioCue> cues,
            string fieldName,
            ICollection<string> validationErrors)
        {
            var seen = new HashSet<EnemyAudioCue>();
            if (cues == null)
            {
                return seen;
            }

            for (var i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                if (!IsRuntimeCue(cue))
                {
                    validationErrors.Add(
                        $"{name} {fieldName} contains non-runtime enemy audio cue '{EnemyAudioCueCatalog.Format(cue)}'.");
                    continue;
                }

                if (!seen.Add(cue))
                {
                    validationErrors.Add(
                        $"{name} {fieldName} contains duplicate enemy audio cue '{EnemyAudioCueCatalog.Format(cue)}'.");
                }
            }

            return seen;
        }

        private static bool ContainsCue(IReadOnlyList<EnemyAudioCue> cues, EnemyAudioCue cue)
        {
            if (cues == null)
            {
                return false;
            }

            for (var i = 0; i < cues.Count; i++)
            {
                if (cues[i] == cue)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRuntimeCue(EnemyAudioCue cue)
        {
            for (var i = 0; i < EnemyAudioCueCatalog.RuntimeCues.Count; i++)
            {
                if (EnemyAudioCueCatalog.RuntimeCues[i] == cue)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

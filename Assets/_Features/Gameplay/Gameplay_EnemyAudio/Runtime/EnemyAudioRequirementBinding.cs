using System;
using System.Collections.Generic;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.EnemyAudio
{
    [Serializable]
    public sealed class EnemyAudioRequirementOverride
    {
        [SerializeField] private EnemyAudioCue cue;
        [SerializeField] private EnemyAudioCueRequirement requirement;

        public EnemyAudioCue Cue => cue;

        public EnemyAudioCueRequirement Requirement => requirement;
    }

    [CreateAssetMenu(
        fileName = "EnemyAudioRequirementBinding",
        menuName = "Game/Gameplay/Enemy Audio Requirement Binding")]
    public sealed class EnemyAudioRequirementBinding : ScriptableObject
    {
        [SerializeField] private EnemyAudioProfile targetProfile;
        [SerializeField] private EnemyAudioRequirementPolicy policy;
        [SerializeField] private EnemyAudioRequirementOverride[] overrides = Array.Empty<EnemyAudioRequirementOverride>();

        public EnemyAudioProfile TargetProfile => targetProfile;

        public EnemyAudioRequirementPolicy Policy => policy;

        public IReadOnlyList<EnemyAudioRequirementOverride> Overrides =>
            overrides ?? Array.Empty<EnemyAudioRequirementOverride>();

        private void OnValidate()
        {
            var validationErrors = CollectValidationErrors();
            for (var i = 0; i < validationErrors.Count; i++)
            {
                UnityEngine.Debug.LogError(validationErrors[i], this);
            }
        }

        public EnemyAudioCueRequirement GetEffectiveRequirement(EnemyAudioCue cue)
        {
            if (TryGetOverride(cue, out var requirement))
            {
                return requirement;
            }

            if (policy == null)
            {
                throw new InvalidOperationException($"{name}: policy is required.");
            }

            return policy.GetRequirement(cue);
        }

        public void ValidateOrThrow()
        {
            var validationErrors = CollectValidationErrors();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(validationErrors[0]);
            }
        }

        private bool TryGetOverride(EnemyAudioCue cue, out EnemyAudioCueRequirement requirement)
        {
            if (overrides != null)
            {
                for (var i = 0; i < overrides.Length; i++)
                {
                    if (overrides[i] != null && overrides[i].Cue == cue)
                    {
                        requirement = overrides[i].Requirement;
                        return true;
                    }
                }
            }

            requirement = default;
            return false;
        }

        private List<string> CollectValidationErrors()
        {
            var validationErrors = new List<string>();

            if (targetProfile == null)
            {
                validationErrors.Add($"{name}: targetProfile is required.");
            }

            if (policy == null)
            {
                validationErrors.Add($"{name}: policy is required.");
            }

            if (policy != null)
            {
                AppendPolicyValidationErrors(validationErrors);
            }

            if (targetProfile != null)
            {
                AppendTargetProfileValidationErrors(validationErrors);
            }

            AppendOverrideRowValidationErrors(validationErrors);

            if (policy == null || targetProfile == null || validationErrors.Count > 0)
            {
                return validationErrors;
            }

            AppendRequirementAgainstBindingValidationErrors(validationErrors);
            return validationErrors;
        }

        private void AppendPolicyValidationErrors(ICollection<string> validationErrors)
        {
            try
            {
                policy.ValidateOrThrow();
            }
            catch (Exception exception)
            {
                validationErrors.Add($"{name}: policy '{policy.name}' is invalid: {exception.Message}");
            }
        }

        private void AppendTargetProfileValidationErrors(ICollection<string> validationErrors)
        {
            try
            {
                targetProfile.ValidateOrThrow();
            }
            catch (Exception exception)
            {
                validationErrors.Add($"{name}: targetProfile '{targetProfile.name}' is invalid: {exception.Message}");
            }
        }

        private void AppendOverrideRowValidationErrors(ICollection<string> validationErrors)
        {
            var seen = new HashSet<EnemyAudioCue>();
            if (overrides == null)
            {
                return;
            }

            for (var i = 0; i < overrides.Length; i++)
            {
                var row = overrides[i];
                if (row == null)
                {
                    validationErrors.Add($"{name} contains a null enemy audio requirement override.");
                    continue;
                }

                var cue = row.Cue;
                if (!IsRuntimeCue(cue))
                {
                    validationErrors.Add(
                        $"{name} override contains non-runtime enemy audio cue '{EnemyAudioCueCatalog.Format(cue)}'.");
                    continue;
                }

                if (!seen.Add(cue))
                {
                    validationErrors.Add(
                        $"{name} contains duplicate enemy audio requirement override for cue '{EnemyAudioCueCatalog.Format(cue)}'.");
                }
            }
        }

        private void AppendRequirementAgainstBindingValidationErrors(ICollection<string> validationErrors)
        {
            for (var i = 0; i < EnemyAudioCueCatalog.RuntimeCues.Count; i++)
            {
                var cue = EnemyAudioCueCatalog.RuntimeCues[i];
                var requirement = GetEffectiveRequirement(cue);
                var hasBinding = targetProfile.TryResolve(cue, out var binding);
                ValidateRequirementAgainstBinding(cue, requirement, hasBinding, binding, validationErrors);
            }
        }

        private void ValidateRequirementAgainstBinding(
            EnemyAudioCue cue,
            EnemyAudioCueRequirement requirement,
            bool hasBinding,
            AudioBinding binding,
            ICollection<string> validationErrors)
        {
            var cueLabel = EnemyAudioCueCatalog.Format(cue);
            if (requirement == EnemyAudioCueRequirement.Required && (!hasBinding || binding == null))
            {
                validationErrors.Add(
                    $"{name} requires target profile '{targetProfile.name}' to bind enemy audio cue '{cueLabel}'.");
            }
            else if (requirement == EnemyAudioCueRequirement.Disabled && hasBinding)
            {
                validationErrors.Add(
                    $"{name} disables enemy audio cue '{cueLabel}', but target profile '{targetProfile.name}' carries a binding.");
            }
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

using System;
using System.Collections.Generic;

namespace Game.Shared.Audio
{
    public readonly struct AudioBindingValidationOptions
    {
        public AudioBindingValidationOptions(
            IReadOnlyList<AudioCategory> allowedCategories,
            bool allowLoopingDefinitions = true,
            bool allowNullBinding = false)
        {
            AllowedCategories = allowedCategories;
            AllowLoopingDefinitions = allowLoopingDefinitions;
            AllowNullBinding = allowNullBinding;
        }

        public IReadOnlyList<AudioCategory> AllowedCategories { get; }

        public bool AllowLoopingDefinitions { get; }

        public bool AllowNullBinding { get; }

        public static AudioBindingValidationOptions Default => new(
            allowedCategories: null,
            allowLoopingDefinitions: true,
            allowNullBinding: false);
    }

    public static class AudioBindingDiagnostics
    {
        public static void ValidateOrThrow(
            AudioBinding binding,
            string ownerDescription,
            string bindingDescription,
            AudioBindingValidationOptions options)
        {
            var validationErrors = new List<string>();
            AppendValidationErrors(binding, ownerDescription, bindingDescription, validationErrors, options);
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(validationErrors[0]);
            }
        }

        public static void AppendValidationErrors(
            AudioBinding binding,
            string ownerDescription,
            string bindingDescription,
            ICollection<string> validationErrors,
            AudioBindingValidationOptions options)
        {
            if (validationErrors == null)
            {
                throw new ArgumentNullException(nameof(validationErrors));
            }

            if (binding == null)
            {
                if (!options.AllowNullBinding)
                {
                    validationErrors.Add(
                        $"{ownerDescription} {bindingDescription} is missing an AudioBinding.");
                }

                return;
            }

            if (binding.Definition == null)
            {
                validationErrors.Add(
                    $"{ownerDescription} {bindingDescription} is missing an AudioDefinition binding.");
            }
            else if (AudioDefinitionCategoryRules.TryGetReservedCategoryMessage(
                         binding.Definition.Category,
                         $"{ownerDescription} {bindingDescription} definition '{binding.Definition.name}'",
                         out var reservedMessage))
            {
                validationErrors.Add(reservedMessage);
            }
            else
            {
                AppendCategoryPolicyErrors(binding, ownerDescription, bindingDescription, validationErrors, options);
                AppendLoopPolicyErrors(binding, ownerDescription, bindingDescription, validationErrors, options);
                if (binding.Definition is RandomAudioDefinition randomDefinition)
                {
                    randomDefinition.AppendValidationErrors(
                        validationErrors,
                        $"{ownerDescription} {bindingDescription} definition '{binding.Definition.name}'");
                }
            }

            if (binding.Policy != null)
            {
                validationErrors.Add(
                    $"{ownerDescription} {bindingDescription} configures AudioBinding.Policy, but v1 keeps policy reserved and it must remain null.");
            }
        }

        private static void AppendCategoryPolicyErrors(
            AudioBinding binding,
            string ownerDescription,
            string bindingDescription,
            ICollection<string> validationErrors,
            AudioBindingValidationOptions options)
        {
            var allowedCategories = options.AllowedCategories;
            if (allowedCategories == null || allowedCategories.Count == 0)
            {
                return;
            }

            for (var i = 0; i < allowedCategories.Count; i++)
            {
                if (binding.Definition.Category == allowedCategories[i])
                {
                    return;
                }
            }

            validationErrors.Add(
                $"{ownerDescription} {bindingDescription} definition '{binding.Definition.name}' uses category '{binding.Definition.Category}', but only [{string.Join(", ", allowedCategories)}] are allowed.");
        }

        private static void AppendLoopPolicyErrors(
            AudioBinding binding,
            string ownerDescription,
            string bindingDescription,
            ICollection<string> validationErrors,
            AudioBindingValidationOptions options)
        {
            if (options.AllowLoopingDefinitions || !binding.Definition.Loop)
            {
                return;
            }

            validationErrors.Add(
                $"{ownerDescription} {bindingDescription} definition '{binding.Definition.name}' enables loop playback, but this binding only allows one-shot definitions.");
        }
    }
}

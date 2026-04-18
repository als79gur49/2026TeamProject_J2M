using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("Game.Feature.Gameplay.Audio")]
[assembly: InternalsVisibleTo("Game.Feature.Gameplay.Tests")]
[assembly: InternalsVisibleTo("Game.Feature.Gameplay.PlayModeTests")]

namespace Game.Shared.Audio
{
    [Serializable]
    public sealed class AudioBinding
    {
        [SerializeField] private AudioDefinition definition;
        [SerializeField] private AudioAttachmentSlot attachmentSlot;
        [HideInInspector] [SerializeReference] private AudioPlaybackPolicy policy;

        public AudioDefinition Definition => definition;

        public AudioAttachmentSlot AttachmentSlot => attachmentSlot;

        public AudioPlaybackPolicy Policy => policy;

        public bool HasAttachmentSlot => !attachmentSlot.IsEmpty;

        public void ValidateOrThrow(string ownerDescription, string semanticId)
        {
            var validationErrors = new List<string>();
            AppendValidationErrors(ownerDescription, semanticId, validationErrors);
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(validationErrors[0]);
            }
        }

        internal void AppendValidationErrors(
            string ownerDescription,
            string semanticId,
            ICollection<string> validationErrors)
        {
            if (definition == null)
            {
                validationErrors.Add(
                    $"{ownerDescription} semantic '{semanticId}' is missing an AudioDefinition binding.");
            }
            else if (AudioDefinitionCategoryRules.TryGetReservedCategoryMessage(
                         definition.Category,
                         $"{ownerDescription} semantic '{semanticId}' definition '{definition.name}'",
                         out var message))
            {
                validationErrors.Add(message);
            }

            if (policy != null)
            {
                validationErrors.Add(
                    $"{ownerDescription} semantic '{semanticId}' configures AudioBinding.Policy, but v1 keeps policy reserved and it must remain null.");
            }
        }
    }
}

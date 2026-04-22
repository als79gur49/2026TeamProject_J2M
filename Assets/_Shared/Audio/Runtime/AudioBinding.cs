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
            AudioBindingDiagnostics.ValidateOrThrow(
                this,
                ownerDescription,
                $"semantic '{semanticId}'",
                AudioBindingValidationOptions.Default);
        }

        internal void AppendValidationErrors(
            string ownerDescription,
            string semanticId,
            ICollection<string> validationErrors)
        {
            AudioBindingDiagnostics.AppendValidationErrors(
                this,
                ownerDescription,
                $"semantic '{semanticId}'",
                validationErrors,
                AudioBindingValidationOptions.Default);
        }
    }
}

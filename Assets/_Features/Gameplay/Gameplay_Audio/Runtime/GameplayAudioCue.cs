using UnityEngine;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Audio
{
    public readonly struct GameplayAudioCue
    {
        public GameplayAudioCue(
            string semanticId,
            in AudioPlaybackContext context)
        {
            SemanticId = semanticId;
            Context = context;
            Owner = null;
            AttachmentSlot = default;
        }

        public GameplayAudioCue(
            string semanticId,
            Component owner,
            AudioAttachmentSlot attachmentSlot,
            in AudioPlaybackContext context)
        {
            SemanticId = semanticId;
            Context = context;
            Owner = owner;
            AttachmentSlot = attachmentSlot;
        }

        public string SemanticId { get; }

        public AudioPlaybackContext Context { get; }

        public Component Owner { get; }

        public AudioAttachmentSlot AttachmentSlot { get; }
    }
}

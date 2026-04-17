using System;
using UnityEngine;

namespace Game.Shared.Audio
{
    [Serializable]
    public struct AudioAttachmentSlot : IEquatable<AudioAttachmentSlot>
    {
        [SerializeField] private string id;

        public AudioAttachmentSlot(string id)
        {
            this.id = Normalize(id);
        }

        public string Id => id ?? string.Empty;

        public bool IsEmpty => string.IsNullOrWhiteSpace(Id);

        public static AudioAttachmentSlot FromId(string id)
        {
            return new AudioAttachmentSlot(id);
        }

        public bool Equals(AudioAttachmentSlot other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AudioAttachmentSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Id);
        }

        public override string ToString()
        {
            return Id;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}

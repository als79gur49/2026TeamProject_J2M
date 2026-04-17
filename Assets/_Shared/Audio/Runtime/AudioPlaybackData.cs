using UnityEngine;

namespace Game.Shared.Audio
{
    public readonly struct AudioPlaybackData
    {
        public AudioPlaybackData(
            AudioClip clip,
            AudioCategory category,
            float volume,
            float pitch,
            bool loop)
        {
            Clip = clip;
            Category = category;
            Volume = volume;
            Pitch = pitch;
            Loop = loop;
        }

        public AudioClip Clip { get; }

        public AudioCategory Category { get; }

        public float Volume { get; }

        public float Pitch { get; }

        public bool Loop { get; }
    }
}

using UnityEngine;

namespace Game.Shared.Audio
{
    public readonly struct AudioChannelState
    {
        public AudioChannelState(float volume, bool isMuted)
        {
            Volume = Mathf.Clamp01(volume);
            IsMuted = isMuted;
        }

        public float Volume { get; }

        public bool IsMuted { get; }

        public float EffectiveFactor => IsMuted ? 0f : Volume;
    }
}

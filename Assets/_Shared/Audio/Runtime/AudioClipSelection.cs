using UnityEngine;

namespace Game.Shared.Audio
{
    public readonly struct AudioClipSelection
    {
        public AudioClipSelection(AudioClip clip, float volumeTrim = 1f, float pitchTrim = 1f)
        {
            Clip = clip;
            VolumeTrim = SanitizeTrim(volumeTrim);
            PitchTrim = SanitizeTrim(pitchTrim);
        }

        public AudioClip Clip { get; }

        public float VolumeTrim { get; }

        public float PitchTrim { get; }

        private static float SanitizeTrim(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? value
                : 1f;
        }
    }
}

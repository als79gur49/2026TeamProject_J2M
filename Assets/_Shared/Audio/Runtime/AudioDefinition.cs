using System;
using UnityEngine;

namespace Game.Shared.Audio
{
    public abstract class AudioDefinition : ScriptableObject
    {
        [SerializeField] private AudioCategory category = AudioCategory.Sfx;
        [SerializeField] [Range(0f, 1f)] private float defaultVolumeTrim = 1f;
        [SerializeField] private Vector2 pitchRange = Vector2.one;
        [SerializeField] private bool loop;

        public AudioCategory Category => category;

        public float DefaultVolumeTrim => defaultVolumeTrim;

        public Vector2 PitchRange => pitchRange;

        public bool Loop => loop;

        public AudioPlaybackData Resolve(in AudioPlaybackContext context)
        {
            var clip = ResolveClip();
            if (clip == null)
            {
                throw new InvalidOperationException($"{name} resolved a null AudioClip.");
            }

            var volumeMultiplier = context.VolumeMultiplier <= 0f ? 0f : context.VolumeMultiplier;
            var pitchMultiplier = context.PitchMultiplier <= 0f ? 1f : context.PitchMultiplier;
            var pitchMin = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
            var pitchMax = Mathf.Max(pitchMin, Mathf.Max(pitchRange.x, pitchRange.y));
            var resolvedPitch = UnityEngine.Random.Range(pitchMin, pitchMax);

            return new AudioPlaybackData(
                clip,
                category,
                Mathf.Clamp01(defaultVolumeTrim * volumeMultiplier),
                resolvedPitch * pitchMultiplier,
                loop);
        }

        protected abstract AudioClip ResolveClip();

        protected virtual void OnValidate()
        {
            defaultVolumeTrim = Mathf.Clamp01(defaultVolumeTrim);
            pitchRange.x = Mathf.Max(0.01f, pitchRange.x);
            pitchRange.y = Mathf.Max(0.01f, pitchRange.y);
        }
    }
}

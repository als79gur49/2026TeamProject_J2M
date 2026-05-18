using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Audio
{
    [CreateAssetMenu(menuName = "Game/Audio/Random Audio Definition")]
    public sealed class RandomAudioDefinition : AudioDefinition
    {
        [Serializable]
        private struct WeightedClip
        {
            public AudioClip Clip;
            [Min(0.01f)] public float Weight;
            [Min(0.01f)] public float VolumeTrim;
            [Min(0.01f)] public float PitchTrim;
        }

        [SerializeField] private WeightedClip[] clips = Array.Empty<WeightedClip>();

        protected override AudioClipSelection ResolveClipSelection()
        {
            if (clips == null || clips.Length == 0)
            {
                return default;
            }

            float totalWeight = 0f;
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i].Clip == null)
                {
                    continue;
                }

                totalWeight += SanitizeWeight(clips[i].Weight);
            }

            if (totalWeight <= 0f)
            {
                return default;
            }

            var selection = UnityEngine.Random.Range(0f, totalWeight);
            float cursor = 0f;
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i].Clip == null)
                {
                    continue;
                }

                cursor += SanitizeWeight(clips[i].Weight);
                if (selection <= cursor)
                {
                    return CreateSelection(clips[i]);
                }
            }

            for (var i = clips.Length - 1; i >= 0; i--)
            {
                if (clips[i].Clip != null)
                {
                    return CreateSelection(clips[i]);
                }
            }

            return default;
        }

        internal void AppendValidationErrors(ICollection<string> validationErrors, string ownerDescription)
        {
            if (validationErrors == null)
            {
                throw new ArgumentNullException(nameof(validationErrors));
            }

            var description = string.IsNullOrEmpty(ownerDescription)
                ? $"RandomAudioDefinition '{name}'"
                : ownerDescription;
            if (clips == null || clips.Length == 0)
            {
                validationErrors.Add($"{description} has an empty random clip variant list.");
                return;
            }

            var nonNullClipCount = 0;
            for (var i = 0; i < clips.Length; i++)
            {
                var entry = clips[i];
                if (entry.Clip == null)
                {
                    validationErrors.Add($"{description} variant {i} has a null AudioClip.");
                }
                else
                {
                    nonNullClipCount++;
                }

                if (!IsPositiveFinite(entry.Weight))
                {
                    validationErrors.Add($"{description} variant {i} has invalid weight '{entry.Weight}'.");
                }

                if (!IsPositiveFinite(entry.VolumeTrim))
                {
                    validationErrors.Add($"{description} variant {i} has invalid volume trim '{entry.VolumeTrim}'.");
                }

                if (!IsPositiveFinite(entry.PitchTrim))
                {
                    validationErrors.Add($"{description} variant {i} has invalid pitch trim '{entry.PitchTrim}'.");
                }
            }

            if (nonNullClipCount == 0)
            {
                validationErrors.Add($"{description} has no non-null random clip variants.");
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (clips == null)
            {
                clips = Array.Empty<WeightedClip>();
                return;
            }

            for (var i = 0; i < clips.Length; i++)
            {
                var entry = clips[i];
                entry.Weight = SanitizeWeight(entry.Weight);
                entry.VolumeTrim = SanitizeTrim(entry.VolumeTrim);
                entry.PitchTrim = SanitizeTrim(entry.PitchTrim);
                clips[i] = entry;
            }
        }

        private static AudioClipSelection CreateSelection(WeightedClip clip)
        {
            return new AudioClipSelection(
                clip.Clip,
                SanitizeTrim(clip.VolumeTrim),
                SanitizeTrim(clip.PitchTrim));
        }

        private static float SanitizeWeight(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? value
                : 1f;
        }

        private static float SanitizeTrim(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? value
                : 1f;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

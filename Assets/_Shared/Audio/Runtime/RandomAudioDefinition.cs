using System;
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
        }

        [SerializeField] private WeightedClip[] clips = Array.Empty<WeightedClip>();

        protected override AudioClip ResolveClip()
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i].Clip == null)
                {
                    continue;
                }

                totalWeight += Mathf.Max(0.01f, clips[i].Weight);
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            var selection = UnityEngine.Random.Range(0f, totalWeight);
            float cursor = 0f;
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i].Clip == null)
                {
                    continue;
                }

                cursor += Mathf.Max(0.01f, clips[i].Weight);
                if (selection <= cursor)
                {
                    return clips[i].Clip;
                }
            }

            return clips[clips.Length - 1].Clip;
        }
    }
}

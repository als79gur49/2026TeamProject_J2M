using System;
using System.Collections.Generic;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Flow.Audio
{
    [CreateAssetMenu(menuName = "Game/Audio/Bgm Profile")]
    public sealed class BgmProfile : ScriptableObject
    {
        [SerializeField] private AudioDefinition loopDefinition;
        [SerializeField]
        [Tooltip("Immediate and FadeOutIn execute in the shared BGM runtime. Crossfade is reserved and falls back.")]
        private BgmTransitionMode transitionMode = BgmTransitionMode.Immediate;
        [SerializeField] [Min(0f)] private float fadeOutSeconds = 0.35f;
        [SerializeField] [Min(0f)] private float fadeInSeconds = 0.35f;
        [SerializeField] private bool restartIfAlreadyPlaying;

        public AudioDefinition LoopDefinition => loopDefinition;

        public BgmTransitionMode TransitionMode => transitionMode;

        public float FadeOutSeconds => fadeOutSeconds;

        public float FadeInSeconds => fadeInSeconds;

        public bool RestartIfAlreadyPlaying => restartIfAlreadyPlaying;

        public void ValidateOrThrow()
        {
            var validationErrors = CollectValidationErrors();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(validationErrors[0]);
            }
        }

        private void OnValidate()
        {
            var validationErrors = CollectValidationErrors();
            for (var i = 0; i < validationErrors.Count; i++)
            {
                Debug.LogError(validationErrors[i], this);
            }
        }

        private List<string> CollectValidationErrors()
        {
            var validationErrors = new List<string>();
            if (loopDefinition == null)
            {
                validationErrors.Add($"BgmProfile '{name}' requires a loopDefinition.");
                return validationErrors;
            }

            if (loopDefinition.Category != AudioCategory.Bgm)
            {
                validationErrors.Add($"BgmProfile '{name}' requires loopDefinition to use AudioCategory.Bgm.");
            }

            AppendFadeDurationValidationError(nameof(fadeOutSeconds), fadeOutSeconds, validationErrors);
            AppendFadeDurationValidationError(nameof(fadeInSeconds), fadeInSeconds, validationErrors);
            return validationErrors;
        }

        private void AppendFadeDurationValidationError(
            string fieldName,
            float seconds,
            ICollection<string> validationErrors)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                validationErrors.Add($"BgmProfile '{name}' requires {fieldName} to be finite and greater than or equal to zero.");
            }
        }
    }
}

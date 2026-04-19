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
        [Tooltip("BGM flow v1 executes Immediate only. FadeOutIn and Crossfade are reserved future policy values.")]
        private BgmTransitionMode transitionMode = BgmTransitionMode.Immediate;
        [SerializeField] private bool restartIfAlreadyPlaying;

        public AudioDefinition LoopDefinition => loopDefinition;

        public BgmTransitionMode TransitionMode => transitionMode;

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

            return validationErrors;
        }
    }
}

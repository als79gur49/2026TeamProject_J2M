using System;
using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Gameplay.ActionAudio
{
    [DisallowMultipleComponent]
    public sealed class GameplayActionAudioAuthoring : MonoBehaviour
    {
        [SerializeField] private GameplayActionAudioProfile profile;

        public GameplayActionAudioProfile Profile => profile;

        public void Validate()
        {
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameplayActionAudioAuthoring)} on '{gameObject.name}' requires a non-null {nameof(GameplayActionAudioProfile)}.");
            }

            profile.ValidateOrThrow();
        }

        public static GameplayActionAudioAuthoring GetOptionalValidatedAuthoring(GameplayEntityView entityView)
        {
            if (entityView == null)
            {
                throw new ArgumentNullException(nameof(entityView));
            }

            if (!entityView.TryGetComponent<GameplayActionAudioAuthoring>(out var authoring) ||
                authoring == null)
            {
                return null;
            }

            authoring.Validate();
            return authoring;
        }
    }
}

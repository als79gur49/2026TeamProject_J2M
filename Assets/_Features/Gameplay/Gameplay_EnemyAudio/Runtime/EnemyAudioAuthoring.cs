using System;
using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Gameplay.EnemyAudio
{
    [DisallowMultipleComponent]
    public sealed class EnemyAudioAuthoring : MonoBehaviour
    {
        [SerializeField] private EnemyAudioProfile profile;

        public EnemyAudioProfile Profile => profile;

        public void Validate()
        {
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(EnemyAudioAuthoring)} on '{gameObject.name}' requires a non-null {nameof(EnemyAudioProfile)}.");
            }

            profile.ValidateOrThrow();
        }

        public static EnemyAudioAuthoring GetOptionalValidatedAuthoring(GameplayEntityView entityView)
        {
            if (entityView == null)
            {
                throw new ArgumentNullException(nameof(entityView));
            }

            if (!entityView.TryGetComponent<EnemyAudioAuthoring>(out var authoring) ||
                authoring == null)
            {
                return null;
            }

            authoring.Validate();
            return authoring;
        }
    }
}

using UnityEngine;

namespace Game.Shared.Audio
{
    [CreateAssetMenu(menuName = "Game/Audio/Single Audio Definition")]
    public sealed class SingleAudioDefinition : AudioDefinition
    {
        [SerializeField] private AudioClip clip;

        protected override AudioClipSelection ResolveClipSelection()
        {
            return new AudioClipSelection(clip);
        }
    }
}

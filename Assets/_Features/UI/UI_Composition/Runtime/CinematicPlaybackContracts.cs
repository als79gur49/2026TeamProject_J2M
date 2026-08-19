using System;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public interface ICinematicSequencePlayer
    {
        bool HasIntroContent { get; }

        bool HasOutroContent { get; }

        bool IsPlaying { get; }

        void PlayIntro(Action<CinematicPlaybackCompletion> completion);

        void PlayOutro(Action<CinematicPlaybackCompletion> completion);
    }

    internal interface ICinematicOpaqueHandoffCancellationOwner
    {
        bool TryReleaseCancelledIntroOpaqueOwner();
    }

    internal interface ICinematicAudioFocusOwner
    {
        void BeginFocus(AudioSource cinematicAudioSource);

        void EndFocus();
    }
}

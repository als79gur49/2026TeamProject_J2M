using System;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public interface IComicIntroOutroFlow
    {
        bool HasIntroSequence { get; }

        bool HasOutroSequence { get; }

        bool IsPresenting { get; }

        void PresentIntro(Action<ComicSequenceResult> completion);

        void PresentOutro(Action<ComicSequenceResult> completion);
    }

    internal interface IComicSequenceOpaqueHandoffCancellationOwner
    {
        bool TryReleaseCancelledIntroOpaqueOwner();
    }

    internal interface IComicSequenceAudioFocusOwner
    {
        void BeginFocus(AudioSource comicSequenceAudioSource);

        void EndFocus();
    }
}

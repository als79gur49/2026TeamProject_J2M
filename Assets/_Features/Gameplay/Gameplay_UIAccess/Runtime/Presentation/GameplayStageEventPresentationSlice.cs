using System;

namespace Game.Feature.Gameplay.UIAccess.Presentation
{
    public enum GameplayStageEventKind
    {
        None = 0,
        Cleared = 1,
    }

    public readonly struct GameplayStageEventPresentationSlice
    {
        public GameplayStageEventPresentationSlice(GameplayStageEventKind eventKind)
        {
            if (eventKind == GameplayStageEventKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Stage-event slices must represent a concrete event.");
            }

            EventKind = eventKind;
        }

        public GameplayStageEventKind EventKind { get; }
    }
}

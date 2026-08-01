using System;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.UIAccess.Presentation
{
    public enum GameplayStageEventKind
    {
        None = 0,
        Cleared = 1,
    }

    public readonly struct GameplayStageEventPresentationSlice
    {
        public GameplayStageEventPresentationSlice(
            GameplayStageEventKind eventKind,
            TerminalSessionToken terminalToken = default)
        {
            if (eventKind == GameplayStageEventKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Stage-event slices must represent a concrete event.");
            }

            EventKind = eventKind;
            TerminalToken = terminalToken;
        }

        public GameplayStageEventKind EventKind { get; }

        public TerminalSessionToken TerminalToken { get; }

        public long TerminalClaimId => TerminalToken.Sequence;

        public GameplayStageEventPresentationSlice(
            GameplayStageEventKind eventKind,
            long terminalClaimId)
            : this(
                eventKind,
                terminalClaimId > 0
                    ? new TerminalSessionToken(
                        TerminalSessionRegistry.Authority.AuthorityGeneration,
                        terminalClaimId)
                    : default)
        {
        }
    }
}

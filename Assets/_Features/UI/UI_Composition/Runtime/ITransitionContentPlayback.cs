using System;

namespace Game.Feature.UI.Composition
{
    internal enum TransitionContentPlaybackOutcome
    {
        Pending = 0,
        Completed = 1,
        Cancelled = 2,
        Failed = 3,
    }

    internal interface ITransitionContentPlayback
    {
        TransitionContentPlaybackOutcome Outcome { get; }

        bool IsCompleted { get; }

        bool IsCancelled { get; }

        bool IsFailed { get; }

        event Action Completed;

        event Action Cancelled;

        event Action Failed;
    }

    internal interface ITransitionContentPlaybackProvider
    {
        ITransitionContentPlayback Playback { get; }
    }

    internal sealed class TransitionContentPlaybackHandle : ITransitionContentPlayback
    {
        public TransitionContentPlaybackOutcome Outcome { get; private set; } =
            TransitionContentPlaybackOutcome.Pending;

        public bool IsCompleted => Outcome == TransitionContentPlaybackOutcome.Completed;

        public bool IsCancelled => Outcome == TransitionContentPlaybackOutcome.Cancelled;

        public bool IsFailed => Outcome == TransitionContentPlaybackOutcome.Failed;

        public event Action Completed;

        public event Action Cancelled;

        public event Action Failed;

        public bool TryComplete()
        {
            return TrySetOutcome(TransitionContentPlaybackOutcome.Completed, Completed);
        }

        public bool TryCancel()
        {
            return TrySetOutcome(TransitionContentPlaybackOutcome.Cancelled, Cancelled);
        }

        public bool TryFail()
        {
            return TrySetOutcome(TransitionContentPlaybackOutcome.Failed, Failed);
        }

        private bool TrySetOutcome(TransitionContentPlaybackOutcome outcome, Action signal)
        {
            if (Outcome != TransitionContentPlaybackOutcome.Pending)
            {
                return false;
            }

            Outcome = outcome;
            signal?.Invoke();
            return true;
        }
    }
}

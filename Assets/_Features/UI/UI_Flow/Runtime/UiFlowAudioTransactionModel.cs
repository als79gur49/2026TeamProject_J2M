using System;
using System.Collections.Generic;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Flow
{
    internal enum UiFlowAudioIntentKind
    {
        None = 0,
        Back = 1,
        OpenForward = 2,
        Confirm = 3,
        Cancel = 4,
        SystemPresentation = 5,
    }

    internal enum UiFlowAudioDeltaKind
    {
        ScreenTransition = 0,
        PopupOpened = 1,
        PopupCompleted = 2,
        RootScreenSet = 3,
    }

    internal enum UiFlowAudioOutcomeKind
    {
        Silent = 0,
        NavigateForward = 1,
        NavigateBack = 2,
        Confirm = 3,
        Cancel = 4,
        GameClear = 5,
        StageClear = 6,
        LevelFailed = 7,
    }

    internal enum UiFlowAudioSilenceReason
    {
        None = 0,
        NoVisibleDelta = 1,
        FailedOperation = 2,
        Cleanup = 3,
        SystemPresentationPolicy = 4,
        Aborted = 5,
    }

    internal readonly struct UiFlowAudioDelta
    {
        private UiFlowAudioDelta(
            UiFlowAudioDeltaKind kind,
            ScreenTransitionKind screenTransitionKind,
            ScreenId previousScreenId,
            ScreenId currentScreenId,
            PopupId popupId,
            PopupCompletionKind popupCompletionKind,
            PopupCloseReason popupCloseReason)
        {
            Kind = kind;
            ScreenTransitionKind = screenTransitionKind;
            PreviousScreenId = previousScreenId;
            CurrentScreenId = currentScreenId;
            PopupId = popupId;
            PopupCompletionKind = popupCompletionKind;
            PopupCloseReason = popupCloseReason;
        }

        public UiFlowAudioDeltaKind Kind { get; }

        public ScreenTransitionKind ScreenTransitionKind { get; }

        public ScreenId PreviousScreenId { get; }

        public ScreenId CurrentScreenId { get; }

        public PopupId PopupId { get; }

        public PopupCompletionKind PopupCompletionKind { get; }

        public PopupCloseReason PopupCloseReason { get; }

        public static UiFlowAudioDelta FromScreenTransition(ScreenTransitionedEvent transitionEvent)
        {
            return new UiFlowAudioDelta(
                UiFlowAudioDeltaKind.ScreenTransition,
                transitionEvent.Kind,
                transitionEvent.PreviousEntry.HasValue ? transitionEvent.PreviousEntry.Value.ScreenId : ScreenId.None,
                transitionEvent.CurrentEntry.HasValue ? transitionEvent.CurrentEntry.Value.ScreenId : ScreenId.None,
                PopupId.None,
                default,
                default);
        }

        public static UiFlowAudioDelta FromPopupOpened(PopupOpenedEvent openedEvent)
        {
            return new UiFlowAudioDelta(
                UiFlowAudioDeltaKind.PopupOpened,
                default,
                ScreenId.None,
                ScreenId.None,
                openedEvent.Entry.PopupId,
                default,
                default);
        }

        public static UiFlowAudioDelta FromPopupCompleted(PopupCompletedEvent completedEvent)
        {
            return new UiFlowAudioDelta(
                UiFlowAudioDeltaKind.PopupCompleted,
                default,
                ScreenId.None,
                ScreenId.None,
                completedEvent.Entry.PopupId,
                completedEvent.Completion.CompletionKind,
                completedEvent.Completion.CloseReason);
        }

        public static UiFlowAudioDelta FromRootScreenSet(ScreenId screenId)
        {
            return new UiFlowAudioDelta(
                UiFlowAudioDeltaKind.RootScreenSet,
                default,
                ScreenId.None,
                screenId,
                PopupId.None,
                default,
                default);
        }

        public override string ToString()
        {
            return Kind switch
            {
                UiFlowAudioDeltaKind.ScreenTransition => $"Screen:{ScreenTransitionKind} {PreviousScreenId}->{CurrentScreenId}",
                UiFlowAudioDeltaKind.PopupOpened => $"PopupOpened:{PopupId}",
                UiFlowAudioDeltaKind.PopupCompleted => $"PopupCompleted:{PopupId}/{PopupCompletionKind}/{PopupCloseReason}",
                UiFlowAudioDeltaKind.RootScreenSet => $"RootScreenSet:{CurrentScreenId}",
                _ => Kind.ToString(),
            };
        }
    }

    internal readonly struct UiFlowAudioClassificationResult
    {
        public UiFlowAudioClassificationResult(
            UiFlowAudioOutcomeKind outcomeKind,
            UiAudioCueId? emittedCueId,
            UiFlowAudioSilenceReason silenceReason)
        {
            OutcomeKind = outcomeKind;
            EmittedCueId = emittedCueId;
            SilenceReason = silenceReason;
        }

        public UiFlowAudioOutcomeKind OutcomeKind { get; }

        public UiAudioCueId? EmittedCueId { get; }

        public UiFlowAudioSilenceReason SilenceReason { get; }
    }

    internal sealed class UiFlowAudioTrace
    {
        public UiFlowAudioTrace(
            UiFlowAudioIntentKind rootIntent,
            IReadOnlyList<UiFlowAudioDelta> deltas,
            UiFlowAudioOutcomeKind outcomeKind,
            UiAudioCueId? emittedCueId,
            UiFlowAudioSilenceReason silenceReason,
            int maxJoinedDepth)
        {
            RootIntent = rootIntent;
            Deltas = deltas ?? throw new ArgumentNullException(nameof(deltas));
            OutcomeKind = outcomeKind;
            EmittedCueId = emittedCueId;
            SilenceReason = silenceReason;
            MaxJoinedDepth = maxJoinedDepth;
        }

        public UiFlowAudioIntentKind RootIntent { get; }

        public IReadOnlyList<UiFlowAudioDelta> Deltas { get; }

        public UiFlowAudioOutcomeKind OutcomeKind { get; }

        public UiAudioCueId? EmittedCueId { get; }

        public UiFlowAudioSilenceReason SilenceReason { get; }

        public int MaxJoinedDepth { get; }
    }

    internal sealed class UiFlowAudioTransaction
    {
        private readonly List<UiFlowAudioDelta> _deltas = new();
        private bool _isAborted;
        private bool _isFinalized;
        private UiFlowAudioSilenceReason _abortReason;
        private UiFlowAudioTrace _finalTrace;

        public UiFlowAudioTransaction(UiFlowAudioIntentKind rootIntent)
        {
            if (rootIntent == UiFlowAudioIntentKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(rootIntent));
            }

            RootIntent = rootIntent;
            Depth = 1;
            MaxDepth = 1;
        }

        public UiFlowAudioIntentKind RootIntent { get; }

        public int Depth { get; private set; }

        public int MaxDepth { get; private set; }

        public IReadOnlyList<UiFlowAudioDelta> Deltas => _deltas;

        public bool IsFinalized => _isFinalized;

        public void Join()
        {
            if (_isFinalized)
            {
                return;
            }

            Depth++;
            if (Depth > MaxDepth)
            {
                MaxDepth = Depth;
            }
        }

        public void Leave()
        {
            if (_isFinalized || Depth <= 0)
            {
                return;
            }

            Depth--;
        }

        public void Record(UiFlowAudioDelta delta)
        {
            if (_isFinalized)
            {
                return;
            }

            _deltas.Add(delta);
        }

        public void Abort(UiFlowAudioSilenceReason reason)
        {
            if (_isFinalized || _isAborted)
            {
                return;
            }

            _isAborted = true;
            _abortReason = reason == UiFlowAudioSilenceReason.None
                ? UiFlowAudioSilenceReason.Aborted
                : reason;
        }

        public UiFlowAudioTrace FinalizeTrace()
        {
            if (_isFinalized)
            {
                return _finalTrace;
            }

            _isFinalized = true;
            var classification = UiFlowAudioOutcomeClassifier.Classify(RootIntent, _deltas, _isAborted, _abortReason);
            _finalTrace = new UiFlowAudioTrace(
                RootIntent,
                _deltas.ToArray(),
                classification.OutcomeKind,
                classification.EmittedCueId,
                classification.SilenceReason,
                MaxDepth);
            return _finalTrace;
        }
    }

    internal static class UiFlowAudioOutcomeClassifier
    {
        public static UiFlowAudioClassificationResult Classify(
            UiFlowAudioIntentKind rootIntent,
            IReadOnlyList<UiFlowAudioDelta> deltas,
            bool isAborted,
            UiFlowAudioSilenceReason abortReason)
        {
            if (isAborted)
            {
                return Silent(abortReason);
            }

            switch (rootIntent)
            {
                case UiFlowAudioIntentKind.Back:
                    if (HasReverseVisibleDelta(deltas))
                    {
                        return Outcome(UiFlowAudioOutcomeKind.NavigateBack);
                    }

                    if (HasForwardVisibleDelta(deltas))
                    {
                        return Outcome(UiFlowAudioOutcomeKind.NavigateForward);
                    }

                    return Silent(UiFlowAudioSilenceReason.NoVisibleDelta);

                case UiFlowAudioIntentKind.OpenForward:
                    return HasForwardVisibleDelta(deltas)
                        ? Outcome(UiFlowAudioOutcomeKind.NavigateForward)
                        : Silent(UiFlowAudioSilenceReason.NoVisibleDelta);

                case UiFlowAudioIntentKind.Confirm:
                    return HasPopupCompletionDelta(deltas)
                        ? Outcome(UiFlowAudioOutcomeKind.Confirm)
                        : Silent(UiFlowAudioSilenceReason.NoVisibleDelta);

                case UiFlowAudioIntentKind.Cancel:
                    return HasPopupCompletionDelta(deltas)
                        ? Outcome(UiFlowAudioOutcomeKind.Cancel)
                        : Silent(UiFlowAudioSilenceReason.NoVisibleDelta);

                case UiFlowAudioIntentKind.SystemPresentation:
                    return TryResolveSystemPresentationCue(deltas, out var systemOutcome)
                        ? Outcome(systemOutcome)
                        : Silent(UiFlowAudioSilenceReason.SystemPresentationPolicy);

                default:
                    return Silent(UiFlowAudioSilenceReason.Aborted);
            }
        }

        private static bool HasForwardVisibleDelta(IReadOnlyList<UiFlowAudioDelta> deltas)
        {
            for (var i = 0; i < deltas.Count; i++)
            {
                var delta = deltas[i];
                switch (delta.Kind)
                {
                    case UiFlowAudioDeltaKind.RootScreenSet:
                    case UiFlowAudioDeltaKind.PopupOpened:
                        return true;

                    case UiFlowAudioDeltaKind.ScreenTransition:
                        switch (delta.ScreenTransitionKind)
                        {
                            case ScreenTransitionKind.Show:
                            case ScreenTransitionKind.Push:
                            case ScreenTransitionKind.Replace:
                                return true;
                        }

                        break;
                }
            }

            return false;
        }

        private static bool TryResolveSystemPresentationCue(
            IReadOnlyList<UiFlowAudioDelta> deltas,
            out UiFlowAudioOutcomeKind outcomeKind)
        {
            for (var i = 0; i < deltas.Count; i++)
            {
                var delta = deltas[i];
                if (delta.Kind != UiFlowAudioDeltaKind.RootScreenSet)
                {
                    continue;
                }

                switch (delta.CurrentScreenId)
                {
                    case ScreenId.GameClear:
                        outcomeKind = UiFlowAudioOutcomeKind.GameClear;
                        return true;

                    case ScreenId.StageResult:
                        outcomeKind = UiFlowAudioOutcomeKind.StageClear;
                        return true;

                    case ScreenId.LevelFailed:
                        outcomeKind = UiFlowAudioOutcomeKind.LevelFailed;
                        return true;
                }
            }

            outcomeKind = UiFlowAudioOutcomeKind.Silent;
            return false;
        }

        private static bool HasReverseVisibleDelta(IReadOnlyList<UiFlowAudioDelta> deltas)
        {
            for (var i = 0; i < deltas.Count; i++)
            {
                var delta = deltas[i];
                switch (delta.Kind)
                {
                    case UiFlowAudioDeltaKind.ScreenTransition:
                        if (delta.ScreenTransitionKind == ScreenTransitionKind.Pop)
                        {
                            return true;
                        }

                        break;

                    case UiFlowAudioDeltaKind.PopupCompleted:
                        if (IsReversePopupDismiss(delta.PopupId, delta.PopupCompletionKind))
                        {
                            return true;
                        }

                        break;
                }
            }

            return false;
        }

        private static bool HasPopupCompletionDelta(IReadOnlyList<UiFlowAudioDelta> deltas)
        {
            for (var i = 0; i < deltas.Count; i++)
            {
                if (deltas[i].Kind == UiFlowAudioDeltaKind.PopupCompleted)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsReversePopupDismiss(PopupId popupId, PopupCompletionKind completionKind)
        {
            switch (popupId)
            {
                case PopupId.Pause:
                    return completionKind == PopupCompletionKind.Closed;

                case PopupId.ObjectiveInfo:
                    return completionKind == PopupCompletionKind.Acknowledged ||
                           completionKind == PopupCompletionKind.Closed;

                case PopupId.Tooltip:
                    return completionKind == PopupCompletionKind.Closed ||
                           completionKind == PopupCompletionKind.Acknowledged;

                default:
                    return false;
            }
        }

        private static UiFlowAudioClassificationResult Outcome(UiFlowAudioOutcomeKind outcomeKind)
        {
            if (!UiFlowAudioOutcomeMapper.TryMap(outcomeKind, out var cueId))
            {
                return Silent(UiFlowAudioSilenceReason.NoVisibleDelta);
            }

            return new UiFlowAudioClassificationResult(
                outcomeKind,
                cueId,
                UiFlowAudioSilenceReason.None);
        }

        private static UiFlowAudioClassificationResult Silent(UiFlowAudioSilenceReason reason)
        {
            return new UiFlowAudioClassificationResult(
                UiFlowAudioOutcomeKind.Silent,
                null,
                reason);
        }
    }
}

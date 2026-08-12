using System;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Stages
{
    public enum StageTerminalReason
    {
        None = 0,
        Cleared = 1,
        Failed = 2,
        Abandoned = 3,
    }

    public sealed class StageSessionState
    {
        public StageSessionState(
            StageId stageId,
            int currentTickIndex,
            bool isTerminal,
            StageTerminalReason terminalReason)
        {
            StageId = stageId;
            CurrentTickIndex = currentTickIndex;
            IsTerminal = isTerminal;
            TerminalReason = terminalReason;
        }

        public StageId StageId { get; }

        public int CurrentTickIndex { get; }

        public bool IsTerminal { get; }

        public StageTerminalReason TerminalReason { get; }

        public StageSessionState With(
            int currentTickIndex,
            bool isTerminal,
            StageTerminalReason terminalReason)
        {
            return new StageSessionState(
                StageId,
                currentTickIndex,
                isTerminal,
                terminalReason);
        }
    }

    public sealed class StageSessionTracker
    {
        private bool terminalResultEmitted;

        public StageSessionState CurrentState { get; private set; }

        public StageSessionState Start(StageId stageId)
        {
            terminalResultEmitted = false;
            CurrentState = new StageSessionState(
                stageId,
                currentTickIndex: 0,
                isTerminal: false,
                StageTerminalReason.None);
            return CurrentState;
        }

        public StageSessionState Advance(
            TickResult tickResult,
            StageTerminalReason forcedTerminalReason = StageTerminalReason.None)
        {
            if (CurrentState == null)
            {
                throw new InvalidOperationException("Stage session has not been started.");
            }

            if (tickResult == null)
            {
                throw new ArgumentNullException(nameof(tickResult));
            }

            if (CurrentState.IsTerminal)
            {
                return CurrentState;
            }

            var terminalReason = forcedTerminalReason;
            if (tickResult.ObjectiveResult != null && tickResult.ObjectiveResult.IsCleared)
            {
                terminalReason = StageTerminalReason.Cleared;
            }

            CurrentState = CurrentState.With(
                tickResult.TickIndex,
                terminalReason != StageTerminalReason.None,
                terminalReason);
            return CurrentState;
        }

        public bool TryCreateClearResult(out StageClearResult clearResult)
        {
            if (CurrentState == null ||
                !CurrentState.IsTerminal ||
                CurrentState.TerminalReason != StageTerminalReason.Cleared ||
                terminalResultEmitted)
            {
                clearResult = null;
                return false;
            }

            terminalResultEmitted = true;
            clearResult = new StageClearResult(
                CurrentState.StageId,
                CurrentState.CurrentTickIndex);
            return true;
        }

        public bool TryEmitForcedClear(out StageClearResult clearResult)
        {
            if (CurrentState == null || terminalResultEmitted)
            {
                clearResult = null;
                return false;
            }

            terminalResultEmitted = true;
            CurrentState = CurrentState.With(
                CurrentState.CurrentTickIndex,
                isTerminal: true,
                StageTerminalReason.Cleared);
            clearResult = new StageClearResult(
                CurrentState.StageId,
                CurrentState.CurrentTickIndex);
            return true;
        }
    }
}

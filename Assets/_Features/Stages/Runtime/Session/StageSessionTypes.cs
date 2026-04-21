using System;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;

namespace Game.Feature.Stages
{
    public enum StageTerminalReason
    {
        None = 0,
        Cleared = 1,
        Failed = 2,
        Abandoned = 3,
    }

    [Serializable]
    public struct StageRunId : IEquatable<StageRunId>
    {
        public StageRunId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static StageRunId New()
        {
            return new StageRunId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(StageRunId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StageRunId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }
    }

    [Serializable]
    public struct StageCompletionAttemptId : IEquatable<StageCompletionAttemptId>
    {
        public StageCompletionAttemptId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static StageCompletionAttemptId New()
        {
            return new StageCompletionAttemptId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(StageCompletionAttemptId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StageCompletionAttemptId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }
    }

    [Serializable]
    public struct RewardGrantId : IEquatable<RewardGrantId>
    {
        public RewardGrantId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(RewardGrantId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RewardGrantId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }
    }

    [Serializable]
    public struct StageObjectiveProgressSnapshot
    {
        public StageObjectiveProgressSnapshot(
            bool hasObjective,
            bool goalReached,
            bool allConditionsSatisfied,
            bool isCleared,
            int satisfiedConditionCount,
            int totalConditionCount)
        {
            HasObjective = hasObjective;
            GoalReached = goalReached;
            AllConditionsSatisfied = allConditionsSatisfied;
            IsCleared = isCleared;
            SatisfiedConditionCount = satisfiedConditionCount;
            TotalConditionCount = totalConditionCount;
        }

        public bool HasObjective { get; }

        public bool GoalReached { get; }

        public bool AllConditionsSatisfied { get; }

        public bool IsCleared { get; }

        public int SatisfiedConditionCount { get; }

        public int TotalConditionCount { get; }

        public static StageObjectiveProgressSnapshot FromObjectiveResult(StageObjectiveTickResult objectiveResult)
        {
            if (objectiveResult == null)
            {
                return default;
            }

            var satisfiedConditions = 0;
            var totalConditions = objectiveResult.ConditionStatuses?.Count ?? 0;
            for (var i = 0; i < totalConditions; i++)
            {
                if (objectiveResult.ConditionStatuses[i].IsSatisfied)
                {
                    satisfiedConditions++;
                }
            }

            return new StageObjectiveProgressSnapshot(
                objectiveResult.HasObjective,
                objectiveResult.GoalReached,
                objectiveResult.AllConditionsSatisfied,
                objectiveResult.IsCleared,
                satisfiedConditions,
                totalConditions);
        }
    }

    [Serializable]
    public struct StageSessionMetricValue
    {
        public StageSessionMetricValue(string metricId, int value)
        {
            MetricId = metricId ?? string.Empty;
            Value = value;
        }

        public string MetricId { get; }

        public int Value { get; }
    }

    [Serializable]
    public struct StageChallengeRuntimeState
    {
        public StageChallengeRuntimeState(
            string challengeId,
            bool isCompleted,
            bool isFailed,
            int currentValue = 0)
        {
            ChallengeId = challengeId ?? string.Empty;
            IsCompleted = isCompleted;
            IsFailed = isFailed;
            CurrentValue = currentValue;
        }

        public string ChallengeId { get; }

        public bool IsCompleted { get; }

        public bool IsFailed { get; }

        public int CurrentValue { get; }
    }

    public sealed class StageSessionState
    {
        public StageSessionState(
            StageId stageId,
            StageRunId runId,
            int startTickIndex,
            int currentTickIndex,
            bool isTerminal,
            StageTerminalReason terminalReason,
            StageObjectiveProgressSnapshot objectiveProgress,
            StageSessionMetricValue[] sessionMetrics,
            StageChallengeRuntimeState[] challengeRuntimeStates)
        {
            StageId = stageId;
            RunId = runId;
            StartTickIndex = startTickIndex;
            CurrentTickIndex = currentTickIndex;
            IsTerminal = isTerminal;
            TerminalReason = terminalReason;
            ObjectiveProgress = objectiveProgress;
            SessionMetrics = sessionMetrics ?? Array.Empty<StageSessionMetricValue>();
            ChallengeRuntimeStates = challengeRuntimeStates ?? Array.Empty<StageChallengeRuntimeState>();
        }

        public StageId StageId { get; }

        public StageRunId RunId { get; }

        public int StartTickIndex { get; }

        public int CurrentTickIndex { get; }

        public bool IsTerminal { get; }

        public StageTerminalReason TerminalReason { get; }

        public StageObjectiveProgressSnapshot ObjectiveProgress { get; }

        public StageSessionMetricValue[] SessionMetrics { get; }

        public StageChallengeRuntimeState[] ChallengeRuntimeStates { get; }

        public StageSessionState With(
            int currentTickIndex,
            bool isTerminal,
            StageTerminalReason terminalReason,
            StageObjectiveProgressSnapshot objectiveProgress,
            StageSessionMetricValue[] sessionMetrics,
            StageChallengeRuntimeState[] challengeRuntimeStates)
        {
            return new StageSessionState(
                StageId,
                RunId,
                StartTickIndex,
                currentTickIndex,
                isTerminal,
                terminalReason,
                objectiveProgress,
                sessionMetrics,
                challengeRuntimeStates);
        }
    }

    public sealed class StageSessionTracker
    {
        private bool terminalResultEmitted;

        public StageSessionState CurrentState { get; private set; }

        public StageSessionState Start(
            StageId stageId,
            int startTickIndex = 0,
            StageChallengeRuntimeState[] challengeRuntimeStates = null)
        {
            terminalResultEmitted = false;
            CurrentState = new StageSessionState(
                stageId,
                StageRunId.New(),
                startTickIndex,
                startTickIndex,
                isTerminal: false,
                StageTerminalReason.None,
                default,
                Array.Empty<StageSessionMetricValue>(),
                challengeRuntimeStates ?? Array.Empty<StageChallengeRuntimeState>());
            return CurrentState;
        }

        public StageSessionState Advance(
            TickResult tickResult,
            StageSessionMetricValue[] sessionMetrics = null,
            StageChallengeRuntimeState[] challengeRuntimeStates = null,
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

            var objectiveProgress = StageObjectiveProgressSnapshot.FromObjectiveResult(tickResult.ObjectiveResult);
            var terminalReason = forcedTerminalReason;
            if (tickResult.ObjectiveResult != null && tickResult.ObjectiveResult.IsCleared)
            {
                terminalReason = StageTerminalReason.Cleared;
            }

            CurrentState = CurrentState.With(
                tickResult.TickIndex,
                terminalReason != StageTerminalReason.None,
                terminalReason,
                objectiveProgress,
                sessionMetrics ?? CurrentState.SessionMetrics,
                challengeRuntimeStates ?? CurrentState.ChallengeRuntimeStates);
            return CurrentState;
        }

        public void Reset()
        {
            terminalResultEmitted = false;
            CurrentState = null;
        }

        public bool TryCreateClearResult(out StageClearResult clearResult)
        {
            if (CurrentState == null ||
                !CurrentState.IsTerminal ||
                terminalResultEmitted)
            {
                clearResult = null;
                return false;
            }

            terminalResultEmitted = true;
            clearResult = new StageClearResult(
                CurrentState.StageId,
                CurrentState.RunId,
                CurrentState.TerminalReason,
                CurrentState.TerminalReason == StageTerminalReason.Cleared,
                CurrentState.CurrentTickIndex,
                CurrentState.ObjectiveProgress,
                CurrentState.SessionMetrics,
                CurrentState.ChallengeRuntimeStates);
            return true;
        }
    }
}

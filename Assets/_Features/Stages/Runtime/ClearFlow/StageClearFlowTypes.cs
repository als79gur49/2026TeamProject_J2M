using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public enum StageClearSource
    {
        Objective = 0,
        ForcedByDemoStageControl = 1,
    }

    public sealed class StageClearResult
    {
        public StageClearResult(
            StageId stageId,
            StageRunId stageRunId,
            StageTerminalReason endReason,
            bool wasCleared,
            int finalTickIndex,
            StageObjectiveProgressSnapshot finalObjectiveProgress,
            StageSessionMetricValue[] sessionMetricsSnapshot,
            StageChallengeRuntimeState[] challengeRuntimeStates,
            StageClearSource clearSource = StageClearSource.Objective)
        {
            StageId = stageId;
            StageRunId = stageRunId;
            EndReason = endReason;
            WasCleared = wasCleared;
            FinalTickIndex = finalTickIndex;
            FinalObjectiveProgress = finalObjectiveProgress;
            SessionMetricsSnapshot = sessionMetricsSnapshot ?? Array.Empty<StageSessionMetricValue>();
            ChallengeRuntimeStates = challengeRuntimeStates ?? Array.Empty<StageChallengeRuntimeState>();
            ClearSource = clearSource;
        }

        public StageId StageId { get; }

        public StageRunId StageRunId { get; }

        public StageTerminalReason EndReason { get; }

        public bool WasCleared { get; }

        public int FinalTickIndex { get; }

        public StageObjectiveProgressSnapshot FinalObjectiveProgress { get; }

        public StageSessionMetricValue[] SessionMetricsSnapshot { get; }

        public StageChallengeRuntimeState[] ChallengeRuntimeStates { get; }

        public StageClearSource ClearSource { get; }
    }

}

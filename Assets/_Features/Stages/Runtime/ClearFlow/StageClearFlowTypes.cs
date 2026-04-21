using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
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
            StageChallengeRuntimeState[] challengeRuntimeStates)
        {
            StageId = stageId;
            StageRunId = stageRunId;
            EndReason = endReason;
            WasCleared = wasCleared;
            FinalTickIndex = finalTickIndex;
            FinalObjectiveProgress = finalObjectiveProgress;
            SessionMetricsSnapshot = sessionMetricsSnapshot ?? Array.Empty<StageSessionMetricValue>();
            ChallengeRuntimeStates = challengeRuntimeStates ?? Array.Empty<StageChallengeRuntimeState>();
        }

        public StageId StageId { get; }

        public StageRunId StageRunId { get; }

        public StageTerminalReason EndReason { get; }

        public bool WasCleared { get; }

        public int FinalTickIndex { get; }

        public StageObjectiveProgressSnapshot FinalObjectiveProgress { get; }

        public StageSessionMetricValue[] SessionMetricsSnapshot { get; }

        public StageChallengeRuntimeState[] ChallengeRuntimeStates { get; }
    }

    public readonly struct StageChallengeEvaluationResult
    {
        public StageChallengeEvaluationResult(
            string challengeId,
            string displayName,
            bool isCompleted,
            bool wasAwarded)
        {
            ChallengeId = challengeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            IsCompleted = isCompleted;
            WasAwarded = wasAwarded;
        }

        public string ChallengeId { get; }

        public string DisplayName { get; }

        public bool IsCompleted { get; }

        public bool WasAwarded { get; }
    }

    public sealed class StageClearEvaluationResult
    {
        public StageClearEvaluationResult(
            StageId stageId,
            StageRunId stageRunId,
            bool wasCleared,
            int score,
            int starsEarned,
            string rankId,
            IReadOnlyList<StageChallengeEvaluationResult> challengeResults)
        {
            StageId = stageId;
            StageRunId = stageRunId;
            WasCleared = wasCleared;
            Score = score;
            StarsEarned = starsEarned;
            RankId = rankId ?? string.Empty;
            ChallengeResults = challengeResults ?? Array.Empty<StageChallengeEvaluationResult>();
        }

        public StageId StageId { get; }

        public StageRunId StageRunId { get; }

        public bool WasCleared { get; }

        public int Score { get; }

        public int StarsEarned { get; }

        public string RankId { get; }

        public IReadOnlyList<StageChallengeEvaluationResult> ChallengeResults { get; }
    }

    public readonly struct RewardGrantEntry
    {
        public RewardGrantEntry(
            string rewardRuleId,
            RewardEntry reward,
            RewardGrantId rewardGrantId)
        {
            RewardRuleId = rewardRuleId ?? string.Empty;
            Reward = reward;
            RewardGrantId = rewardGrantId;
        }

        public string RewardRuleId { get; }

        public RewardEntry Reward { get; }

        public RewardGrantId RewardGrantId { get; }
    }

    public sealed class RewardGrantResult
    {
        public RewardGrantResult(
            StageId stageId,
            StageRunId stageRunId,
            IReadOnlyList<RewardGrantEntry> grantedRewards,
            IReadOnlyList<string> grantedRuleIds,
            IReadOnlyList<RewardGrantId> rewardGrantIds,
            bool wasFirstClear)
        {
            StageId = stageId;
            StageRunId = stageRunId;
            GrantedRewards = grantedRewards ?? Array.Empty<RewardGrantEntry>();
            GrantedRuleIds = grantedRuleIds ?? Array.Empty<string>();
            RewardGrantIds = rewardGrantIds ?? Array.Empty<RewardGrantId>();
            WasFirstClear = wasFirstClear;
        }

        public StageId StageId { get; }

        public StageRunId StageRunId { get; }

        public IReadOnlyList<RewardGrantEntry> GrantedRewards { get; }

        public IReadOnlyList<string> GrantedRuleIds { get; }

        public IReadOnlyList<RewardGrantId> RewardGrantIds { get; }

        public bool WasFirstClear { get; }

        public bool AnyGranted => GrantedRewards.Count > 0;
    }
}

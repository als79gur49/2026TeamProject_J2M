using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public static class StageClearEvaluator
    {
        public static StageClearEvaluationResult Evaluate(
            StageClearEvaluationDefinition definition,
            StageClearResult clearResult)
        {
            if (clearResult == null)
            {
                throw new ArgumentNullException(nameof(clearResult));
            }

            definition ??= ScriptableObjectBackedDefaults.EmptyStageClearEvaluationDefinition;

            var score = definition.BaseScore;
            var metricsById = BuildMetricLookup(clearResult.SessionMetricsSnapshot);
            var scoreRules = definition.ScoreRules;
            for (var i = 0; i < scoreRules.Length; i++)
            {
                var metricValue = metricsById.TryGetValue(scoreRules[i].MetricId ?? string.Empty, out var value)
                    ? value
                    : 0;
                score += scoreRules[i].ConstantBonus;
                score += scoreRules[i].SubtractMetricValue
                    ? -metricValue * scoreRules[i].Multiplier
                    : metricValue * scoreRules[i].Multiplier;
            }

            var challengeResults = EvaluateChallenges(definition, clearResult);
            for (var i = 0; i < definition.Challenges.Length && i < challengeResults.Count; i++)
            {
                if (challengeResults[i].WasAwarded)
                {
                    score += definition.Challenges[i].ScoreBonus;
                }
            }

            var awardsEnabled = clearResult.WasCleared || !definition.RequireClearForAwards;
            var starsEarned = awardsEnabled ? ResolveStars(definition.StarThresholds, score) : 0;
            var rankId = awardsEnabled ? ResolveRank(definition.RankThresholds, score) : string.Empty;

            return new StageClearEvaluationResult(
                clearResult.StageId,
                clearResult.StageRunId,
                clearResult.WasCleared,
                score,
                starsEarned,
                rankId,
                challengeResults);
        }

        private static IReadOnlyList<StageChallengeEvaluationResult> EvaluateChallenges(
            StageClearEvaluationDefinition definition,
            StageClearResult clearResult)
        {
            if (definition.Challenges.Length == 0)
            {
                return Array.Empty<StageChallengeEvaluationResult>();
            }

            var challengeRuntimeLookup = new Dictionary<string, StageChallengeRuntimeState>(StringComparer.Ordinal);
            for (var i = 0; i < clearResult.ChallengeRuntimeStates.Length; i++)
            {
                challengeRuntimeLookup[clearResult.ChallengeRuntimeStates[i].ChallengeId] =
                    clearResult.ChallengeRuntimeStates[i];
            }

            var results = new StageChallengeEvaluationResult[definition.Challenges.Length];
            for (var i = 0; i < definition.Challenges.Length; i++)
            {
                var challenge = definition.Challenges[i];
                var isCompleted = challengeRuntimeLookup.TryGetValue(challenge.ChallengeId ?? string.Empty, out var runtimeState) &&
                                  runtimeState.IsCompleted &&
                                  !runtimeState.IsFailed;
                var wasAwarded = isCompleted && (!challenge.AwardOnlyWhenCleared || clearResult.WasCleared);
                results[i] = new StageChallengeEvaluationResult(
                    challenge.ChallengeId,
                    challenge.DisplayName,
                    isCompleted,
                    wasAwarded);
            }

            return results;
        }

        private static int ResolveStars(IReadOnlyList<StageStarThresholdDefinition> thresholds, int score)
        {
            var stars = 0;
            for (var i = 0; i < thresholds.Count; i++)
            {
                if (score >= thresholds[i].MinimumScore)
                {
                    stars = Math.Max(stars, thresholds[i].StarCount);
                }
            }

            return stars;
        }

        private static string ResolveRank(IReadOnlyList<StageRankThresholdDefinition> thresholds, int score)
        {
            var rankId = string.Empty;
            var bestThreshold = int.MinValue;
            for (var i = 0; i < thresholds.Count; i++)
            {
                if (score >= thresholds[i].MinimumScore &&
                    thresholds[i].MinimumScore >= bestThreshold)
                {
                    bestThreshold = thresholds[i].MinimumScore;
                    rankId = thresholds[i].RankId ?? string.Empty;
                }
            }

            return rankId;
        }

        private static Dictionary<string, int> BuildMetricLookup(IReadOnlyList<StageSessionMetricValue> metrics)
        {
            var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < metrics.Count; i++)
            {
                lookup[metrics[i].MetricId ?? string.Empty] = metrics[i].Value;
            }

            return lookup;
        }
    }

    public static class RewardEvaluator
    {
        public static RewardGrantResult Evaluate(
            StageRewardDefinition definition,
            StageClearEvaluationResult evaluationResult,
            PlayerStageProgress preUpdateProgress)
        {
            if (evaluationResult == null)
            {
                throw new ArgumentNullException(nameof(evaluationResult));
            }

            definition ??= ScriptableObjectBackedDefaults.EmptyStageRewardDefinition;
            preUpdateProgress ??= PlayerStageProgress.CreateEmpty(evaluationResult.StageId);

            var rewardGrants = new List<RewardGrantEntry>();
            var grantedRuleIds = new List<string>();
            var rewardGrantIds = new List<RewardGrantId>();
            var wasFirstClear = evaluationResult.WasCleared && !preUpdateProgress.HasCleared;
            var rules = definition.Rules;

            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                var normalizedRuleId = rule.RuleId?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(normalizedRuleId))
                {
                    continue;
                }

                if (!IsRuleSatisfied(rule, evaluationResult))
                {
                    continue;
                }

                if (rule.GrantOnce && preUpdateProgress.HasConsumedRewardRule(rule))
                {
                    continue;
                }

                var rewardGrantId = CreateRewardGrantId(evaluationResult.StageId, evaluationResult.StageRunId, normalizedRuleId, rule.GrantOnce);
                var rewards = rule.Rewards ?? Array.Empty<RewardEntry>();
                for (var rewardIndex = 0; rewardIndex < rewards.Length; rewardIndex++)
                {
                    rewardGrants.Add(new RewardGrantEntry(normalizedRuleId, rewards[rewardIndex], rewardGrantId));
                }

                grantedRuleIds.Add(normalizedRuleId);
                rewardGrantIds.Add(rewardGrantId);
            }

            return new RewardGrantResult(
                evaluationResult.StageId,
                evaluationResult.StageRunId,
                rewardGrants,
                grantedRuleIds,
                rewardGrantIds,
                wasFirstClear);
        }

        public static RewardGrantId CreateRewardGrantId(
            StageId stageId,
            StageRunId stageRunId,
            string rewardRuleId,
            bool grantOnce)
        {
            return grantOnce
                ? new RewardGrantId($"{stageId.Value}:{rewardRuleId}")
                : new RewardGrantId($"{stageId.Value}:{stageRunId.Value}:{rewardRuleId}");
        }

        private static bool IsRuleSatisfied(
            StageRewardRuleDefinition rule,
            StageClearEvaluationResult evaluationResult)
        {
            if (!evaluationResult.WasCleared)
            {
                return false;
            }

            switch (rule.TriggerKind)
            {
                case StageRewardTriggerKind.Clear:
                    return true;

                case StageRewardTriggerKind.EvaluationThreshold:
                    return evaluationResult.StarsEarned >= Math.Max(0, rule.MinimumStars) &&
                           (string.IsNullOrWhiteSpace(rule.RequiredRankId) ||
                            string.Equals(
                                evaluationResult.RankId,
                                rule.RequiredRankId.Trim(),
                                StringComparison.Ordinal));

                case StageRewardTriggerKind.ChallengeCompletion:
                    if (string.IsNullOrWhiteSpace(rule.RequiredChallengeId))
                    {
                        return false;
                    }

                    for (var i = 0; i < evaluationResult.ChallengeResults.Count; i++)
                    {
                        var challengeResult = evaluationResult.ChallengeResults[i];
                        if (string.Equals(
                                challengeResult.ChallengeId,
                                rule.RequiredChallengeId.Trim(),
                                StringComparison.Ordinal) &&
                            challengeResult.IsCompleted)
                        {
                            return true;
                        }
                    }

                    return false;

                default:
                    return false;
            }
        }
    }

    internal static class ScriptableObjectBackedDefaults
    {
        private static StageClearEvaluationDefinition emptyStageClearEvaluationDefinition;
        private static StageRewardDefinition emptyStageRewardDefinition;

        public static StageClearEvaluationDefinition EmptyStageClearEvaluationDefinition
        {
            get
            {
                if (emptyStageClearEvaluationDefinition == null)
                {
                    emptyStageClearEvaluationDefinition = UnityEngine.ScriptableObject.CreateInstance<StageClearEvaluationDefinition>();
                    emptyStageClearEvaluationDefinition.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
                }

                return emptyStageClearEvaluationDefinition;
            }
        }

        public static StageRewardDefinition EmptyStageRewardDefinition
        {
            get
            {
                if (emptyStageRewardDefinition == null)
                {
                    emptyStageRewardDefinition = UnityEngine.ScriptableObject.CreateInstance<StageRewardDefinition>();
                    emptyStageRewardDefinition.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
                }

                return emptyStageRewardDefinition;
            }
        }
    }
}

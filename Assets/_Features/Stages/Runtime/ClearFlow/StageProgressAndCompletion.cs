using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public sealed class MinimalStageCompletionResult
    {
        public MinimalStageCompletionResult(
            StageId stageId,
            StageRunId stageRunId,
            StageCompletionAttemptId attemptId,
            StageTerminalReason terminalReason,
            bool wasCleared,
            int finalTickIndex,
            StageObjectiveProgressSnapshot objectiveSnapshot,
            StageClearSource clearSource)
        {
            StageId = stageId;
            StageRunId = stageRunId;
            AttemptId = attemptId;
            TerminalReason = terminalReason;
            WasCleared = wasCleared;
            FinalTickIndex = finalTickIndex;
            ObjectiveSnapshot = objectiveSnapshot;
            ClearSource = clearSource;
        }

        public StageId StageId { get; }

        public StageRunId StageRunId { get; }

        public StageCompletionAttemptId AttemptId { get; }

        public StageTerminalReason TerminalReason { get; }

        public bool WasCleared { get; }

        public int FinalTickIndex { get; }

        public StageObjectiveProgressSnapshot ObjectiveSnapshot { get; }

        public StageClearSource ClearSource { get; }
    }

    public sealed class MinimalStageCompletionReadModel
    {
        public MinimalStageCompletionReadModel(
            StageId stageId,
            string displayName,
            MinimalStageCompletionResult result,
            string presentationTitle,
            string presentationSummary,
            string presentationDetail,
            string continueLabel,
            StageNavigationRequest continueRequest,
            StageNavigationRequest retryRequest,
            StageNavigationRequest nextStageRequest)
        {
            StageId = stageId;
            DisplayName = displayName ?? string.Empty;
            Result = result ?? throw new ArgumentNullException(nameof(result));
            PresentationTitle = presentationTitle ?? string.Empty;
            PresentationSummary = presentationSummary ?? string.Empty;
            PresentationDetail = presentationDetail ?? string.Empty;
            ContinueLabel = continueLabel ?? string.Empty;
            ContinueRequest = continueRequest;
            RetryRequest = retryRequest;
            NextStageRequest = nextStageRequest;
        }

        public StageId StageId { get; }

        public string DisplayName { get; }

        public MinimalStageCompletionResult Result { get; }

        public string PresentationTitle { get; }

        public string PresentationSummary { get; }

        public string PresentationDetail { get; }

        public string ContinueLabel { get; }

        public StageNavigationRequest ContinueRequest { get; }

        public StageNavigationRequest RetryRequest { get; }

        public StageNavigationRequest NextStageRequest { get; }
    }

    public static class MinimalStageCompletionReadModelBuilder
    {
        private static readonly Lazy<CampaignStageSequenceResolver> CanonicalCampaignResolver =
            new(() => new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()));

        public static MinimalStageCompletionReadModel Build(
            StageContentEntry entry,
            StageClearResult clearResult)
        {
            if (clearResult == null)
            {
                throw new ArgumentNullException(nameof(clearResult));
            }

            var stageId = clearResult.StageId.IsValid
                ? clearResult.StageId
                : entry != null ? entry.StageId : StageId.None;
            var presentation = entry != null
                ? StagePresentationAssembler.Resolve(entry.PresentationDefinition)
                : StagePresentationAssembler.EmptyResolvedData;
            var result = new MinimalStageCompletionResult(
                stageId,
                clearResult.StageRunId,
                StageCompletionAttemptId.New(),
                clearResult.EndReason,
                clearResult.WasCleared,
                clearResult.FinalTickIndex,
                clearResult.FinalObjectiveProgress,
                clearResult.ClearSource);
            var nextStageRequest = ResolveNextStageRequest(stageId);
            var continueRequest = nextStageRequest.IsValid
                ? nextStageRequest.WithTransitionHint(StageTransitionHint.ForKind(StageTransitionKind.StageClearNext))
                : new StageNavigationRequest(
                    stageId,
                    StageNavigationKind.Continue,
                    "stage-result-continue",
                    StageTransitionHint.ForKind(StageTransitionKind.StageClearNext));
            var retryRequest = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "stage-result-retry",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual));

            return new MinimalStageCompletionReadModel(
                stageId,
                presentation.DisplayName,
                result,
                string.IsNullOrWhiteSpace(presentation.ResultTitle) ? "Stage Cleared" : presentation.ResultTitle,
                presentation.ResultSummaryText,
                presentation.ResultDetailText,
                string.IsNullOrWhiteSpace(presentation.ResultContinueLabel) ? "Continue" : presentation.ResultContinueLabel,
                continueRequest,
                retryRequest,
                nextStageRequest.IsValid
                    ? nextStageRequest.WithTransitionHint(StageTransitionHint.ForKind(StageTransitionKind.StageClearNext))
                    : StageNavigationRequest.None);
        }

        private static StageNavigationRequest ResolveNextStageRequest(StageId stageId)
        {
            if (CampaignStageResultNavigationStore.TryGet(stageId, out var campaignNavigationPlan))
            {
                CampaignStageResultNavigationStore.Clear();
                return campaignNavigationPlan.NextStageRequest;
            }

            var resolver = CanonicalCampaignResolver.Value;
            if (!resolver.Contains(stageId) ||
                resolver.IsFinal(stageId) ||
                !resolver.TryGetNext(stageId, out var nextStageId))
            {
                return StageNavigationRequest.None;
            }

            return new StageNavigationRequest(
                nextStageId,
                StageNavigationKind.NextStage,
                "campaign-auto-next",
                StageTransitionHint.ForKind(StageTransitionKind.StageClearNext));
        }
    }

    [Serializable]
    public sealed class PlayerStageProgress
    {
        public static PlayerStageProgress CreateEmpty(StageId stageId)
        {
            return new PlayerStageProgress
            {
                StageId = stageId,
                HasStarted = false,
                HasCleared = false,
                ClearCount = 0,
                BestScore = 0,
                BestStars = 0,
                BestRankId = string.Empty,
                CompletedChallengeIds = Array.Empty<string>(),
                ConsumedRewardRuleIds = Array.Empty<string>(),
                ProcessedStageRunIds = Array.Empty<string>(),
            };
        }

        public StageId StageId { get; set; }

        public bool HasStarted { get; set; }

        public bool HasCleared { get; set; }

        public int ClearCount { get; set; }

        public int BestScore { get; set; }

        public int BestStars { get; set; }

        public string BestRankId { get; set; } = string.Empty;

        public string[] CompletedChallengeIds { get; set; } = Array.Empty<string>();

        public string[] ConsumedRewardRuleIds { get; set; } = Array.Empty<string>();

        public string[] ProcessedStageRunIds { get; set; } = Array.Empty<string>();

        public PlayerStageProgress Clone()
        {
            return new PlayerStageProgress
            {
                StageId = StageId,
                HasStarted = HasStarted,
                HasCleared = HasCleared,
                ClearCount = ClearCount,
                BestScore = BestScore,
                BestStars = BestStars,
                BestRankId = BestRankId ?? string.Empty,
                CompletedChallengeIds = (string[])(CompletedChallengeIds ?? Array.Empty<string>()).Clone(),
                ConsumedRewardRuleIds = (string[])(ConsumedRewardRuleIds ?? Array.Empty<string>()).Clone(),
                ProcessedStageRunIds = (string[])(ProcessedStageRunIds ?? Array.Empty<string>()).Clone(),
            };
        }

        public bool HasConsumedRewardRule(StageRewardRuleDefinition rule)
        {
            var consumedIds = ConsumedRewardRuleIds ?? Array.Empty<string>();
            var currentRuleId = rule.RuleId?.Trim() ?? string.Empty;
            for (var i = 0; i < consumedIds.Length; i++)
            {
                if (string.Equals(consumedIds[i], currentRuleId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            var deprecatedIds = rule.DeprecatedRuleIds ?? Array.Empty<string>();
            for (var deprecatedIndex = 0; deprecatedIndex < deprecatedIds.Length; deprecatedIndex++)
            {
                var deprecatedId = deprecatedIds[deprecatedIndex]?.Trim() ?? string.Empty;
                for (var consumedIndex = 0; consumedIndex < consumedIds.Length; consumedIndex++)
                {
                    if (string.Equals(consumedIds[consumedIndex], deprecatedId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public sealed class ProgressPatch
    {
        public ProgressPatch(
            StageId stageId,
            bool hasStarted,
            bool hasCleared,
            int clearCountDelta,
            int bestScore,
            int bestStars,
            string bestRankId,
            string[] completedChallengeIds,
            string[] consumedRewardRuleIds,
            string[] processedStageRunIds)
        {
            StageId = stageId;
            HasStarted = hasStarted;
            HasCleared = hasCleared;
            ClearCountDelta = clearCountDelta;
            BestScore = bestScore;
            BestStars = bestStars;
            BestRankId = bestRankId ?? string.Empty;
            CompletedChallengeIds = completedChallengeIds ?? Array.Empty<string>();
            ConsumedRewardRuleIds = consumedRewardRuleIds ?? Array.Empty<string>();
            ProcessedStageRunIds = processedStageRunIds ?? Array.Empty<string>();
        }

        public StageId StageId { get; }

        public bool HasStarted { get; }

        public bool HasCleared { get; }

        public int ClearCountDelta { get; }

        public int BestScore { get; }

        public int BestStars { get; }

        public string BestRankId { get; }

        public string[] CompletedChallengeIds { get; }

        public string[] ConsumedRewardRuleIds { get; }

        public string[] ProcessedStageRunIds { get; }

        public PlayerStageProgress ApplyTo(PlayerStageProgress current)
        {
            var baseline = current?.Clone() ?? PlayerStageProgress.CreateEmpty(StageId);
            baseline.StageId = StageId;
            baseline.HasStarted |= HasStarted;
            baseline.HasCleared |= HasCleared;
            baseline.ClearCount += ClearCountDelta;
            baseline.BestScore = Math.Max(baseline.BestScore, BestScore);
            baseline.BestStars = Math.Max(baseline.BestStars, BestStars);
            if (!string.IsNullOrWhiteSpace(BestRankId))
            {
                baseline.BestRankId = BestRankId;
            }
            baseline.CompletedChallengeIds = MergeDistinct(baseline.CompletedChallengeIds, CompletedChallengeIds);
            baseline.ConsumedRewardRuleIds = MergeDistinct(baseline.ConsumedRewardRuleIds, ConsumedRewardRuleIds);
            baseline.ProcessedStageRunIds = MergeDistinct(baseline.ProcessedStageRunIds, ProcessedStageRunIds);
            return baseline;
        }

        private static string[] MergeDistinct(IReadOnlyList<string> existing, IReadOnlyList<string> incoming)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < existing.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(existing[i]))
                {
                    set.Add(existing[i]);
                }
            }

            for (var i = 0; i < incoming.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(incoming[i]))
                {
                    set.Add(incoming[i]);
                }
            }

            var merged = new string[set.Count];
            set.CopyTo(merged);
            Array.Sort(merged, StringComparer.Ordinal);
            return merged;
        }
    }

    public sealed class InventoryPatch
    {
        public InventoryPatch(RewardEntry[] rewards, RewardGrantId[] appliedRewardGrantIds)
        {
            Rewards = rewards ?? Array.Empty<RewardEntry>();
            AppliedRewardGrantIds = appliedRewardGrantIds ?? Array.Empty<RewardGrantId>();
        }

        public RewardEntry[] Rewards { get; }

        public RewardGrantId[] AppliedRewardGrantIds { get; }

        public void ApplyTo(
            IDictionary<string, int> inventoryBalances,
            ISet<RewardGrantId> appliedRewardGrantIds)
        {
            if (inventoryBalances == null)
            {
                throw new ArgumentNullException(nameof(inventoryBalances));
            }

            if (appliedRewardGrantIds == null)
            {
                throw new ArgumentNullException(nameof(appliedRewardGrantIds));
            }

            for (var i = 0; i < AppliedRewardGrantIds.Length; i++)
            {
                if (!AppliedRewardGrantIds[i].IsValid || appliedRewardGrantIds.Contains(AppliedRewardGrantIds[i]))
                {
                    return;
                }
            }

            for (var i = 0; i < AppliedRewardGrantIds.Length; i++)
            {
                appliedRewardGrantIds.Add(AppliedRewardGrantIds[i]);
            }

            for (var i = 0; i < Rewards.Length; i++)
            {
                var reward = Rewards[i];
                var rewardId = reward.RewardId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rewardId))
                {
                    continue;
                }

                inventoryBalances.TryGetValue(rewardId, out var existingAmount);
                inventoryBalances[rewardId] = existingAmount + reward.Amount;
            }
        }
    }

    public sealed class StageCompletionTransaction
    {
        public StageCompletionTransaction(
            StageClearResult clearResult,
            StageClearEvaluationResult clearEvaluationResult,
            RewardGrantResult rewardGrantResult,
            ProgressPatch progressPatch,
            InventoryPatch inventoryPatch,
            StageCompletionAttemptId attemptId)
        {
            ClearResult = clearResult ?? throw new ArgumentNullException(nameof(clearResult));
            ClearEvaluationResult = clearEvaluationResult ?? throw new ArgumentNullException(nameof(clearEvaluationResult));
            RewardGrantResult = rewardGrantResult ?? throw new ArgumentNullException(nameof(rewardGrantResult));
            ProgressPatch = progressPatch ?? throw new ArgumentNullException(nameof(progressPatch));
            InventoryPatch = inventoryPatch ?? throw new ArgumentNullException(nameof(inventoryPatch));
            AttemptId = attemptId;
        }

        public StageClearResult ClearResult { get; }

        public StageClearEvaluationResult ClearEvaluationResult { get; }

        public RewardGrantResult RewardGrantResult { get; }

        public ProgressPatch ProgressPatch { get; }

        public InventoryPatch InventoryPatch { get; }

        public StageCompletionAttemptId AttemptId { get; }
    }

    public static class StageCompletionTransactionBuilder
    {
        public static StageCompletionTransaction Build(
            StageClearResult clearResult,
            StageClearEvaluationResult clearEvaluationResult,
            RewardGrantResult rewardGrantResult,
            PlayerStageProgress preUpdateProgress)
        {
            var progressPatch = PlayerStageProgressUpdater.BuildPatch(
                clearResult,
                clearEvaluationResult,
                rewardGrantResult,
                preUpdateProgress);
            var inventoryPatch = BuildInventoryPatch(rewardGrantResult);
            return new StageCompletionTransaction(
                clearResult,
                clearEvaluationResult,
                rewardGrantResult,
                progressPatch,
                inventoryPatch,
                StageCompletionAttemptId.New());
        }

        private static InventoryPatch BuildInventoryPatch(RewardGrantResult rewardGrantResult)
        {
            var rewards = new RewardEntry[rewardGrantResult.GrantedRewards.Count];
            for (var i = 0; i < rewardGrantResult.GrantedRewards.Count; i++)
            {
                rewards[i] = rewardGrantResult.GrantedRewards[i].Reward;
            }

            var grantIds = new RewardGrantId[rewardGrantResult.RewardGrantIds.Count];
            for (var i = 0; i < rewardGrantResult.RewardGrantIds.Count; i++)
            {
                grantIds[i] = rewardGrantResult.RewardGrantIds[i];
            }

            return new InventoryPatch(rewards, grantIds);
        }
    }

    public static class PlayerStageProgressUpdater
    {
        public static ProgressPatch BuildPatch(
            StageClearResult clearResult,
            StageClearEvaluationResult evaluationResult,
            RewardGrantResult rewardGrantResult,
            PlayerStageProgress preUpdateProgress)
        {
            preUpdateProgress ??= PlayerStageProgress.CreateEmpty(clearResult.StageId);

            var completedChallengeIds = new List<string>();
            for (var i = 0; i < evaluationResult.ChallengeResults.Count; i++)
            {
                if (evaluationResult.ChallengeResults[i].IsCompleted)
                {
                    completedChallengeIds.Add(evaluationResult.ChallengeResults[i].ChallengeId);
                }
            }

            var consumedRuleIds = new List<string>();
            for (var i = 0; i < rewardGrantResult.GrantedRuleIds.Count; i++)
            {
                consumedRuleIds.Add(rewardGrantResult.GrantedRuleIds[i]);
            }

            var shouldReplaceBestRank =
                string.IsNullOrWhiteSpace(preUpdateProgress.BestRankId) ||
                evaluationResult.Score >= preUpdateProgress.BestScore;

            return new ProgressPatch(
                clearResult.StageId,
                hasStarted: true,
                hasCleared: clearResult.WasCleared,
                clearCountDelta: clearResult.WasCleared ? 1 : 0,
                bestScore: evaluationResult.Score,
                bestStars: evaluationResult.StarsEarned,
                bestRankId: shouldReplaceBestRank ? evaluationResult.RankId : string.Empty,
                completedChallengeIds: completedChallengeIds.ToArray(),
                consumedRewardRuleIds: consumedRuleIds.ToArray(),
                processedStageRunIds: clearResult.StageRunId.IsValid
                    ? new[] { clearResult.StageRunId.Value }
                    : Array.Empty<string>());
        }
    }

    public enum StageCompletionCommitFailureKind
    {
        None = 0,
        SaveFailed = 1,
    }

    public readonly struct CompletionCommitResult
    {
        public CompletionCommitResult(
            bool committedNow,
            bool alreadyCommitted,
            int savedSnapshotVersion,
            StageCompletionCommitFailureKind failureKind)
        {
            CommittedNow = committedNow;
            AlreadyCommitted = alreadyCommitted;
            SavedSnapshotVersion = savedSnapshotVersion;
            FailureKind = failureKind;
        }

        public bool CommittedNow { get; }

        public bool AlreadyCommitted { get; }

        public int SavedSnapshotVersion { get; }

        public StageCompletionCommitFailureKind FailureKind { get; }
    }

    public sealed class StageCompletionProfileSnapshot
    {
        public int Version { get; set; }

        public Dictionary<string, int> InventoryBalances { get; set; } = new(StringComparer.Ordinal);

        public Dictionary<StageId, PlayerStageProgress> ProgressByStageId { get; set; } = new();

        public HashSet<string> ProcessedStageRunIds { get; set; } = new(StringComparer.Ordinal);

        public HashSet<string> ProcessedCompletionAttemptIds { get; set; } = new(StringComparer.Ordinal);

        public HashSet<string> AppliedRewardGrantIds { get; set; } = new(StringComparer.Ordinal);

        public StageCompletionProfileSnapshot Clone()
        {
            var progressByStageId = new Dictionary<StageId, PlayerStageProgress>();
            foreach (var pair in ProgressByStageId)
            {
                progressByStageId[pair.Key] = pair.Value?.Clone();
            }

            return new StageCompletionProfileSnapshot
            {
                Version = Version,
                InventoryBalances = new Dictionary<string, int>(InventoryBalances, StringComparer.Ordinal),
                ProgressByStageId = progressByStageId,
                ProcessedStageRunIds = new HashSet<string>(ProcessedStageRunIds, StringComparer.Ordinal),
                ProcessedCompletionAttemptIds = new HashSet<string>(ProcessedCompletionAttemptIds, StringComparer.Ordinal),
                AppliedRewardGrantIds = new HashSet<string>(AppliedRewardGrantIds, StringComparer.Ordinal),
            };
        }
    }

    public interface IStageCompletionProfileStore
    {
        StageCompletionProfileSnapshot Load();

        void Save(StageCompletionProfileSnapshot snapshot);
    }

    public sealed class StageCompletionCommitter
    {
        private readonly IStageCompletionProfileStore store;

        public StageCompletionCommitter(IStageCompletionProfileStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public CompletionCommitResult Commit(StageCompletionTransaction transaction)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            var snapshot = store.Load() ?? new StageCompletionProfileSnapshot();
            if (transaction.AttemptId.IsValid &&
                snapshot.ProcessedCompletionAttemptIds.Contains(transaction.AttemptId.Value))
            {
                return new CompletionCommitResult(
                    committedNow: false,
                    alreadyCommitted: true,
                    savedSnapshotVersion: snapshot.Version,
                    failureKind: StageCompletionCommitFailureKind.None);
            }

            if (transaction.ClearResult.StageRunId.IsValid &&
                snapshot.ProcessedStageRunIds.Contains(transaction.ClearResult.StageRunId.Value))
            {
                return new CompletionCommitResult(
                    committedNow: false,
                    alreadyCommitted: true,
                    savedSnapshotVersion: snapshot.Version,
                    failureKind: StageCompletionCommitFailureKind.None);
            }

            var updatedSnapshot = snapshot.Clone();
            try
            {
                var rewardGrantIds = new HashSet<RewardGrantId>();
                foreach (var appliedId in updatedSnapshot.AppliedRewardGrantIds)
                {
                    rewardGrantIds.Add(new RewardGrantId(appliedId));
                }

                transaction.InventoryPatch.ApplyTo(updatedSnapshot.InventoryBalances, rewardGrantIds);
                updatedSnapshot.AppliedRewardGrantIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var grantId in rewardGrantIds)
                {
                    if (grantId.IsValid)
                    {
                        updatedSnapshot.AppliedRewardGrantIds.Add(grantId.Value);
                    }
                }

                updatedSnapshot.ProgressByStageId.TryGetValue(transaction.ProgressPatch.StageId, out var currentProgress);
                updatedSnapshot.ProgressByStageId[transaction.ProgressPatch.StageId] =
                    transaction.ProgressPatch.ApplyTo(currentProgress);

                if (transaction.ClearResult.StageRunId.IsValid)
                {
                    updatedSnapshot.ProcessedStageRunIds.Add(transaction.ClearResult.StageRunId.Value);
                }

                if (transaction.AttemptId.IsValid)
                {
                    updatedSnapshot.ProcessedCompletionAttemptIds.Add(transaction.AttemptId.Value);
                }

                updatedSnapshot.Version++;
                store.Save(updatedSnapshot);
                return new CompletionCommitResult(
                    committedNow: true,
                    alreadyCommitted: false,
                    savedSnapshotVersion: updatedSnapshot.Version,
                    failureKind: StageCompletionCommitFailureKind.None);
            }
            catch
            {
                return new CompletionCommitResult(
                    committedNow: false,
                    alreadyCommitted: false,
                    savedSnapshotVersion: snapshot.Version,
                    failureKind: StageCompletionCommitFailureKind.SaveFailed);
            }
        }
    }

    public static class StageProgressionEvaluator
    {
        public static bool IsUnlocked(
            StageProgressionDefinition definition,
            IReadOnlyDictionary<StageId, PlayerStageProgress> progressByStageId)
        {
            if (definition == null)
            {
                return true;
            }

            if (definition.UnlockedByDefault)
            {
                return true;
            }

            var rules = definition.UnlockRules;
            if (rules.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < rules.Length; i++)
            {
                if (!IsRuleSatisfied(rules[i], progressByStageId))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsRuleSatisfied(
            StageUnlockRuleDefinition rule,
            IReadOnlyDictionary<StageId, PlayerStageProgress> progressByStageId)
        {
            if (!rule.RequiredStageId.IsValid ||
                progressByStageId == null ||
                !progressByStageId.TryGetValue(rule.RequiredStageId, out var progress) ||
                progress == null)
            {
                return false;
            }

            if (!progress.HasCleared)
            {
                return false;
            }

            if (progress.BestStars < Math.Max(0, rule.MinimumStars))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(rule.RequiredRankId) &&
                !string.Equals(progress.BestRankId, rule.RequiredRankId.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(rule.RequiredChallengeId))
            {
                var completedChallenges = progress.CompletedChallengeIds ?? Array.Empty<string>();
                for (var i = 0; i < completedChallenges.Length; i++)
                {
                    if (string.Equals(completedChallenges[i], rule.RequiredChallengeId.Trim(), StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            return true;
        }
    }

    public sealed class StageCompletionReadModel
    {
        public StageCompletionReadModel(
            StageId stageId,
            string displayName,
            string resultTitle,
            string resultSummaryText,
            string resultDetailText,
            string resultContinueLabel,
            StageClearResult clearResult,
            StageClearEvaluationResult clearEvaluationResult,
            RewardGrantResult rewardGrantResult,
            PlayerStageProgress updatedProgress)
        {
            StageId = stageId;
            DisplayName = displayName ?? string.Empty;
            ResultTitle = resultTitle ?? string.Empty;
            ResultSummaryText = resultSummaryText ?? string.Empty;
            ResultDetailText = resultDetailText ?? string.Empty;
            ResultContinueLabel = resultContinueLabel ?? string.Empty;
            ClearResult = clearResult;
            ClearEvaluationResult = clearEvaluationResult;
            RewardGrantResult = rewardGrantResult;
            UpdatedProgress = updatedProgress;
        }

        public StageId StageId { get; }

        public string DisplayName { get; }

        public string ResultTitle { get; }

        public string ResultSummaryText { get; }

        public string ResultDetailText { get; }

        public string ResultContinueLabel { get; }

        public StageClearResult ClearResult { get; }

        public StageClearEvaluationResult ClearEvaluationResult { get; }

        public RewardGrantResult RewardGrantResult { get; }

        public PlayerStageProgress UpdatedProgress { get; }
    }

    public static class StageCompletionReadModelBuilder
    {
        public static StageCompletionReadModel Build(
            StageContentEntry entry,
            StageClearResult clearResult,
            StageClearEvaluationResult clearEvaluationResult,
            RewardGrantResult rewardGrantResult,
            PlayerStageProgress updatedProgress)
        {
            var presentation = entry != null
                ? StagePresentationAssembler.Resolve(entry.PresentationDefinition)
                : StagePresentationAssembler.EmptyResolvedData;
            return new StageCompletionReadModel(
                clearResult?.StageId ?? (entry != null ? entry.StageId : StageId.None),
                presentation.DisplayName,
                string.IsNullOrWhiteSpace(presentation.ResultTitle) ? "Stage Cleared" : presentation.ResultTitle,
                presentation.ResultSummaryText,
                presentation.ResultDetailText,
                string.IsNullOrWhiteSpace(presentation.ResultContinueLabel) ? "Continue" : presentation.ResultContinueLabel,
                clearResult,
                clearEvaluationResult,
                rewardGrantResult,
                updatedProgress);
        }
    }
}

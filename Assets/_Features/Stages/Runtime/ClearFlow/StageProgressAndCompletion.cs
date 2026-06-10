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
}

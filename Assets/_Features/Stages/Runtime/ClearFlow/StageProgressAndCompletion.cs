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
            string displayNameKey,
            MinimalStageCompletionResult result,
            StageNavigationRequest continueRequest,
            StageNavigationRequest retryRequest,
            StageNavigationRequest nextStageRequest)
        {
            StageId = stageId;
            DisplayNameKey = StageDisplayNameKeys.Normalize(displayNameKey);
            Result = result ?? throw new ArgumentNullException(nameof(result));
            ContinueRequest = continueRequest;
            RetryRequest = retryRequest;
            NextStageRequest = nextStageRequest;
        }

        public StageId StageId { get; }

        public string DisplayNameKey { get; }

        public MinimalStageCompletionResult Result { get; }

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
                StageDisplayNameKeys.RequireForStage(stageId, presentation.DisplayNameKey),
                result,
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
    public sealed class PlayerStageClearRecord
    {
        public static PlayerStageClearRecord CreateEmpty(StageId stageId)
        {
            return new PlayerStageClearRecord
            {
                StageId = stageId,
                HasAttempted = false,
                HasCleared = false,
                ClearCount = 0,
                ProcessedStageRunIds = Array.Empty<string>(),
            };
        }

        public StageId StageId { get; set; }

        public bool HasAttempted { get; set; }

        public bool HasCleared { get; set; }

        public int ClearCount { get; set; }

        public string[] ProcessedStageRunIds { get; set; } = Array.Empty<string>();

        public PlayerStageClearRecord Clone()
        {
            return new PlayerStageClearRecord
            {
                StageId = StageId,
                HasAttempted = HasAttempted,
                HasCleared = HasCleared,
                ClearCount = ClearCount,
                ProcessedStageRunIds = (string[])(ProcessedStageRunIds ?? Array.Empty<string>()).Clone(),
            };
        }
    }

    public sealed class StageClearProfileSnapshot
    {
        public int Version { get; set; }

        public Dictionary<StageId, PlayerStageClearRecord> ClearRecordsByStageId { get; set; } = new();

        public HashSet<string> ProcessedStageRunIds { get; set; } = new(StringComparer.Ordinal);

        public HashSet<string> ProcessedClearAttemptIds { get; set; } = new(StringComparer.Ordinal);

        public StageClearProfileSnapshot Clone()
        {
            var clearRecordsByStageId = new Dictionary<StageId, PlayerStageClearRecord>();
            foreach (var pair in ClearRecordsByStageId)
            {
                clearRecordsByStageId[pair.Key] = pair.Value?.Clone();
            }

            return new StageClearProfileSnapshot
            {
                Version = Version,
                ClearRecordsByStageId = clearRecordsByStageId,
                ProcessedStageRunIds = new HashSet<string>(ProcessedStageRunIds, StringComparer.Ordinal),
                ProcessedClearAttemptIds = new HashSet<string>(ProcessedClearAttemptIds, StringComparer.Ordinal),
            };
        }
    }

    public interface IStageClearProfileStore
    {
        StageClearProfileSnapshot Load();

        void Save(StageClearProfileSnapshot snapshot);
    }
}

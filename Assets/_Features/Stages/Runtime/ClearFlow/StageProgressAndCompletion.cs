using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public sealed class MinimalStageCompletionReadModel
    {
        public MinimalStageCompletionReadModel(
            StageId stageId,
            int finalTickIndex,
            StageNavigationRequest continueRequest,
            StageNavigationRequest retryRequest,
            StageNavigationRequest nextStageRequest)
        {
            StageId = stageId;
            FinalTickIndex = finalTickIndex;
            ContinueRequest = continueRequest;
            RetryRequest = retryRequest;
            NextStageRequest = nextStageRequest;
        }

        public StageId StageId { get; }

        public int FinalTickIndex { get; }

        public StageNavigationRequest ContinueRequest { get; }

        public StageNavigationRequest RetryRequest { get; }

        public StageNavigationRequest NextStageRequest { get; }
    }

    public static class MinimalStageCompletionReadModelBuilder
    {
        public static MinimalStageCompletionReadModel Build(
            StageContentEntry entry,
            StageClearResult clearResult,
            CampaignStageSequenceResolver sequenceResolver = null)
        {
            if (clearResult == null)
            {
                throw new ArgumentNullException(nameof(clearResult));
            }

            var stageId = clearResult.StageId.IsValid
                ? clearResult.StageId
                : entry != null ? entry.StageId : StageId.None;
            if (entry != null)
            {
                var presentation = StagePresentationAssembler.Resolve(entry.PresentationDefinition);
                StageDisplayNameKeys.RequireForStage(stageId, presentation.DisplayNameKey);
            }

            var nextStageRequest = ResolveNextStageRequest(stageId, sequenceResolver);
            if (nextStageRequest.IsValid)
            {
                nextStageRequest =
                    nextStageRequest.WithTransitionIntent(SceneTransitionIntent.StageAdvance);
            }

            var continueRequest = nextStageRequest.IsValid
                ? nextStageRequest.WithTransitionHint(StageTransitionHint.ForKind(StageTransitionKind.StageClearNext))
                : new StageNavigationRequest(
                    stageId,
                    StageNavigationKind.Continue,
                    "stage-result-continue",
                    StageTransitionHint.ForKind(StageTransitionKind.StageClearNext),
                    SceneTransitionIntent.StageAdvance);
            var retryRequest = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "stage-result-retry",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                SceneTransitionIntent.ManualRetry);

            return new MinimalStageCompletionReadModel(
                stageId,
                clearResult.FinalTickIndex,
                continueRequest,
                retryRequest,
                nextStageRequest.IsValid
                    ? nextStageRequest.WithTransitionHint(StageTransitionHint.ForKind(StageTransitionKind.StageClearNext))
                    : StageNavigationRequest.None);
        }

        private static StageNavigationRequest ResolveNextStageRequest(
            StageId stageId,
            CampaignStageSequenceResolver sequenceResolver)
        {
            if (sequenceResolver == null ||
                !sequenceResolver.Contains(stageId) ||
                sequenceResolver.IsFinal(stageId) ||
                !sequenceResolver.TryGetNext(stageId, out var nextStageId))
            {
                return StageNavigationRequest.None;
            }

            return new StageNavigationRequest(
                nextStageId,
                StageNavigationKind.NextStage,
                "campaign-auto-next",
                StageTransitionHint.ForKind(StageTransitionKind.StageClearNext),
                SceneTransitionIntent.StageAdvance);
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

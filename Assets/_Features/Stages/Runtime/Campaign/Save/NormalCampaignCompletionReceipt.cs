using System;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class NormalCampaignCompletionReceipt
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; }

        public string CompletedStageId { get; set; } = string.Empty;

        public string StageRunId { get; set; } = string.Empty;

        public int ClearSource { get; set; }

        public bool IsStructurallyValid =>
            Version == CurrentVersion &&
            HasCanonicalCompletedStageId() &&
            !string.IsNullOrWhiteSpace(StageRunId) &&
            ClearSource == (int)StageClearSource.Objective;

        public NormalCampaignCompletionReceipt Clone()
        {
            return new NormalCampaignCompletionReceipt
            {
                Version = Version,
                CompletedStageId = CompletedStageId ?? string.Empty,
                StageRunId = StageRunId ?? string.Empty,
                ClearSource = ClearSource,
            };
        }

        private bool HasCanonicalCompletedStageId()
        {
            return StageId.TryCreate(CompletedStageId, out var stageId) &&
                   string.Equals(
                       CompletedStageId,
                       stageId.Value,
                       StringComparison.Ordinal);
        }
    }

    [Serializable]
    public sealed class NormalCampaignCompletionReceiptDocument
    {
        public int Version;
        public string CompletedStageId;
        public string StageRunId;
        public int ClearSource;
    }

    public enum NormalCampaignCompletionReceiptEligibility
    {
        Eligible = 0,
        MissingCompletionResult = 1,
        NotCleared = 2,
        NonObjectiveSource = 3,
        DirectPlay = 4,
        InvalidStage = 5,
        NotCampaignStage = 6,
        NotFinalStage = 7,
        InvalidRunId = 8,
    }

    public readonly struct NormalCampaignCompletionReceiptCreationResult
    {
        public NormalCampaignCompletionReceiptCreationResult(
            NormalCampaignCompletionReceiptEligibility eligibility,
            NormalCampaignCompletionReceipt receipt = null)
        {
            Eligibility = eligibility;
            Receipt = receipt;
        }

        public NormalCampaignCompletionReceiptEligibility Eligibility { get; }

        public NormalCampaignCompletionReceipt Receipt { get; }

        public bool IsEligible =>
            Eligibility == NormalCampaignCompletionReceiptEligibility.Eligible &&
            Receipt != null;
    }

    public static class NormalCampaignCompletionReceiptPolicy
    {
        public static NormalCampaignCompletionReceiptCreationResult Evaluate(
            EditorDirectPlayContext directPlayContext,
            MinimalStageCompletionResult completionResult,
            StageId completedStageId,
            CampaignStageSequenceResolver sequenceResolver)
        {
            if (completionResult == null || sequenceResolver == null)
            {
                return Ineligible(NormalCampaignCompletionReceiptEligibility.MissingCompletionResult);
            }

            if (!completionResult.WasCleared)
            {
                return Ineligible(NormalCampaignCompletionReceiptEligibility.NotCleared);
            }

            if (completionResult.ClearSource != StageClearSource.Objective)
            {
                return Ineligible(NormalCampaignCompletionReceiptEligibility.NonObjectiveSource);
            }

            if (directPlayContext.Mode != EditorDirectPlayMode.None)
            {
                return Ineligible(NormalCampaignCompletionReceiptEligibility.DirectPlay);
            }

            if (!completedStageId.IsValid ||
                !completionResult.StageId.IsValid ||
                !completionResult.StageId.Equals(completedStageId))
            {
                return Ineligible(NormalCampaignCompletionReceiptEligibility.InvalidStage);
            }

            if (!sequenceResolver.Contains(completedStageId))
            {
                return Ineligible(NormalCampaignCompletionReceiptEligibility.NotCampaignStage);
            }

            if (!sequenceResolver.IsFinal(completedStageId))
            {
                return Ineligible(NormalCampaignCompletionReceiptEligibility.NotFinalStage);
            }

            if (!completionResult.StageRunId.IsValid ||
                string.IsNullOrWhiteSpace(completionResult.StageRunId.Value))
            {
                return Ineligible(NormalCampaignCompletionReceiptEligibility.InvalidRunId);
            }

            return new NormalCampaignCompletionReceiptCreationResult(
                NormalCampaignCompletionReceiptEligibility.Eligible,
                new NormalCampaignCompletionReceipt
                {
                    Version = NormalCampaignCompletionReceipt.CurrentVersion,
                    CompletedStageId = completedStageId.Value,
                    StageRunId = completionResult.StageRunId.Value,
                    ClearSource = (int)StageClearSource.Objective,
                });
        }

        public static bool IsEligiblePersistedReceipt(
            NormalCampaignCompletionReceipt receipt,
            CampaignStageSequenceResolver sequenceResolver)
        {
            if (receipt == null ||
                !receipt.IsStructurallyValid ||
                sequenceResolver == null ||
                !StageId.TryCreate(receipt.CompletedStageId, out var completedStageId))
            {
                return false;
            }

            return sequenceResolver.Contains(completedStageId) &&
                   sequenceResolver.IsFinal(completedStageId);
        }

        private static NormalCampaignCompletionReceiptCreationResult Ineligible(
            NormalCampaignCompletionReceiptEligibility eligibility)
        {
            return new NormalCampaignCompletionReceiptCreationResult(eligibility);
        }
    }
}

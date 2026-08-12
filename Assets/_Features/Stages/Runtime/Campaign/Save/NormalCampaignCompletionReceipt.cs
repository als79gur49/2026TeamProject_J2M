using System;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class NormalCampaignCompletionReceipt
    {
        public const int LegacyVersion = 1;
        public const int CurrentVersion = 2;

        internal const int LegacyObjectiveClearSource = 0;
        internal const int LegacyClearSourceAbsent = -1;

        public int Version { get; set; }

        public string CompletedStageId { get; set; } = string.Empty;

        public string StageRunId { get; set; } = string.Empty;

        public int ClearSource { get; set; }

        public bool IsStructurallyValid
        {
            get
            {
                if (!HasCanonicalCompletedStageId())
                {
                    return false;
                }

                return Version switch
                {
                    LegacyVersion =>
                        !string.IsNullOrWhiteSpace(StageRunId) &&
                        ClearSource == LegacyObjectiveClearSource,
                    CurrentVersion => true,
                    _ => false,
                };
            }
        }

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

    public static class NormalCampaignCompletionReceiptPolicy
    {
        internal static NormalCampaignCompletionReceipt CreateV2(StageId completedStageId)
        {
            if (!completedStageId.IsValid)
            {
                throw new ArgumentException(
                    "Normal Campaign completion receipt requires a valid completed StageId.",
                    nameof(completedStageId));
            }

            return new NormalCampaignCompletionReceipt
            {
                Version = NormalCampaignCompletionReceipt.CurrentVersion,
                CompletedStageId = completedStageId.Value,
                StageRunId = string.Empty,
                ClearSource = NormalCampaignCompletionReceipt.LegacyClearSourceAbsent,
            };
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
    }
}

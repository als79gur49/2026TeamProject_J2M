using System;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class CampaignSlotDocument
    {
        public int SlotNumber;
        public string StageId;
        public string LevelGroupId;
        public int RemainingChances;
        public bool CampaignCompleted;
        public bool HasNormalCampaignCompletionReceipt;
        public bool IntroComicCompleted;
        public bool OutroComicCompleted;
        public int TotalDeaths;
        public string LastPlayedAtUtc;
        public NormalCampaignCompletionReceiptDocument NormalCampaignCompletionReceipt;
        public NormalStagePerformanceRecordDocument[] NormalStagePerformanceRecords =
            Array.Empty<NormalStagePerformanceRecordDocument>();
        public CampaignStageClearProfileDocument StageClearProfileSnapshot = new();
    }

    [Serializable]
    public sealed class CampaignStageClearProfileDocument
    {
        public int Version;
        public PlayerStageClearRecordDocument[] Records = Array.Empty<PlayerStageClearRecordDocument>();
        public string[] ProcessedStageRunIds = Array.Empty<string>();
        public string[] ProcessedClearAttemptIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class PlayerStageClearRecordDocument
    {
        public string StageId;
        public bool HasAttempted;
        public bool HasCleared;
        public int ClearCount;
        public string[] ProcessedStageRunIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class NormalStagePerformanceRecord
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public StageId StageId { get; set; }

        public int BestCombinedPushFlipUses { get; set; }

        public bool IsStructurallyValid =>
            Version == CurrentVersion &&
            StageId.IsValid &&
            BestCombinedPushFlipUses >= 0;

        public NormalStagePerformanceRecord Clone()
        {
            return new NormalStagePerformanceRecord
            {
                Version = Version,
                StageId = StageId,
                BestCombinedPushFlipUses = BestCombinedPushFlipUses,
            };
        }
    }

    [Serializable]
    public sealed class NormalStagePerformanceRecordDocument
    {
        public int Version;
        public string StageId;
        public int BestCombinedPushFlipUses;
    }

}

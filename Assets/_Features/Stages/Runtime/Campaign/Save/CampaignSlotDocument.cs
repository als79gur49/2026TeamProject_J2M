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
}

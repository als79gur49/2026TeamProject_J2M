using System;
using System.Collections.Generic;

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

    public static class NormalStagePerformanceRecordPolicy
    {
        public static NormalStagePerformanceRecord[] Normalize(
            IEnumerable<NormalStagePerformanceRecord> records)
        {
            var bestByStage = new Dictionary<StageId, int>();
            if (records != null)
            {
                foreach (var record in records)
                {
                    if (record == null || !record.IsStructurallyValid)
                    {
                        continue;
                    }

                    if (!bestByStage.TryGetValue(record.StageId, out var currentBest) ||
                        record.BestCombinedPushFlipUses < currentBest)
                    {
                        bestByStage[record.StageId] = record.BestCombinedPushFlipUses;
                    }
                }
            }

            var normalized = new List<NormalStagePerformanceRecord>(bestByStage.Count);
            foreach (var pair in bestByStage)
            {
                normalized.Add(new NormalStagePerformanceRecord
                {
                    Version = NormalStagePerformanceRecord.CurrentVersion,
                    StageId = pair.Key,
                    BestCombinedPushFlipUses = pair.Value,
                });
            }

            normalized.Sort((left, right) =>
                StringComparer.Ordinal.Compare(left.StageId.Value, right.StageId.Value));
            return normalized.ToArray();
        }

        public static NormalStagePerformanceRecord[] UpsertBest(
            IEnumerable<NormalStagePerformanceRecord> records,
            StageId stageId,
            int combinedPushFlipUses)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException(
                    "Normal stage performance requires a valid StageId.",
                    nameof(stageId));
            }

            if (combinedPushFlipUses < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(combinedPushFlipUses));
            }

            var candidate = new List<NormalStagePerformanceRecord>();
            if (records != null)
            {
                candidate.AddRange(records);
            }

            candidate.Add(new NormalStagePerformanceRecord
            {
                Version = NormalStagePerformanceRecord.CurrentVersion,
                StageId = stageId,
                BestCombinedPushFlipUses = combinedPushFlipUses,
            });
            return Normalize(candidate);
        }
    }
}

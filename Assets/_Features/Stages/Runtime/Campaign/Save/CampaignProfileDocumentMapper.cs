using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public static class CampaignProfileDocumentMapper
    {
        public static CampaignProfileDocument ToDocument(
            IReadOnlyList<SaveSlotData> slots,
            string profileId,
            int lastPlayedSlotNumber,
            string savedAtUtc,
            string productVersion)
        {
            var slotDocuments = new List<CampaignSlotDocument>();
            if (slots != null)
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null)
                    {
                        slotDocuments.Add(ToSlotDocument(slots[i]));
                    }
                }
            }

            return new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = productVersion ?? string.Empty,
                SavedAtUtc = savedAtUtc ?? string.Empty,
                ProfileId = profileId ?? string.Empty,
                LastPlayedSlotNumber = lastPlayedSlotNumber,
                LegacyImport = new CampaignLegacyImportDocument(),
                Slots = slotDocuments.ToArray(),
            };
        }

        public static CampaignSlotDocument ToSlotDocument(SaveSlotData slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            return new CampaignSlotDocument
            {
                SlotNumber = slot.SlotNumber,
                StageId = slot.CurrentStageId.IsValid ? slot.CurrentStageId.Value : string.Empty,
                LevelGroupId = slot.CurrentLevelGroupId ?? string.Empty,
                RemainingChances = slot.RemainingChances,
                CampaignCompleted = slot.CampaignCompleted,
                HasNormalCampaignCompletionReceipt =
                    slot.HasNormalCampaignCompletionReceipt ||
                    slot.NormalCampaignCompletionReceipt != null,
                NormalCampaignCompletionReceipt = ToReceiptDocument(
                    slot.NormalCampaignCompletionReceipt),
                NormalStagePerformanceRecords = ToPerformanceRecordDocuments(
                    slot.NormalStagePerformanceRecords),
                IntroPlayed = slot.IntroPlayed,
                OutroPlayed = slot.OutroPlayed,
                TotalDeaths = slot.TotalDeaths,
                LastPlayedAtUtc = slot.LastPlayedAt ?? string.Empty,
                StageClearProfileSnapshot = ToStageClearProfileDocument(slot.StageClearProfileSnapshot),
            };
        }

        public static NormalCampaignCompletionReceiptDocument ToReceiptDocument(
            NormalCampaignCompletionReceipt receipt)
        {
            if (receipt == null)
            {
                return null;
            }

            return new NormalCampaignCompletionReceiptDocument
            {
                Version = receipt.Version,
                CompletedStageId = receipt.CompletedStageId ?? string.Empty,
                StageRunId = receipt.StageRunId ?? string.Empty,
                ClearSource = receipt.ClearSource,
            };
        }

        public static NormalCampaignCompletionReceipt ToReceipt(
            NormalCampaignCompletionReceiptDocument document)
        {
            if (document == null)
            {
                return null;
            }

            return new NormalCampaignCompletionReceipt
            {
                Version = document.Version,
                CompletedStageId = document.CompletedStageId ?? string.Empty,
                StageRunId = document.StageRunId ?? string.Empty,
                ClearSource = document.ClearSource,
            };
        }

        public static NormalStagePerformanceRecordDocument[] ToPerformanceRecordDocuments(
            IEnumerable<NormalStagePerformanceRecord> records)
        {
            var normalized = NormalStagePerformanceRecordPolicy.Normalize(records);
            var documents = new NormalStagePerformanceRecordDocument[normalized.Length];
            for (var i = 0; i < normalized.Length; i++)
            {
                documents[i] = new NormalStagePerformanceRecordDocument
                {
                    Version = normalized[i].Version,
                    StageId = normalized[i].StageId.Value,
                    BestCombinedPushFlipUses = normalized[i].BestCombinedPushFlipUses,
                };
            }

            return documents;
        }

        public static NormalStagePerformanceRecord[] ToPerformanceRecords(
            IEnumerable<NormalStagePerformanceRecordDocument> documents)
        {
            var records = new List<NormalStagePerformanceRecord>();
            if (documents != null)
            {
                foreach (var document in documents)
                {
                    if (document == null ||
                        document.Version != NormalStagePerformanceRecord.CurrentVersion ||
                        document.BestCombinedPushFlipUses < 0 ||
                        !StageId.TryCreate(document.StageId, out var stageId))
                    {
                        continue;
                    }

                    records.Add(new NormalStagePerformanceRecord
                    {
                        Version = document.Version,
                        StageId = stageId,
                        BestCombinedPushFlipUses = document.BestCombinedPushFlipUses,
                    });
                }
            }

            return NormalStagePerformanceRecordPolicy.Normalize(records);
        }

        public static CampaignStageClearProfileDocument ToStageClearProfileDocument(
            StageClearProfileSnapshot snapshot)
        {
            snapshot ??= new StageClearProfileSnapshot();

            var records = new List<PlayerStageClearRecordDocument>();
            if (snapshot.ClearRecordsByStageId != null)
            {
                foreach (var pair in snapshot.ClearRecordsByStageId)
                {
                    if (pair.Value == null)
                    {
                        continue;
                    }

                    var record = ToPlayerStageClearRecordDocument(pair.Value, pair.Key);
                    if (!string.IsNullOrEmpty(record.StageId))
                    {
                        records.Add(record);
                    }
                }
            }

            records.Sort((left, right) => string.CompareOrdinal(left.StageId, right.StageId));

            return new CampaignStageClearProfileDocument
            {
                Version = snapshot.Version,
                Records = records.ToArray(),
                ProcessedStageRunIds = ToSortedArray(snapshot.ProcessedStageRunIds),
                ProcessedClearAttemptIds = ToSortedArray(snapshot.ProcessedClearAttemptIds),
            };
        }

        public static PlayerStageClearRecordDocument ToPlayerStageClearRecordDocument(
            PlayerStageClearRecord record)
        {
            return ToPlayerStageClearRecordDocument(record, StageId.None);
        }

        private static PlayerStageClearRecordDocument ToPlayerStageClearRecordDocument(
            PlayerStageClearRecord record,
            StageId fallbackStageId)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            var stageId = record.StageId.IsValid
                ? record.StageId.Value
                : fallbackStageId.IsValid
                    ? fallbackStageId.Value
                    : string.Empty;

            return new PlayerStageClearRecordDocument
            {
                StageId = stageId,
                HasAttempted = record.HasAttempted,
                HasCleared = record.HasCleared,
                ClearCount = record.ClearCount,
                ProcessedStageRunIds = CloneArray(record.ProcessedStageRunIds),
            };
        }

        private static string[] ToSortedArray(HashSet<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[values.Count];
            values.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static string[] CloneArray(string[] values)
        {
            return (string[])(values ?? Array.Empty<string>()).Clone();
        }
    }
}

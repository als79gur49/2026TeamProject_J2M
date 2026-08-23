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
            if (string.IsNullOrWhiteSpace(profileId))
            {
                throw new ArgumentException("Campaign profile id must not be empty.", nameof(profileId));
            }

            var slotDocuments = new List<CampaignSlotDocument>();
            if (slots != null)
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (slot == null || slot.IsEmpty)
                    {
                        continue;
                    }

                    slotDocuments.Add(CampaignSlotMapper.ToDocument(slot));
                }
            }

            slotDocuments.Sort((left, right) => left.SlotNumber.CompareTo(right.SlotNumber));
            var document = new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = productVersion ?? string.Empty,
                SavedAtUtc = savedAtUtc ?? string.Empty,
                ProfileId = profileId,
                LastPlayedSlotNumber = lastPlayedSlotNumber,
                Slots = slotDocuments.ToArray(),
            };

            if (CampaignProfileDocumentValidator.Validate(document) !=
                CampaignProfileDocumentValidationResult.Valid)
            {
                throw new ArgumentException(
                    "Campaign slot data cannot produce a valid profile document.",
                    nameof(slots));
            }

            return document;
        }

        internal static SaveSlotData[] ToDomainSlots(CampaignProfileDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var validation = CampaignProfileDocumentValidator.Validate(document);
            if (validation != CampaignProfileDocumentValidationResult.Valid)
            {
                throw new InvalidOperationException(
                    $"Cannot map invalid campaign profile document: {validation}.");
            }

            var slots = CreateEmptySlots();
            var documentSlots = document.Slots ?? Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < documentSlots.Length; i++)
            {
                var slot = documentSlots[i];
                slots[slot.SlotNumber - 1] = CampaignSlotMapper.ToDomain(slot);
            }

            return slots;
        }

        internal static CampaignSlotUpdate ToFullReplacementUpdate(SaveSlotData slot)
        {
            return CampaignSlotMapper.ToFullReplacementUpdate(slot);
        }

        public static CampaignSlotDocument ToSlotDocument(SaveSlotData slot)
        {
            return CampaignSlotMapper.ToDocument(slot);
        }

        public static NormalCampaignCompletionReceiptDocument ToReceiptDocument(
            NormalCampaignCompletionReceipt receipt)
        {
            return CampaignSlotMapper.ToReceiptDocument(receipt);
        }

        public static NormalCampaignCompletionReceipt ToReceipt(
            NormalCampaignCompletionReceiptDocument document)
        {
            return CampaignSlotMapper.ToReceipt(document);
        }

        public static NormalStagePerformanceRecordDocument[] ToPerformanceRecordDocuments(
            IEnumerable<NormalStagePerformanceRecord> records)
        {
            return CampaignSlotMapper.ToPerformanceRecordDocuments(records);
        }

        public static NormalStagePerformanceRecord[] ToPerformanceRecords(
            IEnumerable<NormalStagePerformanceRecordDocument> documents)
        {
            return CampaignSlotMapper.ToPerformanceRecords(documents);
        }

        public static CampaignStageClearProfileDocument ToStageClearProfileDocument(
            StageClearProfileSnapshot snapshot)
        {
            return CampaignSlotMapper.ToStageClearProfileDocument(snapshot);
        }

        public static PlayerStageClearRecordDocument ToPlayerStageClearRecordDocument(
            PlayerStageClearRecord record)
        {
            return CampaignSlotMapper.ToPlayerStageClearRecordDocument(record);
        }

        private static SaveSlotData[] CreateEmptySlots()
        {
            var slots = new SaveSlotData[CampaignSaveSlotPolicy.SlotCount];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = SaveSlotData.CreateEmpty(i + 1);
            }

            return slots;
        }
    }

    internal static class CampaignSlotMapper
    {
        public static CampaignSlotDocument ToDocument(SaveSlotData slot)
        {
            ValidateDomainSlotForPersistence(slot);
            var document = new CampaignSlotDocument
            {
                SlotNumber = slot.SlotNumber,
                StageId = slot.CurrentStageId.Value,
                LevelGroupId = slot.CurrentLevelGroupId ?? string.Empty,
                RemainingChances = slot.RemainingChances,
                CampaignCompleted = slot.CampaignCompleted,
                HasNormalCampaignCompletionReceipt =
                    slot.HasNormalCampaignCompletionReceipt ||
                    slot.NormalCampaignCompletionReceipt != null,
                NormalCampaignCompletionReceipt = ToReceiptDocument(
                    slot.NormalCampaignCompletionReceipt),
                IntroComicCompleted = slot.IntroComicCompleted,
                OutroComicCompleted = slot.OutroComicCompleted,
                NormalStagePerformanceRecords = ToPerformanceRecordDocuments(
                    slot.NormalStagePerformanceRecords),
                TotalDeaths = slot.TotalDeaths,
                LastPlayedAtUtc = slot.LastPlayedAt ?? string.Empty,
                StageClearProfileSnapshot = ToStageClearProfileDocument(
                    slot.StageClearProfileSnapshot),
            };

            if (!CampaignSlotDocumentValidator.IsValid(document))
            {
                throw new ArgumentException(
                    "Campaign slot data cannot produce a valid persisted slot document.",
                    nameof(slot));
            }

            return document;
        }

        public static SaveSlotData ToDomain(CampaignSlotDocument slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (!CampaignSlotDocumentValidator.IsValid(slot))
            {
                throw new InvalidOperationException(
                    "Campaign profile repository returned an invalid slot document.");
            }

            var stageId = StageId.CreateOrThrow(slot.StageId);

            return new SaveSlotData
            {
                SlotNumber = slot.SlotNumber,
                CurrentStageId = stageId,
                CurrentLevelGroupId = slot.LevelGroupId ?? string.Empty,
                RemainingChances = CampaignSaveSlotPolicy.ToRuntimeRemainingChances(
                    slot.RemainingChances),
                CampaignCompleted = slot.CampaignCompleted,
                HasNormalCampaignCompletionReceipt =
                    slot.HasNormalCampaignCompletionReceipt,
                NormalCampaignCompletionReceipt = ToReceipt(
                    slot.NormalCampaignCompletionReceipt),
                IntroComicCompleted = slot.IntroComicCompleted,
                OutroComicCompleted = slot.OutroComicCompleted,
                NormalStagePerformanceRecords = ToPerformanceRecords(
                    slot.NormalStagePerformanceRecords),
                TotalDeaths = slot.TotalDeaths,
                LastPlayedAt = slot.LastPlayedAtUtc ?? string.Empty,
                StageClearProfileSnapshot = ToStageClearProfileSnapshot(
                    slot.StageClearProfileSnapshot),
            };
        }

        public static CampaignSlotUpdate ToFullReplacementUpdate(SaveSlotData slot)
        {
            var document = ToDocument(slot);
            return new CampaignSlotUpdate
            {
                StageId = document.StageId,
                LevelGroupId = document.LevelGroupId,
                RemainingChances = document.RemainingChances,
                CampaignCompleted = document.CampaignCompleted,
                ReplaceNormalCampaignCompletionReceipt = true,
                HasNormalCampaignCompletionReceipt =
                    document.HasNormalCampaignCompletionReceipt,
                NormalCampaignCompletionReceipt =
                    document.NormalCampaignCompletionReceipt,
                IntroComicCompleted = document.IntroComicCompleted,
                OutroComicCompleted = document.OutroComicCompleted,
                NormalStagePerformanceRecords =
                    document.NormalStagePerformanceRecords,
                TotalDeaths = document.TotalDeaths,
                LastPlayedAtUtc = document.LastPlayedAtUtc,
                StageClearProfileSnapshot = document.StageClearProfileSnapshot,
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

        public static StageClearProfileSnapshot ToStageClearProfileSnapshot(
            CampaignStageClearProfileDocument document)
        {
            var snapshot = new StageClearProfileSnapshot();
            if (document == null)
            {
                return snapshot;
            }

            snapshot.Version = Math.Max(0, document.Version);
            var records = document.Records ?? Array.Empty<PlayerStageClearRecordDocument>();
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                if (record == null || !StageId.TryCreate(record.StageId, out var stageId))
                {
                    continue;
                }

                snapshot.ClearRecordsByStageId[stageId] = new PlayerStageClearRecord
                {
                    StageId = stageId,
                    HasAttempted = record.HasAttempted,
                    HasCleared = record.HasCleared,
                    ClearCount = Math.Max(0, record.ClearCount),
                    ProcessedStageRunIds = CloneArray(record.ProcessedStageRunIds),
                };
            }

            snapshot.ProcessedStageRunIds = new HashSet<string>(
                document.ProcessedStageRunIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            snapshot.ProcessedClearAttemptIds = new HashSet<string>(
                document.ProcessedClearAttemptIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            return snapshot;
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

        private static void ValidateDomainSlotForPersistence(SaveSlotData slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (slot.IsEmpty)
            {
                throw new ArgumentException(
                    "An empty campaign slot has no persisted slot document.",
                    nameof(slot));
            }

            if (!CampaignSaveSlotPolicy.IsValidSlotNumber(slot.SlotNumber) ||
                !slot.CurrentStageId.IsValid ||
                slot.RemainingChances < 0 ||
                slot.TotalDeaths < 0)
            {
                throw new ArgumentException(
                    "Campaign slot data cannot produce a valid persisted slot document.",
                    nameof(slot));
            }
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

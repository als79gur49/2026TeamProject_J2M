using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    /// <summary>
    /// Explicit raw boundary retained for diagnostic evidence and legacy-shaped fixtures.
    /// Runtime business consumers use CampaignSlotEntry and CampaignSlotState directly.
    /// </summary>
    public static class CampaignSlotRawDataMapper
    {
        public static CampaignProfileDocument ToProfileDocument(
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
                    if (slot == null)
                    {
                        continue;
                    }

                    if (IsEmpty(slot))
                    {
                        continue;
                    }

                    slotDocuments.Add(ToDocument(slot));
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

        public static SaveSlotData[] FromProfileDocument(CampaignProfileDocument document)
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
                slots[slot.SlotNumber - 1] = FromDocument(slot);
            }

            return slots;
        }

        public static CampaignSlotDocument ToDocument(SaveSlotData slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            ThrowIfMalformedRawNestedState(slot);

            var document = new CampaignSlotDocument
            {
                SlotNumber = slot.SlotNumber,
                StageId = slot.CurrentStageId.Value,
                LevelGroupId = slot.CurrentLevelGroupId,
                RemainingChances = slot.RemainingChances,
                CampaignCompleted = slot.CampaignCompleted,
                HasNormalCampaignCompletionReceipt =
                    slot.HasNormalCampaignCompletionReceipt,
                NormalCampaignCompletionReceipt = ToReceiptDocument(
                    slot.NormalCampaignCompletionReceipt),
                IntroComicCompleted = slot.IntroComicCompleted,
                OutroComicCompleted = slot.OutroComicCompleted,
                NormalStagePerformanceRecords = ToPerformanceRecordDocuments(
                    slot.NormalStagePerformanceRecords),
                TotalDeaths = slot.TotalDeaths,
                LastPlayedAtUtc = slot.LastPlayedAt,
                StageClearProfileSnapshot = ToStageClearProfileDocument(
                    slot.StageClearProfileSnapshot),
            };

            if (!CampaignSlotDocumentValidator.IsValid(document))
            {
                throw new ArgumentException(
                    "Raw campaign slot data cannot produce a valid slot document.",
                    nameof(slot));
            }

            return document;
        }

        private static void ThrowIfMalformedRawNestedState(SaveSlotData slot)
        {
            var performanceRecords = slot.NormalStagePerformanceRecords;
            if (performanceRecords != null)
            {
                for (var index = 0; index < performanceRecords.Length; index++)
                {
                    if (performanceRecords[index] == null)
                    {
                        throw new ArgumentException(
                            "Raw campaign performance records must not contain null elements.",
                            nameof(slot));
                    }
                }
            }

            var snapshot = slot.StageClearProfileSnapshot;
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.ClearRecordsByStageId == null ||
                snapshot.ProcessedStageRunIds == null ||
                snapshot.ProcessedClearAttemptIds == null)
            {
                throw new ArgumentException(
                    "Raw campaign stage-clear collections must not be null.",
                    nameof(slot));
            }

            foreach (var pair in snapshot.ClearRecordsByStageId)
            {
                if (pair.Value == null ||
                    !pair.Key.Equals(pair.Value.StageId) ||
                    pair.Value.ProcessedStageRunIds == null)
                {
                    throw new ArgumentException(
                        "Raw campaign stage-clear records must be complete and keyed by their StageId.",
                        nameof(slot));
                }
            }
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
        public static SaveSlotData FromDocument(CampaignSlotDocument slot)
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
                RemainingChances = CampaignSaveSlotPolicy.RequireValidRemainingChances(
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

        public static CampaignSlotState ToState(SaveSlotData slot)
        {
            var document = ToDocument(slot);
            var parsed = CampaignSlotParser.ParseEntry(document.SlotNumber, document);
            if (!parsed.IsSuccess || parsed.Entry.IsEmpty)
            {
                throw new ArgumentException(
                    "Raw campaign slot data must describe a valid occupied slot.",
                    nameof(slot));
            }

            return parsed.Entry.State;
        }

        public static SaveSlotData FromState(CampaignSlotState state)
        {
            return FromDocument(CampaignSlotStateDocumentMapper.ToDocument(
                state ?? throw new ArgumentNullException(nameof(state))));
        }

        public static SaveSlotData ToRaw(CampaignSlotState state) => FromState(state);

        public static SaveSlotData ToRaw(CampaignSlotEntry entry) => FromEntry(entry);

        public static SaveSlotData ToRaw(CampaignSlotDocument document) =>
            FromDocument(document);

        public static SaveSlotData[] ToRawSlots(CampaignSlotDocument[] documents) =>
            FromDocuments(documents);

        public static SaveSlotData FromEntry(CampaignSlotEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return entry.IsEmpty
                ? SaveSlotData.CreateEmpty(entry.SlotNumber)
                : FromState(entry.State);
        }

        public static CampaignSlotEntry ToEntry(SaveSlotData slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            return IsEmpty(slot)
                ? CampaignSlotStateFactory.CreateEmptyEntry(slot.SlotNumber)
                : CampaignSlotEntry.Occupied(ToState(slot));
        }

        public static CampaignSlotSeedImportRequest ToSeedImportRequest(
            SaveSlotData rawSeed)
        {
            if (rawSeed == null)
            {
                throw new ArgumentNullException(nameof(rawSeed));
            }

            return new CampaignSlotSeedImportRequest(
                rawSeed.SlotNumber,
                rawSeed.CurrentStageId,
                rawSeed.CurrentLevelGroupId,
                rawSeed.RemainingChances,
                rawSeed.LastPlayedAt);
        }

        public static CampaignSlotEntry[] ToEntries(IReadOnlyList<SaveSlotData> slots)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            var entries = new CampaignSlotEntry[slots.Count];
            for (var index = 0; index < slots.Count; index++)
            {
                entries[index] = ToEntry(slots[index]);
            }

            return entries;
        }

        public static SaveSlotData[] FromDocuments(CampaignSlotDocument[] documents)
        {
            var slots = CreateEmptySlots();
            var source = documents ?? Array.Empty<CampaignSlotDocument>();
            var seenSlotNumbers = new bool[CampaignSaveSlotPolicy.SlotCount];
            for (var index = 0; index < source.Length; index++)
            {
                var slot = FromDocument(source[index]);
                var slotIndex = slot.SlotNumber - 1;
                if (seenSlotNumbers[slotIndex])
                {
                    throw new ArgumentException(
                        "Raw campaign slot documents must not contain duplicate slot numbers.",
                        nameof(documents));
                }

                seenSlotNumbers[slotIndex] = true;
                slots[slotIndex] = slot;
            }

            return slots;
        }

        internal static bool IsEmpty(SaveSlotData slot)
        {
            if (slot == null ||
                !CampaignSaveSlotPolicy.IsValidSlotNumber(slot.SlotNumber) ||
                !CampaignSaveSlotPolicy.IsValidRemainingChances(slot.RemainingChances) ||
                slot.TotalDeaths != 0)
            {
                return false;
            }

            var snapshot = slot.StageClearProfileSnapshot;
            var snapshotIsEmpty = snapshot == null ||
                                  (snapshot.Version == 0 &&
                                   snapshot.ClearRecordsByStageId != null &&
                                   snapshot.ClearRecordsByStageId.Count == 0 &&
                                   snapshot.ProcessedStageRunIds != null &&
                                   snapshot.ProcessedStageRunIds.Count == 0 &&
                                   snapshot.ProcessedClearAttemptIds != null &&
                                   snapshot.ProcessedClearAttemptIds.Count == 0);
            return !slot.CurrentStageId.IsValid &&
                   !slot.CampaignCompleted &&
                   !slot.HasNormalCampaignCompletionReceipt &&
                   slot.NormalCampaignCompletionReceipt == null &&
                   !slot.IntroComicCompleted &&
                   !slot.OutroComicCompleted &&
                   string.IsNullOrWhiteSpace(slot.LastPlayedAt) &&
                   (slot.NormalStagePerformanceRecords == null ||
                    slot.NormalStagePerformanceRecords.Length == 0) &&
                   snapshotIsEmpty;
        }

        private static NormalCampaignCompletionReceiptDocument ToReceiptDocument(
            NormalCampaignCompletionReceipt receipt)
        {
            if (receipt == null)
            {
                return null;
            }

            return new NormalCampaignCompletionReceiptDocument
            {
                Version = receipt.Version,
                CompletedStageId = receipt.CompletedStageId,
                StageRunId = receipt.StageRunId,
                ClearSource = receipt.ClearSource,
            };
        }

        private static NormalCampaignCompletionReceipt ToReceipt(
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

        private static NormalStagePerformanceRecordDocument[] ToPerformanceRecordDocuments(
            IEnumerable<NormalStagePerformanceRecord> records)
        {
            var documents = new List<NormalStagePerformanceRecordDocument>();
            foreach (var record in records ?? Array.Empty<NormalStagePerformanceRecord>())
            {
                documents.Add(new NormalStagePerformanceRecordDocument
                {
                    Version = record.Version,
                    StageId = record.StageId.Value,
                    BestCombinedPushFlipUses = record.BestCombinedPushFlipUses,
                });
            }

            return documents.ToArray();
        }

        private static NormalStagePerformanceRecord[] ToPerformanceRecords(
            IEnumerable<NormalStagePerformanceRecordDocument> documents)
        {
            var records = new List<NormalStagePerformanceRecord>();
            foreach (var document in
                     documents ?? Array.Empty<NormalStagePerformanceRecordDocument>())
            {
                records.Add(new NormalStagePerformanceRecord
                {
                    Version = document.Version,
                    StageId = StageId.CreateOrThrow(document.StageId),
                    BestCombinedPushFlipUses = document.BestCombinedPushFlipUses,
                });
            }

            return records.ToArray();
        }

        private static CampaignStageClearProfileDocument ToStageClearProfileDocument(
            StageClearProfileSnapshot snapshot)
        {
            snapshot ??= new StageClearProfileSnapshot();
            var records = new List<PlayerStageClearRecordDocument>();
            if (snapshot.ClearRecordsByStageId != null)
            {
                foreach (var pair in snapshot.ClearRecordsByStageId)
                {
                    records.Add(ToPlayerStageClearRecordDocument(pair.Value));
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

        private static StageClearProfileSnapshot ToStageClearProfileSnapshot(
            CampaignStageClearProfileDocument document)
        {
            var snapshot = new StageClearProfileSnapshot();
            if (document == null)
            {
                return snapshot;
            }

            snapshot.Version = document.Version;
            var records = document.Records ?? Array.Empty<PlayerStageClearRecordDocument>();
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                var stageId = StageId.CreateOrThrow(record.StageId);

                snapshot.ClearRecordsByStageId[stageId] = new PlayerStageClearRecord
                {
                    StageId = stageId,
                    HasAttempted = record.HasAttempted,
                    HasCleared = record.HasCleared,
                    ClearCount = record.ClearCount,
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

        private static PlayerStageClearRecordDocument ToPlayerStageClearRecordDocument(
            PlayerStageClearRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            return new PlayerStageClearRecordDocument
            {
                StageId = record.StageId.Value,
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

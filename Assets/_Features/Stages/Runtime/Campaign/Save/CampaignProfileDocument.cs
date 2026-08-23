using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class CampaignProfileDocument
    {
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion;
        public string ProductVersion;
        public string SavedAtUtc;
        public string ProfileId;
        public int LastPlayedSlotNumber;
        public CampaignSlotDocument[] Slots = Array.Empty<CampaignSlotDocument>();
    }

    internal enum CampaignProfileDocumentValidationResult
    {
        Valid,
        UnsupportedVersion,
        InvalidDocument,
    }

    internal static class CampaignSlotDocumentValidator
    {
        public static bool IsValid(CampaignSlotDocument slot)
        {
            return slot != null &&
                   CampaignSaveSlotPolicy.IsValidSlotNumber(slot.SlotNumber) &&
                   slot.RemainingChances >= 0 &&
                   slot.TotalDeaths >= 0 &&
                   IsCanonicalStageId(slot.StageId) &&
                   ValidatePerformanceRecords(slot.NormalStagePerformanceRecords) &&
                   ValidateStageClearProfile(slot.StageClearProfileSnapshot);
        }

        private static bool ValidatePerformanceRecords(
            NormalStagePerformanceRecordDocument[] records)
        {
            if (records == null)
            {
                return true;
            }

            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < records.Length; index++)
            {
                var record = records[index];
                if (record == null ||
                    record.Version != NormalStagePerformanceRecord.CurrentVersion ||
                    record.BestCombinedPushFlipUses < 0 ||
                    !IsCanonicalStageId(record.StageId) ||
                    !stageIds.Add(record.StageId))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateStageClearProfile(
            CampaignStageClearProfileDocument profile)
        {
            if (profile == null)
            {
                return true;
            }

            if (profile.Version < 0 ||
                !ValidateProcessedIds(profile.ProcessedStageRunIds) ||
                !ValidateProcessedIds(profile.ProcessedClearAttemptIds))
            {
                return false;
            }

            var records = profile.Records;
            if (records == null)
            {
                return true;
            }

            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < records.Length; index++)
            {
                var record = records[index];
                if (record == null ||
                    record.ClearCount < 0 ||
                    !IsCanonicalStageId(record.StageId) ||
                    !stageIds.Add(record.StageId) ||
                    !ValidateProcessedIds(record.ProcessedStageRunIds))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateProcessedIds(string[] values)
        {
            if (values == null)
            {
                return true;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCanonicalStageId(string rawStageId)
        {
            return StageId.TryCreate(rawStageId, out var stageId) &&
                   string.Equals(rawStageId, stageId.Value, StringComparison.Ordinal);
        }
    }

    internal static class CampaignProfileDocumentValidator
    {
        public static CampaignProfileDocumentValidationResult Validate(
            CampaignProfileDocument document)
        {
            if (document == null)
            {
                return CampaignProfileDocumentValidationResult.InvalidDocument;
            }

            if (document.SchemaVersion != CampaignProfileDocument.CurrentSchemaVersion)
            {
                return CampaignProfileDocumentValidationResult.UnsupportedVersion;
            }

            if (string.IsNullOrWhiteSpace(document.ProfileId))
            {
                return CampaignProfileDocumentValidationResult.InvalidDocument;
            }

            var slots = document.Slots ?? Array.Empty<CampaignSlotDocument>();
            if (slots.Length > CampaignSaveSlotPolicy.SlotCount)
            {
                return CampaignProfileDocumentValidationResult.InvalidDocument;
            }

            var slotNumbers = new HashSet<int>();
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                if (!CampaignSlotDocumentValidator.IsValid(slot) ||
                    !slotNumbers.Add(slot.SlotNumber))
                {
                    return CampaignProfileDocumentValidationResult.InvalidDocument;
                }
            }

            if (document.LastPlayedSlotNumber != 0 &&
                !slotNumbers.Contains(document.LastPlayedSlotNumber))
            {
                return CampaignProfileDocumentValidationResult.InvalidDocument;
            }

            return CampaignProfileDocumentValidationResult.Valid;
        }
    }
}

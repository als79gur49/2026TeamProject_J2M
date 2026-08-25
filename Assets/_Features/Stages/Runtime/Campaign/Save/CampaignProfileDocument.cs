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

    internal static class CampaignProfileDocumentMaterializer
    {
        public static void MaterializeValidated(CampaignProfileDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            document.ProductVersion ??= string.Empty;
            document.SavedAtUtc ??= string.Empty;
            document.Slots ??= Array.Empty<CampaignSlotDocument>();
            for (var slotIndex = 0; slotIndex < document.Slots.Length; slotIndex++)
            {
                var slot = document.Slots[slotIndex];
                slot.LevelGroupId ??= string.Empty;
                slot.LastPlayedAtUtc ??= string.Empty;
                if (!slot.HasNormalCampaignCompletionReceipt ||
                    CampaignSlotDocumentValidator.IsExactDefaultReceiptResidue(
                        slot.NormalCampaignCompletionReceipt))
                {
                    slot.NormalCampaignCompletionReceipt = null;
                }

                slot.NormalStagePerformanceRecords ??=
                    Array.Empty<NormalStagePerformanceRecordDocument>();
                slot.StageClearProfileSnapshot ??= new CampaignStageClearProfileDocument();
                var profile = slot.StageClearProfileSnapshot;
                profile.Records ??= Array.Empty<PlayerStageClearRecordDocument>();
                profile.ProcessedStageRunIds ??= Array.Empty<string>();
                profile.ProcessedClearAttemptIds ??= Array.Empty<string>();
                for (var recordIndex = 0; recordIndex < profile.Records.Length; recordIndex++)
                {
                    profile.Records[recordIndex].ProcessedStageRunIds ??=
                        Array.Empty<string>();
                }
            }
        }
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
                   CampaignSaveSlotPolicy.IsValidRemainingChances(
                       slot.RemainingChances) &&
                   slot.TotalDeaths >= 0 &&
                   IsCanonicalStageId(slot.StageId) &&
                   ValidateCompletionReceipt(
                       slot.HasNormalCampaignCompletionReceipt,
                       slot.NormalCampaignCompletionReceipt) &&
                   ValidatePerformanceRecords(slot.NormalStagePerformanceRecords) &&
                   ValidateStageClearProfile(slot.StageClearProfileSnapshot);
        }

        private static bool ValidateCompletionReceipt(
            bool hasReceipt,
            NormalCampaignCompletionReceiptDocument receipt)
        {
            if (receipt == null)
            {
                return true;
            }

            // JsonUtility serializes a null nested object as an empty object and
            // materializes that shape with exact CLR defaults. It represents either
            // receipt absence or PresentWithoutPayload, depending on the presence flag.
            if (IsExactDefaultReceiptResidue(receipt))
            {
                return true;
            }

            if (!hasReceipt)
            {
                return false;
            }

            if (!IsCanonicalStageId(receipt.CompletedStageId))
            {
                return false;
            }

            return receipt.Version switch
            {
                NormalCampaignCompletionReceipt.LegacyVersion =>
                    !string.IsNullOrWhiteSpace(receipt.StageRunId) &&
                    receipt.ClearSource == NormalCampaignCompletionReceipt.LegacyObjectiveClearSource,
                NormalCampaignCompletionReceipt.CurrentVersion => true,
                _ => false,
            };
        }

        internal static bool IsExactDefaultReceiptResidue(
            NormalCampaignCompletionReceiptDocument receipt)
        {
            return receipt != null &&
                   receipt.Version == 0 &&
                   receipt.ClearSource == 0 &&
                   (receipt.CompletedStageId == null && receipt.StageRunId == null ||
                    receipt.CompletedStageId == string.Empty &&
                    receipt.StageRunId == string.Empty);
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

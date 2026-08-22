using System;
using System.IO;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum CampaignProfileMetadataProbeStatus
    {
        Missing = 0,
        Loaded = 1,
        Corrupt = 2,
        SchemaInvalid = 3,
        Unauthorized = 4,
        IoFailed = 5,
    }

    public readonly struct CampaignProfileMetadataProbeResult
    {
        public CampaignProfileMetadataProbeResult(
            CampaignProfileMetadataProbeStatus status,
            int schemaVersion,
            string savedAtUtc,
            int lastPlayedSlotNumber,
            int slotDocumentCount,
            int validSlotDocumentCount,
            string message)
        {
            Status = status;
            SchemaVersion = schemaVersion;
            SavedAtUtc = savedAtUtc ?? string.Empty;
            LastPlayedSlotNumber = lastPlayedSlotNumber;
            SlotDocumentCount = slotDocumentCount;
            ValidSlotDocumentCount = validSlotDocumentCount;
            Message = message ?? string.Empty;
        }

        public CampaignProfileMetadataProbeStatus Status { get; }

        public int SchemaVersion { get; }

        public string SavedAtUtc { get; }

        public int LastPlayedSlotNumber { get; }

        public int SlotDocumentCount { get; }

        public int ValidSlotDocumentCount { get; }

        public string Message { get; }

        public bool HasProfileMetadata => Status == CampaignProfileMetadataProbeStatus.Loaded;
    }

    public sealed class CampaignProfileMetadataProbe
    {
        public const string ProfileFileName = "profile.json";

        private readonly string _saveRootPath;

        public CampaignProfileMetadataProbe(string saveRootPath)
        {
            _saveRootPath = saveRootPath ?? string.Empty;
        }

        public CampaignProfileMetadataProbeResult Probe()
        {
            if (string.IsNullOrWhiteSpace(_saveRootPath))
            {
                return Missing("Save root is not configured.");
            }

            var profilePath = Path.Combine(_saveRootPath, ProfileFileName);
            try
            {
                if (!File.Exists(profilePath))
                {
                    return Missing("profile.json is missing.");
                }

                var rawProfile = File.ReadAllText(profilePath);
                if (!LooksLikeJsonObject(rawProfile))
                {
                    return StatusOnly(
                        CampaignProfileMetadataProbeStatus.Corrupt,
                        "profile.json is not valid JSON object metadata.");
                }

                CampaignProfileDocument document;
                try
                {
                    document = JsonUtility.FromJson<CampaignProfileDocument>(rawProfile);
                }
                catch (ArgumentException exception)
                {
                    return StatusOnly(
                        CampaignProfileMetadataProbeStatus.Corrupt,
                        exception.Message);
                }

                if (!IsSchemaValid(document))
                {
                    return StatusOnly(
                        CampaignProfileMetadataProbeStatus.SchemaInvalid,
                        "profile.json schema metadata is invalid.");
                }

                return FromDocument(document);
            }
            catch (UnauthorizedAccessException exception)
            {
                return StatusOnly(CampaignProfileMetadataProbeStatus.Unauthorized, exception.Message);
            }
            catch (IOException exception)
            {
                return StatusOnly(CampaignProfileMetadataProbeStatus.IoFailed, exception.Message);
            }
        }

        private static CampaignProfileMetadataProbeResult FromDocument(CampaignProfileDocument document)
        {
            var slots = document.Slots ?? Array.Empty<CampaignSlotDocument>();
            var validSlotDocumentCount = 0;
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && CampaignSaveSlotPolicy.IsValidSlotNumber(slots[i].SlotNumber))
                {
                    validSlotDocumentCount++;
                }
            }

            return new CampaignProfileMetadataProbeResult(
                CampaignProfileMetadataProbeStatus.Loaded,
                document.SchemaVersion,
                document.SavedAtUtc,
                document.LastPlayedSlotNumber,
                slots.Length,
                validSlotDocumentCount,
                "profile.json metadata loaded for diagnostics.");
        }

        private static bool IsSchemaValid(CampaignProfileDocument document)
        {
            return CampaignProfileDocumentValidator.Validate(document) ==
                   CampaignProfileDocumentValidationResult.Valid;
        }

        private static bool LooksLikeJsonObject(string rawProfile)
        {
            if (string.IsNullOrWhiteSpace(rawProfile))
            {
                return false;
            }

            var trimmed = rawProfile.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}';
        }

        private static CampaignProfileMetadataProbeResult Missing(string message)
        {
            return StatusOnly(CampaignProfileMetadataProbeStatus.Missing, message);
        }

        private static CampaignProfileMetadataProbeResult StatusOnly(
            CampaignProfileMetadataProbeStatus status,
            string message)
        {
            return new CampaignProfileMetadataProbeResult(
                status,
                0,
                string.Empty,
                0,
                0,
                0,
                message);
        }
    }
}

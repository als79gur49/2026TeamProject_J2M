using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum CampaignLegacyImportStatus
    {
        Missing = 0,
        Importable = 1,
        InvalidPayload = 2,
        UnsupportedSchema = 3,
        ImportDisabled = 4,
        EmptyPayload = 5,
        ParseFailed = 6,
        MappingFailed = 7,
    }

    public sealed class CampaignLegacyImportResult
    {
        public CampaignLegacyImportResult(
            CampaignLegacyImportStatus status,
            CampaignProfileDocument document,
            string importedSourceHash,
            string reason,
            bool sourceFound,
            bool importDisabled)
        {
            Status = status;
            Document = document;
            ImportedSourceHash = importedSourceHash ?? string.Empty;
            Reason = reason ?? string.Empty;
            SourceFound = sourceFound;
            ImportDisabled = importDisabled;
        }

        public CampaignLegacyImportStatus Status { get; }

        public CampaignProfileDocument Document { get; }

        public string ImportedSourceHash { get; }

        public string Reason { get; }

        public bool SourceFound { get; }

        public bool ImportDisabled { get; }

        public bool HasDocument => Document != null;
    }

    public sealed class CampaignLegacySourceReader
    {
        public const string CampaignSourceKey = SaveSlotPrefsKeys.SaveSlotsKey;
        public const string ActiveSlotKey = SaveSlotPrefsKeys.ActiveSaveSlotKey;

        public bool HasCampaignSource()
        {
            return PlayerPrefs.HasKey(CampaignSourceKey);
        }

        public bool TryReadCampaignSource(out string rawPayload)
        {
            if (!PlayerPrefs.HasKey(CampaignSourceKey))
            {
                rawPayload = string.Empty;
                return false;
            }

            rawPayload = PlayerPrefs.GetString(CampaignSourceKey, string.Empty);
            return true;
        }

        public bool TryReadActiveSlotNumber(out int slotNumber)
        {
            slotNumber = PlayerPrefs.GetInt(ActiveSlotKey, 0);
            return SaveSlotStore.IsValidSlotNumber(slotNumber);
        }
    }

    public sealed class CampaignLegacyImportMarkerStore
    {
        public const string ImportDisabledKey =
            "Game.Feature.Stages.CampaignProfile.LegacyImportDisabled";

        public const string ImportedSourceHashKey =
            "Game.Feature.Stages.CampaignProfile.LegacyImportedSourceHash";

        public const string ResetTombstoneUtcKey =
            "Game.Feature.Stages.CampaignProfile.LegacyResetTombstoneUtc";

        public bool IsImportDisabled()
        {
            return PlayerPrefs.GetInt(ImportDisabledKey, 0) != 0;
        }

        public void SetImportDisabled(bool disabled)
        {
            PlayerPrefs.SetInt(ImportDisabledKey, disabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public string GetImportedSourceHash()
        {
            return PlayerPrefs.GetString(ImportedSourceHashKey, string.Empty);
        }

        public void SetImportedSourceHash(string importedSourceHash)
        {
            PlayerPrefs.SetString(ImportedSourceHashKey, importedSourceHash ?? string.Empty);
            PlayerPrefs.Save();
        }

        public string GetResetTombstoneUtc()
        {
            return PlayerPrefs.GetString(ResetTombstoneUtcKey, string.Empty);
        }

        public void SetResetTombstoneUtc(string resetTombstoneUtc)
        {
            PlayerPrefs.SetString(ResetTombstoneUtcKey, resetTombstoneUtc ?? string.Empty);
            PlayerPrefs.Save();
        }

        public bool HasResetTombstone()
        {
            return !string.IsNullOrWhiteSpace(GetResetTombstoneUtc());
        }
    }

    public sealed class LegacyPlayerPrefsCampaignImporter
    {
        private readonly CampaignLegacySourceReader _sourceReader;
        private readonly CampaignLegacyImportMarkerStore _markerStore;
        private readonly Func<string> _utcNowProvider;
        private readonly string _profileId;
        private readonly string _productVersion;

        public LegacyPlayerPrefsCampaignImporter()
            : this(
                new CampaignLegacySourceReader(),
                new CampaignLegacyImportMarkerStore(),
                () => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                "legacy-playerprefs",
                Application.version)
        {
        }

        public LegacyPlayerPrefsCampaignImporter(
            CampaignLegacySourceReader sourceReader,
            CampaignLegacyImportMarkerStore markerStore,
            Func<string> utcNowProvider,
            string profileId,
            string productVersion)
        {
            _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
            _markerStore = markerStore ?? throw new ArgumentNullException(nameof(markerStore));
            _utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
            _profileId = profileId ?? string.Empty;
            _productVersion = productVersion ?? string.Empty;
        }

        public CampaignLegacyImportResult BuildImportCandidate()
        {
            var sourceFound = _sourceReader.HasCampaignSource();
            if (_markerStore.IsImportDisabled())
            {
                return CreateResult(
                    CampaignLegacyImportStatus.ImportDisabled,
                    null,
                    string.Empty,
                    "Legacy campaign import is disabled by local marker.",
                    sourceFound,
                    true);
            }

            if (_markerStore.HasResetTombstone())
            {
                return CreateResult(
                    CampaignLegacyImportStatus.ImportDisabled,
                    null,
                    string.Empty,
                    "Legacy campaign import is blocked by reset tombstone marker.",
                    sourceFound,
                    true);
            }

            if (!_sourceReader.TryReadCampaignSource(out var rawPayload))
            {
                return CreateResult(
                    CampaignLegacyImportStatus.Missing,
                    null,
                    string.Empty,
                    "Legacy campaign source key is missing.",
                    false,
                    false);
            }

            var importedSourceHash = ComputeImportedSourceHash(rawPayload);
            var inspection = StageClearSavePayloadGuard.Inspect(rawPayload);
            if (inspection.Status == StageClearSavePayloadStatus.Empty)
            {
                return CreateResult(
                    CampaignLegacyImportStatus.EmptyPayload,
                    null,
                    importedSourceHash,
                    inspection.Reason,
                    true,
                    false);
            }

            if (inspection.Status == StageClearSavePayloadStatus.LegacyRejected)
            {
                return CreateResult(
                    CampaignLegacyImportStatus.UnsupportedSchema,
                    null,
                    importedSourceHash,
                    inspection.Reason,
                    true,
                    false);
            }

            if (inspection.Status == StageClearSavePayloadStatus.InvalidRejected)
            {
                return CreateResult(
                    IsUnsupportedSchemaInspection(inspection)
                        ? CampaignLegacyImportStatus.UnsupportedSchema
                        : CampaignLegacyImportStatus.InvalidPayload,
                    null,
                    importedSourceHash,
                    inspection.Reason,
                    true,
                    false);
            }

            SaveSlotStoreDto dto;
            try
            {
                dto = JsonUtility.FromJson<SaveSlotStoreDto>(rawPayload);
            }
            catch (Exception exception)
            {
                return CreateResult(
                    CampaignLegacyImportStatus.ParseFailed,
                    null,
                    importedSourceHash,
                    exception.Message,
                    true,
                    false);
            }

            if (!SaveSlotStoreDtoValidator.IsCurrentDtoValid(dto, out var invalidReason))
            {
                return CreateResult(
                    IsUnsupportedDto(dto)
                        ? CampaignLegacyImportStatus.UnsupportedSchema
                        : CampaignLegacyImportStatus.InvalidPayload,
                    null,
                    importedSourceHash,
                    invalidReason,
                    true,
                    false);
            }

            try
            {
                var slots = SaveSlotDtoMapper.FromDto(dto);
                var lastPlayedSlotNumber = DeriveLastPlayedSlotNumber(slots);
                var document = CampaignProfileDocumentMapper.ToDocument(
                    slots,
                    _profileId,
                    lastPlayedSlotNumber,
                    _utcNowProvider(),
                    _productVersion);
                document.LegacyImport = new CampaignLegacyImportDocument
                {
                    ImportedSourceHash = importedSourceHash,
                    ImportDisabled = _markerStore.IsImportDisabled(),
                    ResetTombstoneUtc = _markerStore.GetResetTombstoneUtc(),
                };

                return CreateResult(
                    CampaignLegacyImportStatus.Importable,
                    document,
                    importedSourceHash,
                    "Legacy campaign source can be imported.",
                    true,
                    false);
            }
            catch (Exception exception)
            {
                return CreateResult(
                    CampaignLegacyImportStatus.MappingFailed,
                    null,
                    importedSourceHash,
                    exception.Message,
                    true,
                    false);
            }
        }

        public static string ComputeImportedSourceHash(string rawPayload)
        {
            var bytes = Encoding.UTF8.GetBytes(rawPayload ?? string.Empty);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private int DeriveLastPlayedSlotNumber(SaveSlotData[] slots)
        {
            if (_sourceReader.TryReadActiveSlotNumber(out var activeSlotNumber) &&
                TryGetSlot(slots, activeSlotNumber, out var activeSlot) &&
                !activeSlot.IsEmpty)
            {
                return activeSlotNumber;
            }

            if (slots != null)
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot != null && !slot.IsEmpty)
                    {
                        return slot.SlotNumber;
                    }
                }
            }

            return 0;
        }

        private static bool TryGetSlot(SaveSlotData[] slots, int slotNumber, out SaveSlotData slot)
        {
            if (slots != null && SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                var index = slotNumber - 1;
                if (index >= 0 && index < slots.Length)
                {
                    slot = slots[index];
                    return slot != null;
                }
            }

            slot = null;
            return false;
        }

        private static bool IsUnsupportedSchemaInspection(StageClearSavePayloadInspectionResult inspection)
        {
            return string.Equals(inspection.MatchedToken, "SchemaId", StringComparison.Ordinal) ||
                   string.Equals(inspection.MatchedToken, "SchemaVersion", StringComparison.Ordinal);
        }

        private static bool IsUnsupportedDto(SaveSlotStoreDto dto)
        {
            return dto != null &&
                   (!string.Equals(dto.SchemaId, SaveSlotStore.SchemaId, StringComparison.Ordinal) ||
                    dto.SchemaVersion != SaveSlotStore.SchemaVersion ||
                    dto.SaveVersion != SaveSlotStore.SaveVersion);
        }

        private static CampaignLegacyImportResult CreateResult(
            CampaignLegacyImportStatus status,
            CampaignProfileDocument document,
            string importedSourceHash,
            string reason,
            bool sourceFound,
            bool importDisabled)
        {
            return new CampaignLegacyImportResult(
                status,
                document,
                importedSourceHash,
                reason,
                sourceFound,
                importDisabled);
        }
    }
}

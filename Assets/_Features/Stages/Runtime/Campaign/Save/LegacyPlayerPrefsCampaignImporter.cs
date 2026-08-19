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

        private readonly string _campaignSourceKey;
        private readonly string _activeSlotKey;

        public CampaignLegacySourceReader()
            : this(CampaignSourceKey, ActiveSlotKey)
        {
        }

        public CampaignLegacySourceReader(string campaignSourceKey, string activeSlotKey)
        {
            _campaignSourceKey = string.IsNullOrWhiteSpace(campaignSourceKey)
                ? throw new ArgumentException("A campaign source key is required.", nameof(campaignSourceKey))
                : campaignSourceKey;
            _activeSlotKey = string.IsNullOrWhiteSpace(activeSlotKey)
                ? throw new ArgumentException("An active slot key is required.", nameof(activeSlotKey))
                : activeSlotKey;
        }

        public bool HasCampaignSource()
        {
            return PlayerPrefs.HasKey(_campaignSourceKey);
        }

        public bool TryReadCampaignSource(out string rawPayload)
        {
            if (!PlayerPrefs.HasKey(_campaignSourceKey))
            {
                rawPayload = string.Empty;
                return false;
            }

            rawPayload = PlayerPrefs.GetString(_campaignSourceKey, string.Empty);
            return true;
        }

        public bool TryReadActiveSlotNumber(out int slotNumber)
        {
            slotNumber = PlayerPrefs.GetInt(_activeSlotKey, 0);
            return SaveSlotStore.IsValidSlotNumber(slotNumber);
        }
    }

    public interface ICampaignLegacyImportMarkerStore
    {
        bool IsImportDisabled();

        string GetImportedSourceHash();

        void SetImportedSourceHash(string importedSourceHash);

        string GetResetTombstoneUtc();

        bool HasResetTombstone();

        void RecordDeletedSlotGuard(
            int slotNumber,
            string importedSourceHash,
            string deletedAtUtc,
            string reason);

        CampaignLegacyDeletedSlotGuardDocument[] ReadDeletedSlotGuards();
    }

    public interface ICampaignLegacyImportCandidateSource
    {
        CampaignLegacyImportResult BuildImportCandidate();
    }

    public sealed class CampaignLegacyImportMarkerStore : ICampaignLegacyImportMarkerStore
    {
        public const string ImportDisabledKey =
            "Game.Feature.Stages.CampaignProfile.LegacyImportDisabled";

        public const string ImportedSourceHashKey =
            "Game.Feature.Stages.CampaignProfile.LegacyImportedSourceHash";

        public const string ResetTombstoneUtcKey =
            "Game.Feature.Stages.CampaignProfile.LegacyResetTombstoneUtc";

        public const string DeletedSlotGuardsKey =
            "Game.Feature.Stages.CampaignProfile.LegacyDeletedSlotGuards";

        private readonly string _importDisabledKey;
        private readonly string _importedSourceHashKey;
        private readonly string _resetTombstoneUtcKey;
        private readonly string _deletedSlotGuardsKey;

        public CampaignLegacyImportMarkerStore()
            : this(
                ImportDisabledKey,
                ImportedSourceHashKey,
                ResetTombstoneUtcKey,
                DeletedSlotGuardsKey)
        {
        }

        public CampaignLegacyImportMarkerStore(
            string importDisabledKey,
            string importedSourceHashKey,
            string resetTombstoneUtcKey,
            string deletedSlotGuardsKey)
        {
            _importDisabledKey = RequireKey(importDisabledKey, nameof(importDisabledKey));
            _importedSourceHashKey = RequireKey(importedSourceHashKey, nameof(importedSourceHashKey));
            _resetTombstoneUtcKey = RequireKey(resetTombstoneUtcKey, nameof(resetTombstoneUtcKey));
            _deletedSlotGuardsKey = RequireKey(deletedSlotGuardsKey, nameof(deletedSlotGuardsKey));
        }

        public bool IsImportDisabled()
        {
            return PlayerPrefs.GetInt(_importDisabledKey, 0) != 0;
        }

        public void SetImportDisabled(bool disabled)
        {
            PlayerPrefs.SetInt(_importDisabledKey, disabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public string GetImportedSourceHash()
        {
            return PlayerPrefs.GetString(_importedSourceHashKey, string.Empty);
        }

        public void SetImportedSourceHash(string importedSourceHash)
        {
            PlayerPrefs.SetString(_importedSourceHashKey, importedSourceHash ?? string.Empty);
            PlayerPrefs.Save();
        }

        public string GetResetTombstoneUtc()
        {
            return PlayerPrefs.GetString(_resetTombstoneUtcKey, string.Empty);
        }

        public void SetResetTombstoneUtc(string resetTombstoneUtc)
        {
            PlayerPrefs.SetString(_resetTombstoneUtcKey, resetTombstoneUtc ?? string.Empty);
            PlayerPrefs.Save();
        }

        public bool HasResetTombstone()
        {
            return !string.IsNullOrWhiteSpace(GetResetTombstoneUtc());
        }

        public void RecordDeletedSlotGuard(
            int slotNumber,
            string importedSourceHash,
            string deletedAtUtc,
            string reason)
        {
            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return;
            }

            var normalizedHash = importedSourceHash ?? string.Empty;
            var guards = new System.Collections.Generic.List<CampaignLegacyDeletedSlotGuardDocument>(
                ReadDeletedSlotGuards());
            var replacement = new CampaignLegacyDeletedSlotGuardDocument
            {
                SlotNumber = slotNumber,
                ImportedSourceHash = normalizedHash,
                DeletedAtUtc = deletedAtUtc ?? string.Empty,
                Reason = reason ?? string.Empty,
            };

            var replaced = false;
            for (var i = 0; i < guards.Count; i++)
            {
                var guard = guards[i];
                if (guard != null &&
                    guard.SlotNumber == slotNumber &&
                    string.Equals(
                        guard.ImportedSourceHash ?? string.Empty,
                        normalizedHash,
                        StringComparison.Ordinal))
                {
                    guards[i] = replacement;
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                guards.Add(replacement);
            }

            PlayerPrefs.SetString(
                _deletedSlotGuardsKey,
                JsonUtility.ToJson(new DeletedSlotGuardMarkerDocument
                {
                    Guards = guards.ToArray(),
                }));
            PlayerPrefs.Save();
        }

        public CampaignLegacyDeletedSlotGuardDocument[] ReadDeletedSlotGuards()
        {
            var raw = PlayerPrefs.GetString(_deletedSlotGuardsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<CampaignLegacyDeletedSlotGuardDocument>();
            }

            try
            {
                var document = JsonUtility.FromJson<DeletedSlotGuardMarkerDocument>(raw);
                var source = document?.Guards ?? Array.Empty<CampaignLegacyDeletedSlotGuardDocument>();
                var guards = new System.Collections.Generic.List<CampaignLegacyDeletedSlotGuardDocument>();
                for (var i = 0; i < source.Length; i++)
                {
                    var guard = source[i];
                    if (guard == null || !SaveSlotStore.IsValidSlotNumber(guard.SlotNumber))
                    {
                        continue;
                    }

                    guards.Add(new CampaignLegacyDeletedSlotGuardDocument
                    {
                        SlotNumber = guard.SlotNumber,
                        ImportedSourceHash = guard.ImportedSourceHash ?? string.Empty,
                        DeletedAtUtc = guard.DeletedAtUtc ?? string.Empty,
                        Reason = guard.Reason ?? string.Empty,
                    });
                }

                return guards.ToArray();
            }
            catch (ArgumentException)
            {
                return Array.Empty<CampaignLegacyDeletedSlotGuardDocument>();
            }
        }

        private static string RequireKey(string key, string paramName)
        {
            return string.IsNullOrWhiteSpace(key)
                ? throw new ArgumentException("A PlayerPrefs key is required.", paramName)
                : key;
        }

        [Serializable]
        private sealed class DeletedSlotGuardMarkerDocument
        {
            public CampaignLegacyDeletedSlotGuardDocument[] Guards =
                Array.Empty<CampaignLegacyDeletedSlotGuardDocument>();
        }
    }

    public sealed class LegacyPlayerPrefsCampaignImporter : ICampaignLegacyImportCandidateSource
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

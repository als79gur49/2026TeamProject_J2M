using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Feature.Stages.Editor
{
    public sealed class CampaignProfileReadinessReportOptions
    {
        public const string DefaultOutputDirectory = "TestLogs/SaveReadiness";

        public CampaignProfileReadinessReportOptions(
            string saveRootPath,
            DateTime? generatedAtUtc = null)
        {
            SaveRootPath = saveRootPath ?? string.Empty;
            GeneratedAtUtc = generatedAtUtc ?? DateTime.UtcNow;
        }

        public string SaveRootPath { get; }

        public DateTime GeneratedAtUtc { get; }
    }

    public sealed class CampaignProfileReadinessReport
    {
        public CampaignProfileReadinessReport(
            DateTime generatedAtUtc,
            string saveRootPath,
            bool profileExistsForDiagnostics,
            CampaignProfileMetadataProbeStatus metadataLoadStatus,
            int schemaVersion,
            string savedAtUtc,
            int diagnosticLastPlayedSlotNumber,
            bool hasImportedSourceHash,
            string importedSourceHash,
            bool importDisabledMarkerMetadata,
            bool hasResetTombstoneMarkerMetadata,
            int deletedSlotGuardCount,
            int profileSlotDocumentCount,
            int validSlotDocumentCount,
            string probeMessage,
            IReadOnlyList<string> diagnosticWarnings)
        {
            GeneratedAtUtc = generatedAtUtc;
            SaveRootPath = saveRootPath ?? string.Empty;
            ProfileExistsForDiagnostics = profileExistsForDiagnostics;
            MetadataLoadStatus = metadataLoadStatus;
            SchemaVersion = schemaVersion;
            SavedAtUtc = savedAtUtc ?? string.Empty;
            DiagnosticLastPlayedSlotNumber = diagnosticLastPlayedSlotNumber;
            HasImportedSourceHash = hasImportedSourceHash;
            ImportedSourceHash = importedSourceHash ?? string.Empty;
            ImportDisabledMarkerMetadata = importDisabledMarkerMetadata;
            HasResetTombstoneMarkerMetadata = hasResetTombstoneMarkerMetadata;
            DeletedSlotGuardCount = deletedSlotGuardCount;
            ProfileSlotDocumentCount = profileSlotDocumentCount;
            ValidSlotDocumentCount = validSlotDocumentCount;
            ProbeMessage = probeMessage ?? string.Empty;
            DiagnosticWarnings = diagnosticWarnings ?? Array.Empty<string>();
        }

        public DateTime GeneratedAtUtc { get; }

        public string SaveRootPath { get; }

        public bool ProfileExistsForDiagnostics { get; }

        public CampaignProfileMetadataProbeStatus MetadataLoadStatus { get; }

        public int SchemaVersion { get; }

        public string SavedAtUtc { get; }

        public int DiagnosticLastPlayedSlotNumber { get; }

        public bool HasImportedSourceHash { get; }

        public string ImportedSourceHash { get; }

        public bool ImportDisabledMarkerMetadata { get; }

        public bool HasResetTombstoneMarkerMetadata { get; }

        public int DeletedSlotGuardCount { get; }

        public int ProfileSlotDocumentCount { get; }

        public int ValidSlotDocumentCount { get; }

        public string ProbeMessage { get; }

        public IReadOnlyList<string> DiagnosticWarnings { get; }

        public string ToMarkdown()
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Campaign Profile Readiness Report");
            builder.AppendLine();
            builder.AppendLine("Scope: Editor-only diagnostics/readiness report.");
            builder.AppendLine("profile.json is a diagnostics/readiness inventory input, not the current production save truth.");
            builder.AppendLine("Current production UX truth remains SaveSlotStore / PlayerPrefs.");
            builder.AppendLine("V2 metadata is readiness inventory and is not used to render MainMenu slots.");
            builder.AppendLine("Diagnostic LastPlayedSlotNumber is metadata only and is not used for resume, quick-continue, or default focus.");
            builder.AppendLine("Corrupt or invalid profile metadata is a diagnostics/readiness finding only; SaveSlotStore / PlayerPrefs remains current UX truth.");
            builder.AppendLine("No Steam/cloud canonical file selection is produced by this report.");
            builder.AppendLine();
            builder.AppendLine("## Inventory");
            builder.AppendLine($"GeneratedAtUtc: {GeneratedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}");
            builder.AppendLine($"SaveRootPath: {SaveRootPath}");
            builder.AppendLine($"profile.json exists/missing for diagnostics: {FormatExists(ProfileExistsForDiagnostics)}");
            builder.AppendLine($"metadata load status: {MetadataLoadStatus}");
            builder.AppendLine($"profile metadata schema version: {SchemaVersion}");
            builder.AppendLine($"profile metadata savedAtUtc: {SavedAtUtc}");
            builder.AppendLine($"diagnostic LastPlayedSlotNumber: {DiagnosticLastPlayedSlotNumber}");
            builder.AppendLine($"imported source hash diagnostic: {FormatPresence(HasImportedSourceHash)}");
            builder.AppendLine($"import/reset marker metadata: ImportDisabled={ImportDisabledMarkerMetadata}, ResetTombstone={HasResetTombstoneMarkerMetadata}");
            builder.AppendLine($"deleted-slot guard count: {DeletedSlotGuardCount}");
            builder.AppendLine($"profile slot document count: {ProfileSlotDocumentCount}");
            builder.AppendLine($"valid profile slot document count: {ValidSlotDocumentCount}");
            builder.AppendLine($"metadata probe message: {ProbeMessage}");
            builder.AppendLine();
            builder.AppendLine("## Diagnostics/Readiness Warnings");
            if (DiagnosticWarnings.Count == 0)
            {
                builder.AppendLine("None");
            }
            else
            {
                for (var i = 0; i < DiagnosticWarnings.Count; i++)
                {
                    builder.AppendLine($"- {DiagnosticWarnings[i]}");
                }
            }

            builder.AppendLine();
            return builder.ToString();
        }

        private static string FormatExists(bool exists)
        {
            return exists ? "exists for diagnostics" : "missing for diagnostics";
        }

        private static string FormatPresence(bool present)
        {
            return present ? "present" : "absent";
        }
    }
}

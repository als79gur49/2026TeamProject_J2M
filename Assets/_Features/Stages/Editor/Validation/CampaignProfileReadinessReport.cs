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
            builder.AppendLine("Report findings are diagnostics/readiness-only.");
            builder.AppendLine("Report findings do not block build or release.");
            builder.AppendLine("profile.json is the production campaign progression save truth and a diagnostics/readiness inventory input.");
            builder.AppendLine("Current production UX truth uses the profile-backed campaign save provider.");
            builder.AppendLine("Profile metadata is readiness inventory and is not used for pending launch selection.");
            builder.AppendLine("Diagnostic LastPlayedSlotNumber is metadata only and is not used for resume, quick-continue, or default focus.");
            builder.AppendLine("Corrupt or invalid profile metadata blocks campaign access at runtime; this report remains non-blocking diagnostics.");
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

    }
}

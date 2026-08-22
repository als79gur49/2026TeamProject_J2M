using System;
using System.Collections.Generic;
using System.IO;

namespace Game.Feature.Stages.Editor
{
    public sealed class CampaignProfileReadinessReportBuilder
    {
        public CampaignProfileReadinessReport Build(CampaignProfileReadinessReportOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var profilePath = Path.Combine(
                options.SaveRootPath,
                CampaignProfileMetadataProbe.ProfileFileName);
            var profileExists = File.Exists(profilePath);
            var probeResult = new CampaignProfileMetadataProbe(options.SaveRootPath).Probe();

            return new CampaignProfileReadinessReport(
                options.GeneratedAtUtc,
                options.SaveRootPath,
                profileExists,
                probeResult.Status,
                probeResult.SchemaVersion,
                probeResult.SavedAtUtc,
                probeResult.LastPlayedSlotNumber,
                probeResult.SlotDocumentCount,
                probeResult.ValidSlotDocumentCount,
                probeResult.Message,
                BuildDiagnosticWarnings(probeResult));
        }

        private static IReadOnlyList<string> BuildDiagnosticWarnings(
            CampaignProfileMetadataProbeResult probeResult)
        {
            var warnings = new List<string>();
            if (probeResult.Status != CampaignProfileMetadataProbeStatus.Loaded)
            {
                warnings.Add(
                    $"Diagnostics/readiness only: metadata load status is {probeResult.Status}; the current profile runtime contract is not changed by this report.");
            }

            if (probeResult.SlotDocumentCount != probeResult.ValidSlotDocumentCount)
            {
                warnings.Add(
                    "Diagnostics/readiness only: profile slot document count differs from valid profile slot document count.");
            }

            return warnings;
        }
    }
}

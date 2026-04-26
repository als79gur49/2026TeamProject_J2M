using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public sealed class CampaignStageSequenceValidator
    {
        public StageValidationReport Validate(
            CampaignStageSequenceDefinition definition,
            IEnumerable<StageContentEntry> catalogEntries = null,
            StageValidationTiming timing = StageValidationTiming.EditorAuthoring)
        {
            var report = new StageValidationReport();
            if (definition == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.missing",
                    "Campaign stage sequence definition is missing.",
                    timing: timing);
                return report;
            }

            ValidateEntries(definition, report, timing);
            ValidateCatalogMembership(definition, catalogEntries, report, timing);
            return report;
        }

        private static void ValidateEntries(
            CampaignStageSequenceDefinition definition,
            StageValidationReport report,
            StageValidationTiming timing)
        {
            var entries = definition.Entries;
            if (entries.Count != CampaignStageSequenceDefinition.CanonicalStageIdValues.Length)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.count",
                    $"Campaign stage sequence must contain exactly {CampaignStageSequenceDefinition.CanonicalStageIdValues.Length} canonical stages.",
                    definition,
                    timing: timing);
            }

            var seenStageIds = new HashSet<StageId>();
            var count = Math.Min(entries.Count, CampaignStageSequenceDefinition.CanonicalStageIdValues.Length);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.StageId.IsValid)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "campaign-sequence.stage-id.invalid",
                        $"Campaign stage sequence entry at index {i} has an invalid stage id.",
                        definition,
                        timing: timing);
                    continue;
                }

                if (!seenStageIds.Add(entry.StageId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "campaign-sequence.stage-id.duplicate",
                        $"Campaign stage sequence contains duplicate stage id '{entry.StageId.Value}'.",
                        definition,
                        timing: timing);
                }
            }

            for (var i = 0; i < count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                var expectedStageId = CampaignStageSequenceDefinition.CanonicalStageIdValues[i];
                var expectedDisplayName = CampaignStageSequenceDefinition.CanonicalDisplayNames[i];
                var expectedLevelGroup = CampaignStageSequenceDefinition.CanonicalLevelGroupIds[i];

                if (!string.Equals(entry.StageId.Value, expectedStageId, StringComparison.Ordinal))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "campaign-sequence.stage-id.order",
                        $"Campaign stage sequence entry {i} must be '{expectedStageId}', but was '{entry.StageId.Value}'.",
                        definition,
                        timing: timing);
                }

                if (!string.Equals(entry.DisplayName, expectedDisplayName, StringComparison.Ordinal))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "campaign-sequence.display-name",
                        $"Campaign stage '{entry.StageId.Value}' must display as '{expectedDisplayName}', but was '{entry.DisplayName}'.",
                        definition,
                        timing: timing);
                }

                if (!string.Equals(entry.LevelGroupId, expectedLevelGroup, StringComparison.Ordinal))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "campaign-sequence.level-group",
                        $"Campaign stage '{entry.StageId.Value}' must belong to level group '{expectedLevelGroup}', but was '{entry.LevelGroupId}'.",
                        definition,
                        timing: timing);
                }
            }
        }

        private static void ValidateCatalogMembership(
            CampaignStageSequenceDefinition definition,
            IEnumerable<StageContentEntry> catalogEntries,
            StageValidationReport report,
            StageValidationTiming timing)
        {
            if (catalogEntries == null)
            {
                return;
            }

            var catalogStageIds = new HashSet<StageId>();
            foreach (var entry in catalogEntries)
            {
                if (entry != null && entry.StageId.IsValid)
                {
                    catalogStageIds.Add(entry.StageId);
                }
            }

            foreach (var sequenceEntry in definition.Entries)
            {
                if (sequenceEntry == null || !sequenceEntry.StageId.IsValid)
                {
                    continue;
                }

                if (catalogStageIds.Contains(sequenceEntry.StageId))
                {
                    continue;
                }

                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.catalog-missing",
                    $"Campaign stage sequence references '{sequenceEntry.StageId.Value}', but the stage catalog does not contain that stage.",
                    definition,
                    timing: timing);
            }
        }
    }
}

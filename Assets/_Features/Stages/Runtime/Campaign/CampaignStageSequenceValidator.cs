using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public sealed class CampaignStageSequenceValidator
    {
        /// <summary>
        /// Long-lived production contract. This validation intentionally derives validity from the
        /// authored sequence asset and production catalog, never from the legacy canonical arrays.
        /// </summary>
        public StageValidationReport ValidateAuthoritativeAsset(
            CampaignStageSequenceDefinition definition,
            IEnumerable<StageContentEntry> catalogEntries,
            StageIdAliasTable aliasTable,
            StageValidationTiming timing = StageValidationTiming.EditorAuthoring)
        {
            return ValidateAuthoritativeAssetCore(
                definition,
                catalogEntries,
                aliasTable,
                timing,
                requireAliasTable: true);
        }

        private static StageValidationReport ValidateAuthoritativeAssetCore(
            CampaignStageSequenceDefinition definition,
            IEnumerable<StageContentEntry> catalogEntries,
            StageIdAliasTable aliasTable,
            StageValidationTiming timing,
            bool requireAliasTable)
        {
            var report = new StageValidationReport();
            if (definition == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.source-null",
                    "Authoritative campaign stage sequence definition is missing.",
                    timing: timing);
                return report;
            }

            var entries = definition.Entries;
            if (entries == null || entries.Count == 0)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.entries-empty",
                    "Authoritative campaign stage sequence must contain at least one entry.",
                    definition,
                    timing: timing);
            }

            var catalogEntriesByStageId = BuildCatalogLookup(catalogEntries, report, definition, timing);
            if (requireAliasTable && aliasTable == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.alias-table-null",
                    "Authoritative campaign sequence validation requires the production StageIdAliasTable.",
                    definition,
                    timing: timing);
            }

            var seenStageIds = new HashSet<StageId>();
            var seenLevelGroups = new HashSet<string>(StringComparer.Ordinal);
            var previousLevelGroupId = string.Empty;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "campaign-sequence.authoritative.entry-null",
                        $"Authoritative campaign stage sequence entry at index {i} is null.",
                        definition,
                        timing: timing);
                    continue;
                }

                ValidateStageId(
                    entry,
                    i,
                    definition,
                    aliasTable,
                    catalogEntriesByStageId,
                    seenStageIds,
                    report,
                    timing);
                ValidateLevelGroup(
                    entry,
                    i,
                    definition,
                    seenLevelGroups,
                    ref previousLevelGroupId,
                    report,
                    timing);
            }

            ValidateCatalogEligibilityCoverage(
                catalogEntriesByStageId,
                seenStageIds,
                definition,
                report,
                timing);
            return report;
        }

        private static Dictionary<StageId, List<StageContentEntry>> BuildCatalogLookup(
            IEnumerable<StageContentEntry> catalogEntries,
            StageValidationReport report,
            CampaignStageSequenceDefinition definition,
            StageValidationTiming timing)
        {
            var result = new Dictionary<StageId, List<StageContentEntry>>();
            if (catalogEntries == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.catalog-null",
                    "Authoritative campaign sequence validation requires the production StageCatalog entries.",
                    definition,
                    timing: timing);
                return result;
            }

            foreach (var catalogEntry in catalogEntries)
            {
                if (catalogEntry == null || !catalogEntry.StageId.IsValid)
                {
                    continue;
                }

                if (!result.TryGetValue(catalogEntry.StageId, out var matches))
                {
                    matches = new List<StageContentEntry>();
                    result.Add(catalogEntry.StageId, matches);
                }

                matches.Add(catalogEntry);
            }

            foreach (var pair in result)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.catalog-duplicate",
                    $"Production StageCatalog contains {pair.Value.Count} entries for StageId '{pair.Key.Value}'; exactly one is required.",
                    definition,
                    timing: timing);
            }

            return result;
        }

        private static void ValidateStageId(
            CampaignStageSequenceEntry entry,
            int index,
            CampaignStageSequenceDefinition definition,
            StageIdAliasTable aliasTable,
            IReadOnlyDictionary<StageId, List<StageContentEntry>> catalogEntriesByStageId,
            ISet<StageId> seenStageIds,
            StageValidationReport report,
            StageValidationTiming timing)
        {
            if (!entry.StageId.IsValid ||
                !StageId.TryCreate(entry.StageId.Value, out var parsedStageId) ||
                !parsedStageId.Equals(entry.StageId))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.stage-id-invalid",
                    $"Authoritative campaign sequence entry at index {index} has an empty or non-canonical StageId.",
                    definition,
                    timing: timing);
                return;
            }

            if (!seenStageIds.Add(entry.StageId))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.stage-id-duplicate",
                    $"Authoritative campaign sequence contains duplicate StageId '{entry.StageId.Value}'.",
                    definition,
                    timing: timing);
            }

            if (aliasTable != null)
            {
                var canonicalizedAlias = aliasTable.Resolve(entry.StageId.Value);
                if (canonicalizedAlias.IsValid)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "campaign-sequence.authoritative.stage-id-alias",
                        $"Authoritative campaign sequence uses deprecated alias source StageId '{entry.StageId.Value}' instead of canonical StageId '{canonicalizedAlias.Value}'.",
                        definition,
                        timing: timing);
                }
            }

            if (!catalogEntriesByStageId.TryGetValue(entry.StageId, out var matches) || matches.Count == 0)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.catalog-missing",
                    $"Authoritative campaign sequence StageId '{entry.StageId.Value}' does not resolve to a production StageCatalog entry.",
                    definition,
                    timing: timing);
                return;
            }

            if (matches.Count != 1)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.catalog-duplicate",
                    $"Authoritative campaign sequence StageId '{entry.StageId.Value}' resolves to {matches.Count} production StageCatalog entries; exactly one is required.",
                    definition,
                    timing: timing);
                return;
            }

            if (matches[0].GameplayDefinition == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.catalog-loading-identity-incomplete",
                    $"Production StageCatalog entry '{entry.StageId.Value}' has no gameplay definition required for stage loading.",
                    matches[0],
                    timing: timing);
            }
        }

        private static void ValidateLevelGroup(
            CampaignStageSequenceEntry entry,
            int index,
            CampaignStageSequenceDefinition definition,
            ISet<string> seenLevelGroups,
            ref string previousLevelGroupId,
            StageValidationReport report,
            StageValidationTiming timing)
        {
            var levelGroupId = entry.LevelGroupId;
            if (string.IsNullOrWhiteSpace(levelGroupId))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.level-group-empty",
                    $"Authoritative campaign sequence entry at index {index} must define a level group for retry/checkpoint progression.",
                    definition,
                    timing: timing);
                return;
            }

            if (!StageIdNormalizer.IsCanonical(levelGroupId))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.level-group-invalid",
                    $"Authoritative campaign sequence entry '{entry.StageId.Value}' has non-canonical level group '{levelGroupId}'.",
                    definition,
                    timing: timing);
                return;
            }

            if (string.Equals(previousLevelGroupId, levelGroupId, StringComparison.Ordinal))
            {
                return;
            }

            if (seenLevelGroups.Contains(levelGroupId))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.authoritative.level-group-noncontiguous",
                    $"Authoritative campaign sequence level group '{levelGroupId}' reappears after another group. Retry/checkpoint groups must be contiguous.",
                    definition,
                    timing: timing);
            }
            else
            {
                seenLevelGroups.Add(levelGroupId);
            }

            previousLevelGroupId = levelGroupId;
        }

        private static void ValidateCatalogEligibilityCoverage(
            IReadOnlyDictionary<StageId, List<StageContentEntry>> catalogEntriesByStageId,
            ISet<StageId> sequencedStageIds,
            CampaignStageSequenceDefinition definition,
            StageValidationReport report,
            StageValidationTiming timing)
        {
            foreach (var pair in catalogEntriesByStageId)
            {
                for (var i = 0; i < pair.Value.Count; i++)
                {
                    var catalogEntry = pair.Value[i];
                    var isSequenced = sequencedStageIds.Contains(pair.Key);
                    switch (catalogEntry.CampaignParticipation)
                    {
                        case CampaignParticipation.Campaign:
                            if (catalogEntry.CatalogOnlyReason != CatalogOnlyReason.None)
                            {
                                report.Add(
                                    StageValidationSeverity.Error,
                                    "campaign-sequence.authoritative.campaign-entry-has-catalog-only-reason",
                                    $"Campaign-eligible catalog entry '{pair.Key.Value}' must not define a CatalogOnly reason.",
                                    catalogEntry,
                                    timing: timing);
                            }

                            if (!isSequenced)
                            {
                                report.Add(
                                    StageValidationSeverity.Error,
                                    "campaign-sequence.authoritative.eligible-catalog-entry-unsequenced",
                                    $"Campaign-eligible catalog entry '{pair.Key.Value}' is missing from the authoritative campaign sequence.",
                                    catalogEntry,
                                    timing: timing);
                            }

                            break;
                        case CampaignParticipation.CatalogOnly:
                            if (catalogEntry.CatalogOnlyReason == CatalogOnlyReason.None)
                            {
                                report.Add(
                                    StageValidationSeverity.Error,
                                    "campaign-sequence.authoritative.catalog-only-reason-missing",
                                    $"CatalogOnly entry '{pair.Key.Value}' must define an explicit exclusion reason.",
                                    catalogEntry,
                                    timing: timing);
                            }

                            if (isSequenced)
                            {
                                report.Add(
                                    StageValidationSeverity.Error,
                                    "campaign-sequence.authoritative.catalog-only-entry-sequenced",
                                    $"CatalogOnly entry '{pair.Key.Value}' must not appear in the authoritative campaign sequence.",
                                    catalogEntry,
                                    timing: timing);
                            }

                            break;
                        default:
                            report.Add(
                                StageValidationSeverity.Error,
                                "campaign-sequence.authoritative.catalog-eligibility-unset",
                                $"Production catalog entry '{pair.Key.Value}' must explicitly declare Campaign or CatalogOnly participation.",
                                catalogEntry,
                                timing: timing);
                            break;
                    }
                }
            }
        }
    }
}

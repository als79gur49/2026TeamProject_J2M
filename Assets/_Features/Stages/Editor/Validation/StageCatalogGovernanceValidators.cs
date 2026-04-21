using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public sealed class StageCatalogKnownWarningValidator
    {
        public StageValidationReport Validate(
            StageCatalog catalog,
            StageValidationReport catalogReport,
            StageCatalogKnownWarningLedger ledger)
        {
            var report = new StageValidationReport();
            if (catalog == null)
            {
                report.Add(StageValidationSeverity.Error, "known-warning.catalog.null", "StageCatalog reference cannot be null.");
                return report;
            }

            if (ledger == null)
            {
                report.Add(StageValidationSeverity.Error, "known-warning.ledger.null", "StageCatalogKnownWarningLedger reference cannot be null.");
                return report;
            }

            var entryByGameplayGuid = BuildEntryByGameplayGuid(catalog);
            var expectedByKey = new Dictionary<string, StageCatalogKnownWarningEntry>(StringComparer.Ordinal);
            var matchedKeys = new HashSet<string>(StringComparer.Ordinal);
            var ledgerEntries = ledger.Entries;
            for (var i = 0; i < ledgerEntries.Count; i++)
            {
                var entry = ledgerEntries[i];
                ValidateLedgerMetadata(entry, report, i);
                var key = BuildKnownWarningKey(entry.IssueCode, entry.AssetGuid);
                if (!expectedByKey.TryAdd(key, entry))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "known-warning.ledger.duplicate",
                        $"Known warning ledger contains duplicate row for '{entry.IssueCode}' and asset guid '{entry.AssetGuid}'.",
                        ledger,
                        AssetDatabase.GetAssetPath(ledger));
                }
            }

            var actualWarnings = (catalogReport?.Issues ?? Array.Empty<StageValidationIssue>())
                .Where(issue => issue.Severity == StageValidationSeverity.Warning)
                .ToArray();
            for (var i = 0; i < actualWarnings.Length; i++)
            {
                var warning = actualWarnings[i];
                var assetGuid = string.IsNullOrWhiteSpace(warning.AssetPath)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(warning.AssetPath);
                var key = BuildKnownWarningKey(warning.Code, assetGuid);
                if (!expectedByKey.TryGetValue(key, out var expected))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "known-warning.unexpected",
                        $"Unexpected catalog warning '{warning.Code}' for asset '{warning.AssetPath}'.",
                        assetPath: warning.AssetPath,
                        timing: warning.Timing);
                    continue;
                }

                matchedKeys.Add(key);

                if (!string.Equals(expected.ExpectedAssetPath, warning.AssetPath, StringComparison.Ordinal))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "known-warning.asset-path-drift",
                        $"Known warning '{warning.Code}' expected asset path '{expected.ExpectedAssetPath}' but found '{warning.AssetPath}'.",
                        assetPath: warning.AssetPath,
                        timing: warning.Timing);
                }

                var loadedAsset = string.IsNullOrWhiteSpace(warning.AssetPath)
                    ? null
                    : AssetDatabase.LoadMainAssetAtPath(warning.AssetPath);
                var actualAssetName = loadedAsset != null ? loadedAsset.name : string.Empty;
                if (!string.Equals(expected.ExpectedAssetName, actualAssetName, StringComparison.Ordinal))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "known-warning.asset-name-drift",
                        $"Known warning '{warning.Code}' expected asset name '{expected.ExpectedAssetName}' but found '{actualAssetName}'.",
                        loadedAsset,
                        warning.AssetPath,
                        warning.Timing);
                }

                if (!entryByGameplayGuid.TryGetValue(assetGuid, out var ownerEntry))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "known-warning.stage-id-unresolved",
                        $"Known warning '{warning.Code}' could not resolve owning StageContentEntry for asset '{warning.AssetPath}'.",
                        loadedAsset,
                        warning.AssetPath,
                        warning.Timing);
                    continue;
                }

                if (!string.Equals(expected.ExpectedStageId, ownerEntry.StageId.Value, StringComparison.Ordinal))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "known-warning.stage-id-drift",
                        $"Known warning '{warning.Code}' expected StageId '{expected.ExpectedStageId}' but found '{ownerEntry.StageId.Value}'.",
                        ownerEntry,
                        AssetDatabase.GetAssetPath(ownerEntry),
                        warning.Timing);
                }
            }

            foreach (var pair in expectedByKey)
            {
                if (matchedKeys.Contains(pair.Key))
                {
                    continue;
                }

                report.Add(
                    StageValidationSeverity.Error,
                    "known-warning.missing",
                    $"Known warning ledger row for '{pair.Value.IssueCode}' and asset guid '{pair.Value.AssetGuid}' no longer matches any current catalog warning.",
                    ledger,
                    AssetDatabase.GetAssetPath(ledger));
            }

            return report;
        }

        private static Dictionary<string, StageContentEntry> BuildEntryByGameplayGuid(StageCatalog catalog)
        {
            var result = new Dictionary<string, StageContentEntry>(StringComparer.Ordinal);
            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                var gameplayDefinition = entries[i] != null ? entries[i].GameplayDefinition : null;
                var assetPath = gameplayDefinition == null ? string.Empty : AssetDatabase.GetAssetPath(gameplayDefinition);
                var guid = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    result[guid] = entries[i];
                }
            }

            return result;
        }

        private static void ValidateLedgerMetadata(
            StageCatalogKnownWarningEntry entry,
            StageValidationReport report,
            int index)
        {
            if (string.IsNullOrWhiteSpace(entry.IssueCode) ||
                string.IsNullOrWhiteSpace(entry.AssetGuid) ||
                string.IsNullOrWhiteSpace(entry.ExpectedAssetPath) ||
                string.IsNullOrWhiteSpace(entry.ExpectedAssetName) ||
                string.IsNullOrWhiteSpace(entry.ExpectedStageId) ||
                string.IsNullOrWhiteSpace(entry.Owner) ||
                string.IsNullOrWhiteSpace(entry.Reason) ||
                string.IsNullOrWhiteSpace(entry.RemovalGate))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "known-warning.ledger.metadata-missing",
                    $"Known warning ledger row at index {index} must populate issue, asset, stage, owner, reason, and removal metadata.");
            }
        }

        private static string BuildKnownWarningKey(string issueCode, string assetGuid)
        {
            return $"{issueCode ?? string.Empty}::{assetGuid ?? string.Empty}";
        }
    }

    public sealed class StageAliasGovernanceValidator
    {
        public StageValidationReport Validate(
            StageIdAliasTable aliasTable,
            StageAliasGovernanceLedger ledger)
        {
            var report = new StageValidationReport();
            if (aliasTable == null)
            {
                report.Add(StageValidationSeverity.Error, "alias-governance.alias-table.null", "StageIdAliasTable reference cannot be null.");
                return report;
            }

            if (ledger == null)
            {
                report.Add(StageValidationSeverity.Error, "alias-governance.ledger.null", "StageAliasGovernanceLedger reference cannot be null.");
                return report;
            }

            var aliasEntries = aliasTable.Entries;
            var ledgerEntries = ledger.Entries;
            var expectedByKey = new Dictionary<string, StageAliasGovernanceEntry>(StringComparer.Ordinal);
            for (var i = 0; i < ledgerEntries.Count; i++)
            {
                var entry = ledgerEntries[i];
                ValidateAliasGovernanceMetadata(entry, report, i);
                var key = BuildAliasKey(entry.DeprecatedStageId, entry.CurrentStageId);
                if (!expectedByKey.TryAdd(key, entry))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "alias-governance.ledger.duplicate",
                        $"Alias governance ledger contains duplicate row for alias '{entry.DeprecatedStageId}' -> '{entry.CurrentStageId.Value}'.",
                        ledger,
                        AssetDatabase.GetAssetPath(ledger));
                }
            }

            var matchedKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < aliasEntries.Count; i++)
            {
                var alias = aliasEntries[i];
                var key = BuildAliasKey(alias.DeprecatedStageId, alias.CurrentStageId);
                if (!expectedByKey.ContainsKey(key))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "alias-governance.unexpected-alias",
                        $"Alias table contains '{alias.DeprecatedStageId}' -> '{alias.CurrentStageId.Value}' without matching governance ledger metadata.",
                        aliasTable,
                        AssetDatabase.GetAssetPath(aliasTable));
                    continue;
                }

                matchedKeys.Add(key);
            }

            foreach (var pair in expectedByKey)
            {
                if (matchedKeys.Contains(pair.Key))
                {
                    continue;
                }

                report.Add(
                    StageValidationSeverity.Error,
                    "alias-governance.missing-alias",
                    $"Alias governance ledger row '{pair.Value.DeprecatedStageId}' -> '{pair.Value.CurrentStageId.Value}' no longer exists in the alias table.",
                    ledger,
                    AssetDatabase.GetAssetPath(ledger));
            }

            return report;
        }

        private static void ValidateAliasGovernanceMetadata(
            StageAliasGovernanceEntry entry,
            StageValidationReport report,
            int index)
        {
            if (string.IsNullOrWhiteSpace(entry.DeprecatedStageId) ||
                !entry.CurrentStageId.IsValid ||
                string.IsNullOrWhiteSpace(entry.SourceKind) ||
                string.IsNullOrWhiteSpace(entry.SourceAssetGuid) ||
                string.IsNullOrWhiteSpace(entry.Owner) ||
                string.IsNullOrWhiteSpace(entry.Reason) ||
                string.IsNullOrWhiteSpace(entry.IntroducedBy) ||
                string.IsNullOrWhiteSpace(entry.RemovalGate))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "alias-governance.ledger.metadata-missing",
                    $"Alias governance ledger row at index {index} must populate alias, source, owner, reason, introducedBy, and removal metadata.");
            }
        }

        private static string BuildAliasKey(string deprecatedStageId, StageId currentStageId)
        {
            return $"{StageIdNormalizer.Normalize(deprecatedStageId)}::{currentStageId.Value}";
        }
    }
}

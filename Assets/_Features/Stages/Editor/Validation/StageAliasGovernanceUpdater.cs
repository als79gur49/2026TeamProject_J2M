using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class StageAliasGovernanceUpdater
    {
        public const string DefaultAliasGovernanceLedgerAssetPath =
            "Assets/_Features/Stages/Editor/Validation/StageAliasGovernanceLedger.asset";

        public static StageAliasGovernanceLedger LoadOrCreateLedger(
            string assetPath = DefaultAliasGovernanceLedgerAssetPath)
        {
            var ledger = AssetDatabase.LoadAssetAtPath<StageAliasGovernanceLedger>(assetPath);
            if (ledger != null)
            {
                return ledger;
            }

            var folderPath = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            EnsureFolder(folderPath);
            ledger = ScriptableObject.CreateInstance<StageAliasGovernanceLedger>();
            ledger.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(ledger, assetPath);
            return ledger;
        }

        public static StageAliasGovernanceEntry CreateEntry(
            StageIdAliasEntry aliasEntry,
            string sourceKind,
            string sourceAssetGuid,
            string introducedBy,
            string owner = "stage-content-refactor",
            string reason = "StageId rename compatibility bridge.",
            string removalGate = "P3-D alias prune")
        {
            return new StageAliasGovernanceEntry
            {
                DeprecatedStageId = aliasEntry.DeprecatedStageId,
                CurrentStageId = aliasEntry.CurrentStageId,
                SourceKind = sourceKind ?? string.Empty,
                SourceAssetGuid = sourceAssetGuid ?? string.Empty,
                Owner = owner ?? string.Empty,
                Reason = reason ?? string.Empty,
                IntroducedBy = introducedBy ?? string.Empty,
                RemovalGate = removalGate ?? string.Empty,
            };
        }

        public static void Apply(
            StageIdAliasTable aliasTable,
            StageAliasGovernanceLedger ledger,
            IEnumerable<StageIdAliasEntry> aliasEntries,
            IEnumerable<StageAliasGovernanceEntry> governanceEntries)
        {
            if (aliasTable == null)
            {
                throw new ArgumentNullException(nameof(aliasTable));
            }

            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            aliasTable.SetEntries(NormalizeAliasEntries(aliasEntries).ToArray());
            ledger.SetEntries(NormalizeGovernanceEntries(governanceEntries).ToArray());
            EditorUtility.SetDirty(aliasTable);
            EditorUtility.SetDirty(ledger);
        }

        private static IEnumerable<StageIdAliasEntry> NormalizeAliasEntries(IEnumerable<StageIdAliasEntry> aliasEntries)
        {
            return (aliasEntries ?? Array.Empty<StageIdAliasEntry>())
                .Where(entry => !string.IsNullOrWhiteSpace(entry.DeprecatedStageId) && entry.CurrentStageId.IsValid)
                .GroupBy(entry => $"{StageIdNormalizer.Normalize(entry.DeprecatedStageId)}::{entry.CurrentStageId.Value}", StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(entry => StageIdNormalizer.Normalize(entry.DeprecatedStageId), StringComparer.Ordinal)
                .ThenBy(entry => entry.CurrentStageId.Value, StringComparer.Ordinal);
        }

        private static IEnumerable<StageAliasGovernanceEntry> NormalizeGovernanceEntries(IEnumerable<StageAliasGovernanceEntry> governanceEntries)
        {
            return (governanceEntries ?? Array.Empty<StageAliasGovernanceEntry>())
                .Where(entry => !string.IsNullOrWhiteSpace(entry.DeprecatedStageId) && entry.CurrentStageId.IsValid)
                .GroupBy(entry => $"{StageIdNormalizer.Normalize(entry.DeprecatedStageId)}::{entry.CurrentStageId.Value}", StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(entry => StageIdNormalizer.Normalize(entry.DeprecatedStageId), StringComparer.Ordinal)
                .ThenBy(entry => entry.CurrentStageId.Value, StringComparer.Ordinal);
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrWhiteSpace(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            var folderName = Path.GetFileName(assetFolder);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

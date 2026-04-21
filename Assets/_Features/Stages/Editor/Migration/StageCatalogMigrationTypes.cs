using System;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public enum StageCatalogMigrationConfidence
    {
        High = 0,
        Medium = 1,
        Low = 2,
    }

    public enum StageCatalogMigrationDisposition
    {
        Migrate = 0,
        MigrateAndAlias = 1,
        AliasOnly = 2,
        Skip = 3,
        Block = 4,
    }

    [Serializable]
    public struct StageCatalogMigrationPlanEntry
    {
        public string SourceAssetGuid;
        public StageCatalogMigrationDisposition Disposition;
        public string CanonicalStageId;
        public string[] AliasSourceIds;
        public bool ForcePrimary;
        public string ApprovedBy;
        public string ReviewNote;
        public string DryRunHash;
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Catalog Migration Plan", fileName = "StageCatalogMigrationPlan")]
    public sealed class StageCatalogMigrationPlan : ScriptableObject
    {
        [SerializeField] private StageCatalogMigrationPlanEntry[] entries = Array.Empty<StageCatalogMigrationPlanEntry>();

        public StageCatalogMigrationPlanEntry[] Entries => entries ?? Array.Empty<StageCatalogMigrationPlanEntry>();

        public bool TryGetOverride(string sourceAssetGuid, out StageCatalogMigrationPlanEntry entry)
        {
            var candidates = Entries;
            for (var i = 0; i < candidates.Length; i++)
            {
                if (string.Equals(candidates[i].SourceAssetGuid, sourceAssetGuid, StringComparison.Ordinal))
                {
                    entry = candidates[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }

    [Serializable]
    public sealed class StageCatalogMigrationReportItem
    {
        public string sourceAssetGuid;
        public string sourceAssetPath;
        public string folderPath;
        public string[] detectedSceneRefs;
        public string[] candidateStageIds;
        public string chosenStageId;
        public string confidence;
        public string disposition;
        public string primaryReason;
        public string[] conflicts;
        public string[] aliasPlan;
        public bool overrideApplied;
        public string[] createdAssets;
        public string[] updatedAssets;
        public string[] validationIssues;
    }

    [Serializable]
    public sealed class StageCatalogMigrationReportSummary
    {
        public int highCount;
        public int mediumCount;
        public int lowCount;
        public int blockerCount;
        public int skippedCount;
        public int staleDuplicateCount;
        public int typoAliasCandidateCount;
    }

    [Serializable]
    public sealed class StageCatalogMigrationReportReview
    {
        public bool requiresHumanReview;
        public string reviewApprovedBy;
        public string reviewChecklistVersion;
    }

    [Serializable]
    public sealed class StageCatalogMigrationReport
    {
        public string runId;
        public string generatedAtUtc;
        public string toolVersion;
        public string catalogPath;
        public string providerPath;
        public string aliasTablePath;
        public string aliasGovernanceLedgerPath;
        public string dryRunHash;
        public string rollbackJournalPath;
        public StageCatalogMigrationReportItem[] items;
        public StageCatalogMigrationReportSummary summary;
        public StageCatalogMigrationReportReview review;
    }

    [Serializable]
    public sealed class StageCatalogMigrationRollbackJournal
    {
        public string[] createdAssetPaths;
        public string catalogAssetPath;
        public string providerAssetPath;
        public string aliasTableAssetPath;
        public string aliasGovernanceLedgerAssetPath;
        public string[] previousCatalogEntryGuids;
        public StageIdAliasEntry[] previousAliasEntries;
        public StageAliasGovernanceEntry[] previousAliasGovernanceEntries;
    }
}

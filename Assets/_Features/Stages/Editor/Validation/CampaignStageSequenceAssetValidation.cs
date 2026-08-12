using System;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class CampaignStageSequenceAssetLoader
    {
        public const string CanonicalAssetGuid = "bdeaa9a608b8dde4b6d0192853f857b0";

        public static string CanonicalAssetPath => StageContentPaths.CampaignStageSequenceAssetPath;

        public static CampaignStageSequenceDefinition LoadCanonical(
            StageValidationReport report,
            StageValidationTiming timing)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var guidAtCanonicalPath = AssetDatabase.AssetPathToGUID(CanonicalAssetPath);
            if (!string.Equals(guidAtCanonicalPath, CanonicalAssetGuid, StringComparison.Ordinal))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.source.guid-mismatch",
                    $"Canonical campaign sequence path '{CanonicalAssetPath}' must resolve to GUID '{CanonicalAssetGuid}', but resolved to '{guidAtCanonicalPath}'.",
                    assetPath: CanonicalAssetPath,
                    timing: timing);
            }

            var pathForCanonicalGuid = AssetDatabase.GUIDToAssetPath(CanonicalAssetGuid);
            if (!string.Equals(pathForCanonicalGuid, CanonicalAssetPath, StringComparison.Ordinal))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.source.path-mismatch",
                    $"Canonical campaign sequence GUID '{CanonicalAssetGuid}' must resolve to '{CanonicalAssetPath}', but resolved to '{pathForCanonicalGuid}'.",
                    assetPath: CanonicalAssetPath,
                    timing: timing);
            }

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(CanonicalAssetPath);
            if (mainAsset == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.source.missing",
                    $"Canonical campaign sequence asset is missing at '{CanonicalAssetPath}'.",
                    assetPath: CanonicalAssetPath,
                    timing: timing);
                return null;
            }

            if (mainAsset is not CampaignStageSequenceDefinition definition)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.source.wrong-type",
                    $"Canonical campaign sequence asset at '{CanonicalAssetPath}' must be a {nameof(CampaignStageSequenceDefinition)}, but is '{mainAsset.GetType().Name}'.",
                    mainAsset,
                    CanonicalAssetPath,
                    timing);
                return null;
            }

            var typedAsset = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(CanonicalAssetPath);
            if (typedAsset == null || typedAsset != definition)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.source.typed-load-failed",
                    $"Canonical campaign sequence asset at '{CanonicalAssetPath}' could not be loaded as {nameof(CampaignStageSequenceDefinition)}.",
                    mainAsset,
                    CanonicalAssetPath,
                    timing);
                return null;
            }

            var serializedDefinition = new SerializedObject(definition);
            var entriesProperty = serializedDefinition.FindProperty("entries");
            if (entriesProperty == null || !entriesProperty.isArray)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "campaign-sequence.source.entries-collection-missing",
                    "Canonical campaign sequence asset does not expose its serialized entries collection.",
                    definition,
                    CanonicalAssetPath,
                    timing);
            }

            return definition;
        }
    }

    internal sealed class CampaignStageSequenceProductionValidation
    {
        public CampaignStageSequenceProductionValidation(
            CampaignStageSequenceDefinition definition,
            StageValidationReport sourceReport,
            StageValidationReport authoritativeReport,
            string sourcePath,
            string sourceGuid)
        {
            Definition = definition;
            SourceReport = sourceReport ?? new StageValidationReport();
            AuthoritativeReport = authoritativeReport ?? new StageValidationReport();
            SourcePath = sourcePath ?? string.Empty;
            SourceGuid = sourceGuid ?? string.Empty;
        }

        public CampaignStageSequenceDefinition Definition { get; }

        public StageValidationReport SourceReport { get; }

        public StageValidationReport AuthoritativeReport { get; }

        public string SourcePath { get; }

        public string SourceGuid { get; }

        public bool HasErrors =>
            SourceReport.HasErrors ||
            AuthoritativeReport.HasErrors;

        public int EntryCount => Definition?.Entries.Count ?? 0;

        public static CampaignStageSequenceProductionValidation Validate(
            StageCatalog catalog,
            StageValidationTiming timing)
        {
            var sourceReport = new StageValidationReport();
            var definition = CampaignStageSequenceAssetLoader.LoadCanonical(sourceReport, timing);
            var validator = new CampaignStageSequenceValidator();
            var authoritativeReport = validator.ValidateAuthoritativeAsset(
                definition,
                catalog != null ? catalog.Entries : null,
                catalog != null ? catalog.StageIdAliasTable : null,
                timing);
            return new CampaignStageSequenceProductionValidation(
                definition,
                sourceReport,
                authoritativeReport,
                CampaignStageSequenceAssetLoader.CanonicalAssetPath,
                CampaignStageSequenceAssetLoader.CanonicalAssetGuid);
        }
    }
}

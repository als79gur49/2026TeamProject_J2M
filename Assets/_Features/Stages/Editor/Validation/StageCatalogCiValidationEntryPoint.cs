using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class StageCatalogCiValidationEntryPoint
    {
        private const string CanonicalStageCatalogAssetPath = "Assets/_Features/Stages/Content/StageCatalog.asset";
        private const string KnownWarningLedgerAssetPath =
            "Assets/_Features/Stages/Editor/Validation/StageCatalogKnownWarningLedger.asset";
        private const string AliasGovernanceLedgerAssetPath =
            StageAliasGovernanceUpdater.DefaultAliasGovernanceLedgerAssetPath;
        private const string ReportDirectory = "Temp/StageCatalogValidation";

        public static int Run()
        {
            var options = new StageCatalogValidationOptions
            {
                RequirePresentationDefinition = true,
                RequireClearEvaluationDefinition = true,
                RequireRewardDefinition = true,
                RequireProgressionDefinition = true,
                Timing = StageValidationTiming.TestOrCi,
                Phase = StageValidationPhase.Phase5_Hardening,
                GrandfatherGameplayAssetGuids = GrandfatherGameplayAssetGuidRegistry.CreateSet(),
            };

            var auditor = new StageCompatUsageAuditor();
            var auditReport = auditor.Audit(CanonicalStageCatalogAssetPath);
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CanonicalStageCatalogAssetPath);
            var validator = new StageCatalogValidator();
            var catalogReport = validator.Validate(catalog, options);
            var editorSeamReport = new StageCatalogEditorSeamValidator().Validate(catalog, options);
            var knownWarningLedger = AssetDatabase.LoadAssetAtPath<StageCatalogKnownWarningLedger>(KnownWarningLedgerAssetPath);
            var knownWarningReport = new StageCatalogKnownWarningValidator().Validate(catalog, catalogReport, knownWarningLedger);
            var aliasGovernanceLedger = AssetDatabase.LoadAssetAtPath<StageAliasGovernanceLedger>(AliasGovernanceLedgerAssetPath);
            var aliasGovernanceReport = new StageAliasGovernanceValidator().Validate(
                catalog != null ? catalog.StageIdAliasTable : null,
                aliasGovernanceLedger);
            var sceneValidator = new StageSceneBootstrapValidator();
            var sceneReport = sceneValidator.ValidateEnabledBuildScenes(options);
            var summary = new StageSceneBootstrapValidator().SummarizeEnabledBuildSceneModes();

            Directory.CreateDirectory(ReportDirectory);
            WriteAuditReport(Path.Combine(ReportDirectory, "stage-compat-audit.md"), auditReport);
            WriteReport(
                Path.Combine(ReportDirectory, "stage-catalog-validation.md"),
                catalogReport,
                editorSeamReport,
                knownWarningReport,
                aliasGovernanceReport,
                sceneReport,
                summary);

            if (catalogReport.HasErrors ||
                editorSeamReport.HasErrors ||
                knownWarningReport.HasErrors ||
                aliasGovernanceReport.HasErrors ||
                sceneReport.HasErrors)
            {
                Debug.LogError("Stage catalog CI validation failed. See Temp/StageCatalogValidation/stage-catalog-validation.md");
                return 1;
            }

            Debug.Log("Stage catalog CI validation passed.");
            return 0;
        }

        private static void WriteReport(
            string outputPath,
            StageValidationReport catalogReport,
            StageValidationReport editorSeamReport,
            StageValidationReport knownWarningReport,
            StageValidationReport aliasGovernanceReport,
            StageValidationReport sceneReport,
            StageSceneBootstrapUsageSummary summary)
        {
            using var writer = new StreamWriter(outputPath, append: false);
            writer.WriteLine("# Stage Catalog Validation");
            writer.WriteLine();
            writer.WriteLine($"GeneratedAtUtc: {DateTime.UtcNow:O}");
            writer.WriteLine();
            writer.WriteLine("AuditReport: stage-compat-audit.md");
            writer.WriteLine();
            writer.WriteLine("## Scene Mode Summary");
            writer.WriteLine($"CatalogResolvedStageId: {summary.CatalogResolvedStageIdCount}");
            writer.WriteLine($"SerializedStageContentEntry: {summary.SerializedStageContentEntryCount}");
            writer.WriteLine($"LegacyStageDefinition: {summary.LegacyStageDefinitionCount}");
            writer.WriteLine();
            WriteIssues(writer, "Catalog Issues", catalogReport);
            WriteIssues(writer, "Editor Seam Issues", editorSeamReport);
            WriteIssues(writer, "Known Warning Governance Issues", knownWarningReport);
            WriteIssues(writer, "Alias Governance Issues", aliasGovernanceReport);
            WriteIssues(writer, "Scene Issues", sceneReport);
        }

        private static void WriteAuditReport(string outputPath, StageCompatAuditReport auditReport)
        {
            using var writer = new StreamWriter(outputPath, append: false);
            writer.WriteLine("# Stage Compat Audit");
            writer.WriteLine();
            writer.WriteLine($"GeneratedAtUtc: {DateTime.UtcNow:O}");
            writer.WriteLine();
            writer.WriteLine("## Audit Snapshot");
            writer.WriteLine($"CanonicalGameplayAssetCount: {auditReport.Snapshot.CanonicalGameplayAssetGuids.Count}");
            writer.WriteLine($"CanonicalGameplayAssetsWithLegacyPresentationIds: {auditReport.CanonicalGameplayWithLegacyPresentationIds.Count}");
            writer.WriteLine($"NonCanonicalGameplayAssetsWithLegacyPresentationIds: {auditReport.NonCanonicalGameplayWithLegacyPresentationIds.Count}");
            writer.WriteLine($"GrandfatherGameplayAssetCount: {auditReport.GrandfatherGameplayAssetCount}");
            writer.WriteLine($"GrandfatherCountMatchesExpected: {auditReport.GrandfatherCountMatchesExpected}");
            writer.WriteLine($"BuildSceneResidueCount: {auditReport.BuildSceneResiduePaths.Count}");
            writer.WriteLine($"AliasCount: {auditReport.Snapshot.AliasEntries.Count}");
            writer.WriteLine();
            WriteLines(writer, "Canonical Gameplay With Legacy PresentationIds", auditReport.CanonicalGameplayWithLegacyPresentationIds);
            WriteLines(writer, "Non-Canonical Gameplay With Legacy PresentationIds", auditReport.NonCanonicalGameplayWithLegacyPresentationIds);
            WriteLines(writer, "Prunable Alias Candidates", auditReport.PrunableAliasIds);
            WriteLines(writer, "Build Scene Residues", auditReport.BuildSceneResiduePaths);
        }

        private static void WriteIssues(StreamWriter writer, string title, StageValidationReport report)
        {
            writer.WriteLine($"## {title}");
            if (report == null || report.Issues.Count == 0)
            {
                writer.WriteLine("None");
                writer.WriteLine();
                return;
            }

            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                writer.WriteLine($"- [{issue.Severity}] {issue.Code}: {issue.Message} ({issue.AssetPath})");
            }

            writer.WriteLine();
        }

        private static void WriteLines(StreamWriter writer, string title, IReadOnlyList<string> values)
        {
            writer.WriteLine($"## {title}");
            if (values == null || values.Count == 0)
            {
                writer.WriteLine("None");
                writer.WriteLine();
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                writer.WriteLine($"- {values[i]}");
            }

            writer.WriteLine();
        }
    }
}

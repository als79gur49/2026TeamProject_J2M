using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class StageCatalogCiValidationEntryPoint
    {
        private const string KnownWarningLedgerAssetPath =
            "Assets/_Features/Stages/Editor/Validation/StageCatalogKnownWarningLedger.asset";
        private const string AliasGovernanceLedgerAssetPath =
            StageAliasGovernanceUpdater.DefaultAliasGovernanceLedgerAssetPath;
        private const string ReportDirectory = "Temp/StageCatalogValidation";

        public static string ReportPath =>
            Path.Combine(GetProjectRoot(), ReportDirectory, "stage-catalog-validation.md");

        public static void RunFromCommandLine()
        {
            EditorApplication.Exit(Run());
        }

        public static int Run()
        {
            var options = new StageCatalogValidationOptions
            {
                RequirePresentationDefinition = true,
                RequireAudioDefinition = true,
                Timing = StageValidationTiming.TestOrCi,
                Phase = StageValidationPhase.Phase6_SunsetFinalization,
                AssetMetadataProvider = StageEditorAssetMetadataProvider.Instance,
            };

            var auditor = new StageContentInventoryResidueAuditor();
            var auditReport = auditor.Audit(StageContentPaths.StageCatalogAssetPath);
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            var validator = new StageCatalogValidator();
            var catalogReport = validator.Validate(catalog, options);
            var knownWarningLedger = AssetDatabase.LoadAssetAtPath<StageCatalogKnownWarningLedger>(KnownWarningLedgerAssetPath);
            var knownWarningReport = new StageCatalogKnownWarningValidator().Validate(catalog, catalogReport, knownWarningLedger);
            var aliasGovernanceLedger = AssetDatabase.LoadAssetAtPath<StageAliasGovernanceLedger>(AliasGovernanceLedgerAssetPath);
            var aliasGovernanceReport = new StageAliasGovernanceValidator().Validate(
                catalog != null ? catalog.StageIdAliasTable : null,
                aliasGovernanceLedger);
            var aliasUsageReport = new StageAliasUsageScanner().ValidateNoHits(StageAliasUsageScanner.P3HistoricalAliasIds);
            var sceneValidator = new StageSceneBootstrapValidator();
            var sceneReport = sceneValidator.ValidateEnabledBuildScenes(options);
            var campaignContentReport = new StageCampaignContentGovernanceValidator().Validate(options.Timing);
            var summary = sceneValidator.SummarizeEnabledBuildSceneModes();

            var reportDirectory = Path.Combine(GetProjectRoot(), ReportDirectory);
            Directory.CreateDirectory(reportDirectory);
            WriteAuditReport(Path.Combine(reportDirectory, "stage-content-inventory-retired-residue-audit.md"), auditReport);
            WriteReport(
                ReportPath,
                catalogReport,
                knownWarningReport,
                aliasGovernanceReport,
                aliasUsageReport,
                campaignContentReport,
                sceneReport,
                summary);

            if (catalogReport.HasErrors ||
                knownWarningReport.HasErrors ||
                aliasGovernanceReport.HasErrors ||
                aliasUsageReport.HasErrors ||
                campaignContentReport.HasErrors ||
                sceneReport.HasErrors)
            {
                LogIssues("Catalog", catalogReport);
                LogIssues("Known Warning Governance", knownWarningReport);
                LogIssues("Alias Governance", aliasGovernanceReport);
                LogIssues("Alias Usage", aliasUsageReport);
                LogIssues("Campaign Content Governance", campaignContentReport);
                LogIssues("Scene", sceneReport);
                Debug.LogError($"Stage catalog CI validation failed. See {ReportPath}");
                return 1;
            }

            Debug.Log("Stage catalog CI validation passed.");
            return 0;
        }

        private static void WriteReport(
            string outputPath,
            StageValidationReport catalogReport,
            StageValidationReport knownWarningReport,
            StageValidationReport aliasGovernanceReport,
            StageValidationReport aliasUsageReport,
            StageValidationReport campaignContentReport,
            StageValidationReport sceneReport,
            StageSceneBootstrapUsageSummary summary)
        {
            using var writer = new StreamWriter(outputPath, append: false);
            writer.WriteLine("# Stage Catalog Validation");
            writer.WriteLine();
            writer.WriteLine($"GeneratedAtUtc: {DateTime.UtcNow:O}");
            writer.WriteLine();
            writer.WriteLine("AuditReport: stage-content-inventory-retired-residue-audit.md");
            writer.WriteLine();
            WriteFullEditModeKnownFailureBaseline(writer);
            WriteAuthoringSurfaceClassification(writer);
            writer.WriteLine("## Scene Bootstrap Guard Summary");
            writer.WriteLine(
                $"LaunchContextCatalogResolvedInstallers: {summary.GuardSummary.LaunchContextCatalogResolvedInstallers}");
            writer.WriteLine(
                $"RetiredSerializedStageContentEntryResidue: {summary.GuardSummary.RetiredSerializedStageContentEntryResidue}");
            writer.WriteLine(
                $"RetiredLegacyStageDefinitionResidue: {summary.GuardSummary.RetiredLegacyStageDefinitionResidue}");
            writer.WriteLine(
                $"RemovedDefaultStageIdFallbackResidue: {summary.GuardSummary.RemovedDefaultStageIdFallbackResidue}");
            writer.WriteLine(
                $"RemovedDirectStageDefinitionLoadResidue: {summary.GuardSummary.RemovedDirectStageDefinitionLoadResidue}");
            writer.WriteLine(
                $"EditorDirectPlayMappingSupport: {StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.EditorDirectPlaySupport)}");
            writer.WriteLine();
            WriteAuthoringIssues(writer, catalogReport);
            WritePresentationCatalogIssues(writer, catalogReport);
            WriteIssues(writer, "Catalog Issues", catalogReport);
            WriteIssues(writer, "Known Warning Governance Issues", knownWarningReport);
            WriteIssues(writer, "Alias Governance Issues", aliasGovernanceReport);
            WriteIssues(writer, "Alias Usage Issues", aliasUsageReport);
            WriteIssues(writer, "Campaign Content Governance Issues", campaignContentReport);
            WriteIssues(writer, "Scene Issues", sceneReport);
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static void LogIssues(string title, StageValidationReport report)
        {
            if (report == null || report.Issues.Count == 0)
            {
                return;
            }

            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                Debug.LogError(
                    $"{title}: [{issue.Severity}] {issue.Code}: {issue.Message} ({issue.AssetPath})");
            }
        }

        private static void WriteFullEditModeKnownFailureBaseline(StreamWriter writer)
        {
            if (FullEditModeKnownFailureBaseline.TryBuildDefaultComparison(
                    out var comparison,
                    out var sourceOrMessage))
            {
                writer.Write(FullEditModeKnownFailureBaseline.BuildMarkdownReport(comparison));
                writer.WriteLine($"SourceXml: {sourceOrMessage}");
                writer.WriteLine();
                return;
            }

            writer.WriteLine("## Full EditMode Known Failure Baseline");
            writer.WriteLine($"Not evaluated: {sourceOrMessage}");
            writer.WriteLine();
        }

        private static void WriteAuthoringIssues(StreamWriter writer, StageValidationReport report)
        {
            writer.WriteLine("## Authoring Sync Issues");
            if (report == null)
            {
                writer.WriteLine("None");
                writer.WriteLine();
                return;
            }

            var wroteIssue = false;
            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                if (!issue.Code.StartsWith("authoring.", StringComparison.Ordinal) &&
                    !issue.Code.StartsWith("GameplayDrift.", StringComparison.Ordinal) &&
                    !issue.Code.StartsWith("PresentationDrift.", StringComparison.Ordinal))
                {
                    continue;
                }

                wroteIssue = true;
                writer.WriteLine($"- [{issue.Severity}] {issue.Code}: {issue.Message} ({issue.AssetPath})");
            }

            if (!wroteIssue)
            {
                writer.WriteLine("None");
            }

            writer.WriteLine();
        }

        private static void WritePresentationCatalogIssues(StreamWriter writer, StageValidationReport report)
        {
            writer.WriteLine("## Presentation Catalog Issues");
            if (report == null)
            {
                writer.WriteLine("None");
                writer.WriteLine();
                return;
            }

            var wroteIssue = false;
            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                if (!issue.Code.StartsWith("PresentationCatalog.", StringComparison.Ordinal) &&
                    !issue.Code.StartsWith("PresentationBinding.", StringComparison.Ordinal))
                {
                    continue;
                }

                wroteIssue = true;
                writer.WriteLine(
                    $"- [{issue.Severity}] {issue.Code}: StageId='{issue.StageId}' EntityId='{FormatOptional(issue.EntityId)}' StableGuid='{issue.StableGuid}' PresentationId='{issue.PresentationId}' Expected='{issue.ExpectedValue}' Actual='{issue.ActualValue}' Message='{issue.Message}' ({issue.AssetPath})");
            }

            if (!wroteIssue)
            {
                writer.WriteLine("None");
            }

            writer.WriteLine();
        }

        private static void WriteAuditReport(string outputPath, StageContentInventoryResidueAuditReport auditReport)
        {
            using var writer = new StreamWriter(outputPath, append: false);
            writer.WriteLine("# Stage Content Inventory / Retired Residue Audit");
            writer.WriteLine();
            writer.WriteLine($"GeneratedAtUtc: {DateTime.UtcNow:O}");
            writer.WriteLine();
            writer.WriteLine("## Audit Snapshot");
            writer.WriteLine($"StageContentEntryClassification: {auditReport.Snapshot.StageContentRootClassificationLabel}");
            writer.WriteLine(
                $"StageDefinitionClassification: {StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.GameplayCompanion)}");
            writer.WriteLine(
                $"StagePresentationDefinitionClassification: {StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.PresentationCompanion)}");
            writer.WriteLine(
                $"StageAudioDefinitionClassification: {StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.AudioCompanion)}");
            writer.WriteLine(
                $"RetiredCompanionClassification: {StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.RetiredCompanionGuard)}");
            writer.WriteLine(
                $"RetiredLoadResidueClassification: {StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.RetiredLoadDetector)}");
            writer.WriteLine(
                $"DirectPlayClassification: {StageAuthoringSurfaceClassificationLabels.GetLabel(StageAuthoringSurfaceKind.EditorDirectPlaySupport)}");
            writer.WriteLine($"CanonicalGameplayCompanionCount: {auditReport.Snapshot.CanonicalGameplayCompanionAssetGuids.Count}");
            writer.WriteLine($"DuplicateLegacyGameplayCompanionAssetCount: {auditReport.DuplicateLegacyGameplayCompanionAssetPaths.Count}");
            writer.WriteLine($"BuildSceneResidueCount: {auditReport.BuildSceneResiduePaths.Count}");
            writer.WriteLine($"BuildSceneDirectPlayCatalogCoverageGapCount: {auditReport.BuildSceneCoverageGapPaths.Count}");
            writer.WriteLine($"AliasCount: {auditReport.Snapshot.AliasEntries.Count}");
            writer.WriteLine($"AliasRuntimeCodeHitCount: {auditReport.AliasUsage.RuntimeCodeHitCount}");
            writer.WriteLine($"AliasEditorToolingHitCount: {auditReport.AliasUsage.EditorToolingHitCount}");
            writer.WriteLine($"AliasSerializedAssetHitCount: {auditReport.AliasUsage.SerializedAssetHitCount}");
            writer.WriteLine($"AliasDocsOrExamplesHitCount: {auditReport.AliasUsage.DocsOrExamplesHitCount}");
            writer.WriteLine();
            WriteLines(writer, "Duplicate Legacy Gameplay Companion Assets", auditReport.DuplicateLegacyGameplayCompanionAssetPaths);
            WriteLines(writer, "Prunable Alias Candidates", auditReport.PrunableAliasIds);
            WriteLines(writer, "Build Scene Retired-Path Guard Residues", auditReport.BuildSceneResiduePaths);
            WriteLines(writer, "Build Scene Direct-Play Catalog Coverage Gaps", auditReport.BuildSceneCoverageGapPaths);
            WriteAliasUsageLines(writer, auditReport.AliasUsage.Hits);
        }

        private static void WriteAuthoringSurfaceClassification(StreamWriter writer)
        {
            writer.WriteLine("## Authoring Surface Classification");
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                nameof(StageContentEntry),
                StageAuthoringSurfaceKind.StageRoot));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                nameof(StageDefinition),
                StageAuthoringSurfaceKind.GameplayCompanion));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                nameof(StagePresentationDefinition),
                StageAuthoringSurfaceKind.PresentationCompanion));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                nameof(StageAudioDefinition),
                StageAuthoringSurfaceKind.AudioCompanion));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                "Reward / Progression / ClearEvaluation",
                StageAuthoringSurfaceKind.RetiredCompanionGuard));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                nameof(RetiredStageLoadPathGuard),
                StageAuthoringSurfaceKind.RetiredLoadGuard));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                "defaultStageId residue",
                StageAuthoringSurfaceKind.RetiredLoadDetector));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                "direct stageDefinition residue",
                StageAuthoringSurfaceKind.RetiredLoadDetector));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                "serialized StageContentEntry residue",
                StageAuthoringSurfaceKind.RetiredLoadDetector));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                "compat mode residue",
                StageAuthoringSurfaceKind.RetiredLoadDetector));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                "StageEditorDirectPlayCatalog / StageEditorDirectPlayLauncher / StageEditorDirectPlayWindow",
                StageAuthoringSurfaceKind.EditorDirectPlaySupport));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                "PresentationId",
                StageAuthoringSurfaceKind.PresentationOnlyBinding));
            writer.WriteLine("- " + StageAuthoringSurfaceClassificationLabels.Format(
                "UI / Audio / Topology helper references",
                StageAuthoringSurfaceKind.WeakHelperReference));
            writer.WriteLine();
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

        private static void WriteAliasUsageLines(StreamWriter writer, IReadOnlyList<StageAliasUsageHit> hits)
        {
            writer.WriteLine("## Alias Usage Hits");
            if (hits == null || hits.Count == 0)
            {
                writer.WriteLine("None");
                writer.WriteLine();
                return;
            }

            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                writer.WriteLine($"- [{hit.Category}] {hit.AliasId}: {hit.AssetPath}");
            }

            writer.WriteLine();
        }

        private static string FormatOptional(int value)
        {
            return value == 0 ? string.Empty : value.ToString();
        }

        private sealed class StageEditorAssetMetadataProvider : IStageValidationAssetMetadataProvider
        {
            public static readonly StageEditorAssetMetadataProvider Instance = new();

            public string GetAssetPath(UnityEngine.Object asset)
            {
                return asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            }

            public string GetAssetGuid(UnityEngine.Object asset)
            {
                var path = GetAssetPath(asset);
                return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            }
        }
    }
}

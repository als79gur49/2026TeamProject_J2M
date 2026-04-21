using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class StageCatalogCiValidationEntryPoint
    {
        private const string CanonicalStageCatalogAssetPath = "Assets/_Features/Stages/Content/StageCatalog.asset";
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
            };

            var validator = new StageCatalogValidator();
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CanonicalStageCatalogAssetPath);
            var catalogReport = validator.Validate(catalog, options);
            var sceneReport = new StageSceneBootstrapValidator().ValidateEnabledBuildScenes(options);
            var summary = new StageSceneBootstrapValidator().SummarizeEnabledBuildSceneModes();

            Directory.CreateDirectory(ReportDirectory);
            WriteReport(Path.Combine(ReportDirectory, "stage-catalog-validation.md"), catalogReport, sceneReport, summary);

            if (catalogReport.HasErrors || sceneReport.HasErrors)
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
            StageValidationReport sceneReport,
            StageSceneBootstrapUsageSummary summary)
        {
            using var writer = new StreamWriter(outputPath, append: false);
            writer.WriteLine("# Stage Catalog Validation");
            writer.WriteLine();
            writer.WriteLine($"GeneratedAtUtc: {DateTime.UtcNow:O}");
            writer.WriteLine();
            writer.WriteLine("## Scene Mode Summary");
            writer.WriteLine($"CatalogResolvedStageId: {summary.CatalogResolvedStageIdCount}");
            writer.WriteLine($"SerializedStageContentEntry: {summary.SerializedStageContentEntryCount}");
            writer.WriteLine($"LegacyStageDefinition: {summary.LegacyStageDefinitionCount}");
            writer.WriteLine();
            WriteIssues(writer, "Catalog Issues", catalogReport);
            WriteIssues(writer, "Scene Issues", sceneReport);
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
    }
}

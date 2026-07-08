using System;
using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReadinessCiArtifactTests
    {
        [Test]
        public void EditModeTest_CanGenerateCampaignProfileReadinessMarkdownUnderTestLogsSaveReadiness()
        {
            using var harness = new ProfileHarness();
            var report = new CampaignProfileReadinessReportBuilder()
                .Build(new CampaignProfileReadinessReportOptions(harness.SaveRootPath));
            var outputDirectory = Path.Combine(
                CampaignProfileReadinessReportOptions.DefaultOutputDirectory,
                CreateRunId());

            var outputPath = CampaignProfileReadinessReportWriter.Write(report, outputDirectory);
            var markdown = File.ReadAllText(outputPath);
            var normalized = outputPath.Replace('\\', '/');

            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(normalized, Does.Contain("/TestLogs/SaveReadiness/"));
            Assert.That(Path.GetFileName(outputPath), Is.EqualTo("CampaignProfileReadiness.md"));
            Assert.That(markdown, Does.Contain("Report findings are diagnostics/readiness-only."));
            Assert.That(markdown, Does.Contain("Report findings do not block build or release."));
            Assert.That(markdown, Does.Contain("Current production UX truth remains SaveSlotStore / PlayerPrefs."));

            Directory.Delete(Path.GetDirectoryName(outputPath), recursive: true);
        }

        [Test]
        public void CiArtifact_IsWrittenOnlyAsNonProductionTestLogArtifact()
        {
            using var harness = new ProfileHarness();
            var report = new CampaignProfileReadinessReportBuilder()
                .Build(new CampaignProfileReadinessReportOptions(harness.SaveRootPath));
            var outputDirectory = Path.Combine(
                CampaignProfileReadinessReportOptions.DefaultOutputDirectory,
                "ci-artifact-contract",
                CreateRunId());

            var outputPath = CampaignProfileReadinessReportWriter.Write(report, outputDirectory);
            var normalized = outputPath.Replace('\\', '/');

            Assert.That(normalized, Does.Contain("/TestLogs/SaveReadiness/"));
            Assert.That(normalized, Does.Not.Contain("/Assets/"));
            Assert.That(normalized, Does.Not.Contain("/Runtime/"));
            Assert.That(normalized, Does.Not.Contain("/Saves/"));
            Assert.That(Path.GetFileName(outputPath), Is.EqualTo("CampaignProfileReadiness.md"));

            Directory.Delete(Path.GetDirectoryName(outputPath), recursive: true);
        }

        private static string CreateRunId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private sealed class ProfileHarness : IDisposable
        {
            private readonly string _testRootPath;

            public ProfileHarness()
            {
                _testRootPath = Path.Combine(
                    "Temp",
                    "CampaignProfileReadinessCiArtifactTests",
                    Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
            }

            public string SaveRootPath { get; }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_testRootPath))
                    {
                        Directory.Delete(_testRootPath, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}

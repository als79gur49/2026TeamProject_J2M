using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReadinessArtifactPathTests
    {
        [Test]
        public void DefaultArtifactPath_IsUnderTestLogsSaveReadinessWithCiFileName()
        {
            var outputPath = CampaignProfileReadinessReportWriter.ResolveOutputPath();
            var normalized = outputPath.Replace('\\', '/');

            Assert.That(normalized, Does.Contain("/TestLogs/SaveReadiness/"));
            Assert.That(Path.GetFileName(outputPath), Is.EqualTo("CampaignProfileReadiness.md"));
        }

        [Test]
        public void ArtifactPathOutsideTestLogsSaveReadiness_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(
                () => CampaignProfileReadinessReportWriter.ResolveOutputPath(
                    Path.Combine("TestLogs", "OtherReadiness")));
        }

        [Test]
        public void ArtifactPathUnderAssets_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(
                () => CampaignProfileReadinessReportWriter.ResolveOutputPath(
                    Path.Combine("Assets", "_Generated", "SaveReadiness")));
        }

        [Test]
        public void ArtifactPathTraversalIntoAssets_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(
                () => CampaignProfileReadinessReportWriter.ResolveOutputPath(
                    Path.Combine(
                        CampaignProfileReadinessReportOptions.DefaultOutputDirectory,
                        "..",
                        "..",
                        "Assets")));
        }

        [Test]
        public void ArtifactPathUnderPersistentDataPath_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(
                () => CampaignProfileReadinessReportWriter.ResolveOutputPath(
                    Application.persistentDataPath));
        }

        [Test]
        public void ArtifactPathUnderSaveRoot_IsRejected()
        {
            using var harness = new ProfileHarness();

            Assert.Throws<InvalidOperationException>(
                () => CampaignProfileReadinessReportWriter.ResolveOutputPath(
                    harness.SaveRootPath));
        }

        [Test]
        public void TestLogsSaveReadinessArtifact_IsIgnoredByGit()
        {
            var gitignore = File.ReadAllText(".gitignore");

            Assert.That(gitignore, Does.Contain("/TestLogs/SaveReadiness/"));
        }

        private sealed class ProfileHarness : IDisposable
        {
            private readonly string _testRootPath;

            public ProfileHarness()
            {
                _testRootPath = Path.Combine(
                    "Temp",
                    "CampaignProfileReadinessArtifactPathTests",
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

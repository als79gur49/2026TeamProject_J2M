using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReadinessArtifactPathTests
    {
        private const string CiGuidePath = "Docs/Testing/Save-Readiness-CI-Guide.md";

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

        [TestCase("Library")]
        [TestCase("ProjectSettings")]
        [TestCase("Packages")]
        public void ArtifactPathUnderProjectRuntimeOrConfigurationRoots_IsRejected(string root)
        {
            Assert.Throws<InvalidOperationException>(
                () => CampaignProfileReadinessReportWriter.ResolveOutputPath(
                    Path.Combine(root, "SaveReadiness")));
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
        public void ArtifactFileNameOtherThanCampaignProfileReadinessMarkdown_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(
                () => CampaignProfileReadinessReportWriter.ResolveOutputPath(
                    CampaignProfileReadinessReportOptions.DefaultOutputDirectory,
                    "OtherReadiness.md"));
        }

        [Test]
        public void TestLogsSaveReadinessArtifact_IsIgnoredByGit()
        {
            var gitignore = File.ReadAllText(".gitignore");

            Assert.That(gitignore, Does.Contain("/TestLogs/SaveReadiness/"));
        }

        [Test]
        public void CiGuide_DocumentsArtifactUploadPolicyAndForbiddenOutputRoots()
        {
            var guide = File.ReadAllText(CiGuidePath);

            Assert.That(guide, Does.Contain("TestLogs/SaveReadiness/**/CampaignProfileReadiness.md"));
            Assert.That(guide, Does.Contain("The artifact is a CI upload target, not a source asset."));
            Assert.That(guide, Does.Contain("must not be generated under `Assets/`"));
            Assert.That(guide, Does.Contain("must not be generated under `Library/`, `ProjectSettings/`, or `Packages/`"));
            Assert.That(guide, Does.Contain("must not be generated under `Application.persistentDataPath` or any save root"));
            Assert.That(guide, Does.Contain("must not be generated at an arbitrary external path"));
            Assert.That(guide, Does.Contain("artifact file name is always `CampaignProfileReadiness.md`"));
            Assert.That(guide, Does.Contain("must not be committed"));
            Assert.That(guide, Does.Contain("`/TestLogs/SaveReadiness/` is ignored by Git"));
            Assert.That(guide, Does.Contain("Contract tests may create and clean up temporary outputs."));
            Assert.That(guide, Does.Contain("Persistent artifact upload must use the editor command output"));
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

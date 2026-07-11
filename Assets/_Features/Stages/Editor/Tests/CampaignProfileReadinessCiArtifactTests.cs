using System;
using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReadinessCiArtifactTests
    {
        private const string CiGuidePath = "Docs/Testing/Save-Readiness-CI-Guide.md";

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
            Assert.That(markdown, Does.Contain("profile.json is the production campaign progression save truth"));

            Directory.Delete(Path.GetDirectoryName(outputPath), recursive: true);
        }

        [Test]
        public void EditorCommand_GeneratesPersistentCampaignProfileReadinessArtifactForCiUpload()
        {
            var outputDirectory = Path.Combine(
                CampaignProfileReadinessReportOptions.DefaultOutputDirectory,
                "command-artifact-preservation",
                CreateRunId());

            var outputPath = CampaignProfileReadinessReportCommand.WriteDefaultReport(outputDirectory);
            var markdown = File.ReadAllText(outputPath);
            var normalized = outputPath.Replace('\\', '/');

            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(normalized, Does.Contain("/TestLogs/SaveReadiness/command-artifact-preservation/"));
            Assert.That(Path.GetFileName(outputPath), Is.EqualTo("CampaignProfileReadiness.md"));
            Assert.That(markdown, Does.Contain("Report findings are diagnostics/readiness-only."));
            Assert.That(markdown, Does.Contain("Report findings do not block build or release."));
            Assert.That(markdown, Does.Not.Contain("Steam Cloud canonical source"));
        }

        [Test]
        public void EditorCommand_RunIdAndCommitArgumentsResolveToArtifactSubdirectory()
        {
            var outputDirectory = CampaignProfileReadinessReportCommand.ResolveOutputDirectoryFromCommandLine(
                new[]
                {
                    "Unity",
                    CampaignProfileReadinessReportCommand.RunIdArgument,
                    "run-17",
                    CampaignProfileReadinessReportCommand.CommitShaArgument,
                    "8264b8f",
                });

            Assert.That(
                outputDirectory.Replace('\\', '/'),
                Is.EqualTo("TestLogs/SaveReadiness/run-17/8264b8f"));
        }

        [Test]
        public void EditorCommand_ExplicitOutputDirectoryArgumentOverridesRunIdAndCommit()
        {
            var outputDirectory = CampaignProfileReadinessReportCommand.ResolveOutputDirectoryFromCommandLine(
                new[]
                {
                    "Unity",
                    CampaignProfileReadinessReportCommand.RunIdArgument,
                    "run-17",
                    CampaignProfileReadinessReportCommand.CommitShaArgument,
                    "8264b8f",
                    CampaignProfileReadinessReportCommand.OutputDirectoryArgument,
                    "TestLogs/SaveReadiness/explicit-output",
                });

            Assert.That(
                outputDirectory.Replace('\\', '/'),
                Is.EqualTo("TestLogs/SaveReadiness/explicit-output"));
        }

        [Test]
        public void EditorCommand_RejectsInvalidRunIdOrCommitPathSegments()
        {
            Assert.Throws<ArgumentException>(
                () => CampaignProfileReadinessReportCommand.ResolveOutputDirectoryFromCommandLine(
                    new[]
                    {
                        "Unity",
                        CampaignProfileReadinessReportCommand.RunIdArgument,
                        "../outside",
                    }));

            Assert.Throws<ArgumentException>(
                () => CampaignProfileReadinessReportCommand.ResolveOutputDirectoryFromCommandLine(
                    new[]
                    {
                        "Unity",
                        CampaignProfileReadinessReportCommand.CommitShaArgument,
                        "feature/save-readiness",
                    }));
        }

        [Test]
        public void EditorCommand_RejectsForbiddenArtifactOutputPaths()
        {
            Assert.Throws<InvalidOperationException>(
                () => CampaignProfileReadinessReportCommand.WriteDefaultReport(
                    Path.Combine("Assets", "_Generated", "SaveReadiness")));

            Assert.Throws<InvalidOperationException>(
                () => CampaignProfileReadinessReportCommand.WriteDefaultReport(
                    Path.Combine("Temp", "SaveReadiness")));
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

        [Test]
        public void CiGuide_DocumentsFilteredReadinessCommandAndArtifactGlob()
        {
            var guide = File.ReadAllText(CiGuidePath);

            Assert.That(guide, Does.Contain("./run_tests.sh full --filter CampaignProfileReadiness"));
            Assert.That(guide, Does.Contain("TestLogs/SaveReadiness/**/CampaignProfileReadiness.md"));
            Assert.That(guide, Does.Contain("Use the existing filtered full lane"));
            Assert.That(guide, Does.Contain("Recommended Persistent Artifact Generation"));
            Assert.That(guide, Does.Contain("Persistent CI artifacts are generated by the editor command."));
            Assert.That(guide, Does.Contain("Persistent artifact upload must use the editor command output"));
            Assert.That(guide, Does.Contain("The broad unfiltered full lane has known baseline red"));
            Assert.That(guide, Does.Not.Contain("Use `./run_tests.sh full` as the readiness gate"));
            Assert.That(guide, Does.Not.Contain("recommended command: ./run_tests.sh full"));
        }

        [Test]
        public void CiGuide_DocumentsNoNewRunnerLaneOrWorkflowYet()
        {
            var guide = File.ReadAllText(CiGuidePath);

            Assert.That(guide, Does.Contain("Do not add a new `run_tests.sh` lane yet"));
            Assert.That(guide, Does.Contain("Do not add a new GitHub Actions workflow yet"));
            Assert.That(guide, Does.Contain("No repo-defined CI workflow currently exists"));
            Assert.That(guide, Does.Contain("External CI ownership is unknown"));
            Assert.That(guide, Does.Not.Contain(".github/workflows/"));
        }

        [Test]
        public void CiGuide_DocumentsOwnerDecisionRequirementBeforeWorkflow()
        {
            var guide = File.ReadAllText(CiGuidePath);

            Assert.That(guide, Does.Contain("CI owner decision required"));
            Assert.That(guide, Does.Contain("GitHub Actions or external CI is not decided by the repository yet"));
            Assert.That(guide, Does.Contain("must be decided by the CI owner"));
            Assert.That(guide, Does.Contain("Do not add `.github/workflows` before the owner decision is recorded"));
            Assert.That(guide, Does.Contain("CI Owner Handoff Checklist"));
            Assert.That(guide, Does.Contain("CI platform: GitHub Actions / Jenkins / Azure / GitLab / other"));
        }

        [Test]
        public void CiGuide_DocumentsUnityRunnerLicenseAndWorkingDirectoryHandoff()
        {
            var guide = File.ReadAllText(CiGuidePath);

            Assert.That(guide, Does.Contain("Unity runner, Unity license, cache, and artifact upload policy"));
            Assert.That(guide, Does.Contain("Runner OS: Windows / Linux / self-hosted / cloud-hosted"));
            Assert.That(guide, Does.Contain("Unity version: `6000.3.11f1`"));
            Assert.That(guide, Does.Contain("Unity license activation: owner-provided"));
            Assert.That(guide, Does.Contain("Working directory: repository root"));
        }

        [Test]
        public void CiGuide_DocumentsArtifactUploadOwnerDecisionAndGlob()
        {
            var guide = File.ReadAllText(CiGuidePath);

            Assert.That(guide, Does.Contain("Artifact upload failure policy: CI owner decision"));
            Assert.That(guide, Does.Contain("Artifact upload glob: `TestLogs/SaveReadiness/**/CampaignProfileReadiness.md`"));
            Assert.That(guide, Does.Contain("TestLogs/SaveReadiness/<run-id>/<commit-sha>/CampaignProfileReadiness.md"));
            Assert.That(guide, Does.Contain("CampaignProfileReadinessReportCommand.WriteDefaultReportFromCommandLine"));
            Assert.That(guide, Does.Contain("Upload `TestLogs/SaveReadiness/**/CampaignProfileReadiness.md` as a CI artifact"));
        }

        [Test]
        public void CiGuide_DocumentsReadinessCommandWithoutBroadFullRecommendation()
        {
            var guide = File.ReadAllText(CiGuidePath);

            Assert.That(guide, Does.Contain("Test command: `./run_tests.sh full --filter CampaignProfileReadiness`"));
            Assert.That(guide, Does.Contain("Do not use broad unfiltered `./run_tests.sh full` for this readiness lane"));
            Assert.That(guide, Does.Contain("Forbidden readiness command"));
            Assert.That(guide, Does.Not.Contain("Test command: `./run_tests.sh full`."));
            Assert.That(guide, Does.Not.Contain("Test command: ./run_tests.sh full"));
            Assert.That(guide, Does.Not.Contain("Use broad unfiltered `./run_tests.sh full` for this readiness lane"));
        }

        [Test]
        public void CiGuide_DocumentsNonBlockingReportAndForbiddenProductionUses()
        {
            var guide = File.ReadAllText(CiGuidePath);

            Assert.That(guide, Does.Contain("Non-blocking policy"));
            Assert.That(guide, Does.Contain("Findings must not fail release, build, or Steam packaging"));
            Assert.That(guide, Does.Contain("Compile, test, contract, and generation failures may fail CI"));
            Assert.That(guide, Does.Contain("is not a release blocker"));
            Assert.That(guide, Does.Contain("A release gate."));
            Assert.That(guide, Does.Contain("A build gate."));
            Assert.That(guide, Does.Contain("A Steam packaging gate."));
            Assert.That(guide, Does.Contain("A Steam Cloud canonical source."));
            Assert.That(guide, Does.Contain("A production repair UX gate."));
            Assert.That(guide, Does.Contain("`profile.json` is production campaign progression truth"));
        }

        [Test]
        public void Repository_DoesNotDefineSaveReadinessWorkflowOrRunnerLane()
        {
            Assert.That(Directory.Exists(Path.Combine(".github", "workflows")), Is.False);

            var runner = File.ReadAllText("run_tests.sh");
            Assert.That(runner, Does.Not.Contain("CampaignProfileReadiness"));
            Assert.That(runner, Does.Not.Contain("SaveReadiness"));
            Assert.That(runner, Does.Not.Contain("save-readiness"));
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

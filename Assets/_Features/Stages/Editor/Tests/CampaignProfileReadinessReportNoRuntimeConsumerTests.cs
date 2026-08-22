using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileReadinessReportNoRuntimeConsumerTests
    {
        private const string ReportBuilderPath =
            "Assets/_Features/Stages/Editor/Validation/CampaignProfileReadinessReportBuilder.cs";
        private const string ReportCommandPath =
            "Assets/_Features/Stages/Editor/Validation/CampaignProfileReadinessReportCommand.cs";

        private static readonly string[] RuntimeRoots =
        {
            "Assets/_Features/UI/UI_Application/Runtime",
            "Assets/_Features/UI/UI_Composition/Runtime",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime",
            "Assets/_Features/Stages/Runtime/Load",
            "Assets/_Features/DemoStageControl/Runtime",
        };

        [Test]
        public void ReportConsumer_RemainsEditorOnly()
        {
            Assert.That(ReportBuilderPath, Does.StartWith("Assets/_Features/Stages/Editor/"));
            Assert.That(ReportCommandPath, Does.StartWith("Assets/_Features/Stages/Editor/"));
        }

        [Test]
        public void ReportBuilder_DoesNotCallRepositoryServiceMigrationOrAdapter()
        {
            var source = File.ReadAllText(ReportBuilderPath);

            AssertSourceDoesNotContain(
                source,
                "FileCampaignProfileRepository",
                "ICampaignProfileRepository",
                "CampaignProfileLoadResult",
                "CampaignSaveService",
                "CampaignSaveServiceFactory",
                "CampaignSaveMigrationCoordinator",
                "CampaignSaveSlotStoreAdapter",
                ".Migrate");
        }

        [TestCase("CampaignProfileReadinessReport")]
        [TestCase("CampaignProfileReadinessReportBuilder")]
        [TestCase("CampaignProfileReadinessReportWriter")]
        [TestCase("CampaignProfileMetadataProbe")]
        [TestCase("LastPlayedSlotNumber")]
        [TestCase("profile.json")]
        public void RuntimeConsumers_DoNotReferenceReadinessReportOrProbeTokens(string forbiddenToken)
        {
            foreach (var path in EnumerateRuntimeSourceFiles())
            {
                Assert.That(File.ReadAllText(path), Does.Not.Contain(forbiddenToken), $"{path}: {forbiddenToken}");
            }
        }

        [Test]
        public void RuntimeConsumers_DoNotUseReportForUxFocusWarningsOrSteamCloud()
        {
            foreach (var path in EnumerateRuntimeSourceFiles())
            {
                AssertSourceDoesNotContain(
                    File.ReadAllText(path),
                    "CampaignProfileReadinessReport",
                    "CampaignProfileReadinessReportWriter",
                    "CampaignProfileMetadataProbe",
                    "QuickContinue",
                    "DefaultFocus",
                    "SaveReadiness",
                    "ISteamRemoteStorage",
                    "SteamRemoteStorage");
            }
        }

        [Test]
        public void ReportCommand_IsEditorDiagnosticArtifactOnly()
        {
            var source = File.ReadAllText(ReportCommandPath);

            Assert.That(source, Does.Contain("MenuItem"));
            Assert.That(source, Does.Contain("CampaignProfileReadinessReportWriter.Write"));
            Assert.That(source, Does.Not.Contain("MainMenu"));
            Assert.That(source, Does.Not.Contain("GameplaySceneHost"));
            Assert.That(source, Does.Not.Contain("DemoStageControl"));
            Assert.That(source, Does.Not.Contain("SteamRemoteStorage"));
        }

        private static IEnumerable<string> EnumerateRuntimeSourceFiles()
        {
            return RuntimeRoots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                .Where(path => !path.Contains("/Tests/") && !path.Contains("\\Tests\\"))
                .OrderBy(path => path);
        }

        private static void AssertSourceDoesNotContain(string source, params string[] forbiddenTokens)
        {
            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(source, Does.Not.Contain(forbiddenTokens[i]), forbiddenTokens[i]);
            }
        }
    }
}

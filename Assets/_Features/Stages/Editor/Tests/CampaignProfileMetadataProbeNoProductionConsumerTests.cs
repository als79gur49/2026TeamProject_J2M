using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignProfileMetadataProbeNoProductionConsumerTests
    {
        private static readonly string[] ProductionRoots =
        {
            "Assets/_Features/UI/UI_Composition",
            "Assets/_Features/Gameplay/Gameplay_Host",
            "Assets/_Features/Stages/Runtime/Load",
            "Assets/_Features/DemoStageControl/Runtime",
        };

        private static readonly string[] MainMenuProductionFiles =
        {
            "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs",
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs",
        };

        [TestCase("CampaignProfileMetadataProbe")]
        [TestCase("CampaignProfileDocument")]
        [TestCase("LastPlayedSlotNumber")]
        [TestCase("profile.json")]
        [TestCase("FileCampaignProfileRepository")]
        [TestCase("CampaignSaveService")]
        [TestCase("CampaignSaveServiceFactory")]
        [TestCase("CampaignSaveMigrationCoordinator")]
        public void ProductionRuntimeConsumers_DoNotReferenceProbeOrV2MetadataTruthTokens(string forbiddenToken)
        {
            foreach (var path in EnumerateProductionSourceFiles())
            {
                Assert.That(File.ReadAllText(path), Does.Not.Contain(forbiddenToken), $"{path}: {forbiddenToken}");
            }
        }

        [Test]
        public void MainMenuProductionPath_DoesNotUseProbeForVisibleWarningFocusOrQuickContinue()
        {
            foreach (var path in MainMenuProductionFiles)
            {
                AssertSourceDoesNotContain(
                    path,
                    "CampaignProfileMetadataProbe",
                    "CampaignProfileDocument",
                    "LastPlayedSlotNumber",
                    "profile.json",
                    "QuickContinue",
                    "DefaultFocus",
                    "CampaignSaveServiceFactory",
                    "FileCampaignProfileRepository");
            }
        }

        [Test]
        public void GameplayStageLoadAndDemoStageControl_DoNotUseProbeAsRuntimeTruth()
        {
            foreach (var path in EnumerateSourceFiles(
                         "Assets/_Features/Gameplay/Gameplay_Host",
                         "Assets/_Features/Stages/Runtime/Load",
                         "Assets/_Features/DemoStageControl/Runtime"))
            {
                AssertSourceDoesNotContain(
                    path,
                    "CampaignProfileMetadataProbe",
                    "CampaignProfileDocument",
                    "LastPlayedSlotNumber",
                    "profile.json",
                    "CampaignSaveServiceFactory",
                    "FileCampaignProfileRepository",
                    "CampaignSaveMigrationCoordinator");
            }
        }

        [Test]
        public void RuntimeDebugUiConsumers_AreNotAllowedInPhase13()
        {
            foreach (var path in EnumerateProductionSourceFiles())
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("CampaignProfileMetadataProbe"), path);
                Assert.That(source, Does.Not.Contain("ImportDisabled"), path);
                Assert.That(source, Does.Not.Contain("ResetTombstone"), path);
                Assert.That(source, Does.Not.Contain("DeletedSlotGuards"), path);
            }
        }

        [Test]
        public void SteamCloudReadiness_DoesNotTreatProfileMetadataAsCanonicalTruth()
        {
            foreach (var path in EnumerateProductionSourceFiles())
            {
                AssertSourceDoesNotContain(
                    path,
                    "Steamworks",
                    "ISteamRemoteStorage",
                    "SteamRemoteStorage",
                    "ImportedSourceHash",
                    "CampaignProfileMetadataProbe");
            }
        }

        private static IEnumerable<string> EnumerateProductionSourceFiles()
        {
            foreach (var path in MainMenuProductionFiles)
            {
                yield return path;
            }

            foreach (var path in EnumerateSourceFiles(ProductionRoots))
            {
                yield return path;
            }
        }

        private static IEnumerable<string> EnumerateSourceFiles(params string[] roots)
        {
            return roots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                .Where(path =>
                    !path.Contains("/Editor/") &&
                    !path.Contains("\\Editor\\") &&
                    !path.Contains("/Tests/") &&
                    !path.Contains("\\Tests\\"))
                .OrderBy(path => path);
        }

        private static void AssertSourceDoesNotContain(string path, params string[] forbiddenTokens)
        {
            var source = File.ReadAllText(path);
            foreach (var forbiddenToken in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(forbiddenToken), $"{path}: {forbiddenToken}");
            }
        }
    }
}

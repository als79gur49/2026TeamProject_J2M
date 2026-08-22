using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class SaveArchitectureV2ProductionReadinessTests
    {
        private const string CompositionPath =
            "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveCompositionProvider.cs";
        private const string FactoryPath =
            "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveServiceFactory.cs";
        private const string PolicyPath =
            "Docs/Architecture/Pre-Release-Save-Baseline-Policy.md";

        [Test]
        public void ProductionComposition_UsesCurrentJsonRepositories()
        {
            var source = File.ReadAllText(CompositionPath);

            Assert.That(source, Does.Contain("CampaignSaveFacadeFactory.Create"));
            Assert.That(source, Does.Contain("FileCampaignLocalLaunchStateRepository"));
            Assert.That(source, Does.Contain("AtomicTextFileStore"));
            Assert.That(source, Does.Not.Contain("new PlayerPrefsActiveSlotStorage"));
        }

        [Test]
        public void ProductionOptions_ExposeNoLegacyOrRollbackControls()
        {
            var properties = typeof(CampaignSaveCompositionOptions)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(properties, Is.EqualTo(new[]
            {
                "PathProvider",
                "ProductVersion",
                "ProfileId",
                "UtcNow",
            }));
        }

        [Test]
        public void Factory_HasNoBackendSwitchOrPlayerPrefsDependency()
        {
            var source = File.ReadAllText(FactoryPath);

            Assert.That(source, Does.Not.Contain("PlayerPrefs"));
            Assert.That(source, Does.Not.Contain("BackendMode"));
            Assert.That(source, Does.Not.Contain("Migration"));
            Assert.That(source, Does.Not.Contain("Rollback"));
        }

        [Test]
        public void ProductionEntryPoints_UseProfileBackedProvider()
        {
            var entryPoints = new[]
            {
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs",
            };

            foreach (var path in entryPoints)
            {
                Assert.That(
                    File.ReadAllText(path),
                    Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"),
                    path);
            }
        }

        [Test]
        public void GameplayHost_DirectPlayUsesTemporaryJsonComposition()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs");
            var directPlayStore = source.IndexOf(
                "CampaignSaveCompositionProvider.CreateTemporaryProfileBacked()",
                StringComparison.Ordinal);
            var productionStore = source.IndexOf(
                "CampaignSaveCompositionProvider.CreateProductionProfileBacked()",
                StringComparison.Ordinal);

            Assert.That(directPlayStore, Is.GreaterThanOrEqualTo(0));
            Assert.That(productionStore, Is.GreaterThan(directPlayStore));
            Assert.That(source, Does.Not.Contain("TransientCampaignSaveSlotStore"));
            Assert.That(source, Does.Not.Contain("PlayerPrefs"));
        }

        [Test]
        public void CurrentPolicy_DeclaresJsonOnlyProductionTruth()
        {
            var policy = File.ReadAllText(PolicyPath);

            Assert.That(policy, Does.Contain("`Saves/profile.json`"));
            Assert.That(policy, Does.Contain("`Saves/local-launch-state.json`"));
            Assert.That(policy, Does.Contain("PlayerPrefs campaign progression import is unsupported"));
            Assert.That(policy, Does.Contain("Unknown save schemas fail closed"));
        }
    }
}

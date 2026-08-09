using System;
using System.IO;
using System.Linq;
using Game.Product.Achievements.Infrastructure;
using NUnit.Framework;

namespace Game.Product.Achievements.Tests
{
    [TestFixture]
    [Category("ProductAchievement")]
    public sealed class ProductAchievementArchitectureTests
    {
        private const string AchievementRoot = "Assets/_Features/Achievements";
        private const string DomainRoot =
            AchievementRoot + "/Achievement_Domain/Runtime";
        private const string InfrastructureRoot =
            AchievementRoot + "/Achievement_Infrastructure/Runtime";
        private const string CompositionRoot =
            AchievementRoot + "/Achievement_Composition/Runtime";
        private const string CampaignReceiptRoot =
            "Assets/_Features/Stages/Runtime/Campaign/Save";

        [Test]
        public void DomainAssembly_HasNoStageSteamUiOrGameplayReference()
        {
            var referencedNames = typeof(GameAchievementId).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(referencedNames, Does.Not.Contain("Game.Feature.Stages"));
            Assert.That(referencedNames.Any(name => name.IndexOf("Steam", StringComparison.Ordinal) >= 0), Is.False);
            Assert.That(referencedNames.Any(name => name.IndexOf("UI", StringComparison.Ordinal) >= 0), Is.False);
            Assert.That(referencedNames.Any(name => name.IndexOf("Gameplay", StringComparison.Ordinal) >= 0), Is.False);

            var asmdef = File.ReadAllText(
                DomainRoot + "/Game.Product.Achievements.Domain.asmdef");
            Assert.That(asmdef, Does.Contain("\"references\": []"));
        }

        [Test]
        public void DomainAndInfrastructure_DoNotReferenceStageSaveImplementation()
        {
            AssertSourcesDoNotContain(
                new[] { DomainRoot, InfrastructureRoot },
                "Game.Feature.Stages",
                "CampaignProfileDocument",
                "CampaignSlotDocument",
                "IAtomicTextFileStore",
                "FileCampaignProfileRepository",
                "CampaignSaveService");
        }

        [Test]
        public void ProductProductionModule_HasNoStoreTransportOrTechnicalAcceptanceTokens()
        {
            AssertSourcesDoNotContain(
                new[]
                {
                    DomainRoot,
                    InfrastructureRoot,
                    AchievementRoot + "/Achievement_Composition/Runtime",
                },
                "Game.Platform.Steam",
                "ISteamAchievementApi",
                "Steamworks",
                "ACH_WIN_ONE_GAME",
                "Spacewar",
                "-j2mSteamAchievementSmoke",
                "VQ_",
                "ACH_");

            AssertSourcesDoNotContain(
                new[] { DomainRoot, InfrastructureRoot },
                "PlatformRuntimeRegistry");
        }

        [Test]
        public void ProductDocument_IsGlobalAndOwnsNoSaveSlotField()
        {
            var fields = typeof(ProductAchievementDocument)
                .GetFields()
                .Where(field => !field.IsLiteral)
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(
                fields,
                Is.EqualTo(
                    new[]
                    {
                        "EarnedAchievementIds",
                        "PendingAchievementPublicationIds",
                        "SchemaVersion",
                    }));
            Assert.That(
                FileProductAchievementRepository.AchievementFileName,
                Is.EqualTo("achievements.json"));
            AssertSourcesDoNotContain(
                new[] { DomainRoot, InfrastructureRoot },
                "DeleteSlot",
                "NewGame",
                "ClearCampaign",
                "SlotNumber");
        }

        [Test]
        public void ProductModule_DoesNotUsePlatformRegistryAsServiceLocator()
        {
            AssertSourcesDoNotContain(
                new[]
                {
                    DomainRoot,
                    InfrastructureRoot,
                    AchievementRoot + "/Achievement_Composition/Runtime",
                },
                "PlatformRuntimeRegistry");
        }

        [Test]
        public void CampaignReceiptProductionSources_OwnNoProductAchievementOrSteamIdentity()
        {
            AssertSourcesDoNotContain(
                new[]
                {
                    CampaignReceiptRoot,
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs",
                },
                "campaign.complete",
                "GameAchievementId",
                "GameAchievementIds",
                "ProductAchievementCoordinator",
                "ISteamAchievementApi",
                "Steamworks",
                "AppID 480",
                "ACH_WIN_ONE_GAME",
                "Spacewar");
        }

        [Test]
        public void CampaignGameplayFlow_HasNoProductEarningCallOrDependency()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs");

            Assert.That(source, Does.Not.Contain("ProductAchievement"));
            Assert.That(source, Does.Not.Contain("IProductAchievementEarningSink"));
            Assert.That(source, Does.Not.Contain(".Earn("));
        }

        [Test]
        public void ProductionComposition_UsesCanonicalProviderAndIntroducesNoServiceLocator()
        {
            var source = File.ReadAllText(
                CompositionRoot + "/ProductAchievementApplicationComposition.cs");

            Assert.That(source, Does.Contain("new ApplicationPersistentDataSavePathProvider()"));
            Assert.That(source, Does.Contain("RuntimeInitializeLoadType.BeforeSceneLoad"));
            Assert.That(source, Does.Contain("Application.quitting"));
            Assert.That(source, Does.Not.Contain("Application.persistentDataPath"));
            Assert.That(source, Does.Not.Contain("DontDestroyOnLoad"));
            Assert.That(source, Does.Not.Contain("ProductAchievementApplicationHost.Instance"));
            Assert.That(source, Does.Not.Contain("GetAchievementHost"));
            Assert.That(source, Does.Not.Contain("ResolveAchievementCoordinator"));
            Assert.That(source, Does.Not.Contain("FindObjectOfType"));
            Assert.That(source, Does.Not.Contain("Resources.Load"));
            Assert.That(source, Does.Not.Contain("PlatformRuntimeRegistry"));
        }

        private static void AssertSourcesDoNotContain(string[] roots, params string[] forbiddenTokens)
        {
            foreach (var root in roots)
            {
                var sourcePaths = File.Exists(root)
                    ? new[] { root }
                    : Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories));
                foreach (var sourcePath in sourcePaths)
                {
                    var source = File.ReadAllText(sourcePath);
                    foreach (var token in forbiddenTokens)
                    {
                        Assert.That(source, Does.Not.Contain(token), $"{sourcePath}: {token}");
                    }
                }
            }
        }
    }
}

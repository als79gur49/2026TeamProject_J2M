using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSaveServiceFactoryTests
    {
        private static readonly DateTime FixedNowUtc =
            new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc);

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportDisabledKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportedSourceHashKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void Create_CreatesServiceWithTempPathProvider()
        {
            using var harness = new FactoryHarness();

            var result = CampaignSaveServiceFactory.Create(harness.Options());
            var saveResult = result.Service.InitializeNewGame(1, "stage-1-1", "level-1");

            Assert.That(result.PathProvider.SaveRootPath, Is.EqualTo(harness.SaveRootPath));
            Assert.That(result.TextFileStore, Is.Not.Null);
            Assert.That(result.Repository, Is.Not.Null);
            Assert.That(result.LegacyImporter, Is.Not.Null);
            Assert.That(result.Coordinator, Is.Not.Null);
            Assert.That(result.Service, Is.Not.Null);
            Assert.That(saveResult.Succeeded, Is.True);
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
        }

        [Test]
        public void Create_CreatesCompatibilityAdapterWhenRequested()
        {
            using var harness = new FactoryHarness();

            var result = CampaignSaveServiceFactory.Create(harness.Options(
                createCompatibilityAdapter: true));

            Assert.That(result.CompatibilityAdapter, Is.Not.Null);
        }

        [Test]
        public void Create_LeavesCompatibilityAdapterNullByDefault()
        {
            using var harness = new FactoryHarness();

            var result = CampaignSaveServiceFactory.Create(harness.Options());

            Assert.That(result.CompatibilityAdapter, Is.Null);
        }

        [Test]
        public void Create_DefaultEnableProfileWriteIsFalse()
        {
            using var harness = new FactoryHarness();
            WriteLegacyPayload(CreateSlot(1, "stage-1-1", "level-1"));

            var result = CampaignSaveServiceFactory.Create(harness.Options());
            var migration = result.Coordinator.Run();

            Assert.That(result.MigrationOptions.EnableProfileWrite, Is.False);
            Assert.That(migration.Status, Is.EqualTo(CampaignSaveMigrationStatus.MigrationDeferred));
            Assert.That(migration.ProfileWriteAttempted, Is.False);
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
        }

        [Test]
        public void Create_ExplicitEnableProfileWriteAllowsCoordinatorWrite()
        {
            using var harness = new FactoryHarness();
            WriteLegacyPayload(CreateSlot(1, "stage-1-1", "level-1"));

            var result = CampaignSaveServiceFactory.Create(harness.Options(
                enableProfileWrite: true));
            var migration = result.Coordinator.Run();

            Assert.That(result.MigrationOptions.EnableProfileWrite, Is.True);
            Assert.That(migration.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportSucceeded));
            Assert.That(migration.ProfileWriteAttempted, Is.True);
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
            Assert.That(result.Repository.Load().Document.ProfileId, Is.EqualTo("factory-test-profile"));
        }

        [Test]
        public void Create_RequiresExplicitPathProvider()
        {
            var options = new CampaignSaveServiceFactoryOptions();

            Assert.That(
                () => CampaignSaveServiceFactory.Create(options),
                Throws.ArgumentException);
        }

        [Test]
        [Category("Infrastructure")]
        public void FactoryClusters_AreInternal_WhileCreatesAndProfileServicesRemainAssemblyVisible()
        {
            var facadeFactoryType = typeof(CampaignSaveFacadeFactory);
            var facadeCreate = facadeFactoryType.GetMethod(
                nameof(CampaignSaveFacadeFactory.Create),
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(CampaignSaveCompositionOptions) },
                null);
            var factoryType = typeof(CampaignSaveServiceFactory);
            var optionsType = typeof(CampaignSaveServiceFactoryOptions);
            var resultType = typeof(CampaignSaveServiceFactoryResult);
            var create = factoryType.GetMethod(
                nameof(CampaignSaveServiceFactory.Create),
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { optionsType },
                null);
            var profileServices = typeof(CampaignSaveFacadeFactoryResult).GetProperty(
                nameof(CampaignSaveFacadeFactoryResult.ProfileServices),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var profileServicesGetter = profileServices?.GetGetMethod(nonPublic: true);

            Assert.That(facadeFactoryType.IsPublic, Is.False);
            Assert.That(facadeFactoryType.IsNotPublic, Is.True);
            Assert.That(facadeCreate, Is.Not.Null);
            Assert.That(facadeCreate.IsPublic, Is.False);
            Assert.That(facadeCreate.IsAssembly, Is.True);
            Assert.That(factoryType.IsPublic, Is.False);
            Assert.That(factoryType.IsNotPublic, Is.True);
            Assert.That(optionsType.IsPublic, Is.False);
            Assert.That(resultType.IsPublic, Is.False);
            Assert.That(create, Is.Not.Null);
            Assert.That(create.IsPublic, Is.False);
            Assert.That(create.IsAssembly, Is.True);
            Assert.That(profileServicesGetter, Is.Not.Null);
            Assert.That(profileServicesGetter.IsPublic, Is.False);
            Assert.That(profileServicesGetter.IsAssembly, Is.True);
            Assert.That(typeof(CampaignSaveCompositionProvider).IsPublic, Is.True);
        }

        [Test]
        [Category("Infrastructure")]
        public void CompositionProvider_PublicApiAndInternalDtos_MatchSupportedBoundary()
        {
            const BindingFlags publicDeclaredStatic =
                BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static;
            const BindingFlags nonPublicDeclaredStatic =
                BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags allDeclaredStatic =
                BindingFlags.DeclaredOnly |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static;

            var providerType = typeof(CampaignSaveCompositionProvider);
            var publicMethods = providerType.GetMethods(publicDeclaredStatic);
            var profileBacked = providerType.GetMethod(
                nameof(CampaignSaveCompositionProvider.CreateProductionProfileBacked),
                publicDeclaredStatic,
                null,
                Type.EmptyTypes,
                null);
            var activeSlotProvider = providerType.GetMethod(
                nameof(CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider),
                publicDeclaredStatic,
                null,
                new[] { typeof(ICampaignSaveSlotStore) },
                null);
            var legacyRollback = providerType.GetMethod(
                nameof(CampaignSaveCompositionProvider.CreateProductionLegacyRollback),
                publicDeclaredStatic,
                null,
                Type.EmptyTypes,
                null);
            var genericCreate = providerType.GetMethod(
                nameof(CampaignSaveCompositionProvider.Create),
                nonPublicDeclaredStatic,
                null,
                new[] { typeof(CampaignSaveCompositionOptions) },
                null);
            var profileOptions = providerType.GetMethod(
                nameof(CampaignSaveCompositionProvider.CreateProductionProfileBackedOptions),
                nonPublicDeclaredStatic,
                null,
                Type.EmptyTypes,
                null);
            var rollbackOptions = providerType.GetMethod(
                nameof(CampaignSaveCompositionProvider.CreateProductionLegacyRollbackOptions),
                nonPublicDeclaredStatic,
                null,
                Type.EmptyTypes,
                null);
            var resetForTests = providerType.GetMethod(
                nameof(CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests),
                nonPublicDeclaredStatic,
                null,
                Type.EmptyTypes,
                null);
            var removedFacadeHelper = providerType.GetMethod(
                "CreateProductionProfileBackedFacade",
                allDeclaredStatic);

            Assert.That(providerType.IsPublic, Is.True);
            Assert.That(publicMethods, Has.Length.EqualTo(3));
            Assert.That(
                publicMethods.Select(method => method.Name),
                Is.EquivalentTo(new[]
                {
                    nameof(CampaignSaveCompositionProvider.CreateProductionProfileBacked),
                    nameof(CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider),
                    nameof(CampaignSaveCompositionProvider.CreateProductionLegacyRollback),
                }));

            Assert.That(profileBacked, Is.Not.Null);
            Assert.That(profileBacked.ReturnType, Is.EqualTo(typeof(ICampaignSaveSlotStore)));
            Assert.That(activeSlotProvider, Is.Not.Null);
            Assert.That(activeSlotProvider.ReturnType, Is.EqualTo(typeof(ActiveSlotProvider)));
            Assert.That(legacyRollback, Is.Not.Null);
            Assert.That(legacyRollback.IsPublic, Is.True);
            Assert.That(legacyRollback.ReturnType, Is.EqualTo(typeof(ICampaignSaveSlotStore)));

            Assert.That(genericCreate, Is.Not.Null);
            Assert.That(genericCreate.IsAssembly, Is.True);
            Assert.That(profileOptions, Is.Not.Null);
            Assert.That(profileOptions.IsAssembly, Is.True);
            Assert.That(rollbackOptions, Is.Not.Null);
            Assert.That(rollbackOptions.IsAssembly, Is.True);
            Assert.That(resetForTests, Is.Not.Null);
            Assert.That(resetForTests.IsAssembly, Is.True);
            Assert.That(removedFacadeHelper, Is.Null);

            Assert.That(typeof(CampaignSaveCompositionOptions).IsNotPublic, Is.True);
            Assert.That(typeof(CampaignSaveBackendMode).IsNotPublic, Is.True);
            Assert.That(typeof(CampaignSaveFacadeFactoryResult).IsNotPublic, Is.True);
            Assert.That((int)CampaignSaveBackendMode.PlayerPrefsLegacy, Is.EqualTo(0));
            Assert.That((int)CampaignSaveBackendMode.ProfileJsonExplicit, Is.EqualTo(1));

            foreach (var publicMethod in publicMethods)
            {
                Assert.That(publicMethod.ReturnType.IsPublic, Is.True, publicMethod.Name);
                Assert.That(
                    publicMethod.GetParameters().All(parameter => parameter.ParameterType.IsPublic),
                    Is.True,
                    publicMethod.Name);
            }
        }

        [Test]
        [Category("Infrastructure")]
        public void RetiredProductionReadinessTypes_AreAbsentFromRuntimeAssembly()
        {
            const string runtimeNamespace = "Game.Feature.Stages.";
            var runtimeAssembly = typeof(CampaignSaveCompositionProvider).Assembly;

            Assert.That(
                runtimeAssembly.GetType(
                    runtimeNamespace + "CampaignSaveProduction" + "ReadinessPolicy",
                    throwOnError: false),
                Is.Null);
            Assert.That(
                runtimeAssembly.GetType(
                    runtimeNamespace + "CampaignSaveProduction" + "ReadinessResult",
                    throwOnError: false),
                Is.Null);
        }

        private static void WriteLegacyPayload(params SaveSlotData[] slots)
        {
            var dto = SaveSlotDtoMapper.ToDto(slots);
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, JsonUtility.ToJson(dto));
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, slots[0].SlotNumber);
            PlayerPrefs.Save();
        }

        private static SaveSlotData CreateSlot(
            int slotNumber,
            string stageId,
            string levelGroupId)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow(stageId),
                CurrentLevelGroupId = levelGroupId,
                RemainingChances = 3,
                LastPlayedAt = "2026-07-06T00:00:00Z",
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private sealed class FactoryHarness : IDisposable
        {
            private readonly TemporarySavePathProvider _pathProvider;

            public FactoryHarness()
            {
                SaveRootPath = Path.Combine(
                    "Temp",
                    "CampaignSaveServiceFactoryTests",
                    Guid.NewGuid().ToString("N"));
                _pathProvider = new TemporarySavePathProvider(SaveRootPath);
            }

            public string SaveRootPath { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, FileCampaignProfileRepository.ProfileFileName);

            public CampaignSaveServiceFactoryOptions Options(
                bool enableProfileWrite = false,
                bool createCompatibilityAdapter = false)
            {
                return new CampaignSaveServiceFactoryOptions
                {
                    PathProvider = _pathProvider,
                    ProductVersion = "factory-test-product",
                    ProfileId = "factory-test-profile",
                    UtcNow = () => FixedNowUtc,
                    EnableProfileWrite = enableProfileWrite,
                    CreateCompatibilityAdapter = createCompatibilityAdapter,
                };
            }

            public void Dispose()
            {
                if (Directory.Exists(SaveRootPath))
                {
                    Directory.Delete(SaveRootPath, recursive: true);
                }
            }
        }

        private sealed class TemporarySavePathProvider : SavePathProviderBase
        {
            public TemporarySavePathProvider(string saveRootPath)
                : base(saveRootPath)
            {
            }
        }
    }
}

using System;
using System.IO;
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
            PlayerPrefs.Save();
        }

        [Test]
        public void CreateForTests_CreatesServiceWithTempPathProvider()
        {
            using var harness = new FactoryHarness();

            var result = CampaignSaveServiceFactory.CreateForTests(harness.Options());
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
        public void CreateForTests_CreatesCompatibilityAdapterWhenRequested()
        {
            using var harness = new FactoryHarness();

            var result = CampaignSaveServiceFactory.CreateForTests(harness.Options(
                createCompatibilityAdapter: true));

            Assert.That(result.CompatibilityAdapter, Is.Not.Null);
        }

        [Test]
        public void CreateForTests_LeavesCompatibilityAdapterNullByDefault()
        {
            using var harness = new FactoryHarness();

            var result = CampaignSaveServiceFactory.CreateForTests(harness.Options());

            Assert.That(result.CompatibilityAdapter, Is.Null);
        }

        [Test]
        public void CreateForTests_DefaultEnableProfileWriteIsFalse()
        {
            using var harness = new FactoryHarness();
            WriteLegacyPayload(CreateSlot(1, "stage-1-1", "level-1"));

            var result = CampaignSaveServiceFactory.CreateForTests(harness.Options());
            var migration = result.Coordinator.Run();

            Assert.That(result.MigrationOptions.EnableProfileWrite, Is.False);
            Assert.That(migration.Status, Is.EqualTo(CampaignSaveMigrationStatus.MigrationDeferred));
            Assert.That(migration.ProfileWriteAttempted, Is.False);
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
        }

        [Test]
        public void CreateForTests_ExplicitEnableProfileWriteAllowsCoordinatorWrite()
        {
            using var harness = new FactoryHarness();
            WriteLegacyPayload(CreateSlot(1, "stage-1-1", "level-1"));

            var result = CampaignSaveServiceFactory.CreateForTests(harness.Options(
                enableProfileWrite: true));
            var migration = result.Coordinator.Run();

            Assert.That(result.MigrationOptions.EnableProfileWrite, Is.True);
            Assert.That(migration.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportSucceeded));
            Assert.That(migration.ProfileWriteAttempted, Is.True);
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
            Assert.That(result.Repository.Load().Document.ProfileId, Is.EqualTo("factory-test-profile"));
        }

        [Test]
        public void CreateForTests_RequiresExplicitPathProvider()
        {
            var options = new CampaignSaveServiceFactoryOptions();

            Assert.That(
                () => CampaignSaveServiceFactory.CreateForTests(options),
                Throws.ArgumentException);
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

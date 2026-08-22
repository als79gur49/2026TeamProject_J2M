using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSaveServiceFactoryTests
    {
        private static readonly DateTime FixedNowUtc =
            new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc);
        private PlayerPrefsTestStateScope _playerPrefsState;

        [SetUp]
        public void SetUp()
        {
            _playerPrefsState = PlayerPrefsTestStateScope.Capture(
                PlayerPrefsKeySpec.String(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey),
                PlayerPrefsKeySpec.Int(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey));
        }

        [TearDown]
        public void TearDown()
        {
            _playerPrefsState?.Dispose();
            _playerPrefsState = null;
        }

        [Test]
        public void Create_CreatesCurrentProfileServiceWithTempPathProvider()
        {
            using var harness = new FactoryHarness();

            var result = CampaignSaveServiceFactory.Create(harness.Options());
            var saveResult = result.Service.InitializeNewGame(1, "stage-1-1", "level-1");

            Assert.That(result.PathProvider.SaveRootPath, Is.EqualTo(harness.SaveRootPath));
            Assert.That(result.TextFileStore, Is.Not.Null);
            Assert.That(result.Repository, Is.Not.Null);
            Assert.That(result.Service, Is.Not.Null);
            Assert.That(saveResult.Succeeded, Is.True);
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
        }

        [Test]
        public void Create_AlwaysCreatesJsonSlotStoreAdapter()
        {
            using var harness = new FactoryHarness();

            var result = CampaignSaveServiceFactory.Create(harness.Options());

            Assert.That(result.SlotStore, Is.Not.Null);
            Assert.That(result.SlotStore, Is.TypeOf<CampaignSaveSlotStoreAdapter>());
        }

        [Test]
        public void MissingProfile_IsNormalNoSaveAndDoesNotReadPlayerPrefsProgression()
        {
            using var harness = new FactoryHarness();
            PlayerPrefs.SetString(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey, "not-current-json");
            PlayerPrefs.SetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 3);
            PlayerPrefs.Save();
            var result = CampaignSaveServiceFactory.Create(harness.Options());

            var slots = result.SlotStore.LoadAll();

            Assert.That(slots, Has.Length.EqualTo(CampaignSaveSlotPolicy.SlotCount));
            Assert.That(slots.All(slot => slot.IsEmpty), Is.True);
            Assert.That(result.SlotStore.LastCampaignLoadReport.Status,
                Is.EqualTo(CampaignSaveLoadStatus.Missing));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(PlayerPrefs.GetString(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey), Is.EqualTo("not-current-json"));
            Assert.That(PlayerPrefs.GetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey), Is.EqualTo(3));
        }

        [Test]
        public void Create_RequiresExplicitPathProvider()
        {
            Assert.That(
                () => CampaignSaveServiceFactory.Create(
                    new CampaignSaveServiceFactoryOptions()),
                Throws.ArgumentException);
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

            public string ProfilePath =>
                Path.Combine(SaveRootPath, FileCampaignProfileRepository.ProfileFileName);

            public CampaignSaveServiceFactoryOptions Options()
            {
                return new CampaignSaveServiceFactoryOptions
                {
                    PathProvider = _pathProvider,
                    ProductVersion = "factory-test-product",
                    ProfileId = "factory-test-profile",
                    UtcNow = () => FixedNowUtc,
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

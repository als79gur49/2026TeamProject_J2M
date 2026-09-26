using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSaveFacadeTests
    {
        [Test]
        public void Factory_CreatesProfileRepositoryServiceAndSlotFacade()
        {
            using var harness = new FacadeHarness();

            var result = CampaignSaveFacadeFactory.Create(harness.Options());

            Assert.That(result.CampaignSaveSlots, Is.TypeOf<CampaignSaveSlotStoreAdapter>());
            Assert.That(result.ProfileServices.Repository, Is.Not.Null);
            Assert.That(result.ProfileServices.Service, Is.Not.Null);
            Assert.That(result.ProfileServices.SlotStore,
                Is.SameAs(result.CampaignSaveSlots));
        }

        [Test]
        public void MissingProfile_ReturnsNormalEmptyStateWithoutCreatingAFile()
        {
            using var harness = new FacadeHarness();
            var result = CampaignSaveFacadeFactory.Create(harness.Options());

            var load = result.CampaignSaveSlots.LoadAllWithReport();

            Assert.That(load.Slots, Has.Length.EqualTo(CampaignSaveSlotPolicy.SlotCount));
            Assert.That(load.Slots.All(slot => slot.IsEmpty), Is.True);
            Assert.That(load.Report.Status, Is.EqualTo(CampaignSaveLoadStatus.Missing));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
        }

        [Test]
        public void CorruptProfile_FailsClosedAndDoesNotCreateANewProfile()
        {
            using var harness = new FacadeHarness();
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.ProfilePath, "{not-json");
            var result = CampaignSaveFacadeFactory.Create(harness.Options());

            var load = result.CampaignSaveSlots.LoadAllWithReport();

            Assert.That(load.Slots, Has.Length.EqualTo(CampaignSaveSlotPolicy.SlotCount));
            Assert.That(load.Slots.All(slot => slot.IsEmpty), Is.True);
            Assert.That(load.Report.Status,
                Is.EqualTo(CampaignSaveLoadStatus.CorruptRepairRequired));
            Assert.That(load.Report.BlocksCampaignAccess, Is.True);
        }

        [TestCase("duplicate-slot")]
        [TestCase("invalid-stage")]
        [TestCase("orphan-last-played")]
        public void StructurallyInvalidCurrentProfile_FailsClosedAcrossRepositoryAndMetadataProbe(
            string invalidShape)
        {
            using var harness = new FacadeHarness();
            var document = CreateValidProfileDocument();
            switch (invalidShape)
            {
                case "duplicate-slot":
                    document.Slots = new[]
                    {
                        CreateSlotDocument(1, "stage-1-1"),
                        CreateSlotDocument(1, "stage-1-2"),
                    };
                    break;
                case "invalid-stage":
                    document.Slots = new[] { CreateSlotDocument(1, string.Empty) };
                    break;
                case "orphan-last-played":
                    document.LastPlayedSlotNumber = 2;
                    break;
                default:
                    Assert.Fail($"Unknown invalid profile shape '{invalidShape}'.");
                    break;
            }

            harness.WriteProfile(document);
            var facade = CampaignSaveFacadeFactory.Create(harness.Options());

            Assert.That(
                facade.CampaignSaveSlots.LoadAllWithReport().Report.Status,
                Is.EqualTo(CampaignSaveLoadStatus.CorruptRepairRequired));
            Assert.That(
                new CampaignProfileMetadataProbe(harness.SaveRootPath).Probe().Status,
                Is.EqualTo(CampaignProfileMetadataProbeStatus.SchemaInvalid));
        }

        [Test]
        public void ProductionOptions_UseApplicationPersistentDataSavePathProvider()
        {
            var options = CampaignSaveCompositionProvider.CreateProductionProfileBackedOptions();

            Assert.That(options.PathProvider,
                Is.TypeOf<ApplicationPersistentDataSavePathProvider>());
        }

        [Test]
        public void ProductionComposition_ReusesProfileBackedRepairingStore()
        {
            CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
            try
            {
                var first = CampaignSaveCompositionProvider.CreateProductionProfileBacked();
                var second = CampaignSaveCompositionProvider.CreateProductionProfileBacked();

                Assert.That(first,
                    Is.TypeOf<CampaignLaunchStateRepairingCampaignSaveSlotStore>());
                Assert.That(second, Is.SameAs(first));
            }
            finally
            {
                CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
            }
        }

        private static CampaignProfileDocument CreateValidProfileDocument()
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = "facade-test-product",
                SavedAtUtc = "2026-08-23T00:00:00Z",
                ProfileId = "facade-test-profile",
                LastPlayedSlotNumber = 1,
                Slots = new[] { CreateSlotDocument(1, "stage-1-1") },
            };
        }

        private static CampaignSlotDocument CreateSlotDocument(int slotNumber, string stageId)
        {
            return new CampaignSlotDocument
            {
                GameMode = GameMode.Hardcore,
                SlotNumber = slotNumber,
                StageId = stageId,
                LevelGroupId = "level-1",
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                StageClearProfileSnapshot = new CampaignStageClearProfileDocument(),
            };
        }

        private sealed class FacadeHarness : IDisposable
        {
            private readonly TemporarySavePathProvider _pathProvider;

            public FacadeHarness()
            {
                SaveRootPath = Path.Combine(
                    "Temp",
                    "CampaignSaveFacadeTests",
                    Guid.NewGuid().ToString("N"));
                _pathProvider = new TemporarySavePathProvider(SaveRootPath);
            }

            public string SaveRootPath { get; }

            public string ProfilePath =>
                Path.Combine(SaveRootPath, FileCampaignProfileRepository.ProfileFileName);

            public CampaignSaveCompositionOptions Options()
            {
                return new CampaignSaveCompositionOptions
                {
                    PathProvider = _pathProvider,
                    ProductVersion = "facade-test-product",
                    ProfileId = "facade-test-profile",
                };
            }

            public void WriteProfile(CampaignProfileDocument document)
            {
                Directory.CreateDirectory(SaveRootPath);
                File.WriteAllText(ProfilePath, JsonUtility.ToJson(document));
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

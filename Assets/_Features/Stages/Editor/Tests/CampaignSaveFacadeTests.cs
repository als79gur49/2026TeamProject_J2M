using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSaveFacadeTests
    {
        private static readonly DateTime FixedNowUtc =
            new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void DefaultFactoryCreatesPlayerPrefsLegacyFacade()
        {
            var result = CampaignSaveFacadeFactory.Create();

            Assert.That(result.BackendMode, Is.EqualTo(CampaignSaveBackendMode.PlayerPrefsLegacy));
            Assert.That(result.CampaignSaveSlots, Is.TypeOf<SaveSlotStore>());
            Assert.That(result.CampaignSaveSlots.DiagnosticsKey, Is.EqualTo(SaveSlotPrefsKeys.SaveSlotsKey));
            Assert.That(result.ProfileServices, Is.Null);
            Assert.That(result.MigrationResult, Is.Null);
        }

        [Test]
        public void ExplicitProfileModeCreatesProfileBackedFacade()
        {
            using var harness = new Harness();

            var result = CampaignSaveFacadeFactory.Create(harness.Options(
                CampaignSaveBackendMode.ProfileJsonExplicit));

            Assert.That(result.BackendMode, Is.EqualTo(CampaignSaveBackendMode.ProfileJsonExplicit));
            Assert.That(result.CampaignSaveSlots, Is.TypeOf<SaveSlotStoreCompatibilityAdapter>());
            Assert.That(result.ProfileServices, Is.Not.Null);
            Assert.That(result.ProfileServices.MigrationOptions.EnableProfileWrite, Is.False);
        }

        [Test]
        public void ExplicitProfileModeImportsLegacyOnlyWhenProfileWriteEnabled()
        {
            using var harness = new Harness();
            WriteLegacyPayload(harness, CreateSlot(1, "stage-1-1", "level-1"));

            var deferred = CampaignSaveFacadeFactory.Create(harness.Options(
                CampaignSaveBackendMode.ProfileJsonExplicit,
                enableProfileWrite: false));

            Assert.That(deferred.MigrationResult.Status, Is.EqualTo(CampaignSaveMigrationStatus.MigrationDeferred));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(PlayerPrefs.HasKey(harness.LegacySourceKey), Is.True);

            var imported = CampaignSaveFacadeFactory.Create(harness.Options(
                CampaignSaveBackendMode.ProfileJsonExplicit,
                enableProfileWrite: true));

            Assert.That(imported.MigrationResult.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportSucceeded));
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
            Assert.That(imported.CampaignSaveSlots.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-1-1"));
            Assert.That(PlayerPrefs.HasKey(harness.LegacySourceKey), Is.True);
        }

        [Test]
        public void ValidProfileWinsOverStaleLegacyPlayerPrefs()
        {
            using var harness = new Harness();
            harness.Repository.Save(CampaignProfileDocumentMapper.ToDocument(
                new[] { CreateSlot(1, "stage-2-1", "level-2") },
                "facade-test-profile",
                1,
                "2026-07-09T00:00:00Z",
                "facade-test-product"));
            WriteLegacyPayload(harness, CreateSlot(1, "stage-1-1", "level-1"));

            var result = CampaignSaveFacadeFactory.Create(harness.Options(
                CampaignSaveBackendMode.ProfileJsonExplicit,
                enableProfileWrite: true));

            Assert.That(result.MigrationResult.Status, Is.EqualTo(CampaignSaveMigrationStatus.FileLoaded));
            Assert.That(result.CampaignSaveSlots.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(PlayerPrefs.HasKey(harness.LegacySourceKey), Is.True);
        }

        [Test]
        public void InvalidLegacyImportIsPreservedAndNotDeleted()
        {
            using var harness = new Harness();
            PlayerPrefs.SetString(harness.LegacySourceKey, "{not-json");
            PlayerPrefs.Save();

            var result = CampaignSaveFacadeFactory.Create(harness.Options(
                CampaignSaveBackendMode.ProfileJsonExplicit,
                enableProfileWrite: true));

            Assert.That(result.MigrationResult.Status, Is.EqualTo(CampaignSaveMigrationStatus.LegacyInvalid));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(PlayerPrefs.HasKey(harness.LegacySourceKey), Is.True);
        }

        [TestCase(CampaignProfileLoadStatus.CorruptNoFallback, CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.CorruptQuarantined, CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.SchemaInvalid, CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        public void ProfileBackedFacadeReportsRepairRequiredInsteadOfFreshEmpty(
            CampaignProfileLoadStatus profileStatus,
            CampaignSaveLoadStatus expectedStatus)
        {
            var adapter = new SaveSlotStoreCompatibilityAdapter(new CampaignSaveService(
                new StatusRepository(profileStatus),
                new NoOpResetMarkerPort(),
                () => "2026-07-09T00:00:00Z",
                "repair-test-profile",
                "repair-test-product"));

            var result = adapter.LoadAllWithReport();

            Assert.That(result.Report.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Report.RequiresRepair, Is.True);
            Assert.That(result.Slots, Has.Length.EqualTo(SaveSlotStore.SlotCount));
            Assert.That(result.Slots.All(slot => slot.IsEmpty), Is.True);
        }

        [Test]
        public void CampaignProfileDocumentDoesNotSerializePendingLaunchOrSettings()
        {
            var fields = typeof(CampaignProfileDocument)
                .GetFields()
                .Select(field => field.Name)
                .ToArray();

            Assert.That(fields, Does.Not.Contain("PendingLaunchSlotNumber"));
            Assert.That(fields, Does.Not.Contain("ActiveStageClearSaveSlot"));
            Assert.That(fields, Does.Not.Contain("AudioSettings"));
            Assert.That(fields, Does.Not.Contain("DisplaySettings"));
            Assert.That(fields, Does.Not.Contain("KeyboardBindingOverridesJson"));
            Assert.That(fields, Does.Not.Contain("DirectPlayTempSaveSlots"));
        }

        [Test]
        public void ProductionCallSitesAcceptCampaignSaveFacadeContract()
        {
            var facade = CampaignSaveFacadeFactory.Create().CampaignSaveSlots;
            Assert.That(facade, Is.InstanceOf<ICampaignSaveSlotStore>());

            Assert.That(
                ReadAssetText("_Features/UI/UI_Application/Runtime/MainMenuController.cs"),
                Does.Contain("ICampaignSaveSlotStore"));
            Assert.That(
                ReadAssetText("_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Contain("ICampaignSaveSlotStore"));
            Assert.That(
                ReadAssetText("_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs"),
                Does.Contain("ICampaignSaveSlotStore"));
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/SlotCinematicProgressStore.cs"),
                Does.Contain("ICampaignSaveSlotStore"));
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/CinematicStageLaunchRouter.cs"),
                Does.Contain("ICampaignSaveSlotStore"));
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/CinematicMainMenuReturnRouter.cs"),
                Does.Contain("ICampaignSaveSlotStore"));
            Assert.That(
                ReadAssetText("_Features/Stages/Runtime/Campaign/SaveSlotValidationService.cs"),
                Does.Contain("ICampaignSaveSlotStore"));
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Contain("CampaignSaveFacadeFactory.Create().CampaignSaveSlots"));
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"),
                Does.Contain("CampaignSaveFacadeFactory.Create().CampaignSaveSlots"));
        }

        private static void WriteLegacyPayload(Harness harness, params SaveSlotData[] slots)
        {
            PlayerPrefs.SetString(
                harness.LegacySourceKey,
                JsonUtility.ToJson(SaveSlotDtoMapper.ToDto(slots)));
            PlayerPrefs.SetInt(harness.LegacyActiveSlotKey, slots[0].SlotNumber);
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
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                LastPlayedAt = "2026-07-09T00:00:00Z",
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private static string ReadAssetText(string relativeAssetPath)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, relativeAssetPath));
        }

        private sealed class Harness : IDisposable
        {
            private readonly TemporarySavePathProvider _pathProvider;

            public Harness()
            {
                var id = Guid.NewGuid().ToString("N");
                SaveRootPath = Path.Combine("Temp", "CampaignSaveFacadeTests", id);
                _pathProvider = new TemporarySavePathProvider(SaveRootPath);
                Repository = new FileCampaignProfileRepository(new AtomicTextFileStore(SaveRootPath));
                LegacySourceKey = "CampaignSaveFacadeTests.SaveSlots." + id;
                LegacyActiveSlotKey = "CampaignSaveFacadeTests.ActiveSlot." + id;
                LegacyMarkerStore = new CampaignLegacyImportMarkerStore(
                    "CampaignSaveFacadeTests.ImportDisabled." + id,
                    "CampaignSaveFacadeTests.ImportedSourceHash." + id,
                    "CampaignSaveFacadeTests.ResetTombstoneUtc." + id,
                    "CampaignSaveFacadeTests.DeletedSlotGuards." + id);
            }

            public string SaveRootPath { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, FileCampaignProfileRepository.ProfileFileName);

            public FileCampaignProfileRepository Repository { get; }

            public string LegacySourceKey { get; }

            public string LegacyActiveSlotKey { get; }

            public CampaignLegacyImportMarkerStore LegacyMarkerStore { get; }

            public CampaignSaveCompositionOptions Options(
                CampaignSaveBackendMode backendMode,
                bool enableProfileWrite = false,
                bool allowLegacyImport = true)
            {
                return new CampaignSaveCompositionOptions
                {
                    BackendMode = backendMode,
                    PathProvider = _pathProvider,
                    ProductVersion = "facade-test-product",
                    ProfileId = "facade-test-profile",
                    UtcNow = () => FixedNowUtc,
                    EnableProfileWrite = enableProfileWrite,
                    AllowLegacyImport = allowLegacyImport,
                    LegacyCampaignSourceKey = LegacySourceKey,
                    LegacyActiveSlotKey = LegacyActiveSlotKey,
                    LegacyImportMarkerStore = LegacyMarkerStore,
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

        private sealed class StatusRepository : ICampaignProfileRepository
        {
            private readonly CampaignProfileLoadStatus _status;

            public StatusRepository(CampaignProfileLoadStatus status)
            {
                _status = status;
            }

            public CampaignProfileLoadResult Load()
            {
                return new CampaignProfileLoadResult(_status, null, _status.ToString());
            }

            public void Save(CampaignProfileDocument document)
            {
                throw new InvalidOperationException("Repair-state test must not write a profile.");
            }
        }

        private sealed class NoOpResetMarkerPort : ICampaignSaveResetMarkerPort
        {
            public void MarkResetImportDisabled(string resetTombstoneUtc)
            {
            }
        }
    }
}

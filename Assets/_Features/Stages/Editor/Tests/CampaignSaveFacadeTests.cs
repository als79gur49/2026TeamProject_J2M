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
        public void ProductionProvider_DefaultsToProfileJsonExplicit()
        {
            var options = CampaignSaveCompositionProvider.CreateProductionProfileBackedOptions();

            Assert.That(options.BackendMode, Is.EqualTo(CampaignSaveBackendMode.ProfileJsonExplicit));
            Assert.That(options.EnableProfileWrite, Is.True);
            Assert.That(options.AllowLegacyImport, Is.True);
            Assert.That(options.PreservePlayerPrefsSource, Is.True);
        }

        [Test]
        public void ProductionProvider_EnablesProfileWriteAndLegacyImportRetention()
        {
            var options = CampaignSaveCompositionProvider.CreateProductionProfileBackedOptions();

            Assert.That(options.EnableProfileWrite, Is.True);
            Assert.That(options.AllowLegacyImport, Is.True);
            Assert.That(options.PreservePlayerPrefsSource, Is.True);
        }

        [Test]
        public void ProductionProvider_UsesApplicationPersistentDataSavesProfileJson()
        {
            var options = CampaignSaveCompositionProvider.CreateProductionProfileBackedOptions();

            Assert.That(options.PathProvider, Is.TypeOf<ApplicationPersistentDataSavePathProvider>());
            Assert.That(
                options.PathProvider.GetSaveFilePath(FileCampaignProfileRepository.ProfileFileName),
                Is.EqualTo(Path.Combine(Application.persistentDataPath, "Saves", "profile.json")));
        }

        [Test]
        public void ProductionProvider_PathProviderRejectsArbitraryProductionPath()
        {
            var options = CampaignSaveCompositionProvider.CreateProductionProfileBackedOptions();

            Assert.That(options.PathProvider, Is.Not.TypeOf<TemporarySavePathProvider>());
            Assert.That(
                options.PathProvider.GetSaveFilePath(FileCampaignProfileRepository.ProfileFileName),
                Does.Not.StartWith("Temp"));
        }

        [Test]
        public void RollbackProvider_CanCreatePlayerPrefsLegacy()
        {
            var options = CampaignSaveCompositionProvider.CreateProductionLegacyRollbackOptions();
            var store = CampaignSaveCompositionProvider.CreateProductionLegacyRollback();

            Assert.That(options.BackendMode, Is.EqualTo(CampaignSaveBackendMode.PlayerPrefsLegacy));
            Assert.That(store, Is.TypeOf<SaveSlotStore>());
            Assert.That(store.DiagnosticsKey, Is.EqualTo(SaveSlotPrefsKeys.SaveSlotsKey));
        }

        [Test]
        public void ProductionRollbackProvider_UsesPlayerPrefsLegacyOnly()
        {
            var options = CampaignSaveCompositionProvider.CreateProductionLegacyRollbackOptions();

            Assert.That(options.BackendMode, Is.EqualTo(CampaignSaveBackendMode.PlayerPrefsLegacy));
            Assert.That(options.PathProvider, Is.Null);
            Assert.That(options.EnableProfileWrite, Is.False);
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
            Assert.That(result.Report.BlocksCampaignAccess, Is.True);
            Assert.That(result.Slots, Has.Length.EqualTo(SaveSlotStore.SlotCount));
            Assert.That(result.Slots.All(slot => slot.IsEmpty), Is.True);
        }

        [TestCase(CampaignProfileLoadStatus.CorruptNoFallback, CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.CorruptQuarantined, CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.SchemaInvalid, CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignProfileLoadStatus.IoFailed, CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignProfileLoadStatus.Unauthorized, CampaignSaveLoadStatus.Unauthorized)]
        public void ProfileBackedFacadeReportsCampaignAccessBlocked(
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
            Assert.That(result.Report.BlocksCampaignAccess, Is.True);
            Assert.That(result.Slots, Has.Length.EqualTo(SaveSlotStore.SlotCount));
            Assert.That(result.Slots.All(slot => slot.IsEmpty), Is.True);
        }

        [Test]
        public void MissingNoLegacy_RemainsFreshEmptyReport()
        {
            var adapter = new SaveSlotStoreCompatibilityAdapter(new CampaignSaveService(
                new StatusRepository(CampaignProfileLoadStatus.Missing),
                new NoOpResetMarkerPort(),
                () => "2026-07-09T00:00:00Z",
                "repair-test-profile",
                "repair-test-product"));

            var result = adapter.LoadAllWithReport();

            Assert.That(result.Report.Status, Is.EqualTo(CampaignSaveLoadStatus.Missing));
            Assert.That(result.Report.RequiresRepair, Is.False);
            Assert.That(result.Report.BlocksCampaignAccess, Is.False);
            Assert.That(result.Slots, Has.Length.EqualTo(SaveSlotStore.SlotCount));
            Assert.That(result.Slots.All(slot => slot.IsEmpty), Is.True);
        }

        [Test]
        public void BackupRecovered_IsNotBlockingReport()
        {
            var document = CampaignProfileDocumentMapper.ToDocument(
                new[] { CreateSlot(1, "stage-1-1", "level-1") },
                "backup-profile",
                1,
                "2026-07-09T00:00:00Z",
                "facade-test-product");
            var adapter = new SaveSlotStoreCompatibilityAdapter(new CampaignSaveService(
                new StatusRepository(CampaignProfileLoadStatus.BackupRecovered, document),
                new NoOpResetMarkerPort(),
                () => "2026-07-09T00:00:00Z",
                "repair-test-profile",
                "repair-test-product"));

            var result = adapter.LoadAllWithReport();

            Assert.That(result.Report.Status, Is.EqualTo(CampaignSaveLoadStatus.BackupRecovered));
            Assert.That(result.Report.RequiresRepair, Is.False);
            Assert.That(result.Report.BlocksCampaignAccess, Is.False);
            Assert.That(result.Slots[0].IsEmpty, Is.False);
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
            var facade = CampaignSaveCompositionProvider.CreateProductionLegacyRollback();
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
                Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"),
                Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(
                ReadAssetText("_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
        }

        [Test]
        public void MainMenuGameplayCinematicAndValidationUseProviderBackedStore()
        {
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Contain("var saveSlotStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();"));
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Contain("new CinematicStageLaunchRouter("));
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"),
                Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(
                ReadAssetText("_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Contain("_saveSlotStore ??= CampaignSaveCompositionProvider.CreateProductionProfileBacked();"));
            Assert.That(
                ReadAssetText("_Features/Stages/Runtime/Campaign/SaveSlotValidationService.cs"),
                Does.Contain("ValidateAndSync(ICampaignSaveSlotStore saveSlotStore"));
        }

        [Test]
        public void ProductionConsumers_UseCampaignSaveCompositionProvider()
        {
            var mainMenuInstaller = ReadAssetText("_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");
            var mainMenuController = ReadAssetText("_Features/UI/UI_Application/Runtime/MainMenuController.cs");
            var gameplayInstaller = ReadAssetText("_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs");
            var gameplayFlow = ReadAssetText("_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs");
            var chancesReadSource = ReadAssetText("_Features/Gameplay/Gameplay_Host/Runtime/CampaignChancesReadSource.cs");
            var gameplayUiInstaller = ReadAssetText("_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var cinematicLaunch = ReadAssetText("_Features/UI/UI_Composition/Runtime/CinematicStageLaunchRouter.cs");
            var cinematicReturn = ReadAssetText("_Features/UI/UI_Composition/Runtime/CinematicMainMenuReturnRouter.cs");
            var directPlayLauncher = File.ReadAllText("Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs");

            Assert.That(mainMenuInstaller, Does.Contain("var saveSlotStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();"));
            Assert.That(mainMenuInstaller, Does.Contain("ImportStandaloneCampaignSaveSeed(saveSlotStore"));
            Assert.That(mainMenuInstaller, Does.Contain("new MainMenuController("));
            Assert.That(mainMenuInstaller, Does.Contain("saveSlotStore,"));
            Assert.That(mainMenuController, Does.Contain("_saveSlotStore.LoadAllWithReport()"));
            Assert.That(mainMenuController, Does.Contain("_saveSlotStore.InitializeNewGame("));
            Assert.That(mainMenuController, Does.Contain("_saveSlotStore.DeleteSlot(slotNumber)"));
            Assert.That(mainMenuController, Does.Contain("_saveSlotValidationService.ValidateAndSync(_saveSlotStore, slotNumber)"));

            Assert.That(gameplayInstaller, Does.Contain("_saveSlotStore ??= CampaignSaveCompositionProvider.CreateProductionProfileBacked();"));
            Assert.That(gameplayInstaller, Does.Contain("new SaveSlotCampaignChancesReadSource("));
            Assert.That(gameplayFlow, Does.Contain("ICampaignSaveSlotStore"));
            Assert.That(chancesReadSource, Does.Contain("ICampaignSaveSlotStore saveSlotStore"));

            Assert.That(gameplayUiInstaller, Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(cinematicLaunch, Does.Contain("ICampaignSaveSlotStore saveSlotStore"));
            Assert.That(cinematicLaunch, Does.Contain("new SlotCinematicProgressStore(saveSlotStore)"));
            Assert.That(cinematicReturn, Does.Contain("ICampaignSaveSlotStore saveSlotStore"));
            Assert.That(cinematicReturn, Does.Contain("new SlotCinematicProgressStore(saveSlotStore)"));
            Assert.That(
                ExtractSourceRange(directPlayLauncher, "private static void PrimeCampaignProductionSlot", "private static void ValidateCampaignStage"),
                Does.Contain("var saveStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();"));
        }

        [Test]
        public void ProductionCampaignPaths_DoNotWriteStageClearSaveSlotsPlayerPrefs()
        {
            AssertProductionBranchDoesNotUsePlayerPrefsCampaignStorage(
                "MainMenu production save-slot composition",
                ExtractSourceRange(
                    ReadAssetText("_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                    "private void BuildSaveSlotModule()",
                    "private void ImportStandaloneCampaignSaveSeed("));
            AssertProductionBranchDoesNotUsePlayerPrefsCampaignStorage(
                "MainMenu standalone seed import caller",
                ExtractSourceRange(
                    ReadAssetText("_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                    "private void ImportStandaloneCampaignSaveSeed(",
                    "private void BuildHubModule()"));
            AssertProductionBranchDoesNotUsePlayerPrefsCampaignStorage(
                "Gameplay production store branch",
                ExtractSourceRange(
                    ReadAssetText("_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                    "_saveSlotStore ??= CampaignSaveCompositionProvider.CreateProductionProfileBacked();",
                    "private int ValidateActiveSlotMatchesLaunchStage"));
            AssertProductionBranchDoesNotUsePlayerPrefsCampaignStorage(
                "Gameplay cinematic return router",
                ExtractSourceRange(
                    ReadAssetText("_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"),
                    "private IMainMenuReturnRouter CreateMainMenuReturnRouter()",
                    "private CinematicFlowCoordinator EnsureCinematicFlowCoordinator()"));
            AssertProductionBranchDoesNotUsePlayerPrefsCampaignStorage(
                "DirectPlay production overwrite branch",
                ExtractSourceRange(
                    File.ReadAllText("Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs"),
                    "private static void PrimeCampaignProductionSlot",
                    "private static void ValidateCampaignStage"));
        }

        [Test]
        public void ProductionCampaignPaths_DoNotInstantiatePlayerPrefsBackendOrDirectDefaultStore()
        {
            var productionFiles = new[]
            {
                "_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs",
                "_Features/UI/UI_Application/Runtime/MainMenuController.cs",
                "_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs",
                "_Features/UI/UI_Composition/Runtime/CinematicStageLaunchRouter.cs",
                "_Features/UI/UI_Composition/Runtime/CinematicMainMenuReturnRouter.cs",
                "_Features/UI/UI_Composition/Runtime/SlotCinematicProgressStore.cs",
                "_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs",
                "_Features/Gameplay/Gameplay_Host/Runtime/CampaignChancesReadSource.cs",
            };

            foreach (var path in productionFiles)
            {
                var source = ReadAssetText(path);
                Assert.That(source, Does.Not.Contain("new PlayerPrefsSaveSlotStorageBackend"), path);
                Assert.That(source, Does.Not.Contain("new SaveSlotStore()"), path);
                Assert.That(source, Does.Not.Contain("PlayerPrefs.SetString"), path);
                Assert.That(source, Does.Not.Contain("PlayerPrefs.DeleteKey"), path);
                Assert.That(source, Does.Not.Contain("SaveSlotPrefsKeys.SaveSlotsKey"), path);
            }
        }

        [Test]
        public void DirectPlayTemp_RemainsTempPlayerPrefsAllowlist()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs");
            var tempMethod = ExtractSourceRange(
                source,
                "private static void PrimeCampaignTempSlot",
                "private static void PrimeCampaignProductionSlot");
            var productionMethod = ExtractSourceRange(
                source,
                "private static void PrimeCampaignProductionSlot",
                "private static void ValidateCampaignStage");

            Assert.That(tempMethod, Does.Contain("EditorDirectPlayContextStore.TempSaveSlotStoreKey"));
            Assert.That(tempMethod, Does.Contain("EditorDirectPlayContextStore.TempActiveSlotProviderKey"));
            Assert.That(tempMethod, Does.Contain("new SaveSlotStore("));
            Assert.That(tempMethod, Does.Not.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(productionMethod, Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(productionMethod, Does.Not.Contain("EditorDirectPlayContextStore.TempSaveSlotStoreKey"));
            Assert.That(productionMethod, Does.Not.Contain("new SaveSlotStore("));
        }

        [Test]
        public void LegacyCampaignPlayerPrefsRead_IsImporterOnlyForProfileSwitch()
        {
            var importer = ReadAssetText("_Features/Stages/Runtime/Campaign/Save/LegacyPlayerPrefsCampaignImporter.cs");
            var provider = ReadAssetText("_Features/Stages/Runtime/Campaign/Save/CampaignSaveCompositionProvider.cs");

            Assert.That(importer, Does.Contain("PlayerPrefs.GetString"));
            Assert.That(importer, Does.Contain("SaveSlotPrefsKeys.SaveSlotsKey"));
            Assert.That(importer, Does.Not.Contain("PlayerPrefs.DeleteKey"));
            Assert.That(provider, Does.Contain("AllowLegacyImport = true"));
            Assert.That(provider, Does.Contain("PreservePlayerPrefsSource = true"));
        }

        [Test]
        public void CampaignProfileDocument_DoesNotSerializePendingLaunchSettingsInputOrDirectPlayKeys()
        {
            var document = CampaignProfileDocumentMapper.ToDocument(
                new[] { CreateSlot(1, "stage-1-1", "level-1") },
                "profile-exclusion",
                1,
                "2026-07-09T00:00:00Z",
                "facade-test-product");

            var json = JsonUtility.ToJson(document, prettyPrint: true);

            Assert.That(json, Does.Not.Contain("ActiveStageClearSaveSlot"));
            Assert.That(json, Does.Not.Contain("PendingLaunch"));
            Assert.That(json, Does.Not.Contain("settings.audio"));
            Assert.That(json, Does.Not.Contain("settings.display"));
            Assert.That(json, Does.Not.Contain("Game.Feature.Input"));
            Assert.That(json, Does.Not.Contain("DirectPlay.TempSaveSlots"));
            Assert.That(json, Does.Not.Contain("DirectPlay.TempActiveSaveSlot"));
            Assert.That(json, Does.Not.Contain("Game.Feature.Stages.SaveSlots"));
            Assert.That(json, Does.Not.Contain("Game.Feature.Stages.ActiveSaveSlot"));
            Assert.That(json, Does.Not.Contain("All1Shader"));
            Assert.That(json, Does.Not.Contain("Steam"));
            Assert.That(json, Does.Not.Contain("Cloud"));
            Assert.That(json, Does.Not.Contain("VDF"));
        }

        [Test]
        public void SteamApiCloudVdf_RemainAbsentFromProductionCampaignSaveSources()
        {
            foreach (var path in Directory.GetFiles(
                         "Assets/_Features/Stages/Runtime/Campaign",
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("Steamworks"), path);
                Assert.That(source, Does.Not.Contain("ISteamRemoteStorage"), path);
                Assert.That(source, Does.Not.Contain("SteamRemoteStorage"), path);
                Assert.That(source, Does.Not.Contain("RemoteStorage"), path);
                Assert.That(source, Does.Not.Contain(".vdf"), path);
            }
        }

        [Test]
        public void ProductionProviderSource_DoesNotDeleteLegacyPlayerPrefsCampaignKey()
        {
            var source = ReadAssetText("_Features/Stages/Runtime/Campaign/Save/CampaignSaveCompositionProvider.cs");

            Assert.That(source, Does.Not.Contain("PlayerPrefs.DeleteKey"));
            Assert.That(source, Does.Not.Contain("PlayerPrefs.SetString"));
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

        private static string ExtractSourceRange(string source, string startToken, string endToken)
        {
            var start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startToken);
            var end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endToken);
            return source.Substring(start, end - start);
        }

        private static void AssertProductionBranchDoesNotUsePlayerPrefsCampaignStorage(
            string branchName,
            string source)
        {
            Assert.That(source, Does.Not.Contain("PlayerPrefs.SetString"), branchName);
            Assert.That(source, Does.Not.Contain("PlayerPrefs.Save"), branchName);
            Assert.That(source, Does.Not.Contain("PlayerPrefs.DeleteKey"), branchName);
            Assert.That(source, Does.Not.Contain("PlayerPrefsSaveSlotStorageBackend"), branchName);
            Assert.That(source, Does.Not.Contain("new SaveSlotStore()"), branchName);
            Assert.That(source, Does.Not.Contain("SaveSlotPrefsKeys.SaveSlotsKey"), branchName);
            Assert.That(source, Does.Not.Contain("Game.Feature.Stages.StageClearSaveSlots"), branchName);
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
            private readonly CampaignProfileDocument _document;

            public StatusRepository(CampaignProfileLoadStatus status, CampaignProfileDocument document = null)
            {
                _status = status;
                _document = document;
            }

            public CampaignProfileLoadResult Load()
            {
                return new CampaignProfileLoadResult(_status, _document, _status.ToString());
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

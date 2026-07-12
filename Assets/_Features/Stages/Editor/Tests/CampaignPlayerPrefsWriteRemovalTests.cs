using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignPlayerPrefsWriteRemovalTests
    {
        private const string RollbackRetentionPolicyPath =
            "Docs/Architecture/Campaign-Save-Rollback-Retention-Policy.md";

        private static readonly DateTime FixedNowUtc =
            new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

        [TearDown]
        public void TearDown()
        {
            CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
        }

        [Test]
        public void ProductionCampaignPaths_DoNotWriteStageClearSaveSlotsPlayerPrefs()
        {
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/UI/UI_Application/Runtime/MainMenuController.cs");
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs");
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs");
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/Gameplay/Gameplay_Host/Runtime/CampaignChancesReadSource.cs");
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/UI/UI_Composition/Runtime/SlotCinematicProgressStore.cs");
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/UI/UI_Composition/Runtime/CinematicStageLaunchRouter.cs");
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/UI/UI_Composition/Runtime/CinematicMainMenuReturnRouter.cs");
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/Stages/Runtime/Campaign/SaveSlotValidationService.cs");
            AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(
                "_Features/DemoStageControl/Runtime/DemoStageControlBridges.cs");
        }

        [Test]
        public void ProductionCampaignPaths_DoNotDeleteStageClearSaveSlotsPlayerPrefs()
        {
            var directPlayProduction = ExtractSourceRange(
                File.ReadAllText("Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs"),
                "private static void PrimeCampaignProductionSlot",
                "private static void ValidateCampaignStage");

            Assert.That(directPlayProduction, Does.Not.Contain("PlayerPrefs.DeleteKey"));
            Assert.That(directPlayProduction, Does.Not.Contain("PlayerPrefs.Save"));
            Assert.That(directPlayProduction, Does.Not.Contain("SaveSlotPrefsKeys.SaveSlotsKey"));
            Assert.That(directPlayProduction, Does.Not.Contain("Game.Feature.Stages.StageClearSaveSlots"));
        }

        [Test]
        public void ProductionCampaignPaths_DoNotInstantiatePlayerPrefsBackendOrDirectDefaultStore()
        {
            var productionFiles = new[]
            {
                "_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs",
                "_Features/UI/UI_Application/Runtime/MainMenuController.cs",
                "_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs",
                "_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs",
                "_Features/Gameplay/Gameplay_Host/Runtime/CampaignChancesReadSource.cs",
                "_Features/UI/UI_Composition/Runtime/SlotCinematicProgressStore.cs",
                "_Features/UI/UI_Composition/Runtime/CinematicStageLaunchRouter.cs",
                "_Features/UI/UI_Composition/Runtime/CinematicMainMenuReturnRouter.cs",
                "_Features/Stages/Runtime/Campaign/SaveSlotValidationService.cs",
                "_Features/DemoStageControl/Runtime/DemoStageControlBridges.cs",
            };

            foreach (var path in productionFiles)
            {
                var source = ReadAssetText(path);
                Assert.That(source, Does.Not.Contain("new PlayerPrefsSaveSlotStorageBackend"), path);
                Assert.That(source, Does.Not.Contain("new SaveSlotStore()"), path);
                Assert.That(source, Does.Not.Contain("new SaveSlotStore("), path);
            }

            var stageBackedProductionBranch = ExtractSourceRange(
                ReadAssetText("_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                "_saveSlotStore ??= CampaignSaveCompositionProvider.CreateProductionProfileBacked();",
                "private int ValidateActiveSlotMatchesLaunchStage");
            Assert.That(stageBackedProductionBranch, Does.Not.Contain("new PlayerPrefsSaveSlotStorageBackend"));
            Assert.That(stageBackedProductionBranch, Does.Not.Contain("new SaveSlotStore()"));
            Assert.That(stageBackedProductionBranch, Does.Not.Contain("new SaveSlotStore("));
        }

        [Test]
        public void ProductionCampaignPaths_UseProviderBackedStore()
        {
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Contain("var saveSlotStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();"));
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Contain("ImportStandaloneCampaignSaveSeed(saveSlotStore"));
            Assert.That(
                ReadAssetText("_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"),
                Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(
                ReadAssetText("_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Contain("_saveSlotStore ??= CampaignSaveCompositionProvider.CreateProductionProfileBacked();"));
            Assert.That(
                ExtractSourceRange(
                    File.ReadAllText("Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs"),
                    "private static void PrimeCampaignProductionSlot",
                    "private static void ValidateCampaignStage"),
                Does.Contain("var saveStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();"));
        }

        [Test]
        public void LegacyCampaignPlayerPrefsRead_IsImporterOnlyForProfileSwitch()
        {
            var sourceReader = ExtractSourceRange(
                ReadAssetText("_Features/Stages/Runtime/Campaign/Save/LegacyPlayerPrefsCampaignImporter.cs"),
                "public sealed class CampaignLegacySourceReader",
                "public interface ICampaignLegacyImportMarkerStore");

            Assert.That(sourceReader, Does.Contain("PlayerPrefs.HasKey"));
            Assert.That(sourceReader, Does.Contain("PlayerPrefs.GetString"));
            Assert.That(sourceReader, Does.Contain("PlayerPrefs.GetInt"));
            Assert.That(sourceReader, Does.Contain("SaveSlotPrefsKeys.SaveSlotsKey"));
            Assert.That(sourceReader, Does.Not.Contain("PlayerPrefs.SetString"));
            Assert.That(sourceReader, Does.Not.Contain("PlayerPrefs.DeleteKey"));
            Assert.That(sourceReader, Does.Not.Contain("PlayerPrefs.Save"));
        }

        [Test]
        public void CampaignLegacyMarkerWrites_RemainExplicitAllowlist()
        {
            var markerStore = ExtractSourceRange(
                ReadAssetText("_Features/Stages/Runtime/Campaign/Save/LegacyPlayerPrefsCampaignImporter.cs"),
                "public sealed class CampaignLegacyImportMarkerStore",
                "public sealed class LegacyPlayerPrefsCampaignImporter");

            Assert.That(markerStore, Does.Contain("LegacyImportDisabled"));
            Assert.That(markerStore, Does.Contain("LegacyImportedSourceHash"));
            Assert.That(markerStore, Does.Contain("LegacyResetTombstoneUtc"));
            Assert.That(markerStore, Does.Contain("LegacyDeletedSlotGuards"));
            Assert.That(markerStore, Does.Contain("PlayerPrefs.SetInt"));
            Assert.That(markerStore, Does.Contain("PlayerPrefs.SetString"));
        }

        [Test]
        public void CampaignLegacyMarkerWrites_AreNotCountedAsStageClearSaveSlotsWrites()
        {
            var markerStore = ExtractSourceRange(
                ReadAssetText("_Features/Stages/Runtime/Campaign/Save/LegacyPlayerPrefsCampaignImporter.cs"),
                "public sealed class CampaignLegacyImportMarkerStore",
                "public sealed class LegacyPlayerPrefsCampaignImporter");

            Assert.That(markerStore, Does.Not.Contain("SaveSlotPrefsKeys.SaveSlotsKey"));
            Assert.That(markerStore, Does.Not.Contain("Game.Feature.Stages.StageClearSaveSlots"));
            Assert.That(markerStore, Does.Not.Contain("CampaignSourceKey"));
            Assert.That(markerStore, Does.Not.Contain("ActiveSlotKey"));
        }

        [Test]
        public void RollbackProvider_IsOnlyExplicitPlayerPrefsLegacyPath()
        {
            var provider = ReadAssetText("_Features/Stages/Runtime/Campaign/Save/CampaignSaveCompositionProvider.cs");
            var factory = ReadAssetText("_Features/Stages/Runtime/Campaign/Save/CampaignSaveServiceFactory.cs");
            var rollbackOptions = ExtractSourceRange(
                provider,
                "public static CampaignSaveCompositionOptions CreateProductionLegacyRollbackOptions()",
                "public static void ResetProductionProfileBackedForTests()");

            Assert.That(rollbackOptions, Does.Contain("BackendMode = CampaignSaveBackendMode.PlayerPrefsLegacy"));
            Assert.That(rollbackOptions, Does.Not.Contain("ProfileJsonExplicit"));
            Assert.That(factory, Does.Contain("case CampaignSaveBackendMode.PlayerPrefsLegacy:"));
            Assert.That(factory, Does.Contain("new SaveSlotStore()"));
        }

        [Test]
        public void RollbackRetentionPolicy_DocumentsHybridWindowAndCleanupGate()
        {
            var policy = ReadRollbackRetentionPolicy();

            Assert.That(policy, Does.Contain("profile.json"));
            Assert.That(policy, Does.Contain("StageClearSaveSlots"));
            Assert.That(policy, Does.Contain("legacy import source"));
            Assert.That(policy, Does.Contain("2 profile-backed public releases"));
            Assert.That(policy, Does.Contain("evidence gate"));
            Assert.That(policy, Does.Contain("cleanup/delete"));
            Assert.That(policy, Does.Contain("future investigation"));
            Assert.That(policy, Does.Contain("operator/dev fallback"));
            Assert.That(policy, Does.Contain("not user-facing continuity"));
        }

        [Test]
        public void RollbackProvider_IsOperatorDevFallback_NotUserFacingContinuity()
        {
            var policy = ReadRollbackRetentionPolicy();
            var provider = ReadAssetText("_Features/Stages/Runtime/Campaign/Save/CampaignSaveCompositionProvider.cs");
            var rollbackOptions = ExtractSourceRange(
                provider,
                "public static CampaignSaveCompositionOptions CreateProductionLegacyRollbackOptions()",
                "public static void ResetProductionProfileBackedForTests()");

            Assert.That(policy, Does.Contain("Rollback provider may show stale legacy data"));
            Assert.That(policy, Does.Contain("It must not be presented as a user-facing save continuity path"));
            Assert.That(policy, Does.Contain("operator/dev fallback"));
            Assert.That(rollbackOptions, Does.Contain("BackendMode = CampaignSaveBackendMode.PlayerPrefsLegacy"));
            Assert.That(rollbackOptions, Does.Not.Contain("PathProvider"));
            Assert.That(rollbackOptions, Does.Not.Contain("ProfileJsonExplicit"));
            Assert.That(rollbackOptions, Does.Not.Contain("EnableProfileWrite = true"));
        }

        [Test]
        public void MainMenu_NewGame_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(MainMenu_NewGame_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));
            var store = CreateProfileBackedStore(harness);

            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");

            var profile = ReadProfile(harness);
            Assert.That(profile.Slots[0].SlotNumber, Is.EqualTo(1));
            Assert.That(profile.Slots[0].StageId, Is.EqualTo("stage-0-1"));
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void MainMenuNewGame_ProfileOnly_DoesNotTouchDefaultStageClearSaveSlots()
        {
            using var defaultSaveSlotsBackup = PlayerPrefsStringBackup.Capture(SaveSlotPrefsKeys.SaveSlotsKey);
            using var harness = new Harness();
            var sentinel = WriteStageClearSentinel(SaveSlotPrefsKeys.SaveSlotsKey, nameof(MainMenuNewGame_ProfileOnly_DoesNotTouchDefaultStageClearSaveSlots));
            var store = CreateProfileBackedStore(harness);

            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");

            var profile = ReadProfile(harness);
            Assert.That(profile.Slots[0].SlotNumber, Is.EqualTo(1));
            Assert.That(profile.Slots[0].StageId, Is.EqualTo("stage-0-1"));
            AssertStageClearSentinelUnchanged(SaveSlotPrefsKeys.SaveSlotsKey, sentinel);
        }

        [Test]
        public void MainMenu_DeleteSlot_WritesProfileJsonGuard_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(MainMenu_DeleteSlot_WritesProfileJsonGuard_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));

            store.DeleteSlot(1);

            var profile = ReadProfile(harness);
            Assert.That(profile.Slots, Is.Empty);
            Assert.That(profile.LegacyImport.DeletedSlotGuards, Has.Length.EqualTo(1));
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void MainMenuDeleteSlot_ProfileOnly_DoesNotTouchDefaultStageClearSaveSlots()
        {
            using var defaultSaveSlotsBackup = PlayerPrefsStringBackup.Capture(SaveSlotPrefsKeys.SaveSlotsKey);
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");
            var sentinel = WriteStageClearSentinel(SaveSlotPrefsKeys.SaveSlotsKey, nameof(MainMenuDeleteSlot_ProfileOnly_DoesNotTouchDefaultStageClearSaveSlots));

            store.DeleteSlot(1);

            var profile = ReadProfile(harness);
            Assert.That(profile.Slots, Is.Empty);
            Assert.That(profile.LegacyImport.DeletedSlotGuards, Has.Length.EqualTo(1));
            AssertStageClearSentinelUnchanged(SaveSlotPrefsKeys.SaveSlotsKey, sentinel);
        }

        [Test]
        public void MainMenu_ClearAll_WritesProfileTombstone_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(MainMenu_ClearAll_WritesProfileTombstone_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));

            store.ClearAll();

            var profile = ReadProfile(harness);
            Assert.That(profile.Slots, Is.Empty);
            Assert.That(profile.LegacyImport.ImportDisabled, Is.True);
            Assert.That(profile.LegacyImport.ResetTombstoneUtc, Is.Not.Empty);
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void MainMenuClearAll_ProfileOnly_DoesNotTouchDefaultStageClearSaveSlots()
        {
            using var defaultSaveSlotsBackup = PlayerPrefsStringBackup.Capture(SaveSlotPrefsKeys.SaveSlotsKey);
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");
            var sentinel = WriteStageClearSentinel(SaveSlotPrefsKeys.SaveSlotsKey, nameof(MainMenuClearAll_ProfileOnly_DoesNotTouchDefaultStageClearSaveSlots));

            store.ClearAll();

            var profile = ReadProfile(harness);
            Assert.That(profile.Slots, Is.Empty);
            Assert.That(profile.LegacyImport.ImportDisabled, Is.True);
            Assert.That(profile.LegacyImport.ResetTombstoneUtc, Is.Not.Empty);
            AssertStageClearSentinelUnchanged(SaveSlotPrefsKeys.SaveSlotsKey, sentinel);
        }

        [Test]
        public void GameplayDeath_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(GameplayDeath_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));

            store.UpdateSlot(1, slot =>
            {
                slot.RemainingChances = 2;
                slot.TotalDeaths += 1;
                slot.LastPlayedAt = "2026-07-11T00:01:00Z";
            });

            var slot = ReadProfile(harness).Slots[0];
            Assert.That(slot.RemainingChances, Is.EqualTo(2));
            Assert.That(slot.TotalDeaths, Is.EqualTo(1));
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void GameplayChanceUpdate_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(GameplayChanceUpdate_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));

            store.UpdateSlot(1, slot => slot.RemainingChances = 1);

            Assert.That(ReadProfile(harness).Slots[0].RemainingChances, Is.EqualTo(1));
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void GameplayStageClear_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(GameplayStageClear_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));

            store.UpdateSlot(1, slot =>
            {
                slot.CurrentStageId = StageId.CreateOrThrow("stage-0-2");
                slot.CurrentLevelGroupId = "level-0";
                slot.LastPlayedAt = "2026-07-11T00:02:00Z";
            });

            var slot = ReadProfile(harness).Slots[0];
            Assert.That(slot.StageId, Is.EqualTo("stage-0-2"));
            Assert.That(slot.LevelGroupId, Is.EqualTo("level-0"));
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void GameplayStageClear_ProfileOnly_DoesNotTouchDefaultStageClearSaveSlots()
        {
            using var defaultSaveSlotsBackup = PlayerPrefsStringBackup.Capture(SaveSlotPrefsKeys.SaveSlotsKey);
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");
            var sentinel = WriteStageClearSentinel(SaveSlotPrefsKeys.SaveSlotsKey, nameof(GameplayStageClear_ProfileOnly_DoesNotTouchDefaultStageClearSaveSlots));

            store.UpdateSlot(1, slot =>
            {
                slot.CurrentStageId = StageId.CreateOrThrow("stage-0-2");
                slot.CurrentLevelGroupId = "level-0";
                slot.LastPlayedAt = "2026-07-11T00:02:00Z";
            });

            var slot = ReadProfile(harness).Slots[0];
            Assert.That(slot.StageId, Is.EqualTo("stage-0-2"));
            Assert.That(slot.LevelGroupId, Is.EqualTo("level-0"));
            AssertStageClearSentinelUnchanged(SaveSlotPrefsKeys.SaveSlotsKey, sentinel);
        }

        [Test]
        public void CinematicIntroFlag_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(CinematicIntroFlag_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));

            store.UpdateSlot(1, slot => slot.IntroPlayed = true);

            Assert.That(ReadProfile(harness).Slots[0].IntroPlayed, Is.True);
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void CinematicOutroFlag_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            store.InitializeNewGame(1, CreateResolver(), "2026-07-11T00:00:00Z");
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(CinematicOutroFlag_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));

            store.UpdateSlot(1, slot => slot.OutroPlayed = true);

            Assert.That(ReadProfile(harness).Slots[0].OutroPlayed, Is.True);
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void SaveSlotValidationSync_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            using var provider = CreateProvider("stage-0-1");
            var store = CreateProfileBackedStore(harness);
            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-0-1"),
                CurrentLevelGroupId = "stale-level",
                RemainingChances = 3,
            });
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(SaveSlotValidationSync_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));
            var validation = new SaveSlotValidationService(CreateResolver(), provider.Provider);

            var result = validation.ValidateAndSync(store, 1);

            Assert.That(result.Status, Is.EqualTo(SaveSlotValidationStatus.Valid));
            Assert.That(ReadProfile(harness).Slots[0].LevelGroupId, Is.EqualTo("level-0"));
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void SeedImport_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            using var provider = CreateProvider("stage-0-1");
            var seedPath = Path.Combine(harness.SaveRootPath, "seed.json");
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(
                seedPath,
                StandaloneCampaignSaveSeedImporter.BuildSeedJson(
                    StageId.CreateOrThrow("stage-0-1"),
                    2,
                    2));
            var store = CreateProfileBackedStore(harness);
            var activeSlotProvider = new ActiveSlotProvider(harness.ActiveSlotKey);
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(SeedImport_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));

            var imported = StandaloneCampaignSaveSeedImporter.TryImportSeedFile(
                seedPath,
                store,
                activeSlotProvider,
                CreateResolver(),
                provider.Provider,
                deleteAfterImport: false,
                out var result);

            Assert.That(imported, Is.True);
            Assert.That(result.Status, Is.EqualTo(StandaloneCampaignSaveSeedImportStatus.Imported));
            var slot = ReadProfile(harness).Slots[0];
            Assert.That(slot.SlotNumber, Is.EqualTo(2));
            Assert.That(slot.StageId, Is.EqualTo("stage-0-1"));
            Assert.That(slot.RemainingChances, Is.EqualTo(2));
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void SeedImport_ProfileOnly_DoesNotTouchDefaultStageClearSaveSlots()
        {
            using var defaultSaveSlotsBackup = PlayerPrefsStringBackup.Capture(SaveSlotPrefsKeys.SaveSlotsKey);
            using var harness = new Harness();
            using var provider = CreateProvider("stage-0-1");
            var seedPath = Path.Combine(harness.SaveRootPath, "seed.json");
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(
                seedPath,
                StandaloneCampaignSaveSeedImporter.BuildSeedJson(
                    StageId.CreateOrThrow("stage-0-1"),
                    2,
                    2));
            var store = CreateProfileBackedStore(harness);
            var activeSlotProvider = new ActiveSlotProvider(harness.ActiveSlotKey);
            var sentinel = WriteStageClearSentinel(SaveSlotPrefsKeys.SaveSlotsKey, nameof(SeedImport_ProfileOnly_DoesNotTouchDefaultStageClearSaveSlots));

            var imported = StandaloneCampaignSaveSeedImporter.TryImportSeedFile(
                seedPath,
                store,
                activeSlotProvider,
                CreateResolver(),
                provider.Provider,
                deleteAfterImport: false,
                out var result);

            Assert.That(imported, Is.True);
            Assert.That(result.Status, Is.EqualTo(StandaloneCampaignSaveSeedImportStatus.Imported));
            var slot = ReadProfile(harness).Slots[0];
            Assert.That(slot.SlotNumber, Is.EqualTo(2));
            Assert.That(slot.StageId, Is.EqualTo("stage-0-1"));
            Assert.That(slot.RemainingChances, Is.EqualTo(2));
            AssertStageClearSentinelUnchanged(SaveSlotPrefsKeys.SaveSlotsKey, sentinel);
        }

        [Test]
        public void DirectPlayProductionOverwrite_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var harness = new Harness();
            var store = CreateProfileBackedStore(harness);
            var sentinel = WriteStageClearSentinel(harness.LegacySourceKey, nameof(DirectPlayProductionOverwrite_WritesProfileJson_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));

            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 3,
                CurrentStageId = StageId.CreateOrThrow("stage-0-1"),
                CurrentLevelGroupId = "level-0",
                RemainingChances = 2,
                LastPlayedAt = "2026-07-11T00:03:00Z",
            });

            var slot = ReadProfile(harness).Slots[0];
            Assert.That(slot.SlotNumber, Is.EqualTo(3));
            Assert.That(slot.StageId, Is.EqualTo("stage-0-1"));
            Assert.That(slot.RemainingChances, Is.EqualTo(2));
            AssertStageClearSentinelUnchanged(harness.LegacySourceKey, sentinel);
        }

        [Test]
        public void DirectPlayTemp_WritesOnlyTempKeys_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs()
        {
            using var saveBackup = PlayerPrefsStringBackup.Capture(SaveSlotPrefsKeys.SaveSlotsKey);
            using var activeBackup = PlayerPrefsIntBackup.Capture(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            try
            {
                EditorDirectPlayContextStore.ClearTempDirectPlaySave();
                var sentinel = WriteStageClearSentinel(SaveSlotPrefsKeys.SaveSlotsKey, nameof(DirectPlayTemp_WritesOnlyTempKeys_AndDoesNotTouchStageClearSaveSlotsPlayerPrefs));
                var tempStore = new SaveSlotStore(
                    EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                    EditorDirectPlayContextStore.TempActiveSlotProviderKey);
                var activeSlotProvider = new ActiveSlotProvider(EditorDirectPlayContextStore.TempActiveSlotProviderKey);

                tempStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-0-1"),
                    CurrentLevelGroupId = "level-0",
                    RemainingChances = 2,
                });
                activeSlotProvider.SetActiveSlot(1);

                Assert.That(PlayerPrefs.HasKey(EditorDirectPlayContextStore.TempSaveSlotStoreKey), Is.True);
                Assert.That(PlayerPrefs.HasKey(EditorDirectPlayContextStore.TempActiveSlotProviderKey), Is.True);
                AssertStageClearSentinelUnchanged(SaveSlotPrefsKeys.SaveSlotsKey, sentinel);
            }
            finally
            {
                EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            }
        }

        [Test]
        public void RollbackProvider_UsesRetainedPlayerPrefsLegacyView()
        {
            using var saveBackup = PlayerPrefsStringBackup.Capture(SaveSlotPrefsKeys.SaveSlotsKey);
            using var activeBackup = PlayerPrefsIntBackup.Capture(SaveSlotPrefsKeys.ActiveSaveSlotKey);

            WriteLegacyPayload(
                SaveSlotPrefsKeys.SaveSlotsKey,
                SaveSlotPrefsKeys.ActiveSaveSlotKey,
                CreateSlot(1, "stage-0-1", "level-0"));

            var rollback = CampaignSaveCompositionProvider.CreateProductionLegacyRollback();

            Assert.That(rollback, Is.TypeOf<SaveSlotStore>());
            Assert.That(rollback.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-0-1"));
        }

        [Test]
        public void RollbackProvider_DoesNotBackfillProfileProgressIntoPlayerPrefs()
        {
            using var saveBackup = PlayerPrefsStringBackup.Capture(SaveSlotPrefsKeys.SaveSlotsKey);
            using var activeBackup = PlayerPrefsIntBackup.Capture(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            using var harness = new Harness();
            WriteLegacyPayload(
                SaveSlotPrefsKeys.SaveSlotsKey,
                SaveSlotPrefsKeys.ActiveSaveSlotKey,
                CreateSlot(1, "stage-0-1", "level-0"));
            var retainedLegacy = PlayerPrefs.GetString(SaveSlotPrefsKeys.SaveSlotsKey);
            var profileStore = CampaignSaveFacadeFactory.Create(harness.Options(
                CampaignSaveBackendMode.ProfileJsonExplicit,
                enableProfileWrite: true,
                legacySourceKey: SaveSlotPrefsKeys.SaveSlotsKey,
                legacyActiveSlotKey: SaveSlotPrefsKeys.ActiveSaveSlotKey)).CampaignSaveSlots;

            profileStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-0-2"),
                CurrentLevelGroupId = "level-0",
                RemainingChances = 2,
            });

            Assert.That(ReadProfile(harness).Slots[0].StageId, Is.EqualTo("stage-0-2"));
            Assert.That(PlayerPrefs.GetString(SaveSlotPrefsKeys.SaveSlotsKey), Is.EqualTo(retainedLegacy));
            Assert.That(
                CampaignSaveCompositionProvider.CreateProductionLegacyRollback().LoadSlot(1).CurrentStageId.Value,
                Is.EqualTo("stage-0-1"));
        }

        [Test]
        public void RollbackProvider_DoesNotDeleteOrModifyProfileJson()
        {
            using var saveBackup = PlayerPrefsStringBackup.Capture(SaveSlotPrefsKeys.SaveSlotsKey);
            using var activeBackup = PlayerPrefsIntBackup.Capture(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            using var harness = new Harness();
            var profileStore = CreateProfileBackedStore(harness);
            profileStore.SaveSlot(CreateSlot(1, "stage-0-2", "level-0"));
            var before = File.ReadAllText(harness.ProfilePath);

            var rollback = CampaignSaveCompositionProvider.CreateProductionLegacyRollback();
            rollback.SaveSlot(CreateSlot(1, "stage-0-1", "level-0"));

            Assert.That(rollback.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-0-1"));
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
            Assert.That(File.ReadAllText(harness.ProfilePath), Is.EqualTo(before));
        }

        [Test]
        public void RollbackProvider_MayShowRetainedLegacyView()
        {
            var policy = ReadRollbackRetentionPolicy();

            Assert.That(policy, Does.Contain("Rollback provider may show stale legacy data"));
            Assert.That(policy, Does.Contain("This is expected"));
            Assert.That(policy, Does.Contain("Does not backfill profile-era progress into PlayerPrefs"));
        }

        [Test]
        public void RollbackProvider_IsOperatorFallbackNotDataContinuityPath()
        {
            var provider = ReadAssetText("_Features/Stages/Runtime/Campaign/Save/CampaignSaveCompositionProvider.cs");

            Assert.That(provider, Does.Contain("CreateProductionLegacyRollback"));
            Assert.That(provider, Does.Not.Contain("profile.json"));
            Assert.That(provider, Does.Not.Contain("DeleteKey"));
            Assert.That(provider, Does.Not.Contain("migrate"));
        }

        [Test]
        public void MarkerRemoval_IsNotAllowedBeforeRetentionWindow()
        {
            var policy = ReadRollbackRetentionPolicy();

            Assert.That(policy, Does.Contain("CampaignProfile.Legacy*"));
            Assert.That(policy, Does.Contain("Game.Feature.Stages.CampaignProfile.LegacyImportDisabled"));
            Assert.That(policy, Does.Contain("Game.Feature.Stages.CampaignProfile.LegacyImportedSourceHash"));
            Assert.That(policy, Does.Contain("Game.Feature.Stages.CampaignProfile.LegacyResetTombstoneUtc"));
            Assert.That(policy, Does.Contain("Game.Feature.Stages.CampaignProfile.LegacyDeletedSlotGuards"));
            Assert.That(policy, Does.Contain("Marker removal is not ready"));
            Assert.That(policy, Does.Contain("ClearAll remigration"));
            Assert.That(policy, Does.Contain("DeleteSlot resurrection"));
            Assert.That(policy, Does.Contain("separate future slice"));
        }

        private static void AssertProductionSourceDoesNotTouchCampaignPlayerPrefs(string relativeAssetPath)
        {
            var source = ReadAssetText(relativeAssetPath);
            Assert.That(source, Does.Not.Contain("PlayerPrefs.SetString"), relativeAssetPath);
            Assert.That(source, Does.Not.Contain("PlayerPrefs.DeleteKey"), relativeAssetPath);
            Assert.That(source, Does.Not.Contain("PlayerPrefs.Save"), relativeAssetPath);
            Assert.That(source, Does.Not.Contain("PlayerPrefsSaveSlotStorageBackend"), relativeAssetPath);
            Assert.That(source, Does.Not.Contain("SaveSlotPrefsKeys.SaveSlotsKey"), relativeAssetPath);
            Assert.That(source, Does.Not.Contain("Game.Feature.Stages.StageClearSaveSlots"), relativeAssetPath);
        }

        private static void WriteLegacyPayload(string saveKey, string activeKey, params SaveSlotData[] slots)
        {
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(SaveSlotDtoMapper.ToDto(slots)));
            PlayerPrefs.SetInt(activeKey, slots[0].SlotNumber);
            PlayerPrefs.Save();
        }

        private static SaveSlotData CreateSlot(int slotNumber, string stageId, string levelGroupId)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow(stageId),
                CurrentLevelGroupId = levelGroupId,
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                LastPlayedAt = "2026-07-11T00:00:00Z",
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private static ICampaignSaveSlotStore CreateProfileBackedStore(Harness harness)
        {
            return CampaignSaveFacadeFactory.Create(harness.Options(
                CampaignSaveBackendMode.ProfileJsonExplicit,
                enableProfileWrite: true)).CampaignSaveSlots;
        }

        private static CampaignStageSequenceResolver CreateResolver()
        {
            return new CampaignStageSequenceResolver(
                CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
        }

        private static CampaignProfileDocument ReadProfile(Harness harness)
        {
            Assert.That(File.Exists(harness.ProfilePath), Is.True, harness.ProfilePath);
            return JsonUtility.FromJson<CampaignProfileDocument>(File.ReadAllText(harness.ProfilePath));
        }

        private static string WriteStageClearSentinel(string key, string scope)
        {
            var sentinel = "stage-clear-sentinel:" + scope + ":" + Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(key, sentinel);
            PlayerPrefs.Save();
            AssertStageClearSentinelUnchanged(key, sentinel);
            return sentinel;
        }

        private static void AssertStageClearSentinelUnchanged(string key, string sentinel)
        {
            Assert.That(PlayerPrefs.GetString(key, string.Empty), Is.EqualTo(sentinel), key);
        }

        private static ProviderHarness CreateProvider(params string[] stageIds)
        {
            var catalog = ScriptableObject.CreateInstance<StageCatalog>();
            var provider = ScriptableObject.CreateInstance<ScriptableObjectStageCatalogProvider>();
            var entries = new StageContentEntry[stageIds.Length];
            for (var i = 0; i < stageIds.Length; i++)
            {
                entries[i] = ScriptableObject.CreateInstance<StageContentEntry>();
                entries[i].AssignStageId(StageId.CreateOrThrow(stageIds[i]));
            }

            catalog.SetEntries(entries);
            provider.AssignCatalog(catalog);
            return new ProviderHarness(provider, catalog, entries);
        }

        private static string ReadAssetText(string relativeAssetPath)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, relativeAssetPath));
        }

        private static string ReadRollbackRetentionPolicy()
        {
            Assert.That(File.Exists(RollbackRetentionPolicyPath), Is.True, RollbackRetentionPolicyPath);
            return File.ReadAllText(RollbackRetentionPolicyPath);
        }

        private static string ExtractSourceRange(string source, string startToken, string endToken)
        {
            var start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startToken);
            var end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endToken);
            return source.Substring(start, end - start);
        }

        private sealed class Harness : IDisposable
        {
            private readonly TemporarySavePathProvider _pathProvider;
            private readonly string _importDisabledKey;
            private readonly string _importedSourceHashKey;
            private readonly string _resetTombstoneUtcKey;
            private readonly string _deletedSlotGuardsKey;

            public Harness()
            {
                var id = Guid.NewGuid().ToString("N");
                SaveRootPath = Path.Combine("Temp", "CampaignPlayerPrefsWriteRemovalTests", id);
                _pathProvider = new TemporarySavePathProvider(SaveRootPath);
                LegacySourceKey = "CampaignPlayerPrefsWriteRemovalTests.SaveSlots." + id;
                LegacyActiveSlotKey = "CampaignPlayerPrefsWriteRemovalTests.ActiveSlot." + id;
                ActiveSlotKey = "CampaignPlayerPrefsWriteRemovalTests.ActiveSlotProvider." + id;
                _importDisabledKey = "CampaignPlayerPrefsWriteRemovalTests.ImportDisabled." + id;
                _importedSourceHashKey = "CampaignPlayerPrefsWriteRemovalTests.ImportedSourceHash." + id;
                _resetTombstoneUtcKey = "CampaignPlayerPrefsWriteRemovalTests.ResetTombstoneUtc." + id;
                _deletedSlotGuardsKey = "CampaignPlayerPrefsWriteRemovalTests.DeletedSlotGuards." + id;
                LegacyMarkerStore = new CampaignLegacyImportMarkerStore(
                    _importDisabledKey,
                    _importedSourceHashKey,
                    _resetTombstoneUtcKey,
                    _deletedSlotGuardsKey);
            }

            public string SaveRootPath { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, FileCampaignProfileRepository.ProfileFileName);

            public string LegacySourceKey { get; }

            public string LegacyActiveSlotKey { get; }

            public string ActiveSlotKey { get; }

            public CampaignLegacyImportMarkerStore LegacyMarkerStore { get; }

            public CampaignSaveCompositionOptions Options(
                CampaignSaveBackendMode backendMode,
                bool enableProfileWrite = false,
                bool allowLegacyImport = true,
                string legacySourceKey = null,
                string legacyActiveSlotKey = null)
            {
                return new CampaignSaveCompositionOptions
                {
                    BackendMode = backendMode,
                    PathProvider = _pathProvider,
                    ProductVersion = "write-removal-test-product",
                    ProfileId = "write-removal-test-profile",
                    UtcNow = () => FixedNowUtc,
                    EnableProfileWrite = enableProfileWrite,
                    AllowLegacyImport = allowLegacyImport,
                    LegacyCampaignSourceKey = legacySourceKey ?? LegacySourceKey,
                    LegacyActiveSlotKey = legacyActiveSlotKey ?? LegacyActiveSlotKey,
                    LegacyImportMarkerStore = LegacyMarkerStore,
                };
            }

            public void Dispose()
            {
                if (Directory.Exists(SaveRootPath))
                {
                    Directory.Delete(SaveRootPath, recursive: true);
                }

                PlayerPrefs.DeleteKey(LegacySourceKey);
                PlayerPrefs.DeleteKey(LegacyActiveSlotKey);
                PlayerPrefs.DeleteKey(ActiveSlotKey);
                PlayerPrefs.DeleteKey(_importDisabledKey);
                PlayerPrefs.DeleteKey(_importedSourceHashKey);
                PlayerPrefs.DeleteKey(_resetTombstoneUtcKey);
                PlayerPrefs.DeleteKey(_deletedSlotGuardsKey);
                PlayerPrefs.Save();
            }
        }

        private sealed class TemporarySavePathProvider : SavePathProviderBase
        {
            public TemporarySavePathProvider(string saveRootPath)
                : base(saveRootPath)
            {
            }
        }

        private sealed class ProviderHarness : IDisposable
        {
            private readonly StageCatalog _catalog;
            private readonly StageContentEntry[] _entries;

            public ProviderHarness(
                ScriptableObjectStageCatalogProvider provider,
                StageCatalog catalog,
                StageContentEntry[] entries)
            {
                Provider = provider;
                _catalog = catalog;
                _entries = entries;
            }

            public ScriptableObjectStageCatalogProvider Provider { get; }

            public void Dispose()
            {
                for (var i = 0; i < _entries.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(_entries[i]);
                }

                UnityEngine.Object.DestroyImmediate(Provider);
                UnityEngine.Object.DestroyImmediate(_catalog);
            }
        }

        private sealed class PlayerPrefsStringBackup : IDisposable
        {
            private readonly string _key;
            private readonly bool _hadValue;
            private readonly string _value;

            private PlayerPrefsStringBackup(string key)
            {
                _key = key;
                _hadValue = PlayerPrefs.HasKey(key);
                _value = _hadValue ? PlayerPrefs.GetString(key, string.Empty) : string.Empty;
            }

            public static PlayerPrefsStringBackup Capture(string key)
            {
                return new PlayerPrefsStringBackup(key);
            }

            public void Dispose()
            {
                if (_hadValue)
                {
                    PlayerPrefs.SetString(_key, _value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(_key);
                }

                PlayerPrefs.Save();
            }
        }

        private sealed class PlayerPrefsIntBackup : IDisposable
        {
            private readonly string _key;
            private readonly bool _hadValue;
            private readonly int _value;

            private PlayerPrefsIntBackup(string key)
            {
                _key = key;
                _hadValue = PlayerPrefs.HasKey(key);
                _value = _hadValue ? PlayerPrefs.GetInt(key, 0) : 0;
            }

            public static PlayerPrefsIntBackup Capture(string key)
            {
                return new PlayerPrefsIntBackup(key);
            }

            public void Dispose()
            {
                if (_hadValue)
                {
                    PlayerPrefs.SetInt(_key, _value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(_key);
                }

                PlayerPrefs.Save();
            }
        }
    }
}

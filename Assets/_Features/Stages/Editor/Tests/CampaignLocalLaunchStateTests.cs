using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignLocalLaunchStateTests
    {
        private const string FixedNowUtc = "2026-07-12T00:00:00.0000000Z";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
            PlayerPrefs.Save();
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
        }

        [Test]
        public void LocalStateRepository_LoadsMissingAsEmpty()
        {
            using var harness = new Harness();

            var result = harness.Repository.Load();

            Assert.That(result.Status, Is.EqualTo(CampaignLocalLaunchStateLoadStatus.Missing));
            Assert.That(result.HasDocument, Is.False);
        }

        [Test]
        public void LocalStateRepository_ValidFileWinsOverPlayerPrefs()
        {
            using var harness = new Harness();
            harness.Profile.SaveSlot(CreateSlot(2));
            harness.Repository.SaveActiveSlot(2);
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.Save();

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out var activeSlotNumber), Is.True);
            Assert.That(activeSlotNumber, Is.EqualTo(2));
            Assert.That(PlayerPrefs.GetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(1));
        }

        [Test]
        public void LocalStateMigration_ImportsPlayerPrefsActiveSlotWhenLocalStateMissing()
        {
            using var harness = new Harness();
            harness.Profile.SaveSlot(CreateSlot(1));
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.Save();

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out var activeSlotNumber), Is.True);
            Assert.That(activeSlotNumber, Is.EqualTo(1));
            Assert.That(File.Exists(harness.LocalStatePath), Is.True);
            Assert.That(ReadLocalState(harness).campaign.activeSlotNumber, Is.EqualTo(1));
        }

        [Test]
        public void LocalStateMigration_DoesNotDeletePlayerPrefsActiveSlot()
        {
            using var harness = new Harness();
            harness.Profile.SaveSlot(CreateSlot(1));
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.Save();

            _ = harness.CreateStorage().TryGetActiveSlot(out _);

            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.True);
            Assert.That(PlayerPrefs.GetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(1));
        }

        [Test]
        public void LocalStateMigration_ValidatesActiveSlotAgainstProfileSlots()
        {
            using var harness = new Harness();
            harness.Profile.SaveSlot(CreateSlot(1));
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 2);
            PlayerPrefs.Save();

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(File.Exists(harness.LocalStatePath), Is.False);
            Assert.That(PlayerPrefs.GetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(2));
        }

        [Test]
        public void LocalStateCorrupt_DoesNotSilentlyFallbackToPlayerPrefs()
        {
            using var harness = new Harness();
            harness.Profile.SaveSlot(CreateSlot(1));
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.LocalStatePath, "{not-json");
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.Save();

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(PlayerPrefs.GetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(1));
            Assert.That(File.Exists(harness.LocalStatePath), Is.True);
        }

        [Test]
        public void LocalStateValidation_ClearsLoadedActiveSlotWhenProfileSlotIsEmpty()
        {
            using var harness = new Harness();
            harness.Repository.SaveActiveSlot(2);

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(ReadLocalState(harness).campaign.activeSlotNumber, Is.Zero);
        }

        [Test]
        public void DeleteSlot_ClearsLocalStateActiveSlotWhenDeleted()
        {
            using var harness = new Harness();
            harness.Profile.SaveSlot(CreateSlot(1));
            harness.Repository.SaveActiveSlot(1);
            var storage = harness.CreateStorage();
            var repairingStore = new ActiveSlotRepairingCampaignSaveSlotStore(harness.Profile, storage);

            repairingStore.DeleteSlot(1);

            Assert.That(harness.Profile.LoadSlot(1).IsEmpty, Is.True);
            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(ReadLocalState(harness).campaign.activeSlotNumber, Is.Zero);
        }

        [Test]
        public void DeleteSlot_KeepsLocalStateActiveSlotWhenDeletedSlotDiffers()
        {
            using var harness = new Harness();
            harness.Profile.SaveSlot(CreateSlot(1));
            harness.Profile.SaveSlot(CreateSlot(2));
            harness.Repository.SaveActiveSlot(2);
            var storage = harness.CreateStorage();
            var repairingStore = new ActiveSlotRepairingCampaignSaveSlotStore(harness.Profile, storage);

            repairingStore.DeleteSlot(1);

            Assert.That(storage.TryGetActiveSlot(out var activeSlotNumber), Is.True);
            Assert.That(activeSlotNumber, Is.EqualTo(2));
        }

        [Test]
        public void ClearAll_ClearsLocalStateActiveSlot()
        {
            using var harness = new Harness();
            harness.Profile.SaveSlot(CreateSlot(1));
            harness.Repository.SaveActiveSlot(1);
            var storage = harness.CreateStorage();
            var repairingStore = new ActiveSlotRepairingCampaignSaveSlotStore(harness.Profile, storage);

            repairingStore.ClearAll();

            Assert.That(harness.Profile.LoadAll().All(slot => slot.IsEmpty), Is.True);
            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(ReadLocalState(harness).campaign.activeSlotNumber, Is.Zero);
        }

        [Test]
        public void ProductionMainMenu_UsesLocalStateProviderForActiveSlot()
        {
            var mainMenuInstaller = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");
            var gameplayInstaller = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var stageInstaller = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs");

            Assert.That(mainMenuInstaller, Does.Contain("CreateProductionActiveSlotProvider(saveSlotStore)"));
            Assert.That(gameplayInstaller, Does.Contain("CreateProductionActiveSlotProvider(saveSlotStore)"));
            Assert.That(stageInstaller, Does.Contain("CreateProductionActiveSlotProvider(_saveSlotStore)"));
        }

        [Test]
        public void DirectPlayProductionSlot_WritesLocalStateActiveSlot_NotPlayerPrefsActiveSlot()
        {
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            using var harness = new Harness();
            var resolver = new CampaignStageSequenceResolver(
                CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
            var stageId = StageId.CreateOrThrow("stage-2-2");
            const int playerPrefsActiveSentinel = 3;
            const string playerPrefsSaveSentinel = "retained-stage-clear-save-slots-sentinel";
            PlayerPrefs.SetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, playerPrefsActiveSentinel);
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, playerPrefsSaveSentinel);
            PlayerPrefs.Save();
            var activeSlotProvider = new ActiveSlotProvider(harness.CreateStorage());

            StageEditorDirectPlayLauncher.PrimeCampaignProductionSlotForTests(
                stageId,
                resolver,
                remainingChances: 2,
                productionSlotNumber: 2,
                harness.Profile,
                activeSlotProvider);

            var slot = harness.Profile.LoadSlot(2);
            Assert.That(slot.CurrentStageId, Is.EqualTo(stageId));
            Assert.That(slot.CurrentLevelGroupId, Is.EqualTo("level-2"));
            Assert.That(slot.RemainingChances, Is.EqualTo(2));
            Assert.That(ReadLocalState(harness).campaign.activeSlotNumber, Is.EqualTo(2));
            Assert.That(PlayerPrefs.GetInt(SaveSlotPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(playerPrefsActiveSentinel));
            Assert.That(PlayerPrefs.GetString(SaveSlotPrefsKeys.SaveSlotsKey), Is.EqualTo(playerPrefsSaveSentinel));
            Assert.That(PlayerPrefs.HasKey(EditorDirectPlayContextStore.TempSaveSlotStoreKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(EditorDirectPlayContextStore.TempActiveSlotProviderKey), Is.False);
            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out var context), Is.True);
            Assert.That(context.Mode, Is.EqualTo(EditorDirectPlayMode.CampaignProductionSlot));
            Assert.That(context.SaveSlotStoreKey, Is.Empty);
            Assert.That(context.ActiveSlotProviderKey, Is.Empty);
        }

        [Test]
        public void ProfileDocument_DoesNotContainLocalLaunchState()
        {
            var fields = typeof(CampaignProfileDocument)
                .GetFields()
                .Select(field => field.Name)
                .ToArray();

            Assert.That(fields, Does.Not.Contain("activeSlotNumber"));
            Assert.That(fields, Does.Not.Contain("pendingLaunchSlotNumber"));
            Assert.That(fields, Does.Not.Contain("runningSlotNumber"));
            Assert.That(fields, Does.Not.Contain("LocalLaunchState"));
        }

        [Test]
        public void PendingLaunch_RemainsSessionOnly()
        {
            var localStateDocument = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignLocalLaunchStateDocument.cs");
            var pendingProvider = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Campaign/PendingLaunchSlotProvider.cs");

            Assert.That(localStateDocument, Does.Not.Contain("pendingLaunchSlotNumber"));
            Assert.That(localStateDocument, Does.Not.Contain("PendingLaunch"));
            Assert.That(pendingProvider, Does.Contain("IPendingLaunchSlotProvider"));
            Assert.That(pendingProvider, Does.Not.Contain("CampaignProfileDocument"));
        }

        [Test]
        public void RunningSlotContext_RemainsSessionOnly()
        {
            var localStateDocument = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignLocalLaunchStateDocument.cs");
            var saveSlotModels = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Campaign/SaveSlotModels.cs");

            Assert.That(localStateDocument, Does.Not.Contain("runningSlot"));
            Assert.That(saveSlotModels, Does.Contain("CampaignRunningSlotContext"));
            Assert.That(saveSlotModels, Does.Not.Contain("runningSlotNumber"));
        }

        [Test]
        public void DirectPlayTemp_DoesNotUseProductionLocalLaunchState()
        {
            var directPlayContext = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Load/EditorDirectPlayContextStore.cs");
            var directPlayLauncher = ReadRepoFile(
                "Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs");
            var localStateDocument = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignLocalLaunchStateDocument.cs");

            Assert.That(directPlayContext, Does.Contain("Game.Feature.Stages.DirectPlay.TempActiveSaveSlot"));
            Assert.That(directPlayLauncher, Does.Contain("EditorDirectPlayContextStore.TempActiveSlotProviderKey"));
            Assert.That(directPlayContext, Does.Not.Contain("local-launch-state.json"));
            Assert.That(directPlayLauncher, Does.Not.Contain("local-launch-state.json"));
            Assert.That(localStateDocument, Does.Not.Contain("DirectPlay"));
        }

        private static CampaignLocalLaunchStateDocument ReadLocalState(Harness harness)
        {
            return JsonUtility.FromJson<CampaignLocalLaunchStateDocument>(
                File.ReadAllText(harness.LocalStatePath));
        }

        private static SaveSlotData CreateSlot(int slotNumber)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                LastPlayedAt = FixedNowUtc,
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private sealed class Harness : IDisposable
        {
            private readonly string _testRootPath;
            private readonly TemporarySavePathProvider _pathProvider;

            public Harness()
            {
                _testRootPath = Path.Combine("Temp", "CampaignLocalLaunchStateTests", Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
                _pathProvider = new TemporarySavePathProvider(SaveRootPath);
                Repository = new FileCampaignLocalLaunchStateRepository(
                    new AtomicTextFileStore(SaveRootPath),
                    () => new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));
                Profile = new FakeCampaignSaveSlotStore();
            }

            public string SaveRootPath { get; }

            public string LocalStatePath =>
                _pathProvider.GetSaveFilePath(CampaignLocalLaunchStateRepository.FileName);

            public FileCampaignLocalLaunchStateRepository Repository { get; }

            public FakeCampaignSaveSlotStore Profile { get; }

            public LocalStateActiveSlotStorage CreateStorage()
            {
                return new LocalStateActiveSlotStorage(
                    Repository,
                    new PlayerPrefsActiveSlotStorage(SaveSlotPrefsKeys.ActiveSaveSlotKey),
                    Profile);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_testRootPath))
                    {
                        Directory.Delete(_testRootPath, recursive: true);
                    }
                }
                catch
                {
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

        private sealed class FakeCampaignSaveSlotStore : ICampaignSaveSlotStore
        {
            private readonly SaveSlotData[] _slots =
            {
                SaveSlotData.CreateEmpty(1),
                SaveSlotData.CreateEmpty(2),
                SaveSlotData.CreateEmpty(3),
            };

            public string DiagnosticsKey => "FakeCampaignSaveSlotStore";

            public CampaignSaveLoadReport LastCampaignLoadReport { get; private set; } =
                CampaignSaveLoadReport.Loaded("fake profile loaded.", "fake");

            public SaveSlotData[] LoadAll()
            {
                return _slots.Select(slot => slot.Clone()).ToArray();
            }

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                return new CampaignSaveLoadResult(LoadAll(), LastCampaignLoadReport);
            }

            public SaveSlotData LoadSlot(int slotNumber)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
                return _slots[slotNumber - 1].Clone();
            }

            public void SaveSlot(SaveSlotData slot)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slot.SlotNumber);
                _slots[slot.SlotNumber - 1] = slot.Clone();
            }

            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt)
            {
                var slot = SaveSlotData.CreateNewGame(slotNumber, sequenceResolver, lastPlayedAt);
                SaveSlot(slot);
                return slot.Clone();
            }

            public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
            {
                var slot = LoadSlot(slotNumber);
                mutation(slot);
                SaveSlot(slot);
            }

            public void DeleteSlot(int slotNumber)
            {
                _slots[slotNumber - 1] = SaveSlotData.CreateEmpty(slotNumber);
            }

            public void ClearAll()
            {
                for (var i = 0; i < _slots.Length; i++)
                {
                    _slots[i] = SaveSlotData.CreateEmpty(i + 1);
                }
            }
        }
    }
}

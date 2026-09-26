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
            try
            {
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
                EditorDirectPlayContextStore.ClearTemporaryCampaignState();
                CampaignLaunchHandoffSessionStore.ResetForTests();
                CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
            }
            finally
            {
                _playerPrefsState?.Dispose();
                _playerPrefsState = null;
            }
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
        public void LocalStateRepository_ValidFileIsCanonicalAndDoesNotTouchPlayerPrefs()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(2));
            harness.Repository.SaveActiveSlot(2);
            PlayerPrefs.SetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.Save();

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out var activeSlotNumber), Is.True);
            Assert.That(activeSlotNumber, Is.EqualTo(2));
            Assert.That(PlayerPrefs.GetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(1));
        }

        [Test]
        public void LocalStateMissing_DoesNotImportPlayerPrefsActiveSlot()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            PlayerPrefs.SetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.Save();

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(File.Exists(harness.LocalStatePath), Is.False);
            Assert.That(PlayerPrefs.GetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(1));
        }

        [Test]
        public void LocalStateMissing_DoesNotMutatePlayerPrefsActiveSlot()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            PlayerPrefs.SetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.Save();

            _ = harness.CreateStorage().TryGetActiveSlot(out _);

            Assert.That(PlayerPrefs.HasKey(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey), Is.True);
            Assert.That(PlayerPrefs.GetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(1));
        }

        [Test]
        public void LocalStateMissing_IgnoresInvalidPlayerPrefsActiveSlot()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            PlayerPrefs.SetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 2);
            PlayerPrefs.Save();

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(File.Exists(harness.LocalStatePath), Is.False);
            Assert.That(PlayerPrefs.GetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(2));
        }

        [Test]
        public void LocalStateCorrupt_DoesNotSilentlyFallbackToPlayerPrefs()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            Directory.CreateDirectory(harness.SaveRootPath);
            File.WriteAllText(harness.LocalStatePath, "{not-json");
            PlayerPrefs.SetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 1);
            PlayerPrefs.Save();

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(PlayerPrefs.GetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(1));
            Assert.That(File.Exists(harness.LocalStatePath), Is.True);
        }

        [Test]
        public void LocalStateCorrupt_WithValidBackupDoesNotFallback()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            harness.Repository.SaveActiveSlot(1);
            harness.Repository.SaveActiveSlot(1);
            var backupPath = harness.LocalStatePath + ".bak";
            Assert.That(File.Exists(backupPath), Is.True);
            var validBackup = File.ReadAllText(backupPath);
            File.WriteAllText(harness.LocalStatePath, "{not-json");

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(File.ReadAllText(harness.LocalStatePath), Is.EqualTo("{not-json"));
            Assert.That(File.ReadAllText(backupPath), Is.EqualTo(validBackup));
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

        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized)]
        [TestCase(CampaignSaveLoadStatus.RecoveryPending)]
        public void LocalStateValidation_ProfileAvailabilityIndeterminate_PreservesCommittedActive(
            CampaignSaveLoadStatus status)
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(2));
            harness.Repository.SaveActiveSlot(2);
            harness.Profile.SetLoadStatus(status);

            var storage = harness.CreateStorage();

            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(ReadLocalState(harness).campaign.activeSlotNumber, Is.EqualTo(2));
        }

        [Test]
        public void DeleteSlot_ClearsLocalStateActiveSlotWhenDeleted()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            harness.Repository.SaveActiveSlot(1);
            var storage = harness.CreateStorage();
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                harness.Profile,
                storage,
                CampaignLaunchHandoffSessionStore.Instance);

            repairingStore.DeleteSlot(1);

            Assert.That(harness.Profile.LoadSlot(1).IsEmpty, Is.True);
            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(ReadLocalState(harness).campaign.activeSlotNumber, Is.Zero);
        }

        [Test]
        public void DeleteSlot_KeepsLocalStateActiveSlotWhenDeletedSlotDiffers()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            harness.Profile.ImportSlotSeed(CreateSlot(2));
            harness.Repository.SaveActiveSlot(2);
            var storage = harness.CreateStorage();
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                harness.Profile,
                storage,
                CampaignLaunchHandoffSessionStore.Instance);

            repairingStore.DeleteSlot(1);

            Assert.That(storage.TryGetActiveSlot(out var activeSlotNumber), Is.True);
            Assert.That(activeSlotNumber, Is.EqualTo(2));
        }

        [Test]
        public void ClearAll_ClearsLocalStateActiveSlot()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            harness.Repository.SaveActiveSlot(1);
            var storage = harness.CreateStorage();
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                harness.Profile,
                storage,
                CampaignLaunchHandoffSessionStore.Instance);

            repairingStore.ClearAll();

            Assert.That(harness.Profile.LoadAll().All(slot => slot.IsEmpty), Is.True);
            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(ReadLocalState(harness).campaign.activeSlotNumber, Is.Zero);
        }

        [Test]
        public void DeleteSlot_RepairsMatchingActiveAndPendingIndependently()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            harness.Profile.ImportSlotSeed(CreateSlot(2));
            harness.Repository.SaveActiveSlot(2);
            var storage = harness.CreateStorage();
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                handoffStore.TryBegin(
                    1,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "delete-repair",
                    out _),
                Is.True);
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                harness.Profile,
                storage,
                handoffStore);

            repairingStore.DeleteSlot(1);

            Assert.That(handoffStore.TryPeek(out _), Is.False);
            Assert.That(storage.TryGetActiveSlot(out var activeSlotNumber), Is.True);
            Assert.That(activeSlotNumber, Is.EqualTo(2));
        }

        [Test]
        public void MaintenanceReplacement_RejectsEmptySlotWithoutChangingLaunchState()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            harness.Repository.SaveActiveSlot(1);
            var storage = harness.CreateStorage();
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                handoffStore.TryBegin(
                    1,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "empty-save-repair",
                    out _),
                Is.True);
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                harness.Profile,
                storage,
                handoffStore);

            Assert.Throws<ArgumentException>(() =>
                repairingStore.ImportSlotSeed(
                    CampaignSlotRawDataMapper.ToSeedImportRequest(
                        SaveSlotData.CreateEmpty(1))));

            Assert.That(harness.Profile.LoadSlot(1).IsEmpty, Is.False);
            Assert.That(storage.TryGetActiveSlot(out _), Is.True);
            Assert.That(handoffStore.TryPeek(out _), Is.True);
        }

        [Test]
        public void DeleteSlot_RepairsMatchingActiveAndPending()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            harness.Repository.SaveActiveSlot(1);
            var storage = harness.CreateStorage();
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                handoffStore.TryBegin(
                    1,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "empty-update-repair",
                    out _),
                Is.True);
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                harness.Profile,
                storage,
                handoffStore);

            repairingStore.DeleteSlot(1);

            Assert.That(harness.Profile.LoadSlot(1).IsEmpty, Is.True);
            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(handoffStore.TryPeek(out _), Is.False);
        }

        [Test]
        public void DeleteSlot_WhenInnerRejects_DoesNotRepairLaunchState()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            harness.Repository.SaveActiveSlot(1);
            harness.Profile.DeleteFailure = new IOException("Injected recovery-pending delete failure.");
            var storage = harness.CreateStorage();
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                handoffStore.TryBegin(
                    1,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "rejected-update",
                    out _),
                Is.True);
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                harness.Profile,
                storage,
                handoffStore);
            Assert.Throws<IOException>(() => repairingStore.DeleteSlot(1));

            Assert.That(storage.TryGetActiveSlot(out var activeSlotNumber), Is.True);
            Assert.That(activeSlotNumber, Is.EqualTo(1));
            Assert.That(handoffStore.TryPeek(out var pending), Is.True);
            Assert.That(pending.SlotNumber, Is.EqualTo(1));
        }

        [Test]
        public void ClearAll_ClearsActiveAndPending()
        {
            using var harness = new Harness();
            harness.Profile.ImportSlotSeed(CreateSlot(1));
            harness.Repository.SaveActiveSlot(1);
            var storage = harness.CreateStorage();
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                handoffStore.TryBegin(
                    1,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "clear-all-repair",
                    out _),
                Is.True);
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                harness.Profile,
                storage,
                handoffStore);

            repairingStore.ClearAll();

            Assert.That(storage.TryGetActiveSlot(out _), Is.False);
            Assert.That(handoffStore.TryPeek(out _), Is.False);
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
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
            using var harness = new Harness();
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var stageId = StageId.CreateOrThrow("stage-2-2");
            const int playerPrefsActiveSentinel = 3;
            const string playerPrefsSaveSentinel = "retained-stage-clear-save-slots-sentinel";
            PlayerPrefs.SetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, playerPrefsActiveSentinel);
            PlayerPrefs.SetString(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey, playerPrefsSaveSentinel);
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
            Assert.That(PlayerPrefs.GetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 0), Is.EqualTo(playerPrefsActiveSentinel));
            Assert.That(PlayerPrefs.GetString(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey), Is.EqualTo(playerPrefsSaveSentinel));
            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out var context), Is.True);
            Assert.That(context.Mode, Is.EqualTo(EditorDirectPlayMode.CampaignProductionSlot));
            Assert.That(context.UsesTemporaryCampaignState, Is.False);
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
            Assert.That(pendingProvider, Does.Contain("CampaignLaunchHandoffSessionStore"));
            Assert.That(pendingProvider, Does.Contain("RuntimeInitializeLoadType.SubsystemRegistration"));
            Assert.That(pendingProvider, Does.Not.Contain("CampaignProfileDocument"));
        }

        [Test]
        public void RunningSlotContext_RemainsSessionOnly()
        {
            var localStateDocument = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignLocalLaunchStateDocument.cs");
            var runningSlotContext = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Campaign/PendingLaunchSlotProvider.cs");

            Assert.That(localStateDocument, Does.Not.Contain("runningSlot"));
            Assert.That(runningSlotContext, Does.Contain("CampaignRunningSlotContext"));
            Assert.That(runningSlotContext, Does.Not.Contain("runningSlotNumber"));
        }

        [Test]
        public void DirectPlayTemp_UsesIsolatedCanonicalLocalLaunchState()
        {
            var directPlayContext = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Load/EditorDirectPlayContextStore.cs");
            var directPlayLauncher = ReadRepoFile(
                "Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs");
            var localStateDocument = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignLocalLaunchStateDocument.cs");

            Assert.That(directPlayContext, Does.Contain("UsesTemporaryCampaignState"));
            Assert.That(directPlayLauncher, Does.Contain("CreateTemporaryActiveSlotProvider"));
            Assert.That(localStateDocument, Does.Not.Contain("DirectPlay"));
        }

        private static CampaignLocalLaunchStateDocument ReadLocalState(Harness harness)
        {
            return JsonUtility.FromJson<CampaignLocalLaunchStateDocument>(
                File.ReadAllText(harness.LocalStatePath));
        }

        private static CampaignSlotSeedImportRequest CreateSlot(int slotNumber)
        {
            return new CampaignSlotSeedImportRequest(
                slotNumber,
                StageId.CreateOrThrow("stage-1-1"),
                "level-1",
                CampaignSaveSlotPolicy.DefaultRemainingChances,
                FixedNowUtc);
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

        private sealed class FakeCampaignSaveSlotStore : ICampaignSaveRuntime
        {
            private readonly CampaignSlotEntry[] _slots =
            {
                CampaignSlotStateFactory.CreateEmptyEntry(1),
                CampaignSlotStateFactory.CreateEmptyEntry(2),
                CampaignSlotStateFactory.CreateEmptyEntry(3),
            };

            public string DiagnosticsKey => "FakeCampaignSaveSlotStore";

            public CampaignSaveLoadReport LastCampaignLoadReport { get; private set; } =
                CampaignSaveLoadReport.Loaded("fake profile loaded.", "fake");

            public Exception DeleteFailure { get; set; }

            public CampaignSlotEntry[] LoadAll()
            {
                return (CampaignSlotEntry[])_slots.Clone();
            }

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                return new CampaignSaveLoadResult(
                    LoadAll(),
                    LastCampaignLoadReport);
            }

            public CampaignSlotEntry LoadSlot(int slotNumber)
            {
                CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
                return _slots[slotNumber - 1];
            }

            public CampaignContinuePreparationResult PrepareContinue(
                CampaignContinuePreparationCommand command)
            {
                var preparation = CampaignContinuePreparationPolicy.Evaluate(
                    LoadSlot(command.SlotNumber).State,
                    command);
                if (preparation.Succeeded && preparation.LevelGroupSynchronized)
                {
                    _slots[command.SlotNumber - 1] =
                        CampaignSlotEntry.Occupied(preparation.CommittedState);
                }

                return preparation;
            }

            public CampaignSlotState InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt, GameMode gameMode = GameMode.Hardcore)
            {
                var state = CampaignSlotStateFactory.CreateNewGame(
                    slotNumber,
                    sequenceResolver,
                    lastPlayedAt);
                _slots[slotNumber - 1] = CampaignSlotEntry.Occupied(state);
                return state;
            }

            public void DeleteSlot(int slotNumber)
            {
                if (DeleteFailure != null)
                {
                    throw DeleteFailure;
                }

                _slots[slotNumber - 1] =
                    CampaignSlotStateFactory.CreateEmptyEntry(slotNumber);
            }

            public void ClearAll()
            {
                for (var i = 0; i < _slots.Length; i++)
                {
                    _slots[i] = CampaignSlotStateFactory.CreateEmptyEntry(i + 1);
                }
            }

            public void MarkIntroComicCompleted(int slotNumber) =>
                throw new NotSupportedException();

            public void MarkOutroComicCompleted(int slotNumber) =>
                throw new NotSupportedException();

            public CampaignSlotState SetActiveStageForDiagnostics(
                int slotNumber,
                StageId stageId,
                string levelGroupId) => throw new NotSupportedException();

            public CampaignSlotState ImportSlotSeed(CampaignSlotSeedImportRequest request)
            {
                var state = CampaignSlotStateFactory.CreateImportedSeed(request);
                _slots[request.SlotNumber - 1] = CampaignSlotEntry.Occupied(state);
                return state;
            }

            public CampaignSurvivalCommitResult CommitSurvival(int slotNumber, CampaignSurvivalCommitRequest request) =>
                throw new NotSupportedException();

            public CampaignDeathCommitResult CommitDeath(
                int slotNumber,
                CampaignDeathTransitionPlan plan) => throw new NotSupportedException();

            public CampaignStageClearCommitResult CommitStageClear(
                int slotNumber,
                CampaignStageClearCommitRequest request) => throw new NotSupportedException();

            public void SetLoadStatus(CampaignSaveLoadStatus status)
            {
                LastCampaignLoadReport = new CampaignSaveLoadReport(
                    status,
                    "injected profile load status",
                    "fake");
            }
        }
    }
}

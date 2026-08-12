using System;
using System.IO;
using System.Linq;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class CampaignSaveRepairStateTests
    {
        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized)]
        public void BlockingLoad_DoesNotRenderFreshEmptySlots(CampaignSaveLoadStatus status)
        {
            var controller = CreateController(new RecordingSaveSlotStore(BlockedReport(status)));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards, Has.Count.EqualTo(SaveSlotStore.SlotCount));
            Assert.That(viewModel.SlotCards.All(card => card.State == SaveSlotCardState.Empty), Is.False);
            Assert.That(viewModel.SlotCards.All(card => card.State == SaveSlotCardState.Corrupted), Is.True);
        }

        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized)]
        public void BlockingLoad_RendersPrimaryIntentNone(CampaignSaveLoadStatus status)
        {
            var controller = CreateController(new RecordingSaveSlotStore(BlockedReport(status)));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards.All(card => card.PrimaryIntentKind == SaveSlotIntentKind.None), Is.True);
            Assert.That(viewModel.SlotCards.All(card => string.IsNullOrEmpty(card.PrimaryActionText)), Is.True);
        }

        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized)]
        public void BlockingLoad_HidesDelete(CampaignSaveLoadStatus status)
        {
            var controller = CreateController(new RecordingSaveSlotStore(BlockedReport(status)));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards.All(card => card.ShowDelete), Is.False);
        }

        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized)]
        public void BlockingLoad_DisablesContinue(CampaignSaveLoadStatus status)
        {
            var controller = CreateController(new RecordingSaveSlotStore(BlockedReport(status)));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards.Any(card => card.PrimaryIntentKind == SaveSlotIntentKind.Continue), Is.False);
        }

        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired, SaveSlotFailurePresentationKind.CorruptedData, "Save Data Damaged", "This save data could not be read.")]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired, SaveSlotFailurePresentationKind.NeedsRepair, "Save Data Unavailable", "This save cannot be used in its current state.")]
        [TestCase(CampaignSaveLoadStatus.IoFailed, SaveSlotFailurePresentationKind.LoadFailed, "Save Load Failed", "The save data could not be loaded.")]
        [TestCase(CampaignSaveLoadStatus.Unauthorized, SaveSlotFailurePresentationKind.PermissionDenied, "Save Access Failed", "The save data could not be accessed. Check file permissions.")]
        public void BlockingLoad_UsesStatusSpecificMessage(
            CampaignSaveLoadStatus status,
            SaveSlotFailurePresentationKind expectedKind,
            string expectedStatusText,
            string expectedDetailText)
        {
            var controller = CreateController(new RecordingSaveSlotStore(
                new CampaignSaveLoadReport(status, string.Empty, "CampaignProfileDocument")));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards.All(card => card.StatusText == expectedStatusText), Is.True);
            Assert.That(viewModel.SlotCards.All(card => card.StageText == expectedDetailText), Is.True);
            Assert.That(viewModel.SlotCards.All(card => card.FailureKind == expectedKind), Is.True);
        }

        [TestCase(CampaignSaveLoadStatus.Missing, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSaveLoadStatus.Loaded, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSaveLoadStatus.ImportedLegacy, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSaveLoadStatus.BackupRecovered, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired, SaveSlotFailurePresentationKind.CorruptedData)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired, SaveSlotFailurePresentationKind.NeedsRepair)]
        [TestCase(CampaignSaveLoadStatus.IoFailed, SaveSlotFailurePresentationKind.LoadFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized, SaveSlotFailurePresentationKind.PermissionDenied)]
        public void CampaignLoadStatus_MapsToTypedUiFailureKind(
            CampaignSaveLoadStatus status,
            SaveSlotFailurePresentationKind expected)
        {
            Assert.That(MainMenuSlotViewModelMapper.MapFailureKind(status), Is.EqualTo(expected));
        }

        [TestCase(SaveSlotValidationStatus.Empty, SaveSlotFailurePresentationKind.None)]
        [TestCase(SaveSlotValidationStatus.Valid, SaveSlotFailurePresentationKind.None)]
        [TestCase(SaveSlotValidationStatus.Completed, SaveSlotFailurePresentationKind.None)]
        [TestCase(SaveSlotValidationStatus.Corrupted, SaveSlotFailurePresentationKind.CorruptedData)]
        [TestCase(SaveSlotValidationStatus.UnsupportedVersion, SaveSlotFailurePresentationKind.UnsupportedVersion)]
        [TestCase(SaveSlotValidationStatus.StageMissingFromSequence, SaveSlotFailurePresentationKind.NeedsRepair)]
        [TestCase(SaveSlotValidationStatus.StageMissingFromCatalog, SaveSlotFailurePresentationKind.NeedsRepair)]
        public void SlotValidationStatus_MapsToTypedUiFailureKind(
            SaveSlotValidationStatus status,
            SaveSlotFailurePresentationKind expected)
        {
            Assert.That(MainMenuSlotViewModelMapper.MapFailureKind(status), Is.EqualTo(expected));
        }

        [TestCase(SaveSlotValidationStatus.UnsupportedVersion, SaveSlotFailurePresentationKind.UnsupportedVersion, "Unsupported Save", "This save was created by an unsupported version.")]
        [TestCase(SaveSlotValidationStatus.Corrupted, SaveSlotFailurePresentationKind.CorruptedData, "Save Data Damaged", "This save data could not be read.")]
        [TestCase(SaveSlotValidationStatus.StageMissingFromSequence, SaveSlotFailurePresentationKind.NeedsRepair, "Save Data Unavailable", "This save cannot be used in its current state.")]
        [TestCase(SaveSlotValidationStatus.StageMissingFromCatalog, SaveSlotFailurePresentationKind.NeedsRepair, "Save Data Unavailable", "This save cannot be used in its current state.")]
        public void InvalidSlot_UsesSafeLocalizedCopyAndOnlySupportedActions(
            SaveSlotValidationStatus status,
            SaveSlotFailurePresentationKind expectedKind,
            string expectedTitle,
            string expectedDetail)
        {
            var slot = CreateExistingSlot(1);
            slot.TotalDeaths = 99;
            slot.LastPlayedAt = "2026-07-30T01:23:45+09:00";
            var validation = new SaveSlotValidationResult(
                slot,
                status,
                "untrusted-level",
                levelGroupWasSynced: false);

            var card = MainMenuSlotViewModelMapper.MapSlot(
                slot,
                null,
                validation,
                PackageFreeLocalizedTextResolver.CreateSettingsDefault());

            Assert.That(card.FailureKind, Is.EqualTo(expectedKind));
            Assert.That(card.StatusText, Is.EqualTo(expectedTitle));
            Assert.That(card.StageText, Is.EqualTo(expectedDetail));
            Assert.That(card.ChancesText, Is.Empty);
            Assert.That(card.DeathsText, Is.Empty);
            Assert.That(card.LastPlayedText, Is.Empty);
            Assert.That(card.PrimaryActionText, Is.EqualTo("Restart"));
            Assert.That(card.PrimaryIntentKind, Is.EqualTo(SaveSlotIntentKind.Restart));
            Assert.That(card.ShowDelete, Is.True);
            Assert.That(card.DeleteActionText, Is.EqualTo("Delete"));
        }

        [Test]
        public void BlockingLoad_RawDiagnosticExceptionAndPathNeverReachCardText()
        {
            const string diagnostic =
                "UnauthorizedAccessException: C:\\Users\\Player\\Saves\\profile.json";
            var controller = CreateController(new RecordingSaveSlotStore(
                new CampaignSaveLoadReport(
                    CampaignSaveLoadStatus.Unauthorized,
                    diagnostic,
                    "CampaignProfileDocument")));

            var viewModel = controller.BuildViewModel();

            foreach (var card in viewModel.SlotCards)
            {
                var playerText = string.Join(
                    "\n",
                    card.TitleText,
                    card.StatusText,
                    card.StageText,
                    card.ChancesText,
                    card.DeathsText,
                    card.LastPlayedText,
                    card.PrimaryActionText,
                    card.DeleteActionText);
                Assert.That(playerText, Does.Not.Contain(diagnostic));
                Assert.That(playerText, Does.Not.Contain("UnauthorizedAccessException"));
                Assert.That(playerText, Does.Not.Contain("C:\\Users\\Player"));
            }
        }

        [Test]
        public void BlockingLoad_ForwardsTypedFailureAndRawReasonToDiagnosticPort()
        {
            const string diagnostic = "Access to profile.json was denied.";
            var diagnostics = new RecordingSaveDiagnosticPort();
            var controller = CreateController(
                new RecordingSaveSlotStore(new CampaignSaveLoadReport(
                    CampaignSaveLoadStatus.Unauthorized,
                    diagnostic,
                    "CampaignProfileDocument")),
                saveDiagnosticPort: diagnostics);

            controller.BuildViewModel();

            Assert.That(diagnostics.ReportCount, Is.EqualTo(1));
            Assert.That(diagnostics.Last.FailureKind, Is.EqualTo(SaveSlotFailurePresentationKind.PermissionDenied));
            Assert.That(diagnostics.Last.LoadStatus, Is.EqualTo(CampaignSaveLoadStatus.Unauthorized));
            Assert.That(diagnostics.Last.Reason, Is.EqualTo(diagnostic));
            Assert.That(diagnostics.Last.SlotNumber, Is.Zero);
            Assert.That(diagnostics.Last.Operation, Is.EqualTo(SaveSlotRepositoryOperation.LoadAllWithReport));
        }

        [Test]
        public void MissingNoLegacy_RemainsFreshEmpty()
        {
            var controller = CreateController(new RecordingSaveSlotStore(CampaignSaveLoadReport.Missing("missing")));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards.All(card => card.State == SaveSlotCardState.Empty), Is.True);
            Assert.That(viewModel.SlotCards.All(card => card.PrimaryIntentKind == SaveSlotIntentKind.NewGame), Is.True);
            Assert.That(viewModel.SlotCards.All(card => card.ShowDelete), Is.False);
        }

        [Test]
        public void BackupRecovered_IsNotBlocking()
        {
            var store = new RecordingSaveSlotStore(
                new CampaignSaveLoadReport(
                    CampaignSaveLoadStatus.BackupRecovered,
                    "backup recovered",
                    "CampaignProfileDocument"));
            store.SetSlot(CreateExistingSlot(1));
            var controller = CreateController(store);

            var viewModel = controller.BuildViewModel();

            Assert.That(store.LastCampaignLoadReport.BlocksCampaignAccess, Is.False);
            Assert.That(viewModel.SlotCards[0].State, Is.EqualTo(SaveSlotCardState.Existing));
            Assert.That(viewModel.SlotCards[0].PrimaryIntentKind, Is.EqualTo(SaveSlotIntentKind.Continue));
        }

        [Test]
        public void BlockingLoad_DoesNotInitializeNewGame()
        {
            var store = new RecordingSaveSlotStore(BlockedReport(CampaignSaveLoadStatus.IoFailed));
            var router = new RecordingStageLaunchRouter();
            var pending = new RecordingCampaignLaunchHandoffStore();
            var controller = CreateController(store, router: router, launchHandoffStore: pending);

            controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));

            Assert.That(store.InitializeNewGameCount, Is.Zero);
            Assert.That(store.ProfileWriteCount, Is.Zero);
            Assert.That(router.LaunchCount, Is.Zero);
            Assert.That(pending.BeginCount, Is.Zero);
        }

        [Test]
        public void BlockingLoad_DoesNotLaunchContinue()
        {
            var store = new RecordingSaveSlotStore(BlockedReport(CampaignSaveLoadStatus.Unauthorized));
            var router = new RecordingStageLaunchRouter();
            var pending = new RecordingCampaignLaunchHandoffStore();
            var controller = CreateController(store, router: router, launchHandoffStore: pending);

            controller.Continue(1);

            Assert.That(store.LoadSlotCount, Is.Zero);
            Assert.That(store.InitializeNewGameCount, Is.Zero);
            Assert.That(router.LaunchCount, Is.Zero);
            Assert.That(pending.BeginCount, Is.Zero);
        }

        [Test]
        public void BlockingLoad_DoesNotDeleteSlot()
        {
            var store = new RecordingSaveSlotStore(BlockedReport(CampaignSaveLoadStatus.CorruptRepairRequired));
            var confirmPort = new RecordingConfirmPopupPort();
            var controller = CreateController(store, confirmPopupPort: confirmPort);

            controller.RequestDelete(1);

            Assert.That(confirmPort.RequestCount, Is.Zero);
            Assert.That(store.DeleteSlotCount, Is.Zero);
            Assert.That(store.ProfileWriteCount, Is.Zero);
        }

        [Test]
        public void BlockingConfirmedDeletePath_DoesNotDeleteSlot()
        {
            var store = new RecordingSaveSlotStore(CampaignSaveLoadReport.Loaded("loaded", "test"));
            store.SetSlot(CreateExistingSlot(1));
            var confirmPort = new RecordingConfirmPopupPort();
            var controller = CreateController(store, confirmPopupPort: confirmPort);

            controller.RequestDelete(1);
            store.Report = BlockedReport(CampaignSaveLoadStatus.IoFailed);
            confirmPort.Complete(true);

            Assert.That(confirmPort.RequestCount, Is.EqualTo(1));
            Assert.That(store.DeleteSlotCount, Is.Zero);
            Assert.That(store.ProfileWriteCount, Is.Zero);
        }

        [Test]
        public void BlockingLoad_DoesNotClearAll()
        {
            var store = new RecordingSaveSlotStore(BlockedReport(CampaignSaveLoadStatus.SchemaInvalidRepairRequired));
            var controller = CreateController(store);

            controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));
            controller.RequestDelete(1);
            controller.RequestRestart(1);

            Assert.That(store.ClearAllCount, Is.Zero);
            Assert.That(store.ProfileWriteCount, Is.Zero);
        }

        [Test]
        public void BlockingLoad_DoesNotWriteProfile()
        {
            var store = new RecordingSaveSlotStore(BlockedReport(CampaignSaveLoadStatus.IoFailed));
            var controller = CreateController(store);

            controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));
            controller.Continue(1);
            controller.RequestRestart(1);
            controller.RequestDelete(1);

            Assert.That(store.ProfileWriteCount, Is.Zero);
        }

        [Test]
        public void CampaignSaveLoadReport_BlockingPredicateCoversRepairIoAndUnauthorized()
        {
            Assert.That(BlockedReport(CampaignSaveLoadStatus.CorruptRepairRequired).BlocksCampaignAccess, Is.True);
            Assert.That(BlockedReport(CampaignSaveLoadStatus.SchemaInvalidRepairRequired).BlocksCampaignAccess, Is.True);
            Assert.That(BlockedReport(CampaignSaveLoadStatus.IoFailed).BlocksCampaignAccess, Is.True);
            Assert.That(BlockedReport(CampaignSaveLoadStatus.Unauthorized).BlocksCampaignAccess, Is.True);
            Assert.That(CampaignSaveLoadReport.Missing("missing").BlocksCampaignAccess, Is.False);
            Assert.That(CampaignSaveLoadReport.Loaded("loaded", "token").BlocksCampaignAccess, Is.False);
            Assert.That(
                new CampaignSaveLoadReport(
                    CampaignSaveLoadStatus.BackupRecovered,
                    "backup",
                    "CampaignProfileDocument").BlocksCampaignAccess,
                Is.False);
        }

        [Test]
        public void CampaignSaveLoadReport_RequiresRepairRemainsRepairOnly()
        {
            Assert.That(BlockedReport(CampaignSaveLoadStatus.CorruptRepairRequired).RequiresRepair, Is.True);
            Assert.That(BlockedReport(CampaignSaveLoadStatus.SchemaInvalidRepairRequired).RequiresRepair, Is.True);
            Assert.That(BlockedReport(CampaignSaveLoadStatus.IoFailed).RequiresRepair, Is.False);
            Assert.That(BlockedReport(CampaignSaveLoadStatus.Unauthorized).RequiresRepair, Is.False);
        }

        [Test]
        public void MainMenuController_SourceUsesCampaignAccessBlockerInsteadOfRepairOnlyCheck()
        {
            var source = File.ReadAllText("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");
            var mapper = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuSlotViewModelMapper.cs");

            Assert.That(source, Does.Contain("BlocksCampaignAccess"));
            Assert.That(source, Does.Not.Contain(".RequiresRepair"));
            Assert.That(mapper, Does.Not.Contain("report.Reason"));
            Assert.That(mapper, Does.Not.Contain("Reason.Contains"));
            Assert.That(mapper, Does.Not.Contain("Reason.StartsWith"));
        }

        private static MainMenuController CreateController(
            RecordingSaveSlotStore store,
            RecordingCampaignLaunchHandoffStore launchHandoffStore = null,
            RecordingStageLaunchRouter router = null,
            RecordingConfirmPopupPort confirmPopupPort = null,
            IMainMenuSaveDiagnosticPort saveDiagnosticPort = null)
        {
            return new MainMenuController(
                store,
                launchHandoffStore ?? new RecordingCampaignLaunchHandoffStore(),
                CampaignStageSequenceTestAsset.LoadProductionResolver(),
                router ?? new RecordingStageLaunchRouter(),
                confirmPopupPort ?? new RecordingConfirmPopupPort(),
                saveDiagnosticPort: saveDiagnosticPort);
        }

        private static CampaignSaveLoadReport BlockedReport(CampaignSaveLoadStatus status)
        {
            return new CampaignSaveLoadReport(status, ReasonFor(status), "CampaignProfileDocument");
        }

        private static string ReasonFor(CampaignSaveLoadStatus status)
        {
            switch (status)
            {
                case CampaignSaveLoadStatus.CorruptRepairRequired:
                    return "Save data needs repair";
                case CampaignSaveLoadStatus.SchemaInvalidRepairRequired:
                    return "Save data needs repair";
                case CampaignSaveLoadStatus.IoFailed:
                    return "Save data cannot be loaded";
                case CampaignSaveLoadStatus.Unauthorized:
                    return "Save data permission denied";
                default:
                    return status.ToString();
            }
        }

        private static SaveSlotData CreateExistingSlot(int slotNumber)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                LastPlayedAt = "2026-07-10T00:00:00Z",
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private sealed class RecordingSaveSlotStore : ICampaignSaveSlotStore
        {
            private readonly SaveSlotData[] _slots = new SaveSlotData[SaveSlotStore.SlotCount];

            public RecordingSaveSlotStore(CampaignSaveLoadReport report)
            {
                Report = report;
                for (var i = 0; i < _slots.Length; i++)
                {
                    _slots[i] = SaveSlotData.CreateEmpty(i + 1);
                }
            }

            public CampaignSaveLoadReport Report { get; set; }

            public int LoadAllWithReportCount { get; private set; }

            public int LoadSlotCount { get; private set; }

            public int SaveSlotCount { get; private set; }

            public int InitializeNewGameCount { get; private set; }

            public int UpdateSlotCount { get; private set; }

            public int DeleteSlotCount { get; private set; }

            public int ClearAllCount { get; private set; }

            public int ProfileWriteCount =>
                SaveSlotCount + InitializeNewGameCount + UpdateSlotCount + DeleteSlotCount + ClearAllCount;

            public string DiagnosticsKey => "recording-campaign-save";

            public CampaignSaveLoadReport LastCampaignLoadReport => Report;

            public void SetSlot(SaveSlotData slot)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slot.SlotNumber);
                _slots[slot.SlotNumber - 1] = slot;
            }

            public SaveSlotData[] LoadAll()
            {
                return LoadAllWithReport().Slots;
            }

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                LoadAllWithReportCount++;
                return new CampaignSaveLoadResult(_slots.ToArray(), Report);
            }

            public SaveSlotData LoadSlot(int slotNumber)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
                LoadSlotCount++;
                return _slots[slotNumber - 1];
            }

            public void SaveSlot(SaveSlotData slot)
            {
                SaveSlotCount++;
                SetSlot(slot);
            }

            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
                InitializeNewGameCount++;
                var slot = CreateExistingSlot(slotNumber);
                slot.LastPlayedAt = lastPlayedAt;
                SetSlot(slot);
                return slot;
            }

            public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
                UpdateSlotCount++;
                mutation?.Invoke(_slots[slotNumber - 1]);
            }

            public void DeleteSlot(int slotNumber)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
                DeleteSlotCount++;
                _slots[slotNumber - 1] = SaveSlotData.CreateEmpty(slotNumber);
            }

            public void ClearAll()
            {
                ClearAllCount++;
                for (var i = 0; i < _slots.Length; i++)
                {
                    _slots[i] = SaveSlotData.CreateEmpty(i + 1);
                }
            }
        }

        private sealed class RecordingStageLaunchRouter : IStageLaunchRouter
        {
            public int LaunchCount { get; private set; }

            public StageNavigationRequest LastRequest { get; private set; }

            public void Launch(StageNavigationRequest request)
            {
                LaunchCount++;
                LastRequest = request;
            }
        }

        private sealed class RecordingConfirmPopupPort : IConfirmPopupPort
        {
            private Action<bool> _completion;

            public int RequestCount { get; private set; }

            public ConfirmPopupPayload LastPayload { get; private set; }

            public void Request(ConfirmPopupPayload payload, Action<bool> completion)
            {
                RequestCount++;
                LastPayload = payload;
                _completion = completion;
            }

            public void Complete(bool confirmed)
            {
                var completion = _completion;
                _completion = null;
                completion?.Invoke(confirmed);
            }
        }

        private sealed class RecordingSaveDiagnosticPort : IMainMenuSaveDiagnosticPort
        {
            public int ReportCount { get; private set; }

            public SaveSlotFailureDiagnostic Last { get; private set; }

            public void Report(SaveSlotFailureDiagnostic diagnostic)
            {
                ReportCount++;
                Last = diagnostic;
            }
        }
    }
}

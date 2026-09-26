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
        [TestCase(CampaignSaveLoadStatus.RecoveryPending)]
        public void BlockingLoad_DoesNotRenderFreshEmptySlots(CampaignSaveLoadStatus status)
        {
            var controller = CreateController(new RecordingSaveSlotStore(BlockedReport(status)));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards, Is.Empty);
            Assert.That(viewModel.BlockedState, Is.Not.Null);
            Assert.That(viewModel.IsBlocked, Is.True);
        }

        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized)]
        [TestCase(CampaignSaveLoadStatus.RecoveryPending)]
        public void BlockingLoad_RendersPrimaryIntentNone(CampaignSaveLoadStatus status)
        {
            var controller = CreateController(new RecordingSaveSlotStore(BlockedReport(status)));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards, Is.Empty);
            Assert.That(viewModel.BlockedState, Is.Not.Null);
        }

        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized)]
        [TestCase(CampaignSaveLoadStatus.RecoveryPending)]
        public void BlockingLoad_HidesDelete(CampaignSaveLoadStatus status)
        {
            var controller = CreateController(new RecordingSaveSlotStore(BlockedReport(status)));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards, Is.Empty);
            Assert.That(viewModel.BlockedState.ShowResetProfile,
                Is.EqualTo(status == CampaignSaveLoadStatus.CorruptRepairRequired ||
                           status == CampaignSaveLoadStatus.SchemaInvalidRepairRequired));
        }

        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired)]
        [TestCase(CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized)]
        [TestCase(CampaignSaveLoadStatus.RecoveryPending)]
        public void BlockingLoad_DisablesContinue(CampaignSaveLoadStatus status)
        {
            var controller = CreateController(new RecordingSaveSlotStore(BlockedReport(status)));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards, Is.Empty);
            Assert.That(viewModel.BlockedState.ShowRetry, Is.True);
        }

        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired, SaveSlotFailurePresentationKind.CorruptedData, "Save Data Damaged", "This save data could not be read.")]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired, SaveSlotFailurePresentationKind.UnsupportedVersion, "Unsupported Save", "This save was created by an unsupported version.")]
        [TestCase(CampaignSaveLoadStatus.IoFailed, SaveSlotFailurePresentationKind.LoadFailed, "Save Load Failed", "The save data could not be loaded.")]
        [TestCase(CampaignSaveLoadStatus.Unauthorized, SaveSlotFailurePresentationKind.PermissionDenied, "Save Access Failed", "The save data could not be accessed. Check file permissions.")]
        [TestCase(CampaignSaveLoadStatus.RecoveryPending, SaveSlotFailurePresentationKind.RecoveryPending, "Save Reset Incomplete", "The save reset did not finish. Retry to continue.")]
        public void BlockingLoad_UsesStatusSpecificMessage(
            CampaignSaveLoadStatus status,
            SaveSlotFailurePresentationKind expectedKind,
            string expectedStatusText,
            string expectedDetailText)
        {
            var controller = CreateController(new RecordingSaveSlotStore(
                new CampaignSaveLoadReport(status, string.Empty, "CampaignProfileDocument")));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.BlockedState.TitleText, Is.EqualTo(expectedStatusText));
            Assert.That(viewModel.BlockedState.DetailText, Is.EqualTo(expectedDetailText));
            Assert.That(viewModel.BlockedState.FailureKind, Is.EqualTo(expectedKind));
        }

        [TestCase(CampaignSaveLoadStatus.Missing, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSaveLoadStatus.Loaded, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSaveLoadStatus.BackupRecovered, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSaveLoadStatus.CorruptRepairRequired, SaveSlotFailurePresentationKind.CorruptedData)]
        [TestCase(CampaignSaveLoadStatus.SchemaInvalidRepairRequired, SaveSlotFailurePresentationKind.UnsupportedVersion)]
        [TestCase(CampaignSaveLoadStatus.IoFailed, SaveSlotFailurePresentationKind.LoadFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized, SaveSlotFailurePresentationKind.PermissionDenied)]
        [TestCase(CampaignSaveLoadStatus.RecoveryPending, SaveSlotFailurePresentationKind.RecoveryPending)]
        public void CampaignLoadStatus_MapsToTypedUiFailureKind(
            CampaignSaveLoadStatus status,
            SaveSlotFailurePresentationKind expected)
        {
            Assert.That(MainMenuSlotViewModelMapper.MapFailureKind(status), Is.EqualTo(expected));
        }

        [TestCase(CampaignSlotLaunchStatus.Empty, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSlotLaunchStatus.Ready, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSlotLaunchStatus.Completed, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSlotLaunchStatus.LevelGroupSynchronizationRequired, SaveSlotFailurePresentationKind.None)]
        [TestCase(CampaignSlotLaunchStatus.StageMissingFromSequence, SaveSlotFailurePresentationKind.NeedsRepair)]
        [TestCase(CampaignSlotLaunchStatus.StageMissingFromCatalog, SaveSlotFailurePresentationKind.NeedsRepair)]
        public void SlotLaunchStatus_MapsToTypedUiFailureKind(
            CampaignSlotLaunchStatus status,
            SaveSlotFailurePresentationKind expected)
        {
            Assert.That(MainMenuSlotViewModelMapper.MapFailureKind(status), Is.EqualTo(expected));
        }

        [Test]
        public void SlotLaunchFailure_UsesSafeLocalizedCopyAndOnlySupportedActions()
        {
            var slot = CreateExistingSlot(1);
            slot.CurrentStageId = StageId.CreateOrThrow("stage-5-1");
            slot.CurrentLevelGroupId = "level-5";
            slot.TotalDeaths = 99;
            slot.LastPlayedAt = "2026-07-30T01:23:45+09:00";
            var entry = CampaignSlotRawDataMapper.ToEntry(slot);
            var evaluation = CampaignStageSequenceTestAsset
                .LoadProductionLaunchEvaluator()
                .Evaluate(entry);

            var card = MainMenuSlotViewModelMapper.MapSlot(
                entry,
                evaluation,
                CampaignSlotActionPolicy.Evaluate(evaluation),
                PackageFreeLocalizedTextResolver.CreateSettingsDefault());

            Assert.That(evaluation.Status, Is.EqualTo(CampaignSlotLaunchStatus.StageMissingFromSequence));
            Assert.That(card.FailureKind, Is.EqualTo(SaveSlotFailurePresentationKind.NeedsRepair));
            Assert.That(card.StatusText, Is.EqualTo("Save Data Unavailable"));
            Assert.That(card.StageText, Is.EqualTo("This save cannot be used in its current state."));
            Assert.That(card.ModeText, Is.Empty);
            Assert.That(card.SurvivalText, Is.Empty);
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

            var playerText = string.Join(
                "\n",
                viewModel.BlockedState.TitleText,
                viewModel.BlockedState.DetailText,
                viewModel.BlockedState.RetryActionText,
                viewModel.BlockedState.ResetProfileActionText);
            Assert.That(playerText, Does.Not.Contain(diagnostic));
            Assert.That(playerText, Does.Not.Contain("UnauthorizedAccessException"));
            Assert.That(playerText, Does.Not.Contain("C:\\Users\\Player"));
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
        public void BlockingLoad_ResetProfileConfirmation_UsesRecoveryPortInsteadOfSlotClear()
        {
            var store = new RecordingSaveSlotStore(
                BlockedReport(CampaignSaveLoadStatus.SchemaInvalidRepairRequired));
            var confirmPort = new RecordingConfirmPopupPort();
            var recoveryPort = new RecordingSaveRecoveryPort();
            var controller = CreateController(
                store,
                confirmPopupPort: confirmPort,
                saveRecoveryPort: recoveryPort);

            controller.RequestResetBlockedSave();
            confirmPort.Complete(true);

            Assert.That(confirmPort.RequestCount, Is.EqualTo(1));
            Assert.That(confirmPort.LastPayload.IsConfirmDestructive, Is.True);
            Assert.That(recoveryPort.ResetCount, Is.EqualTo(1));
            Assert.That(recoveryPort.LastExpectedStatus,
                Is.EqualTo(CampaignSaveLoadStatus.SchemaInvalidRepairRequired));
            Assert.That(store.ClearAllCount, Is.Zero);
            Assert.That(store.DeleteSlotCount, Is.Zero);
        }

        [Test]
        public void BlockingLoad_ResetProfileConfirmationAfterDisposeDoesNotMutate()
        {
            var store = new RecordingSaveSlotStore(
                BlockedReport(CampaignSaveLoadStatus.SchemaInvalidRepairRequired));
            var confirmPort = new RecordingConfirmPopupPort();
            var recoveryPort = new RecordingSaveRecoveryPort();
            var controller = CreateController(
                store,
                confirmPopupPort: confirmPort,
                saveRecoveryPort: recoveryPort);

            controller.RequestResetBlockedSave();
            controller.Dispose();
            confirmPort.Complete(true);

            Assert.That(recoveryPort.ResetCount, Is.Zero);
            Assert.That(store.ProfileWriteCount, Is.Zero);
        }

        [Test]
        public void BlockingLoad_ResetProfileCancellation_DoesNotMutate()
        {
            var store = new RecordingSaveSlotStore(
                BlockedReport(CampaignSaveLoadStatus.CorruptRepairRequired));
            var confirmPort = new RecordingConfirmPopupPort();
            var recoveryPort = new RecordingSaveRecoveryPort();
            var controller = CreateController(
                store,
                confirmPopupPort: confirmPort,
                saveRecoveryPort: recoveryPort);

            controller.RequestResetBlockedSave();
            confirmPort.Complete(false);

            Assert.That(recoveryPort.ResetCount, Is.Zero);
            Assert.That(store.ProfileWriteCount, Is.Zero);
        }

        [Test]
        public void RecoveryPending_RetryResumesRecoveryBeforeRefreshing()
        {
            var store = new RecordingSaveSlotStore(
                BlockedReport(CampaignSaveLoadStatus.RecoveryPending));
            var recoveryPort = new RecordingSaveRecoveryPort
            {
                HasPendingReset = true,
            };
            var controller = CreateController(store, saveRecoveryPort: recoveryPort);
            SaveSlotPanelViewModel refreshed = null;
            controller.ViewModelChanged += viewModel => refreshed = viewModel;

            controller.RetryBlockedSave();

            Assert.That(recoveryPort.RetryCount, Is.EqualTo(1));
            Assert.That(store.LoadAllWithReportCount, Is.EqualTo(1));
            Assert.That(refreshed, Is.Not.Null);
        }

        [TestCase(CampaignSaveLoadStatus.IoFailed)]
        [TestCase(CampaignSaveLoadStatus.Unauthorized)]
        [TestCase(CampaignSaveLoadStatus.RecoveryPending)]
        public void NonRepairableBlockingLoad_DoesNotOfferReset(CampaignSaveLoadStatus status)
        {
            var confirmPort = new RecordingConfirmPopupPort();
            var recoveryPort = new RecordingSaveRecoveryPort();
            var controller = CreateController(
                new RecordingSaveSlotStore(BlockedReport(status)),
                confirmPopupPort: confirmPort,
                saveRecoveryPort: recoveryPort);

            var viewModel = controller.BuildViewModel();
            controller.RequestResetBlockedSave();

            Assert.That(viewModel.BlockedState.ShowRetry, Is.True);
            Assert.That(viewModel.BlockedState.ShowResetProfile, Is.False);
            Assert.That(confirmPort.RequestCount, Is.Zero);
            Assert.That(recoveryPort.ResetCount, Is.Zero);
        }

        [Test]
        public void CampaignSaveLoadReport_BlockingPredicateCoversRepairIoAndUnauthorized()
        {
            Assert.That(BlockedReport(CampaignSaveLoadStatus.CorruptRepairRequired).BlocksCampaignAccess, Is.True);
            Assert.That(BlockedReport(CampaignSaveLoadStatus.SchemaInvalidRepairRequired).BlocksCampaignAccess, Is.True);
            Assert.That(BlockedReport(CampaignSaveLoadStatus.IoFailed).BlocksCampaignAccess, Is.True);
            Assert.That(BlockedReport(CampaignSaveLoadStatus.Unauthorized).BlocksCampaignAccess, Is.True);
            Assert.That(BlockedReport(CampaignSaveLoadStatus.RecoveryPending).BlocksCampaignAccess, Is.True);
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
            IMainMenuSaveDiagnosticPort saveDiagnosticPort = null,
            ICampaignSaveRecoveryPort saveRecoveryPort = null)
        {
            return new MainMenuController(
                store,
                store,
                store,
                launchHandoffStore ?? new RecordingCampaignLaunchHandoffStore(),
                CampaignStageSequenceTestAsset.LoadProductionResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router ?? new RecordingStageLaunchRouter(),
                confirmPopupPort ?? new RecordingConfirmPopupPort(),
                saveDiagnosticPort: saveDiagnosticPort,
                saveRecoveryPort: saveRecoveryPort);
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
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                LastPlayedAt = "2026-07-10T00:00:00Z",
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private sealed class RecordingSaveSlotStore :
            ICampaignSaveQuery,
            ICampaignContinuePreparationPort,
            ICampaignSlotLifecyclePort
        {
            private readonly SaveSlotData[] _slots = new SaveSlotData[CampaignSaveSlotPolicy.SlotCount];

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

            public int DeleteSlotCount { get; private set; }

            public int ClearAllCount { get; private set; }

            public int ProfileWriteCount =>
                SaveSlotCount + InitializeNewGameCount + DeleteSlotCount + ClearAllCount;

            public string DiagnosticsKey => "recording-campaign-save";

            public CampaignSaveLoadReport LastCampaignLoadReport => Report;

            public void SetSlot(SaveSlotData slot)
            {
                CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slot.SlotNumber);
                _slots[slot.SlotNumber - 1] = slot;
            }

            public SaveSlotData[] LoadAll()
            {
                return _slots.ToArray();
            }

            CampaignSlotEntry[] ICampaignSaveQuery.LoadAll() =>
                LoadAll().Select(ToEntry).ToArray();

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                LoadAllWithReportCount++;
                return new CampaignSaveLoadResult(
                    ((ICampaignSaveQuery)this).LoadAll(),
                    Report);
            }

            public SaveSlotData LoadSlot(int slotNumber)
            {
                CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
                LoadSlotCount++;
                return _slots[slotNumber - 1];
            }

            CampaignSlotEntry ICampaignSaveQuery.LoadSlot(int slotNumber) =>
                ToEntry(LoadSlot(slotNumber));

            public CampaignContinuePreparationResult PrepareContinue(
                CampaignContinuePreparationCommand command)
            {
                var preparation = CampaignContinuePreparationPolicy.Evaluate(
                    ToEntry(LoadSlot(command.SlotNumber)).State,
                    command);
                if (preparation.Succeeded && preparation.LevelGroupSynchronized)
                {
                    SaveSlotCount++;
                    SetSlot(CampaignSlotRawDataMapper.ToRaw(
                        preparation.CommittedState));
                }

                return preparation;
            }

            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt)
            {
                CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
                InitializeNewGameCount++;
                var slot = CreateExistingSlot(slotNumber);
                slot.LastPlayedAt = lastPlayedAt;
                SetSlot(slot);
                return slot;
            }

            CampaignSlotState ICampaignSlotLifecyclePort.InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt, GameMode gameMode) => CampaignSlotRawDataMapper.ToState(
                    InitializeNewGame(slotNumber, sequenceResolver, lastPlayedAt));

            public void DeleteSlot(int slotNumber)
            {
                CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
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

            private static CampaignSlotEntry ToEntry(SaveSlotData slot) => slot.IsEmpty
                ? CampaignSlotEntry.Empty(slot.SlotNumber)
                : CampaignSlotEntry.Occupied(
                    CampaignSlotRawDataMapper.ToState(slot));
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

        private sealed class RecordingSaveRecoveryPort : ICampaignSaveRecoveryPort
        {
            public bool HasPendingReset { get; set; }

            public int ResetCount { get; private set; }

            public int RetryCount { get; private set; }

            public CampaignSaveLoadStatus LastExpectedStatus { get; private set; }

            public CampaignSaveResetResult ResetBlockedProfile(CampaignSaveLoadStatus expectedStatus)
            {
                ResetCount++;
                LastExpectedStatus = expectedStatus;
                return CampaignSaveResetResult.Completed;
            }

            public CampaignSaveResetResult RetryPendingReset()
            {
                RetryCount++;
                HasPendingReset = false;
                return CampaignSaveResetResult.Completed;
            }
        }
    }
}

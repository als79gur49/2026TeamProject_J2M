using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class PendingLaunchSlotProviderTests
    {
        private string _saveNamespace;

        [SetUp]
        public void SetUp()
        {
            _saveNamespace = CreateTransientNamespace("saves");
            CampaignChanceHudDiagnostics.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            new TransientCampaignSaveSlotStore(_saveNamespace).ClearAll();
            CampaignChanceHudDiagnostics.IsEnabled = false;
            CampaignChanceHudDiagnostics.Clear();
        }

        [Test]
        public void MainMenu_NewGame_CreatesHandoffBeforeRouting()
        {
            var saveStore = new TransientCampaignSaveSlotStore(_saveNamespace);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter(() =>
                Assert.That(handoffStore.TryPeek(out _), Is.True));
            var resolver = CreateResolver();
            var controller = CreateController(
                saveStore,
                handoffStore,
                resolver,
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(resolver),
                router,
                confirmPort);
            saveStore.ImportSlotSeed(CreateExistingSlot(2, "stage-2-1"));

            controller.HandleIntent(new SaveSlotIntent(2, SaveSlotIntentKind.NewGame));
            confirmPort.Complete(true);

            Assert.That(handoffStore.TryPeek(out var handoff), Is.True);
            Assert.That(handoff.SlotNumber, Is.EqualTo(2));
            Assert.That(handoff.StageId, Is.EqualTo(resolver.FirstStageId));
            Assert.That(handoff.Source, Is.EqualTo("main-menu-new-game"));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void MainMenu_Continue_UsesExplicitSelectedSlot()
        {
            var saveStore = new TransientCampaignSaveSlotStore(_saveNamespace);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var router = new FakeStageLaunchRouter();
            var selectedStage = StageId.CreateOrThrow("stage-3-1");
            saveStore.ImportSlotSeed(CreateExistingSlot(3, selectedStage.Value));
            var controller = CreateController(
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router);

            controller.Continue(3);

            Assert.That(handoffStore.TryPeek(out var handoff), Is.True);
            Assert.That(handoff.SlotNumber, Is.EqualTo(3));
            Assert.That(handoff.StageId, Is.EqualTo(selectedStage));
            Assert.That(handoff.Source, Is.EqualTo("main-menu-continue"));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void MainMenu_Continue_WhenPreparationIsStale_ClearsOnlyOwnedHandoffAndRefreshes()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            inner.ImportSlotSeed(CreateExistingSlot(1, "stage-1-1"));
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var router = new FakeStageLaunchRouter();
            CampaignLaunchHandoff newer = null;
            saveStore.PrepareContinueOverride = _ =>
            {
                Assert.That(handoffStore.TryPeek(out var owned), Is.True);
                Assert.That(handoffStore.TryClear(owned.Token), Is.True);
                Assert.That(
                    handoffStore.TryBegin(
                        2,
                        StageId.CreateOrThrow("stage-2-1"),
                        StageNavigationKind.Continue,
                        "newer-owner",
                        out newer),
                    Is.True);
                return CampaignContinuePreparationResult.StalePrecondition();
            };
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                new FakeConfirmPopupPort());
            var refreshCount = 0;
            controller.ViewModelChanged += _ => refreshCount++;

            controller.Continue(1);

            Assert.That(handoffStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(newer));
            Assert.That(router.Requests, Is.Empty);
            Assert.That(refreshCount, Is.EqualTo(1));
        }

        [Test]
        public void MainMenu_SecondRequest_CannotOverwriteAcceptedHandoff()
        {
            var saveStore = new TransientCampaignSaveSlotStore(_saveNamespace);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var router = new FakeStageLaunchRouter();
            saveStore.ImportSlotSeed(CreateExistingSlot(1, "stage-1-1"));
            saveStore.ImportSlotSeed(CreateExistingSlot(3, "stage-3-1"));
            var controller = CreateController(
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router);

            controller.Continue(3);
            Assert.That(handoffStore.TryPeek(out var first), Is.True);
            controller.Continue(1);

            Assert.That(handoffStore.TryPeek(out var stillPending), Is.True);
            Assert.That(stillPending, Is.SameAs(first));
            Assert.That(stillPending.SlotNumber, Is.EqualTo(3));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryBeginFailure_LeavesProfileAndLastPlayedUnchanged()
        {
            var repository = new CloningCampaignProfileRepository();
            var saveStore = new CampaignSaveSlotStoreAdapter(
                new CampaignSaveService(
                    repository,
                    utcNowProvider: () => "2026-07-21T00:00:00Z"));
            var resolver = CreateResolver();
            saveStore.InitializeNewGame(1, resolver, "2026-07-20T00:00:00Z");
            saveStore.InitializeNewGame(2, resolver, "2026-07-20T01:00:00Z");
            repository.ResetSaveCount();
            var before = JsonUtility.ToJson(repository.CurrentDocument);
            var beforeLastPlayed = repository.CurrentDocument.LastPlayedSlotNumber;
            var handoffStore = new RacingCampaignLaunchHandoffStore(
                3,
                StageId.CreateOrThrow("stage-2-1"),
                StageNavigationKind.Continue,
                "existing-owner");
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                resolver,
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(resolver),
                router,
                confirmPort);

            controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));

            Assert.That(JsonUtility.ToJson(repository.CurrentDocument), Is.EqualTo(before));
            Assert.That(repository.CurrentDocument.LastPlayedSlotNumber, Is.EqualTo(beforeLastPlayed));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(handoffStore.TryPeek(out var stillPending), Is.True);
            Assert.That(stillPending, Is.SameAs(handoffStore.Existing));
            Assert.That(confirmPort.RequestCount, Is.Zero);
            Assert.That(router.Requests, Is.Empty);
        }

        [Test]
        public void SecondRestartRequest_WhenPendingExists_DoesNotResetProfile()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            inner.ImportSlotSeed(CreateExistingSlot(1, "stage-3-1"));
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            handoffStore.TryBegin(
                2,
                StageId.CreateOrThrow("stage-2-1"),
                StageNavigationKind.Continue,
                "existing-owner",
                out var existing);
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                confirmPort);
            var before = JsonUtility.ToJson(inner.LoadSlot(1));

            controller.RequestRestart(1);

            Assert.That(JsonUtility.ToJson(inner.LoadSlot(1)), Is.EqualTo(before));
            Assert.That(saveStore.InitializeNewGameCount, Is.Zero);
            Assert.That(confirmPort.RequestCount, Is.Zero);
            Assert.That(router.Requests, Is.Empty);
            Assert.That(handoffStore.TryPeek(out var stillPending), Is.True);
            Assert.That(stillPending, Is.SameAs(existing));
        }

        [Test]
        public void EmptyContinue_WhenPendingExists_DoesNotInitializeSlot()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            handoffStore.TryBegin(
                2,
                StageId.CreateOrThrow("stage-2-1"),
                StageNavigationKind.Continue,
                "existing-owner",
                out var existing);
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                new FakeConfirmPopupPort());

            controller.Continue(1);

            Assert.That(inner.LoadSlot(1).IsEmpty, Is.True);
            Assert.That(saveStore.InitializeNewGameCount, Is.Zero);
            Assert.That(router.Requests, Is.Empty);
            Assert.That(handoffStore.TryPeek(out var stillPending), Is.True);
            Assert.That(stillPending, Is.SameAs(existing));
        }

        [Test]
        public void CancelledConfirmation_ReleasesOnlyMatchingReservation()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            inner.ImportSlotSeed(CreateExistingSlot(1, "stage-3-1"));
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                new FakeStageLaunchRouter(),
                confirmPort);
            var before = JsonUtility.ToJson(inner.LoadSlot(1));

            controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));

            Assert.That(handoffStore.TryPeek(out var reservation), Is.True);
            Assert.That(confirmPort.RequestCount, Is.EqualTo(1));
            confirmPort.CompleteRequest(0, false);

            Assert.That(JsonUtility.ToJson(inner.LoadSlot(1)), Is.EqualTo(before));
            Assert.That(saveStore.InitializeNewGameCount, Is.Zero);
            Assert.That(handoffStore.TryPeek(out _), Is.False);
            Assert.That(handoffStore.ClearedTokens, Is.EqualTo(new[] { reservation.Token }));
        }

        [Test]
        public void LateOverwriteConfirmation_DoesNotMutateAfterNewerLaunchOperation()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            inner.ImportSlotSeed(CreateExistingSlot(1, "stage-3-1"));
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                confirmPort);
            var slotOneBefore = JsonUtility.ToJson(inner.LoadSlot(1));

            controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));
            confirmPort.CompleteRequest(0, false);
            controller.Continue(2);
            Assert.That(handoffStore.TryPeek(out var newer), Is.True);

            confirmPort.CompleteRequest(0, true);

            Assert.That(JsonUtility.ToJson(inner.LoadSlot(1)), Is.EqualTo(slotOneBefore));
            Assert.That(saveStore.InitializeNewGameCount, Is.EqualTo(1));
            Assert.That(handoffStore.TryPeek(out var stillCurrent), Is.True);
            Assert.That(stillCurrent, Is.SameAs(newer));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
            Assert.That(router.Requests[0].StageId, Is.EqualTo(CreateResolver().FirstStageId));
        }

        [Test]
        public void LateRestartConfirmation_DoesNotResetAfterOperationInvalidated()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            inner.ImportSlotSeed(CreateExistingSlot(1, "stage-3-1"));
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                confirmPort);
            var before = JsonUtility.ToJson(inner.LoadSlot(1));

            controller.RequestRestart(1);
            Assert.That(handoffStore.TryPeek(out var restartReservation), Is.True);
            Assert.That(handoffStore.TryClear(restartReservation.Token), Is.True);
            controller.Continue(2);
            Assert.That(handoffStore.TryPeek(out var newer), Is.True);

            confirmPort.CompleteRequest(0, true);

            Assert.That(JsonUtility.ToJson(inner.LoadSlot(1)), Is.EqualTo(before));
            Assert.That(saveStore.InitializeNewGameCount, Is.EqualTo(1));
            Assert.That(handoffStore.TryPeek(out var stillCurrent), Is.True);
            Assert.That(stillCurrent, Is.SameAs(newer));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void DuplicateConfirmationCallback_MutatesProfileAtMostOnce()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            inner.ImportSlotSeed(CreateExistingSlot(1, "stage-3-1"));
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                confirmPort);

            controller.RequestRestart(1);
            confirmPort.CompleteRequest(0, true);
            confirmPort.CompleteRequest(0, true);

            Assert.That(saveStore.InitializeNewGameCount, Is.EqualTo(1));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void ConfirmationAfterControllerDispose_DoesNotInitializeOrRouteAndReleasesReservation()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            inner.ImportSlotSeed(CreateExistingSlot(1, "stage-3-1"));
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                confirmPort);

            controller.RequestRestart(1);
            Assert.That(handoffStore.TryPeek(out _), Is.True);
            controller.Dispose();
            confirmPort.CompleteRequest(0, true);

            Assert.That(saveStore.InitializeNewGameCount, Is.Zero);
            Assert.That(router.Requests, Is.Empty);
            Assert.That(handoffStore.TryPeek(out _), Is.False);
        }

        [Test]
        public void PublicCommandsAfterControllerDispose_DoNotMutateOrRoute()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                confirmPort);

            controller.Dispose();
            controller.Continue(1);
            controller.RequestRestart(1);
            controller.RequestDelete(1);
            controller.RetryBlockedSave();
            controller.RequestResetBlockedSave();

            Assert.That(saveStore.InitializeNewGameCount, Is.Zero);
            Assert.That(inner.LoadSlot(1).IsEmpty, Is.True);
            Assert.That(handoffStore.TryPeek(out _), Is.False);
            Assert.That(router.Requests, Is.Empty);
            Assert.That(confirmPort.RequestCount, Is.Zero);
        }

        [Test]
        public void WrongConfirmationToken_DoesNotClearCurrentOperation()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            inner.ImportSlotSeed(CreateExistingSlot(1, "stage-3-1"));
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                new FakeStageLaunchRouter(),
                confirmPort);

            controller.RequestRestart(1);
            Assert.That(handoffStore.TryPeek(out var stale), Is.True);
            Assert.That(handoffStore.TryClear(stale.Token), Is.True);
            handoffStore.TryBegin(
                2,
                StageId.CreateOrThrow("stage-2-1"),
                StageNavigationKind.Continue,
                "newer-owner",
                out var newer);

            confirmPort.CompleteRequest(0, false);

            Assert.That(handoffStore.TryPeek(out var stillCurrent), Is.True);
            Assert.That(stillCurrent, Is.SameAs(newer));
            Assert.That(handoffStore.ClearedTokens.Contains(newer.Token), Is.False);
        }

        [Test]
        public void SuccessfulNewGame_DoesNotWritePersistentActive()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var activeStorage = new RecordingActiveSlotStorage(3);
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                inner,
                activeStorage,
                handoffStore);
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                repairingStore,
                repairingStore,
                repairingStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                new FakeConfirmPopupPort());

            controller.Continue(1);

            Assert.That(inner.LoadSlot(1).IsEmpty, Is.False);
            Assert.That(activeStorage.TryGetActiveSlot(out var activeSlot), Is.True);
            Assert.That(activeSlot, Is.EqualTo(3));
            Assert.That(activeStorage.SetCount, Is.Zero);
            Assert.That(activeStorage.ClearCount, Is.Zero);
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void InitializeNewGameFailure_ClearsMatchingReservationAndDoesNotRoute()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            var saveStore = new RecordingCampaignSaveSlotStore(inner)
            {
                ThrowOnInitializeNewGame = true,
            };
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                new FakeConfirmPopupPort());

            Assert.Throws<InvalidOperationException>(() => controller.Continue(1));

            Assert.That(inner.LoadSlot(1).IsEmpty, Is.True);
            Assert.That(saveStore.InitializeNewGameCount, Is.EqualTo(1));
            Assert.That(handoffStore.TryPeek(out _), Is.False);
            Assert.That(router.Requests, Is.Empty);
        }

        [Test]
        public void RoutingFailure_ClearsMatchingOwnership()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var router = new FakeStageLaunchRouter
            {
                ThrowOnLaunch = true,
            };
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                new FakeConfirmPopupPort());

            Assert.Throws<InvalidOperationException>(() => controller.Continue(1));

            Assert.That(saveStore.InitializeNewGameCount, Is.EqualTo(1));
            Assert.That(handoffStore.TryPeek(out _), Is.False);
            Assert.That(router.Requests, Is.Empty);
        }

        [Test]
        public void RoutingPreparationFailure_ClearsMatchingOwnership()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            var saveStore = new RecordingCampaignSaveSlotStore(inner)
            {
                LoadedStageOverrideAfterInitialize = StageId.CreateOrThrow("stage-2-1"),
            };
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                new FakeConfirmPopupPort());

            controller.Continue(1);

            Assert.That(saveStore.InitializeNewGameCount, Is.EqualTo(1));
            Assert.That(handoffStore.TryPeek(out _), Is.False);
            Assert.That(router.Requests, Is.Empty);
        }

        [Test]
        public void MainMenu_Delete_WhenLaunchIsPending_DoesNotRequestConfirmationOrMutate()
        {
            var saveStore = new TransientCampaignSaveSlotStore(_saveNamespace);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            handoffStore.TryBegin(
                1,
                StageId.CreateOrThrow("stage-1-1"),
                StageNavigationKind.Continue,
                "delete-test",
                out var existing);
            saveStore.ImportSlotSeed(CreateExistingSlot(1, "stage-1-1"));
            var confirmPort = new FakeConfirmPopupPort();
            var controller = CreateController(
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                confirmPort: confirmPort);

            controller.RequestDelete(1);
            confirmPort.Complete(true);

            Assert.That(confirmPort.RequestCount, Is.Zero);
            Assert.That(saveStore.LoadSlot(1).IsEmpty, Is.False);
            Assert.That(handoffStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(existing));
            Assert.That(handoffStore.ClearCount, Is.Zero);
        }

        [Test]
        public void ContinueThenDelete_DoesNotDeleteAcceptedLaunchOwner()
        {
            var saveStore = new TransientCampaignSaveSlotStore(_saveNamespace);
            saveStore.ImportSlotSeed(CreateExistingSlot(1, "stage-1-1"));
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                confirmPort);

            controller.Continue(1);
            controller.RequestDelete(1);

            Assert.That(confirmPort.RequestCount, Is.Zero);
            Assert.That(saveStore.LoadSlot(1).IsEmpty, Is.False);
            Assert.That(handoffStore.TryPeek(out var current), Is.True);
            Assert.That(current.SlotNumber, Is.EqualTo(1));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void DeleteConfirmationAfterContinue_DoesNotDeleteLaunchedSlotOrHandoff()
        {
            var saveStore = new TransientCampaignSaveSlotStore(_saveNamespace);
            saveStore.ImportSlotSeed(CreateExistingSlot(1, "stage-1-1"));
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                confirmPort);

            controller.RequestDelete(1);
            controller.Continue(1);
            confirmPort.CompleteRequest(0, true);

            Assert.That(saveStore.LoadSlot(1).IsEmpty, Is.False);
            Assert.That(handoffStore.TryPeek(out var current), Is.True);
            Assert.That(current.SlotNumber, Is.EqualTo(1));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void NoneAndInvalidIntent_DoNotCancelExistingRestartConfirmation()
        {
            var inner = new TransientCampaignSaveSlotStore(_saveNamespace);
            inner.ImportSlotSeed(CreateExistingSlot(1, "stage-3-1"));
            var saveStore = new RecordingCampaignSaveSlotStore(inner);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var controller = new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                CreateResolver(),
                CampaignStageSequenceTestAsset.LoadProductionLaunchEvaluator(),
                router,
                confirmPort);

            controller.RequestRestart(1);
            controller.HandleIntent(new SaveSlotIntent(0, SaveSlotIntentKind.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.RequestDelete(0));
            confirmPort.CompleteRequest(0, true);

            Assert.That(saveStore.InitializeNewGameCount, Is.EqualTo(1));
            Assert.That(router.Requests, Has.Count.EqualTo(1));
            Assert.That(handoffStore.TryPeek(out var current), Is.True);
            Assert.That(current.SlotNumber, Is.EqualTo(1));
        }

        [Test]
        public void MainMenu_Continue_DoesNotUseLastPlayedSlotNumber()
        {
            var source = ReadRepoFile(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");

            Assert.That(source, Does.Contain("_launchHandoffStore.TryBegin"));
            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(source, Does.Not.Contain("CampaignProfileDocument"));
        }

        [Test]
        public void MainMenuComposition_SharesApplicationSessionOwner()
        {
            var source = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");

            Assert.That(
                CountOccurrences(source, "CampaignLaunchHandoffSessionStore.Instance"),
                Is.EqualTo(1));
            Assert.That(source, Does.Contain("launchHandoffStore,\n                EnsureComicSequenceFlowCoordinator()"));
            Assert.That(source, Does.Contain("ComicSequenceFlowCoordinator"));
            Assert.That(source, Does.Contain("saveSlotStore,\n                launchHandoffStore,"));
            Assert.That(source, Does.Not.Contain("ActiveSlotProviderPendingLaunchAdapter"));
        }

        private static MainMenuController CreateController(
            TransientCampaignSaveSlotStore saveStore,
            ICampaignLaunchHandoffStore handoffStore,
            CampaignStageSequenceResolver resolver,
            CampaignSlotLaunchEvaluator launchEvaluator,
            IStageLaunchRouter router = null,
            FakeConfirmPopupPort confirmPort = null)
        {
            return new MainMenuController(
                saveStore,
                saveStore,
                saveStore,
                handoffStore,
                resolver,
                launchEvaluator,
                router ?? new FakeStageLaunchRouter(),
                confirmPort ?? new FakeConfirmPopupPort());
        }

        private static CampaignStageSequenceResolver CreateResolver()
        {
            return CampaignStageSequenceTestAsset.LoadProductionResolver();
        }

        private static CampaignSlotSeedImportRequest CreateExistingSlot(
            int slotNumber,
            string stageId)
        {
            return new CampaignSlotSeedImportRequest(
                slotNumber,
                StageId.CreateOrThrow(stageId),
                "level-" + slotNumber,
                CampaignSaveSlotPolicy.DefaultRemainingChances,
                string.Empty);
        }

        private static string CreateTransientNamespace(string suffix)
        {
            return
                "Game.Feature.UI.Tests.PendingLaunchSlotProviderTests." +
                suffix +
                "." +
                Guid.NewGuid().ToString("N");
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private sealed class FakeConfirmPopupPort : IConfirmPopupPort
        {
            private readonly List<Action<bool>> _completions = new();

            public int RequestCount => _completions.Count;

            public void Request(ConfirmPopupPayload payload, Action<bool> completion)
            {
                _completions.Add(completion ?? throw new ArgumentNullException(nameof(completion)));
            }

            public void Complete(bool confirmed)
            {
                if (_completions.Count == 0)
                {
                    return;
                }

                _completions[_completions.Count - 1].Invoke(confirmed);
            }

            public void CompleteRequest(int requestIndex, bool confirmed)
            {
                _completions[requestIndex].Invoke(confirmed);
            }
        }

        private sealed class FakeStageLaunchRouter : IStageLaunchRouter
        {
            private readonly Action _beforeRecord;

            public FakeStageLaunchRouter(Action beforeRecord = null)
            {
                _beforeRecord = beforeRecord;
            }

            public List<StageNavigationRequest> Requests { get; } = new();

            public bool ThrowOnLaunch { get; set; }

            public void Launch(StageNavigationRequest request)
            {
                _beforeRecord?.Invoke();
                if (ThrowOnLaunch)
                {
                    throw new InvalidOperationException("Injected routing failure.");
                }

                Requests.Add(request);
            }
        }

        private sealed class RecordingCampaignSaveSlotStore :
            ICampaignSaveQuery,
            ICampaignContinuePreparationPort,
            ICampaignSlotLifecyclePort
        {
            private readonly ICampaignSaveRuntime _inner;

            public RecordingCampaignSaveSlotStore(ICampaignSaveRuntime inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public int InitializeNewGameCount { get; private set; }

            public bool ThrowOnInitializeNewGame { get; set; }

            public StageId LoadedStageOverrideAfterInitialize { get; set; }

            public string DiagnosticsKey => _inner.DiagnosticsKey;

            public CampaignSaveLoadReport LastCampaignLoadReport => _inner.LastCampaignLoadReport;

            public CampaignSlotEntry[] LoadAll()
            {
                return _inner.LoadAll();
            }

            public CampaignSaveLoadResult LoadAllWithReport()
            {
                return _inner.LoadAllWithReport();
            }

            public CampaignSlotEntry LoadSlot(int slotNumber)
            {
                var slot = _inner.LoadSlot(slotNumber);
                if (InitializeNewGameCount > 0 &&
                    LoadedStageOverrideAfterInitialize.IsValid &&
                    !slot.IsEmpty)
                {
                    var legacy = CampaignSlotRawDataMapper.ToRaw(slot);
                    legacy.CurrentStageId = LoadedStageOverrideAfterInitialize;
                    return CampaignSlotEntry.Occupied(
                        CampaignSlotRawDataMapper.ToState(legacy));
                }

                return slot;
            }

            public Func<CampaignContinuePreparationCommand, CampaignContinuePreparationResult>
                PrepareContinueOverride { get; set; }

            public CampaignContinuePreparationResult PrepareContinue(
                CampaignContinuePreparationCommand command)
            {
                return PrepareContinueOverride != null
                    ? PrepareContinueOverride(command)
                    : _inner.PrepareContinue(command);
            }

            public CampaignSlotState InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt, GameMode gameMode = GameMode.Hardcore)
            {
                InitializeNewGameCount++;
                if (ThrowOnInitializeNewGame)
                {
                    throw new InvalidOperationException("Injected initialization failure.");
                }

                return _inner.InitializeNewGame(slotNumber, sequenceResolver, lastPlayedAt, gameMode);
            }

            public void DeleteSlot(int slotNumber)
            {
                _inner.DeleteSlot(slotNumber);
            }

            public void ClearAll()
            {
                _inner.ClearAll();
            }
        }

        private sealed class CloningCampaignProfileRepository : ICampaignProfileRepository
        {
            public CampaignProfileDocument CurrentDocument { get; private set; }

            public int SaveCount { get; private set; }

            public CampaignProfileLoadResult Load()
            {
                return CurrentDocument == null
                    ? new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Missing,
                        null,
                        "missing")
                    : new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Loaded,
                        Clone(CurrentDocument),
                        "loaded");
            }

            public void Save(CampaignProfileDocument document)
            {
                SaveCount++;
                CurrentDocument = Clone(document);
            }

            public void SaveDestructive(CampaignProfileDocument document)
            {
                Save(document);
            }

            public void ResetSaveCount()
            {
                SaveCount = 0;
            }

            private static CampaignProfileDocument Clone(CampaignProfileDocument document)
            {
                return JsonUtility.FromJson<CampaignProfileDocument>(JsonUtility.ToJson(document));
            }
        }

        private sealed class RecordingActiveSlotStorage : IActiveSlotStorage
        {
            private int _slotNumber;

            public RecordingActiveSlotStorage(int initialSlotNumber)
            {
                _slotNumber = initialSlotNumber;
            }

            public string DiagnosticsKey => "recording-active";

            public int SetCount { get; private set; }

            public int ClearCount { get; private set; }

            public bool TryGetActiveSlot(out int slotNumber)
            {
                slotNumber = _slotNumber;
                return CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber);
            }

            public void SetActiveSlot(int slotNumber)
            {
                CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
                SetCount++;
                _slotNumber = slotNumber;
            }

            public void ClearActiveSlot()
            {
                ClearCount++;
                _slotNumber = 0;
            }
        }

        private sealed class RacingCampaignLaunchHandoffStore : ICampaignLaunchHandoffStore
        {
            private bool _hideNextPeek = true;
            private CampaignLaunchHandoff _pending;

            public RacingCampaignLaunchHandoffStore(
                int slotNumber,
                StageId stageId,
                StageNavigationKind navigationKind,
                string source)
            {
                Existing = new CampaignLaunchHandoff(
                    slotNumber,
                    stageId,
                    navigationKind,
                    source,
                    Guid.NewGuid());
                _pending = Existing;
            }

            public CampaignLaunchHandoff Existing { get; }

            public bool TryBegin(
                int slotNumber,
                StageId stageId,
                StageNavigationKind navigationKind,
                string source,
                out CampaignLaunchHandoff handoff)
            {
                _hideNextPeek = false;
                if (_pending != null)
                {
                    handoff = _pending;
                    return false;
                }

                handoff = new CampaignLaunchHandoff(
                    slotNumber,
                    stageId,
                    navigationKind,
                    source,
                    Guid.NewGuid());
                _pending = handoff;
                return true;
            }

            public bool TryPeek(out CampaignLaunchHandoff handoff)
            {
                if (_hideNextPeek)
                {
                    _hideNextPeek = false;
                    handoff = null;
                    return false;
                }

                handoff = _pending;
                return handoff != null;
            }

            public bool TryClear(Guid token)
            {
                if (_pending == null || _pending.Token != token)
                {
                    return false;
                }

                _pending = null;
                return true;
            }

            public bool TryConsume(Guid token, out CampaignLaunchHandoff handoff)
            {
                if (_pending == null || _pending.Token != token)
                {
                    handoff = null;
                    return false;
                }

                handoff = _pending;
                _pending = null;
                return true;
            }
        }
    }

    internal sealed class RecordingCampaignLaunchHandoffStore : ICampaignLaunchHandoffStore
    {
        private CampaignLaunchHandoff _pending;

        public int BeginCount { get; private set; }

        public int ClearCount { get; private set; }

        public int ConsumeCount { get; private set; }

        public List<Guid> ClearedTokens { get; } = new();

        public bool TryBegin(
            int slotNumber,
            StageId stageId,
            StageNavigationKind navigationKind,
            string source,
            out CampaignLaunchHandoff handoff)
        {
            BeginCount++;
            if (_pending != null)
            {
                handoff = _pending;
                return false;
            }

            _pending = new CampaignLaunchHandoff(
                slotNumber,
                stageId,
                navigationKind,
                source,
                Guid.NewGuid());
            handoff = _pending;
            return true;
        }

        public bool TryPeek(out CampaignLaunchHandoff handoff)
        {
            handoff = _pending;
            return handoff != null;
        }

        public bool TryClear(Guid token)
        {
            if (_pending == null || _pending.Token != token)
            {
                return false;
            }

            ClearCount++;
            ClearedTokens.Add(token);
            _pending = null;
            return true;
        }

        public bool TryConsume(Guid token, out CampaignLaunchHandoff handoff)
        {
            if (_pending == null || _pending.Token != token)
            {
                handoff = null;
                return false;
            }

            ConsumeCount++;
            handoff = _pending;
            _pending = null;
            return true;
        }
    }
}

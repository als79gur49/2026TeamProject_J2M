using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class CampaignMainMenuAndAutoNextTests
    {
        [SetUp]
        public void SetUp()
        {
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            TerminalSessionRegistry.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
        }

        [Test]
        public void MainMenuSlotViewModelMapper_MapsEmptyExistingAndCompletedSlots()
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var slots = new[]
            {
                SaveSlotData.CreateEmpty(1),
                new SaveSlotData
                {
                    SlotNumber = 2,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                    CurrentLevelGroupId = "level-2",
                    RemainingChances = 2,
                    TotalDeaths = 1,
                    LastPlayedAt = "played",
                },
                new SaveSlotData
                {
                    SlotNumber = 3,
                    CurrentStageId = StageId.CreateOrThrow("stage-4-3"),
                    CurrentLevelGroupId = "level-4",
                    RemainingChances = 3,
                    CampaignCompleted = true,
                    TotalDeaths = 5,
                },
            };

            var viewModel = MainMenuSlotViewModelMapper.Map(slots, resolver);

            Assert.That(viewModel.SlotCards[0].State, Is.EqualTo(SaveSlotCardState.Empty));
            Assert.That(viewModel.SlotCards[0].PrimaryActionText, Is.EqualTo("New Game"));
            Assert.That(viewModel.SlotCards[1].State, Is.EqualTo(SaveSlotCardState.Existing));
            Assert.That(viewModel.SlotCards[1].PrimaryActionText, Is.EqualTo("Continue"));
            Assert.That(viewModel.SlotCards[1].StageText, Is.EqualTo("Stage Ward[A]-02"));
            Assert.That(viewModel.SlotCards[2].State, Is.EqualTo(SaveSlotCardState.Completed));
            Assert.That(viewModel.SlotCards[2].PrimaryActionText, Is.EqualTo("Restart"));
            Assert.That(viewModel.SlotCards[2].ShowDelete, Is.True);
        }

        [Test]
        public void MainMenuController_DeleteMutatesOnlyWhenConfirmed()
        {
            var saveKey = CreatePrefsKey(nameof(MainMenuController_DeleteMutatesOnlyWhenConfirmed));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotStorage = new PlayerPrefsActiveSlotStorage(activeKey);
            var activeSlotProvider = new ActiveSlotProvider(activeSlotStorage);
            var launchHandoffStore = new RecordingCampaignLaunchHandoffStore();
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                saveStore,
                activeSlotStorage,
                launchHandoffStore);
            var confirmPort = new FakeConfirmPopupPort();
            var controller = new MainMenuController(
                repairingStore,
                launchHandoffStore,
                CampaignStageSequenceTestAsset.LoadProductionResolver(),
                new FakeStageLaunchRouter(),
                confirmPort);
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
            });
            activeSlotProvider.SetActiveSlot(1);

            controller.RequestDelete(1);
            confirmPort.Complete(false);
            Assert.That(saveStore.LoadSlot(1).IsEmpty, Is.False);
            Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(1));

            controller.RequestDelete(1);
            confirmPort.Complete(true);
            Assert.That(saveStore.LoadSlot(1).IsEmpty, Is.True);
            Assert.That(activeSlotProvider.TryGetActiveSlotNumber(out _), Is.False);
        }

        [Test]
        public void StageResultAutoNextDriver_LaunchesOnceAfterCountdown()
        {
            var screenController = new ScreenController(new FakeScreenRuntimeFactory());
            var popupController = new PopupController(new FakePopupRuntimeFactory());
            var router = new FakeStageLaunchRouter();
            using var driver = new StageResultAutoNextDriver(screenController, popupController, router);
            var nextRequest = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-2-1"),
                StageNavigationKind.NextStage,
                "test");
            var payload = new StageResultScreenPayload(
                nextRequest,
                StageNavigationRequest.None,
                nextRequest);

            screenController.SetRoot(new ScreenRequest(ScreenId.StageResult, payload, "result"));
            driver.Tick(1f);
            Assert.That(router.Requests, Is.Empty);

            driver.Tick(2f);
            driver.Tick(2f);

            Assert.That(router.Requests.Count, Is.EqualTo(1));
            Assert.That(router.Requests[0].StageId.Value, Is.EqualTo("stage-2-1"));
            Assert.That(driver.LaunchCount, Is.EqualTo(1));
        }

        [Test]
        public void StageResultAutoNextDriver_ExpiredDuringTerminalSessionWaitsThenLaunchesExactlyOnce()
        {
            var screenController = new ScreenController(new FakeScreenRuntimeFactory());
            var popupController = new PopupController(new FakePopupRuntimeFactory());
            var router = new FakeStageLaunchRouter();
            using var driver = new StageResultAutoNextDriver(
                screenController,
                popupController,
                request =>
                {
                    if (TerminalSessionRegistry.IsActive)
                    {
                        return false;
                    }

                    router.Launch(request);
                    return true;
                });
            var nextRequest = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-2-1"),
                StageNavigationKind.NextStage,
                "terminal-admission-test");
            var payload = new StageResultScreenPayload(
                nextRequest,
                StageNavigationRequest.None,
                nextRequest);
            var authority = TerminalSessionRegistry.Authority;
            var sceneGeneration = authority.RegisterSceneBootstrap(81, "AutoNextScene");
            var claim = authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Victory,
                sceneGeneration,
                TerminalDestinationKind.SameSceneStageResult));

            screenController.SetRoot(new ScreenRequest(ScreenId.StageResult, payload, "result"));
            driver.Tick(StageResultAutoNextDriver.DefaultCountdownSeconds);
            driver.Tick(10f);

            Assert.That(router.Requests, Is.Empty);
            Assert.That(driver.LaunchCount, Is.Zero);
            Assert.That(driver.IsArmed, Is.False);
            Assert.That(
                authority.TryAdvancePhase(
                    claim.Token,
                    TerminalSessionPhase.WaitingResultInteraction),
                Is.True);
            Assert.That(authority.TryComplete(claim.Token), Is.True);

            driver.Tick(0f);
            driver.Tick(10f);

            Assert.That(router.Requests, Has.Count.EqualTo(1));
            Assert.That(driver.LaunchCount, Is.EqualTo(1));
        }

        private static string CreatePrefsKey(string suffix)
        {
            return "Game.Feature.UI.Tests." + suffix + "." + Guid.NewGuid().ToString("N");
        }

        private sealed class FakeConfirmPopupPort : IConfirmPopupPort
        {
            private Action<bool> _completion;

            public List<ConfirmPopupPayload> Requests { get; } = new();

            public void Request(ConfirmPopupPayload payload, Action<bool> completion)
            {
                Requests.Add(payload);
                _completion = completion;
            }

            public void Complete(bool confirmed)
            {
                _completion?.Invoke(confirmed);
                _completion = null;
            }
        }
    }
}

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
    public sealed class MainMenuLastPlayedContinuePolicyTests
    {
        private const string MainMenuControllerPath =
            "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs";

        private string _activeKey;
        private string _saveKey;

        [SetUp]
        public void SetUp()
        {
            _activeKey = CreatePrefsKey("active");
            _saveKey = CreatePrefsKey("saves");
            PlayerPrefs.DeleteKey(_activeKey);
            PlayerPrefs.DeleteKey(_saveKey);
            CampaignChanceHudDiagnostics.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_activeKey);
            PlayerPrefs.DeleteKey(_saveKey);
            PlayerPrefs.Save();
            CampaignChanceHudDiagnostics.IsEnabled = false;
            CampaignChanceHudDiagnostics.Clear();
        }

        [TestCase("LastPlayedSlotNumber")]
        [TestCase("CampaignProfileDocument")]
        [TestCase("CampaignSaveService")]
        [TestCase("CampaignSaveServiceFactory")]
        [TestCase("FileCampaignProfileRepository")]
        [TestCase("ICampaignProfileRepository")]
        [TestCase("profile.json")]
        public void MainMenuController_DoesNotReferenceV2ProfileOrStorageTokens(string forbiddenToken)
        {
            Assert.That(ReadRepoFile(MainMenuControllerPath), Does.Not.Contain(forbiddenToken));
        }

        [Test]
        public void MainMenuContinue_RemainsExplicitSelectedSlotIntent()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            var router = new RecordingStageLaunchRouter();
            var selectedStage = StageId.CreateOrThrow("stage-1-2");
            var otherStage = StageId.CreateOrThrow("stage-3-1");
            saveStore.SaveSlot(CreateExistingSlot(1, selectedStage));
            saveStore.SaveSlot(CreateExistingSlot(3, otherStage));
            var controller = CreateController(saveStore, handoffStore, router);

            controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.Continue));

            Assert.That(handoffStore.TryPeek(out var handoff), Is.True);
            Assert.That(handoff.SlotNumber, Is.EqualTo(1));
            Assert.That(router.Requests.Count, Is.EqualTo(1));
            Assert.That(router.Requests[0].StageId, Is.EqualTo(selectedStage));
            Assert.That(router.Requests[0].Source, Is.EqualTo("main-menu-continue"));
        }

        [Test]
        public void MainMenuSlotList_RemainsSaveSlotStoreLoadAllBased()
        {
            var saveStore = new SaveSlotStore(_saveKey);
            var controller = CreateController(
                saveStore,
                new RecordingCampaignLaunchHandoffStore(),
                new RecordingStageLaunchRouter());
            saveStore.SaveSlot(CreateExistingSlot(2, StageId.CreateOrThrow("stage-2-1")));

            var viewModel = controller.BuildViewModel();

            Assert.That(viewModel.SlotCards.Count, Is.EqualTo(SaveSlotStore.SlotCount));
            Assert.That(viewModel.SlotCards[0].State, Is.EqualTo(SaveSlotCardState.Empty));
            Assert.That(viewModel.SlotCards[1].State, Is.EqualTo(SaveSlotCardState.Existing));
            Assert.That(ReadRepoFile(MainMenuControllerPath), Does.Contain("_saveSlotStore.LoadAllWithReport()"));
            Assert.That(ReadRepoFile(MainMenuControllerPath), Does.Not.Contain("CampaignSaveService"));
        }

        [Test]
        public void LastPlayedSlotNumber_IsNotQuickContinueOrDefaultFocusSource()
        {
            var source = ReadRepoFile(MainMenuControllerPath);

            Assert.That(source, Does.Contain("public void Continue(int slotNumber)"));
            Assert.That(source, Does.Contain("_launchHandoffStore.TryBegin"));
            Assert.That(source, Does.Contain("_saveSlotStore.LoadAllWithReport()"));
            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(source, Does.Not.Contain("QuickContinue"));
            Assert.That(source, Does.Not.Contain("DefaultFocus"));
            Assert.That(source, Does.Not.Contain("CampaignProfileDocument"));
        }

        [Test]
        public void MainMenuPendingLaunch_UsesSessionHandoffStore()
        {
            var source = ReadRepoFile(MainMenuControllerPath);

            Assert.That(source, Does.Contain("ICampaignLaunchHandoffStore launchHandoffStore"));
            Assert.That(source, Does.Contain("_launchHandoffStore.TryBegin"));
            Assert.That(source, Does.Contain("_launchHandoffStore.TryPeek"));
            Assert.That(source, Does.Not.Contain("ActiveSlotProvider _activeSlotProvider"));
            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(source, Does.Not.Contain("CampaignProfileDocument"));
        }

        private MainMenuController CreateController(
            SaveSlotStore saveStore,
            ICampaignLaunchHandoffStore handoffStore,
            IStageLaunchRouter router)
        {
            return new MainMenuController(
                saveStore,
                handoffStore,
                new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance()),
                router,
                new ImmediateConfirmPopupPort());
        }

        private static SaveSlotData CreateExistingSlot(int slotNumber, StageId stageId)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = stageId,
                CurrentLevelGroupId = $"level-{slotNumber}",
            };
        }

        private static string CreatePrefsKey(string suffix)
        {
            return "Game.Feature.UI.Tests.MainMenuLastPlayedContinuePolicyTests." +
                suffix +
                "." +
                Guid.NewGuid().ToString("N");
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private sealed class RecordingStageLaunchRouter : IStageLaunchRouter
        {
            public List<StageNavigationRequest> Requests { get; } = new();

            public void Launch(StageNavigationRequest request)
            {
                Requests.Add(request);
            }
        }

        private sealed class ImmediateConfirmPopupPort : IConfirmPopupPort
        {
            public void Request(ConfirmPopupPayload payload, Action<bool> completion)
            {
                completion?.Invoke(true);
            }
        }

    }
}

using System.IO;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class MainMenuUiAudioFeedbackTests
    {
        private const string ControllerSourcePath =
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiAudioFeedbackController.cs";
        private const string InstallerSourcePath =
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs";

        [Test]
        public void MainMenuUiAudioFeedback_UsesUiAudioPort_NotGameplayAudioMap()
        {
            var source = ReadRepoFile(ControllerSourcePath);

            Assert.That(source, Does.Contain("IUiAudioPort"));
            Assert.That(source, Does.Not.Contain("IAudioService"));
            Assert.That(source, Does.Not.Contain("GameplayAudioMap"));
            Assert.That(source, Does.Not.Contain("PlayBgm"));
        }

        [Test]
        public void MainMenuUiAudioFeedback_SaveSlotContinue_PlaysStageLaunchCue()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);

            controller.HandleSaveSlotIntentRequested(new SaveSlotIntent(1, SaveSlotIntentKind.Continue));

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.StageLaunch }));
        }

        [Test]
        public void MainMenuUiAudioFeedback_SaveSlotNewGame_WaitsForModeSelection()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);

            controller.HandleSaveSlotIntentRequested(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));

            Assert.That(uiAudioPort.PlayedCueIds, Is.Empty);
        }

        [TestCase(PopupCompletionKind.Confirmed)]
        [TestCase(PopupCompletionKind.AlternativeSelected)]
        public void MainMenuUiAudioFeedback_ModeButton_PlaysStageLaunchOnce(
            PopupCompletionKind selection)
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);
            var entry = CreateCampaignModePopupEntry();

            controller.HandleSaveSlotIntentRequested(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));
            controller.HandlePopupOpened(new PopupOpenedEvent(entry));
            controller.HandlePopupCompleted(CreatePopupCompletedEvent(entry, selection));

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[]
            {
                UiAudioCueId.NavigateForward,
                UiAudioCueId.StageLaunch,
            }));
        }

        [Test]
        public void MainMenuUiAudioFeedback_ModeSelectionCancelled_DoesNotPlayStageLaunch()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);
            var entry = CreateCampaignModePopupEntry();

            controller.HandlePopupOpened(new PopupOpenedEvent(entry));
            controller.HandlePopupCompleted(CreatePopupCompletedEvent(entry, PopupCompletionKind.Cancelled));

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[]
            {
                UiAudioCueId.NavigateForward,
                UiAudioCueId.Cancel,
            }));
        }

        [Test]
        public void MainMenuUiAudioFeedback_DestructiveIntent_DoesNotDoublePlaySelectAndNavigateForward()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);

            controller.HandlePopupOpened(CreateConfirmPopupOpenedEvent());
            controller.HandleSaveSlotIntentRequested(new SaveSlotIntent(1, SaveSlotIntentKind.Delete));

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.NavigateForward }));
        }

        [Test]
        public void MainMenuUiAudioFeedback_SettingsOpen_PlaysNavigateForwardCue()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);

            controller.HandleSettingsOpened();

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.NavigateForward }));
        }

        [Test]
        public void MainMenuUiAudioFeedback_StartNavigation_PlaysPrimaryMenuCommandCue()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);

            controller.HandleNavigationRequested(new MainMenuNavigationIntent(MainMenuSectionId.SaveSlots));

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.PrimaryMenuCommand }));
        }

        [Test]
        public void MainMenuUiAudioFeedback_SettingsCommand_SuppressesFollowupSettingsOpenCue()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);

            controller.HandleCommandRequested(new MainMenuCommandIntent(MainMenuCommandKind.OpenSettings));
            controller.HandleSettingsOpened();

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.PrimaryMenuCommand }));
        }

        [Test]
        public void MainMenuUiAudioFeedback_QuitCommand_SuppressesFollowupConfirmOpenCue()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);

            controller.HandleCommandRequested(new MainMenuCommandIntent(MainMenuCommandKind.Quit));
            controller.HandlePopupOpened(CreateConfirmPopupOpenedEvent());

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.PrimaryMenuCommand }));
        }

        [Test]
        public void MainMenuUiAudioFeedback_QuitConfirm_PlaysConfirmCueOnce()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);

            controller.HandlePopupCompleted(CreateConfirmPopupCompletedEvent(PopupCompletionKind.Confirmed));

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Confirm }));
        }

        [Test]
        public void MainMenuUiAudioFeedback_QuitCancel_PlaysCancelCueOnce()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);

            controller.HandlePopupCompleted(CreateConfirmPopupCompletedEvent(PopupCompletionKind.Cancelled));

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Cancel }));
        }

        [Test]
        public void MainMenuAudio_DoesNotUseUIFlowCoordinatorForCuePlayback()
        {
            var source = ReadRepoFile(ControllerSourcePath);

            Assert.That(source, Does.Not.Contain("UIFlowCoordinator"));
        }

        [Test]
        public void MainMenuAudioFeedback_InstallsBeforeCommandSideEffects()
        {
            var source = ReadRepoFile(InstallerSourcePath);

            Assert.That(
                source.IndexOf("BuildAudioFeedbackModule()", System.StringComparison.Ordinal),
                Is.LessThan(source.IndexOf("BuildSaveSlotModule()", System.StringComparison.Ordinal)));
            Assert.That(
                source.IndexOf("BuildAudioFeedbackModule()", System.StringComparison.Ordinal),
                Is.LessThan(source.IndexOf("BuildHubModule()", System.StringComparison.Ordinal)));
        }

        private static PopupOpenedEvent CreateConfirmPopupOpenedEvent()
        {
            return new PopupOpenedEvent(CreateConfirmPopupEntry());
        }

        private static PopupCompletedEvent CreateConfirmPopupCompletedEvent(PopupCompletionKind completionKind)
        {
            var entry = CreateConfirmPopupEntry();
            return CreatePopupCompletedEvent(entry, completionKind);
        }

        private static PopupCompletedEvent CreatePopupCompletedEvent(
            PopupEntry entry,
            PopupCompletionKind completionKind)
        {
            return new PopupCompletedEvent(entry, new PopupCompletion(
                entry.InstanceId,
                entry.PopupId,
                completionKind,
                PopupCloseReason.UserAction));
        }

        private static PopupEntry CreateCampaignModePopupEntry()
        {
            var payload = new ConfirmPopupPayload(
                new LocalizedTextDescriptor("UI", "ui.campaign.mode.title", LocalizedTextRole.Title),
                new LocalizedTextDescriptor("UI", "ui.campaign.mode.casual_detail"),
                new LocalizedTextDescriptor("UI", "ui.campaign.mode.casual", LocalizedTextRole.Button),
                new LocalizedTextDescriptor("UI", "ui.campaign.mode.hardcore", LocalizedTextRole.Button),
                false)
            { SecondaryIsAlternative = true, IsCampaignModeSelection = true };
            return new PopupEntry(new PopupInstanceId(2), PopupId.Confirm, payload, default, null);
        }

        private static PopupEntry CreateConfirmPopupEntry()
        {
            return new PopupEntry(
                new PopupInstanceId(1),
                PopupId.Confirm,
                new ConfirmPopupPayload("Title", "Body", "Confirm", "Cancel", true),
                default,
                null);
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }
    }
}

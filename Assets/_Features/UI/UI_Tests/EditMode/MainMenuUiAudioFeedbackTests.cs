using System.IO;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class MainMenuUiAudioFeedbackTests
    {
        private const string ControllerSourcePath =
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiAudioFeedbackController.cs";

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
        public void MainMenuUiAudioFeedback_SaveSlotNormalSelect_PlaysSelectCue()
        {
            var uiAudioPort = new RecordingUiAudioPort();
            var controller = new MainMenuUiAudioFeedbackController(uiAudioPort);

            controller.HandleSaveSlotIntentRequested(new SaveSlotIntent(1, SaveSlotIntentKind.Continue));

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Select }));
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

        private static PopupOpenedEvent CreateConfirmPopupOpenedEvent()
        {
            return new PopupOpenedEvent(CreateConfirmPopupEntry());
        }

        private static PopupCompletedEvent CreateConfirmPopupCompletedEvent(PopupCompletionKind completionKind)
        {
            var entry = CreateConfirmPopupEntry();
            return new PopupCompletedEvent(
                entry,
                new PopupCompletion(
                    entry.InstanceId,
                    entry.PopupId,
                    completionKind,
                    PopupCloseReason.UserAction));
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

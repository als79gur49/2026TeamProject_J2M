using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UIBlockPolicyTests
    {
        private static readonly ScreenEntry BlockingSettingsScreenEntry = new(
            new ScreenInstanceId(1),
            ScreenId.Settings,
            SettingsScreenPayload.Default,
            new ScreenPolicy(
                ScreenPolicyClass.Configuration,
                ScreenRetentionMode.RetainMountedHistory,
                ScreenBackAction.Pop,
                HudShellMode.Hidden,
                blocksUiGameplayInput: true),
            ScreenId.Settings.ToString());

        private static readonly ScreenEntry GameplayRootEntry = new(
            new ScreenInstanceId(2),
            ScreenId.Gameplay,
            GameplayRootPayload.Default,
            new ScreenPolicy(
                ScreenPolicyClass.GameplayRoot,
                ScreenRetentionMode.RetainMountedHistory,
                ScreenBackAction.None,
                HudShellMode.Visible,
                blocksUiGameplayInput: false),
            ScreenId.Gameplay.ToString());

        [Test]
        public void Evaluate_BlockingScreenWithoutPopup_BlocksHudAndGameplayInputOnly()
        {
            var policy = new UIBlockPolicy();

            var snapshot = policy.Evaluate(new UIFlowStateSnapshot(BlockingSettingsScreenEntry, null, popupCount: 0));

            Assert.That(snapshot.BlocksHudInteraction, Is.True);
            Assert.That(snapshot.BlocksScreenInteraction, Is.False);
            Assert.That(snapshot.BlocksUiGameplayInput, Is.True);
            Assert.That(snapshot.PopupConsumesBack, Is.False);
            Assert.That(snapshot.ShowsPopupDim, Is.False);
            Assert.That(snapshot.PopupBackdropMode, Is.EqualTo(PopupBackdropMode.None));
        }

        [Test]
        public void Evaluate_ModalPopupOnGameplayRoot_BlocksHudScreenInputAndShowsDim()
        {
            var policy = new UIBlockPolicy();

            var snapshot = policy.Evaluate(
                new UIFlowStateSnapshot(
                    GameplayRootEntry,
                    new PopupEntry(
                        new PopupInstanceId(1),
                        PopupId.Confirm,
                        new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false),
                        new PopupPolicy(
                            PopupPolicyClass.ModalBlocking,
                            PopupLifetimeScope.CurrentScreen,
                            PopupBackAction.Cancel,
                            PopupBackdropMode.Consume,
                            showsDim: true,
                            blocksLowerLayers: true),
                        completionCallback: null),
                    popupCount: 1));

            Assert.That(snapshot.BlocksHudInteraction, Is.True);
            Assert.That(snapshot.BlocksScreenInteraction, Is.True);
            Assert.That(snapshot.BlocksUiGameplayInput, Is.True);
            Assert.That(snapshot.PopupConsumesBack, Is.True);
            Assert.That(snapshot.ShowsPopupDim, Is.True);
            Assert.That(snapshot.BlocksLowerLayerPointer, Is.True);
            Assert.That(snapshot.PopupBackdropMode, Is.EqualTo(PopupBackdropMode.Consume));
        }

        [Test]
        public void Evaluate_NonModalBackdropClose_BlocksPointerWithoutBlockingScreenOwnership()
        {
            var policy = new UIBlockPolicy();

            var snapshot = policy.Evaluate(
                new UIFlowStateSnapshot(
                    GameplayRootEntry,
                    new PopupEntry(
                        new PopupInstanceId(3),
                        PopupId.Pause,
                        PausePopupPayload.Default,
                        new PopupPolicy(
                            PopupPolicyClass.NonModalInformational,
                            PopupLifetimeScope.CurrentScreen,
                            PopupBackAction.Close,
                            PopupBackdropMode.CloseTop,
                            showsDim: false,
                            blocksLowerLayers: false),
                        completionCallback: null),
                    popupCount: 1));

            Assert.That(snapshot.BlocksHudInteraction, Is.False);
            Assert.That(snapshot.BlocksScreenInteraction, Is.False);
            Assert.That(snapshot.BlocksUiGameplayInput, Is.False);
            Assert.That(snapshot.PopupConsumesBack, Is.True);
            Assert.That(snapshot.ShowsPopupDim, Is.False);
            Assert.That(snapshot.BlocksLowerLayerPointer, Is.True);
            Assert.That(snapshot.PopupBackdropMode, Is.EqualTo(PopupBackdropMode.CloseTop));
        }
    }
}

using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UIBlockPolicyTests
    {
        private static readonly ScreenEntry HelpScreenEntry = new(
            new ScreenInstanceId(1),
            ScreenId.Help,
            HelpScreenPayload.Default,
            new ScreenPolicy(
                ScreenPolicyClass.InformationalOverlay,
                ScreenRetentionMode.RetainMountedHistory,
                ScreenBackAction.Pop,
                HudShellMode.Visible,
                blocksUiGameplayInput: true),
            ScreenId.Help.ToString());

        private static readonly ScreenEntry GameplayScreenEntry = new(
            new ScreenInstanceId(2),
            ScreenId.Gameplay,
            GameplayScreenPayload.Default,
            new ScreenPolicy(
                ScreenPolicyClass.GameplayRoot,
                ScreenRetentionMode.RetainMountedHistory,
                ScreenBackAction.None,
                HudShellMode.Visible,
                blocksUiGameplayInput: false),
            ScreenId.Gameplay.ToString());

        [Test]
        public void Evaluate_HelpScreenWithoutPopup_BlocksHudAndGameplayInputOnly()
        {
            var policy = new UIBlockPolicy();

            var snapshot = policy.Evaluate(new UIFlowStateSnapshot(HelpScreenEntry, null, popupCount: 0));

            Assert.That(snapshot.BlocksHudInteraction, Is.True);
            Assert.That(snapshot.BlocksScreenInteraction, Is.False);
            Assert.That(snapshot.BlocksUiGameplayInput, Is.True);
            Assert.That(snapshot.PopupConsumesBack, Is.False);
            Assert.That(snapshot.ShowsPopupDim, Is.False);
            Assert.That(snapshot.PopupBackdropMode, Is.EqualTo(PopupBackdropMode.None));
        }

        [Test]
        public void Evaluate_ModalPopupOnGameplayScreen_BlocksHudScreenInputAndShowsDim()
        {
            var policy = new UIBlockPolicy();

            var snapshot = policy.Evaluate(
                new UIFlowStateSnapshot(
                    GameplayScreenEntry,
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
        public void Evaluate_TooltipPopup_KeepsDimAndLowerLayerBlockingOff()
        {
            var policy = new UIBlockPolicy();

            var snapshot = policy.Evaluate(
                new UIFlowStateSnapshot(
                    GameplayScreenEntry,
                    new PopupEntry(
                        new PopupInstanceId(2),
                        PopupId.Tooltip,
                        new TooltipPopupPayload("Tip", "Body"),
                        new PopupPolicy(
                            PopupPolicyClass.AnchoredEphemeral,
                            PopupLifetimeScope.CurrentScreen,
                            PopupBackAction.Close,
                            PopupBackdropMode.None,
                            showsDim: false,
                            blocksLowerLayers: false),
                        completionCallback: null),
                    popupCount: 1));

            Assert.That(snapshot.BlocksHudInteraction, Is.False);
            Assert.That(snapshot.BlocksScreenInteraction, Is.False);
            Assert.That(snapshot.BlocksUiGameplayInput, Is.False);
            Assert.That(snapshot.ShowsPopupDim, Is.False);
            Assert.That(snapshot.BlocksLowerLayerPointer, Is.False);
            Assert.That(snapshot.PopupBackdropMode, Is.EqualTo(PopupBackdropMode.None));
        }
    }
}

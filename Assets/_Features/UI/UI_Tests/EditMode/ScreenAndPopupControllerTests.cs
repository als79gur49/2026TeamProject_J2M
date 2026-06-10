using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class ScreenAndPopupControllerTests
    {
        [Test]
        public void ScreenController_RetainMountedHistory_Default_KeepsPreviousScreenMountedUntilRestored()
        {
            var runtimeFactory = new FakeScreenRuntimeFactory();
            using var controller = new ScreenController(runtimeFactory);

            controller.SetRoot(new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default, ScreenId.Gameplay.ToString()));

            Assert.That(controller.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(controller.BackStackCount, Is.EqualTo(0));

            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings-push")), Is.True);
            Assert.That(controller.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(controller.BackStackCount, Is.EqualTo(1));
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsDisposed, Is.False);
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsCurrent, Is.False);

            Assert.That(controller.Pop(), Is.True);
            Assert.That(controller.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(controller.BackStackCount, Is.EqualTo(0));
            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsDisposed, Is.True);
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsCurrent, Is.True);
        }

        [Test]
        public void ScreenController_DisposeOnHide_IsSupportedWithoutChangingControllerShape()
        {
            var runtimeFactory = new FakeScreenRuntimeFactory();
            runtimeFactory.SetPolicy(
                ScreenId.Settings,
                new ScreenPolicy(
                    ScreenPolicyClass.Configuration,
                    ScreenRetentionMode.DisposeOnHide,
                    ScreenBackAction.Pop,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true));
            using var controller = new ScreenController(runtimeFactory);

            controller.SetRoot(new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default, ScreenId.Gameplay.ToString()));
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings-pushed")), Is.True);
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings-replacement")), Is.True);

            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsDisposed, Is.True);

            Assert.That(controller.Pop(), Is.True);
            Assert.That(controller.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(runtimeFactory.CreatedRuntimes, Has.Count.EqualTo(4));
            Assert.That(runtimeFactory.CreatedRuntimes[3].Request.ScreenId, Is.EqualTo(ScreenId.Settings));
        }

        [Test]
        public void ScreenController_ReusesByKeyButAllowsDistinctInstancesWhenReuseKeyIsNull()
        {
            var runtimeFactory = new FakeScreenRuntimeFactory();
            using var controller = new ScreenController(runtimeFactory);

            controller.SetRoot(new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default, ScreenId.Gameplay.ToString()));
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings-a")), Is.True);
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings-b")), Is.True);
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, new SettingsScreenPayload("Updated", "Back"), "settings-a")), Is.True);

            Assert.That(controller.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
            Assert.That(controller.BackStackCount, Is.EqualTo(1));
            Assert.That(runtimeFactory.CreatedRuntimes, Has.Count.EqualTo(3));

            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, reuseKey: null)), Is.True);
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, reuseKey: null)), Is.True);
            Assert.That(controller.BackStackCount, Is.EqualTo(3));
            Assert.That(runtimeFactory.CreatedRuntimes, Has.Count.EqualTo(5));
        }

        [Test]
        public void ScreenController_PopTo_PrunesLaterHistoryAndRestoresNamedEntry_Deterministically()
        {
            var runtimeFactory = new FakeScreenRuntimeFactory();
            using var controller = new ScreenController(runtimeFactory);

            controller.SetRoot(new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default, ScreenId.Gameplay.ToString()));
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings-transition")), Is.True);
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings-overlay")), Is.True);

            Assert.That(controller.PopTo(ScreenId.Gameplay), Is.True);
            Assert.That(controller.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(controller.BackStackCount, Is.EqualTo(0));
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsDisposed, Is.False);
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsCurrent, Is.True);
            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsDisposed, Is.True);
            Assert.That(runtimeFactory.CreatedRuntimes[2].Runtime.IsDisposed, Is.True);
        }

        [Test]
        public void ScreenController_RelaysRuntimeRequestedActions_WithoutOwningFeatureLogic()
        {
            var runtimeFactory = new FakeScreenRuntimeFactory();
            using var controller = new ScreenController(runtimeFactory);
            ScreenAction? relayedAction = null;
            controller.ActionRequested += action => relayedAction = action;

            controller.SetRoot(new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default, ScreenId.Gameplay.ToString()));
            var emittedAction = ScreenAction.Popup(new PopupRequest(
                PopupId.Tooltip,
                new TooltipPopupPayload("Tip", "Body")));

            runtimeFactory.CreatedRuntimes[0].Runtime.Emit(emittedAction);

            Assert.That(relayedAction.HasValue, Is.True);
            Assert.That(relayedAction.Value.ActionKind, Is.EqualTo(ScreenActionKind.RequestPopup));
            Assert.That(relayedAction.Value.PopupRequest.PopupId, Is.EqualTo(PopupId.Tooltip));
        }

        [Test]
        public void ScreenController_ScreenTransitioned_FiresOnlyOnRealCurrentInstanceChanges()
        {
            var runtimeFactory = new FakeScreenRuntimeFactory();
            using var controller = new ScreenController(runtimeFactory);
            var transitions = new System.Collections.Generic.List<ScreenTransitionedEvent>();
            controller.ScreenTransitioned += transitions.Add;

            controller.SetRoot(new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default, ScreenId.Gameplay.ToString()));
            Assert.That(transitions, Is.Empty);

            Assert.That(controller.Show(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, ScreenId.Settings.ToString())), Is.True);
            Assert.That(transitions, Has.Count.EqualTo(1));
            Assert.That(transitions[0].Kind, Is.EqualTo(ScreenTransitionKind.Show));
            Assert.That(transitions[0].PreviousEntry.HasValue, Is.True);
            Assert.That(transitions[0].CurrentEntry.HasValue, Is.True);
            Assert.That(transitions[0].CurrentEntry.Value.ScreenId, Is.EqualTo(ScreenId.Settings));

            Assert.That(controller.Show(new ScreenRequest(
                ScreenId.Settings,
                new SettingsScreenPayload("Updated", "Back"),
                ScreenId.Settings.ToString())), Is.True);
            Assert.That(transitions, Has.Count.EqualTo(1));

            Assert.That(controller.Replace(new ScreenRequest(
                ScreenId.StageResult,
                new StageResultScreenPayload(
                    "Title",
                    "Summary",
                    "Detail",
                    "Continue",
                    StageNavigationRequest.None,
                    StageNavigationRequest.None,
                    StageNavigationRequest.None),
                ScreenId.StageResult.ToString())), Is.True);
            Assert.That(transitions, Has.Count.EqualTo(2));
            Assert.That(transitions[1].Kind, Is.EqualTo(ScreenTransitionKind.Replace));
            Assert.That(transitions[1].CurrentEntry.Value.ScreenId, Is.EqualTo(ScreenId.StageResult));
        }

        [Test]
        public void PopupController_PushCloseAndBackHandling_UseExplicitIdentityAndTopmostState()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Tooltip, new TooltipPopupPayload("Tip", "Tooltip body")),
                out var tooltipId), Is.True);
            Assert.That(controller.Push(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)),
                out var confirmId), Is.True);

            Assert.That(tooltipId.Equals(confirmId), Is.False);
            Assert.That(controller.PopupCount, Is.EqualTo(2));
            Assert.That(controller.TopPopup.HasValue, Is.True);
            Assert.That(controller.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Confirm));
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsTopmost, Is.False);
            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsTopmost, Is.True);

            Assert.That(controller.Close(tooltipId, PopupCloseReason.Programmatic), Is.True);
            Assert.That(controller.PopupCount, Is.EqualTo(1));
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsDisposed, Is.True);
            Assert.That(controller.TopPopup.Value.InstanceId, Is.EqualTo(confirmId));

            Assert.That(controller.HandleBackRequested(), Is.True);
            Assert.That(controller.PopupCount, Is.EqualTo(0));
            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsDisposed, Is.True);
        }

        [Test]
        public void PopupController_OnlyTopPopupInteractive_AndNonDismissibleRewardConsumesBack()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Tooltip, new TooltipPopupPayload("Tip", "Tooltip body")),
                out _), Is.True);
            Assert.That(controller.Push(
                new PopupRequest(
                    PopupId.Reward,
                    new RewardPopupPayload(
                        "Rewards",
                        new[] { new RewardPopupItemPayload("Crystal", 2) },
                        "Collected",
                        "Close")),
                out _), Is.True);

            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsTopmost, Is.False);
            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsTopmost, Is.True);
            Assert.That(controller.HandleBackRequested(), Is.True);
            Assert.That(controller.PopupCount, Is.EqualTo(2));

            runtimeFactory.CreatedRuntimes[1].Runtime.Emit(PopupCompletionKind.Acknowledged);
            Assert.That(controller.PopupCount, Is.EqualTo(1));
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsTopmost, Is.True);
        }

        [Test]
        public void PopupController_CloseAll_NotifiesInTopDownOrder_AndClearsStack()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);
            var completions = new System.Collections.Generic.List<PopupCompletion>();

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Tooltip, new TooltipPopupPayload("Tip", "Body"), completions.Add),
                out _), Is.True);
            Assert.That(controller.Push(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false), completions.Add),
                out _), Is.True);

            controller.CloseAll(PopupCloseReason.ScreenTransition);

            Assert.That(controller.PopupCount, Is.EqualTo(0));
            Assert.That(completions, Has.Count.EqualTo(2));
            Assert.That(completions[0].PopupId, Is.EqualTo(PopupId.Confirm));
            Assert.That(completions[1].PopupId, Is.EqualTo(PopupId.Tooltip));
            Assert.That(completions[0].CloseReason, Is.EqualTo(PopupCloseReason.ScreenTransition));
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsDisposed, Is.True);
            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsDisposed, Is.True);
        }

        [Test]
        public void PopupController_LifecycleSignalEvents_OnlyFireForSuccessfulUserVisibleTransitions()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);
            var opened = new System.Collections.Generic.List<PopupOpenedEvent>();
            var completed = new System.Collections.Generic.List<PopupCompletedEvent>();
            controller.PopupOpened += opened.Add;
            controller.PopupCompleted += completed.Add;

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Tooltip, new TooltipPopupPayload("Tip", "Body")),
                out var tooltipId), Is.True);
            Assert.That(opened, Has.Count.EqualTo(1));
            Assert.That(opened[0].Entry.PopupId, Is.EqualTo(PopupId.Tooltip));
            Assert.That(completed, Is.Empty);

            Assert.That(controller.Close(tooltipId, PopupCloseReason.Programmatic), Is.True);
            Assert.That(completed, Is.Empty);

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)),
                out _), Is.True);
            runtimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.Confirmed);
            Assert.That(completed, Has.Count.EqualTo(1));
            Assert.That(completed[0].Completion.CloseReason, Is.EqualTo(PopupCloseReason.UserAction));
            Assert.That(completed[0].Completion.CompletionKind, Is.EqualTo(PopupCompletionKind.Confirmed));

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Tooltip, new TooltipPopupPayload("Tip", "Body")),
                out _), Is.True);
            controller.CloseAll(PopupCloseReason.ScreenTransition);
            Assert.That(completed, Has.Count.EqualTo(1));
        }

        [Test]
        public void PopupController_HandleBackdropClicked_UsesOnlyTopPopupPolicy()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Tooltip, new TooltipPopupPayload("Tip", "Tooltip body")),
                out _), Is.True);
            Assert.That(controller.Push(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)),
                out var confirmId), Is.True);

            Assert.That(controller.HandleBackdropClicked(), Is.True);
            Assert.That(controller.PopupCount, Is.EqualTo(2));

            Assert.That(controller.Close(confirmId, PopupCloseReason.Programmatic), Is.True);
            Assert.That(controller.TopPopup.HasValue, Is.True);
            Assert.That(controller.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Tooltip));
            Assert.That(controller.HandleBackdropClicked(), Is.False);
            Assert.That(controller.PopupCount, Is.EqualTo(1));
        }

    }
}

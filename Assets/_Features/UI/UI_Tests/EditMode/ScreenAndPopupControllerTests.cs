using Game.Feature.Gameplay.UIAccess.DebugCommands;
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

            Assert.That(controller.Push(new ScreenRequest(ScreenId.ObjectiveStatus, ObjectiveStatusScreenPayload.Default, ScreenId.ObjectiveStatus.ToString())), Is.True);
            Assert.That(controller.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
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
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, ScreenId.Settings.ToString())), Is.True);
            Assert.That(controller.Push(new ScreenRequest(ScreenId.ObjectiveStatus, ObjectiveStatusScreenPayload.Default, ScreenId.ObjectiveStatus.ToString())), Is.True);

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
            Assert.That(controller.Push(new ScreenRequest(ScreenId.ObjectiveStatus, ObjectiveStatusScreenPayload.Default, ScreenId.ObjectiveStatus.ToString())), Is.True);
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, ScreenId.Settings.ToString())), Is.True);
            Assert.That(controller.Push(new ScreenRequest(ScreenId.ObjectiveStatus, new ObjectiveStatusScreenPayload("Updated"), ScreenId.ObjectiveStatus.ToString())), Is.True);

            Assert.That(controller.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
            Assert.That(controller.BackStackCount, Is.EqualTo(1));
            Assert.That(runtimeFactory.CreatedRuntimes, Has.Count.EqualTo(3));

            Assert.That(controller.Push(new ScreenRequest(ScreenId.ObjectiveStatus, ObjectiveStatusScreenPayload.Default, reuseKey: null)), Is.True);
            Assert.That(controller.Push(new ScreenRequest(ScreenId.ObjectiveStatus, ObjectiveStatusScreenPayload.Default, reuseKey: null)), Is.True);
            Assert.That(controller.BackStackCount, Is.EqualTo(3));
            Assert.That(runtimeFactory.CreatedRuntimes, Has.Count.EqualTo(5));
        }

        [Test]
        public void ScreenController_PopTo_PrunesLaterHistoryAndRestoresNamedEntry_Deterministically()
        {
            var runtimeFactory = new FakeScreenRuntimeFactory();
            using var controller = new ScreenController(runtimeFactory);

            controller.SetRoot(new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default, ScreenId.Gameplay.ToString()));
            Assert.That(controller.Push(new ScreenRequest(ScreenId.ObjectiveStatus, ObjectiveStatusScreenPayload.Default, ScreenId.ObjectiveStatus.ToString())), Is.True);
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, ScreenId.Settings.ToString())), Is.True);

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

            Assert.That(controller.Show(new ScreenRequest(ScreenId.ObjectiveStatus, ObjectiveStatusScreenPayload.Default, ScreenId.ObjectiveStatus.ToString())), Is.True);
            Assert.That(transitions, Has.Count.EqualTo(1));
            Assert.That(transitions[0].Kind, Is.EqualTo(ScreenTransitionKind.Show));
            Assert.That(transitions[0].PreviousEntry.HasValue, Is.True);
            Assert.That(transitions[0].CurrentEntry.HasValue, Is.True);
            Assert.That(transitions[0].CurrentEntry.Value.ScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));

            Assert.That(controller.Show(new ScreenRequest(
                ScreenId.ObjectiveStatus,
                new ObjectiveStatusScreenPayload("Updated"),
                ScreenId.ObjectiveStatus.ToString())), Is.True);
            Assert.That(transitions, Has.Count.EqualTo(1));

            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, ScreenId.Settings.ToString())), Is.True);
            Assert.That(transitions, Has.Count.EqualTo(2));
            Assert.That(transitions[1].Kind, Is.EqualTo(ScreenTransitionKind.Push));

            Assert.That(controller.Pop(), Is.True);
            Assert.That(transitions, Has.Count.EqualTo(3));
            Assert.That(transitions[2].Kind, Is.EqualTo(ScreenTransitionKind.Pop));
            Assert.That(transitions[2].CurrentEntry.Value.ScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
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

        [Test]
        public void DebugCommandsView_EmitsCommandsOnly()
        {
            var root = new GameObject("DebugCommandsViewTest");
            try
            {
                var view = root.AddComponent<DebugCommandsPopupView>();
                var viewModel = new DebugCommandsPopupViewModel();
                view.Bind(viewModel);
                viewModel.SetContent(CreateDebugPayload(canNext: true, canForceClear: true));
                view.IsVisible = true;
                view.SetIsTopmost(true);
                var nextCount = 0;
                var forceClearCount = 0;
                var closeCount = 0;
                view.NextStageRequested += () => nextCount++;
                view.ForceClearResultOnlyRequested += () => forceClearCount++;
                view.CompletionRequested += completion =>
                {
                    if (completion == PopupCompletionKind.Closed)
                    {
                        closeCount++;
                    }
                };

                view.ClickNextStage();
                view.ClickForceClearResultOnly();
                view.ClickClose();

                Assert.That(nextCount, Is.EqualTo(1));
                Assert.That(forceClearCount, Is.EqualTo(1));
                Assert.That(closeCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DebugNextStage_ButtonDisabledWhenUnavailable()
        {
            var root = new GameObject("DebugCommandsDisabledNextTest");
            try
            {
                var view = root.AddComponent<DebugCommandsPopupView>();
                var viewModel = new DebugCommandsPopupViewModel();
                view.Bind(viewModel);
                viewModel.SetContent(CreateDebugPayload(canNext: false, canForceClear: true));
                view.IsVisible = true;
                view.SetIsTopmost(true);
                var nextCount = 0;
                view.NextStageRequested += () => nextCount++;

                view.ClickNextStage();

                Assert.That(nextCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DebugForceClearResultOnly_ContinueDoesNotBypassDebugConstraint()
        {
            var root = new GameObject("DebugStageResultContinueDisabledTest");
            try
            {
                var view = root.AddComponent<StageResultScreenView>();
                var viewModel = new StageResultScreenViewModel();
                view.Bind(viewModel);
                viewModel.SetContent(
                    "DEBUG FORCED CLEAR",
                    "Result Only mode",
                    "NO SAVE / NO REWARD",
                    "Next stage unavailable",
                    isContinueEnabled: false);
                view.IsVisible = true;
                var continueCount = 0;
                view.ContinueRequested += () => continueCount++;

                view.ClickContinue();

                Assert.That(continueCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DebugCommandsPopup_DevGatePreventsProductionCreation()
        {
            using var harness = new PopupLayerHarness();
            var factory = new GameplayPopupRuntimeFactory(
                harness.PopupLayerView,
                ScriptableObject.CreateInstance<PopupPrefabCatalog>(),
                DebugCommandAccess.Disabled,
                isDebugCommandsRuntimeEnabled: () => false);

            Assert.Throws<System.InvalidOperationException>(() => factory.Create(new PopupRequest(
                PopupId.DebugCommands,
                CreateDebugPayload(canNext: false, canForceClear: false))));
        }

        [Test]
        public void DebugCommandsPopup_UsesExpectedBlockingPolicy()
        {
            using var harness = new PopupLayerHarness();
            var factory = new GameplayPopupRuntimeFactory(
                harness.PopupLayerView,
                ScriptableObject.CreateInstance<PopupPrefabCatalog>(),
                DebugCommandAccess.Enabled(new FakeDebugStageCommandPort()),
                isDebugCommandsRuntimeEnabled: () => true);

            var result = factory.Create(new PopupRequest(
                PopupId.DebugCommands,
                CreateDebugPayload(canNext: true, canForceClear: true)));
            try
            {
                Assert.That(result.Policy.PolicyClass, Is.EqualTo(PopupPolicyClass.ModalBlocking));
                Assert.That(result.Policy.BackdropMode, Is.EqualTo(PopupBackdropMode.Consume));
                Assert.That(result.Policy.BackAction, Is.EqualTo(PopupBackAction.Close));
                Assert.That(result.Policy.ShowsDim, Is.True);
                Assert.That(result.Policy.BlocksLowerLayers, Is.True);
            }
            finally
            {
                result.Runtime.Dispose();
            }
        }

        private static DebugCommandsPopupPayload CreateDebugPayload(bool canNext, bool canForceClear)
        {
            return new DebugCommandsPopupPayload(
                "Debug Commands",
                "Debug build: enabled",
                "Current StageId: stage-0-1",
                canNext ? "Next StageId: stage-0-2" : "Next StageId: No next stage",
                "Topology/Presentation lock: clear",
                canForceClear ? "Force Clear Result Only: available" : "Force Clear Result Only: unavailable",
                string.Empty,
                canNext,
                canForceClear);
        }

        private sealed class PopupLayerHarness : System.IDisposable
        {
            private readonly GameObject _root;

            public PopupLayerHarness()
            {
                _root = new GameObject("DebugPopupLayerHarness", typeof(RectTransform));
                var layerObject = new GameObject("PopupLayer", typeof(RectTransform));
                layerObject.transform.SetParent(_root.transform, false);
                var contentObject = new GameObject("ContentRoot", typeof(RectTransform));
                contentObject.transform.SetParent(layerObject.transform, false);
                var backdropObject = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
                backdropObject.transform.SetParent(layerObject.transform, false);
                PopupLayerView = layerObject.AddComponent<PopupLayerView>();
                PopupLayerView.Configure(
                    layerObject,
                    backdropObject.GetComponent<CanvasGroup>(),
                    backdropObject.GetComponent<Image>(),
                    backdropObject.GetComponent<Button>(),
                    contentObject.GetComponent<RectTransform>());
            }

            public PopupLayerView PopupLayerView { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(_root);
            }
        }

        private sealed class FakeDebugStageCommandPort : IDebugStageCommandPort
        {
            public DebugCommandAvailabilitySnapshot GetAvailability()
            {
                return new DebugCommandAvailabilitySnapshot(
                    isDebugBuildEnabled: true,
                    canOpenPanel: true,
                    canGoNextStage: true,
                    canForceClearResultOnly: true,
                    isPresentationLocked: false,
                    reasonText: "Ready.",
                    currentStageId: StageId.CreateOrThrow("stage-0-1"),
                    nextStageId: StageId.CreateOrThrow("stage-0-2"));
            }

            public DebugCommandResult GoToNextStage()
            {
                return DebugCommandResult.Success("next");
            }

            public DebugCommandResult ForceClearResultOnly()
            {
                return DebugCommandResult.Success("force clear");
            }
        }
    }
}

using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
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
            Assert.That(controller.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings-a")), Is.True);

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
                PopupId.Confirm,
                new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)));

            runtimeFactory.CreatedRuntimes[0].Runtime.Emit(emittedAction);

            Assert.That(relayedAction.HasValue, Is.True);
            Assert.That(relayedAction.Value.ActionKind, Is.EqualTo(ScreenActionKind.RequestPopup));
            Assert.That(relayedAction.Value.PopupRequest.PopupId, Is.EqualTo(PopupId.Confirm));
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
                SettingsScreenPayload.Default,
                ScreenId.Settings.ToString())), Is.True);
            Assert.That(transitions, Has.Count.EqualTo(1));

            Assert.That(controller.Replace(new ScreenRequest(
                ScreenId.StageResult,
                new StageResultScreenPayload(
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
                new PopupRequest(PopupId.Pause, PausePopupPayload.Default),
                out var pauseId), Is.True);
            Assert.That(controller.Push(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)),
                out var confirmId), Is.True);

            Assert.That(pauseId.Equals(confirmId), Is.False);
            Assert.That(controller.PopupCount, Is.EqualTo(2));
            Assert.That(controller.TopPopup.HasValue, Is.True);
            Assert.That(controller.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Confirm));
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsTopmost, Is.False);
            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsTopmost, Is.True);

            Assert.That(controller.Close(pauseId, PopupCloseReason.Programmatic), Is.True);
            Assert.That(controller.PopupCount, Is.EqualTo(1));
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsDisposed, Is.True);
            Assert.That(controller.TopPopup.Value.InstanceId, Is.EqualTo(confirmId));

            Assert.That(controller.HandleBackRequested(), Is.True);
            Assert.That(controller.PopupCount, Is.EqualTo(0));
            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsDisposed, Is.True);
        }

        [Test]
        public void PopupController_OnlyTopPopupInteractive_AndBackClosesTopPopup()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Pause, PausePopupPayload.Default),
                out _), Is.True);
            Assert.That(controller.Push(
                new PopupRequest(
                    PopupId.Confirm,
                    new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)),
                out _), Is.True);

            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsTopmost, Is.False);
            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsTopmost, Is.True);
            Assert.That(controller.HandleBackRequested(), Is.True);
            Assert.That(controller.PopupCount, Is.EqualTo(1));
            Assert.That(runtimeFactory.CreatedRuntimes[1].Runtime.IsDisposed, Is.True);
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsTopmost, Is.True);

            Assert.That(controller.HandleBackRequested(), Is.True);
            Assert.That(controller.PopupCount, Is.EqualTo(0));
            Assert.That(runtimeFactory.CreatedRuntimes[0].Runtime.IsDisposed, Is.True);
        }

        [Test]
        public void RetryCompletionCallbackThrows_RestoresRetainedPopupRuntime()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);
            var dispatchCount = 0;
            Assert.That(
                controller.Push(
                    new PopupRequest(
                        PopupId.Pause,
                        PausePopupPayload.Default,
                        _ =>
                        {
                            dispatchCount++;
                            if (dispatchCount == 1)
                            {
                                throw new InvalidOperationException("Injected retry routing failure.");
                            }
                        }),
                    out var popupId),
                Is.True);
            var runtime = runtimeFactory.CreatedRuntimes[0].Runtime;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                runtime.Emit(PopupCompletionKind.RetryRequested));

            Assert.That(exception.Message, Is.EqualTo("Injected retry routing failure."));
            Assert.That(controller.PopupCount, Is.EqualTo(1));
            Assert.That(controller.TopPopup.Value.InstanceId, Is.EqualTo(popupId));
            Assert.That(runtime.IsDisposed, Is.False);
            Assert.That(runtime.IsTopmost, Is.True);

            runtime.Emit(PopupCompletionKind.RetryRequested);

            Assert.That(dispatchCount, Is.EqualTo(2));
        }

        [Test]
        public void MainMenuCompletionCallbackThrows_RestoresRetainedPopupRuntime()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);
            var dispatchCount = 0;
            Assert.That(
                controller.Push(
                    new PopupRequest(
                        PopupId.Pause,
                        PausePopupPayload.Default,
                        completion =>
                        {
                            if (completion.CompletionKind !=
                                PopupCompletionKind.MainMenuRequested)
                            {
                                return;
                            }

                            dispatchCount++;
                            if (dispatchCount == 1)
                            {
                                throw new InvalidOperationException(
                                    "Injected Main Menu routing failure.");
                            }
                        }),
                    out var popupId),
                Is.True);
            var runtime = runtimeFactory.CreatedRuntimes[0].Runtime;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                runtime.Emit(PopupCompletionKind.MainMenuRequested));

            Assert.That(
                exception.Message,
                Is.EqualTo("Injected Main Menu routing failure."));
            Assert.That(controller.PopupCount, Is.EqualTo(1));
            Assert.That(controller.TopPopup.Value.InstanceId, Is.EqualTo(popupId));
            Assert.That(runtime.IsDisposed, Is.False);
            Assert.That(runtime.IsTopmost, Is.True);

            runtime.Emit(PopupCompletionKind.MainMenuRequested);

            Assert.That(dispatchCount, Is.EqualTo(2));
        }

        [Test]
        public void RoutingThrow_WithNewPopupPushed_PreservesNewPopupAsTopmost()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);
            var stateChangedCount = 0;
            controller.StateChanged += () => stateChangedCount++;
            PopupInstanceId newerPopupId = default;
            Assert.That(
                controller.Push(
                    new PopupRequest(
                        PopupId.Pause,
                        PausePopupPayload.Default,
                        completion =>
                        {
                            if (completion.CompletionKind !=
                                PopupCompletionKind.RetryRequested)
                            {
                                return;
                            }

                            controller.Push(
                                new PopupRequest(
                                    PopupId.Confirm,
                                    new ConfirmPopupPayload(
                                        "Confirm",
                                        "Body",
                                        "Yes",
                                        "No",
                                        false)),
                                out newerPopupId);
                            throw new InvalidOperationException(
                                "Injected routing failure after popup push.");
                        }),
                    out var retainedPopupId),
                Is.True);
            var retainedRuntime = runtimeFactory.CreatedRuntimes[0].Runtime;

            Assert.Throws<InvalidOperationException>(() =>
                retainedRuntime.Emit(PopupCompletionKind.RetryRequested));

            Assert.That(controller.PopupCount, Is.EqualTo(2));
            Assert.That(
                controller.TopPopup.Value.InstanceId,
                Is.EqualTo(newerPopupId));
            Assert.That(
                controller.TopPopup.Value.InstanceId,
                Is.Not.EqualTo(retainedPopupId));
            Assert.That(retainedRuntime.IsTopmost, Is.False);
            Assert.That(
                runtimeFactory.CreatedRuntimes[1].Runtime.IsTopmost,
                Is.True);
            Assert.That(stateChangedCount, Is.EqualTo(4));
        }

        [Test]
        public void CallbackClosesRetainedPopupThenThrows_DoesNotResurrectPopup()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);
            PopupInstanceId popupId = default;
            Assert.That(
                controller.Push(
                    new PopupRequest(
                        PopupId.Pause,
                        PausePopupPayload.Default,
                        completion =>
                        {
                            if (completion.CompletionKind !=
                                PopupCompletionKind.RetryRequested)
                            {
                                return;
                            }

                            Assert.That(
                                controller.Close(
                                    popupId,
                                    PopupCloseReason.Programmatic),
                                Is.True);
                            throw new InvalidOperationException(
                                "Injected failure after retained popup close.");
                        }),
                    out popupId),
                Is.True);
            var runtime = runtimeFactory.CreatedRuntimes[0].Runtime;

            Assert.Throws<InvalidOperationException>(() =>
                runtime.Emit(PopupCompletionKind.RetryRequested));

            Assert.That(controller.PopupCount, Is.Zero);
            Assert.That(runtime.IsDisposed, Is.True);
            Assert.DoesNotThrow(() =>
                runtime.Emit(PopupCompletionKind.RetryRequested));
        }

        [Test]
        public void CallbackCloseAllThenThrows_DoesNotRestoreDisposedRuntime()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);
            Assert.That(
                controller.Push(
                    new PopupRequest(
                        PopupId.Pause,
                        PausePopupPayload.Default,
                        completion =>
                        {
                            if (completion.CompletionKind !=
                                PopupCompletionKind.MainMenuRequested)
                            {
                                return;
                            }

                            controller.CloseAll(PopupCloseReason.ScreenTransition);
                            throw new InvalidOperationException(
                                "Injected failure after CloseAll.");
                        }),
                    out _),
                Is.True);
            var retainedRuntime = runtimeFactory.CreatedRuntimes[0].Runtime;
            Assert.That(
                controller.Push(
                    new PopupRequest(
                        PopupId.Confirm,
                        new ConfirmPopupPayload(
                            "Confirm",
                            "Body",
                            "Yes",
                            "No",
                            false)),
                    out _),
                Is.True);
            Assert.That(controller.CloseTop(PopupCloseReason.Programmatic), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                retainedRuntime.Emit(PopupCompletionKind.MainMenuRequested));

            Assert.That(controller.PopupCount, Is.Zero);
            Assert.That(retainedRuntime.IsDisposed, Is.True);
            Assert.DoesNotThrow(() =>
                retainedRuntime.Emit(PopupCompletionKind.MainMenuRequested));
        }

        [Test]
        public void PopupController_CloseAll_NotifiesInTopDownOrder_AndClearsStack()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            using var controller = new PopupController(runtimeFactory);
            var completions = new System.Collections.Generic.List<PopupCompletion>();

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Pause, PausePopupPayload.Default, completions.Add),
                out _), Is.True);
            Assert.That(controller.Push(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false), completions.Add),
                out _), Is.True);

            controller.CloseAll(PopupCloseReason.ScreenTransition);

            Assert.That(controller.PopupCount, Is.EqualTo(0));
            Assert.That(completions, Has.Count.EqualTo(2));
            Assert.That(completions[0].PopupId, Is.EqualTo(PopupId.Confirm));
            Assert.That(completions[1].PopupId, Is.EqualTo(PopupId.Pause));
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
                new PopupRequest(PopupId.Pause, PausePopupPayload.Default),
                out var pauseId), Is.True);
            Assert.That(opened, Has.Count.EqualTo(1));
            Assert.That(opened[0].Entry.PopupId, Is.EqualTo(PopupId.Pause));
            Assert.That(completed, Is.Empty);

            Assert.That(controller.Close(pauseId, PopupCloseReason.Programmatic), Is.True);
            Assert.That(completed, Is.Empty);

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)),
                out _), Is.True);
            runtimeFactory.CreatedRuntimes[^1].Runtime.Emit(PopupCompletionKind.Confirmed);
            Assert.That(completed, Has.Count.EqualTo(1));
            Assert.That(completed[0].Completion.CloseReason, Is.EqualTo(PopupCloseReason.UserAction));
            Assert.That(completed[0].Completion.CompletionKind, Is.EqualTo(PopupCompletionKind.Confirmed));

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Pause, PausePopupPayload.Default),
                out _), Is.True);
            controller.CloseAll(PopupCloseReason.ScreenTransition);
            Assert.That(completed, Has.Count.EqualTo(1));
        }

        [Test]
        public void PopupController_HandleBackdropClicked_UsesOnlyTopPopupPolicy()
        {
            var runtimeFactory = new FakePopupRuntimeFactory();
            runtimeFactory.SetPolicy(
                PopupId.Pause,
                new PopupPolicy(
                    PopupPolicyClass.ModalBlocking,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Close,
                    PopupBackdropMode.None,
                    showsDim: false,
                    blocksLowerLayers: false));
            using var controller = new PopupController(runtimeFactory);

            Assert.That(controller.Push(
                new PopupRequest(PopupId.Pause, PausePopupPayload.Default),
                out _), Is.True);
            Assert.That(controller.Push(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)),
                out var confirmId), Is.True);

            Assert.That(controller.HandleBackdropClicked(), Is.True);
            Assert.That(controller.PopupCount, Is.EqualTo(2));

            Assert.That(controller.Close(confirmId, PopupCloseReason.Programmatic), Is.True);
            Assert.That(controller.TopPopup.HasValue, Is.True);
            Assert.That(controller.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Pause));
            Assert.That(controller.HandleBackdropClicked(), Is.False);
            Assert.That(controller.PopupCount, Is.EqualTo(1));
        }

    }
}

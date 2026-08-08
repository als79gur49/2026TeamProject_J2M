using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class UiScreenRuntimeAudioCueTests
    {
        [Test]
        public void GameplayScreenRuntimeFactory_SettingsLocalInteractions_EmitOnlyMappedLocalCues()
        {
            using var harness = UiAudioHarness.Create();

            Assert.That(harness.Coordinator.OpenSettingsScreen(), Is.True);
            harness.UiAudioPort.Clear();

            var view = harness.ScreenLayerView.FindScreenView<SettingsScreenView>();
            Assert.That(view, Is.Not.Null);

            view.AudioView.SetMuted(AudioSettingsChannel.Sfx, true);
            view.AudioView.BeginInteraction(AudioSettingsChannel.Main);
            view.AudioView.SetVolume(AudioSettingsChannel.Main, 0.25f);
            view.AudioView.CommitInteraction(AudioSettingsChannel.Main);
            view.ClickDisplayTab();
            view.DisplayView.SelectResolution(1);
            view.DisplayView.SetFullscreen(true);

            Assert.That(
                harness.UiAudioPort.PlayedCueIds,
                Is.EqualTo(new[]
                {
                    UiAudioCueId.Toggle,
                    UiAudioCueId.AdjustValueCommit,
                    UiAudioCueId.Select,
                    UiAudioCueId.Select,
                    UiAudioCueId.Toggle,
                }));

            harness.UiAudioPort.Clear();
            view.DisplayView.ClickRevert();
            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Cancel }));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsInputMovementToggleClick_EmitsToggleCue()
        {
            var keyboardPort = new CompletingKeyboardSettingsPort();
            using var harness = UiAudioHarness.Create(keyboardPort);

            Assert.That(harness.Coordinator.OpenSettingsScreen(), Is.True);
            var view = harness.ScreenLayerView.FindScreenView<SettingsScreenView>();
            Assert.That(view, Is.Not.Null);
            view.ClickInputTab();
            harness.UiAudioPort.Clear();

            var movementToggle = view.InputView.transform
                .Find("MovementInputRow/WASDKeyDisplay")
                .GetComponent<Button>();
            Assert.That(movementToggle, Is.Not.Null);
            Assert.That(movementToggle.GetComponent<UiHoverScaleEffect>(), Is.Not.Null);

            movementToggle.onClick.Invoke();

            Assert.That(view.InputView.IsMovementUsingArrowKeys, Is.True);

            movementToggle.onClick.Invoke();

            Assert.That(view.InputView.IsMovementUsingArrowKeys, Is.False);
            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[]
            {
                UiAudioCueId.Toggle,
                UiAudioCueId.Toggle,
            }));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsInputKeycapRebind_EmitsSelectThenConfirmCues()
        {
            var keyboardPort = new CompletingKeyboardSettingsPort();
            using var harness = UiAudioHarness.Create(keyboardPort);

            Assert.That(harness.Coordinator.OpenSettingsScreen(), Is.True);
            var view = harness.ScreenLayerView.FindScreenView<SettingsScreenView>();
            Assert.That(view, Is.Not.Null);
            view.ClickInputTab();
            harness.UiAudioPort.Clear();

            view.InputView.ClickPushRebind();

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Select }));

            harness.UiAudioPort.Clear();
            keyboardPort.CompleteRebind(KeyboardBindingValidationResult.Success);

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Confirm }));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_StageResultContinue_EmitsStageLaunchCue()
        {
            using var harness = UiAudioHarness.Create();
            var request = CreateStageNavigationRequest(StageNavigationKind.NextStage);
            var payload = new StageResultScreenPayload(
                request,
                StageNavigationRequest.None,
                request);

            Assert.That(
                harness.ScreenController.Show(new ScreenRequest(ScreenId.StageResult, payload, "stage-result-audio")),
                Is.True);
            harness.UiAudioPort.Clear();

            var view = harness.ScreenLayerView.FindScreenView<StageResultScreenView>();
            Assert.That(view, Is.Not.Null);
            view.ClickContinue();

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.StageLaunch }));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_LevelFailedRestart_EmitsStageLaunchCue()
        {
            using var harness = UiAudioHarness.Create();
            var request = CreateStageNavigationRequest(StageNavigationKind.Retry);
            var payload = new LevelFailedScreenPayload(
                TerminalResultTextDescriptors.ChancesExhaustedDetail,
                request);

            Assert.That(
                harness.ScreenController.Show(new ScreenRequest(ScreenId.LevelFailed, payload, "level-failed-audio")),
                Is.True);
            harness.UiAudioPort.Clear();

            var view = harness.ScreenLayerView.FindScreenView<LevelFailedScreenView>();
            Assert.That(view, Is.Not.Null);
            view.ClickRestartLevel();

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.StageLaunch }));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_LevelFailedMain_EmitsSelectCue()
        {
            using var harness = UiAudioHarness.Create();
            var payload = new LevelFailedScreenPayload(
                TerminalResultTextDescriptors.ChancesExhaustedDetail,
                CreateStageNavigationRequest(StageNavigationKind.Retry));

            Assert.That(
                harness.ScreenController.Show(new ScreenRequest(ScreenId.LevelFailed, payload, "level-failed-main-audio")),
                Is.True);
            harness.UiAudioPort.Clear();

            var view = harness.ScreenLayerView.FindScreenView<LevelFailedScreenView>();
            Assert.That(view, Is.Not.Null);
            view.ClickMain();

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Select }));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_GameClearMain_EmitsSelectCue()
        {
            using var harness = UiAudioHarness.Create();

            Assert.That(
                harness.ScreenController.Show(new ScreenRequest(
                    ScreenId.GameClear,
                    GameClearScreenPayload.Default,
                    "game-clear-main-audio")),
                Is.True);
            harness.UiAudioPort.Clear();

            var view = harness.ScreenLayerView.FindScreenView<GameClearScreenView>();
            Assert.That(view, Is.Not.Null);
            view.ClickMain();

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Select }));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsCurrentSectionTab_DoesNotEmitCue()
        {
            using var harness = UiAudioHarness.Create();

            Assert.That(harness.Coordinator.OpenSettingsScreen(), Is.True);
            harness.UiAudioPort.Clear();

            var view = harness.ScreenLayerView.FindScreenView<SettingsScreenView>();
            Assert.That(view, Is.Not.Null);

            view.ClickAudioTab();

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.Empty);
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsDisplayApply_EmitsOnlySingleForwardFlowCue()
        {
            using var harness = UiAudioHarness.Create();

            Assert.That(harness.Coordinator.OpenSettingsScreen(), Is.True);
            var view = harness.ScreenLayerView.FindScreenView<SettingsScreenView>();
            Assert.That(view, Is.Not.Null);

            view.ClickDisplayTab();
            view.DisplayView.SelectResolution(1);
            harness.UiAudioPort.Clear();

            view.DisplayView.ClickApply();

            Assert.That(harness.PopupController.Contains(PopupId.Confirm), Is.True);
            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.NavigateForward }));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsBackButton_EmitsSingleFlowBackCue()
        {
            using var harness = UiAudioHarness.Create();

            Assert.That(harness.Coordinator.OpenSettingsScreen(), Is.True);
            harness.UiAudioPort.Clear();

            var view = harness.ScreenLayerView.FindScreenView<SettingsScreenView>();
            Assert.That(view, Is.Not.Null);

            view.ClickBack();

            Assert.That(harness.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.NavigateBack }));
            Assert.That(harness.Coordinator.LastFlowAudioTrace.OutcomeKind, Is.EqualTo(UiFlowAudioOutcomeKind.NavigateBack));
        }

        private static StageNavigationRequest CreateStageNavigationRequest(StageNavigationKind navigationKind)
        {
            return new StageNavigationRequest(
                StageId.CreateOrThrow("stage-1-1"),
                navigationKind,
                "ui-audio-test",
                transitionIntent: navigationKind == StageNavigationKind.NextStage
                    ? SceneTransitionIntent.StageAdvance
                    : SceneTransitionIntent.ManualRetry);
        }

        private sealed class UiAudioHarness : System.IDisposable
        {
            private readonly GameObject _rootObject;

            private UiAudioHarness(
                GameObject rootObject,
                ScreenLayerView screenLayerView,
                ScreenController screenController,
                PopupController popupController,
                UIFlowCoordinator coordinator,
                RecordingUiAudioPort uiAudioPort)
            {
                _rootObject = rootObject;
                ScreenLayerView = screenLayerView;
                ScreenController = screenController;
                PopupController = popupController;
                Coordinator = coordinator;
                UiAudioPort = uiAudioPort;
            }

            public ScreenLayerView ScreenLayerView { get; }

            public ScreenController ScreenController { get; }

            public PopupController PopupController { get; }

            public UIFlowCoordinator Coordinator { get; }

            public RecordingUiAudioPort UiAudioPort { get; }

            public static UiAudioHarness Create(IKeyboardBindingSettingsPort keyboardBindingSettingsPort = null)
            {
                var rootObject = new GameObject("UiScreenRuntimeAudioCueTests_Harness");

                var screenLayerRoot = new GameObject("ScreenLayerRoot", typeof(RectTransform));
                screenLayerRoot.transform.SetParent(rootObject.transform, false);
                var contentRootObject = new GameObject("ScreenContentRoot", typeof(RectTransform));
                contentRootObject.transform.SetParent(screenLayerRoot.transform, false);
                var screenLayerView = screenLayerRoot.AddComponent<ScreenLayerView>();
                screenLayerView.Configure(screenLayerRoot, contentRootObject.GetComponent<RectTransform>());

                var popupLayerRoot = new GameObject("PopupLayerRoot", typeof(RectTransform));
                popupLayerRoot.transform.SetParent(rootObject.transform, false);
                var popupRoot = new GameObject("PopupRoot", typeof(RectTransform));
                popupRoot.transform.SetParent(popupLayerRoot.transform, false);
                var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
                backdrop.transform.SetParent(popupRoot.transform, false);
                var popupContentRootObject = new GameObject("PopupContentRoot", typeof(RectTransform));
                popupContentRootObject.transform.SetParent(popupRoot.transform, false);
                var popupLayerView = popupLayerRoot.AddComponent<PopupLayerView>();
                popupLayerView.Configure(
                    popupRoot,
                    backdrop.GetComponent<CanvasGroup>(),
                    backdrop.GetComponent<Image>(),
                    backdrop.GetComponent<Button>(),
                    popupContentRootObject.GetComponent<RectTransform>());

                var popupController = new PopupController(new GameplayPopupRuntimeFactory(
                    popupLayerView,
                    UiTestPrefabAssetUtility.LoadPopupCatalog(),
                    localizedTextResolver: PackageFreeLocalizedTextResolver.CreateSettingsDefault()));
                var timeoutRelay = rootObject.AddComponent<DisplayPreviewTimeoutRelay>();
                var lifecycleRelay = rootObject.AddComponent<DisplaySettingsLifecycleRelay>();
                var previewSessionHost = new DisplayPreviewSessionHost(popupController, timeoutRelay);
                var uiAudioPort = new RecordingUiAudioPort();
                var presentationSource = new ManualGameplayUiPresentationSource();
                var screenFactory = new GameplayScreenRuntimeFactory(
                    screenLayerView,
                    new FakeGameplayQueryFacade(
                        new Game.Feature.Gameplay.UIAccess.Models.GameplaySessionReadModel(1, false, true, false),
                        FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                        new Game.Feature.Gameplay.UIAccess.Models.GameplayObjectiveReadModel(true, true, false, false)),
                    presentationSource,
                    new FakeAudioSettingsPort(),
                    new FakeDisplaySettingsPort(),
                    keyboardBindingSettingsPort ?? NoOpKeyboardBindingSettingsPort.Instance,
                    uiAudioPort,
                    previewSessionHost,
                    lifecycleRelay,
                    UiTestPrefabAssetUtility.LoadScreenCatalog(),
                    localizedTextResolver: PackageFreeLocalizedTextResolver.CreateSettingsDefault());
                var screenController = new ScreenController(screenFactory);
                var coordinator = new UIFlowCoordinator(
                    screenController,
                    popupController,
                    new UIBlockPolicy(),
                    new FakeGameplayPauseService(),
                    presentationSource,
                    uiAudioPort,
                    new FakeStageLaunchRouter());
                previewSessionHost.BindAudioIntentBoundary(coordinator);
                coordinator.Initialize();

                return new UiAudioHarness(rootObject, screenLayerView, screenController, popupController, coordinator, uiAudioPort);
            }

            public void Dispose()
            {
                Coordinator.Dispose();
                ScreenController.Dispose();
                PopupController.Dispose();
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }
        }

        private sealed class CompletingKeyboardSettingsPort : IKeyboardBindingSettingsPort
        {
            private Action<KeyboardRebindResult> completed;
            private KeyboardBindableAction rebindingAction;
            private KeyboardMovementScheme movementScheme = KeyboardMovementScheme.Wasd;

            public bool IsRebinding { get; private set; }

            public KeyboardBindingSettingsSnapshot Read()
            {
                return BuildSnapshot(IsRebinding);
            }

            public KeyboardBindingValidationResult TrySetMovementScheme(KeyboardMovementScheme scheme)
            {
                movementScheme = scheme;
                return KeyboardBindingValidationResult.Success;
            }

            public KeyboardRebindStartResult StartRebind(
                KeyboardBindableAction action,
                Action<KeyboardRebindResult> completed)
            {
                IsRebinding = true;
                rebindingAction = action;
                this.completed = completed;
                return new KeyboardRebindStartResult(
                    true,
                    KeyboardBindingValidationResult.Success,
                    BuildSnapshot(isRebinding: true));
            }

            public void CancelRebind()
            {
                IsRebinding = false;
                completed = null;
            }

            public KeyboardBindingSettingsSnapshot ResetToDefaults()
            {
                IsRebinding = false;
                return BuildSnapshot(isRebinding: false);
            }

            public void CompleteRebind(KeyboardBindingValidationResult result)
            {
                IsRebinding = false;
                var completion = completed;
                completed = null;
                completion?.Invoke(new KeyboardRebindResult(rebindingAction, result, BuildSnapshot(isRebinding: false)));
            }

            private KeyboardBindingSettingsSnapshot BuildSnapshot(bool isRebinding)
            {
                return new KeyboardBindingSettingsSnapshot(
                    movementScheme,
                    movementScheme == KeyboardMovementScheme.ArrowKeys ? "Arrow Keys" : "WASD",
                    "J",
                    "K",
                    isRebinding,
                    isRebinding ? (KeyboardBindableAction?)KeyboardBindableAction.Push : null);
            }
        }
    }
}

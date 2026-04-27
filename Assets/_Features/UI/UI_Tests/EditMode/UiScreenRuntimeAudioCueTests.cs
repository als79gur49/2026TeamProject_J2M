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
    public sealed class UiScreenRuntimeAudioCueTests
    {
        [Test]
        public void GameplayScreenRuntimeFactory_InventoryLocalInteractions_EmitOnlySelectCues()
        {
            using var harness = UiAudioHarness.Create();

            Assert.That(harness.Coordinator.OpenInventoryScreen(), Is.True);
            harness.UiAudioPort.Clear();

            var view = harness.ScreenLayerView.FindScreenView<InventoryScreenView>();
            Assert.That(view, Is.Not.Null);

            view.ClickSearch();
            view.ClickFilter();
            view.ClickSort();
            view.ClickItemRow(0);
            view.ClickPrimaryAction();
            view.ClickSecondaryAction();

            Assert.That(
                harness.UiAudioPort.PlayedCueIds,
                Is.EqualTo(new[]
                {
                    UiAudioCueId.Select,
                    UiAudioCueId.Select,
                    UiAudioCueId.Select,
                    UiAudioCueId.Select,
                    UiAudioCueId.Select,
                    UiAudioCueId.Select,
                }));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_ObjectiveStatusLocalInteractions_EmitOnlySelectCues()
        {
            using var harness = UiAudioHarness.Create();

            Assert.That(harness.Coordinator.OpenObjectiveStatusScreen(), Is.True);
            harness.UiAudioPort.Clear();

            var view = harness.ScreenLayerView.FindScreenView<ObjectiveStatusScreenView>();
            Assert.That(view, Is.Not.Null);

            view.ClickOverview();
            view.ClickSession();

            Assert.That(
                harness.UiAudioPort.PlayedCueIds,
                Is.EqualTo(new[]
                {
                    UiAudioCueId.Select,
                    UiAudioCueId.Select,
                }));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsLocalInteractions_EmitOnlyMappedLocalCues()
        {
            using var harness = UiAudioHarness.Create();

            Assert.That(harness.Coordinator.OpenSettingsScreen(), Is.True);
            harness.UiAudioPort.Clear();

            var view = harness.ScreenLayerView.FindScreenView<SettingsScreenView>();
            Assert.That(view, Is.Not.Null);

            view.ClickTooltipToggle();
            view.ClickLargeTextToggle();
            view.SetAudioMuted(AudioSettingsChannel.Sfx, true);
            view.CommitAudioInteraction(AudioSettingsChannel.Main);
            view.ClickDisplayTab();
            view.SelectDisplayResolution(1);
            view.SetDisplayFullscreen(true);

            Assert.That(
                harness.UiAudioPort.PlayedCueIds,
                Is.EqualTo(new[]
                {
                    UiAudioCueId.Toggle,
                    UiAudioCueId.Toggle,
                    UiAudioCueId.Toggle,
                    UiAudioCueId.AdjustValueCommit,
                    UiAudioCueId.Select,
                    UiAudioCueId.Toggle,
                }));

            harness.UiAudioPort.Clear();
            view.ClickDisplayRevert();
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
            view.SelectDisplayResolution(1);
            harness.UiAudioPort.Clear();

            view.ClickDisplayApply();

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

            public static UiAudioHarness Create()
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
                    UiTestPrefabAssetUtility.LoadPopupCatalog()));
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
                    new AccessibilitySettingsStore(),
                    new FakeAudioSettingsPort(),
                    new FakeDisplaySettingsPort(),
                    uiAudioPort,
                    previewSessionHost,
                    lifecycleRelay,
                    UiTestPrefabAssetUtility.LoadScreenCatalog());
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
                Object.DestroyImmediate(_rootObject);
            }
        }
    }
}

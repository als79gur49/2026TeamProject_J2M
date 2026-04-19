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
    public sealed class SettingsDisplayRuntimeContractTests
    {
        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_StartsPreviewAndCommitsThroughConfirmPopup()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_Commit");
            try
            {
                var runtimeContext = CreateRuntimeContext(rootObject);
                var displayPort = new FakeDisplaySettingsPort();
                var factory = CreateFactory(runtimeContext, displayPort);

                var result = factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings"));
                result.Runtime.ApplyPayload(SettingsScreenPayload.Default);
                result.Runtime.SetIsCurrent(true);

                var view = runtimeContext.ScreenLayerView.FindScreenView<SettingsScreenView>();
                Assert.That(view, Is.Not.Null);

                view.SelectDisplayResolution(0);
                view.SetDisplayFullscreen(true);
                view.ClickDisplayApply();

                Assert.That(displayPort.BeginPreviewCallCount, Is.EqualTo(1));
                Assert.That(runtimeContext.PopupController.Contains(PopupId.Confirm), Is.True);
                Assert.That(view.DisplayStatusText, Is.EqualTo("Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in 15 seconds."));

                runtimeContext.PopupController.CloseTop(PopupCloseReason.UserAction, PopupCompletionKind.Confirmed);

                Assert.That(displayPort.CommitPreviewCallCount, Is.EqualTo(1));
                Assert.That(runtimeContext.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(view.DisplayStatusText, Is.EqualTo("Display settings saved."));
                Assert.That(view.IsDisplayApplyInteractable, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_HideDuringPreview_RevertsAndClearsPopup()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_Hide");
            try
            {
                var runtimeContext = CreateRuntimeContext(rootObject);
                var displayPort = new FakeDisplaySettingsPort();
                var factory = CreateFactory(runtimeContext, displayPort);

                var result = factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings"));
                result.Runtime.ApplyPayload(SettingsScreenPayload.Default);
                result.Runtime.SetIsCurrent(true);

                var view = runtimeContext.ScreenLayerView.FindScreenView<SettingsScreenView>();
                view.SelectDisplayResolution(1);
                view.SetDisplayFullscreen(true);
                view.ClickDisplayApply();

                Assert.That(runtimeContext.PopupController.Contains(PopupId.Confirm), Is.True);

                result.Runtime.SetIsCurrent(false);

                Assert.That(displayPort.RevertPreviewCallCount, Is.EqualTo(1));
                Assert.That(runtimeContext.PopupController.PopupCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        private static GameplayScreenRuntimeFactory CreateFactory(
            RuntimeContext runtimeContext,
            FakeDisplaySettingsPort displayPort)
        {
            return new GameplayScreenRuntimeFactory(
                runtimeContext.ScreenLayerView,
                new FakeGameplayQueryFacade(
                    new Game.Feature.Gameplay.UIAccess.Models.GameplaySessionReadModel(1, false, true, false),
                    FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                    new Game.Feature.Gameplay.UIAccess.Models.GameplayObjectiveReadModel(false, false, false, false)),
                new ManualGameplayUiPresentationSource(),
                new AccessibilitySettingsStore(),
                new FakeAudioSettingsPort(),
                displayPort,
                runtimeContext.PreviewSessionHost,
                runtimeContext.LifecycleRelay,
                UiTestPrefabAssetUtility.LoadScreenCatalog());
        }

        private static RuntimeContext CreateRuntimeContext(GameObject rootObject)
        {
            var screenLayerRoot = new GameObject("ScreenLayerRoot", typeof(RectTransform));
            screenLayerRoot.transform.SetParent(rootObject.transform, false);
            var contentRootObject = new GameObject("ScreenContentRoot", typeof(RectTransform));
            contentRootObject.transform.SetParent(screenLayerRoot.transform, false);
            var contentRoot = contentRootObject.GetComponent<RectTransform>();
            var screenLayerView = screenLayerRoot.AddComponent<ScreenLayerView>();
            screenLayerView.Configure(screenLayerRoot, contentRoot);

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

            return new RuntimeContext(screenLayerView, popupController, previewSessionHost, lifecycleRelay);
        }

        private sealed class RuntimeContext
        {
            public RuntimeContext(
                ScreenLayerView screenLayerView,
                PopupController popupController,
                DisplayPreviewSessionHost previewSessionHost,
                DisplaySettingsLifecycleRelay lifecycleRelay)
            {
                ScreenLayerView = screenLayerView;
                PopupController = popupController;
                PreviewSessionHost = previewSessionHost;
                LifecycleRelay = lifecycleRelay;
            }

            public ScreenLayerView ScreenLayerView { get; }

            public PopupController PopupController { get; }

            public DisplayPreviewSessionHost PreviewSessionHost { get; }

            public DisplaySettingsLifecycleRelay LifecycleRelay { get; }
        }
    }
}

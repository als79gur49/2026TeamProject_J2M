using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsAudioRuntimeContractTests
    {
        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_UpdatesAudioImmediately_AndFlushesOnCommitAndDispose()
        {
            var rootObject = new GameObject("SettingsAudioRuntimeContractRoot");
            try
            {
                var screenLayerRoot = new GameObject("ScreenLayerRoot", typeof(RectTransform));
                screenLayerRoot.transform.SetParent(rootObject.transform, false);
                var screenLayerRootRect = screenLayerRoot.GetComponent<RectTransform>();
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

                var audioPort = new FakeAudioSettingsPort();
                var displayPort = new FakeDisplaySettingsPort();
                var factory = new GameplayScreenRuntimeFactory(
                    screenLayerView,
                    new FakeGameplayQueryFacade(
                        new Game.Feature.Gameplay.UIAccess.Models.GameplaySessionReadModel(1, false, true, false),
                        FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                        new Game.Feature.Gameplay.UIAccess.Models.GameplayObjectiveReadModel(false, false, false, false)),
                    new ManualGameplayUiPresentationSource(),
                    new AccessibilitySettingsStore(),
                    audioPort,
                    displayPort,
                    new RecordingUiAudioPort(),
                    previewSessionHost,
                    lifecycleRelay,
                    UiTestPrefabAssetUtility.LoadScreenCatalog());

                var result = factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings"));
                result.Runtime.ApplyPayload(SettingsScreenPayload.Default);
                result.Runtime.SetIsCurrent(true);

                var view = screenLayerView.FindScreenView<SettingsScreenView>();
                Assert.That(view, Is.Not.Null);

                view.BeginAudioInteraction(AudioSettingsChannel.Main);
                view.SetAudioVolume(AudioSettingsChannel.Main, 0.25f);
                Assert.That(audioPort.Read().Main.Volume, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(audioPort.FlushCallCount, Is.EqualTo(0));

                view.CommitAudioInteraction(AudioSettingsChannel.Main);
                Assert.That(audioPort.FlushCallCount, Is.EqualTo(1));

                view.SetAudioMuted(AudioSettingsChannel.Bgm, true);
                Assert.That(audioPort.Read().Bgm.IsMuted, Is.True);
                Assert.That(audioPort.FlushCallCount, Is.EqualTo(1));

                result.Runtime.SetIsCurrent(false);
                Assert.That(audioPort.FlushCallCount, Is.EqualTo(2));

                result.Runtime.Dispose();
                Assert.That(audioPort.FlushCallCount, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void AudioSettingsLifecycleRelay_FlushesOnPauseAndQuit()
        {
            var rootObject = new GameObject("AudioSettingsLifecycleRelayRoot");
            try
            {
                var audioPort = new FakeAudioSettingsPort();
                var relay = rootObject.AddComponent<AudioSettingsLifecycleRelay>();
                relay.Initialize(audioPort);

                InvokeLifecycleMethod(relay, "OnApplicationPause", true);
                Assert.That(audioPort.FlushCallCount, Is.EqualTo(1));

                InvokeLifecycleMethod(relay, "OnApplicationQuit");
                Assert.That(audioPort.FlushCallCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        private static void InvokeLifecycleMethod(AudioSettingsLifecycleRelay relay, string methodName, params object[] arguments)
        {
            var method = typeof(AudioSettingsLifecycleRelay).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing lifecycle method '{methodName}'.");
            method.Invoke(relay, arguments);
        }
    }
}

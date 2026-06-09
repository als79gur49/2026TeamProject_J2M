using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsDisplayRuntimeContractTests
    {
        [Test]
        public void GameplayScreenRuntimeFactory_SettingsScreen_UsesSettingsRuntimeBuilder()
        {
            var factorySource = System.IO.File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs");

            Assert.That(factorySource, Does.Contain("SettingsScreenRuntimeBuilder"));
            Assert.That(factorySource, Does.Not.Contain("HandleDisplayApplyRequested"));
            Assert.That(factorySource, Does.Not.Contain("BuildDisplayPreviewConfirmPayload"));
        }

        [Test]
        public void SettingsScreenRuntimeBuilder_DoesNotReferenceSharedAudioRuntime()
        {
            var builderSource = System.IO.File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/SettingsScreenRuntimeBuilder.cs");

            Assert.That(builderSource, Does.Not.Contain("Game.Shared.Audio"));
            Assert.That(builderSource, Does.Not.Contain("AudioRuntimeInstaller"));
            Assert.That(builderSource, Does.Not.Contain("AudioManager"));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_StartsPreviewAndCommitsThroughConfirmPopup()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_Commit");
            try
            {
                var runtimeContext = CreateRuntimeContext(rootObject);
                double statusNow = 0d;
                runtimeContext.TransientStatusRelay.SetTimeProviderForTesting(() => statusNow);
                var displayPort = new FakeDisplaySettingsPort();
                var factory = CreateFactory(runtimeContext, displayPort);

                var result = factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings"));
                result.Runtime.ApplyPayload(SettingsScreenPayload.Default);
                result.Runtime.SetIsCurrent(true);

                var view = runtimeContext.ScreenLayerView.FindScreenView<SettingsScreenView>();
                Assert.That(view, Is.Not.Null);
                view.ClickDisplayTab();

                view.DisplayView.SelectResolution(2);
                view.DisplayView.SetFullscreen(true);
                view.DisplayView.ClickApply();

                Assert.That(displayPort.BeginPreviewCallCount, Is.EqualTo(1));
                Assert.That(runtimeContext.PopupController.Contains(PopupId.Confirm), Is.True);
                Assert.That(view.DisplayView.DisplayStatusText, Is.EqualTo("Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in 15 seconds."));

                runtimeContext.PopupController.CloseTop(PopupCloseReason.UserAction, PopupCompletionKind.Confirmed);

                Assert.That(displayPort.CommitPreviewCallCount, Is.EqualTo(1));
                Assert.That(runtimeContext.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(view.DisplayView.DisplayStatusText, Is.EqualTo("Display settings saved."));
                Assert.That(GetDisplayPrivateField<TMP_Text>(view.DisplayView, "_displayStatusLabel").gameObject.activeSelf, Is.True);
                Assert.That(view.DisplayView.IsDisplayApplyInteractable, Is.False);

                statusNow = 2.1d;
                InvokePrivateMethod(runtimeContext.TransientStatusRelay, "Update");

                Assert.That(view.DisplayView.DisplayStatusText, Is.Empty);
                Assert.That(GetDisplayPrivateField<TMP_Text>(view.DisplayView, "_displayStatusLabel").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_ShowsPreviewCountdownOnlyAfterSuccessfulPopupOpen()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_CountdownSuccess");
            try
            {
                var runtimeContext = CreateRuntimeContext(rootObject);
                double now = 0d;
                runtimeContext.TimeoutRelay.SetTimeProviderForTesting(() => now);
                var displayPort = new FakeDisplaySettingsPort();
                var factory = CreateFactory(runtimeContext, displayPort);

                var result = factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings"));
                result.Runtime.ApplyPayload(SettingsScreenPayload.Default);
                result.Runtime.SetIsCurrent(true);

                var view = runtimeContext.ScreenLayerView.FindScreenView<SettingsScreenView>();
                view.ClickDisplayTab();
                var displayView = view.DisplayView;
                var countdownRoot = GetDisplayPrivateField<RectTransform>(displayView, "_previewCountdownRoot");
                var countdownLabel = GetDisplayPrivateField<TMP_Text>(displayView, "_previewCountdownLabel");
                var countdownSlider = GetDisplayPrivateField<Slider>(displayView, "_previewCountdownSlider");

                Assert.That(countdownRoot.gameObject.activeSelf, Is.False);

                displayView.SelectResolution(2);
                displayView.SetFullscreen(true);
                displayView.ClickApply();

                Assert.That(countdownRoot.gameObject.activeSelf, Is.True);
                Assert.That(countdownLabel.text, Is.EqualTo("Reverting in 15s"));
                Assert.That(countdownSlider.interactable, Is.False);
                Assert.That(countdownSlider.minValue, Is.EqualTo(0f));
                Assert.That(countdownSlider.maxValue, Is.EqualTo(1f));
                Assert.That(countdownSlider.wholeNumbers, Is.False);
                Assert.That(countdownSlider.value, Is.EqualTo(1f).Within(0.0001f));

                now = 1.1d;
                InvokePrivateMethod(runtimeContext.TimeoutRelay, "Update");

                Assert.That(countdownRoot.gameObject.activeSelf, Is.True);
                Assert.That(countdownLabel.text, Is.EqualTo("Reverting in 14s"));
                Assert.That(countdownSlider.value, Is.EqualTo(14f / 15f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_UsesHostTimeoutForStatusAndConfirmCopy()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_TimeoutSource");
            try
            {
                var runtimeContext = CreateRuntimeContext(rootObject, previewTimeoutSeconds: 21d);
                var displayPort = new FakeDisplaySettingsPort();
                var factory = CreateFactory(runtimeContext, displayPort);

                var result = factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings"));
                result.Runtime.ApplyPayload(SettingsScreenPayload.Default);
                result.Runtime.SetIsCurrent(true);

                var view = runtimeContext.ScreenLayerView.FindScreenView<SettingsScreenView>();
                view.ClickDisplayTab();
                view.DisplayView.SelectResolution(2);
                view.DisplayView.SetFullscreen(true);
                view.DisplayView.ClickApply();

                Assert.That(
                    view.DisplayView.DisplayStatusText,
                    Is.EqualTo("Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in 21 seconds."));

                Assert.That(runtimeContext.PopupController.TopPopup.HasValue, Is.True);
                var confirmPayload = runtimeContext.PopupController.TopPopup.Value.Payload as ConfirmPopupPayload;
                Assert.That(confirmPayload, Is.Not.Null);
                Assert.That(confirmPayload.BodyText, Does.Contain("revert in 21 seconds unless you confirm."));
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_PopupOpenFailure_RevertsPreviewWithoutShowingCountdown()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_CountdownFailure");
            try
            {
                var runtimeContext = CreateRuntimeContext(rootObject, new FailingPopupRuntimeFactory());
                double statusNow = 0d;
                runtimeContext.TransientStatusRelay.SetTimeProviderForTesting(() => statusNow);
                var displayPort = new FakeDisplaySettingsPort();
                var factory = CreateFactory(runtimeContext, displayPort);

                var result = factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings"));
                result.Runtime.ApplyPayload(SettingsScreenPayload.Default);
                result.Runtime.SetIsCurrent(true);

                var view = runtimeContext.ScreenLayerView.FindScreenView<SettingsScreenView>();
                view.ClickDisplayTab();
                var displayView = view.DisplayView;
                var countdownRoot = GetDisplayPrivateField<RectTransform>(displayView, "_previewCountdownRoot");

                displayView.SelectResolution(1);
                displayView.ClickApply();

                Assert.That(displayPort.BeginPreviewCallCount, Is.EqualTo(1));
                Assert.That(displayPort.RevertPreviewCallCount, Is.EqualTo(1));
                Assert.That(runtimeContext.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(countdownRoot.gameObject.activeSelf, Is.False);
                Assert.That(displayView.DisplayStatusText, Is.EqualTo("Preview reverted to the previous saved display settings."));

                statusNow = 2.1d;
                InvokePrivateMethod(runtimeContext.TransientStatusRelay, "Update");

                Assert.That(displayView.DisplayStatusText, Is.Empty);
                Assert.That(GetDisplayPrivateField<TMP_Text>(displayView, "_displayStatusLabel").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_ClearsPreviewCountdown_OnTimeoutAndReentry()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_CountdownLifecycle");
            try
            {
                var runtimeContext = CreateRuntimeContext(rootObject);
                double now = 0d;
                runtimeContext.TimeoutRelay.SetTimeProviderForTesting(() => now);
                var displayPort = new FakeDisplaySettingsPort();
                var factory = CreateFactory(runtimeContext, displayPort);

                var result = factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings"));
                result.Runtime.ApplyPayload(SettingsScreenPayload.Default);
                result.Runtime.SetIsCurrent(true);

                var view = runtimeContext.ScreenLayerView.FindScreenView<SettingsScreenView>();
                view.ClickDisplayTab();
                var displayView = view.DisplayView;
                var countdownRoot = GetDisplayPrivateField<RectTransform>(displayView, "_previewCountdownRoot");
                var countdownLabel = GetDisplayPrivateField<TMP_Text>(displayView, "_previewCountdownLabel");
                var countdownSlider = GetDisplayPrivateField<Slider>(displayView, "_previewCountdownSlider");

                displayView.SelectResolution(2);
                displayView.ClickApply();
                Assert.That(countdownRoot.gameObject.activeSelf, Is.True);

                now = 20d;
                InvokePrivateMethod(runtimeContext.TimeoutRelay, "Update");

                Assert.That(displayPort.RevertPreviewCallCount, Is.EqualTo(1));
                Assert.That(countdownRoot.gameObject.activeSelf, Is.False);
                Assert.That(countdownLabel.text, Is.EqualTo(string.Empty));
                Assert.That(countdownSlider.value, Is.Zero);

                result.Runtime.SetIsCurrent(false);
                result.Runtime.SetIsCurrent(true);

                Assert.That(countdownRoot.gameObject.activeSelf, Is.False);
                Assert.That(countdownLabel.text, Is.EqualTo(string.Empty));
                Assert.That(countdownSlider.value, Is.Zero);
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
                view.ClickDisplayTab();
                view.DisplayView.SelectResolution(1);
                view.DisplayView.SetFullscreen(true);
                view.DisplayView.ClickApply();

                Assert.That(runtimeContext.PopupController.Contains(PopupId.Confirm), Is.True);

                result.Runtime.SetIsCurrent(false);

                Assert.That(displayPort.RevertPreviewCallCount, Is.EqualTo(1));
                Assert.That(runtimeContext.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(GetDisplayPrivateField<RectTransform>(view.DisplayView, "_previewCountdownRoot").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_SetIsCurrentFalse_HidesResolutionHoverHint_AndReentryStartsHidden()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_HoverLifecycle");
            try
            {
                var runtimeContext = CreateRuntimeContext(rootObject);
                var displayPort = new FakeDisplaySettingsPort();
                var factory = CreateFactory(runtimeContext, displayPort);

                var result = factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings"));
                result.Runtime.ApplyPayload(SettingsScreenPayload.Default);
                result.Runtime.SetIsCurrent(true);

                var view = runtimeContext.ScreenLayerView.FindScreenView<SettingsScreenView>();
                view.ClickDisplayTab();
                var displayView = view.DisplayView;
                var hintRoot = GetDisplayPrivateField<RectTransform>(displayView, "_resolutionHoverHintRoot");

                EnterResolutionHover(displayView);
                Assert.That(hintRoot.gameObject.activeSelf, Is.True);

                result.Runtime.SetIsCurrent(false);
                Assert.That(hintRoot.gameObject.activeSelf, Is.False);

                result.Runtime.SetIsCurrent(true);
                Assert.That(hintRoot.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_DisplayApply_HidesResolutionHoverHint_BeforeConfirmPopup()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_HoverPreview");
            try
            {
                var runtimeContext = CreateRuntimeContext(rootObject);
                var displayPort = new FakeDisplaySettingsPort();
                var factory = CreateFactory(runtimeContext, displayPort);

                var result = factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings"));
                result.Runtime.ApplyPayload(SettingsScreenPayload.Default);
                result.Runtime.SetIsCurrent(true);

                var view = runtimeContext.ScreenLayerView.FindScreenView<SettingsScreenView>();
                view.ClickDisplayTab();
                var displayView = view.DisplayView;
                var hintRoot = GetDisplayPrivateField<RectTransform>(displayView, "_resolutionHoverHintRoot");

                view.DisplayView.SelectResolution(2);
                EnterResolutionHover(displayView);
                Assert.That(hintRoot.gameObject.activeSelf, Is.True);

                view.DisplayView.ClickApply();

                Assert.That(hintRoot.gameObject.activeSelf, Is.False);
                Assert.That(runtimeContext.PopupController.Contains(PopupId.Confirm), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_FailsFast_WhenAudioSectionViewIsMissing()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_MissingAudioSection");
            var settingsClone = Object.Instantiate(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));
            var tempCatalog = CreateCatalogWithSettingsPrefab(settingsClone);

            try
            {
                SetObjectReference(settingsClone, "_audioView", null);
                var runtimeContext = CreateRuntimeContext(rootObject);
                var factory = CreateFactory(runtimeContext, new FakeDisplaySettingsPort(), tempCatalog);

                var exception = Assert.Throws<System.InvalidOperationException>(() =>
                    factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings")));

                Assert.That(exception.Message, Does.Contain("missing or miswired required authored audio section"));
                Assert.That(exception.Message, Does.Contain("Repair: assign SettingsScreenView._audioView"));
            }
            finally
            {
                Object.DestroyImmediate(tempCatalog);
                Object.DestroyImmediate(settingsClone.gameObject);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_FailsFast_WhenDisplaySectionViewIsMissing()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_MissingDisplaySection");
            var settingsClone = Object.Instantiate(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));
            var tempCatalog = CreateCatalogWithSettingsPrefab(settingsClone);

            try
            {
                SetObjectReference(settingsClone, "_displayView", null);
                var runtimeContext = CreateRuntimeContext(rootObject);
                var factory = CreateFactory(runtimeContext, new FakeDisplaySettingsPort(), tempCatalog);

                var exception = Assert.Throws<System.InvalidOperationException>(() =>
                    factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings")));

                Assert.That(exception.Message, Does.Contain("missing or miswired required authored display section"));
                Assert.That(exception.Message, Does.Contain("Repair: assign SettingsScreenView._displayView"));
            }
            finally
            {
                Object.DestroyImmediate(tempCatalog);
                Object.DestroyImmediate(settingsClone.gameObject);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_FailsFast_WhenAudioChildControlsAreMissing()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_MissingAudioControls");
            var settingsClone = Object.Instantiate(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));
            var tempCatalog = CreateCatalogWithSettingsPrefab(settingsClone);

            try
            {
                SetNestedObjectReference(settingsClone.AudioView, "_mainRow._slider", null);
                var runtimeContext = CreateRuntimeContext(rootObject);
                var factory = CreateFactory(runtimeContext, new FakeDisplaySettingsPort(), tempCatalog);

                var exception = Assert.Throws<System.InvalidOperationException>(() =>
                    factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings")));

                Assert.That(exception.Message, Does.Contain("Settings audio section is missing required authored controls"));
                Assert.That(exception.Message, Does.Contain("Repair: open SettingsScreen.prefab"));
                Assert.That(exception.Message, Does.Contain("MainAudioRow slider"));
            }
            finally
            {
                Object.DestroyImmediate(tempCatalog);
                Object.DestroyImmediate(settingsClone.gameObject);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_FailsFast_WhenDisplayChildControlsAreMissing()
        {
            var rootObject = new GameObject("SettingsDisplayRuntimeContractRoot_MissingDisplayControls");
            var settingsClone = Object.Instantiate(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));
            var tempCatalog = CreateCatalogWithSettingsPrefab(settingsClone);

            try
            {
                SetNestedObjectReference(settingsClone.DisplayView, "_resolutionDropdown", null);
                var runtimeContext = CreateRuntimeContext(rootObject);
                var factory = CreateFactory(runtimeContext, new FakeDisplaySettingsPort(), tempCatalog);

                var exception = Assert.Throws<System.InvalidOperationException>(() =>
                    factory.Create(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, "settings")));

                Assert.That(exception.Message, Does.Contain("Settings display section is missing required authored controls"));
                Assert.That(exception.Message, Does.Contain("Repair: open SettingsScreen.prefab"));
                Assert.That(exception.Message, Does.Contain("_resolutionDropdown"));
            }
            finally
            {
                Object.DestroyImmediate(tempCatalog);
                Object.DestroyImmediate(settingsClone.gameObject);
                Object.DestroyImmediate(rootObject);
            }
        }

        private static GameplayScreenRuntimeFactory CreateFactory(
            RuntimeContext runtimeContext,
            FakeDisplaySettingsPort displayPort,
            ScreenPrefabCatalog screenCatalog = null)
        {
            return new GameplayScreenRuntimeFactory(
                runtimeContext.ScreenLayerView,
                new FakeGameplayQueryFacade(
                    new Game.Feature.Gameplay.UIAccess.Models.GameplaySessionReadModel(1, false, true, false),
                    FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                    new Game.Feature.Gameplay.UIAccess.Models.GameplayObjectiveReadModel(false, false, false, false)),
                new ManualGameplayUiPresentationSource(),
                new FakeAudioSettingsPort(),
                displayPort,
                new RecordingUiAudioPort(),
                runtimeContext.PreviewSessionHost,
                runtimeContext.LifecycleRelay,
                screenCatalog ?? UiTestPrefabAssetUtility.LoadScreenCatalog(),
                runtimeContext.TransientStatusRelay);
        }

        private static RuntimeContext CreateRuntimeContext(
            GameObject rootObject,
            IPopupRuntimeFactory popupRuntimeFactory = null,
            double previewTimeoutSeconds = 15d)
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

            var popupController = new PopupController(popupRuntimeFactory ?? new GameplayPopupRuntimeFactory(
                popupLayerView,
                UiTestPrefabAssetUtility.LoadPopupCatalog()));
            var timeoutRelay = rootObject.AddComponent<DisplayPreviewTimeoutRelay>();
            var transientStatusRelay = rootObject.AddComponent<DisplayStatusTransientRelay>();
            var lifecycleRelay = rootObject.AddComponent<DisplaySettingsLifecycleRelay>();
            var previewSessionHost = new DisplayPreviewSessionHost(popupController, timeoutRelay, previewTimeoutSeconds);

            return new RuntimeContext(screenLayerView, popupController, previewSessionHost, lifecycleRelay, timeoutRelay, transientStatusRelay);
        }

        private static ScreenPrefabCatalog CreateCatalogWithSettingsPrefab(SettingsScreenView settingsPrefab)
        {
            var catalog = ScriptableObject.CreateInstance<ScreenPrefabCatalog>();
            SetPrivateField(catalog, "_settingsPrefab", settingsPrefab);
            return catalog;
        }

        private static void SetNestedObjectReference(UnityEngine.Object target, string propertyPath, UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyPath);
            Assert.That(property, Is.Not.Null, propertyPath);
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SetNestedObjectReference(target, propertyName, value);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class RuntimeContext
        {
            public RuntimeContext(
                ScreenLayerView screenLayerView,
                PopupController popupController,
                DisplayPreviewSessionHost previewSessionHost,
                DisplaySettingsLifecycleRelay lifecycleRelay,
                DisplayPreviewTimeoutRelay timeoutRelay,
                DisplayStatusTransientRelay transientStatusRelay)
            {
                ScreenLayerView = screenLayerView;
                PopupController = popupController;
                PreviewSessionHost = previewSessionHost;
                LifecycleRelay = lifecycleRelay;
                TimeoutRelay = timeoutRelay;
                TransientStatusRelay = transientStatusRelay;
            }

            public ScreenLayerView ScreenLayerView { get; }

            public PopupController PopupController { get; }

            public DisplayPreviewSessionHost PreviewSessionHost { get; }

            public DisplaySettingsLifecycleRelay LifecycleRelay { get; }

            public DisplayPreviewTimeoutRelay TimeoutRelay { get; }

            public DisplayStatusTransientRelay TransientStatusRelay { get; }
        }

        private static void EnterResolutionHover(SettingsDisplayView displayView)
        {
            var relay = GetDisplayPrivateField<SettingsHoverRelay>(displayView, "_resolutionHoverRelay");
            relay.OnPointerEnter(new PointerEventData(null));
        }

        private static TField GetDisplayPrivateField<TField>(SettingsDisplayView displayView, string fieldName)
            where TField : class
        {
            var field = typeof(SettingsDisplayView).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            var value = field.GetValue(displayView) as TField;
            Assert.That(value, Is.Not.Null, fieldName);
            return value;
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private sealed class FailingPopupRuntimeFactory : IPopupRuntimeFactory
        {
            public PopupRuntimeFactoryResult Create(PopupRequest request)
            {
                throw new InvalidOperationException("Synthetic popup creation failure for countdown gating.");
            }
        }
    }
}

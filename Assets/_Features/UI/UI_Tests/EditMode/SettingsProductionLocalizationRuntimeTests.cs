using System;
using System.Reflection;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsProductionLocalizationRuntimeTests
    {
        private const int SettingsStaticBindingCount = 13;
        private const string ScaleRatioA = "_ScaleRatioA";
        private const string ScaleRatioC = "_ScaleRatioC";

        [TearDown]
        public void TearDown()
        {
            RestoreNanumGothicMaterialRatios();
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_BindsPackageFreeResolverAndRefreshesLocale()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            using var harness = GameplaySettingsHarness.Create(resolver);

            harness.ShowSettings();
            var view = harness.SettingsView;

            AssertSettingsLabels(view, "Settings", "Audio", "Display", "Input", "Back");
            AssertInputLabels(view.InputView, "Movement Keys", "Use Arrow Keys", "Push", "Flip", "Change", "Reset Input");

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            AssertSettingsLabels(view, "설정", "오디오", "디스플레이", "입력", "뒤로");
            AssertInputLabels(view.InputView, "이동 키", "화살표 키 사용", "밀기", "뒤집기", "변경", "입력 초기화");
        }

        [Test]
        public void SettingsScreenPrefab_DisplayLanguageRow_IsAuthoredAndInvokesCyclePath()
        {
            var prefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(
                UiTestPrefabAssetUtility.SettingsScreenPrefabPath);

            Assert.DoesNotThrow(prefab.DisplayView.ValidateAuthoredControlsOrThrow);

            var languageLabel = GetText(prefab.DisplayView, "_languageLabel");
            var languageButton = GetField<Button>(prefab.DisplayView, "_languageCycleButton");
            var languageButtonLabel = GetText(prefab.DisplayView, "_languageCycleButtonLabel");
            Assert.That(languageLabel.transform.parent.name, Is.EqualTo("LanguageRow"));
            Assert.That(languageButton.transform.parent.name, Is.EqualTo("LanguageRow"));
            Assert.That(languageButtonLabel.transform.IsChildOf(languageButton.transform), Is.True);
            Assert.That(languageLabel.gameObject.activeSelf, Is.True);
            Assert.That(languageButton.gameObject.activeSelf, Is.True);

            var view = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var displayModel = new SettingsDisplayViewModel();
                view.DisplayView.gameObject.SetActive(true);
                view.DisplayView.Bind(displayModel);
                displayModel.SetContent(
                    "1920 x 1080",
                    Array.Empty<string>(),
                    0,
                    false,
                    string.Empty,
                    false,
                    false,
                    false,
                    string.Empty,
                    0f,
                    false,
                    languageLabelText: "Language",
                    currentLanguageText: "English",
                    isLanguageSelectionAvailable: true);
                view.DisplayView.SetIsVisible(true);

                var clickCount = 0;
                view.DisplayView.LanguageCycleRequested += () => clickCount++;
                GetField<Button>(view.DisplayView, "_languageCycleButton").onClick.Invoke();

                Assert.That(clickCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_UnbindsAndAvoidsDuplicateLocaleSubscriptions()
        {
            var resolver = new CountingLocalizedTextResolver();
            using var harness = GameplaySettingsHarness.Create(resolver);

            harness.ShowSettings();
            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(SettingsStaticBindingCount));

            harness.ShowSettings();
            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(SettingsStaticBindingCount));

            harness.DisposeController();

            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_UsesInjectedKoreanFontResolver()
        {
            var nanumGothic = LoadNanumGothic();
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            var fontResolver = new DefaultLocalizedTmpFontResolver(nanumGothic);
            using var harness = GameplaySettingsHarness.Create(resolver, fontResolver: fontResolver);

            harness.ShowSettings();

            var titleLabel = GetText(harness.SettingsView, "_titleLabel");
            Assert.That(titleLabel.text, Is.EqualTo("설정"));
            Assert.That(titleLabel.font, Is.SameAs(nanumGothic));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_LanguageCycleSwitchesLocaleRefreshesLabelsAndFont()
        {
            var nanumGothic = LoadNanumGothic();
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var fontResolver = new DefaultLocalizedTmpFontResolver(nanumGothic);
            using var harness = GameplaySettingsHarness.Create(resolver, fontResolver: fontResolver);

            harness.ShowSettings();
            var view = harness.SettingsView;
            var titleLabel = GetText(view, "_titleLabel");
            var languageButtonLabel = GetText(view.DisplayView, "_languageCycleButtonLabel");
            var startingFont = titleLabel.font;
            var startingLanguageFont = languageButtonLabel.font;

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(titleLabel.text, Is.EqualTo("Settings"));
            Assert.That(view.DisplayView.LanguageLabelText, Is.EqualTo("Language"));
            Assert.That(view.DisplayView.CurrentLanguageText, Is.EqualTo("English"));

            view.ClickDisplayTab();
            view.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(titleLabel.text, Is.EqualTo("설정"));
            Assert.That(titleLabel.font, Is.SameAs(nanumGothic));
            Assert.That(view.DisplayView.LanguageLabelText, Is.EqualTo("언어"));
            Assert.That(view.DisplayView.CurrentLanguageText, Is.EqualTo("한국어"));
            Assert.That(languageButtonLabel.font, Is.SameAs(nanumGothic));
            Assert.That(harness.UiAudioPort.PlayedCueIds, Does.Contain(UiAudioCueId.Toggle));

            view.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(titleLabel.text, Is.EqualTo("Settings"));
            Assert.That(titleLabel.font, Is.SameAs(startingFont));
            Assert.That(view.DisplayView.LanguageLabelText, Is.EqualTo("Language"));
            Assert.That(view.DisplayView.CurrentLanguageText, Is.EqualTo("English"));
            Assert.That(languageButtonLabel.font, Is.SameAs(startingLanguageFont));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_ReopenStartsFromPersistedLocaleAndFont()
        {
            var store = new FakeUiLocalePreferenceStore();
            var nanumGothic = LoadNanumGothic();
            var fontResolver = new DefaultLocalizedTmpFontResolver(nanumGothic);

            using (var firstHarness = GameplaySettingsHarness.Create(
                       PackageFreeLocalizedTextResolver.CreateSettingsDefault(store),
                       fontResolver))
            {
                firstHarness.ShowSettings();
                firstHarness.SettingsView.ClickDisplayTab();
                firstHarness.SettingsView.DisplayView.ClickLanguageCycle();

                Assert.That(store.LastSavedLocaleCode, Is.EqualTo(PackageFreeLocalizedTextResolver.KoreanLocaleCode));
            }

            using (var secondHarness = GameplaySettingsHarness.Create(
                       PackageFreeLocalizedTextResolver.CreateSettingsDefault(store),
                       fontResolver))
            {
                secondHarness.ShowSettings();

                var titleLabel = GetText(secondHarness.SettingsView, "_titleLabel");
                Assert.That(titleLabel.text, Is.EqualTo("설정"));
                Assert.That(titleLabel.font, Is.SameAs(nanumGothic));
                Assert.That(secondHarness.SettingsView.DisplayView.LanguageLabelText, Is.EqualTo("언어"));
                Assert.That(secondHarness.SettingsView.DisplayView.CurrentLanguageText, Is.EqualTo("한국어"));
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_NullKoreanFontKeepsExistingTargetFont()
        {
            var expectedFont = GetText(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(
                    UiTestPrefabAssetUtility.SettingsScreenPrefabPath),
                "_titleLabel").font;
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            var fontResolver = new DefaultLocalizedTmpFontResolver(null);
            using var harness = GameplaySettingsHarness.Create(resolver, fontResolver: fontResolver);

            harness.ShowSettings();

            var titleLabel = GetText(harness.SettingsView, "_titleLabel");
            Assert.That(titleLabel.text, Is.EqualTo("설정"));
            Assert.That(titleLabel.font, Is.SameAs(expectedFont));
        }

        [Test]
        public void MainMenuSettingsRuntime_BindsPackageFreeResolverRefreshesLocaleAndUnbindsOnDispose()
        {
            var resolver = new CountingLocalizedTextResolver();
            using var harness = MainMenuSettingsHarness.Create(resolver);

            harness.Runtime.Open();
            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(SettingsStaticBindingCount));
            AssertSettingsLabels(harness.Runtime.View, "Settings", "Audio", "Display", "Input", "Back");

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            AssertSettingsLabels(harness.Runtime.View, "설정", "오디오", "디스플레이", "입력", "뒤로");

            harness.Runtime.Dispose();

            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void MainMenuSettingsRuntime_UsesInjectedKoreanFontResolver()
        {
            var nanumGothic = LoadNanumGothic();
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            var fontResolver = new DefaultLocalizedTmpFontResolver(nanumGothic);
            using var harness = MainMenuSettingsHarness.Create(resolver, fontResolver);

            harness.Runtime.Open();

            var titleLabel = GetText(harness.Runtime.View, "_titleLabel");
            Assert.That(titleLabel.text, Is.EqualTo("설정"));
            Assert.That(titleLabel.font, Is.SameAs(nanumGothic));
        }

        [Test]
        public void MainMenuSettingsRuntime_LanguageCycleSwitchesLocaleAndRefreshesLabels()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            using var harness = MainMenuSettingsHarness.Create(resolver);

            harness.Runtime.Open();
            var view = harness.Runtime.View;
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            AssertSettingsLabels(view, "Settings", "Audio", "Display", "Input", "Back");

            view.ClickDisplayTab();
            view.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            AssertSettingsLabels(view, "설정", "오디오", "디스플레이", "입력", "뒤로");

            harness.Runtime.Dispose();

            Assert.DoesNotThrow(() => resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode));
        }

        [Test]
        public void MainMenuSettingsRuntime_SelectionPersistsIntoNewGameplaySettingsResolver()
        {
            var store = new FakeUiLocalePreferenceStore();

            using (var mainMenuHarness = MainMenuSettingsHarness.Create(
                       PackageFreeLocalizedTextResolver.CreateSettingsDefault(store)))
            {
                mainMenuHarness.Runtime.Open();
                mainMenuHarness.Runtime.View.ClickDisplayTab();
                mainMenuHarness.Runtime.View.DisplayView.ClickLanguageCycle();

                Assert.That(store.LastSavedLocaleCode, Is.EqualTo(PackageFreeLocalizedTextResolver.KoreanLocaleCode));
            }

            using var gameplayHarness = GameplaySettingsHarness.Create(
                PackageFreeLocalizedTextResolver.CreateSettingsDefault(store));
            gameplayHarness.ShowSettings();

            Assert.That(GetText(gameplayHarness.SettingsView, "_titleLabel").text, Is.EqualTo("설정"));
            Assert.That(gameplayHarness.SettingsView.DisplayView.CurrentLanguageText, Is.EqualTo("한국어"));
        }

        [Test]
        public void PlayerPrefsUiLocalePreferenceStore_RoundTripsInjectedKeyAndFallsBackUnsupportedThroughResolver()
        {
            Assert.That(PlayerPrefsUiLocalePreferenceStore.DefaultKey, Is.EqualTo("ui.selected_locale"));

            var key = "Game.Feature.UI.Tests.Locale." + Guid.NewGuid().ToString("N");
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            try
            {
                var store = new PlayerPrefsUiLocalePreferenceStore(key);

                Assert.That(store.TryLoad(out _), Is.False);

                store.Save(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

                Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(PackageFreeLocalizedTextResolver.KoreanLocaleCode));
                Assert.That(new PlayerPrefsUiLocalePreferenceStore(key).TryLoad(out var loadedLocaleCode), Is.True);
                Assert.That(loadedLocaleCode, Is.EqualTo(PackageFreeLocalizedTextResolver.KoreanLocaleCode));
                Assert.That(
                    PackageFreeLocalizedTextResolver.CreateSettingsDefault(store).CurrentLocaleCode,
                    Is.EqualTo(PackageFreeLocalizedTextResolver.KoreanLocaleCode));

                store.Save("fr-FR");

                Assert.That(
                    PackageFreeLocalizedTextResolver.CreateSettingsDefault(store).CurrentLocaleCode,
                    Is.EqualTo(PackageFreeLocalizedTextResolver.DefaultLocaleCode));
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void PackageFreeLocalizedTextResolver_ProvidesSettingsCatalogAndFallbacks()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();

            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Title), Is.EqualTo("Settings"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Language), Is.EqualTo("Language"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.LanguageKorean), Is.EqualTo("Korean"));

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Title), Is.EqualTo("설정"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Language), Is.EqualTo("언어"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.LanguageKorean), Is.EqualTo("한국어"));

            resolver.SetLocale("fr-FR");

            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Back), Is.EqualTo("Back"));
            Assert.That(
                resolver.Resolve(new LocalizedTextDescriptor("UI", "ui.settings.missing")),
                Is.EqualTo("[UI:ui.settings.missing]"));
        }

        [Test]
        public void LocalePersistence_DoesNotLeakPlayerPrefsIntoUiApplicationOrViewShared()
        {
            var source = System.IO.File.ReadAllText("Assets/_Features/UI/UI_ViewShared/Runtime/LocalizedTextDescriptor.cs") +
                         System.IO.File.ReadAllText("Assets/_Features/UI/UI_Application/Runtime/Settings/SettingsScreenPresenters.cs");

            Assert.That(source, Does.Not.Contain("PlayerPrefs"));
            Assert.That(source, Does.Not.Contain("Unity.Localization"));
            Assert.That(source, Does.Not.Contain("Addressables"));
        }

        private static void AssertSettingsLabels(
            SettingsScreenView view,
            string title,
            string audio,
            string display,
            string input,
            string back)
        {
            Assert.That(GetText(view, "_titleLabel").text, Is.EqualTo(title));
            Assert.That(GetText(view, "_audioTabButtonLabel").text, Is.EqualTo(audio));
            Assert.That(GetText(view, "_displayTabButtonLabel").text, Is.EqualTo(display));
            Assert.That(GetText(view, "_inputTabButtonLabel").text, Is.EqualTo(input));
            Assert.That(GetText(view, "_backButtonLabel").text, Is.EqualTo(back));
        }

        private static void AssertInputLabels(
            SettingsInputView view,
            string movement,
            string useArrowKeys,
            string push,
            string flip,
            string change,
            string reset)
        {
            Assert.That(GetText(view, "_movementLabel").text, Is.EqualTo(movement));
            Assert.That(GetText(view, "_movementToggleLabel").text, Is.EqualTo(useArrowKeys));
            Assert.That(GetText(view, "_pushLabel").text, Is.EqualTo(push));
            Assert.That(GetText(view, "_flipLabel").text, Is.EqualTo(flip));
            Assert.That(GetText(view, "_pushChangeButtonLabel").text, Is.EqualTo(change));
            Assert.That(GetText(view, "_flipChangeButtonLabel").text, Is.EqualTo(change));
            Assert.That(GetText(view, "_resetButtonLabel").text, Is.EqualTo(reset));
        }

        private static TMP_Text GetText(object target, string fieldName)
        {
            return GetField<TMP_Text>(target, fieldName);
        }

        private static TField GetField<TField>(object target, string fieldName)
            where TField : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must exist.");
            var value = field.GetValue(target) as TField;
            Assert.That(value, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must reference {typeof(TField).Name}.");
            return value;
        }

        private static TMP_FontAsset LoadNanumGothic()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NanumGothicFontValidationUtility.FontAssetPath);
            Assert.That(fontAsset, Is.Not.Null, $"{NanumGothicFontValidationUtility.FontAssetPath} must be present.");
            return fontAsset;
        }

        private static void RestoreNanumGothicMaterialRatios()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NanumGothicFontValidationUtility.FontAssetPath);
            if (fontAsset == null || fontAsset.material == null)
            {
                return;
            }

            if (Mathf.Approximately(fontAsset.material.GetFloat(ScaleRatioA), 1f) &&
                Mathf.Approximately(fontAsset.material.GetFloat(ScaleRatioC), 1f))
            {
                return;
            }

            fontAsset.material.SetFloat(ScaleRatioA, 1f);
            fontAsset.material.SetFloat(ScaleRatioC, 1f);
            EditorUtility.SetDirty(fontAsset.material);
            AssetDatabase.SaveAssetIfDirty(fontAsset.material);
        }

        private static ScreenLayerView CreateScreenLayer(GameObject rootObject)
        {
            var screenLayerRoot = new GameObject("ScreenLayerRoot", typeof(RectTransform));
            screenLayerRoot.transform.SetParent(rootObject.transform, false);
            var contentRootObject = new GameObject("ScreenContentRoot", typeof(RectTransform));
            contentRootObject.transform.SetParent(screenLayerRoot.transform, false);
            var screenLayerView = screenLayerRoot.AddComponent<ScreenLayerView>();
            screenLayerView.Configure(screenLayerRoot, contentRootObject.GetComponent<RectTransform>());
            return screenLayerView;
        }

        private static PopupController CreatePopupController(GameObject rootObject, out DisplayPreviewTimeoutRelay timeoutRelay)
        {
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
            timeoutRelay = rootObject.AddComponent<DisplayPreviewTimeoutRelay>();
            return new PopupController(new GameplayPopupRuntimeFactory(
                popupLayerView,
                UiTestPrefabAssetUtility.LoadPopupCatalog(),
                localizedTextResolver: PackageFreeLocalizedTextResolver.CreateSettingsDefault()));
        }

        private static FakeGameplayQueryFacade CreateQueryFacade()
        {
            return new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false));
        }

        internal sealed class GameplaySettingsHarness : IDisposable
        {
            private readonly GameObject _rootObject;
            private readonly PopupController _popupController;

            private GameplaySettingsHarness(
                GameObject rootObject,
                ScreenLayerView screenLayerView,
                ScreenController screenController,
                PopupController popupController,
                RecordingUiAudioPort uiAudioPort)
            {
                _rootObject = rootObject;
                ScreenLayerView = screenLayerView;
                ScreenController = screenController;
                _popupController = popupController;
                UiAudioPort = uiAudioPort;
            }

            public ScreenLayerView ScreenLayerView { get; }

            public ScreenController ScreenController { get; private set; }

            public RecordingUiAudioPort UiAudioPort { get; }

            public SettingsScreenView SettingsView => ScreenLayerView.FindScreenView<SettingsScreenView>();

            public static GameplaySettingsHarness Create(
                ILocalizedTextResolver resolver,
                ILocalizedTmpFontResolver fontResolver = null)
            {
                var rootObject = new GameObject("SettingsProductionLocalizationRuntimeTests_GameplayHarness");
                var screenLayerView = CreateScreenLayer(rootObject);
                var popupController = CreatePopupController(rootObject, out var timeoutRelay);
                var lifecycleRelay = rootObject.AddComponent<DisplaySettingsLifecycleRelay>();
                var previewSessionHost = new DisplayPreviewSessionHost(popupController, timeoutRelay);
                var uiAudioPort = new RecordingUiAudioPort();
                var screenFactory = new GameplayScreenRuntimeFactory(
                    screenLayerView,
                    CreateQueryFacade(),
                    new ManualGameplayUiPresentationSource(),
                    new FakeAudioSettingsPort(),
                    new FakeDisplaySettingsPort(),
                    NoOpKeyboardBindingSettingsPort.Instance,
                    uiAudioPort,
                    previewSessionHost,
                    lifecycleRelay,
                    UiTestPrefabAssetUtility.LoadScreenCatalog(),
                    null,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    fontResolver);
                return new GameplaySettingsHarness(
                    rootObject,
                    screenLayerView,
                    new ScreenController(screenFactory),
                    popupController,
                    uiAudioPort);
            }

            public void ShowSettings()
            {
                ScreenController.Show(new ScreenRequest(
                    ScreenId.Settings,
                    SettingsScreenPayload.Default,
                    ScreenId.Settings.ToString()));
            }

            public void DisposeController()
            {
                ScreenController?.Dispose();
                ScreenController = null;
            }

            public void Dispose()
            {
                DisposeController();
                _popupController.Dispose();
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }
        }

        private sealed class MainMenuSettingsHarness : IDisposable
        {
            private readonly GameObject _rootObject;
            private readonly PopupController _popupController;

            private MainMenuSettingsHarness(
                GameObject rootObject,
                MainMenuSettingsRuntime runtime,
                PopupController popupController)
            {
                _rootObject = rootObject;
                Runtime = runtime;
                _popupController = popupController;
            }

            public MainMenuSettingsRuntime Runtime { get; }

            public static MainMenuSettingsHarness Create(
                ILocalizedTextResolver resolver,
                ILocalizedTmpFontResolver fontResolver = null)
            {
                var rootObject = new GameObject("SettingsProductionLocalizationRuntimeTests_MainMenuHarness");
                var contentRootObject = new GameObject("SettingsContentRoot", typeof(RectTransform));
                contentRootObject.transform.SetParent(rootObject.transform, false);
                var popupController = CreatePopupController(rootObject, out var timeoutRelay);
                var previewSessionHost = new DisplayPreviewSessionHost(popupController, timeoutRelay);
                var lifecycleRelay = rootObject.AddComponent<DisplaySettingsLifecycleRelay>();
                var runtime = new MainMenuSettingsRuntime(
                    UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(
                        UiTestPrefabAssetUtility.SettingsScreenPrefabPath),
                    contentRootObject.GetComponent<RectTransform>(),
                    new FakeAudioSettingsPort(),
                    new FakeDisplaySettingsPort(),
                    NoOpKeyboardBindingSettingsPort.Instance,
                    popupController,
                    previewSessionHost,
                    lifecycleRelay,
                    SettingsScreenPayload.Default,
                    previewTimeoutSeconds: 15d,
                    localizedTextResolver: resolver,
                    localizedTypographyResolver: DefaultLocalizedTypographyResolver.Instance,
                    localizedTmpFontResolver: fontResolver);
                return new MainMenuSettingsHarness(rootObject, runtime, popupController);
            }

            public void Dispose()
            {
                Runtime.Dispose();
                _popupController.Dispose();
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }
        }

        private sealed class CountingLocalizedTextResolver : ILocalizedTextResolver
        {
            private readonly PackageFreeLocalizedTextResolver _inner =
                PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            private Action _localeChanged;

            public string CurrentLocaleCode => _inner.CurrentLocaleCode;

            public int LocaleChangedSubscriberCount =>
                _localeChanged != null ? _localeChanged.GetInvocationList().Length : 0;

            public event Action LocaleChanged
            {
                add => _localeChanged += value;
                remove => _localeChanged -= value;
            }

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                return _inner.Resolve(descriptor);
            }

            public void SetLocale(string localeCode)
            {
                if (string.Equals(CurrentLocaleCode, localeCode, StringComparison.Ordinal))
                {
                    return;
                }

                _inner.SetLocale(localeCode);
                _localeChanged?.Invoke();
            }
        }

        private sealed class FakeUiLocalePreferenceStore : IUiLocalePreferenceStore
        {
            private string _localeCode;

            public int SaveCallCount { get; private set; }

            public string LastSavedLocaleCode { get; private set; }

            public bool TryLoad(out string localeCode)
            {
                localeCode = _localeCode;
                return localeCode != null;
            }

            public void Save(string localeCode)
            {
                SaveCallCount++;
                LastSavedLocaleCode = localeCode;
                _localeCode = localeCode;
            }
        }
    }
}

using System;
using System.Collections.Generic;
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
        private const string TypographyThemeAssetPath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";
        private const int SettingsStaticBindingCount = 29;
        private const int SettingsTypographyBindingCount = 29;
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
        public void GameplayScreenRuntimeFactory_SettingsResetConfirmation_FollowsLocaleRoundTripAndDirectKoreanOpen()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            using var harness = GameplaySettingsHarness.Create(resolver);

            harness.ShowSettings();
            harness.SettingsView.ClickInputTab();
            harness.SettingsView.InputView.ClickReset();

            AssertConfirmCopy(
                harness.ConfirmPopupView,
                "Reset Input Settings",
                "Reset input settings to defaults?",
                "Reset",
                "Cancel");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            AssertConfirmCopy(
                harness.ConfirmPopupView,
                "입력 설정 초기화",
                "입력 설정 초기화 확인",
                "초기화",
                "취소");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            AssertConfirmCopy(
                harness.ConfirmPopupView,
                "Reset Input Settings",
                "Reset input settings to defaults?",
                "Reset",
                "Cancel");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            harness.CloseConfirm();
            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            harness.SettingsView.InputView.ClickReset();

            AssertConfirmCopy(
                harness.ConfirmPopupView,
                "입력 설정 초기화",
                "입력 설정 초기화 확인",
                "초기화",
                "취소");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.KoreanLocaleCode);
        }

        [Test]
        public void GameplayScreenRuntimeFactory_DisplayPreviewConfirmation_FollowsLocaleRoundTripAndDirectKoreanOpen()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            using var harness = GameplaySettingsHarness.Create(resolver);

            harness.ShowSettings();
            harness.SettingsView.ClickDisplayTab();
            harness.SettingsView.DisplayView.SelectResolution(2);
            harness.SettingsView.DisplayView.SetFullscreen(true);
            harness.SettingsView.DisplayView.ClickApply();

            AssertConfirmCopy(
                harness.ConfirmPopupView,
                "화면 설정 미리 보기 확인",
                "1280 x 720 전체 화면 창 미리 보기. 변경은 임시이며 확인하지 않으면 15초 후 되돌아갑니다.",
                "유지",
                "되돌리기");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            AssertConfirmCopy(
                harness.ConfirmPopupView,
                "Confirm Display Preview",
                "Preview 1280 x 720 in Fullscreen Window. These changes are temporary and will revert in 15 seconds unless you confirm.",
                "Keep",
                "Revert");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            AssertConfirmCopy(
                harness.ConfirmPopupView,
                "화면 설정 미리 보기 확인",
                "1280 x 720 전체 화면 창 미리 보기. 변경은 임시이며 확인하지 않으면 15초 후 되돌아갑니다.",
                "유지",
                "되돌리기");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.KoreanLocaleCode);
        }

        [Test]
        public void GameplayPopupRuntimeFactory_RawConfirmPayload_PreservesCopyAndAppliesLocaleTypography()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            using var harness = GameplaySettingsHarness.Create(resolver);

            harness.OpenConfirm(new ConfirmPopupPayload(
                "Raw English Title",
                "Raw English body 123.",
                "Accept",
                "Decline",
                false));

            AssertConfirmCopy(
                harness.ConfirmPopupView,
                "Raw English Title",
                "Raw English body 123.",
                "Accept",
                "Decline");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            AssertConfirmCopy(
                harness.ConfirmPopupView,
                "Raw English Title",
                "Raw English body 123.",
                "Accept",
                "Decline");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            AssertConfirmCopy(
                harness.ConfirmPopupView,
                "Raw English Title",
                "Raw English body 123.",
                "Accept",
                "Decline");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            harness.CloseConfirm();
            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            harness.OpenConfirm(new ConfirmPopupPayload(
                "입력 설정",
                "화면 설정 123.",
                "확인",
                "취소",
                false));

            AssertConfirmCopy(harness.ConfirmPopupView, "입력 설정", "화면 설정 123.", "확인", "취소");
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.KoreanLocaleCode);
        }

        [Test]
        public void GameplayPopupRuntimeFactory_ConfirmClose_DisposesLocaleAndTypographySubscriptions()
        {
            var resolver = new CountingLocalizedTextResolver();
            using var harness = GameplaySettingsHarness.Create(resolver);
            harness.ShowSettings();
            var baselineSubscriberCount = resolver.LocaleChangedSubscriberCount;

            harness.OpenConfirm(new ConfirmPopupPayload("Title", "Body", "Yes", "No", false));

            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(baselineSubscriberCount + 5));
            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            harness.CloseConfirm();

            Assert.That(harness.ConfirmPopupView, Is.Null);
            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(baselineSubscriberCount));
            Assert.DoesNotThrow(() => resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode));
            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(baselineSubscriberCount));

            harness.OpenConfirm(new ConfirmPopupPayload("Second", "Independent", "Yes", "No", false));
            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(baselineSubscriberCount + 5));
            AssertConfirmTypography(harness.ConfirmPopupView, PackageFreeLocalizedTextResolver.DefaultLocaleCode);
            harness.CloseConfirm();
            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(baselineSubscriberCount));
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
            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(SettingsTypographyBindingCount));

            harness.ShowSettings();
            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(SettingsTypographyBindingCount));

            harness.DisposeController();

            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_UsesCatalogKoreanTypographyTheme()
        {
            var climateCrisisKr = UiTestPrefabAssetUtility.LoadClimateCrisisKrFont();
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            using var harness = GameplaySettingsHarness.Create(resolver);

            harness.ShowSettings();

            var titleLabel = GetText(harness.SettingsView, "_titleLabel");
            Assert.That(titleLabel.text, Is.EqualTo("설정"));
            Assert.That(titleLabel.font, Is.SameAs(climateCrisisKr));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_LanguageCycleSwitchesLocaleRefreshesLabelsAndFont()
        {
            var climateCrisisKr = UiTestPrefabAssetUtility.LoadClimateCrisisKrFont();
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            using var harness = GameplaySettingsHarness.Create(resolver);

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
            Assert.That(titleLabel.font, Is.SameAs(climateCrisisKr));
            Assert.That(view.DisplayView.LanguageLabelText, Is.EqualTo("언어"));
            Assert.That(view.DisplayView.CurrentLanguageText, Is.EqualTo("한국어"));
            Assert.That(languageButtonLabel.font, Is.SameAs(climateCrisisKr));
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
        public void GameplayScreenRuntimeFactory_SettingsRuntime_LanguageCycleRefreshesAudioDynamicValueText()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            using var harness = GameplaySettingsHarness.Create(resolver);
            harness.AudioPort.SetVolume(AudioSettingsChannel.Main, 0.5f);
            harness.AudioPort.SetMuted(AudioSettingsChannel.Main, true);

            harness.ShowSettings();
            var view = harness.SettingsView;

            Assert.That(GetAudioValueText(view.AudioView, "_mainRow").text, Is.EqualTo("50% (Muted)"));

            view.ClickDisplayTab();
            view.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(GetAudioValueText(view.AudioView, "_mainRow").text, Is.EqualTo("50% (음소거)"));

            view.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(GetAudioValueText(view.AudioView, "_mainRow").text, Is.EqualTo("50% (Muted)"));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_LanguageCycleRefreshesDisplayResolutionDynamicValueText()
        {
            var resolver = new DisplayResolutionValueResolver();
            using var harness = GameplaySettingsHarness.Create(resolver);

            harness.ShowSettings();
            var view = harness.SettingsView;

            Assert.That(view.DisplayView.CurrentDisplayValueText, Is.EqualTo("en-US: 1920 x 1080"));

            view.ClickDisplayTab();
            view.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(view.DisplayView.CurrentDisplayValueText, Is.EqualTo("ko-KR: 1920 x 1080"));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_LanguageCycleRefreshesDisplayPreviewCountdown()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            using var harness = GameplaySettingsHarness.Create(resolver);

            harness.ShowSettings();
            var view = harness.SettingsView;
            view.ClickDisplayTab();
            view.DisplayView.SelectResolution(2);
            view.DisplayView.ClickApply();

            Assert.That(GetText(view.DisplayView, "_previewCountdownLabel").text, Is.EqualTo("Reverting in 15s"));

            view.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(GetText(view.DisplayView, "_previewCountdownLabel").text, Is.EqualTo("15초 후 되돌림"));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_ExternalLocaleChange_RefreshesAllDynamicStateWithoutReplacingViewModels()
        {
            var resolver = new CountingLocalizedTextResolver();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.ReservedKey);
            using var harness = GameplaySettingsHarness.Create(resolver, keyboardPort: keyboardPort);
            harness.AudioPort.SetVolume(AudioSettingsChannel.Main, 0.5f);
            harness.AudioPort.SetMuted(AudioSettingsChannel.Main, true);

            harness.ShowSettings();
            var view = harness.SettingsView;
            view.ClickDisplayTab();
            view.DisplayView.SelectResolution(2);
            view.DisplayView.ClickApply();
            view.ClickInputTab();
            view.InputView.ClickPushChange();

            var screenModel = GetField<SettingsScreenViewModel>(view, "_viewModel");
            var audioModel = GetField<SettingsAudioViewModel>(view.AudioView, "_viewModel");
            var displayModel = GetField<SettingsDisplayViewModel>(view.DisplayView, "_viewModel");
            var inputModel = GetField<SettingsInputViewModel>(view.InputView, "_viewModel");
            Assert.That(view.DisplayView.DisplayStatusText, Does.Contain("15 seconds"));
            Assert.That(GetText(view.DisplayView, "_previewCountdownLabel").text, Is.EqualTo("Reverting in 15s"));
            Assert.That(keyboardPort.IsRebinding, Is.True);
            Assert.That(inputModel.IsRebinding, Is.True);
            Assert.That(view.InputView.StatusText, Is.EqualTo("Press a key for Push..."));
            Assert.That(resolver.LocaleChangedSubscriberCount, Is.GreaterThan(0));

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            AssertSettingsLabels(view, "설정", "오디오", "디스플레이", "입력", "뒤로");
            Assert.That(GetAudioValueText(view.AudioView, "_mainRow").text, Is.EqualTo("50% (음소거)"));
            Assert.That(view.DisplayView.LanguageLabelText, Is.EqualTo("언어"));
            Assert.That(view.DisplayView.CurrentLanguageText, Is.EqualTo("한국어"));
            Assert.That(view.DisplayView.DisplayStatusText, Does.Contain("15초"));
            Assert.That(GetText(view.DisplayView, "_previewCountdownLabel").text, Is.EqualTo("15초 후 되돌림"));
            Assert.That(keyboardPort.IsRebinding, Is.True);
            Assert.That(view.InputView.StatusText, Is.EqualTo("밀기 키 입력하세요..."));
            Assert.That(GetField<SettingsScreenViewModel>(view, "_viewModel"), Is.SameAs(screenModel));
            Assert.That(GetField<SettingsAudioViewModel>(view.AudioView, "_viewModel"), Is.SameAs(audioModel));
            Assert.That(GetField<SettingsDisplayViewModel>(view.DisplayView, "_viewModel"), Is.SameAs(displayModel));
            Assert.That(GetField<SettingsInputViewModel>(view.InputView, "_viewModel"), Is.SameAs(inputModel));

            keyboardPort.Complete();

            Assert.That(keyboardPort.IsRebinding, Is.False);
            Assert.That(view.InputView.StatusText, Is.EqualTo("이 키는 예약되어 있습니다."));
            Assert.That(GetField<SettingsInputViewModel>(view.InputView, "_viewModel"), Is.SameAs(inputModel));

            harness.DisposeController();

            Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(0));
            Assert.DoesNotThrow(() => resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode));
        }

        [TestCase(
            KeyboardBindableAction.Push,
            "밀기 키 입력하세요...")]
        [TestCase(
            KeyboardBindableAction.Flip,
            "뒤집기 키 입력하세요...")]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_RebindPromptStartedInKoreanUsesCurrentLocale(
            KeyboardBindableAction action,
            string expectedPrompt)
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.Success);
            using var harness = GameplaySettingsHarness.Create(resolver, keyboardPort: keyboardPort);

            harness.ShowSettings();
            var view = harness.SettingsView;
            view.ClickInputTab();
            StartRebind(view.InputView, action);

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(view.InputView.StatusText, Is.EqualTo(expectedPrompt));
        }

        [TestCase(
            KeyboardBindableAction.Push,
            "Press a key for Push...",
            "밀기 키 입력하세요...")]
        [TestCase(
            KeyboardBindableAction.Flip,
            "Press a key for Flip...",
            "뒤집기 키 입력하세요...")]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_ActiveRebindPromptFollowsLocaleRoundTrip(
            KeyboardBindableAction action,
            string englishPrompt,
            string koreanPrompt)
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.Success);
            using var harness = GameplaySettingsHarness.Create(resolver, keyboardPort: keyboardPort);

            harness.ShowSettings();
            var view = harness.SettingsView;
            view.ClickInputTab();
            StartRebind(view.InputView, action);

            Assert.That(view.InputView.StatusText, Is.EqualTo(englishPrompt));

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            Assert.That(view.InputView.StatusText, Is.EqualTo(koreanPrompt));

            resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            Assert.That(view.InputView.StatusText, Is.EqualTo(englishPrompt));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_LanguageCycleRefreshesInputReservedKeyStatus()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.ReservedKey);
            using var harness = GameplaySettingsHarness.Create(resolver, keyboardPort: keyboardPort);

            harness.ShowSettings();
            var view = harness.SettingsView;
            view.ClickInputTab();
            view.InputView.ClickPushChange();
            keyboardPort.Complete();

            Assert.That(view.InputView.StatusText, Is.EqualTo("This key is reserved."));
            Assert.That(GetText(view.InputView, "_pushKeyDisplayLabel").text, Is.EqualTo("E"));

            view.ClickDisplayTab();
            view.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(view.InputView.StatusText, Is.EqualTo("이 키는 예약되어 있습니다."));
            Assert.That(GetText(view.InputView, "_pushKeyDisplayLabel").text, Is.EqualTo("E"));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_LanguageCycleRefreshesInputMovementConflictStatus()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.MovementConflict);
            using var harness = GameplaySettingsHarness.Create(resolver, keyboardPort: keyboardPort);

            harness.ShowSettings();
            var view = harness.SettingsView;
            view.ClickInputTab();
            view.InputView.ClickPushChange();
            keyboardPort.Complete();

            Assert.That(view.InputView.StatusText, Is.EqualTo("This key conflicts with movement keys."));
            Assert.That(GetText(view.InputView, "_pushKeyDisplayLabel").text, Is.EqualTo("E"));

            view.ClickDisplayTab();
            view.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(view.InputView.StatusText, Is.EqualTo("이 키는 이동 키와 충돌합니다."));
            Assert.That(GetText(view.InputView, "_pushKeyDisplayLabel").text, Is.EqualTo("E"));
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_LanguageCycleRefreshesInputAlreadyRebindingStatus()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var keyboardPort = new RejectingKeyboardSettingsPort(KeyboardBindingValidationResult.AlreadyRebinding);
            using var harness = GameplaySettingsHarness.Create(resolver, keyboardPort: keyboardPort);

            harness.ShowSettings();
            var view = harness.SettingsView;
            view.ClickInputTab();
            view.InputView.ClickPushChange();

            Assert.That(view.InputView.StatusText, Is.EqualTo("Rebind already in progress."));
            Assert.That(GetText(view.InputView, "_pushKeyDisplayLabel").text, Is.EqualTo("E"));

            view.ClickDisplayTab();
            view.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(view.InputView.StatusText, Is.EqualTo("키 변경이 이미 진행 중입니다."));
            Assert.That(GetText(view.InputView, "_pushKeyDisplayLabel").text, Is.EqualTo("E"));
        }

        [TestCase("Space", "Left Shift")]
        [TestCase("Enter", "Numpad Enter")]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_WordKeyNamesRemainRawAndTypographicallyInvariant(
            string pushDisplayName,
            string flipDisplayName)
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var keyboardPort = new MutableKeyboardSettingsPort(pushDisplayName, flipDisplayName);
            using var harness = GameplaySettingsHarness.Create(resolver, keyboardPort);

            harness.ShowSettings();
            var input = harness.SettingsView.InputView;
            var pushCurrent = GetText(input, "_pushCurrentText");
            var pushKeycap = GetText(input, "_pushKeyDisplayLabel");
            var flipCurrent = GetText(input, "_flipCurrentText");
            var flipKeycap = GetText(input, "_flipKeyDisplayLabel");
            var states = new Dictionary<TMP_Text, InvariantTypographyState>
            {
                [pushCurrent] = new InvariantTypographyState(pushCurrent),
                [pushKeycap] = new InvariantTypographyState(pushKeycap),
                [flipCurrent] = new InvariantTypographyState(flipCurrent),
                [flipKeycap] = new InvariantTypographyState(flipKeycap),
            };

            AssertKeyDisplayPair(pushCurrent, pushKeycap, pushDisplayName);
            AssertKeyDisplayPair(flipCurrent, flipKeycap, flipDisplayName);

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            AssertInputLabels(input, "이동 키", "화살표 키 사용", "밀기", "뒤집기", "변경", "입력 초기화");
            AssertKeyDisplayPair(pushCurrent, pushKeycap, pushDisplayName);
            AssertKeyDisplayPair(flipCurrent, flipKeycap, flipDisplayName);
            AssertInvariantTypography(states, "ko-KR");

            resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            AssertInputLabels(input, "Movement Keys", "Use Arrow Keys", "Push", "Flip", "Change", "Reset Input");
            AssertKeyDisplayPair(pushCurrent, pushKeycap, pushDisplayName);
            AssertKeyDisplayPair(flipCurrent, flipKeycap, flipDisplayName);
            AssertInvariantTypography(states, "restored en-US");
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_RebindFromEToSpaceRefreshesWithoutLocaleTypographyMutation()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var keyboardPort = new MutableKeyboardSettingsPort("E", "Q");
            using var harness = GameplaySettingsHarness.Create(resolver, keyboardPort);

            harness.ShowSettings();
            var input = harness.SettingsView.InputView;
            var pushCurrent = GetText(input, "_pushCurrentText");
            var pushKeycap = GetText(input, "_pushKeyDisplayLabel");
            var states = new Dictionary<TMP_Text, InvariantTypographyState>
            {
                [pushCurrent] = new InvariantTypographyState(pushCurrent),
                [pushKeycap] = new InvariantTypographyState(pushKeycap),
            };

            AssertKeyDisplayPair(pushCurrent, pushKeycap, "E");
            harness.SettingsView.ClickInputTab();
            input.ClickPushChange();
            keyboardPort.Complete("Space");

            AssertKeyDisplayPair(pushCurrent, pushKeycap, "Space");
            AssertInvariantTypography(states, "after rebind");

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            AssertKeyDisplayPair(pushCurrent, pushKeycap, "Space");
            AssertInvariantTypography(states, "ko-KR after rebind");

            resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode);
            AssertKeyDisplayPair(pushCurrent, pushKeycap, "Space");
            AssertInvariantTypography(states, "restored en-US after rebind");
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_LongWordKeyNamesFitCurrentAndKeycapLabels()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var keyboardPort = new MutableKeyboardSettingsPort("Print Screen", "Numpad Enter");
            using var harness = GameplaySettingsHarness.Create(resolver, keyboardPort);

            harness.ShowSettings();
            var input = harness.SettingsView.InputView;
            var labels = new[]
            {
                GetText(input, "_pushCurrentText"),
                GetText(input, "_pushKeyDisplayLabel"),
                GetText(input, "_flipCurrentText"),
                GetText(input, "_flipKeyDisplayLabel"),
            };

            foreach (var label in labels)
            {
                label.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                Assert.That(label.isTextOverflowing, Is.False, $"{label.name} '{label.text}' overflow");
                Assert.That(label.textInfo.lineCount, Is.LessThanOrEqualTo(1), $"{label.name} '{label.text}' wrapping");
            }

            Assert.That(GetText(input, "_pushKeyDisplayLabel").enableAutoSizing, Is.True);
            Assert.That(GetText(input, "_flipKeyDisplayLabel").enableAutoSizing, Is.True);
            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            foreach (var label in labels)
            {
                label.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                Assert.That(label.isTextOverflowing, Is.False, $"ko-KR {label.name} '{label.text}' overflow");
                Assert.That(label.textInfo.lineCount, Is.LessThanOrEqualTo(1), $"ko-KR {label.name} '{label.text}' wrapping");
            }
        }


        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_ReopenStartsFromPersistedLocaleAndFont()
        {
            var store = new FakeUiLocalePreferenceStore();
            var climateCrisisKr = UiTestPrefabAssetUtility.LoadClimateCrisisKrFont();

            using (var firstHarness = GameplaySettingsHarness.Create(
                       PackageFreeLocalizedTextResolver.CreateSettingsDefault(store)))
            {
                firstHarness.ShowSettings();
                firstHarness.SettingsView.ClickDisplayTab();
                firstHarness.SettingsView.DisplayView.ClickLanguageCycle();

                Assert.That(store.LastSavedLocaleCode, Is.EqualTo(PackageFreeLocalizedTextResolver.KoreanLocaleCode));
            }

            using (var secondHarness = GameplaySettingsHarness.Create(
                       PackageFreeLocalizedTextResolver.CreateSettingsDefault(store)))
            {
                secondHarness.ShowSettings();

                var titleLabel = GetText(secondHarness.SettingsView, "_titleLabel");
                Assert.That(titleLabel.text, Is.EqualTo("설정"));
                Assert.That(titleLabel.font, Is.SameAs(climateCrisisKr));
                Assert.That(secondHarness.SettingsView.DisplayView.LanguageLabelText, Is.EqualTo("언어"));
                Assert.That(secondHarness.SettingsView.DisplayView.CurrentLanguageText, Is.EqualTo("한국어"));
            }
        }

        [Test]
        public void GameplayScreenRuntimeFactory_SettingsRuntime_DoesNotRequireLegacyKoreanFontResolver()
        {
            var expectedFont = UiTestPrefabAssetUtility.LoadClimateCrisisKrFont();
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            using var harness = GameplaySettingsHarness.Create(resolver);

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
        public void MainMenuSettingsRuntime_UsesCatalogKoreanTypographyTheme()
        {
            var climateCrisisKr = UiTestPrefabAssetUtility.LoadClimateCrisisKrFont();
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            using var harness = MainMenuSettingsHarness.Create(resolver);

            harness.Runtime.Open();

            var titleLabel = GetText(harness.Runtime.View, "_titleLabel");
            Assert.That(titleLabel.text, Is.EqualTo("설정"));
            Assert.That(titleLabel.font, Is.SameAs(climateCrisisKr));
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
            var staticLabels = new[]
            {
                (SettingsStaticTextDescriptors.AudioMain, "Main", "마스터"),
                (SettingsStaticTextDescriptors.AudioBgm, "Background Music", "배경 음악"),
                (SettingsStaticTextDescriptors.AudioSfx, "Effects", "효과음"),
                (SettingsStaticTextDescriptors.AudioMute, "Mute", "음소거"),
                (SettingsStaticTextDescriptors.DisplayCurrent, "Current Display", "현재 디스플레이"),
                (SettingsStaticTextDescriptors.DisplayResolution, "Resolution", "해상도"),
                (
                    SettingsStaticTextDescriptors.DisplayResolutionHint,
                    "Only automatically detected resolutions are shown.",
                    "자동으로 감지된 해상도만 표시됩니다."),
                (SettingsStaticTextDescriptors.DisplayFullscreenWindow, "Fullscreen Window", "전체 화면 창"),
                (SettingsStaticTextDescriptors.DisplayFullscreenOn, "On", "켜짐"),
                (SettingsStaticTextDescriptors.DisplayApply, "Apply", "적용"),
                (SettingsStaticTextDescriptors.DisplayRevert, "Revert", "되돌리기"),
            };

            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Title), Is.EqualTo("Settings"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Language), Is.EqualTo("Language"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.LanguageKorean), Is.EqualTo("Korean"));
            foreach (var (descriptor, english, _) in staticLabels)
            {
                Assert.That(
                    resolver.Resolve(descriptor),
                    Is.EqualTo(english),
                    $"Expected canonical en-US value for {descriptor.Key}.");
            }

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Title), Is.EqualTo("설정"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Language), Is.EqualTo("언어"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.LanguageKorean), Is.EqualTo("한국어"));
            foreach (var (descriptor, _, korean) in staticLabels)
            {
                Assert.That(
                    resolver.Resolve(descriptor),
                    Is.EqualTo(korean),
                    $"Expected canonical ko-KR value for {descriptor.Key}.");
            }

            resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            foreach (var (descriptor, english, _) in staticLabels)
            {
                Assert.That(
                    resolver.Resolve(descriptor),
                    Is.EqualTo(english),
                    $"Expected canonical en-US value after locale round-trip for {descriptor.Key}.");
            }

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

        private static void AssertConfirmCopy(
            ConfirmPopupView view,
            string title,
            string body,
            string confirm,
            string cancel)
        {
            Assert.That(view, Is.Not.Null);
            Assert.That(view.TitleText, Is.EqualTo(title));
            Assert.That(view.BodyText, Is.EqualTo(body));
            Assert.That(GetText(view, "_confirmButtonLabel").text, Is.EqualTo(confirm));
            Assert.That(GetText(view, "_cancelButtonLabel").text, Is.EqualTo(cancel));
        }

        private static void AssertConfirmTypography(ConfirmPopupView view, string localeCode)
        {
            Assert.That(view, Is.Not.Null);
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(TypographyThemeAssetPath);
            Assert.That(theme, Is.Not.Null, TypographyThemeAssetPath);
            AssertConfirmTypographyTarget(
                GetText(view, "_titleLabel"),
                theme,
                localeCode,
                TypographyStyleTag.HeaderLarge,
                30f,
                16f,
                30f);
            AssertConfirmTypographyTarget(
                GetText(view, "_bodyLabel"),
                theme,
                localeCode,
                TypographyStyleTag.PopupBody,
                20f,
                12f,
                20f);
            AssertConfirmTypographyTarget(
                GetText(view, "_confirmButtonLabel"),
                theme,
                localeCode,
                TypographyStyleTag.PopupAction,
                18f,
                14f,
                18f);
            AssertConfirmTypographyTarget(
                GetText(view, "_cancelButtonLabel"),
                theme,
                localeCode,
                TypographyStyleTag.PopupAction,
                18f,
                14f,
                18f);
        }

        private static void AssertConfirmTypographyTarget(
            TMP_Text target,
            GameplayUiTypographyTheme theme,
            string localeCode,
            TypographyStyleTag expectedTag,
            float expectedFontSize,
            float expectedMinSize,
            float expectedMaxSize)
        {
            var binding = TypographyBinding.FindFor(target);
            Assert.That(binding, Is.Not.Null, target.name);
            Assert.That(binding.StyleTag, Is.EqualTo(expectedTag), target.name);
            Assert.That(binding.SizingSourceOverride, Is.EqualTo(TypographySizingSource.Hybrid), target.name);
            Assert.That(binding.UseApplyMaskOverride, Is.False, target.name);

            var style = theme.ResolveOrThrow(localeCode, expectedTag);
            Assert.That(target.font, Is.SameAs(style.FontAsset), $"{target.name} font {localeCode}");
            Assert.That(
                target.fontSharedMaterial,
                Is.SameAs(style.MaterialPreset),
                $"{target.name} material {localeCode}");
            Assert.That(target.fontStyle, Is.EqualTo(style.FontStyle), $"{target.name} style {localeCode}");
            Assert.That(style.ApplyMask & TypographyApplyMask.Sizing, Is.EqualTo(TypographyApplyMask.None));
            Assert.That(target.fontSize, Is.EqualTo(expectedFontSize), $"{target.name} size {localeCode}");
            Assert.That(target.enableAutoSizing, Is.True, $"{target.name} auto sizing {localeCode}");
            Assert.That(target.fontSizeMin, Is.EqualTo(expectedMinSize), $"{target.name} min size {localeCode}");
            Assert.That(target.fontSizeMax, Is.EqualTo(expectedMaxSize), $"{target.name} max size {localeCode}");

            if (string.Equals(localeCode, PackageFreeLocalizedTextResolver.KoreanLocaleCode, StringComparison.Ordinal))
            {
                Assert.That(
                    target.font,
                    Is.SameAs(UiTestPrefabAssetUtility.LoadClimateCrisisKrFont()),
                    $"{target.name} ko-KR Climate identity");
            }
        }

        private static TMP_Text GetText(object target, string fieldName)
        {
            return GetField<TMP_Text>(target, fieldName);
        }

        private static void AssertKeyDisplayPair(TMP_Text current, TMP_Text keycap, string expected)
        {
            Assert.That(current.text, Is.EqualTo(expected));
            Assert.That(keycap.text, Is.EqualTo(expected));
        }

        private static void StartRebind(SettingsInputView inputView, KeyboardBindableAction action)
        {
            if (action == KeyboardBindableAction.Push)
            {
                inputView.ClickPushChange();
                return;
            }

            if (action == KeyboardBindableAction.Flip)
            {
                inputView.ClickFlipChange();
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported keyboard rebind action.");
        }

        private static void AssertInvariantTypography(
            IReadOnlyDictionary<TMP_Text, InvariantTypographyState> states,
            string stage)
        {
            foreach (var pair in states)
            {
                pair.Value.AssertSame(pair.Key, $"{pair.Key.name} at {stage}");
            }
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

        private static TMP_Text GetAudioValueText(SettingsAudioView view, string rowFieldName)
        {
            var rowField = typeof(SettingsAudioView).GetField(rowFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rowField, Is.Not.Null, $"{nameof(SettingsAudioView)}.{rowFieldName} must exist.");
            var row = rowField.GetValue(view);
            Assert.That(row, Is.Not.Null);

            var valueProperty = row.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(valueProperty, Is.Not.Null, $"{row.GetType().Name}.Value must exist.");
            var value = valueProperty.GetValue(row) as TMP_Text;
            Assert.That(value, Is.Not.Null);
            return value;
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

        private static PopupController CreatePopupController(
            GameObject rootObject,
            ILocalizedTextResolver resolver,
            out DisplayPreviewTimeoutRelay timeoutRelay)
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
                localizedTextResolver: resolver));
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
                FakeAudioSettingsPort audioPort,
                RecordingUiAudioPort uiAudioPort)
            {
                _rootObject = rootObject;
                ScreenLayerView = screenLayerView;
                ScreenController = screenController;
                _popupController = popupController;
                AudioPort = audioPort;
                UiAudioPort = uiAudioPort;
                ScreenController.ActionRequested += HandleScreenActionRequested;
            }

            public ScreenLayerView ScreenLayerView { get; }

            public ScreenController ScreenController { get; private set; }

            public FakeAudioSettingsPort AudioPort { get; }

            public RecordingUiAudioPort UiAudioPort { get; }

            public SettingsScreenView SettingsView => ScreenLayerView.FindScreenView<SettingsScreenView>();

            public ConfirmPopupView ConfirmPopupView =>
                _rootObject.GetComponentInChildren<ConfirmPopupView>(true);

            public static GameplaySettingsHarness Create(
                ILocalizedTextResolver resolver,
                IKeyboardBindingSettingsPort keyboardPort = null)
            {
                var rootObject = new GameObject("SettingsProductionLocalizationRuntimeTests_GameplayHarness");
                var screenLayerView = CreateScreenLayer(rootObject);
                var popupController = CreatePopupController(rootObject, resolver, out var timeoutRelay);
                var lifecycleRelay = rootObject.AddComponent<DisplaySettingsLifecycleRelay>();
                var previewSessionHost = new DisplayPreviewSessionHost(popupController, timeoutRelay);
                var audioPort = new FakeAudioSettingsPort();
                var uiAudioPort = new RecordingUiAudioPort();
                var screenFactory = new GameplayScreenRuntimeFactory(
                    screenLayerView,
                    CreateQueryFacade(),
                    new ManualGameplayUiPresentationSource(),
                    audioPort,
                    new FakeDisplaySettingsPort(),
                    keyboardPort ?? NoOpKeyboardBindingSettingsPort.Instance,
                    uiAudioPort,
                    previewSessionHost,
                    lifecycleRelay,
                    UiTestPrefabAssetUtility.LoadScreenCatalog(),
                    null,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance);
                return new GameplaySettingsHarness(
                    rootObject,
                    screenLayerView,
                    new ScreenController(screenFactory),
                    popupController,
                    audioPort,
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
                if (ScreenController != null)
                {
                    ScreenController.ActionRequested -= HandleScreenActionRequested;
                }

                ScreenController?.Dispose();
                ScreenController = null;
            }

            public void CloseConfirm()
            {
                _popupController.CloseTop(PopupCloseReason.UserAction, PopupCompletionKind.Cancelled);
            }

            public void OpenConfirm(ConfirmPopupPayload payload)
            {
                _popupController.Push(new PopupRequest(PopupId.Confirm, payload), out _);
            }

            public void Dispose()
            {
                DisposeController();
                _popupController.Dispose();
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }

            private void HandleScreenActionRequested(ScreenAction action)
            {
                if (action.ActionKind == ScreenActionKind.RequestPopup)
                {
                    _popupController.Push(action.PopupRequest, out _);
                }
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

            public static MainMenuSettingsHarness Create(ILocalizedTextResolver resolver)
            {
                var rootObject = new GameObject("SettingsProductionLocalizationRuntimeTests_MainMenuHarness");
                var contentRootObject = new GameObject("SettingsContentRoot", typeof(RectTransform));
                contentRootObject.transform.SetParent(rootObject.transform, false);
                var popupController = CreatePopupController(rootObject, resolver, out var timeoutRelay);
                var previewSessionHost = new DisplayPreviewSessionHost(popupController, timeoutRelay);
                var lifecycleRelay = rootObject.AddComponent<DisplaySettingsLifecycleRelay>();
                var catalog = UiTestPrefabAssetUtility.LoadScreenCatalog();
                var runtime = new MainMenuSettingsRuntime(
                    new SettingsScreenRuntimeBuildContext(
                        parent: contentRootObject.GetComponent<RectTransform>(),
                        prefab: catalog.SettingsPrefab,
                        audioSettingsPort: new FakeAudioSettingsPort(),
                        displaySettingsPort: new FakeDisplaySettingsPort(),
                        keyboardBindingSettingsPort: NoOpKeyboardBindingSettingsPort.Instance,
                        uiAudioPort: new RecordingUiAudioPort(),
                        displayPreviewSessionHost: previewSessionHost,
                        displaySettingsLifecycleRelay: lifecycleRelay,
                        typographyTheme: catalog.SettingsTypographyTheme,
                        localizedTextResolver: resolver,
                        localizedTypographyResolver: DefaultLocalizedTypographyResolver.Instance),
                    SettingsScreenPayload.Default,
                    popupController);
                return new MainMenuSettingsHarness(rootObject, runtime, popupController);
            }

            public void Dispose()
            {
                Runtime.Dispose();
                _popupController.Dispose();
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }
        }

        private sealed class CompletingKeyboardSettingsPort : IKeyboardBindingSettingsPort
        {
            private readonly KeyboardBindingValidationResult _completionResult;
            private Action<KeyboardRebindResult> _completed;
            private KeyboardBindableAction _rebindingAction;
            private bool _isRebinding;

            public CompletingKeyboardSettingsPort(KeyboardBindingValidationResult completionResult)
            {
                _completionResult = completionResult;
            }

            public bool IsRebinding => _isRebinding;

            public KeyboardBindingSettingsSnapshot Read()
            {
                return BuildSnapshot();
            }

            public KeyboardBindingValidationResult TrySetMovementScheme(KeyboardMovementScheme scheme)
            {
                return KeyboardBindingValidationResult.Success;
            }

            public KeyboardRebindStartResult StartRebind(
                KeyboardBindableAction action,
                Action<KeyboardRebindResult> completed)
            {
                _isRebinding = true;
                _rebindingAction = action;
                _completed = completed;
                return new KeyboardRebindStartResult(
                    true,
                    KeyboardBindingValidationResult.Success,
                    BuildSnapshot());
            }

            public void CancelRebind()
            {
                _isRebinding = false;
                _completed = null;
            }

            public KeyboardBindingSettingsSnapshot ResetToDefaults()
            {
                _isRebinding = false;
                return BuildSnapshot();
            }

            public void Complete()
            {
                var completed = _completed;
                _completed = null;
                _isRebinding = false;
                completed?.Invoke(new KeyboardRebindResult(
                    _rebindingAction,
                    _completionResult,
                    BuildSnapshot()));
            }

            private KeyboardBindingSettingsSnapshot BuildSnapshot()
            {
                return new KeyboardBindingSettingsSnapshot(
                    KeyboardMovementScheme.Wasd,
                    "WASD",
                    "E",
                    "Q",
                    _isRebinding,
                    _isRebinding ? (KeyboardBindableAction?)_rebindingAction : null);
            }
        }

        private sealed class MutableKeyboardSettingsPort : IKeyboardBindingSettingsPort
        {
            private Action<KeyboardRebindResult> _completed;
            private string _flipDisplayName;
            private bool _isRebinding;
            private string _pushDisplayName;
            private KeyboardBindableAction _rebindingAction;

            public MutableKeyboardSettingsPort(string pushDisplayName, string flipDisplayName)
            {
                _pushDisplayName = pushDisplayName;
                _flipDisplayName = flipDisplayName;
            }

            public bool IsRebinding => _isRebinding;

            public KeyboardBindingSettingsSnapshot Read() => BuildSnapshot();

            public KeyboardBindingValidationResult TrySetMovementScheme(KeyboardMovementScheme scheme) =>
                KeyboardBindingValidationResult.Success;

            public KeyboardRebindStartResult StartRebind(
                KeyboardBindableAction action,
                Action<KeyboardRebindResult> completed)
            {
                _isRebinding = true;
                _rebindingAction = action;
                _completed = completed;
                return new KeyboardRebindStartResult(true, KeyboardBindingValidationResult.Success, BuildSnapshot());
            }

            public void CancelRebind()
            {
                _isRebinding = false;
                _completed = null;
            }

            public KeyboardBindingSettingsSnapshot ResetToDefaults()
            {
                _isRebinding = false;
                _pushDisplayName = "E";
                _flipDisplayName = "Q";
                return BuildSnapshot();
            }

            public void Complete(string displayName)
            {
                if (_rebindingAction == KeyboardBindableAction.Push)
                {
                    _pushDisplayName = displayName;
                }
                else
                {
                    _flipDisplayName = displayName;
                }

                var completed = _completed;
                _completed = null;
                _isRebinding = false;
                completed?.Invoke(new KeyboardRebindResult(
                    _rebindingAction,
                    KeyboardBindingValidationResult.Success,
                    BuildSnapshot()));
            }

            private KeyboardBindingSettingsSnapshot BuildSnapshot()
            {
                return new KeyboardBindingSettingsSnapshot(
                    KeyboardMovementScheme.Wasd,
                    "WASD",
                    _pushDisplayName,
                    _flipDisplayName,
                    _isRebinding,
                    _isRebinding ? (KeyboardBindableAction?)_rebindingAction : null);
            }
        }

        private readonly struct InvariantTypographyState
        {
            private readonly bool _enableAutoSizing;
            private readonly TMP_FontAsset _font;
            private readonly float _fontSize;
            private readonly float _fontSizeMax;
            private readonly float _fontSizeMin;
            private readonly FontStyles _fontStyle;
            private readonly float _characterSpacing;
            private readonly float _lineSpacing;
            private readonly Material _material;

            public InvariantTypographyState(TMP_Text target)
            {
                _font = target.font;
                _material = target.fontSharedMaterial;
                _fontStyle = target.fontStyle;
                _fontSize = target.fontSize;
                _enableAutoSizing = target.enableAutoSizing;
                _fontSizeMin = target.fontSizeMin;
                _fontSizeMax = target.fontSizeMax;
                _lineSpacing = target.lineSpacing;
                _characterSpacing = target.characterSpacing;
            }

            public void AssertSame(TMP_Text target, string context)
            {
                Assert.That(target.font, Is.SameAs(_font), context + " font");
                Assert.That(target.fontSharedMaterial, Is.SameAs(_material), context + " material");
                Assert.That(target.fontStyle, Is.EqualTo(_fontStyle), context + " fontStyle");
                Assert.That(target.fontSize, Is.EqualTo(_fontSize), context + " fontSize");
                Assert.That(target.enableAutoSizing, Is.EqualTo(_enableAutoSizing), context + " autoSizing");
                Assert.That(target.fontSizeMin, Is.EqualTo(_fontSizeMin), context + " fontSizeMin");
                Assert.That(target.fontSizeMax, Is.EqualTo(_fontSizeMax), context + " fontSizeMax");
                Assert.That(target.lineSpacing, Is.EqualTo(_lineSpacing), context + " lineSpacing");
                Assert.That(target.characterSpacing, Is.EqualTo(_characterSpacing), context + " characterSpacing");
            }
        }

        private sealed class RejectingKeyboardSettingsPort : IKeyboardBindingSettingsPort
        {
            private readonly KeyboardBindingValidationResult _validationResult;

            public RejectingKeyboardSettingsPort(KeyboardBindingValidationResult validationResult)
            {
                _validationResult = validationResult;
            }

            public bool IsRebinding => false;

            public KeyboardBindingSettingsSnapshot Read()
            {
                return BuildSnapshot();
            }

            public KeyboardBindingValidationResult TrySetMovementScheme(KeyboardMovementScheme scheme)
            {
                return KeyboardBindingValidationResult.Success;
            }

            public KeyboardRebindStartResult StartRebind(
                KeyboardBindableAction action,
                Action<KeyboardRebindResult> completed)
            {
                return new KeyboardRebindStartResult(false, _validationResult, BuildSnapshot());
            }

            public void CancelRebind()
            {
            }

            public KeyboardBindingSettingsSnapshot ResetToDefaults()
            {
                return BuildSnapshot();
            }

            private static KeyboardBindingSettingsSnapshot BuildSnapshot()
            {
                return new KeyboardBindingSettingsSnapshot(
                    KeyboardMovementScheme.Wasd,
                    "WASD",
                    "E",
                    "Q",
                    false,
                    null);
            }
        }

        private sealed class CountingLocalizedTextResolver : ILocalizedTextResolver, IUiLocaleSelectionPort
        {
            private readonly PackageFreeLocalizedTextResolver _inner =
                PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            private Action _localeChanged;

            public string CurrentLocaleCode => _inner.CurrentLocaleCode;

            public IReadOnlyList<string> AvailableLocaleCodes => _inner.AvailableLocaleCodes;

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

            public bool TrySetLocale(string localeCode)
            {
                if (!_inner.TrySetLocale(localeCode))
                {
                    return false;
                }

                _localeChanged?.Invoke();
                return true;
            }
        }

        private sealed class DisplayResolutionValueResolver : ILocalizedTextResolver, IUiLocaleSelectionPort
        {
            private readonly PackageFreeLocalizedTextResolver _inner =
                PackageFreeLocalizedTextResolver.CreateSettingsDefault();

            public string CurrentLocaleCode => _inner.CurrentLocaleCode;

            public IReadOnlyList<string> AvailableLocaleCodes => _inner.AvailableLocaleCodes;

            public event Action LocaleChanged;

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                if (string.Equals(
                        descriptor.Key,
                        SettingsDynamicTextDescriptors.DisplayResolutionValueKey,
                        StringComparison.Ordinal))
                {
                    var resolutionLabel = descriptor.Arguments.Count > 0
                        ? descriptor.Arguments[0]?.ToString() ?? string.Empty
                        : string.Empty;
                    return $"{CurrentLocaleCode}: {resolutionLabel}";
                }

                return _inner.Resolve(descriptor);
            }

            public bool TrySetLocale(string localeCode)
            {
                if (!_inner.TrySetLocale(localeCode))
                {
                    return false;
                }

                LocaleChanged?.Invoke();
                return true;
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

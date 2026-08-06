using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsLocalizationFoundationTests
    {
        private static readonly LocalizedTextDescriptor[] ExpectedSettingsDescriptors =
        {
            SettingsStaticTextDescriptors.Title,
            SettingsStaticTextDescriptors.AudioTab,
            SettingsStaticTextDescriptors.DisplayTab,
            SettingsStaticTextDescriptors.InputTab,
            SettingsStaticTextDescriptors.MovementKeys,
            SettingsStaticTextDescriptors.UseArrowKeys,
            SettingsStaticTextDescriptors.Push,
            SettingsStaticTextDescriptors.Flip,
            SettingsStaticTextDescriptors.Change,
            SettingsStaticTextDescriptors.ResetInput,
            SettingsStaticTextDescriptors.Language,
            SettingsStaticTextDescriptors.LanguageEnglish,
            SettingsStaticTextDescriptors.LanguageKorean,
            SettingsStaticTextDescriptors.AudioMain,
            SettingsStaticTextDescriptors.AudioBgm,
            SettingsStaticTextDescriptors.AudioSfx,
            SettingsStaticTextDescriptors.AudioMute,
            SettingsStaticTextDescriptors.DisplayCurrent,
            SettingsStaticTextDescriptors.DisplayResolution,
            SettingsStaticTextDescriptors.DisplayResolutionHint,
            SettingsStaticTextDescriptors.DisplayFullscreenWindow,
            SettingsStaticTextDescriptors.DisplayFullscreenOn,
            SettingsStaticTextDescriptors.DisplayApply,
            SettingsStaticTextDescriptors.DisplayRevert,
            SettingsStaticTextDescriptors.Back,
        };

        private static readonly LocalizedTextDescriptor[] ExpectedPauseDescriptors =
        {
            PauseStaticTextDescriptors.Title,
            PauseStaticTextDescriptors.Description,
            PauseStaticTextDescriptors.Resume,
            PauseStaticTextDescriptors.Settings,
            PauseStaticTextDescriptors.Retry,
            PauseStaticTextDescriptors.MainMenu,
        };

        private static readonly LocalizedTextDescriptor[] ExpectedMainMenuDescriptors =
        {
            MainMenuStaticTextDescriptors.Start,
            MainMenuStaticTextDescriptors.Settings,
            MainMenuStaticTextDescriptors.Quit,
        };

        [Test]
        public void LocalizedTextDescriptor_PreservesShapeAndDefaultStyle()
        {
            var descriptor = new LocalizedTextDescriptor("UI", "ui.test.message");

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.test.message"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Body));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Default));
            Assert.That(descriptor.Arguments, Is.Empty);
        }

        [Test]
        public void LocalizedTextDescriptor_UsesDeterministicEquality()
        {
            var left = new LocalizedTextDescriptor(
                "UI",
                "ui.test.message",
                LocalizedTextRole.Button,
                LocalizedTextWeight.Bold,
                new object[] { "A", 7 });
            var right = new LocalizedTextDescriptor(
                "UI",
                "ui.test.message",
                LocalizedTextRole.Button,
                LocalizedTextWeight.Bold,
                new object[] { "A", 7 });

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left == right, Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void LocalizedTextDescriptor_CopiesDynamicArguments()
        {
            var arguments = new object[] { 50 };
            var descriptor = new LocalizedTextDescriptor(
                "UI",
                "ui.settings.audio.volume_value",
                arguments: arguments);

            arguments[0] = 75;

            Assert.That(descriptor.Arguments.Count, Is.EqualTo(1));
            Assert.That(descriptor.Arguments[0], Is.EqualTo(50));
        }

        [Test]
        public void SettingsDisplayResolutionDynamicDescriptor_UsesUiSmartStringArgument()
        {
            var descriptor = SettingsDynamicTextDescriptors.DisplayResolutionValue("1920 x 1080");

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.display.resolution_value"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Label));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.EqualTo(new object[] { "1920 x 1080" }));
        }

        [Test]
        public void SettingsDisplayPreviewCountdownDynamicDescriptor_UsesUiSmartStringArgument()
        {
            var descriptor = SettingsDynamicTextDescriptors.DisplayPreviewCountdown(10);

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.display.preview_countdown"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Label));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.EqualTo(new object[] { 10 }));
        }

        [Test]
        public void SettingsDisplayPreviewStatusDynamicDescriptor_UsesUiSmartStringArgument()
        {
            var descriptor = SettingsDynamicTextDescriptors.DisplayPreviewActiveStatus(10);

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.display.status.preview_active"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Body));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.EqualTo(new object[] { 10 }));
        }

        [TestCase(
            true,
            "ui.settings.display.preview_confirm.fullscreen_body")]
        [TestCase(
            false,
            "ui.settings.display.preview_confirm.windowed_body")]
        public void SettingsDisplayPreviewConfirmationDynamicDescriptor_UsesTypedDisplayArguments(
            bool isFullscreen,
            string expectedKey)
        {
            var descriptor = SettingsDynamicTextDescriptors.DisplayPreviewConfirmBody(
                1920,
                1080,
                isFullscreen,
                15);

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo(expectedKey));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Body));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.EqualTo(new object[] { 1920, 1080, 15 }));
        }

        [Test]
        public void ConfirmPopupPresenter_LocalizedPayloadRefreshesOnLocaleChangeAndStopsAfterDispose()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var presenter = new ConfirmPopupPresenter(resolver);
            var payload = new ConfirmPopupPayload(
                SettingsStaticTextDescriptors.InputResetConfirmTitle,
                SettingsStaticTextDescriptors.InputResetConfirmBody,
                SettingsStaticTextDescriptors.InputResetConfirmLabel,
                SettingsStaticTextDescriptors.Cancel,
                false);

            presenter.Apply(payload);

            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo("Reset Input Settings"));
            Assert.That(presenter.ViewModel.BodyText, Is.EqualTo("Reset input settings to defaults?"));
            Assert.That(presenter.ViewModel.ConfirmLabel, Is.EqualTo("Reset"));
            Assert.That(presenter.ViewModel.CancelLabel, Is.EqualTo("Cancel"));

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo("입력 설정 초기화"));
            Assert.That(presenter.ViewModel.BodyText, Is.EqualTo("입력 설정 초기화 확인"));
            Assert.That(presenter.ViewModel.ConfirmLabel, Is.EqualTo("초기화"));
            Assert.That(presenter.ViewModel.CancelLabel, Is.EqualTo("취소"));

            presenter.Dispose();
            resolver.SetLocale(PackageFreeLocalizedTextResolver.DefaultLocaleCode);

            Assert.That(
                presenter.ViewModel.TitleText,
                Is.EqualTo("입력 설정 초기화"),
                "Disposed confirmation presenters must stop receiving locale refreshes.");
        }

        [Test]
        public void SettingsInputRebindCanceledDynamicDescriptor_UsesUiSmartStringKeyWithoutRuntimeKeyName()
        {
            var descriptor = SettingsDynamicTextDescriptors.InputRebindCanceled();

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.input.rebind_canceled"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Label));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.Empty);
        }

        [Test]
        public void SettingsInputResetCompleteDynamicDescriptor_UsesUiKeyWithoutArguments()
        {
            var descriptor = SettingsDynamicTextDescriptors.InputResetComplete();

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.input.reset_complete"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Label));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.Empty);
        }

        [Test]
        public void SettingsInputReservedKeyDynamicDescriptor_UsesUiKeyWithoutArguments()
        {
            var descriptor = SettingsDynamicTextDescriptors.InputReservedKey();

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.input.reserved_key"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Label));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.Empty);
        }

        [Test]
        public void SettingsInputMovementConflictDynamicDescriptor_UsesUiKeyWithoutArguments()
        {
            var descriptor = SettingsDynamicTextDescriptors.InputMovementConflict();

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.input.movement_conflict"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Label));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.Empty);
        }

        [Test]
        public void SettingsInputAlreadyRebindingDynamicDescriptor_UsesUiKeyWithoutArguments()
        {
            var descriptor = SettingsDynamicTextDescriptors.InputAlreadyRebinding();

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.input.already_rebinding"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Label));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.Empty);
        }

        [Test]
        public void SettingsInputActionConflictDynamicDescriptor_UsesNestedActionDescriptor()
        {
            var descriptor = SettingsDynamicTextDescriptors.InputActionConflict(
                SettingsStaticTextDescriptors.Flip);

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.input.action_conflict"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Label));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments.Count, Is.EqualTo(1));
            Assert.That(descriptor.Arguments[0], Is.EqualTo(SettingsStaticTextDescriptors.Flip));
        }

        [Test]
        public void SettingsInputActionConflictDynamicDescriptor_UnknownActionUsesGenericFallback()
        {
            var descriptor = SettingsDynamicTextDescriptors.InputActionConflict(default);

            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.input.unsupported_key"));
            Assert.That(descriptor.Arguments, Is.Empty);
        }

        [Test]
        public void SettingsInputUnsupportedKeyDynamicDescriptor_UsesUiKeyWithoutArguments()
        {
            var descriptor = SettingsDynamicTextDescriptors.InputUnsupportedKey();

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo("ui.settings.input.unsupported_key"));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Label));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.Empty);
        }

        [TestCase(
            KeyboardBindableAction.Push,
            "ui.settings.input.rebind_push_prompt")]
        [TestCase(
            KeyboardBindableAction.Flip,
            "ui.settings.input.rebind_flip_prompt")]
        public void SettingsInputRebindPromptDynamicDescriptor_UsesActionSpecificUiKeyWithoutArguments(
            KeyboardBindableAction action,
            string expectedKey)
        {
            var descriptor = SettingsDynamicTextDescriptors.InputRebindPrompt(action);

            Assert.That(descriptor.Table, Is.EqualTo("UI"));
            Assert.That(descriptor.Key, Is.EqualTo(expectedKey));
            Assert.That(descriptor.Role, Is.EqualTo(LocalizedTextRole.Label));
            Assert.That(descriptor.Weight, Is.EqualTo(LocalizedTextWeight.Regular));
            Assert.That(descriptor.Arguments, Is.Empty);
        }

        [Test]
        public void SettingsInputRebindPromptDynamicDescriptor_RejectsUnsupportedAction()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SettingsDynamicTextDescriptors.InputRebindPrompt((KeyboardBindableAction)999));
        }

        [Test]
        public void InvariantSettingsFallback_DefaultDisplayPresenter_FormatsPreviewCountdown()
        {
            var displayPort = new FakeDisplaySettingsPort();
            displayPort.SetPreviewState(1, DisplayWindowMode.FullScreenWindow);
            var presenter = new SettingsDisplayPresenter(displayPort);

            presenter.Apply(previewTimeoutSeconds: 15d);
            presenter.SetPreviewCountdown(new DisplayPreviewCountdownSnapshot(
                isActive: true,
                remainingSeconds: 7,
                totalSeconds: 15));

            Assert.That(presenter.ViewModel.PreviewCountdownText, Is.EqualTo("Reverting in 7s"));
            Assert.That(presenter.ViewModel.PreviewCountdownText, Does.Not.Contain("[UI:"));
            Assert.That(presenter.ViewModel.IsPreviewCountdownVisible, Is.True);
        }

        [TestCase(
            KeyboardBindingValidationResult.ReservedKey,
            "This key cannot be used.")]
        [TestCase(
            KeyboardBindingValidationResult.MovementConflict,
            "Movement keys cannot overlap.")]
        [TestCase(
            KeyboardBindingValidationResult.AlreadyRebinding,
            "Another key is already being reassigned.")]
        public void InvariantSettingsFallback_DefaultInputPresenter_ResolvesValidationStatus(
            KeyboardBindingValidationResult validationResult,
            string expectedStatus)
        {
            var statusText = ResolveDefaultInputValidationStatus(validationResult);

            Assert.That(statusText, Is.EqualTo(expectedStatus));
            Assert.That(statusText, Does.Not.Contain("[UI:"));
        }

        [TestCase(
            KeyboardBindableAction.Push,
            "Press a key for Push...")]
        [TestCase(
            KeyboardBindableAction.Flip,
            "Press a key for Flip...")]
        public void InvariantSettingsFallback_DefaultInputPresenter_ResolvesRebindPrompt(
            KeyboardBindableAction action,
            string expectedStatus)
        {
            var presenter = new SettingsInputPresenter(
                new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.Success));
            presenter.Apply(new SettingsInputPresenterInput(
                SettingsStaticTextDescriptors.MovementKeys,
                SettingsStaticTextDescriptors.UseArrowKeys,
                SettingsStaticTextDescriptors.Push,
                SettingsStaticTextDescriptors.Flip,
                SettingsStaticTextDescriptors.Change,
                SettingsStaticTextDescriptors.ResetInput));

            presenter.StartRebind(action);

            Assert.That(presenter.ViewModel.StatusText, Is.EqualTo(expectedStatus));
            Assert.That(presenter.ViewModel.StatusText, Does.Not.Contain("[UI:"));
        }

        [Test]
        public void InvariantSettingsFallback_DefaultInputPresenter_PreservesUnknownDiagnosticFallback()
        {
            var presenter = new SettingsInputPresenter(
                new RejectingKeyboardSettingsPort(KeyboardBindingValidationResult.Success));
            presenter.Apply(new SettingsInputPresenterInput(
                new LocalizedTextDescriptor("UI", "ui.settings.input.unknown"),
                SettingsStaticTextDescriptors.UseArrowKeys,
                SettingsStaticTextDescriptors.Push,
                SettingsStaticTextDescriptors.Flip,
                SettingsStaticTextDescriptors.Change,
                SettingsStaticTextDescriptors.ResetInput));

            Assert.That(
                presenter.ViewModel.MovementLabel,
                Is.EqualTo("[UI:ui.settings.input.unknown]"));
        }

        [Test]
        public void PackageFreeResolver_ResolvesSelectedDisplayDynamicFixtureKey()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();

            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayResolutionValue("1920 x 1080")),
                Is.EqualTo("1920 x 1080"));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayPreviewCountdown(10)),
                Is.EqualTo("Reverting in 10s"));

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayResolutionValue("1920 x 1080")),
                Is.EqualTo("1920 x 1080"));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayPreviewCountdown(10)),
                Is.EqualTo("10초 후 되돌림"));
        }

        [Test]
        public void PackageFreeResolver_ResolvesSelectedInputStatusFixtureKey()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();

            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindCanceled()),
                Is.EqualTo("Key reassignment cancelled."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputResetComplete()),
                Is.EqualTo("Input settings reset."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputReservedKey()),
                Is.EqualTo("This key cannot be used."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputMovementConflict()),
                Is.EqualTo("Movement keys cannot overlap."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputAlreadyRebinding()),
                Is.EqualTo("Another key is already being reassigned."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputActionConflict(
                    SettingsStaticTextDescriptors.Flip)),
                Is.EqualTo("This key is already used by Flip."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputUnsupportedKey()),
                Is.EqualTo("This key cannot be used."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindPrompt(KeyboardBindableAction.Push)),
                Is.EqualTo("Press a key for Push..."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindPrompt(KeyboardBindableAction.Flip)),
                Is.EqualTo("Press a key for Flip..."));

            resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);

            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindCanceled()),
                Is.EqualTo("키 재지정을 취소했습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputResetComplete()),
                Is.EqualTo("입력 설정이 초기화되었습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputReservedKey()),
                Is.EqualTo("이 키는 사용할 수 없습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputMovementConflict()),
                Is.EqualTo("이동 키는 서로 중복될 수 없습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputAlreadyRebinding()),
                Is.EqualTo("다른 키를 이미 재지정하고 있습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputActionConflict(
                    SettingsStaticTextDescriptors.Flip)),
                Is.EqualTo("이 키는 이미 뒤집기에 사용 중입니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputUnsupportedKey()),
                Is.EqualTo("이 키는 사용할 수 없습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindPrompt(KeyboardBindableAction.Push)),
                Is.EqualTo("밀기 키 입력하세요..."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindPrompt(KeyboardBindableAction.Flip)),
                Is.EqualTo("뒤집기 키 입력하세요..."));
        }

        [Test]
        public void SettingsScreenPayload_Default_ProvidesStaticShellDescriptors()
        {
            var descriptors = GetSettingsPayloadDescriptors(SettingsScreenPayload.Default);

            Assert.That(descriptors, Is.EqualTo(ExpectedSettingsDescriptors));
            Assert.That(
                descriptors.Select(descriptor => descriptor.Key).ToArray(),
                Is.EqualTo(new[]
                {
                    "ui.settings.title",
                    "ui.settings.audio",
                    "ui.settings.display",
                    "ui.settings.input",
                    "ui.settings.input.movement_keys",
                    "ui.settings.input.use_arrow_keys",
                    "ui.settings.input.push",
                    "ui.settings.input.flip",
                    "ui.settings.input.change",
                    "ui.settings.input.reset_input",
                    "ui.settings.language",
                    "ui.settings.language.english",
                    "ui.settings.language.korean",
                    "ui.settings.audio.main",
                    "ui.settings.audio.bgm",
                    "ui.settings.audio.sfx",
                    "ui.settings.audio.mute",
                    "ui.settings.display.current",
                    "ui.settings.display.resolution",
                    "ui.settings.display.resolution_hint",
                    "ui.settings.display.fullscreen_window",
                    "ui.settings.display.fullscreen_on",
                    "ui.settings.display.apply",
                    "ui.settings.display.revert",
                    "ui.common.back",
                }));
        }

        [Test]
        public void PausePopupPayload_Default_ProvidesStaticShellDescriptors()
        {
            var descriptors = GetPausePayloadDescriptors(PausePopupPayload.Default);

            Assert.That(descriptors, Is.EqualTo(ExpectedPauseDescriptors));
            Assert.That(
                descriptors.Select(descriptor => descriptor.Key).ToArray(),
                Is.EqualTo(new[]
                {
                    "ui.pause.title",
                    "ui.pause.description",
                    "ui.pause.resume",
                    "ui.common.settings",
                    "ui.pause.retry",
                    "ui.pause.main_menu",
                }));
            Assert.That(
                typeof(PausePopupPayload)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.PropertyType == typeof(string))
                    .Select(property => property.Name)
                    .ToArray(),
                Is.Empty);
        }

        [Test]
        public void MainMenuStaticTextPayload_Default_ProvidesCommandShellDescriptors()
        {
            var descriptors = GetMainMenuPayloadDescriptors(MainMenuStaticTextPayload.Default);

            Assert.That(descriptors, Is.EqualTo(ExpectedMainMenuDescriptors));
            Assert.That(
                descriptors.Select(descriptor => descriptor.Key).ToArray(),
                Is.EqualTo(new[]
                {
                    "ui.main_menu.start",
                    "ui.common.settings",
                    "ui.main_menu.quit",
                }));
        }

        [Test]
        public void PausePopupPresenter_MapsStaticDescriptorsThroughResolver()
        {
            var resolver = new FakeLocalizedTextResolver();
            resolver.SetLocale("ko-KR");
            var presenter = new PausePopupPresenter(resolver);

            presenter.Apply(PausePopupPayload.Default);

            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo("일시 정지"));
            Assert.That(presenter.ViewModel.DescriptionText, Is.EqualTo("일시 정지 팝업"));
            Assert.That(presenter.ViewModel.ResumeLabel, Is.EqualTo("계속하기"));
            Assert.That(presenter.ViewModel.SettingsLabel, Is.EqualTo("설정"));
            Assert.That(presenter.ViewModel.RetryLabel, Is.EqualTo("다시 시도"));
            Assert.That(presenter.ViewModel.MainMenuLabel, Is.EqualTo("메인 메뉴"));
        }

        [Test]
        public void FakeResolver_ResolvesSettingsTitleByLocale()
        {
            var resolver = new FakeLocalizedTextResolver();

            resolver.SetLocale("en-US");
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.TitleTextDescriptor), Is.EqualTo("Settings"));

            resolver.SetLocale("ko-KR");
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.TitleTextDescriptor), Is.EqualTo("설정"));
        }

        [Test]
        public void PackageFreeResolver_ResolvesSettingsStaticCatalogByLocale()
        {
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.TitleTextDescriptor), Is.EqualTo("Settings"));
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.LanguageLabelDescriptor), Is.EqualTo("Language"));
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.KoreanLanguageLabelDescriptor), Is.EqualTo("Korean"));
            Assert.That(resolver.Resolve(PausePopupPayload.Default.TitleTextDescriptor), Is.EqualTo("Paused"));
            Assert.That(resolver.Resolve(PausePopupPayload.Default.MainMenuLabelDescriptor), Is.EqualTo("Main Menu"));
            Assert.That(resolver.Resolve(MainMenuStaticTextPayload.Default.StartLabelDescriptor), Is.EqualTo("Start"));
            Assert.That(resolver.Resolve(MainMenuStaticTextPayload.Default.SettingsLabelDescriptor), Is.EqualTo("Settings"));
            Assert.That(resolver.Resolve(MainMenuStaticTextPayload.Default.QuitLabelDescriptor), Is.EqualTo("Quit"));

            resolver.SetLocale("ko-KR");

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.TitleTextDescriptor), Is.EqualTo("설정"));
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.ResetInputLabelDescriptor), Is.EqualTo("입력 초기화"));
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.LanguageLabelDescriptor), Is.EqualTo("언어"));
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.KoreanLanguageLabelDescriptor), Is.EqualTo("한국어"));
            Assert.That(resolver.Resolve(PausePopupPayload.Default.TitleTextDescriptor), Is.EqualTo("일시 정지"));
            Assert.That(resolver.Resolve(PausePopupPayload.Default.MainMenuLabelDescriptor), Is.EqualTo("메인 메뉴"));
            Assert.That(resolver.Resolve(MainMenuStaticTextPayload.Default.StartLabelDescriptor), Is.EqualTo("시작"));
            Assert.That(resolver.Resolve(MainMenuStaticTextPayload.Default.SettingsLabelDescriptor), Is.EqualTo("설정"));
            Assert.That(resolver.Resolve(MainMenuStaticTextPayload.Default.QuitLabelDescriptor), Is.EqualTo("종료"));
        }

        [Test]
        public void PackageFreeResolver_LocaleSelectionPort_SupportsOnlyEnglishAndKorean()
        {
            IUiLocaleSelectionPort localeSelectionPort = PackageFreeLocalizedTextResolver.CreateSettingsDefault();

            Assert.That(localeSelectionPort.AvailableLocaleCodes, Is.EqualTo(new[] { "en-US", "ko-KR" }));
            Assert.That(localeSelectionPort.CurrentLocaleCode, Is.EqualTo("en-US"));

            Assert.That(localeSelectionPort.TrySetLocale("ko-KR"), Is.True);
            Assert.That(localeSelectionPort.CurrentLocaleCode, Is.EqualTo("ko-KR"));

            Assert.That(localeSelectionPort.TrySetLocale("fr-FR"), Is.False);
            Assert.That(localeSelectionPort.CurrentLocaleCode, Is.EqualTo("ko-KR"));
        }

        [Test]
        public void PackageFreeResolver_LoadsPersistedLocaleOrDefaultsDeterministically()
        {
            Assert.That(
                PackageFreeLocalizedTextResolver.CreateSettingsDefault(new FakeUiLocalePreferenceStore()).CurrentLocaleCode,
                Is.EqualTo("en-US"));
            Assert.That(
                PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                    new FakeUiLocalePreferenceStore("ko-KR")).CurrentLocaleCode,
                Is.EqualTo("ko-KR"));
            Assert.That(
                PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                    new FakeUiLocalePreferenceStore("en-US")).CurrentLocaleCode,
                Is.EqualTo("en-US"));
            Assert.That(
                PackageFreeLocalizedTextResolver.CreateSettingsDefault(
                    new FakeUiLocalePreferenceStore("fr-FR")).CurrentLocaleCode,
                Is.EqualTo("en-US"));
        }

        [Test]
        public void PackageFreeResolver_SavesSuccessfulLocaleSelectionAndRestoresInNewResolver()
        {
            var store = new FakeUiLocalePreferenceStore();
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault(store);

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);
            Assert.That(store.SaveCallCount, Is.EqualTo(1));
            Assert.That(store.LastSavedLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(
                PackageFreeLocalizedTextResolver.CreateSettingsDefault(store).CurrentLocaleCode,
                Is.EqualTo("ko-KR"));

            Assert.That(resolver.TrySetLocale("fr-FR"), Is.False);
            Assert.That(store.SaveCallCount, Is.EqualTo(1));

            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);
            Assert.That(store.SaveCallCount, Is.EqualTo(1));

            Assert.That(resolver.TrySetLocale("en-US"), Is.True);
            Assert.That(store.SaveCallCount, Is.EqualTo(2));
            Assert.That(
                PackageFreeLocalizedTextResolver.CreateSettingsDefault(store).CurrentLocaleCode,
                Is.EqualTo("en-US"));
        }

        [Test]
        public void FakeResolver_LocaleChanged_AllowsConsumerRefresh()
        {
            var resolver = new FakeLocalizedTextResolver();
            var refreshedValues = new List<string>();
            var eventCount = 0;

            resolver.LocaleChanged += () =>
            {
                eventCount++;
                refreshedValues.Add(resolver.Resolve(SettingsScreenPayload.Default.BackLabelDescriptor));
            };

            resolver.SetLocale("ko-KR");

            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(refreshedValues, Is.EqualTo(new[] { "뒤로" }));
        }

        [Test]
        public void SettingsPresenter_MapsStaticDescriptorsThroughResolver()
        {
            var resolver = new FakeLocalizedTextResolver();
            resolver.SetLocale("ko-KR");
            var presenter = new SettingsScreenPresenter(
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort(),
                NoOpKeyboardBindingSettingsPort.Instance,
                resolver);

            presenter.Apply(SettingsScreenPayload.Default, previewTimeoutSeconds: 15d);

            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo("설정"));
            Assert.That(presenter.ViewModel.AudioTabLabel, Is.EqualTo("오디오"));
            Assert.That(presenter.ViewModel.DisplayTabLabel, Is.EqualTo("디스플레이"));
            Assert.That(presenter.ViewModel.InputTabLabel, Is.EqualTo("입력"));
            Assert.That(presenter.ViewModel.BackLabel, Is.EqualTo("뒤로"));
            Assert.That(presenter.DisplayPresenter.ViewModel.LanguageLabelText, Is.EqualTo("언어"));
            Assert.That(presenter.DisplayPresenter.ViewModel.CurrentLanguageText, Is.EqualTo("한국어"));
            Assert.That(presenter.InputPresenter.ViewModel.MovementLabel, Is.EqualTo("이동 키"));
            Assert.That(presenter.InputPresenter.ViewModel.UseArrowKeysLabel, Is.EqualTo("화살표 키 사용"));
            Assert.That(presenter.InputPresenter.ViewModel.PushLabel, Is.EqualTo("밀기"));
            Assert.That(presenter.InputPresenter.ViewModel.FlipLabel, Is.EqualTo("뒤집기"));
            Assert.That(presenter.InputPresenter.ViewModel.PushChangeLabel, Is.EqualTo("변경"));
            Assert.That(presenter.InputPresenter.ViewModel.FlipChangeLabel, Is.EqualTo("변경"));
            Assert.That(presenter.InputPresenter.ViewModel.ResetLabel, Is.EqualTo("입력 초기화"));
        }

        [Test]
        public void LocalizedTmpTextBinding_AppliesResolvedTextAndTypography()
        {
            var resolver = new FakeLocalizedTextResolver();
            var typographyResolver = new RecordingTypographyResolver
            {
                Style = new LocalizedTypographyStyle(41f, 2f, true),
            };
            var label = CreateTmpText("localized-label");

            try
            {
                using var binding = new LocalizedTmpTextBinding(
                    label,
                    SettingsStaticTextDescriptors.Title,
                    resolver,
                    typographyResolver);

                Assert.That(label.text, Is.EqualTo("Settings"));
                Assert.That(label.fontSize, Is.EqualTo(41f));
                Assert.That(label.lineSpacing, Is.EqualTo(2f));
                Assert.That((label.fontStyle & FontStyles.Bold) == FontStyles.Bold, Is.True);
                Assert.That(typographyResolver.Calls, Has.Count.EqualTo(1));
                Assert.That(typographyResolver.Calls[0].LocaleCode, Is.EqualTo("en-US"));
                Assert.That(typographyResolver.Calls[0].Role, Is.EqualTo(LocalizedTextRole.Title));
                Assert.That(typographyResolver.Calls[0].Weight, Is.EqualTo(LocalizedTextWeight.Bold));
            }
            finally
            {
                DestroyText(label);
            }
        }

        [Test]
        public void LocalizedTmpTextBinding_RefreshesOnLocaleChanged_AndStopsAfterDispose()
        {
            var resolver = new FakeLocalizedTextResolver();
            var typographyResolver = new RecordingTypographyResolver();
            var label = CreateTmpText("settings-title");
            var binding = new LocalizedTmpTextBinding(
                label,
                SettingsStaticTextDescriptors.Title,
                resolver,
                typographyResolver);

            try
            {
                Assert.That(label.text, Is.EqualTo("Settings"));
                Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(1));

                resolver.SetLocale("ko-KR");

                Assert.That(label.text, Is.EqualTo("설정"));
                Assert.That(typographyResolver.Calls.Last().LocaleCode, Is.EqualTo("ko-KR"));

                binding.Dispose();
                Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(0));

                resolver.SetLocale("en-US");

                Assert.That(label.text, Is.EqualTo("설정"));
            }
            finally
            {
                binding.Dispose();
                DestroyText(label);
            }
        }

        [Test]
        public void SettingsView_StaticLabels_CanBindDescriptorsAndRefreshLocaleWithoutDynamicStrings()
        {
            var resolver = new FakeLocalizedTextResolver();
            var typographyResolver = new RecordingTypographyResolver
            {
                Style = new LocalizedTypographyStyle(32f, 1f, true),
            };
            var fixture = CreateSettingsViewFixture();

            try
            {
                fixture.View.BindStaticLocalization(
                    SettingsScreenPayload.Default,
                    resolver,
                    typographyResolver,
                    null);

                fixture.ScreenViewModel.SetContent(
                    "MODEL Title",
                    "MODEL Back",
                    "MODEL Audio",
                    "MODEL Display",
                    "MODEL Input",
                    SettingsSectionId.Input);
                fixture.View.Bind(fixture.ScreenViewModel);
                fixture.InputView.Bind(fixture.InputViewModel);
                fixture.InputViewModel.SetContent(
                    "MODEL Movement",
                    "MODEL Arrows",
                    false,
                    "WASD",
                    "MODEL Push",
                    "P",
                    "MODEL Change",
                    "MODEL Flip",
                    "F",
                    "MODEL Change",
                    "MODEL Reset",
                    "Waiting for key",
                    false,
                    null,
                    true);

                Assert.That(fixture.TitleLabel.text, Is.EqualTo("Settings"));
                Assert.That(fixture.AudioTabLabel.text, Is.EqualTo("Audio"));
                Assert.That(fixture.DisplayTabLabel.text, Is.EqualTo("Display"));
                Assert.That(fixture.InputTabLabel.text, Is.EqualTo("Input"));
                Assert.That(fixture.BackLabel.text, Is.EqualTo("Back"));
                Assert.That(fixture.MovementLabel.text, Is.EqualTo("Movement Keys"));
                Assert.That(fixture.PushChangeLabel.text, Is.EqualTo("Change"));
                Assert.That(fixture.MovementCurrentText.text, Is.EqualTo("WASD"));
                Assert.That(fixture.StatusText.text, Is.EqualTo("Waiting for key"));

                resolver.SetLocale("ko-KR");

                Assert.That(fixture.TitleLabel.text, Is.EqualTo("설정"));
                Assert.That(fixture.AudioTabLabel.text, Is.EqualTo("오디오"));
                Assert.That(fixture.MovementLabel.text, Is.EqualTo("이동 키"));
                Assert.That(fixture.PushChangeLabel.text, Is.EqualTo("변경"));
                Assert.That(fixture.MovementCurrentText.text, Is.EqualTo("WASD"));
                Assert.That(fixture.StatusText.text, Is.EqualTo("Waiting for key"));
                Assert.That(
                    typographyResolver.Calls.Any(call =>
                        call.Role == LocalizedTextRole.Title &&
                        call.Weight == LocalizedTextWeight.Bold),
                    Is.True);
                Assert.That(
                    typographyResolver.Calls.Any(call =>
                        call.Role == LocalizedTextRole.Label &&
                        call.Weight == LocalizedTextWeight.Regular),
                    Is.True);

                fixture.View.UnbindStaticLocalization();
                Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(0));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void MainMenuView_StaticCommandLabels_CanBindDescriptorsAndRefreshLocale()
        {
            var resolver = new FakeLocalizedTextResolver();
            var typographyResolver = new RecordingTypographyResolver
            {
                Style = new LocalizedTypographyStyle(24f, 0f, false),
            };
            var fixture = CreateMainMenuViewFixture();

            try
            {
                fixture.View.BindStaticLocalization(
                    MainMenuStaticTextPayload.Default,
                    resolver,
                    typographyResolver);

                Assert.That(fixture.StartLabel.text, Is.EqualTo("Start"));
                Assert.That(fixture.SettingsLabel.text, Is.EqualTo("Settings"));
                Assert.That(fixture.QuitLabel.text, Is.EqualTo("Quit"));

                resolver.SetLocale("ko-KR");

                Assert.That(fixture.StartLabel.text, Is.EqualTo("시작"));
                Assert.That(fixture.SettingsLabel.text, Is.EqualTo("설정"));
                Assert.That(fixture.QuitLabel.text, Is.EqualTo("종료"));
                Assert.That(
                    typographyResolver.Calls.All(call => call.Role == LocalizedTextRole.Button),
                    Is.True);

                fixture.View.UnbindStaticLocalization();
                Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(0));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void DynamicSettingsStrings_RemainOutsideStaticDescriptorMap()
        {
            var descriptors = GetSettingsPayloadDescriptors(SettingsScreenPayload.Default);
            var descriptorKeys = descriptors.Select(descriptor => descriptor.Key).ToArray();

            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.audio.percent"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.audio.muted"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.audio.volume_value"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.audio.volume_value_muted"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.display.resolution_value"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.display.preview_countdown"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.input.rebind_status"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.input.rebind_error"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.input.rebind_canceled"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.input.reset_complete"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.input.reserved_key"));
            Assert.That(descriptorKeys, Does.Contain("ui.settings.language"));
            Assert.That(descriptorKeys, Does.Contain("ui.settings.language.english"));
            Assert.That(descriptorKeys, Does.Contain("ui.settings.language.korean"));
            Assert.That(typeof(AudioSettingsRowViewModel).GetProperty(nameof(AudioSettingsRowViewModel.ValueText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsDisplayViewModel).GetProperty(nameof(SettingsDisplayViewModel.DisplayStatusText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsDisplayViewModel).GetProperty(nameof(SettingsDisplayViewModel.PreviewCountdownText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsInputViewModel).GetProperty(nameof(SettingsInputViewModel.MovementCurrentText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsInputViewModel).GetProperty(nameof(SettingsInputViewModel.PushCurrentText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsInputViewModel).GetProperty(nameof(SettingsInputViewModel.FlipCurrentText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsInputViewModel).GetProperty(nameof(SettingsInputViewModel.StatusText))?.PropertyType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void PausePopupStaticMigration_DoesNotReviveDeferredStaticSurfaces()
        {
            var descriptors = GetPausePayloadDescriptors(PausePopupPayload.Default);
            var descriptorKeys = descriptors.Select(descriptor => descriptor.Key).ToArray();
            var stageResultPayloadProperties = typeof(StageResultScreenPayload)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();
            var stagePresentationProperties = typeof(StagePresentationDefinition)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(property => property.Name)
                .ToArray();

            Assert.That(descriptorKeys, Does.Not.Contain("ui.stage.display_name"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.hud.objective"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.display.preview_countdown"));
            Assert.That(stageResultPayloadProperties, Does.Not.Contain("TitleText"));
            Assert.That(stageResultPayloadProperties, Does.Not.Contain("DetailText"));
            Assert.That(stageResultPayloadProperties, Does.Not.Contain("ContinueLabel"));
            Assert.That(stagePresentationProperties, Does.Contain("DisplayNameKey"));
        }

        [Test]
        public void MainMenuStaticMigration_DoesNotLocalizeSaveSlotConfirmOrDeferredSurfaces()
        {
            var descriptors = GetMainMenuPayloadDescriptors(MainMenuStaticTextPayload.Default);
            var descriptorKeys = descriptors.Select(descriptor => descriptor.Key).ToArray();
            var saveSlotStringProperties = typeof(SaveSlotCardViewModel)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.PropertyType == typeof(string))
                .Select(property => property.Name)
                .ToArray();

            Assert.That(descriptorKeys, Does.Contain("ui.main_menu.start"));
            Assert.That(descriptorKeys, Does.Contain("ui.common.settings"));
            Assert.That(descriptorKeys, Does.Contain("ui.main_menu.quit"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.main_menu.slot.title"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.main_menu.slot.stage"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.confirm.title"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.stage.display_name"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.hud.objective"));
            Assert.That(saveSlotStringProperties, Does.Contain(nameof(SaveSlotCardViewModel.TitleText)));
            Assert.That(saveSlotStringProperties, Does.Contain(nameof(SaveSlotCardViewModel.StageText)));
            Assert.That(saveSlotStringProperties, Does.Contain(nameof(SaveSlotCardViewModel.LastPlayedText)));
            Assert.That(saveSlotStringProperties, Does.Contain(nameof(SaveSlotCardViewModel.PrimaryActionText)));
        }

        [Test]
        public void SettingsInputPresenter_LocalizesOnlySelectedCanceledStatusAndKeepsKeyDisplayNamesRaw()
        {
            var resolver = new FakeLocalizedTextResolver();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.Canceled);
            var presenter = new SettingsScreenPresenter(
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort(),
                keyboardPort,
                resolver,
                resolver);

            presenter.Apply(SettingsScreenPayload.Default, previewTimeoutSeconds: 15d);
            presenter.InputPresenter.StartRebind(KeyboardBindableAction.Push);

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("Press a key for Push..."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));

            keyboardPort.Complete();

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("Key reassignment cancelled."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));

            resolver.SetLocale("ko-KR");
            presenter.RefreshLocalization();

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("키 재지정을 취소했습니다."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));
            Assert.That(presenter.InputPresenter.ViewModel.FlipCurrentText, Is.EqualTo("K"));
        }

        [TestCase(
            KeyboardBindableAction.Push,
            "ui.settings.input.rebind_push_prompt",
            "Press a key for Push...",
            "밀기 키 입력하세요...")]
        [TestCase(
            KeyboardBindableAction.Flip,
            "ui.settings.input.rebind_flip_prompt",
            "Press a key for Flip...",
            "뒤집기 키 입력하세요...")]
        public void SettingsInputPresenter_ActiveRebindRetainsDescriptorAndReResolvesCurrentLocale(
            KeyboardBindableAction action,
            string expectedKey,
            string englishPrompt,
            string koreanPrompt)
        {
            var resolver = new FakeLocalizedTextResolver();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.Success);
            var presenter = new SettingsInputPresenter(keyboardPort, resolver);
            presenter.Apply(new SettingsInputPresenterInput(
                SettingsStaticTextDescriptors.MovementKeys,
                SettingsStaticTextDescriptors.UseArrowKeys,
                SettingsStaticTextDescriptors.Push,
                SettingsStaticTextDescriptors.Flip,
                SettingsStaticTextDescriptors.Change,
                SettingsStaticTextDescriptors.ResetInput));

            presenter.StartRebind(action);

            var descriptorField = typeof(SettingsInputPresenter).GetField(
                "_statusTextDescriptor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(descriptorField, Is.Not.Null);
            var descriptor = (LocalizedTextDescriptor)descriptorField.GetValue(presenter);

            Assert.That(descriptor.Key, Is.EqualTo(expectedKey));
            Assert.That(descriptor.Arguments, Is.Empty);
            Assert.That(presenter.ViewModel.StatusText, Is.EqualTo(englishPrompt));

            resolver.SetLocale("ko-KR");
            presenter.RefreshLocalization();

            Assert.That(presenter.ViewModel.StatusText, Is.EqualTo(koreanPrompt));

            resolver.SetLocale("en-US");
            presenter.RefreshLocalization();

            Assert.That(presenter.ViewModel.StatusText, Is.EqualTo(englishPrompt));
        }

        [Test]
        public void SettingsInputPresenter_LocalizesSelectedResetCompleteStatusAndKeepsKeyDisplayNamesRaw()
        {
            var resolver = new FakeLocalizedTextResolver();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.Success);
            var presenter = new SettingsScreenPresenter(
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort(),
                keyboardPort,
                resolver,
                resolver);

            presenter.Apply(SettingsScreenPayload.Default, previewTimeoutSeconds: 15d);
            presenter.InputPresenter.ResetToDefaults();

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("Input settings reset."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));

            resolver.SetLocale("ko-KR");
            presenter.RefreshLocalization();

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("입력 설정이 초기화되었습니다."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));
            Assert.That(presenter.InputPresenter.ViewModel.FlipCurrentText, Is.EqualTo("K"));
        }

        [Test]
        public void SettingsInputPresenter_LocalizesSelectedReservedKeyStatusAndKeepsKeyDisplayNamesRaw()
        {
            var resolver = new FakeLocalizedTextResolver();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.ReservedKey);
            var presenter = new SettingsScreenPresenter(
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort(),
                keyboardPort,
                resolver,
                resolver);

            presenter.Apply(SettingsScreenPayload.Default, previewTimeoutSeconds: 15d);
            presenter.InputPresenter.StartRebind(KeyboardBindableAction.Push);
            keyboardPort.Complete();

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("This key cannot be used."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));

            resolver.SetLocale("ko-KR");
            presenter.RefreshLocalization();

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("이 키는 사용할 수 없습니다."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));
            Assert.That(presenter.InputPresenter.ViewModel.FlipCurrentText, Is.EqualTo("K"));
        }

        [Test]
        public void SettingsInputPresenter_LocalizesSelectedMovementConflictStatusAndKeepsKeyDisplayNamesRaw()
        {
            var resolver = new FakeLocalizedTextResolver();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.MovementConflict);
            var presenter = new SettingsScreenPresenter(
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort(),
                keyboardPort,
                resolver,
                resolver);

            presenter.Apply(SettingsScreenPayload.Default, previewTimeoutSeconds: 15d);
            presenter.InputPresenter.StartRebind(KeyboardBindableAction.Push);
            keyboardPort.Complete();

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("Movement keys cannot overlap."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));

            resolver.SetLocale("ko-KR");
            presenter.RefreshLocalization();

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("이동 키는 서로 중복될 수 없습니다."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));
            Assert.That(presenter.InputPresenter.ViewModel.FlipCurrentText, Is.EqualTo("K"));
        }

        [Test]
        public void SettingsInputPresenter_LocalizesSelectedAlreadyRebindingStatusAndKeepsKeyDisplayNamesRaw()
        {
            var resolver = new FakeLocalizedTextResolver();
            var keyboardPort = new RejectingKeyboardSettingsPort(KeyboardBindingValidationResult.AlreadyRebinding);
            var presenter = new SettingsScreenPresenter(
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort(),
                keyboardPort,
                resolver,
                resolver);

            presenter.Apply(SettingsScreenPayload.Default, previewTimeoutSeconds: 15d);
            presenter.InputPresenter.StartRebind(KeyboardBindableAction.Push);

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("Another key is already being reassigned."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));

            resolver.SetLocale("ko-KR");
            presenter.RefreshLocalization();

            Assert.That(presenter.InputPresenter.ViewModel.StatusText, Is.EqualTo("다른 키를 이미 재지정하고 있습니다."));
            Assert.That(presenter.InputPresenter.ViewModel.PushCurrentText, Is.EqualTo("J"));
            Assert.That(presenter.InputPresenter.ViewModel.FlipCurrentText, Is.EqualTo("K"));
        }

        [Test]
        public void SettingsInputView_SelectedResetCompleteStatusRefreshesThroughBoundView()
        {
            var resolver = new FakeLocalizedTextResolver();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.Success);
            var presenter = new SettingsScreenPresenter(
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort(),
                keyboardPort,
                resolver,
                resolver);
            var fixture = CreateSettingsViewFixture();

            try
            {
                presenter.Apply(SettingsScreenPayload.Default, previewTimeoutSeconds: 15d);
                fixture.View.Bind(presenter.ViewModel);
                fixture.InputView.Bind(presenter.InputPresenter.ViewModel);
                fixture.View.SetIsCurrent(true);

                presenter.InputPresenter.ResetToDefaults();

                Assert.That(fixture.InputView.StatusText, Is.EqualTo("Input settings reset."));
                Assert.That(fixture.MovementCurrentText.text, Is.EqualTo("WASD"));

                resolver.SetLocale("ko-KR");
                presenter.RefreshLocalization();

                Assert.That(fixture.InputView.StatusText, Is.EqualTo("입력 설정이 초기화되었습니다."));
                Assert.That(fixture.MovementCurrentText.text, Is.EqualTo("WASD"));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void SettingsInputView_SelectedReservedKeyStatusRefreshesThroughBoundView()
        {
            var resolver = new FakeLocalizedTextResolver();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.ReservedKey);
            var presenter = new SettingsScreenPresenter(
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort(),
                keyboardPort,
                resolver,
                resolver);
            var fixture = CreateSettingsViewFixture();

            try
            {
                presenter.Apply(SettingsScreenPayload.Default, previewTimeoutSeconds: 15d);
                fixture.View.Bind(presenter.ViewModel);
                fixture.InputView.Bind(presenter.InputPresenter.ViewModel);
                fixture.View.SetIsCurrent(true);

                presenter.InputPresenter.StartRebind(KeyboardBindableAction.Push);
                keyboardPort.Complete();

                Assert.That(fixture.InputView.StatusText, Is.EqualTo("This key cannot be used."));
                Assert.That(fixture.MovementCurrentText.text, Is.EqualTo("WASD"));

                resolver.SetLocale("ko-KR");
                presenter.RefreshLocalization();

                Assert.That(fixture.InputView.StatusText, Is.EqualTo("이 키는 사용할 수 없습니다."));
                Assert.That(fixture.MovementCurrentText.text, Is.EqualTo("WASD"));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void SettingsInputView_SelectedMovementConflictStatusRefreshesThroughBoundView()
        {
            var resolver = new FakeLocalizedTextResolver();
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.MovementConflict);
            var presenter = new SettingsScreenPresenter(
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort(),
                keyboardPort,
                resolver,
                resolver);
            var fixture = CreateSettingsViewFixture();

            try
            {
                presenter.Apply(SettingsScreenPayload.Default, previewTimeoutSeconds: 15d);
                fixture.View.Bind(presenter.ViewModel);
                fixture.InputView.Bind(presenter.InputPresenter.ViewModel);
                fixture.View.SetIsCurrent(true);

                presenter.InputPresenter.StartRebind(KeyboardBindableAction.Push);
                keyboardPort.Complete();

                Assert.That(fixture.InputView.StatusText, Is.EqualTo("Movement keys cannot overlap."));
                Assert.That(fixture.MovementCurrentText.text, Is.EqualTo("WASD"));

                resolver.SetLocale("ko-KR");
                presenter.RefreshLocalization();

                Assert.That(fixture.InputView.StatusText, Is.EqualTo("이동 키는 서로 중복될 수 없습니다."));
                Assert.That(fixture.MovementCurrentText.text, Is.EqualTo("WASD"));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void SettingsInputView_SelectedAlreadyRebindingStatusRefreshesThroughBoundView()
        {
            var resolver = new FakeLocalizedTextResolver();
            var keyboardPort = new RejectingKeyboardSettingsPort(KeyboardBindingValidationResult.AlreadyRebinding);
            var presenter = new SettingsScreenPresenter(
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort(),
                keyboardPort,
                resolver,
                resolver);
            var fixture = CreateSettingsViewFixture();

            try
            {
                presenter.Apply(SettingsScreenPayload.Default, previewTimeoutSeconds: 15d);
                fixture.View.Bind(presenter.ViewModel);
                fixture.InputView.Bind(presenter.InputPresenter.ViewModel);
                fixture.View.SetIsCurrent(true);

                presenter.InputPresenter.StartRebind(KeyboardBindableAction.Push);

                Assert.That(fixture.InputView.StatusText, Is.EqualTo("Another key is already being reassigned."));
                Assert.That(fixture.MovementCurrentText.text, Is.EqualTo("WASD"));
                Assert.That(fixture.PushCurrentText.text, Is.EqualTo("J"));

                resolver.SetLocale("ko-KR");
                presenter.RefreshLocalization();

                Assert.That(fixture.InputView.StatusText, Is.EqualTo("다른 키를 이미 재지정하고 있습니다."));
                Assert.That(fixture.MovementCurrentText.text, Is.EqualTo("WASD"));
                Assert.That(fixture.PushCurrentText.text, Is.EqualTo("J"));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void SettingsInputPolicy_LocalizesAllPlayerFacingStatusesAndKeepsKeyNamesInvariant()
        {
            var presenterSource = System.IO.File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/Settings/SettingsScreenPresenters.cs");
            var runtimeBuilderSource = System.IO.File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/SettingsScreenRuntimeBuilder.cs");
            var inputPresenterStart = presenterSource.IndexOf(
                "public sealed class SettingsInputPresenter",
                StringComparison.Ordinal);
            var inputPresenterEnd = presenterSource.IndexOf(
                "public sealed class SettingsScreenPresenter",
                inputPresenterStart,
                StringComparison.Ordinal);
            Assert.That(inputPresenterStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(inputPresenterEnd, Is.GreaterThan(inputPresenterStart));
            var inputPresenterSource = presenterSource.Substring(
                inputPresenterStart,
                inputPresenterEnd - inputPresenterStart);
            var startRebindStart = presenterSource.IndexOf(
                "public void StartRebind(KeyboardBindableAction action)",
                StringComparison.Ordinal);
            var startRebindEnd = presenterSource.IndexOf(
                "public void CancelRebind()",
                startRebindStart,
                StringComparison.Ordinal);
            Assert.That(startRebindStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(startRebindEnd, Is.GreaterThan(startRebindStart));
            var startRebindSource = presenterSource.Substring(
                startRebindStart,
                startRebindEnd - startRebindStart);
            Assert.That(presenterSource, Does.Contain("SettingsDynamicTextDescriptors.InputResetComplete()"));
            Assert.That(presenterSource, Does.Contain("SettingsDynamicTextDescriptors.InputReservedKey()"));
            Assert.That(presenterSource, Does.Contain("SettingsDynamicTextDescriptors.InputMovementConflict()"));
            Assert.That(presenterSource, Does.Contain("SettingsDynamicTextDescriptors.InputAlreadyRebinding()"));
            Assert.That(presenterSource, Does.Contain("SettingsDynamicTextDescriptors.InputActionConflict("));
            Assert.That(presenterSource, Does.Contain("SettingsDynamicTextDescriptors.InputUnsupportedKey()"));
            Assert.That(startRebindSource, Does.Contain("SettingsDynamicTextDescriptors.InputRebindPrompt(action)"));
            Assert.That(startRebindSource, Does.Not.Contain("SetRawStatus"));
            Assert.That(startRebindSource, Does.Not.Contain("Press a key for Push..."));
            Assert.That(startRebindSource, Does.Not.Contain("Press a key for Flip..."));
            Assert.That(inputPresenterSource, Does.Not.Contain("This key is already used by Flip."));
            Assert.That(inputPresenterSource, Does.Not.Contain("This key is already used by Push."));
            Assert.That(inputPresenterSource, Does.Not.Contain("\"This key cannot be used.\""));
            Assert.That(inputPresenterSource, Does.Not.Contain("private static string ToStatusText"));
            Assert.That(inputPresenterSource, Does.Not.Contain("SetRawStatus"));
            Assert.That(presenterSource, Does.Not.Contain("ui.settings.input.waiting_for_key"));
            Assert.That(runtimeBuilderSource, Does.Contain("SettingsStaticTextDescriptors.InputResetConfirmTitle"));
            Assert.That(runtimeBuilderSource, Does.Contain("SettingsStaticTextDescriptors.InputResetConfirmBody"));
            Assert.That(runtimeBuilderSource, Does.Contain("SettingsStaticTextDescriptors.InputResetConfirmLabel"));
            Assert.That(runtimeBuilderSource, Does.Contain("SettingsStaticTextDescriptors.Cancel"));
            Assert.That(runtimeBuilderSource, Does.Not.Contain("\"Reset Input Settings\""));
            Assert.That(runtimeBuilderSource, Does.Not.Contain("\"Reset input settings to defaults?\""));
        }

        [Test]
        public void SettingsInputPresenter_ActionConflictLocalizesNestedActionAndTracksLocale()
        {
            var resolver = new FakeLocalizedTextResolver();
            resolver.SetLocale("ko-KR");
            var keyboardPort = new CompletingKeyboardSettingsPort(KeyboardBindingValidationResult.DuplicateAction);
            var presenter = new SettingsInputPresenter(keyboardPort, resolver);

            presenter.Apply(new SettingsInputPresenterInput(
                SettingsStaticTextDescriptors.MovementKeys,
                SettingsStaticTextDescriptors.UseArrowKeys,
                SettingsStaticTextDescriptors.Push,
                SettingsStaticTextDescriptors.Flip,
                SettingsStaticTextDescriptors.Change,
                SettingsStaticTextDescriptors.ResetInput));

            presenter.StartRebind(KeyboardBindableAction.Push);

            Assert.That(presenter.ViewModel.StatusText, Is.EqualTo("밀기 키 입력하세요..."));

            keyboardPort.Complete();

            Assert.That(presenter.ViewModel.StatusText, Is.EqualTo("이 키는 이미 뒤집기에 사용 중입니다."));

            resolver.SetLocale("en-US");
            presenter.RefreshLocalization();

            Assert.That(presenter.ViewModel.StatusText, Is.EqualTo("This key is already used by Flip."));

            presenter.ResetToDefaults();

            Assert.That(presenter.ViewModel.StatusText, Is.EqualTo("Input settings reset."));
        }

        [Test]
        public void SettingsInputInvalidDefaultStatus_UsesLocalizedUnsupportedKeyFallback()
        {
            var source = System.IO.File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/Settings/SettingsScreenPresenters.cs");
            var inputPresenterStart = source.IndexOf(
                "public sealed class SettingsInputPresenter",
                StringComparison.Ordinal);
            var inputPresenterEnd = source.IndexOf(
                "public sealed class SettingsScreenPresenter",
                inputPresenterStart,
                StringComparison.Ordinal);
            var inputPresenterSource = source.Substring(
                inputPresenterStart,
                inputPresenterEnd - inputPresenterStart);

            Assert.That(inputPresenterSource, Does.Not.Contain("\"This key cannot be used.\""));
            Assert.That(inputPresenterSource, Does.Contain("SettingsDynamicTextDescriptors.InputUnsupportedKey()"));

            AssertInvalidDefaultFallbackIsLocalized(KeyboardBindingValidationResult.MissingBinding);
            AssertInvalidDefaultFallbackIsLocalized(KeyboardBindingValidationResult.InvalidKey);
            AssertInvalidDefaultFallbackIsLocalized((KeyboardBindingValidationResult)999);
        }


        [Test]
        public void UiApplicationLocalizationFoundation_RemainsPackageFree()
        {
            var references = typeof(SettingsScreenPresenter).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("Unity.TextMeshPro"));
            Assert.That(references, Does.Not.Contain("Unity.Localization"));
            Assert.That(references, Does.Not.Contain("Unity.Addressables"));
            Assert.That(references, Does.Not.Contain("Unity.ResourceManager"));
        }

        [Test]
        public void LocalizedTypographyContract_RemainsPackageFree()
        {
            var references = typeof(LocalizedTypographyStyle).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var typographySource = System.IO.File.ReadAllText(
                "Assets/_Features/UI/UI_ViewShared/Runtime/LocalizedTypographyStyle.cs");
            var bindingSource = System.IO.File.ReadAllText(
                "Assets/_Features/UI/UI_Screens/Runtime/LocalizedTmpTextBinding.cs");

            Assert.That(references, Does.Not.Contain("Unity.TextMeshPro"));
            AssertPackageFreeSource(typographySource);
            Assert.That(typeof(LocalizedTmpTextBinding).Assembly.GetName().Name, Is.EqualTo("Game.Feature.UI.Screens"));
            Assert.That(bindingSource, Does.Contain("TMPro"));
            Assert.That(bindingSource, Does.Not.Contain("UnityEngine.Localization"));
            Assert.That(bindingSource, Does.Not.Contain("UnityEngine.AddressableAssets"));
            Assert.That(bindingSource, Does.Not.Contain("LocalizationSettings"));
            Assert.That(bindingSource, Does.Not.Contain("Addressables"));
        }

        [Test]
        public void DescriptorModelSource_DoesNotUseUnityLocalizationAddressablesOrTmp()
        {
            var descriptorSource = System.IO.File.ReadAllText(
                "Assets/_Features/UI/UI_ViewShared/Runtime/LocalizedTextDescriptor.cs");
            var presenterSource = System.IO.File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/Settings/SettingsScreenPresenters.cs");

            AssertPackageFreeSource(descriptorSource);
            Assert.That(presenterSource, Does.Not.Contain("LocalizationSettings"));
            Assert.That(presenterSource, Does.Not.Contain("StringTable"));
            Assert.That(presenterSource, Does.Not.Contain("Addressables"));
            Assert.That(presenterSource, Does.Not.Contain("TMPro"));
        }

        private static LocalizedTextDescriptor[] GetSettingsPayloadDescriptors(SettingsScreenPayload payload)
        {
            return new[]
            {
                payload.TitleTextDescriptor,
                payload.AudioTabLabelDescriptor,
                payload.DisplayTabLabelDescriptor,
                payload.InputTabLabelDescriptor,
                payload.MovementLabelDescriptor,
                payload.UseArrowKeysLabelDescriptor,
                payload.PushLabelDescriptor,
                payload.FlipLabelDescriptor,
                payload.InputChangeLabelDescriptor,
                payload.ResetInputLabelDescriptor,
                payload.LanguageLabelDescriptor,
                payload.EnglishLanguageLabelDescriptor,
                payload.KoreanLanguageLabelDescriptor,
                payload.AudioMainLabelDescriptor,
                payload.AudioBgmLabelDescriptor,
                payload.AudioSfxLabelDescriptor,
                payload.AudioMuteLabelDescriptor,
                payload.DisplayCurrentLabelDescriptor,
                payload.DisplayResolutionTextDescriptor,
                payload.ResolutionHintDescriptor,
                payload.FullscreenWindowLabelDescriptor,
                payload.FullscreenOnLabelDescriptor,
                payload.DisplayApplyButtonTextDescriptor,
                payload.DisplayRevertButtonTextDescriptor,
                payload.BackLabelDescriptor,
            };
        }

        private static LocalizedTextDescriptor[] GetPausePayloadDescriptors(PausePopupPayload payload)
        {
            return new[]
            {
                payload.TitleTextDescriptor,
                payload.DescriptionTextDescriptor,
                payload.ResumeLabelDescriptor,
                payload.SettingsLabelDescriptor,
                payload.RetryLabelDescriptor,
                payload.MainMenuLabelDescriptor,
            };
        }

        private static LocalizedTextDescriptor[] GetMainMenuPayloadDescriptors(MainMenuStaticTextPayload payload)
        {
            return new[]
            {
                payload.StartLabelDescriptor,
                payload.SettingsLabelDescriptor,
                payload.QuitLabelDescriptor,
            };
        }

        private static void AssertPackageFreeSource(string source)
        {
            Assert.That(source, Does.Not.Contain("UnityEngine.Localization"));
            Assert.That(source, Does.Not.Contain("UnityEngine.AddressableAssets"));
            Assert.That(source, Does.Not.Contain("LocalizationSettings"));
            Assert.That(source, Does.Not.Contain("StringTable"));
            Assert.That(source, Does.Not.Contain("Addressables"));
            Assert.That(source, Does.Not.Contain("TMPro"));
            Assert.That(source, Does.Not.Contain("ScriptableObject"));
        }

        private static TextMeshProUGUI CreateTmpText(string name)
        {
            var gameObject = new GameObject(name);
            return gameObject.AddComponent<TextMeshProUGUI>();
        }

        private static TextMeshProUGUI CreateTmpText(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.AddComponent<TextMeshProUGUI>();
        }

        private static void DestroyText(TMP_Text label)
        {
            if (label != null)
            {
                UnityEngine.Object.DestroyImmediate(label.gameObject);
            }
        }

        private static SettingsViewFixture CreateSettingsViewFixture()
        {
            var root = new GameObject("SettingsViewFixture");
            var view = root.AddComponent<SettingsScreenView>();
            var titleLabel = CreateTmpText("TitleLabel", root.transform);
            var audioTabLabel = CreateTmpText("AudioTabLabel", root.transform);
            var displayTabLabel = CreateTmpText("DisplayTabLabel", root.transform);
            var inputTabLabel = CreateTmpText("InputTabLabel", root.transform);
            var backLabel = CreateTmpText("BackLabel", root.transform);

            var inputRoot = new GameObject("InputView");
            inputRoot.transform.SetParent(root.transform, false);
            var inputView = inputRoot.AddComponent<SettingsInputView>();
            var movementLabel = CreateTmpText("MovementLabel", inputRoot.transform);
            var movementToggleLabel = CreateTmpText("MovementToggleLabel", inputRoot.transform);
            var movementCurrentText = CreateTmpText("MovementCurrentText", inputRoot.transform);
            var pushLabel = CreateTmpText("PushLabel", inputRoot.transform);
            var pushCurrentText = CreateTmpText("PushCurrentText", inputRoot.transform);
            var pushChangeLabel = CreateTmpText("PushChangeLabel", inputRoot.transform);
            var flipLabel = CreateTmpText("FlipLabel", inputRoot.transform);
            var flipChangeLabel = CreateTmpText("FlipChangeLabel", inputRoot.transform);
            var resetButtonLabel = CreateTmpText("ResetButtonLabel", inputRoot.transform);
            var statusText = CreateTmpText("StatusText", inputRoot.transform);

            SetPrivateField(view, "_titleLabel", titleLabel);
            SetPrivateField(view, "_audioTabButtonLabel", audioTabLabel);
            SetPrivateField(view, "_displayTabButtonLabel", displayTabLabel);
            SetPrivateField(view, "_inputTabButtonLabel", inputTabLabel);
            SetPrivateField(view, "_backButtonLabel", backLabel);
            SetPrivateField(view, "_inputView", inputView);

            SetPrivateField(inputView, "_movementLabel", movementLabel);
            SetPrivateField(inputView, "_movementToggleLabel", movementToggleLabel);
            SetPrivateField(inputView, "_movementCurrentText", movementCurrentText);
            SetPrivateField(inputView, "_pushLabel", pushLabel);
            SetPrivateField(inputView, "_pushCurrentText", pushCurrentText);
            SetPrivateField(inputView, "_pushChangeButtonLabel", pushChangeLabel);
            SetPrivateField(inputView, "_flipLabel", flipLabel);
            SetPrivateField(inputView, "_flipChangeButtonLabel", flipChangeLabel);
            SetPrivateField(inputView, "_resetButtonLabel", resetButtonLabel);
            SetPrivateField(inputView, "_statusText", statusText);

            return new SettingsViewFixture(
                root,
                view,
                inputView,
                new SettingsScreenViewModel(),
                new SettingsInputViewModel(),
                titleLabel,
                audioTabLabel,
                displayTabLabel,
                inputTabLabel,
                backLabel,
                movementLabel,
                movementCurrentText,
                pushCurrentText,
                pushChangeLabel,
                statusText);
        }

        private static MainMenuViewFixture CreateMainMenuViewFixture()
        {
            var root = new GameObject("MainMenuViewFixture");
            var view = root.AddComponent<MainMenuScreenView>();
            var startLabel = CreateTmpText("StartLabel", root.transform);
            var settingsLabel = CreateTmpText("SettingsLabel", root.transform);
            var quitLabel = CreateTmpText("QuitLabel", root.transform);

            SetPrivateField(view, "_startButtonLabel", startLabel);
            SetPrivateField(view, "_settingsButtonLabel", settingsLabel);
            SetPrivateField(view, "_quitButtonLabel", quitLabel);

            return new MainMenuViewFixture(root, view, startLabel, settingsLabel, quitLabel);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must exist for this view fixture.");
            field.SetValue(target, value);
        }

        private static void AssertInvalidDefaultFallbackIsLocalized(KeyboardBindingValidationResult result)
        {
            var resolver = new FakeLocalizedTextResolver();
            resolver.SetLocale("ko-KR");
            var keyboardPort = new CompletingKeyboardSettingsPort(result);
            var presenter = new SettingsInputPresenter(keyboardPort, resolver);

            presenter.Apply(new SettingsInputPresenterInput(
                SettingsStaticTextDescriptors.MovementKeys,
                SettingsStaticTextDescriptors.UseArrowKeys,
                SettingsStaticTextDescriptors.Push,
                SettingsStaticTextDescriptors.Flip,
                SettingsStaticTextDescriptors.Change,
                SettingsStaticTextDescriptors.ResetInput));

            presenter.StartRebind(KeyboardBindableAction.Push);
            keyboardPort.Complete();

            Assert.That(presenter.ViewModel.StatusText, Is.EqualTo("이 키는 사용할 수 없습니다."));
            Assert.That(presenter.ViewModel.PushCurrentText, Is.EqualTo("J"));
            Assert.That(presenter.ViewModel.FlipCurrentText, Is.EqualTo("K"));

            resolver.SetLocale("en-US");
            presenter.Apply(new SettingsInputPresenterInput(
                SettingsStaticTextDescriptors.MovementKeys,
                SettingsStaticTextDescriptors.UseArrowKeys,
                SettingsStaticTextDescriptors.Push,
                SettingsStaticTextDescriptors.Flip,
                SettingsStaticTextDescriptors.Change,
                SettingsStaticTextDescriptors.ResetInput));

            Assert.That(presenter.ViewModel.StatusText, Is.EqualTo("This key cannot be used."));
            Assert.That(presenter.ViewModel.PushCurrentText, Is.EqualTo("J"));
            Assert.That(presenter.ViewModel.FlipCurrentText, Is.EqualTo("K"));
        }

        private static string ResolveDefaultInputValidationStatus(
            KeyboardBindingValidationResult validationResult)
        {
            if (validationResult == KeyboardBindingValidationResult.AlreadyRebinding)
            {
                var rejectingPresenter = new SettingsInputPresenter(
                    new RejectingKeyboardSettingsPort(validationResult));
                rejectingPresenter.Apply(new SettingsInputPresenterInput(
                    SettingsStaticTextDescriptors.MovementKeys,
                    SettingsStaticTextDescriptors.UseArrowKeys,
                    SettingsStaticTextDescriptors.Push,
                    SettingsStaticTextDescriptors.Flip,
                    SettingsStaticTextDescriptors.Change,
                    SettingsStaticTextDescriptors.ResetInput));
                rejectingPresenter.StartRebind(KeyboardBindableAction.Push);
                return rejectingPresenter.ViewModel.StatusText;
            }

            var keyboardPort = new CompletingKeyboardSettingsPort(validationResult);
            var presenter = new SettingsInputPresenter(keyboardPort);
            presenter.Apply(new SettingsInputPresenterInput(
                SettingsStaticTextDescriptors.MovementKeys,
                SettingsStaticTextDescriptors.UseArrowKeys,
                SettingsStaticTextDescriptors.Push,
                SettingsStaticTextDescriptors.Flip,
                SettingsStaticTextDescriptors.Change,
                SettingsStaticTextDescriptors.ResetInput));
            presenter.StartRebind(KeyboardBindableAction.Push);
            keyboardPort.Complete();
            return presenter.ViewModel.StatusText;
        }

        private sealed class FakeLocalizedTextResolver : ILocalizedTextResolver, IUiLocaleSelectionPort
        {
            private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _values =
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en-US"] = new Dictionary<string, string>
                    {
                        ["ui.settings.title"] = "Settings",
                        ["ui.settings.audio"] = "Audio",
                        ["ui.settings.display"] = "Display",
                        ["ui.settings.input"] = "Input",
                        ["ui.settings.input.movement_keys"] = "Movement Keys",
                        ["ui.settings.input.use_arrow_keys"] = "Use Arrow Keys",
                        ["ui.settings.input.push"] = "Push",
                        ["ui.settings.input.flip"] = "Flip",
                        ["ui.settings.input.change"] = "Change",
                        ["ui.settings.input.reset_input"] = "Reset Input",
                        ["ui.settings.language"] = "Language",
                        ["ui.settings.language.english"] = "English",
                        ["ui.settings.language.korean"] = "Korean",
                        ["ui.settings.input.rebind_canceled"] = "Key reassignment cancelled.",
                        ["ui.settings.input.reset_complete"] = "Input settings reset.",
                        ["ui.settings.input.reserved_key"] = "This key cannot be used.",
                        ["ui.settings.input.movement_conflict"] = "Movement keys cannot overlap.",
                        ["ui.settings.input.already_rebinding"] = "Another key is already being reassigned.",
                        ["ui.settings.input.action_conflict"] = "This key is already used by {0}.",
                        ["ui.settings.input.unsupported_key"] = "This key cannot be used.",
                        ["ui.settings.input.rebind_push_prompt"] = "Press a key for Push...",
                        ["ui.settings.input.rebind_flip_prompt"] = "Press a key for Flip...",
                        ["ui.common.back"] = "Back",
                        ["ui.common.settings"] = "Settings",
                        ["ui.main_menu.start"] = "Start",
                        ["ui.main_menu.quit"] = "Quit",
                        ["ui.pause.title"] = "Paused",
                        ["ui.pause.description"] = "Pausing modal popup",
                        ["ui.pause.resume"] = "Resume",
                        ["ui.pause.retry"] = "Retry",
                        ["ui.pause.main_menu"] = "Main Menu",
                    },
                    ["ko-KR"] = new Dictionary<string, string>
                    {
                        ["ui.settings.title"] = "설정",
                        ["ui.settings.audio"] = "오디오",
                        ["ui.settings.display"] = "디스플레이",
                        ["ui.settings.input"] = "입력",
                        ["ui.settings.input.movement_keys"] = "이동 키",
                        ["ui.settings.input.use_arrow_keys"] = "화살표 키 사용",
                        ["ui.settings.input.push"] = "밀기",
                        ["ui.settings.input.flip"] = "뒤집기",
                        ["ui.settings.input.change"] = "변경",
                        ["ui.settings.input.reset_input"] = "입력 초기화",
                        ["ui.settings.language"] = "언어",
                        ["ui.settings.language.english"] = "영어",
                        ["ui.settings.language.korean"] = "한국어",
                        ["ui.settings.input.rebind_canceled"] = "키 재지정을 취소했습니다.",
                        ["ui.settings.input.reset_complete"] = "입력 설정이 초기화되었습니다.",
                        ["ui.settings.input.reserved_key"] = "이 키는 사용할 수 없습니다.",
                        ["ui.settings.input.movement_conflict"] = "이동 키는 서로 중복될 수 없습니다.",
                        ["ui.settings.input.already_rebinding"] = "다른 키를 이미 재지정하고 있습니다.",
                        ["ui.settings.input.action_conflict"] = "이 키는 이미 {0}에 사용 중입니다.",
                        ["ui.settings.input.unsupported_key"] = "이 키는 사용할 수 없습니다.",
                        ["ui.settings.input.rebind_push_prompt"] = "밀기 키 입력하세요...",
                        ["ui.settings.input.rebind_flip_prompt"] = "뒤집기 키 입력하세요...",
                        ["ui.common.back"] = "뒤로",
                        ["ui.common.settings"] = "설정",
                        ["ui.main_menu.start"] = "시작",
                        ["ui.main_menu.quit"] = "종료",
                        ["ui.pause.title"] = "일시 정지",
                        ["ui.pause.description"] = "일시 정지 팝업",
                        ["ui.pause.resume"] = "계속하기",
                        ["ui.pause.retry"] = "다시 시도",
                        ["ui.pause.main_menu"] = "메인 메뉴",
                    },
                };

            public string CurrentLocaleCode { get; private set; } = "en-US";

            public IReadOnlyList<string> AvailableLocaleCodes { get; } =
                new[] { "en-US", "ko-KR" };

            private Action _localeChanged;

            public int LocaleChangedSubscriberCount =>
                _localeChanged != null ? _localeChanged.GetInvocationList().Length : 0;

            public event Action LocaleChanged
            {
                add => _localeChanged += value;
                remove => _localeChanged -= value;
            }

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                if (!_values.TryGetValue(CurrentLocaleCode, out var localeValues) ||
                    !string.Equals(descriptor.Table, SettingsStaticTextDescriptors.Table, StringComparison.Ordinal) ||
                    !localeValues.TryGetValue(descriptor.Key, out var value))
                {
                    return $"[{CurrentLocaleCode}:{descriptor.Table}:{descriptor.Key}]";
                }

                for (var i = 0; i < descriptor.Arguments.Count; i++)
                {
                    var argument = descriptor.Arguments[i] is LocalizedTextDescriptor nestedDescriptor
                        ? Resolve(nestedDescriptor)
                        : descriptor.Arguments[i]?.ToString() ?? string.Empty;
                    value = value.Replace($"{{{i}}}", argument);
                }

                return value;
            }

            public void SetLocale(string localeCode)
            {
                if (string.Equals(CurrentLocaleCode, localeCode, StringComparison.Ordinal))
                {
                    return;
                }

                CurrentLocaleCode = localeCode;
                _localeChanged?.Invoke();
            }

            public bool TrySetLocale(string localeCode)
            {
                if (!AvailableLocaleCodes.Contains(localeCode))
                {
                    return false;
                }

                SetLocale(localeCode);
                return true;
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
                    BuildSnapshot(),
                    _completionResult == KeyboardBindingValidationResult.DuplicateAction
                        ? (_rebindingAction == KeyboardBindableAction.Push
                            ? KeyboardBindableAction.Flip
                            : KeyboardBindableAction.Push)
                        : (KeyboardBindableAction?)null));
            }

            private KeyboardBindingSettingsSnapshot BuildSnapshot()
            {
                return new KeyboardBindingSettingsSnapshot(
                    KeyboardMovementScheme.Wasd,
                    "WASD",
                    "J",
                    "K",
                    _isRebinding,
                    _isRebinding ? (KeyboardBindableAction?)_rebindingAction : null);
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
                    "J",
                    "K",
                    false,
                    null);
            }
        }

        private readonly struct TypographyCall
        {
            public TypographyCall(
                string localeCode,
                LocalizedTextRole role,
                LocalizedTextWeight weight)
            {
                LocaleCode = localeCode;
                Role = role;
                Weight = weight;
            }

            public string LocaleCode { get; }

            public LocalizedTextRole Role { get; }

            public LocalizedTextWeight Weight { get; }
        }

        private sealed class RecordingTypographyResolver : ILocalizedTypographyResolver
        {
            public readonly List<TypographyCall> Calls = new();

            public LocalizedTypographyStyle Style { get; set; } = new(18f, 0f, false);

            public LocalizedTypographyStyle Resolve(
                string localeCode,
                LocalizedTextRole role,
                LocalizedTextWeight weight)
            {
                Calls.Add(new TypographyCall(localeCode, role, weight));
                return Style;
            }
        }

        private sealed class SettingsViewFixture
        {
            public SettingsViewFixture(
                GameObject root,
                SettingsScreenView view,
                SettingsInputView inputView,
                SettingsScreenViewModel screenViewModel,
                SettingsInputViewModel inputViewModel,
                TMP_Text titleLabel,
                TMP_Text audioTabLabel,
                TMP_Text displayTabLabel,
                TMP_Text inputTabLabel,
                TMP_Text backLabel,
                TMP_Text movementLabel,
                TMP_Text movementCurrentText,
                TMP_Text pushCurrentText,
                TMP_Text pushChangeLabel,
                TMP_Text statusText)
            {
                Root = root;
                View = view;
                InputView = inputView;
                ScreenViewModel = screenViewModel;
                InputViewModel = inputViewModel;
                TitleLabel = titleLabel;
                AudioTabLabel = audioTabLabel;
                DisplayTabLabel = displayTabLabel;
                InputTabLabel = inputTabLabel;
                BackLabel = backLabel;
                MovementLabel = movementLabel;
                MovementCurrentText = movementCurrentText;
                PushCurrentText = pushCurrentText;
                PushChangeLabel = pushChangeLabel;
                StatusText = statusText;
            }

            public GameObject Root { get; }

            public SettingsScreenView View { get; }

            public SettingsInputView InputView { get; }

            public SettingsScreenViewModel ScreenViewModel { get; }

            public SettingsInputViewModel InputViewModel { get; }

            public TMP_Text TitleLabel { get; }

            public TMP_Text AudioTabLabel { get; }

            public TMP_Text DisplayTabLabel { get; }

            public TMP_Text InputTabLabel { get; }

            public TMP_Text BackLabel { get; }

            public TMP_Text MovementLabel { get; }

            public TMP_Text MovementCurrentText { get; }

            public TMP_Text PushCurrentText { get; }

            public TMP_Text PushChangeLabel { get; }

            public TMP_Text StatusText { get; }

            public void Destroy()
            {
                if (Root != null)
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }
            }
        }

        private sealed class MainMenuViewFixture
        {
            public MainMenuViewFixture(
                GameObject root,
                MainMenuScreenView view,
                TMP_Text startLabel,
                TMP_Text settingsLabel,
                TMP_Text quitLabel)
            {
                Root = root;
                View = view;
                StartLabel = startLabel;
                SettingsLabel = settingsLabel;
                QuitLabel = quitLabel;
            }

            public GameObject Root { get; }

            public MainMenuScreenView View { get; }

            public TMP_Text StartLabel { get; }

            public TMP_Text SettingsLabel { get; }

            public TMP_Text QuitLabel { get; }

            public void Destroy()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private sealed class FakeUiLocalePreferenceStore : IUiLocalePreferenceStore
        {
            private string _localeCode;

            public FakeUiLocalePreferenceStore(string localeCode = null)
            {
                _localeCode = localeCode;
            }

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

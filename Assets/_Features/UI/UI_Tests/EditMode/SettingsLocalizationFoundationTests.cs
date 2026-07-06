using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Application;
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
            SettingsStaticTextDescriptors.Back,
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
                    "ui.common.back",
                }));
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

            resolver.SetLocale("ko-KR");

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.TitleTextDescriptor), Is.EqualTo("설정"));
            Assert.That(resolver.Resolve(SettingsScreenPayload.Default.ResetInputLabelDescriptor), Is.EqualTo("입력 초기화"));
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
                    typographyResolver);

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
        public void DynamicSettingsStrings_RemainOutsideStaticDescriptorMap()
        {
            var descriptors = GetSettingsPayloadDescriptors(SettingsScreenPayload.Default);
            var descriptorKeys = descriptors.Select(descriptor => descriptor.Key).ToArray();

            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.audio.percent"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.audio.muted"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.display.preview_countdown"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.input.rebind_status"));
            Assert.That(descriptorKeys, Does.Not.Contain("ui.settings.input.rebind_error"));
            Assert.That(typeof(AudioSettingsRowViewModel).GetProperty(nameof(AudioSettingsRowViewModel.ValueText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsDisplayViewModel).GetProperty(nameof(SettingsDisplayViewModel.DisplayStatusText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsDisplayViewModel).GetProperty(nameof(SettingsDisplayViewModel.PreviewCountdownText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsInputViewModel).GetProperty(nameof(SettingsInputViewModel.MovementCurrentText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsInputViewModel).GetProperty(nameof(SettingsInputViewModel.PushCurrentText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsInputViewModel).GetProperty(nameof(SettingsInputViewModel.FlipCurrentText))?.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(typeof(SettingsInputViewModel).GetProperty(nameof(SettingsInputViewModel.StatusText))?.PropertyType, Is.EqualTo(typeof(string)));
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
                payload.BackLabelDescriptor,
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
                pushChangeLabel,
                statusText);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must exist for this view fixture.");
            field.SetValue(target, value);
        }

        private sealed class FakeLocalizedTextResolver : ILocalizedTextResolver
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
                        ["ui.common.back"] = "Back",
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
                        ["ui.common.back"] = "뒤로",
                    },
                };

            public string CurrentLocaleCode { get; private set; } = "en-US";

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
                return _values.TryGetValue(CurrentLocaleCode, out var localeValues) &&
                       string.Equals(descriptor.Table, SettingsStaticTextDescriptors.Table, StringComparison.Ordinal) &&
                       localeValues.TryGetValue(descriptor.Key, out var value)
                    ? value
                    : $"[{CurrentLocaleCode}:{descriptor.Table}:{descriptor.Key}]";
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
    }
}

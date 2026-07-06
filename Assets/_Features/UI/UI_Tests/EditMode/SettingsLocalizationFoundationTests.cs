using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Application;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;

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

            public event Action LocaleChanged;

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
                LocaleChanged?.Invoke();
            }
        }
    }
}

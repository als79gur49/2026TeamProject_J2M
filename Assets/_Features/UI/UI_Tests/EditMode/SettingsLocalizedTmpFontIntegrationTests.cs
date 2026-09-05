using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Application;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsLocalizedTmpFontIntegrationTests
    {
        private const string LiberationSansFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string UiApplicationRuntimePath = "Assets/_Features/UI/UI_Application/Runtime";
        private const string UiScreensRuntimePath = "Assets/_Features/UI/UI_Screens/Runtime";
        private const string UiViewSharedRuntimePath = "Assets/_Features/UI/UI_ViewShared/Runtime";

        [Test]
        public void TmpFontResolver_BelongsToViewSideBoundary()
        {
            Assert.That(
                typeof(DefaultLocalizedTmpFontResolver).Assembly.GetName().Name,
                Is.EqualTo("Game.Feature.UI.Screens"));
            Assert.That(
                typeof(ILocalizedTmpFontResolver).Assembly.GetName().Name,
                Is.EqualTo("Game.Feature.UI.Screens"));
            Assert.That(
                typeof(DefaultLocalizedTmpFontResolver).Assembly.GetReferencedAssemblies().Select(reference => reference.Name),
                Does.Contain("Unity.TextMeshPro"));

            AssertRuntimeSourceDoesNotContain(UiApplicationRuntimePath, "TMP_FontAsset");
            AssertRuntimeSourceDoesNotContain(UiViewSharedRuntimePath, "TMP_FontAsset");
            AssertRuntimeSourceContains(UiScreensRuntimePath, "TMP_FontAsset");
        }

        [Test]
        public void LocalizedTmpTextBinding_AppliesKboDiaGothicMediumFontForKoreanSettingsTitle()
        {
            var kboDiaGothicMedium = LoadKboDiaGothicMedium();
            var englishFont = LoadLiberationSans();
            var resolver = new FakeLocalizedTextResolver();
            resolver.SetLocale("ko-KR");
            var typographyResolver = new StaticTypographyResolver(new LocalizedTypographyStyle(33f, 4f, false));
            var fontResolver = new DefaultLocalizedTmpFontResolver(kboDiaGothicMedium);
            var label = CreateTmpText("settings-title");
            label.font = englishFont;

            try
            {
                using var binding = new LocalizedTmpTextBinding(
                    label,
                    SettingsStaticTextDescriptors.Title,
                    resolver,
                    typographyResolver,
                    fontResolver);

                Assert.That(label.text, Is.EqualTo("설정"));
                Assert.That(label.font, Is.SameAs(kboDiaGothicMedium));
                Assert.That(label.fontSize, Is.EqualTo(33f));
                Assert.That(label.lineSpacing, Is.EqualTo(4f));
                Assert.That((label.fontStyle & FontStyles.Bold) == FontStyles.Bold, Is.False);
            }
            finally
            {
                DestroyText(label);
            }
        }

        [Test]
        public void LocalizedTmpTextBinding_KeepsEnglishFontThenRefreshesToKboDiaGothicMediumOnKoreanLocale()
        {
            var kboDiaGothicMedium = LoadKboDiaGothicMedium();
            var englishFont = LoadLiberationSans();
            var resolver = new FakeLocalizedTextResolver();
            var fontResolver = new DefaultLocalizedTmpFontResolver(kboDiaGothicMedium);
            var typographyResolver = new StaticTypographyResolver(new LocalizedTypographyStyle(18f, 0f, false));
            var label = CreateTmpText("settings-title");
            label.font = englishFont;

            try
            {
                using var binding = new LocalizedTmpTextBinding(
                    label,
                    SettingsStaticTextDescriptors.Title,
                    resolver,
                    typographyResolver,
                    fontResolver);

                Assert.That(label.text, Is.EqualTo("Settings"));
                Assert.That(label.font, Is.SameAs(englishFont));

                resolver.SetLocale("ko-KR");

                Assert.That(label.text, Is.EqualTo("설정"));
                Assert.That(label.font, Is.SameAs(kboDiaGothicMedium));

                resolver.SetLocale("en-US");

                Assert.That(label.text, Is.EqualTo("Settings"));
                Assert.That(label.font, Is.SameAs(englishFont));
            }
            finally
            {
                DestroyText(label);
            }
        }

        [Test]
        public void LocalizedTmpTextBinding_DisposeStopsTextAndFontRefresh()
        {
            var kboDiaGothicMedium = LoadKboDiaGothicMedium();
            var englishFont = LoadLiberationSans();
            var resolver = new FakeLocalizedTextResolver();
            var fontResolver = new DefaultLocalizedTmpFontResolver(kboDiaGothicMedium);
            var typographyResolver = new StaticTypographyResolver(new LocalizedTypographyStyle(18f, 0f, false));
            var label = CreateTmpText("settings-title");
            label.font = englishFont;
            var binding = new LocalizedTmpTextBinding(
                label,
                SettingsStaticTextDescriptors.Title,
                resolver,
                typographyResolver,
                fontResolver);

            try
            {
                Assert.That(label.text, Is.EqualTo("Settings"));
                Assert.That(label.font, Is.SameAs(englishFont));

                binding.Dispose();
                resolver.SetLocale("ko-KR");

                Assert.That(label.text, Is.EqualTo("Settings"));
                Assert.That(label.font, Is.SameAs(englishFont));
                Assert.That(resolver.LocaleChangedSubscriberCount, Is.EqualTo(0));
            }
            finally
            {
                binding.Dispose();
                DestroyText(label);
            }
        }

        private static TMP_FontAsset LoadKboDiaGothicMedium()
        {
            return UiTestPrefabAssetUtility.LoadKboDiaGothicMediumFont();
        }

        private static TMP_FontAsset LoadLiberationSans()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationSansFontAssetPath);
            Assert.That(fontAsset, Is.Not.Null, $"{LiberationSansFontAssetPath} must be present.");
            return fontAsset;
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must exist.");
            field.SetValue(target, value);
        }

        private static void AssertRuntimeSourceContains(string rootPath, string token)
        {
            var hits = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(hits, Is.Not.Empty, $"{token} must remain in the view-side runtime boundary.");
        }

        private static void AssertRuntimeSourceDoesNotContain(string rootPath, string token)
        {
            var hits = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(hits, Is.Empty, $"{token} leaked into {rootPath}: {string.Join(", ", hits)}");
        }

        private sealed class FakeLocalizedTextResolver : ILocalizedTextResolver
        {
            private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _values =
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en-US"] = new Dictionary<string, string>
                    {
                        ["ui.settings.title"] = "Settings",
                    },
                    ["ko-KR"] = new Dictionary<string, string>
                    {
                        ["ui.settings.title"] = "설정",
                    },
                };

            private Action _localeChanged;

            public string CurrentLocaleCode { get; private set; } = "en-US";

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

        private sealed class StaticTypographyResolver : ILocalizedTypographyResolver
        {
            private readonly LocalizedTypographyStyle _style;

            public StaticTypographyResolver(LocalizedTypographyStyle style)
            {
                _style = style;
            }

            public LocalizedTypographyStyle Resolve(
                string localeCode,
                LocalizedTextRole role,
                LocalizedTextWeight weight)
            {
                return _style;
            }
        }
    }
}

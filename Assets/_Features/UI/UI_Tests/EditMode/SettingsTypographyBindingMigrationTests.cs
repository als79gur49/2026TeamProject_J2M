using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsTypographyBindingMigrationTests
    {
        private const string SettingsPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab";
        private const string ThemeAssetPath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";
        private const string ScreenCatalogPath =
            "Assets/_Features/UI/UI_Screens/Prefabs/GameplayScreenPrefabCatalog.asset";
        private const string OrbitronFontAssetPath =
            "Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Orbitron/Orbitron-ExtraBold SDF.asset";
        private const string UiApplicationRuntimePath = "Assets/_Features/UI/UI_Application/Runtime";
        private const string UiViewSharedRuntimePath = "Assets/_Features/UI/UI_ViewShared/Runtime";

        [Test]
        public void SettingsPrefab_HasTypographyBindingsForRequiredLocalizedText()
        {
            var prefab = LoadSettingsPrefab();
            var required = new[]
            {
                ("Settings title", GetField<TMP_Text>(prefab, "_titleLabel"), TypographyStyleTag.SettingsDisplay),
                ("Audio tab", GetField<TMP_Text>(prefab, "_audioTabButtonLabel"), TypographyStyleTag.SettingsDisplay),
                ("Display tab", GetField<TMP_Text>(prefab, "_displayTabButtonLabel"), TypographyStyleTag.SettingsDisplay),
                ("Input tab", GetField<TMP_Text>(prefab, "_inputTabButtonLabel"), TypographyStyleTag.SettingsDisplay),
                ("Back button", GetField<TMP_Text>(prefab, "_backButtonLabel"), TypographyStyleTag.SettingsDisplay),
                ("Language label", GetField<TMP_Text>(prefab.DisplayView, "_languageLabel"), TypographyStyleTag.SettingsLabel),
                ("Language value", GetField<TMP_Text>(prefab.DisplayView, "_languageCycleButtonLabel"), TypographyStyleTag.SettingsAction),
                ("Preview countdown", GetField<TMP_Text>(prefab.DisplayView, "_previewCountdownLabel"), TypographyStyleTag.SettingsBody),
                ("Input status", GetField<TMP_Text>(prefab.InputView, "_statusText"), TypographyStyleTag.SettingsAction),
                ("Movement label", GetField<TMP_Text>(prefab.InputView, "_movementLabel"), TypographyStyleTag.SettingsLabel),
                ("Push label", GetField<TMP_Text>(prefab.InputView, "_pushLabel"), TypographyStyleTag.SettingsLabel),
                ("Flip label", GetField<TMP_Text>(prefab.InputView, "_flipLabel"), TypographyStyleTag.SettingsLabel),
                ("Reset input button", GetField<TMP_Text>(prefab.InputView, "_resetButtonLabel"), TypographyStyleTag.SettingsAction),
            };

            foreach (var (name, text, expectedTag) in required)
            {
                var binding = TypographyBinding.FindFor(text);

                Assert.That(binding, Is.Not.Null, $"{name} must have a TypographyBinding.");
                Assert.That(binding.StyleTag, Is.EqualTo(expectedTag), name);
                Assert.That(binding.SizingSourceOverride, Is.EqualTo(TypographySizingSource.Hybrid), name);
                Assert.That(binding.UseApplyMaskOverride, Is.False, name);
            }
        }

        [Test]
        public void SettingsTypographyTheme_ResolvesAllBoundStyleTagsForEnglishAndKorean()
        {
            var theme = LoadTheme();
            var prefab = LoadSettingsPrefab();
            var styleTags = prefab
                .GetComponentsInChildren<TypographyBinding>(true)
                .Select(binding => binding.StyleTag)
                .Distinct()
                .ToArray();

            Assert.That(styleTags, Is.Not.Empty);
            foreach (var styleTag in styleTags)
            {
                Assert.That(theme.TryResolve("en-US", styleTag, out _), Is.True, $"en-US {styleTag}");
                Assert.That(theme.TryResolve("ko-KR", styleTag, out _), Is.True, $"ko-KR {styleTag}");
            }
        }

        [Test]
        public void SettingsTitle_KoreanThemeAppliesNanumGothicWithoutChangingSizing()
        {
            var theme = LoadTheme();
            var prefab = UnityEngine.Object.Instantiate(LoadSettingsPrefab().gameObject);
            var view = prefab.GetComponent<SettingsScreenView>();
            var title = GetField<TMP_Text>(view, "_titleLabel");
            var originalFontSize = title.fontSize;
            var originalAutoSizing = title.enableAutoSizing;
            var originalMin = title.fontSizeMin;
            var originalMax = title.fontSizeMax;
            var koreanStyle = theme.ResolveOrThrow("ko-KR", TypographyStyleTag.SettingsDisplay);
            var originalScaleRatioA = koreanStyle.MaterialPreset.GetFloat("_ScaleRatioA");
            var originalScaleRatioB = koreanStyle.MaterialPreset.GetFloat("_ScaleRatioB");
            var originalScaleRatioC = koreanStyle.MaterialPreset.GetFloat("_ScaleRatioC");

            try
            {
                Assert.That(LocalizedTmpTextApplicator.ApplyTypographyTheme(title, theme, "ko-KR"), Is.True);

                Assert.That(title.font, Is.SameAs(LoadNanumGothic()));
                Assert.That(title.fontSharedMaterial, Is.SameAs(koreanStyle.MaterialPreset));
                Assert.That(title.fontStyle, Is.EqualTo(FontStyles.Bold));
                Assert.That(title.fontSize, Is.EqualTo(originalFontSize));
                Assert.That(title.enableAutoSizing, Is.EqualTo(originalAutoSizing));
                Assert.That(title.fontSizeMin, Is.EqualTo(originalMin));
                Assert.That(title.fontSizeMax, Is.EqualTo(originalMax));
                Assert.That(koreanStyle.MaterialPreset.GetFloat("_ScaleRatioA"), Is.EqualTo(originalScaleRatioA));
                Assert.That(koreanStyle.MaterialPreset.GetFloat("_ScaleRatioB"), Is.EqualTo(originalScaleRatioB));
                Assert.That(koreanStyle.MaterialPreset.GetFloat("_ScaleRatioC"), Is.EqualTo(originalScaleRatioC));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void SettingsTitle_EnglishThemeAppliesDisplayFont()
        {
            var theme = LoadTheme();
            var prefab = UnityEngine.Object.Instantiate(LoadSettingsPrefab().gameObject);
            var view = prefab.GetComponent<SettingsScreenView>();
            var title = GetField<TMP_Text>(view, "_titleLabel");

            try
            {
                Assert.That(LocalizedTmpTextApplicator.ApplyTypographyTheme(title, theme, "en-US"), Is.True);

                Assert.That(title.font, Is.SameAs(LoadOrbitron()));
                Assert.That(title.fontSharedMaterial, Is.SameAs(theme.ResolveOrThrow("en-US", TypographyStyleTag.SettingsDisplay).MaterialPreset));
                Assert.That(title.fontStyle, Is.EqualTo(FontStyles.UpperCase));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void LocalizedTmpTextBinding_RefreshesTextAndThemeFontOnLocaleSwitch()
        {
            var theme = LoadTheme();
            var resolver = new FakeLocalizedTextResolver();
            var label = CreateTextWithBinding(TypographyStyleTag.HeaderLarge);

            try
            {
                using var binding = new LocalizedTmpTextBinding(
                    label,
                    SettingsStaticTextDescriptors.Title,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    typographyTheme: theme);

                Assert.That(label.text, Is.EqualTo("Settings"));
                Assert.That(label.font, Is.SameAs(LoadOrbitron()));

                resolver.SetLocale("ko-KR");

                Assert.That(label.text, Is.EqualTo("설정"));
                Assert.That(label.font, Is.SameAs(LoadNanumGothic()));

                resolver.SetLocale("en-US");

                Assert.That(label.text, Is.EqualTo("Settings"));
                Assert.That(label.font, Is.SameAs(LoadOrbitron()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(label.gameObject);
            }
        }

        [Test]
        public void TypographyThemeApplicator_UsesSharedMaterialPreset()
        {
            var theme = LoadTheme();
            var label = CreateTextWithBinding(TypographyStyleTag.HeaderLarge);

            try
            {
                Assert.That(LocalizedTmpTextApplicator.ApplyTypographyTheme(label, theme, "ko-KR"), Is.True);

                var style = theme.ResolveOrThrow("ko-KR", TypographyStyleTag.HeaderLarge);
                Assert.That(label.fontSharedMaterial, Is.SameAs(style.MaterialPreset));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(label.gameObject);
            }
        }

        [Test]
        public void LocaleInvariantApplicator_IsSuccessfulNoOpEvenWithRequiredMask()
        {
            var theme = LoadTheme();
            var prefab = UnityEngine.Object.Instantiate(LoadSettingsPrefab().gameObject);
            var view = prefab.GetComponent<SettingsScreenView>();
            var label = GetField<TMP_Text>(view.InputView, "_pushKeyDisplayLabel");
            var binding = TypographyBinding.FindFor(label);
            var before = TmpTypographyAuthoredState.Capture(label);
            var beforeText = label.text;
            var allMasks = TypographyApplyMask.Font |
                           TypographyApplyMask.Material |
                           TypographyApplyMask.FontStyle |
                           TypographyApplyMask.Sizing |
                           TypographyApplyMask.LineSpacing |
                           TypographyApplyMask.CharacterSpacing;

            try
            {
                Assert.That(binding, Is.Not.Null);
                Assert.That(binding.LocaleParticipation, Is.EqualTo(TypographyLocaleParticipation.LocaleInvariant));
                Assert.That(
                    LocalizedTmpTextApplicator.ApplyTypographyTheme(label, theme, "ko-KR", binding, allMasks),
                    Is.True);
                LocalizedTmpTextApplicator.ApplyResolvedTypography(
                    label,
                    theme.ResolveOrThrow("ko-KR", binding.StyleTag),
                    binding,
                    allMasks);

                AssertTypographyState(label, before, beforeText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void ScreenCatalog_ReferencesSettingsTypographyTheme()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ScreenPrefabCatalog>(ScreenCatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                GetField<GameplayUiTypographyTheme>(catalog, "_settingsTypographyTheme"),
                Is.SameAs(LoadTheme()));
            Assert.That(
                typeof(SettingsScreenRuntimeBuildContext).GetProperty("LocalizedTmpFontResolver"),
                Is.Null);
            foreach (var viewType in new[]
                     {
                         typeof(SettingsScreenView),
                         typeof(SettingsAudioView),
                         typeof(SettingsDisplayView),
                         typeof(SettingsInputView),
                     })
            {
                var bindMethod = viewType.GetMethod("BindStaticLocalization");
                Assert.That(bindMethod, Is.Not.Null, viewType.Name);
                Assert.That(
                    bindMethod.GetParameters().Select(parameter => parameter.ParameterType),
                    Has.None.EqualTo(typeof(ILocalizedTmpFontResolver)),
                    viewType.Name);
            }
        }

        [Test]
        public void LowerUiContracts_DoNotReferenceTypographyTmpBoundary()
        {
            AssertRuntimeSourceDoesNotContain(UiApplicationRuntimePath, "TypographyBinding");
            AssertRuntimeSourceDoesNotContain(UiApplicationRuntimePath, "GameplayUiTypographyTheme");
            AssertRuntimeSourceDoesNotContain(UiApplicationRuntimePath, "TMP_Text");
            AssertRuntimeSourceDoesNotContain(UiViewSharedRuntimePath, "TypographyBinding");
            AssertRuntimeSourceDoesNotContain(UiViewSharedRuntimePath, "GameplayUiTypographyTheme");
            AssertRuntimeSourceDoesNotContain(UiViewSharedRuntimePath, "TMP_Text");
        }

        private static SettingsScreenView LoadSettingsPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<SettingsScreenView>(SettingsPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"{SettingsPrefabPath} must exist.");
            return prefab;
        }

        private static GameplayUiTypographyTheme LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemeAssetPath);
            Assert.That(theme, Is.Not.Null, $"{ThemeAssetPath} must exist.");
            return theme;
        }

        private static TMP_FontAsset LoadNanumGothic()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NanumGothicFontValidationUtility.FontAssetPath);
            Assert.That(font, Is.Not.Null, $"{NanumGothicFontValidationUtility.FontAssetPath} must exist.");
            return font;
        }

        private static TMP_FontAsset LoadOrbitron()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OrbitronFontAssetPath);
            Assert.That(font, Is.Not.Null, $"{OrbitronFontAssetPath} must exist.");
            return font;
        }

        private static TextMeshProUGUI CreateTextWithBinding(TypographyStyleTag styleTag)
        {
            var text = new GameObject("TypographyBindingTestText").AddComponent<TextMeshProUGUI>();
            var binding = text.gameObject.AddComponent<TypographyBinding>();
            SetPrivateField(binding, "target", text);
            SetPrivateField(binding, "styleTag", styleTag);
            SetPrivateField(binding, "sizingSourceOverride", TypographySizingSource.Hybrid);
            return text;
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must exist.");
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must exist.");
            field.SetValue(target, value);
        }

        private static void AssertTypographyState(
            TMP_Text target,
            TmpTypographyAuthoredState expected,
            string expectedText)
        {
            Assert.That(target.font, Is.SameAs(expected.OriginalFont));
            Assert.That(target.fontSharedMaterial, Is.SameAs(expected.OriginalMaterial));
            Assert.That(target.fontStyle, Is.EqualTo(expected.OriginalFontStyle));
            Assert.That(target.fontSize, Is.EqualTo(expected.FontSize));
            Assert.That(target.enableAutoSizing, Is.EqualTo(expected.EnableAutoSizing));
            Assert.That(target.fontSizeMin, Is.EqualTo(expected.FontSizeMin));
            Assert.That(target.fontSizeMax, Is.EqualTo(expected.FontSizeMax));
            Assert.That(target.lineSpacing, Is.EqualTo(expected.LineSpacing));
            Assert.That(target.characterSpacing, Is.EqualTo(expected.CharacterSpacing));
            Assert.That(target.text, Is.EqualTo(expectedText));
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
            private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> values =
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

            public string CurrentLocaleCode { get; private set; } = "en-US";

            public event Action LocaleChanged;

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                return values.TryGetValue(CurrentLocaleCode, out var localeValues) &&
                       localeValues.TryGetValue(descriptor.Key, out var value)
                    ? value
                    : descriptor.Key;
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

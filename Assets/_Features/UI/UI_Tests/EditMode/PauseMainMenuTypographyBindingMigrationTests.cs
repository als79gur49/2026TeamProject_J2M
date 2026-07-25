using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Composition.Editor;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class PauseMainMenuTypographyBindingMigrationTests
    {
        private const string ThemeAssetPath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";
        private const string UiApplicationRuntimePath = "Assets/_Features/UI/UI_Application/Runtime";
        private const string UiViewSharedRuntimePath = "Assets/_Features/UI/UI_ViewShared/Runtime";
        private const string LocalizedTmpTextBindingPath =
            "Assets/_Features/UI/UI_Screens/Runtime/LocalizedTmpTextBinding.cs";
        private static readonly MainMenuAuthoredTypographyBaseline MainMenuEnglishBaseline =
            new MainMenuAuthoredTypographyBaseline(
                "819507a38fa816a489de88dad2de2ce9",
                11400000,
                "819507a38fa816a489de88dad2de2ce9",
                -6419728470944652023,
                FontStyles.Bold,
                30f,
                true,
                18f,
                30f);
        private static readonly MainMenuAuthoredTypographyBaseline MainMenuKoreanBaseline =
            new MainMenuAuthoredTypographyBaseline(
                "4662feb1d501d1f479b757a82e304069",
                11400000,
                "4662feb1d501d1f479b757a82e304069",
                2769584723452840789,
                FontStyles.Bold,
                30f,
                true,
                18f,
                30f);

        [Test]
        public void PausePrefab_HasTypographyBindingsForRequiredLocalizedText()
        {
            var prefab = LoadPausePrefab();
            var required = GetPauseRequiredBindings(prefab);

            AssertRequiredBindings(required);
        }

        [Test]
        public void PausePrefab_DescriptionTextIsVisibleSurface()
        {
            var prefab = LoadPausePrefab();
            var description = GetField<TMP_Text>(prefab, "_descriptionLabel");

            Assert.That(description.gameObject.activeSelf, Is.True, "Pause description is product copy, not a hidden template.");
            Assert.That(description.enabled, Is.True);
            Assert.That(description.color.a, Is.GreaterThan(0f));
            Assert.That(description.rectTransform.rect.width, Is.GreaterThan(0f));
            Assert.That(description.rectTransform.rect.height, Is.GreaterThan(0f));
        }

        [Test]
        public void MainMenuPrefab_HasTypographyBindingsForCommandText()
        {
            var prefab = LoadMainMenuPrefab();
            var required = GetMainMenuRequiredBindings(prefab);

            AssertRequiredBindings(required);
        }

        [Test]
        public void PauseAndMainMenuBoundStyleTags_ResolveForEnglishAndKorean()
        {
            var theme = LoadTheme();
            var styleTags = LoadPausePrefab()
                .GetComponentsInChildren<TypographyBinding>(true)
                .Concat(LoadMainMenuPrefab().GetComponentsInChildren<TypographyBinding>(true))
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
        public void MainMenuCommandTheme_ResolvesMainIdentityWithoutSizingOverride()
        {
            var theme = LoadTheme();
            var english = theme.ResolveOrThrow("en-US", TypographyStyleTag.MainMenuCommand);
            var korean = theme.ResolveOrThrow("ko-KR", TypographyStyleTag.MainMenuCommand);
            var expectedApplyMask =
                TypographyApplyMask.Font |
                TypographyApplyMask.Material |
                TypographyApplyMask.FontStyle;

            AssertAssetIdentity(
                english.FontAsset,
                MainMenuEnglishBaseline.FontGuid,
                MainMenuEnglishBaseline.FontLocalId,
                "MainMenuCommand en-US font");
            AssertAssetIdentity(
                english.MaterialPreset,
                MainMenuEnglishBaseline.MaterialGuid,
                MainMenuEnglishBaseline.MaterialLocalId,
                "MainMenuCommand en-US material");
            Assert.That(english.FontStyle, Is.EqualTo(FontStyles.Bold));
            Assert.That(english.SizingSource, Is.EqualTo(TypographySizingSource.Hybrid));
            Assert.That(english.SizingMode, Is.EqualTo(TypographySizingMode.PreserveAuthored));
            Assert.That(english.ApplyMask, Is.EqualTo(expectedApplyMask));
            Assert.That(english.ApplyMask & TypographyApplyMask.Sizing, Is.EqualTo(TypographyApplyMask.None));

            AssertAssetIdentity(
                korean.FontAsset,
                MainMenuKoreanBaseline.FontGuid,
                MainMenuKoreanBaseline.FontLocalId,
                "MainMenuCommand ko-KR font");
            AssertAssetIdentity(
                korean.MaterialPreset,
                MainMenuKoreanBaseline.MaterialGuid,
                MainMenuKoreanBaseline.MaterialLocalId,
                "MainMenuCommand ko-KR material");
            Assert.That(korean.FontStyle, Is.EqualTo(FontStyles.Bold));
            Assert.That(korean.SizingSource, Is.EqualTo(TypographySizingSource.Hybrid));
            Assert.That(korean.SizingMode, Is.EqualTo(TypographySizingMode.PreserveAuthored));
            Assert.That(korean.ApplyMask, Is.EqualTo(expectedApplyMask));
            Assert.That(korean.ApplyMask & TypographyApplyMask.Sizing, Is.EqualTo(TypographyApplyMask.None));
        }

        [Test]
        public void GenericButtonTheme_StillResolvesSciFiSoldierBold()
        {
            var style = LoadTheme().ResolveOrThrow("en-US", TypographyStyleTag.Button);

            AssertAssetIdentity(
                style.FontAsset,
                "dec0b1c5d015b39438a16d1bffa2e9ca",
                11400000,
                "generic Button en-US font");
            AssertAssetIdentity(
                style.MaterialPreset,
                "dec0b1c5d015b39438a16d1bffa2e9ca",
                6254369423063020181,
                "generic Button en-US material");
            Assert.That(style.FontStyle, Is.EqualTo(FontStyles.Bold));
            Assert.That(
                style.ApplyMask,
                Is.EqualTo(
                    TypographyApplyMask.Font |
                    TypographyApplyMask.Material |
                    TypographyApplyMask.FontStyle));
        }

        [Test]
        public void PauseStaticLocalization_RefreshesTextAndThemeFontOnLocaleSwitch()
        {
            var theme = LoadTheme();
            var resolver = new FakeLocalizedTextResolver();
            var root = UnityEngine.Object.Instantiate(LoadPausePrefab().gameObject);
            var view = root.GetComponent<PausePopupView>();
            var title = GetField<TMP_Text>(view, "_titleLabel");
            var originalFontSize = title.fontSize;
            var originalAutoSizing = title.enableAutoSizing;
            var originalMaterial = title.fontSharedMaterial;
            IDisposable localizationScope = null;

            try
            {
                localizationScope = PausePopupProductionLocalizationComposer.Bind(
                    view,
                    PausePopupPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    theme);

                Assert.That(title.text, Is.EqualTo("Pause"));
                Assert.That(title.font, Is.SameAs(theme.ResolveOrThrow("en-US", TypographyStyleTag.HeaderMedium).FontAsset));
                Assert.That(title.fontSharedMaterial, Is.SameAs(theme.ResolveOrThrow("en-US", TypographyStyleTag.HeaderMedium).MaterialPreset));

                resolver.SetLocale("ko-KR");

                Assert.That(title.text, Is.EqualTo("일시정지"));
                Assert.That(title.font, Is.SameAs(theme.ResolveOrThrow("ko-KR", TypographyStyleTag.HeaderMedium).FontAsset));
                Assert.That(title.fontSharedMaterial, Is.SameAs(theme.ResolveOrThrow("ko-KR", TypographyStyleTag.HeaderMedium).MaterialPreset));
                Assert.That(title.fontSize, Is.EqualTo(originalFontSize));
                Assert.That(title.enableAutoSizing, Is.EqualTo(originalAutoSizing));
                Assert.That(title.fontSharedMaterial, Is.Not.SameAs(originalMaterial));
            }
            finally
            {
                localizationScope?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase("en-US")]
        [TestCase("ko-KR")]
        public void PauseScreenshotCapture_ResolvesProductionTypographyIdentity(string localeCode)
        {
            var theme = LoadTheme();
            var productionResolver = new FakeLocalizedTextResolver();
            productionResolver.SetLocale(localeCode);
            var productionRoot = UnityEngine.Object.Instantiate(LoadPausePrefab().gameObject);
            var captureRoot = UnityEngine.Object.Instantiate(LoadPausePrefab().gameObject);
            var productionView = productionRoot.GetComponent<PausePopupView>();
            var captureView = captureRoot.GetComponent<PausePopupView>();
            IDisposable productionScope = null;
            IDisposable captureScope = null;

            try
            {
                productionScope = PausePopupProductionLocalizationComposer.Bind(
                    productionView,
                    PausePopupPayload.Default,
                    productionResolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    theme);

                var target = TypographyPreviewScreenshotUtility.RequiredTargets.Single(candidate =>
                    string.Equals(candidate.FileStem, "Pause", StringComparison.Ordinal));
                var capture = new TypographyPreviewScreenshotCaptureResult(target, localeCode, "unused.png");
                var applyPreview = typeof(TypographyPreviewScreenshotUtility).GetMethod(
                    "ApplyLocalizedTextPreview",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(applyPreview, Is.Not.Null);
                captureScope = (IDisposable)applyPreview.Invoke(
                    null,
                    new object[] { captureRoot, target, localeCode, theme, capture });

                Assert.That(capture.Errors, Is.Empty);
                Assert.That(productionResolver.CurrentLocaleCode, Is.EqualTo(localeCode));
                Assert.That(capture.LocaleCode, Is.EqualTo(localeCode));
                AssertPauseTypographyIdentity(
                    GetPauseRequiredBindings(productionView),
                    GetPauseRequiredBindings(captureView),
                    localeCode);

                var productionSource = File.ReadAllText(
                    "Assets/_Features/UI/UI_Composition/Runtime/GameplayPopupRuntimeFactory.cs");
                var captureSource = File.ReadAllText(
                    "Assets/_Features/UI/UI_Composition/Editor/Typography/TypographyPreviewScreenshotUtility.cs");
                Assert.That(productionSource, Does.Contain("PausePopupProductionLocalizationComposer.Bind("));
                Assert.That(captureSource, Does.Contain("PausePopupProductionLocalizationComposer.Bind("));
                Assert.That(captureSource, Does.Not.Contain(
                    "view.BindStaticLocalization(\n                    PausePopupPayload.Default"));
            }
            finally
            {
                captureScope?.Dispose();
                productionScope?.Dispose();
                UnityEngine.Object.DestroyImmediate(captureRoot);
                UnityEngine.Object.DestroyImmediate(productionRoot);
            }
        }

        [TestCase("_startButtonLabel", "Start command", "Start", "시작")]
        [TestCase("_settingsButtonLabel", "Settings command", "Settings", "설정")]
        [TestCase("_quitButtonLabel", "Quit command", "Quit", "종료")]
        public void MainMenuCommandTypography_RoundTripsMainAuthoredIdentity(
            string fieldName,
            string commandName,
            string englishText,
            string koreanText)
        {
            var theme = LoadTheme();
            var resolver = new FakeLocalizedTextResolver();
            var root = UnityEngine.Object.Instantiate(LoadMainMenuPrefab().gameObject);
            var view = root.GetComponent<MainMenuScreenView>();
            var commands = new[]
            {
                (commandName, GetField<TMP_Text>(view, fieldName), englishText, koreanText),
            };

            try
            {
                AssertCommandTypography(commands, MainMenuEnglishBaseline, "authored");

                view.BindStaticLocalization(
                    MainMenuStaticTextPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    typographyTheme: theme);

                AssertCommandTypography(commands, MainMenuEnglishBaseline, "initial en-US", useKoreanText: false);

                resolver.SetLocale("ko-KR");

                AssertCommandTypography(commands, MainMenuKoreanBaseline, "ko-KR", useKoreanText: true);

                resolver.SetLocale("en-US");

                AssertCommandTypography(commands, MainMenuEnglishBaseline, "round-trip en-US", useKoreanText: false);
            }
            finally
            {
                view.UnbindStaticLocalization();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyRuntime_UsesSharedMaterialAndDoesNotInstantiateMaterial()
        {
            var theme = LoadTheme();
            var label = CreateTextWithBinding(TypographyStyleTag.Button);

            try
            {
                Assert.That(LocalizedTmpTextApplicator.ApplyTypographyTheme(label, theme, "ko-KR"), Is.True);

                var style = theme.ResolveOrThrow("ko-KR", TypographyStyleTag.Button);
                Assert.That(label.fontSharedMaterial, Is.SameAs(style.MaterialPreset));
                Assert.That(
                    File.ReadAllText(LocalizedTmpTextBindingPath),
                    Does.Not.Contain("target.material = new Material("));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(label.gameObject);
            }
        }

        [Test]
        public void PopupCatalog_ReferencesGameplayTypographyTheme()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PopupPrefabCatalog>(UiTestPrefabAssetUtility.PopupCatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(GetField<GameplayUiTypographyTheme>(catalog, "_typographyTheme"), Is.SameAs(LoadTheme()));
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

        private static PausePopupView LoadPausePrefab()
        {
            return UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(
                UiTestPrefabAssetUtility.PausePopupPrefabPath);
        }

        private static MainMenuScreenView LoadMainMenuPrefab()
        {
            return UiTestPrefabAssetUtility.LoadScreenPrefab<MainMenuScreenView>(
                UiTestPrefabAssetUtility.MainMenuScreenPrefabPath);
        }

        private static GameplayUiTypographyTheme LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemeAssetPath);
            Assert.That(theme, Is.Not.Null, $"{ThemeAssetPath} must exist.");
            return theme;
        }

        private static IReadOnlyList<(string Name, TMP_Text Text, TypographyStyleTag ExpectedTag)> GetPauseRequiredBindings(
            PausePopupView prefab)
        {
            return new[]
            {
                ("Pause title", GetField<TMP_Text>(prefab, "_titleLabel"), TypographyStyleTag.HeaderMedium),
                ("Pause description", GetField<TMP_Text>(prefab, "_descriptionLabel"), TypographyStyleTag.BodySmall),
                ("Resume button", GetField<TMP_Text>(prefab, "_resumeButtonLabel"), TypographyStyleTag.Button),
                ("Settings button", GetField<TMP_Text>(prefab, "_settingsButtonLabel"), TypographyStyleTag.Button),
                ("Retry button", GetField<TMP_Text>(prefab, "_retryButtonLabel"), TypographyStyleTag.Button),
                ("Main Menu button", GetField<TMP_Text>(prefab, "_mainMenuButtonLabel"), TypographyStyleTag.Button),
            };
        }

        private static IReadOnlyList<(string Name, TMP_Text Text, TypographyStyleTag ExpectedTag)> GetMainMenuRequiredBindings(
            MainMenuScreenView prefab)
        {
            return new[]
            {
                ("Start command", GetField<TMP_Text>(prefab, "_startButtonLabel"), TypographyStyleTag.MainMenuCommand),
                ("Settings command", GetField<TMP_Text>(prefab, "_settingsButtonLabel"), TypographyStyleTag.MainMenuCommand),
                ("Quit command", GetField<TMP_Text>(prefab, "_quitButtonLabel"), TypographyStyleTag.MainMenuCommand),
            };
        }

        private static void AssertCommandTypography(
            IReadOnlyList<(string Name, TMP_Text Text, string EnglishText, string KoreanText)> commands,
            MainMenuAuthoredTypographyBaseline expected,
            string stage,
            bool? useKoreanText = null)
        {
            foreach (var command in commands)
            {
                var context = $"{command.Name} {stage}";
                if (useKoreanText.HasValue)
                {
                    Assert.That(
                        command.Text.text,
                        Is.EqualTo(useKoreanText.Value ? command.KoreanText : command.EnglishText),
                        $"{context} text");
                }

                AssertAssetIdentity(
                    command.Text.font,
                    expected.FontGuid,
                    expected.FontLocalId,
                    $"{context} font");
                AssertAssetIdentity(
                    command.Text.fontSharedMaterial,
                    expected.MaterialGuid,
                    expected.MaterialLocalId,
                    $"{context} material");
                Assert.That(command.Text.fontStyle, Is.EqualTo(expected.FontStyle), $"{context} fontStyle");
                Assert.That(command.Text.fontSize, Is.EqualTo(expected.FontSize), $"{context} fontSize");
                Assert.That(
                    command.Text.enableAutoSizing,
                    Is.EqualTo(expected.EnableAutoSizing),
                    $"{context} enableAutoSizing");
                Assert.That(command.Text.fontSizeMin, Is.EqualTo(expected.FontSizeMin), $"{context} fontSizeMin");
                Assert.That(command.Text.fontSizeMax, Is.EqualTo(expected.FontSizeMax), $"{context} fontSizeMax");
            }
        }

        private static void AssertAssetIdentity(
            UnityEngine.Object asset,
            string expectedGuid,
            long expectedLocalId,
            string context)
        {
            Assert.That(asset, Is.Not.Null, context);
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var actualGuid, out long actualLocalId),
                Is.True,
                context);
            Assert.That(actualGuid, Is.EqualTo(expectedGuid), $"{context} GUID");
            Assert.That(actualLocalId, Is.EqualTo(expectedLocalId), $"{context} local ID");
        }

        private static void AssertRequiredBindings(
            IReadOnlyList<(string Name, TMP_Text Text, TypographyStyleTag ExpectedTag)> required)
        {
            foreach (var (name, text, expectedTag) in required)
            {
                var binding = TypographyBinding.FindFor(text);

                Assert.That(binding, Is.Not.Null, $"{name} must have a TypographyBinding.");
                Assert.That(binding.StyleTag, Is.EqualTo(expectedTag), name);
                Assert.That(binding.SizingSourceOverride, Is.EqualTo(TypographySizingSource.Hybrid), name);
                Assert.That(binding.UseApplyMaskOverride, Is.False, name);
            }
        }

        private static void AssertPauseTypographyIdentity(
            IReadOnlyList<(string Name, TMP_Text Text, TypographyStyleTag ExpectedTag)> production,
            IReadOnlyList<(string Name, TMP_Text Text, TypographyStyleTag ExpectedTag)> capture,
            string localeCode)
        {
            Assert.That(capture.Count, Is.EqualTo(production.Count));
            for (var i = 0; i < production.Count; i++)
            {
                var productionTarget = production[i];
                var captureTarget = capture[i];
                var context = $"{productionTarget.Name} {localeCode}";
                var productionBinding = TypographyBinding.FindFor(productionTarget.Text);
                var captureBinding = TypographyBinding.FindFor(captureTarget.Text);

                Assert.That(captureTarget.Name, Is.EqualTo(productionTarget.Name), $"{context} target");
                Assert.That(productionBinding, Is.Not.Null, $"{context} production binding");
                Assert.That(captureBinding, Is.Not.Null, $"{context} capture binding");
                Assert.That(captureBinding.StyleTag, Is.EqualTo(productionBinding.StyleTag), $"{context} style tag");
                Assert.That(
                    captureBinding.SizingSourceOverride,
                    Is.EqualTo(productionBinding.SizingSourceOverride),
                    $"{context} sizing source");
                Assert.That(
                    captureBinding.UseApplyMaskOverride,
                    Is.EqualTo(productionBinding.UseApplyMaskOverride),
                    $"{context} apply-mask override usage");
                Assert.That(
                    captureBinding.ApplyMaskOverride,
                    Is.EqualTo(productionBinding.ApplyMaskOverride),
                    $"{context} apply-mask override");
                AssertSameAssetIdentity(productionTarget.Text.font, captureTarget.Text.font, $"{context} font");
                AssertSameAssetIdentity(
                    productionTarget.Text.fontSharedMaterial,
                    captureTarget.Text.fontSharedMaterial,
                    $"{context} material");
                Assert.That(captureTarget.Text.fontStyle, Is.EqualTo(productionTarget.Text.fontStyle), $"{context} fontStyle");
                Assert.That(captureTarget.Text.fontSize, Is.EqualTo(productionTarget.Text.fontSize), $"{context} fontSize");
                Assert.That(
                    captureTarget.Text.enableAutoSizing,
                    Is.EqualTo(productionTarget.Text.enableAutoSizing),
                    $"{context} enableAutoSizing");
                Assert.That(
                    captureTarget.Text.fontSizeMin,
                    Is.EqualTo(productionTarget.Text.fontSizeMin),
                    $"{context} fontSizeMin");
                Assert.That(
                    captureTarget.Text.fontSizeMax,
                    Is.EqualTo(productionTarget.Text.fontSizeMax),
                    $"{context} fontSizeMax");
            }
        }

        private static void AssertSameAssetIdentity(
            UnityEngine.Object production,
            UnityEngine.Object capture,
            string context)
        {
            Assert.That(production, Is.Not.Null, $"{context} production asset");
            Assert.That(capture, Is.Not.Null, $"{context} capture asset");
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    production,
                    out var productionGuid,
                    out long productionLocalId),
                Is.True,
                $"{context} production identity");
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    capture,
                    out var captureGuid,
                    out long captureLocalId),
                Is.True,
                $"{context} capture identity");
            Assert.That(captureGuid, Is.EqualTo(productionGuid), $"{context} GUID");
            Assert.That(captureLocalId, Is.EqualTo(productionLocalId), $"{context} local ID");
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

        private static void AssertRuntimeSourceDoesNotContain(string rootPath, string token)
        {
            var hits = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(hits, Is.Empty, $"{token} leaked into {rootPath}: {string.Join(", ", hits)}");
        }

        private readonly struct MainMenuAuthoredTypographyBaseline
        {
            public readonly string FontGuid;
            public readonly long FontLocalId;
            public readonly string MaterialGuid;
            public readonly long MaterialLocalId;
            public readonly FontStyles FontStyle;
            public readonly float FontSize;
            public readonly bool EnableAutoSizing;
            public readonly float FontSizeMin;
            public readonly float FontSizeMax;

            public MainMenuAuthoredTypographyBaseline(
                string fontGuid,
                long fontLocalId,
                string materialGuid,
                long materialLocalId,
                FontStyles fontStyle,
                float fontSize,
                bool enableAutoSizing,
                float fontSizeMin,
                float fontSizeMax)
            {
                FontGuid = fontGuid;
                FontLocalId = fontLocalId;
                MaterialGuid = materialGuid;
                MaterialLocalId = materialLocalId;
                FontStyle = fontStyle;
                FontSize = fontSize;
                EnableAutoSizing = enableAutoSizing;
                FontSizeMin = fontSizeMin;
                FontSizeMax = fontSizeMax;
            }
        }

        private sealed class FakeLocalizedTextResolver : ILocalizedTextResolver
        {
            private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> values =
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["en-US"] = new Dictionary<string, string>
                    {
                        ["ui.pause.title"] = "Pause",
                        ["ui.pause.description"] = "Game paused",
                        ["ui.pause.resume"] = "Resume",
                        ["ui.common.settings"] = "Settings",
                        ["ui.pause.retry"] = "Retry",
                        ["ui.pause.main_menu"] = "Main Menu",
                        ["ui.main_menu.start"] = "Start",
                        ["ui.main_menu.quit"] = "Quit",
                    },
                    ["ko-KR"] = new Dictionary<string, string>
                    {
                        ["ui.pause.title"] = "일시정지",
                        ["ui.pause.description"] = "게임 일시정지",
                        ["ui.pause.resume"] = "계속",
                        ["ui.common.settings"] = "설정",
                        ["ui.pause.retry"] = "다시 시도",
                        ["ui.pause.main_menu"] = "메인 메뉴",
                        ["ui.main_menu.start"] = "시작",
                        ["ui.main_menu.quit"] = "종료",
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Composition.Editor;
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
                35f,
                true,
                18f,
                35f);
        private static readonly MainMenuAuthoredTypographyBaseline MainMenuKoreanBaseline =
            new MainMenuAuthoredTypographyBaseline(
                "40d61154fd6576b4d85c2d78460b16ad",
                11400000,
                "40d61154fd6576b4d85c2d78460b16ad",
                1352911973252649374,
                FontStyles.Normal,
                35f,
                true,
                18f,
                35f);

        [Test]
        public void PausePrefab_HasTypographyBindingsForRequiredLocalizedText()
        {
            var prefab = LoadPausePrefab();
            var required = GetPauseRequiredBindings(prefab);

            AssertRequiredBindings(required);
        }

        [Test]
        public void PauseAndMainMenuActions_UseExpandedAuthoredAutoSizeRanges()
        {
            var pauseActions = GetPauseRequiredBindings(LoadPausePrefab()).Skip(1);
            foreach (var action in pauseActions)
            {
                AssertAutoSizeRange(action.Text, 24f, 10f, 24f, action.Name);
            }

            var blockedSaveActions = GetMainMenuBlockedSaveRequiredBindings(LoadMainMenuPrefab()).Skip(2).ToArray();
            AssertAutoSizeRange(blockedSaveActions[0].Text, 30f, 16f, 30f, blockedSaveActions[0].Name);
            AssertAutoSizeRange(blockedSaveActions[1].Text, 30f, 16f, 30f, blockedSaveActions[1].Name);
        }

        [Test]
        public void ConfirmPopupPrefab_HasFiveSemanticTypographyBindingsWithAuthoredLayout()
        {
            var prefab = LoadConfirmPrefab();
            var required = GetConfirmRequiredBindings(prefab);

            Assert.That(prefab.GetComponentsInChildren<TypographyBinding>(true), Has.Length.EqualTo(5));
            AssertRequiredBindings(required);
            AssertConfirmTarget(required[0].Text, "Title", 1859700045196034169, Vector2.zero);
            AssertConfirmTarget(required[1].Text, "Body", 786007152345686790, Vector2.zero);
            AssertConfirmTarget(required[2].Text, "Warning", 990004, Vector2.zero);
            AssertConfirmTarget(required[3].Text, "Buttons/ConfirmButton/Label", 5809623355355838332, Vector2.zero);
            AssertConfirmTarget(required[4].Text, "Buttons/CancelButton/Label", 5096854244621539299, Vector2.zero);
        }

        [Test]
        public void ConfirmPopupProductionTypography_RoundTripsIdentityAndDisposesLocaleBinding()
        {
            var theme = LoadTheme();
            var resolver = new FakeLocalizedTextResolver();
            var root = UnityEngine.Object.Instantiate(LoadConfirmPrefab().gameObject);
            var view = root.GetComponent<ConfirmPopupView>();
            var targets = GetConfirmRequiredBindings(view);
            var authoredStates = targets.ToDictionary(target => target.Text, target => new ConfirmTypographyState(target.Text));
            IDisposable typographyScope = null;

            try
            {
                typographyScope = ConfirmPopupProductionLocalizationComposer.Bind(view, resolver, theme);

                AssertConfirmTypography(targets, authoredStates, theme, "en-US");
                resolver.SetLocale("ko-KR");
                AssertConfirmTypography(targets, authoredStates, theme, "ko-KR");
                resolver.SetLocale("en-US");
                AssertConfirmTypography(targets, authoredStates, theme, "en-US");

                typographyScope.Dispose();
                typographyScope = null;
                resolver.SetLocale("ko-KR");
                AssertConfirmTypography(targets, authoredStates, theme, "en-US");
            }
            finally
            {
                typographyScope?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConfirmPopupWarningRow_CollapsesAndRestoresAcrossReuseAndLocaleChanges()
        {
            var root = UnityEngine.Object.Instantiate(LoadConfirmPrefab().gameObject);
            var view = root.GetComponent<ConfirmPopupView>();
            var warning = GetField<TMP_Text>(view, "_warningLabel");
            var warningLayout = warning.GetComponent<LayoutElement>();
            var body = GetField<TMP_Text>(view, "_bodyLabel");
            var buttons = (RectTransform)root.transform.Find("Buttons");
            var verticalLayout = root.GetComponent<VerticalLayoutGroup>();
            var viewModel = new ConfirmPopupViewModel();

            try
            {
                Assert.That(warningLayout, Is.Not.Null);
                Assert.That(verticalLayout, Is.Not.Null);
                Assert.That(buttons, Is.Not.Null);

                view.IsVisible = true;
                view.Bind(viewModel);
                viewModel.SetContent("Title", "Body", "Warning", "Confirm", "Cancel", true);
                RebuildConfirmLayout((RectTransform)root.transform);

                var authoredWarningHeight = warningLayout.preferredHeight;
                var warningContentHeight = CalculatePreferredHeight(verticalLayout);
                var warningButtonY = buttons.anchoredPosition.y;
                var bodyY = body.rectTransform.anchoredPosition.y;
                var popupHeight = ((RectTransform)root.transform).rect.height;

                Assert.That(authoredWarningHeight, Is.GreaterThan(0f));
                Assert.That(warning.gameObject.activeSelf, Is.True);
                Assert.That(warning.text, Is.EqualTo("Warning"));
                Assert.That(warning.rectTransform.rect.height, Is.EqualTo(authoredWarningHeight).Within(0.01f));

                viewModel.SetContent("Title", "Body", string.Empty, "Confirm", "Cancel", false);
                RebuildConfirmLayout((RectTransform)root.transform);

                var noWarningContentHeight = CalculatePreferredHeight(verticalLayout);
                Assert.That(warning.text, Is.Empty);
                Assert.That(warning.gameObject.activeSelf, Is.False);
                Assert.That(warning.gameObject.activeInHierarchy, Is.False);
                Assert.That(
                    warningContentHeight - noWarningContentHeight,
                    Is.EqualTo(authoredWarningHeight + verticalLayout.spacing).Within(0.01f),
                    "The inactive warning row must contribute neither its authored height nor an adjacent spacing slot.");
                Assert.That(
                    buttons.anchoredPosition.y - warningButtonY,
                    Is.EqualTo(authoredWarningHeight + verticalLayout.spacing).Within(0.01f),
                    "Buttons must move into the collapsed warning row instead of leaving a blank gap.");
                Assert.That(body.rectTransform.anchoredPosition.y, Is.EqualTo(bodyY).Within(0.01f));
                Assert.That(((RectTransform)root.transform).rect.height, Is.EqualTo(popupHeight).Within(0.01f));
                Assert.That(warningLayout.preferredHeight, Is.EqualTo(authoredWarningHeight));

                viewModel.SetContent("Title", "Body", " \t ", "Confirm", "Cancel", false);
                RebuildConfirmLayout((RectTransform)root.transform);
                Assert.That(warning.gameObject.activeSelf, Is.False, "Whitespace warning copy is not player-visible content.");

                viewModel.SetContent("Title", "Body", "Warning restored", "Confirm", "Cancel", true);
                RebuildConfirmLayout((RectTransform)root.transform);
                Assert.That(warning.gameObject.activeSelf, Is.True);
                Assert.That(warning.text, Is.EqualTo("Warning restored"));
                Assert.That(warningLayout.preferredHeight, Is.EqualTo(authoredWarningHeight));
                Assert.That(warning.rectTransform.rect.height, Is.EqualTo(authoredWarningHeight).Within(0.01f));
                Assert.That(buttons.anchoredPosition.y, Is.EqualTo(warningButtonY).Within(0.01f));

                view.Bind(null);
                RebuildConfirmLayout((RectTransform)root.transform);
                Assert.That(warning.text, Is.Empty, "Unbind must clear transient warning copy.");
                Assert.That(warning.gameObject.activeSelf, Is.False, "Unbind must collapse transient warning layout.");

                var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
                using var presenter = new ConfirmPopupPresenter(resolver);
                view.Bind(presenter.ViewModel);
                presenter.Apply(MainMenuLocalization.CreateConfirmationPayload(
                    MainMenuConfirmationKind.RestartSlot,
                    2));
                RebuildConfirmLayout((RectTransform)root.transform);
                var englishWarning = warning.text;

                Assert.That(englishWarning, Is.Not.Empty);
                Assert.That(warning.gameObject.activeSelf, Is.True);
                Assert.That(warningLayout.preferredHeight, Is.EqualTo(authoredWarningHeight));

                resolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);
                RebuildConfirmLayout((RectTransform)root.transform);

                Assert.That(warning.text, Is.Not.Empty);
                Assert.That(warning.text, Is.Not.EqualTo(englishWarning));
                Assert.That(warning.gameObject.activeSelf, Is.True);
                Assert.That(warningLayout.preferredHeight, Is.EqualTo(authoredWarningHeight));
                Assert.That(warning.rectTransform.rect.height, Is.EqualTo(authoredWarningHeight).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PausePrefab_ProgressionStripIsBoundAndUsesInactiveTemplates()
        {
            var prefab = LoadPausePrefab();
            var progression = GetField<PauseProgressionStripView>(prefab, "_progressionView");
            var groupTemplate = GetField<PauseProgressionMarkerView>(progression, "_groupStartMarkerTemplate");
            var stageTemplate = GetField<PauseProgressionMarkerView>(progression, "_stageMarkerTemplate");
            var progressionRoot = GetField<RectTransform>(progression, "_root");
            var scrollRect = GetField<ScrollRect>(progression, "_scrollRect");
            var content = GetField<RectTransform>(progression, "_content");
            var backLine = scrollRect.viewport.Find("BackLine") as RectTransform;

            Assert.That(progression, Is.Not.Null);
            Assert.That(progressionRoot.Find("LeftButton"), Is.Null);
            Assert.That(progressionRoot.Find("RightButton"), Is.Null);
            Assert.That(scrollRect.viewport.sizeDelta.x, Is.EqualTo(0f));
            Assert.That(backLine, Is.Not.Null);
            Assert.That(backLine.parent, Is.SameAs(scrollRect.viewport));
            Assert.That(backLine.GetSiblingIndex(), Is.LessThan(content.GetSiblingIndex()));
            Assert.That(backLine.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(backLine.anchorMax, Is.EqualTo(Vector2.right));
            Assert.That(backLine.pivot, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(backLine.anchoredPosition.y, Is.GreaterThan(0f));
            Assert.That(backLine.sizeDelta.x, Is.LessThan(0f), "Stretched back line should reserve horizontal padding.");
            Assert.That(backLine.sizeDelta.y, Is.GreaterThan(0f));
            Assert.That(backLine.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(groupTemplate.gameObject.activeSelf, Is.False);
            Assert.That(stageTemplate.gameObject.activeSelf, Is.False);
            Assert.That(groupTemplate.VisualImage, Is.Not.Null);
            Assert.That(stageTemplate.VisualImage, Is.Not.Null);
            Assert.That(groupTemplate.VisualImage.raycastTarget, Is.False);
            Assert.That(stageTemplate.VisualImage.raycastTarget, Is.False);
            Assert.That(groupTemplate.CurrentFrame, Is.Not.Null);
            Assert.That(stageTemplate.CurrentFrame, Is.Not.Null);
            Assert.That(groupTemplate.CurrentFrame.name, Is.EqualTo("CurrentFrame"));
            Assert.That(stageTemplate.CurrentFrame.name, Is.EqualTo("CurrentFrame"));
            Assert.That(groupTemplate.CurrentFrame.activeSelf, Is.False);
            Assert.That(stageTemplate.CurrentFrame.activeSelf, Is.False);
            Assert.That(groupTemplate.RectTransform.sizeDelta.x, Is.GreaterThan(stageTemplate.RectTransform.sizeDelta.x));
            Assert.That(groupTemplate.RectTransform.sizeDelta.y, Is.EqualTo(stageTemplate.RectTransform.sizeDelta.y));
            Assert.That(groupTemplate.VisualImage.rectTransform.rect.width, Is.GreaterThan(stageTemplate.VisualImage.rectTransform.rect.width));
            Assert.That(groupTemplate.VisualImage.rectTransform.rect.width, Is.EqualTo(groupTemplate.VisualImage.rectTransform.rect.height));
            Assert.That(stageTemplate.VisualImage.rectTransform.rect.width, Is.EqualTo(stageTemplate.VisualImage.rectTransform.rect.height));
        }

        [Test]
        public void PausePrefab_DoesNotExposeDescriptionCopy()
        {
            var prefab = LoadPausePrefab();
            var description = prefab.transform.Find("Description");

            Assert.That(
                typeof(PausePopupView).GetField("_descriptionLabel", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(description, Is.Null);
        }

        [Test]
        public void MainMenuPrefab_HasTypographyBindingsForCommandText()
        {
            var prefab = LoadMainMenuPrefab();
            var required = GetMainMenuRequiredBindings(prefab);

            AssertRequiredBindings(required);
        }

        [Test]
        public void MainMenuBlockedSavePrefab_HasSemanticTypographyBindings()
        {
            var prefab = LoadMainMenuPrefab();
            var required = GetMainMenuBlockedSaveRequiredBindings(prefab);

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
            Assert.That(korean.FontStyle, Is.EqualTo(FontStyles.Normal));
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

                Assert.That(title.text, Is.EqualTo("일시 정지"));
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

        [Test]
        public void PauseTitleLayout_PreservesVisualCenterAndKeepsLocalizedTitlesOnOneLine()
        {
            var theme = LoadTheme();
            var resolver = new FakeLocalizedTextResolver();
            var root = UnityEngine.Object.Instantiate(LoadPausePrefab().gameObject);
            var view = root.GetComponent<PausePopupView>();
            var title = GetField<TMP_Text>(view, "_titleLabel");
            var binding = TypographyBinding.FindFor(title);
            var authoredFontSize = title.fontSize;
            var authoredAutoSizing = title.enableAutoSizing;
            var authoredFontSizeMin = title.fontSizeMin;
            var authoredFontSizeMax = title.fontSizeMax;
            TMP_FontAsset englishFont = null;
            Material englishMaterial = null;
            var englishStyle = FontStyles.Normal;
            IDisposable localizationScope = null;

            try
            {
                Assert.That(binding, Is.Not.Null);
                Assert.That(binding.StyleTag, Is.EqualTo(TypographyStyleTag.HeaderMedium));
                Assert.That(binding.SizingSourceOverride, Is.EqualTo(TypographySizingSource.Hybrid));
                Assert.That(title.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(title.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(title.rectTransform.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(title.rectTransform.sizeDelta, Is.EqualTo(Vector2.zero));
                Assert.That(title.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(title.transform.parent.name, Is.EqualTo("Title"));
                Assert.That(title.transform.parent.GetComponent<LayoutElement>().preferredHeight, Is.EqualTo(70f));

                localizationScope = PausePopupProductionLocalizationComposer.Bind(
                    view,
                    PausePopupPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    theme);
                view.IsVisible = true;
                englishFont = title.font;
                englishMaterial = title.fontSharedMaterial;
                englishStyle = title.fontStyle;

                AssertPauseTitleLayout(
                    title,
                    "Pause",
                    authoredFontSize,
                    authoredAutoSizing,
                    authoredFontSizeMin,
                    authoredFontSizeMax,
                    "en-US");

                resolver.SetLocale("ko-KR");

                AssertPauseTitleLayout(
                    title,
                    "일시 정지",
                    authoredFontSize,
                    authoredAutoSizing,
                    authoredFontSizeMin,
                    authoredFontSizeMax,
                    "ko-KR");
                Assert.That(title.font, Is.SameAs(UiTestPrefabAssetUtility.LoadClimateCrisisKr2019Font()));
                Assert.That(title.fontStyle, Is.EqualTo(FontStyles.Normal));

                resolver.SetLocale("en-US");

                AssertPauseTitleLayout(
                    title,
                    "Pause",
                    authoredFontSize,
                    authoredAutoSizing,
                    authoredFontSizeMin,
                    authoredFontSizeMax,
                    "restored en-US");
                Assert.That(title.font, Is.SameAs(englishFont));
                Assert.That(title.fontSharedMaterial, Is.SameAs(englishMaterial));
                Assert.That(title.fontStyle, Is.EqualTo(englishStyle));
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
                var captureProgression = GetField<PauseProgressionStripView>(captureView, "_progressionView");
                Assert.That(captureProgression.gameObject.activeInHierarchy, Is.True);
                Assert.That(captureProgression.MarkerCount, Is.EqualTo(13));
                Assert.That(captureProgression.CurrentIndex, Is.EqualTo(5));
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
        public void MainMenuBlockedSaveTypography_RoundTripsLocaleFontsAndPreservesSizing()
        {
            var theme = LoadTheme();
            var resolver = new FakeLocalizedTextResolver();
            var root = UnityEngine.Object.Instantiate(LoadMainMenuPrefab().gameObject);
            var view = root.GetComponent<MainMenuScreenView>();
            var required = GetMainMenuBlockedSaveRequiredBindings(view);
            var authoredStates = required.ToDictionary(
                target => target.Text,
                target => new ConfirmTypographyState(target.Text));

            try
            {
                view.BindStaticLocalization(
                    MainMenuStaticTextPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    typographyTheme: theme);

                AssertResolvedTypography(required, authoredStates, theme, "en-US");

                resolver.SetLocale("ko-KR");

                AssertResolvedTypography(required, authoredStates, theme, "ko-KR");
                Assert.That(required[2].Text.font, Is.SameAs(required[3].Text.font));
                Assert.That(required[2].Text.fontSharedMaterial, Is.SameAs(required[3].Text.fontSharedMaterial));

                resolver.SetLocale("en-US");

                AssertResolvedTypography(required, authoredStates, theme, "en-US");
            }
            finally
            {
                view.UnbindStaticLocalization();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainMenuBlockedSaveTypography_MissingBindingFailsBeforeOrdinalFallback()
        {
            var theme = LoadTheme();
            var resolver = new FakeLocalizedTextResolver();
            var root = UnityEngine.Object.Instantiate(LoadMainMenuPrefab().gameObject);
            var view = root.GetComponent<MainMenuScreenView>();
            var detail = GetField<TMP_Text>(view.SaveSlotPanel, "_blockedDetailLabel");
            var binding = TypographyBinding.FindFor(detail);

            try
            {
                Assert.That(binding, Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(binding);

                var exception = Assert.Throws<InvalidOperationException>(() => view.BindStaticLocalization(
                    MainMenuStaticTextPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    typographyTheme: theme));

                Assert.That(exception.Message, Does.Contain("BlockedSaveDetail"));
                Assert.That(exception.Message, Does.Contain("requires an authored TypographyBinding"));
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

        private static ConfirmPopupView LoadConfirmPrefab()
        {
            return UiTestPrefabAssetUtility.LoadPopupPrefab<ConfirmPopupView>(
                UiTestPrefabAssetUtility.ConfirmPopupPrefabPath);
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

        private static float CalculatePreferredHeight(VerticalLayoutGroup layout)
        {
            layout.CalculateLayoutInputVertical();
            return layout.preferredHeight;
        }

        private static void RebuildConfirmLayout(RectTransform root)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
        }

        private static IReadOnlyList<(string Name, TMP_Text Text, TypographyStyleTag ExpectedTag)> GetPauseRequiredBindings(
            PausePopupView prefab)
        {
            return new[]
            {
                ("Pause title", GetField<TMP_Text>(prefab, "_titleLabel"), TypographyStyleTag.HeaderMedium),
                ("Resume button", GetField<TMP_Text>(prefab, "_resumeButtonLabel"), TypographyStyleTag.Button),
                ("Settings button", GetField<TMP_Text>(prefab, "_settingsButtonLabel"), TypographyStyleTag.Button),
                ("Retry button", GetField<TMP_Text>(prefab, "_retryButtonLabel"), TypographyStyleTag.Button),
                ("Main Menu button", GetField<TMP_Text>(prefab, "_mainMenuButtonLabel"), TypographyStyleTag.Button),
            };
        }

        private static IReadOnlyList<(string Name, TMP_Text Text, TypographyStyleTag ExpectedTag)> GetConfirmRequiredBindings(
            ConfirmPopupView prefab)
        {
            return new[]
            {
                ("Confirm title", GetField<TMP_Text>(prefab, "_titleLabel"), TypographyStyleTag.HeaderLarge),
                ("Confirm body", GetField<TMP_Text>(prefab, "_bodyLabel"), TypographyStyleTag.PopupBody),
                ("Confirm warning", GetField<TMP_Text>(prefab, "_warningLabel"), TypographyStyleTag.PopupBody),
                ("Confirm action", GetField<TMP_Text>(prefab, "_confirmButtonLabel"), TypographyStyleTag.PopupAction),
                ("Cancel action", GetField<TMP_Text>(prefab, "_cancelButtonLabel"), TypographyStyleTag.PopupAction),
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

        private static IReadOnlyList<(string Name, TMP_Text Text, TypographyStyleTag ExpectedTag)> GetMainMenuBlockedSaveRequiredBindings(
            MainMenuScreenView prefab)
        {
            var panel = prefab.SaveSlotPanel;
            return new[]
            {
                ("Blocked save title", GetField<TMP_Text>(panel, "_blockedTitleLabel"), TypographyStyleTag.HeaderMedium),
                ("Blocked save detail", GetField<TMP_Text>(panel, "_blockedDetailLabel"), TypographyStyleTag.Body),
                ("Blocked save retry", GetField<TMP_Text>(panel, "_retryButtonLabel"), TypographyStyleTag.Button),
                ("Blocked save reset", GetField<TMP_Text>(panel, "_resetProfileButtonLabel"), TypographyStyleTag.Button),
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

        private static void AssertAutoSizeRange(
            TMP_Text target,
            float expectedFontSize,
            float expectedFontSizeMin,
            float expectedFontSizeMax,
            string context)
        {
            Assert.That(target.fontSize, Is.EqualTo(expectedFontSize), $"{context} fontSize");
            Assert.That(target.enableAutoSizing, Is.True, $"{context} enableAutoSizing");
            Assert.That(target.fontSizeMin, Is.EqualTo(expectedFontSizeMin), $"{context} fontSizeMin");
            Assert.That(target.fontSizeMax, Is.EqualTo(expectedFontSizeMax), $"{context} fontSizeMax");
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

        private static void AssertResolvedTypography(
            IReadOnlyList<(string Name, TMP_Text Text, TypographyStyleTag ExpectedTag)> targets,
            IReadOnlyDictionary<TMP_Text, ConfirmTypographyState> authoredStates,
            GameplayUiTypographyTheme theme,
            string localeCode)
        {
            foreach (var target in targets)
            {
                var style = theme.ResolveOrThrow(localeCode, target.ExpectedTag);
                var context = $"{target.Name} {localeCode}";

                Assert.That(target.Text.font, Is.SameAs(style.FontAsset), $"{context} font");
                Assert.That(target.Text.fontSharedMaterial, Is.SameAs(style.MaterialPreset), $"{context} material");
                Assert.That(target.Text.fontStyle, Is.EqualTo(style.FontStyle), $"{context} style");
                Assert.That(style.ApplyMask & TypographyApplyMask.Sizing, Is.EqualTo(TypographyApplyMask.None));
                authoredStates[target.Text].AssertSizing(target.Text, context);
            }
        }

        private static void AssertConfirmTarget(
            TMP_Text target,
            string expectedPath,
            long expectedLocalId,
            Vector2 expectedSizeDelta)
        {
            Assert.That(GetRelativePath(target.transform), Is.EqualTo(expectedPath));
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out _, out long actualLocalId),
                Is.True,
                expectedPath);
            Assert.That(actualLocalId, Is.EqualTo(expectedLocalId), $"{expectedPath} TMP component fileID");
            Assert.That(target.rectTransform.sizeDelta, Is.EqualTo(expectedSizeDelta), $"{expectedPath} Rect");
        }

        private static void AssertConfirmTypography(
            IReadOnlyList<(string Name, TMP_Text Text, TypographyStyleTag ExpectedTag)> targets,
            IReadOnlyDictionary<TMP_Text, ConfirmTypographyState> authoredStates,
            GameplayUiTypographyTheme theme,
            string localeCode)
        {
            foreach (var target in targets)
            {
                var style = theme.ResolveOrThrow(localeCode, target.ExpectedTag);
                var context = $"{target.Name} {localeCode}";

                AssertSameAssetIdentity(style.FontAsset, target.Text.font, $"{context} font");
                AssertSameAssetIdentity(style.MaterialPreset, target.Text.fontSharedMaterial, $"{context} material");
                Assert.That(target.Text.fontStyle, Is.EqualTo(style.FontStyle), $"{context} style");
                Assert.That(style.ApplyMask & TypographyApplyMask.Sizing, Is.EqualTo(TypographyApplyMask.None));
                authoredStates[target.Text].AssertSizing(target.Text, context);

                if (string.Equals(localeCode, "ko-KR", StringComparison.Ordinal))
                {
                    var expectedGuid = target.ExpectedTag == TypographyStyleTag.HeaderLarge
                        ? "40d61154fd6576b4d85c2d78460b16ad"
                        : "7dfd9aae81fc1d242b007a3b7a042fb0";
                    AssertAssetIdentity(
                        target.Text.font,
                        expectedGuid,
                        11400000,
                        $"{context} Climate font");
                }
            }
        }

        private static string GetRelativePath(Transform target)
        {
            var segments = new Stack<string>();
            for (var current = target; current != null && current.parent != null; current = current.parent)
            {
                segments.Push(current.name);
            }

            return string.Join("/", segments);
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

        private static void AssertPauseTitleLayout(
            TMP_Text title,
            string expectedText,
            float expectedFontSize,
            bool expectedAutoSizing,
            float expectedFontSizeMin,
            float expectedFontSizeMax,
            string stage)
        {
            Canvas.ForceUpdateCanvases();
            title.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            var singleLinePreferred = title.GetPreferredValues(title.text, Mathf.Infinity, Mathf.Infinity);
            var constrainedPreferred =
                title.GetPreferredValues(title.text, title.rectTransform.rect.width, Mathf.Infinity);

            Assert.That(title.text, Is.EqualTo(expectedText), stage);
            Assert.That(
                singleLinePreferred.x,
                Is.LessThanOrEqualTo(title.rectTransform.rect.width + 0.01f),
                $"{stage} single-line width");
            Assert.That(
                constrainedPreferred.y,
                Is.LessThanOrEqualTo(singleLinePreferred.y + 0.01f),
                $"{stage} line count");
            Assert.That(
                constrainedPreferred.y,
                Is.LessThanOrEqualTo(title.rectTransform.rect.height + 0.01f),
                $"{stage} vertical fit");
            Assert.That(title.isTextOverflowing, Is.False, $"{stage} overflow");
            Assert.That(title.fontSize, Is.EqualTo(expectedFontSize), $"{stage} fontSize");
            Assert.That(title.enableAutoSizing, Is.EqualTo(expectedAutoSizing), $"{stage} Auto Size");
            Assert.That(title.fontSizeMin, Is.EqualTo(expectedFontSizeMin), $"{stage} min");
            Assert.That(title.fontSizeMax, Is.EqualTo(expectedFontSizeMax), $"{stage} max");
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

        private readonly struct ConfirmTypographyState
        {
            private readonly float fontSize;
            private readonly bool enableAutoSizing;
            private readonly float fontSizeMin;
            private readonly float fontSizeMax;
            private readonly Vector2 sizeDelta;

            public ConfirmTypographyState(TMP_Text target)
            {
                fontSize = target.fontSize;
                enableAutoSizing = target.enableAutoSizing;
                fontSizeMin = target.fontSizeMin;
                fontSizeMax = target.fontSizeMax;
                sizeDelta = target.rectTransform.sizeDelta;
            }

            public void AssertSizing(TMP_Text target, string context)
            {
                Assert.That(target.fontSize, Is.EqualTo(fontSize), $"{context} fontSize");
                Assert.That(target.enableAutoSizing, Is.EqualTo(enableAutoSizing), $"{context} auto sizing");
                Assert.That(target.fontSizeMin, Is.EqualTo(fontSizeMin), $"{context} fontSizeMin");
                Assert.That(target.fontSizeMax, Is.EqualTo(fontSizeMax), $"{context} fontSizeMax");
                Assert.That(target.rectTransform.sizeDelta, Is.EqualTo(sizeDelta), $"{context} Rect");
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
                        ["ui.pause.resume"] = "Resume",
                        ["ui.common.settings"] = "Settings",
                        ["ui.pause.retry"] = "Retry",
                        ["ui.pause.main_menu"] = "Main Menu",
                        ["ui.main_menu.start"] = "Start",
                        ["ui.main_menu.quit"] = "Quit",
                    },
                    ["ko-KR"] = new Dictionary<string, string>
                    {
                        ["ui.pause.title"] = "일시 정지",
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

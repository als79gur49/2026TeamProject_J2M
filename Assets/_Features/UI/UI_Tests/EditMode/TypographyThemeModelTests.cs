using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.UI.Composition;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class TypographyThemeModelTests
    {
        private const string LiberationSansFontAssetPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string ScaleRatioA = "_ScaleRatioA";
        private const string ScaleRatioC = "_ScaleRatioC";
        private const string UiApplicationRuntimePath = "Assets/_Features/UI/UI_Application/Runtime";
        private const string UiViewSharedRuntimePath = "Assets/_Features/UI/UI_ViewShared/Runtime";
        private const string UnityStringTableTextResolverPath =
            "Assets/_Features/UI/UI_Composition/Runtime/UnityStringTableTextResolver.cs";

        [TearDown]
        public void TearDown()
        {
            RestoreNanumGothicMaterialRatios();
        }

        [Test]
        public void ThemeModel_CanResolveAllRequiredStyleTags()
        {
            var theme = CreateValidTheme();

            try
            {
                var cache = theme.BuildCache();
                var requiredStyleCount = Enum.GetValues(typeof(TypographyStyleTag)).Length;

                Assert.That(cache.Count, Is.EqualTo(requiredStyleCount * 2));
                foreach (TypographyStyleTag styleTag in Enum.GetValues(typeof(TypographyStyleTag)))
                {
                    Assert.That(theme.TryResolve("en-US", styleTag, out var englishStyle), Is.True, styleTag.ToString());
                    Assert.That(englishStyle.FontAsset, Is.SameAs(LoadLiberationSans()));
                    Assert.That(theme.TryResolve("ko-KR", styleTag, out var koreanStyle), Is.True, styleTag.ToString());
                    Assert.That(koreanStyle.FontAsset, Is.SameAs(LoadNanumGothic()));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void MissingBaseRule_FailsValidation()
        {
            var theme = CreateValidTheme();
            theme.SetBaseRules(
                GameplayUiTypographyTheme.CreateDefaultBaseRules()
                    .Where(rule => rule.StyleTag != TypographyStyleTag.HeaderLarge));

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => theme.BuildCache());

                Assert.That(exception.Message, Does.Contain("Missing base typography rule for HeaderLarge"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void MissingLocaleFontSet_FailsValidation()
        {
            var theme = ScriptableObject.CreateInstance<GameplayUiTypographyTheme>();
            theme.SetRequiredLocaleCodes(new[] { "en-US", "ko-KR" });
            theme.SetBaseRules(GameplayUiTypographyTheme.CreateDefaultBaseRules());
            theme.SetLocaleFontSets(new[] { CreateFontSet("en-US", LoadLiberationSans(), false) });

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => theme.BuildCache());

                Assert.That(exception.Message, Does.Contain("Missing locale font set for 'ko-KR'"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void MissingFontCategoryEntry_FailsValidation()
        {
            var theme = ScriptableObject.CreateInstance<GameplayUiTypographyTheme>();
            theme.SetRequiredLocaleCodes(new[] { "en-US", "ko-KR" });
            theme.SetBaseRules(GameplayUiTypographyTheme.CreateDefaultBaseRules());
            theme.SetLocaleFontSets(new[]
            {
                CreateFontSet("en-US", LoadLiberationSans(), false, FontCategory.Display),
                CreateFontSet("ko-KR", LoadNanumGothic(), true),
            });

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => theme.BuildCache());

                Assert.That(exception.Message, Does.Contain("Missing font entry for locale 'en-US', category 'Display'"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void MissingMaterialPreset_FailsValidation()
        {
            var theme = ScriptableObject.CreateInstance<GameplayUiTypographyTheme>();
            theme.SetRequiredLocaleCodes(new[] { "en-US", "ko-KR" });
            theme.SetBaseRules(GameplayUiTypographyTheme.CreateDefaultBaseRules());
            theme.SetLocaleFontSets(new[]
            {
                CreateFontSet("en-US", LoadLiberationSans(), false, nullMaterialCategory: FontCategory.Body),
                CreateFontSet("ko-KR", LoadNanumGothic(), true),
            });

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => theme.BuildCache());

                Assert.That(exception.Message, Does.Contain("Missing material preset for locale 'en-US', category 'Body'"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void LocaleSpecificFontSets_ResolveEnglishAndKoreanIndependently()
        {
            var theme = CreateValidTheme();

            try
            {
                var english = theme.ResolveOrThrow("en-US", TypographyStyleTag.Body);
                var korean = theme.ResolveOrThrow("ko-KR", TypographyStyleTag.Body);

                Assert.That(english.FontAsset, Is.SameAs(LoadLiberationSans()));
                Assert.That(english.MaterialPreset, Is.SameAs(LoadLiberationSans().material));
                Assert.That(english.WeightStrategy, Is.EqualTo(TypographyWeightStrategy.UseFontAsset));
                Assert.That(korean.FontAsset, Is.SameAs(LoadNanumGothic()));
                Assert.That(korean.MaterialPreset, Is.SameAs(LoadNanumGothic().material));
                Assert.That(korean.WeightStrategy, Is.EqualTo(TypographyWeightStrategy.UseFontAsset));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void HybridSizing_PreservesPreserveAuthoredPolicy()
        {
            var theme = CreateValidTheme();

            try
            {
                var style = theme.ResolveOrThrow("en-US", TypographyStyleTag.HeaderLarge);

                Assert.That(style.SizingSource, Is.EqualTo(TypographySizingSource.Hybrid));
                Assert.That(style.SizingMode, Is.EqualTo(TypographySizingMode.PreserveAuthored));
                Assert.That((style.ApplyMask & TypographyApplyMask.Sizing), Is.EqualTo(TypographyApplyMask.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void ApplyMask_IsPreservedInResolvedStyle()
        {
            var theme = CreateValidTheme();

            try
            {
                var style = theme.ResolveOrThrow("ko-KR", TypographyStyleTag.Button);

                Assert.That(style.ApplyMask, Is.EqualTo(
                    TypographyApplyMask.Font |
                    TypographyApplyMask.Material |
                    TypographyApplyMask.FontStyle));
                Assert.That(style.FontStyle, Is.EqualTo(FontStyles.Bold));
                Assert.That(style.WeightStrategy, Is.EqualTo(TypographyWeightStrategy.UseSyntheticBold));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void UtilityCategory_ExistsForLiberationSansResidue()
        {
            var fontSet = CreateFontSet("en-US", LoadLiberationSans(), false);

            Assert.That(Enum.IsDefined(typeof(FontCategory), FontCategory.Utility), Is.True);
            Assert.That(
                fontSet.Entries.Any(entry =>
                    entry.FontCategory == FontCategory.Utility &&
                    entry.Weight == LocalizedTextWeight.Regular &&
                    entry.FontAsset == LoadLiberationSans()),
                Is.True);
        }

        [Test]
        public void LowerUiContracts_DoNotReferenceTmpFontOrMaterialTypes()
        {
            AssertRuntimeSourceDoesNotContain(UiApplicationRuntimePath, "TMP_FontAsset");
            AssertRuntimeSourceDoesNotContain(UiApplicationRuntimePath, "UnityEngine.Material");
            AssertRuntimeSourceDoesNotContain(UiApplicationRuntimePath, "Material ");
            AssertRuntimeSourceDoesNotContain(UiViewSharedRuntimePath, "TMP_FontAsset");
            AssertRuntimeSourceDoesNotContain(UiViewSharedRuntimePath, "UnityEngine.Material");
            AssertRuntimeSourceDoesNotContain(UiViewSharedRuntimePath, "Material ");
        }

        [Test]
        public void ProductionLocalizationResolver_DoesNotDependOnTypographyTheme()
        {
            var source = File.ReadAllText(UnityStringTableTextResolverPath);

            Assert.That(source, Does.Not.Contain(nameof(GameplayUiTypographyTheme)));
            Assert.That(source, Does.Not.Contain(nameof(TypographyThemeResolver)));
            Assert.That(source, Does.Not.Contain(nameof(TypographyStyleTag)));
        }

        private static GameplayUiTypographyTheme CreateValidTheme()
        {
            var theme = ScriptableObject.CreateInstance<GameplayUiTypographyTheme>();
            theme.SetRequiredLocaleCodes(new[] { "en-US", "ko-KR" });
            theme.SetBaseRules(GameplayUiTypographyTheme.CreateDefaultBaseRules());
            theme.SetLocaleFontSets(new[]
            {
                CreateFontSet("en-US", LoadLiberationSans(), false),
                CreateFontSet("ko-KR", LoadNanumGothic(), true),
            });
            return theme;
        }

        private static LocaleFontSet CreateFontSet(
            string localeCode,
            TMP_FontAsset fontAsset,
            bool useSyntheticBold,
            FontCategory? missingCategory = null,
            FontCategory? nullMaterialCategory = null)
        {
            var material = fontAsset.material;
            var entries = new List<LocaleFontEntry>
            {
                CreateEntry(
                    FontCategory.Display,
                    LocalizedTextWeight.Bold,
                    fontAsset,
                    missingCategory,
                    nullMaterialCategory,
                    material,
                    useSyntheticBold),
                CreateEntry(
                    FontCategory.Heading,
                    LocalizedTextWeight.Bold,
                    fontAsset,
                    missingCategory,
                    nullMaterialCategory,
                    material,
                    useSyntheticBold),
                CreateEntry(
                    FontCategory.Body,
                    LocalizedTextWeight.Regular,
                    fontAsset,
                    missingCategory,
                    nullMaterialCategory,
                    material,
                    false),
                CreateEntry(
                    FontCategory.UI,
                    LocalizedTextWeight.Regular,
                    fontAsset,
                    missingCategory,
                    nullMaterialCategory,
                    material,
                    false),
                CreateEntry(
                    FontCategory.UI,
                    LocalizedTextWeight.Bold,
                    fontAsset,
                    missingCategory,
                    nullMaterialCategory,
                    material,
                    useSyntheticBold),
                CreateEntry(
                    FontCategory.Utility,
                    LocalizedTextWeight.Regular,
                    fontAsset,
                    missingCategory,
                    nullMaterialCategory,
                    material,
                    false),
                CreateEntry(
                    FontCategory.Symbol,
                    LocalizedTextWeight.Regular,
                    fontAsset,
                    missingCategory,
                    nullMaterialCategory,
                    material,
                    false),
            };

            return new LocaleFontSet(localeCode, entries.Where(entry => entry != null));
        }

        private static LocaleFontEntry CreateEntry(
            FontCategory fontCategory,
            LocalizedTextWeight weight,
            TMP_FontAsset fontAsset,
            FontCategory? missingCategory,
            FontCategory? nullMaterialCategory,
            Material material,
            bool syntheticBold)
        {
            if (missingCategory.HasValue && missingCategory.Value == fontCategory)
            {
                return null;
            }

            return new LocaleFontEntry(
                fontCategory,
                weight,
                fontAsset,
                nullMaterialCategory.HasValue && nullMaterialCategory.Value == fontCategory ? null : material,
                syntheticBold ? TypographyWeightStrategy.UseSyntheticBold : TypographyWeightStrategy.UseFontAsset);
        }

        private static TMP_FontAsset LoadLiberationSans()
        {
            return LoadFont(LiberationSansFontAssetPath);
        }

        private static TMP_FontAsset LoadNanumGothic()
        {
            return LoadFont(NanumGothicFontValidationUtility.FontAssetPath);
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

        private static TMP_FontAsset LoadFont(string assetPath)
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            Assert.That(fontAsset, Is.Not.Null, $"Missing TMP font asset at {assetPath}.");
            Assert.That(fontAsset.material, Is.Not.Null, $"Missing TMP material preset for {assetPath}.");
            return fontAsset;
        }

        private static void AssertRuntimeSourceDoesNotContain(string rootPath, string token)
        {
            var hits = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(hits, Is.Empty, $"{token} leaked into {rootPath}: {string.Join(", ", hits)}");
        }
    }
}

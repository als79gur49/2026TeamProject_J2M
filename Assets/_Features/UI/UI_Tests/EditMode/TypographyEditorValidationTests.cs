using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Composition.Editor;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class TypographyEditorValidationTests
    {
        private const string LiberationSansFontAssetPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string TmpSettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [Test]
        public void TypographyThemeValidator_DetectsMissingLocaleFontSet()
        {
            var theme = CreateTheme(fontSets: new[] { CreateFontSet("en-US", LoadLiberationSans()) });

            try
            {
                var report = TypographyThemeValidator.ValidateTheme(theme, "TestTheme");

                Assert.That(report.HasErrors, Is.True);
                Assert.That(report.Issues.Select(issue => issue.Message), Has.Some.Contains("Required locale 'ko-KR' has no LocaleFontSet"));
            }
            finally
            {
                Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void TypographyThemeValidator_DetectsMissingMaterialPreset()
        {
            var theme = CreateTheme(
                fontSets: new[]
                {
                    CreateFontSet("en-US", LoadLiberationSans(), nullMaterialCategory: FontCategory.Body),
                    CreateFontSet("ko-KR", UiTestPrefabAssetUtility.LoadNanumGothicFont()),
                });

            try
            {
                var report = TypographyThemeValidator.ValidateTheme(theme, "TestTheme");

                Assert.That(report.HasErrors, Is.True);
                Assert.That(report.Issues.Select(issue => issue.Message), Has.Some.Contains("Missing material preset for locale 'en-US', category 'Body'"));
            }
            finally
            {
                Object.DestroyImmediate(theme);
            }
        }

        [Test]
        public void TypographyBindingValidator_DetectsMissingSerializedTarget()
        {
            var root = new GameObject("TypographyBindingMissingTarget");
            root.AddComponent<TypographyBinding>();

            try
            {
                var report = TypographyBindingValidator.ValidateRoot(
                    root,
                    "TestRoot",
                    TypographyThemeValidator.FindThemeAsset());

                Assert.That(report.HasErrors, Is.True);
                Assert.That(report.Issues.Select(issue => issue.Message), Has.Some.Contains("TypographyBinding.target is not assigned"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyBindingValidator_ValidatesSettingsPrefab()
        {
            AssertPrefabHasNoValidationErrors(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
        }

        [Test]
        public void TypographyBindingValidator_ValidatesPausePrefab()
        {
            AssertPrefabHasNoValidationErrors(UiTestPrefabAssetUtility.PausePopupPrefabPath);
        }

        [Test]
        public void TypographyBindingValidator_ValidatesMainMenuPrefab()
        {
            AssertPrefabHasNoValidationErrors(UiTestPrefabAssetUtility.MainMenuScreenPrefabPath);
        }

        [Test]
        public void TypographyPreviewUtility_PreviewsPrefabAssetWithoutDirtyingAsset()
        {
            var theme = TypographyThemeValidator.FindThemeAsset();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiTestPrefabAssetUtility.MainMenuScreenPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(EditorUtility.IsDirty(prefab), Is.False);

            var result = TypographyPreviewUtility.ApplyPreviewToPrefabAsset(
                UiTestPrefabAssetUtility.MainMenuScreenPrefabPath,
                "ko-KR",
                theme);

            Assert.That(result.HasErrors, Is.False, string.Join("; ", result.Errors));
            Assert.That(result.AppliedCount, Is.GreaterThan(0));
            Assert.That(EditorUtility.IsDirty(prefab), Is.False);
        }

        [Test]
        public void TypographyPreviewUtility_PreservesHybridAuthoredSizingAndRestoresInstance()
        {
            var theme = TypographyThemeValidator.FindThemeAsset();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var root = Object.Instantiate(prefab);
            var binding = root
                .GetComponentsInChildren<TypographyBinding>(true)
                .First(candidate => candidate.StyleTag == TypographyStyleTag.HeaderLarge);
            var target = binding.Target;
            var originalFont = target.font;
            var originalMaterial = target.fontSharedMaterial;
            var originalFontSize = target.fontSize;
            var originalAutoSizing = target.enableAutoSizing;
            var originalMin = target.fontSizeMin;
            var originalMax = target.fontSizeMax;

            try
            {
                var result = TypographyPreviewUtility.ApplyPreview(root, "ko-KR", theme, recordUndo: false);

                Assert.That(result.HasErrors, Is.False, string.Join("; ", result.Errors));
                Assert.That(target.font, Is.SameAs(theme.ResolveOrThrow("ko-KR", TypographyStyleTag.HeaderLarge).FontAsset));
                Assert.That(target.fontSharedMaterial, Is.SameAs(theme.ResolveOrThrow("ko-KR", TypographyStyleTag.HeaderLarge).MaterialPreset));
                Assert.That(target.fontSize, Is.EqualTo(originalFontSize));
                Assert.That(target.enableAutoSizing, Is.EqualTo(originalAutoSizing));
                Assert.That(target.fontSizeMin, Is.EqualTo(originalMin));
                Assert.That(target.fontSizeMax, Is.EqualTo(originalMax));

                Assert.That(TypographyPreviewUtility.RestorePreview(root, recordUndo: false), Is.GreaterThan(0));
                Assert.That(target.font, Is.SameAs(originalFont));
                Assert.That(target.fontSharedMaterial, Is.SameAs(originalMaterial));
                Assert.That(target.fontSize, Is.EqualTo(originalFontSize));
                Assert.That(target.enableAutoSizing, Is.EqualTo(originalAutoSizing));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyPreviewScreenshotUtility_ResolvesRequiredTargetsAndFileNames()
        {
            var targets = TypographyPreviewScreenshotUtility.RequiredTargets;

            Assert.That(targets.Select(target => target.PrefabPath), Is.EquivalentTo(TypographyBindingValidator.RequiredPrefabPaths));
            foreach (var target in targets)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(target.PrefabPath), Is.Not.Null, target.PrefabPath);
            }

            var fileNames = targets
                .SelectMany(target => TypographyThemeValidator.RequiredLocaleCodes.Select(locale =>
                    TypographyPreviewScreenshotUtility.BuildFileName(target, locale)))
                .ToArray();

            Assert.That(fileNames, Does.Contain("Settings_en-US.png"));
            Assert.That(fileNames, Does.Contain("Settings_ko-KR.png"));
            Assert.That(fileNames, Does.Contain("Pause_en-US.png"));
            Assert.That(fileNames, Does.Contain("Pause_ko-KR.png"));
            Assert.That(fileNames, Does.Contain("MainMenu_en-US.png"));
            Assert.That(fileNames, Does.Contain("MainMenu_ko-KR.png"));
        }

        [Test]
        public void TypographyPreviewScreenshotUtility_CreatesRequiredScreenshotsWithoutDirtyingGuardedAssets()
        {
            var outputDirectory = Path.Combine(
                "Temp",
                "TypographyPreviewScreenshotTests",
                "TestRun-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture));

            var guardedAssets = new[]
            {
                UiTestPrefabAssetUtility.SettingsScreenPrefabPath,
                UiTestPrefabAssetUtility.PausePopupPrefabPath,
                UiTestPrefabAssetUtility.MainMenuScreenPrefabPath,
                UiTestPrefabAssetUtility.NanumGothicFontAssetPath,
                TmpSettingsAssetPath,
            };

            AssertGuardedAssetsAreClean(guardedAssets);

            TypographyPreviewScreenshotBatchResult result;
            var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                result = TypographyPreviewScreenshotUtility.CaptureRequiredScreenshots(
                    outputDirectory,
                    new TypographyPreviewScreenshotOptions
                    {
                        Width = 960,
                        Height = 540,
                    });
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            TestContext.WriteLine("Typography screenshot output: " + result.OutputDirectory);

            Assert.That(result.HasErrors, Is.False, string.Join("; ", result.Errors.Concat(result.Captures.SelectMany(capture => capture.Errors))));
            Assert.That(result.Captures, Has.Count.EqualTo(6));

            foreach (var capture in result.Captures)
            {
                Assert.That(File.Exists(capture.FilePath), Is.True, capture.FilePath);
                Assert.That(new FileInfo(capture.FilePath).Length, Is.GreaterThan(0), capture.FilePath);
                Assert.That(capture.AppliedBindingCount, Is.GreaterThan(0), capture.FilePath);
                Assert.That(capture.LocalizedTextAppliedCount, Is.EqualTo(ExpectedLocalizedTextCount(capture.Target.FileStem)), capture.FilePath);

                var texture = new Texture2D(2, 2);
                try
                {
                    Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(capture.FilePath)), Is.True, capture.FilePath);
                    Assert.That(texture.width, Is.EqualTo(960), capture.FilePath);
                    Assert.That(texture.height, Is.EqualTo(540), capture.FilePath);
                    if (IsGraphicsCaptureAvailable())
                    {
                        AssertTextureIsNonBlank(texture, capture.FilePath);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }

            AssertGuardedAssetsAreClean(guardedAssets);

            Directory.Delete(result.OutputDirectory, recursive: true);
        }

        [Test]
        public void TypographyPreviewScreenshotUtility_CapturesLeftRightOrientationWithoutHorizontalMirror()
        {
            var root = new GameObject("TypographyOrientationRoot", typeof(RectTransform));
            var rectTransform = root.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(320f, 180f);

            if (!IsGraphicsCaptureAvailable())
            {
                Assert.Pass("Graphics readback orientation validation is skipped because the Unity UI lane runs with -nographics.");
            }

            var redTexture = CreateSolidTexture(Color.red);
            var blueTexture = CreateSolidTexture(Color.blue);
            CreateOrientationMarker(root.transform, "LeftRedMarker", new Vector2(0f, 0f), new Vector2(0.5f, 1f), redTexture);
            CreateOrientationMarker(root.transform, "RightBlueMarker", new Vector2(0.5f, 0f), new Vector2(1f, 1f), blueTexture);

            Texture2D texture = null;
            try
            {
                texture = TypographyPreviewScreenshotUtility.CaptureRootForValidation(
                    root,
                    new TypographyPreviewScreenshotOptions
                    {
                        Width = 320,
                        Height = 180,
                        BackgroundColor = Color.black,
                    });

                AssertDominantColor(texture.GetPixel(80, 90), Color.red, "left sample should be red");
                AssertDominantColor(texture.GetPixel(240, 90), Color.blue, "right sample should be blue");
            }
            finally
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }

                Object.DestroyImmediate(root);
                Object.DestroyImmediate(redTexture);
                Object.DestroyImmediate(blueTexture);
            }
        }

        [Test]
        public void TypographyPreviewScreenshotUtility_ValidatesTargetsBeforeRendering()
        {
            var outputDirectory = Path.Combine(
                "Temp",
                "TypographyPreviewScreenshotValidation",
                System.Guid.NewGuid().ToString("N"));
            var missingTarget = new TypographyPreviewScreenshotTarget(
                "Missing",
                "Missing",
                "Assets/_Features/UI/UI_Screens/Prefabs/MissingTypographyScreenshotTarget.prefab");

            var result = TypographyPreviewScreenshotUtility.CaptureScreenshots(
                new[] { missingTarget },
                TypographyThemeValidator.RequiredLocaleCodes,
                outputDirectory);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Captures, Is.Empty);
            Assert.That(Directory.Exists(outputDirectory), Is.False);
            Assert.That(result.Errors, Has.Some.Contains("Prefab asset was not found"));
        }

        private static void AssertPrefabHasNoValidationErrors(string prefabPath)
        {
            var report = TypographyBindingValidator.ValidatePrefabAtPath(
                prefabPath,
                TypographyThemeValidator.FindThemeAsset());

            Assert.That(report.HasErrors, Is.False, string.Join("; ", report.Issues.Select(issue => issue.ToString())));
        }

        private static void AssertGuardedAssetsAreClean(IEnumerable<string> assetPaths)
        {
            foreach (var assetPath in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                Assert.That(asset, Is.Not.Null, assetPath);
                Assert.That(EditorUtility.IsDirty(asset), Is.False, assetPath);
            }
        }

        private static int ExpectedLocalizedTextCount(string fileStem)
        {
            switch (fileStem)
            {
                case "Settings":
                    return 11;

                case "Pause":
                    return 6;

                case "MainMenu":
                    return 3;

                default:
                    Assert.Fail("Unexpected screenshot target: " + fileStem);
                    return 0;
            }
        }

        private static bool IsGraphicsCaptureAvailable()
        {
            return SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        }

        private static void AssertTextureIsNonBlank(Texture2D texture, string context)
        {
            var pixels = texture.GetPixels32();
            Assert.That(pixels.Length, Is.GreaterThan(0), context);
            var first = pixels[0];
            Assert.That(
                pixels.Any(pixel => !pixel.Equals(first)),
                Is.True,
                $"{context} should not be a single-color image. First pixel RGBA=({first.r},{first.g},{first.b},{first.a}).");
        }

        private static void CreateOrientationMarker(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Texture texture)
        {
            var marker = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            marker.transform.SetParent(parent, false);
            var markerRect = marker.GetComponent<RectTransform>();
            markerRect.anchorMin = anchorMin;
            markerRect.anchorMax = anchorMax;
            markerRect.offsetMin = Vector2.zero;
            markerRect.offsetMax = Vector2.zero;
            marker.GetComponent<RawImage>().texture = texture;
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void AssertDominantColor(Color actual, Color expected, string context)
        {
            const float dominantThreshold = 0.85f;
            const float quietThreshold = 0.15f;
            var actualMessage = $"{context}. Actual RGBA=({actual.r:F3},{actual.g:F3},{actual.b:F3},{actual.a:F3})";
            if (expected == Color.red)
            {
                Assert.That(actual.r, Is.GreaterThan(dominantThreshold), actualMessage);
                Assert.That(actual.g, Is.LessThan(quietThreshold), actualMessage);
                Assert.That(actual.b, Is.LessThan(quietThreshold), actualMessage);
                return;
            }

            if (expected == Color.blue)
            {
                Assert.That(actual.b, Is.GreaterThan(dominantThreshold), actualMessage);
                Assert.That(actual.r, Is.LessThan(quietThreshold), actualMessage);
                Assert.That(actual.g, Is.LessThan(quietThreshold), actualMessage);
            }
        }

        private static GameplayUiTypographyTheme CreateTheme(IEnumerable<LocaleFontSet> fontSets)
        {
            var theme = ScriptableObject.CreateInstance<GameplayUiTypographyTheme>();
            theme.SetRequiredLocaleCodes(TypographyThemeValidator.RequiredLocaleCodes);
            theme.SetBaseRules(GameplayUiTypographyTheme.CreateDefaultBaseRules());
            theme.SetLocaleFontSets(fontSets);
            return theme;
        }

        private static LocaleFontSet CreateFontSet(
            string localeCode,
            TMP_FontAsset fontAsset,
            FontCategory? nullMaterialCategory = null)
        {
            var material = fontAsset.material;
            return new LocaleFontSet(
                localeCode,
                new[]
                {
                    CreateEntry(FontCategory.Display, LocalizedTextWeight.Bold, fontAsset, material, nullMaterialCategory),
                    CreateEntry(FontCategory.Heading, LocalizedTextWeight.Bold, fontAsset, material, nullMaterialCategory),
                    CreateEntry(FontCategory.Body, LocalizedTextWeight.Regular, fontAsset, material, nullMaterialCategory),
                    CreateEntry(FontCategory.UI, LocalizedTextWeight.Regular, fontAsset, material, nullMaterialCategory),
                    CreateEntry(FontCategory.UI, LocalizedTextWeight.Bold, fontAsset, material, nullMaterialCategory),
                    CreateEntry(FontCategory.Utility, LocalizedTextWeight.Regular, fontAsset, material, nullMaterialCategory),
                    CreateEntry(FontCategory.Symbol, LocalizedTextWeight.Regular, fontAsset, material, nullMaterialCategory),
                });
        }

        private static LocaleFontEntry CreateEntry(
            FontCategory fontCategory,
            LocalizedTextWeight weight,
            TMP_FontAsset fontAsset,
            Material material,
            FontCategory? nullMaterialCategory)
        {
            return new LocaleFontEntry(
                fontCategory,
                weight,
                fontAsset,
                nullMaterialCategory.HasValue && nullMaterialCategory.Value == fontCategory ? null : material,
                weight == LocalizedTextWeight.Bold
                    ? TypographyWeightStrategy.UseSyntheticBold
                    : TypographyWeightStrategy.UseFontAsset);
        }

        private static TMP_FontAsset LoadLiberationSans()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationSansFontAssetPath);
            Assert.That(font, Is.Not.Null, LiberationSansFontAssetPath);
            return font;
        }
    }
}

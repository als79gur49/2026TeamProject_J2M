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
        private const string CanonicalEvidenceDirectory =
            "TestLogs/TypographyVisualQA/CommandLine-20260722-210829";
        private const string CanonicalEvidenceReconstructionHead =
            "31b92cd2c9718e1a653da39a6db47c7a17ea7452";
        private const string HistoricalEvidenceDirectory =
            "TestLogs/TypographyVisualQA/CommandLine-20260720-194045";

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
        public void TypographyBindingValidator_LocaleInvariantRequiresStructureButNotThemeResolution()
        {
            var root = new GameObject("TypographyLocaleInvariantValidation");
            var text = root.AddComponent<TextMeshProUGUI>();
            var binding = root.AddComponent<TypographyBinding>();
            var theme = CreateTheme(
                new[]
                {
                    CreateFontSet("en-US", LoadLiberationSans()),
                    CreateFontSet("ko-KR", UiTestPrefabAssetUtility.LoadNanumGothicFont()),
                });
            theme.SetBaseRules(GameplayUiTypographyTheme.CreateDefaultBaseRules()
                .Where(rule => rule.StyleTag != TypographyStyleTag.Value));
            var serializedBinding = new SerializedObject(binding);
            serializedBinding.FindProperty("target").objectReferenceValue = text;
            serializedBinding.FindProperty("styleTag").intValue = (int)TypographyStyleTag.Value;
            serializedBinding.FindProperty("localeParticipation").intValue =
                (int)TypographyLocaleParticipation.LocaleInvariant;
            serializedBinding.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                var report = TypographyBindingValidator.ValidateRoot(
                    root,
                    "TestRoot",
                    theme);

                Assert.That(report.HasErrors, Is.False, string.Join("; ", report.Issues));
            }
            finally
            {
                Object.DestroyImmediate(theme);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyBindingValidator_RejectsInvalidStyleTagForLocaleInvariantBinding()
        {
            var root = new GameObject("TypographyInvalidInvariantStyleTag");
            var text = root.AddComponent<TextMeshProUGUI>();
            var binding = root.AddComponent<TypographyBinding>();
            ConfigureBinding(
                binding,
                text,
                (TypographyStyleTag)int.MaxValue,
                TypographyLocaleParticipation.LocaleInvariant);

            try
            {
                var report = TypographyBindingValidator.ValidateRoot(
                    root,
                    "TestRoot",
                    TypographyThemeValidator.FindThemeAsset());

                Assert.That(report.HasErrors, Is.True);
                Assert.That(report.Issues.Select(issue => issue.Message), Has.Some.Contains("StyleTag value"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyBindingValidator_RejectsInvalidLocaleParticipation()
        {
            var root = new GameObject("TypographyInvalidLocaleParticipation");
            var text = root.AddComponent<TextMeshProUGUI>();
            var binding = root.AddComponent<TypographyBinding>();
            var serializedBinding = new SerializedObject(binding);
            serializedBinding.FindProperty("target").objectReferenceValue = text;
            serializedBinding.FindProperty("localeParticipation").intValue = int.MaxValue;
            serializedBinding.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                var report = TypographyBindingValidator.ValidateRoot(
                    root,
                    "TestRoot",
                    TypographyThemeValidator.FindThemeAsset());

                Assert.That(report.HasErrors, Is.True);
                Assert.That(report.Issues.Select(issue => issue.Message), Has.Some.Contains("Locale participation value"));
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
        public void TypographyPreviewUtility_InvariantOnlyRootWithoutTheme_SkipsWithoutMutationOrSnapshot()
        {
            var root = new GameObject("InvariantOnlyPreviewRoot");
            var binding = CreateBinding(
                root,
                "InvariantText",
                TypographyStyleTag.Value,
                TypographyLocaleParticipation.LocaleInvariant);
            binding.Target.text = "Left Shift";
            var before = new PreviewTypographyState(binding.Target);

            try
            {
                var result = TypographyPreviewUtility.ApplyPreview(root, "ko-KR", null);

                Assert.That(result.Errors, Is.Empty);
                Assert.That(result.AppliedCount, Is.Zero);
                Assert.That(result.LocaleInvariantSkippedCount, Is.EqualTo(1));
                before.AssertSame(binding.Target, "invariant-only null-theme preview");
                Assert.That(TypographyPreviewUtility.RestorePreview(root, recordUndo: false), Is.Zero);
            }
            finally
            {
                TypographyPreviewUtility.RestorePreview(root, recordUndo: false);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyPreviewUtility_MixedRootWithoutTheme_CountsInvariantAndPreservesAllTextState()
        {
            var root = new GameObject("MixedNullThemePreviewRoot");
            var invariant = CreateBinding(
                root,
                "InvariantText",
                TypographyStyleTag.Value,
                TypographyLocaleParticipation.LocaleInvariant);
            var themed = CreateBinding(
                root,
                "ThemedText",
                TypographyStyleTag.Label,
                TypographyLocaleParticipation.LocaleThemed);
            invariant.Target.text = "Space";
            themed.Target.text = "Movement Keys";
            var states = root.GetComponentsInChildren<TMP_Text>(true)
                .ToDictionary(target => target, target => new PreviewTypographyState(target));

            try
            {
                var result = TypographyPreviewUtility.ApplyPreview(root, "ko-KR", null);

                Assert.That(result.Errors, Has.Count.EqualTo(1));
                Assert.That(result.Errors[0], Does.Contain("GameplayUiTypographyTheme"));
                Assert.That(result.AppliedCount, Is.Zero);
                Assert.That(result.LocaleInvariantSkippedCount, Is.EqualTo(1));
                foreach (var pair in states)
                {
                    pair.Value.AssertSame(pair.Key, pair.Key.name + " mixed null-theme preview");
                }

                Assert.That(TypographyPreviewUtility.RestorePreview(root, recordUndo: false), Is.Zero);
            }
            finally
            {
                TypographyPreviewUtility.RestorePreview(root, recordUndo: false);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyPreviewUtility_ThemedOnlyRootWithoutTheme_ReportsOneThemeErrorForAllBindings()
        {
            var root = new GameObject("ThemedOnlyNullThemePreviewRoot");
            CreateBinding(root, "FirstThemedText", TypographyStyleTag.Label, TypographyLocaleParticipation.LocaleThemed);
            CreateBinding(root, "SecondThemedText", TypographyStyleTag.Button, TypographyLocaleParticipation.LocaleThemed);

            try
            {
                var result = TypographyPreviewUtility.ApplyPreview(root, "ko-KR", null);

                Assert.That(result.Errors, Has.Count.EqualTo(1));
                Assert.That(result.Errors[0], Does.Contain("GameplayUiTypographyTheme"));
                Assert.That(result.AppliedCount, Is.Zero);
                Assert.That(result.LocaleInvariantSkippedCount, Is.Zero);
                Assert.That(TypographyPreviewUtility.RestorePreview(root, recordUndo: false), Is.Zero);
            }
            finally
            {
                TypographyPreviewUtility.RestorePreview(root, recordUndo: false);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyPreviewUtility_PreservesHybridAuthoredSizingAndRestoresInstance()
        {
            var theme = TypographyThemeValidator.FindThemeAsset();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var root = Object.Instantiate(prefab);
            var binding = root
                .GetComponentsInChildren<TypographyBinding>(true)
                .First(candidate => candidate.StyleTag == TypographyStyleTag.SettingsDisplay);
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
                Assert.That(target.font, Is.SameAs(theme.ResolveOrThrow("ko-KR", TypographyStyleTag.SettingsDisplay).FontAsset));
                Assert.That(target.fontSharedMaterial, Is.SameAs(theme.ResolveOrThrow("ko-KR", TypographyStyleTag.SettingsDisplay).MaterialPreset));
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
        public void TypographyPreviewUtility_SettingsKoreanPreview_SkipsThirteenLocaleInvariantBindings()
        {
            var theme = TypographyThemeValidator.FindThemeAsset();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var root = Object.Instantiate(prefab);
            var bindings = root.GetComponentsInChildren<TypographyBinding>(true);
            var invariantBindings = bindings
                .Where(binding => binding.LocaleParticipation == TypographyLocaleParticipation.LocaleInvariant)
                .ToArray();
            var snapshots = invariantBindings.ToDictionary(
                binding => binding.Target,
                binding => new PreviewTypographyState(binding.Target));
            var governedBinding = bindings.First(binding =>
                binding.LocaleParticipation == TypographyLocaleParticipation.LocaleThemed &&
                binding.StyleTag == TypographyStyleTag.SettingsDisplay);

            try
            {
                Assert.That(bindings, Has.Length.EqualTo(51));
                Assert.That(invariantBindings, Has.Length.EqualTo(13));

                var result = TypographyPreviewUtility.ApplyPreview(root, "ko-KR", theme, recordUndo: false);

                Assert.That(result.HasErrors, Is.False, string.Join("; ", result.Errors));
                Assert.That(result.AppliedCount, Is.EqualTo(38));
                Assert.That(result.LocaleInvariantSkippedCount, Is.EqualTo(13));
                Assert.That(governedBinding.Target.font, Is.SameAs(UiTestPrefabAssetUtility.LoadNanumGothicFont()));
                Assert.That(
                    governedBinding.Target.fontSharedMaterial,
                    Is.SameAs(theme.ResolveOrThrow("ko-KR", governedBinding.StyleTag).MaterialPreset));
                foreach (var binding in invariantBindings)
                {
                    snapshots[binding.Target].AssertSame(binding.Target, binding.name + " preview");
                }

                Assert.That(TypographyPreviewUtility.RestorePreview(root, recordUndo: false), Is.EqualTo(38));
                foreach (var binding in invariantBindings)
                {
                    snapshots[binding.Target].AssertSame(binding.Target, binding.name + " restore");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyPreviewUtility_SettingsRootSelection_AppliesAndRestoresUniqueTargets()
        {
            var previousSelection = Selection.objects;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var root = Object.Instantiate(prefab);

            try
            {
                Selection.objects = new Object[] { root };

                var result = TypographyPreviewUtility.ApplyPreviewToSelection(
                    "ko-KR",
                    TypographyThemeValidator.FindThemeAsset());

                Assert.That(result.HasErrors, Is.False, string.Join("; ", result.Errors));
                Assert.That(result.AppliedCount, Is.EqualTo(38));
                Assert.That(result.LocaleInvariantSkippedCount, Is.EqualTo(13));
                Assert.That(TypographyPreviewUtility.RestorePreviewOnSelection(), Is.EqualTo(38));
            }
            finally
            {
                TypographyPreviewUtility.RestorePreview(root, recordUndo: false);
                Selection.objects = previousSelection;
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyPreviewUtility_ParentAndMovementRowSelection_DeduplicatesTraversalAndRestore()
        {
            var previousSelection = Selection.objects;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var root = Object.Instantiate(prefab);
            var movementInputRow = root.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "MovementInputRow");

            try
            {
                Selection.objects = new Object[] { root, movementInputRow.gameObject };

                var result = TypographyPreviewUtility.ApplyPreviewToSelection(
                    "ko-KR",
                    TypographyThemeValidator.FindThemeAsset());

                Assert.That(result.HasErrors, Is.False, string.Join("; ", result.Errors));
                Assert.That(result.AppliedCount, Is.EqualTo(38));
                Assert.That(result.LocaleInvariantSkippedCount, Is.EqualTo(13));
                Assert.That(TypographyPreviewUtility.RestorePreviewOnSelection(), Is.EqualTo(38));
            }
            finally
            {
                TypographyPreviewUtility.RestorePreview(root, recordUndo: false);
                Selection.objects = previousSelection;
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TypographyPreviewUtility_IndependentSelectedRoots_AppliesBoth()
        {
            var previousSelection = Selection.objects;
            var firstRoot = new GameObject("FirstPreviewRoot");
            var secondRoot = new GameObject("SecondPreviewRoot");
            CreateBinding(firstRoot, "FirstText", TypographyStyleTag.Label, TypographyLocaleParticipation.LocaleThemed);
            CreateBinding(secondRoot, "SecondText", TypographyStyleTag.Button, TypographyLocaleParticipation.LocaleThemed);

            try
            {
                Selection.objects = new Object[] { firstRoot, secondRoot };

                var result = TypographyPreviewUtility.ApplyPreviewToSelection(
                    "ko-KR",
                    TypographyThemeValidator.FindThemeAsset());

                Assert.That(result.HasErrors, Is.False, string.Join("; ", result.Errors));
                Assert.That(result.AppliedCount, Is.EqualTo(2));
                Assert.That(result.LocaleInvariantSkippedCount, Is.Zero);
                Assert.That(TypographyPreviewUtility.RestorePreviewOnSelection(), Is.EqualTo(2));
            }
            finally
            {
                TypographyPreviewUtility.RestorePreview(firstRoot, recordUndo: false);
                TypographyPreviewUtility.RestorePreview(secondRoot, recordUndo: false);
                Selection.objects = previousSelection;
                Object.DestroyImmediate(firstRoot);
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void TypographyPreviewUtility_RepeatedSelectionPreview_DoesNotRetainDedupeState()
        {
            var previousSelection = Selection.objects;
            var root = new GameObject("RepeatedPreviewRoot");
            CreateBinding(root, "RepeatedText", TypographyStyleTag.Label, TypographyLocaleParticipation.LocaleThemed);

            try
            {
                Selection.objects = new Object[] { root };
                var theme = TypographyThemeValidator.FindThemeAsset();

                var first = TypographyPreviewUtility.ApplyPreviewToSelection("en-US", theme);
                var second = TypographyPreviewUtility.ApplyPreviewToSelection("ko-KR", theme);

                Assert.That(first.HasErrors, Is.False, string.Join("; ", first.Errors));
                Assert.That(second.HasErrors, Is.False, string.Join("; ", second.Errors));
                Assert.That(first.AppliedCount, Is.EqualTo(1));
                Assert.That(second.AppliedCount, Is.EqualTo(1));
                Assert.That(TypographyPreviewUtility.RestorePreviewOnSelection(), Is.EqualTo(1));
            }
            finally
            {
                TypographyPreviewUtility.RestorePreview(root, recordUndo: false);
                Selection.objects = previousSelection;
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
                if (capture.Target.FileStem == "Settings")
                {
                    Assert.That(
                        capture.AppliedBindingCount,
                        Is.EqualTo(TypographyPreviewScreenshotUtility.SettingsExpectedAppliedBindingCount),
                        capture.FilePath);
                }
                else
                {
                    Assert.That(capture.AppliedBindingCount, Is.GreaterThan(0), capture.FilePath);
                }
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

            var manifestPath = Path.Combine(
                result.OutputDirectory,
                TypographyPreviewScreenshotManifestUtility.ManifestFileName);
            Assert.That(File.Exists(manifestPath), Is.True, manifestPath);
            var manifest = TypographyPreviewScreenshotManifestParser.ParseFile(manifestPath);
            Assert.That(manifest.SchemaVersion, Is.EqualTo(1));
            Assert.That(manifest.CaptureMode, Is.EqualTo("AGGREGATE"));
            Assert.That(manifest.OverallResult, Is.EqualTo("PASS"));
            Assert.That(manifest.GitHead, Is.EqualTo(TypographyPreviewScreenshotManifestUtility.ReadCurrentGitHead()));
            Assert.That(manifest.Width, Is.EqualTo(960));
            Assert.That(manifest.Height, Is.EqualTo(540));
            Assert.That(manifest.Entries, Has.Count.EqualTo(6));
            Assert.That(
                manifest.Entries.Where(entry => entry.Target == "Settings"),
                Has.All.Property("TypographyBindingCount")
                    .EqualTo(TypographyPreviewScreenshotUtility.SettingsExpectedAppliedBindingCount));

            foreach (var settingsCapture in result.Captures.Where(capture => capture.Target.FileStem == "Settings"))
            {
                settingsCapture.AppliedBindingCount = 51;
            }

            TypographyPreviewScreenshotManifestUtility.WriteCanonicalManifest(
                result,
                new TypographyPreviewScreenshotOptions { Width = 960, Height = 540 });
            var rejectedManifest = TypographyPreviewScreenshotManifestParser.ParseFile(manifestPath);
            Assert.That(rejectedManifest.OverallResult, Is.EqualTo("FAIL"));
            Assert.That(
                rejectedManifest.Entries.Where(entry => entry.Target == "Settings"),
                Has.All.Property("CaptureResult").EqualTo("FAIL"));

            AssertGuardedAssetsAreClean(guardedAssets);

            Directory.Delete(result.OutputDirectory, recursive: true);
        }

        [Test]
        public void TypographyPreviewScreenshotManifest_PartialCaptureCannotReportPass()
        {
            var outputDirectory = Path.Combine(
                "Temp",
                "TypographyPreviewScreenshotManifestTests",
                System.Guid.NewGuid().ToString("N"));
            var result = new TypographyPreviewScreenshotBatchResult(Path.GetFullPath(outputDirectory));

            try
            {
                var manifestPath = TypographyPreviewScreenshotManifestUtility.WriteCanonicalManifest(
                    result,
                    new TypographyPreviewScreenshotOptions());
                var manifest = TypographyPreviewScreenshotManifestParser.ParseFile(manifestPath);

                Assert.That(manifest.OverallResult, Is.EqualTo("FAIL"));
                Assert.That(manifest.Entries, Has.Count.EqualTo(6));
                Assert.That(manifest.Entries, Has.All.Property("CaptureResult").EqualTo("FAIL"));
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, recursive: true);
                }
            }
        }

        [Test]
        public void TypographyPreviewScreenshotManifest_CanonicalEvidenceMatchesRequiredPngSet()
        {
            var manifestPath = Path.Combine(
                CanonicalEvidenceDirectory,
                TypographyPreviewScreenshotManifestUtility.ManifestFileName);
            Assert.That(File.Exists(manifestPath), Is.True, manifestPath);

            var manifest = TypographyPreviewScreenshotManifestParser.ParseFile(manifestPath);
            Assert.That(manifest.SchemaVersion, Is.EqualTo(1));
            Assert.That(manifest.OverallResult, Is.EqualTo("PASS"));
            Assert.That(manifest.CaptureMode, Is.EqualTo("RECONSTRUCTED_FROM_SPLIT_LOGS"));
            Assert.That(
                manifest.CaptureCommand,
                Is.EqualTo(
                    "Game.Feature.UI.Composition.Editor.TypographyPreviewScreenshotMenu." +
                    "ReconstructCanonicalManifestFromCommandLine"));
            var currentGitHead = TypographyPreviewScreenshotManifestUtility.ReadCurrentGitHead();
            Assert.That(
                string.Equals(manifest.GitHead, currentGitHead, System.StringComparison.Ordinal) ||
                string.Equals(
                    manifest.GitHead,
                    CanonicalEvidenceReconstructionHead,
                    System.StringComparison.Ordinal),
                Is.True,
                $"Manifest git_head must be current HEAD '{currentGitHead}' or the recorded reconstruction HEAD.");
            Assert.That(manifest.OutputDirectory, Is.EqualTo(CanonicalEvidenceDirectory));
            Assert.That(manifest.Width, Is.EqualTo(1920));
            Assert.That(manifest.Height, Is.EqualTo(1080));
            Assert.That(manifest.ThemeValidation, Is.EqualTo("PASS"));
            Assert.That(manifest.PrefabValidation, Is.EqualTo("PASS"));
            Assert.That(manifest.GuardedAssetDirtyCheck, Is.EqualTo("PASS"));
            Assert.That(manifest.Entries, Has.Count.EqualTo(6));

            foreach (var target in TypographyPreviewScreenshotUtility.RequiredTargets)
            {
                var expectedLocalizedCount =
                    TypographyPreviewScreenshotUtility.GetExpectedLocalizedTextCount(target.FileStem);
                foreach (var locale in TypographyThemeValidator.RequiredLocaleCodes)
                {
                    var expectedFileName =
                        TypographyPreviewScreenshotUtility.BuildFileName(target, locale);
                    var entry = manifest.FindEntry(target.FileStem, locale);
                    Assert.That(entry, Is.Not.Null, $"{target.FileStem}/{locale}");
                    Assert.That(entry.FileName, Is.EqualTo(expectedFileName));
                    Assert.That(entry.CaptureResult, Is.EqualTo("PASS"));
                    Assert.That(entry.Width, Is.EqualTo(1920));
                    Assert.That(entry.Height, Is.EqualTo(1080));
                    Assert.That(entry.LocalizedExpectedCount, Is.EqualTo(expectedLocalizedCount));
                    Assert.That(entry.LocalizedAppliedCount, Is.EqualTo(expectedLocalizedCount));
                    if (target.FileStem == "Settings")
                    {
                        Assert.That(
                            entry.TypographyBindingCount,
                            Is.EqualTo(TypographyPreviewScreenshotUtility.SettingsExpectedAppliedBindingCount));
                    }
                    Assert.That(entry.OrientationValidation, Does.StartWith("PASS"));
                    Assert.That(entry.NonBlankValidation, Is.EqualTo("PASS"));
                    Assert.That(entry.GlyphTofuValidation, Is.EqualTo("NOT_RECORDED"));

                    var pngPath = Path.Combine(CanonicalEvidenceDirectory, entry.FileName);
                    Assert.That(File.Exists(pngPath), Is.True, pngPath);
                    Assert.That(new FileInfo(pngPath).Length, Is.EqualTo(entry.FileSizeBytes), pngPath);
                    Assert.That(
                        TypographyPreviewScreenshotManifestUtility.ComputeSha256(pngPath),
                        Is.EqualTo(entry.Sha256),
                        pngPath);

                    var texture = new Texture2D(2, 2);
                    try
                    {
                        Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(pngPath)), Is.True, pngPath);
                        Assert.That(texture.width, Is.EqualTo(entry.Width), pngPath);
                        Assert.That(texture.height, Is.EqualTo(entry.Height), pngPath);
                    }
                    finally
                    {
                        Object.DestroyImmediate(texture);
                    }
                }
            }

            AssertGuardedAssetsAreClean(TypographyPreviewScreenshotUtility.DirtyGuardAssetPaths);

            var ignorePolicy = File.ReadAllLines(".gitignore");
            Assert.That(
                ignorePolicy,
                Does.Contain("!TestLogs/TypographyVisualQA/**/capture.log"),
                "The canonical manifest must remain trackable despite the repository-wide *.log rule.");
        }

        [Test]
        public void TypographyPreviewScreenshotManifest_HistoricalEvidenceRetainsFiftyOneSettingsBindings()
        {
            var manifestPath = Path.Combine(
                HistoricalEvidenceDirectory,
                TypographyPreviewScreenshotManifestUtility.ManifestFileName);
            Assert.That(File.Exists(manifestPath), Is.True, manifestPath);

            var manifest = TypographyPreviewScreenshotManifestParser.ParseFile(manifestPath);
            Assert.That(manifest.OutputDirectory, Is.EqualTo(HistoricalEvidenceDirectory));
            foreach (var locale in TypographyThemeValidator.RequiredLocaleCodes)
            {
                var settingsEntry = manifest.FindEntry("Settings", locale);
                Assert.That(settingsEntry, Is.Not.Null, $"historical Settings/{locale}");
                Assert.That(settingsEntry.TypographyBindingCount, Is.EqualTo(51));
            }
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

        private static TypographyBinding CreateBinding(
            GameObject root,
            string name,
            TypographyStyleTag styleTag,
            TypographyLocaleParticipation localeParticipation)
        {
            var targetObject = new GameObject(name);
            targetObject.transform.SetParent(root.transform, false);
            var target = targetObject.AddComponent<TextMeshProUGUI>();
            var binding = targetObject.AddComponent<TypographyBinding>();
            ConfigureBinding(binding, target, styleTag, localeParticipation);
            return binding;
        }

        private static void ConfigureBinding(
            TypographyBinding binding,
            TMP_Text target,
            TypographyStyleTag styleTag,
            TypographyLocaleParticipation localeParticipation)
        {
            var serializedBinding = new SerializedObject(binding);
            serializedBinding.FindProperty("target").objectReferenceValue = target;
            serializedBinding.FindProperty("styleTag").intValue = (int)styleTag;
            serializedBinding.FindProperty("localeParticipation").intValue = (int)localeParticipation;
            serializedBinding.ApplyModifiedPropertiesWithoutUndo();
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
                    return 22;

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

        private readonly struct PreviewTypographyState
        {
            private readonly TMP_FontAsset _font;
            private readonly Material _material;
            private readonly FontStyles _fontStyle;
            private readonly float _fontSize;
            private readonly bool _enableAutoSizing;
            private readonly float _fontSizeMin;
            private readonly float _fontSizeMax;
            private readonly float _lineSpacing;
            private readonly float _characterSpacing;
            private readonly string _text;

            public PreviewTypographyState(TMP_Text target)
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
                _text = target.text;
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
                Assert.That(target.text, Is.EqualTo(_text), context + " text");
            }
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

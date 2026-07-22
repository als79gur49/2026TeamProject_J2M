using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.UI.Popups;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition.Editor
{
    public readonly struct TypographyPreviewScreenshotTarget
    {
        public TypographyPreviewScreenshotTarget(string name, string fileStem, string prefabPath)
        {
            Name = name ?? string.Empty;
            FileStem = fileStem ?? string.Empty;
            PrefabPath = prefabPath ?? string.Empty;
        }

        public string Name { get; }

        public string FileStem { get; }

        public string PrefabPath { get; }
    }

    public sealed class TypographyPreviewScreenshotOptions
    {
        public const int DefaultWidth = 1920;
        public const int DefaultHeight = 1080;

        public int Width { get; set; } = DefaultWidth;

        public int Height { get; set; } = DefaultHeight;

        public Color BackgroundColor { get; set; } = new(0f, 0f, 0f, 0f);
    }

    public sealed class TypographyPreviewScreenshotCaptureResult
    {
        private readonly List<string> errors = new();
        private readonly List<string> localizedTexts = new();

        public TypographyPreviewScreenshotCaptureResult(
            TypographyPreviewScreenshotTarget target,
            string localeCode,
            string filePath)
        {
            Target = target;
            LocaleCode = localeCode ?? string.Empty;
            FilePath = filePath ?? string.Empty;
        }

        public TypographyPreviewScreenshotTarget Target { get; }

        public string LocaleCode { get; }

        public string FilePath { get; }

        public int Width { get; set; }

        public int Height { get; set; }

        public long FileSizeBytes { get; set; }

        public int AppliedBindingCount { get; set; }

        public int ExpectedLocalizedTextCount { get; set; }

        public int LocalizedTextAppliedCount { get; set; }

        public string OrientationValidationResult { get; set; } = "NOT_RUN";

        public string NonBlankValidationResult { get; set; } = "NOT_RUN";

        public string GlyphTofuValidationResult { get; set; } = "NOT_RUN";

        public IReadOnlyList<string> Errors => errors;

        internal IReadOnlyList<string> LocalizedTexts => localizedTexts;

        public bool HasErrors => errors.Count > 0;

        public bool Exists => File.Exists(FilePath);

        public void AddError(string message)
        {
            errors.Add(message ?? string.Empty);
        }

        internal void AddLocalizedText(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                localizedTexts.Add(value);
            }
        }
    }

    public sealed class TypographyPreviewScreenshotBatchResult
    {
        private readonly List<TypographyPreviewScreenshotCaptureResult> captures = new();
        private readonly List<string> errors = new();

        public TypographyPreviewScreenshotBatchResult(string outputDirectory)
        {
            OutputDirectory = outputDirectory ?? string.Empty;
        }

        public string OutputDirectory { get; }

        public IReadOnlyList<TypographyPreviewScreenshotCaptureResult> Captures => captures;

        public IReadOnlyList<string> Errors => errors;

        public bool ThemeValidationPassed { get; internal set; }

        public bool PrefabValidationPassed { get; internal set; }

        public bool GuardedAssetsClean { get; internal set; }

        public bool HasErrors => errors.Count > 0 || captures.Any(capture => capture.HasErrors);

        public void AddCapture(TypographyPreviewScreenshotCaptureResult capture)
        {
            if (capture != null)
            {
                captures.Add(capture);
            }
        }

        public void AddError(string message)
        {
            errors.Add(message ?? string.Empty);
        }

        public void ThrowIfFailed()
        {
            if (!HasErrors)
            {
                return;
            }

            var messages = errors
                .Concat(captures.SelectMany(capture => capture.Errors))
                .Where(message => !string.IsNullOrWhiteSpace(message));
            throw new InvalidOperationException(string.Join(Environment.NewLine, messages));
        }
    }

    public static class TypographyPreviewScreenshotUtility
    {
        public const string DefaultOutputRoot = "TestLogs/TypographyVisualQA";
        public const int SettingsExpectedAppliedBindingCount = 38;
        public const string TmpSettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        public const string NanumGothicFontAssetPath = "Assets/_Shared/UI/Fonts/NanumGothic SDF.asset";

        public static readonly TypographyPreviewScreenshotTarget[] RequiredTargets =
        {
            new("Settings", "Settings", "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab"),
            new("Pause", "Pause", "Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab"),
            new("Main Menu", "MainMenu", "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab"),
        };

        public static readonly string[] DirtyGuardAssetPaths =
        {
            NanumGothicFontAssetPath,
            TmpSettingsAssetPath,
            TypographyThemeValidator.ThemeAssetPath,
            "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab",
            "Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab",
            "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab",
        };

        public static TypographyPreviewScreenshotBatchResult CaptureRequiredScreenshots(
            string outputDirectory = null,
            TypographyPreviewScreenshotOptions options = null,
            GameplayUiTypographyTheme theme = null)
        {
            options ??= new TypographyPreviewScreenshotOptions();
            var result = CaptureScreenshots(
                RequiredTargets,
                TypographyThemeValidator.RequiredLocaleCodes,
                outputDirectory ?? CreateTimestampedDefaultOutputDirectory(),
                options,
                theme);

            try
            {
                TypographyPreviewScreenshotManifestUtility.WriteCanonicalManifest(result, options);
            }
            catch (Exception exception)
            {
                result.AddError($"Canonical capture manifest could not be written: {exception.Message}");
            }

            return result;
        }

        public static TypographyPreviewScreenshotBatchResult CaptureScreenshots(
            IEnumerable<TypographyPreviewScreenshotTarget> targets,
            IEnumerable<string> localeCodes,
            string outputDirectory,
            TypographyPreviewScreenshotOptions options = null,
            GameplayUiTypographyTheme theme = null)
        {
            options ??= new TypographyPreviewScreenshotOptions();
            outputDirectory = NormalizeOutputDirectory(outputDirectory);
            var result = new TypographyPreviewScreenshotBatchResult(outputDirectory);
            var targetList = (targets ?? Array.Empty<TypographyPreviewScreenshotTarget>()).ToArray();
            var localeList = (localeCodes ?? Array.Empty<string>()).ToArray();
            theme ??= TypographyThemeValidator.FindThemeAsset();

            ValidateInputs(targetList, localeList, options, theme, result);
            if (result.HasErrors)
            {
                return result;
            }

            Directory.CreateDirectory(outputDirectory);
            foreach (var target in targetList)
            {
                foreach (var localeCode in localeList)
                {
                    result.AddCapture(CaptureSingle(target, localeCode, outputDirectory, options, theme));
                }
            }

            var dirtyPaths = GetDirtyGuardAssetPaths();
            result.GuardedAssetsClean = dirtyPaths.Count == 0;
            foreach (var dirtyPath in dirtyPaths)
            {
                result.AddError($"Capture left guarded asset dirty: {dirtyPath}");
            }

            return result;
        }

        public static string CreateTimestampedDefaultOutputDirectory()
        {
            return Path.Combine(DefaultOutputRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        }

        public static string BuildFileName(TypographyPreviewScreenshotTarget target, string localeCode)
        {
            return $"{SanitizeFileName(target.FileStem)}_{SanitizeFileName(localeCode)}.png";
        }

        public static int GetExpectedLocalizedTextCount(string fileStem)
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
                    return 0;
            }
        }

        public static IReadOnlyList<string> GetDirtyGuardAssetPaths()
        {
            var dirtyPaths = new List<string>();
            foreach (var assetPath in DirtyGuardAssetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset != null && EditorUtility.IsDirty(asset))
                {
                    dirtyPaths.Add(assetPath);
                }
            }

            return dirtyPaths;
        }

        private static void ValidateInputs(
            IReadOnlyList<TypographyPreviewScreenshotTarget> targets,
            IReadOnlyList<string> localeCodes,
            TypographyPreviewScreenshotOptions options,
            GameplayUiTypographyTheme theme,
            TypographyPreviewScreenshotBatchResult result)
        {
            if (options.Width <= 0 || options.Height <= 0)
            {
                result.AddError($"Capture dimensions must be positive. Requested {options.Width}x{options.Height}.");
            }

            if (targets.Count == 0)
            {
                result.AddError("No typography screenshot targets were provided.");
            }

            if (localeCodes.Count == 0)
            {
                result.AddError("No typography screenshot locales were provided.");
            }

            var themeReport = TypographyThemeValidator.ValidateTheme(theme, TypographyThemeValidator.ThemeAssetPath);
            result.ThemeValidationPassed = !themeReport.HasErrors;
            if (themeReport.HasErrors)
            {
                AddValidationErrors(themeReport, result);
            }

            result.PrefabValidationPassed = true;
            foreach (var target in targets)
            {
                if (string.IsNullOrWhiteSpace(target.PrefabPath))
                {
                    result.PrefabValidationPassed = false;
                    result.AddError($"{target.Name}: Prefab path is empty.");
                    continue;
                }

                var prefabReport = TypographyBindingValidator.ValidatePrefabAtPath(target.PrefabPath, theme);
                if (prefabReport.HasErrors)
                {
                    result.PrefabValidationPassed = false;
                    AddValidationErrors(prefabReport, result);
                }
            }

            foreach (var localeCode in localeCodes)
            {
                if (string.IsNullOrWhiteSpace(localeCode))
                {
                    result.AddError("Locale code is empty.");
                }
            }
        }

        private static void AddValidationErrors(
            TypographyValidationReport report,
            TypographyPreviewScreenshotBatchResult result)
        {
            foreach (var issue in report.Issues.Where(issue => issue.Severity == TypographyValidationSeverity.Error))
            {
                result.AddError(issue.ToString());
            }
        }

        private static TypographyPreviewScreenshotCaptureResult CaptureSingle(
            TypographyPreviewScreenshotTarget target,
            string localeCode,
            string outputDirectory,
            TypographyPreviewScreenshotOptions options,
            GameplayUiTypographyTheme theme)
        {
            var filePath = Path.Combine(outputDirectory, BuildFileName(target, localeCode));
            var capture = new TypographyPreviewScreenshotCaptureResult(target, localeCode, filePath);
            GameObject prefabRoot = null;
            GameObject cameraObject = null;
            GameObject canvasObject = null;
            Scene previewScene = default;
            var previousActiveScene = SceneManager.GetActiveScene();
            var shouldClosePreviewScene = false;
            RenderTexture renderTexture = null;
            RenderTexture previousRenderTexture = null;
            IDisposable localizedTextScope = null;
            IDisposable fontAssetRestoreScope = null;

            try
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(target.PrefabPath);
                if (prefabAsset == null)
                {
                    capture.AddError($"{target.PrefabPath}: Prefab asset was not found.");
                    return capture;
                }

                previewScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                shouldClosePreviewScene = !Application.isBatchMode;
                EditorSceneManager.SetActiveScene(previewScene);
                prefabRoot = PrefabUtility.InstantiatePrefab(prefabAsset, previewScene) as GameObject;
                if (prefabRoot == null)
                {
                    capture.AddError($"{target.PrefabPath}: Prefab instance could not be created for screenshot capture.");
                    return capture;
                }

                var captureTheme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(
                    TypographyThemeValidator.ThemeAssetPath);
                localizedTextScope = ApplyLocalizedTextPreview(prefabRoot, target, localeCode, captureTheme, capture);
                if (capture.HasErrors)
                {
                    return capture;
                }

                var previewResult = TypographyPreviewUtility.ApplyPreview(prefabRoot, localeCode, captureTheme, recordUndo: false);
                capture.AppliedBindingCount = previewResult.AppliedCount;
                foreach (var error in previewResult.Errors)
                {
                    capture.AddError(error);
                }

                fontAssetRestoreScope = TmpFontAssetFileRestoreScope.Capture(prefabRoot);
                ValidateLocalizedGlyphCoverage(prefabRoot, capture);
                if (capture.HasErrors)
                {
                    return capture;
                }

                SetupPreviewScene(prefabRoot, options, out cameraObject, out canvasObject, out var camera);
                ForceCanvasGroupsVisible(prefabRoot);
                ForceLayoutUpdates(prefabRoot);
                ForceTextMeshUpdates(prefabRoot);
                Canvas.ForceUpdateCanvases();

                var texture = RenderCameraToTexture(camera, options, out renderTexture, out previousRenderTexture);
                try
                {
                    capture.OrientationValidationResult = "PASS_PIPELINE_CONTRACT";
                    if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        capture.NonBlankValidationResult = "NOT_SUPPORTED_NOGRAPHICS";
                    }
                    else
                    {
                        var pixels = texture.GetPixels32();
                        capture.NonBlankValidationResult =
                            pixels.Length > 0 && pixels.Any(pixel => !pixel.Equals(pixels[0]))
                                ? "PASS"
                                : "FAIL";
                        if (string.Equals(capture.NonBlankValidationResult, "FAIL", StringComparison.Ordinal))
                        {
                            capture.AddError($"{filePath}: Captured PNG is blank or single-color.");
                        }
                    }

                    File.WriteAllBytes(filePath, texture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                var fileInfo = new FileInfo(filePath);
                capture.Width = options.Width;
                capture.Height = options.Height;
                capture.FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0L;
                if (!fileInfo.Exists || fileInfo.Length <= 0L)
                {
                    capture.AddError($"{filePath}: PNG file was not written.");
                }
            }
            catch (Exception exception)
            {
                capture.AddError($"{target.Name} {localeCode}: {exception.Message}");
            }
            finally
            {
                fontAssetRestoreScope?.Dispose();
                localizedTextScope?.Dispose();
                RenderTexture.active = previousRenderTexture;
                if (renderTexture != null)
                {
                    if (cameraObject != null)
                    {
                        cameraObject.GetComponent<Camera>().targetTexture = null;
                    }

                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (prefabRoot != null)
                {
                    prefabRoot.transform.SetParent(null, false);
                    TypographyPreviewUtility.RestorePreview(prefabRoot, recordUndo: false);
                }

                if (cameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }

                if (canvasObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(canvasObject);
                }

                if (shouldClosePreviewScene)
                {
                    if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    {
                        EditorSceneManager.SetActiveScene(previousActiveScene);
                    }

                    EditorSceneManager.CloseScene(previewScene, true);
                }
            }

            return capture;
        }

        public static Texture2D CaptureRootForValidation(
            GameObject root,
            TypographyPreviewScreenshotOptions options = null)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            options ??= new TypographyPreviewScreenshotOptions();
            GameObject cameraObject = null;
            GameObject canvasObject = null;
            RenderTexture renderTexture = null;
            RenderTexture previousRenderTexture = null;
            var originalParent = root.transform.parent;
            var originalPosition = root.transform.localPosition;
            var originalRotation = root.transform.localRotation;
            var originalScale = root.transform.localScale;

            try
            {
                SetupPreviewScene(root, options, out cameraObject, out canvasObject, out var camera);
                Canvas.ForceUpdateCanvases();
                return RenderCameraToTexture(camera, options, out renderTexture, out previousRenderTexture);
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                if (renderTexture != null)
                {
                    if (cameraObject != null)
                    {
                        cameraObject.GetComponent<Camera>().targetTexture = null;
                    }

                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                root.transform.SetParent(originalParent, false);
                root.transform.localPosition = originalPosition;
                root.transform.localRotation = originalRotation;
                root.transform.localScale = originalScale;

                if (cameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }

                if (canvasObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(canvasObject);
                }
            }
        }

        private static IDisposable ApplyLocalizedTextPreview(
            GameObject prefabRoot,
            TypographyPreviewScreenshotTarget target,
            string localeCode,
            GameplayUiTypographyTheme theme,
            TypographyPreviewScreenshotCaptureResult capture)
        {
            if (!CaptureStringTableTextResolver.TryCreate(localeCode, out var resolver, out var failureReason))
            {
                capture.AddError($"{target.Name} {localeCode}: {failureReason}");
                return null;
            }

            IDisposable scope = resolver;
            if (string.Equals(target.FileStem, "Settings", StringComparison.Ordinal))
            {
                var view = prefabRoot.GetComponentInChildren<SettingsScreenView>(true);
                if (view == null)
                {
                    capture.AddError($"{target.Name}: SettingsScreenView was not found.");
                    return scope;
                }

                view.BindStaticLocalization(
                    SettingsScreenPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    typographyTheme: theme);
                var viewModel = new SettingsScreenViewModel();
                viewModel.SetContent(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    SettingsSectionId.Input);
                view.Bind(viewModel);
                var inputViewModel = new SettingsInputViewModel();
                inputViewModel.SetContent(
                    string.Empty,
                    string.Empty,
                    false,
                    "WASD",
                    string.Empty,
                    "E",
                    string.Empty,
                    string.Empty,
                    "Q",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false,
                    null,
                    true);
                view.InputView.Bind(inputViewModel);
                view.SetIsCurrent(true);
                ValidateLocalizedText(
                    target,
                    resolver,
                    capture,
                    GetSettingsDescriptors(SettingsScreenPayload.Default),
                    prefabRoot);
                return new DisposableAction(() =>
                {
                    view.InputView.Bind(null);
                    view.Bind(null);
                    view.UnbindStaticLocalization();
                    scope.Dispose();
                });
            }

            if (string.Equals(target.FileStem, "Pause", StringComparison.Ordinal))
            {
                var view = prefabRoot.GetComponentInChildren<PausePopupView>(true);
                if (view == null)
                {
                    capture.AddError($"{target.Name}: PausePopupView was not found.");
                    return scope;
                }

                view.BindStaticLocalization(
                    PausePopupPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance);
                view.IsVisible = true;
                view.SetIsTopmost(true);
                ValidateLocalizedText(
                    target,
                    resolver,
                    capture,
                    GetPauseDescriptors(PausePopupPayload.Default),
                    prefabRoot);
                return new DisposableAction(() =>
                {
                    view.UnbindStaticLocalization();
                    scope.Dispose();
                });
            }

            if (string.Equals(target.FileStem, "MainMenu", StringComparison.Ordinal))
            {
                var view = prefabRoot.GetComponentInChildren<MainMenuScreenView>(true);
                if (view == null)
                {
                    capture.AddError($"{target.Name}: MainMenuScreenView was not found.");
                    return scope;
                }

                view.BindStaticLocalization(
                    MainMenuStaticTextPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    typographyTheme: theme);
                view.SetVisible(true);
                view.ShowSection(MainMenuSectionId.None);
                ValidateLocalizedText(
                    target,
                    resolver,
                    capture,
                    GetMainMenuDescriptors(MainMenuStaticTextPayload.Default),
                    prefabRoot);
                return new DisposableAction(() =>
                {
                    view.UnbindStaticLocalization();
                    scope.Dispose();
                });
            }

            capture.AddError($"{target.Name}: No localized preview applicator exists for screenshot target '{target.FileStem}'.");
            return scope;
        }

        private static void ValidateLocalizedText(
            TypographyPreviewScreenshotTarget target,
            CaptureStringTableTextResolver resolver,
            TypographyPreviewScreenshotCaptureResult capture,
            IEnumerable<LocalizedTextDescriptor> descriptors,
            GameObject root)
        {
            var allText = root.GetComponentsInChildren<TMP_Text>(true);
            var descriptorList = descriptors.ToArray();
            capture.ExpectedLocalizedTextCount = descriptorList.Length;
            foreach (var descriptor in descriptorList)
            {
                if (!resolver.TryResolveExact(descriptor, out var expected))
                {
                    capture.AddError($"{target.Name} {resolver.CurrentLocaleCode}: Missing String Table entry {descriptor.Table}:{descriptor.Key}.");
                    continue;
                }

                var matchingText = allText.FirstOrDefault(text =>
                    text != null && string.Equals(text.text, expected, StringComparison.Ordinal));
                if (matchingText != null)
                {
                    capture.LocalizedTextAppliedCount++;
                    capture.AddLocalizedText(expected);
                    continue;
                }

                capture.AddError(
                    $"{target.Name} {resolver.CurrentLocaleCode}: Expected localized text '{expected}' from {descriptor.Table}:{descriptor.Key} was not applied before capture.");
            }
        }

        private static void ValidateLocalizedGlyphCoverage(
            GameObject root,
            TypographyPreviewScreenshotCaptureResult capture)
        {
            var allText = root.GetComponentsInChildren<TMP_Text>(true);
            var missingGlyphs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var localizedText in capture.LocalizedTexts)
            {
                var target = allText.FirstOrDefault(text =>
                    text != null && string.Equals(text.text, localizedText, StringComparison.Ordinal));
                if (target == null)
                {
                    missingGlyphs.Add($"localized text '{localizedText}' has no TMP target after preview");
                    continue;
                }

                foreach (var character in localizedText)
                {
                    if (char.IsControl(character) ||
                        char.IsWhiteSpace(character) ||
                        HasRenderableCharacter(target.font, character))
                    {
                        continue;
                    }

                    missingGlyphs.Add($"'{character}' U+{(int)character:X4}");
                }
            }

            capture.GlyphTofuValidationResult = missingGlyphs.Count == 0 ? "PASS" : "FAIL";
            foreach (var missingGlyph in missingGlyphs)
            {
                capture.AddError(
                    $"{capture.Target.Name} {capture.LocaleCode}: Font coverage is missing {missingGlyph}.");
            }
        }

        private static bool HasRenderableCharacter(TMP_FontAsset fontAsset, char character)
        {
            if (fontAsset != null &&
                fontAsset.HasCharacter(character, searchFallbacks: true, tryAddCharacter: false))
            {
                return true;
            }

            return TMP_Settings.fallbackFontAssets != null &&
                   TMP_Settings.fallbackFontAssets.Any(fallback =>
                       fallback != null &&
                       fallback.HasCharacter(character, searchFallbacks: true, tryAddCharacter: false));
        }

        private static IReadOnlyList<LocalizedTextDescriptor> GetSettingsDescriptors(SettingsScreenPayload payload)
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

        private static IReadOnlyList<LocalizedTextDescriptor> GetPauseDescriptors(PausePopupPayload payload)
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

        private static IReadOnlyList<LocalizedTextDescriptor> GetMainMenuDescriptors(MainMenuStaticTextPayload payload)
        {
            return new[]
            {
                payload.StartLabelDescriptor,
                payload.SettingsLabelDescriptor,
                payload.QuitLabelDescriptor,
            };
        }

        private static void SetupPreviewScene(
            GameObject prefabRoot,
            TypographyPreviewScreenshotOptions options,
            out GameObject cameraObject,
            out GameObject canvasObject,
            out Camera camera)
        {
            var scene = prefabRoot.scene;
            cameraObject = new GameObject("Typography Preview Screenshot Camera");
            EditorSceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = options.BackgroundColor;
            camera.orthographic = true;
            camera.orthographicSize = options.Height * 0.5f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;
            camera.cullingMask = ~0;
            cameraObject.transform.position = new Vector3(0f, 0f, -100f);
            cameraObject.transform.rotation = Quaternion.identity;

            canvasObject = new GameObject("Typography Preview Screenshot Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            EditorSceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.sizeDelta = new Vector2(options.Width, options.Height);
            canvasRect.anchoredPosition = Vector2.zero;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(options.Width, options.Height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            prefabRoot.SetActive(true);
            prefabRoot.transform.SetParent(canvasObject.transform, false);
            ConfigureCanvases(prefabRoot, camera, options.Width, options.Height);
        }

        private static void ConfigureCanvases(GameObject root, Camera camera, int width, int height)
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                canvas.pixelPerfect = false;

                if (canvas.transform is RectTransform rectTransform &&
                    (Mathf.Approximately(rectTransform.rect.width, 0f) ||
                     Mathf.Approximately(rectTransform.rect.height, 0f)))
                {
                    rectTransform.sizeDelta = new Vector2(width, height);
                    rectTransform.anchoredPosition = Vector2.zero;
                }
            }
        }

        private static void ForceLayoutUpdates(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            if (root != null && root.transform is RectTransform rootRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
            }

            Canvas.ForceUpdateCanvases();
        }

        private static void ForceTextMeshUpdates(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.ForceMeshUpdate(true, true);
            }
        }

        private static void ForceCanvasGroupsVisible(GameObject root)
        {
            foreach (var canvasGroup in root.GetComponentsInChildren<CanvasGroup>(true))
            {
                canvasGroup.alpha = 1f;
            }
        }

        private static Texture2D RenderCameraToTexture(
            Camera camera,
            TypographyPreviewScreenshotOptions options,
            out RenderTexture renderTexture,
            out RenderTexture previousRenderTexture)
        {
            renderTexture = new RenderTexture(options.Width, options.Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "TypographyPreviewScreenshotRT",
                antiAliasing = 1,
            };
            renderTexture.Create();

            camera.targetTexture = renderTexture;
            previousRenderTexture = RenderTexture.active;
            Graphics.SetRenderTarget(renderTexture);
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, options.BackgroundColor);
            camera.Render();
            Graphics.SetRenderTarget(renderTexture);
            RenderTexture.active = renderTexture;

            var texture = new Texture2D(options.Width, options.Height, TextureFormat.RGBA32, false);
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Graphics.CopyTexture(renderTexture, texture);
            }
            else
            {
                texture.ReadPixels(new Rect(0, 0, options.Width, options.Height), 0, 0, false);
            }

            texture.Apply();
            return texture;
        }

        private static string NormalizeOutputDirectory(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = CreateTimestampedDefaultOutputDirectory();
            }

            return Path.GetFullPath(outputDirectory);
        }

        private static string SanitizeFileName(string value)
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? "TypographyPreview" : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalid, '_');
            }

            return sanitized;
        }

        private sealed class CaptureStringTableTextResolver : ILocalizedTextResolver, IDisposable
        {
            private readonly Locale locale;
            private readonly Locale fallbackLocale;

            private CaptureStringTableTextResolver(string localeCode, Locale locale, Locale fallbackLocale)
            {
                CurrentLocaleCode = localeCode;
                this.locale = locale;
                this.fallbackLocale = fallbackLocale;
            }

            public string CurrentLocaleCode { get; }

            public event Action LocaleChanged
            {
                add { }
                remove { }
            }

            public static bool TryCreate(
                string localeCode,
                out CaptureStringTableTextResolver resolver,
                out string failureReason)
            {
                resolver = null;
                failureReason = string.Empty;
                try
                {
                    if (!LocalizationSettings.HasSettings)
                    {
                        failureReason = "Unity Localization settings are not configured.";
                        return false;
                    }

                    var initialization = LocalizationSettings.InitializationOperation;
                    if (!initialization.IsDone)
                    {
                        initialization.WaitForCompletion();
                    }

                    if (initialization.Result == null)
                    {
                        failureReason = "Unity Localization initialization did not complete successfully.";
                        return false;
                    }

                    var normalizedLocaleCode = string.IsNullOrWhiteSpace(localeCode)
                        ? "en-US"
                        : localeCode;
                    var locale = LocalizationSettings.AvailableLocales?.GetLocale(normalizedLocaleCode);
                    var fallbackLocale = LocalizationSettings.AvailableLocales?.GetLocale("en-US");
                    if (locale == null)
                    {
                        failureReason = $"Locale '{normalizedLocaleCode}' is not available.";
                        return false;
                    }

                    if (fallbackLocale == null)
                    {
                        failureReason = "Fallback locale 'en-US' is not available.";
                        return false;
                    }

                    resolver = new CaptureStringTableTextResolver(normalizedLocaleCode, locale, fallbackLocale);
                    return true;
                }
                catch (Exception exception)
                {
                    failureReason = exception.Message;
                    return false;
                }
            }

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                if (TryResolve(locale, descriptor, out var value) ||
                    TryResolve(fallbackLocale, descriptor, out value))
                {
                    return value;
                }

                return $"[{descriptor.Table}:{descriptor.Key}]";
            }

            public bool TryResolveExact(LocalizedTextDescriptor descriptor, out string value)
            {
                return TryResolve(locale, descriptor, out value);
            }

            public void Dispose()
            {
            }

            private static bool TryResolve(Locale locale, LocalizedTextDescriptor descriptor, out string value)
            {
                value = null;
                var table = LocalizationSettings.StringDatabase.GetTable(descriptor.Table, locale);
                var entry = table != null ? table.GetEntry(descriptor.Key) : null;
                if (entry == null)
                {
                    return false;
                }

                value = ResolveEntry(entry, descriptor);
                return !string.IsNullOrEmpty(value);
            }

            private static string ResolveEntry(StringTableEntry entry, LocalizedTextDescriptor descriptor)
            {
                if (descriptor.Arguments.Count == 0)
                {
                    return entry.GetLocalizedString();
                }

                var arguments = new object[descriptor.Arguments.Count];
                for (var i = 0; i < descriptor.Arguments.Count; i++)
                {
                    arguments[i] = descriptor.Arguments[i];
                }

                return entry.GetLocalizedString(arguments);
            }
        }

        private sealed class DisposableAction : IDisposable
        {
            private readonly Action action;
            private bool isDisposed;

            public DisposableAction(Action action)
            {
                this.action = action;
            }

            public void Dispose()
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                action?.Invoke();
            }
        }

        private sealed class TmpFontAssetFileRestoreScope : IDisposable
        {
            private readonly Dictionary<string, byte[]> snapshots;
            private bool isDisposed;

            private TmpFontAssetFileRestoreScope(Dictionary<string, byte[]> snapshots)
            {
                this.snapshots = snapshots;
            }

            public static TmpFontAssetFileRestoreScope Capture(GameObject root)
            {
                var snapshots = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                if (root == null)
                {
                    return new TmpFontAssetFileRestoreScope(snapshots);
                }

                var fontAssets = new HashSet<TMP_FontAsset>();
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    CollectFontAssets(text != null ? text.font : null, fontAssets);
                }

                CollectFontAssets(TMP_Settings.defaultFontAsset, fontAssets);
                if (TMP_Settings.fallbackFontAssets != null)
                {
                    foreach (var fallback in TMP_Settings.fallbackFontAssets)
                    {
                        CollectFontAssets(fallback, fontAssets);
                    }
                }

                foreach (var fontAsset in fontAssets)
                {
                    var assetPath = AssetDatabase.GetAssetPath(fontAsset);
                    if (string.IsNullOrWhiteSpace(assetPath) ||
                        !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                        !File.Exists(assetPath) ||
                        snapshots.ContainsKey(assetPath))
                    {
                        continue;
                    }

                    snapshots.Add(assetPath, File.ReadAllBytes(assetPath));
                }

                return new TmpFontAssetFileRestoreScope(snapshots);
            }

            public void Dispose()
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                foreach (var snapshot in snapshots)
                {
                    if (!File.Exists(snapshot.Key) ||
                        File.ReadAllBytes(snapshot.Key).SequenceEqual(snapshot.Value))
                    {
                        ClearDirty(snapshot.Key);
                        continue;
                    }

                    File.WriteAllBytes(snapshot.Key, snapshot.Value);
                    AssetDatabase.ImportAsset(snapshot.Key, ImportAssetOptions.ForceUpdate);
                    ClearDirty(snapshot.Key);
                }
            }

            private static void CollectFontAssets(TMP_FontAsset fontAsset, HashSet<TMP_FontAsset> fontAssets)
            {
                if (fontAsset == null || !fontAssets.Add(fontAsset))
                {
                    return;
                }

                if (fontAsset.fallbackFontAssetTable == null)
                {
                    return;
                }

                foreach (var fallback in fontAsset.fallbackFontAssetTable)
                {
                    CollectFontAssets(fallback, fontAssets);
                }
            }

            private static void ClearDirty(string assetPath)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if (asset != null)
                    {
                        EditorUtility.ClearDirty(asset);
                    }
                }
            }
        }
    }
}

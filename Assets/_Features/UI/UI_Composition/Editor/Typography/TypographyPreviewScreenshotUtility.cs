using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using ConfirmPopupPresenter = Game.Feature.UI.Application.ConfirmPopupPresenter;
using MainMenuConfirmationKind = Game.Feature.UI.Application.MainMenuConfirmationKind;
using MainMenuLocalization = Game.Feature.UI.Application.MainMenuLocalization;
using MainMenuSlotViewModelMapper = Game.Feature.UI.Application.MainMenuSlotViewModelMapper;
using IKeyboardBindingSettingsPort = Game.Feature.UI.Application.IKeyboardBindingSettingsPort;
using KeyboardBindingSettingsSnapshot = Game.Feature.UI.Application.KeyboardBindingSettingsSnapshot;
using KeyboardRebindResult = Game.Feature.UI.Application.KeyboardRebindResult;
using KeyboardRebindStartResult = Game.Feature.UI.Application.KeyboardRebindStartResult;
using SettingsInputPresenter = Game.Feature.UI.Application.SettingsInputPresenter;
using SettingsInputPresenterInput = Game.Feature.UI.Application.SettingsInputPresenterInput;
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

        public int M1bPixelProofCount { get; set; }

        public int M1bPixelProofPassCount { get; set; }

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

        public bool AssetMutationObservationPassed { get; internal set; }

        public int UnexpectedAssetMutationCount { get; internal set; }

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
        public const string ClimateCrisisKrFontAssetPath =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset";

        public static readonly TypographyPreviewScreenshotTarget[] RequiredTargets =
        {
            new("Settings", "Settings", "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab"),
            new("Pause", "Pause", "Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab"),
            new("Main Menu", "MainMenu", "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab"),
        };

        public static readonly TypographyPreviewScreenshotTarget[] ClimateDiagnosticTargets =
        {
            new(
                "Settings Audio Muted",
                "SettingsAudioMuted",
                "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab"),
            new(
                "Settings Display Status",
                "SettingsDisplayStatus",
                "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab"),
            new(
                "Confirm Popup",
                "ConfirmPopup",
                "Assets/_Features/UI/UI_Popups/Prefabs/ConfirmPopup.prefab"),
        };

        public static readonly TypographyPreviewScreenshotTarget[] M1bDiagnosticTargets =
        {
            new(
                "Main Menu Save Slots",
                "M1BSaveSlots",
                "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab"),
            new(
                "Delete Slot Confirmation",
                "M1BDeleteConfirmation",
                "Assets/_Features/UI/UI_Popups/Prefabs/ConfirmPopup.prefab"),
            new(
                "Restart Slot Confirmation",
                "M1BRestartConfirmation",
                "Assets/_Features/UI/UI_Popups/Prefabs/ConfirmPopup.prefab"),
            new(
                "Overwrite Slot Confirmation",
                "M1BOverwriteConfirmation",
                "Assets/_Features/UI/UI_Popups/Prefabs/ConfirmPopup.prefab"),
            new(
                "Quit Confirmation",
                "M1BQuitConfirmation",
                "Assets/_Features/UI/UI_Popups/Prefabs/ConfirmPopup.prefab"),
        };

        public static readonly TypographyPreviewScreenshotTarget[] M2aDiagnosticTargets =
        {
            new(
                "Main Menu Corrupt Save",
                "M2ACorrupt",
                "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab"),
            new(
                "Main Menu Save Permission Failure",
                "M2APermission",
                "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab"),
            new(
                "Main Menu Save Load Failure",
                "M2ALoadFailed",
                "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab"),
            new(
                "Main Menu Save Needs Repair",
                "M2ANeedsRepair",
                "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab"),
        };

        public static readonly TypographyPreviewScreenshotTarget[] M2bDiagnosticTargets =
        {
            new(
                "Settings Reserved Key",
                "M2BReserved",
                "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab"),
            new(
                "Settings Action Conflict Flip",
                "M2BActionConflictFlip",
                "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab"),
            new(
                "Settings Action Conflict Push",
                "M2BActionConflictPush",
                "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab"),
            new(
                "Settings Movement Conflict",
                "M2BMovementConflict",
                "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab"),
            new(
                "Settings Already Rebinding",
                "M2BAlreadyRebinding",
                "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab"),
            new(
                "Settings Rebinding Prompt",
                "M2BRebindingPrompt",
                "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab"),
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
            var assetMutationGuard = CaptureAssetMutationGuard.Capture(
                DirtyGuardAssetPaths.Concat(new[] { ClimateCrisisKrFontAssetPath }));
            try
            {
                foreach (var target in targetList)
                {
                    foreach (var localeCode in localeList)
                    {
                        result.AddCapture(CaptureSingle(
                            target,
                            localeCode,
                            outputDirectory,
                            options,
                            theme,
                            assetMutationGuard));
                    }
                }
            }
            finally
            {
                try
                {
                    var evidence = assetMutationGuard.ObserveBeforeRestore();
                    result.UnexpectedAssetMutationCount = evidence.Count(item => !item.Allowed);
                    result.AssetMutationObservationPassed =
                        result.UnexpectedAssetMutationCount == 0;
                    foreach (var item in evidence)
                    {
                        Debug.Log(
                            "CAPTURE_ASSET_MUTATION " +
                            $"path={item.Path} " +
                            $"before_hash={item.BeforeHash} " +
                            $"after_capture_hash={item.AfterCaptureHash} " +
                            $"mutation_detected={(item.MutationDetected ? 1 : 0)} " +
                            $"changed_properties={item.ChangedProperties} " +
                            $"classification={item.Classification} " +
                            $"allowed={(item.Allowed ? 1 : 0)} " +
                            $"lane_verdict_before_restore={item.LaneVerdictBeforeRestore}");
                        if (!item.Allowed)
                        {
                            result.AddError(
                                $"UNEXPECTED_ASSET_MUTATION before restore: {item.Path}");
                        }
                    }
                }
                catch (Exception exception)
                {
                    result.AssetMutationObservationPassed = false;
                    result.AddError(
                        $"Capture asset mutation observation failed before restore: {exception.Message}");
                }

                try
                {
                    assetMutationGuard.Dispose();
                    foreach (var item in assetMutationGuard.Evidence)
                    {
                        Debug.Log(
                            "CAPTURE_ASSET_RESTORE " +
                            $"path={item.Path} " +
                            $"restored={(item.Restored ? 1 : 0)} " +
                            $"restored_hash={item.RestoredHash}");
                        if (!item.Restored)
                        {
                            result.AddError($"Capture asset restore failed: {item.Path}");
                        }
                    }
                }
                catch (Exception exception)
                {
                    result.AddError($"Capture asset restore failed: {exception.Message}");
                }

                Debug.Log(
                    "CAPTURE_ASSET_MUTATION_OBSERVED_BEFORE_RESTORE: " +
                    (result.AssetMutationObservationPassed ? "PASS" : "FAIL"));
                Debug.Log(
                    $"CAPTURE_UNEXPECTED_ASSET_MUTATION_COUNT: {result.UnexpectedAssetMutationCount}");
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
                case "SettingsAudioMuted":
                case "SettingsDisplayStatus":
                    return 22;

                case "Pause":
                    return 6;

                case "MainMenu":
                    return 3;

                case "ConfirmPopup":
                    return 4;

                case "M1BSaveSlots":
                    return 20;

                case "M1BDeleteConfirmation":
                case "M1BRestartConfirmation":
                case "M1BOverwriteConfirmation":
                case "M1BQuitConfirmation":
                    return 5;

                case "M2ACorrupt":
                case "M2APermission":
                case "M2ALoadFailed":
                case "M2ANeedsRepair":
                    return 9;

                case "M2BReserved":
                case "M2BActionConflictFlip":
                case "M2BActionConflictPush":
                case "M2BMovementConflict":
                case "M2BAlreadyRebinding":
                case "M2BRebindingPrompt":
                    return 23;

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
            GameplayUiTypographyTheme theme,
            CaptureAssetMutationGuard assetMutationGuard)
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
            LocaleInvariantTypographyScope localeInvariantTypographyScope = null;

            try
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(target.PrefabPath);
                if (prefabAsset == null)
                {
                    capture.AddError($"{target.PrefabPath}: Prefab asset was not found.");
                    return capture;
                }

                assetMutationGuard?.IncludeFontAssets(prefabAsset);
                previewScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    UnityEngine.Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                shouldClosePreviewScene = !UnityEngine.Application.isBatchMode;
                EditorSceneManager.SetActiveScene(previewScene);
                prefabRoot = PrefabUtility.InstantiatePrefab(prefabAsset, previewScene) as GameObject;
                if (prefabRoot == null)
                {
                    capture.AddError($"{target.PrefabPath}: Prefab instance could not be created for screenshot capture.");
                    return capture;
                }

                localeInvariantTypographyScope = LocaleInvariantTypographyScope.Capture(prefabRoot);
                var captureTheme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(
                    TypographyThemeValidator.ThemeAssetPath);
                localizedTextScope = ApplyLocalizedTextPreview(prefabRoot, target, localeCode, captureTheme, capture);
                if (capture.HasErrors)
                {
                    return capture;
                }

                localeInvariantTypographyScope.Restore();

                var previewResult = TypographyPreviewUtility.ApplyPreview(prefabRoot, localeCode, captureTheme, recordUndo: false);
                capture.AppliedBindingCount = previewResult.AppliedCount;
                foreach (var error in previewResult.Errors)
                {
                    capture.AddError(error);
                }

                ValidateLocalizedGlyphCoverage(prefabRoot, capture);
                if (capture.HasErrors)
                {
                    return capture;
                }

                SetupPreviewScene(prefabRoot, options, out cameraObject, out canvasObject, out var camera);
                ForceCanvasGroupsVisible(prefabRoot);
                ForceLayoutUpdates(prefabRoot);
                ForceGraphicUpdates(prefabRoot);
                ForceTextMeshUpdates(prefabRoot);
                Canvas.ForceUpdateCanvases();

                var texture = RenderCameraToTexture(
                    camera,
                    options,
                    out renderTexture,
                    out previousRenderTexture,
                    out _,
                    out _);
                try
                {
                    ValidatePauseRenderedTargets(prefabRoot, capture);
                    ValidateLocaleInvariantPreview(prefabRoot, capture);
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

                        ValidateM1bRenderedTargets(
                            prefabRoot,
                            capture,
                            camera,
                            renderTexture,
                            options,
                            pixels);
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
                localeInvariantTypographyScope?.Dispose();
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
            return CaptureRootForValidation(
                root,
                options,
                out _,
                out _);
        }

        public static Texture2D CaptureRootForValidation(
            GameObject root,
            TypographyPreviewScreenshotOptions options,
            out int cameraRenderPassCount,
            out int captureFrameIndex)
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
                SetupPreviewScene(
                    root,
                    options,
                    out cameraObject,
                    out canvasObject,
                    out var camera,
                    useWorldSpaceCanvas: true);
                ForceLayoutUpdates(root);
                ForceGraphicUpdates(root);
                ForceTextMeshUpdates(root);
                Canvas.ForceUpdateCanvases();
                return RenderCameraToTexture(
                    camera,
                    options,
                    out renderTexture,
                    out previousRenderTexture,
                    out cameraRenderPassCount,
                    out captureFrameIndex);
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
            if (string.Equals(target.FileStem, "Settings", StringComparison.Ordinal) ||
                string.Equals(target.FileStem, "SettingsAudioMuted", StringComparison.Ordinal) ||
                string.Equals(target.FileStem, "SettingsDisplayStatus", StringComparison.Ordinal) ||
                TryGetM2bVisualState(target.FileStem, out _))
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
                    GetSettingsPreviewSection(target.FileStem));
                view.Bind(viewModel);
                ApplySettingsPreviewState(prefabRoot, target, resolver);
                view.SetIsCurrent(true);
                ValidateLocalizedText(
                    target,
                    resolver,
                    capture,
                    GetSettingsDescriptors(SettingsScreenPayload.Default)
                        .Concat(GetM2bStatusDescriptors(target.FileStem)),
                    prefabRoot);
                return new DisposableAction(() =>
                {
                    view.InputView.Bind(null);
                    view.AudioView.Bind(null);
                    view.DisplayView.Bind(null);
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

                var productionLocalizationScope = PausePopupProductionLocalizationComposer.Bind(
                    view,
                    PausePopupPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    theme);
                view.IsVisible = true;
                view.SetIsTopmost(true);
                // The command-line en-US slice captures Settings before Pause, so DOTween is already
                // initialized and applies the modal enter start pose synchronously. Disable the view
                // after localization to execute its normal OnDisable cleanup and restore the authored
                // alpha/scale before the deterministic evidence frame is rendered.
                view.enabled = false;
                ValidateLocalizedText(
                    target,
                    resolver,
                    capture,
                    GetPauseDescriptors(PausePopupPayload.Default),
                    prefabRoot);
                return new DisposableAction(() =>
                {
                    productionLocalizationScope.Dispose();
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

            if (string.Equals(target.FileStem, "M1BSaveSlots", StringComparison.Ordinal))
            {
                var view = prefabRoot.GetComponentInChildren<MainMenuScreenView>(true);
                if (view == null)
                {
                    capture.AddError($"{target.Name}: MainMenuScreenView was not found.");
                    return scope;
                }

                var sequenceDefinition = CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance();
                var sequenceResolver = new CampaignStageSequenceResolver(sequenceDefinition);
                var slots = new[]
                {
                    SaveSlotData.CreateEmpty(1),
                    new SaveSlotData
                    {
                        SlotNumber = 2,
                        CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                        CurrentLevelGroupId = "level-2",
                        RemainingChances = 2,
                        TotalDeaths = 3,
                        LastPlayedAt = "2026-07-29T12:34:00+09:00",
                    },
                    new SaveSlotData
                    {
                        SlotNumber = 3,
                        CurrentStageId = StageId.CreateOrThrow("stage-4-2"),
                        CurrentLevelGroupId = "level-4",
                        RemainingChances = 1,
                        TotalDeaths = 5,
                        LastPlayedAt = "2026-07-29T12:34:00+09:00",
                        CampaignCompleted = true,
                    },
                };
                view.BindStaticLocalization(
                    MainMenuStaticTextPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    typographyTheme: theme);
                view.SaveSlotPanel.Bind(
                    MainMenuSlotViewModelMapper.Map(slots, sequenceResolver, null, resolver));
                view.SetVisible(true);
                view.ShowSection(MainMenuSectionId.SaveSlots);

                var localizedTargets = view.SaveSlotPanel
                    .CreateTypographyTargets()
                    .Where(text => text != null &&
                                   text.gameObject.activeInHierarchy &&
                                   !string.IsNullOrWhiteSpace(text.text))
                    .ToArray();
                RecordM1bLocalizedTargets(target, capture, localizedTargets);
                return new DisposableAction(() =>
                {
                    view.SaveSlotPanel.Bind(null);
                    view.UnbindStaticLocalization();
                    UnityEngine.Object.DestroyImmediate(sequenceDefinition);
                    scope.Dispose();
                });
            }

            if (TryGetM2aCampaignSaveStatus(target.FileStem, out var campaignSaveStatus))
            {
                var view = prefabRoot.GetComponentInChildren<MainMenuScreenView>(true);
                if (view == null)
                {
                    capture.AddError($"{target.Name}: MainMenuScreenView was not found.");
                    return scope;
                }

                const string diagnosticReason =
                    "UnauthorizedAccessException: C:\\Users\\Player\\Saves\\profile.json";
                view.BindStaticLocalization(
                    MainMenuStaticTextPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    typographyTheme: theme);
                view.SaveSlotPanel.Bind(MainMenuSlotViewModelMapper.MapCampaignAccessBlocked(
                    new CampaignSaveLoadReport(
                        campaignSaveStatus,
                        diagnosticReason,
                        "CampaignProfileDocument"),
                    resolver));
                view.SetVisible(true);
                view.ShowSection(MainMenuSectionId.SaveSlots);

                var localizedTargets = view.SaveSlotPanel
                    .CreateTypographyTargets()
                    .Where(text => text != null &&
                                   text.gameObject.activeInHierarchy &&
                                   !string.IsNullOrWhiteSpace(text.text))
                    .ToArray();
                RecordM1bLocalizedTargets(target, capture, localizedTargets);
                if (localizedTargets.Any(text =>
                        text.text.Contains(diagnosticReason, StringComparison.Ordinal) ||
                        text.text.Contains("C:\\Users\\", StringComparison.Ordinal)))
                {
                    capture.AddError(
                        $"{target.Name}: raw diagnostic reason or file path reached player-facing text.");
                }

                var cards = view.SaveSlotPanel.GetComponentsInChildren<SaveSlotCardView>(true);
                if (cards.Any(card => card.HasAnyFocusableAction))
                {
                    capture.AddError(
                        $"{target.Name}: campaign-blocking failure exposed an unsupported action.");
                }

                return new DisposableAction(() =>
                {
                    view.SaveSlotPanel.Bind(null);
                    view.UnbindStaticLocalization();
                    scope.Dispose();
                });
            }

            if (TryGetM1bConfirmationKind(target.FileStem, out var confirmationKind))
            {
                var view = prefabRoot.GetComponentInChildren<ConfirmPopupView>(true);
                if (view == null)
                {
                    capture.AddError($"{target.Name}: ConfirmPopupView was not found.");
                    return scope;
                }

                var presenter = new ConfirmPopupPresenter(resolver);
                presenter.Apply(MainMenuLocalization.CreateConfirmationPayload(
                    confirmationKind,
                    confirmationKind == MainMenuConfirmationKind.QuitGame ? null : 2));
                view.Bind(presenter.ViewModel);
                var productionTypographyScope = ConfirmPopupProductionLocalizationComposer.Bind(
                    view,
                    resolver,
                    theme);
                view.IsVisible = true;
                view.SetIsTopmost(true);
                view.enabled = false;
                RecordM1bLocalizedTargets(
                    target,
                    capture,
                    view.CreateTypographyTargets().Where(text => text != null).ToArray());
                return new DisposableAction(() =>
                {
                    productionTypographyScope.Dispose();
                    view.Bind(null);
                    presenter.Dispose();
                    scope.Dispose();
                });
            }

            if (string.Equals(target.FileStem, "ConfirmPopup", StringComparison.Ordinal))
            {
                var view = prefabRoot.GetComponentInChildren<ConfirmPopupView>(true);
                if (view == null)
                {
                    capture.AddError($"{target.Name}: ConfirmPopupView was not found.");
                    return scope;
                }

                var viewModel = new ConfirmPopupViewModel();
                var localizedValues = new[]
                {
                    resolver.Resolve(SettingsStaticTextDescriptors.DisplayPreviewConfirmTitle),
                    resolver.Resolve(SettingsDynamicTextDescriptors.DisplayPreviewConfirmBody(
                        1280,
                        720,
                        isFullscreen: true,
                        seconds: 15)),
                    resolver.Resolve(SettingsStaticTextDescriptors.DisplayPreviewConfirmKeep),
                    resolver.Resolve(SettingsStaticTextDescriptors.DisplayRevert),
                };
                viewModel.SetContent(
                    localizedValues[0],
                    localizedValues[1],
                    localizedValues[2],
                    localizedValues[3],
                    isConfirmDestructive: false);
                view.Bind(viewModel);
                var productionTypographyScope = ConfirmPopupProductionLocalizationComposer.Bind(
                    view,
                    resolver,
                    theme);
                view.IsVisible = true;
                view.SetIsTopmost(true);
                view.enabled = false;
                capture.ExpectedLocalizedTextCount = localizedValues.Length;
                capture.LocalizedTextAppliedCount = localizedValues.Length;
                foreach (var value in localizedValues)
                {
                    capture.AddLocalizedText(value);
                }

                return new DisposableAction(() =>
                {
                    productionTypographyScope.Dispose();
                    view.Bind(null);
                    scope.Dispose();
                });
            }

            capture.AddError($"{target.Name}: No localized preview applicator exists for screenshot target '{target.FileStem}'.");
            return scope;
        }

        private static void RecordM1bLocalizedTargets(
            TypographyPreviewScreenshotTarget target,
            TypographyPreviewScreenshotCaptureResult capture,
            IReadOnlyList<TMP_Text> localizedTargets)
        {
            capture.ExpectedLocalizedTextCount = GetExpectedLocalizedTextCount(target.FileStem);
            capture.LocalizedTextAppliedCount = localizedTargets.Count;
            foreach (var localizedTarget in localizedTargets)
            {
                capture.AddLocalizedText(localizedTarget.text);
            }

            if (capture.LocalizedTextAppliedCount != capture.ExpectedLocalizedTextCount)
            {
                capture.AddError(
                    $"{target.Name}: expected {capture.ExpectedLocalizedTextCount} active localized targets, " +
                    $"found {capture.LocalizedTextAppliedCount}.");
            }
        }

        private static bool TryGetM1bConfirmationKind(
            string fileStem,
            out MainMenuConfirmationKind kind)
        {
            switch (fileStem)
            {
                case "M1BDeleteConfirmation":
                    kind = MainMenuConfirmationKind.DeleteSlot;
                    return true;
                case "M1BRestartConfirmation":
                    kind = MainMenuConfirmationKind.RestartSlot;
                    return true;
                case "M1BOverwriteConfirmation":
                    kind = MainMenuConfirmationKind.OverwriteSlot;
                    return true;
                case "M1BQuitConfirmation":
                    kind = MainMenuConfirmationKind.QuitGame;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        private static bool TryGetM2aCampaignSaveStatus(
            string fileStem,
            out CampaignSaveLoadStatus status)
        {
            switch (fileStem)
            {
                case "M2ACorrupt":
                    status = CampaignSaveLoadStatus.CorruptRepairRequired;
                    return true;
                case "M2APermission":
                    status = CampaignSaveLoadStatus.Unauthorized;
                    return true;
                case "M2ALoadFailed":
                    status = CampaignSaveLoadStatus.IoFailed;
                    return true;
                case "M2ANeedsRepair":
                    status = CampaignSaveLoadStatus.SchemaInvalidRepairRequired;
                    return true;
                default:
                    status = default;
                    return false;
            }
        }

        private static bool IsM1bDiagnosticTarget(string fileStem)
        {
            return string.Equals(fileStem, "M1BSaveSlots", StringComparison.Ordinal) ||
                   TryGetM1bConfirmationKind(fileStem, out _) ||
                   TryGetM2aCampaignSaveStatus(fileStem, out _);
        }

        private static bool TryGetM2bVisualState(
            string fileStem,
            out M2bRebindVisualState state)
        {
            switch (fileStem)
            {
                case "M2BReserved":
                    state = M2bRebindVisualState.Reserved;
                    return true;
                case "M2BActionConflictFlip":
                    state = M2bRebindVisualState.ActionConflictFlip;
                    return true;
                case "M2BActionConflictPush":
                    state = M2bRebindVisualState.ActionConflictPush;
                    return true;
                case "M2BMovementConflict":
                    state = M2bRebindVisualState.MovementConflict;
                    return true;
                case "M2BAlreadyRebinding":
                    state = M2bRebindVisualState.AlreadyRebinding;
                    return true;
                case "M2BRebindingPrompt":
                    state = M2bRebindVisualState.RebindingPrompt;
                    return true;
                default:
                    state = default;
                    return false;
            }
        }

        private static IEnumerable<LocalizedTextDescriptor> GetM2bStatusDescriptors(
            string fileStem)
        {
            if (!TryGetM2bVisualState(fileStem, out var state))
            {
                return Array.Empty<LocalizedTextDescriptor>();
            }

            return new[] { GetM2bStatusDescriptor(state) };
        }

        private static LocalizedTextDescriptor GetM2bStatusDescriptor(M2bRebindVisualState state)
        {
            switch (state)
            {
                case M2bRebindVisualState.Reserved:
                    return SettingsDynamicTextDescriptors.InputReservedKey();
                case M2bRebindVisualState.ActionConflictFlip:
                    return SettingsDynamicTextDescriptors.InputActionConflict(
                        SettingsStaticTextDescriptors.Flip);
                case M2bRebindVisualState.ActionConflictPush:
                    return SettingsDynamicTextDescriptors.InputActionConflict(
                        SettingsStaticTextDescriptors.Push);
                case M2bRebindVisualState.MovementConflict:
                    return SettingsDynamicTextDescriptors.InputMovementConflict();
                case M2bRebindVisualState.AlreadyRebinding:
                    return SettingsDynamicTextDescriptors.InputAlreadyRebinding();
                case M2bRebindVisualState.RebindingPrompt:
                    return SettingsDynamicTextDescriptors.InputRebindPrompt(
                        KeyboardBindableAction.Push);
                default:
                    return SettingsDynamicTextDescriptors.InputUnsupportedKey();
            }
        }

        private static void ValidateM1bRenderedTargets(
            GameObject prefabRoot,
            TypographyPreviewScreenshotCaptureResult capture,
            Camera camera,
            RenderTexture renderTexture,
            TypographyPreviewScreenshotOptions options,
            IReadOnlyList<Color32> enabledPixels)
        {
            var isM2bTarget = TryGetM2bVisualState(
                capture.Target.FileStem,
                out var m2bState);
            if (!IsM1bDiagnosticTarget(capture.Target.FileStem) && !isM2bTarget)
            {
                return;
            }

            TMP_Text[] targets;
            if (isM2bTarget)
            {
                var expectedStatus = GetM2bStatusDescriptor(m2bState);
                var inputView = prefabRoot.GetComponentInChildren<SettingsInputView>(true);
                var expectedText = capture.LocalizedTexts.LastOrDefault();
                var activeTexts = prefabRoot
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Where(text =>
                        text != null &&
                        text.gameObject.activeInHierarchy)
                    .ToArray();
                var statusTarget = activeTexts
                    .SingleOrDefault(text =>
                        string.Equals(text.text, expectedText, StringComparison.Ordinal));
                var frameProofTargets = activeTexts
                    .Where(text =>
                        string.Equals(text.gameObject.name, "Title", StringComparison.Ordinal) ||
                        string.Equals(text.text, "E", StringComparison.Ordinal) ||
                        string.Equals(text.text, "Q", StringComparison.Ordinal))
                    .ToArray();
                targets = statusTarget == null
                    ? frameProofTargets
                    : new[] { statusTarget }
                        .Concat(frameProofTargets)
                        .Distinct()
                        .ToArray();
                ValidateM2bRenderedState(
                    prefabRoot,
                    capture,
                    inputView,
                    expectedStatus,
                    expectedText,
                    m2bState);
            }
            else if (string.Equals(capture.Target.FileStem, "M1BSaveSlots", StringComparison.Ordinal) ||
                TryGetM2aCampaignSaveStatus(capture.Target.FileStem, out _))
            {
                var panel = prefabRoot.GetComponentInChildren<MainMenuScreenView>(true)?.SaveSlotPanel;
                targets = panel == null
                    ? Array.Empty<TMP_Text>()
                    : panel.CreateTypographyTargets()
                        .Where(text => text != null &&
                                       text.gameObject.activeInHierarchy &&
                                       !string.IsNullOrWhiteSpace(text.text))
                        .ToArray();
            }
            else
            {
                targets = prefabRoot
                    .GetComponentInChildren<ConfirmPopupView>(true)
                    ?.CreateTypographyTargets()
                    .Where(text => text != null &&
                                   text.gameObject.activeInHierarchy &&
                                   !string.IsNullOrWhiteSpace(text.text))
                    .ToArray()
                    ?? Array.Empty<TMP_Text>();
            }

            var expectedCount = isM2bTarget
                ? 4
                : GetExpectedLocalizedTextCount(capture.Target.FileStem);
            if (targets.Length != expectedCount)
            {
                capture.AddError(
                    $"{capture.Target.Name} {capture.LocaleCode}: expected {expectedCount} rendered targets, " +
                    $"found {targets.Length}.");
                return;
            }

            foreach (var target in targets)
            {
                target.ForceMeshUpdate();
                if (!target.isActiveAndEnabled ||
                    target.canvasRenderer.cull ||
                    target.color.a <= 0f ||
                    target.font == null ||
                    target.fontSharedMaterial == null)
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: '{BuildHierarchyPath(target.transform)}' " +
                        "is not an active production text renderer with font/material.");
                    continue;
                }

                if (target.textInfo == null ||
                    target.textInfo.characterCount == 0 ||
                    target.textInfo.meshInfo == null ||
                    target.textInfo.meshInfo.All(mesh => mesh.vertices == null || mesh.vertices.Length == 0))
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: '{BuildHierarchyPath(target.transform)}' " +
                        "has no generated text mesh.");
                    continue;
                }

                if (target.isTextOverflowing)
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: '{BuildHierarchyPath(target.transform)}' " +
                        $"overflows its authored bounds with text '{target.text}'.");
                }

                var corners = new Vector3[4];
                target.rectTransform.GetWorldCorners(corners);
                var screenCorners = corners
                    .Select(corner => RectTransformUtility.WorldToScreenPoint(camera, corner))
                    .ToArray();
                var minX = screenCorners.Min(point => point.x);
                var maxX = screenCorners.Max(point => point.x);
                var minY = screenCorners.Min(point => point.y);
                var maxY = screenCorners.Max(point => point.y);
                if (minX < -0.5f ||
                    minY < -0.5f ||
                    maxX > options.Width + 0.5f ||
                    maxY > options.Height + 0.5f ||
                    maxX <= minX ||
                    maxY <= minY)
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: '{BuildHierarchyPath(target.transform)}' " +
                        $"is outside screen bounds ({minX:F1},{minY:F1})-({maxX:F1},{maxY:F1}).");
                }

                foreach (var character in target.text)
                {
                    if (char.IsControl(character) ||
                        char.IsWhiteSpace(character) ||
                        target.font.HasCharacter(character, searchFallbacks: false, tryAddCharacter: false))
                    {
                        continue;
                    }

                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: '{BuildHierarchyPath(target.transform)}' " +
                        $"requires fallback for '{character}' U+{(int)character:X4}.");
                }

                target.enabled = false;
                ForceGraphicUpdates(prefabRoot);
                camera.Render();
                var diagnostic = new Texture2D(
                    options.Width,
                    options.Height,
                    TextureFormat.RGBA32,
                    mipChain: false);
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = renderTexture;
                    diagnostic.ReadPixels(
                        new Rect(0f, 0f, options.Width, options.Height),
                        0,
                        0,
                        recalculateMipMaps: false);
                    diagnostic.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                }
                finally
                {
                    RenderTexture.active = previous;
                    target.enabled = true;
                }

                var disabledPixels = diagnostic.GetPixels32();
                UnityEngine.Object.DestroyImmediate(diagnostic);
                var pixelDelta = 0;
                for (var i = 0; i < enabledPixels.Count && i < disabledPixels.Length; i++)
                {
                    if (!enabledPixels[i].Equals(disabledPixels[i]))
                    {
                        pixelDelta++;
                    }
                }

                capture.M1bPixelProofCount++;
                if (pixelDelta > 4)
                {
                    capture.M1bPixelProofPassCount++;
                }
                else
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: '{BuildHierarchyPath(target.transform)}' " +
                        $"pixel proof failed with delta={pixelDelta}.");
                }

                Debug.Log(
                    "TYPOGRAPHY_PIXEL_PROOF " +
                    $"target={capture.Target.FileStem} " +
                    $"locale={capture.LocaleCode} " +
                    $"renderer={BuildHierarchyPath(target.transform)} " +
                    $"text={target.text} " +
                    $"pixel_delta={pixelDelta} " +
                    $"result={(pixelDelta > 4 ? "PASS" : "FAIL")}");
            }

            if (capture.M1bPixelProofPassCount != capture.M1bPixelProofCount)
            {
                capture.AddError(
                    $"{capture.Target.Name} {capture.LocaleCode}: pixel proofs passed " +
                    $"{capture.M1bPixelProofPassCount}/{capture.M1bPixelProofCount}.");
            }
        }

        private static void ValidateM2bRenderedState(
            GameObject prefabRoot,
            TypographyPreviewScreenshotCaptureResult capture,
            SettingsInputView inputView,
            LocalizedTextDescriptor expectedStatus,
            string expectedText,
            M2bRebindVisualState state)
        {
            if (inputView == null)
            {
                capture.AddError($"{capture.Target.Name}: SettingsInputView was not found.");
                return;
            }

            if (string.IsNullOrWhiteSpace(expectedText) ||
                !string.Equals(inputView.StatusText, expectedText, StringComparison.Ordinal))
            {
                capture.AddError(
                    $"{capture.Target.Name} {capture.LocaleCode}: production status target does not contain " +
                    $"the resolved {expectedStatus.Table}:{expectedStatus.Key} copy.");
            }

            var expectsActiveRebind =
                state == M2bRebindVisualState.AlreadyRebinding ||
                state == M2bRebindVisualState.RebindingPrompt;
            if (inputView.IsRebindingActive != expectsActiveRebind)
            {
                capture.AddError(
                    $"{capture.Target.Name} {capture.LocaleCode}: active rebind state was " +
                    $"{inputView.IsRebindingActive}, expected {expectsActiveRebind}.");
            }

            var activeTexts = prefabRoot
                .GetComponentsInChildren<TMP_Text>(true)
                .Where(text => text != null && text.gameObject.activeInHierarchy)
                .ToArray();
            if (!activeTexts.Any(text => string.Equals(text.text, "E", StringComparison.Ordinal)) ||
                !activeTexts.Any(text => string.Equals(text.text, "Q", StringComparison.Ordinal)))
            {
                capture.AddError(
                    $"{capture.Target.Name} {capture.LocaleCode}: invariant E/Q keycaps were not rendered.");
            }

            var forbiddenFragments = new[]
            {
                "KeyboardBindableAction",
                "DuplicateAction",
                "ReservedKey",
                "MovementConflict",
                "AlreadyRebinding",
                "InvalidKey",
                "MissingBinding",
                "Exception:",
                "[UI:",
            };
            foreach (var text in activeTexts)
            {
                if (forbiddenFragments.Any(fragment =>
                        text.text.Contains(fragment, StringComparison.Ordinal)))
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: internal identifier or diagnostic " +
                        $"reached '{BuildHierarchyPath(text.transform)}'.");
                }
            }
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            var parts = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                parts.Push(current.name);
            }

            return string.Join("/", parts);
        }

        private static void ValidatePauseRenderedTargets(
            GameObject prefabRoot,
            TypographyPreviewScreenshotCaptureResult capture)
        {
            if (!string.Equals(capture.Target.FileStem, "Pause", StringComparison.Ordinal))
            {
                return;
            }

            var targets = prefabRoot
                .GetComponentsInChildren<TypographyBinding>(true)
                .Select(binding => binding.Target)
                .Where(target => target != null)
                .ToArray();
            if (targets.Length != GetExpectedLocalizedTextCount("Pause"))
            {
                capture.AddError(
                    $"{capture.Target.Name} {capture.LocaleCode}: expected 6 rendered TMP targets, found {targets.Length}.");
                return;
            }

            foreach (var target in targets)
            {
                if (!target.isActiveAndEnabled)
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: localized target '{target.name}' is inactive.");
                }
                else if (target.canvasRenderer.cull)
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: localized target '{target.name}' was culled from the rendered frame.");
                }
                else if (target.color.a <= 0f)
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: localized target '{target.name}' is transparent.");
                }
            }
        }

        private static SettingsSectionId GetSettingsPreviewSection(string fileStem)
        {
            if (string.Equals(fileStem, "SettingsAudioMuted", StringComparison.Ordinal))
            {
                return SettingsSectionId.Audio;
            }

            if (string.Equals(fileStem, "SettingsDisplayStatus", StringComparison.Ordinal))
            {
                return SettingsSectionId.Display;
            }

            return SettingsSectionId.Input;
        }

        private static void ApplySettingsPreviewState(
            GameObject prefabRoot,
            TypographyPreviewScreenshotTarget target,
            CaptureStringTableTextResolver resolver)
        {
            var view = prefabRoot.GetComponentInChildren<SettingsScreenView>(true);
            if (view == null)
            {
                return;
            }

            if (string.Equals(target.FileStem, "SettingsAudioMuted", StringComparison.Ordinal))
            {
                var value = resolver.Resolve(SettingsDynamicTextDescriptors.AudioVolumeValue(25, isMuted: true));
                var row = new AudioSettingsRowViewModel(value, 0.25f, isMuted: true);
                var audioViewModel = new SettingsAudioViewModel();
                audioViewModel.SetContent(row, row, row);
                view.AudioView.Bind(audioViewModel);
                return;
            }

            if (string.Equals(target.FileStem, "SettingsDisplayStatus", StringComparison.Ordinal))
            {
                var displayViewModel = new SettingsDisplayViewModel();
                displayViewModel.SetContent(
                    "1920 x 1080",
                    new[] { "1920 x 1080" },
                    selectedResolutionIndex: 0,
                    isFullscreenEnabled: true,
                    displayStatusText: resolver.Resolve(
                        SettingsDynamicTextDescriptors.DisplayPreviewActiveStatus(15)),
                    isDisplayApplyInteractable: false,
                    isDisplayRevertInteractable: false,
                    isDisplayPreviewActive: true,
                    previewCountdownText: resolver.Resolve(
                        SettingsDynamicTextDescriptors.DisplayPreviewCountdown(15)),
                    previewCountdownNormalized: 1f,
                    isPreviewCountdownVisible: true,
                    isDisplayStatusVisible: true,
                    isDisplayStatusTransient: true,
                    languageLabelText: resolver.Resolve(SettingsStaticTextDescriptors.Language),
                    currentLanguageText: resolver.Resolve(SettingsStaticTextDescriptors.LanguageKorean),
                    isLanguageSelectionAvailable: true,
                    selectedResolutionWidth: 1920,
                    selectedResolutionHeight: 1080);
                view.DisplayView.Bind(displayViewModel);
                return;
            }

            if (TryGetM2bVisualState(target.FileStem, out var m2bState))
            {
                var port = new M2bKeyboardBindingSettingsPort(m2bState);
                var presenter = new SettingsInputPresenter(port, resolver);
                presenter.Apply(new SettingsInputPresenterInput(
                    SettingsStaticTextDescriptors.MovementKeys,
                    SettingsStaticTextDescriptors.UseArrowKeys,
                    SettingsStaticTextDescriptors.Push,
                    SettingsStaticTextDescriptors.Flip,
                    SettingsStaticTextDescriptors.Change,
                    SettingsStaticTextDescriptors.ResetInput));
                var requestedAction = m2bState == M2bRebindVisualState.ActionConflictPush
                    ? KeyboardBindableAction.Flip
                    : KeyboardBindableAction.Push;
                presenter.StartRebind(requestedAction);
                port.CompleteConflictIfNeeded(requestedAction);
                view.InputView.Bind(presenter.ViewModel);
                return;
            }

            if (!string.Equals(target.FileStem, "Settings", StringComparison.Ordinal) ||
                view.InputView == null)
            {
                return;
            }

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
            out Camera camera,
            bool useWorldSpaceCanvas = false)
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
            canvasRect.anchorMin = useWorldSpaceCanvas
                ? new Vector2(0.5f, 0.5f)
                : Vector2.zero;
            canvasRect.anchorMax = useWorldSpaceCanvas
                ? new Vector2(0.5f, 0.5f)
                : Vector2.one;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(options.Width, options.Height);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.localScale = Vector3.one;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = useWorldSpaceCanvas
                ? RenderMode.WorldSpace
                : RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(options.Width, options.Height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            prefabRoot.SetActive(true);
            prefabRoot.transform.SetParent(canvasObject.transform, false);
            ConfigureCanvases(
                prefabRoot,
                camera,
                options.Width,
                options.Height,
                useWorldSpaceCanvas ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera);
        }

        private static void ConfigureCanvases(
            GameObject root,
            Camera camera,
            int width,
            int height,
            RenderMode renderMode)
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = renderMode;
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
                text.SetAllDirty();
                text.ForceMeshUpdate(true, true);
            }
        }

        private static void ForceGraphicUpdates(GameObject root)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is MaskableGraphic maskableGraphic)
                {
                    maskableGraphic.RecalculateClipping();
                    maskableGraphic.RecalculateMasking();
                }

                graphic.SetAllDirty();
            }

            Canvas.ForceUpdateCanvases();
        }

        private static void ForceCanvasGroupsVisible(GameObject root)
        {
            foreach (var canvasGroup in root.GetComponentsInChildren<CanvasGroup>(true))
            {
                canvasGroup.alpha = 1f;
            }
        }

        private static void ValidateLocaleInvariantPreview(
            GameObject root,
            TypographyPreviewScreenshotCaptureResult capture)
        {
            if (!string.Equals(capture.Target.FileStem, "Settings", StringComparison.Ordinal))
            {
                return;
            }

            var activeTargetCount = 0;
            foreach (var binding in root.GetComponentsInChildren<TypographyBinding>(true))
            {
                if (binding.LocaleParticipation != TypographyLocaleParticipation.LocaleInvariant)
                {
                    continue;
                }

                var target = binding.Target;
                var targetName = target != null ? target.name : binding.name;
                if (target == null || !target.isActiveAndEnabled)
                {
                    continue;
                }

                activeTargetCount++;

                if (string.IsNullOrWhiteSpace(target.text))
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: Locale-invariant target '{targetName}' has no display text.");
                    continue;
                }

                if (target.rectTransform.rect.width <= 0f || target.rectTransform.rect.height <= 0f)
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: Locale-invariant target '{targetName}' has invalid capture geometry.");
                    continue;
                }

                var missingGlyphs = target.text
                    .Where(character => !char.IsControl(character) && !char.IsWhiteSpace(character))
                    .Where(character => !HasRenderableCharacter(target.font, character))
                    .Distinct()
                    .Select(character => $"'{character}' U+{(int)character:X4}")
                    .ToArray();
                if (missingGlyphs.Length > 0)
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: Locale-invariant target '{targetName}' is missing glyphs {string.Join(", ", missingGlyphs)}.");
                    continue;
                }

                if (!target.textInfo.characterInfo.Any(character => character.isVisible))
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: Locale-invariant target '{targetName}' produced no visible TMP characters.");
                }

                else if (target.canvasRenderer.cull)
                {
                    capture.AddError(
                        $"{capture.Target.Name} {capture.LocaleCode}: Locale-invariant target '{targetName}' was culled from the rendered frame.");
                }
            }

            if (root.GetComponentsInChildren<TypographyBinding>(true)
                    .Any(binding => binding.LocaleParticipation == TypographyLocaleParticipation.LocaleInvariant) &&
                activeTargetCount == 0)
            {
                capture.AddError(
                    $"{capture.Target.Name} {capture.LocaleCode}: No active locale-invariant target was available for capture.");
            }
        }

        private static Texture2D RenderCameraToTexture(
            Camera camera,
            TypographyPreviewScreenshotOptions options,
            out RenderTexture renderTexture,
            out RenderTexture previousRenderTexture,
            out int cameraRenderPassCount,
            out int captureFrameIndex)
        {
            const int maximumRenderPasses = 8;
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
            Texture2D previousTexture = null;
            string previousHash = null;
            var observedHashes = new List<string>(maximumRenderPasses);
            cameraRenderPassCount = 0;
            captureFrameIndex = -1;
            for (var pass = 1; pass <= maximumRenderPasses; pass++)
            {
                Canvas.ForceUpdateCanvases();
                Graphics.SetRenderTarget(renderTexture);
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, options.BackgroundColor);
                camera.Render();
                Graphics.SetRenderTarget(renderTexture);
                RenderTexture.active = renderTexture;

                var texture = ReadRenderTexture(renderTexture, options);
                var hash = ComputeTextureHash(texture);
                observedHashes.Add(hash);
                cameraRenderPassCount = pass;
                Debug.Log($"CAPTURE_RENDER_PASS index={pass - 1} sha256={hash}");
                if (string.Equals(hash, previousHash, StringComparison.Ordinal))
                {
                    if (previousTexture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(previousTexture);
                    }

                    captureFrameIndex = pass - 1;
                    return texture;
                }

                if (previousTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(previousTexture);
                }

                previousTexture = texture;
                previousHash = hash;
            }

            if (previousTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(previousTexture);
            }

            throw new InvalidOperationException(
                "Capture rendering did not converge to two identical consecutive frames. " +
                $"Observed hashes: {string.Join(", ", observedHashes)}.");
        }

        private static Texture2D ReadRenderTexture(
            RenderTexture renderTexture,
            TypographyPreviewScreenshotOptions options)
        {
            var texture = new Texture2D(
                options.Width,
                options.Height,
                TextureFormat.RGBA32,
                false);
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Graphics.CopyTexture(renderTexture, texture);
            }
            else
            {
                texture.ReadPixels(
                    new Rect(0, 0, options.Width, options.Height),
                    0,
                    0,
                    false);
            }

            texture.Apply();
            return texture;
        }

        private static string ComputeTextureHash(Texture2D texture)
        {
            using var sha = SHA256.Create();
            var bytes = texture.GetRawTextureData<byte>().ToArray();
            return string.Concat(
                sha.ComputeHash(bytes)
                    .Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
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

            private bool TryResolve(Locale locale, LocalizedTextDescriptor descriptor, out string value)
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

            private string ResolveEntry(StringTableEntry entry, LocalizedTextDescriptor descriptor)
            {
                if (descriptor.Arguments.Count == 0)
                {
                    return entry.GetLocalizedString();
                }

                var arguments = new object[descriptor.Arguments.Count];
                for (var i = 0; i < descriptor.Arguments.Count; i++)
                {
                    arguments[i] = descriptor.Arguments[i] is LocalizedTextDescriptor nestedDescriptor
                        ? Resolve(nestedDescriptor)
                        : descriptor.Arguments[i];
                }

                return entry.GetLocalizedString(arguments);
            }
        }

        private enum M2bRebindVisualState
        {
            Reserved = 0,
            ActionConflictFlip = 1,
            ActionConflictPush = 2,
            MovementConflict = 3,
            AlreadyRebinding = 4,
            RebindingPrompt = 5,
        }

        private sealed class M2bKeyboardBindingSettingsPort : IKeyboardBindingSettingsPort
        {
            private readonly M2bRebindVisualState state;
            private Action<KeyboardRebindResult> completed;
            private KeyboardBindingSettingsSnapshot snapshot;

            public M2bKeyboardBindingSettingsPort(M2bRebindVisualState state)
            {
                this.state = state;
                snapshot = CreateSnapshot(
                    isRebinding: state == M2bRebindVisualState.AlreadyRebinding,
                    rebindingAction: state == M2bRebindVisualState.AlreadyRebinding
                        ? KeyboardBindableAction.Flip
                        : null);
            }

            public bool IsRebinding => snapshot.IsRebinding;

            public KeyboardBindingSettingsSnapshot Read()
            {
                return snapshot;
            }

            public KeyboardBindingValidationResult TrySetMovementScheme(
                KeyboardMovementScheme scheme)
            {
                return KeyboardBindingValidationResult.Success;
            }

            public KeyboardRebindStartResult StartRebind(
                KeyboardBindableAction action,
                Action<KeyboardRebindResult> completed)
            {
                this.completed = completed;
                switch (state)
                {
                    case M2bRebindVisualState.Reserved:
                        return Reject(KeyboardBindingValidationResult.ReservedKey);
                    case M2bRebindVisualState.MovementConflict:
                        return Reject(KeyboardBindingValidationResult.MovementConflict);
                    case M2bRebindVisualState.AlreadyRebinding:
                        return Reject(KeyboardBindingValidationResult.AlreadyRebinding);
                    default:
                        snapshot = CreateSnapshot(isRebinding: true, rebindingAction: action);
                        return new KeyboardRebindStartResult(
                            true,
                            KeyboardBindingValidationResult.Success,
                            snapshot);
                }
            }

            public void CompleteConflictIfNeeded(KeyboardBindableAction requestedAction)
            {
                KeyboardBindableAction? conflictingAction = null;
                if (state == M2bRebindVisualState.ActionConflictFlip)
                {
                    conflictingAction = KeyboardBindableAction.Flip;
                }
                else if (state == M2bRebindVisualState.ActionConflictPush)
                {
                    conflictingAction = KeyboardBindableAction.Push;
                }

                if (!conflictingAction.HasValue)
                {
                    return;
                }

                snapshot = CreateSnapshot(isRebinding: false, rebindingAction: null);
                completed?.Invoke(new KeyboardRebindResult(
                    requestedAction,
                    KeyboardBindingValidationResult.DuplicateAction,
                    snapshot,
                    conflictingAction));
            }

            public void CancelRebind()
            {
                snapshot = CreateSnapshot(isRebinding: false, rebindingAction: null);
            }

            public KeyboardBindingSettingsSnapshot ResetToDefaults()
            {
                snapshot = CreateSnapshot(isRebinding: false, rebindingAction: null);
                return snapshot;
            }

            private KeyboardRebindStartResult Reject(
                KeyboardBindingValidationResult validationResult)
            {
                return new KeyboardRebindStartResult(false, validationResult, snapshot);
            }

            private static KeyboardBindingSettingsSnapshot CreateSnapshot(
                bool isRebinding,
                KeyboardBindableAction? rebindingAction)
            {
                return new KeyboardBindingSettingsSnapshot(
                    KeyboardMovementScheme.Wasd,
                    "WASD",
                    "E",
                    "Q",
                    isRebinding,
                    rebindingAction);
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

        private sealed class LocaleInvariantTypographyScope : IDisposable
        {
            private readonly List<LocaleInvariantTypographySnapshot> snapshots;
            private bool isRestored;

            private LocaleInvariantTypographyScope(List<LocaleInvariantTypographySnapshot> snapshots)
            {
                this.snapshots = snapshots;
            }

            public static LocaleInvariantTypographyScope Capture(GameObject root)
            {
                var snapshots = new List<LocaleInvariantTypographySnapshot>();
                if (root == null)
                {
                    return new LocaleInvariantTypographyScope(snapshots);
                }

                foreach (var binding in root.GetComponentsInChildren<TypographyBinding>(true))
                {
                    if (binding.LocaleParticipation != TypographyLocaleParticipation.LocaleInvariant ||
                        binding.Target == null)
                    {
                        continue;
                    }

                    snapshots.Add(LocaleInvariantTypographySnapshot.Capture(binding.Target));
                }

                return new LocaleInvariantTypographyScope(snapshots);
            }

            public void Restore()
            {
                if (isRestored)
                {
                    return;
                }

                isRestored = true;
                foreach (var snapshot in snapshots)
                {
                    snapshot.Restore();
                }
            }

            public void Dispose()
            {
                Restore();
            }

            private readonly struct LocaleInvariantTypographySnapshot
            {
                private readonly TMP_Text target;
                private readonly TMP_FontAsset font;
                private readonly Material material;
                private readonly FontStyles fontStyle;
                private readonly float fontSize;
                private readonly bool enableAutoSizing;
                private readonly float fontSizeMin;
                private readonly float fontSizeMax;
                private readonly float lineSpacing;
                private readonly float characterSpacing;

                private LocaleInvariantTypographySnapshot(TMP_Text target)
                {
                    this.target = target;
                    font = target.font;
                    material = target.fontSharedMaterial;
                    fontStyle = target.fontStyle;
                    fontSize = target.fontSize;
                    enableAutoSizing = target.enableAutoSizing;
                    fontSizeMin = target.fontSizeMin;
                    fontSizeMax = target.fontSizeMax;
                    lineSpacing = target.lineSpacing;
                    characterSpacing = target.characterSpacing;
                }

                public static LocaleInvariantTypographySnapshot Capture(TMP_Text target)
                {
                    return new LocaleInvariantTypographySnapshot(target);
                }

                public void Restore()
                {
                    if (target == null)
                    {
                        return;
                    }

                    target.font = font;
                    target.fontSharedMaterial = material;
                    target.fontStyle = fontStyle;
                    target.fontSize = fontSize;
                    target.enableAutoSizing = enableAutoSizing;
                    target.fontSizeMin = fontSizeMin;
                    target.fontSizeMax = fontSizeMax;
                    target.lineSpacing = lineSpacing;
                    target.characterSpacing = characterSpacing;
                }
            }
        }

    }

    public sealed class CaptureAssetMutationEvidence
    {
        internal CaptureAssetMutationEvidence(
            string path,
            string beforeHash,
            string afterCaptureHash,
            bool mutationDetected,
            string changedProperties,
            string classification,
            bool allowed)
        {
            Path = path;
            BeforeHash = beforeHash;
            AfterCaptureHash = afterCaptureHash;
            MutationDetected = mutationDetected;
            ChangedProperties = changedProperties;
            Classification = classification;
            Allowed = allowed;
            LaneVerdictBeforeRestore = allowed ? "PASS" : "FAIL";
        }

        public string Path { get; }

        public string BeforeHash { get; }

        public string AfterCaptureHash { get; }

        public bool MutationDetected { get; }

        public string ChangedProperties { get; }

        public string Classification { get; }

        public bool Allowed { get; }

        public string LaneVerdictBeforeRestore { get; }

        public bool Restored { get; internal set; }

        public string RestoredHash { get; internal set; } = string.Empty;
    }

    public sealed class CaptureAssetMutationGuard : IDisposable
    {
        public const string BaselineRootCommandLineArgument =
            "-captureAssetBaselineRoot";
        public const string ClimateFontAssetPath =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset";

        private readonly Dictionary<string, byte[]> snapshots =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly string baselineRoot;
        private IReadOnlyList<CaptureAssetMutationEvidence> evidence =
            Array.Empty<CaptureAssetMutationEvidence>();
        private bool observed;
        private bool disposed;

        private CaptureAssetMutationGuard(IEnumerable<string> paths)
        {
            baselineRoot = ReadCommandLineArgument(
                Environment.GetCommandLineArgs(),
                BaselineRootCommandLineArgument);
            foreach (var path in paths ?? Array.Empty<string>())
            {
                IncludePath(path);
            }
        }

        public IReadOnlyList<CaptureAssetMutationEvidence> Evidence => evidence;

        public static CaptureAssetMutationGuard Capture(IEnumerable<string> paths)
        {
            return new CaptureAssetMutationGuard(paths);
        }

        public void IncludeFontAssets(GameObject root)
        {
            if (root == null || observed || disposed)
            {
                return;
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
                IncludePath(AssetDatabase.GetAssetPath(fontAsset));
            }
        }

        public IReadOnlyList<CaptureAssetMutationEvidence> ObserveBeforeRestore()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(CaptureAssetMutationGuard));
            }

            if (observed)
            {
                return evidence;
            }

            observed = true;
            evidence = snapshots
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => Observe(pair.Key, pair.Value))
                .ToArray();
            return evidence;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (!observed)
            {
                ObserveBeforeRestore();
            }

            disposed = true;
            foreach (var snapshot in snapshots)
            {
                if (!File.Exists(snapshot.Key) ||
                    !File.ReadAllBytes(snapshot.Key).SequenceEqual(snapshot.Value))
                {
                    File.WriteAllBytes(snapshot.Key, snapshot.Value);
                }

                ClearDirty(snapshot.Key);
                var restoredBytes = File.ReadAllBytes(snapshot.Key);
                var record = evidence.First(item =>
                    string.Equals(item.Path, snapshot.Key, StringComparison.Ordinal));
                record.RestoredHash = ComputeSha256(restoredBytes);
                record.Restored = restoredBytes.SequenceEqual(snapshot.Value);
            }
        }

        internal static CaptureAssetMutationEvidence ClassifyForTests(
            string path,
            byte[] before,
            byte[] after)
        {
            return Observe(path, before, after);
        }

        private void IncludePath(string path)
        {
            if (observed ||
                disposed ||
                string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !File.Exists(path) ||
                snapshots.ContainsKey(path))
            {
                return;
            }

            var baselinePath = string.IsNullOrWhiteSpace(baselineRoot)
                ? string.Empty
                : Path.Combine(
                    baselineRoot,
                    path.Replace('/', Path.DirectorySeparatorChar));
            snapshots.Add(
                path,
                baselinePath.Length > 0 && File.Exists(baselinePath)
                    ? File.ReadAllBytes(baselinePath)
                    : File.ReadAllBytes(path));
        }

        private static CaptureAssetMutationEvidence Observe(string path, byte[] before)
        {
            var after = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
            return Observe(path, before, after);
        }

        private static CaptureAssetMutationEvidence Observe(
            string path,
            byte[] before,
            byte[] after)
        {
            before ??= Array.Empty<byte>();
            after ??= Array.Empty<byte>();
            var mutationDetected = !before.SequenceEqual(after);
            if (!mutationDetected)
            {
                return new CaptureAssetMutationEvidence(
                    path,
                    ComputeSha256(before),
                    ComputeSha256(after),
                    mutationDetected: false,
                    changedProperties: string.Empty,
                    classification: "NO_MUTATION",
                    allowed: true);
            }

            var allowed = TryClassifyAllowedClimateScaleRatioDrift(
                path,
                before,
                after,
                out var changedProperties);
            return new CaptureAssetMutationEvidence(
                path,
                ComputeSha256(before),
                ComputeSha256(after),
                mutationDetected: true,
                changedProperties,
                allowed
                    ? "EXPECTED_IMPORT_DERIVED_DRIFT"
                    : "UNEXPECTED_ASSET_MUTATION",
                allowed);
        }

        private static bool TryClassifyAllowedClimateScaleRatioDrift(
            string path,
            byte[] before,
            byte[] after,
            out string changedProperties)
        {
            changedProperties = "binary-or-unclassified";
            if (!string.Equals(path, ClimateFontAssetPath, StringComparison.Ordinal))
            {
                return false;
            }

            var beforeLines = DecodeLines(before);
            var afterLines = DecodeLines(after);
            if (beforeLines.Length != afterLines.Length)
            {
                return false;
            }

            var changed = new List<string>();
            var requiredWhitespaceProperties = new HashSet<string>(
                new[]
                {
                    "m_MipmapLimitGroupName:",
                    "m_PlatformBlob:",
                    "path:",
                    "referencedFontAssetGUID:",
                    "referencedTextAssetGUID:",
                    "m_SourceFontFilePath:",
                    "Name:",
                    "m_LockedProperties:",
                },
                StringComparer.Ordinal);
            var observedWhitespaceProperties = new HashSet<string>(
                StringComparer.Ordinal);
            for (var index = 0; index < beforeLines.Length; index++)
            {
                if (string.Equals(beforeLines[index], afterLines[index], StringComparison.Ordinal))
                {
                    continue;
                }

                var beforeValue = beforeLines[index].Trim();
                var afterValue = afterLines[index].Trim();
                if (string.Equals(beforeValue, afterValue, StringComparison.Ordinal) &&
                    requiredWhitespaceProperties.Contains(beforeValue) &&
                    string.Equals(
                        afterLines[index],
                        beforeLines[index] + " ",
                        StringComparison.Ordinal))
                {
                    observedWhitespaceProperties.Add(beforeValue);
                    changed.Add($"serialization-whitespace:{beforeValue}");
                    continue;
                }

                if (string.Equals(beforeValue, "- _ScaleRatioA: 1", StringComparison.Ordinal) &&
                    string.Equals(afterValue, "- _ScaleRatioA: 0.9", StringComparison.Ordinal))
                {
                    changed.Add("_ScaleRatioA:1->0.9");
                    continue;
                }

                if (string.Equals(beforeValue, "- _ScaleRatioC: 1", StringComparison.Ordinal) &&
                    string.Equals(afterValue, "- _ScaleRatioC: 0.73125", StringComparison.Ordinal))
                {
                    changed.Add("_ScaleRatioC:1->0.73125");
                    continue;
                }

                return false;
            }

            changedProperties = string.Join(",", changed);
            var hasScaleRatioA = changed.Contains("_ScaleRatioA:1->0.9");
            var hasScaleRatioC = changed.Contains("_ScaleRatioC:1->0.73125");
            return observedWhitespaceProperties.SetEquals(requiredWhitespaceProperties) &&
                   hasScaleRatioA == hasScaleRatioC;
        }

        private static string[] DecodeLines(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes ?? Array.Empty<byte>())
                .Replace("\r\n", "\n")
                .Split('\n');
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(bytes ?? Array.Empty<byte>()))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static void CollectFontAssets(
            TMP_FontAsset fontAsset,
            HashSet<TMP_FontAsset> fontAssets)
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

        private static string ReadCommandLineArgument(
            IReadOnlyList<string> args,
            string key)
        {
            for (var index = 0; index < args.Count - 1; index++)
            {
                if (string.Equals(args[index], key, StringComparison.Ordinal))
                {
                    return Path.GetFullPath(args[index + 1]);
                }
            }

            return string.Empty;
        }
    }
}

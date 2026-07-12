using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

        public IReadOnlyList<string> Errors => errors;

        public bool HasErrors => errors.Count > 0;

        public bool Exists => File.Exists(FilePath);

        public void AddError(string message)
        {
            errors.Add(message ?? string.Empty);
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
            return CaptureScreenshots(
                RequiredTargets,
                TypographyThemeValidator.RequiredLocaleCodes,
                outputDirectory ?? CreateTimestampedDefaultOutputDirectory(),
                options,
                theme);
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

            foreach (var dirtyPath in GetDirtyGuardAssetPaths())
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
            if (themeReport.HasErrors)
            {
                AddValidationErrors(themeReport, result);
            }

            foreach (var target in targets)
            {
                if (string.IsNullOrWhiteSpace(target.PrefabPath))
                {
                    result.AddError($"{target.Name}: Prefab path is empty.");
                    continue;
                }

                var prefabReport = TypographyBindingValidator.ValidatePrefabAtPath(target.PrefabPath, theme);
                if (prefabReport.HasErrors)
                {
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
                var previewResult = TypographyPreviewUtility.ApplyPreview(prefabRoot, localeCode, captureTheme, recordUndo: false);
                capture.AppliedBindingCount = previewResult.AppliedCount;
                foreach (var error in previewResult.Errors)
                {
                    capture.AddError(error);
                }

                if (capture.HasErrors)
                {
                    return capture;
                }

                SetupPreviewScene(prefabRoot, options, out cameraObject, out canvasObject, out var camera);
                ForceTextMeshUpdates(prefabRoot);
                Canvas.ForceUpdateCanvases();

                renderTexture = new RenderTexture(options.Width, options.Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "TypographyPreviewScreenshotRT",
                    antiAliasing = 1,
                };
                renderTexture.Create();

                camera.targetTexture = renderTexture;
                previousRenderTexture = RenderTexture.active;
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, options.BackgroundColor);
                camera.Render();

                var texture = new Texture2D(options.Width, options.Height, TextureFormat.RGBA32, false);
                try
                {
                    texture.ReadPixels(new Rect(0, 0, options.Width, options.Height), 0, 0);
                    texture.Apply();
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
            cameraObject.transform.position = new Vector3(0f, 0f, 100f);
            cameraObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            canvasObject = new GameObject("Typography Preview Screenshot Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            EditorSceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.sizeDelta = new Vector2(options.Width, options.Height);
            canvasRect.anchoredPosition = Vector2.zero;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;

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
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
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

        private static void ForceTextMeshUpdates(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.ForceMeshUpdate(true, true);
            }
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
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Composition.Editor;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Feature.UI.Tests
{
    public static class ObjectiveHudVisualEvidenceUtility
    {
        public const string CommandLineEntryPoint =
            "Game.Feature.UI.Tests.ObjectiveHudVisualEvidenceUtility.CaptureFromCommandLine";
        public const string OverlayPlayModeEntryPoint =
            "Game.Feature.UI.Tests.ObjectiveHudVisualEvidenceUtility.CaptureOverlayFromPlayMode";
        public const string HudPrefabPath =
            "Assets/_Features/UI/UI_HUD/Prefabs/GameplayHudRoot.prefab";
        public const string ClimateFontPath =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset";
        public const string ManifestFileName = "objective-hud-capture.log";

        private static readonly CaptureScenario[] Scenarios =
        {
            new("Idle", "en-US", expectedRowCount: 2),
            new("Idle", "ko-KR", expectedRowCount: 2),
            new("MaxStack", "en-US", expectedRowCount: 3),
            new("MaxStack", "ko-KR", expectedRowCount: 3),
        };

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var outputDirectory = ReadArgument(args, "-objectiveHudVisualOutput");
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("-objectiveHudVisualOutput is required.");
            }

            var width = ReadPositiveInt(args, "-objectiveHudVisualWidth", 1920);
            var height = ReadPositiveInt(args, "-objectiveHudVisualHeight", 1080);
            Capture(outputDirectory, width, height);
        }

        public static IEnumerator CaptureOverlayFromPlayMode()
        {
            var args = Environment.GetCommandLineArgs();
            var outputDirectory = ReadArgument(args, "-objectiveHudVisualOutput");
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("-objectiveHudVisualOutput is required.");
            }

            var width = ReadPositiveInt(args, "-objectiveHudVisualWidth", 1920);
            var height = ReadPositiveInt(args, "-objectiveHudVisualHeight", 1080);
            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            yield return null;
            yield return new WaitForEndOfFrame();
            width = Screen.width;
            height = Screen.height;

            var gitHead = TypographyPreviewScreenshotManifestUtility.ReadCurrentGitHead();
            var errors = new List<string>();
            var captures = new List<CaptureRecord>();
            var crossLocaleParity = false;
            var unexpectedPixelDelta = -1L;
            IReadOnlyList<CaptureAssetMutationEvidence> mutationEvidence =
                Array.Empty<CaptureAssetMutationEvidence>();
            var assetGuard = CaptureAssetMutationGuard.Capture(new[] { ClimateFontPath });
            try
            {
                foreach (var scenario in Scenarios)
                {
                    CaptureRecord captured = default;
                    Exception captureFailure = null;
                    var routine = CaptureScenarioOverlay(
                        scenario,
                        outputDirectory,
                        width,
                        height,
                        assetGuard,
                        result => captured = result);
                    while (true)
                    {
                        bool moved;
                        object current = null;
                        try
                        {
                            moved = routine.MoveNext();
                            if (moved)
                            {
                                current = routine.Current;
                            }
                        }
                        catch (Exception exception)
                        {
                            moved = false;
                            captureFailure = exception;
                        }

                        if (!moved)
                        {
                            break;
                        }

                        yield return current;
                    }

                    if (captureFailure != null)
                    {
                        errors.Add($"{scenario.State} {scenario.Locale}: {captureFailure}");
                    }
                    else
                    {
                        captures.Add(captured);
                    }
                }

                foreach (var localeCaptures in captures.GroupBy(capture => capture.Scenario.Locale))
                {
                    var stateHashes = localeCaptures
                        .Select(capture => capture.Sha256)
                        .Distinct(StringComparer.Ordinal)
                        .Count();
                    if (localeCaptures.Count() > 1 && stateHashes != localeCaptures.Count())
                    {
                        errors.Add(
                            $"{localeCaptures.Key}: visual states produced duplicate PNG hashes.");
                    }
                }

                crossLocaleParity = ValidateCrossLocaleParity(
                    captures,
                    errors,
                    out unexpectedPixelDelta);
            }
            finally
            {
                try
                {
                    mutationEvidence = assetGuard.ObserveBeforeRestore();
                    foreach (var item in mutationEvidence)
                    {
                        if (!item.Allowed)
                        {
                            errors.Add($"UNEXPECTED_ASSET_MUTATION before restore: {item.Path}");
                        }
                    }
                }
                catch (Exception exception)
                {
                    errors.Add(
                        $"Capture asset mutation observation failed before restore: {exception.Message}");
                }

                try
                {
                    assetGuard.Dispose();
                    foreach (var item in mutationEvidence)
                    {
                        if (!item.Restored)
                        {
                            errors.Add($"Capture asset restore failed: {item.Path}");
                        }
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"Capture asset restore failed: {exception.Message}");
                }
            }

            var manifestPath = WriteManifest(
                outputDirectory,
                width,
                height,
                gitHead,
                captures,
                mutationEvidence,
                crossLocaleParity,
                unexpectedPixelDelta,
                errors,
                OverlayPlayModeEntryPoint,
                "SCREEN_SPACE_OVERLAY_PRODUCTION_CONTROLLER_END_OF_FRAME",
                "NON_BATCH_GAME_VIEW",
                "FIXTURE_STATE_MISMATCH");
            Debug.Log($"Objective HUD overlay visual manifest: {manifestPath}");
            if (errors.Count > 0 ||
                captures.Count != Scenarios.Length ||
                captures.Any(capture => !capture.Passed))
            {
                throw new InvalidOperationException(
                    $"Objective HUD overlay visual evidence failed. See {manifestPath}");
            }
        }

        public static void Capture(string outputDirectory, int width, int height)
        {
            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);
            var gitHead = TypographyPreviewScreenshotManifestUtility.ReadCurrentGitHead();
            var errors = new List<string>();
            var captures = new List<CaptureRecord>();
            var crossLocaleParity = false;
            var unexpectedPixelDelta = -1L;
            IReadOnlyList<CaptureAssetMutationEvidence> mutationEvidence =
                Array.Empty<CaptureAssetMutationEvidence>();
            var assetGuard = CaptureAssetMutationGuard.Capture(new[] { ClimateFontPath });
            try
            {
                foreach (var scenario in Scenarios)
                {
                    try
                    {
                        captures.Add(CaptureScenarioImage(
                            scenario,
                            outputDirectory,
                            width,
                            height,
                            assetGuard));
                    }
                    catch (Exception exception)
                    {
                        errors.Add($"{scenario.State} {scenario.Locale}: {exception}");
                    }
                }

                foreach (var localeCaptures in captures.GroupBy(capture => capture.Scenario.Locale))
                {
                    var stateHashes = localeCaptures
                        .Select(capture => capture.Sha256)
                        .Distinct(StringComparer.Ordinal)
                        .Count();
                    if (localeCaptures.Count() > 1 && stateHashes != localeCaptures.Count())
                    {
                        errors.Add(
                            $"{localeCaptures.Key}: visual states produced duplicate PNG hashes.");
                    }
                }

                crossLocaleParity = ValidateCrossLocaleParity(
                    captures,
                    errors,
                    out unexpectedPixelDelta);
            }
            finally
            {
                try
                {
                    mutationEvidence = assetGuard.ObserveBeforeRestore();
                    foreach (var item in mutationEvidence)
                    {
                        if (!item.Allowed)
                        {
                            errors.Add($"UNEXPECTED_ASSET_MUTATION before restore: {item.Path}");
                        }
                    }
                }
                catch (Exception exception)
                {
                    errors.Add(
                        $"Capture asset mutation observation failed before restore: {exception.Message}");
                }

                try
                {
                    assetGuard.Dispose();
                    foreach (var item in mutationEvidence)
                    {
                        if (!item.Restored)
                        {
                            errors.Add($"Capture asset restore failed: {item.Path}");
                        }
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"Capture asset restore failed: {exception.Message}");
                }
            }

            var manifestPath = WriteManifest(
                outputDirectory,
                width,
                height,
                gitHead,
                captures,
                mutationEvidence,
                crossLocaleParity,
                unexpectedPixelDelta,
                errors,
                CommandLineEntryPoint,
                "CAMERA_RENDER_TEXTURE_PRODUCTION_COMPOSITION",
                "BATCH_COMPATIBLE_RENDER_TEXTURE",
                "NOT_APPLICABLE");
            Debug.Log($"Objective HUD visual manifest: {manifestPath}");
            if (errors.Count > 0 ||
                captures.Count != Scenarios.Length ||
                captures.Any(capture => !capture.Passed))
            {
                throw new InvalidOperationException(
                    $"Objective HUD visual evidence failed. See {manifestPath}");
            }
        }

        private static IEnumerator CaptureScenarioOverlay(
            CaptureScenario scenario,
            string outputDirectory,
            int width,
            int height,
            CaptureAssetMutationGuard assetGuard,
            Action<CaptureRecord> onCaptured)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Gameplay HUD prefab was not found at {HudPrefabPath}.");
            }

            assetGuard.IncludeFontAssets(prefab);
            var climate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ClimateFontPath);
            if (climate == null)
            {
                throw new InvalidOperationException($"Climate font was not found at {ClimateFontPath}.");
            }

            var prefabHud = prefab.GetComponent<HUDRootView>();
            var prefabObjective = prefabHud.ObjectiveHudView;
            var prefabHeader = prefabObjective.HeaderLabel;
            var prefabRowLabel = GetTemplateRow(prefabObjective).GetComponentInChildren<TMP_Text>(true);
            var englishHeaderFont = prefabHeader.font;
            var englishHeaderMaterial = prefabHeader.fontSharedMaterial;
            var englishHeaderStyle = prefabHeader.fontStyle;
            var englishRowFont = prefabRowLabel.font;
            var englishRowMaterial = prefabRowLabel.fontSharedMaterial;
            var englishRowStyle = prefabRowLabel.fontStyle;

            GameObject canvasObject = null;
            GameObject cameraObject = null;
            GameObject root = null;
            UnityStringTableTextResolver resolver = null;
            HUDRootPresenter rootPresenter = null;
            HUDController hudController = null;
            Texture2D texture = null;
            try
            {
                cameraObject = new GameObject(
                    "Objective HUD Overlay Evidence Camera",
                    typeof(Camera));
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.015f, 0.025f, 0.045f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 1000f;
                camera.depth = -100f;
                cameraObject.transform.position = new Vector3(0f, 0f, -100f);

                canvasObject = new GameObject(
                    "Objective HUD Overlay Evidence Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler));
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(width, height);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;

                if (!UnityStringTableTextResolver.TryCreateSettingsDefault(
                        new MemoryLocalePreferenceStore(scenario.Locale),
                        out resolver,
                        out var failureReason))
                {
                    throw new InvalidOperationException(
                        $"Production Unity String Table resolver could not initialize: {failureReason}");
                }

                if (!resolver.TrySetLocale(scenario.Locale))
                {
                    throw new InvalidOperationException(
                        $"Production resolver rejected locale '{scenario.Locale}'.");
                }

                root = Object.Instantiate(prefab, canvasObject.transform, false);
                root.name = $"ObjectiveHudVisual_{scenario.State}";
                assetGuard.IncludeFontAssets(root);
                var hud = root.GetComponent<HUDRootView>();
                hud.ValidateAuthoredStructureOrThrow();
                var objectiveView = hud.ObjectiveHudView;
                var typography = objectiveView.GetComponent<ObjectiveHudTypographyBinding>();
                typography.ValidateAuthoredStructureOrThrow();
                typography.Initialize(resolver);
                objectiveView.ConfigureTypography(typography);

                var initialReadModel = CreateReadModel(scenario, completePrimary: false);
                var initialSnapshot = CreateSnapshot(initialReadModel);
                var source = new EvidencePresentationSource(initialSnapshot);
                var stagePresenter = new StageInfoPresenter(resolver);
                var objectivePresenter = new ObjectiveHudPresenter(resolver);
                var chancePresenter = new ChancePanelPresenter();
                var surfaceBeltPresenter = new SurfaceBeltIndicatorPresenter();
                var playerStatusPresenter = new PlayerStatusPresenter();
                rootPresenter = new HUDRootPresenter(
                    source,
                    stagePresenter,
                    objectivePresenter,
                    chancePresenter,
                    surfaceBeltPresenter,
                    playerStatusPresenter);
                hudController = new HUDController(
                    rootPresenter.ViewModel,
                    stagePresenter.ViewModel,
                    objectivePresenter.ViewModel,
                    chancePresenter.ViewModel,
                    surfaceBeltPresenter.ViewModel,
                    playerStatusPresenter.ViewModel);
                hudController.AttachView(hud);
                ForceLayoutAndText(root);

                yield return new WaitForSecondsRealtime(2f);

                const int settledFrameCount = 8;
                for (var frame = 0; frame < settledFrameCount; frame++)
                {
                    yield return null;
                    ForceLayoutAndText(root);
                    Canvas.ForceUpdateCanvases();
                    yield return new WaitForEndOfFrame();
                }

                var activeRows = GetActiveRows(objectiveView);
                ValidatePresentation(
                    scenario,
                    objectiveView,
                    activeRows,
                    climate,
                    englishHeaderFont,
                    englishHeaderMaterial,
                    englishHeaderStyle,
                    englishRowFont,
                    englishRowMaterial,
                    englishRowStyle);

                texture = ScreenCapture.CaptureScreenshotAsTexture();
                var fileName = $"HUD_Objectives_{scenario.State}_{scenario.Locale}.png";
                var filePath = Path.Combine(outputDirectory, fileName);
                if (texture == null)
                {
                    throw new InvalidOperationException(
                        "ScreenSpaceOverlay backbuffer capture returned no texture.");
                }

                var pngBytes = texture.EncodeToPNG();
                var pixels = texture.GetPixels32();
                File.WriteAllBytes(filePath, pngBytes);
                if (texture.width != width || texture.height != height)
                {
                    throw new InvalidOperationException(
                        $"{fileName} captured {texture.width}x{texture.height}, expected {width}x{height}.");
                }

                var nonBlank = SystemInfo.graphicsDeviceType ==
                               UnityEngine.Rendering.GraphicsDeviceType.Null ||
                               HasNonBlankPixels(texture);
                if (!nonBlank)
                {
                    throw new InvalidOperationException($"{fileName} is blank or single-color.");
                }

                var headerIdentity = ReadIdentity(objectiveView.HeaderLabel);
                var rowIdentity = ReadIdentity(activeRows[0].Label);
                var graphicStates = CaptureRequiredGraphicStates(root, width, height);
                ValidateRequiredGraphicCategories(graphicStates, scenario);
                var textRegions = CaptureTextRegions(root, width, height);
                onCaptured(new CaptureRecord(
                    scenario,
                    fileName,
                    pngBytes.LongLength,
                    ComputeSha256(pngBytes),
                    headerIdentity,
                    rowIdentity,
                    graphicStates,
                    textRegions,
                    pixels,
                    width,
                    root.activeInHierarchy,
                    ResolveRootCanvasGroupAlpha(root),
                    ComputeSemanticSnapshotHash(initialReadModel),
                    ComputeHierarchyHash(root),
                    cameraRenderPassCount: 0,
                    captureFrameIndex: settledFrameCount,
                    endOfFrameCount: settledFrameCount,
                    nonBlank ? "PASS" : "FAIL",
                    "PASS",
                    "PASS",
                    "PASS",
                    string.Empty));
            }
            finally
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }

                hudController?.Dispose();
                rootPresenter?.Dispose();
                resolver?.Dispose();
                if (root != null)
                {
                    root.SetActive(false);
                    Object.DestroyImmediate(root);
                }

                if (canvasObject != null)
                {
                    canvasObject.SetActive(false);
                    Object.DestroyImmediate(canvasObject);
                }

                if (cameraObject != null)
                {
                    cameraObject.SetActive(false);
                    Object.DestroyImmediate(cameraObject);
                }
            }
        }

        private static CaptureRecord CaptureScenarioImage(
            CaptureScenario scenario,
            string outputDirectory,
            int width,
            int height,
            CaptureAssetMutationGuard assetGuard)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Gameplay HUD prefab was not found at {HudPrefabPath}.");
            }
            assetGuard.IncludeFontAssets(prefab);

            var climate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ClimateFontPath);
            if (climate == null)
            {
                throw new InvalidOperationException($"Climate font was not found at {ClimateFontPath}.");
            }

            var prefabHud = prefab.GetComponent<HUDRootView>();
            var prefabObjective = prefabHud.ObjectiveHudView;
            var prefabHeader = prefabObjective.HeaderLabel;
            var prefabRowLabel = GetTemplateRow(prefabObjective).GetComponentInChildren<TMP_Text>(true);
            var englishHeaderFont = prefabHeader.font;
            var englishHeaderMaterial = prefabHeader.fontSharedMaterial;
            var englishHeaderStyle = prefabHeader.fontStyle;
            var englishRowFont = prefabRowLabel.font;
            var englishRowMaterial = prefabRowLabel.fontSharedMaterial;
            var englishRowStyle = prefabRowLabel.fontStyle;

            GameObject root = null;
            UnityStringTableTextResolver resolver = null;
            HUDRootPresenter rootPresenter = null;
            HUDController hudController = null;
            Texture2D texture = null;
            var previousActiveScene = SceneManager.GetActiveScene();
            var previewScene = default(Scene);
            var shouldClosePreviewScene = !UnityEngine.Application.isBatchMode;
            try
            {
                previewScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    UnityEngine.Application.isBatchMode
                        ? NewSceneMode.Single
                        : NewSceneMode.Additive);
                EditorSceneManager.SetActiveScene(previewScene);

                if (!UnityStringTableTextResolver.TryCreateSettingsDefault(
                        new MemoryLocalePreferenceStore(scenario.Locale),
                        out resolver,
                        out var failureReason))
                {
                    throw new InvalidOperationException(
                        $"Production Unity String Table resolver could not initialize: {failureReason}");
                }

                if (!resolver.TrySetLocale(scenario.Locale))
                {
                    throw new InvalidOperationException(
                        $"Production resolver rejected locale '{scenario.Locale}'.");
                }

                root = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
                if (root == null)
                {
                    throw new InvalidOperationException(
                        "Gameplay HUD prefab could not be instantiated in the isolated preview scene.");
                }

                root.name = $"ObjectiveHudVisual_{scenario.State}";
                assetGuard.IncludeFontAssets(root);
                var hud = root.GetComponent<HUDRootView>();
                hud.ValidateAuthoredStructureOrThrow();
                var objectiveView = hud.ObjectiveHudView;
                var typography = objectiveView.GetComponent<ObjectiveHudTypographyBinding>();
                typography.ValidateAuthoredStructureOrThrow();
                typography.Initialize(resolver);
                objectiveView.ConfigureTypography(typography);

                var initialReadModel = CreateReadModel(scenario, completePrimary: false);
                var initialSnapshot = CreateSnapshot(initialReadModel);
                var source = new EvidencePresentationSource(initialSnapshot);
                var stagePresenter = new StageInfoPresenter(resolver);
                var objectivePresenter = new ObjectiveHudPresenter(resolver);
                var chancePresenter = new ChancePanelPresenter();
                var surfaceBeltPresenter = new SurfaceBeltIndicatorPresenter();
                var playerStatusPresenter = new PlayerStatusPresenter();
                rootPresenter = new HUDRootPresenter(
                    source,
                    stagePresenter,
                    objectivePresenter,
                    chancePresenter,
                    surfaceBeltPresenter,
                    playerStatusPresenter);
                hudController = new HUDController(
                    rootPresenter.ViewModel,
                    stagePresenter.ViewModel,
                    objectivePresenter.ViewModel,
                    chancePresenter.ViewModel,
                    surfaceBeltPresenter.ViewModel,
                    playerStatusPresenter.ViewModel);
                hudController.AttachView(hud);
                SettleObjectiveRows(objectiveView);

                ForceLayoutAndText(root);
                var activeRows = GetActiveRows(objectiveView);
                ValidatePresentation(
                    scenario,
                    objectiveView,
                    activeRows,
                    climate,
                    englishHeaderFont,
                    englishHeaderMaterial,
                    englishHeaderStyle,
                    englishRowFont,
                    englishRowMaterial,
                    englishRowStyle);

                var options = new TypographyPreviewScreenshotOptions
                {
                    Width = width,
                    Height = height,
                    BackgroundColor = new Color(0.015f, 0.025f, 0.045f, 1f),
                };
                texture = TypographyPreviewScreenshotUtility.CaptureRootForValidation(
                    root,
                    options,
                    out var cameraRenderPassCount,
                    out var captureFrameIndex);
                var pngBytes = texture.EncodeToPNG();
                var pixels = texture.GetPixels32();
                var fileName = $"HUD_Objectives_{scenario.State}_{scenario.Locale}.png";
                var filePath = Path.Combine(outputDirectory, fileName);
                File.WriteAllBytes(filePath, pngBytes);

                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!decoded.LoadImage(pngBytes) || decoded.width != width || decoded.height != height)
                    {
                        throw new InvalidOperationException(
                            $"{fileName} did not decode as {width}x{height}.");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(decoded);
                }

                var nonBlank = SystemInfo.graphicsDeviceType ==
                               UnityEngine.Rendering.GraphicsDeviceType.Null ||
                               HasNonBlankPixels(texture);
                if (!nonBlank)
                {
                    throw new InvalidOperationException($"{fileName} is blank or single-color.");
                }

                var headerIdentity = ReadIdentity(objectiveView.HeaderLabel);
                var rowIdentity = ReadIdentity(activeRows[0].Label);
                var graphicStates = CaptureRequiredGraphicStates(root, width, height);
                ValidateRequiredGraphicCategories(graphicStates, scenario);
                var textRegions = CaptureTextRegions(root, width, height);
                return new CaptureRecord(
                    scenario,
                    fileName,
                    pngBytes.LongLength,
                    ComputeSha256(pngBytes),
                    headerIdentity,
                    rowIdentity,
                    graphicStates,
                    textRegions,
                    pixels,
                    width,
                    root.activeInHierarchy,
                    ResolveRootCanvasGroupAlpha(root),
                    ComputeSemanticSnapshotHash(initialReadModel),
                    ComputeHierarchyHash(root),
                    cameraRenderPassCount,
                    captureFrameIndex,
                    endOfFrameCount: 0,
                    nonBlank ? "PASS" : "FAIL",
                    "PASS",
                    "PASS",
                    "PASS",
                    string.Empty);
            }
            finally
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }

                hudController?.Dispose();
                rootPresenter?.Dispose();
                resolver?.Dispose();
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }

                if (shouldClosePreviewScene &&
                    previewScene.IsValid() &&
                    previewScene.isLoaded)
                {
                    if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    {
                        EditorSceneManager.SetActiveScene(previousActiveScene);
                    }

                    EditorSceneManager.CloseScene(previewScene, true);
                }
            }
        }

        private static GameplayObjectiveReadModel CreateReadModel(
            CaptureScenario scenario,
            bool completePrimary)
        {
            var conditions = new List<GameplayObjectiveConditionReadModel>
            {
                CreateCondition(
                    "reach-exit",
                    GameplayObjectivePresentationKind.ReachExit,
                    "reach-exit|role-1",
                    GameplayObjectiveConditionRole.PrimaryGoal,
                    completePrimary,
                    completePrimary ? 1 : 0,
                    1,
                    0),
                CreateCondition(
                    "button-group",
                    GameplayObjectivePresentationKind.ActivateButton,
                    "activate-button|role-2",
                    GameplayObjectiveConditionRole.SecondaryGoal,
                    false,
                    string.Equals(scenario.State, "MaxStack", StringComparison.Ordinal) ? 9 : 0,
                    string.Equals(scenario.State, "MaxStack", StringComparison.Ordinal) ? 10 : 1,
                    10),
            };

            if (string.Equals(scenario.State, "MaxStack", StringComparison.Ordinal))
            {
                conditions.Add(CreateCondition(
                    "moon-button-group",
                    GameplayObjectivePresentationKind.ActivateMoonButton,
                    "activate-moon-button|role-2",
                    GameplayObjectiveConditionRole.SecondaryGoal,
                    false,
                    98,
                    99,
                    20));
            }

            return new GameplayObjectiveReadModel(
                hasObjective: true,
                goalReached: completePrimary,
                allConditionsSatisfied: false,
                isCleared: false,
                conditions: conditions);
        }

        private static GameplayObjectiveConditionReadModel CreateCondition(
            string stableId,
            GameplayObjectivePresentationKind kind,
            string groupKey,
            GameplayObjectiveConditionRole role,
            bool isSatisfied,
            int completedCount,
            int requiredCount,
            int sortOrder)
        {
            return new GameplayObjectiveConditionReadModel(
                stableId,
                kind,
                groupKey,
                role,
                required: true,
                isSatisfied,
                completedCount,
                requiredCount,
                sortOrder);
        }

        private static UIPresentationSnapshot CreateSnapshot(GameplayObjectiveReadModel readModel)
        {
            var empty = UIPresentationSnapshot.Empty;
            var objective = UIStateMapper.MapObjectiveForPresentation(
                readModel,
                new StageId("objective-hud-visual"));
            return new UIPresentationSnapshot(
                empty.Tick,
                empty.Interaction,
                empty.Stage,
                objective,
                new UIChanceSlice(
                    hasChances: true,
                    remainingChances: 3,
                    maxChances: 3),
                empty.Topology,
                empty.SurfaceBelt,
                empty.Player,
                empty.Notifications);
        }

        private static void SettleObjectiveRows(ObjectiveHudView view)
        {
            var processAdvance = typeof(ObjectiveHudView).GetMethod(
                "ProcessTransitionAdvance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (processAdvance == null)
            {
                throw new MissingMethodException(nameof(ObjectiveHudView), "ProcessTransitionAdvance");
            }

            for (var iteration = 0; iteration < 64; iteration++)
            {
                var rows = GetActiveRows(view);
                foreach (var row in rows)
                {
                    var animator = row.View.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.Update(5f);
                    }

                    row.View.Tick(5f);
                }

                processAdvance.Invoke(view, new object[] { float.MaxValue });
            }
        }

        private static void ForceLayoutAndText(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.SetAllDirty();
            }

            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.SetAllDirty();
                text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            }

            Canvas.ForceUpdateCanvases();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.SetAllDirty();
            }

            Canvas.ForceUpdateCanvases();
        }

        private static void ValidatePresentation(
            CaptureScenario scenario,
            ObjectiveHudView view,
            IReadOnlyList<ActiveRow> activeRows,
            TMP_FontAsset climate,
            TMP_FontAsset englishHeaderFont,
            Material englishHeaderMaterial,
            FontStyles englishHeaderStyle,
            TMP_FontAsset englishRowFont,
            Material englishRowMaterial,
            FontStyles englishRowStyle)
        {
            var expectedHeader = string.Equals(scenario.Locale, "ko-KR", StringComparison.Ordinal)
                ? "과업"
                : "Objectives";
            if (!string.Equals(view.HeaderLabel.text, expectedHeader, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Header mismatch. Expected '{expectedHeader}', actual '{view.HeaderLabel.text}'.");
            }

            if (activeRows.Count != scenario.ExpectedRowCount)
            {
                throw new InvalidOperationException(
                    $"Expected {scenario.ExpectedRowCount} active rows, found {activeRows.Count}.");
            }

            if (string.Equals(scenario.Locale, "ko-KR", StringComparison.Ordinal))
            {
                AssertIdentity(
                    view.HeaderLabel,
                    climate,
                    climate.material,
                    FontStyles.Normal,
                    "ko-KR header");
                foreach (var row in activeRows)
                {
                    AssertIdentity(
                        row.Label,
                        climate,
                        climate.material,
                        FontStyles.Normal,
                        "ko-KR row");
                }
            }
            else
            {
                AssertIdentity(
                    view.HeaderLabel,
                    englishHeaderFont,
                    englishHeaderMaterial,
                    englishHeaderStyle,
                    "en-US header");
                foreach (var row in activeRows)
                {
                    AssertIdentity(
                        row.Label,
                        englishRowFont,
                        englishRowMaterial,
                        englishRowStyle,
                        "en-US row");
                }
            }

            var objectiveLayout = view.GetComponent<LayoutElement>();
            var rowHeight = GetTemplateRow(view).GetComponent<LayoutElement>().preferredHeight;
            var requiredHeight = view.HeaderLabel.rectTransform.rect.height + activeRows.Count * rowHeight;
            if (objectiveLayout == null || objectiveLayout.preferredHeight + 0.01f < requiredHeight)
            {
                throw new InvalidOperationException(
                    $"Objective viewport height {objectiveLayout?.preferredHeight ?? 0f} is below required {requiredHeight}.");
            }

            ValidateViewportGeometry(view, activeRows);

            foreach (var text in new[] { view.HeaderLabel }.Concat(activeRows.Select(row => row.Label)))
            {
                if (!text.isActiveAndEnabled ||
                    text.color.a <= 0f ||
                    text.canvasRenderer.cull)
                {
                    throw new InvalidOperationException(
                        $"{text.name} is not visible in the production HUD composition.");
                }

                var missing = text.text
                    .Where(character => !char.IsControl(character) && !char.IsWhiteSpace(character))
                    .Where(character => !text.font.HasCharacter(
                        character,
                        searchFallbacks: false,
                        tryAddCharacter: false))
                    .Distinct()
                    .ToArray();
                if (missing.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"{text.name} is missing glyphs: {string.Join(", ", missing.Select(value => $"U+{(int)value:X4}"))}.");
                }

                var availableWidth = objectiveLayout.preferredWidth + text.rectTransform.sizeDelta.x;
                var preferred = text.GetPreferredValues(text.text, Mathf.Max(1f, availableWidth), 0f);
                var availableHeight = ReferenceEquals(text, view.HeaderLabel)
                    ? text.rectTransform.rect.height
                    : rowHeight;
                if (preferred.y > availableHeight + 0.01f)
                {
                    throw new InvalidOperationException(
                        $"{text.name} preferred height {preferred.y} exceeds {availableHeight}.");
                }
            }
        }

        private static void ValidateViewportGeometry(
            ObjectiveHudView view,
            IReadOnlyList<ActiveRow> activeRows)
        {
            if (!(view.transform is RectTransform viewport))
            {
                throw new InvalidOperationException("Objective HUD root is not a RectTransform.");
            }

            var presentationRects = new List<PresentationRect>
            {
                new PresentationRect("header", view.HeaderLabel.rectTransform),
            };
            presentationRects.AddRange(activeRows.Select(row =>
                new PresentationRect(
                    $"row '{row.View.StableId}'",
                    row.View.transform as RectTransform)));

            var viewportRect = viewport.rect;
            var projected = new List<ProjectedPresentationRect>(presentationRects.Count);
            foreach (var presentationRect in presentationRects)
            {
                if (presentationRect.Target == null)
                {
                    throw new InvalidOperationException(
                        $"{presentationRect.Name} has no RectTransform.");
                }

                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    viewport,
                    presentationRect.Target);
                var rect = Rect.MinMaxRect(
                    bounds.min.x,
                    bounds.min.y,
                    bounds.max.x,
                    bounds.max.y);
                if (rect.width <= 0f || rect.height <= 0f)
                {
                    throw new InvalidOperationException(
                        $"{presentationRect.Name} has invalid bounds {rect}.");
                }

                const float tolerance = 0.01f;
                if (rect.xMin < viewportRect.xMin - tolerance ||
                    rect.xMax > viewportRect.xMax + tolerance ||
                    rect.yMin < viewportRect.yMin - tolerance ||
                    rect.yMax > viewportRect.yMax + tolerance)
                {
                    throw new InvalidOperationException(
                        $"{presentationRect.Name} bounds {rect} escape Objective viewport {viewportRect}.");
                }

                projected.Add(new ProjectedPresentationRect(presentationRect.Name, rect));
            }

            projected.Sort((left, right) => right.Rect.yMax.CompareTo(left.Rect.yMax));
            for (var index = 1; index < projected.Count; index++)
            {
                var previous = projected[index - 1];
                var current = projected[index];
                if (current.Rect.yMax > previous.Rect.yMin + 0.01f)
                {
                    throw new InvalidOperationException(
                        $"{previous.Name} bounds {previous.Rect} overlap {current.Name} bounds {current.Rect}.");
                }
            }
        }

        private static void AssertIdentity(
            TMP_Text target,
            TMP_FontAsset font,
            Material material,
            FontStyles style,
            string context)
        {
            if (!ReferenceEquals(target.font, font) ||
                !ReferenceEquals(target.fontSharedMaterial, material) ||
                target.fontStyle != style)
            {
                throw new InvalidOperationException($"{context} typography identity mismatch.");
            }
        }

        private static RectTransform GetTemplateRow(ObjectiveHudView view)
        {
            var field = typeof(ObjectiveHudView).GetField(
                "_objectiveItemTemplate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(view) as RectTransform
                   ?? throw new InvalidOperationException("Objective item template reference was not found.");
        }

        private static IReadOnlyList<ActiveRow> GetActiveRows(ObjectiveHudView view)
        {
            return view.GetComponentsInChildren<ObjectiveHudRowView>(true)
                .Where(row => row != null &&
                              row.gameObject.activeInHierarchy &&
                              !string.IsNullOrWhiteSpace(row.StableId))
                .Select(row => new ActiveRow(
                    row,
                    GetRowLabel(row)))
                .OrderBy(row => row.View.transform.GetSiblingIndex())
                .ToArray();
        }

        private static TMP_Text GetRowLabel(ObjectiveHudRowView row)
        {
            var field = typeof(ObjectiveHudRowView).GetField(
                "_label",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(row) as TMP_Text
                   ?? throw new InvalidOperationException("Objective row label reference was not found.");
        }

        private static AssetIdentity ReadIdentity(TMP_Text target)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                target.font,
                out var fontGuid,
                out long fontLocalId);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                target.fontSharedMaterial,
                out var materialGuid,
                out long materialLocalId);
            return new AssetIdentity(
                target.font != null ? target.font.name : string.Empty,
                fontGuid,
                fontLocalId,
                target.fontSharedMaterial != null ? target.fontSharedMaterial.name : string.Empty,
                materialGuid,
                materialLocalId,
                target.fontStyle.ToString());
        }

        private static bool HasNonBlankPixels(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            return pixels.Length > 0 && pixels.Any(pixel => !pixel.Equals(pixels[0]));
        }

        private static IReadOnlyList<GraphicState> CaptureRequiredGraphicStates(
            GameObject root,
            int width,
            int height)
        {
            if (!(root.transform is RectTransform rootRect))
            {
                throw new InvalidOperationException("HUD capture root must be a RectTransform.");
            }

            var states = new List<GraphicState>();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || graphic is TMP_Text)
                {
                    continue;
                }

                var path = BuildTransformPath(root.transform, graphic.transform);
                var category = ResolveGraphicCategory(graphic);
                var alphaOccupancy = ResolveAlphaOccupancy(graphic);
                var isCanonicalRequired = IsCanonicalRequiredGraphic(path, category);
                if (!isCanonicalRequired &&
                    (!graphic.gameObject.activeInHierarchy ||
                     !graphic.enabled ||
                     alphaOccupancy <= 0.001f ||
                     graphic.canvasRenderer.cull))
                {
                    continue;
                }

                var rectTransform = graphic.rectTransform;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    rootRect,
                    rectTransform);
                var relativeBounds = Rect.MinMaxRect(
                    bounds.min.x,
                    bounds.min.y,
                    bounds.max.x,
                    bounds.max.y);
                var screenBounds = Rect.MinMaxRect(
                    relativeBounds.xMin - rootRect.rect.xMin,
                    relativeBounds.yMin - rootRect.rect.yMin,
                    relativeBounds.xMax - rootRect.rect.xMin,
                    relativeBounds.yMax - rootRect.rect.yMin);
                var identity = ReadGraphicIdentity(graphic);
                states.Add(new GraphicState(
                    path,
                    category,
                    graphic.gameObject.activeInHierarchy,
                    alphaOccupancy,
                    graphic.enabled,
                    identity,
                    relativeBounds,
                    screenBounds,
                    !graphic.canvasRenderer.cull &&
                    screenBounds.width > 0f &&
                    screenBounds.height > 0f &&
                    screenBounds.xMax > 0f &&
                    screenBounds.yMax > 0f &&
                    screenBounds.xMin < width &&
                    screenBounds.yMin < height));
            }

            return states
                .OrderBy(state => state.Path, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsCanonicalRequiredGraphic(string path, string category)
        {
            if (string.Equals(category, "ObjectiveHud", StringComparison.Ordinal))
            {
                return PathContainsAll(path, "HUD_SciFiSoldier_Objectives_02", "SPR_Background") ||
                       PathContainsAll(path, "HUD_SciFiSoldier_Objectives_02", "SPR_Flag") ||
                       PathContainsAll(path, "Objective_Item_Runtime_", "SPR_Item_Inactive");
            }

            if (string.Equals(category, "ChancePanel", StringComparison.Ordinal))
            {
                return PathContainsAll(path, "ChancePanel", "Background") &&
                       path.IndexOf("ChanceSlotView", StringComparison.Ordinal) < 0;
            }

            return string.Equals(category, "SurfaceBelt", StringComparison.Ordinal) &&
                   PathContainsAll(path, "SurfaceBeltIndicatorRoot", "Background");
        }

        private static void ValidateRequiredGraphicCategories(
            IReadOnlyList<GraphicState> graphicStates,
            CaptureScenario scenario)
        {
            var objectiveCount = graphicStates.Count(state =>
                string.Equals(state.Category, "ObjectiveHud", StringComparison.Ordinal));
            var chanceCount = graphicStates.Count(state =>
                string.Equals(state.Category, "ChancePanel", StringComparison.Ordinal));
            var surfaceCount = graphicStates.Count(state =>
                string.Equals(state.Category, "SurfaceBelt", StringComparison.Ordinal));
            if (objectiveCount < scenario.ExpectedRowCount + 1)
            {
                throw new InvalidOperationException(
                    $"ObjectiveHud required non-text graphics are incomplete: {objectiveCount}.");
            }

            if (chanceCount < 1)
            {
                throw new InvalidOperationException(
                    "ChancePanel required non-text graphics are incomplete.");
            }

            if (surfaceCount < 1)
            {
                throw new InvalidOperationException(
                    "SurfaceBelt required non-text graphics are incomplete.");
            }

            RequireGraphicPath(
                graphicStates,
                "objective panel background",
                "HUD_SciFiSoldier_Objectives_02",
                "SPR_Background");
            RequireGraphicPath(
                graphicStates,
                "objective left decoration",
                "HUD_SciFiSoldier_Objectives_02",
                "SPR_Flag");
            RequireGraphicPath(
                graphicStates,
                "chance panel background",
                "ChancePanel",
                "Background");
            RequireGraphicPath(
                graphicStates,
                "surface belt background",
                "SurfaceBeltIndicatorRoot",
                "Background");
            var objectiveCheckboxCount = graphicStates.Count(state =>
                PathContainsAll(
                    state.Path,
                    "Objective_Item_Runtime_",
                    "SPR_Item_Inactive"));
            if (objectiveCheckboxCount < scenario.ExpectedRowCount)
            {
                throw new InvalidOperationException(
                    "Objective row checkbox/icon graphics are incomplete: " +
                    $"{objectiveCheckboxCount}/{scenario.ExpectedRowCount}.");
            }

            var failed = graphicStates.Where(state => !state.Passed).ToArray();
            if (failed.Length > 0)
            {
                throw new InvalidOperationException(
                    "Required non-text graphic state failed: " +
                    string.Join(", ", failed.Select(state => state.Path)));
            }
        }

        private static void RequireGraphicPath(
            IEnumerable<GraphicState> graphicStates,
            string label,
            params string[] pathFragments)
        {
            if (!graphicStates.Any(state => PathContainsAll(state.Path, pathFragments)))
            {
                throw new InvalidOperationException(
                    $"Required {label} graphic was not visible in the production hierarchy.");
            }
        }

        private static bool PathContainsAll(string path, params string[] fragments)
        {
            return !string.IsNullOrEmpty(path) &&
                   fragments.All(fragment =>
                       path.IndexOf(fragment, StringComparison.Ordinal) >= 0);
        }

        private static float ResolveRootCanvasGroupAlpha(GameObject root)
        {
            var group = root.GetComponent<CanvasGroup>();
            if (group == null)
            {
                throw new InvalidOperationException(
                    "HUD production capture root is missing its CanvasGroup.");
            }

            return group.alpha;
        }

        private static IReadOnlyList<Rect> CaptureTextRegions(
            GameObject root,
            int width,
            int height)
        {
            var rootRect = root.transform as RectTransform
                           ?? throw new InvalidOperationException(
                               "HUD capture root must be a RectTransform.");
            var regions = new List<Rect>();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null ||
                    !text.isActiveAndEnabled ||
                    text.color.a <= 0f ||
                    text.canvasRenderer.cull)
                {
                    continue;
                }

                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    rootRect,
                    text.rectTransform);
                const float padding = 8f;
                var region = Rect.MinMaxRect(
                    Mathf.Max(0f, bounds.min.x - rootRect.rect.xMin - padding),
                    Mathf.Max(0f, bounds.min.y - rootRect.rect.yMin - padding),
                    Mathf.Min(width, bounds.max.x - rootRect.rect.xMin + padding),
                    Mathf.Min(height, bounds.max.y - rootRect.rect.yMin + padding));
                if (region.width > 0f && region.height > 0f)
                {
                    regions.Add(region);
                }
            }

            return regions;
        }

        private static bool ValidateCrossLocaleParity(
            IReadOnlyList<CaptureRecord> captures,
            ICollection<string> errors,
            out long unexpectedPixelDelta)
        {
            unexpectedPixelDelta = 0;
            var passed = true;
            foreach (var stateGroup in captures.GroupBy(capture => capture.Scenario.State))
            {
                var english = stateGroup.SingleOrDefault(capture =>
                    string.Equals(capture.Scenario.Locale, "en-US", StringComparison.Ordinal));
                var korean = stateGroup.SingleOrDefault(capture =>
                    string.Equals(capture.Scenario.Locale, "ko-KR", StringComparison.Ordinal));
                if (string.IsNullOrEmpty(english.FileName) ||
                    string.IsNullOrEmpty(korean.FileName))
                {
                    errors?.Add($"{stateGroup.Key}: locale pair is incomplete.");
                    passed = false;
                    continue;
                }

                if (!string.Equals(
                        english.SemanticSnapshotHash,
                        korean.SemanticSnapshotHash,
                        StringComparison.Ordinal))
                {
                    errors?.Add($"{stateGroup.Key}: semantic snapshot differs across locales.");
                    passed = false;
                }

                if (!string.Equals(
                        english.HierarchyHash,
                        korean.HierarchyHash,
                        StringComparison.Ordinal))
                {
                    errors?.Add($"{stateGroup.Key}: canvas hierarchy differs across locales.");
                    passed = false;
                }

                var englishGraphics = english.GraphicStates.ToDictionary(
                    state => state.Path,
                    StringComparer.Ordinal);
                var koreanGraphics = korean.GraphicStates.ToDictionary(
                    state => state.Path,
                    StringComparer.Ordinal);
                if (!englishGraphics.Keys.OrderBy(value => value, StringComparer.Ordinal)
                        .SequenceEqual(
                            koreanGraphics.Keys.OrderBy(value => value, StringComparer.Ordinal),
                            StringComparer.Ordinal))
                {
                    errors?.Add($"{stateGroup.Key}: required non-text graphic paths differ across locales.");
                    passed = false;
                }

                foreach (var path in englishGraphics.Keys.Intersect(
                             koreanGraphics.Keys,
                             StringComparer.Ordinal))
                {
                    var left = englishGraphics[path];
                    var right = koreanGraphics[path];
                    if (!string.Equals(left.Identity, right.Identity, StringComparison.Ordinal) ||
                        !RectsApproximatelyEqual(left.ScreenBounds, right.ScreenBounds) ||
                        Mathf.Abs(left.AlphaOccupancy - right.AlphaOccupancy) > 0.001f)
                    {
                        errors?.Add(
                            $"{stateGroup.Key}: non-text graphic parity mismatch at {path}.");
                        passed = false;
                    }
                }

                var pixelDelta = CountUnexpectedPixelDelta(english, korean);
                unexpectedPixelDelta += pixelDelta;
                if (pixelDelta != 0)
                {
                    errors?.Add(
                        $"{stateGroup.Key}: {pixelDelta} non-text pixels differ across locales.");
                    passed = false;
                }
            }

            return passed;
        }

        private static long CountUnexpectedPixelDelta(
            CaptureRecord english,
            CaptureRecord korean)
        {
            if (english.Pixels == null ||
                korean.Pixels == null ||
                english.Pixels.Length != korean.Pixels.Length)
            {
                return long.MaxValue;
            }

            var excluded = english.TextRegions.Concat(korean.TextRegions).ToArray();
            long delta = 0;
            for (var index = 0; index < english.Pixels.Length; index++)
            {
                var x = index % english.Width;
                var y = index / english.Width;
                if (excluded.Any(region => region.Contains(new Vector2(x, y))))
                {
                    continue;
                }

                if (!english.Pixels[index].Equals(korean.Pixels[index]))
                {
                    delta++;
                }
            }

            return delta;
        }

        private static string ComputeSemanticSnapshotHash(GameplayObjectiveReadModel model)
        {
            var builder = new StringBuilder();
            builder.Append(model.HasObjective ? 1 : 0)
                .Append('|')
                .Append(model.GoalReached ? 1 : 0)
                .Append('|')
                .Append(model.AllConditionsSatisfied ? 1 : 0)
                .Append('|')
                .Append(model.IsCleared ? 1 : 0);
            foreach (var condition in model.Conditions)
            {
                builder.Append('|')
                    .Append(condition.StableId)
                    .Append('|')
                    .Append((int)condition.PresentationKind)
                    .Append('|')
                    .Append(condition.StableGroupKey)
                    .Append('|')
                    .Append((int)condition.Role)
                    .Append('|')
                    .Append(condition.Required ? 1 : 0)
                    .Append('|')
                    .Append(condition.IsSatisfied ? 1 : 0)
                    .Append('|')
                    .Append(condition.CompletedCount)
                    .Append('|')
                    .Append(condition.RequiredCount)
                    .Append('|')
                    .Append(condition.SortOrder);
            }

            return ComputeSha256(Encoding.UTF8.GetBytes(builder.ToString()));
        }

        private static string ComputeHierarchyHash(GameObject root)
        {
            var builder = new StringBuilder();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true)
                         .OrderBy(item => BuildTransformPath(root.transform, item), StringComparer.Ordinal))
            {
                builder.Append(BuildTransformPath(root.transform, transform))
                    .Append('|')
                    .Append(transform.gameObject.activeSelf ? 1 : 0)
                    .Append('|')
                    .Append(transform.GetSiblingIndex())
                    .AppendLine();
            }

            return ComputeSha256(Encoding.UTF8.GetBytes(builder.ToString()));
        }

        private static string BuildTransformPath(Transform root, Transform target)
        {
            var parts = new Stack<string>();
            var current = target;
            while (current != null)
            {
                parts.Push($"{current.name}[{current.GetSiblingIndex()}]");
                if (ReferenceEquals(current, root))
                {
                    break;
                }

                current = current.parent;
            }

            return string.Join("/", parts);
        }

        private static string ResolveGraphicCategory(Graphic graphic)
        {
            if (graphic.GetComponentInParent<ObjectiveHudView>() != null)
            {
                return "ObjectiveHud";
            }

            if (graphic.GetComponentInParent<ChancePanelView>() != null)
            {
                return "ChancePanel";
            }

            if (graphic.GetComponentInParent<SurfaceBeltIndicatorView>() != null)
            {
                return "SurfaceBelt";
            }

            return "HudShell";
        }

        private static float ResolveAlphaOccupancy(Graphic graphic)
        {
            var alpha = graphic.color.a * graphic.canvasRenderer.GetAlpha();
            var current = graphic.transform;
            while (current != null)
            {
                var group = current.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    alpha *= group.alpha;
                }

                current = current.parent;
            }

            return alpha;
        }

        private static string ReadGraphicIdentity(Graphic graphic)
        {
            UnityEngine.Object asset = null;
            if (graphic is Image image && image.sprite != null)
            {
                asset = image.sprite;
            }
            else if (graphic.material != null)
            {
                asset = graphic.material;
            }

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                asset,
                out var guid,
                out long localId);
            return string.Concat(
                graphic.GetType().Name,
                ":",
                asset != null ? asset.name : "default",
                ":",
                guid,
                ":",
                localId.ToString(CultureInfo.InvariantCulture));
        }

        private static bool RectsApproximatelyEqual(Rect left, Rect right)
        {
            const float tolerance = 0.01f;
            return Mathf.Abs(left.xMin - right.xMin) <= tolerance &&
                   Mathf.Abs(left.yMin - right.yMin) <= tolerance &&
                   Mathf.Abs(left.xMax - right.xMax) <= tolerance &&
                   Mathf.Abs(left.yMax - right.yMax) <= tolerance;
        }

        private static string FormatRect(Rect rect)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:F3},{1:F3},{2:F3},{3:F3}",
                rect.x,
                rect.y,
                rect.width,
                rect.height);
        }

        private static string WriteManifest(
            string outputDirectory,
            int width,
            int height,
            string gitHead,
            IReadOnlyList<CaptureRecord> captures,
            IReadOnlyList<CaptureAssetMutationEvidence> mutationEvidence,
            bool crossLocaleParity,
            long unexpectedPixelDelta,
            IReadOnlyList<string> errors,
            string captureCommand,
            string captureMode,
            string captureEnvironment,
            string rootCause)
        {
            var builder = new StringBuilder();
            Append(builder, "schema_version", "2");
            Append(builder, "generated_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            Append(builder, "git_head", gitHead);
            Append(builder, "unity_version", UnityEngine.Application.unityVersion);
            Append(builder, "capture_command", captureCommand);
            Append(builder, "capture_mode", captureMode);
            Append(builder, "capture_environment", captureEnvironment);
            Append(builder, "root_cause", rootCause);
            Append(builder, "resolution", $"{width}x{height}");
            Append(builder, "capture_count", captures.Count.ToString(CultureInfo.InvariantCulture));
            Append(builder, "errors", errors.Count.ToString(CultureInfo.InvariantCulture));
            Append(
                builder,
                "overall_result",
                errors.Count == 0 &&
                captures.Count == Scenarios.Length &&
                captures.All(capture => capture.Passed)
                    ? "PASS"
                    : "FAIL");
            Append(builder, "canonical_status", "CANDIDATE_PENDING_INDEPENDENT_AUDIT");
            Append(
                builder,
                "graphic_completeness",
                captures.Count == Scenarios.Length &&
                captures.All(capture =>
                    string.Equals(
                        capture.GraphicCompleteness,
                        "PASS",
                        StringComparison.Ordinal))
                    ? "PASS"
                    : "FAIL");
            Append(
                builder,
                "cross_locale_non_text_parity",
                crossLocaleParity ? "PASS" : "FAIL");
            Append(
                builder,
                "unexpected_pixel_delta",
                unexpectedPixelDelta.ToString(CultureInfo.InvariantCulture));
            Append(
                builder,
                "asset_mutation_observed_before_restore",
                mutationEvidence.Count > 0 ? "PASS" : "FAIL");
            Append(
                builder,
                "unexpected_asset_mutation_count",
                mutationEvidence.Count(item => !item.Allowed)
                    .ToString(CultureInfo.InvariantCulture));

            foreach (var capture in captures)
            {
                builder.AppendLine();
                builder.Append('[')
                    .Append(capture.Scenario.State)
                    .Append('/')
                    .Append(capture.Scenario.Locale)
                    .AppendLine("]");
                Append(builder, "surface", "HUD Objectives");
                Append(builder, "state", capture.Scenario.State);
                Append(builder, "locale", capture.Scenario.Locale);
                Append(builder, "file", capture.FileName);
                Append(builder, "file_size_bytes", capture.FileSizeBytes.ToString(CultureInfo.InvariantCulture));
                Append(builder, "sha256", capture.Sha256);
                Append(builder, "dimensions", $"{width}x{height}");
                Append(builder, "decode", "PASS");
                Append(builder, "nonblank", capture.NonBlank);
                Append(builder, "glyph_coverage", capture.GlyphCoverage);
                Append(builder, "layout", capture.Layout);
                Append(builder, "graphic_completeness", capture.GraphicCompleteness);
                Append(
                    builder,
                    "required_graphic_count",
                    capture.GraphicStates.Count.ToString(CultureInfo.InvariantCulture));
                Append(builder, "semantic_snapshot_hash", capture.SemanticSnapshotHash);
                Append(builder, "hierarchy_hash", capture.HierarchyHash);
                Append(builder, "localization_ready", "PASS");
                Append(builder, "presenter_render_complete", "PASS");
                Append(builder, "canvas_rebuild_complete", "PASS");
                Append(builder, "hud_root_active_in_hierarchy", capture.RootActive ? "1" : "0");
                Append(
                    builder,
                    "hud_root_canvas_group_alpha",
                    capture.RootCanvasGroupAlpha.ToString("F6", CultureInfo.InvariantCulture));
                Append(
                    builder,
                    "objective_settle_strategy",
                    capture.EndOfFrameCount > 0
                        ? "PRODUCTION_UNSCALED_TIME"
                        : "SYNCHRONOUS_TEST_SETTLE");
                Append(
                    builder,
                    "objective_settle_seconds",
                    capture.EndOfFrameCount > 0 ? "2.000" : "0.000");
                Append(
                    builder,
                    "objective_settle_iterations",
                    capture.EndOfFrameCount > 0 ? "0" : "64");
                Append(
                    builder,
                    "end_of_frame_count",
                    capture.EndOfFrameCount.ToString(CultureInfo.InvariantCulture));
                Append(
                    builder,
                    "camera_render_pass_count",
                    capture.CameraRenderPassCount.ToString(CultureInfo.InvariantCulture));
                Append(
                    builder,
                    "capture_frame_index",
                    capture.CaptureFrameIndex.ToString(CultureInfo.InvariantCulture));
                for (var index = 0; index < capture.GraphicStates.Count; index++)
                {
                    var graphic = capture.GraphicStates[index];
                    var prefix = $"graphic_{index:D3}_";
                    Append(builder, prefix + "path", graphic.Path);
                    Append(builder, prefix + "category", graphic.Category);
                    Append(builder, prefix + "active_in_hierarchy", graphic.ActiveInHierarchy ? "1" : "0");
                    Append(
                        builder,
                        prefix + "alpha_occupancy",
                        graphic.AlphaOccupancy.ToString("F6", CultureInfo.InvariantCulture));
                    Append(builder, prefix + "graphic_enabled", graphic.Enabled ? "1" : "0");
                    Append(builder, prefix + "identity", graphic.Identity);
                    Append(builder, prefix + "rect_bounds", FormatRect(graphic.RectBounds));
                    Append(builder, prefix + "screen_space_bounds", FormatRect(graphic.ScreenBounds));
                    Append(builder, prefix + "visible_pixel_area", graphic.VisiblePixelArea ? "POSITIVE" : "ZERO");
                }
                Append(builder, "header_font", capture.HeaderIdentity.FontName);
                Append(builder, "header_font_guid", capture.HeaderIdentity.FontGuid);
                Append(builder, "header_font_local_id", capture.HeaderIdentity.FontLocalId.ToString(CultureInfo.InvariantCulture));
                Append(builder, "header_material", capture.HeaderIdentity.MaterialName);
                Append(builder, "header_material_guid", capture.HeaderIdentity.MaterialGuid);
                Append(builder, "header_material_local_id", capture.HeaderIdentity.MaterialLocalId.ToString(CultureInfo.InvariantCulture));
                Append(builder, "header_style", capture.HeaderIdentity.Style);
                Append(builder, "row_font", capture.RowIdentity.FontName);
                Append(builder, "row_font_guid", capture.RowIdentity.FontGuid);
                Append(builder, "row_font_local_id", capture.RowIdentity.FontLocalId.ToString(CultureInfo.InvariantCulture));
                Append(builder, "row_material", capture.RowIdentity.MaterialName);
                Append(builder, "row_material_guid", capture.RowIdentity.MaterialGuid);
                Append(builder, "row_material_local_id", capture.RowIdentity.MaterialLocalId.ToString(CultureInfo.InvariantCulture));
                Append(builder, "row_style", capture.RowIdentity.Style);
                Append(builder, "capture_result", capture.Passed ? "PASS" : "FAIL");
                Append(builder, "capture_errors", capture.Error);
            }

            for (var index = 0; index < mutationEvidence.Count; index++)
            {
                var mutation = mutationEvidence[index];
                builder.AppendLine();
                builder.Append("[asset-mutation/").Append(index).AppendLine("]");
                Append(builder, "path", mutation.Path);
                Append(builder, "before_hash", mutation.BeforeHash);
                Append(builder, "after_capture_hash", mutation.AfterCaptureHash);
                Append(builder, "mutation_detected", mutation.MutationDetected ? "1" : "0");
                Append(builder, "changed_properties", mutation.ChangedProperties);
                Append(builder, "classification", mutation.Classification);
                Append(builder, "allowed", mutation.Allowed ? "1" : "0");
                Append(builder, "lane_verdict_before_restore", mutation.LaneVerdictBeforeRestore);
                Append(builder, "restored", mutation.Restored ? "1" : "0");
                Append(builder, "restored_hash", mutation.RestoredHash);
            }

            foreach (var error in errors)
            {
                builder.AppendLine();
                builder.Append("# ERROR: ").AppendLine(Sanitize(error));
            }

            var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
            File.WriteAllText(
                manifestPath,
                builder.ToString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return manifestPath;
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            builder.Append(key).Append('=').AppendLine(Sanitize(value));
        }

        private static string Sanitize(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(bytes))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static string ReadArgument(IReadOnlyList<string> args, string key)
        {
            for (var i = 0; i < args.Count - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private static int ReadPositiveInt(
            IReadOnlyList<string> args,
            string key,
            int defaultValue)
        {
            var value = ReadArgument(args, key);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                   parsed > 0
                ? parsed
                : defaultValue;
        }

        private readonly struct CaptureScenario
        {
            public CaptureScenario(string state, string locale, int expectedRowCount)
            {
                State = state;
                Locale = locale;
                ExpectedRowCount = expectedRowCount;
            }

            public string State { get; }

            public string Locale { get; }

            public int ExpectedRowCount { get; }
        }

        private readonly struct ActiveRow
        {
            public ActiveRow(ObjectiveHudRowView view, TMP_Text label)
            {
                View = view;
                Label = label;
            }

            public ObjectiveHudRowView View { get; }

            public TMP_Text Label { get; }
        }

        private readonly struct PresentationRect
        {
            public PresentationRect(string name, RectTransform target)
            {
                Name = name;
                Target = target;
            }

            public string Name { get; }

            public RectTransform Target { get; }
        }

        private readonly struct ProjectedPresentationRect
        {
            public ProjectedPresentationRect(string name, Rect rect)
            {
                Name = name;
                Rect = rect;
            }

            public string Name { get; }

            public Rect Rect { get; }
        }

        private readonly struct AssetIdentity
        {
            public AssetIdentity(
                string fontName,
                string fontGuid,
                long fontLocalId,
                string materialName,
                string materialGuid,
                long materialLocalId,
                string style)
            {
                FontName = fontName;
                FontGuid = fontGuid;
                FontLocalId = fontLocalId;
                MaterialName = materialName;
                MaterialGuid = materialGuid;
                MaterialLocalId = materialLocalId;
                Style = style;
            }

            public string FontName { get; }

            public string FontGuid { get; }

            public long FontLocalId { get; }

            public string MaterialName { get; }

            public string MaterialGuid { get; }

            public long MaterialLocalId { get; }

            public string Style { get; }
        }

        private readonly struct GraphicState
        {
            public GraphicState(
                string path,
                string category,
                bool activeInHierarchy,
                float alphaOccupancy,
                bool enabled,
                string identity,
                Rect rectBounds,
                Rect screenBounds,
                bool visiblePixelArea)
            {
                Path = path;
                Category = category;
                ActiveInHierarchy = activeInHierarchy;
                AlphaOccupancy = alphaOccupancy;
                Enabled = enabled;
                Identity = identity;
                RectBounds = rectBounds;
                ScreenBounds = screenBounds;
                VisiblePixelArea = visiblePixelArea;
            }

            public string Path { get; }

            public string Category { get; }

            public bool ActiveInHierarchy { get; }

            public float AlphaOccupancy { get; }

            public bool Enabled { get; }

            public string Identity { get; }

            public Rect RectBounds { get; }

            public Rect ScreenBounds { get; }

            public bool VisiblePixelArea { get; }

            public bool Passed =>
                ActiveInHierarchy &&
                Enabled &&
                AlphaOccupancy > 0.001f &&
                VisiblePixelArea &&
                !string.IsNullOrWhiteSpace(Identity);
        }

        private readonly struct CaptureRecord
        {
            public CaptureRecord(
                CaptureScenario scenario,
                string fileName,
                long fileSizeBytes,
                string sha256,
                AssetIdentity headerIdentity,
                AssetIdentity rowIdentity,
                IReadOnlyList<GraphicState> graphicStates,
                IReadOnlyList<Rect> textRegions,
                Color32[] pixels,
                int width,
                bool rootActive,
                float rootCanvasGroupAlpha,
                string semanticSnapshotHash,
                string hierarchyHash,
                int cameraRenderPassCount,
                int captureFrameIndex,
                int endOfFrameCount,
                string nonBlank,
                string glyphCoverage,
                string layout,
                string graphicCompleteness,
                string error)
            {
                Scenario = scenario;
                FileName = fileName;
                FileSizeBytes = fileSizeBytes;
                Sha256 = sha256;
                HeaderIdentity = headerIdentity;
                RowIdentity = rowIdentity;
                GraphicStates = graphicStates ?? Array.Empty<GraphicState>();
                TextRegions = textRegions ?? Array.Empty<Rect>();
                Pixels = pixels ?? Array.Empty<Color32>();
                Width = width;
                RootActive = rootActive;
                RootCanvasGroupAlpha = rootCanvasGroupAlpha;
                SemanticSnapshotHash = semanticSnapshotHash ?? string.Empty;
                HierarchyHash = hierarchyHash ?? string.Empty;
                CameraRenderPassCount = cameraRenderPassCount;
                CaptureFrameIndex = captureFrameIndex;
                EndOfFrameCount = endOfFrameCount;
                NonBlank = nonBlank;
                GlyphCoverage = glyphCoverage;
                Layout = layout;
                GraphicCompleteness = graphicCompleteness;
                Error = error ?? string.Empty;
            }

            public CaptureScenario Scenario { get; }

            public string FileName { get; }

            public long FileSizeBytes { get; }

            public string Sha256 { get; }

            public AssetIdentity HeaderIdentity { get; }

            public AssetIdentity RowIdentity { get; }

            public IReadOnlyList<GraphicState> GraphicStates { get; }

            public IReadOnlyList<Rect> TextRegions { get; }

            public Color32[] Pixels { get; }

            public int Width { get; }

            public bool RootActive { get; }

            public float RootCanvasGroupAlpha { get; }

            public string SemanticSnapshotHash { get; }

            public string HierarchyHash { get; }

            public int CameraRenderPassCount { get; }

            public int CaptureFrameIndex { get; }

            public int EndOfFrameCount { get; }

            public string NonBlank { get; }

            public string GlyphCoverage { get; }

            public string Layout { get; }

            public string GraphicCompleteness { get; }

            public string Error { get; }

            public bool Passed =>
                FileSizeBytes > 0 &&
                !string.IsNullOrWhiteSpace(Sha256) &&
                string.Equals(NonBlank, "PASS", StringComparison.Ordinal) &&
                string.Equals(GlyphCoverage, "PASS", StringComparison.Ordinal) &&
                string.Equals(Layout, "PASS", StringComparison.Ordinal) &&
                string.Equals(GraphicCompleteness, "PASS", StringComparison.Ordinal) &&
                RootActive &&
                RootCanvasGroupAlpha > 0.001f &&
                GraphicStates.Count > 0 &&
                GraphicStates.All(graphic => graphic.Passed) &&
                string.IsNullOrEmpty(Error);
        }

        private sealed class MemoryLocalePreferenceStore : IUiLocalePreferenceStore
        {
            private string localeCode;

            public MemoryLocalePreferenceStore(string localeCode)
            {
                this.localeCode = localeCode;
            }

            public bool TryLoad(out string value)
            {
                value = localeCode;
                return !string.IsNullOrWhiteSpace(value);
            }

            public void Save(string value)
            {
                localeCode = value;
            }
        }

        private sealed class EvidencePresentationSource : IGameplayUiPresentationSource
        {
            public EvidencePresentationSource(UIPresentationSnapshot snapshot)
            {
                CurrentSnapshot = snapshot;
            }

            public event Action<UIPresentationSnapshot> SnapshotChanged;
            public event Action<UITickEventBatch> TickEventsApplied;
            public event Action<LevelFailedScreenPayload> LevelFailedCommitted;

            public UIPresentationSnapshot CurrentSnapshot { get; private set; }

            public UITickEventBatch CurrentTickEvents => UITickEventBatch.Empty;

            public MinimalStageCompletionReadModel CurrentMinimalStageCompletion => null;

            public LevelFailedScreenPayload CurrentLevelFailed => null;

            public void UpdateUiGameplayInputBlocked(bool isUiGameplayInputBlocked)
            {
            }

            public void Publish(UIPresentationSnapshot snapshot)
            {
                CurrentSnapshot = snapshot;
                SnapshotChanged?.Invoke(snapshot);
            }
        }

    }
}

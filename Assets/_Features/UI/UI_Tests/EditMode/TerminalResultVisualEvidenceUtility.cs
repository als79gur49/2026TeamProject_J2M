using System;
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
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Feature.UI.Tests
{
    public static class TerminalResultVisualEvidenceUtility
    {
        public const string CommandLineEntryPoint =
            "Game.Feature.UI.Tests.TerminalResultVisualEvidenceUtility.CaptureFromCommandLine";
        public const string ManifestFileName = "terminal-result-capture.log";
        public const int CanonicalWidth = 1920;
        public const int CanonicalHeight = 1080;
        public const int DiagnosticWidth = 960;
        public const int DiagnosticHeight = 540;

        private const string RootShellResourcePath = "UI/GameplayUiCanvasRootShell";
        private const string ClimateFontPath =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset";
        private const string StringTableSharedDataPath =
            "Assets/Localization/StringTables/UI/UI Shared Data.asset";

        private static readonly CaptureScenario[] CanonicalScenarios =
        {
            new(ScreenId.StageResult, "en-US", CaptureClassification.Canonical),
            new(ScreenId.StageResult, "ko-KR", CaptureClassification.Canonical),
            new(ScreenId.LevelFailed, "en-US", CaptureClassification.Canonical),
            new(ScreenId.LevelFailed, "ko-KR", CaptureClassification.Canonical),
            new(ScreenId.GameClear, "en-US", CaptureClassification.Canonical),
            new(ScreenId.GameClear, "ko-KR", CaptureClassification.Canonical),
        };

        private static readonly CaptureScenario[] DiagnosticScenarios =
        {
            new(ScreenId.LevelFailed, "en-US", CaptureClassification.Diagnostic),
            new(ScreenId.LevelFailed, "ko-KR", CaptureClassification.Diagnostic),
        };

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var outputDirectory = ReadArgument(args, "-terminalResultVisualOutput");
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("-terminalResultVisualOutput is required.");
            }

            var width = ReadPositiveInt(args, "-terminalResultVisualWidth", CanonicalWidth);
            var height = ReadPositiveInt(args, "-terminalResultVisualHeight", CanonicalHeight);
            if (width != CanonicalWidth || height != CanonicalHeight)
            {
                throw new InvalidOperationException(
                    $"Canonical terminal-result captures must be {CanonicalWidth}x{CanonicalHeight}.");
            }

            Capture(outputDirectory, width, height);
        }

        public static void Capture(string outputDirectory, int width, int height)
        {
            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);
            var diagnosticsDirectory = Path.Combine(outputDirectory, "Diagnostics");
            Directory.CreateDirectory(diagnosticsDirectory);
            var records = new List<CaptureRecord>();
            var errors = new List<string>();
            var stageResultAuthoredMaterial = UiTestPrefabAssetUtility
                .LoadScreenPrefab<StageResultScreenView>(
                    UiTestPrefabAssetUtility.StageResultScreenPrefabPath)
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.text == "Level Clear")
                .fontSharedMaterial;
            var stageResultCaptureSource = new Material(stageResultAuthoredMaterial)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var stageResultCaptureMaterials = new List<Material>();
            try
            {
                foreach (var scenario in CanonicalScenarios)
                {
                    TryCapture(
                        scenario,
                        outputDirectory,
                        width,
                        height,
                        stageResultCaptureSource,
                        stageResultCaptureMaterials,
                        records,
                        errors);
                }

                foreach (var scenario in DiagnosticScenarios)
                {
                    TryCapture(
                        scenario,
                        diagnosticsDirectory,
                        DiagnosticWidth,
                        DiagnosticHeight,
                        stageResultCaptureSource,
                        stageResultCaptureMaterials,
                        records,
                        errors);
                }
            }
            finally
            {
                foreach (var material in stageResultCaptureMaterials)
                {
                    Object.DestroyImmediate(material);
                }
                Object.DestroyImmediate(stageResultCaptureSource);
            }

            ValidateLocaleParity(records, errors);
            var manifestPath = WriteManifest(outputDirectory, records, errors);
            Debug.Log($"Terminal result visual manifest: {manifestPath}");
            if (errors.Count > 0 ||
                records.Count(record => record.Classification == CaptureClassification.Canonical) != 6 ||
                records.Count(record => record.Classification == CaptureClassification.Diagnostic) != 2)
            {
                throw new InvalidOperationException(
                    $"Terminal result visual evidence failed. See {manifestPath}");
            }
        }

        private static void TryCapture(
            CaptureScenario scenario,
            string outputDirectory,
            int width,
            int height,
            Material stageResultCaptureSource,
            ICollection<Material> stageResultCaptureMaterials,
            ICollection<CaptureRecord> records,
            ICollection<string> errors)
        {
            try
            {
                records.Add(CaptureScenarioImage(
                    scenario,
                    outputDirectory,
                    width,
                    height,
                    stageResultCaptureSource,
                    stageResultCaptureMaterials));
            }
            catch (Exception exception)
            {
                errors.Add($"{scenario.Screen}/{scenario.Locale}/{scenario.Classification}: {exception}");
            }
        }

        private static CaptureRecord CaptureScenarioImage(
            CaptureScenario scenario,
            string outputDirectory,
            int width,
            int height,
            Material stageResultCaptureSource,
            ICollection<Material> stageResultCaptureMaterials)
        {
            StencilMaterial.ClearAll();
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                UnityEngine.Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene);

            UnityStringTableTextResolver resolver = null;
            PopupController popupController = null;
            ScreenController screenController = null;
            GameObject shell = null;
            Texture2D texture = null;
            TMP_Text stageResultTitle = null;
            Material stageResultAuthoredMaterial = null;
            Material stageResultCaptureMaterial = null;
            try
            {
                if (!UnityStringTableTextResolver.TryCreateSettingsDefault(
                        new MemoryLocalePreferenceStore(scenario.Locale),
                        out resolver,
                        out var failureReason))
                {
                    throw new InvalidOperationException(
                        $"Production String Table resolver initialization failed: {failureReason}");
                }
                if (!resolver.TrySetLocale(scenario.Locale))
                {
                    throw new InvalidOperationException(
                        $"Production resolver rejected locale '{scenario.Locale}'.");
                }

                var shellPrefab = Resources.Load<GameObject>(RootShellResourcePath);
                if (shellPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Missing production GameplayUiCanvasRootShell resource: {RootShellResourcePath}");
                }
                shell = PrefabUtility.InstantiatePrefab(shellPrefab, scene) as GameObject;
                if (shell == null)
                {
                    throw new InvalidOperationException(
                        "Production GameplayUiCanvasRootShell could not be instantiated.");
                }
                shell.name = $"TerminalResultVisual_{scenario.Screen}_{scenario.Locale}";
                var rootView = shell.GetComponent<GameplayUiCanvasRootView>();
                if (rootView == null)
                {
                    throw new InvalidOperationException(
                        "Production shell is missing GameplayUiCanvasRootView.");
                }
                rootView.EnsureHierarchy();
                if (rootView.HudView != null)
                {
                    rootView.HudView.gameObject.SetActive(false);
                }

                popupController = new PopupController(new GameplayPopupRuntimeFactory(
                    rootView.PopupLayerView,
                    UiTestPrefabAssetUtility.LoadPopupCatalog(),
                    localizedTextResolver: resolver));
                var timeoutRelay = shell.AddComponent<DisplayPreviewTimeoutRelay>();
                var lifecycleRelay = shell.AddComponent<DisplaySettingsLifecycleRelay>();
                var previewHost = new DisplayPreviewSessionHost(popupController, timeoutRelay);
                var factory = new GameplayScreenRuntimeFactory(
                    rootView.ScreenLayerView,
                    new FakeGameplayQueryFacade(
                        new GameplaySessionReadModel(1, false, true, false),
                        FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                        new GameplayObjectiveReadModel(true, true, false, false)),
                    new ManualGameplayUiPresentationSource(),
                    new FakeAudioSettingsPort(),
                    new FakeDisplaySettingsPort(),
                    NoOpKeyboardBindingSettingsPort.Instance,
                    new RecordingUiAudioPort(),
                    previewHost,
                    lifecycleRelay,
                    UiTestPrefabAssetUtility.LoadScreenCatalog(),
                    localizedTextResolver: resolver,
                    localizedTypographyResolver: DefaultLocalizedTypographyResolver.Instance);
                screenController = new ScreenController(factory);
                var payload = CreatePayload(scenario.Screen);
                if (!screenController.Show(new ScreenRequest(
                        scenario.Screen,
                        payload,
                        $"terminal-result-visual-{scenario.Screen}")))
                {
                    throw new InvalidOperationException(
                        $"ScreenController rejected terminal screen '{scenario.Screen}'.");
                }

                var viewRoot = ResolveCurrentViewRoot(rootView.ScreenLayerView, scenario.Screen);
                SettleScreenEnterMotion(viewRoot);
                SettleNestedPresentationAnimators(viewRoot);
                ForceLayoutAndText(shell);
                var textStates = ValidatePresentation(
                    scenario,
                    rootView,
                    screenController,
                    viewRoot,
                    width,
                    height);
                if (scenario.Screen == ScreenId.StageResult)
                {
                    stageResultTitle = viewRoot
                        .GetComponentsInChildren<TMP_Text>(true)
                        .Single(text => text.text == "Level Clear");
                    stageResultAuthoredMaterial = stageResultTitle.fontSharedMaterial;
                    stageResultCaptureMaterial = new Material(stageResultCaptureSource)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        name = $"{stageResultCaptureSource.name} [{scenario.Locale}]",
                    };
                    stageResultCaptureMaterials.Add(stageResultCaptureMaterial);
                    stageResultTitle.fontSharedMaterial = stageResultCaptureMaterial;
                }
                var nonTextHash = ComputeNonTextStateHash(viewRoot);
                var hierarchyHash = ComputeHierarchyHash(viewRoot);
                var options = new TypographyPreviewScreenshotOptions
                {
                    Width = width,
                    Height = height,
                    BackgroundColor = new Color(0.015f, 0.025f, 0.045f, 1f),
                };
                texture = TypographyPreviewScreenshotUtility.CaptureRootForValidation(
                    shell,
                    options,
                    out var renderPassCount,
                    out var captureFrameIndex);
                ValidateRenderedTextVisibility(scenario, viewRoot);
                var pngBytes = texture.EncodeToPNG();
                var fileName = $"{scenario.Screen}_{scenario.Locale}_{width}x{height}.png";
                var filePath = Path.Combine(outputDirectory, fileName);
                File.WriteAllBytes(filePath, pngBytes);
                ValidatePng(texture, pngBytes, fileName, width, height);

                ReadAssetIdentity(
                    textStates[0].Font,
                    out var fontGuid,
                    out var fontLocalId);
                ReadAssetIdentity(
                    textStates[0].Material,
                    out _,
                    out var materialLocalId);
                return new CaptureRecord(
                    scenario,
                    fileName,
                    width,
                    height,
                    pngBytes.LongLength,
                    ComputeSha256(pngBytes),
                    hierarchyHash,
                    nonTextHash,
                    textStates.Count,
                    fontGuid,
                    fontLocalId,
                    materialLocalId,
                    renderPassCount,
                    captureFrameIndex);
            }
            finally
            {
                if (stageResultTitle != null && stageResultAuthoredMaterial != null)
                {
                    stageResultTitle.fontSharedMaterial = stageResultAuthoredMaterial;
                }
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
                screenController?.Dispose();
                popupController?.Dispose();
                resolver?.Dispose();
                if (shell != null)
                {
                    Object.DestroyImmediate(shell);
                }
                StencilMaterial.ClearAll();
                if (!UnityEngine.Application.isBatchMode &&
                    scene.IsValid() &&
                    scene.isLoaded)
                {
                    if (previousScene.IsValid() && previousScene.isLoaded)
                    {
                        EditorSceneManager.SetActiveScene(previousScene);
                    }
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static IScreenPayload CreatePayload(ScreenId screen)
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            switch (screen)
            {
                case ScreenId.StageResult:
                    return new StageResultScreenPayload(
                        new StageNavigationRequest(
                            stageId,
                            StageNavigationKind.NextStage,
                            "terminal-result-visual-continue"),
                        StageNavigationRequest.None,
                        StageNavigationRequest.None);
                case ScreenId.LevelFailed:
                    return LevelFailedPayloadMapper.Map(new GameplayLevelFailedReadModel(
                        GameplayLevelFailureReason.ChancesExhausted,
                        new StageNavigationRequest(
                            stageId,
                            StageNavigationKind.Retry,
                            "terminal-result-visual-restart",
                            StageTransitionHint.ForKind(StageTransitionKind.LevelFailedRestart))));
                case ScreenId.GameClear:
                    return GameClearScreenPayload.Default;
                default:
                    throw new InvalidOperationException($"Unsupported terminal screen: {screen}");
            }
        }

        private static GameObject ResolveCurrentViewRoot(ScreenLayerView layer, ScreenId screen)
        {
            Component view = screen switch
            {
                ScreenId.StageResult => layer.FindScreenView<StageResultScreenView>(),
                ScreenId.LevelFailed => layer.FindScreenView<LevelFailedScreenView>(),
                ScreenId.GameClear => layer.FindScreenView<GameClearScreenView>(),
                _ => null,
            };
            if (view == null)
            {
                throw new InvalidOperationException(
                    $"Production factory did not mount the '{screen}' view.");
            }
            return view.gameObject;
        }

        private static IReadOnlyList<TextState> ValidatePresentation(
            CaptureScenario scenario,
            GameplayUiCanvasRootView rootView,
            ScreenController controller,
            GameObject viewRoot,
            int width,
            int height)
        {
            if (!rootView.gameObject.activeInHierarchy ||
                !viewRoot.activeInHierarchy ||
                controller.CurrentScreenId != scenario.Screen)
            {
                throw new InvalidOperationException(
                    $"{scenario.Screen}/{scenario.Locale} is not the active current production screen.");
            }
            if (rootView.HudView != null && rootView.HudView.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException("Terminal result visual capture must hide the HUD.");
            }
            var policy = controller.CurrentEntry.Value.Policy;
            if (policy.RetentionMode != ScreenRetentionMode.DisposeOnHide ||
                policy.HudShellMode != HudShellMode.Hidden)
            {
                throw new InvalidOperationException(
                    "Terminal result visual capture requires DisposeOnHide and hidden HUD policy.");
            }

            var canvasGroups = viewRoot.GetComponentsInChildren<CanvasGroup>(true);
            if (canvasGroups.Any(group => group.gameObject.activeInHierarchy && group.alpha <= 0f))
            {
                throw new InvalidOperationException("An active terminal CanvasGroup has zero alpha.");
            }

            var graphics = viewRoot.GetComponentsInChildren<Graphic>(true)
                .Where(graphic => graphic.gameObject.activeInHierarchy)
                .ToArray();
            if (graphics.Length == 0 ||
                graphics.Any(graphic => !graphic.enabled || graphic.materialForRendering == null))
            {
                throw new InvalidOperationException(
                    "Required terminal graphics must be active, enabled, and material-resolved.");
            }
            var buttons = viewRoot.GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.activeInHierarchy)
                .ToArray();
            var expectedButtonCount = scenario.Screen == ScreenId.LevelFailed ? 2 : 1;
            if (buttons.Length != expectedButtonCount ||
                buttons.Any(button => !button.interactable || button.targetGraphic == null))
            {
                throw new InvalidOperationException(
                    $"{scenario.Screen} button completeness failed.");
            }

            var texts = viewRoot.GetComponentsInChildren<TMP_Text>(true)
                .Where(text => text.gameObject.activeInHierarchy)
                .ToArray();
            var expectations = BuildTextExpectations(scenario);
            var expectedActiveTextCount = expectations.Count +
                                          (scenario.Screen == ScreenId.StageResult ? 1 : 0);
            if (texts.Length != expectedActiveTextCount)
            {
                throw new InvalidOperationException(
                    $"{scenario.Screen} expected {expectedActiveTextCount} active TMP labels, found {texts.Length}.");
            }

            var states = new List<TextState>();
            var validatedTexts = new HashSet<TMP_Text>();
            foreach (var expectation in expectations)
            {
                var text = GetField<TMP_Text>(
                    viewRoot.GetComponent(expectation.ViewType),
                    expectation.FieldName);
                ValidateText(
                    text,
                    expectation,
                    scenario.Locale,
                    viewRoot,
                    width,
                    height);
                states.Add(new TextState(text.font, text.fontSharedMaterial));
                validatedTexts.Add(text);
            }

            if (scenario.Screen == ScreenId.StageResult)
            {
                var title = texts.Single(text => !validatedTexts.Contains(text));
                ValidateStageResultTitle(title, viewRoot, width, height);
                states.Add(new TextState(title.font, title.fontSharedMaterial));
            }

            var joined = string.Join("\n", texts.Select(text => text.text));
            if (joined.Contains("Game Over", StringComparison.Ordinal) ||
                expectations.Any(expectation => string.IsNullOrWhiteSpace(expectation.ExpectedText)))
            {
                throw new InvalidOperationException("Terminal capture contains stale or blank text.");
            }
            if (scenario.Locale == "ko-KR" &&
                (joined.Contains("Stage Failed", StringComparison.Ordinal) ||
                 joined.Contains("Main Menu", StringComparison.Ordinal) ||
                 joined.Contains("Restart Stage", StringComparison.Ordinal) ||
                 joined.Contains("Continue", StringComparison.Ordinal) ||
                 joined.Contains("Game Clear", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Korean terminal capture retains stale English.");
            }
            if (scenario.Locale == "en-US" && joined.Any(character => character >= '\uAC00' && character <= '\uD7A3'))
            {
                throw new InvalidOperationException("English terminal capture retains stale Korean.");
            }
            return states;
        }

        private static IReadOnlyList<TextExpectation> BuildTextExpectations(CaptureScenario scenario)
        {
            switch (scenario.Screen)
            {
                case ScreenId.StageResult:
                    return new[]
                    {
                        new TextExpectation(
                            typeof(StageResultScreenView),
                            "_continueButtonLabel",
                            scenario.Locale == "ko-KR" ? "계속" : "Continue",
                            TypographyStyleTag.Button),
                    };
                case ScreenId.LevelFailed:
                    if (scenario.Locale == "ko-KR")
                    {
                        return new[]
                        {
                            new TextExpectation(typeof(LevelFailedScreenView), "_titleLabel", "스테이지 실패", TypographyStyleTag.HeaderLarge),
                            new TextExpectation(typeof(LevelFailedScreenView), "_detailLabel", "모든 기회를 소진했습니다. 스테이지를 다시 시작하거나 메인 메뉴로 돌아가세요.", TypographyStyleTag.Body, isLevelFailedDetail: true),
                            new TextExpectation(typeof(LevelFailedScreenView), "_restartLevelButtonLabel", "스테이지 다시 시작", TypographyStyleTag.Button),
                            new TextExpectation(typeof(LevelFailedScreenView), "_mainButtonLabel", "메인 메뉴", TypographyStyleTag.Button),
                        };
                    }
                    return new[]
                    {
                        new TextExpectation(typeof(LevelFailedScreenView), "_titleLabel", "Stage Failed", TypographyStyleTag.HeaderLarge),
                        new TextExpectation(typeof(LevelFailedScreenView), "_detailLabel", "All chances have been used. Restart the stage or return to the main menu.", TypographyStyleTag.Body, isLevelFailedDetail: true),
                        new TextExpectation(typeof(LevelFailedScreenView), "_restartLevelButtonLabel", "Restart Stage", TypographyStyleTag.Button),
                        new TextExpectation(typeof(LevelFailedScreenView), "_mainButtonLabel", "Main Menu", TypographyStyleTag.Button),
                    };
                case ScreenId.GameClear:
                    return new[]
                    {
                        new TextExpectation(
                            typeof(GameClearScreenView),
                            "_titleLabel",
                            scenario.Locale == "ko-KR" ? "게임 클리어" : "Game Clear",
                            TypographyStyleTag.HeaderLarge),
                        new TextExpectation(
                            typeof(GameClearScreenView),
                            "_mainButtonLabel",
                            scenario.Locale == "ko-KR" ? "메인 메뉴" : "Main Menu",
                            TypographyStyleTag.Button),
                    };
                default:
                    throw new InvalidOperationException($"Unsupported terminal screen: {scenario.Screen}");
            }
        }

        private static void ValidateText(
            TMP_Text text,
            TextExpectation expectation,
            string locale,
            GameObject viewRoot,
            int width,
            int height)
        {
            if (text == null || !text.isActiveAndEnabled || text.text != expectation.ExpectedText)
            {
                throw new InvalidOperationException(
                    $"{expectation.FieldName} text mismatch or inactive.");
            }
            var theme = UiTestPrefabAssetUtility.LoadScreenCatalog().SettingsTypographyTheme;
            TMP_FontAsset expectedFont;
            Material expectedMaterial;
            FontStyles expectedFontStyle;
            if (locale == "en-US")
            {
                var authored = ResolveAuthoredEnglishText(expectation);
                expectedFont = authored.font;
                expectedMaterial = authored.fontSharedMaterial;
                expectedFontStyle = authored.fontStyle;
            }
            else
            {
                var expectedStyle = theme.ResolveOrThrow(locale, expectation.Role);
                expectedFont = expectedStyle.FontAsset;
                expectedMaterial = expectedStyle.MaterialPreset;
                expectedFontStyle = expectedStyle.FontStyle;
            }
            if (text.font != expectedFont ||
                text.fontSharedMaterial != expectedMaterial ||
                text.fontStyle != expectedFontStyle)
            {
                throw new InvalidOperationException(
                    $"{expectation.FieldName} typography identity mismatch.");
            }
            if (expectation.ExpectedText.Any(
                    character => !char.IsControl(character) &&
                                 !char.IsWhiteSpace(character) &&
                                 !text.font.HasCharacter(character, false, false)))
            {
                throw new InvalidOperationException(
                    $"{expectation.FieldName} has missing native glyph coverage.");
            }
            text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            if (text.textInfo.characterInfo.Any(
                    character => character.isVisible && character.fontAsset != text.font))
            {
                throw new InvalidOperationException(
                    $"{expectation.FieldName} used a fallback font.");
            }
            if (text.textInfo.characterCount == 0 ||
                text.textBounds.size.x > text.rectTransform.rect.width + 0.5f ||
                text.textBounds.size.y > text.rectTransform.rect.height + 0.5f)
            {
                throw new InvalidOperationException(
                    $"{expectation.FieldName} text bounds overflow its authored rect.");
            }
            var relativeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                viewRoot.transform,
                text.rectTransform);
            if (relativeBounds.min.x < -width ||
                relativeBounds.max.x > width ||
                relativeBounds.min.y < -height ||
                relativeBounds.max.y > height)
            {
                throw new InvalidOperationException(
                    $"{expectation.FieldName} lies outside the capture viewport.");
            }
            if (!expectation.IsLevelFailedDetail)
            {
                return;
            }

            var serialized = new SerializedObject(text);
            var baseSize = serialized.FindProperty("m_fontSizeBase")?.floatValue ?? -1f;
            if (!Mathf.Approximately(text.fontSize, 18f) ||
                !Mathf.Approximately(baseSize, 18f) ||
                text.textInfo.lineCount > 2 ||
                text.textWrappingMode == TextWrappingModes.NoWrap ||
                text.overflowMode != TextOverflowModes.Overflow)
            {
                throw new InvalidOperationException(
                    "LevelFailed detail must remain size/base 18, wrapped, unclipped, and at most two lines.");
            }
        }

        private static void ValidateStageResultTitle(
            TMP_Text title,
            GameObject viewRoot,
            int width,
            int height)
        {
            var authored = UiTestPrefabAssetUtility
                .LoadScreenPrefab<StageResultScreenView>(
                    UiTestPrefabAssetUtility.StageResultScreenPrefabPath)
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.text == "Level Clear");
            if (!title.isActiveAndEnabled ||
                title.text != "Level Clear" ||
                title.font != authored.font ||
                title.fontSharedMaterial != authored.fontSharedMaterial ||
                title.fontStyle != authored.fontStyle)
            {
                throw new InvalidOperationException(
                    "StageResult title text or authored typography identity mismatch.");
            }
            if (title.text.Any(
                    character => !char.IsControl(character) &&
                                 !char.IsWhiteSpace(character) &&
                                 !title.font.HasCharacter(character, false, false)))
            {
                throw new InvalidOperationException(
                    "StageResult title has missing native glyph coverage.");
            }
            title.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            if (title.textInfo.characterInfo.Any(
                    character => character.isVisible && character.fontAsset != title.font))
            {
                throw new InvalidOperationException(
                    "StageResult title used a fallback font.");
            }
            if (title.textInfo.characterCount == 0 ||
                title.textBounds.size.x > title.rectTransform.rect.width + 0.5f ||
                title.textBounds.size.y > title.rectTransform.rect.height + 0.5f)
            {
                throw new InvalidOperationException(
                    "StageResult title text bounds overflow its authored rect.");
            }
            var relativeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                viewRoot.transform,
                title.rectTransform);
            var rootRect = viewRoot.transform as RectTransform;
            if (rootRect == null ||
                relativeBounds.min.x < rootRect.rect.xMin - 0.5f ||
                relativeBounds.max.x > rootRect.rect.xMax + 0.5f ||
                relativeBounds.min.y < rootRect.rect.yMin - 0.5f ||
                relativeBounds.max.y > rootRect.rect.yMax + 0.5f)
            {
                throw new InvalidOperationException(
                    "StageResult title lies outside the capture viewport.");
            }
        }

        private static void ValidateRenderedTextVisibility(
            CaptureScenario scenario,
            GameObject viewRoot)
        {
            foreach (var text in viewRoot.GetComponentsInChildren<TMP_Text>(true)
                         .Where(text => text.gameObject.activeInHierarchy))
            {
                text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                var visibleCharacterCount = text.textInfo.characterInfo.Count(
                    character => character.isVisible);
                var vertexCount = text.textInfo.meshInfo.Sum(meshInfo => meshInfo.vertexCount);
                if (text.canvasRenderer.cull ||
                    text.canvasRenderer.GetAlpha() <= 0f ||
                    text.color.a <= 0f ||
                    visibleCharacterCount == 0 ||
                    vertexCount == 0)
                {
                    throw new InvalidOperationException(
                        $"{scenario.Screen}/{scenario.Locale} rendered TMP '{text.name}' is not visible: " +
                        $"cull={text.canvasRenderer.cull}, rendererAlpha={text.canvasRenderer.GetAlpha()}, " +
                        $"colorAlpha={text.color.a}, visibleCharacters={visibleCharacterCount}, vertices={vertexCount}.");
                }
            }
        }

        private static TMP_Text ResolveAuthoredEnglishText(TextExpectation expectation)
        {
            Component prefab = expectation.ViewType == typeof(StageResultScreenView)
                ? UiTestPrefabAssetUtility.LoadScreenPrefab<StageResultScreenView>(
                    UiTestPrefabAssetUtility.StageResultScreenPrefabPath)
                : expectation.ViewType == typeof(LevelFailedScreenView)
                    ? UiTestPrefabAssetUtility.LoadScreenPrefab<LevelFailedScreenView>(
                        UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath)
                    : UiTestPrefabAssetUtility.LoadScreenPrefab<GameClearScreenView>(
                        UiTestPrefabAssetUtility.GameClearScreenPrefabPath);
            return GetField<TMP_Text>(prefab, expectation.FieldName);
        }

        private static void ValidateLocaleParity(
            IReadOnlyCollection<CaptureRecord> records,
            ICollection<string> errors)
        {
            foreach (var group in records
                         .Where(record => record.Classification == CaptureClassification.Canonical)
                         .GroupBy(record => record.Screen))
            {
                if (group.Count() != 2 ||
                    group.Select(record => record.HierarchyHash).Distinct().Count() != 1 ||
                    group.Select(record => record.NonTextStateHash).Distinct().Count() != 1)
                {
                    errors.Add(
                        $"{group.Key}: locale captures differ in hierarchy or non-text graphic state.");
                }
            }
        }

        private static string WriteManifest(
            string outputDirectory,
            IReadOnlyCollection<CaptureRecord> records,
            IReadOnlyCollection<string> errors)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(
                TerminalResultLocalizationContract.Table);
            var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
            var builder = new StringBuilder();
            builder.AppendLine("schema_version=1");
            builder.AppendLine($"capture_target_implementation_sha={TypographyPreviewScreenshotManifestUtility.ReadCurrentGitHead()}");
            builder.AppendLine($"evidence_generation_timestamp_utc={DateTime.UtcNow:O}");
            builder.AppendLine($"worktree_path={Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."))}");
            builder.AppendLine($"unity_version={UnityEngine.Application.unityVersion}");
            builder.AppendLine($"runner_command=./run_tests.sh typography-result-visual");
            builder.AppendLine($"capture_method={CommandLineEntryPoint}");
            builder.AppendLine("production_composition=GameplayUiCanvasRootShell|ScreenPrefabCatalog|GameplayScreenRuntimeFactory|ScreenController|ProductionStringTableResolver|ProductionTypographyTheme");
            builder.AppendLine($"canonical_count={records.Count(record => record.Classification == CaptureClassification.Canonical)}");
            builder.AppendLine($"diagnostic_count={records.Count(record => record.Classification == CaptureClassification.Diagnostic)}");
            builder.AppendLine($"string_table_collection={collection?.TableCollectionName ?? string.Empty}");
            builder.AppendLine($"string_table_shared_data_guid={AssetDatabase.AssetPathToGUID(StringTableSharedDataPath)}");
            builder.AppendLine($"error_count={errors.Count}");
            foreach (var record in records)
            {
                builder.AppendLine();
                builder.AppendLine($"[capture/{record.Classification}/{record.Screen}/{record.Locale}]");
                builder.AppendLine($"classification={record.Classification.ToString().ToUpperInvariant()}");
                builder.AppendLine($"screen={record.Screen}");
                builder.AppendLine($"locale={record.Locale}");
                builder.AppendLine($"resolution={record.Width}x{record.Height}");
                builder.AppendLine($"png={record.FileName}");
                builder.AppendLine($"png_byte_count={record.PngByteCount}");
                builder.AppendLine($"png_sha256={record.PngSha256}");
                builder.AppendLine($"font_asset_guid={record.FontGuid}");
                builder.AppendLine($"font_asset_local_id={record.FontLocalId}");
                builder.AppendLine($"material_local_id={record.MaterialLocalId}");
                builder.AppendLine($"hierarchy_sha256={record.HierarchyHash}");
                builder.AppendLine($"non_text_state_sha256={record.NonTextStateHash}");
                builder.AppendLine($"text_count={record.TextCount}");
                builder.AppendLine($"camera_render_pass_count={record.RenderPassCount}");
                builder.AppendLine($"capture_frame_index={record.CaptureFrameIndex}");
            }
            foreach (var error in errors)
            {
                builder.AppendLine();
                builder.AppendLine($"error={error.Replace(Environment.NewLine, " ")}");
            }
            File.WriteAllText(manifestPath, builder.ToString(), new UTF8Encoding(false));
            return manifestPath;
        }

        private static void ValidatePng(
            Texture2D texture,
            byte[] pngBytes,
            string fileName,
            int width,
            int height)
        {
            if (texture == null ||
                texture.width != width ||
                texture.height != height ||
                pngBytes == null ||
                pngBytes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{fileName} is missing or has the wrong resolution.");
            }
            var pixels = texture.GetPixels32();
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null &&
                pixels.All(pixel => pixel.Equals(pixels[0])))
            {
                throw new InvalidOperationException($"{fileName} is blank or single-color.");
            }
        }

        private static string ComputeHierarchyHash(GameObject root)
        {
            var values = root.GetComponentsInChildren<Transform>(true)
                .Select(transform =>
                    $"{GetPath(root.transform, transform)}|{transform.gameObject.activeSelf}|{transform.GetType().FullName}")
                .OrderBy(value => value, StringComparer.Ordinal);
            return ComputeSha256(Encoding.UTF8.GetBytes(string.Join("\n", values)));
        }

        private static string ComputeNonTextStateHash(GameObject root)
        {
            var values = new List<string>();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)
                         .Where(graphic => graphic is not TMP_Text))
            {
                var spriteGuid = string.Empty;
                if (graphic is Image image && image.sprite != null)
                {
                    spriteGuid = AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(image.sprite));
                }
                var materialGuid = graphic.materialForRendering != null
                    ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graphic.materialForRendering))
                    : string.Empty;
                values.Add(
                    $"{GetPath(root.transform, graphic.transform)}|{graphic.GetType().FullName}|" +
                    $"{graphic.gameObject.activeSelf}|{graphic.enabled}|{spriteGuid}|{materialGuid}");
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                values.Add(
                    $"BUTTON|{GetPath(root.transform, button.transform)}|" +
                    $"{button.gameObject.activeSelf}|{button.enabled}|{button.interactable}");
            }
            values.Sort(StringComparer.Ordinal);
            return ComputeSha256(Encoding.UTF8.GetBytes(string.Join("\n", values)));
        }

        private static void ForceLayoutAndText(GameObject root)
        {
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            }
        }

        private static void SettleScreenEnterMotion(GameObject viewRoot)
        {
            foreach (var component in viewRoot.GetComponents<MonoBehaviour>())
            {
                var stopMethod = component.GetType().GetMethod(
                    "StopRootEnterMotion",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                stopMethod?.Invoke(component, null);
            }
        }

        private static void SettleNestedPresentationAnimators(GameObject viewRoot)
        {
            foreach (var animator in viewRoot.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController == null)
                {
                    continue;
                }

                animator.Rebind();
                animator.Play("Base Layer.Idle", 0, 0f);
                animator.Update(0f);
                if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"))
                {
                    throw new InvalidOperationException(
                        $"Unable to settle presentation Animator '{animator.name}' to Idle.");
                }
                animator.enabled = false;
            }
        }

        private static T GetField<T>(object instance, string fieldName) where T : class
        {
            var field = instance?.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(instance) is T value)
            {
                return value;
            }
            throw new InvalidOperationException(
                $"{instance?.GetType().Name ?? "<null>"}.{fieldName} is unavailable.");
        }

        private static string GetPath(Transform root, Transform transform)
        {
            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                if (current == root)
                {
                    break;
                }
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static void ReadAssetIdentity(
            Object asset,
            out string guid,
            out long localId)
        {
            if (asset == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId))
            {
                throw new InvalidOperationException(
                    $"Unable to resolve asset identity for {asset?.name ?? "<null>"}.");
            }
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }

        private static string ReadArgument(IReadOnlyList<string> args, string name)
        {
            for (var index = 0; index + 1 < args.Count; index++)
            {
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                {
                    return args[index + 1];
                }
            }
            return string.Empty;
        }

        private static int ReadPositiveInt(
            IReadOnlyList<string> args,
            string name,
            int fallback)
        {
            var value = ReadArgument(args, name);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                   parsed > 0
                ? parsed
                : fallback;
        }

        private enum CaptureClassification
        {
            Canonical,
            Diagnostic,
        }

        private readonly struct CaptureScenario
        {
            public CaptureScenario(
                ScreenId screen,
                string locale,
                CaptureClassification classification)
            {
                Screen = screen;
                Locale = locale;
                Classification = classification;
            }

            public ScreenId Screen { get; }
            public string Locale { get; }
            public CaptureClassification Classification { get; }
        }

        private readonly struct TextExpectation
        {
            public TextExpectation(
                Type viewType,
                string fieldName,
                string expectedText,
                TypographyStyleTag role,
                bool isLevelFailedDetail = false)
            {
                ViewType = viewType;
                FieldName = fieldName;
                ExpectedText = expectedText;
                Role = role;
                IsLevelFailedDetail = isLevelFailedDetail;
            }

            public Type ViewType { get; }
            public string FieldName { get; }
            public string ExpectedText { get; }
            public TypographyStyleTag Role { get; }
            public bool IsLevelFailedDetail { get; }
        }

        private readonly struct TextState
        {
            public TextState(TMP_FontAsset font, Material material)
            {
                Font = font;
                Material = material;
            }

            public TMP_FontAsset Font { get; }
            public Material Material { get; }
        }

        private readonly struct CaptureRecord
        {
            public CaptureRecord(
                CaptureScenario scenario,
                string fileName,
                int width,
                int height,
                long pngByteCount,
                string pngSha256,
                string hierarchyHash,
                string nonTextStateHash,
                int textCount,
                string fontGuid,
                long fontLocalId,
                long materialLocalId,
                int renderPassCount,
                int captureFrameIndex)
            {
                Screen = scenario.Screen;
                Locale = scenario.Locale;
                Classification = scenario.Classification;
                FileName = fileName;
                Width = width;
                Height = height;
                PngByteCount = pngByteCount;
                PngSha256 = pngSha256;
                HierarchyHash = hierarchyHash;
                NonTextStateHash = nonTextStateHash;
                TextCount = textCount;
                FontGuid = fontGuid;
                FontLocalId = fontLocalId;
                MaterialLocalId = materialLocalId;
                RenderPassCount = renderPassCount;
                CaptureFrameIndex = captureFrameIndex;
            }

            public ScreenId Screen { get; }
            public string Locale { get; }
            public CaptureClassification Classification { get; }
            public string FileName { get; }
            public int Width { get; }
            public int Height { get; }
            public long PngByteCount { get; }
            public string PngSha256 { get; }
            public string HierarchyHash { get; }
            public string NonTextStateHash { get; }
            public int TextCount { get; }
            public string FontGuid { get; }
            public long FontLocalId { get; }
            public long MaterialLocalId { get; }
            public int RenderPassCount { get; }
            public int CaptureFrameIndex { get; }
        }

        private sealed class MemoryLocalePreferenceStore : IUiLocalePreferenceStore
        {
            private string _locale;

            public MemoryLocalePreferenceStore(string locale)
            {
                _locale = locale;
            }

            public bool TryLoad(out string localeCode)
            {
                localeCode = _locale;
                return !string.IsNullOrWhiteSpace(localeCode);
            }

            public void Save(string localeCode)
            {
                _locale = localeCode;
            }
        }
    }
}

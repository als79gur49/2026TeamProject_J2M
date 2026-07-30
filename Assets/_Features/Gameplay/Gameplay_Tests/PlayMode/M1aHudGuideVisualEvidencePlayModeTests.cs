#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Flow.Audio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using Game.Shared.Audio;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class M1aHudGuideVisualEvidencePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/UIAudioScene.unity";
        private const string LabStageIdValue = "stage-0-1";
        private const string WardStageIdValue = "stage-2-1";
        private const string LocalePreferenceKey = "ui.selected_locale";
        private const string ClimateFontPath =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset";
        private const string ManifestFileName = "m1a-hud-guide-capture.log";
        private const int ExpectedRemainingChances = 2;
        private const int ExpectedMaxChances = 3;
        private const int PixelDeltaThreshold = 8;

        private static readonly LocaleScenario[] Scenarios =
        {
            new("en-US", "Lab-01", "Pause", "CHANCES", "Move", "Push", "Flip"),
            new("ko-KR", "연구실-01", "일시 정지", "기회", "이동", "밀기", "뒤집기"),
        };

        private static readonly StageHudScenario[] WardScenarios =
        {
            new("en-US", "Ward[A]-01"),
            new("ko-KR", "병동[A]-01"),
        };

        [Category("Full")]
        [UnityTest]
        public IEnumerator CaptureStage0_1HudAndWorldGuideCanonicalEvidence()
        {
            var args = Environment.GetCommandLineArgs();
            var outputDirectory = ReadRequiredArgument(args, "-m1aHudGuideVisualOutput");
            var expectedHead = ReadRequiredArgument(args, "-m1aHudGuideVisualHead");
            var expectedTree = ReadRequiredArgument(args, "-m1aHudGuideVisualTree");
            var requestedWidth = ReadPositiveInt(args, "-m1aHudGuideVisualWidth", 1920);
            var requestedHeight = ReadPositiveInt(args, "-m1aHudGuideVisualHeight", 1080);
            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);

            var preferenceBackups = new[]
            {
                PlayerPrefsBackup.CaptureString(LocalePreferenceKey),
                PlayerPrefsBackup.CaptureString(EditorDirectPlayContextStore.TempSaveSlotStoreKey),
                PlayerPrefsBackup.CaptureInt(EditorDirectPlayContextStore.TempActiveSlotProviderKey),
            };
            var errors = new List<string>();
            var captures = new List<LocaleCapture>();
            var wardCaptures = new List<StageHudCapture>();

            try
            {
                Screen.SetResolution(requestedWidth, requestedHeight, FullScreenMode.Windowed);
                yield return null;
                yield return new WaitForEndOfFrame();
                requestedWidth = Screen.width;
                requestedHeight = Screen.height;

                foreach (var scenario in Scenarios)
                {
                    LocaleCapture captured = null;
                    Exception failure = null;
                    var routine = CaptureLocale(
                        scenario,
                        outputDirectory,
                        requestedWidth,
                        requestedHeight,
                        value => captured = value);
                    try
                    {
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
                                failure = exception;
                            }

                            if (!moved)
                            {
                                break;
                            }

                            yield return current;
                        }
                    }
                    finally
                    {
                        (routine as IDisposable)?.Dispose();
                    }

                    var cleanup = CleanupSceneRuntime();
                    while (cleanup.MoveNext())
                    {
                        yield return cleanup.Current;
                    }

                    if (failure != null)
                    {
                        errors.Add($"{scenario.Locale}: {failure}");
                    }
                    else if (captured == null)
                    {
                        errors.Add($"{scenario.Locale}: capture returned no evidence.");
                    }
                    else
                    {
                        captures.Add(captured);
                    }
                }

                ValidateCrossLocaleParity(captures, errors);

                foreach (var scenario in WardScenarios)
                {
                    StageHudCapture captured = null;
                    Exception failure = null;
                    var routine = CaptureStageHudLocale(
                        scenario,
                        outputDirectory,
                        requestedWidth,
                        requestedHeight,
                        value => captured = value);
                    try
                    {
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
                                failure = exception;
                            }

                            if (!moved)
                            {
                                break;
                            }

                            yield return current;
                        }
                    }
                    finally
                    {
                        (routine as IDisposable)?.Dispose();
                    }

                    var cleanup = CleanupSceneRuntime();
                    while (cleanup.MoveNext())
                    {
                        yield return cleanup.Current;
                    }

                    if (failure != null)
                    {
                        errors.Add($"{WardStageIdValue}/{scenario.Locale}: {failure}");
                    }
                    else if (captured == null)
                    {
                        errors.Add(
                            $"{WardStageIdValue}/{scenario.Locale}: capture returned no evidence.");
                    }
                    else
                    {
                        wardCaptures.Add(captured);
                    }
                }
            }
            finally
            {
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
                EditorDirectPlayContextStore.ClearTempDirectPlaySave();
                for (var i = 0; i < preferenceBackups.Length; i++)
                {
                    preferenceBackups[i].Restore();
                }

                PlayerPrefs.Save();
                Time.timeScale = 1f;
            }

            var manifestPath = WriteManifest(
                outputDirectory,
                expectedHead,
                expectedTree,
                requestedWidth,
                requestedHeight,
                captures,
                wardCaptures,
                errors);
            UnityEngine.Debug.Log($"M1A HUD/World Guide visual manifest: {manifestPath}");
            Assert.That(
                errors,
                Is.Empty,
                $"M1A HUD/World Guide visual evidence failed. See {manifestPath}");
            Assert.That(captures, Has.Count.EqualTo(Scenarios.Length));
            Assert.That(wardCaptures, Has.Count.EqualTo(WardScenarios.Length));
        }

        private static IEnumerator CaptureLocale(
            LocaleScenario scenario,
            string outputDirectory,
            int requestedWidth,
            int requestedHeight,
            Action<LocaleCapture> onCaptured)
        {
            var previousTimeScale = Time.timeScale;
            var stageId = StageId.CreateOrThrow(LabStageIdValue);
            Texture2D canonicalFrame = null;
            try
            {
                PrepareFreshRuntime(stageId, scenario.Locale);
                var load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                    ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                if (load == null)
                {
                    throw new InvalidOperationException($"Failed to load {ScenePath}.");
                }

                while (!load.isDone)
                {
                    yield return null;
                }

                yield return null;
                yield return new WaitForSecondsRealtime(2f);
                for (var frame = 0; frame < 12; frame++)
                {
                    ForceLayoutAndText();
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }

                if (Screen.width != requestedWidth || Screen.height != requestedHeight)
                {
                    throw new InvalidOperationException(
                        $"Game view is {Screen.width}x{Screen.height}; expected {requestedWidth}x{requestedHeight}.");
                }

                var host = FindExactlyOne<GameplaySceneHost>("GameplaySceneHost");
                var installer = FindExactlyOne<GameplayUiFlowInstaller>("GameplayUiFlowInstaller");
                var guidePresenter = host.GetComponent<GameplayWorldGuidePresenter>();
                if (guidePresenter == null)
                {
                    throw new InvalidOperationException(
                        "Stage 0-1 runtime is missing GameplayWorldGuidePresenter.");
                }

                if (installer.RootView == null ||
                    installer.HudRootPresenter == null ||
                    installer.HudController == null ||
                    installer.HudView == null)
                {
                    throw new InvalidOperationException(
                        "GameplayUiFlowInstaller did not complete the production HUD composition.");
                }

                var hudBinding = installer.HudView.GetComponent<GameplayHudLocalizationBinding>();
                if (hudBinding == null)
                {
                    throw new InvalidOperationException(
                        "Production HUD is missing GameplayHudLocalizationBinding.");
                }

                var chanceView = installer.HudView.ChancePanelView;
                var chanceRoot = ReadPrivateField<GameObject>(chanceView, "_root");
                var stageNameLabel = ReadPrivateField<TMP_Text>(installer.HudView, "_stageNameLabel");
                var pauseButton = hudBinding.PauseText.GetComponentInParent<Button>(includeInactive: true);
                if (pauseButton == null)
                {
                    throw new InvalidOperationException(
                        "Pause TMP is not owned by the production pause Button.");
                }

                var guideTargets = new List<IWorldGuideLocalizationTarget>();
                guidePresenter.CopyLocalizationTargets(guideTargets);
                if (guideTargets.Count != 3)
                {
                    throw new InvalidOperationException(
                        $"Stage 0-1 produced {guideTargets.Count} World Guide targets; expected 3.");
                }

                var guides = guideTargets
                    .OrderBy(view => view.InstructionKind)
                    .ToDictionary(view => view.InstructionKind);
                RequireGuide(guides, WorldGuideInstructionKind.Movement);
                RequireGuide(guides, WorldGuideInstructionKind.Push);
                RequireGuide(guides, WorldGuideInstructionKind.Flip);

                var chanceSlotBounds = ValidateChanceFixture(chanceView);
                Time.timeScale = 0f;
                ForceLayoutAndText();
                yield return null;
                yield return new WaitForEndOfFrame();

                var climate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ClimateFontPath);
                if (climate == null)
                {
                    throw new InvalidOperationException($"Climate font missing at {ClimateFontPath}.");
                }

                var targets = new Dictionary<string, TMP_Text>(StringComparer.Ordinal)
                {
                    ["StageName"] = stageNameLabel,
                    ["Pause"] = hudBinding.PauseText,
                    ["Chance"] = hudBinding.ChancesText,
                    ["Movement"] = guides[WorldGuideInstructionKind.Movement].ActionTextLabel,
                    ["Push"] = guides[WorldGuideInstructionKind.Push].ActionTextLabel,
                    ["Flip"] = guides[WorldGuideInstructionKind.Flip].ActionTextLabel,
                };
                var expectedTexts = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["StageName"] = scenario.StageName,
                    ["Pause"] = scenario.Pause,
                    ["Chance"] = scenario.Chance,
                    ["Movement"] = scenario.Movement,
                    ["Push"] = scenario.Push,
                    ["Flip"] = scenario.Flip,
                };

                var evidence = new Dictionary<string, TargetEvidence>(StringComparer.Ordinal);
                foreach (var pair in targets)
                {
                    evidence.Add(
                        pair.Key,
                        ValidateTextTarget(
                            pair.Key,
                            pair.Value,
                            expectedTexts[pair.Key],
                            scenario.Locale,
                            climate,
                            localizedTarget: true,
                            requestedWidth,
                            requestedHeight));
                }

                ValidateHudLayout(
                    installer.HudView,
                    evidence["StageName"].ScreenBounds,
                    pauseButton.GetComponent<RectTransform>(),
                    evidence["Pause"].ScreenBounds,
                    evidence["Chance"].ScreenBounds,
                    chanceSlotBounds);
                var keycaps = ValidateWorldGuideLayoutAndKeycaps(
                    guides,
                    evidence,
                    requestedWidth,
                    requestedHeight);
                ValidateRequiredGraphics(
                    pauseButton.gameObject,
                    chanceRoot,
                    guides.Values);

                canonicalFrame = ScreenCapture.CaptureScreenshotAsTexture();
                if (canonicalFrame == null)
                {
                    throw new InvalidOperationException("Canonical backbuffer capture returned null.");
                }

                if (canonicalFrame.width != requestedWidth ||
                    canonicalFrame.height != requestedHeight ||
                    !HasPixelVariation(canonicalFrame))
                {
                    throw new InvalidOperationException(
                        "Canonical frame is blank or has the wrong resolution.");
                }

                var hudBounds = Union(
                    evidence["StageName"].ScreenBounds,
                    ScreenBounds(pauseButton.GetComponent<RectTransform>()),
                    ScreenBounds(chanceRoot.GetComponent<RectTransform>()));
                var guideBounds = Union(
                    evidence["Movement"].ScreenBounds,
                    evidence["Push"].ScreenBounds,
                    evidence["Flip"].ScreenBounds,
                    keycaps.MovementBounds,
                    keycaps.PushBounds,
                    keycaps.FlipBounds);
                var hudFile = $"M1A_HUD_{scenario.Locale}.png";
                var guideFile = $"M1A_WorldGuide_{scenario.Locale}.png";
                var hudPath = Path.Combine(outputDirectory, hudFile);
                var guidePath = Path.Combine(outputDirectory, guideFile);
                WriteCrop(canonicalFrame, hudBounds, 48, hudPath);
                WriteCrop(canonicalFrame, guideBounds, 72, guidePath);

                foreach (var key in new[] { "StageName", "Pause", "Chance", "Movement", "Push", "Flip" })
                {
                    var delta = -1L;
                    var proof = CapturePixelProof(
                        canonicalFrame,
                        targets[key],
                        evidence[key].ScreenBounds,
                        value => delta = value);
                    while (proof.MoveNext())
                    {
                        yield return proof.Current;
                    }

                    if (delta <= PixelDeltaThreshold)
                    {
                        throw new InvalidOperationException(
                            $"{key} pixel delta {delta} did not exceed {PixelDeltaThreshold}.");
                    }

                    evidence[key].PixelDelta = delta;
                }

                var graphicIdentity = ComputeNonTextGraphicIdentity(
                    installer.HudView.gameObject,
                    guides.Values.Select(view => ((Component)view).gameObject));
                var semanticFixture = ComputeSha256(
                    Encoding.UTF8.GetBytes(
                        $"{LabStageIdValue}|{ExpectedRemainingChances}/{ExpectedMaxChances}|" +
                        $"{keycaps.Movement}|{keycaps.Push}|{keycaps.Flip}"));
                onCaptured(new LocaleCapture(
                    scenario.Locale,
                    hudFile,
                    FileLength(hudPath),
                    FileSha256(hudPath),
                    guideFile,
                    FileLength(guidePath),
                    FileSha256(guidePath),
                    evidence,
                    keycaps,
                    graphicIdentity,
                    semanticFixture));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                if (canonicalFrame != null)
                {
                    Object.DestroyImmediate(canonicalFrame);
                }
            }
        }

        private static IEnumerator CaptureStageHudLocale(
            StageHudScenario scenario,
            string outputDirectory,
            int requestedWidth,
            int requestedHeight,
            Action<StageHudCapture> onCaptured)
        {
            var previousTimeScale = Time.timeScale;
            var stageId = StageId.CreateOrThrow(WardStageIdValue);
            Texture2D canonicalFrame = null;
            try
            {
                PrepareFreshRuntime(stageId, scenario.Locale);
                var load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                    ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                if (load == null)
                {
                    throw new InvalidOperationException($"Failed to load {ScenePath}.");
                }

                while (!load.isDone)
                {
                    yield return null;
                }

                yield return null;
                yield return new WaitForSecondsRealtime(2f);
                for (var frame = 0; frame < 12; frame++)
                {
                    ForceLayoutAndText();
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }

                if (Screen.width != requestedWidth || Screen.height != requestedHeight)
                {
                    throw new InvalidOperationException(
                        $"Game view is {Screen.width}x{Screen.height}; expected {requestedWidth}x{requestedHeight}.");
                }

                if (!StageLaunchContextStore.TryGetCurrent(out var currentStageId) ||
                    !currentStageId.Equals(stageId))
                {
                    throw new InvalidOperationException(
                        $"Production runtime Stage identity is '{currentStageId.Value}', expected '{stageId.Value}'.");
                }

                var installer = FindExactlyOne<GameplayUiFlowInstaller>("GameplayUiFlowInstaller");
                if (installer.HudView == null ||
                    installer.HudView.StageInfoViewModel == null ||
                    !string.Equals(
                        installer.HudView.StageInfoViewModel.StageName,
                        scenario.StageName,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Production StageInfoViewModel did not resolve '{scenario.StageName}'.");
                }

                var climate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ClimateFontPath);
                if (climate == null)
                {
                    throw new InvalidOperationException($"Climate font missing at {ClimateFontPath}.");
                }

                Time.timeScale = 0f;
                ForceLayoutAndText();
                yield return null;
                yield return new WaitForEndOfFrame();

                var stageNameLabel = ReadPrivateField<TMP_Text>(
                    installer.HudView,
                    "_stageNameLabel");
                var stageEvidence = ValidateTextTarget(
                    "StageName",
                    stageNameLabel,
                    scenario.StageName,
                    scenario.Locale,
                    climate,
                    localizedTarget: true,
                    requestedWidth,
                    requestedHeight);
                var authoredRoot = ReadPrivateField<GameObject>(installer.HudView, "_root");
                if (authoredRoot == null ||
                    !Contains(
                        ScreenBounds(authoredRoot.GetComponent<RectTransform>()),
                        stageEvidence.ScreenBounds,
                        1f))
                {
                    throw new InvalidOperationException(
                        "Ward Stage name escapes the production HUD bounds.");
                }

                canonicalFrame = ScreenCapture.CaptureScreenshotAsTexture();
                if (canonicalFrame == null ||
                    canonicalFrame.width != requestedWidth ||
                    canonicalFrame.height != requestedHeight ||
                    !HasPixelVariation(canonicalFrame))
                {
                    throw new InvalidOperationException(
                        "Ward canonical frame is blank or has the wrong resolution.");
                }

                var hudBinding = installer.HudView.GetComponent<GameplayHudLocalizationBinding>();
                var pauseButton = hudBinding?.PauseText?.GetComponentInParent<Button>(includeInactive: true);
                var cropBounds = stageEvidence.ScreenBounds;
                if (pauseButton != null)
                {
                    cropBounds = Union(
                        cropBounds,
                        ScreenBounds(pauseButton.GetComponent<RectTransform>()));
                }

                var hudFile = $"M3_StageHUD_{WardStageIdValue}_{scenario.Locale}.png";
                var hudPath = Path.Combine(outputDirectory, hudFile);
                WriteCrop(canonicalFrame, cropBounds, 72, hudPath);

                var delta = -1L;
                var proof = CapturePixelProof(
                    canonicalFrame,
                    stageNameLabel,
                    stageEvidence.ScreenBounds,
                    value => delta = value);
                while (proof.MoveNext())
                {
                    yield return proof.Current;
                }

                if (delta <= PixelDeltaThreshold)
                {
                    throw new InvalidOperationException(
                        $"Ward StageName pixel delta {delta} did not exceed {PixelDeltaThreshold}.");
                }

                stageEvidence.PixelDelta = delta;
                onCaptured(new StageHudCapture(
                    WardStageIdValue,
                    scenario.Locale,
                    hudFile,
                    FileLength(hudPath),
                    FileSha256(hudPath),
                    stageEvidence));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                if (canonicalFrame != null)
                {
                    Object.DestroyImmediate(canonicalFrame);
                }
            }
        }

        private static void PrepareFreshRuntime(StageId stageId, string locale)
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            PlayerPrefs.SetString(LocalePreferenceKey, locale);

            var saveStore = new SaveSlotStore(
                EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            var activeSlot = new ActiveSlotProvider(
                EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            saveStore.ClearAll();
            activeSlot.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = stageId,
                CurrentLevelGroupId = "level-01",
                RemainingChances = ExpectedRemainingChances,
                LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
            });
            activeSlot.SetActiveSlot(1);
            StageLaunchContextStore.SetCurrent(stageId);
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateCampaignTempSlot(
                    stageId,
                    ExpectedRemainingChances));
            PlayerPrefs.Save();
        }

        private static Rect ValidateChanceFixture(ChancePanelView chanceView)
        {
            var model = chanceView.ViewModel;
            if (model == null ||
                !model.HasChances ||
                model.RemainingChances != ExpectedRemainingChances ||
                model.MaxChances != ExpectedMaxChances)
            {
                throw new InvalidOperationException(
                    "Production chance presenter did not expose the canonical 2/3 fixture.");
            }

            if (!chanceView.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Chance panel is not visible with canonical state.");
            }

            var slots = chanceView.SlotViews;
            if (slots.Count != ExpectedMaxChances || model.Slots.Count != ExpectedMaxChances)
            {
                throw new InvalidOperationException(
                    $"Chance slot count is {slots.Count}/{model.Slots.Count}; expected {ExpectedMaxChances}.");
            }

            var filledCount = 0;
            var emptyCount = 0;
            var visibleBounds = new List<Rect>();
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                var filled = ReadPrivateField<Image>(slot, "_filledIcon");
                var empty = ReadPrivateField<Image>(slot, "_emptyIcon");
                var expectedFilled = model.Slots[index].IsFilled;
                var visible = expectedFilled ? filled : empty;
                var hidden = expectedFilled ? empty : filled;
                if (!slot.gameObject.activeInHierarchy ||
                    !visible.gameObject.activeInHierarchy ||
                    !visible.isActiveAndEnabled ||
                    visible.canvasRenderer.cull ||
                    visible.color.a * visible.canvasRenderer.GetAlpha() <= 0.001f ||
                    hidden.gameObject.activeInHierarchy)
                {
                    throw new InvalidOperationException(
                        $"Chance slot {index} does not rasterize the expected {(expectedFilled ? "filled" : "empty")} state.");
                }

                var bounds = ScreenBounds(visible.rectTransform);
                if (!HasPositiveViewportIntersection(bounds, Screen.width, Screen.height))
                {
                    throw new InvalidOperationException(
                        $"Chance slot {index} does not intersect the viewport.");
                }

                visibleBounds.Add(bounds);
                if (expectedFilled)
                {
                    filledCount++;
                }
                else
                {
                    emptyCount++;
                }
            }

            if (filledCount != ExpectedRemainingChances ||
                emptyCount != ExpectedMaxChances - ExpectedRemainingChances)
            {
                throw new InvalidOperationException(
                    $"Chance slot graphic state is {filledCount} filled/{emptyCount} empty; expected 2/1.");
            }

            return Union(visibleBounds.ToArray());
        }

        private static TargetEvidence ValidateTextTarget(
            string name,
            TMP_Text target,
            string expectedText,
            string locale,
            TMP_FontAsset climate,
            bool localizedTarget,
            int width,
            int height)
        {
            if (target == null)
            {
                throw new InvalidOperationException($"{name} TMP target is null.");
            }

            target.ForceMeshUpdate(ignoreActiveState: false, forceTextReparsing: true);
            if (!target.gameObject.activeSelf ||
                !target.gameObject.activeInHierarchy ||
                !target.enabled ||
                !target.isActiveAndEnabled ||
                target.canvasRenderer.cull)
            {
                throw new InvalidOperationException($"{name} TMP is not raster-visible.");
            }

            if (!string.Equals(target.text, expectedText, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{name} expected '{expectedText}', actual '{target.text}'.");
            }

            if (target.font == null || target.fontSharedMaterial == null)
            {
                throw new InvalidOperationException($"{name} font/material identity is missing.");
            }

            if (localizedTarget &&
                string.Equals(locale, "ko-KR", StringComparison.Ordinal) &&
                !ReferenceEquals(target.font, climate))
            {
                throw new InvalidOperationException(
                    $"{name} ko-KR target did not resolve the Climate font.");
            }

            var inheritedAlpha = target.color.a * target.canvasRenderer.GetAlpha();
            for (var current = target.transform; current != null; current = current.parent)
            {
                var group = current.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    inheritedAlpha *= group.alpha;
                }
            }

            if (inheritedAlpha <= 0.001f)
            {
                throw new InvalidOperationException($"{name} inherited alpha is zero.");
            }

            var missing = target.text
                .Where(character => !char.IsControl(character) && !char.IsWhiteSpace(character))
                .Where(character => !target.font.HasCharacter(
                    character,
                    searchFallbacks: false,
                    tryAddCharacter: false))
                .Distinct()
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{name} missing glyphs: {string.Join(",", missing.Select(value => $"U+{(int)value:X4}"))}.");
            }

            var fallbackCount = 0;
            var visibleCharacters = 0;
            var vertexCount = 0;
            for (var i = 0; i < target.textInfo.characterCount; i++)
            {
                var character = target.textInfo.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                visibleCharacters++;
                vertexCount += 4;
                if (!ReferenceEquals(character.fontAsset, target.font))
                {
                    fallbackCount++;
                }
            }

            var expectedVisibleCharacters = expectedText.Count(character =>
                !char.IsControl(character) && !char.IsWhiteSpace(character));
            if (visibleCharacters != expectedVisibleCharacters ||
                vertexCount != expectedVisibleCharacters * 4 ||
                fallbackCount != 0)
            {
                throw new InvalidOperationException(
                    $"{name} mesh/fallback invalid: chars={visibleCharacters}/{expectedVisibleCharacters}, " +
                    $"vertices={vertexCount}/{expectedVisibleCharacters * 4}, fallback={fallbackCount}.");
            }

            if (target.isTextOverflowing)
            {
                throw new InvalidOperationException($"{name} TMP reports overflow.");
            }

            var bounds = ScreenBounds(target.rectTransform);
            if (!HasPositiveViewportIntersection(bounds, width, height))
            {
                throw new InvalidOperationException(
                    $"{name} screen bounds {FormatRect(bounds)} do not intersect the viewport.");
            }

            ReadAssetIdentity(target.font, out var fontGuid, out var fontLocalId);
            ReadAssetIdentity(
                target.fontSharedMaterial,
                out var materialGuid,
                out var materialLocalId);
            if (string.IsNullOrWhiteSpace(fontGuid) ||
                fontLocalId == 0 ||
                string.IsNullOrWhiteSpace(materialGuid) ||
                materialLocalId == 0)
            {
                throw new InvalidOperationException(
                    $"{name} font/material GUID/localID identity is incomplete.");
            }

            return new TargetEvidence(
                TransformPath(target.transform),
                target.text,
                target.font.name,
                fontGuid,
                fontLocalId,
                target.fontSharedMaterial.name,
                materialGuid,
                materialLocalId,
                bounds,
                inheritedAlpha,
                visibleCharacters,
                vertexCount,
                fallbackCount);
        }

        private static void ValidateHudLayout(
            HUDRootView hudView,
            Rect stageName,
            RectTransform pauseButton,
            Rect pauseText,
            Rect chanceHeader,
            Rect chanceSlots)
        {
            var authoredRoot = ReadPrivateField<GameObject>(hudView, "_root");
            if (authoredRoot == null ||
                !Contains(ScreenBounds(authoredRoot.GetComponent<RectTransform>()), stageName, 1f))
            {
                throw new InvalidOperationException(
                    "Stage name escapes the production HUD bounds.");
            }

            var buttonBounds = ScreenBounds(pauseButton);
            if (!Contains(buttonBounds, pauseText, 1f))
            {
                throw new InvalidOperationException(
                    "Pause localized text escapes the production Button bounds.");
            }

            if (chanceHeader.Overlaps(chanceSlots))
            {
                throw new InvalidOperationException(
                    "Chance localized header overlaps the chance slot count graphics.");
            }
        }

        private static KeycapEvidence ValidateWorldGuideLayoutAndKeycaps(
            IReadOnlyDictionary<WorldGuideInstructionKind, IWorldGuideLocalizationTarget> guides,
            IReadOnlyDictionary<string, TargetEvidence> targets,
            int width,
            int height)
        {
            var movement = guides[WorldGuideInstructionKind.Movement];
            var push = guides[WorldGuideInstructionKind.Push];
            var flip = guides[WorldGuideInstructionKind.Flip];
            var movementRoot = movement.WasdDisplayRoot != null &&
                               movement.WasdDisplayRoot.activeInHierarchy
                ? movement.WasdDisplayRoot
                : movement.ArrowDisplayRoot;
            if (movementRoot == null || !movementRoot.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Movement guide has no active WASD/Arrow keycap root.");
            }

            var movementValue = ReferenceEquals(movementRoot, movement.WasdDisplayRoot)
                ? "WASD"
                : "Arrow Keys";
            var movementBounds = ActiveTextUnion(movementRoot, width, height, "Movement keycap");
            var pushBounds = ValidateActionKeycap(push, width, height, "Push");
            var flipBounds = ValidateActionKeycap(flip, width, height, "Flip");
            if (movementBounds.Overlaps(targets["Movement"].ScreenBounds) ||
                pushBounds.Overlaps(targets["Push"].ScreenBounds) ||
                flipBounds.Overlaps(targets["Flip"].ScreenBounds))
            {
                throw new InvalidOperationException(
                    "World Guide keycap overlaps localized action text.");
            }

            var guideBounds = new[]
            {
                Union(movementBounds, targets["Movement"].ScreenBounds),
                Union(pushBounds, targets["Push"].ScreenBounds),
                Union(flipBounds, targets["Flip"].ScreenBounds),
            };
            for (var left = 0; left < guideBounds.Length; left++)
            {
                for (var right = left + 1; right < guideBounds.Length; right++)
                {
                    if (guideBounds[left].Overlaps(guideBounds[right]))
                    {
                        throw new InvalidOperationException(
                            $"World Guide {left} overlaps guide {right}.");
                    }
                }
            }

            return new KeycapEvidence(
                movementValue,
                movementBounds,
                push.ActionKeyLabel.text,
                pushBounds,
                flip.ActionKeyLabel.text,
                flipBounds);
        }

        private static Rect ValidateActionKeycap(
            IWorldGuideLocalizationTarget guide,
            int width,
            int height,
            string label)
        {
            if (guide.ActionKeyLabel == null ||
                string.IsNullOrWhiteSpace(guide.ActionKeyLabel.text))
            {
                throw new InvalidOperationException($"{label} keycap is blank.");
            }

            ValidateTextTarget(
                $"{label}Keycap",
                guide.ActionKeyLabel,
                guide.ActionKeyLabel.text,
                "keycap",
                climate: null,
                localizedTarget: false,
                width,
                height);
            return ScreenBounds(guide.ActionKeyLabel.rectTransform);
        }

        private static Rect ActiveTextUnion(
            GameObject root,
            int width,
            int height,
            string label)
        {
            var texts = root.GetComponentsInChildren<TMP_Text>(includeInactive: false)
                .Where(text => text != null && text.isActiveAndEnabled)
                .ToArray();
            if (texts.Length == 0)
            {
                throw new InvalidOperationException($"{label} has no visible TMP objects.");
            }

            var bounds = new List<Rect>();
            foreach (var text in texts)
            {
                ValidateTextTarget(
                    label,
                    text,
                    text.text,
                    "keycap",
                    climate: null,
                    localizedTarget: false,
                    width,
                    height);
                bounds.Add(ScreenBounds(text.rectTransform));
            }

            return Union(bounds.ToArray());
        }

        private static void ValidateRequiredGraphics(
            GameObject pauseButton,
            GameObject chanceRoot,
            IEnumerable<IWorldGuideLocalizationTarget> guides)
        {
            if (!HasVisibleNonTextGraphic(pauseButton))
            {
                throw new InvalidOperationException(
                    "Pause Button required graphic is missing or hidden.");
            }

            if (!HasVisibleNonTextGraphic(chanceRoot))
            {
                throw new InvalidOperationException(
                    "Chance panel required graphic is missing or hidden.");
            }

            foreach (var guide in guides)
            {
                var component = guide as Component;
                if (component == null ||
                    !component.gameObject.activeInHierarchy ||
                    component.GetComponentsInChildren<CanvasRenderer>(includeInactive: false).Length == 0)
                {
                    throw new InvalidOperationException(
                        $"{guide.InstructionKind} guide has no visible authored graphic/render surface.");
                }
            }
        }

        private static bool HasVisibleNonTextGraphic(GameObject root)
        {
            return root != null &&
                   root.GetComponentsInChildren<Graphic>(includeInactive: false)
                       .Any(graphic =>
                           graphic != null &&
                           graphic is not TMP_Text &&
                           graphic.isActiveAndEnabled &&
                           !graphic.canvasRenderer.cull &&
                           graphic.color.a * graphic.canvasRenderer.GetAlpha() > 0.001f);
        }

        private static IEnumerator CapturePixelProof(
            Texture2D visibleFrame,
            TMP_Text target,
            Rect targetBounds,
            Action<long> onCaptured)
        {
            var wasEnabled = target.enabled;
            Texture2D comparison = null;
            try
            {
                target.enabled = false;
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return new WaitForEndOfFrame();
                comparison = ScreenCapture.CaptureScreenshotAsTexture();
                if (comparison == null ||
                    comparison.width != visibleFrame.width ||
                    comparison.height != visibleFrame.height)
                {
                    throw new InvalidOperationException(
                        $"{target.name} comparison frame is invalid.");
                }

                onCaptured(CountPixelDelta(
                    visibleFrame,
                    comparison,
                    ToPixelRect(targetBounds, visibleFrame.width, visibleFrame.height, 4)));
            }
            finally
            {
                target.enabled = wasEnabled;
                Canvas.ForceUpdateCanvases();
                if (comparison != null)
                {
                    Object.DestroyImmediate(comparison);
                }
            }

            yield return null;
            yield return new WaitForEndOfFrame();
        }

        private static long CountPixelDelta(
            Texture2D visible,
            Texture2D comparison,
            RectInt crop)
        {
            var left = ReadCropPixels(visible, crop);
            var right = ReadCropPixels(comparison, crop);
            long count = 0;
            for (var i = 0; i < left.Length; i++)
            {
                if (Math.Abs(left[i].r - right[i].r) > 2 ||
                    Math.Abs(left[i].g - right[i].g) > 2 ||
                    Math.Abs(left[i].b - right[i].b) > 2 ||
                    Math.Abs(left[i].a - right[i].a) > 2)
                {
                    count++;
                }
            }

            return count;
        }

        private static string ComputeNonTextGraphicIdentity(
            GameObject hudRoot,
            IEnumerable<GameObject> guideRoots)
        {
            var entries = new List<string>();
            AppendGraphicIdentity(entries, hudRoot, "HUD");
            foreach (var root in guideRoots.OrderBy(value => value.name, StringComparer.Ordinal))
            {
                AppendGraphicIdentity(entries, root, "Guide");
            }

            entries.Sort(StringComparer.Ordinal);
            return ComputeSha256(Encoding.UTF8.GetBytes(string.Join("\n", entries)));
        }

        private static void AppendGraphicIdentity(
            ICollection<string> entries,
            GameObject root,
            string scope)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(includeInactive: true))
            {
                if (graphic == null || graphic is TMP_Text)
                {
                    continue;
                }

                var asset = graphic.material;
                var spriteName = string.Empty;
                var spriteGuid = string.Empty;
                long spriteLocalId = 0;
                if (graphic is Image image && image.sprite != null)
                {
                    spriteName = image.sprite.name;
                    ReadAssetIdentity(image.sprite, out spriteGuid, out spriteLocalId);
                }

                ReadAssetIdentity(asset, out var materialGuid, out var materialLocalId);
                entries.Add(
                    $"{scope}|{TransformPath(graphic.transform)}|" +
                    $"{graphic.GetType().FullName}|{graphic.gameObject.activeSelf}|" +
                    $"{graphic.enabled}|{spriteName}|{spriteGuid}|{spriteLocalId}|" +
                    $"{materialGuid}|{materialLocalId}");
            }
        }

        private static void ValidateCrossLocaleParity(
            IReadOnlyList<LocaleCapture> captures,
            ICollection<string> errors)
        {
            if (captures.Count != Scenarios.Length)
            {
                errors.Add(
                    $"Cross-locale capture set is incomplete: {captures.Count}/{Scenarios.Length}.");
                return;
            }

            var english = captures.Single(value => value.Locale == "en-US");
            var korean = captures.Single(value => value.Locale == "ko-KR");
            if (!string.Equals(
                    english.Keycaps.Movement,
                    korean.Keycaps.Movement,
                    StringComparison.Ordinal) ||
                !string.Equals(english.Keycaps.Push, korean.Keycaps.Push, StringComparison.Ordinal) ||
                !string.Equals(english.Keycaps.Flip, korean.Keycaps.Flip, StringComparison.Ordinal))
            {
                errors.Add("World Guide keycap values differ across locales.");
            }

            if (!string.Equals(
                    english.GraphicIdentityHash,
                    korean.GraphicIdentityHash,
                    StringComparison.Ordinal))
            {
                errors.Add("Non-text graphic identity differs across locales.");
            }

            if (!string.Equals(
                    english.SemanticFixtureHash,
                    korean.SemanticFixtureHash,
                    StringComparison.Ordinal))
            {
                errors.Add("Semantic/chance/binding fixture differs across locales.");
            }

        }

        private static string WriteManifest(
            string outputDirectory,
            string expectedHead,
            string expectedTree,
            int width,
            int height,
            IReadOnlyList<LocaleCapture> captures,
            IReadOnlyList<StageHudCapture> wardCaptures,
            IReadOnlyCollection<string> errors)
        {
            var builder = new StringBuilder();
            Append(builder, "schema_version", "1");
            Append(builder, "git_head", expectedHead);
            Append(builder, "git_tree", expectedTree);
            Append(builder, "worktree_path", Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
            Append(builder, "scene", ScenePath);
            Append(builder, "stage_id", $"{LabStageIdValue},{WardStageIdValue}");
            Append(builder, "resolution", $"{width}x{height}");
            Append(
                builder,
                "capture_count",
                (captures.Count * 2 + wardCaptures.Count).ToString(CultureInfo.InvariantCulture));
            Append(
                builder,
                "locale_runtime_count",
                (captures.Count + wardCaptures.Count).ToString(CultureInfo.InvariantCulture));
            Append(builder, "pixel_delta_threshold", PixelDeltaThreshold.ToString(CultureInfo.InvariantCulture));
            Append(builder, "errors", errors.Count.ToString(CultureInfo.InvariantCulture));
            Append(
                builder,
                "overall_result",
                errors.Count == 0 && captures.Count == 2 && wardCaptures.Count == 2
                    ? "PASS"
                    : "FAIL");
            for (var index = 0; index < errors.Count; index++)
            {
                Append(builder, $"error_{index:000}", Sanitize(errors.ElementAt(index)));
            }

            foreach (var capture in captures.OrderBy(value => value.Locale, StringComparer.Ordinal))
            {
                builder.AppendLine();
                builder.Append('[').Append(capture.Locale).AppendLine("]");
                Append(builder, "hud_file", capture.HudFile);
                Append(builder, "hud_bytes", capture.HudBytes.ToString(CultureInfo.InvariantCulture));
                Append(builder, "hud_sha256", capture.HudSha256);
                Append(builder, "guide_file", capture.GuideFile);
                Append(builder, "guide_bytes", capture.GuideBytes.ToString(CultureInfo.InvariantCulture));
                Append(builder, "guide_sha256", capture.GuideSha256);
                Append(
                    builder,
                    "chance_count",
                    $"{ExpectedRemainingChances}/{ExpectedMaxChances}");
                Append(builder, "movement_keycap", capture.Keycaps.Movement);
                Append(builder, "push_keycap", capture.Keycaps.Push);
                Append(builder, "flip_keycap", capture.Keycaps.Flip);
                Append(builder, "semantic_fixture_hash", capture.SemanticFixtureHash);
                Append(builder, "non_text_graphic_hash", capture.GraphicIdentityHash);
                Append(builder, "missing_glyph_count", "0");
                Append(builder, "fallback_count", "0");
                Append(builder, "layout", "PASS");
                Append(builder, "graphics", "PASS");
                Append(builder, "mixed_locale", "0");
                Append(builder, "stale_locale", "0");

                foreach (var pair in capture.Targets.OrderBy(value => value.Key, StringComparer.Ordinal))
                {
                    var prefix = "target_" + pair.Key.ToLowerInvariant() + "_";
                    var target = pair.Value;
                    Append(builder, prefix + "path", target.ObjectPath);
                    Append(builder, prefix + "text", target.Text);
                    Append(builder, prefix + "font", target.FontName);
                    Append(builder, prefix + "font_guid", target.FontGuid);
                    Append(builder, prefix + "font_local_id", target.FontLocalId.ToString(CultureInfo.InvariantCulture));
                    Append(builder, prefix + "material", target.MaterialName);
                    Append(builder, prefix + "material_guid", target.MaterialGuid);
                    Append(builder, prefix + "material_local_id", target.MaterialLocalId.ToString(CultureInfo.InvariantCulture));
                    Append(builder, prefix + "bounds", FormatRect(target.ScreenBounds));
                    Append(builder, prefix + "alpha", target.InheritedAlpha.ToString("R", CultureInfo.InvariantCulture));
                    Append(builder, prefix + "mesh_characters", target.VisibleCharacters.ToString(CultureInfo.InvariantCulture));
                    Append(builder, prefix + "mesh_vertices", target.VertexCount.ToString(CultureInfo.InvariantCulture));
                    Append(builder, prefix + "fallback", target.FallbackCount.ToString(CultureInfo.InvariantCulture));
                    Append(builder, prefix + "pixel_delta", target.PixelDelta.ToString(CultureInfo.InvariantCulture));
                }
            }

            foreach (var capture in wardCaptures.OrderBy(value => value.Locale, StringComparer.Ordinal))
            {
                builder.AppendLine();
                builder.Append('[')
                    .Append(capture.StageId)
                    .Append('/')
                    .Append(capture.Locale)
                    .AppendLine("]");
                Append(builder, "stage_id", capture.StageId);
                Append(builder, "hud_file", capture.HudFile);
                Append(builder, "hud_bytes", capture.HudBytes.ToString(CultureInfo.InvariantCulture));
                Append(builder, "hud_sha256", capture.HudSha256);
                Append(builder, "missing_glyph_count", "0");
                Append(builder, "fallback_count", "0");
                Append(builder, "layout", "PASS");
                Append(builder, "mixed_locale", "0");
                Append(builder, "stale_locale", "0");
                Append(builder, "stage_identity", "PASS");

                const string prefix = "target_stage_name_";
                var target = capture.StageName;
                Append(builder, prefix + "path", target.ObjectPath);
                Append(builder, prefix + "text", target.Text);
                Append(builder, prefix + "font", target.FontName);
                Append(builder, prefix + "font_guid", target.FontGuid);
                Append(builder, prefix + "font_local_id", target.FontLocalId.ToString(CultureInfo.InvariantCulture));
                Append(builder, prefix + "material", target.MaterialName);
                Append(builder, prefix + "material_guid", target.MaterialGuid);
                Append(builder, prefix + "material_local_id", target.MaterialLocalId.ToString(CultureInfo.InvariantCulture));
                Append(builder, prefix + "bounds", FormatRect(target.ScreenBounds));
                Append(builder, prefix + "alpha", target.InheritedAlpha.ToString("R", CultureInfo.InvariantCulture));
                Append(builder, prefix + "mesh_characters", target.VisibleCharacters.ToString(CultureInfo.InvariantCulture));
                Append(builder, prefix + "mesh_vertices", target.VertexCount.ToString(CultureInfo.InvariantCulture));
                Append(builder, prefix + "fallback", target.FallbackCount.ToString(CultureInfo.InvariantCulture));
                Append(builder, prefix + "pixel_delta", target.PixelDelta.ToString(CultureInfo.InvariantCulture));
            }

            var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
            File.WriteAllText(manifestPath, builder.ToString(), new UTF8Encoding(false));
            return manifestPath;
        }

        private static IEnumerator CleanupSceneRuntime()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload == null)
                {
                    continue;
                }

                while (!unload.isDone)
                {
                    yield return null;
                }
            }

            foreach (var root in Object.FindObjectsByType<GlobalAudioFlowRoot>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                Object.Destroy(root.gameObject);
            }

            foreach (var root in Object.FindObjectsByType<AudioRuntimeRoot>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                Object.Destroy(root.gameObject);
            }

            yield return null;
        }

        private static void ForceLayoutAndText()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in Object.FindObjectsByType<Graphic>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                graphic.SetAllDirty();
            }

            foreach (var text in Object.FindObjectsByType<TMP_Text>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (text.gameObject.scene.IsValid())
                {
                    text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                }
            }

            Canvas.ForceUpdateCanvases();
        }

        private static T FindExactlyOne<T>(string label) where T : Object
        {
            var values = Object.FindObjectsByType<T>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (values.Length != 1)
            {
                throw new InvalidOperationException(
                    $"{label} count is {values.Length}; expected exactly one.");
            }

            return values[0];
        }

        private static void RequireGuide(
            IReadOnlyDictionary<WorldGuideInstructionKind, IWorldGuideLocalizationTarget> guides,
            WorldGuideInstructionKind kind)
        {
            if (!guides.TryGetValue(kind, out var guide) ||
                guide is not Component component ||
                !component.gameObject.activeSelf ||
                !component.gameObject.activeInHierarchy ||
                guide.ActionTextLabel == null)
            {
                throw new InvalidOperationException(
                    $"{kind} World Guide is missing or not active in Stage 0-1.");
            }
        }

        private static T ReadPrivateField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            var value = field?.GetValue(target) as T;
            return value ?? throw new InvalidOperationException(
                $"{target.GetType().Name}.{fieldName} is missing.");
        }

        private static Rect ScreenBounds(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                throw new InvalidOperationException("RectTransform is null.");
            }

            var canvas = rectTransform.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera != null ? canvas.worldCamera : Camera.main
                : null;
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            for (var index = 0; index < corners.Length; index++)
            {
                if (camera != null && camera.WorldToScreenPoint(corners[index]).z <= 0f)
                {
                    throw new InvalidOperationException(
                        $"{rectTransform.name} is behind the capture camera.");
                }

                var screen = RectTransformUtility.WorldToScreenPoint(camera, corners[index]);
                minX = Mathf.Min(minX, screen.x);
                minY = Mathf.Min(minY, screen.y);
                maxX = Mathf.Max(maxX, screen.x);
                maxY = Mathf.Max(maxY, screen.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static bool HasPositiveViewportIntersection(Rect bounds, int width, int height)
        {
            return bounds.width > 0f &&
                   bounds.height > 0f &&
                   bounds.xMax > 0f &&
                   bounds.yMax > 0f &&
                   bounds.xMin < width &&
                   bounds.yMin < height;
        }

        private static bool Contains(Rect outer, Rect inner, float tolerance)
        {
            return inner.xMin >= outer.xMin - tolerance &&
                   inner.yMin >= outer.yMin - tolerance &&
                   inner.xMax <= outer.xMax + tolerance &&
                   inner.yMax <= outer.yMax + tolerance;
        }

        private static Rect Union(params Rect[] values)
        {
            if (values == null || values.Length == 0)
            {
                throw new ArgumentException("At least one Rect is required.", nameof(values));
            }

            var minX = values.Min(value => value.xMin);
            var minY = values.Min(value => value.yMin);
            var maxX = values.Max(value => value.xMax);
            var maxY = values.Max(value => value.yMax);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static RectInt ToPixelRect(
            Rect bounds,
            int width,
            int height,
            int padding)
        {
            var xMin = Mathf.Clamp(Mathf.FloorToInt(bounds.xMin) - padding, 0, width - 1);
            var yMin = Mathf.Clamp(Mathf.FloorToInt(bounds.yMin) - padding, 0, height - 1);
            var xMax = Mathf.Clamp(Mathf.CeilToInt(bounds.xMax) + padding, xMin + 1, width);
            var yMax = Mathf.Clamp(Mathf.CeilToInt(bounds.yMax) + padding, yMin + 1, height);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static void WriteCrop(
            Texture2D source,
            Rect bounds,
            int padding,
            string path)
        {
            var crop = ToPixelRect(bounds, source.width, source.height, padding);
            var texture = new Texture2D(
                crop.width,
                crop.height,
                TextureFormat.RGBA32,
                mipChain: false);
            try
            {
                texture.SetPixels32(ReadCropPixels(source, crop));
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                var bytes = texture.EncodeToPNG();
                if (bytes == null || bytes.Length < 24)
                {
                    throw new InvalidOperationException(
                        $"Crop {Path.GetFileName(path)} did not encode as PNG.");
                }

                File.WriteAllBytes(path, bytes);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static Color32[] ReadCropPixels(Texture2D source, RectInt crop)
        {
            var sourcePixels = source.GetPixels32();
            var croppedPixels = new Color32[crop.width * crop.height];
            for (var row = 0; row < crop.height; row++)
            {
                Array.Copy(
                    sourcePixels,
                    (crop.y + row) * source.width + crop.x,
                    croppedPixels,
                    row * crop.width,
                    crop.width);
            }

            return croppedPixels;
        }

        private static bool HasPixelVariation(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            return pixels.Length > 0 && pixels.Any(pixel => !pixel.Equals(pixels[0]));
        }

        private static void ReadAssetIdentity(
            Object value,
            out string guid,
            out long localId)
        {
            if (value == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out localId))
            {
                guid = string.Empty;
                localId = 0;
            }
        }

        private static string TransformPath(Transform transform)
        {
            var parts = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                parts.Push($"{current.name}[{current.GetSiblingIndex()}]");
            }

            return string.Join("/", parts);
        }

        private static long FileLength(string path)
        {
            return new FileInfo(path).Length;
        }

        private static string FileSha256(string path)
        {
            return ComputeSha256(File.ReadAllBytes(path));
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (var value in hash)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            builder.Append(key).Append('=').AppendLine(value ?? string.Empty);
        }

        private static string FormatRect(Rect value)
        {
            return string.Join(
                ",",
                value.xMin.ToString("R", CultureInfo.InvariantCulture),
                value.yMin.ToString("R", CultureInfo.InvariantCulture),
                value.width.ToString("R", CultureInfo.InvariantCulture),
                value.height.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string Sanitize(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static string ReadRequiredArgument(string[] args, string name)
        {
            var value = ReadArgument(args, name);
            return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException($"{name} is required.")
                : value;
        }

        private static int ReadPositiveInt(string[] args, string name, int fallback)
        {
            var value = ReadArgument(args, name);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                   parsed > 0
                ? parsed
                : fallback;
        }

        private static string ReadArgument(string[] args, string name)
        {
            for (var index = 0; index + 1 < args.Length; index++)
            {
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                {
                    return args[index + 1];
                }
            }

            return string.Empty;
        }

        private sealed class LocaleScenario
        {
            public LocaleScenario(
                string locale,
                string stageName,
                string pause,
                string chance,
                string movement,
                string push,
                string flip)
            {
                Locale = locale;
                StageName = stageName;
                Pause = pause;
                Chance = chance;
                Movement = movement;
                Push = push;
                Flip = flip;
            }

            public string Locale { get; }
            public string StageName { get; }
            public string Pause { get; }
            public string Chance { get; }
            public string Movement { get; }
            public string Push { get; }
            public string Flip { get; }
        }

        private sealed class StageHudScenario
        {
            public StageHudScenario(string locale, string stageName)
            {
                Locale = locale;
                StageName = stageName;
            }

            public string Locale { get; }
            public string StageName { get; }
        }

        private sealed class TargetEvidence
        {
            public TargetEvidence(
                string objectPath,
                string text,
                string fontName,
                string fontGuid,
                long fontLocalId,
                string materialName,
                string materialGuid,
                long materialLocalId,
                Rect screenBounds,
                float inheritedAlpha,
                int visibleCharacters,
                int vertexCount,
                int fallbackCount)
            {
                ObjectPath = objectPath;
                Text = text;
                FontName = fontName;
                FontGuid = fontGuid;
                FontLocalId = fontLocalId;
                MaterialName = materialName;
                MaterialGuid = materialGuid;
                MaterialLocalId = materialLocalId;
                ScreenBounds = screenBounds;
                InheritedAlpha = inheritedAlpha;
                VisibleCharacters = visibleCharacters;
                VertexCount = vertexCount;
                FallbackCount = fallbackCount;
            }

            public string ObjectPath { get; }
            public string Text { get; }
            public string FontName { get; }
            public string FontGuid { get; }
            public long FontLocalId { get; }
            public string MaterialName { get; }
            public string MaterialGuid { get; }
            public long MaterialLocalId { get; }
            public Rect ScreenBounds { get; }
            public float InheritedAlpha { get; }
            public int VisibleCharacters { get; }
            public int VertexCount { get; }
            public int FallbackCount { get; }
            public long PixelDelta { get; set; } = -1;
        }

        private sealed class KeycapEvidence
        {
            public KeycapEvidence(
                string movement,
                Rect movementBounds,
                string push,
                Rect pushBounds,
                string flip,
                Rect flipBounds)
            {
                Movement = movement;
                MovementBounds = movementBounds;
                Push = push;
                PushBounds = pushBounds;
                Flip = flip;
                FlipBounds = flipBounds;
            }

            public string Movement { get; }
            public Rect MovementBounds { get; }
            public string Push { get; }
            public Rect PushBounds { get; }
            public string Flip { get; }
            public Rect FlipBounds { get; }
        }

        private sealed class LocaleCapture
        {
            public LocaleCapture(
                string locale,
                string hudFile,
                long hudBytes,
                string hudSha256,
                string guideFile,
                long guideBytes,
                string guideSha256,
                IReadOnlyDictionary<string, TargetEvidence> targets,
                KeycapEvidence keycaps,
                string graphicIdentityHash,
                string semanticFixtureHash)
            {
                Locale = locale;
                HudFile = hudFile;
                HudBytes = hudBytes;
                HudSha256 = hudSha256;
                GuideFile = guideFile;
                GuideBytes = guideBytes;
                GuideSha256 = guideSha256;
                Targets = targets;
                Keycaps = keycaps;
                GraphicIdentityHash = graphicIdentityHash;
                SemanticFixtureHash = semanticFixtureHash;
            }

            public string Locale { get; }
            public string HudFile { get; }
            public long HudBytes { get; }
            public string HudSha256 { get; }
            public string GuideFile { get; }
            public long GuideBytes { get; }
            public string GuideSha256 { get; }
            public IReadOnlyDictionary<string, TargetEvidence> Targets { get; }
            public KeycapEvidence Keycaps { get; }
            public string GraphicIdentityHash { get; }
            public string SemanticFixtureHash { get; }
        }

        private sealed class StageHudCapture
        {
            public StageHudCapture(
                string stageId,
                string locale,
                string hudFile,
                long hudBytes,
                string hudSha256,
                TargetEvidence stageName)
            {
                StageId = stageId;
                Locale = locale;
                HudFile = hudFile;
                HudBytes = hudBytes;
                HudSha256 = hudSha256;
                StageName = stageName;
            }

            public string StageId { get; }
            public string Locale { get; }
            public string HudFile { get; }
            public long HudBytes { get; }
            public string HudSha256 { get; }
            public TargetEvidence StageName { get; }
        }

        private readonly struct PlayerPrefsBackup
        {
            private PlayerPrefsBackup(
                string key,
                bool existed,
                string stringValue,
                int intValue,
                bool isInteger)
            {
                Key = key;
                Existed = existed;
                StringValue = stringValue;
                IntValue = intValue;
                IsInteger = isInteger;
            }

            private string Key { get; }
            private bool Existed { get; }
            private string StringValue { get; }
            private int IntValue { get; }
            private bool IsInteger { get; }

            public static PlayerPrefsBackup CaptureString(string key)
            {
                return new PlayerPrefsBackup(
                    key,
                    PlayerPrefs.HasKey(key),
                    PlayerPrefs.GetString(key, string.Empty),
                    0,
                    isInteger: false);
            }

            public static PlayerPrefsBackup CaptureInt(string key)
            {
                return new PlayerPrefsBackup(
                    key,
                    PlayerPrefs.HasKey(key),
                    string.Empty,
                    PlayerPrefs.GetInt(key, 0),
                    isInteger: true);
            }

            public void Restore()
            {
                if (Existed)
                {
                    if (IsInteger)
                    {
                        PlayerPrefs.SetInt(Key, IntValue);
                    }
                    else
                    {
                        PlayerPrefs.SetString(Key, StringValue);
                    }
                }
                else
                {
                    PlayerPrefs.DeleteKey(Key);
                }
            }
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class UiRepositoryPrefabCatalogSmokeTests
    {
        public enum TerminalIrisSourceRoute
        {
            Victory,
            ActualDefeat,
            ManualRetry,
            LevelFailedRestart,
            DemoStageRelaunch,
            GameplayEntry,
            ReturnToMainMenu,
            DeathRetry,
        }

        private const string TransitionContentCatalogPath =
            "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/SceneTransitionOverlayContentCatalog.asset";
        private const string TerminalIrisMotionProfilePath =
            "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/TerminalIrisMotionProfile.asset";
        private const string UiHoverScaleEffectPath =
            "Assets/_Features/UI/UI_Screens/Runtime/UiHoverScaleEffect.cs";

        [Test]
        public void ScreenPrefabCatalog_RepositoryAsset_AllScreenIdsHaveValidPrefab()
        {
            var catalog = UiTestPrefabAssetUtility.LoadScreenCatalog();
            AssertSharedResultTransitionStyle(catalog);
            var expectedScreens = Enum.GetValues(typeof(ScreenId))
                .Cast<ScreenId>()
                .Where(screenId => screenId != ScreenId.None && screenId != ScreenId.Gameplay)
                .ToArray();
            var failures = new List<string>();

            foreach (var screenId in expectedScreens)
            {
                try
                {
                    var prefab = ResolveScreenPrefab(catalog, screenId);
                    Assert.That(prefab, Is.Not.Null, screenId.ToString());
                    Assert.That(prefab.GetComponent<IScreenView>(), Is.Not.Null, screenId.ToString());
                    if (prefab is SettingsScreenView settings)
                    {
                        settings.ValidateAuthoredStructureOrThrow();
                        settings.AudioView.ValidateAuthoredControlsOrThrow();
                        settings.DisplayView.ValidateAuthoredControlsOrThrow();
                        settings.InputView.ValidateAuthoredControlsOrThrow();
                    }
                    else if (prefab is StageResultScreenView stageResult)
                    {
                        AssertStageResultMinimalNavigationEndpointPrefab(stageResult);
                        AssertResultTransitionPrefab(stageResult, "_continueButton");
                    }
                    else if (prefab is GameClearScreenView gameClear)
                    {
                        AssertGameClearResultOnlyPrefab(gameClear);
                        AssertResultTransitionPrefab(gameClear, "_mainButton");
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{screenId}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "Screen prefab catalog repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        public void PopupPrefabCatalog_RepositoryAsset_AllPopupIdsHaveValidPrefab()
        {
            var catalog = UiTestPrefabAssetUtility.LoadPopupCatalog();
            var expectedPopups = Enum.GetValues(typeof(PopupId))
                .Cast<PopupId>()
                .Where(popupId => popupId != PopupId.None)
                .ToArray();
            var failures = new List<string>();

            foreach (var popupId in expectedPopups)
            {
                try
                {
                    var prefab = ResolvePopupPrefab(catalog, popupId);
                    Assert.That(prefab, Is.Not.Null, popupId.ToString());
                    Assert.That(prefab.GetComponent<IPopupView>(), Is.Not.Null, popupId.ToString());
                    if (prefab is PausePopupView pausePopup)
                    {
                        AssertPausePopupButtonHasSingleHoverScaleEffect(pausePopup, "ResumeButton");
                        AssertPausePopupButtonHasSingleHoverScaleEffect(pausePopup, "SettingsButton");
                        AssertPausePopupButtonHasSingleHoverScaleEffect(pausePopup, "RetryButton");
                        AssertPausePopupButtonHasSingleHoverScaleEffect(pausePopup, "MainMenuButton");
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{popupId}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "Popup prefab catalog repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        public void ConfirmPopupPrefab_ActionButtonsHaveExplicitClickPunchPreset()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiTestPrefabAssetUtility.ConfirmPopupPrefabPath);
            Assert.That(prefab, Is.Not.Null, UiTestPrefabAssetUtility.ConfirmPopupPrefabPath);

            foreach (var buttonName in new[] { "ConfirmButton", "CancelButton" })
            {
                var button = FindChildByName(prefab.transform, buttonName);
                Assert.That(button, Is.Not.Null, buttonName);

                var effect = button.GetComponent<UiHoverScaleEffect>();
                Assert.That(effect, Is.Not.Null, buttonName);
                Assert.That(effect.Target, Is.EqualTo(button as RectTransform), buttonName);

                var serialized = new SerializedObject(effect);
                Assert.That(
                    serialized.FindProperty("_clickPunchStrength").floatValue,
                    Is.EqualTo(0.08f).Within(0.001f),
                    buttonName);
                Assert.That(
                    serialized.FindProperty("_clickPunchDurationSeconds").floatValue,
                    Is.EqualTo(0.18f).Within(0.001f),
                    buttonName);
                Assert.That(serialized.FindProperty("_clickPunchVibrato").intValue, Is.EqualTo(6), buttonName);
                Assert.That(
                    serialized.FindProperty("_clickPunchElasticity").floatValue,
                    Is.EqualTo(0.65f).Within(0.001f),
                    buttonName);
            }
        }

        [Test]
        public void UiHoverScaleEffect_RepositoryPrefabs_HaveSelectableAndCompletePunchSerialization()
        {
            var effectGuid = AssetDatabase.AssetPathToGUID(UiHoverScaleEffectPath);
            Assert.That(effectGuid, Is.Not.Empty, UiHoverScaleEffectPath);

            var scriptReference = $"m_Script: {{fileID: 11500000, guid: {effectGuid}, type: 3}}";
            var punchFields = new[]
            {
                "  _clickPunchStrength:",
                "  _clickPunchDurationSeconds:",
                "  _clickPunchVibrato:",
                "  _clickPunchElasticity:",
            };
            var totalEffectCount = 0;
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Features/UI" });

            foreach (var prefabGuid in prefabGuids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                var source = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), prefabPath));
                var authoredEffectCount = CountOccurrences(source, scriptReference);
                if (authoredEffectCount == 0)
                {
                    continue;
                }

                totalEffectCount += authoredEffectCount;
                foreach (var punchField in punchFields)
                {
                    Assert.That(
                        CountOccurrences(source, punchField),
                        Is.EqualTo(authoredEffectCount),
                        $"{prefabPath} must explicitly serialize {punchField.Trim()} for every UiHoverScaleEffect.");
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null, prefabPath);
                var effects = prefab.GetComponentsInChildren<UiHoverScaleEffect>(true);
                Assert.That(effects.Length, Is.GreaterThanOrEqualTo(authoredEffectCount), prefabPath);
                foreach (var effect in effects)
                {
                    Assert.That(
                        effect.GetComponent<Selectable>(),
                        Is.Not.Null,
                        $"{prefabPath}:{effect.transform.name} must pair UiHoverScaleEffect with a Selectable.");
                    Assert.That(
                        effect.Target,
                        Is.Not.Null,
                        $"{prefabPath}:{effect.transform.name} must resolve its hover scale target.");
                    Assert.That(
                        effect.Target.pivot,
                        Is.EqualTo(new Vector2(0.5f, 0.5f)),
                        $"{prefabPath}:{effect.transform.name} must scale from its visual center.");
                }
            }

            Assert.That(totalEffectCount, Is.GreaterThan(0), "Repository scan found no UiHoverScaleEffect components.");
        }

        [Test]
        public void SceneTransitionContentCatalog_RepositoryAsset_ContentPrefabsValidate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SceneTransitionOverlayContentCatalog>(TransitionContentCatalogPath);
            Assert.That(catalog, Is.Not.Null, TransitionContentCatalogPath);

            var contentPrefabs = new HashSet<SceneTransitionOverlayContentView>();
            foreach (var entry in catalog.Entries)
            {
                if (entry?.ContentPrefab != null)
                {
                    contentPrefabs.Add(entry.ContentPrefab);
                }
            }

            var failures = new List<string>();
            foreach (var prefab in contentPrefabs.OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal))
            {
                try
                {
                    Assert.That(prefab.CollectValidationIssues(), Is.Empty, Describe(prefab));
                    Assert.That(CountMissingScripts(prefab.gameObject), Is.EqualTo(0), Describe(prefab));
                }
                catch (Exception exception)
                {
                    failures.Add($"{Describe(prefab)}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "Scene transition content catalog repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        public void GameplayUiCanvasRootShell_RequiredLayersAndEventSystemContract()
        {
            var shellPrefab = Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
            Assert.That(shellPrefab, Is.Not.Null);

            var instance = UnityEngine.Object.Instantiate(shellPrefab);
            try
            {
                var rootView = instance.GetComponent<GameplayUiCanvasRootView>();
                Assert.That(rootView, Is.Not.Null);

                rootView.EnsureHierarchy();

                Assert.That(rootView.HudLayer, Is.Not.Null);
                Assert.That(rootView.ScreenLayer, Is.Not.Null);
                Assert.That(rootView.PopupLayer, Is.Not.Null);
                Assert.That(rootView.TransitionLayer, Is.Not.Null);
                Assert.That(instance.transform.Find("DiagnosticsLayer"), Is.Null);
                Assert.That(rootView.ScreenLayerView, Is.Not.Null);
                Assert.That(rootView.PopupLayerView, Is.Not.Null);
                Assert.That(rootView.TerminalIrisOverlayView, Is.Not.Null);
                var motionProfile = rootView.RequireTerminalIrisMotionProfile();
                Assert.That(motionProfile, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(motionProfile),
                    Is.EqualTo(TerminalIrisMotionProfilePath));
                Assert.That(rootView.TransitionLayer.GetSiblingIndex(), Is.EqualTo(3));
                Assert.That(rootView.TerminalIrisOverlayView.IsVisible, Is.False);
                Assert.That(rootView.TerminalIrisOverlayView.BlocksRaycasts, Is.False);
                var authoredMaterial = rootView.TerminalIrisOverlayView.AuthoredMaterialForTests;
                Assert.That(authoredMaterial, Is.Not.Null);
                Assert.That(authoredMaterial.shader, Is.Not.Null);
                Assert.That(authoredMaterial.shader.name, Is.EqualTo(TerminalIrisOverlayView.IrisShaderName));
                var shellPath = AssetDatabase.GetAssetPath(shellPrefab);
                var dependencies = AssetDatabase.GetDependencies(shellPath, recursive: true);
                Assert.That(
                    dependencies,
                    Does.Contain(AssetDatabase.GetAssetPath(authoredMaterial)));
                Assert.That(
                    dependencies,
                    Does.Contain(AssetDatabase.GetAssetPath(authoredMaterial.shader)));
                Assert.That(dependencies, Does.Contain(TerminalIrisMotionProfilePath));
                var irisImage =
                    rootView.TerminalIrisOverlayView.GetComponent<UnityEngine.UI.Image>();
                Assert.That(irisImage, Is.Not.Null);
                Assert.That(irisImage.sprite, Is.Null);
                Assert.That(irisImage.type, Is.EqualTo(UnityEngine.UI.Image.Type.Simple));
                Assert.That(irisImage.preserveAspect, Is.False);
                Assert.That(irisImage.color, Is.EqualTo(Color.white));
                Assert.That(irisImage.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(irisImage.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(irisImage.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(irisImage.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
                Assert.That(irisImage.rectTransform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(
                    irisImage.rectTransform.localRotation,
                    Is.EqualTo(Quaternion.identity));

                rootView.TerminalIrisOverlayView.Show();
                var runtimeMaterial = rootView.TerminalIrisOverlayView.RuntimeMaterialForTests;
                Assert.That(runtimeMaterial, Is.Not.Null);
                Assert.That(runtimeMaterial, Is.Not.SameAs(authoredMaterial));
                var authoredRadius = authoredMaterial.GetFloat("_Radius");
                runtimeMaterial.SetFloat("_Radius", authoredRadius + 0.25f);
                Assert.That(authoredMaterial.GetFloat("_Radius"), Is.EqualTo(authoredRadius));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                DestroySceneObject("EventSystem");
            }
        }

        [Test]
        public void TerminalIrisMotionProfile_RepositoryAsset_PreservesProductionDefaults()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TerminalIrisMotionProfile>(
                TerminalIrisMotionProfilePath);

            Assert.That(profile, Is.Not.Null, TerminalIrisMotionProfilePath);
            Assert.That(profile.VictoryClose, Is.Not.Null);
            Assert.That(profile.DefeatClose, Is.Not.Null);
            Assert.That(profile.RetryClose, Is.Not.Null);
            Assert.That(profile.GameplayEntryClose, Is.Not.Null);
            Assert.That(profile.DefeatReveal, Is.Not.Null);
            Assert.That(profile.StageEntryOpen, Is.Not.Null);

            var resolver = profile.CreateResolver();
            var victory = resolver.ResolveClose(TerminalTransitionKind.Victory);
            var defeat = resolver.ResolveClose(TerminalTransitionKind.Defeat);
            var retry = resolver.ResolveRetryClose(SceneTransitionIntent.ManualRetry);
            var gameplayEntry = resolver.ResolveGameplayEntrySourceClose(
                SceneTransitionIntent.GameplayEntry);
            var stageEntry = resolver.ResolveStageEntryOpen();

            AssertCloseTiming(victory, 0.20f, 0.60f, 0.28f);
            AssertCloseTiming(defeat, 0.16f, 0.45f, 0.26f);
            AssertCloseTiming(retry, 0.12f, 0.03f, 0.20f);
            AssertCloseTiming(gameplayEntry, 0.12f, 0.03f, 0.20f);
            Assert.That(victory.HoldDuration, Is.GreaterThan(retry.HoldDuration));
            Assert.That(defeat.HoldDuration, Is.GreaterThan(retry.HoldDuration));
            Assert.That(victory.HoldDuration, Is.GreaterThan(gameplayEntry.HoldDuration));
            Assert.That(defeat.HoldDuration, Is.GreaterThan(gameplayEntry.HoldDuration));
            Assert.That(retry.BlackAt, Is.LessThanOrEqualTo(0.35f));
            Assert.That(gameplayEntry.BlackAt, Is.LessThanOrEqualTo(0.35f));
            Assert.That(stageEntry.OpeningDuration, Is.EqualTo(0.34f));
            Assert.That(stageEntry.OpeningDuration, Is.LessThanOrEqualTo(0.34f));
            Assert.That(stageEntry.PreOpenHoldDuration, Is.Zero);
            Assert.That(stageEntry.FullOpenMargin, Is.EqualTo(0.012f));
            Assert.That(stageEntry.FinalClosedOvershootPixels, Is.EqualTo(3.5f));
            Assert.That(stageEntry.Edge.EdgeAntiAliasScale, Is.EqualTo(0.82f));
            Assert.That(stageEntry.Edge.MinimumAAPixels, Is.EqualTo(0.95f));
            Assert.That(stageEntry.Edge.ArtisticFeatherHalfWidthPixels, Is.EqualTo(0.5f));
            Assert.That(stageEntry.Edge.RimWidthPixels, Is.Zero);
            Assert.That(stageEntry.Edge.RimSoftnessPixels, Is.EqualTo(0.85f));
            Assert.That(stageEntry.OpeningEasing, Is.EqualTo(TerminalIrisEasing.SmoothStep));
            Assert.That(defeat.RevealPreset, Is.Not.Null);
            Assert.That(defeat.RevealPreset.Value.OpeningDuration, Is.EqualTo(0.40f));
            Assert.That(defeat.RevealPreset.Value.OpeningDuration, Is.LessThanOrEqualTo(0.40f));
            Assert.That(defeat.RevealPreset.Value.PreOpenHoldDuration, Is.Zero);
            Assert.That(victory.FocusEasing, Is.EqualTo(TerminalIrisEasing.SmoothStep));
            Assert.That(victory.CloseEasing, Is.EqualTo(TerminalIrisEasing.EaseOutSine));
            Assert.That(defeat.FocusEasing, Is.EqualTo(TerminalIrisEasing.SmoothStep));
            Assert.That(defeat.CloseEasing, Is.EqualTo(TerminalIrisEasing.EaseOutSine));
            Assert.That(retry.FocusEasing, Is.EqualTo(TerminalIrisEasing.SmoothStep));
            Assert.That(retry.CloseEasing, Is.EqualTo(TerminalIrisEasing.EaseOutSine));
            Assert.That(gameplayEntry.FocusEasing, Is.EqualTo(TerminalIrisEasing.SmoothStep));
            Assert.That(gameplayEntry.CloseEasing, Is.EqualTo(TerminalIrisEasing.EaseOutSine));
        }

        [TestCase(TerminalIrisSourceRoute.Victory, 0.60f, false)]
        [TestCase(TerminalIrisSourceRoute.ActualDefeat, 0.45f, true)]
        [TestCase(TerminalIrisSourceRoute.ManualRetry, 0.03f, false)]
        [TestCase(TerminalIrisSourceRoute.LevelFailedRestart, 0.03f, false)]
        [TestCase(TerminalIrisSourceRoute.DemoStageRelaunch, 0.03f, false)]
        [TestCase(TerminalIrisSourceRoute.GameplayEntry, 0.03f, false)]
        [TestCase(TerminalIrisSourceRoute.ReturnToMainMenu, 0.03f, false)]
        [TestCase(TerminalIrisSourceRoute.DeathRetry, 0f, false)]
        public void TerminalIrisMotionProfile_RouteSemanticMappingResolvesAuthoredPreset(
            TerminalIrisSourceRoute route,
            float expectedHoldDuration,
            bool expectedReveal)
        {
            var profile = AssetDatabase.LoadAssetAtPath<TerminalIrisMotionProfile>(
                TerminalIrisMotionProfilePath);
            var resolver = profile.CreateResolver();

            var resolved = ResolveSourceCloseForRoute(resolver, route);

            if (route == TerminalIrisSourceRoute.DeathRetry)
            {
                Assert.That(resolved.HasValue, Is.False);
                Assert.That(
                    resolver.ResolveRetryVisual(SceneTransitionIntent.DeathRetry)
                        .SourceCloseVisualKind,
                    Is.EqualTo(GameplayEntrySourceCloseVisualKind.ExistingDefeatIris));
                return;
            }

            Assert.That(resolved.HasValue, Is.True, route.ToString());
            Assert.That(
                resolved.Value.HoldDuration,
                Is.EqualTo(expectedHoldDuration).Within(0.000001f),
                route.ToString());
            Assert.That(resolved.Value.RevealPreset.HasValue, Is.EqualTo(expectedReveal));
        }

        [Test]
        public void TerminalIrisMotionProfile_DeathRetryInheritsExistingDefeatClosedState()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TerminalIrisMotionProfile>(
                TerminalIrisMotionProfilePath);
            var resolver = profile.CreateResolver();

            var visual = resolver.ResolveRetryVisual(SceneTransitionIntent.DeathRetry);

            Assert.That(
                visual.SourceCloseVisualKind,
                Is.EqualTo(GameplayEntrySourceCloseVisualKind.ExistingDefeatIris));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                resolver.ResolveRetryClose(SceneTransitionIntent.DeathRetry));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                resolver.ResolveGameplayEntrySourceClose(SceneTransitionIntent.DeathRetry));
        }

        [Test]
        public void TerminalIrisMotionProfile_MissingOrInvalidProfile_FailsFast()
        {
            var missingException = Assert.Throws<InvalidOperationException>(
                () => new TerminalIrisMotionProfileResolver(null));
            Assert.That(missingException.Message, Does.Contain("serialized TerminalIrisMotionProfile"));

            var source = AssetDatabase.LoadAssetAtPath<TerminalIrisMotionProfile>(
                TerminalIrisMotionProfilePath);
            var invalidDuration = UnityEngine.Object.Instantiate(source);
            var invalidRadius = UnityEngine.Object.Instantiate(source);
            var invalidRetry = UnityEngine.Object.Instantiate(source);
            GameObject missingBindingRoot = null;
            try
            {
                SetProfileFloat(
                    invalidDuration,
                    "_victoryClose",
                    "_focusDuration",
                    -0.01f);
                var durationException = Assert.Throws<InvalidOperationException>(
                    () => invalidDuration.CreateResolver());
                Assert.That(durationException.Message, Does.Contain("VictoryClose"));

                SetProfileFloat(
                    invalidRadius,
                    "_defeatClose",
                    "_fallbackRadius",
                    -0.01f);
                var radiusException = Assert.Throws<InvalidOperationException>(
                    () => invalidRadius.CreateResolver());
                Assert.That(radiusException.Message, Does.Contain("DefeatClose"));

                SetProfileFloat(
                    invalidRetry,
                    "_retryClose",
                    "_holdDuration",
                    -0.01f);
                var retryException = Assert.Throws<InvalidOperationException>(
                    () => invalidRetry.CreateResolver());
                Assert.That(retryException.Message, Does.Contain("RetryClose"));

                var shellPrefab = Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
                missingBindingRoot = UnityEngine.Object.Instantiate(shellPrefab);
                var rootView = missingBindingRoot.GetComponent<GameplayUiCanvasRootView>();
                var serializedRoot = new SerializedObject(rootView);
                serializedRoot.FindProperty("_terminalIrisMotionProfile").objectReferenceValue = null;
                serializedRoot.ApplyModifiedPropertiesWithoutUndo();
                var bindingException = Assert.Throws<InvalidOperationException>(
                    rootView.EnsureHierarchy);
                Assert.That(bindingException.Message, Does.Contain("TerminalIrisMotionProfile"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidDuration);
                UnityEngine.Object.DestroyImmediate(invalidRadius);
                UnityEngine.Object.DestroyImmediate(invalidRetry);
                if (missingBindingRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(missingBindingRoot);
                }

                DestroySceneObject("EventSystem");
            }
        }

        [Test]
        public void TerminalIrisMotionProfile_InspectorValuesDriveNextRuntimePlayback()
        {
            var source = AssetDatabase.LoadAssetAtPath<TerminalIrisMotionProfile>(
                TerminalIrisMotionProfilePath);
            var profile = UnityEngine.Object.Instantiate(source);
            try
            {
                SetProfileFloat(profile, "_victoryClose", "_focusDuration", 0.5f);
                SetProfileFloat(
                    profile,
                    "_victoryClose",
                    "_finalClosedOvershootPixels",
                    6f);
                SetProfileEdgeFloat(
                    profile,
                    "_victoryClose",
                    "_artisticFeatherHalfWidthPixels",
                    1.45f);
                SetProfileEdgeFloat(profile, "_victoryClose", "_rimWidthPixels", 1.25f);
                SetProfileEdgeColor(profile, "_victoryClose", "_rimColor", Color.green);
                SetProfileEnum(
                    profile,
                    "_victoryClose",
                    "_focusEasing",
                    TerminalIrisEasing.SmoothStep);
                SetProfileFloat(profile, "_defeatClose", "_closeDuration", 0.61f);
                SetProfileFloat(profile, "_stageEntryOpen", "_openingDuration", 0.73f);
                SetProfileEnum(
                    profile,
                    "_stageEntryOpen",
                    "_openingEasing",
                    TerminalIrisEasing.Linear);

                var resolver = profile.CreateResolver();
                var victory = resolver.ResolveClose(TerminalTransitionKind.Victory);
                var defeat = resolver.ResolveClose(TerminalTransitionKind.Defeat);
                var stageEntry = resolver.ResolveStageEntryOpen();

                Assert.That(victory.FocusDuration, Is.EqualTo(0.5f));
                Assert.That(victory.Edge.ArtisticFeatherHalfWidthPixels, Is.EqualTo(1.45f));
                Assert.That(victory.Edge.RimWidthPixels, Is.EqualTo(1.25f));
                Assert.That(victory.Edge.RimColor, Is.EqualTo(Color.green));
                Assert.That(victory.FocusEasing, Is.EqualTo(TerminalIrisEasing.SmoothStep));
                Assert.That(defeat.CloseDuration, Is.EqualTo(0.61f));
                Assert.That(stageEntry.OpeningDuration, Is.EqualTo(0.73f));
                Assert.That(stageEntry.OpeningEasing, Is.EqualTo(TerminalIrisEasing.Linear));

                var playback = new TerminalTransitionPlayback(victory);
                Assert.That(
                    playback.TryBegin(
                        new TerminalTransitionRequest(
                            TerminalTransitionKind.Victory,
                            focusEntityId: 10,
                            claimId: 88,
                            destinationMode: TerminalTransitionDestinationMode.SameScene),
                        new TerminalFocusTarget(
                            new Vector2(0.9f, 0.5f),
                            0.15f,
                            isFallback: false)),
                    Is.True);
                playback.Advance(victory.FocusDuration * 0.25f);

                Assert.That(playback.CurrentCenter.x, Is.EqualTo(0.5625f).Within(0.000001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TerminalIrisMotionProfile_PlaybackKeepsSnapshotUntilNextPlayback()
        {
            var source = AssetDatabase.LoadAssetAtPath<TerminalIrisMotionProfile>(
                TerminalIrisMotionProfilePath);
            var profile = UnityEngine.Object.Instantiate(source);
            try
            {
                var resolver = profile.CreateResolver();
                var captured = resolver.ResolveClose(TerminalTransitionKind.Victory);
                var playback = new TerminalTransitionPlayback(captured);
                playback.TryBegin(
                    new TerminalTransitionRequest(
                        TerminalTransitionKind.Victory,
                        focusEntityId: 10,
                        claimId: 89,
                        destinationMode: TerminalTransitionDestinationMode.SameScene),
                    new TerminalFocusTarget(new Vector2(0.8f, 0.5f), 0.15f, false));

                SetProfileFloat(profile, "_victoryClose", "_focusDuration", 0.81f);
                SetProfileFloat(
                    profile,
                    "_victoryClose",
                    "_finalClosedOvershootPixels",
                    6f);
                SetProfileEdgeFloat(
                    profile,
                    "_victoryClose",
                    "_artisticFeatherHalfWidthPixels",
                    1.71f);
                SetProfileEdgeFloat(profile, "_victoryClose", "_rimWidthPixels", 0.83f);
                SetProfileEdgeColor(profile, "_victoryClose", "_rimColor", Color.magenta);
                SetProfileEnum(
                    profile,
                    "_victoryClose",
                    "_closeEasing",
                    TerminalIrisEasing.SmoothStep);

                Assert.That(playback.Preset.FocusDuration, Is.EqualTo(0.20f));
                Assert.That(
                    playback.Preset.Edge.ArtisticFeatherHalfWidthPixels,
                    Is.EqualTo(1.25f));
                Assert.That(playback.Preset.Edge.RimWidthPixels, Is.Zero);
                Assert.That(playback.Preset.Edge.RimColor, Is.Not.EqualTo(Color.magenta));
                Assert.That(
                    playback.Preset.CloseEasing,
                    Is.EqualTo(TerminalIrisEasing.EaseOutSine));

                var next = resolver.ResolveClose(TerminalTransitionKind.Victory);
                Assert.That(next.FocusDuration, Is.EqualTo(0.81f));
                Assert.That(next.Edge.ArtisticFeatherHalfWidthPixels, Is.EqualTo(1.71f));
                Assert.That(next.Edge.RimWidthPixels, Is.EqualTo(0.83f));
                Assert.That(next.Edge.RimColor, Is.EqualTo(Color.magenta));
                Assert.That(next.CloseEasing, Is.EqualTo(TerminalIrisEasing.SmoothStep));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TerminalIrisMotionProfile_ProductionSourceHasNoLegacyPresetResidue()
        {
            var repositoryRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            Assert.That(repositoryRoot, Is.Not.Null);
            var terminalTypes = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Assets/_Features/Stages/Runtime/Queries/TerminalTransitionTypes.cs"));
            var transitionPort = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayTerminalTransitionPort.cs"));
            var installer = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"));
            var resultStyle = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Assets/_Features/UI/UI_Composition/Runtime/ResultTransitionVisualStyle.cs"));

            Assert.That(terminalTypes, Does.Not.Contain("class TerminalIrisPreset"));
            Assert.That(terminalTypes, Does.Not.Contain("static TerminalIrisRuntimePreset Victory"));
            Assert.That(terminalTypes, Does.Not.Contain("static TerminalIrisRuntimePreset Defeat"));
            Assert.That(terminalTypes, Does.Not.Contain("0.24f"));
            Assert.That(terminalTypes, Does.Not.Contain("0.34f"));
            Assert.That(terminalTypes, Does.Not.Contain("0.42f"));
            Assert.That(terminalTypes, Does.Not.Contain("0.18f"));
            Assert.That(terminalTypes, Does.Not.Contain("0.32f"));
            Assert.That(transitionPort, Does.Not.Contain("TerminalIrisPreset."));
            Assert.That(transitionPort, Does.Not.Contain("Resources.Load"));
            Assert.That(installer, Does.Not.Contain("EntryOpeningDuration"));
            Assert.That(installer, Does.Not.Contain("0.35f"));
            Assert.That(resultStyle, Does.Not.Contain("EntryOpeningDuration"));
        }

        [Test]
        public void TerminalIrisShader_UsesRenderTargetDerivativesAndPixelAuthoredEdges()
        {
            var repositoryRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            Assert.That(repositoryRoot, Is.Not.Null);
            var shaderSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Assets/_Features/UI/UI_Composition/Shaders/TerminalIris.shader"));
            var viewSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Assets/_Features/UI/UI_Composition/Runtime/TerminalIrisOverlayView.cs"));
            var installerSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"));
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Features/UI/UI_Composition/Shaders/TerminalIrisOverlay.mat");

            Assert.That(shaderSource, Does.Contain("#pragma target 3.0"));
            Assert.That(shaderSource, Does.Contain("_ScreenParams.x / max(1.0, _ScreenParams.y)"));
            Assert.That(shaderSource, Does.Contain("rcp(max(1.0, _ScreenParams.y))"));
            Assert.That(shaderSource, Does.Contain("ddx(signedDistance)"));
            Assert.That(shaderSource, Does.Contain("ddy(signedDistance)"));
            Assert.That(shaderSource, Does.Contain("length(distanceDerivative)"));
            Assert.That(shaderSource, Does.Contain("_ArtisticFeatherHalfWidthPixels"));
            Assert.That(shaderSource, Does.Contain("_RimWidthPixels"));
            Assert.That(shaderSource, Does.Contain("_RimSoftnessPixels"));
            Assert.That(shaderSource, Does.Not.Contain("CanUseSpriteAtlas"));
            Assert.That(shaderSource, Does.Not.Contain("step(0.0001, _Radius)"));
            Assert.That(shaderSource, Does.Not.Contain("fixed4 frag"));
            Assert.That(viewSource, Does.Not.Contain("Screen.width"));
            Assert.That(viewSource, Does.Not.Contain("Screen.height"));
            Assert.That(viewSource, Does.Not.Contain("_AspectRatio"));
            Assert.That(installerSource, Does.Not.Contain("Screen.width"));
            Assert.That(installerSource, Does.Not.Contain("Screen.height"));
            Assert.That(material, Is.Not.Null);
            Assert.That(material.HasProperty("_Feather"), Is.False);
            Assert.That(material.HasProperty("_RimWidth"), Is.False);
            Assert.That(material.HasProperty("_AspectRatio"), Is.False);
            Assert.That(material.HasProperty("_MinimumAAPixels"), Is.True);
        }

        [Test]
        public void StageResultPrefab_ResultHandoffKeepsContentBlockedUntilEntranceCompletes()
        {
            var catalog = UiTestPrefabAssetUtility.LoadScreenCatalog();
            AssertResultContentOrdering(catalog.StageResultPrefab, "_continueButton", catalog.ResultTransitionStyle);
        }

        [Test]
        public void GameClearPrefab_ResultHandoffKeepsContentBlockedUntilEntranceCompletes()
        {
            var catalog = UiTestPrefabAssetUtility.LoadScreenCatalog();
            AssertResultContentOrdering(catalog.GameClearPrefab, "_mainButton", catalog.ResultTransitionStyle);
        }

        [Test]
        public void ResultTransitionVisualStyle_SnapshotIsImmutableAndNextPlaybackUsesNewAuthoring()
        {
            var style = ScriptableObject.CreateInstance<ResultTransitionVisualStyle>();
            try
            {
                var capturedDim = style.CreateDimSnapshot();
                var capturedRuntime = style.CreateRuntimeSnapshot();
                var serialized = new SerializedObject(style);
                serialized.FindProperty("_baseDimColor").colorValue =
                    new Color(0.2f, 0.4f, 0.6f, 0.8f);
                serialized.FindProperty("_resultHandoffFadeDuration").floatValue = 0.75f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(capturedDim.FinalColor, Is.Not.EqualTo(style.CreateDimSnapshot().FinalColor));
                Assert.That(capturedRuntime.ResultHandoffFadeDuration, Is.Not.EqualTo(0.75f));
                Assert.That(style.CreateDimSnapshot().OpaqueColor, Is.EqualTo(new Color(0.2f, 0.4f, 0.6f, 1f)));
                Assert.That(style.CreateRuntimeSnapshot().ResultHandoffFadeDuration, Is.EqualTo(0.75f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(style);
            }
        }

        [Test]
        public void ResultTransitionVisualStyle_RepositoryAssetPreservesLockedFunctionalTiming()
        {
            var style = UiTestPrefabAssetUtility.LoadScreenCatalog().ResultTransitionStyle;
            Assert.That(style, Is.Not.Null);

            var runtime = style.CreateRuntimeSnapshot();

            Assert.That(runtime.ResultHandoffHoldDuration, Is.EqualTo(0.04f));
            Assert.That(runtime.ResultHandoffFadeDuration, Is.EqualTo(0.18f));
            Assert.That(runtime.ResultContentEntranceDelay, Is.EqualTo(0.04f));
            Assert.That(runtime.ContentEntranceDuration, Is.EqualTo(0.18f));
            Assert.That(runtime.ResultExitCoverFadeDuration, Is.EqualTo(0.18f));
            Assert.That(
                Mathf.Max(
                    runtime.ResultHandoffHoldDuration +
                    runtime.ResultHandoffFadeDuration,
                    runtime.ResultContentEntranceDelay +
                    runtime.ContentEntranceDuration),
                Is.EqualTo(0.22f).Within(0.000001f));
            Assert.That(
                runtime.ResultHandoffEasing,
                Is.EqualTo(TerminalIrisEasing.SmoothStep));
            Assert.That(
                runtime.ResultContentEntranceEasing,
                Is.EqualTo(TerminalIrisEasing.SmoothStep));
            Assert.That(
                runtime.ResultExitCoverEasing,
                Is.EqualTo(TerminalIrisEasing.SmoothStep));
        }

        [TestCase("_resultHandoffFadeDuration", 0f)]
        [TestCase("_contentEntranceDuration", 0f)]
        [TestCase("_resultExitCoverFadeDuration", 0f)]
        [TestCase("_brightnessMeanDeltaTolerance", -0.001f)]
        [TestCase("_brightnessP99DeltaTolerance", -0.001f)]
        public void ResultTransitionVisualStyle_InvalidAuthoringFailsFast(
            string propertyName,
            float invalidValue)
        {
            var style = ScriptableObject.CreateInstance<ResultTransitionVisualStyle>();
            try
            {
                var serialized = new SerializedObject(style);
                serialized.FindProperty(propertyName).floatValue = invalidValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => style.CreateRuntimeSnapshot());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(style);
            }
        }

        [Test]
        public void ResultTransitionProductionSource_HasNoCloneReparentRenderTextureOrBackdropAnimationResidue()
        {
            var repositoryRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            Assert.That(repositoryRoot, Is.Not.Null);
            var viewSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Assets/_Features/UI/UI_Screens/Runtime/StageResultScreenView.cs"));
            var factorySource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs"));

            Assert.That(factorySource, Does.Not.Contain("Instantiate(liveResultBackdrop"));
            Assert.That(factorySource, Does.Not.Contain("RenderTexture"));
            Assert.That(factorySource, Does.Not.Contain("SetParent("));
            Assert.That(viewSource, Does.Not.Contain("_resultBackdrop.color = _resultBackdropColor"));
            Assert.That(viewSource, Does.Not.Contain("_resultBackdrop.color = closedCoverColor"));
            Assert.That(viewSource, Does.Not.Contain("Time.deltaTime"));
            Assert.That(viewSource, Does.Not.Contain("_resultHandoffCover.raycastTarget = true"));
        }

        [Test]
        public void TerminalIrisOverlayView_MissingAuthoredMaterialFailsFast()
        {
            var root = new GameObject(
                nameof(TerminalIrisOverlayView_MissingAuthoredMaterialFailsFast),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image),
                typeof(CanvasGroup),
                typeof(TerminalIrisOverlayView));
            try
            {
                var view = root.GetComponent<TerminalIrisOverlayView>();
                var exception = Assert.Throws<InvalidOperationException>(
                    () => view.Initialize(
                        root.GetComponent<UnityEngine.UI.Image>(),
                        root.GetComponent<CanvasGroup>()));

                Assert.That(
                    exception.Message,
                    Does.Contain("requires a serialized authored material"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TerminalFocusTargetSource_CapturesAllRendererBoundsAsImmutableSnapshot()
        {
            var registryObject = new GameObject("Registry");
            var cameraObject = new GameObject("Camera");
            var viewObject = new GameObject("PlayerView");
            var renderTexture = new RenderTexture(1920, 1080, 0);
            try
            {
                var registry = registryObject.AddComponent<GameplayEntityViewRegistry>();
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = renderTexture;
                camera.transform.position = Vector3.zero;
                camera.transform.rotation = Quaternion.identity;
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(10);
                viewObject.transform.position = new Vector3(0f, 0f, 5f);
                var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
                left.name = "LeftRenderer";
                left.transform.SetParent(viewObject.transform, false);
                left.transform.localPosition = new Vector3(-0.75f, 0f, 0f);
                var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
                right.name = "RightRenderer";
                right.transform.SetParent(viewObject.transform, false);
                right.transform.localPosition = new Vector3(0.75f, 0f, 0f);
                registry.Register(view);
                var source = new GameplayTerminalFocusTargetSource(registry, camera);

                Assert.That(source.TryCapture(10, out var snapshot), Is.True);
                Assert.That(snapshot.IsFallback, Is.False);
                Assert.That(snapshot.NormalizedCenter.x, Is.EqualTo(0.5f).Within(0.03f));
                Assert.That(snapshot.NormalizedRadius, Is.GreaterThan(0.1f));

                var capturedCenter = snapshot.NormalizedCenter;
                var capturedRadius = snapshot.NormalizedRadius;
                viewObject.transform.position = new Vector3(20f, 20f, 5f);
                if (viewObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(viewObject);
                }

                Assert.That(snapshot.NormalizedCenter, Is.EqualTo(capturedCenter));
                Assert.That(snapshot.NormalizedRadius, Is.EqualTo(capturedRadius));
            }
            finally
            {
                if (cameraObject != null)
                {
                    cameraObject.GetComponent<Camera>().targetTexture = null;
                }
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                if (viewObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(viewObject);
                }
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(registryObject);
            }
        }

        [Test]
        public void TerminalFocusTargetSource_MissingCameraViewAndBehindCameraReturnFallbackSignal()
        {
            var registryObject = new GameObject("Registry");
            var cameraObject = new GameObject("Camera");
            var viewObject = new GameObject("PlayerView");
            var renderTexture = new RenderTexture(1920, 1080, 0);
            try
            {
                var registry = registryObject.AddComponent<GameplayEntityViewRegistry>();
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = renderTexture;
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(10);
                viewObject.transform.position = new Vector3(0f, 0f, -5f);
                var rendererObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rendererObject.transform.SetParent(viewObject.transform, false);
                registry.Register(view);

                Assert.That(
                    new GameplayTerminalFocusTargetSource(registry, null)
                        .TryCapture(10, out _),
                    Is.False);
                Assert.That(
                    new GameplayTerminalFocusTargetSource(registry, camera)
                        .TryCapture(999, out _),
                    Is.False);
                Assert.That(
                    new GameplayTerminalFocusTargetSource(registry, camera)
                        .TryCapture(10, out _),
                    Is.False);
            }
            finally
            {
                if (cameraObject != null)
                {
                    cameraObject.GetComponent<Camera>().targetTexture = null;
                }
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(viewObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(registryObject);
            }
        }

        [Test]
        public void TerminalFocusTargetSource_CommonAspectRatiosPreserveRadiusAndClampEdgeCapture()
        {
            var registryObject = new GameObject("Registry");
            var cameraObject = new GameObject("Camera");
            var viewObject = new GameObject("PlayerView");
            var radii = new List<float>();
            try
            {
                var registry = registryObject.AddComponent<GameplayEntityViewRegistry>();
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(10);
                viewObject.transform.position = new Vector3(0f, 0f, 5f);
                var rendererObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rendererObject.transform.SetParent(viewObject.transform, false);
                registry.Register(view);

                var resolutions = new[]
                {
                    new Vector2Int(1920, 1080),
                    new Vector2Int(2560, 1440),
                    new Vector2Int(1920, 1200),
                    new Vector2Int(2560, 1080),
                    new Vector2Int(3440, 1440),
                };
                for (var i = 0; i < resolutions.Length; i++)
                {
                    var resolution = resolutions[i];
                    var renderTexture = new RenderTexture(resolution.x, resolution.y, 0);
                    try
                    {
                        camera.targetTexture = renderTexture;
                        var source = new GameplayTerminalFocusTargetSource(registry, camera);

                        Assert.That(
                            source.TryCapture(10, out var target),
                            Is.True,
                            $"{resolution.x}x{resolution.y}");
                        Assert.That(
                            target.NormalizedCenter.x,
                            Is.EqualTo(0.5f).Within(0.0001f),
                            $"{resolution.x}x{resolution.y}");
                        Assert.That(
                            target.NormalizedCenter.y,
                            Is.EqualTo(0.5f).Within(0.0001f),
                            $"{resolution.x}x{resolution.y}");
                        radii.Add(target.NormalizedRadius);
                    }
                    finally
                    {
                        camera.targetTexture = null;
                        renderTexture.Release();
                        UnityEngine.Object.DestroyImmediate(renderTexture);
                    }
                }

                Assert.That(radii.Max() - radii.Min(), Is.LessThan(0.005f));

                var ultrawideResolutions = new[]
                {
                    new Vector2Int(2560, 1080),
                    new Vector2Int(3440, 1440),
                };
                var edgeFocuses = new[]
                {
                    new Vector2(0.01f, 0.5f),
                    new Vector2(0.99f, 0.5f),
                    new Vector2(0.5f, 0.01f),
                    new Vector2(0.5f, 0.99f),
                    new Vector2(0.99f, 0.99f),
                };
                for (var resolutionIndex = 0; resolutionIndex < ultrawideResolutions.Length; resolutionIndex++)
                {
                    var resolution = ultrawideResolutions[resolutionIndex];
                    var edgeTexture = new RenderTexture(resolution.x, resolution.y, 0);
                    try
                    {
                        camera.targetTexture = edgeTexture;
                        for (var focusIndex = 0; focusIndex < edgeFocuses.Length; focusIndex++)
                        {
                            var requestedFocus = edgeFocuses[focusIndex];
                            viewObject.transform.position = camera.ViewportToWorldPoint(
                                new Vector3(requestedFocus.x, requestedFocus.y, 5f));
                            var source = new GameplayTerminalFocusTargetSource(registry, camera);

                            Assert.That(
                                source.TryCapture(10, out var edgeTarget),
                                Is.True,
                                $"{resolution.x}x{resolution.y} focus={requestedFocus}");
                            Assert.That(
                                edgeTarget.IsFallback,
                                Is.False,
                                $"{resolution.x}x{resolution.y} focus={requestedFocus}");
                            Assert.That(
                                edgeTarget.NormalizedCenter.x,
                                Is.EqualTo(Mathf.Clamp(requestedFocus.x, 0.025f, 0.975f)).Within(0.0001f));
                            Assert.That(
                                edgeTarget.NormalizedCenter.y,
                                Is.EqualTo(Mathf.Clamp(requestedFocus.y, 0.025f, 0.975f)).Within(0.0001f));
                            var bounds = rendererObject.GetComponent<Renderer>().bounds;
                            for (var x = 0; x <= 1; x++)
                            {
                                for (var y = 0; y <= 1; y++)
                                {
                                    for (var z = 0; z <= 1; z++)
                                    {
                                        var viewport = camera.WorldToViewportPoint(new Vector3(
                                            x == 0 ? bounds.min.x : bounds.max.x,
                                            y == 0 ? bounds.min.y : bounds.max.y,
                                            z == 0 ? bounds.min.z : bounds.max.z));
                                        var delta = new Vector2(
                                            (viewport.x - edgeTarget.NormalizedCenter.x) *
                                            ((float)resolution.x / resolution.y),
                                            viewport.y - edgeTarget.NormalizedCenter.y);
                                        Assert.That(
                                            delta.magnitude,
                                            Is.LessThanOrEqualTo(edgeTarget.NormalizedRadius + 0.0001f),
                                            $"{resolution.x}x{resolution.y} focus={requestedFocus} omitted renderer corner {x},{y},{z}");
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        camera.targetTexture = null;
                        edgeTexture.Release();
                        UnityEngine.Object.DestroyImmediate(edgeTexture);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(registryObject);
            }
        }

        [TestCase(16f / 9f, 0.5f, 0.5f)]
        [TestCase(16f / 10f, 0.025f, 0.5f)]
        [TestCase(21f / 9f, 0.975f, 0.025f)]
        [TestCase(3440f / 1440f, 0.975f, 0.975f)]
        public void TerminalRevealRadius_CoversFarthestAspectCorrectedCornerAndFeather(
            float aspect,
            float centerX,
            float centerY)
        {
            const float feather = 0.018f;
            var center = new Vector2(centerX, centerY);
            var radius = TerminalTransitionPlayback.CalculateFullyRevealedRadius(
                center,
                aspect,
                feather);
            var farthest = 0f;
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    var delta = new Vector2((x - center.x) * aspect, y - center.y);
                    farthest = Mathf.Max(farthest, delta.magnitude);
                }
            }

            Assert.That(radius, Is.GreaterThanOrEqualTo(farthest + feather));
        }

        [Test]
        public void GameplayHudRoot_RequiredBoundViewsPresent_AndRetiredProofResidueRemoved()
        {
            var hudPrefab = UiTestPrefabAssetUtility.LoadHudPrefab();
            var instance = UnityEngine.Object.Instantiate(hudPrefab);
            try
            {
                instance.ValidateAuthoredStructureOrThrow();

                Assert.That(instance.GetComponentsInChildren<MonoBehaviour>(true).All(component => component != null), Is.True, "Canonical HUD must not contain missing scripts.");
                Assert.That(instance.transform.Find("PlayerStatus"), Is.Null);
                Assert.That(instance.ObjectiveHudView, Is.Not.Null);
                Assert.That(instance.ChancePanelView, Is.Not.Null);
                Assert.That(instance.SurfaceBeltIndicatorView, Is.Not.Null);

                var pauseButton = FindChildByName(instance.transform, "PauseButton") as RectTransform;
                Assert.That(pauseButton, Is.Not.Null);
                Assert.That(pauseButton.anchorMin, Is.EqualTo(new Vector2(1f, 0.5f)));
                Assert.That(pauseButton.anchorMax, Is.EqualTo(new Vector2(1f, 0.5f)));
                Assert.That(pauseButton.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(pauseButton.sizeDelta, Is.EqualTo(new Vector2(50f, 60f)));
                Assert.That(pauseButton.anchoredPosition.x, Is.EqualTo(-45f).Within(0.001f));
                Assert.That(pauseButton.anchoredPosition.y, Is.EqualTo(13.1f).Within(0.001f));
                var pauseButtonRightOffset =
                    pauseButton.anchoredPosition.x + ((1f - pauseButton.pivot.x) * pauseButton.sizeDelta.x);
                Assert.That(pauseButtonRightOffset, Is.EqualTo(-20f).Within(0.001f));
                Assert.That(FindChildByName(instance.transform, "Label_StageName"), Is.Not.Null);
                Assert.That(FindChildByName(instance.transform, "CenterArrow"), Is.Not.Null);
                Assert.That(FindChildByName(instance.transform, "LegacyTopologyDebugText"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "LegacyCenterArrow"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "Action" + "Bar"), Is.Null);
                Assert.That(CountMissingScripts(instance.gameObject), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void UiAudioCueMaps_RepositoryAssets_AllCuesExplicitUiOneShot()
        {
            var cueMaps = LoadAllAssets<UiAudioCueMap>();
            Assert.That(cueMaps, Is.Not.Empty, "Repository scan found no UiAudioCueMap assets.");

            var failures = new List<string>();
            foreach (var cueMap in cueMaps)
            {
                try
                {
                    cueMap.ValidateOrThrow();
                    Assert.That(cueMap.Entries.Count, Is.EqualTo(Enum.GetValues(typeof(UiAudioCueId)).Length));
                    foreach (var entry in cueMap.Entries)
                    {
                        Assert.That(entry.Binding.Definition.Category, Is.EqualTo(AudioCategory.Ui), Describe(cueMap));
                        Assert.That(entry.Binding.Definition.Loop, Is.False, Describe(cueMap));
                        Assert.That(entry.Binding.HasAttachmentSlot, Is.False, Describe(cueMap));
                        Assert.That(entry.Binding.Policy, Is.Null, Describe(cueMap));
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{Describe(cueMap)}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "UiAudioCueMap repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        public void SettingsScreenPrefab_AuthoredSectionsValidate()
        {
            var prefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(
                UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.ValidateAuthoredStructureOrThrow();
                instance.AudioView.ValidateAuthoredControlsOrThrow();
                instance.DisplayView.ValidateAuthoredControlsOrThrow();
                instance.InputView.ValidateAuthoredControlsOrThrow();

                Assert.That(FindChildByName(instance.transform, "ResetInput_Legacy"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "DisplayApplyButton"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "DisplayRevertButton"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "DisplayReveryButton_New"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "ResetInput_New"), Is.Not.Null);
                Assert.That(FindChildByName(instance.transform, "DisplayApplyButton_New"), Is.Not.Null);
                Assert.That(FindChildByName(instance.transform, "DisplayRevertButton_New"), Is.Not.Null);

                var tabRow = FindChildByName(instance.transform, "SettingsTabRow") as RectTransform;
                Assert.That(tabRow, Is.Not.Null);
                var tabLayout = tabRow.GetComponent<HorizontalLayoutGroup>();
                Assert.That(tabLayout, Is.Not.Null);
                Assert.That(tabLayout.childScaleWidth, Is.False);
                Assert.That(tabLayout.childScaleHeight, Is.False);

                LayoutRebuilder.ForceRebuildLayoutImmediate(tabRow);
                var tabPositions = Enumerable.Range(0, tabRow.childCount)
                    .Select(index => ((RectTransform)tabRow.GetChild(index)).anchoredPosition)
                    .ToArray();
                var audioTab = FindChildByName(tabRow, "AudioTab") as RectTransform;
                Assert.That(audioTab, Is.Not.Null);
                audioTab.localScale = Vector3.one * 1.1f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(tabRow);

                for (var index = 0; index < tabRow.childCount; index++)
                {
                    var child = (RectTransform)tabRow.GetChild(index);
                    Assert.That(
                        child.anchoredPosition,
                        Is.EqualTo(tabPositions[index]),
                        $"{child.name} position must not reflow when a settings tab is hover-scaled.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void StageResultPayload_ActualReadModel_MapsToNavigationRequest()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            var sequenceResolver = LoadProductionCampaignSequenceResolver();
            Assert.That(sequenceResolver.Contains(stageId), Is.True, stageId.Value);
            Assert.That(sequenceResolver.IsFinal(stageId), Is.False, stageId.Value);
            Assert.That(sequenceResolver.TryGetNext(stageId, out var expectedNextStageId), Is.True, stageId.Value);

            var readModel = CreateActualReadModel(stageId, sequenceResolver);

            var payload = StageResultPayloadMapper.Map(readModel);

            Assert.That(payload.ContinueStageRequest.IsValid, Is.True);
            Assert.That(payload.RetryStageRequest.IsValid, Is.True);
            Assert.That(payload.NextStageRequest.IsValid, Is.True);
            Assert.That(payload.NextStageRequest.StageId, Is.EqualTo(expectedNextStageId));
        }

        [Test]
        private static Component ResolveScreenPrefab(ScreenPrefabCatalog catalog, ScreenId screenId)
        {
            return screenId switch
            {
                ScreenId.Settings => catalog.SettingsPrefab,
                ScreenId.StageResult => catalog.StageResultPrefab,
                ScreenId.LevelFailed => catalog.LevelFailedPrefab,
                ScreenId.GameClear => catalog.GameClearPrefab,
                _ => throw new ArgumentOutOfRangeException(nameof(screenId), screenId, null),
            };
        }

        private static Component ResolvePopupPrefab(PopupPrefabCatalog catalog, PopupId popupId)
        {
            return popupId switch
            {
                PopupId.Pause => catalog.PausePrefab,
                PopupId.Confirm => catalog.ConfirmPrefab,
                PopupId.DemoStageControl => ResolveDemoStageControlPrefab(catalog),
                _ => throw new ArgumentOutOfRangeException(nameof(popupId), popupId, null),
            };
        }

        private static Component ResolveDemoStageControlPrefab(PopupPrefabCatalog catalog)
        {
            var property = typeof(PopupPrefabCatalog).GetProperty("DemoStageControlPrefab");
            Assert.That(property, Is.Not.Null,
                "PopupPrefabCatalog must expose a typed DemoStageControl prefab reference.");
            return property?.GetValue(catalog) as Component;
        }

        private static MinimalStageCompletionReadModel CreateActualReadModel(
            StageId stageId,
            CampaignStageSequenceResolver sequenceResolver)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            Assert.That(catalog, Is.Not.Null, StageContentPaths.StageCatalogAssetPath);
            var entry = catalog.Entries.FirstOrDefault(candidate => candidate.StageId.Equals(stageId));
            Assert.That(entry, Is.Not.Null, stageId.Value);

            return MinimalStageCompletionReadModelBuilder.Build(
                entry,
                new StageClearResult(
                    entry.StageId,
                    finalTickIndex: 5),
                sequenceResolver);
        }

        private static CampaignStageSequenceResolver LoadProductionCampaignSequenceResolver()
        {
            var definition = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                StageContentPaths.CampaignStageSequenceAssetPath);
            Assert.That(
                definition,
                Is.Not.Null,
                $"Missing campaign sequence at '{StageContentPaths.CampaignStageSequenceAssetPath}'.");
            return new CampaignStageSequenceResolver(definition);
        }

        private static IReadOnlyList<T> LoadAllAssets<T>() where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            Array.Sort(guids, StringComparer.Ordinal);
            return guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .ToArray();
        }

        private static string Describe(UnityEngine.Object asset)
        {
            return asset == null
                ? "<null>"
                : $"{asset.GetType().Name} '{asset.name}' Path='{AssetDatabase.GetAssetPath(asset)}'";
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => string.Equals(child.name, childName, StringComparison.Ordinal));
        }

        private static void AssertGameClearResultOnlyPrefab(GameClearScreenView gameClear)
        {
            var serialized = new SerializedObject(gameClear);
            AssertRequiredObjectReference(serialized, "_titleLabel", nameof(GameClearScreenView));
            AssertRequiredObjectReference(serialized, "_mainButton", nameof(GameClearScreenView));
            AssertRequiredObjectReference(serialized, "_mainButtonLabel", nameof(GameClearScreenView));
            Assert.That(serialized.FindProperty("_detailLabel"), Is.Null);
            Assert.That(serialized.FindProperty("_restartLevelButton"), Is.Null);
            Assert.That(serialized.FindProperty("_restartLevelButtonLabel"), Is.Null);

            Assert.That(FindChildByName(gameClear.transform, "Title"), Is.Not.Null);
            Assert.That(FindChildByName(gameClear.transform, "ResultDetail"), Is.Null);
            Assert.That(FindChildByName(gameClear.transform, "Detail"), Is.Null);
            Assert.That(FindChildByName(gameClear.transform, "RestartLevelButton"), Is.Null);

            var mainButton = FindChildByName(gameClear.transform, "MainButton");
            Assert.That(mainButton, Is.Not.Null);
            Assert.That(FindChildByName(mainButton, "SelectionFrame"), Is.Not.Null);
        }

        private static void AssertStageResultMinimalNavigationEndpointPrefab(StageResultScreenView stageResult)
        {
            var serialized = new SerializedObject(stageResult);
            AssertRequiredObjectReference(serialized, "_titleLabel", nameof(StageResultScreenView));
            Assert.That(serialized.FindProperty("_summaryLabel"), Is.Null);
            Assert.That(serialized.FindProperty("_detailLabel"), Is.Null);
            AssertRequiredObjectReference(serialized, "_continueButton", nameof(StageResultScreenView));
            AssertRequiredObjectReference(serialized, "_continueButtonLabel", nameof(StageResultScreenView));

            Assert.That(FindChildByName(stageResult.transform, "Title"), Is.Null);
            Assert.That(FindChildByName(stageResult.transform, "ResultSummary"), Is.Null);
            Assert.That(FindChildByName(stageResult.transform, "Summary"), Is.Null);
            Assert.That(FindChildByName(stageResult.transform, "ResultDetail"), Is.Null);
            Assert.That(FindChildByName(stageResult.transform, "Detail"), Is.Null);
            Assert.That(FindChildByName(stageResult.transform, "ContinueButton (1)"), Is.Null);

            var continueButton = FindChildByName(stageResult.transform, "ContinueButton");
            Assert.That(continueButton, Is.Not.Null);
            Assert.That(FindChildByName(continueButton, "SelectionFrame"), Is.Not.Null);
        }

        private static void AssertRequiredObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            string ownerName)
        {
            var property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"{ownerName}.{propertyName}");
            Assert.That(property.objectReferenceValue, Is.Not.Null, $"{ownerName}.{propertyName}");
        }

        private static void AssertSharedResultTransitionStyle(ScreenPrefabCatalog catalog)
        {
            var style = catalog.ResultTransitionStyle;
            Assert.That(style, Is.Not.Null);
            var dim = style.CreateDimSnapshot();
            var runtime = style.CreateRuntimeSnapshot();
            Assert.That(dim.OpaqueColor.a, Is.EqualTo(1f));
            Assert.That(dim.FinalColor.a, Is.EqualTo(0.9411765f).Within(0.000001f));
            Assert.That(dim.FinalColor.r, Is.EqualTo(dim.OpaqueColor.r));
            Assert.That(dim.FinalColor.g, Is.EqualTo(dim.OpaqueColor.g));
            Assert.That(dim.FinalColor.b, Is.EqualTo(dim.OpaqueColor.b));
            Assert.That(dim.IsFullStretch, Is.True);
            Assert.That(dim.RaycastTarget, Is.False);
            Assert.That(runtime.ResultHandoffFadeDuration, Is.GreaterThan(0f));
            Assert.That(runtime.ContentEntranceDuration, Is.GreaterThan(0f));
            Assert.That(runtime.ResultExitCoverFadeDuration, Is.GreaterThan(0f));
            Assert.That(runtime.PixelComparisonTolerance, Is.GreaterThanOrEqualTo(0f));
            Assert.That(runtime.BrightnessMeanDeltaTolerance, Is.GreaterThanOrEqualTo(0f));
            Assert.That(runtime.BrightnessP99DeltaTolerance, Is.GreaterThanOrEqualTo(0f));
        }

        private static void AssertResultTransitionPrefab(Component view, string primaryButtonProperty)
        {
            var serialized = new SerializedObject(view);
            var backdropRoot = serialized.FindProperty("_backdropRoot")?.objectReferenceValue as CanvasGroup;
            var backdrop = serialized.FindProperty("_resultBackdrop")?.objectReferenceValue as UnityEngine.UI.Image;
            var handoffCover = serialized.FindProperty("_resultHandoffCover")?.objectReferenceValue as UnityEngine.UI.Image;
            var handoffGroup = serialized.FindProperty("_resultHandoffCoverGroup")?.objectReferenceValue as CanvasGroup;
            var contentRoot = serialized.FindProperty("_contentRoot")?.objectReferenceValue as CanvasGroup;
            var primaryButton = serialized.FindProperty(primaryButtonProperty)?.objectReferenceValue as UnityEngine.UI.Button;

            Assert.That(backdropRoot, Is.Not.Null, $"{view.name} BackdropRoot");
            Assert.That(backdrop, Is.Not.Null, $"{view.name} ResultBackdrop");
            Assert.That(handoffCover, Is.Not.Null, $"{view.name} ResultHandoffCover");
            Assert.That(handoffGroup, Is.Not.Null, $"{view.name} ResultHandoffCover CanvasGroup");
            Assert.That(contentRoot, Is.Not.Null, $"{view.name} ContentRoot");
            Assert.That(primaryButton, Is.Not.Null, $"{view.name} primary button");
            Assert.That(backdropRoot.ignoreParentGroups, Is.True);
            Assert.That(backdrop.raycastTarget, Is.False);
            Assert.That(backdrop.sprite, Is.Null);
            Assert.That(backdrop.material, Is.EqualTo(backdrop.defaultMaterial));
            Assert.That(backdrop.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(backdrop.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(backdrop.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(backdrop.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(handoffCover.raycastTarget, Is.False);
            Assert.That(handoffCover.sprite, Is.Null);
            Assert.That(handoffCover.material, Is.EqualTo(handoffCover.defaultMaterial));
            Assert.That(handoffGroup.interactable, Is.False);
            Assert.That(handoffGroup.blocksRaycasts, Is.False);
            Assert.That(handoffCover.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(handoffCover.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(handoffCover.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(handoffCover.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(contentRoot.transform.parent, Is.EqualTo(view.transform));
            Assert.That(backdrop.transform.parent, Is.EqualTo(view.transform));
            Assert.That(handoffCover.transform.parent, Is.EqualTo(view.transform));
            Assert.That(contentRoot.alpha, Is.Zero);
            Assert.That(contentRoot.interactable, Is.False);
            Assert.That(contentRoot.blocksRaycasts, Is.False);
            Assert.That(FindChildByName(contentRoot.transform, "Effect"), Is.Not.Null);
            Assert.That(FindChildByName(contentRoot.transform, "SPR_VIgnette"), Is.Not.Null);
        }

        private static void AssertResultContentOrdering(
            Component prefab,
            string primaryButtonProperty,
            ResultTransitionVisualStyle style)
        {
            var canvasRoot = new GameObject(
                $"{prefab.name}-ResultTransitionTestCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var instance = UnityEngine.Object.Instantiate(prefab, canvasRoot.transform, false);
            try
            {
                var view = (IResultTransitionScreenView)instance;
                var screenView = (IScreenView)instance;
                var serialized = new SerializedObject(instance);
                var backdrop = (UnityEngine.UI.Image)serialized
                    .FindProperty("_resultBackdrop")
                    .objectReferenceValue;
                var handoffCover = (UnityEngine.UI.Image)serialized
                    .FindProperty("_resultHandoffCover")
                    .objectReferenceValue;
                var handoffGroup = (CanvasGroup)serialized
                    .FindProperty("_resultHandoffCoverGroup")
                    .objectReferenceValue;
                var content = (CanvasGroup)serialized
                    .FindProperty("_contentRoot")
                    .objectReferenceValue;
                var primaryButton = (UnityEngine.UI.Button)serialized
                    .FindProperty(primaryButtonProperty)
                    .objectReferenceValue;

                var dim = style.CreateDimSnapshot();
                var runtime = style.CreateRuntimeSnapshot();
                view.ConfigureDimSnapshot(dim, runtime);
                view.PrepareOpaqueHandoff();
                screenView.SetIsCurrent(true);

                Assert.That(backdrop.color, Is.EqualTo(dim.FinalColor));
                Assert.That(handoffCover.color, Is.EqualTo(dim.OpaqueColor));
                Assert.That(handoffGroup.alpha, Is.EqualTo(1f));
                Assert.That(content.alpha, Is.Zero);
                Assert.That(content.interactable, Is.False);
                Assert.That(content.blocksRaycasts, Is.False);
                Assert.That(primaryButton.interactable, Is.False);

                var presentationField = instance.GetType().GetField(
                    "_resultTransition",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var presentation = presentationField?.GetValue(instance);
                Assert.That(presentation, Is.Not.Null);
                presentation.GetType().GetField(
                        "_handoffRequestFrame",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(presentation, Time.frameCount - 1);
                Canvas.ForceUpdateCanvases();
                presentation.GetType().GetMethod(
                        "ObserveCanvasRender",
                        BindingFlags.Instance | BindingFlags.Public)
                    ?.Invoke(presentation, null);
                if (!view.IsHandoffCoverRendered)
                {
                    presentation.GetType().GetProperty(
                            "IsHandoffCoverRendered",
                            BindingFlags.Instance | BindingFlags.Public)
                        ?.SetValue(presentation, true);
                }

                Assert.That(view.IsHandoffCoverRendered, Is.True);
                Assert.That(view.BeginHandoffFade(), Is.True);
                view.AdvanceResultTransition(runtime.ResultContentEntranceDelay);
                Assert.That(view.CanBeginContentEntrance, Is.True);
                Assert.That(view.BeginContentEntrance(), Is.True);
                Assert.That(primaryButton.interactable, Is.False);
                view.AdvanceResultTransition(runtime.ContentEntranceDuration * 0.5f);
                Assert.That(content.alpha, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That(backdrop.color, Is.EqualTo(dim.FinalColor));
                Assert.That(primaryButton.interactable, Is.False);
                view.AdvanceResultTransition(
                    runtime.ContentEntranceDuration +
                    runtime.ResultHandoffFadeDuration);
                Assert.That(view.IsInteractionReady, Is.True);
                Assert.That(view.IsHandoffFadeComplete, Is.True);
                Assert.That(view.IsContentEntranceComplete, Is.True);
                Assert.That(handoffCover.gameObject.activeSelf, Is.False);
                Assert.That(content.alpha, Is.EqualTo(1f));
                Assert.That(content.interactable, Is.True);
                Assert.That(content.blocksRaycasts, Is.True);
                Assert.That(primaryButton.interactable, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
                UnityEngine.Object.DestroyImmediate(canvasRoot);
            }
        }

        private static void AssertPausePopupButtonHasSingleHoverScaleEffect(PausePopupView pausePopup, string buttonName)
        {
            var button = FindChildByName(pausePopup.transform, buttonName);
            Assert.That(button, Is.Not.Null, buttonName);
            Assert.That(button.GetComponents<UiHoverScaleEffect>().Length, Is.EqualTo(1), buttonName);
            Assert.That(FindChildByName(button, "SelectionFrame"), Is.Not.Null, buttonName);
        }

        private static void SetProfileFloat(
            TerminalIrisMotionProfile profile,
            string presetProperty,
            string valueProperty,
            float value)
        {
            var property = FindProfileProperty(profile, presetProperty, valueProperty);
            property.floatValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetProfileColor(
            TerminalIrisMotionProfile profile,
            string presetProperty,
            string valueProperty,
            Color value)
        {
            var property = FindProfileProperty(profile, presetProperty, valueProperty);
            property.colorValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetProfileEdgeFloat(
            TerminalIrisMotionProfile profile,
            string presetProperty,
            string valueProperty,
            float value)
        {
            var property = FindProfileEdgeProperty(profile, presetProperty, valueProperty);
            property.floatValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TerminalIrisRuntimePreset? ResolveSourceCloseForRoute(
            TerminalIrisMotionProfileResolver resolver,
            TerminalIrisSourceRoute route)
        {
            return route switch
            {
                TerminalIrisSourceRoute.Victory =>
                    resolver.ResolveClose(TerminalTransitionKind.Victory),
                TerminalIrisSourceRoute.ActualDefeat =>
                    resolver.ResolveClose(TerminalTransitionKind.Defeat),
                TerminalIrisSourceRoute.ManualRetry =>
                    resolver.ResolveRetryClose(SceneTransitionIntent.ManualRetry),
                TerminalIrisSourceRoute.LevelFailedRestart =>
                    resolver.ResolveRetryClose(SceneTransitionIntent.ManualRetry),
                TerminalIrisSourceRoute.DemoStageRelaunch =>
                    resolver.ResolveRetryClose(SceneTransitionIntent.DemoStageRelaunch),
                TerminalIrisSourceRoute.GameplayEntry =>
                    resolver.ResolveGameplayEntrySourceClose(
                        SceneTransitionIntent.GameplayEntry),
                TerminalIrisSourceRoute.ReturnToMainMenu =>
                    resolver.ResolveGameplayEntrySourceClose(
                        SceneTransitionIntent.ReturnToMainMenu),
                TerminalIrisSourceRoute.DeathRetry => null,
                _ => throw new ArgumentOutOfRangeException(nameof(route), route, null),
            };
        }

        private static void AssertCloseTiming(
            TerminalIrisRuntimePreset preset,
            float focus,
            float hold,
            float close)
        {
            Assert.That(preset.FocusDuration, Is.EqualTo(focus).Within(0.000001f));
            Assert.That(preset.HoldDuration, Is.EqualTo(hold).Within(0.000001f));
            Assert.That(preset.CloseDuration, Is.EqualTo(close).Within(0.000001f));
            Assert.That(
                preset.BlackAt,
                Is.EqualTo(focus + hold + close).Within(0.000001f));
        }

        private static void SetProfileEdgeColor(
            TerminalIrisMotionProfile profile,
            string presetProperty,
            string valueProperty,
            Color value)
        {
            var property = FindProfileEdgeProperty(profile, presetProperty, valueProperty);
            property.colorValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetProfileEnum(
            TerminalIrisMotionProfile profile,
            string presetProperty,
            string valueProperty,
            TerminalIrisEasing value)
        {
            var property = FindProfileProperty(profile, presetProperty, valueProperty);
            property.intValue = (int)value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty FindProfileProperty(
            TerminalIrisMotionProfile profile,
            string presetProperty,
            string valueProperty)
        {
            var serialized = new SerializedObject(profile);
            var preset = serialized.FindProperty(presetProperty);
            Assert.That(preset, Is.Not.Null, presetProperty);
            var property = preset.FindPropertyRelative(valueProperty);
            Assert.That(property, Is.Not.Null, $"{presetProperty}.{valueProperty}");
            return property;
        }

        private static SerializedProperty FindProfileEdgeProperty(
            TerminalIrisMotionProfile profile,
            string presetProperty,
            string valueProperty)
        {
            var edge = FindProfileProperty(profile, presetProperty, "_edge");
            var property = edge.FindPropertyRelative(valueProperty);
            Assert.That(property, Is.Not.Null, $"{presetProperty}._edge.{valueProperty}");
            return property;
        }

        private static int CountMissingScripts(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject));
        }

        private static int CountOccurrences(string source, string value)
        {
            return source.Split(new[] { value }, StringSplitOptions.None).Length - 1;
        }

        private static void DestroySceneObject(string objectName)
        {
            var sceneObject = GameObject.Find(objectName);
            if (sceneObject != null)
            {
                UnityEngine.Object.DestroyImmediate(sceneObject);
            }
        }
    }
}

using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class ComicSequenceFlowTests
    {
        private const string IntroSequencePath =
            "Assets/_Features/UI/UI_Composition/Authoring/ComicSequences/Intro/" +
            "IntroComicSequence_CampaignMain.asset";
        private const string OutroSequencePath =
            "Assets/_Features/UI/UI_Composition/Authoring/ComicSequences/Outro/" +
            "OutroComicSequence_CampaignMain.asset";
        private const string GameplayScenePath =
            "Assets/Scenes/UIAudioScene.unity";

        [SetUp]
        public void SetUp()
        {
            TerminalSessionRegistry.ResetForTests();
            ComicSequenceOpaqueHandoffRegistry.ResetForTests();
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                9812,
                nameof(ComicSequenceFlowTests));
        }

        [TearDown]
        public void TearDown()
        {
            ComicSequenceOpaqueHandoffRegistry.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
        }

        [Test]
        public void ProductionIntroSequence_UsesSelectedPanels_FinalPair_AndAudio()
        {
            var definition = LoadProductionDefinition();

            Assert.That(definition.TryValidate(out var failureReason), Is.True, failureReason);
            Assert.That(definition.Pages, Has.Length.EqualTo(2));
            Assert.That(definition.Pages[0].Panels, Has.Length.EqualTo(5));
            Assert.That(definition.Pages[1].Panels, Has.Length.EqualTo(6));
            Assert.That(
                Array.ConvertAll(definition.Pages[1].Panels, panel => panel.Sprite.name),
                Is.EqualTo(new[] { "2-1", "2-2-2", "2-4-1", "2-3", "2-5", "2-6" }));
            Assert.That(
                Array.ConvertAll(
                    definition.Pages[1].Panels,
                    panel => panel.ReplacementSprite != null
                        ? panel.ReplacementSprite.name
                        : null),
                Is.EqualTo(new[] { null, null, "2-4-2", null, null, null }));
            Assert.That(definition.FinalTransitionBeforeSprite.name, Is.EqualTo("3-1"));
            Assert.That(definition.FinalTransitionAfterSprite.name, Is.EqualTo("3-2"));
            Assert.That(definition.AudioClip, Is.Not.Null);

            foreach (var page in definition.Pages)
            {
                foreach (var panel in page.Panels)
                {
                    AssertUiSpriteImport(panel.Sprite);
                    if (panel.ReplacementSprite != null)
                    {
                        AssertUiSpriteImport(panel.ReplacementSprite);
                    }
                }
            }

            AssertUiSpriteImport(definition.FinalTransitionBeforeSprite);
            AssertUiSpriteImport(definition.FinalTransitionAfterSprite);
            Assert.That(
                definition.FinalTransitionBeforeSprite.rect.width /
                definition.FinalTransitionBeforeSprite.rect.height,
                Is.EqualTo(ComicSequenceDefinition.FinalTransitionAspectRatio)
                    .Within(0.005f));
        }

        [Test]
        public void ProductionOutroSequence_UsesAuthoredSixPanelOrderAndSceneWiring()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ComicSequenceDefinition>(
                OutroSequencePath);
            Assert.That(definition, Is.Not.Null, $"Missing {OutroSequencePath}");
            Assert.That(definition.TryValidate(out var failureReason), Is.True, failureReason);
            Assert.That(definition.Pages, Has.Length.EqualTo(1));
            Assert.That(definition.Pages[0].Panels, Has.Length.EqualTo(6));
            Assert.That(
                Array.ConvertAll(definition.Pages[0].Panels, panel => panel.Sprite.name),
                Is.EqualTo(new[] { "1-1", "1-2", "1-3", "1-4", "1-5", "1-6" }));
            Assert.That(
                Array.ConvertAll(definition.Pages[0].Panels, panel => panel.HasReplacement),
                Is.All.False);
            Assert.That(
                Array.ConvertAll(definition.Pages[0].Panels, panel => panel.ReferenceRect),
                Is.EqualTo(new[]
                {
                    new Rect(0f, 0f, 704f, 440f),
                    new Rect(103f, 526f, 853f, 480f),
                    new Rect(1085f, 73.17969f, 83.00781f, 337.8906f),
                    new Rect(1194.8633f, 39f, 640.1367f, 405.76172f),
                    new Rect(982f, 535f, 866f, 451f),
                    new Rect(1448f, 1004f, 448f, 50f),
                }));
            Assert.That(definition.HasFinalTransition, Is.False);
            Assert.That(definition.AudioClip, Is.Null);
            foreach (var panel in definition.Pages[0].Panels)
            {
                AssertUiSpriteImport(panel.Sprite);
            }

            var projectRoot = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, ".."));
            var sceneText = System.IO.File.ReadAllText(
                System.IO.Path.Combine(projectRoot, GameplayScenePath));

            Assert.That(
                sceneText,
                Does.Contain(
                    "_outroComicSequence: {fileID: 11400000, " +
                    "guid: a4c8e2f1d7634b55a3f0e6c91b72d001, type: 2}"));

        }

        [Test]
        public void OverlayPrefab_AuthorsFixedShell_AndLeavesPanelsMountEmpty()
        {
            var view = CreateOverlayInstance();
            try
            {
                Assert.DoesNotThrow(view.EnsureHierarchy);
                Assert.That(view.gameObject.activeSelf, Is.False);
                Assert.That(view.GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(view.GetComponent<AudioSource>(), Is.Not.Null);
                Assert.That(view.PanelImages, Is.Empty);
                FindSinglePointerTargetImage(view);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Overlay_InvalidAuthoredReference_FailsWithoutRepairingHierarchy()
        {
            var view = CreateOverlayInstance();
            try
            {
                var authoredTransforms =
                    view.GetComponentsInChildren<Transform>(includeInactive: true);
                var serializedView = new SerializedObject(view);
                var blackFadeProperty = serializedView.FindProperty("_blackFadeImage");
                Assert.That(blackFadeProperty, Is.Not.Null);
                var authoredBlackFade = blackFadeProperty.objectReferenceValue;
                Assert.That(authoredBlackFade, Is.Not.Null);
                blackFadeProperty.objectReferenceValue = null;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    () => view.EnsureHierarchy(),
                    Throws.InvalidOperationException);
                Assert.That(
                    view.GetComponentsInChildren<Transform>(includeInactive: true),
                    Is.EqualTo(authoredTransforms));
                Assert.That(authoredBlackFade, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        [TestCase(typeof(MainMenuUiFlowInstaller))]
        [TestCase(typeof(GameplayUiFlowInstaller))]
        public void ProductionInstaller_MissingOverlayPrefab_FailsBeforeCreatingRuntimeShell(
            Type installerType)
        {
            var root = new GameObject(
                nameof(ProductionInstaller_MissingOverlayPrefab_FailsBeforeCreatingRuntimeShell));
            try
            {
                var installer = root.AddComponent(installerType);
                InvokePublicInstallExpectingFailure(installer);
                Assert.That(
                    root.GetComponentsInChildren<ComicSequenceOverlayView>(true),
                    Is.Empty);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(typeof(MainMenuUiFlowInstaller))]
        [TestCase(typeof(GameplayUiFlowInstaller))]
        public void ProductionInstaller_MalformedOverlayPrefab_FailsBeforePartialInstall(
            Type installerType)
        {
            var root = new GameObject(
                nameof(ProductionInstaller_MalformedOverlayPrefab_FailsBeforePartialInstall));
            var malformedPrefab = CreateOverlayInstance();
            try
            {
                FindSinglePointerTargetImage(malformedPrefab).color = Color.red;
                var installer = root.AddComponent(installerType);
                var serializedInstaller = new SerializedObject(installer);
                serializedInstaller.FindProperty("_comicSequenceOverlayPrefab")
                    .objectReferenceValue = malformedPrefab;
                serializedInstaller.ApplyModifiedPropertiesWithoutUndo();

                InvokePublicInstallExpectingFailure(installer);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(
                    root.GetComponentsInChildren<ComicSequenceOverlayView>(true),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(malformedPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Overlay_PointerAdvance_IsExactlyOnceAndTransitionInputIsIgnored()
        {
            var definition = CreateDefinitionWithoutAudio();
            var view = CreateOverlayInstance();
            try
            {
                view.Present(definition, default, _ => { });
                SettleFade(view);
                SettleFade(view);
                var initialVisiblePanelCount = view.VisiblePanelCount;

                view.OnPointerClick(new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Right,
                });
                Assert.That(view.VisiblePanelCount, Is.EqualTo(initialVisiblePanelCount));

                var leftClick = new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left,
                };
                view.OnPointerClick(leftClick);
                view.OnPointerClick(leftClick);

                Assert.That(
                    view.VisiblePanelCount,
                    Is.EqualTo(initialVisiblePanelCount + 1));
                Assert.That(
                    view.CurrentPresentationState,
                    Is.EqualTo(ComicSequencePresentationState.Revealing));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Overlay_EnterFade_TransitionsFromSourceSceneToOpaqueBlack()
        {
            var definition = CreateDefinitionWithoutAudio();
            var view = CreateOverlayInstance();
            try
            {
                view.Present(definition, default, _ => { });

                var backgroundImage = FindSinglePointerTargetImage(view);
                var enterFadeDuration = definition.Timing.EnterFadeDuration;
                Assert.That(enterFadeDuration, Is.GreaterThan(0f));
                Assert.That(backgroundImage.color.a, Is.Zero);
                Assert.That(view.CurrentFadeAlpha, Is.Zero);

                view.AdvanceForTesting(enterFadeDuration * 0.5f);

                Assert.That(backgroundImage.color.a, Is.Zero);
                Assert.That(view.CurrentFadeAlpha, Is.GreaterThan(0f));
                Assert.That(view.CurrentFadeAlpha, Is.LessThan(1f));

                view.AdvanceForTesting(enterFadeDuration);

                Assert.That(backgroundImage.color.a, Is.EqualTo(1f));
                Assert.That(view.CurrentFadeAlpha, Is.EqualTo(1f));
                Assert.That(view.CurrentPageIndex, Is.Zero);
                Assert.That(view.VisiblePanelCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Overlay_ReplacesSecondPagePanel_ThenCompletesAfterFourteenAdvances()
        {
            var definition = CreateDefinitionWithoutAudio();
            var view = CreateOverlayInstance();
            try
            {
                ComicSequenceResult? completion = null;

                view.Present(definition, default, result => completion = result);
                SettleFade(view);
                SettleFade(view);

                Assert.That(view.CurrentPresentationState,
                    Is.EqualTo(ComicSequencePresentationState.AwaitingAdvance));
                Assert.That(view.CurrentPageIndex, Is.EqualTo(0));
                Assert.That(view.VisiblePanelCount, Is.EqualTo(1));
                var firstPooledPanel = view.PanelImages[0];

                var advanceCount = 0;
                for (var panel = 1; panel < 5; panel++)
                {
                    view.RequestAdvance();
                    advanceCount++;
                    view.RequestAdvance();
                    Assert.That(view.CurrentPresentationState,
                        Is.EqualTo(ComicSequencePresentationState.Revealing));
                    SettleFade(view);
                }

                Assert.That(view.VisiblePanelCount, Is.EqualTo(5));
                view.RequestAdvance();
                advanceCount++;
                SettleFade(view);
                SettleFade(view);
                Assert.That(view.CurrentPageIndex, Is.EqualTo(1));
                Assert.That(view.VisiblePanelCount, Is.EqualTo(1));

                for (var panel = 1; panel < 3; panel++)
                {
                    view.RequestAdvance();
                    advanceCount++;
                    SettleFade(view);
                }

                Assert.That(view.VisiblePanelCount, Is.EqualTo(3));
                Assert.That(view.PanelImages[2].sprite.name, Is.EqualTo("2-4-1"));
                Assert.That(view.CurrentFadeAlpha, Is.Zero);
                Assert.That(view.PanelImages[0].color.a, Is.EqualTo(1f));
                Assert.That(view.PanelImages[1].color.a, Is.EqualTo(1f));

                view.RequestAdvance();
                advanceCount++;
                view.RequestAdvance();
                Assert.That(view.CurrentPresentationState,
                    Is.EqualTo(ComicSequencePresentationState.PanelSwap));
                Assert.That(view.CurrentFadeAlpha, Is.Zero);

                view.AdvanceForTesting(
                    definition.Timing.FinalSwapFadeOutDuration * 0.5f);
                Assert.That(view.PanelImages[2].color.a, Is.GreaterThan(0f));
                Assert.That(view.PanelImages[2].color.a, Is.LessThan(1f));
                Assert.That(view.PanelImages[2].sprite.name, Is.EqualTo("2-4-1"));
                Assert.That(view.PanelImages[0].color.a, Is.EqualTo(1f));
                Assert.That(view.PanelImages[1].color.a, Is.EqualTo(1f));
                Assert.That(view.CurrentFadeAlpha, Is.Zero);

                SettleFade(view);
                Assert.That(view.PanelImages[2].sprite.name, Is.EqualTo("2-4-2"));
                Assert.That(view.PanelImages[2].color.a, Is.Zero);
                Assert.That(view.CurrentPresentationState,
                    Is.EqualTo(ComicSequencePresentationState.PanelSwap));
                Assert.That(view.CurrentFadeAlpha, Is.Zero);

                SettleFade(view);

                Assert.That(view.VisiblePanelCount, Is.EqualTo(3));
                Assert.That(view.PanelImages[2].sprite.name, Is.EqualTo("2-4-2"));
                Assert.That(view.PanelImages[2].color.a, Is.EqualTo(1f));
                Assert.That(view.PanelImages[0].color.a, Is.EqualTo(1f));
                Assert.That(view.PanelImages[1].color.a, Is.EqualTo(1f));
                Assert.That(view.CurrentFadeAlpha, Is.Zero);
                Assert.That(view.CurrentPresentationState,
                    Is.EqualTo(ComicSequencePresentationState.AwaitingAdvance));

                for (var panel = 3; panel < 6; panel++)
                {
                    view.RequestAdvance();
                    advanceCount++;
                    SettleFade(view);
                }

                view.RequestAdvance();
                advanceCount++;
                SettleFade(view);
                SettleFade(view);
                Assert.That(view.IsFinalTransitionBeforeDisplayed, Is.True);

                view.RequestAdvance();
                advanceCount++;
                SettleFade(view);
                SettleFade(view);
                SettleFade(view);
                Assert.That(view.IsFinalTransitionAfterDisplayed, Is.True);

                view.RequestAdvance();
                advanceCount++;
                SettleFade(view);

                Assert.That(advanceCount, Is.EqualTo(14));
                Assert.That(view.IsPresenting, Is.False);
                Assert.That(view.CurrentPresentationState,
                    Is.EqualTo(ComicSequencePresentationState.Completed));
                Assert.That(completion.HasValue, Is.True);
                Assert.That(completion.Value.Kind,
                    Is.EqualTo(ComicSequenceResultKind.Completed));

                view.Present(definition, default, _ => { });
                Assert.That(view.PanelImages[0], Is.SameAs(firstPooledPanel));
                Assert.That(view.PanelImages, Has.Count.EqualTo(6));
                Assert.That(view.PanelImages, Has.All.Matches<Image>(image =>
                    !image.raycastTarget && image.preserveAspect));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void PanelLayout_IsResolutionIndependent_AndDoesNotUseNativeSpriteSize()
        {
            var definition = CreateDefinitionWithoutAudio();
            var largeTexture = new Texture2D(2048, 1024);
            var largeSprite = Sprite.Create(
                largeTexture,
                new Rect(0f, 0f, largeTexture.width, largeTexture.height),
                new Vector2(0.5f, 0.5f));
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition
                .FindProperty("_pages.Array.data[0]._panels.Array.data[0]._sprite")
                .objectReferenceValue = largeSprite;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            var view = CreateOverlayInstance();
            try
            {
                view.Present(definition, default, _ => { });
                SettleFade(view);
                SettleFade(view);

                var panelRect = view.PanelImages[0].rectTransform;
                var expectedRect = definition.Pages[0].Panels[0].ReferenceRect;
                Assert.That(panelRect.anchorMin.x,
                    Is.EqualTo(expectedRect.xMin / 1920f).Within(0.0001f));
                Assert.That(panelRect.anchorMin.y,
                    Is.EqualTo(1f - expectedRect.yMax / 1080f).Within(0.0001f));
                Assert.That(panelRect.anchorMax.x,
                    Is.EqualTo(expectedRect.xMax / 1920f).Within(0.0001f));
                Assert.That(panelRect.anchorMax.y,
                    Is.EqualTo(1f - expectedRect.yMin / 1080f).Within(0.0001f));
                Assert.That(panelRect.sizeDelta, Is.EqualTo(Vector2.zero));
                Assert.That(view.PanelImages[0].preserveAspect, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(largeSprite);
                UnityEngine.Object.DestroyImmediate(largeTexture);
            }
        }

        [Test]
        public void Coordinator_OnlyTakesAudioFocus_WhenSequenceProvidesAudio()
        {
            var withAudio = LoadProductionDefinition();
            var withoutAudio = CreateDefinitionWithoutAudio();
            var overlayObject = new GameObject(
                nameof(Coordinator_OnlyTakesAudioFocus_WhenSequenceProvidesAudio));
            try
            {
                var audioSource = overlayObject.AddComponent<AudioSource>();
                var focus = new RecordingAudioFocusOwner();
                var overlay = new RecordingComicOverlay(audioSource);
                var withAudioCoordinator = new ComicSequenceFlowCoordinator(
                    withAudio,
                    null,
                    overlay,
                    null,
                    focus);

                withAudioCoordinator.PresentIntro(_ => { });
                Assert.That(focus.BeginCount, Is.EqualTo(1));

                ComicSequenceOpaqueHandoffRegistry.ResetForTests();
                var withoutAudioCoordinator = new ComicSequenceFlowCoordinator(
                    withoutAudio,
                    null,
                    overlay,
                    null,
                    focus);
                withoutAudioCoordinator.PresentIntro(_ => { });

                Assert.That(focus.BeginCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlayObject);
                UnityEngine.Object.DestroyImmediate(withoutAudio);
            }
        }

        [Test]
        public void AudioFocus_PlaybackVolume_CombinesMasterBgmAndComicFade()
        {
            var snapshot = AudioSettingsSnapshot.Default
                .WithChannelState(
                    AudioChannel.Master,
                    new AudioChannelState(0.8f, false))
                .WithChannelState(
                    AudioChannel.Bgm,
                    new AudioChannelState(0.25f, false));

            Assert.That(
                ComicSequenceAudioFocusController.ResolvePlaybackVolume(snapshot, 0.5f),
                Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void AudioFocus_PlaybackMute_FollowsMasterOrBgmMute()
        {
            var masterMuted = AudioSettingsSnapshot.Default.WithChannelState(
                AudioChannel.Master,
                new AudioChannelState(1f, true));
            var bgmMuted = AudioSettingsSnapshot.Default.WithChannelState(
                AudioChannel.Bgm,
                new AudioChannelState(1f, true));

            Assert.That(
                ComicSequenceAudioFocusController.ResolvePlaybackMuted(masterMuted),
                Is.True);
            Assert.That(
                ComicSequenceAudioFocusController.ResolvePlaybackMuted(bgmMuted),
                Is.True);
            Assert.That(
                ComicSequenceAudioFocusController.ResolvePlaybackMuted(
                    AudioSettingsSnapshot.Default),
                Is.False);
        }

        [Test]
        public void Coordinator_ClaimConflict_DoesNotTouchAudioFocus()
        {
            var definition = LoadProductionDefinition();
            var overlayObject = new GameObject(
                nameof(Coordinator_ClaimConflict_DoesNotTouchAudioFocus));
            try
            {
                Assert.That(
                    ComicSequenceOpaqueHandoffRegistry.TryClaim(
                        SceneTransitionIntent.ComicOutroToMainMenu,
                        TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                        Color.black,
                        () => { },
                        out _),
                    Is.True);
                var focus = new RecordingAudioFocusOwner();
                var overlay = new RecordingComicOverlay(
                    overlayObject.AddComponent<AudioSource>());
                var coordinator = new ComicSequenceFlowCoordinator(
                    definition,
                    null,
                    overlay,
                    null,
                    focus);
                ComicSequenceResult? completion = null;

                coordinator.PresentIntro(result => completion = result);

                Assert.That(completion.HasValue, Is.True);
                Assert.That(
                    completion.Value.Kind,
                    Is.EqualTo(ComicSequenceResultKind.Failed));
                Assert.That(focus.BeginCount, Is.Zero);
                Assert.That(focus.EndCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlayObject);
            }
        }

        [Test]
        public void Coordinator_OverlaySetupFailure_ReleasesClaimAndAudioFocus()
        {
            var definition = LoadProductionDefinition();
            var overlayObject = new GameObject(
                nameof(Coordinator_OverlaySetupFailure_ReleasesClaimAndAudioFocus));
            try
            {
                var focus = new RecordingAudioFocusOwner();
                var overlay = new RecordingComicOverlay(
                    overlayObject.AddComponent<AudioSource>())
                {
                    PresentException = new InvalidOperationException("injected setup failure"),
                };
                var coordinator = new ComicSequenceFlowCoordinator(
                    definition,
                    null,
                    overlay,
                    null,
                    focus);

                Assert.Throws<InvalidOperationException>(() =>
                    coordinator.PresentIntro(_ => { }));

                Assert.That(focus.BeginCount, Is.EqualTo(1));
                Assert.That(focus.EndCount, Is.EqualTo(1));
                Assert.That(
                    focus.LastEndMode,
                    Is.EqualTo(ComicSequenceAudioFocusEndMode.RestoreCurrentBgm));
                Assert.That(overlay.AbortCount, Is.EqualTo(1));
                Assert.That(ComicSequenceOpaqueHandoffRegistry.IsActive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlayObject);
            }
        }

        [Test]
        public void Coordinator_CancelledCompletion_RestoresCurrentBgmAfterCallback()
        {
            var definition = LoadProductionDefinition();
            var overlayObject = new GameObject(
                nameof(Coordinator_CancelledCompletion_RestoresCurrentBgmAfterCallback));
            try
            {
                var focus = new RecordingAudioFocusOwner();
                var overlay = new RecordingComicOverlay(
                    overlayObject.AddComponent<AudioSource>());
                var coordinator = new ComicSequenceFlowCoordinator(
                    definition,
                    null,
                    overlay,
                    null,
                    focus);
                var focusWasActiveDuringCallback = false;

                coordinator.PresentIntro(_ =>
                    focusWasActiveDuringCallback = focus.EndCount == 0);
                overlay.Emit(ComicSequenceResultKind.Cancelled);

                Assert.That(focusWasActiveDuringCallback, Is.True);
                Assert.That(focus.EndCount, Is.EqualTo(1));
                Assert.That(
                    focus.LastEndMode,
                    Is.EqualTo(ComicSequenceAudioFocusEndMode.RestoreCurrentBgm));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlayObject);
            }
        }

        [Test]
        public void Coordinator_AcceptedTransition_KeepsBgmStopped()
        {
            var definition = LoadProductionDefinition();
            var overlayObject = new GameObject(
                nameof(Coordinator_AcceptedTransition_KeepsBgmStopped));
            try
            {
                var focus = new RecordingAudioFocusOwner();
                var overlay = new RecordingComicOverlay(
                    overlayObject.AddComponent<AudioSource>());
                var coordinator = new ComicSequenceFlowCoordinator(
                    definition,
                    null,
                    overlay,
                    null,
                    focus);

                coordinator.PresentIntro(_ =>
                    ((IComicSequenceTransitionAudioHandoffOwner)coordinator)
                    .CommitAudioFocusToTransition());
                overlay.Emit(ComicSequenceResultKind.Completed);

                Assert.That(focus.EndCount, Is.EqualTo(1));
                Assert.That(
                    focus.LastEndMode,
                    Is.EqualTo(
                        ComicSequenceAudioFocusEndMode.KeepBgmStoppedForTransition));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlayObject);
            }
        }

        [Test]
        public void Overlay_DisabledMidPresentation_CancelsAndReleasesOpaqueOwner()
        {
            var definition = CreateDefinitionWithoutAudio();
            var view = CreateOverlayInstance();
            try
            {
                ComicSequenceOpaqueHandoffToken token = default;
                Assert.That(
                    ComicSequenceOpaqueHandoffRegistry.TryClaim(
                        SceneTransitionIntent.ComicIntroToGameplay,
                        TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                        Color.black,
                        () => view.ReleaseOpaqueHandoff(token),
                        out token),
                    Is.True);
                ComicSequenceResult? completion = null;
                view.Present(definition, token, result => completion = result);

                view.gameObject.SetActive(false);
                typeof(ComicSequenceOverlayView)
                    .GetMethod(
                        "OnDisable",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(view, null);

                Assert.That(completion.HasValue, Is.True);
                Assert.That(
                    completion.Value.Kind,
                    Is.EqualTo(ComicSequenceResultKind.Cancelled));
                Assert.That(view.IsPresenting, Is.False);
                Assert.That(ComicSequenceOpaqueHandoffRegistry.IsActive, Is.False);
                Assert.That(
                    ComicSequenceOpaqueHandoffRegistry.Current.Phase,
                    Is.EqualTo(ComicSequenceOpaqueHandoffPhase.Released));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Overlay_BackgroundUsesPointerClickWithoutSubmitButton()
        {
            var view = CreateOverlayInstance();
            try
            {
                view.EnsureHierarchy();

                Assert.That(view, Is.InstanceOf<IPointerClickHandler>());
                Assert.That(
                    FindSinglePointerTargetImage(view).GetComponent<Button>(),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        private static ComicSequenceOverlayView CreateOverlayInstance()
        {
            return UnityEngine.Object.Instantiate(
                UiTestPrefabAssetUtility.LoadComicSequenceOverlayPrefab());
        }

        private static Image FindSinglePointerTargetImage(
            ComicSequenceOverlayView view)
        {
            var images = view.GetComponentsInChildren<Image>(includeInactive: true);
            Image pointerTarget = null;
            var pointerTargetCount = 0;
            foreach (var image in images)
            {
                if (!image.raycastTarget)
                {
                    continue;
                }

                pointerTarget = image;
                pointerTargetCount++;
            }

            Assert.That(
                pointerTargetCount,
                Is.EqualTo(1),
                "The authored overlay must expose exactly one pointer raycast target.");
            return pointerTarget;
        }

        private static InvalidOperationException InvokePublicInstallExpectingFailure(
            Component installer)
        {
            if (installer is MainMenuUiFlowInstaller mainMenuInstaller)
            {
                return Assert.Throws<InvalidOperationException>(mainMenuInstaller.Install);
            }

            var gameplayInstaller = (GameplayUiFlowInstaller)installer;
            return Assert.Throws<InvalidOperationException>(
                () => gameplayInstaller.Install(default(GameplayUiFlowPorts)));
        }

        private static ComicSequenceDefinition LoadProductionDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ComicSequenceDefinition>(
                IntroSequencePath);
            Assert.That(definition, Is.Not.Null, $"Missing {IntroSequencePath}");
            return definition;
        }

        private static ComicSequenceDefinition CreateDefinitionWithoutAudio()
        {
            var definition = UnityEngine.Object.Instantiate(LoadProductionDefinition());
            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("_audioClip").objectReferenceValue = null;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static void AssertUiSpriteImport(Sprite sprite)
        {
            var assetPath = AssetDatabase.GetAssetPath(sprite);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, assetPath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), assetPath);
            Assert.That(importer.maxTextureSize, Is.EqualTo(4096), assetPath);
            Assert.That(importer.mipmapEnabled, Is.False, assetPath);
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed),
                assetPath);
        }

        private static void SettleFade(ComicSequenceOverlayView view)
        {
            view.AdvanceForTesting(10f);
        }

        private sealed class RecordingAudioFocusOwner : IComicSequenceAudioFocusOwner
        {
            public int BeginCount { get; private set; }
            public int EndCount { get; private set; }
            public ComicSequenceAudioFocusEndMode? LastEndMode { get; private set; }

            public void BeginFocus(AudioSource comicSequenceAudioSource)
            {
                BeginCount++;
            }

            public void EndFocus(ComicSequenceAudioFocusEndMode endMode)
            {
                EndCount++;
                LastEndMode = endMode;
            }
        }

        private sealed class RecordingComicOverlay : IComicSequenceOverlay
        {
            public RecordingComicOverlay(AudioSource audioSource)
            {
                ComicSequenceAudioSource = audioSource;
            }

            public bool IsPresenting => false;
            public AudioSource ComicSequenceAudioSource { get; }
            public Exception PresentException { get; set; }
            public int AbortCount { get; private set; }
            private Action<ComicSequenceResult> Completion { get; set; }

            public void EnsureHierarchy()
            {
            }

            public void SetAudioFocusController(
                ComicSequenceAudioFocusController audioFocusController)
            {
            }

            public void Present(
                ComicSequenceDefinition definition,
                ComicSequenceOpaqueHandoffToken opaqueHandoffToken,
                Action<ComicSequenceResult> completion)
            {
                if (PresentException != null)
                {
                    throw PresentException;
                }

                Completion = completion;
            }

            public void Emit(ComicSequenceResultKind kind)
            {
                Completion?.Invoke(new ComicSequenceResult(kind));
            }

            public bool AbortSetupAfterFailure(ComicSequenceOpaqueHandoffToken expectedToken)
            {
                AbortCount++;
                return true;
            }

            public void ReleaseOpaqueHandoff(ComicSequenceOpaqueHandoffToken token)
            {
            }
        }
    }
}

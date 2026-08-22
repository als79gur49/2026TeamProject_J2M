using System;
using Game.Feature.Stages;
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
            Assert.That(definition.FinalTransitionBeforeSprite.name, Is.EqualTo("3-1"));
            Assert.That(definition.FinalTransitionAfterSprite.name, Is.EqualTo("3-2"));
            Assert.That(definition.AudioClip, Is.Not.Null);

            foreach (var page in definition.Pages)
            {
                foreach (var panel in page.Panels)
                {
                    AssertUiSpriteImport(panel.Sprite);
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
        public void ProductionGameplayScene_LeavesOutroDefinitionUnassigned()
        {
            var projectRoot = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, ".."));
            var sceneText = System.IO.File.ReadAllText(
                System.IO.Path.Combine(projectRoot, GameplayScenePath));

            Assert.That(
                sceneText,
                Does.Contain("_outroComicSequence: {fileID: 0}"));
            Assert.That(
                sceneText,
                Does.Not.Contain("d58280cd100a46cc89dd187f38310202"),
                "The retired temporary outro definition must not remain as a missing GUID reference.");
        }

        [Test]
        public void Overlay_AutoShowsFirstPanel_ThenCompletesAfterThirteenAdvances()
        {
            var definition = CreateDefinitionWithoutAudio();
            var root = new GameObject(
                nameof(Overlay_AutoShowsFirstPanel_ThenCompletesAfterThirteenAdvances),
                typeof(RectTransform));
            try
            {
                var view = root.AddComponent<ComicSequenceOverlayView>();
                ComicSequenceResult? completion = null;

                view.Present(definition, default, result => completion = result);
                SettleFade(view);
                SettleFade(view);

                Assert.That(view.CurrentPresentationState,
                    Is.EqualTo(ComicSequencePresentationState.AwaitingAdvance));
                Assert.That(view.CurrentPageIndex, Is.EqualTo(0));
                Assert.That(view.VisiblePanelCount, Is.EqualTo(1));

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

                for (var panel = 1; panel < 6; panel++)
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

                Assert.That(advanceCount, Is.EqualTo(13));
                Assert.That(view.IsPresenting, Is.False);
                Assert.That(view.CurrentPresentationState,
                    Is.EqualTo(ComicSequencePresentationState.Completed));
                Assert.That(completion.HasValue, Is.True);
                Assert.That(completion.Value.Kind,
                    Is.EqualTo(ComicSequenceResultKind.Completed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
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

            var root = new GameObject(
                nameof(PanelLayout_IsResolutionIndependent_AndDoesNotUseNativeSpriteSize),
                typeof(RectTransform));
            try
            {
                var view = root.AddComponent<ComicSequenceOverlayView>();
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
                UnityEngine.Object.DestroyImmediate(root);
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
                Assert.That(overlay.AbortCount, Is.EqualTo(1));
                Assert.That(ComicSequenceOpaqueHandoffRegistry.IsActive, Is.False);
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
            var root = new GameObject(
                nameof(Overlay_DisabledMidPresentation_CancelsAndReleasesOpaqueOwner),
                typeof(RectTransform));
            try
            {
                var view = root.AddComponent<ComicSequenceOverlayView>();
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

                root.SetActive(false);
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
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Overlay_BackgroundUsesPointerClickWithoutSubmitButton()
        {
            var root = new GameObject(
                nameof(Overlay_BackgroundUsesPointerClickWithoutSubmitButton),
                typeof(RectTransform));
            try
            {
                var view = root.AddComponent<ComicSequenceOverlayView>();
                view.EnsureHierarchy();

                Assert.That(view, Is.InstanceOf<IPointerClickHandler>());
                Assert.That(root.transform.Find("Background"), Is.Not.Null);
                Assert.That(
                    root.transform.Find("Background").GetComponent<Button>(),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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

            public void BeginFocus(AudioSource comicSequenceAudioSource)
            {
                BeginCount++;
            }

            public void EndFocus()
            {
                EndCount++;
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

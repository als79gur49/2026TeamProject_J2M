using System;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class ComicCinematicFlowTests
    {
        private const string IntroSequencePath =
            "Assets/_Features/UI/UI_Composition/Authoring/Cinematics/Intro/" +
            "IntroComicSequence_CampaignMain.asset";
        private const string OutroSequencePath =
            "Assets/_Features/UI/UI_Composition/Authoring/Cinematics/Outro/" +
            "OutroComicSequence_CampaignMain.asset";

        [SetUp]
        public void SetUp()
        {
            TerminalSessionRegistry.ResetForTests();
            CinematicOpaqueHandoffRegistry.ResetForTests();
            TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                9812,
                nameof(ComicCinematicFlowTests));
        }

        [TearDown]
        public void TearDown()
        {
            CinematicOpaqueHandoffRegistry.ResetForTests();
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
            Assert.That(definition.FinalBeforeSprite.name, Is.EqualTo("3-1"));
            Assert.That(definition.FinalAfterSprite.name, Is.EqualTo("3-2"));
            Assert.That(definition.AudioClip, Is.Not.Null);

            foreach (var page in definition.Pages)
            {
                foreach (var panel in page.Panels)
                {
                    AssertUiSpriteImport(panel.Sprite);
                }
            }

            AssertUiSpriteImport(definition.FinalBeforeSprite);
            AssertUiSpriteImport(definition.FinalAfterSprite);
            Assert.That(
                definition.FinalBeforeSprite.rect.width /
                definition.FinalBeforeSprite.rect.height,
                Is.EqualTo(ComicCinematicSequenceDefinition.FinalShotAspectRatio)
                    .Within(0.005f));
        }

        [Test]
        public void ProductionOutroValidationSequence_IsIndependentCopyOfIntro()
        {
            var intro = LoadProductionDefinition();
            var outro = AssetDatabase.LoadAssetAtPath<ComicCinematicSequenceDefinition>(
                OutroSequencePath);

            Assert.That(outro, Is.Not.Null, $"Missing {OutroSequencePath}");
            Assert.That(outro, Is.Not.SameAs(intro));
            Assert.That(outro.TryValidate(out var failureReason), Is.True, failureReason);
            Assert.That(outro.Pages, Has.Length.EqualTo(intro.Pages.Length));
            for (var pageIndex = 0; pageIndex < intro.Pages.Length; pageIndex++)
            {
                var introPanels = intro.Pages[pageIndex].Panels;
                var outroPanels = outro.Pages[pageIndex].Panels;
                Assert.That(outroPanels, Has.Length.EqualTo(introPanels.Length));
                for (var panelIndex = 0; panelIndex < introPanels.Length; panelIndex++)
                {
                    Assert.That(
                        outroPanels[panelIndex].Sprite,
                        Is.SameAs(introPanels[panelIndex].Sprite));
                    Assert.That(
                        outroPanels[panelIndex].ReferenceRect,
                        Is.EqualTo(introPanels[panelIndex].ReferenceRect));
                }
            }

            Assert.That(outro.FinalBeforeSprite, Is.SameAs(intro.FinalBeforeSprite));
            Assert.That(outro.FinalAfterSprite, Is.SameAs(intro.FinalAfterSprite));
            Assert.That(outro.AudioClip, Is.SameAs(intro.AudioClip));
            Assert.That(outro.Timing, Is.EqualTo(intro.Timing));
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
                var view = root.AddComponent<ComicCinematicOverlayView>();
                CinematicPlaybackCompletion? completion = null;

                view.Play(definition, default, result => completion = result);
                SettleFade(view);
                SettleFade(view);

                Assert.That(view.CurrentPresentationState,
                    Is.EqualTo(ComicCinematicPresentationState.AwaitingAdvance));
                Assert.That(view.CurrentPageIndex, Is.EqualTo(0));
                Assert.That(view.VisiblePanelCount, Is.EqualTo(1));

                var advanceCount = 0;
                for (var panel = 1; panel < 5; panel++)
                {
                    view.RequestAdvance();
                    advanceCount++;
                    view.RequestAdvance();
                    Assert.That(view.CurrentPresentationState,
                        Is.EqualTo(ComicCinematicPresentationState.Revealing));
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
                Assert.That(view.IsFinalBeforeDisplayed, Is.True);

                view.RequestAdvance();
                advanceCount++;
                SettleFade(view);
                SettleFade(view);
                SettleFade(view);
                Assert.That(view.IsFinalAfterDisplayed, Is.True);

                view.RequestAdvance();
                advanceCount++;
                SettleFade(view);

                Assert.That(advanceCount, Is.EqualTo(13));
                Assert.That(view.IsPlaying, Is.False);
                Assert.That(view.CurrentPresentationState,
                    Is.EqualTo(ComicCinematicPresentationState.Completed));
                Assert.That(completion.HasValue, Is.True);
                Assert.That(completion.Value.Kind,
                    Is.EqualTo(CinematicPlaybackCompletionKind.Completed));
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
                var view = root.AddComponent<ComicCinematicOverlayView>();
                view.Play(definition, default, _ => { });
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
                var withAudioCoordinator = new ComicCinematicFlowCoordinator(
                    withAudio,
                    null,
                    overlay,
                    null,
                    focus);

                withAudioCoordinator.PlayIntro(_ => { });
                Assert.That(focus.BeginCount, Is.EqualTo(1));

                CinematicOpaqueHandoffRegistry.ResetForTests();
                var withoutAudioCoordinator = new ComicCinematicFlowCoordinator(
                    withoutAudio,
                    null,
                    overlay,
                    null,
                    focus);
                withoutAudioCoordinator.PlayIntro(_ => { });

                Assert.That(focus.BeginCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlayObject);
                UnityEngine.Object.DestroyImmediate(withoutAudio);
            }
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
                    CinematicOpaqueHandoffRegistry.TryClaim(
                        SceneTransitionIntent.CinematicToMainMenu,
                        TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                        Color.black,
                        () => { },
                        out _),
                    Is.True);
                var focus = new RecordingAudioFocusOwner();
                var overlay = new RecordingComicOverlay(
                    overlayObject.AddComponent<AudioSource>());
                var coordinator = new ComicCinematicFlowCoordinator(
                    definition,
                    null,
                    overlay,
                    null,
                    focus);
                CinematicPlaybackCompletion? completion = null;

                coordinator.PlayIntro(result => completion = result);

                Assert.That(completion.HasValue, Is.True);
                Assert.That(
                    completion.Value.Kind,
                    Is.EqualTo(CinematicPlaybackCompletionKind.Failed));
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
                    PlayException = new InvalidOperationException("injected setup failure"),
                };
                var coordinator = new ComicCinematicFlowCoordinator(
                    definition,
                    null,
                    overlay,
                    null,
                    focus);

                Assert.Throws<InvalidOperationException>(() =>
                    coordinator.PlayIntro(_ => { }));

                Assert.That(focus.BeginCount, Is.EqualTo(1));
                Assert.That(focus.EndCount, Is.EqualTo(1));
                Assert.That(overlay.AbortCount, Is.EqualTo(1));
                Assert.That(CinematicOpaqueHandoffRegistry.IsActive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlayObject);
            }
        }

        [Test]
        public void Overlay_DisabledMidPlayback_CancelsAndReleasesOpaqueOwner()
        {
            var definition = CreateDefinitionWithoutAudio();
            var root = new GameObject(
                nameof(Overlay_DisabledMidPlayback_CancelsAndReleasesOpaqueOwner),
                typeof(RectTransform));
            try
            {
                var view = root.AddComponent<ComicCinematicOverlayView>();
                CinematicOpaqueHandoffToken token = default;
                Assert.That(
                    CinematicOpaqueHandoffRegistry.TryClaim(
                        SceneTransitionIntent.CinematicToGameplay,
                        TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                        Color.black,
                        () => view.ReleaseOpaqueHandoff(token),
                        out token),
                    Is.True);
                CinematicPlaybackCompletion? completion = null;
                view.Play(definition, token, result => completion = result);

                root.SetActive(false);
                typeof(ComicCinematicOverlayView)
                    .GetMethod(
                        "OnDisable",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(view, null);

                Assert.That(completion.HasValue, Is.True);
                Assert.That(
                    completion.Value.Kind,
                    Is.EqualTo(CinematicPlaybackCompletionKind.Cancelled));
                Assert.That(view.IsPlaying, Is.False);
                Assert.That(CinematicOpaqueHandoffRegistry.IsActive, Is.False);
                Assert.That(
                    CinematicOpaqueHandoffRegistry.Current.Phase,
                    Is.EqualTo(CinematicOpaqueHandoffPhase.Released));
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
                var view = root.AddComponent<ComicCinematicOverlayView>();
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

        private static ComicCinematicSequenceDefinition LoadProductionDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ComicCinematicSequenceDefinition>(
                IntroSequencePath);
            Assert.That(definition, Is.Not.Null, $"Missing {IntroSequencePath}");
            return definition;
        }

        private static ComicCinematicSequenceDefinition CreateDefinitionWithoutAudio()
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

        private static void SettleFade(ComicCinematicOverlayView view)
        {
            view.AdvanceForTesting(10f);
        }

        private sealed class RecordingAudioFocusOwner : ICinematicAudioFocusOwner
        {
            public int BeginCount { get; private set; }
            public int EndCount { get; private set; }

            public void BeginFocus(AudioSource cinematicAudioSource)
            {
                BeginCount++;
            }

            public void EndFocus()
            {
                EndCount++;
            }
        }

        private sealed class RecordingComicOverlay : IComicCinematicPlaybackOverlay
        {
            public RecordingComicOverlay(AudioSource audioSource)
            {
                CinematicAudioSource = audioSource;
            }

            public bool IsPlaying => false;
            public AudioSource CinematicAudioSource { get; }
            public Exception PlayException { get; set; }
            public int AbortCount { get; private set; }

            public void EnsureHierarchy()
            {
            }

            public void SetAudioFocusController(
                CinematicAudioFocusController audioFocusController)
            {
            }

            public void Play(
                ComicCinematicSequenceDefinition definition,
                CinematicOpaqueHandoffToken opaqueHandoffToken,
                Action<CinematicPlaybackCompletion> completion)
            {
                if (PlayException != null)
                {
                    throw PlayException;
                }
            }

            public bool AbortSetupAfterFailure(CinematicOpaqueHandoffToken expectedToken)
            {
                AbortCount++;
                return true;
            }

            public void ReleaseOpaqueHandoff(CinematicOpaqueHandoffToken token)
            {
            }
        }
    }
}

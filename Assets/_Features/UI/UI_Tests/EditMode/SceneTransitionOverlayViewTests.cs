using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class SceneTransitionOverlayViewTests
    {
        private const string OverlayPrefabPath =
            "Assets/_Features/UI/UI_Composition/Resources/UI/SceneTransitionOverlayView.prefab";

        [TearDown]
        public void TearDown()
        {
            SceneTransitionCoordinator.SetOverlayResourceLoaderForTests(null);
            SceneTransitionCoordinator.SetOverlayShellResourceLoaderForTests(null);
            SceneTransitionCoordinator.SetContentCatalogResourceLoaderForTests(null);
            SceneTransitionCoordinator.SetContentResourceLoaderForTests(null);
        }

        [Test]
        public void SceneTransitionOverlayView_ShowBlockerOnly_HidesVisualRoot()
        {
            using var harness = SceneTransitionOverlayViewHarness.Create();

            harness.View.ShowBlockerOnly(true);

            Assert.That(harness.Root.activeSelf, Is.True);
            Assert.That(harness.Blocker.activeSelf, Is.True);
            Assert.That(harness.BlockerImage.raycastTarget, Is.True);
            Assert.That(harness.VisualRoot.activeSelf, Is.False);
            Assert.That(harness.ChanceLostGroup.activeSelf, Is.False);
        }

        [Test]
        public void SceneTransitionOverlayView_ShowChanceLost_BindsTmpTexts()
        {
            using var harness = SceneTransitionOverlayViewHarness.Create();
            var model = new SceneTransitionOverlayViewModel(
                TransitionOverlayKind.ChanceLost,
                "Chance Lost",
                "Retrying.",
                blockInput: true,
                showProgress: true,
                progress01: 0.25f,
                hasChanceLost: true,
                previousRemainingChances: 2,
                currentRemainingChances: 1,
                totalChances: 3,
                deathCount: 4);

            harness.View.ShowOverlay(model);

            Assert.That(harness.VisualRoot.activeSelf, Is.True);
            Assert.That(harness.ChanceLostGroup.activeSelf, Is.True);
            Assert.That(harness.GenericLoadingGroup.activeSelf, Is.False);
            Assert.That(harness.TitleText.text, Is.EqualTo("Chance Lost"));
            Assert.That(harness.MessageText.text, Is.EqualTo("Retrying."));
            Assert.That(harness.PreviousChanceText.text, Is.EqualTo("2"));
            Assert.That(harness.CurrentChanceText.text, Is.EqualTo("1"));
            Assert.That(harness.TotalChanceText.text, Is.EqualTo("/ 3"));
            Assert.That(harness.DeathCountText.text, Is.EqualTo("Deaths 4"));
        }

        [Test]
        public void SceneTransitionOverlayView_SetProgress_Clamps01()
        {
            using var harness = SceneTransitionOverlayViewHarness.Create();
            harness.View.ShowOverlay(new SceneTransitionOverlayViewModel(
                TransitionOverlayKind.GenericLoading,
                "Loading",
                "Preparing.",
                blockInput: true,
                showProgress: true,
                progress01: 0f,
                hasChanceLost: false,
                previousRemainingChances: 0,
                currentRemainingChances: 0,
                totalChances: 0,
                deathCount: 0));

            harness.View.SetProgress(-1f);
            Assert.That(harness.ProgressFill.anchorMax.x, Is.EqualTo(0f));
            harness.View.SetProgress(0.5f);
            Assert.That(harness.ProgressFill.anchorMax.x, Is.EqualTo(0.5f));
            harness.View.SetProgress(2f);
            Assert.That(harness.ProgressFill.anchorMax.x, Is.EqualTo(1f));
        }

        [Test]
        public void SceneTransitionOverlayView_HideAll_DisablesBlockerAndVisual()
        {
            using var harness = SceneTransitionOverlayViewHarness.Create();

            harness.View.ShowBlockerOnly(true);
            harness.View.HideAll();

            Assert.That(harness.Root.activeSelf, Is.False);
            Assert.That(harness.Blocker.activeSelf, Is.False);
            Assert.That(harness.VisualRoot.activeSelf, Is.False);
            Assert.That(harness.ChanceLostGroup.activeSelf, Is.False);
        }

        [Test]
        public void SceneTransitionOverlayView_RejectsEventSystemChild()
        {
            using var harness = SceneTransitionOverlayViewHarness.Create();
            new GameObject("EventSystem", typeof(EventSystem)).transform.SetParent(harness.Root.transform, false);

            Assert.That(
                harness.View.CollectValidationIssues(),
                Has.Some.Contains("EventSystem"));
        }

        [Test]
        public void SceneTransitionOverlayView_RejectsAudioRuntimeRootChild()
        {
            using var harness = SceneTransitionOverlayViewHarness.Create();
            new GameObject("AudioRuntimeRoot", typeof(AudioRuntimeRoot)).transform.SetParent(harness.Root.transform, false);

            Assert.That(
                harness.View.CollectValidationIssues(),
                Has.Some.Contains("AudioRuntimeRoot"));
        }

        [Test]
        public void SceneTransitionOverlayView_RejectsGlobalAudioFlowRootChild()
        {
            using var harness = SceneTransitionOverlayViewHarness.Create();
            new GameObject("GlobalAudioFlowRoot", typeof(GlobalAudioFlowRoot)).transform.SetParent(harness.Root.transform, false);

            Assert.That(
                harness.View.CollectValidationIssues(),
                Has.Some.Contains("GlobalAudioFlowRoot"));
        }

        [Test]
        public void SceneTransitionOverlayPrefab_HasRequiredCanvasAndTmpStructure()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<SceneTransitionOverlayView>(OverlayPrefabPath);
            Assert.That(prefab, Is.Not.Null, OverlayPrefabPath);

            UiTestPrefabAssetUtility.AssertOverlayCanvasScaling(prefab.gameObject);
            Assert.That(prefab.GetComponent<Canvas>().sortingOrder, Is.EqualTo(5000));
            Assert.That(prefab.GetComponentsInChildren<TMP_Text>(true).Length, Is.GreaterThanOrEqualTo(8));
            Assert.That(prefab.GetComponentsInChildren<EventSystem>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Game.Shared.Audio.AudioRuntimeRoot>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Game.Feature.Flow.Audio.GlobalAudioFlowRoot>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.CollectValidationIssues(), Is.Empty);
        }

        [Test]
        public void SceneTransitionCoordinator_UsesPrefabOverlayWhenResourcesPrefabExists()
        {
            using var harness = SceneTransitionOverlayViewHarness.Create();
            SceneTransitionCoordinator.SetOverlayResourceLoaderForTests(() => harness.View);
            var coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();

                InvokeEnsureOverlay(coordinator);

                Assert.That(coordinator.IsUsingGeneratedOverlayForTests, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_FallsBackToGeneratedOverlayWhenPrefabMissing()
        {
            SceneTransitionCoordinator.SetOverlayResourceLoaderForTests(() => null);
            SceneTransitionCoordinator.SetOverlayShellResourceLoaderForTests(() => null);
            LogAssert.Expect(
                LogType.Warning,
                "Scene transition overlay shell prefab was not found at Resources path 'UI/Transitions/SceneTransitionOverlayShell' " +
                "and legacy overlay prefab was not found at Resources path 'UI/SceneTransitionOverlayView'. " +
                "Using the generated fallback overlay.");
            var coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();

                InvokeEnsureOverlay(coordinator);

                Assert.That(coordinator.IsUsingGeneratedOverlayForTests, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        private static void InvokeEnsureOverlay(SceneTransitionCoordinator coordinator)
        {
            var method = typeof(SceneTransitionCoordinator).GetMethod(
                "EnsureOverlay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(coordinator, Array.Empty<object>());
        }

        private sealed class SceneTransitionOverlayViewHarness : IDisposable
        {
            private SceneTransitionOverlayViewHarness(
                GameObject root,
                SceneTransitionOverlayView view,
                GameObject blocker,
                Image blockerImage,
                GameObject visualRoot,
                GameObject genericLoadingGroup,
                GameObject chanceLostGroup,
                TMP_Text titleText,
                TMP_Text messageText,
                TMP_Text previousChanceText,
                TMP_Text currentChanceText,
                TMP_Text totalChanceText,
                TMP_Text deathCountText,
                RectTransform progressFill)
            {
                Root = root;
                View = view;
                Blocker = blocker;
                BlockerImage = blockerImage;
                VisualRoot = visualRoot;
                GenericLoadingGroup = genericLoadingGroup;
                ChanceLostGroup = chanceLostGroup;
                TitleText = titleText;
                MessageText = messageText;
                PreviousChanceText = previousChanceText;
                CurrentChanceText = currentChanceText;
                TotalChanceText = totalChanceText;
                DeathCountText = deathCountText;
                ProgressFill = progressFill;
            }

            public GameObject Root { get; }
            public SceneTransitionOverlayView View { get; }
            public GameObject Blocker { get; }
            public Image BlockerImage { get; }
            public GameObject VisualRoot { get; }
            public GameObject GenericLoadingGroup { get; }
            public GameObject ChanceLostGroup { get; }
            public TMP_Text TitleText { get; }
            public TMP_Text MessageText { get; }
            public TMP_Text PreviousChanceText { get; }
            public TMP_Text CurrentChanceText { get; }
            public TMP_Text TotalChanceText { get; }
            public TMP_Text DeathCountText { get; }
            public RectTransform ProgressFill { get; }

            public static SceneTransitionOverlayViewHarness Create()
            {
                var root = new GameObject("SceneTransitionOverlayView", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
                var view = root.AddComponent<SceneTransitionOverlayView>();
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;

                var blocker = CreateChild(root.transform, "Blocker", typeof(Image));
                var blockerImage = blocker.GetComponent<Image>();
                var visualRoot = CreateChild(root.transform, "VisualRoot", typeof(CanvasGroup));
                var genericLoadingGroup = CreateChild(visualRoot.transform, "GenericLoadingGroup");
                var chanceLostGroup = CreateChild(visualRoot.transform, "ChanceLostGroup");
                var restartGroup = CreateChild(visualRoot.transform, "RestartGroup");
                var stageClearGroup = CreateChild(visualRoot.transform, "StageClearGroup");
                var mainMenuReturnGroup = CreateChild(visualRoot.transform, "MainMenuReturnGroup");
                var titleText = CreateText(visualRoot.transform, "TitleText_TMP");
                var messageText = CreateText(visualRoot.transform, "MessageText_TMP");
                var loadingText = CreateText(genericLoadingGroup.transform, "LoadingText_TMP");
                var previousChanceText = CreateText(chanceLostGroup.transform, "PreviousChanceText_TMP");
                var currentChanceText = CreateText(chanceLostGroup.transform, "CurrentChanceText_TMP");
                var totalChanceText = CreateText(chanceLostGroup.transform, "TotalChanceText_TMP");
                var deathCountText = CreateText(chanceLostGroup.transform, "DeathCountText_TMP");
                var progressRoot = CreateChild(genericLoadingGroup.transform, "ProgressRoot");
                var progressFill = CreateChild(progressRoot.transform, "ProgressFill").GetComponent<RectTransform>();

                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("_canvas").objectReferenceValue = canvas;
                serializedView.FindProperty("_rootGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
                serializedView.FindProperty("_visualGroup").objectReferenceValue = visualRoot.GetComponent<CanvasGroup>();
                serializedView.FindProperty("_blocker").objectReferenceValue = blocker;
                serializedView.FindProperty("_blockerImage").objectReferenceValue = blockerImage;
                serializedView.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
                serializedView.FindProperty("_genericLoadingGroup").objectReferenceValue = genericLoadingGroup;
                serializedView.FindProperty("_chanceLostGroup").objectReferenceValue = chanceLostGroup;
                serializedView.FindProperty("_restartGroup").objectReferenceValue = restartGroup;
                serializedView.FindProperty("_stageClearGroup").objectReferenceValue = stageClearGroup;
                serializedView.FindProperty("_mainMenuReturnGroup").objectReferenceValue = mainMenuReturnGroup;
                serializedView.FindProperty("_titleText").objectReferenceValue = titleText;
                serializedView.FindProperty("_messageText").objectReferenceValue = messageText;
                serializedView.FindProperty("_loadingText").objectReferenceValue = loadingText;
                serializedView.FindProperty("_previousChanceText").objectReferenceValue = previousChanceText;
                serializedView.FindProperty("_currentChanceText").objectReferenceValue = currentChanceText;
                serializedView.FindProperty("_totalChanceText").objectReferenceValue = totalChanceText;
                serializedView.FindProperty("_deathCountText").objectReferenceValue = deathCountText;
                serializedView.FindProperty("_progressRoot").objectReferenceValue = progressRoot;
                serializedView.FindProperty("_progressFill").objectReferenceValue = progressFill;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                view.HideAll();
                return new SceneTransitionOverlayViewHarness(
                    root,
                    view,
                    blocker,
                    blockerImage,
                    visualRoot,
                    genericLoadingGroup,
                    chanceLostGroup,
                    titleText,
                    messageText,
                    previousChanceText,
                    currentChanceText,
                    totalChanceText,
                    deathCountText,
                    progressFill);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }

            private static GameObject CreateChild(Transform parent, string name, params Type[] components)
            {
                var allComponents = new[] { typeof(RectTransform) }
                    .Concat(components)
                    .ToArray();
                var child = new GameObject(name, allComponents);
                child.transform.SetParent(parent, false);
                return child;
            }

            private static TMP_Text CreateText(Transform parent, string name)
            {
                var textObject = CreateChild(parent, name, typeof(TextMeshProUGUI));
                return textObject.GetComponent<TMP_Text>();
            }
        }

        private sealed class AudioRuntimeRoot : MonoBehaviour
        {
        }

        private sealed class GlobalAudioFlowRoot : MonoBehaviour
        {
        }
    }
}

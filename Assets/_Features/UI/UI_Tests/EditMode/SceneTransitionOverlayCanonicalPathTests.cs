using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class SceneTransitionOverlayCanonicalPathTests
    {
        private const string ShellPrefabPath =
            "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/SceneTransitionOverlayShell.prefab";

        private const string CatalogPath =
            "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/SceneTransitionOverlayContentCatalog.asset";

        private const string GenericLoadingContentPrefabPath =
            "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/Contents/GenericLoadingOverlayContent.prefab";

        private const string ChanceLostContentPrefabPath =
            "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/Contents/ChanceLostOverlayContent.prefab";

        private const string SceneTransitionOverlayAssetAuthoringPath =
            "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionOverlayAssetAuthoring.cs";

        [TearDown]
        public void TearDown()
        {
            SceneTransitionCoordinator.SetOverlayShellResourceLoaderForTests(null);
            SceneTransitionCoordinator.SetContentCatalogResourceLoaderForTests(null);
            SceneTransitionCoordinator.BindUiAudioPortForCurrentScene(null);
        }

        [Test]
        public void SceneTransitionOverlayShellPrefab_HasRequiredCanvasAndBoundaryStructure()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<SceneTransitionOverlayShellView>(ShellPrefabPath);
            Assert.That(prefab, Is.Not.Null, ShellPrefabPath);

            UiTestPrefabAssetUtility.AssertOverlayCanvasScaling(prefab.gameObject);
            Assert.That(prefab.GetComponent<Canvas>().sortingOrder, Is.EqualTo(5000));
            Assert.That(prefab.GetComponentsInChildren<EventSystem>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Game.Shared.Audio.AudioRuntimeRoot>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Game.Feature.Flow.Audio.GlobalAudioFlowRoot>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.CollectValidationIssues(), Is.Empty);
        }

        [Test]
        public void SceneTransitionOverlayContentCatalog_AllSemanticIdsResolveAfterSharedMapping()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SceneTransitionOverlayContentCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            Assert.That(catalog.GenericFallbackPrefab, Is.Not.Null);

            var coveredKinds = catalog.Entries
                .Where(entry => entry != null && entry.ContentPrefab != null)
                .Select(entry => entry.TransitionKind)
                .ToArray();
            Assert.That(coveredKinds, Does.Contain(StageTransitionKind.MainToGameplay));
            Assert.That(coveredKinds, Does.Contain(StageTransitionKind.GameplayToMain));
            Assert.That(coveredKinds, Does.Contain(StageTransitionKind.StageClearNext));
            Assert.That(coveredKinds, Does.Contain(StageTransitionKind.StageRetryManual));
            Assert.That(coveredKinds, Does.Contain(StageTransitionKind.DeathRetryChanceLost));
            Assert.That(coveredKinds, Does.Contain(StageTransitionKind.LevelFailedRestart));

            foreach (var transitionKind in CommonContentTransitionKinds)
            {
                var prefab = ResolveCatalogContent(catalog, transitionKind);

                Assert.That(prefab, Is.SameAs(catalog.GenericFallbackPrefab), transitionKind.ToString());
                Assert.That(prefab, Is.TypeOf<GenericLoadingOverlayContentView>(), transitionKind.ToString());
            }

            var levelFailed = ResolveCatalogContent(catalog, StageTransitionKind.LevelFailedRestart);
            var manualRestart = ResolveCatalogContent(catalog, StageTransitionKind.StageRetryManual);

            Assert.That(levelFailed, Is.SameAs(catalog.GenericFallbackPrefab));
            Assert.That(levelFailed, Is.SameAs(manualRestart));
            Assert.That(levelFailed.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(text => text.name), Does.Not.Contain("Level" + "Restart" + "MessageText_TMP"));

            var chanceLost = ResolveCatalogContent(catalog, StageTransitionKind.DeathRetryChanceLost);

            Assert.That(chanceLost, Is.Not.SameAs(catalog.GenericFallbackPrefab));
            Assert.That(chanceLost, Is.TypeOf<ChanceLostOverlayContentView>());

            var catalogYaml = File.ReadAllText(CatalogPath);

            foreach (var guid in DeletedDuplicatePrefabGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Assert.That(string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath), Is.True, guid);
                Assert.That(catalogYaml, Does.Not.Contain(guid), guid);
            }
        }

        [Test]
        public void SceneTransitionOverlayContentPrefabs_HaveCurrentBaseBindingsOnly()
        {
            foreach (var path in TransitionContentPrefabPaths)
            {
                var yaml = File.ReadAllText(path);

                Assert.That(yaml, Does.Contain("_rootGroup:"), path);
                Assert.That(yaml, Does.Contain("_progressText:"), path);
                foreach (var retiredField in RetiredBaseContentFields)
                {
                    Assert.That(yaml, Does.Not.Contain(retiredField + ":"), path);
                }

                if (path == ChanceLostContentPrefabPath)
                {
                    foreach (var retiredField in RetiredChanceTextFields)
                    {
                        Assert.That(yaml, Does.Not.Contain(retiredField + ":"), retiredField);
                    }

                    foreach (var retiredChildName in RetiredChanceTextChildNames)
                    {
                        Assert.That(yaml, Does.Not.Contain(retiredChildName), retiredChildName);
                    }

                    Assert.That(yaml, Does.Contain("_chanceSlotRoots:"), "PR-T4 must not remove the slot root inspector array.");
                    Assert.That(yaml, Does.Not.Contain("_chanceSlotRoots: []"), "PR-T5 requires explicit ChanceLost slot root inspector bindings.");
                    Assert.That(yaml, Does.Contain("ChanceSlotView"), "PR-T4 must preserve the slot visual hierarchy.");
                }
            }
        }

        [Test]
        public void ChanceLostOverlayContentPrefab_HasExplicitChanceSlotRootBindings()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<ChanceLostOverlayContentView>(ChanceLostContentPrefabPath);
            Assert.That(prefab, Is.Not.Null, ChanceLostContentPrefabPath);

            var serialized = new SerializedObject(prefab);
            var chanceSlotRoots = serialized.FindProperty("_chanceSlotRoots");
            Assert.That(chanceSlotRoots, Is.Not.Null);
            Assert.That(chanceSlotRoots.arraySize, Is.EqualTo(3));

            var expectedNames = new[]
            {
                "ChanceSlotView 0",
                "ChanceSlotView 1",
                "ChanceSlotView 2",
            };

            for (var i = 0; i < expectedNames.Length; i++)
            {
                var slot = chanceSlotRoots.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;

                Assert.That(slot, Is.Not.Null, $"_chanceSlotRoots[{i}]");
                Assert.That(slot.name, Is.EqualTo(expectedNames[i]), $"_chanceSlotRoots[{i}]");
            }

            Assert.That(prefab.CollectValidationIssues(), Is.Empty);
        }

        [Test]
        public void ChanceLostOverlayContentAuthoring_BindsChanceSlotRootsOnRebuild()
        {
            var source = File.ReadAllText(SceneTransitionOverlayAssetAuthoringPath);

            Assert.That(source, Does.Contain("var chanceSlots = CreateChanceSlotRoots(panel);"));
            Assert.That(source, Does.Contain("var chanceSlotRoots = serialized.FindProperty(\"_chanceSlotRoots\");"));
            Assert.That(source, Does.Contain("chanceSlotRoots.arraySize = chanceSlots.Length;"));
            Assert.That(source, Does.Contain("chanceSlotRoots.GetArrayElementAtIndex(i).objectReferenceValue = chanceSlots[i];"));
            foreach (var retiredChildName in RetiredChanceTextChildNames)
            {
                Assert.That(source, Does.Not.Contain(retiredChildName), retiredChildName);
            }
        }

        [Test]
        public void ChanceLostOverlayContentPrefab_DoesNotRestoreRetiredChanceTextBindings()
        {
            var yaml = File.ReadAllText(ChanceLostContentPrefabPath);

            foreach (var retiredField in RetiredChanceTextFields)
            {
                Assert.That(yaml, Does.Not.Contain(retiredField + ":"), retiredField);
            }

            foreach (var retiredChildName in RetiredChanceTextChildNames)
            {
                Assert.That(yaml, Does.Not.Contain(retiredChildName), retiredChildName);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_UsesCanonicalShellWhenResourcesPrefabExists()
        {
            using var shell = ShellHandle.Create();
            SceneTransitionCoordinator.SetOverlayShellResourceLoaderForTests(() => shell.View);
            var coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();

                var overlay = InvokeEnsureOverlay(coordinator);

                Assert.That(overlay, Is.TypeOf<SceneTransitionOverlayShellView>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_MissingShellReportsSetupDefect()
        {
            SceneTransitionCoordinator.SetOverlayShellResourceLoaderForTests(() => null);
            var coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();

                var exception = Assert.Throws<InvalidOperationException>(() => InvokeEnsureOverlay(coordinator));
                Assert.That(exception.Message, Does.Contain("canonical scene transition overlay shell"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_MissingContentCatalogReportsSetupDefect()
        {
            SceneTransitionCoordinator.SetContentCatalogResourceLoaderForTests(() => null);
            var coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => InvokeResolveContentPrefab(coordinator, Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart)));
                Assert.That(exception.Message, Does.Contain("canonical scene transition content catalog"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_UnresolvedCatalogContentReportsSetupDefect()
        {
            var catalog = ScriptableObject.CreateInstance<SceneTransitionOverlayContentCatalog>();
            SceneTransitionCoordinator.SetContentCatalogResourceLoaderForTests(() => catalog);
            var coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => InvokeResolveContentPrefab(coordinator, Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart)));
                Assert.That(exception.Message, Does.Contain("did not resolve content"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_DeathRetryChanceLostTransitionPlaysChanceLossCue()
        {
            var coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();
                var uiAudioPort = new RecordingUiAudioPort();
                coordinator.BindUiAudioPort(uiAudioPort);

                coordinator.PlayTransitionAudio(new SceneTransitionOverlayModel(
                    StageTransitionKind.DeathRetryChanceLost,
                    TransitionOverlayKind.ChanceLost,
                    "Chance Lost",
                    "Retrying.",
                    blockInput: true,
                    showProgress: true,
                    progress01: 0f,
                    hasChanceLost: true,
                    previousRemainingChances: 2,
                    currentRemainingChances: 1,
                    totalChances: 3,
                    deathCount: 1));

                Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.ChanceLoss }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_NonRetryTransitionsDoNotPlayOverlayAudio()
        {
            var coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();
                var uiAudioPort = new RecordingUiAudioPort();
                coordinator.BindUiAudioPort(uiAudioPort);

                coordinator.PlayTransitionAudio(new SceneTransitionOverlayModel(
                    StageTransitionKind.LevelFailedRestart,
                    TransitionOverlayKind.Restart,
                    "Restarting Level",
                    "Returning.",
                    blockInput: true,
                    showProgress: true,
                    progress01: 0f,
                    hasChanceLost: false,
                    previousRemainingChances: 0,
                    currentRemainingChances: 0,
                    totalChances: 0,
                    deathCount: 0));

                Assert.That(uiAudioPort.PlayedCueIds, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        private static ISceneTransitionOverlayShellView InvokeEnsureOverlay(SceneTransitionCoordinator coordinator)
        {
            var method = typeof(SceneTransitionCoordinator).GetMethod(
                "EnsureOverlay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (ISceneTransitionOverlayShellView)Invoke(method, coordinator);
        }

        private static SceneTransitionOverlayContentView InvokeResolveContentPrefab(
            SceneTransitionCoordinator coordinator,
            SceneTransitionOverlayModel model)
        {
            var method = typeof(SceneTransitionCoordinator).GetMethod(
                "ResolveContentPrefab",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (SceneTransitionOverlayContentView)Invoke(method, coordinator, model);
        }

        private static object Invoke(MethodInfo method, object target, params object[] args)
        {
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static SceneTransitionOverlayModel Model(
            StageTransitionKind transitionKind,
            TransitionOverlayKind overlayKind)
        {
            return new SceneTransitionOverlayModel(
                transitionKind,
                overlayKind,
                "Title",
                "Message",
                blockInput: true,
                showProgress: true,
                progress01: 0f,
                hasChanceLost: false,
                previousRemainingChances: 0,
                currentRemainingChances: 0,
                totalChances: 0,
                deathCount: 0);
        }

        private static readonly StageTransitionKind[] CommonContentTransitionKinds =
        {
            StageTransitionKind.MainToGameplay,
            StageTransitionKind.GameplayToMain,
            StageTransitionKind.StageClearNext,
            StageTransitionKind.StageRetryManual,
            StageTransitionKind.LevelFailedRestart,
        };

        private static readonly string[] DeletedDuplicatePrefabGuids =
        {
            "bb994410" + "0587cc84f9524c34a1745c93",
            "e53afcd5" + "78e311b4f91209518d25e637",
            "a287560d" + "78e3fd7448af54f2859fb663",
            "d1e542b8" + "9be2719489c78a72f68f4d3d",
        };

        private static readonly string[] TransitionContentPrefabPaths =
        {
            GenericLoadingContentPrefabPath,
            ChanceLostContentPrefabPath,
        };

        private static readonly string[] RetiredBaseContentFields =
        {
            "_titleText",
            "_messageText",
            "_progressRoot",
            "_progressFill",
            "_animator",
        };

        private static readonly string[] RetiredChanceTextFields =
        {
            "_previousChanceText",
            "_currentChanceText",
            "_totalChanceText",
            "_deathCountText",
            "_currentTextPulseScalePunch",
            "_currentTextPulseDurationSeconds",
            "_previousTextDimAlpha",
            "_previousTextDimDurationSeconds",
        };

        private static readonly string[] RetiredChanceTextChildNames =
        {
            "PreviousChanceText_TMP",
            "CurrentChanceText_TMP",
            "TotalChanceText_TMP",
            "DeathCountText_TMP",
        };

        private static SceneTransitionOverlayContentView ResolveCatalogContent(
            SceneTransitionOverlayContentCatalog catalog,
            StageTransitionKind transitionKind)
        {
            var entry = catalog.Entries.SingleOrDefault(candidate => candidate.TransitionKind == transitionKind);
            Assert.That(entry, Is.Not.Null, transitionKind.ToString());
            Assert.That(entry.ContentPrefab, Is.Not.Null, transitionKind.ToString());
            return entry.ContentPrefab;
        }

        private sealed class ShellHandle : IDisposable
        {
            private ShellHandle(GameObject root, SceneTransitionOverlayShellView view)
            {
                Root = root;
                View = view;
            }

            public GameObject Root { get; }
            public SceneTransitionOverlayShellView View { get; }

            public static ShellHandle Create()
            {
                var root = new GameObject("Shell", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
                var view = root.AddComponent<SceneTransitionOverlayShellView>();
                var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
                blocker.transform.SetParent(root.transform, false);
                var visualRoot = new GameObject("VisualRoot", typeof(RectTransform), typeof(CanvasGroup));
                visualRoot.transform.SetParent(root.transform, false);
                var contentMount = new GameObject("ContentMount", typeof(RectTransform));
                contentMount.transform.SetParent(visualRoot.transform, false);

                var serialized = new SerializedObject(view);
                serialized.FindProperty("_canvas").objectReferenceValue = root.GetComponent<Canvas>();
                serialized.FindProperty("_rootGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
                serialized.FindProperty("_blocker").objectReferenceValue = blocker;
                serialized.FindProperty("_blockerImage").objectReferenceValue = blocker.GetComponent<Image>();
                serialized.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
                serialized.FindProperty("_visualGroup").objectReferenceValue = visualRoot.GetComponent<CanvasGroup>();
                serialized.FindProperty("_contentMount").objectReferenceValue = contentMount.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                view.HideAll();
                return new ShellHandle(root, view);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private sealed class RecordingUiAudioPort : IUiAudioPort
        {
            private readonly List<UiAudioCueId> _playedCueIds = new();

            public IReadOnlyList<UiAudioCueId> PlayedCueIds => _playedCueIds;

            public void Play(UiAudioCueId cueId)
            {
                _playedCueIds.Add(cueId);
            }
        }
    }
}

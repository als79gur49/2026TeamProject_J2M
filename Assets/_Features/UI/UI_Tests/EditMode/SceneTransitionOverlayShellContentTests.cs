using System;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class SceneTransitionOverlayShellContentTests
    {
        [Test]
        public void SceneTransitionOverlayContentResolver_UsesStageTransitionKindExactMatch()
        {
            using var fallback = ContentHandle.Create<ChanceLostOverlayContentView>("FallbackRestart");
            using var generic = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");
            using var catalog = CatalogHandle.Create(
                Entry(StageTransitionKind.Unknown, TransitionOverlayKind.Restart, fallback.View),
                Entry(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart, generic.View));
            var resolver = new SceneTransitionOverlayContentResolver();
            var model = Model(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart);

            var resolved = resolver.Resolve(model, catalog.Catalog);

            Assert.That(resolved, Is.SameAs(generic.View));
        }

        [Test]
        public void SceneTransitionOverlayContentResolver_CommonRestartSemanticIdsCanShareGenericContent()
        {
            using var generic = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");
            using var catalog = CatalogHandle.Create(
                Entry(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart, generic.View),
                Entry(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart, generic.View));
            var resolver = new SceneTransitionOverlayContentResolver();

            var manualResolved = resolver.Resolve(
                Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart),
                catalog.Catalog);
            var levelFailedResolved = resolver.Resolve(
                Model(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart),
                catalog.Catalog);

            Assert.That(manualResolved, Is.SameAs(generic.View));
            Assert.That(levelFailedResolved, Is.SameAs(generic.View));
        }

        [Test]
        public void SceneTransitionOverlayContentResolver_FallsBackToOverlayKind()
        {
            using var generic = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");
            using var catalog = CatalogHandle.Create(
                Entry(StageTransitionKind.Unknown, TransitionOverlayKind.Restart, generic.View));
            var resolver = new SceneTransitionOverlayContentResolver();

            var resolved = resolver.Resolve(
                Model(StageTransitionKind.Unknown, TransitionOverlayKind.Restart),
                catalog.Catalog);

            Assert.That(resolved, Is.SameAs(generic.View));
        }

        [Test]
        public void SceneTransitionOverlayShell_ShowBlockerOnly_HidesContentMount()
        {
            using var shell = ShellHandle.Create();

            shell.View.ShowBlockerOnly(true);

            Assert.That(shell.Root.activeSelf, Is.True);
            Assert.That(shell.Blocker.activeSelf, Is.True);
            Assert.That(shell.VisualRoot.activeSelf, Is.False);
        }

        [Test]
        public void SceneTransitionOverlayShell_ShowContent_MountsOnlySelectedContent()
        {
            using var shell = ShellHandle.Create();
            using var generic = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");

            var content = shell.View.MountContent(generic.View);
            shell.View.ShowContent(Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart), content);

            Assert.That(shell.VisualRoot.activeSelf, Is.True);
            Assert.That(shell.ContentMount.childCount, Is.EqualTo(1));
            Assert.That(shell.ContentMount.GetChild(0).name, Is.EqualTo("GenericLoadingOverlayContent"));
        }

        [Test]
        public void SceneTransitionOverlayShell_HideAll_DisablesBlockerAndVisual()
        {
            using var shell = ShellHandle.Create();
            using var generic = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");
            var content = shell.View.MountContent(generic.View);
            shell.View.ShowContent(Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart), content);

            shell.View.HideAll();

            Assert.That(shell.Root.activeSelf, Is.False);
            Assert.That(shell.Blocker.activeSelf, Is.False);
            Assert.That(shell.VisualRoot.activeSelf, Is.False);
        }

        [Test]
        public void SceneTransitionOverlayShell_MountContentWithoutCatalogPrefabReportsSetupDefect()
        {
            using var shell = ShellHandle.Create();

            var exception = Assert.Throws<InvalidOperationException>(() => shell.View.MountContent(null));

            Assert.That(exception.Message, Does.Contain("catalog-authored content prefab"));
        }

        [Test]
        public void ChanceLostOverlayContent_BindsTmpTexts()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var model = new SceneTransitionOverlayModel(
                StageTransitionKind.DeathRetryChanceLost,
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

            content.View.Bind(model);

            Assert.That(content.PreviousChanceText.text, Is.EqualTo("2"));
            Assert.That(content.CurrentChanceText.text, Is.EqualTo("1"));
            Assert.That(content.TotalChanceText.text, Is.EqualTo("/ 3"));
            Assert.That(content.DeathCountText.text, Is.EqualTo("Deaths 4"));
        }

        [Test]
        public void ChanceLostOverlayContent_Show_AnimatesLostChanceSlot()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;
            var lostSlot = content.ChanceSlots[1];
            var startPosition = lostSlot.anchoredPosition;
            var effectImage = FindEffectImage(lostSlot);
            var filledIcon = FindImage(lostSlot, "FilledIcon");
            var authoredEffectColor = effectImage.color;
            var authoredEffectMaterial = effectImage.material;
            var authoredFilledIconColor = filledIcon.color;
            var authoredFilledIconMaterial = filledIcon.material;

            view.Bind(new SceneTransitionOverlayModel(
                StageTransitionKind.DeathRetryChanceLost,
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
                deathCount: 4));

            view.Show();

            Assert.That(view.ResolvedChanceSlotCountForTests, Is.EqualTo(3));
            Assert.That(view.ActiveLostChanceAnimationCountForTests, Is.EqualTo(1));
            Assert.That(lostSlot.anchoredPosition, Is.EqualTo(startPosition));
            Assert.That(lostSlot.Find("LostChanceTweenRoot"), Is.Not.Null);
            var lostTweenRoot = (RectTransform)lostSlot.Find("LostChanceTweenRoot");
            var allIn1Shader = Shader.Find("AllIn1SpriteShader/AllIn1SpriteShaderUiMask");
            if (allIn1Shader != null)
            {
                Assert.That(effectImage.material.shader, Is.SameAs(allIn1Shader));
                Assert.That(filledIcon.material.shader, Is.SameAs(allIn1Shader));
            }

            view.ResetView();

            Assert.That(view.ActiveLostChanceAnimationCountForTests, Is.Zero);
            Assert.That(lostSlot.anchoredPosition, Is.EqualTo(startPosition));
            Assert.That(lostTweenRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(effectImage.color, Is.EqualTo(authoredEffectColor));
            Assert.That(effectImage.material, Is.SameAs(authoredEffectMaterial));
            Assert.That(filledIcon.color, Is.EqualTo(authoredFilledIconColor));
            Assert.That(filledIcon.material, Is.SameAs(authoredFilledIconMaterial));
            Assert.That(CountCrackLines(lostTweenRoot), Is.EqualTo(5));
            Assert.That(CountCrackShards(lostTweenRoot), Is.EqualTo(9));
            AssertCrackShardsRestored(lostTweenRoot);
            Assert.That(content.CurrentChanceText.rectTransform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(lostSlot.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [Test]
        public void ChanceLostOverlayContent_CrackShardColor_DecaysBySpawnTiming()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;

            var firstShardColor = view.CrackShardVisibleColorForTests(0f);
            var middleShardColor = view.CrackShardVisibleColorForTests(0.55f);
            var lateShardColor = view.CrackShardVisibleColorForTests(1.1f);

            Assert.That(firstShardColor.r, Is.GreaterThan(middleShardColor.r));
            Assert.That(middleShardColor.r, Is.GreaterThan(lateShardColor.r));
            Assert.That(firstShardColor.g, Is.GreaterThan(middleShardColor.g));
            Assert.That(middleShardColor.g, Is.GreaterThan(lateShardColor.g));
            Assert.That(firstShardColor.b, Is.GreaterThan(middleShardColor.b));
            Assert.That(middleShardColor.b, Is.GreaterThan(lateShardColor.b));
        }

        [Test]
        public void ChanceLostOverlayContent_ShowWithoutChanceLost_DoesNotAnimate()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;

            view.Bind(new SceneTransitionOverlayModel(
                StageTransitionKind.DeathRetryChanceLost,
                TransitionOverlayKind.ChanceLost,
                "Chance Lost",
                "Retrying.",
                blockInput: true,
                showProgress: true,
                progress01: 0.25f,
                hasChanceLost: false,
                previousRemainingChances: 1,
                currentRemainingChances: 1,
                totalChances: 3,
                deathCount: 4));

            view.Show();

            Assert.That(view.ActiveLostChanceAnimationCountForTests, Is.Zero);
            Assert.That(content.CurrentChanceText.text, Is.Empty);
            Assert.That(content.CurrentChanceText.rectTransform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void GenericLoadingOverlayContent_LevelFailedRestartDoesNotShowChanceLostFields()
        {
            using var content = ContentHandle.Create<GenericLoadingOverlayContentView>("GenericLoadingOverlayContent");

            content.View.Bind(Model(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart));

            Assert.That(content.Root.GetComponentsInChildren<ChanceLostOverlayContentView>(true), Is.Empty);
            Assert.That(content.Root.GetComponentsInChildren<TMP_Text>(true).Length, Is.EqualTo(3));
        }

        [Test]
        public void TransitionOverlayShell_RejectsEventSystemChild()
        {
            using var shell = ShellHandle.Create();
            new GameObject("EventSystem", typeof(EventSystem)).transform.SetParent(shell.Root.transform, false);

            Assert.That(shell.View.CollectValidationIssues(), Has.Some.Contains("EventSystem"));
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

        private static Image FindEffectImage(RectTransform slot)
        {
            return FindImage(slot, "Effect");
        }

        private static Image FindImage(RectTransform slot, string name)
        {
            var images = slot.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i].name == name)
                {
                    return images[i];
                }
            }

            throw new AssertionException($"Expected chance slot to contain a {name} image.");
        }

        private static int CountCrackLines(RectTransform root)
        {
            return CountImagesByPrefix(root, "CrackLine");
        }

        private static int CountCrackShards(RectTransform root)
        {
            return CountImagesByPrefix(root, "CrackShard");
        }

        private static int CountImagesByPrefix(RectTransform root, string prefix)
        {
            var images = root.GetComponentsInChildren<Image>(true);
            var count = 0;
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i].name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertCrackShardsRestored(RectTransform root)
        {
            var images = root.GetComponentsInChildren<Image>(true);
            var shardCount = 0;
            var bottomShardCount = 0;
            var sideShardCount = 0;
            for (var i = 0; i < images.Length; i++)
            {
                if (!images[i].name.StartsWith("CrackShard", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                shardCount++;
                Assert.That(images[i].color.a, Is.EqualTo(0f));
                Assert.That(images[i].raycastTarget, Is.False);
                Assert.That(images[i].rectTransform.sizeDelta.x, Is.GreaterThanOrEqualTo(8f));
                Assert.That(images[i].rectTransform.sizeDelta.y, Is.GreaterThanOrEqualTo(8f));
                Assert.That(images[i].rectTransform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(images[i].GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(images[i].GetComponent<CanvasGroup>().ignoreParentGroups, Is.True);

                var anchoredPosition = images[i].rectTransform.anchoredPosition;
                if (anchoredPosition.y <= -78f)
                {
                    bottomShardCount++;
                }

                if (Mathf.Abs(anchoredPosition.x) >= 56f)
                {
                    sideShardCount++;
                }

                Assert.That(
                    anchoredPosition.y <= -78f || Mathf.Abs(anchoredPosition.x) >= 56f,
                    Is.True,
                    "Crack shards should start near the bottom or lower side area of the chance icon.");
            }

            Assert.That(shardCount, Is.EqualTo(9));
            Assert.That(bottomShardCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(sideShardCount, Is.GreaterThanOrEqualTo(5));
        }

        private static CatalogEntrySpec Entry(
            StageTransitionKind transitionKind,
            TransitionOverlayKind overlayKind,
            SceneTransitionOverlayContentView prefab)
        {
            return new CatalogEntrySpec(transitionKind, overlayKind, prefab);
        }

        private readonly struct CatalogEntrySpec
        {
            public CatalogEntrySpec(
                StageTransitionKind transitionKind,
                TransitionOverlayKind overlayKind,
                SceneTransitionOverlayContentView prefab)
            {
                TransitionKind = transitionKind;
                OverlayKind = overlayKind;
                Prefab = prefab;
            }

            public StageTransitionKind TransitionKind { get; }
            public TransitionOverlayKind OverlayKind { get; }
            public SceneTransitionOverlayContentView Prefab { get; }
        }

        private sealed class CatalogHandle : IDisposable
        {
            private CatalogHandle(SceneTransitionOverlayContentCatalog catalog)
            {
                Catalog = catalog;
            }

            public SceneTransitionOverlayContentCatalog Catalog { get; }

            public static CatalogHandle Create(params CatalogEntrySpec[] specs)
            {
                var catalog = ScriptableObject.CreateInstance<SceneTransitionOverlayContentCatalog>();
                var serialized = new SerializedObject(catalog);
                var entries = serialized.FindProperty("_entries");
                entries.arraySize = specs.Length;
                for (var i = 0; i < specs.Length; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("_transitionKind").enumValueIndex = (int)specs[i].TransitionKind;
                    entry.FindPropertyRelative("_fallbackOverlayKind").enumValueIndex = (int)specs[i].OverlayKind;
                    entry.FindPropertyRelative("_contentPrefab").objectReferenceValue = specs[i].Prefab;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                return new CatalogHandle(catalog);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Catalog);
            }
        }

        private sealed class ShellHandle : IDisposable
        {
            private ShellHandle(
                GameObject root,
                SceneTransitionOverlayShellView view,
                GameObject blocker,
                GameObject visualRoot,
                Transform contentMount)
            {
                Root = root;
                View = view;
                Blocker = blocker;
                VisualRoot = visualRoot;
                ContentMount = contentMount;
            }

            public GameObject Root { get; }
            public SceneTransitionOverlayShellView View { get; }
            public GameObject Blocker { get; }
            public GameObject VisualRoot { get; }
            public Transform ContentMount { get; }

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
                return new ShellHandle(root, view, blocker, visualRoot, contentMount.transform);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private sealed class ContentHandle : IDisposable
        {
            private ContentHandle(
                GameObject root,
                SceneTransitionOverlayContentView view,
                TMP_Text previousChanceText,
                TMP_Text currentChanceText,
                TMP_Text totalChanceText,
                TMP_Text deathCountText,
                RectTransform[] chanceSlots)
            {
                Root = root;
                View = view;
                PreviousChanceText = previousChanceText;
                CurrentChanceText = currentChanceText;
                TotalChanceText = totalChanceText;
                DeathCountText = deathCountText;
                ChanceSlots = chanceSlots;
            }

            public GameObject Root { get; }
            public SceneTransitionOverlayContentView View { get; }
            public TMP_Text PreviousChanceText { get; }
            public TMP_Text CurrentChanceText { get; }
            public TMP_Text TotalChanceText { get; }
            public TMP_Text DeathCountText { get; }
            public RectTransform[] ChanceSlots { get; }

            public static ContentHandle Create<T>(string name)
                where T : SceneTransitionOverlayContentView
            {
                var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
                var view = root.AddComponent<T>();
                var title = CreateText(root.transform, "TitleText_TMP");
                var message = CreateText(root.transform, "MessageText_TMP");
                var progressRoot = new GameObject("ProgressRoot", typeof(RectTransform));
                progressRoot.transform.SetParent(root.transform, false);
                var progressFill = new GameObject("ProgressFill", typeof(RectTransform));
                progressFill.transform.SetParent(progressRoot.transform, false);
                var progressText = CreateText(progressRoot.transform, "ProgressText_TMP");

                TMP_Text previous = null;
                TMP_Text current = null;
                TMP_Text total = null;
                TMP_Text deaths = null;
                var chanceSlots = Array.Empty<RectTransform>();

                var serialized = new SerializedObject(view);
                serialized.FindProperty("_rootGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
                serialized.FindProperty("_titleText").objectReferenceValue = title;
                serialized.FindProperty("_messageText").objectReferenceValue = message;
                serialized.FindProperty("_progressRoot").objectReferenceValue = progressRoot;
                serialized.FindProperty("_progressFill").objectReferenceValue = progressFill.GetComponent<RectTransform>();
                serialized.FindProperty("_progressText").objectReferenceValue = progressText;

                if (view is ChanceLostOverlayContentView)
                {
                    previous = CreateText(root.transform, "PreviousChanceText_TMP");
                    current = CreateText(root.transform, "CurrentChanceText_TMP");
                    total = CreateText(root.transform, "TotalChanceText_TMP");
                    deaths = CreateText(root.transform, "DeathCountText_TMP");
                    serialized.FindProperty("_previousChanceText").objectReferenceValue = previous;
                    serialized.FindProperty("_currentChanceText").objectReferenceValue = current;
                    serialized.FindProperty("_totalChanceText").objectReferenceValue = total;
                    serialized.FindProperty("_deathCountText").objectReferenceValue = deaths;
                    chanceSlots = CreateChanceSlots(root.transform);
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                return new ContentHandle(root, view, previous, current, total, deaths, chanceSlots);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }

            private static TMP_Text CreateText(Transform parent, string name)
            {
                var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(parent, false);
                return textObject.GetComponent<TMP_Text>();
            }

            private static RectTransform[] CreateChanceSlots(Transform parent)
            {
                var row = new GameObject("BottomRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(parent, false);
                var slots = new RectTransform[3];
                for (var i = 0; i < slots.Length; i++)
                {
                    var slot = new GameObject($"ChanceSlotView {i}", typeof(RectTransform), typeof(CanvasGroup));
                    slot.transform.SetParent(row.transform, false);
                    var rect = slot.GetComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2((i + 1) * 48f, 0f);
                    var visual = new GameObject("FilledIcon", typeof(RectTransform), typeof(Image));
                    visual.transform.SetParent(slot.transform, false);
                    var effect = new GameObject("Effect", typeof(RectTransform), typeof(Image));
                    effect.transform.SetParent(slot.transform, false);
                    effect.GetComponent<Image>().color = new Color(1f, 0.1f, 0.1f, 0.12f);
                    slots[i] = rect;
                }

                return slots;
            }
        }
    }
}

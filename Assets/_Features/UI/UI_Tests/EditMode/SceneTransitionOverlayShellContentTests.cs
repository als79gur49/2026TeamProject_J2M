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
            using var manual = ContentHandle.Create<ManualRestartOverlayContentView>("Manual");
            using var levelFailed = ContentHandle.Create<LevelFailedRestartOverlayContentView>("LevelFailed");
            var resolver = new SceneTransitionOverlayContentResolver();
            var model = Model(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart);

            var resolved = resolver.Resolve(
                model,
                null,
                transitionKind => transitionKind == StageTransitionKind.LevelFailedRestart ? levelFailed.View : null,
                overlayKind => overlayKind == TransitionOverlayKind.Restart ? manual.View : null,
                () => manual.View);

            Assert.That(resolved, Is.SameAs(levelFailed.View));
        }

        [Test]
        public void SceneTransitionOverlayContentResolver_StageRetryManualAndLevelFailedRestartResolveDifferentContents()
        {
            using var manual = ContentHandle.Create<ManualRestartOverlayContentView>("Manual");
            using var levelFailed = ContentHandle.Create<LevelFailedRestartOverlayContentView>("LevelFailed");
            var resolver = new SceneTransitionOverlayContentResolver();

            var manualResolved = resolver.Resolve(
                Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart),
                null,
                transitionKind => transitionKind == StageTransitionKind.StageRetryManual ? manual.View : levelFailed.View,
                null,
                null);
            var levelFailedResolved = resolver.Resolve(
                Model(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart),
                null,
                transitionKind => transitionKind == StageTransitionKind.LevelFailedRestart ? levelFailed.View : manual.View,
                null,
                null);

            Assert.That(manualResolved, Is.SameAs(manual.View));
            Assert.That(levelFailedResolved, Is.SameAs(levelFailed.View));
            Assert.That(manualResolved, Is.Not.SameAs(levelFailedResolved));
        }

        [Test]
        public void SceneTransitionOverlayContentResolver_FallsBackToOverlayKind()
        {
            using var manual = ContentHandle.Create<ManualRestartOverlayContentView>("Manual");
            var resolver = new SceneTransitionOverlayContentResolver();

            var resolved = resolver.Resolve(
                Model(StageTransitionKind.Unknown, TransitionOverlayKind.Restart),
                null,
                _ => null,
                overlayKind => overlayKind == TransitionOverlayKind.Restart ? manual.View : null,
                null);

            Assert.That(resolved, Is.SameAs(manual.View));
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
            using var manual = ContentHandle.Create<ManualRestartOverlayContentView>("ManualRestartOverlayContent");

            var content = shell.View.MountContent(manual.View);
            shell.View.ShowContent(Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart), content);

            Assert.That(shell.VisualRoot.activeSelf, Is.True);
            Assert.That(shell.ContentMount.childCount, Is.EqualTo(1));
            Assert.That(shell.ContentMount.GetChild(0).name, Is.EqualTo("ManualRestartOverlayContent"));
        }

        [Test]
        public void SceneTransitionOverlayShell_HideAll_DisablesBlockerAndVisual()
        {
            using var shell = ShellHandle.Create();
            using var manual = ContentHandle.Create<ManualRestartOverlayContentView>("ManualRestartOverlayContent");
            var content = shell.View.MountContent(manual.View);
            shell.View.ShowContent(Model(StageTransitionKind.StageRetryManual, TransitionOverlayKind.Restart), content);

            shell.View.HideAll();

            Assert.That(shell.Root.activeSelf, Is.False);
            Assert.That(shell.Blocker.activeSelf, Is.False);
            Assert.That(shell.VisualRoot.activeSelf, Is.False);
        }

        [Test]
        public void ChanceLostOverlayContent_BindsTmpTexts()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var model = new SceneTransitionOverlayViewModel(
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

            view.Bind(new SceneTransitionOverlayViewModel(
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
            Assert.That(content.CurrentChanceText.rectTransform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(lostSlot.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [Test]
        public void ChanceLostOverlayContent_ShowWithoutChanceLost_DoesNotAnimate()
        {
            using var content = ContentHandle.Create<ChanceLostOverlayContentView>("ChanceLost");
            var view = (ChanceLostOverlayContentView)content.View;

            view.Bind(new SceneTransitionOverlayViewModel(
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
        public void LevelFailedRestartOverlayContent_DoesNotShowChanceLostFields()
        {
            using var content = ContentHandle.Create<LevelFailedRestartOverlayContentView>("LevelFailed");

            content.View.Bind(Model(StageTransitionKind.LevelFailedRestart, TransitionOverlayKind.Restart));

            Assert.That(content.Root.GetComponentsInChildren<ChanceLostOverlayContentView>(true), Is.Empty);
            Assert.That(content.Root.GetComponentsInChildren<TMP_Text>(true).Length, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void TransitionOverlayShell_RejectsEventSystemChild()
        {
            using var shell = ShellHandle.Create();
            new GameObject("EventSystem", typeof(EventSystem)).transform.SetParent(shell.Root.transform, false);

            Assert.That(shell.View.CollectValidationIssues(), Has.Some.Contains("EventSystem"));
        }

        private static SceneTransitionOverlayViewModel Model(
            StageTransitionKind transitionKind,
            TransitionOverlayKind overlayKind)
        {
            return new SceneTransitionOverlayViewModel(
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
            var images = root.GetComponentsInChildren<Image>(true);
            var count = 0;
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i].name.StartsWith("CrackLine", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
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

                if (view is LevelFailedRestartOverlayContentView)
                {
                    serialized.FindProperty("_levelRestartMessageText").objectReferenceValue =
                        CreateText(root.transform, "LevelRestartMessageText_TMP");
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

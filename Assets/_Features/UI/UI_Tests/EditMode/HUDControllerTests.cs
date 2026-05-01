using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class HUDControllerTests
    {
        [Test]
        public void HUDController_AttachView_BindsChildViewModels()
        {
            var rootObject = new GameObject("HUDController_AttachView_BindsChildViewModels");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var playerStatusPresenter = new PlayerStatusPresenter();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var notificationPresenter = new NotificationPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    playerStatusPresenter,
                    notificationPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    playerStatusPresenter.ViewModel,
                    notificationPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot());

                Assert.That(hudView.ViewModel, Is.SameAs(controller.RootViewModel));
                Assert.That(hudView.StageInfoViewModel, Is.SameAs(controller.StageInfoViewModel));
                Assert.That(hudView.ObjectiveHudView.ViewModel, Is.SameAs(controller.ObjectiveHudViewModel));
                Assert.That(hudView.PlayerStatusView.ViewModel, Is.SameAs(controller.PlayerStatusViewModel));
                Assert.That(hudView.NotificationView.ViewModel, Is.SameAs(controller.NotificationViewModel));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDController_CanonicalPrefab_BindsChancePanelViewModel()
        {
            var rootObject = new GameObject("HUDController_CanonicalPrefab_BindsChancePanelViewModel");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var playerStatusPresenter = new PlayerStatusPresenter();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var chancePanelPresenter = new ChancePanelPresenter();
                var topologyHudPresenter = new TopologyHudPresenter();
                var notificationPresenter = new NotificationPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    topologyHudPresenter,
                    playerStatusPresenter,
                    notificationPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    topologyHudPresenter.ViewModel,
                    playerStatusPresenter.ViewModel,
                    notificationPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot(hasRemainingChances: true, remainingChances: 2, maxChances: 3));

                Assert.That(hudView.ChancePanelView, Is.Not.Null);
                Assert.That(hudView.ChancePanelView.ViewModel, Is.SameAs(chancePanelPresenter.ViewModel));
                Assert.That(chancePanelPresenter.ViewModel.HasChances, Is.True);
                Assert.That(chancePanelPresenter.ViewModel.RemainingChances, Is.EqualTo(2));
                Assert.That(chancePanelPresenter.ViewModel.MaxChances, Is.EqualTo(3));
                Assert.That(hudView.ChancePanelView.GetComponentsInChildren<ChanceSlotView>(true).Length, Is.EqualTo(3));
                Assert.That(CountSlotsWithChild(hudView.ChancePanelView, "Glow"), Is.EqualTo(3));

                source.PublishSnapshot(CreateSnapshot(hasRemainingChances: true, remainingChances: 1, maxChances: 3));

                Assert.That(hudView.ChancePanelView.GetComponentsInChildren<ChanceSlotView>(true).Length, Is.EqualTo(3));
                Assert.That(CountSlotsWithChild(hudView.ChancePanelView, "Glow"), Is.EqualTo(3));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDController_CanonicalPrefab_ArrangesVisibleHudElementsIntoStacks()
        {
            var rootObject = new GameObject("HUDController_CanonicalPrefab_ArrangesVisibleHudElementsIntoStacks");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var playerStatusPresenter = new PlayerStatusPresenter();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var chancePanelPresenter = new ChancePanelPresenter();
                var topologyHudPresenter = new TopologyHudPresenter();
                var notificationPresenter = new NotificationPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    topologyHudPresenter,
                    playerStatusPresenter,
                    notificationPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    topologyHudPresenter.ViewModel,
                    playerStatusPresenter.ViewModel,
                    notificationPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot(
                    hasRemainingChances: true,
                    remainingChances: 2,
                    maxChances: 3,
                    stageDisplayName: "Stage 1-1"));

                var topLeftStack = FindRequiredRect(hudView.transform, "HudTopLeftStack");
                var topRightStack = FindRequiredRect(hudView.transform, "HudTopRightStack");
                var bottomRightStack = FindRequiredRect(hudView.transform, "HudBottomRightStack");

                AssertStackTransform(topLeftStack, new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f), new Vector2(24.0f, -24.0f));
                AssertStackTransform(topRightStack, new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(-24.0f, -24.0f));
                AssertStackTransform(bottomRightStack, new Vector2(1.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(-24.0f, 24.0f));

                Assert.That(hudView.ObjectiveHudView.transform.parent, Is.SameAs(topLeftStack));
                AssertChildOrder(topRightStack, "StageName", "PauseButton", "TopologyBelt");
                AssertChildOrder(bottomRightStack, "Notifications", "ChancePanel");
                Assert.That(hudView.TopologyBeltView.transform.parent, Is.SameAs(topRightStack));
                Assert.That(hudView.NotificationView.transform.parent, Is.SameAs(bottomRightStack));
                Assert.That(hudView.ChancePanelView.transform.parent, Is.SameAs(bottomRightStack));
                Assert.That(hudView.ChancePanelView.ViewModel, Is.SameAs(chancePanelPresenter.ViewModel));
                Assert.That(hudView.TopologyBeltView.ViewModel, Is.SameAs(topologyHudPresenter.ViewModel));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [TestCase(1920.0f, 1080.0f)]
        [TestCase(1280.0f, 720.0f)]
        [TestCase(1440.0f, 1080.0f)]
        [TestCase(1080.0f, 1080.0f)]
        public void HUDController_CanonicalPrefab_HudStacksDoNotOverlapAtSupportedLandscapeResolutions(
            float width,
            float height)
        {
            var rootObject = new GameObject(
                $"HUDController_CanonicalPrefab_HudStacksDoNotOverlapAtSupportedLandscapeResolutions_{width}_{height}",
                typeof(RectTransform));

            try
            {
                var rootRect = (RectTransform)rootObject.transform;
                rootRect.sizeDelta = new Vector2(width, height);

                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var playerStatusPresenter = new PlayerStatusPresenter();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var chancePanelPresenter = new ChancePanelPresenter();
                var topologyHudPresenter = new TopologyHudPresenter();
                var notificationPresenter = new NotificationPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    topologyHudPresenter,
                    playerStatusPresenter,
                    notificationPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    topologyHudPresenter.ViewModel,
                    playerStatusPresenter.ViewModel,
                    notificationPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot(
                    hasRemainingChances: true,
                    remainingChances: 2,
                    maxChances: 3,
                    stageDisplayName: "Stage 1-1"));

                var hudRect = (RectTransform)hudView.transform;
                hudRect.anchorMin = Vector2.zero;
                hudRect.anchorMax = Vector2.zero;
                hudRect.pivot = Vector2.zero;
                hudRect.sizeDelta = new Vector2(width, height);

                LayoutRebuilder.ForceRebuildLayoutImmediate(hudRect);
                Canvas.ForceUpdateCanvases();

                var topLeftStack = FindRequiredRect(hudView.transform, "HudTopLeftStack");
                var topRightStack = FindRequiredRect(hudView.transform, "HudTopRightStack");
                var bottomRightStack = FindRequiredRect(hudView.transform, "HudBottomRightStack");

                AssertNoOverlap(topLeftStack, topRightStack);
                AssertNoOverlap(topLeftStack, bottomRightStack);
                AssertNoOverlap(topRightStack, bottomRightStack);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDController_CanonicalPrefab_RemovesDuplicateChancePanelViews()
        {
            var rootObject = new GameObject("HUDController_CanonicalPrefab_RemovesDuplicateChancePanelViews");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var duplicateObject = new GameObject("DuplicateChancePanel", typeof(RectTransform), typeof(ChancePanelView));
                duplicateObject.transform.SetParent(hudView.transform, false);

                Assert.That(hudView.GetComponentsInChildren<ChancePanelView>(true).Length, Is.GreaterThanOrEqualTo(2));

                var source = new ManualGameplayUiPresentationSource();
                var playerStatusPresenter = new PlayerStatusPresenter();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var chancePanelPresenter = new ChancePanelPresenter();
                var topologyHudPresenter = new TopologyHudPresenter();
                var notificationPresenter = new NotificationPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    chancePanelPresenter,
                    topologyHudPresenter,
                    playerStatusPresenter,
                    notificationPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    chancePanelPresenter.ViewModel,
                    topologyHudPresenter.ViewModel,
                    playerStatusPresenter.ViewModel,
                    notificationPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot(hasRemainingChances: true, remainingChances: 3, maxChances: 3));

                var chancePanelViews = hudView.GetComponentsInChildren<ChancePanelView>(true);
                Assert.That(chancePanelViews.Length, Is.EqualTo(1));
                Assert.That(chancePanelViews[0].ViewModel, Is.SameAs(chancePanelPresenter.ViewModel));
                Assert.That(chancePanelViews[0].SlotViews.Count, Is.EqualTo(3));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDController_CanonicalPrefab_RendersStageNameFromSnapshot()
        {
            var rootObject = new GameObject("HUDController_CanonicalPrefab_RendersStageNameFromSnapshot");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var playerStatusPresenter = new PlayerStatusPresenter();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var notificationPresenter = new NotificationPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    playerStatusPresenter,
                    notificationPresenter);
                using var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    playerStatusPresenter.ViewModel,
                    notificationPresenter.ViewModel);

                controller.AttachView(hudView);
                source.PublishSnapshot(CreateSnapshot(stageDisplayName: "Stage 1-1"));

                var stageLabel = FindRequiredRect(hudView.transform, "StageName").GetComponent<TMPro.TMP_Text>();
                Assert.That(stageLabel, Is.Not.Null);
                Assert.That(stageLabel.gameObject.activeSelf, Is.True);
                Assert.That(stageLabel.text, Is.EqualTo("Stage 1-1"));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDPrefab_HasObjectiveHudView()
        {
            var rootObject = new GameObject("HUDPrefab_HasObjectiveHudView");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                Assert.That(hudView.ObjectiveHudView, Is.Not.Null);
                Assert.That(hudView.ObjectiveHudView.transform.name, Is.EqualTo("ObjectiveHud"));
                Assert.That(hudView.ObjectiveHudView.GetComponent<Button>(), Is.Not.Null);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDController_Dispose_DetachesAllHudBindings()
        {
            var rootObject = new GameObject("HUDController_Dispose_DetachesAllHudBindings");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);

                var source = new ManualGameplayUiPresentationSource();
                var playerStatusPresenter = new PlayerStatusPresenter();
                var stageInfoPresenter = new StageInfoPresenter();
                var objectiveHudPresenter = new ObjectiveHudPresenter();
                var notificationPresenter = new NotificationPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    stageInfoPresenter,
                    objectiveHudPresenter,
                    playerStatusPresenter,
                    notificationPresenter);
                var controller = new HUDController(
                    rootPresenter.ViewModel,
                    stageInfoPresenter.ViewModel,
                    objectiveHudPresenter.ViewModel,
                    playerStatusPresenter.ViewModel,
                    notificationPresenter.ViewModel);

                controller.AttachView(hudView);
                controller.Dispose();

                Assert.That(hudView.ViewModel, Is.Null);
                Assert.That(hudView.StageInfoViewModel, Is.Null);
                Assert.That(hudView.ObjectiveHudView.ViewModel, Is.Null);
                Assert.That(hudView.PlayerStatusView.ViewModel, Is.Null);
                Assert.That(hudView.NotificationView.ViewModel, Is.Null);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        private static GameplayUiCanvasRootView CreateCanonicalRootView(GameObject rootObject, out HUDRootView hudView)
        {
            var rootShellPrefab = Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
            Assert.That(rootShellPrefab, Is.Not.Null);

            var rootShellInstance = Object.Instantiate(rootShellPrefab, rootObject.transform, false);
            var rootView = rootShellInstance.GetComponent<GameplayUiCanvasRootView>();
            Assert.That(rootView, Is.Not.Null);
            rootView.EnsureHierarchy();

            var hudLayer = rootView.transform.Find("HudLayer");
            Assert.That(hudLayer, Is.Not.Null);
            hudView = UiTestPrefabAssetUtility.InstantiateHudPrefab(hudLayer.GetComponent<RectTransform>());
            AttachHudView(rootView, hudView);
            return rootView;
        }

        private static void AttachHudView(GameplayUiCanvasRootView rootView, HUDRootView hudView)
        {
            var attachMethod = typeof(GameplayUiCanvasRootView).GetMethod(
                "AttachHudView",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(attachMethod, Is.Not.Null);
            attachMethod.Invoke(rootView, new object[] { hudView });
        }

        private static UIPresentationSnapshot CreateSnapshot(
            bool isPaused = false,
            bool isUiBlocked = false,
            bool hasBlockingPresentation = false,
            GameplayUiActionKind activeActionKind = GameplayUiActionKind.None,
            bool isRecoveryPhase = false,
            bool canMoveThisTick = true,
            bool canStartActionThisTick = true,
            GameplayUiActionResolutionKind lastOutcome = GameplayUiActionResolutionKind.None,
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0,
            string stageDisplayName = "")
        {
            return new UIPresentationSnapshot(
                new UITickSlice(
                    lastReducedTickIndex: 4,
                    finalTopology: new GameplayUiTopology(GameplayUiFace.Front),
                    isStageCleared: false,
                    isTopologyTransitionActive: false),
                new UIInteractionSlice(
                    isPaused,
                    canAcceptGameplayCommands: !isPaused && !hasBlockingPresentation,
                    hasBlockingGameplayPresentation: hasBlockingPresentation,
                    isUiGameplayInputBlocked: isUiBlocked),
                new UIStageSlice(
                    string.IsNullOrWhiteSpace(stageDisplayName)
                        ? StageId.None
                        : StageId.CreateOrThrow("stage-1-1"),
                    stageDisplayName),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: GameplayUiDirection.Up,
                    activeActionKind: activeActionKind,
                    isRecoveryPhase: isRecoveryPhase,
                    canMoveThisTick: canMoveThisTick,
                    canStartActionThisTick: canStartActionThisTick,
                    lastResolvedOutcome: lastOutcome,
                    lastResolvedTickIndex: lastOutcome == GameplayUiActionResolutionKind.None ? 0 : 4,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0,
                    hasRemainingChances: hasRemainingChances,
                    remainingChances: remainingChances,
                    maxChances: maxChances),
                new UINotificationLedgerSlice(new[]
                {
                    new UINotificationRecord(
                        new UITickEventKey(
                            tickIndex: 4,
                            eventKind: UITickEventKind.PlayerActionResolved,
                            actorEntityId: 10,
                            actionKind: GameplayUiActionKind.Flip,
                            actionSequence: 2,
                            resolutionKind: GameplayUiActionResolutionKind.Success),
                        UITickEventKind.PlayerActionResolved,
                        damageAmount: 0,
                        expireAfterTickIndex: 8),
                }));
        }

        private static int CountCharacter(string value, char character)
        {
            var count = 0;
            if (value == null)
            {
                return count;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == character)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSlotsWithChild(ChancePanelView chancePanelView, string childName)
        {
            var count = 0;
            var slots = chancePanelView.GetComponentsInChildren<ChanceSlotView>(true);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].transform.Find(childName) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static RectTransform FindRequiredRect(Transform root, string name)
        {
            var transforms = root.GetComponentsInChildren<RectTransform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == name)
                {
                    return transforms[i];
                }
            }

            Assert.Fail($"Expected to find RectTransform named '{name}'.");
            return null;
        }

        private static void AssertStackTransform(
            RectTransform stack,
            Vector2 expectedAnchor,
            Vector2 expectedPivot,
            Vector2 expectedPosition)
        {
            Assert.That(stack.anchorMin, Is.EqualTo(expectedAnchor));
            Assert.That(stack.anchorMax, Is.EqualTo(expectedAnchor));
            Assert.That(stack.pivot, Is.EqualTo(expectedPivot));
            Assert.That(stack.anchoredPosition, Is.EqualTo(expectedPosition));
            var layoutGroup = stack.GetComponent<VerticalLayoutGroup>();
            Assert.That(layoutGroup, Is.Not.Null);
            Assert.That(layoutGroup.spacing, Is.EqualTo(8.0f));
            Assert.That(layoutGroup.childControlWidth, Is.False);
            Assert.That(layoutGroup.childControlHeight, Is.False);
            Assert.That(layoutGroup.childForceExpandWidth, Is.False);
            Assert.That(layoutGroup.childForceExpandHeight, Is.False);
        }

        private static void AssertChildOrder(RectTransform parent, params string[] childNames)
        {
            Assert.That(parent.childCount, Is.GreaterThanOrEqualTo(childNames.Length));
            for (var i = 0; i < childNames.Length; i++)
            {
                Assert.That(parent.GetChild(i).name, Is.EqualTo(childNames[i]));
            }
        }

        private static void AssertNoOverlap(RectTransform first, RectTransform second)
        {
            var firstRect = GetWorldRect(first);
            var secondRect = GetWorldRect(second);
            var overlaps = firstRect.xMin < secondRect.xMax
                && firstRect.xMax > secondRect.xMin
                && firstRect.yMin < secondRect.yMax
                && firstRect.yMax > secondRect.yMin;

            Assert.That(overlaps, Is.False, $"{first.name} overlaps {second.name}.");
        }

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void DestroySupportObjects(GameObject rootObject)
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }

            if (rootObject != null)
            {
                Object.DestroyImmediate(rootObject);
            }
        }
    }
}

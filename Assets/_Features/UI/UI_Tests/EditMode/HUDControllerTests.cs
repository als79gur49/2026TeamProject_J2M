using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
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
        public void HUDRootView_CanonicalPrefab_ValidateAuthoredStructurePasses()
        {
            var hudPrefab = UiTestPrefabAssetUtility.LoadHudPrefab();

            Assert.DoesNotThrow(() => hudPrefab.ValidateAuthoredStructureOrThrow());
        }

        [Test]
        public void HUDRootView_MissingRequiredChildReference_ThrowsAuthoredContractFailure()
        {
            var rootObject = new GameObject("HUDRootView_MissingRequiredChildReference_ThrowsAuthoredContractFailure");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                LogAssert.Expect(
                    LogType.Warning,
                    "HUDRootView on 'GameplayHudRoot(Clone)' is missing serialized reference '_chancePanelView'.");
                SetSerializedReference(hudView, "_chancePanelView", null);

                var exception = Assert.Throws<InvalidOperationException>(() => hudView.ValidateAuthoredStructureOrThrow());
                Assert.That(exception.Message, Does.Contain("_chancePanelView"));
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

                var objectiveListRoot = GetSerializedReference<RectTransform>(hudView.ObjectiveHudView, "_objectiveListRoot");
                AssertOwnedBy(objectiveListRoot, topLeftStack);
                AssertOwnedBy(GetSerializedReference<TMP_Text>(hudView, "_stageNameLabel").transform, topRightStack);
                AssertOwnedBy(GetSerializedReference<Button>(hudView, "_pauseButton").transform, topRightStack);
                AssertOwnedBy(hudView.SurfaceIndicatorView.transform, topRightStack);
                AssertOwnedBy(hudView.NotificationView.transform, bottomRightStack);
                AssertOwnedBy(hudView.ChancePanelView.transform, bottomRightStack);
                Assert.That(hudView.ChancePanelView.ViewModel, Is.SameAs(chancePanelPresenter.ViewModel));
                Assert.That(hudView.SurfaceIndicatorView.ViewModel, Is.SameAs(topologyHudPresenter.ViewModel));
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
        public void HUDController_CanonicalPrefab_RuntimeChanceAndTopologyContentFitsWithinHudModules()
        {
            var rootObject = new GameObject(
                "HUDController_CanonicalPrefab_RuntimeChanceAndTopologyContentFitsWithinHudModules",
                typeof(RectTransform));

            try
            {
                var rootRect = (RectTransform)rootObject.transform;
                rootRect.sizeDelta = new Vector2(1280.0f, 720.0f);

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
                    remainingChances: 3,
                    maxChances: 3,
                    stageDisplayName: "Stage 1-1"));

                var hudRect = (RectTransform)hudView.transform;
                hudRect.anchorMin = Vector2.zero;
                hudRect.anchorMax = Vector2.zero;
                hudRect.pivot = Vector2.zero;
                hudRect.sizeDelta = new Vector2(1280.0f, 720.0f);

                LayoutRebuilder.ForceRebuildLayoutImmediate(hudRect);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)hudView.ChancePanelView.transform);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)hudView.SurfaceIndicatorView.transform);
                Canvas.ForceUpdateCanvases();

                var chancePanel = (RectTransform)hudView.ChancePanelView.transform;
                var slotContainer = FindRequiredRect(chancePanel, "SlotContainer");
                var floatingFeedbackRoot = FindRequiredRect(chancePanel, "FloatingFeedbackRoot");
                AssertWorldRectContains(chancePanel, slotContainer);
                AssertWorldRectContains(chancePanel, floatingFeedbackRoot);

                var cubeMapView = GetSerializedReference<SurfaceCubeMapView>(hudView.SurfaceIndicatorView, "_cubeMapView");
                AssertOwnedBy(cubeMapView.transform, hudView.transform);
                Assert.That(cubeMapView.PreviewImage, Is.Not.Null);
                AssertCubeMapPreviewHasRenderableLayoutContract(cubeMapView);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDPrefab_AuthorsChanceAndSurfaceIndicatorModulesInPrefabHierarchy()
        {
            var hudPrefab = UiTestPrefabAssetUtility.LoadHudPrefab();
            var serializedChancePanel = GetSerializedReference<ChancePanelView>(hudPrefab, "_chancePanelView");
            var serializedSurfaceIndicator = GetSerializedReference<SurfaceIndicatorView>(hudPrefab, "_surfaceIndicatorView");

            var chancePanels = hudPrefab.GetComponentsInChildren<ChancePanelView>(true);
            Assert.That(chancePanels.Length, Is.EqualTo(1));
            Assert.That(chancePanels[0], Is.SameAs(serializedChancePanel));

            var surfaceIndicators = hudPrefab.GetComponentsInChildren<SurfaceIndicatorView>(true);
            Assert.That(surfaceIndicators.Length, Is.EqualTo(1));
            Assert.That(surfaceIndicators[0], Is.SameAs(serializedSurfaceIndicator));

            var topRightStack = FindRequiredRect(hudPrefab.transform, "HudTopRightStack");
            var bottomRightStack = FindRequiredRect(hudPrefab.transform, "HudBottomRightStack");
            AssertOwnedBy(GetSerializedReference<TMP_Text>(hudPrefab, "_stageNameLabel").transform, topRightStack);
            AssertOwnedBy(GetSerializedReference<Button>(hudPrefab, "_pauseButton").transform, topRightStack);
            AssertOwnedBy(serializedSurfaceIndicator.transform, topRightStack);
            AssertOwnedBy(serializedChancePanel.transform, bottomRightStack);

            var slotContainer = FindRequiredRect(serializedChancePanel.transform, "SlotContainer");
            var slotViews = serializedChancePanel.GetComponentsInChildren<ChanceSlotView>(true);
            Assert.That(slotViews.Length, Is.EqualTo(3));
            AssertSerializedReference(serializedChancePanel, "_slotContainer", slotContainer);
            AssertSerializedArrayCount(serializedChancePanel, "_slotViews", 3);

            foreach (var slot in slotViews)
            {
                AssertOwnedBy(slot.transform, slotContainer);
                AssertSerializedReferenceIsAssigned(slot, "_filledIcon");
                AssertSerializedReferenceIsAssigned(slot, "_emptyIcon");
                AssertSerializedReferenceIsAssigned(slot, "_glow");
                AssertSerializedReferenceIsAssigned(slot, "_canvasGroup");
            }

            var cubeMapView = GetSerializedReference<SurfaceCubeMapView>(serializedSurfaceIndicator, "_cubeMapView");
            AssertOwnedBy(cubeMapView.transform, hudPrefab.transform);
            AssertSerializedReference(serializedSurfaceIndicator, "_cubeMapView", cubeMapView);
            AssertSerializedReferenceIsAssigned(cubeMapView, "_previewImage");
            AssertSerializedReferenceIsAssigned(cubeMapView, "_cubeMapPrefab");
            AssertCubeMapPreviewHasRenderableLayoutContract(cubeMapView);
        }

        [Test]
        public void SurfaceCubeMapView_ResolvesCubeMapFaces_FromLocalPositions()
        {
            Assert.That(
                SurfaceCubeMapView.TryResolveFaceRole(new Vector3(0.0f, -0.2f, 0.0f), out var floorRole),
                Is.True);
            Assert.That(floorRole, Is.EqualTo(SurfaceCubeMapFaceRole.Floor));
            Assert.That(
                SurfaceCubeMapView.TryResolveFaceRole(new Vector3(0.0f, 0.0f, 0.2f), out var frontRole),
                Is.True);
            Assert.That(frontRole, Is.EqualTo(SurfaceCubeMapFaceRole.Front));
            Assert.That(
                SurfaceCubeMapView.TryResolveFaceRole(new Vector3(0.0f, 0.2f, 0.0f), out var ceilingRole),
                Is.True);
            Assert.That(ceilingRole, Is.EqualTo(SurfaceCubeMapFaceRole.Ceiling));
            Assert.That(
                SurfaceCubeMapView.TryResolveFaceRole(new Vector3(0.0f, 0.0f, -0.2f), out var backRole),
                Is.True);
            Assert.That(backRole, Is.EqualTo(SurfaceCubeMapFaceRole.Back));
            Assert.That(
                SurfaceCubeMapView.TryResolveFaceRole(new Vector3(-0.2f, 0.0f, 0.0f), out var leftRole),
                Is.True);
            Assert.That(leftRole, Is.EqualTo(SurfaceCubeMapFaceRole.Left));
            Assert.That(SurfaceCubeMapView.IsSelectableFace(leftRole), Is.False);
            Assert.That(
                SurfaceCubeMapView.TryResolveFaceRole(new Vector3(0.2f, 0.0f, 0.0f), out var rightRole),
                Is.True);
            Assert.That(rightRole, Is.EqualTo(SurfaceCubeMapFaceRole.Right));
            Assert.That(SurfaceCubeMapView.IsSelectableFace(rightRole), Is.False);
        }

        [Test]
        public void SurfaceCubeMapPreviewLayer_IsExcludedFromGameplayMainCameras()
        {
            var previewLayer = LayerMask.NameToLayer(SurfaceCubeMapView.PreviewLayerName);
            Assert.That(previewLayer, Is.GreaterThanOrEqualTo(0));

            var previewMask = 1 << previewLayer;
            var scenePaths = new[]
            {
                "Assets/Scenes/CombinedGameplayShowcase.unity",
                "Assets/Scenes/TutorialScene.unity",
                "Assets/Scenes/UIAudioScene.unity",
            };

            foreach (var scenePath in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var mainCameras = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .Where(camera => camera.CompareTag("MainCamera"))
                    .ToArray();

                Assert.That(mainCameras, Is.Not.Empty, scenePath);
                foreach (var mainCamera in mainCameras)
                {
                    Assert.That(
                        mainCamera.cullingMask & previewMask,
                        Is.EqualTo(0),
                        $"{scenePath} MainCamera '{mainCamera.name}' must not render {SurfaceCubeMapView.PreviewLayerName}.");
                }
            }
        }

        [Test]
        public void HUDController_CanonicalPrefab_DuplicateChancePanelView_FailsPrefabContract()
        {
            var rootObject = new GameObject("HUDController_CanonicalPrefab_DuplicateChancePanelView_FailsPrefabContract");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var duplicateObject = UnityEngine.Object.Instantiate(hudView.ChancePanelView.gameObject, hudView.transform, false);
                duplicateObject.name = "DuplicateChancePanel";

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

                Assert.Throws<InvalidOperationException>(() => controller.AttachView(hudView));
                Assert.That(hudView.GetComponentsInChildren<ChancePanelView>(true).Length, Is.EqualTo(2));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ChancePanelView_MaxChancesBeyondAuthoredSlots_Throws()
        {
            var rootObject = new GameObject("ChancePanelView_MaxChancesBeyondAuthoredSlots_Throws");

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

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    source.PublishSnapshot(CreateSnapshot(hasRemainingChances: true, remainingChances: 4, maxChances: 4)));
                Assert.That(exception.Message, Does.Contain("authored slots"));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_ClonesAuthoredObjectiveItemTemplate()
        {
            var rootObject = new GameObject("ObjectiveHudView_ClonesAuthoredObjectiveItemTemplate");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("reach-exit", "Reach the exit zone", false, false),
                    });

                objectiveView.Bind(viewModel);

                var runtimeItems = objectiveListRoot
                    .Cast<Transform>()
                    .Where(child => child != itemTemplate && child.gameObject.activeSelf)
                    .ToArray();

                Assert.That(itemTemplate.gameObject.activeSelf, Is.False);
                Assert.That(runtimeItems, Has.Length.EqualTo(1));
                Assert.That(runtimeItems[0].parent, Is.SameAs(objectiveListRoot));
                Assert.That(runtimeItems[0].gameObject.activeSelf, Is.True);
                Assert.That(FindObjectiveLabel(runtimeItems[0]).text, Is.EqualTo("Reach the exit zone"));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_NewObjectiveRow_PlaysInState()
        {
            var rootObject = new GameObject("ObjectiveHudView_NewObjectiveRow_PlaysInState");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("reach-exit", "Reach the exit zone", false, false),
                    });

                objectiveView.Bind(viewModel);

                var runtimeItem = FindActiveObjectiveRuntimeItem(objectiveListRoot, itemTemplate);
                var animator = runtimeItem.GetComponent<Animator>();
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("In"), Is.True);
                Assert.That(animator.GetBool("Active"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_InitialRows_EnterSerially()
        {
            var rootObject = new GameObject("ObjectiveHudView_InitialRows_EnterSerially");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);

                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                Assert.That(aRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"), Is.Null);
                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c"), Is.Null);

                CompleteObjectiveEnter(objectiveView, aRow);

                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c"), Is.Null);

                CompleteObjectiveEnter(objectiveView, bRow);

                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                Assert.That(cRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_EnterFinished_DoesNotStartNextEnterSynchronously()
        {
            var rootObject = new GameObject("ObjectiveHudView_EnterFinished_DoesNotStartNextEnterSynchronously");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterWithoutScheduler(
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));

                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"), Is.Null);
                Assert.That(GetPrivateField<bool>(objectiveView, "_transitionAdvanceRequested"), Is.True);
                Assert.That(GetPrivateField<string>(objectiveView, "_transitioningStableId"), Is.Empty);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_UpdateAfterEnterFinished_StartsNextEnter()
        {
            var rootObject = new GameObject("ObjectiveHudView_UpdateAfterEnterFinished_StartsNextEnter");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterWithoutScheduler(
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));

                ForceObjectiveSchedulerDue(objectiveView);

                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(GetPrivateField<string>(objectiveView, "_transitioningStableId"), Is.EqualTo("b"));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_CollectionTransitionGap_IsRespected()
        {
            var rootObject = new GameObject("ObjectiveHudView_CollectionTransitionGap_IsRespected");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterWithoutScheduler(
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));

                var allowedAt = GetPrivateField<float>(objectiveView, "_nextTransitionAllowedAt");
                InvokeObjectiveScheduler(objectiveView, allowedAt - 0.001f);

                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"), Is.Null);

                InvokeObjectiveScheduler(objectiveView, allowedAt);

                Assert.That(
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b").VisualState,
                    Is.EqualTo(ObjectiveRowVisualState.Entering));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_DismissFinished_DoesNotStartNextTransitionSynchronously()
        {
            var rootObject = new GameObject("ObjectiveHudView_DismissFinished_DoesNotStartNextTransitionSynchronously");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterSequence(objectiveView, objectiveListRoot, itemTemplate, "a", "b", "c");
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                    });

                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));

                CompleteObjectiveDismissWithoutScheduler(bRow);

                Assert.That(cRow.gameObject.activeSelf, Is.True);
                Assert.That(cRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Idle));
                Assert.That(GetPrivateField<bool>(objectiveView, "_transitionAdvanceRequested"), Is.True);
                Assert.That(GetPrivateField<string>(objectiveView, "_transitioningStableId"), Is.Empty);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_EnterTween_ClampsLargeDelta()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_EnterTween_ClampsLargeDelta");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                var finished = false;
                row.TransitionFinished += (_, kind) => finished |= kind == ObjectiveRowTransitionKind.Enter;

                row.Tick(1.0f);

                var layout = row.GetComponent<LayoutElement>();
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(finished, Is.False);
                Assert.That(layout.preferredHeight, Is.GreaterThan(0.0f).And.LessThan(60.0f));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_CollapseTween_ClampsLargeDelta()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_CollapseTween_ClampsLargeDelta");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                CompleteObjectiveEnter(objectiveView, row);
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", true, true),
                    });

                row.Tick(0.0f);
                var animator = row.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                animator.Play("Out", 0, 1.0f);
                animator.Update(1.0f);
                row.Tick(0.0f);
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Collapsing));

                var finished = false;
                row.TransitionFinished += (_, kind) => finished |= kind == ObjectiveRowTransitionKind.Dismiss;
                row.Tick(1.0f);

                var layout = row.GetComponent<LayoutElement>();
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Collapsing));
                Assert.That(finished, Is.False);
                Assert.That(layout.preferredHeight, Is.GreaterThan(0.0f).And.LessThan(60.0f));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_ForceResetForPool_InactiveObject_DoesNotTouchAnimator()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_ForceResetForPool_InactiveObject_DoesNotTouchAnimator");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                row.gameObject.SetActive(false);

                row.ForceResetForPool();

                LogAssert.NoUnexpectedReceived();
                var layout = row.GetComponent<LayoutElement>();
                Assert.That(row.StableId, Is.Empty);
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Hidden));
                Assert.That(layout.ignoreLayout, Is.True);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_ForceClear_CancelsPendingAdvance()
        {
            var rootObject = new GameObject("ObjectiveHudView_ForceClear_CancelsPendingAdvance");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterWithoutScheduler(
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));
                Assert.That(GetPrivateField<bool>(objectiveView, "_transitionAdvanceRequested"), Is.True);

                viewModel.Reset();
                ForceObjectiveSchedulerDue(objectiveView);

                Assert.That(GetPrivateField<bool>(objectiveView, "_transitionAdvanceRequested"), Is.False);
                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"), Is.Null);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_JustSatisfiedRow_PlaysActiveAndHoldsOutGate()
        {
            var rootObject = new GameObject("ObjectiveHudView_JustSatisfiedRow_PlaysActiveAndHoldsOutGate");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("reach-exit", "Reach the exit zone", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnter(
                    objectiveView,
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "reach-exit"));
                viewModel.SetState(
                    true,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("reach-exit", "Reach the exit zone", true, true),
                    });

                var runtimeItem = FindActiveObjectiveRuntimeItem(objectiveListRoot, itemTemplate);
                var animator = runtimeItem.GetComponent<Animator>();
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Active"), Is.True);
                Assert.That(animator.GetBool("Active"), Is.True);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_GroupedRow_InitialPartialEnter_DoesNotPulse()
        {
            var rootObject = new GameObject("ObjectiveHudView_GroupedRow_InitialPartialEnter_DoesNotPulse");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 2, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);

                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                Assert.That(FindObjectiveLabel(row.transform).text, Is.EqualTo("Place a push box on the button (2/4)"));
                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(0));
                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(row.GetComponent<Animator>().GetBool("Active"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_GroupedRow_VisibleIdleProgressIncrease_PulsesOnce()
        {
            var rootObject = new GameObject("ObjectiveHudView_GroupedRow_VisibleIdleProgressIncrease_PulsesOnce");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);

                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 2, 4, isSatisfied: false, justSatisfied: false),
                    });

                Assert.That(FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons"), Is.SameAs(row));
                Assert.That(FindObjectiveLabel(row.transform).text, Is.EqualTo("Place a push box on the button (2/4)"));
                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(1));
                Assert.That(GetProgressHighlightAlpha(row), Is.GreaterThan(0.0f));
                Assert.That(GetProgressPulseScaleTarget(row).localScale.x, Is.GreaterThan(1.0f));
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Idle));
                Assert.That(row.GetComponent<Animator>().GetBool("Active"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_GroupedRow_FinalComplete_UsesActiveDismissNotProgressPulse()
        {
            var rootObject = new GameObject("ObjectiveHudView_GroupedRow_FinalComplete_UsesActiveDismissNotProgressPulse");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 3, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);

                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 4, 4, isSatisfied: true, justSatisfied: true),
                    });

                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(0));
                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                Assert.That(row.GetComponent<Animator>().GetBool("Active"), Is.True);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_PendingEnterProgressChange_UpdatesBaselineWithoutPulse()
        {
            var rootObject = new GameObject("ObjectiveHudView_PendingEnterProgressChange_UpdatesBaselineWithoutPulse");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        CreateGroupedObjectiveRow("buttons", 0, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                CompleteObjectiveEnter(objectiveView, aRow);
                var pendingRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");

                Assert.That(FindObjectiveLabel(pendingRow.transform).text, Is.EqualTo("Place a push box on the button (1/4)"));
                Assert.That(GetProgressPulsePlayCount(pendingRow), Is.EqualTo(0));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_ProgressPulse_FadesHighlightToZero()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_ProgressPulse_FadesHighlightToZero");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);

                row.PlayProgressPulse();
                Assert.That(GetProgressHighlightAlpha(row), Is.GreaterThan(0.0f));

                row.Tick(1.0f);

                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(GetProgressPulseScaleTarget(row).localScale, Is.EqualTo(Vector3.one));
                Assert.That(GetPrivateField<bool>(row, "_isProgressPulsePlaying"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_ProgressPulse_RestartsCleanly()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_ProgressPulse_RestartsCleanly");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);

                row.PlayProgressPulse();
                row.Tick(0.1f);
                var fadedAlpha = GetProgressHighlightAlpha(row);

                row.PlayProgressPulse();

                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(2));
                Assert.That(GetProgressHighlightAlpha(row), Is.GreaterThan(fadedAlpha));

                row.Tick(1.0f);
                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(GetProgressPulseScaleTarget(row).localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_ForceResetForPool_ClearsProgressHighlight()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_ForceResetForPool_ClearsProgressHighlight");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);
                row.PlayProgressPulse();

                row.ForceResetForPool();

                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(GetProgressPulseScaleTarget(row).localScale, Is.EqualTo(Vector3.one));
                Assert.That(GetPrivateField<bool>(row, "_isProgressPulsePlaying"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudRowView_CompleteAndDismiss_CancelsProgressPulse()
        {
            var rootObject = new GameObject("ObjectiveHudRowView_CompleteAndDismiss_CancelsProgressPulse");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 3, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                CompleteObjectiveEnter(objectiveView, row);
                row.PlayProgressPulse();

                row.CompleteAndDismiss();

                Assert.That(GetProgressHighlightAlpha(row), Is.Zero);
                Assert.That(GetProgressPulseScaleTarget(row).localScale, Is.EqualTo(Vector3.one));
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                Assert.That(row.GetComponent<Animator>().GetBool("Active"), Is.True);
                Assert.That(GetPrivateField<bool>(row, "_isProgressPulsePlaying"), Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_PendingEnterAlreadySatisfied_IsSkipped()
        {
            var rootObject = new GameObject("ObjectiveHudView_PendingEnterAlreadySatisfied_IsSkipped");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        CreateGroupedObjectiveRow("buttons", 0, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        CreateGroupedObjectiveRow("buttons", 4, 4, isSatisfied: true, justSatisfied: true),
                    });

                CompleteObjectiveEnter(objectiveView, aRow);

                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons"), Is.Null);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_EnteringRowBecomesSatisfied_CompletesAfterEnter()
        {
            var rootObject = new GameObject("ObjectiveHudView_EnteringRowBecomesSatisfied_CompletesAfterEnter");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 3, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 4, 4, isSatisfied: true, justSatisfied: true),
                    });

                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));

                CompleteObjectiveEnter(objectiveView, row);

                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(0));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_ObjectiveIdentityChange_ClearsProgressBaselines()
        {
            var rootObject = new GameObject("ObjectiveHudView_ObjectiveIdentityChange_ClearsProgressBaselines");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 1, 4, isSatisfied: false, justSatisfied: false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnter(
                    objectiveView,
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons"));

                viewModel.SetState(
                    true,
                    "objective-b",
                    new[]
                    {
                        CreateGroupedObjectiveRow("buttons", 2, 4, isSatisfied: false, justSatisfied: false),
                    });

                var row = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "buttons");
                Assert.That(FindObjectiveLabel(row.transform).text, Is.EqualTo("Place a push box on the button (2/4)"));
                Assert.That(GetProgressPulsePlayCount(row), Is.EqualTo(0));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_InsertingMiddleRow_UsesTargetSiblingPosition()
        {
            var rootObject = new GameObject("ObjectiveHudView_InsertingMiddleRow_UsesTargetSiblingPosition");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnter(objectiveView, FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                CompleteObjectiveEnter(objectiveView, cRow);

                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                Assert.That(FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c"), Is.SameAs(cRow));
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(cRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Idle));
                Assert.That(aRow.transform.GetSiblingIndex(), Is.LessThan(bRow.transform.GetSiblingIndex()));
                Assert.That(bRow.transform.GetSiblingIndex(), Is.LessThan(cRow.transform.GetSiblingIndex()));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_HidesRootAndRuntimeItems_WhenNoObjective()
        {
            var rootObject = new GameObject("ObjectiveHudView_HidesRootAndRuntimeItems_WhenNoObjective");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveRoot = GetSerializedReference<GameObject>(objectiveView, "_root");
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("reach-exit", "Reach the exit zone", false, false),
                    });

                objectiveView.Bind(viewModel);
                viewModel.Reset();

                var runtimeItems = objectiveListRoot
                    .Cast<Transform>()
                    .Where(child => child != itemTemplate && child.name.StartsWith("Objective_Item_Runtime_", StringComparison.Ordinal))
                    .ToArray();

                Assert.That(objectiveRoot.activeSelf, Is.False);
                Assert.That(runtimeItems, Has.Length.EqualTo(1));
                Assert.That(runtimeItems[0].gameObject.activeSelf, Is.False);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_RemovingMiddleRow_KeepsLowerRowInstance()
        {
            var rootObject = new GameObject("ObjectiveHudView_RemovingMiddleRow_KeepsLowerRowInstance");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnter(objectiveView, FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));
                CompleteObjectiveEnter(objectiveView, FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"));
                CompleteObjectiveEnter(objectiveView, FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c"));
                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");

                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                Assert.That(FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c"), Is.SameAs(cRow));
                Assert.That(bRow.gameObject.activeSelf, Is.True);
                Assert.That(bRow.transform.GetSiblingIndex(), Is.LessThan(cRow.transform.GetSiblingIndex()));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_CompletedMiddleRow_CollapsesLayoutHeightBeforePooling()
        {
            var rootObject = new GameObject("ObjectiveHudView_CompletedMiddleRow_CollapsesLayoutHeightBeforePooling");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterSequence(objectiveView, objectiveListRoot, itemTemplate, "a", "b", "c");
                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                LayoutRebuilder.ForceRebuildLayoutImmediate(objectiveListRoot);

                var initialGap = Mathf.Abs(
                    ((RectTransform)cRow.transform).anchoredPosition.y -
                    ((RectTransform)aRow.transform).anchoredPosition.y);

                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", true, true),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                bRow.Tick(0.0f);

                var animator = bRow.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                animator.Play("Out", 0, 1.0f);
                animator.Update(1.0f);

                bRow.Tick(0.0f);
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Collapsing));
                bRow.Tick(0.1f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(objectiveListRoot);

                var bLayout = bRow.GetComponent<LayoutElement>();
                var collapsedGap = Mathf.Abs(
                    ((RectTransform)cRow.transform).anchoredPosition.y -
                    ((RectTransform)aRow.transform).anchoredPosition.y);

                Assert.That(bRow.gameObject.activeSelf, Is.True);
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Collapsing));
                Assert.That(bLayout.preferredHeight, Is.GreaterThan(0.0f).And.LessThan(60.0f));
                Assert.That(collapsedGap, Is.LessThan(initialGap));
                Assert.That(bRow.transform.GetSiblingIndex(), Is.LessThan(cRow.transform.GetSiblingIndex()));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_MultipleRemovedRows_ExitSerially()
        {
            var rootObject = new GameObject("ObjectiveHudView_MultipleRemovedRows_ExitSerially");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                        new ObjectiveConditionHudViewModel("d", "D", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterSequence(objectiveView, objectiveListRoot, itemTemplate, "a", "b", "c", "d");

                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("d", "D", false, false),
                    });

                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                Assert.That(cRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Idle));

                CompleteObjectiveDismiss(objectiveView, bRow);

                cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                Assert.That(cRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_ExitQueueRunsBeforeEnterQueue()
        {
            var rootObject = new GameObject("ObjectiveHudView_ExitQueueRunsBeforeEnterQueue");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                objectiveView.Bind(viewModel);
                CompleteObjectiveEnterSequence(objectiveView, objectiveListRoot, itemTemplate, "a", "b", "c");

                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("d", "D", false, false),
                        new ObjectiveConditionHudViewModel("c", "C", false, false),
                    });

                var bRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b");
                Assert.That(bRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "d"), Is.Null);

                CompleteObjectiveDismiss(objectiveView, bRow);

                var aRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a");
                var cRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "c");
                var dRow = FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "d");
                Assert.That(dRow.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
                Assert.That(aRow.transform.GetSiblingIndex(), Is.LessThan(dRow.transform.GetSiblingIndex()));
                Assert.That(dRow.transform.GetSiblingIndex(), Is.LessThan(cRow.transform.GetSiblingIndex()));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void ObjectiveHudView_StalePendingEnter_IsSkipped()
        {
            var rootObject = new GameObject("ObjectiveHudView_StalePendingEnter_IsSkipped");

            try
            {
                CreateCanonicalRootView(rootObject, out var hudView);
                var objectiveView = hudView.ObjectiveHudView;
                var objectiveListRoot = GetSerializedReference<RectTransform>(objectiveView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(objectiveView, "_objectiveItemTemplate");
                var viewModel = new ObjectiveHudViewModel();
                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                        new ObjectiveConditionHudViewModel("b", "B", false, false),
                    });

                objectiveView.Bind(viewModel);
                Assert.That(FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"), Is.Not.Null);

                viewModel.SetState(
                    true,
                    "objective-a",
                    new[]
                    {
                        new ObjectiveConditionHudViewModel("a", "A", false, false),
                    });

                CompleteObjectiveEnter(objectiveView, FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "a"));

                Assert.That(TryFindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, "b"), Is.Null);
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

                var stageLabel = GetSerializedReference<TMP_Text>(hudView, "_stageNameLabel");
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
                hudView.ObjectiveHudView.Bind(new ObjectiveHudViewModel());
                var objectiveListRoot = GetSerializedReference<RectTransform>(hudView.ObjectiveHudView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(hudView.ObjectiveHudView, "_objectiveItemTemplate");
                Assert.That(objectiveListRoot, Is.Not.Null);
                Assert.That(objectiveListRoot.name, Is.EqualTo("Objective_List"));
                Assert.That(itemTemplate, Is.Not.Null);
                Assert.That(itemTemplate.parent, Is.SameAs(objectiveListRoot));
                Assert.That(objectiveListRoot.childCount, Is.EqualTo(1));
                Assert.That(itemTemplate.gameObject.activeSelf, Is.False);
                Assert.That(itemTemplate.GetComponent<Animator>(), Is.Not.Null);
                Assert.That(itemTemplate.GetComponent<LayoutElement>(), Is.Not.Null);
                var rowView = itemTemplate.GetComponent<ObjectiveHudRowView>();
                Assert.That(rowView, Is.Not.Null);
                var layoutGroup = objectiveListRoot.GetComponent<VerticalLayoutGroup>();
                Assert.That(layoutGroup, Is.Not.Null);
                Assert.That(layoutGroup.childControlHeight, Is.True);
                Assert.That(layoutGroup.childForceExpandHeight, Is.False);
                Assert.That(
                    itemTemplate.GetComponentsInChildren<TMP_Text>(true).Any(label => label.name == "Label_Objective"),
                    Is.True);

                var overlay = itemTemplate
                    .GetComponentsInChildren<RectTransform>(true)
                    .SingleOrDefault(rect => rect.name == "ProgressHighlightOverlay");
                Assert.That(overlay, Is.Not.Null);
                AssertSerializedReference(rowView, "_progressHighlightGraphic", overlay.GetComponent<Graphic>());
                AssertSerializedReference(rowView, "_progressHighlightGroup", overlay.GetComponent<CanvasGroup>());
                AssertSerializedReference(rowView, "_progressPulseScaleTarget", overlay.parent);
                Assert.That(overlay.GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(overlay.GetComponent<Graphic>().color.a, Is.EqualTo(1.0f));
                Assert.That(overlay.GetComponent<Graphic>().raycastTarget, Is.False);
                Assert.That(overlay.GetSiblingIndex(), Is.LessThan(FindRequiredRect(itemTemplate, "Icon").GetSiblingIndex()));
                Assert.That(overlay.GetSiblingIndex(), Is.LessThan(FindRequiredRect(itemTemplate, "Text").GetSiblingIndex()));
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        private static TMP_Text FindObjectiveLabel(Transform item)
        {
            return item
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(label => label.name == "Label_Objective");
        }

        private static ObjectiveConditionHudViewModel CreateGroupedObjectiveRow(
            string stableId,
            int completedCount,
            int requiredCount,
            bool isSatisfied,
            bool justSatisfied)
        {
            return new ObjectiveConditionHudViewModel(
                stableId,
                $"Place a push box on the button ({completedCount}/{requiredCount})",
                isSatisfied,
                justSatisfied,
                isGrouped: true,
                completedCount: completedCount,
                requiredCount: requiredCount,
                rowKind: ObjectiveHudRowKind.ButtonGroupGeneric,
                groupKey: "button-group-push");
        }

        private static int GetProgressPulsePlayCount(ObjectiveHudRowView rowView)
        {
            return GetPrivateField<int>(rowView, "<ProgressPulsePlayCount>k__BackingField");
        }

        private static float GetProgressHighlightAlpha(ObjectiveHudRowView rowView)
        {
            var group = GetPrivateField<CanvasGroup>(rowView, "_progressHighlightGroup");
            if (group != null)
            {
                return group.alpha;
            }

            var graphic = GetPrivateField<Graphic>(rowView, "_progressHighlightGraphic");
            return graphic != null ? graphic.color.a : 0.0f;
        }

        private static Transform GetProgressPulseScaleTarget(ObjectiveHudRowView rowView)
        {
            return GetPrivateField<Transform>(rowView, "_progressPulseScaleTarget");
        }

        private static GameObject FindActiveObjectiveRuntimeItem(
            RectTransform objectiveListRoot,
            RectTransform itemTemplate)
        {
            return objectiveListRoot
                .Cast<Transform>()
                .Where(child => child != itemTemplate && child.gameObject.activeSelf)
                .Single()
                .gameObject;
        }

        private static ObjectiveHudRowView FindObjectiveRuntimeRow(
            RectTransform objectiveListRoot,
            RectTransform itemTemplate,
            string stableId)
        {
            return objectiveListRoot
                .Cast<Transform>()
                .Where(child => child != itemTemplate)
                .Select(child => child.GetComponent<ObjectiveHudRowView>())
                .Single(row => row != null && row.gameObject.activeSelf && row.StableId == stableId);
        }

        private static ObjectiveHudRowView TryFindObjectiveRuntimeRow(
            RectTransform objectiveListRoot,
            RectTransform itemTemplate,
            string stableId)
        {
            return objectiveListRoot
                .Cast<Transform>()
                .Where(child => child != itemTemplate)
                .Select(child => child.GetComponent<ObjectiveHudRowView>())
                .SingleOrDefault(row => row != null && row.gameObject.activeSelf && row.StableId == stableId);
        }

        private static void CompleteObjectiveEnterSequence(
            ObjectiveHudView objectiveView,
            RectTransform objectiveListRoot,
            RectTransform itemTemplate,
            params string[] stableIds)
        {
            for (var i = 0; i < stableIds.Length; i++)
            {
                CompleteObjectiveEnter(
                    objectiveView,
                    FindObjectiveRuntimeRow(objectiveListRoot, itemTemplate, stableIds[i]));
            }
        }

        private static void CompleteObjectiveEnter(ObjectiveHudView objectiveView, ObjectiveHudRowView rowView)
        {
            Assert.That(objectiveView, Is.Not.Null);
            Assert.That(rowView, Is.Not.Null);
            CompleteObjectiveEnterWithoutScheduler(rowView);
            ForceObjectiveSchedulerDue(objectiveView);
        }

        private static void CompleteObjectiveEnterWithoutScheduler(ObjectiveHudRowView rowView)
        {
            Assert.That(rowView, Is.Not.Null);
            for (var i = 0; i < 16 && rowView.VisualState == ObjectiveRowVisualState.Entering; i++)
            {
                rowView.Tick(1.0f);
            }

            Assert.That(rowView.VisualState, Is.EqualTo(ObjectiveRowVisualState.Idle));
        }

        private static void CompleteObjectiveDismiss(ObjectiveHudView objectiveView, ObjectiveHudRowView rowView)
        {
            Assert.That(objectiveView, Is.Not.Null);
            Assert.That(rowView, Is.Not.Null);
            CompleteObjectiveDismissWithoutScheduler(rowView);
            ForceObjectiveSchedulerDue(objectiveView);
        }

        private static void CompleteObjectiveDismissWithoutScheduler(ObjectiveHudRowView rowView)
        {
            Assert.That(rowView, Is.Not.Null);
            rowView.Tick(0.0f);

            var animator = rowView.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            animator.Play("Out", 0, 1.0f);
            animator.Update(1.0f);

            rowView.Tick(0.0f);
            for (var i = 0; i < 16 && rowView.VisualState == ObjectiveRowVisualState.Collapsing; i++)
            {
                rowView.Tick(1.0f);
            }
        }

        private static void ForceObjectiveSchedulerDue(ObjectiveHudView objectiveView)
        {
            SetPrivateField(objectiveView, "_nextTransitionAllowedAt", 0.0f);
            InvokeObjectiveScheduler(objectiveView, 0.0f);
        }

        private static void InvokeObjectiveScheduler(ObjectiveHudView objectiveView, float now)
        {
            var processMethod = typeof(ObjectiveHudView).GetMethod(
                "ProcessTransitionAdvance",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(processMethod, Is.Not.Null);
            processMethod.Invoke(objectiveView, new object[] { now });
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

            var rootShellInstance = UnityEngine.Object.Instantiate(rootShellPrefab, rootObject.transform, false);
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
        }

        private static void AssertOwnedBy(Transform child, Transform owner)
        {
            Assert.That(child, Is.Not.Null);
            Assert.That(owner, Is.Not.Null);
            Assert.That(child.IsChildOf(owner), Is.True, $"{child.name} should be under {owner.name}.");
        }

        private static void AssertCubeMapPreviewHasRenderableLayoutContract(SurfaceCubeMapView cubeMapView)
        {
            Assert.That(cubeMapView, Is.Not.Null);
            Assert.That(cubeMapView.transform, Is.InstanceOf<RectTransform>());

            var layoutElement = cubeMapView.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                Assert.That(layoutElement.enabled, Is.True);
                Assert.That(layoutElement.ignoreLayout, Is.False);
                Assert.That(
                    layoutElement.preferredWidth > 0.0f || layoutElement.minWidth > 0.0f,
                    Is.True);
                Assert.That(
                    layoutElement.preferredHeight > 0.0f || layoutElement.minHeight > 0.0f,
                    Is.True);
                return;
            }

            var rectTransform = (RectTransform)cubeMapView.transform;
            Assert.That(rectTransform.sizeDelta.x, Is.GreaterThan(0.0f));
            Assert.That(rectTransform.sizeDelta.y, Is.GreaterThan(0.0f));
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

        private static TReference GetSerializedReference<TReference>(
            UnityEngine.Object target,
            string fieldName)
            where TReference : UnityEngine.Object
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            Assert.That(property.objectReferenceValue, Is.Not.Null, fieldName);

            var reference = property.objectReferenceValue as TReference;
            Assert.That(reference, Is.Not.Null, fieldName);
            return reference;
        }

        private static TValue GetPrivateField<TValue>(object target, string fieldName)
        {
            Assert.That(target, Is.Not.Null);
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (TValue)field.GetValue(target);
        }

        private static void SetPrivateField<TValue>(object target, string fieldName, TValue value)
        {
            Assert.That(target, Is.Not.Null);
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void AssertSerializedReference(
            UnityEngine.Object target,
            string fieldName,
            UnityEngine.Object expected)
        {
            var actual = GetSerializedReference<UnityEngine.Object>(target, fieldName);
            Assert.That(actual, Is.SameAs(expected), fieldName);
        }

        private static void SetSerializedReference(
            UnityEngine.Object target,
            string fieldName,
            UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        private static void AssertSerializedReferenceIsAssigned(
            UnityEngine.Object target,
            string fieldName)
        {
            GetSerializedReference<UnityEngine.Object>(target, fieldName);
        }

        private static void AssertSerializedArrayCount(
            UnityEngine.Object target,
            string fieldName,
            int expectedCount)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            Assert.That(property.isArray, Is.True, fieldName);
            Assert.That(property.arraySize, Is.EqualTo(expectedCount), fieldName);
            for (var i = 0; i < property.arraySize; i++)
            {
                Assert.That(property.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null, $"{fieldName}[{i}]");
            }
        }

        private static void AssertWorldRectContains(RectTransform outer, RectTransform inner)
        {
            var outerRect = GetWorldRect(outer);
            var innerRect = GetWorldRect(inner);
            const float tolerance = 0.1f;
            Assert.That(innerRect.xMin, Is.GreaterThanOrEqualTo(outerRect.xMin - tolerance), $"{inner.name} extends left of {outer.name}.");
            Assert.That(innerRect.xMax, Is.LessThanOrEqualTo(outerRect.xMax + tolerance), $"{inner.name} extends right of {outer.name}.");
            Assert.That(innerRect.yMin, Is.GreaterThanOrEqualTo(outerRect.yMin - tolerance), $"{inner.name} extends below {outer.name}.");
            Assert.That(innerRect.yMax, Is.LessThanOrEqualTo(outerRect.yMax + tolerance), $"{inner.name} extends above {outer.name}.");
        }

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void DestroySupportObjects(GameObject rootObject)
        {
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
            }

            if (rootObject != null)
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }
    }
}

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
                var objectiveListRoot = GetSerializedReference<RectTransform>(hudView.ObjectiveHudView, "_objectiveListRoot");
                var itemTemplate = GetSerializedReference<RectTransform>(hudView.ObjectiveHudView, "_objectiveItemTemplate");
                Assert.That(objectiveListRoot, Is.Not.Null);
                Assert.That(objectiveListRoot.name, Is.EqualTo("Objective_List"));
                Assert.That(itemTemplate, Is.Not.Null);
                Assert.That(itemTemplate.parent, Is.SameAs(objectiveListRoot));
                Assert.That(itemTemplate.gameObject.activeSelf, Is.False);
                Assert.That(itemTemplate.GetComponent<Animator>(), Is.Not.Null);
                Assert.That(
                    itemTemplate.GetComponentsInChildren<TMP_Text>(true).Any(label => label.name == "Label_Objective"),
                    Is.True);
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

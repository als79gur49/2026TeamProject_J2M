using System;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using Game.Shared.Input;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests
{
    [Category("Core")]
    public sealed class GameplayWorldGuidePresenterTests
    {
        [Test]
        public void GameplayWorldGuidePresenter_ValidGuide_InstantiatesView()
        {
            var root = new GameObject("PresenterRoot");
            var parent = new GameObject("GuideParent").transform;
            var prefab = CreateGuidePrefab(WorldGuideBindingKind.Push);
            var catalog = CreateCatalog(CreateEntry("push", prefab));
            var presenter = root.AddComponent<GameplayWorldGuidePresenter>();

            try
            {
                presenter.Initialize(
                    catalog,
                    new[]
                    {
                        new StageWorldGuideInstructionResolved(
                            "push",
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            Vector3.zero,
                            0.25f,
                            StageWorldGuideFacingMode.SurfaceAligned,
                            hideWhenFaceInactive: false),
                    },
                    boardSurfaceRenderer: null,
                    poseResolver: new FixedPoseResolver(),
                    viewCamera: null,
                    parent);

                Assert.That(presenter.InstanceCount, Is.EqualTo(1));
                var label = parent.GetComponentInChildren<TMP_Text>(includeInactive: true);
                Assert.That(label, Is.Not.Null);
                Assert.That(label.text, Is.EqualTo("E"));
                var view = parent.GetComponentInChildren<WorldGuideInstructionView>(includeInactive: true);
                Assert.That(view, Is.Not.Null);
                Assert.That(view.transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, 2.75f)));
                Assert.That(view.transform.localScale, Is.EqualTo(new Vector3(0.02f, 0.02f, 0.02f)));
            }
            finally
            {
                DestroyObjects(root, parent.gameObject, prefab, catalog);
            }
        }

        [Test]
        public void GameplayWorldGuidePresenter_MissingCatalogKey_SkipsGuide()
        {
            var root = new GameObject("PresenterRoot");
            var parent = new GameObject("GuideParent").transform;
            var prefab = CreateGuidePrefab(WorldGuideBindingKind.Movement);
            var catalog = CreateCatalog(CreateEntry("movement", prefab));
            var presenter = root.AddComponent<GameplayWorldGuidePresenter>();

            try
            {
                LogAssert.Expect(LogType.Warning, "Skipping StageWorldGuideInstruction 'missing' at index 0; no catalog prefab resolved.");

                presenter.Initialize(
                    catalog,
                    new[]
                    {
                        new StageWorldGuideInstructionResolved(
                            "missing",
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            Vector3.zero,
                            0.25f,
                            StageWorldGuideFacingMode.SurfaceAligned,
                            hideWhenFaceInactive: false),
                    },
                    boardSurfaceRenderer: null,
                    poseResolver: new FixedPoseResolver(),
                    viewCamera: null,
                    parent);

                Assert.That(presenter.InstanceCount, Is.Zero);
            }
            finally
            {
                DestroyObjects(root, parent.gameObject, prefab, catalog);
            }
        }

        [Test]
        public void GameplayWorldGuidePresenter_MissingTileHandle_HidesGuide()
        {
            var root = new GameObject("PresenterRoot");
            var parent = new GameObject("GuideParent").transform;
            var prefab = CreateGuidePrefab(WorldGuideBindingKind.Movement);
            var catalog = CreateCatalog(CreateEntry("movement", prefab));
            var presenter = root.AddComponent<GameplayWorldGuidePresenter>();

            try
            {
                presenter.Initialize(
                    catalog,
                    new[]
                    {
                        new StageWorldGuideInstructionResolved(
                            "movement",
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            Vector3.zero,
                            0.25f,
                            StageWorldGuideFacingMode.SurfaceAligned,
                            hideWhenFaceInactive: true),
                    },
                    boardSurfaceRenderer: null,
                    poseResolver: new FixedPoseResolver(),
                    viewCamera: null,
                    parent);

                var view = parent.GetComponentInChildren<WorldGuideInstructionView>(includeInactive: true);
                Assert.That(view, Is.Not.Null);
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                DestroyObjects(root, parent.gameObject, prefab, catalog);
            }
        }

        [Test]
        public void GameplayWorldGuidePresenter_Cleanup_DestroysOwnedInstances()
        {
            var root = new GameObject("PresenterRoot");
            var parent = new GameObject("GuideParent").transform;
            var prefab = CreateGuidePrefab(WorldGuideBindingKind.Movement);
            var catalog = CreateCatalog(CreateEntry("movement", prefab));
            var presenter = root.AddComponent<GameplayWorldGuidePresenter>();

            try
            {
                presenter.Initialize(
                    catalog,
                    new[]
                    {
                        new StageWorldGuideInstructionResolved(
                            "movement",
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            Vector3.zero,
                            0.25f,
                            StageWorldGuideFacingMode.SurfaceAligned,
                            hideWhenFaceInactive: false),
                    },
                    boardSurfaceRenderer: null,
                    poseResolver: new FixedPoseResolver(),
                    viewCamera: null,
                    parent);

                presenter.Cleanup();

                Assert.That(presenter.InstanceCount, Is.Zero);
                Assert.That(parent.childCount, Is.Zero);
            }
            finally
            {
                DestroyObjects(root, parent.gameObject, prefab, catalog);
            }
        }

        [Test]
        public void GameplayWorldGuidePresenter_BoardSurfaceOffset_PlacesGuideAboveTileTop()
        {
            var root = new GameObject("PresenterRoot");
            var parent = new GameObject("GuideParent").transform;
            var prefab = CreateGuidePrefab(WorldGuideBindingKind.Push);
            var catalog = CreateCatalog(CreateEntry("push", prefab));
            var presenter = root.AddComponent<GameplayWorldGuidePresenter>();

            try
            {
                presenter.Initialize(
                    catalog,
                    new[]
                    {
                        new StageWorldGuideInstructionResolved(
                            "push",
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            Vector3.zero,
                            0.08f,
                            StageWorldGuideFacingMode.SurfaceAligned,
                            hideWhenFaceInactive: false),
                    },
                    boardSurfaceRenderer: null,
                    poseResolver: new FixedPoseResolver(surfaceOutwardOffset: 0.25f),
                    viewCamera: null,
                    parent);

                var view = parent.GetComponentInChildren<WorldGuideInstructionView>(includeInactive: true);

                Assert.That(view, Is.Not.Null);
                Assert.That(view.transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, 2.67f)));
            }
            finally
            {
                DestroyObjects(root, parent.gameObject, prefab, catalog);
            }
        }

        [Test]
        public void WorldGuideInstructionView_MovementBinding_TogglesAuthoredInputRoots()
        {
            var prefab = CreateGuidePrefab(WorldGuideBindingKind.Movement);

            try
            {
                var view = prefab.GetComponent<WorldGuideInstructionView>();
                var wasd = prefab.transform.Find("WASD").gameObject;
                var arrows = prefab.transform.Find("Arrows").gameObject;

                view.ApplyKeyboardBindings(new KeyboardBindingSettingsSnapshot(
                    KeyboardMovementScheme.ArrowKeys,
                    "Arrow Keys",
                    "E",
                    "Q",
                    isRebinding: false,
                    rebindingAction: null));

                Assert.That(wasd.activeSelf, Is.False);
                Assert.That(arrows.activeSelf, Is.True);

                view.ApplyKeyboardBindings(new KeyboardBindingSettingsSnapshot(
                    KeyboardMovementScheme.Wasd,
                    "WASD",
                    "E",
                    "Q",
                    isRebinding: false,
                    rebindingAction: null));

                Assert.That(wasd.activeSelf, Is.True);
                Assert.That(arrows.activeSelf, Is.False);
            }
            finally
            {
                DestroyObjects(prefab);
            }
        }

        private sealed class FixedPoseResolver : ISurfaceCellPresentationPoseResolver
        {
            private readonly float _surfaceOutwardOffset;

            public FixedPoseResolver(float surfaceOutwardOffset = 0f)
            {
                _surfaceOutwardOffset = surfaceOutwardOffset;
            }

            public bool TryResolvePose(SurfaceCell cell, out SurfaceCellPresentationPose pose)
            {
                pose = new SurfaceCellPresentationPose(
                    new Vector3(1f, 2f, 3f),
                    Quaternion.identity,
                    Vector3.one,
                    _surfaceOutwardOffset);
                return true;
            }
        }

        private static GameObject CreateGuidePrefab(WorldGuideBindingKind bindingKind)
        {
            var root = new GameObject("WorldGuidePrefab", typeof(RectTransform), typeof(Canvas));
            var view = root.AddComponent<WorldGuideInstructionView>();
            root.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
            var wasdObject = new GameObject("WASD", typeof(RectTransform));
            wasdObject.transform.SetParent(root.transform, worldPositionStays: false);
            var arrowObject = new GameObject("Arrows", typeof(RectTransform));
            arrowObject.transform.SetParent(root.transform, worldPositionStays: false);
            var textObject = new GameObject("ActionKey", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.transform, worldPositionStays: false);

            SetPrivateField(view, "canvas", root.GetComponent<Canvas>());
            SetPrivateField(view, "bindingKind", bindingKind);
            SetPrivateField(view, "wasdDisplayRoot", wasdObject);
            SetPrivateField(view, "arrowDisplayRoot", arrowObject);
            SetPrivateField(view, "actionKeyLabel", textObject.GetComponent<TMP_Text>());
            return root;
        }

        private static StageWorldGuideCatalogEntry CreateEntry(string guideKey, GameObject prefab)
        {
            var entry = new StageWorldGuideCatalogEntry();
            SetPrivateField(entry, "guideKey", guideKey);
            SetPrivateField(entry, "prefab", prefab);
            return entry;
        }

        private static StageWorldGuideCatalog CreateCatalog(params StageWorldGuideCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<StageWorldGuideCatalog>();
            SetPrivateField(catalog, "entries", entries ?? Array.Empty<StageWorldGuideCatalogEntry>());
            return catalog;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void DestroyObjects(params UnityEngine.Object[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[i]);
                }
            }
        }
    }
}

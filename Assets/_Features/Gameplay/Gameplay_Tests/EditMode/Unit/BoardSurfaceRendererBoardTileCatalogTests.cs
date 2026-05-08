using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class BoardSurfaceRendererBoardTileCatalogTests
    {
        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_CatalogNull_UsesExistingCubeMaterialPath()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_CatalogNull_UsesExistingCubeMaterialPath");

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor));

                var tile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");

                Assert.That(tile.GetComponent<MeshFilter>(), Is.Not.Null);
                Assert.That(tile.GetComponent<MeshRenderer>(), Is.Not.Null);
                Assert.That(tile.GetComponent<BoardTileCatalogTestMarker>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_UsesCatalogPrefabForActiveBottom()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_UsesCatalogPrefabForActiveBottom");
            var prefab = CreatePrefab("ActiveBottomBoardTilePrefab");
            var material = CreateMaterial("ActiveFrontFallback");
            var catalog = CreateCatalog(
                Entry("bottom", BoardTileVisualRole.ActiveBottom, prefab, null, isDefault: true),
                Entry("front", BoardTileVisualRole.ActiveFront, null, material, isDefault: true));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog);

                var bottomTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");

                Assert.That(bottomTile.GetComponent<BoardTileCatalogTestMarker>(), Is.Not.Null);
            }
            finally
            {
                DestroyObjects(rootObject, prefab, catalog, material);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_CellOverrideBeatsRoleDefault()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_CellOverrideBeatsRoleDefault");
            var overridePrefab = CreatePrefab("OverrideBoardTilePrefab");
            var defaultMaterial = CreateMaterial("DefaultBottomMaterial");
            var frontMaterial = CreateMaterial("DefaultFrontMaterial");
            var catalog = CreateCatalog(
                Entry("bottom-default", BoardTileVisualRole.ActiveBottom, null, defaultMaterial, isDefault: true),
                Entry("front-default", BoardTileVisualRole.ActiveFront, null, frontMaterial, isDefault: true),
                Entry("override-cell", BoardTileVisualRole.ActiveBottom, overridePrefab, null, isDefault: false));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    boardTilePresentationOverrides: new[]
                    {
                        Override(new SurfaceCell(FaceId.Floor, 1, 0), "override-cell"),
                    });

                var defaultTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");
                var overrideTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_1_0");

                Assert.That(defaultTile.GetComponent<BoardTileCatalogTestMarker>(), Is.Null);
                Assert.That(defaultTile.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(defaultMaterial));
                Assert.That(overrideTile.GetComponent<BoardTileCatalogTestMarker>(), Is.Not.Null);
            }
            finally
            {
                DestroyObjects(rootObject, overridePrefab, catalog, defaultMaterial, frontMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_UsesMaterialFallbackWhenNoPrefab()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_UsesMaterialFallbackWhenNoPrefab");
            var bottomMaterial = CreateMaterial("ActiveBottomMaterialFallback");
            var frontMaterial = CreateMaterial("ActiveFrontMaterialFallback");
            var catalog = CreateCatalog(
                Entry("bottom", BoardTileVisualRole.ActiveBottom, null, bottomMaterial, isDefault: true),
                Entry("front", BoardTileVisualRole.ActiveFront, null, frontMaterial, isDefault: true));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog);

                var bottomTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");
                var bottomRenderer = bottomTile.GetComponent<MeshRenderer>();

                Assert.That(bottomRenderer, Is.Not.Null);
                Assert.That(bottomRenderer.sharedMaterial, Is.SameAs(bottomMaterial));
            }
            finally
            {
                DestroyObjects(rootObject, catalog, bottomMaterial, frontMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_CellOverrideUsesPrefabDescriptor()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_CellOverrideUsesPrefabDescriptor");
            var overridePrefab = CreatePrefab("CellOverridePrefab");
            var defaultMaterial = CreateMaterial("CellOverrideDefaultMaterial");
            var frontMaterial = CreateMaterial("CellOverrideFrontMaterial");
            var catalog = CreateCatalog(
                Entry("bottom-default", BoardTileVisualRole.ActiveBottom, null, defaultMaterial, isDefault: true),
                Entry("front-default", BoardTileVisualRole.ActiveFront, null, frontMaterial, isDefault: true),
                Entry("prefab-override", BoardTileVisualRole.GenericDefault, overridePrefab, null, isDefault: false));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    boardTilePresentationOverrides: new[]
                    {
                        Override(new SurfaceCell(FaceId.Floor, 0, 0), "prefab-override"),
                    });

                var bottomTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");

                Assert.That(bottomTile.GetComponent<BoardTileCatalogTestMarker>(), Is.Not.Null);
            }
            finally
            {
                DestroyObjects(rootObject, overridePrefab, catalog, defaultMaterial, frontMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_CellOverrideUsesMaterialFallbackDescriptor()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_CellOverrideUsesMaterialFallbackDescriptor");
            var defaultMaterial = CreateMaterial("MaterialOverrideDefault");
            var overrideMaterial = CreateMaterial("MaterialOverrideCell");
            var frontMaterial = CreateMaterial("MaterialOverrideFront");
            var catalog = CreateCatalog(
                Entry("bottom-default", BoardTileVisualRole.ActiveBottom, null, defaultMaterial, isDefault: true),
                Entry("front-default", BoardTileVisualRole.ActiveFront, null, frontMaterial, isDefault: true),
                Entry("material-override", BoardTileVisualRole.ActiveBottom, null, overrideMaterial, isDefault: false));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    boardTilePresentationOverrides: new[]
                    {
                        Override(new SurfaceCell(FaceId.Floor, 0, 0), "material-override"),
                    });

                var bottomTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");

                Assert.That(bottomTile.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(overrideMaterial));
            }
            finally
            {
                DestroyObjects(rootObject, catalog, defaultMaterial, overrideMaterial, frontMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_MissingOverrideKeyFallsBackAtRuntime()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_MissingOverrideKeyFallsBackAtRuntime");
            var defaultMaterial = CreateMaterial("MissingOverrideDefault");
            var frontMaterial = CreateMaterial("MissingOverrideFront");
            var catalog = CreateCatalog(
                Entry("bottom-default", BoardTileVisualRole.ActiveBottom, null, defaultMaterial, isDefault: true),
                Entry("front-default", BoardTileVisualRole.ActiveFront, null, frontMaterial, isDefault: true));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();
                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("BoardTilePresentationOverride.*missing-key.*Falling back"));

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    boardTilePresentationOverrides: new[]
                    {
                        Override(new SurfaceCell(FaceId.Floor, 0, 0), "missing-key"),
                    });

                var bottomTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");

                Assert.That(bottomTile.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(defaultMaterial));
            }
            finally
            {
                DestroyObjects(rootObject, catalog, defaultMaterial, frontMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_UsesGenericDefaultWhenRoleDefaultMissing()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_UsesGenericDefaultWhenRoleDefaultMissing");
            var prefab = CreatePrefab("GenericBoardTilePrefab");
            var catalog = CreateCatalog(
                Entry("generic", BoardTileVisualRole.GenericDefault, prefab, null, isDefault: true));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog);

                var bottomTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");
                var frontTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveFront_Front_0_0");

                Assert.That(bottomTile.GetComponent<BoardTileCatalogTestMarker>(), Is.Not.Null);
                Assert.That(frontTile.GetComponent<BoardTileCatalogTestMarker>(), Is.Not.Null);
            }
            finally
            {
                DestroyObjects(rootObject, prefab, catalog);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_TransitionPool_UsesCatalogDescriptor()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_TransitionPool_UsesCatalogDescriptor");
            var bottomPrefab = CreatePrefab("TransitionBottomPrefab");
            var frontMaterial = CreateMaterial("TransitionFrontMaterial");
            var catalog = CreateCatalog(
                Entry("bottom", BoardTileVisualRole.ActiveBottom, bottomPrefab, null, isDefault: true),
                Entry("front", BoardTileVisualRole.ActiveFront, null, frontMaterial, isDefault: true));
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    sourceTopology,
                    boardTilePresentationCatalog: catalog);
                renderer.BeginTopologyTransition(sourceTopology, destinationTopology);

                var transitionTile = FindTile(renderer.TransitionTilePoolRoot, "ActiveBottom_Front_0_0");

                Assert.That(transitionTile.GetComponent<BoardTileCatalogTestMarker>(), Is.Not.Null);
            }
            finally
            {
                DestroyObjects(rootObject, bottomPrefab, catalog, frontMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_TransitionPoolUsesCellOverride()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_TransitionPoolUsesCellOverride");
            var overridePrefab = CreatePrefab("TransitionOverridePrefab");
            var bottomMaterial = CreateMaterial("TransitionOverrideBottomDefault");
            var frontMaterial = CreateMaterial("TransitionOverrideFrontDefault");
            var catalog = CreateCatalog(
                Entry("bottom-default", BoardTileVisualRole.ActiveBottom, null, bottomMaterial, isDefault: true),
                Entry("front-default", BoardTileVisualRole.ActiveFront, null, frontMaterial, isDefault: true),
                Entry("transition-override", BoardTileVisualRole.ActiveBottom, overridePrefab, null, isDefault: false));
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    sourceTopology,
                    boardTilePresentationCatalog: catalog,
                    boardTilePresentationOverrides: new[]
                    {
                        Override(new SurfaceCell(FaceId.Front, 0, 0), "transition-override"),
                    });
                renderer.BeginTopologyTransition(sourceTopology, destinationTopology);

                var transitionTile = FindTile(renderer.TransitionTilePoolRoot, "ActiveBottom_Front_0_0");

                Assert.That(transitionTile.GetComponent<BoardTileCatalogTestMarker>(), Is.Not.Null);
            }
            finally
            {
                DestroyObjects(rootObject, overridePrefab, catalog, bottomMaterial, frontMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_CatalogNullStillLegacyFallback()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_CatalogNullStillLegacyFallback");

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: null);

                var tile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");

                Assert.That(tile.GetComponent<MeshFilter>(), Is.Not.Null);
                Assert.That(tile.GetComponent<MeshRenderer>(), Is.Not.Null);
                Assert.That(tile.GetComponent<BoardTileCatalogTestMarker>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_Rebuild_DoesNotLeakOldPrefabTiles()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_Rebuild_DoesNotLeakOldPrefabTiles");
            var oldPrefab = CreatePrefab("OldBoardTilePrefab");
            var newMaterial = CreateMaterial("NewBoardTileMaterial");
            var oldCatalog = CreateCatalog(
                Entry("old-bottom", BoardTileVisualRole.ActiveBottom, oldPrefab, null, isDefault: true),
                Entry("old-front", BoardTileVisualRole.ActiveFront, oldPrefab, null, isDefault: true));
            var newCatalog = CreateCatalog(
                Entry("new-bottom", BoardTileVisualRole.ActiveBottom, null, newMaterial, isDefault: true),
                Entry("new-front", BoardTileVisualRole.ActiveFront, null, newMaterial, isDefault: true));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();
                var boardBounds = new BoardBounds(Vector2Int.zero, Vector2Int.zero);
                var topology = new CubeTopologyState(FaceId.Floor);

                renderer.Initialize(boardBounds, 1f, topology, boardTilePresentationCatalog: oldCatalog);
                var oldTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");

                renderer.Initialize(boardBounds, 1f, topology, boardTilePresentationCatalog: newCatalog);
                var newTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");

                Assert.That(oldTile == null, Is.True);
                Assert.That(newTile.GetComponent<BoardTileCatalogTestMarker>(), Is.Null);
                Assert.That(newTile.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(newMaterial));
            }
            finally
            {
                DestroyObjects(rootObject, oldPrefab, oldCatalog, newCatalog, newMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_SuppressesSteadyBaseTileForReplaceTileFeature()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_SuppressesSteadyBaseTileForReplaceTileFeature");

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    suppressedBaseTileCells: new[]
                    {
                        new SurfaceCell(FaceId.Floor, 0, 0),
                    });

                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveBottom_Floor_0_0"), Is.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveFront_Front_0_0"), Is.Not.Null);
                Assert.That(renderer.SteadyTileCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_SuppressesTransitionBaseTileForReplaceTileFeature()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_SuppressesTransitionBaseTileForReplaceTileFeature");
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    sourceTopology,
                    suppressedBaseTileCells: new[]
                    {
                        new SurfaceCell(FaceId.Front, 0, 0),
                    });
                renderer.BeginTopologyTransition(sourceTopology, destinationTopology);

                Assert.That(renderer.TransitionTilePoolRoot.Find("ActiveBottom_Front_0_0"), Is.Null);
                Assert.That(renderer.TransitionTilePoolRoot.Find("ActiveFront_Ceiling_0_0"), Is.Not.Null);
                Assert.That(renderer.TransitionTileCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_EmptySuppressSetKeepsExistingBoardTiles()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_EmptySuppressSetKeepsExistingBoardTiles");

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    suppressedBaseTileCells: Array.Empty<SurfaceCell>());

                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveBottom_Floor_0_0"), Is.Not.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveFront_Front_0_0"), Is.Not.Null);
                Assert.That(renderer.SteadyTileCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_SuppressedCellDoesNotAllocateDescriptorPool()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_SuppressedCellDoesNotAllocateDescriptorPool");
            var bottomPrefab = CreatePrefab("SuppressedBottomPrefab");
            var frontMaterial = CreateMaterial("SuppressedFrontMaterial");
            var catalog = CreateCatalog(
                Entry("bottom", BoardTileVisualRole.ActiveBottom, bottomPrefab, null, isDefault: true),
                Entry("front", BoardTileVisualRole.ActiveFront, null, frontMaterial, isDefault: true));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    suppressedBaseTileCells: new[]
                    {
                        new SurfaceCell(FaceId.Floor, 0, 0),
                    });

                Assert.That(renderer.VisibleTilePoolRoot.Find("ActiveBottom_Floor_0_0"), Is.Null);
                Assert.That(
                    renderer.VisibleTilePoolRoot.GetComponentsInChildren<BoardTileCatalogTestMarker>(includeInactive: true),
                    Is.Empty);
            }
            finally
            {
                DestroyObjects(rootObject, bottomPrefab, catalog, frontMaterial);
            }
        }

        private static GameObject FindTile(Transform root, string tileName)
        {
            Assert.That(root, Is.Not.Null);
            var tile = root.Find(tileName);
            Assert.That(tile, Is.Not.Null, $"Expected tile '{tileName}' under '{root.name}'.");
            return tile.gameObject;
        }

        private static GameObject CreatePrefab(string name)
        {
            var prefab = new GameObject(name);
            prefab.AddComponent<BoardTileCatalogTestMarker>();
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = "Renderer";
            child.transform.SetParent(prefab.transform, worldPositionStays: false);
            var collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return prefab;
        }

        private static Material CreateMaterial(string materialName)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null, "Expected a test shader to be available.");
            return new Material(shader)
            {
                name = materialName,
            };
        }

        private static BoardTilePresentationCatalogEntry Entry(
            string presentationKey,
            BoardTileVisualRole role,
            GameObject tilePrefab,
            Material materialFallback,
            bool isDefault)
        {
            var entry = new BoardTilePresentationCatalogEntry();
            SetPrivateField(entry, "presentationKey", presentationKey);
            SetPrivateField(entry, "displayName", presentationKey);
            SetPrivateField(entry, "role", role);
            SetPrivateField(entry, "tilePrefab", tilePrefab);
            SetPrivateField(entry, "materialFallback", materialFallback);
            SetPrivateField(entry, "isDefaultForRole", isDefault);
            return entry;
        }

        private static BoardTilePresentationCatalog CreateCatalog(
            params BoardTilePresentationCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<BoardTilePresentationCatalog>();
            catalog.name = "BoardSurfaceRendererBoardTileCatalogTests";
            SetPrivateField(catalog, "entries", entries ?? Array.Empty<BoardTilePresentationCatalogEntry>());
            return catalog;
        }

        private static BoardTilePresentationOverride Override(SurfaceCell cell, string presentationKey)
        {
            return new BoardTilePresentationOverride(cell, presentationKey);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
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

        private sealed class BoardTileCatalogTestMarker : MonoBehaviour
        {
        }
    }
}

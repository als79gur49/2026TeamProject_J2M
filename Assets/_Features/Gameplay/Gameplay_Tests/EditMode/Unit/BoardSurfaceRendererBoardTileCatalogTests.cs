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

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_TileVisualHandlesExposeOnlyActiveVisibleCells()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_TileVisualHandlesExposeOnlyActiveVisibleCells");

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

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out _),
                    Is.False);
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Front, 0, 0), out var frontHandle),
                    Is.True);
                Assert.That(frontHandle.Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
                Assert.That(frontHandle.VisualRole, Is.EqualTo(BoardTileVisualRole.ActiveFront));
                Assert.That(frontHandle.IsActive, Is.True);
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Front, 2, 0), out _),
                    Is.False);
                Assert.That(renderer.TileVisualHandleCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_TileVisualHandlesRefreshAcrossTopologyTransition()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_TileVisualHandlesRefreshAcrossTopologyTransition");
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    sourceTopology);

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out var sourceHandle),
                    Is.True);
                Assert.That(sourceHandle.VisualRole, Is.EqualTo(BoardTileVisualRole.ActiveBottom));

                renderer.BeginTopologyTransition(sourceTopology, destinationTopology);

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out _),
                    Is.False);
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Front, 0, 0), out var transitionBottomHandle),
                    Is.True);
                Assert.That(transitionBottomHandle.VisualRole, Is.EqualTo(BoardTileVisualRole.ActiveBottom));
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Ceiling, 0, 0), out var transitionFrontHandle),
                    Is.True);
                Assert.That(transitionFrontHandle.VisualRole, Is.EqualTo(BoardTileVisualRole.ActiveFront));

                renderer.CompleteTopologyTransition(destinationTopology);

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out _),
                    Is.False);
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Front, 0, 0), out var steadyBottomHandle),
                    Is.True);
                Assert.That(steadyBottomHandle.VisualRole, Is.EqualTo(BoardTileVisualRole.ActiveBottom));
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Ceiling, 0, 0), out var steadyFrontHandle),
                    Is.True);
                Assert.That(steadyFrontHandle.VisualRole, Is.EqualTo(BoardTileVisualRole.ActiveFront));
                Assert.That(renderer.TileVisualHandleCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_PaintOverrideTintsFallbackTileWithMaterialPropertyBlock()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_PaintOverrideTintsFallbackTileWithMaterialPropertyBlock");
            var fallbackMaterial = CreateMaterial("PaintFallbackMaterial");
            fallbackMaterial.color = Color.green;
            var catalog = CreateCatalog(
                Entry("bottom", BoardTileVisualRole.ActiveBottom, null, fallbackMaterial, isDefault: true),
                Entry("front", BoardTileVisualRole.ActiveFront, null, fallbackMaterial, isDefault: true));
            var styleCatalog = CreateStyleCatalog(StyleEntry("paint-red", Color.red));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    boardTileStyleCatalog: styleCatalog,
                    boardTilePaintOverrides: new[]
                    {
                        PaintOverride(new SurfaceCell(FaceId.Floor, 0, 0), "paint-red"),
                    });

                var bottomTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");
                var bottomRenderer = bottomTile.GetComponent<MeshRenderer>();

                AssertRendererTint(bottomRenderer, Color.red);
                Assert.That(bottomRenderer.sharedMaterial, Is.SameAs(fallbackMaterial));
                Assert.That(fallbackMaterial.color, Is.EqualTo(Color.green));
            }
            finally
            {
                DestroyObjects(rootObject, catalog, styleCatalog, fallbackMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_PaintOverrideTintsPrefabChildRenderer()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_PaintOverrideTintsPrefabChildRenderer");
            var prefab = CreatePrefab("PaintedPrefabTile");
            var frontMaterial = CreateMaterial("PaintedPrefabFrontMaterial");
            var catalog = CreateCatalog(
                Entry("bottom", BoardTileVisualRole.ActiveBottom, prefab, null, isDefault: true),
                Entry("front", BoardTileVisualRole.ActiveFront, null, frontMaterial, isDefault: true));
            var styleCatalog = CreateStyleCatalog(StyleEntry("paint-blue", Color.blue));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    boardTileStyleCatalog: styleCatalog,
                    boardTilePaintOverrides: new[]
                    {
                        PaintOverride(new SurfaceCell(FaceId.Floor, 0, 0), "paint-blue"),
                    });

                var bottomTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");
                var childRenderer = bottomTile.GetComponentInChildren<Renderer>();

                Assert.That(childRenderer, Is.Not.Null);
                AssertRendererTint(childRenderer, Color.blue);
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out var handle),
                    Is.True);
                Assert.That(handle.StyleRenderers, Is.Not.Empty);
            }
            finally
            {
                DestroyObjects(rootObject, prefab, catalog, styleCatalog, frontMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_MissingPaintCatalogOrKeyKeepsExistingVisualResolve()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_MissingPaintCatalogOrKeyKeepsExistingVisualResolve");
            var fallbackMaterial = CreateMaterial("MissingPaintFallbackMaterial");
            var catalog = CreateCatalog(
                Entry("bottom", BoardTileVisualRole.ActiveBottom, null, fallbackMaterial, isDefault: true),
                Entry("front", BoardTileVisualRole.ActiveFront, null, fallbackMaterial, isDefault: true));
            var styleCatalog = CreateStyleCatalog(StyleEntry("other-style", Color.red));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    boardTileStyleCatalog: styleCatalog,
                    boardTilePaintOverrides: new[]
                    {
                        PaintOverride(new SurfaceCell(FaceId.Floor, 0, 0), "missing-style"),
                    });

                var bottomTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");
                var bottomRenderer = bottomTile.GetComponent<MeshRenderer>();

                Assert.That(bottomRenderer.sharedMaterial, Is.SameAs(fallbackMaterial));
                AssertRendererNotTinted(bottomRenderer, Color.red);

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    boardTileStyleCatalog: null,
                    boardTilePaintOverrides: new[]
                    {
                        PaintOverride(new SurfaceCell(FaceId.Floor, 0, 0), "other-style"),
                    });

                bottomTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0");
                bottomRenderer = bottomTile.GetComponent<MeshRenderer>();

                Assert.That(bottomRenderer.sharedMaterial, Is.SameAs(fallbackMaterial));
                AssertRendererNotTinted(bottomRenderer, Color.red);
            }
            finally
            {
                DestroyObjects(rootObject, catalog, styleCatalog, fallbackMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_ReinitializeClearsStalePaintFromPooledTile()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_ReinitializeClearsStalePaintFromPooledTile");
            var styleCatalog = CreateStyleCatalog(StyleEntry("paint-red", Color.red));
            var bounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0));
            var topology = new CubeTopologyState(FaceId.Floor);

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    bounds,
                    1f,
                    topology,
                    boardTileStyleCatalog: styleCatalog,
                    boardTilePaintOverrides: new[]
                    {
                        PaintOverride(new SurfaceCell(FaceId.Floor, 0, 0), "paint-red"),
                    });
                AssertRendererTint(
                    FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0").GetComponent<MeshRenderer>(),
                    Color.red);

                renderer.Initialize(
                    bounds,
                    1f,
                    topology,
                    boardTileStyleCatalog: styleCatalog,
                    boardTilePaintOverrides: new[]
                    {
                        PaintOverride(new SurfaceCell(FaceId.Floor, 1, 0), "paint-red"),
                    });

                AssertRendererNotTinted(
                    FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0").GetComponent<MeshRenderer>(),
                    Color.red);
                AssertRendererTint(
                    FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_1_0").GetComponent<MeshRenderer>(),
                    Color.red);
            }
            finally
            {
                DestroyObjects(rootObject, styleCatalog);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_TransitionTilesApplyPaintAndDoNotExposeSuppressedCells()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_TransitionTilesApplyPaintAndDoNotExposeSuppressedCells");
            var styleCatalog = CreateStyleCatalog(StyleEntry("paint-red", Color.red));
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    sourceTopology,
                    boardTileStyleCatalog: styleCatalog,
                    boardTilePaintOverrides: new[]
                    {
                        PaintOverride(new SurfaceCell(FaceId.Front, 0, 0), "paint-red"),
                        PaintOverride(new SurfaceCell(FaceId.Floor, 0, 0), "paint-red"),
                    },
                    suppressedBaseTileCells: new[]
                    {
                        new SurfaceCell(FaceId.Floor, 0, 0),
                    });

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out _),
                    Is.False);

                renderer.BeginTopologyTransition(sourceTopology, destinationTopology);

                var transitionTile = FindTile(renderer.TransitionTilePoolRoot, "ActiveBottom_Front_0_0");
                AssertRendererTint(transitionTile.GetComponent<MeshRenderer>(), Color.red);
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out _),
                    Is.False);
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Front, 0, 0), out var transitionHandle),
                    Is.True);
                Assert.That(transitionHandle.StyleRenderers, Is.Not.Empty);

                renderer.CompleteTopologyTransition(destinationTopology);

                var steadyTile = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Front_0_0");
                AssertRendererTint(steadyTile.GetComponent<MeshRenderer>(), Color.red);
                Assert.That(renderer.TileVisualHandleCount, Is.EqualTo(2));
            }
            finally
            {
                DestroyObjects(rootObject, styleCatalog);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_OverlayAppliesToFallbackTileWithoutMutatingBasePaintOrMaterial()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_OverlayAppliesToFallbackTileWithoutMutatingBasePaintOrMaterial");
            var fallbackMaterial = CreateMaterial("OverlayFallbackMaterial");
            fallbackMaterial.color = Color.green;
            var catalog = CreateCatalog(
                Entry("bottom", BoardTileVisualRole.ActiveBottom, null, fallbackMaterial, isDefault: true),
                Entry("front", BoardTileVisualRole.ActiveFront, null, fallbackMaterial, isDefault: true));
            var styleCatalog = CreateStyleCatalog(StyleEntry("paint-red", Color.red));
            var overlayCatalog = CreateOverlayCatalog(
                OverlayEntry("guide", BoardTileOverlayLayer.Guide, Color.yellow, 0.35f, 20));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    boardTileStyleCatalog: styleCatalog,
                    boardTilePaintOverrides: new[]
                    {
                        PaintOverride(new SurfaceCell(FaceId.Floor, 0, 0), "paint-red"),
                    },
                    boardTileOverlayCatalog: overlayCatalog,
                    boardTileOverlayOverrides: new[]
                    {
                        OverlayOverride(new SurfaceCell(FaceId.Floor, 0, 0), "guide"),
                    });

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out var handle),
                    Is.True);
                Assert.That(handle.StyleRenderers, Is.Not.Empty);
                Assert.That(handle.OverlayRoot, Is.Not.Null);
                Assert.That(handle.OverlayRenderers.Count, Is.EqualTo(1));

                var baseRenderer = FindTile(renderer.VisibleTilePoolRoot, "ActiveBottom_Floor_0_0").GetComponent<MeshRenderer>();
                AssertRendererTint(baseRenderer, Color.red);
                AssertRendererTint(handle.OverlayRenderers[0], new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.35f));
                Assert.That(baseRenderer.sharedMaterial.color, Is.EqualTo(Color.green));
                Assert.That(handle.OverlayRenderers[0].sharedMaterial.color, Is.EqualTo(Color.white));
            }
            finally
            {
                DestroyObjects(rootObject, catalog, styleCatalog, overlayCatalog, fallbackMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_OverlayAppliesToPrefabTileAndUsesCatalogOrder()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_OverlayAppliesToPrefabTileAndUsesCatalogOrder");
            var prefab = CreatePrefab("OverlayPrefabTile");
            var frontMaterial = CreateMaterial("OverlayPrefabFrontMaterial");
            var catalog = CreateCatalog(
                Entry("bottom", BoardTileVisualRole.ActiveBottom, prefab, null, isDefault: true),
                Entry("front", BoardTileVisualRole.ActiveFront, null, frontMaterial, isDefault: true));
            var overlayCatalog = CreateOverlayCatalog(
                OverlayEntry("hover", BoardTileOverlayLayer.Hover, Color.white, 0.2f, 30),
                OverlayEntry("danger", BoardTileOverlayLayer.Danger, Color.red, 0.6f, 10));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTilePresentationCatalog: catalog,
                    boardTileOverlayCatalog: overlayCatalog,
                    boardTileOverlayOverrides: new[]
                    {
                        OverlayOverride(new SurfaceCell(FaceId.Floor, 0, 0), "hover"),
                        OverlayOverride(new SurfaceCell(FaceId.Floor, 0, 0), "danger"),
                    });

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out var handle),
                    Is.True);
                Assert.That(handle.StyleRenderers, Is.Not.Empty);
                Assert.That(handle.OverlayRenderers.Count, Is.EqualTo(2));
                Assert.That(handle.OverlayRenderers[0].gameObject.name, Does.Contain("danger"));
                Assert.That(handle.OverlayRenderers[1].gameObject.name, Does.Contain("hover"));
                AssertRendererTint(handle.OverlayRenderers[0], new Color(Color.red.r, Color.red.g, Color.red.b, 0.6f));
                AssertRendererTint(handle.OverlayRenderers[1], new Color(Color.white.r, Color.white.g, Color.white.b, 0.2f));
            }
            finally
            {
                DestroyObjects(rootObject, prefab, catalog, overlayCatalog, frontMaterial);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_MissingOverlayCatalogOrKeySkipsOverlay()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_MissingOverlayCatalogOrKeySkipsOverlay");
            var overlayCatalog = CreateOverlayCatalog(
                OverlayEntry("known", BoardTileOverlayLayer.Guide, Color.yellow, 0.4f, 0));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("BoardTileOverlayOverride.*no BoardTileOverlayCatalog"));
                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTileOverlayOverrides: new[]
                    {
                        OverlayOverride(new SurfaceCell(FaceId.Floor, 0, 0), "known"),
                    });

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out var handle),
                    Is.True);
                Assert.That(handle.OverlayRenderers, Is.Empty);

                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("BoardTileOverlayOverride.*missing.*Overlay visual will be skipped"));
                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTileOverlayCatalog: overlayCatalog,
                    boardTileOverlayOverrides: new[]
                    {
                        OverlayOverride(new SurfaceCell(FaceId.Floor, 0, 0), "missing"),
                    });

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out handle),
                    Is.True);
                Assert.That(handle.OverlayRenderers, Is.Empty);
            }
            finally
            {
                DestroyObjects(rootObject, overlayCatalog);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_ReinitializeAndTransitionClearStaleOverlays()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_ReinitializeAndTransitionClearStaleOverlays");
            var overlayCatalog = CreateOverlayCatalog(
                OverlayEntry("guide", BoardTileOverlayLayer.Guide, Color.yellow, 0.4f, 0));
            var bounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0));
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    bounds,
                    1f,
                    sourceTopology,
                    boardTileOverlayCatalog: overlayCatalog,
                    boardTileOverlayOverrides: new[]
                    {
                        OverlayOverride(new SurfaceCell(FaceId.Floor, 0, 0), "guide"),
                        OverlayOverride(new SurfaceCell(FaceId.Front, 0, 0), "guide"),
                    });
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out _),
                    Is.True);

                renderer.Initialize(
                    bounds,
                    1f,
                    sourceTopology,
                    boardTileOverlayCatalog: overlayCatalog,
                    boardTileOverlayOverrides: new[]
                    {
                        OverlayOverride(new SurfaceCell(FaceId.Floor, 1, 0), "guide"),
                        OverlayOverride(new SurfaceCell(FaceId.Front, 0, 0), "guide"),
                    });

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out var oldHandle),
                    Is.True);
                Assert.That(oldHandle.OverlayRenderers, Is.Empty);
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 1, 0), out var newHandle),
                    Is.True);
                Assert.That(newHandle.OverlayRenderers.Count, Is.EqualTo(1));

                renderer.BeginTopologyTransition(sourceTopology, destinationTopology);

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Front, 0, 0), out var transitionHandle),
                    Is.True);
                Assert.That(transitionHandle.OverlayRenderers.Count, Is.EqualTo(1));
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out _),
                    Is.False);

                renderer.CompleteTopologyTransition(destinationTopology);

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Front, 0, 0), out var steadyHandle),
                    Is.True);
                Assert.That(steadyHandle.OverlayRenderers.Count, Is.EqualTo(1));
                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out _),
                    Is.False);
            }
            finally
            {
                DestroyObjects(rootObject, overlayCatalog);
            }
        }

        [Test]
        [Category("Full")]
        public void BoardSurfaceRenderer_SuppressedCellsDoNotExposeOverlayHandles()
        {
            var rootObject = new GameObject("BoardSurfaceRenderer_SuppressedCellsDoNotExposeOverlayHandles");
            var overlayCatalog = CreateOverlayCatalog(
                OverlayEntry("guide", BoardTileOverlayLayer.Guide, Color.yellow, 0.4f, 0));

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();

                renderer.Initialize(
                    new BoardBounds(Vector2Int.zero, Vector2Int.zero),
                    1f,
                    new CubeTopologyState(FaceId.Floor),
                    boardTileOverlayCatalog: overlayCatalog,
                    boardTileOverlayOverrides: new[]
                    {
                        OverlayOverride(new SurfaceCell(FaceId.Floor, 0, 0), "guide"),
                    },
                    suppressedBaseTileCells: new[]
                    {
                        new SurfaceCell(FaceId.Floor, 0, 0),
                    });

                Assert.That(
                    renderer.TryGetTileVisualHandle(new SurfaceCell(FaceId.Floor, 0, 0), out _),
                    Is.False);
            }
            finally
            {
                DestroyObjects(rootObject, overlayCatalog);
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

        private static BoardTileStyleCatalogEntry StyleEntry(string styleKey, Color tint)
        {
            var entry = new BoardTileStyleCatalogEntry();
            SetPrivateField(entry, "styleKey", styleKey);
            SetPrivateField(entry, "displayName", styleKey);
            SetPrivateField(entry, "tint", tint);
            return entry;
        }

        private static BoardTileStyleCatalog CreateStyleCatalog(
            params BoardTileStyleCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<BoardTileStyleCatalog>();
            catalog.name = "BoardSurfaceRendererBoardTileStyleCatalogTests";
            SetPrivateField(catalog, "entries", entries ?? Array.Empty<BoardTileStyleCatalogEntry>());
            return catalog;
        }

        private static BoardTilePaintOverride PaintOverride(SurfaceCell cell, string styleKey)
        {
            return new BoardTilePaintOverride(cell, styleKey);
        }

        private static BoardTileOverlayCatalogEntry OverlayEntry(
            string overlayKey,
            BoardTileOverlayLayer layer,
            Color tint,
            float alpha,
            int order)
        {
            var entry = new BoardTileOverlayCatalogEntry();
            SetPrivateField(entry, "overlayKey", overlayKey);
            SetPrivateField(entry, "displayName", overlayKey);
            SetPrivateField(entry, "layer", layer);
            SetPrivateField(entry, "tint", tint);
            SetPrivateField(entry, "alpha", alpha);
            SetPrivateField(entry, "order", order);
            return entry;
        }

        private static BoardTileOverlayCatalog CreateOverlayCatalog(
            params BoardTileOverlayCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<BoardTileOverlayCatalog>();
            catalog.name = "BoardSurfaceRendererBoardTileOverlayCatalogTests";
            SetPrivateField(catalog, "entries", entries ?? Array.Empty<BoardTileOverlayCatalogEntry>());
            return catalog;
        }

        private static BoardTileOverlayOverride OverlayOverride(SurfaceCell cell, string overlayKey)
        {
            return new BoardTileOverlayOverride(cell, overlayKey);
        }

        private static void AssertRendererTint(Renderer renderer, Color expected)
        {
            Assert.That(renderer, Is.Not.Null);
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(expected));
            Assert.That(propertyBlock.GetColor("_Color"), Is.EqualTo(expected));
        }

        private static void AssertRendererNotTinted(Renderer renderer, Color staleTint)
        {
            Assert.That(renderer, Is.Not.Null);
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            Assert.That(propertyBlock.GetColor("_BaseColor"), Is.Not.EqualTo(staleTint));
            Assert.That(propertyBlock.GetColor("_Color"), Is.Not.EqualTo(staleTint));
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

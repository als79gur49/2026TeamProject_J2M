using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayBoardSurfaceRenderer : MonoBehaviour
    {
        private const string VisibleTilePoolObjectName = "VisibleTilePool";
        private const float TileCoverageMultiplier = 0.98f;
        private const float TileThicknessMultiplier = 0.08f;

        [SerializeField] private bool renderDecorativeFaces = true;
        [SerializeField] private Transform visibleTilePoolRoot;

        private readonly List<Material> _ownedMaterials = new();
        private readonly List<SurfaceTileView> _tilePool = new();

        private BoardBounds _boardBounds;
        private float _cellSize;
        private Material _activeBottomFaceMaterial;
        private Material _activeFrontFaceMaterial;
        private Material _decorativeBackFaceMaterial;
        private Material _decorativeTopFaceMaterial;
        private bool _isInitialized;
        private GameplayCubeProjector _projector;

        public int ActiveTileCount { get; private set; }

        public Transform VisibleTilePoolRoot => visibleTilePoolRoot != null ? visibleTilePoolRoot : EnsureVisibleTilePoolRoot();

        private void Awake()
        {
            EnsureVisibleTilePoolRoot();
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _ownedMaterials.Count; i++)
            {
                var material = _ownedMaterials[i];
                if (material == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }

            _ownedMaterials.Clear();
        }

        public void Initialize(
            BoardBounds boardBounds,
            float cellSize,
            CubeTopologyState topology)
        {
            if (!boardBounds.IsBounded)
            {
                throw new InvalidOperationException("GameplayBoardSurfaceRenderer requires bounded board bounds.");
            }

            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            _boardBounds = boardBounds;
            _cellSize = cellSize;
            _projector = new GameplayCubeProjector(boardBounds, cellSize);
            EnsureVisibleTilePoolRoot();
            EnsureMaterials();
            EnsureTilePool(GetRequiredTileCount());
            _isInitialized = true;
            RefreshTopology(topology);
        }

        public void RefreshTopology(CubeTopologyState topology)
        {
            EnsureInitialized();

            var tileScale = ResolveTileScale();
            var tileIndex = 0;

            tileIndex = PopulateFaceTiles(topology.BottomFace, topology, SurfaceTileRole.ActiveBottom, tileScale, tileIndex);
            tileIndex = PopulateFaceTiles(topology.FrontFace, topology, SurfaceTileRole.ActiveFront, tileScale, tileIndex);

            if (renderDecorativeFaces)
            {
                tileIndex = PopulateFaceTiles(
                    FaceIdUtility.GetNext(topology.FrontFace),
                    topology,
                    SurfaceTileRole.DecorativeTop,
                    tileScale,
                    tileIndex);
                tileIndex = PopulateFaceTiles(
                    FaceIdUtility.GetPrevious(topology.BottomFace),
                    topology,
                    SurfaceTileRole.DecorativeBack,
                    tileScale,
                    tileIndex);
            }

            ActiveTileCount = tileIndex;
            for (var i = tileIndex; i < _tilePool.Count; i++)
            {
                _tilePool[i].SetActive(false);
            }
        }

        private Transform EnsureVisibleTilePoolRoot()
        {
            if (visibleTilePoolRoot == null)
            {
                var existingChild = transform.Find(VisibleTilePoolObjectName);
                if (existingChild == null)
                {
                    var tilePoolObject = new GameObject(VisibleTilePoolObjectName);
                    existingChild = tilePoolObject.transform;
                    existingChild.SetParent(transform, worldPositionStays: false);
                }
                else if (existingChild.parent != transform)
                {
                    existingChild.SetParent(transform, worldPositionStays: false);
                }

                visibleTilePoolRoot = existingChild;
            }

            visibleTilePoolRoot.name = VisibleTilePoolObjectName;
            visibleTilePoolRoot.localPosition = Vector3.zero;
            visibleTilePoolRoot.localRotation = Quaternion.identity;
            visibleTilePoolRoot.localScale = Vector3.one;
            return visibleTilePoolRoot;
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized || _projector == null)
            {
                throw new InvalidOperationException("GameplayBoardSurfaceRenderer must be initialized before use.");
            }
        }

        private void EnsureMaterials()
        {
            if (_activeBottomFaceMaterial != null &&
                _activeFrontFaceMaterial != null &&
                _decorativeTopFaceMaterial != null &&
                _decorativeBackFaceMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "GameplayBoardSurfaceRenderer requires the 'Universal Render Pipeline/Unlit' shader.");
            }

            _activeBottomFaceMaterial = CreateMaterial(shader, "BoardSurface_ActiveBottom", new Color(0.77f, 0.84f, 0.92f));
            _activeFrontFaceMaterial = CreateMaterial(shader, "BoardSurface_ActiveFront", new Color(0.63f, 0.72f, 0.82f));
            _decorativeTopFaceMaterial = CreateMaterial(shader, "BoardSurface_DecorativeTop", new Color(0.45f, 0.5f, 0.58f));
            _decorativeBackFaceMaterial = CreateMaterial(shader, "BoardSurface_DecorativeBack", new Color(0.33f, 0.37f, 0.44f));
        }

        private Material CreateMaterial(Shader shader, string materialName, Color color)
        {
            var material = new Material(shader)
            {
                name = materialName,
                color = color,
            };

            _ownedMaterials.Add(material);
            return material;
        }

        private void EnsureTilePool(int requiredTileCount)
        {
            while (_tilePool.Count < requiredTileCount)
            {
                _tilePool.Add(CreateTileView(_tilePool.Count));
            }
        }

        private SurfaceTileView CreateTileView(int index)
        {
            var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObject.name = $"SurfaceTile_{index}";
            tileObject.transform.SetParent(VisibleTilePoolRoot, worldPositionStays: false);
            tileObject.transform.localPosition = Vector3.zero;
            tileObject.transform.localRotation = Quaternion.identity;
            tileObject.transform.localScale = Vector3.one;

            var collider = tileObject.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            var renderer = tileObject.GetComponent<MeshRenderer>();
            return new SurfaceTileView(tileObject, tileObject.transform, renderer);
        }

        private int PopulateFaceTiles(
            FaceId face,
            CubeTopologyState topology,
            SurfaceTileRole tileRole,
            Vector3 tileScale,
            int tileIndex)
        {
            for (var y = _boardBounds.MinInclusive.y; y <= _boardBounds.MaxInclusive.y; y++)
            {
                for (var x = _boardBounds.MinInclusive.x; x <= _boardBounds.MaxInclusive.x; x++)
                {
                    var cell = new SurfaceCell(face, x, y);
                    if (!_projector.TryProjectSurfaceCell(cell, topology, out var projectedPose))
                    {
                        throw new InvalidOperationException(
                            $"Failed to project visible board surface cell '{cell}' for topology '{topology}'.");
                    }

                    var tileView = _tilePool[tileIndex++];
                    tileView.GameObject.name = $"{tileRole}_{cell.face}_{cell.x}_{cell.y}";
                    tileView.Transform.localPosition = projectedPose.LocalPosition - (projectedPose.Normal * (tileScale.z * 0.5f));
                    tileView.Transform.localRotation = projectedPose.LocalRotation;
                    tileView.Transform.localScale = tileScale;
                    tileView.Renderer.sharedMaterial = ResolveMaterial(tileRole);
                    tileView.SetActive(true);
                }
            }

            return tileIndex;
        }

        private int GetRequiredTileCount()
        {
            var width = _boardBounds.MaxInclusive.x - _boardBounds.MinInclusive.x + 1;
            var height = _boardBounds.MaxInclusive.y - _boardBounds.MinInclusive.y + 1;
            var visibleFaceCount = renderDecorativeFaces ? 4 : 2;
            return width * height * visibleFaceCount;
        }

        private Vector3 ResolveTileScale()
        {
            return new Vector3(
                _cellSize * TileCoverageMultiplier,
                _cellSize * TileCoverageMultiplier,
                _cellSize * TileThicknessMultiplier);
        }

        private Material ResolveMaterial(SurfaceTileRole tileRole)
        {
            return tileRole switch
            {
                SurfaceTileRole.ActiveBottom => _activeBottomFaceMaterial,
                SurfaceTileRole.ActiveFront => _activeFrontFaceMaterial,
                SurfaceTileRole.DecorativeTop => _decorativeTopFaceMaterial,
                _ => _decorativeBackFaceMaterial,
            };
        }

        private enum SurfaceTileRole
        {
            ActiveBottom = 0,
            ActiveFront = 1,
            DecorativeTop = 2,
            DecorativeBack = 3,
        }

        private sealed class SurfaceTileView
        {
            public SurfaceTileView(GameObject gameObject, Transform transform, MeshRenderer renderer)
            {
                GameObject = gameObject;
                Transform = transform;
                Renderer = renderer;
            }

            public GameObject GameObject { get; }

            public MeshRenderer Renderer { get; }

            public Transform Transform { get; }

            public void SetActive(bool isActive)
            {
                if (GameObject.activeSelf == isActive)
                {
                    return;
                }

                GameObject.SetActive(isActive);
            }
        }
    }
}

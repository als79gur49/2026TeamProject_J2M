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
        private const string TransitionTilePoolObjectName = "TransitionTilePool";
        private const float TileCoverageMultiplier = 0.98f;
        private const float TileThicknessMultiplier = 0.08f;

        [SerializeField] private bool renderDecorativeFaces = false;
        [SerializeField] private Transform visibleTilePoolRoot;
        [SerializeField] private Transform transitionTilePoolRoot;

        private readonly List<Material> _ownedMaterials = new();
        private readonly List<SurfaceTileView> _steadyTilePool = new();
        private readonly List<SurfaceTileView> _transitionTilePool = new();

        private BoardBounds _boardBounds;
        private float _cellSize;
        private Material _activeBottomFaceMaterial;
        private Material _activeFrontFaceMaterial;
        private Material _decorativeBackFaceMaterial;
        private Material _decorativeTopFaceMaterial;
        private bool _isInitialized;
        private CubeTopologyState _steadyTopology;
        private GameplayCubeProjector _projector;

        public int ActiveTileCount => SteadyTileCount + TransitionTileCount;

        public bool IsTopologyTransitionActive => TransitionTileCount > 0;

        public int SteadyTileCount { get; private set; }

        public CubeTopologyState SteadyTopology => _steadyTopology;

        public int TransitionTileCount { get; private set; }

        public Transform VisibleTilePoolRoot => visibleTilePoolRoot != null ? visibleTilePoolRoot : EnsureVisibleTilePoolRoot();

        public Transform TransitionTilePoolRoot => transitionTilePoolRoot != null ? transitionTilePoolRoot : EnsureTransitionTilePoolRoot();

        private void Awake()
        {
            EnsureVisibleTilePoolRoot();
            EnsureTransitionTilePoolRoot();
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
            EnsureTransitionTilePoolRoot();
            EnsureMaterials();
            EnsureTilePool(_steadyTilePool, VisibleTilePoolRoot, GetRequiredTileCount());
            EnsureTilePool(_transitionTilePool, TransitionTilePoolRoot, GetRequiredTileCount());
            _isInitialized = true;
            RefreshTopology(topology);
        }

        public void RefreshTopology(CubeTopologyState topology)
        {
            EnsureInitialized();
            RefreshSteadyTopology(topology);
            ClearTopologyTransition();
        }

        public void BeginTopologyTransition(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            Quaternion transitionStartRotation)
        {
            EnsureInitialized();

            RefreshSteadyTopology(destinationTopology);
            ClearTopologyTransition();
            EnsureTilePool(_transitionTilePool, TransitionTilePoolRoot, GetRequiredTileCount());

            var tileScale = ResolveTileScale();
            var tileIndex = 0;

            tileIndex = PopulateRetainedFaceTilesIfNeeded(
                sourceTopology.BottomFace,
                sourceTopology,
                SurfaceTileRole.ActiveBottom,
                tileScale,
                tileIndex,
                transitionStartRotation);
            tileIndex = PopulateRetainedFaceTilesIfNeeded(
                sourceTopology.FrontFace,
                sourceTopology,
                SurfaceTileRole.ActiveFront,
                tileScale,
                tileIndex,
                transitionStartRotation);

            if (renderDecorativeFaces)
            {
                tileIndex = PopulateRetainedFaceTilesIfNeeded(
                    FaceIdUtility.GetNext(sourceTopology.FrontFace),
                    sourceTopology,
                    SurfaceTileRole.DecorativeTop,
                    tileScale,
                    tileIndex,
                    transitionStartRotation);
                tileIndex = PopulateRetainedFaceTilesIfNeeded(
                    FaceIdUtility.GetPrevious(sourceTopology.BottomFace),
                    sourceTopology,
                    SurfaceTileRole.DecorativeBack,
                    tileScale,
                    tileIndex,
                    transitionStartRotation);
            }

            TransitionTileCount = tileIndex;
            for (var i = tileIndex; i < _transitionTilePool.Count; i++)
            {
                _transitionTilePool[i].SetActive(false);
            }
        }

        public void CompleteTopologyTransition(CubeTopologyState topology)
        {
            EnsureInitialized();
            RefreshSteadyTopology(topology);
            ClearTopologyTransition();
        }

        public void ClearTopologyTransition()
        {
            TransitionTileCount = 0;
            for (var i = 0; i < _transitionTilePool.Count; i++)
            {
                _transitionTilePool[i].SetActive(false);
            }
        }

        private void RefreshSteadyTopology(CubeTopologyState topology)
        {
            _steadyTopology = topology;
            var tileScale = ResolveTileScale();
            var tileIndex = 0;

            tileIndex = PopulateFaceTiles(
                topology.BottomFace,
                topology,
                SurfaceTileRole.ActiveBottom,
                tileScale,
                tileIndex,
                _steadyTilePool);
            tileIndex = PopulateFaceTiles(
                topology.FrontFace,
                topology,
                SurfaceTileRole.ActiveFront,
                tileScale,
                tileIndex,
                _steadyTilePool);

            if (renderDecorativeFaces)
            {
                tileIndex = PopulateFaceTiles(
                    FaceIdUtility.GetNext(topology.FrontFace),
                    topology,
                    SurfaceTileRole.DecorativeTop,
                    tileScale,
                    tileIndex,
                    _steadyTilePool);
                tileIndex = PopulateFaceTiles(
                    FaceIdUtility.GetPrevious(topology.BottomFace),
                    topology,
                    SurfaceTileRole.DecorativeBack,
                    tileScale,
                    tileIndex,
                    _steadyTilePool);
            }

            SteadyTileCount = tileIndex;
            for (var i = tileIndex; i < _steadyTilePool.Count; i++)
            {
                _steadyTilePool[i].SetActive(false);
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

        private Transform EnsureTransitionTilePoolRoot()
        {
            if (transitionTilePoolRoot == null)
            {
                var existingChild = transform.Find(TransitionTilePoolObjectName);
                if (existingChild == null)
                {
                    var tilePoolObject = new GameObject(TransitionTilePoolObjectName);
                    existingChild = tilePoolObject.transform;
                    existingChild.SetParent(transform, worldPositionStays: false);
                }
                else if (existingChild.parent != transform)
                {
                    existingChild.SetParent(transform, worldPositionStays: false);
                }

                transitionTilePoolRoot = existingChild;
            }

            transitionTilePoolRoot.name = TransitionTilePoolObjectName;
            transitionTilePoolRoot.localPosition = Vector3.zero;
            transitionTilePoolRoot.localRotation = Quaternion.identity;
            transitionTilePoolRoot.localScale = Vector3.one;
            return transitionTilePoolRoot;
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

        private void EnsureTilePool(List<SurfaceTileView> tilePool, Transform tilePoolRoot, int requiredTileCount)
        {
            while (tilePool.Count < requiredTileCount)
            {
                tilePool.Add(CreateTileView(tilePool.Count, tilePoolRoot));
            }
        }

        private SurfaceTileView CreateTileView(int index, Transform parent)
        {
            var tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObject.name = $"SurfaceTile_{index}";
            tileObject.transform.SetParent(parent, worldPositionStays: false);
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
            int tileIndex,
            List<SurfaceTileView> tilePool)
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

                    var tileView = tilePool[tileIndex++];
                    ApplyTilePose(tileView, tileRole, cell, projectedPose, tileScale);
                }
            }

            return tileIndex;
        }

        private int PopulateRetainedFaceTilesIfNeeded(
            FaceId face,
            CubeTopologyState sourceTopology,
            SurfaceTileRole tileRole,
            Vector3 tileScale,
            int tileIndex,
            Quaternion transitionStartRotation)
        {
            if (!IsFaceVisibleInTopology(face, sourceTopology) ||
                IsFaceVisibleInTopology(face, SteadyTopology))
            {
                return tileIndex;
            }

            for (var y = _boardBounds.MinInclusive.y; y <= _boardBounds.MaxInclusive.y; y++)
            {
                for (var x = _boardBounds.MinInclusive.x; x <= _boardBounds.MaxInclusive.x; x++)
                {
                    var cell = new SurfaceCell(face, x, y);
                    if (!TryResolveRetainedTransitionTilePose(
                            cell,
                            sourceTopology,
                            transitionStartRotation,
                            out var projectedPose))
                    {
                        throw new InvalidOperationException(
                            $"Failed to project retained board surface cell '{cell}' for topology '{sourceTopology}'.");
                    }

                    var tileView = _transitionTilePool[tileIndex++];
                    ApplyTilePose(tileView, tileRole, cell, projectedPose, tileScale);
                }
            }

            return tileIndex;
        }

        private void ApplyTilePose(
            SurfaceTileView tileView,
            SurfaceTileRole tileRole,
            SurfaceCell cell,
            ProjectedCellPose projectedPose,
            Vector3 tileScale)
        {
            tileView.GameObject.name = $"{tileRole}_{cell.face}_{cell.x}_{cell.y}";
            tileView.Transform.localPosition = projectedPose.LocalPosition - (projectedPose.Normal * (tileScale.z * 0.5f));
            tileView.Transform.localRotation = projectedPose.LocalRotation;
            tileView.Transform.localScale = tileScale;
            tileView.Renderer.sharedMaterial = ResolveMaterial(tileRole);
            tileView.SetActive(true);
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

        private bool IsFaceVisibleInTopology(FaceId face, CubeTopologyState topology)
        {
            if (face == topology.BottomFace ||
                face == topology.FrontFace)
            {
                return true;
            }

            if (!renderDecorativeFaces)
            {
                return false;
            }

            return face == FaceIdUtility.GetNext(topology.FrontFace) ||
                   face == FaceIdUtility.GetPrevious(topology.BottomFace);
        }

        private bool TryResolveRetainedTransitionTilePose(
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            Quaternion transitionStartRotation,
            out ProjectedCellPose projectedPose)
        {
            if (!_projector.TryProjectSurfaceCell(
                    cell,
                    sourceTopology,
                    out var sourceProjectedPose))
            {
                projectedPose = default;
                return false;
            }

            var inverseTransitionStartRotation = Quaternion.Inverse(transitionStartRotation);
            projectedPose = new ProjectedCellPose(
                inverseTransitionStartRotation * sourceProjectedPose.LocalPosition,
                inverseTransitionStartRotation * sourceProjectedPose.LocalRotation,
                inverseTransitionStartRotation * sourceProjectedPose.Normal);
            return true;
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

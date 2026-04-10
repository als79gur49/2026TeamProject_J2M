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
        [SerializeField] private bool renderDecorativeFaces = false;
        [SerializeField] private Transform visibleTilePoolRoot;
        [SerializeField] private Transform transitionTilePoolRoot;

        private readonly List<Material> _ownedMaterials = new();
        private readonly List<SurfaceTileView> _steadyTilePool = new();
        private readonly List<SurfaceTransitionTileState> _transitionTileStates = new();
        private readonly List<SurfaceTileView> _transitionTilePool = new();

        private BoardBounds _boardBounds;
        private float _cellSize;
        private Material _activeBottomFaceMaterial;
        private Material _activeFrontFaceMaterial;
        private Material _decorativeBackFaceMaterial;
        private Material _decorativeTopFaceMaterial;
        private bool _isInitialized;
        private bool _areSteadyTilesVisible = true;
        private CubeTopologyState _steadyTopology;
        private GameplayCubeProjector _projector;

        public int ActiveTileCount => (_areSteadyTilesVisible ? SteadyTileCount : 0) + TransitionTileCount;

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
            EnsureTilePool(_transitionTilePool, TransitionTilePoolRoot, GetRequiredTransitionTileCount());
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
            CubeTopologyState destinationTopology)
        {
            EnsureInitialized();

            ClearTopologyTransition();
            if (!_steadyTopology.Equals(sourceTopology))
            {
                RefreshSteadyTopology(sourceTopology);
            }

            SetSteadyTilesActive(isActive: false);
            EnsureTilePool(_transitionTilePool, TransitionTilePoolRoot, GetRequiredTransitionTileCount());

            var tileScale = ResolveTileScale();
            var tileIndex = 0;

            tileIndex = PopulateTransitionFaceTilesIfNeeded(
                FaceId.Floor,
                sourceTopology,
                destinationTopology,
                tileScale,
                tileIndex);
            tileIndex = PopulateTransitionFaceTilesIfNeeded(
                FaceId.Front,
                sourceTopology,
                destinationTopology,
                tileScale,
                tileIndex);
            tileIndex = PopulateTransitionFaceTilesIfNeeded(
                FaceId.Ceiling,
                sourceTopology,
                destinationTopology,
                tileScale,
                tileIndex);
            tileIndex = PopulateTransitionFaceTilesIfNeeded(
                FaceId.Back,
                sourceTopology,
                destinationTopology,
                tileScale,
                tileIndex);

            TransitionTileCount = tileIndex;
            for (var i = tileIndex; i < _transitionTilePool.Count; i++)
            {
                _transitionTilePool[i].SetActive(false);
            }
        }

        public void UpdateTopologyTransition(float progress)
        {
            EnsureInitialized();

            if (TransitionTileCount <= 0)
            {
                return;
            }

            _ = Mathf.Clamp01(progress);

            // Transition tiles stay pinned to their physical-face local pose.
            // Camera orbit supplies the visual topology motion, so progress does not remap geometry here.
            var tileScale = ResolveTileScale();
            for (var i = 0; i < TransitionTileCount; i++)
            {
                var tileState = _transitionTileStates[i];
                ApplyTilePose(_transitionTilePool[i], tileState.TileRole, tileState.Cell, tileState.LocalPose, tileScale);
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
            _transitionTileStates.Clear();
            SetSteadyTilesActive(isActive: true);
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
            _areSteadyTilesVisible = true;
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

        private int PopulateTransitionFaceTilesIfNeeded(
            FaceId face,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            Vector3 tileScale,
            int tileIndex)
        {
            if (!IsFaceVisibleInTopology(face, sourceTopology) &&
                !IsFaceVisibleInTopology(face, destinationTopology))
            {
                return tileIndex;
            }

            var tileRole = ResolveTransitionTileRole(face, sourceTopology, destinationTopology);

            for (var y = _boardBounds.MinInclusive.y; y <= _boardBounds.MaxInclusive.y; y++)
            {
                for (var x = _boardBounds.MinInclusive.x; x <= _boardBounds.MaxInclusive.x; x++)
                {
                    var cell = new SurfaceCell(face, x, y);
                    if (!TryResolveTransitionTileLocalPose(
                            cell,
                            sourceTopology,
                            destinationTopology,
                            out var localPose))
                    {
                        throw new InvalidOperationException(
                            $"Failed to project board surface transition cell '{cell}' from '{sourceTopology}' to '{destinationTopology}'.");
                    }

                    var tileView = _transitionTilePool[tileIndex++];
                    if (_transitionTileStates.Count < tileIndex)
                    {
                        _transitionTileStates.Add(new SurfaceTransitionTileState(cell, tileRole, localPose));
                    }
                    else
                    {
                        _transitionTileStates[tileIndex - 1] = new SurfaceTransitionTileState(cell, tileRole, localPose);
                    }

                    ApplyTilePose(tileView, tileRole, cell, localPose, tileScale);
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
            ApplyTilePose(
                tileView,
                tileRole,
                cell,
                ResolveTileLocalPose(projectedPose, tileScale),
                tileScale);
        }

        private void ApplyTilePose(
            SurfaceTileView tileView,
            SurfaceTileRole tileRole,
            SurfaceCell cell,
            GameplayEntityPose localPose,
            Vector3 tileScale)
        {
            tileView.GameObject.name = $"{tileRole}_{cell.face}_{cell.x}_{cell.y}";
            tileView.Transform.localPosition = localPose.Position;
            tileView.Transform.localRotation = localPose.Rotation;
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

        private int GetRequiredTransitionTileCount()
        {
            var width = _boardBounds.MaxInclusive.x - _boardBounds.MinInclusive.x + 1;
            var height = _boardBounds.MaxInclusive.y - _boardBounds.MinInclusive.y + 1;
            return width * height * 4;
        }

        private Vector3 ResolveTileScale()
        {
            return new Vector3(
                _cellSize * GameplayPresentationGeometry.TileCoverageMultiplier,
                _cellSize * GameplayPresentationGeometry.TileCoverageMultiplier,
                _cellSize * GameplayPresentationGeometry.TileThicknessMultiplier);
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

        private SurfaceTileRole ResolveTransitionTileRole(
            FaceId face,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology)
        {
            if (TryResolveTileRole(face, sourceTopology, out var sourceTileRole))
            {
                return sourceTileRole;
            }

            if (TryResolveTileRole(face, destinationTopology, out var destinationTileRole))
            {
                return destinationTileRole;
            }

            throw new InvalidOperationException($"Face '{face}' is not visible in either transition topology.");
        }

        private bool TryResolveTileRole(FaceId face, CubeTopologyState topology, out SurfaceTileRole tileRole)
        {
            if (face == topology.BottomFace)
            {
                tileRole = SurfaceTileRole.ActiveBottom;
                return true;
            }

            if (face == topology.FrontFace)
            {
                tileRole = SurfaceTileRole.ActiveFront;
                return true;
            }

            if (renderDecorativeFaces && face == FaceIdUtility.GetNext(topology.FrontFace))
            {
                tileRole = SurfaceTileRole.DecorativeTop;
                return true;
            }

            if (renderDecorativeFaces && face == FaceIdUtility.GetPrevious(topology.BottomFace))
            {
                tileRole = SurfaceTileRole.DecorativeBack;
                return true;
            }

            tileRole = default;
            return false;
        }

        private void SetSteadyTilesActive(bool isActive)
        {
            _areSteadyTilesVisible = isActive;
            for (var i = 0; i < SteadyTileCount && i < _steadyTilePool.Count; i++)
            {
                _steadyTilePool[i].SetActive(isActive);
            }
        }

        private GameplayEntityPose ResolveTileLocalPose(ProjectedCellPose projectedPose, Vector3 tileScale)
        {
            return new GameplayEntityPose(
                projectedPose.LocalPosition - (projectedPose.Normal * (tileScale.z * 0.5f)),
                projectedPose.LocalRotation);
        }

        private bool TryResolveTransitionTileLocalPose(
            SurfaceCell cell,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            out GameplayEntityPose localPose)
        {
            if (!_projector.TryProjectTransitionSurfaceCell(
                    cell,
                    sourceTopology,
                    destinationTopology,
                    out var projectedPose))
            {
                localPose = default;
                return false;
            }

            var tileScale = ResolveTileScale();
            localPose = ResolveTileLocalPose(projectedPose, tileScale);
            return true;
        }

        private enum SurfaceTileRole
        {
            ActiveBottom = 0,
            ActiveFront = 1,
            DecorativeTop = 2,
            DecorativeBack = 3,
        }

        private readonly struct SurfaceTransitionTileState
        {
            public SurfaceTransitionTileState(
                SurfaceCell cell,
                SurfaceTileRole tileRole,
                GameplayEntityPose localPose)
            {
                Cell = cell;
                TileRole = tileRole;
                LocalPose = localPose;
            }

            public SurfaceCell Cell { get; }

            public SurfaceTileRole TileRole { get; }

            public GameplayEntityPose LocalPose { get; }
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

using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Stages;
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
        private readonly List<SurfaceTileView> _steadyActiveTiles = new();
        private readonly List<SurfaceTransitionTileState> _transitionTileStates = new();
        private readonly List<SurfaceTileView> _transitionActiveTiles = new();
        private readonly Dictionary<BoardTilePoolKey, List<SurfaceTileView>> _steadyTilePools = new();
        private readonly Dictionary<BoardTilePoolKey, List<SurfaceTileView>> _transitionTilePools = new();

        private BoardBounds _boardBounds;
        private float _cellSize;
        private BoardTilePresentationCatalog _boardTilePresentationCatalog;
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
            CubeTopologyState topology,
            float faceSeamGap = -1f,
            Texture2D sharedTileTexture = null,
            BoardTilePresentationCatalog boardTilePresentationCatalog = null)
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
            if (_boardTilePresentationCatalog != boardTilePresentationCatalog)
            {
                DestroyAllTilePools();
                _boardTilePresentationCatalog = boardTilePresentationCatalog;
            }

            var resolvedFaceSeamGap = faceSeamGap >= 0f ? faceSeamGap : cellSize;
            _projector = new GameplayCubeProjector(boardBounds, cellSize, resolvedFaceSeamGap);
            EnsureVisibleTilePoolRoot();
            EnsureTransitionTilePoolRoot();
            EnsureMaterials(sharedTileTexture);
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
            DeactivateAllTileViews(_transitionTilePools);
            _transitionActiveTiles.Clear();

            var tileScale = ResolveTileScale();
            var tileIndex = 0;

            tileIndex = PopulateTransitionFaceTilesIfNeeded(
                FaceId.Floor,
                destinationTopology,
                tileScale,
                tileIndex);
            tileIndex = PopulateTransitionFaceTilesIfNeeded(
                FaceId.Front,
                destinationTopology,
                tileScale,
                tileIndex);
            tileIndex = PopulateTransitionFaceTilesIfNeeded(
                FaceId.Ceiling,
                destinationTopology,
                tileScale,
                tileIndex);
            tileIndex = PopulateTransitionFaceTilesIfNeeded(
                FaceId.Back,
                destinationTopology,
                tileScale,
                tileIndex);

            TransitionTileCount = tileIndex;
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
                ApplyTilePose(_transitionActiveTiles[i], tileState.TileRole, tileState.Cell, tileState.LocalPose, tileScale);
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
            DeactivateAllTileViews(_transitionTilePools);
            _transitionActiveTiles.Clear();
        }

        private void RefreshSteadyTopology(CubeTopologyState topology)
        {
            _steadyTopology = topology;
            DeactivateAllTileViews(_steadyTilePools);
            _steadyActiveTiles.Clear();
            var tileScale = ResolveTileScale();
            var tileIndex = 0;

            tileIndex = PopulateFaceTiles(
                topology.BottomFace,
                topology,
                SurfaceTileRole.ActiveBottom,
                tileScale,
                tileIndex,
                _steadyTilePools,
                _steadyActiveTiles,
                VisibleTilePoolRoot);
            tileIndex = PopulateFaceTiles(
                topology.FrontFace,
                topology,
                SurfaceTileRole.ActiveFront,
                tileScale,
                tileIndex,
                _steadyTilePools,
                _steadyActiveTiles,
                VisibleTilePoolRoot);

            if (renderDecorativeFaces)
            {
                tileIndex = PopulateFaceTiles(
                    FaceIdUtility.GetNext(topology.FrontFace),
                    topology,
                    SurfaceTileRole.DecorativeTop,
                    tileScale,
                    tileIndex,
                    _steadyTilePools,
                    _steadyActiveTiles,
                    VisibleTilePoolRoot);
                tileIndex = PopulateFaceTiles(
                    FaceIdUtility.GetPrevious(topology.BottomFace),
                    topology,
                    SurfaceTileRole.DecorativeBack,
                    tileScale,
                    tileIndex,
                    _steadyTilePools,
                    _steadyActiveTiles,
                    VisibleTilePoolRoot);
            }

            SteadyTileCount = tileIndex;
            _areSteadyTilesVisible = true;
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

        private void EnsureMaterials(Texture2D sharedTileTexture)
        {
            if (sharedTileTexture != null)
            {
                var sharedTileMaterial = CreateTexturedMaterial(
                    "BoardSurface_SharedTexture",
                    sharedTileTexture);
                _activeBottomFaceMaterial = sharedTileMaterial;
                _activeFrontFaceMaterial = sharedTileMaterial;
                _decorativeTopFaceMaterial = sharedTileMaterial;
                _decorativeBackFaceMaterial = sharedTileMaterial;
                return;
            }

            if (_activeBottomFaceMaterial != null &&
                _activeFrontFaceMaterial != null &&
                _decorativeTopFaceMaterial != null &&
                _decorativeBackFaceMaterial != null &&
                _ownedMaterials.Count > 0)
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

        private Material CreateTexturedMaterial(string materialName, Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "GameplayBoardSurfaceRenderer requires the 'Universal Render Pipeline/Unlit' shader.");
            }

            var material = new Material(shader)
            {
                name = materialName,
                color = Color.white,
                mainTexture = texture,
            };

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            _ownedMaterials.Add(material);
            return material;
        }

        private SurfaceTileView RentTileView(
            Dictionary<BoardTilePoolKey, List<SurfaceTileView>> tilePools,
            List<SurfaceTileView> activeTiles,
            Transform tilePoolRoot,
            BoardTileVisualDescriptor descriptor)
        {
            if (!tilePools.TryGetValue(descriptor.PoolKey, out var tilePool))
            {
                tilePool = new List<SurfaceTileView>();
                tilePools.Add(descriptor.PoolKey, tilePool);
            }

            for (var i = 0; i < tilePool.Count; i++)
            {
                var candidate = tilePool[i];
                if (candidate != null &&
                    !candidate.GameObject.activeSelf &&
                    !activeTiles.Contains(candidate))
                {
                    activeTiles.Add(candidate);
                    return candidate;
                }
            }

            var tileView = CreateTileView(tilePool.Count, tilePoolRoot, descriptor);
            tilePool.Add(tileView);
            activeTiles.Add(tileView);
            return tileView;
        }

        private SurfaceTileView CreateTileView(
            int index,
            Transform parent,
            BoardTileVisualDescriptor descriptor)
        {
            var tileObject = descriptor.UsesPrefab
                ? Instantiate(descriptor.Prefab, parent)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObject.name = $"SurfaceTile_{index}";
            tileObject.transform.SetParent(parent, worldPositionStays: false);
            tileObject.transform.localPosition = Vector3.zero;
            tileObject.transform.localRotation = Quaternion.identity;
            tileObject.transform.localScale = Vector3.one;

            if (!descriptor.UsesPrefab)
            {
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
            }

            var renderer = tileObject.GetComponent<MeshRenderer>();
            tileObject.SetActive(false);
            return new SurfaceTileView(tileObject, tileObject.transform, renderer, descriptor);
        }

        private int PopulateFaceTiles(
            FaceId face,
            CubeTopologyState topology,
            SurfaceTileRole tileRole,
            Vector3 tileScale,
            int tileIndex,
            Dictionary<BoardTilePoolKey, List<SurfaceTileView>> tilePools,
            List<SurfaceTileView> activeTiles,
            Transform tilePoolRoot)
        {
            var descriptor = ResolveBoardTileVisualDescriptor(tileRole);
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

                    var tileView = RentTileView(tilePools, activeTiles, tilePoolRoot, descriptor);
                    tileIndex++;
                    ApplyTilePose(tileView, tileRole, cell, projectedPose, tileScale);
                }
            }

            return tileIndex;
        }

        private int PopulateTransitionFaceTilesIfNeeded(
            FaceId face,
            CubeTopologyState destinationTopology,
            Vector3 tileScale,
            int tileIndex)
        {
            if (!IsFaceVisibleInTopology(face, destinationTopology))
            {
                return tileIndex;
            }

            if (!TryResolveTileRole(face, destinationTopology, out var tileRole))
            {
                throw new InvalidOperationException(
                    $"Face '{face}' is not visible in destination topology '{destinationTopology}'.");
            }

            for (var y = _boardBounds.MinInclusive.y; y <= _boardBounds.MaxInclusive.y; y++)
            {
                for (var x = _boardBounds.MinInclusive.x; x <= _boardBounds.MaxInclusive.x; x++)
                {
                    var cell = new SurfaceCell(face, x, y);
                    if (!TryResolveTransitionTileLocalPose(
                            cell,
                            _steadyTopology,
                            destinationTopology,
                            out var localPose))
                    {
                        throw new InvalidOperationException(
                            $"Failed to project board surface transition cell '{cell}' from '{_steadyTopology}' to '{destinationTopology}'.");
                    }

                    var descriptor = ResolveBoardTileVisualDescriptor(tileRole);
                    var tileView = RentTileView(
                        _transitionTilePools,
                        _transitionActiveTiles,
                        TransitionTilePoolRoot,
                        descriptor);
                    tileIndex++;
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
            if (!tileView.Descriptor.UsesPrefab && tileView.Renderer != null)
            {
                tileView.Renderer.sharedMaterial = tileView.Descriptor.MaterialFallback ?? ResolveMaterial(tileRole);
            }

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

        private BoardTileVisualDescriptor ResolveBoardTileVisualDescriptor(SurfaceTileRole tileRole)
        {
            var visualRole = ToBoardTileVisualRole(tileRole);
            if (_boardTilePresentationCatalog != null)
            {
                if (_boardTilePresentationCatalog.TryGetDefaultEntry(visualRole, out var roleEntry))
                {
                    return BoardTileVisualDescriptor.FromEntry(visualRole, roleEntry, ResolveMaterial(tileRole));
                }

                if (_boardTilePresentationCatalog.TryGetDefaultEntry(
                        BoardTileVisualRole.GenericDefault,
                        out var genericEntry))
                {
                    return BoardTileVisualDescriptor.FromEntry(visualRole, genericEntry, ResolveMaterial(tileRole));
                }
            }

            return BoardTileVisualDescriptor.MaterialOnly(visualRole, ResolveMaterial(tileRole));
        }

        private static BoardTileVisualRole ToBoardTileVisualRole(SurfaceTileRole tileRole)
        {
            return tileRole switch
            {
                SurfaceTileRole.ActiveBottom => BoardTileVisualRole.ActiveBottom,
                SurfaceTileRole.ActiveFront => BoardTileVisualRole.ActiveFront,
                SurfaceTileRole.DecorativeTop => BoardTileVisualRole.DecorativeTop,
                _ => BoardTileVisualRole.DecorativeBack,
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
            for (var i = 0; i < SteadyTileCount && i < _steadyActiveTiles.Count; i++)
            {
                _steadyActiveTiles[i].SetActive(isActive);
            }
        }

        private void DestroyAllTilePools()
        {
            DestroyTilePools(_steadyTilePools);
            DestroyTilePools(_transitionTilePools);
            _steadyActiveTiles.Clear();
            _transitionActiveTiles.Clear();
            _transitionTileStates.Clear();
            SteadyTileCount = 0;
            TransitionTileCount = 0;
        }

        private void DestroyTilePools(Dictionary<BoardTilePoolKey, List<SurfaceTileView>> tilePools)
        {
            foreach (var pair in tilePools)
            {
                var tilePool = pair.Value;
                for (var i = 0; i < tilePool.Count; i++)
                {
                    var tileView = tilePool[i];
                    if (tileView == null || tileView.GameObject == null)
                    {
                        continue;
                    }

                    if (Application.isPlaying)
                    {
                        Destroy(tileView.GameObject);
                    }
                    else
                    {
                        DestroyImmediate(tileView.GameObject);
                    }
                }
            }

            tilePools.Clear();
        }

        private static void DeactivateAllTileViews(Dictionary<BoardTilePoolKey, List<SurfaceTileView>> tilePools)
        {
            foreach (var pair in tilePools)
            {
                var tilePool = pair.Value;
                for (var i = 0; i < tilePool.Count; i++)
                {
                    tilePool[i]?.SetActive(false);
                }
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

        private readonly struct BoardTileVisualDescriptor
        {
            private BoardTileVisualDescriptor(
                BoardTileVisualRole role,
                GameObject prefab,
                Material materialFallback,
                bool usesPrefab)
            {
                Role = role;
                Prefab = prefab;
                MaterialFallback = materialFallback;
                UsesPrefab = usesPrefab;
                PoolKey = new BoardTilePoolKey(role, prefab, materialFallback, usesPrefab);
            }

            public BoardTileVisualRole Role { get; }

            public GameObject Prefab { get; }

            public Material MaterialFallback { get; }

            public bool UsesPrefab { get; }

            public BoardTilePoolKey PoolKey { get; }

            public static BoardTileVisualDescriptor FromEntry(
                BoardTileVisualRole role,
                BoardTilePresentationCatalogEntry entry,
                Material legacyMaterial)
            {
                if (entry != null && entry.TilePrefab != null)
                {
                    return new BoardTileVisualDescriptor(
                        role,
                        entry.TilePrefab,
                        entry.MaterialFallback,
                        usesPrefab: true);
                }

                return MaterialOnly(role, entry?.MaterialFallback ?? legacyMaterial);
            }

            public static BoardTileVisualDescriptor MaterialOnly(
                BoardTileVisualRole role,
                Material materialFallback)
            {
                return new BoardTileVisualDescriptor(
                    role,
                    null,
                    materialFallback,
                    usesPrefab: false);
            }
        }

        private readonly struct BoardTilePoolKey : IEquatable<BoardTilePoolKey>
        {
            public BoardTilePoolKey(
                BoardTileVisualRole role,
                GameObject prefab,
                Material materialFallback,
                bool usesPrefab)
            {
                Role = role;
                PrefabInstanceId = prefab != null ? prefab.GetInstanceID() : 0;
                MaterialFallbackInstanceId = materialFallback != null ? materialFallback.GetInstanceID() : 0;
                UsesPrefab = usesPrefab;
            }

            public BoardTileVisualRole Role { get; }

            public int PrefabInstanceId { get; }

            public int MaterialFallbackInstanceId { get; }

            public bool UsesPrefab { get; }

            public bool Equals(BoardTilePoolKey other)
            {
                return Role == other.Role &&
                       PrefabInstanceId == other.PrefabInstanceId &&
                       MaterialFallbackInstanceId == other.MaterialFallbackInstanceId &&
                       UsesPrefab == other.UsesPrefab;
            }

            public override bool Equals(object obj)
            {
                return obj is BoardTilePoolKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (int)Role;
                    hashCode = (hashCode * 397) ^ PrefabInstanceId;
                    hashCode = (hashCode * 397) ^ MaterialFallbackInstanceId;
                    hashCode = (hashCode * 397) ^ UsesPrefab.GetHashCode();
                    return hashCode;
                }
            }
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
            public SurfaceTileView(
                GameObject gameObject,
                Transform transform,
                MeshRenderer renderer,
                BoardTileVisualDescriptor descriptor)
            {
                GameObject = gameObject;
                Transform = transform;
                Renderer = renderer;
                Descriptor = descriptor;
            }

            public GameObject GameObject { get; }

            public BoardTileVisualDescriptor Descriptor { get; }

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

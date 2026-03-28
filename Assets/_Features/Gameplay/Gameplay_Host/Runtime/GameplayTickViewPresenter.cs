using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplaySurfaceProjector
    {
        private readonly BoardBounds _boardBounds;
        private readonly float _cellSize;
        private readonly Vector3 _gridOrigin;

        public GameplaySurfaceProjector(
            BoardBounds boardBounds,
            Vector3 gridOrigin,
            float cellSize)
        {
            if (!boardBounds.IsBounded)
            {
                throw new InvalidOperationException("GameplaySurfaceProjector requires bounded board bounds.");
            }

            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            _boardBounds = boardBounds;
            _gridOrigin = gridOrigin;
            _cellSize = cellSize;
        }

        public BoardBounds BoardBounds => _boardBounds;

        public float CellSize => _cellSize;

        public Vector3 GridOrigin => _gridOrigin;

        public int Width => _boardBounds.MaxInclusive.x - _boardBounds.MinInclusive.x + 1;

        public int Height => _boardBounds.MaxInclusive.y - _boardBounds.MinInclusive.y + 1;

        public bool TryProject(
            SurfaceCell cell,
            CubeTopologyState topology,
            Vector3 continuityAnchor,
            out Vector3 worldPosition)
        {
            if (!_boardBounds.Contains(cell.PlanarPosition) || !topology.IsFaceActive(cell.face))
            {
                worldPosition = default;
                return false;
            }

            var localX = (cell.x - _boardBounds.MinInclusive.x) * _cellSize;
            var localY = (cell.y - _boardBounds.MinInclusive.y) * _cellSize;
            var faceOffsetY = ResolveFaceOffsetY(cell.face, topology);
            worldPosition = _gridOrigin + continuityAnchor + new Vector3(localX, localY + faceOffsetY, 0f);
            return true;
        }

        public Vector3 GetActiveStripCenter(Vector3 continuityAnchor)
        {
            return _gridOrigin +
                   continuityAnchor +
                   new Vector3(
                       (Width - 1) * _cellSize * 0.5f,
                       ((Height * 2) - 1) * _cellSize * 0.5f,
                       0f);
        }

        private float ResolveFaceOffsetY(FaceId face, CubeTopologyState topology)
        {
            if (face == topology.BottomFace)
            {
                return 0f;
            }

            if (face == topology.FrontFace)
            {
                return Height * _cellSize;
            }

            throw new InvalidOperationException(
                $"Cannot project inactive face {face}. Bottom={topology.BottomFace}, Front={topology.FrontFace}");
        }
    }

    public sealed class GameplayTickViewPresenter : MonoBehaviour
    {
        private readonly HashSet<int> _visibleEntityIds = new();

        private CubeTopologyState _currentTopology;
        private GameplaySurfaceProjector _projector;
        private GameplayEntityViewBinder _viewBinder;
        private bool _hasPresentedFrame;
        private bool _isInitialized;

        public event Action<Vector3> StripCenterChanged;

        public Vector3 ActiveStripCenter { get; private set; }

        public Vector3 ContinuityAnchor { get; private set; }

        public CubeTopologyState CurrentTopology => _currentTopology;

        public void Initialize(
            GameplayEntityViewBinder viewBinder,
            BoardBounds boardBounds,
            CubeTopologyState initialTopology,
            Vector3 gridOrigin,
            float cellSize)
        {
            if (viewBinder == null)
            {
                throw new ArgumentNullException(nameof(viewBinder));
            }

            _viewBinder = viewBinder;
            _projector = new GameplaySurfaceProjector(boardBounds, gridOrigin, cellSize);
            _currentTopology = initialTopology;
            ContinuityAnchor = Vector3.zero;
            ActiveStripCenter = _projector.GetActiveStripCenter(ContinuityAnchor);
            _hasPresentedFrame = false;
            _isInitialized = true;
        }

        public void Present(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            EnsureInitialized();
            PresentFrame(result.FinalEntities, result.FinalTopology, updateContinuity: true);
        }

        public void PresentInitial(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            EnsureInitialized();
            PresentFrame(entities, topology, updateContinuity: false);
        }

        private void PresentFrame(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology,
            bool updateContinuity)
        {
            if (updateContinuity && _hasPresentedFrame)
            {
                UpdateContinuityAnchor(_currentTopology, topology);
            }

            _currentTopology = topology;
            PresentEntities(entities, topology);

            ActiveStripCenter = _projector.GetActiveStripCenter(ContinuityAnchor);
            StripCenterChanged?.Invoke(ActiveStripCenter);
            _hasPresentedFrame = true;
        }

        private void PresentEntities(IReadOnlyList<EntityState> entities, CubeTopologyState topology)
        {
            _visibleEntityIds.Clear();

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (!ShouldPresent(entity, topology) ||
                    !_projector.TryProject(entity.position, topology, ContinuityAnchor, out var worldPosition))
                {
                    continue;
                }

                var view = _viewBinder.ResolveOrCreate(entity);
                if (view == null)
                {
                    continue;
                }

                _visibleEntityIds.Add(entity.entityId);
                view.SetVisible(true);
                view.ApplyPose(worldPosition);
            }

            _viewBinder.HideViewsExcept(_visibleEntityIds);
        }

        private void UpdateContinuityAnchor(CubeTopologyState previousTopology, CubeTopologyState currentTopology)
        {
            var stepDistance = Vector3.up * (_projector.Height * _projector.CellSize);

            if (FaceIdUtility.GetNext(previousTopology.BottomFace) == currentTopology.BottomFace)
            {
                ContinuityAnchor += stepDistance;
                return;
            }

            if (FaceIdUtility.GetPrevious(previousTopology.BottomFace) == currentTopology.BottomFace)
            {
                ContinuityAnchor -= stepDistance;
            }
        }

        private static bool ShouldPresent(EntityState entity, CubeTopologyState topology)
        {
            return entity.boardPresence == EntityBoardPresence.Occupying &&
                   topology.IsFaceActive(entity.position.face);
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("GameplayTickViewPresenter must be initialized before use.");
            }
        }
    }
}

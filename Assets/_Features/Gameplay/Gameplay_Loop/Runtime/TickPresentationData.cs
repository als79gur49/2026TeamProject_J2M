using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    public enum TickEntityMotionKind
    {
        None = 0,
        Move = 1,
        Push = 2,
        Flip = 3,
        ProjectileMove = 4,
        BoxSlide = 5,
    }

    public enum TickVisibilityChangeKind
    {
        None = 0,
        Spawn = 1,
        Detach = 2,
        Remove = 3,
    }

    public enum TickTransitionVisibilityMode
    {
        None = 0,
        RetainUntilTransitionComplete = 1,
        ShowAtTransitionStart = 2,
    }

    public readonly struct TickEntityMotion
    {
        // Motion records describe a render transition between already-committed logical cells.
        public TickEntityMotion(
            int entityId,
            TickEntityMotionKind motionKind,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell)
            : this(
                entityId,
                motionKind,
                sourceCell,
                destinationCell,
                sourceTopology: null,
                destinationTopology: null,
                sourceFacing: null,
                destinationFacing: null)
        {
        }

        public TickEntityMotion(
            int entityId,
            TickEntityMotionKind motionKind,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            CubeTopologyState? sourceTopology,
            CubeTopologyState? destinationTopology,
            Direction? sourceFacing,
            Direction? destinationFacing)
        {
            EntityId = entityId;
            MotionKind = motionKind;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            SourceFacing = sourceFacing;
            DestinationFacing = destinationFacing;
        }

        public int EntityId { get; }

        public TickEntityMotionKind MotionKind { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public CubeTopologyState? SourceTopology { get; }

        public CubeTopologyState? DestinationTopology { get; }

        public Direction? SourceFacing { get; }

        public Direction? DestinationFacing { get; }
    }

    public readonly struct TickTopologyMotion
    {
        public TickTopologyMotion(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind)
        {
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            RotationKind = rotationKind;
        }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState DestinationTopology { get; }

        public CubeRotationKind RotationKind { get; }
    }

    public readonly struct TickVisibilityChange
    {
        public TickVisibilityChange(
            int entityId,
            TickVisibilityChangeKind changeKind,
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing)
        {
            EntityId = entityId;
            ChangeKind = changeKind;
            Cell = cell;
            Topology = topology;
            Facing = facing;
        }

        public int EntityId { get; }

        public TickVisibilityChangeKind ChangeKind { get; }

        public SurfaceCell Cell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }
    }

    public readonly struct TickTransitionVisibilityChange
    {
        public TickTransitionVisibilityChange(
            int entityId,
            TickTransitionVisibilityMode mode,
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing)
        {
            EntityId = entityId;
            Mode = mode;
            Cell = cell;
            Topology = topology;
            Facing = facing;
        }

        public int EntityId { get; }

        public TickTransitionVisibilityMode Mode { get; }

        public SurfaceCell Cell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }
    }

    public sealed class TickPresentationData
    {
        public static readonly TickPresentationData Empty = new(
            Array.Empty<TickEntityMotion>(),
            topologyMotion: null,
            Array.Empty<TickVisibilityChange>(),
            Array.Empty<TickTransitionVisibilityChange>());

        private readonly ReadOnlyCollection<TickEntityMotion> _entityMotions;
        private readonly TickTopologyMotion? _topologyMotion;
        private readonly ReadOnlyCollection<TickTransitionVisibilityChange> _transitionVisibilityChanges;
        private readonly ReadOnlyCollection<TickVisibilityChange> _visibilityChanges;

        // Presentation data is render-only metadata layered on top of authoritative gameplay state.
        public TickPresentationData(IEnumerable<TickEntityMotion> entityMotions)
            : this(
                entityMotions,
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                Array.Empty<TickTransitionVisibilityChange>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges)
        {
            if (entityMotions == null)
            {
                throw new ArgumentNullException(nameof(entityMotions));
            }

            if (visibilityChanges == null)
            {
                throw new ArgumentNullException(nameof(visibilityChanges));
            }

            if (transitionVisibilityChanges == null)
            {
                throw new ArgumentNullException(nameof(transitionVisibilityChanges));
            }

            _entityMotions = new ReadOnlyCollection<TickEntityMotion>(new List<TickEntityMotion>(entityMotions));
            _topologyMotion = topologyMotion;
            _visibilityChanges = new ReadOnlyCollection<TickVisibilityChange>(new List<TickVisibilityChange>(visibilityChanges));
            _transitionVisibilityChanges = new ReadOnlyCollection<TickTransitionVisibilityChange>(
                new List<TickTransitionVisibilityChange>(transitionVisibilityChanges));
        }

        public IReadOnlyList<TickEntityMotion> EntityMotions => _entityMotions;

        public TickTopologyMotion? TopologyMotion => _topologyMotion;

        public IReadOnlyList<TickVisibilityChange> VisibilityChanges => _visibilityChanges;

        public IReadOnlyList<TickTransitionVisibilityChange> TransitionVisibilityChanges => _transitionVisibilityChanges;
    }
}

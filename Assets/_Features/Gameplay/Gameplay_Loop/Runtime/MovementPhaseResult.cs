using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;

namespace Game.Feature.Gameplay.Loop
{
    internal readonly struct FrontFaceShieldSourcePresentationExport
    {
        public FrontFaceShieldSourcePresentationExport(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int radius,
            bool includeSourceCell,
            FrontFaceShieldTargetPattern targetPattern,
            int tickIndex,
            int presentationSeed)
        {
            SourceEntityId = sourceEntityId;
            SourceCell = sourceCell;
            Topology = topology;
            Radius = radius;
            IncludeSourceCell = includeSourceCell;
            TargetPattern = targetPattern;
            TickIndex = tickIndex;
            PresentationSeed = presentationSeed;
        }

        public int SourceEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState Topology { get; }

        public int Radius { get; }

        public bool IncludeSourceCell { get; }

        public FrontFaceShieldTargetPattern TargetPattern { get; }

        public int TickIndex { get; }

        public int PresentationSeed { get; }
    }

    internal readonly struct FrontFaceShieldBlockPresentationExport
    {
        public FrontFaceShieldBlockPresentationExport(
            int shieldSourceEntityId,
            int boxEntityId,
            int actorEntityId,
            SurfaceCell blockedCell,
            SurfaceCell shieldSourceCell,
            FrontFaceShieldBlockMovementKind movementKind,
            CubeTopologyState topology,
            int tickIndex,
            int presentationSeed)
        {
            ShieldSourceEntityId = shieldSourceEntityId;
            BoxEntityId = boxEntityId;
            ActorEntityId = actorEntityId;
            BlockedCell = blockedCell;
            ShieldSourceCell = shieldSourceCell;
            MovementKind = movementKind;
            Topology = topology;
            TickIndex = tickIndex;
            PresentationSeed = presentationSeed;
        }

        public int ShieldSourceEntityId { get; }

        public int BoxEntityId { get; }

        public int ActorEntityId { get; }

        public SurfaceCell BlockedCell { get; }

        public SurfaceCell ShieldSourceCell { get; }

        public FrontFaceShieldBlockMovementKind MovementKind { get; }

        public CubeTopologyState Topology { get; }

        public int TickIndex { get; }

        public int PresentationSeed { get; }
    }

    internal readonly struct BarricadeBlockFact
    {
        public BarricadeBlockFact(
            int tileId,
            SurfaceCell cell,
            int boxEntityId,
            Direction attemptedDirection)
        {
            TileId = tileId;
            Cell = cell;
            BoxEntityId = boxEntityId;
            AttemptedDirection = attemptedDirection;
        }

        public int TileId { get; }

        public SurfaceCell Cell { get; }

        public int BoxEntityId { get; }

        public Direction AttemptedDirection { get; }
    }

    public enum BoxSlideStopperKind
    {
        None = 0,
        SolidEntity = 1,
        Terrain = 2,
        BoardEdge = 3,
        Shield = 4,
    }

    public enum BoxSlideStopCause
    {
        None = 0,
        SlidingContinuationBlocked = 1,
    }

    internal readonly struct BoxSlideStopResult
    {
        public BoxSlideStopResult(
            int intentId,
            int boxEntityId,
            SurfaceCell sourceCell,
            SurfaceCell stopperCell,
            Direction slideDirection,
            BoxSlideStopperKind stopperKind,
            int stopperEntityId,
            SolidKind solidKind,
            CubeTopologyState topology,
            BoxSlideStopCause cause)
        {
            IntentId = intentId;
            BoxEntityId = boxEntityId;
            SourceCell = sourceCell;
            StopperCell = stopperCell;
            SlideDirection = slideDirection;
            StopperKind = stopperKind;
            StopperEntityId = stopperEntityId;
            SolidKind = solidKind;
            Topology = topology;
            Cause = cause;
        }

        internal int IntentId { get; }

        public int BoxEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell StopperCell { get; }

        public Direction SlideDirection { get; }

        public BoxSlideStopperKind StopperKind { get; }

        public int StopperEntityId { get; }

        public SolidKind SolidKind { get; }

        public CubeTopologyState Topology { get; }

        public BoxSlideStopCause Cause { get; }
    }

    internal sealed class MovementPhaseResult
    {
        public static readonly MovementPhaseResult Empty = new(
            Array.Empty<RawMovementIntent>(),
            Array.Empty<MoveIntent>(),
            Array.Empty<ResolutionRecord>(),
            Array.Empty<ImpactDispositionResolutionRecord>(),
            Array.Empty<FinalizationOperation>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<FrontFaceShieldSourcePresentationExport>(),
            Array.Empty<FrontFaceShieldBlockPresentationExport>(),
            Array.Empty<BarricadeBlockFact>(),
            Array.Empty<BoxSlideStopResult>(),
            Array.Empty<string>());

        private readonly ReadOnlyCollection<BarricadeBlockFact> _barricadeBlockFacts;
        private readonly ReadOnlyCollection<BoxSlideStopResult> _boxSlideStops;
        private readonly ReadOnlyCollection<string> _commitEvents;
        private readonly ReadOnlyCollection<string> _debugEvents;
        private readonly ReadOnlyCollection<FrontFaceShieldBlockPresentationExport> _frontFaceShieldBlockExports;
        private readonly ReadOnlyCollection<FrontFaceShieldSourcePresentationExport> _frontFaceShieldSourceExports;
        private readonly ReadOnlyCollection<ImpactDispositionResolutionRecord> _impactDispositionRecords;
        private readonly ReadOnlyCollection<RawMovementIntent> _rawIntents;
        private readonly ReadOnlyCollection<string> _rejectedReasons;
        private readonly ReadOnlyCollection<ResolutionRecord> _resolutionRecords;
        private readonly ReadOnlyCollection<FinalizationOperation> _resolvedOperations;
        private readonly ReadOnlyCollection<MoveIntent> _sortedIntents;

        public MovementPhaseResult(
            IEnumerable<RawMovementIntent> rawIntents,
            IEnumerable<MoveIntent> sortedIntents,
            IEnumerable<ResolutionRecord> resolutionRecords,
            IEnumerable<ImpactDispositionResolutionRecord> impactDispositionRecords,
            IEnumerable<FinalizationOperation> resolvedOperations,
            IEnumerable<string> commitEvents,
            IEnumerable<string> rejectedReasons,
            IEnumerable<FrontFaceShieldSourcePresentationExport> frontFaceShieldSourceExports = null,
            IEnumerable<FrontFaceShieldBlockPresentationExport> frontFaceShieldBlockExports = null,
            IEnumerable<BarricadeBlockFact> barricadeBlockFacts = null,
            IEnumerable<BoxSlideStopResult> boxSlideStops = null,
            IEnumerable<string> debugEvents = null)
        {
            if (rawIntents == null)
            {
                throw new ArgumentNullException(nameof(rawIntents));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
            }

            if (resolutionRecords == null)
            {
                throw new ArgumentNullException(nameof(resolutionRecords));
            }

            if (resolvedOperations == null)
            {
                throw new ArgumentNullException(nameof(resolvedOperations));
            }

            if (impactDispositionRecords == null)
            {
                throw new ArgumentNullException(nameof(impactDispositionRecords));
            }

            if (commitEvents == null)
            {
                throw new ArgumentNullException(nameof(commitEvents));
            }

            if (rejectedReasons == null)
            {
                throw new ArgumentNullException(nameof(rejectedReasons));
            }

            _rawIntents = new ReadOnlyCollection<RawMovementIntent>(new List<RawMovementIntent>(rawIntents));
            _sortedIntents = new ReadOnlyCollection<MoveIntent>(new List<MoveIntent>(sortedIntents));
            _resolutionRecords = new ReadOnlyCollection<ResolutionRecord>(new List<ResolutionRecord>(resolutionRecords));
            _impactDispositionRecords = new ReadOnlyCollection<ImpactDispositionResolutionRecord>(
                new List<ImpactDispositionResolutionRecord>(impactDispositionRecords));
            _resolvedOperations = new ReadOnlyCollection<FinalizationOperation>(new List<FinalizationOperation>(resolvedOperations));
            _commitEvents = new ReadOnlyCollection<string>(new List<string>(commitEvents));
            _rejectedReasons = new ReadOnlyCollection<string>(new List<string>(rejectedReasons));
            _debugEvents = new ReadOnlyCollection<string>(
                new List<string>(debugEvents ?? Array.Empty<string>()));
            _frontFaceShieldSourceExports = new ReadOnlyCollection<FrontFaceShieldSourcePresentationExport>(
                new List<FrontFaceShieldSourcePresentationExport>(
                    frontFaceShieldSourceExports ?? Array.Empty<FrontFaceShieldSourcePresentationExport>()));
            _frontFaceShieldBlockExports = new ReadOnlyCollection<FrontFaceShieldBlockPresentationExport>(
                new List<FrontFaceShieldBlockPresentationExport>(
                    frontFaceShieldBlockExports ?? Array.Empty<FrontFaceShieldBlockPresentationExport>()));
            _barricadeBlockFacts = new ReadOnlyCollection<BarricadeBlockFact>(
                new List<BarricadeBlockFact>(
                    barricadeBlockFacts ?? Array.Empty<BarricadeBlockFact>()));
            _boxSlideStops = new ReadOnlyCollection<BoxSlideStopResult>(
                new List<BoxSlideStopResult>(
                    boxSlideStops ?? Array.Empty<BoxSlideStopResult>()));
        }

        public IReadOnlyList<RawMovementIntent> RawIntents => _rawIntents;

        public IReadOnlyList<MoveIntent> SortedIntents => _sortedIntents;

        public IReadOnlyList<ResolutionRecord> ResolutionRecords => _resolutionRecords;

        internal IReadOnlyList<ImpactDispositionResolutionRecord> ImpactDispositionRecords => _impactDispositionRecords;

        // Includes accepted impact follow-through movement-visible writes such as
        // local vacate, box move, and contingent facing/state updates.
        public IReadOnlyList<FinalizationOperation> ResolvedOperations => _resolvedOperations;

        public IReadOnlyList<string> CommitEvents => _commitEvents;

        public IReadOnlyList<string> RejectedReasons => _rejectedReasons;

        public IReadOnlyList<string> DebugEvents => _debugEvents;

        internal IReadOnlyList<FrontFaceShieldSourcePresentationExport> FrontFaceShieldSourceExports =>
            _frontFaceShieldSourceExports;

        internal IReadOnlyList<FrontFaceShieldBlockPresentationExport> FrontFaceShieldBlockExports =>
            _frontFaceShieldBlockExports;

        internal IReadOnlyList<BarricadeBlockFact> BarricadeBlockFacts => _barricadeBlockFacts;

        internal IReadOnlyList<BoxSlideStopResult> BoxSlideStops => _boxSlideStops;
    }
}

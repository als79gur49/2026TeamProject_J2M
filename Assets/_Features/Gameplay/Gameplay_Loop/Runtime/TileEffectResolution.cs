using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Phases;

namespace Game.Feature.Gameplay.Loop
{
    internal enum TileEffectBoxContactKind
    {
        SlideEnter = 0,
        PushEnter = 1,
        FlipLanding = 2,
        ImpactFollowThrough = 3,
    }

    internal readonly struct TileEffectBoxContact
    {
        public TileEffectBoxContact(
            int boxEntityId,
            SurfaceCell cell,
            TileEffectBoxContactKind kind)
        {
            BoxEntityId = boxEntityId;
            Cell = cell;
            Kind = kind;
        }

        public int BoxEntityId { get; }

        public SurfaceCell Cell { get; }

        public TileEffectBoxContactKind Kind { get; }
    }

    internal enum TileEffectBoxMovementFamily
    {
        None = 0,
        Push = 1,
        Slide = 2,
    }

    internal readonly struct TileEffectBoxStop
    {
        public TileEffectBoxStop(
            int boxEntityId,
            SurfaceCell cell,
            TileEffectBoxMovementFamily movementFamily)
        {
            BoxEntityId = boxEntityId;
            Cell = cell;
            MovementFamily = movementFamily;
        }

        public int BoxEntityId { get; }

        public SurfaceCell Cell { get; }

        public TileEffectBoxMovementFamily MovementFamily { get; }
    }

    internal readonly struct TileEffectResolutionContext
    {
        public TileEffectResolutionContext(
            int tickIndex,
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            IReadOnlyList<TileEffectBoxContact> boxContacts = null,
            WorldSnapshot previousSnapshot = null,
            IReadOnlyList<TileEffectBoxStop> boxStops = null)
        {
            TickIndex = tickIndex;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            TileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
            BoxContacts = boxContacts ?? Array.Empty<TileEffectBoxContact>();
            PreviousSnapshot = previousSnapshot;
            BoxStops = boxStops ?? Array.Empty<TileEffectBoxStop>();
        }

        public int TickIndex { get; }

        public WorldSnapshot Snapshot { get; }

        public IReadOnlyList<TileFeatureRuntimeDefinition> TileFeatureDefinitions { get; }

        public IReadOnlyList<TileEffectBoxContact> BoxContacts { get; }

        public IReadOnlyList<TileEffectBoxStop> BoxStops { get; }

        public WorldSnapshot PreviousSnapshot { get; }
    }

    internal readonly struct TileEffectResolutionResult
    {
        private readonly TileFeatureOperationBatch _operations;
        private readonly FinalizationBatch _entityOperations;
        private readonly IReadOnlyList<TilePresentationEvent> _tileEvents;

        public TileEffectResolutionResult(
            TileFeatureOperationBatch operations,
            FinalizationBatch entityOperations = null,
            IReadOnlyList<TilePresentationEvent> tileEvents = null)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            _entityOperations = entityOperations;
            _tileEvents = tileEvents ?? Array.Empty<TilePresentationEvent>();
        }

        public static TileEffectResolutionResult Empty => new(new TileFeatureOperationBatch());

        public TileFeatureOperationBatch Operations => _operations ?? new TileFeatureOperationBatch();

        public FinalizationBatch EntityOperations => _entityOperations ?? new FinalizationBatch();

        public IReadOnlyList<TilePresentationEvent> TileEvents => _tileEvents ?? Array.Empty<TilePresentationEvent>();

        public bool IsEmpty => Operations.IsEmpty && EntityOperations.Operations.Count == 0 && TileEvents.Count == 0;
    }

    internal interface ITileEffectResolver
    {
        TileEffectResolutionResult Resolve(in TileEffectResolutionContext context);
    }

    internal sealed class EmptyTileEffectResolver : ITileEffectResolver
    {
        public static readonly EmptyTileEffectResolver Instance = new();

        private EmptyTileEffectResolver()
        {
        }

        public TileEffectResolutionResult Resolve(in TileEffectResolutionContext context)
        {
            return TileEffectResolutionResult.Empty;
        }
    }

    internal sealed class TileFeatureEffectResolver : ITileEffectResolver
    {
        public static readonly TileFeatureEffectResolver Instance = new();

        private TileFeatureEffectResolver()
        {
        }

        public TileEffectResolutionResult Resolve(in TileEffectResolutionContext context)
        {
            var tileFeatures = new List<TileFeatureState>();
            context.Snapshot.EnumerateTileFeaturesOrdered(tileFeatures);

            var entityOperations = ResolveDestroyTiles(context, out var tileEvents, out var destroyedBoxIds);
            entityOperations.MergeFrom(ResolveBarricadeCrushes(context, destroyedBoxIds, tileEvents));
            entityOperations.MergeFrom(ResolveSlideTiles(context, destroyedBoxIds, tileEvents));

            TileFeatureOperationBatch operations = null;
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileFeature = tileFeatures[i];
                if (!ShouldLatchButton(context, tileFeature, destroyedBoxIds))
                {
                    continue;
                }

                operations ??= new TileFeatureOperationBatch();
                operations.Add(TileFeatureOperation.Update(CreateActivatedState(tileFeature)));
            }

            return (operations == null || operations.IsEmpty) &&
                   entityOperations.Operations.Count == 0 &&
                   tileEvents.Count == 0
                ? TileEffectResolutionResult.Empty
                : new TileEffectResolutionResult(
                    operations ?? new TileFeatureOperationBatch(),
                    entityOperations,
                    tileEvents);
        }

        private static bool ShouldLatchButton(
            in TileEffectResolutionContext context,
            TileFeatureState tileFeature,
            HashSet<int> excludedBoxIds)
        {
            if (tileFeature.Kind != TileFeatureKind.Button ||
                (tileFeature.Flags & TileFeatureFlags.Activated) != 0)
            {
                return false;
            }

            if (!TryFindDefinition(context.TileFeatureDefinitions, tileFeature.TileId, out var definition) ||
                !TileFeatureActivationQueries.IsActive(tileFeature, definition, context.Snapshot.Topology))
            {
                return false;
            }

            return TryGetAcceptedPushSlideStoppedBox(
                context.Snapshot,
                context.BoxStops,
                tileFeature.Cell,
                definition.BoxSelector,
                excludedBoxIds,
                out _);
        }

        private static FinalizationBatch ResolveDestroyTiles(
            in TileEffectResolutionContext context,
            out List<TilePresentationEvent> tileEvents,
            out HashSet<int> destroyedBoxIds)
        {
            tileEvents = new List<TilePresentationEvent>();
            var batch = new FinalizationBatch();
            destroyedBoxIds = new HashSet<int>();
            if (context.BoxContacts.Count == 0)
            {
                return batch;
            }

            var orderedContacts = new List<TileEffectBoxContact>(context.BoxContacts);
            orderedContacts.Sort(CompareTileEffectBoxContacts);
            var tileFeaturesAtCell = new List<TileFeatureState>();

            for (var i = 0; i < orderedContacts.Count; i++)
            {
                var contact = orderedContacts[i];
                if (contact.BoxEntityId <= 0 ||
                    destroyedBoxIds.Contains(contact.BoxEntityId))
                {
                    continue;
                }

                context.Snapshot.EnumerateTileFeaturesAt(contact.Cell, tileFeaturesAtCell);
                for (var tileIndex = 0; tileIndex < tileFeaturesAtCell.Count; tileIndex++)
                {
                    var tileFeature = tileFeaturesAtCell[tileIndex];
                    if (tileFeature.Kind != TileFeatureKind.Destroy ||
                        !TryFindDefinition(context.TileFeatureDefinitions, tileFeature.TileId, out var definition) ||
                        !TileFeatureActivationQueries.IsActive(tileFeature, definition, context.Snapshot.Topology) ||
                        !TryGetValidDestroyTarget(context.Snapshot, contact.BoxEntityId, contact.Cell, out _))
                    {
                        continue;
                    }

                    batch.SetBoardPresence(
                        contact.BoxEntityId,
                        EntityBoardPresence.Detached,
                        CreateDestroyTileMetadata(context.TickIndex, contact.BoxEntityId, contact.Cell));
                    batch.MarkDestroy(
                        contact.BoxEntityId,
                        CreateDestroyTileMetadata(context.TickIndex, contact.BoxEntityId, contact.Cell));
                    tileEvents.Add(new TilePresentationEvent(
                        TilePresentationEventKind.DestroyTileTriggered,
                        tileFeature.TileId,
                        tileFeature.Cell,
                        tileFeature.Kind,
                        tileFeature.SourceEntityId,
                        tileFeature.OwnerEntityId,
                        tileFeature.TeamId,
                        targetEntityId: contact.BoxEntityId));
                    destroyedBoxIds.Add(contact.BoxEntityId);
                    break;
                }
            }

            return batch;
        }

        private static FinalizationBatch ResolveBarricadeCrushes(
            in TileEffectResolutionContext context,
            HashSet<int> destroyedBoxIds,
            List<TilePresentationEvent> tileEvents)
        {
            var batch = new FinalizationBatch();
            if (context.PreviousSnapshot == null)
            {
                return batch;
            }

            var tileFeatures = new List<TileFeatureState>();
            context.Snapshot.EnumerateTileFeaturesOrdered(tileFeatures);
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileFeature = tileFeatures[i];
                if (tileFeature.Kind != TileFeatureKind.Barricade ||
                    !TryFindDefinition(context.TileFeatureDefinitions, tileFeature.TileId, out var definition) ||
                    !IsBarricadeActivationTransition(context.PreviousSnapshot, context.Snapshot, tileFeature, definition) ||
                    !TryGetValidOccupyingBox(context.Snapshot, tileFeature.Cell, out var box) ||
                    (destroyedBoxIds != null && destroyedBoxIds.Contains(box.entityId)))
                {
                    continue;
                }

                var metadata = CreateBarricadeCrushMetadata(context.TickIndex, box.entityId, tileFeature.Cell);
                batch.SetBoardPresence(box.entityId, EntityBoardPresence.Detached, metadata);
                batch.MarkDestroy(box.entityId, metadata);
                destroyedBoxIds?.Add(box.entityId);
                tileEvents?.Add(new TilePresentationEvent(
                    TilePresentationEventKind.BarricadeCrushed,
                    tileFeature.TileId,
                    tileFeature.Cell,
                    tileFeature.Kind,
                    tileFeature.SourceEntityId,
                    tileFeature.OwnerEntityId,
                    tileFeature.TeamId,
                    targetEntityId: box.entityId));
            }

            return batch;
        }

        private static bool IsBarricadeActivationTransition(
            WorldSnapshot previousSnapshot,
            WorldSnapshot currentSnapshot,
            TileFeatureState currentTileFeature,
            TileFeatureRuntimeDefinition definition)
        {
            if (!previousSnapshot.TryGetTileFeature(currentTileFeature.TileId, out var previousTileFeature) ||
                previousTileFeature.Kind != TileFeatureKind.Barricade ||
                previousTileFeature.Cell != currentTileFeature.Cell)
            {
                return false;
            }

            return !TileFeatureActivationQueries.IsActive(previousTileFeature, definition, previousSnapshot.Topology) &&
                   TileFeatureActivationQueries.IsActive(currentTileFeature, definition, currentSnapshot.Topology);
        }

        private static FinalizationBatch ResolveSlideTiles(
            in TileEffectResolutionContext context,
            HashSet<int> destroyedBoxIds,
            List<TilePresentationEvent> tileEvents)
        {
            var batch = new FinalizationBatch();
            if (context.BoxContacts.Count == 0)
            {
                return batch;
            }

            var redirectedBoxIds = new HashSet<int>();
            var orderedContacts = new List<TileEffectBoxContact>(context.BoxContacts);
            orderedContacts.Sort(CompareTileEffectBoxContacts);
            var tileFeaturesAtCell = new List<TileFeatureState>();

            for (var i = 0; i < orderedContacts.Count; i++)
            {
                var contact = orderedContacts[i];
                if (!IsSlideRedirectContact(contact.Kind) ||
                    contact.BoxEntityId <= 0 ||
                    (destroyedBoxIds != null && destroyedBoxIds.Contains(contact.BoxEntityId)) ||
                    redirectedBoxIds.Contains(contact.BoxEntityId) ||
                    !TryGetValidSlideTarget(context.Snapshot, contact.BoxEntityId, contact.Cell, out var box))
                {
                    continue;
                }

                context.Snapshot.EnumerateTileFeaturesAt(contact.Cell, tileFeaturesAtCell);
                for (var tileIndex = 0; tileIndex < tileFeaturesAtCell.Count; tileIndex++)
                {
                    var tileFeature = tileFeaturesAtCell[tileIndex];
                    if (tileFeature.Kind != TileFeatureKind.Slide ||
                        !TryFindDefinition(context.TileFeatureDefinitions, tileFeature.TileId, out var definition) ||
                        !TileFeatureActivationQueries.IsActive(tileFeature, definition, context.Snapshot.Topology) ||
                        !TryResolveDirection(definition.Direction, out var redirectDirection))
                    {
                        continue;
                    }

                    if (box.facing != redirectDirection)
                    {
                        batch.SetFacing(
                            contact.BoxEntityId,
                            redirectDirection,
                            CreateSlideTileRedirectMetadata(context.TickIndex, contact.BoxEntityId, contact.Cell));
                        tileEvents.Add(new TilePresentationEvent(
                            TilePresentationEventKind.SlideTileRedirected,
                            tileFeature.TileId,
                            tileFeature.Cell,
                            tileFeature.Kind,
                            tileFeature.SourceEntityId,
                            tileFeature.OwnerEntityId,
                            tileFeature.TeamId,
                            targetEntityId: contact.BoxEntityId,
                            direction: redirectDirection));
                    }

                    redirectedBoxIds.Add(contact.BoxEntityId);
                    break;
                }
            }

            return batch;
        }

        private static int CompareTileEffectBoxContacts(TileEffectBoxContact left, TileEffectBoxContact right)
        {
            var cellCompare = CompareSurfaceCells(left.Cell, right.Cell);
            if (cellCompare != 0)
            {
                return cellCompare;
            }

            var boxCompare = left.BoxEntityId.CompareTo(right.BoxEntityId);
            if (boxCompare != 0)
            {
                return boxCompare;
            }

            return left.Kind.CompareTo(right.Kind);
        }

        private static int CompareSurfaceCells(SurfaceCell left, SurfaceCell right)
        {
            var faceCompare = left.face.CompareTo(right.face);
            if (faceCompare != 0)
            {
                return faceCompare;
            }

            var xCompare = left.x.CompareTo(right.x);
            return xCompare != 0 ? xCompare : left.y.CompareTo(right.y);
        }

        private static bool TryGetValidDestroyTarget(
            WorldSnapshot snapshot,
            int boxEntityId,
            SurfaceCell contactCell,
            out EntityState box)
        {
            if (snapshot.TryGetEntity(boxEntityId, out box) &&
                box.type == EntityType.Box &&
                box.position == contactCell &&
                box.boardPresence == EntityBoardPresence.Occupying &&
                box.hp > 0 &&
                !box.markedForDeath)
            {
                return true;
            }

            box = default;
            return false;
        }

        private static bool TryGetValidSlideTarget(
            WorldSnapshot snapshot,
            int boxEntityId,
            SurfaceCell contactCell,
            out EntityState box)
        {
            if (TryGetValidDestroyTarget(snapshot, boxEntityId, contactCell, out box) &&
                box.state == EntityPhaseState.Sliding)
            {
                return true;
            }

            box = default;
            return false;
        }

        private static bool IsSlideRedirectContact(TileEffectBoxContactKind kind)
        {
            return kind == TileEffectBoxContactKind.PushEnter ||
                   kind == TileEffectBoxContactKind.SlideEnter;
        }

        private static bool TryResolveDirection(Direction2D direction, out Direction resolved)
        {
            switch (direction)
            {
                case Direction2D.Up:
                    resolved = Direction.Up;
                    return true;

                case Direction2D.Right:
                    resolved = Direction.Right;
                    return true;

                case Direction2D.Down:
                    resolved = Direction.Down;
                    return true;

                case Direction2D.Left:
                    resolved = Direction.Left;
                    return true;

                default:
                    resolved = Direction.None;
                    return false;
            }
        }

        private static FinalizationOperationMetadata CreateDestroyTileMetadata(
            int tickIndex,
            int boxEntityId,
            SurfaceCell contactCell)
        {
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.None,
                sourceActorEntityId: boxEntityId,
                actionPlanId: tickIndex,
                exitCauseHint: TickEntityExitCause.BoxDestroy,
                damageSourceType: DamageSourceType.Environmental,
                presentationTargetCell: contactCell,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.ScriptedRelocation,
                boundaryReason: "DestroyTile",
                exitPresentationTiming: EntityExitPresentationTiming.AfterEntityMotion,
                hasPresentationTargetCell: true);
        }

        private static FinalizationOperationMetadata CreateBarricadeCrushMetadata(
            int tickIndex,
            int boxEntityId,
            SurfaceCell contactCell)
        {
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.None,
                sourceActorEntityId: boxEntityId,
                actionPlanId: tickIndex,
                exitCauseHint: TickEntityExitCause.BoxDestroy,
                damageSourceType: DamageSourceType.Environmental,
                presentationTargetCell: contactCell,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.ScriptedRelocation,
                boundaryReason: "BarricadeCrush",
                hasPresentationTargetCell: true);
        }

        private static FinalizationOperationMetadata CreateSlideTileRedirectMetadata(
            int tickIndex,
            int boxEntityId,
            SurfaceCell contactCell)
        {
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.Slide,
                sourceActorEntityId: boxEntityId,
                actionPlanId: tickIndex,
                movementSemanticKind: MovementSemanticKind.Slide,
                presentationTargetCell: contactCell,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.ScriptedRelocation,
                boundaryReason: "SlideTileRedirect",
                hasPresentationTargetCell: true);
        }

        private static bool TryFindDefinition(
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            int tileId,
            out TileFeatureRuntimeDefinition definition)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].TileId == tileId)
                {
                    definition = definitions[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        private static bool TryGetAcceptedPushSlideStoppedBox(
            WorldSnapshot snapshot,
            IReadOnlyList<TileEffectBoxStop> stops,
            SurfaceCell buttonCell,
            TileFeatureBoxSelector selector,
            HashSet<int> excludedBoxIds,
            out EntityState acceptedBox)
        {
            acceptedBox = default;
            if (stops == null || stops.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < stops.Count; i++)
            {
                var stop = stops[i];
                if (stop.Cell != buttonCell ||
                    (excludedBoxIds != null && excludedBoxIds.Contains(stop.BoxEntityId)) ||
                    (stop.MovementFamily != TileEffectBoxMovementFamily.Push &&
                     stop.MovementFamily != TileEffectBoxMovementFamily.Slide) ||
                    !TryGetValidStoppedBox(snapshot, stop.BoxEntityId, buttonCell, out var box) ||
                    !MatchesButtonBoxSelector(box, selector))
                {
                    continue;
                }

                acceptedBox = box;
                return true;
            }

            return false;
        }

        private static bool MatchesButtonBoxSelector(
            EntityState box,
            TileFeatureBoxSelector selector)
        {
            switch (selector)
            {
                case TileFeatureBoxSelector.AnyPushableBox:
                    return (box.boxCapabilities & BoxCapabilities.Push) != 0;

                case TileFeatureBoxSelector.MoonBlockOnly:
                    return box.boxArchetype == BoxArchetype.Moon;

                case TileFeatureBoxSelector.None:
                case TileFeatureBoxSelector.FeatureCell:
                case TileFeatureBoxSelector.BoundEntity:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(selector), selector, "Unknown TileFeature box selector.");
            }
        }

        private static bool TryGetValidStoppedBox(
            WorldSnapshot snapshot,
            int boxEntityId,
            SurfaceCell expectedCell,
            out EntityState box)
        {
            if (snapshot.TryGetEntity(boxEntityId, out var entity) &&
                entity.type == EntityType.Box &&
                entity.position == expectedCell &&
                entity.boardPresence == EntityBoardPresence.Occupying &&
                entity.hp > 0 &&
                !entity.markedForDeath &&
                entity.state == EntityPhaseState.Idle &&
                snapshot.TryGetSolidSemanticAt(expectedCell, out var semantic) &&
                semantic.Kind == SolidKind.Box &&
                semantic.Entity.entityId == boxEntityId)
            {
                box = entity;
                return true;
            }

            box = default;
            return false;
        }

        private static bool TryGetValidOccupyingBox(WorldSnapshot snapshot, SurfaceCell cell, out EntityState box)
        {
            if (snapshot.TryGetSolidSemanticAt(cell, out var semantic) &&
                semantic.Kind == SolidKind.Box &&
                semantic.Entity.type == EntityType.Box &&
                semantic.Entity.boardPresence == EntityBoardPresence.Occupying &&
                semantic.Entity.hp > 0 &&
                !semantic.Entity.markedForDeath)
            {
                box = semantic.Entity;
                return true;
            }

            box = default;
            return false;
        }

        private static TileFeatureState CreateActivatedState(TileFeatureState tileFeature)
        {
            return new TileFeatureState(
                tileFeature.TileId,
                tileFeature.Cell,
                tileFeature.Kind,
                tileFeature.Flags | TileFeatureFlags.Activated,
                tileFeature.SourceEntityId,
                tileFeature.OwnerEntityId,
                tileFeature.TeamId,
                tileFeature.LifetimeTicks,
                tileFeature.Charges);
        }
    }
}

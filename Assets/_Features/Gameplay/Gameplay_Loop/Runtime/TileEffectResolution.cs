using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Phases;

namespace Game.Feature.Gameplay.Loop
{
    internal enum TileEffectEntityContactKind
    {
        MoveEnter = 0,
        SlideEnter = 1,
        PushEnter = 2,
        FlipLanding = 3,
        ImpactFollowThrough = 4,
    }

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

        public TileEffectEntityContact ToEntityContact()
        {
            return new TileEffectEntityContact(
                BoxEntityId,
                EntityType.Box,
                Cell,
                Cell,
                Cell,
                TileEffectEntityContact.ToEntityContactKind(Kind),
                MovementSemanticKind.None,
                operationOrder: 0);
        }
    }

    internal readonly struct TileEffectEntityContact
    {
        public TileEffectEntityContact(
            int entityId,
            EntityType entityType,
            SurfaceCell fromCell,
            SurfaceCell destinationCell,
            SurfaceCell tileCell,
            TileEffectEntityContactKind contactKind,
            MovementSemanticKind movementSemanticKind,
            long operationOrder,
            int actionPlanId = 0,
            int localActionIndex = 0,
            int intentId = 0,
            float visualContactNormalizedTime = 0f)
        {
            EntityId = entityId;
            EntityType = entityType;
            FromCell = fromCell;
            DestinationCell = destinationCell;
            TileCell = tileCell;
            ContactKind = contactKind;
            MovementSemanticKind = movementSemanticKind;
            OperationOrder = operationOrder;
            ActionPlanId = actionPlanId;
            LocalActionIndex = localActionIndex;
            IntentId = intentId;
            VisualContactNormalizedTime = ClampNormalized(visualContactNormalizedTime);
        }

        public int EntityId { get; }

        public EntityType EntityType { get; }

        public SurfaceCell FromCell { get; }

        public SurfaceCell DestinationCell { get; }

        public SurfaceCell TileCell { get; }

        public TileEffectEntityContactKind ContactKind { get; }

        public MovementSemanticKind MovementSemanticKind { get; }

        public long OperationOrder { get; }

        public int ActionPlanId { get; }

        public int LocalActionIndex { get; }

        public int IntentId { get; }

        public float VisualContactNormalizedTime { get; }

        public static TileEffectEntityContactKind ToEntityContactKind(TileEffectBoxContactKind kind)
        {
            return kind switch
            {
                TileEffectBoxContactKind.SlideEnter => TileEffectEntityContactKind.SlideEnter,
                TileEffectBoxContactKind.PushEnter => TileEffectEntityContactKind.PushEnter,
                TileEffectBoxContactKind.FlipLanding => TileEffectEntityContactKind.FlipLanding,
                TileEffectBoxContactKind.ImpactFollowThrough => TileEffectEntityContactKind.ImpactFollowThrough,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown box contact kind."),
            };
        }

        private static float ClampNormalized(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }

    internal static class TileEffectPresentationTiming
    {
        public static PresentationTimingAnchor ForDestroyTileContact(
            in TileEffectEntityContact contact)
        {
            if (contact.MovementSemanticKind != MovementSemanticKind.Flip)
            {
                return PresentationTimingAnchor.Immediate();
            }

            return PresentationTimingAnchor.MotionContact(
                contact.EntityId,
                0,
                contact.ActionPlanId,
                contact.LocalActionIndex,
                MovementSemanticKind.Flip,
                contact.VisualContactNormalizedTime > 0f
                    ? contact.VisualContactNormalizedTime
                    : GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime);
        }
    }

    internal enum TileEffectTriggerSourceKind
    {
        MoveEntityAccepted = 0,
        ImpactFollowThroughSettlement = 1,
        FeatureActivatedUnderOccupant = 2,
        SpawnSettlement = 3,
        RespawnSettlement = 4,
        ScriptedRelocation = 5,
        PersistentOverlapDiagnosticOnly = 6,
    }

    internal readonly struct TileFeatureActivationOccupantFact
    {
        public TileFeatureActivationOccupantFact(
            int featureTileId,
            SurfaceCell featureCell,
            TileFeatureKind featureKind,
            int occupantEntityId,
            EntityType occupantType,
            TileEffectTriggerSourceKind sourceKind,
            int? sourceOperationOrdinal)
        {
            FeatureTileId = featureTileId;
            FeatureCell = featureCell;
            FeatureKind = featureKind;
            OccupantEntityId = occupantEntityId;
            OccupantType = occupantType;
            SourceKind = sourceKind;
            SourceOperationOrdinal = sourceOperationOrdinal;
        }

        public int FeatureTileId { get; }

        public SurfaceCell FeatureCell { get; }

        public TileFeatureKind FeatureKind { get; }

        public int OccupantEntityId { get; }

        public EntityType OccupantType { get; }

        public TileEffectTriggerSourceKind SourceKind { get; }

        public int? SourceOperationOrdinal { get; }
    }

    internal enum TileEffectBoxMovementFamily
    {
        None = 0,
        Push = 1,
        Slide = 2,
        Flip = 3,
    }

    internal readonly struct TileEffectBoxStop
    {
        public TileEffectBoxStop(
            int boxEntityId,
            SurfaceCell cell,
            TileEffectBoxMovementFamily movementFamily)
            : this(
                boxEntityId,
                cell,
                movementFamily,
                TileEffectBoxStopCause.Create(
                    boxEntityId,
                    cell,
                    movementFamily,
                    MovementSemanticKind.None,
                    actionPlanId: 0,
                    localActionIndex: 0,
                    intentId: 0,
                    visualContactNormalizedTime: 0f))
        {
        }

        public TileEffectBoxStop(
            int boxEntityId,
            SurfaceCell cell,
            TileEffectBoxMovementFamily movementFamily,
            TileEffectBoxStopCause cause)
        {
            BoxEntityId = boxEntityId;
            Cell = cell;
            MovementFamily = movementFamily;
            Cause = cause.IsValid
                ? cause
                : TileEffectBoxStopCause.Create(
                    boxEntityId,
                    cell,
                    movementFamily,
                    MovementSemanticKind.None,
                    actionPlanId: 0,
                    localActionIndex: 0,
                    intentId: 0,
                    visualContactNormalizedTime: 0f);
        }

        public int BoxEntityId { get; }

        public SurfaceCell Cell { get; }

        public TileEffectBoxMovementFamily MovementFamily { get; }

        public TileEffectBoxStopCause Cause { get; }
    }

    internal readonly struct TileEffectBoxStopCause
    {
        private TileEffectBoxStopCause(
            int boxEntityId,
            SurfaceCell cell,
            TileEffectBoxMovementFamily movementFamily,
            MovementSemanticKind movementSemanticKind,
            int actionPlanId,
            int localActionIndex,
            int intentId,
            float visualContactNormalizedTime)
        {
            BoxEntityId = boxEntityId;
            Cell = cell;
            MovementFamily = movementFamily;
            MovementSemanticKind = movementSemanticKind;
            ActionPlanId = actionPlanId;
            LocalActionIndex = localActionIndex;
            IntentId = intentId;
            VisualContactNormalizedTime = ClampNormalized(visualContactNormalizedTime);
        }

        public int BoxEntityId { get; }

        public SurfaceCell Cell { get; }

        public TileEffectBoxMovementFamily MovementFamily { get; }

        public MovementSemanticKind MovementSemanticKind { get; }

        public int ActionPlanId { get; }

        public int LocalActionIndex { get; }

        public int IntentId { get; }

        public float VisualContactNormalizedTime { get; }

        public bool IsValid => BoxEntityId > 0 && MovementFamily != TileEffectBoxMovementFamily.None;

        public static TileEffectBoxStopCause Create(
            int boxEntityId,
            SurfaceCell cell,
            TileEffectBoxMovementFamily movementFamily,
            MovementSemanticKind movementSemanticKind,
            int actionPlanId,
            int localActionIndex,
            int intentId,
            float visualContactNormalizedTime)
        {
            return new TileEffectBoxStopCause(
                boxEntityId,
                cell,
                movementFamily,
                movementSemanticKind,
                actionPlanId,
                localActionIndex,
                intentId,
                visualContactNormalizedTime);
        }

        public PresentationTimingAnchor CreateTimingAnchor(PresentationBarrierKey barrierKey)
        {
            if (MovementSemanticKind != MovementSemanticKind.Flip)
            {
                return PresentationTimingAnchor.Immediate();
            }

            return PresentationTimingAnchor.MotionContact(
                BoxEntityId,
                0,
                ActionPlanId,
                LocalActionIndex,
                MovementSemanticKind,
                VisualContactNormalizedTime > 0f
                    ? VisualContactNormalizedTime
                    : GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime,
                barrierKey);
        }

        private static float ClampNormalized(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }

    internal readonly struct TileEffectResolutionContext
    {
        public TileEffectResolutionContext(
            int tickIndex,
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            IReadOnlyList<TileEffectBoxContact> boxContacts = null,
            WorldSnapshot previousSnapshot = null,
            IReadOnlyList<TileEffectBoxStop> boxStops = null,
            IReadOnlyList<TileFeatureActivationOccupantFact> activationOccupantFacts = null)
            : this(
                tickIndex,
                snapshot,
                tileFeatureDefinitions,
                ConvertBoxContacts(boxContacts),
                previousSnapshot,
                boxStops,
                activationOccupantFacts)
        {
        }

        public TileEffectResolutionContext(
            int tickIndex,
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            IReadOnlyList<TileEffectEntityContact> entityContacts,
            WorldSnapshot previousSnapshot = null,
            IReadOnlyList<TileEffectBoxStop> boxStops = null,
            IReadOnlyList<TileFeatureActivationOccupantFact> activationOccupantFacts = null)
        {
            TickIndex = tickIndex;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            TileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
            EntityContacts = entityContacts ?? Array.Empty<TileEffectEntityContact>();
            PreviousSnapshot = previousSnapshot;
            BoxStops = boxStops ?? Array.Empty<TileEffectBoxStop>();
            ActivationOccupantFacts = activationOccupantFacts ?? Array.Empty<TileFeatureActivationOccupantFact>();
        }

        public int TickIndex { get; }

        public WorldSnapshot Snapshot { get; }

        public IReadOnlyList<TileFeatureRuntimeDefinition> TileFeatureDefinitions { get; }

        public IReadOnlyList<TileEffectEntityContact> EntityContacts { get; }

        public IReadOnlyList<TileEffectBoxStop> BoxStops { get; }

        public IReadOnlyList<TileFeatureActivationOccupantFact> ActivationOccupantFacts { get; }

        public WorldSnapshot PreviousSnapshot { get; }

        private static IReadOnlyList<TileEffectEntityContact> ConvertBoxContacts(
            IReadOnlyList<TileEffectBoxContact> boxContacts)
        {
            if (boxContacts == null || boxContacts.Count == 0)
            {
                return Array.Empty<TileEffectEntityContact>();
            }

            var converted = new TileEffectEntityContact[boxContacts.Count];
            for (var i = 0; i < boxContacts.Count; i++)
            {
                converted[i] = boxContacts[i].ToEntityContact();
            }

            return converted;
        }
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
                if (!ShouldLatchButton(context, tileFeature, destroyedBoxIds, out var acceptedCause))
                {
                    continue;
                }

                operations ??= new TileFeatureOperationBatch();
                operations.Add(TileFeatureOperation.Update(CreateActivatedState(tileFeature)));
                var barrierKey = PresentationBarrierKey.ButtonActivated(tileFeature.TileId);
                tileEvents.Add(new TilePresentationEvent(
                    TilePresentationEventKind.ButtonActivated,
                    tileFeature.TileId,
                    tileFeature.Cell,
                    tileFeature.Kind,
                    tileFeature.SourceEntityId,
                    tileFeature.OwnerEntityId,
                    tileFeature.TeamId,
                    targetEntityId: acceptedCause.BoxEntityId,
                    timingAnchor: acceptedCause.CreateTimingAnchor(barrierKey),
                    barrierKey: barrierKey));
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
            HashSet<int> excludedBoxIds,
            out TileEffectBoxStopCause acceptedCause)
        {
            acceptedCause = default;
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

            return TryGetAcceptedButtonStoppedBox(
                context.Snapshot,
                context.BoxStops,
                tileFeature.Cell,
                definition.BoxSelector,
                excludedBoxIds,
                out _,
                out acceptedCause);
        }

        private static FinalizationBatch ResolveDestroyTiles(
            in TileEffectResolutionContext context,
            out List<TilePresentationEvent> tileEvents,
            out HashSet<int> destroyedBoxIds)
        {
            tileEvents = new List<TilePresentationEvent>();
            var batch = new FinalizationBatch();
            destroyedBoxIds = new HashSet<int>();
            if (context.EntityContacts.Count == 0 &&
                context.ActivationOccupantFacts.Count == 0)
            {
                return batch;
            }

            var destroyedEntityIds = new HashSet<int>();
            var orderedContacts = new List<TileEffectEntityContact>(context.EntityContacts);
            orderedContacts.Sort(CompareTileEffectEntityContacts);
            var tileFeaturesAtCell = new List<TileFeatureState>();

            for (var i = 0; i < orderedContacts.Count; i++)
            {
                var contact = orderedContacts[i];
                if (contact.EntityId <= 0 ||
                    destroyedEntityIds.Contains(contact.EntityId))
                {
                    continue;
                }

                context.Snapshot.EnumerateTileFeaturesAt(contact.TileCell, tileFeaturesAtCell);
                for (var tileIndex = 0; tileIndex < tileFeaturesAtCell.Count; tileIndex++)
                {
                    var tileFeature = tileFeaturesAtCell[tileIndex];
                    if (tileFeature.Kind != TileFeatureKind.Destroy ||
                        !TryFindDefinition(context.TileFeatureDefinitions, tileFeature.TileId, out var definition) ||
                        !TileFeatureActivationQueries.IsActive(tileFeature, definition, context.Snapshot.Topology) ||
                        !TryGetValidDestroyTarget(context.Snapshot, contact, out var target))
                    {
                        continue;
                    }

                    AddDestroyTileDestroy(
                        context,
                        batch,
                        tileEvents,
                        destroyedEntityIds,
                        destroyedBoxIds,
                        contact.EntityId,
                        target,
                        tileFeature,
                        contact.TileCell,
                        TileEffectPresentationTiming.ForDestroyTileContact(contact));
                    break;
                }
            }

            if (context.ActivationOccupantFacts.Count > 0)
            {
                var orderedFacts = new List<TileFeatureActivationOccupantFact>(context.ActivationOccupantFacts);
                orderedFacts.Sort(CompareTileFeatureActivationOccupantFacts);

                for (var i = 0; i < orderedFacts.Count; i++)
                {
                    var fact = orderedFacts[i];
                    if (fact.SourceKind != TileEffectTriggerSourceKind.FeatureActivatedUnderOccupant ||
                        fact.FeatureKind != TileFeatureKind.Destroy ||
                        fact.OccupantType != EntityType.Box ||
                        fact.OccupantEntityId <= 0 ||
                        destroyedEntityIds.Contains(fact.OccupantEntityId) ||
                        !context.Snapshot.TryGetTileFeature(fact.FeatureTileId, out var tileFeature) ||
                        tileFeature.Kind != TileFeatureKind.Destroy ||
                        tileFeature.Cell != fact.FeatureCell ||
                        !TryFindDefinition(context.TileFeatureDefinitions, tileFeature.TileId, out var definition) ||
                        !TileFeatureActivationQueries.IsActive(tileFeature, definition, context.Snapshot.Topology) ||
                        !TryGetValidDestroyBoxTarget(
                            context.Snapshot,
                            fact.OccupantEntityId,
                            fact.FeatureCell,
                            out var target))
                    {
                        continue;
                    }

                    AddDestroyTileDestroy(
                        context,
                        batch,
                        tileEvents,
                        destroyedEntityIds,
                        destroyedBoxIds,
                        fact.OccupantEntityId,
                        target,
                        tileFeature,
                        fact.FeatureCell,
                        PresentationTimingAnchor.Immediate());
                }
            }

            return batch;
        }

        private static void AddDestroyTileDestroy(
            in TileEffectResolutionContext context,
            FinalizationBatch batch,
            List<TilePresentationEvent> tileEvents,
            HashSet<int> destroyedEntityIds,
            HashSet<int> destroyedBoxIds,
            int targetEntityId,
            in EntityState target,
            TileFeatureState tileFeature,
            SurfaceCell tileCell,
            PresentationTimingAnchor timingAnchor)
        {
            var metadata = CreateDestroyTileMetadata(
                context.TickIndex,
                target,
                tileCell);
            batch.SetBoardPresence(
                targetEntityId,
                EntityBoardPresence.Detached,
                metadata);
            batch.MarkDestroy(
                targetEntityId,
                metadata);
            tileEvents.Add(new TilePresentationEvent(
                TilePresentationEventKind.DestroyTileTriggered,
                tileFeature.TileId,
                tileFeature.Cell,
                tileFeature.Kind,
                tileFeature.SourceEntityId,
                tileFeature.OwnerEntityId,
                tileFeature.TeamId,
                targetEntityId: targetEntityId,
                timingAnchor: timingAnchor));
            destroyedEntityIds.Add(targetEntityId);
            if (target.type == EntityType.Box)
            {
                destroyedBoxIds.Add(targetEntityId);
            }
        }

        private static int CompareTileFeatureActivationOccupantFacts(
            TileFeatureActivationOccupantFact left,
            TileFeatureActivationOccupantFact right)
        {
            var cellCompare = CompareSurfaceCells(left.FeatureCell, right.FeatureCell);
            if (cellCompare != 0)
            {
                return cellCompare;
            }

            var kindCompare = left.FeatureKind.CompareTo(right.FeatureKind);
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            var tileCompare = left.FeatureTileId.CompareTo(right.FeatureTileId);
            if (tileCompare != 0)
            {
                return tileCompare;
            }

            var occupantCompare = left.OccupantEntityId.CompareTo(right.OccupantEntityId);
            if (occupantCompare != 0)
            {
                return occupantCompare;
            }

            var sourceCompare = left.SourceKind.CompareTo(right.SourceKind);
            if (sourceCompare != 0)
            {
                return sourceCompare;
            }

            if (!left.SourceOperationOrdinal.HasValue && !right.SourceOperationOrdinal.HasValue)
            {
                return 0;
            }

            if (!left.SourceOperationOrdinal.HasValue)
            {
                return -1;
            }

            if (!right.SourceOperationOrdinal.HasValue)
            {
                return 1;
            }

            return left.SourceOperationOrdinal.Value.CompareTo(right.SourceOperationOrdinal.Value);
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
            if (context.EntityContacts.Count == 0)
            {
                return batch;
            }

            var redirectedBoxIds = new HashSet<int>();
            var orderedContacts = new List<TileEffectEntityContact>(context.EntityContacts);
            orderedContacts.Sort(CompareTileEffectEntityContacts);
            var tileFeaturesAtCell = new List<TileFeatureState>();

            for (var i = 0; i < orderedContacts.Count; i++)
            {
                var contact = orderedContacts[i];
                if (contact.EntityType != EntityType.Box ||
                    !IsSlideRedirectContact(contact.ContactKind) ||
                    contact.EntityId <= 0 ||
                    (destroyedBoxIds != null && destroyedBoxIds.Contains(contact.EntityId)) ||
                    redirectedBoxIds.Contains(contact.EntityId) ||
                    !TryGetValidSlideTarget(context.Snapshot, contact.EntityId, contact.TileCell, out var box))
                {
                    continue;
                }

                context.Snapshot.EnumerateTileFeaturesAt(contact.TileCell, tileFeaturesAtCell);
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
                            contact.EntityId,
                            redirectDirection,
                            CreateSlideTileRedirectMetadata(context.TickIndex, contact.EntityId, contact.TileCell));
                        tileEvents.Add(new TilePresentationEvent(
                            TilePresentationEventKind.SlideTileRedirected,
                            tileFeature.TileId,
                            tileFeature.Cell,
                            tileFeature.Kind,
                            tileFeature.SourceEntityId,
                            tileFeature.OwnerEntityId,
                            tileFeature.TeamId,
                            targetEntityId: contact.EntityId,
                            direction: redirectDirection));
                    }

                    redirectedBoxIds.Add(contact.EntityId);
                    break;
                }
            }

            return batch;
        }

        private static int CompareTileEffectEntityContacts(TileEffectEntityContact left, TileEffectEntityContact right)
        {
            var cellCompare = CompareSurfaceCells(left.TileCell, right.TileCell);
            if (cellCompare != 0)
            {
                return cellCompare;
            }

            var entityCompare = left.EntityId.CompareTo(right.EntityId);
            if (entityCompare != 0)
            {
                return entityCompare;
            }

            var kindCompare = left.ContactKind.CompareTo(right.ContactKind);
            return kindCompare != 0 ? kindCompare : left.OperationOrder.CompareTo(right.OperationOrder);
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
            in TileEffectEntityContact contact,
            out EntityState entity)
        {
            if (contact.EntityType == EntityType.Box)
            {
                return TryGetValidDestroyBoxTarget(snapshot, contact.EntityId, contact.TileCell, out entity);
            }

            if (contact.EntityType == EntityType.Unit)
            {
                return TryGetValidDestroyUnitTarget(snapshot, contact.EntityId, contact.TileCell, out entity);
            }

            entity = default;
            return false;
        }

        private static bool TryGetValidDestroyBoxTarget(
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

        private static bool TryGetValidDestroyUnitTarget(
            WorldSnapshot snapshot,
            int unitEntityId,
            SurfaceCell contactCell,
            out EntityState unit)
        {
            if (snapshot.TryGetEntity(unitEntityId, out unit) &&
                unit.type == EntityType.Unit &&
                TileFeatureHazardQueries.IsDestroyTileLethalForUnit(unit) &&
                unit.position == contactCell &&
                unit.boardPresence == EntityBoardPresence.Occupying &&
                unit.hp > 0 &&
                !unit.markedForDeath)
            {
                return true;
            }

            unit = default;
            return false;
        }

        private static bool TryGetValidSlideTarget(
            WorldSnapshot snapshot,
            int boxEntityId,
            SurfaceCell contactCell,
            out EntityState box)
        {
            if (TryGetValidDestroyBoxTarget(snapshot, boxEntityId, contactCell, out box) &&
                box.state == EntityPhaseState.Sliding)
            {
                return true;
            }

            box = default;
            return false;
        }

        private static bool IsSlideRedirectContact(TileEffectEntityContactKind kind)
        {
            return kind == TileEffectEntityContactKind.PushEnter ||
                   kind == TileEffectEntityContactKind.SlideEnter;
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
            in EntityState target,
            SurfaceCell contactCell)
        {
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.None,
                sourceActorEntityId: target.entityId,
                actionPlanId: tickIndex,
                exitCauseHint: ResolveDestroyTileExitCause(target),
                damageSourceType: DamageSourceType.Environmental,
                presentationTargetCell: contactCell,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.ScriptedRelocation,
                boundaryReason: "DestroyTile",
                exitPresentationTiming: EntityExitPresentationTiming.AfterEntityMotion,
                hasPresentationTargetCell: true);
        }

        private static TickEntityExitCause ResolveDestroyTileExitCause(in EntityState target)
        {
            if (target.type == EntityType.Box)
            {
                return TickEntityExitCause.BoxDestroy;
            }

            if (EntityRolePolicy.IsEnemyUnit(target))
            {
                return TickEntityExitCause.Killed;
            }

            return TickEntityExitCause.None;
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

        private static bool TryGetAcceptedButtonStoppedBox(
            WorldSnapshot snapshot,
            IReadOnlyList<TileEffectBoxStop> stops,
            SurfaceCell buttonCell,
            TileFeatureBoxSelector selector,
            HashSet<int> excludedBoxIds,
            out EntityState acceptedBox,
            out TileEffectBoxStopCause acceptedCause)
        {
            acceptedBox = default;
            acceptedCause = default;
            if (stops == null || stops.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < stops.Count; i++)
            {
                var stop = stops[i];
                if (stop.Cell != buttonCell ||
                    (excludedBoxIds != null && excludedBoxIds.Contains(stop.BoxEntityId)) ||
                    !IsButtonAcceptedStopFamily(stop.MovementFamily) ||
                    !TryGetValidStoppedBox(snapshot, stop.BoxEntityId, buttonCell, out var box) ||
                    !MatchesButtonBoxSelector(box, selector))
                {
                    continue;
                }

                acceptedBox = box;
                acceptedCause = stop.Cause;
                return true;
            }

            return false;
        }

        private static bool IsButtonAcceptedStopFamily(TileEffectBoxMovementFamily movementFamily)
        {
            return movementFamily == TileEffectBoxMovementFamily.Push ||
                   movementFamily == TileEffectBoxMovementFamily.Slide ||
                   movementFamily == TileEffectBoxMovementFamily.Flip;
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

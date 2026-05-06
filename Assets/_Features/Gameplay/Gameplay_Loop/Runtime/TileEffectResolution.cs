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

    internal readonly struct TileEffectResolutionContext
    {
        public TileEffectResolutionContext(
            int tickIndex,
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            IReadOnlyList<TileEffectBoxContact> boxContacts = null)
        {
            TickIndex = tickIndex;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            TileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
            BoxContacts = boxContacts ?? Array.Empty<TileEffectBoxContact>();
        }

        public int TickIndex { get; }

        public WorldSnapshot Snapshot { get; }

        public IReadOnlyList<TileFeatureRuntimeDefinition> TileFeatureDefinitions { get; }

        public IReadOnlyList<TileEffectBoxContact> BoxContacts { get; }
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

            TileFeatureOperationBatch operations = null;
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileFeature = tileFeatures[i];
                if (!ShouldLatchButton(context, tileFeature))
                {
                    continue;
                }

                operations ??= new TileFeatureOperationBatch();
                operations.Add(TileFeatureOperation.Update(CreateActivatedState(tileFeature)));
            }

            var entityOperations = ResolveDestroyTiles(context, out var tileEvents);

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
            TileFeatureState tileFeature)
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

            return IsAcceptedBox(context.Snapshot, tileFeature.Cell, definition.BoxSelector);
        }

        private static FinalizationBatch ResolveDestroyTiles(
            in TileEffectResolutionContext context,
            out List<TilePresentationEvent> tileEvents)
        {
            tileEvents = new List<TilePresentationEvent>();
            var batch = new FinalizationBatch();
            if (context.BoxContacts.Count == 0)
            {
                return batch;
            }

            var destroyedBoxIds = new HashSet<int>();
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
                boundaryReason: "DestroyTile");
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

        private static bool IsAcceptedBox(
            WorldSnapshot snapshot,
            SurfaceCell cell,
            TileFeatureBoxSelector selector)
        {
            switch (selector)
            {
                case TileFeatureBoxSelector.AnyPushableBox:
                    return TryGetValidOccupyingBox(snapshot, cell, out var anyBox) &&
                           (anyBox.boxCapabilities & BoxCapabilities.Push) != 0;

                case TileFeatureBoxSelector.MoonBlockOnly:
                    return TryGetValidOccupyingBox(snapshot, cell, out var moonBox) &&
                           moonBox.boxArchetype == BoxArchetype.Moon;

                case TileFeatureBoxSelector.None:
                case TileFeatureBoxSelector.FeatureCell:
                case TileFeatureBoxSelector.BoundEntity:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(selector), selector, "Unknown TileFeature box selector.");
            }
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

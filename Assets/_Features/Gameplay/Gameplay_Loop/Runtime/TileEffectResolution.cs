using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    internal readonly struct TileEffectResolutionContext
    {
        public TileEffectResolutionContext(
            int tickIndex,
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            TickIndex = tickIndex;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            TileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
        }

        public int TickIndex { get; }

        public WorldSnapshot Snapshot { get; }

        public IReadOnlyList<TileFeatureRuntimeDefinition> TileFeatureDefinitions { get; }
    }

    internal readonly struct TileEffectResolutionResult
    {
        private readonly TileFeatureOperationBatch _operations;

        public TileEffectResolutionResult(TileFeatureOperationBatch operations)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public static TileEffectResolutionResult Empty => new(new TileFeatureOperationBatch());

        public TileFeatureOperationBatch Operations => _operations ?? new TileFeatureOperationBatch();

        public bool IsEmpty => Operations.IsEmpty;
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

            return operations == null || operations.IsEmpty
                ? TileEffectResolutionResult.Empty
                : new TileEffectResolutionResult(operations);
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

using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal enum TileFeatureBlockerSubject
    {
        Box = 0,
        Unit = 1,
    }

    internal enum TileFeatureMovementKind
    {
        GroundStep = 0,
        Free2DTopologyTransition = 1,
        PushStart = 2,
        SlidingContinuation = 3,
        FlipLanding = 4,
    }

    internal static class TileFeatureMovementBlockerQuery
    {
        public static bool HasActiveBarricadeBlocker(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            SurfaceCell cell,
            TileFeatureBlockerSubject subject,
            TileFeatureMovementKind movementKind)
        {
            return TryGetActiveBarricadeBlocker(snapshot, definitions, cell, subject, movementKind, out _);
        }

        public static bool TryGetActiveBarricadeBlocker(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            SurfaceCell cell,
            TileFeatureBlockerSubject subject,
            TileFeatureMovementKind movementKind,
            out TileFeatureState barricade)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!IsSupportedBarricadeBlockerSubject(subject, movementKind) ||
                definitions == null ||
                definitions.Count == 0)
            {
                barricade = default;
                return false;
            }

            var tileFeatures = new List<TileFeatureState>();
            snapshot.EnumerateTileFeaturesAt(cell, tileFeatures);
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileFeature = tileFeatures[i];
                if (tileFeature.Kind != TileFeatureKind.Barricade ||
                    !TryFindDefinition(definitions, tileFeature.TileId, out var definition))
                {
                    continue;
                }

                if (TileFeatureActivationQueries.IsActive(tileFeature, definition, snapshot.Topology))
                {
                    barricade = tileFeature;
                    return true;
                }
            }

            barricade = default;
            return false;
        }

        private static bool IsSupportedBarricadeBlockerSubject(
            TileFeatureBlockerSubject subject,
            TileFeatureMovementKind movementKind)
        {
            return subject switch
            {
                TileFeatureBlockerSubject.Box => movementKind == TileFeatureMovementKind.PushStart ||
                                                 movementKind == TileFeatureMovementKind.SlidingContinuation ||
                                                 movementKind == TileFeatureMovementKind.FlipLanding,
                TileFeatureBlockerSubject.Unit => movementKind == TileFeatureMovementKind.GroundStep ||
                                                  movementKind == TileFeatureMovementKind.Free2DTopologyTransition,
                _ => false,
            };
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
    }
}

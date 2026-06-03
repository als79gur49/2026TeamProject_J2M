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
        UnitPlacement = 5,
        UnitSettlement = 6,
        ImpactFollowThrough = 7,
    }

    internal static class TileFeatureMovementBlockerQuery
    {
        public static bool HasTopologyTransitionTileFeatureBlocker(
            WorldSnapshot snapshot,
            SurfaceCell targetCell)
        {
            return TryGetTopologyTransitionTileFeatureBlocker(
                snapshot,
                targetCell,
                out _);
        }

        public static bool TryGetTopologyTransitionTileFeatureBlocker(
            WorldSnapshot snapshot,
            SurfaceCell targetCell,
            out TileFeatureState blocker)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var tileFeatures = new List<TileFeatureState>();
            snapshot.EnumerateTileFeaturesAt(targetCell, tileFeatures);
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileFeature = tileFeatures[i];
                if (tileFeature.Kind == TileFeatureKind.Destroy ||
                    tileFeature.Kind == TileFeatureKind.Barricade)
                {
                    blocker = tileFeature;
                    return true;
                }
            }

            blocker = default;
            return false;
        }

        public static bool HasActiveBarricadeBlocker(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            SurfaceCell cell,
            TileFeatureBlockerSubject subject,
            TileFeatureMovementKind movementKind,
            CubeTopologyState? evaluationTopology = null,
            int existingOccupantEntityId = 0)
        {
            return TryGetActiveBarricadeBlocker(
                snapshot,
                definitions,
                cell,
                subject,
                movementKind,
                out _,
                evaluationTopology,
                existingOccupantEntityId);
        }

        public static bool TryGetActiveBarricadeBlocker(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            SurfaceCell cell,
            TileFeatureBlockerSubject subject,
            TileFeatureMovementKind movementKind,
            out TileFeatureState barricade,
            CubeTopologyState? evaluationTopology = null,
            int existingOccupantEntityId = 0)
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

                var topology = evaluationTopology ?? snapshot.Topology;
                var activation = BarricadeEffectiveActivationPolicy.Evaluate(
                    snapshot,
                    topology,
                    tileFeature,
                    definition);
                if (activation.TopologyActive)
                {
                    if (subject == TileFeatureBlockerSubject.Unit &&
                        BarricadeActivationOccupantQuery.IsExistingBlockingUnitAt(
                            snapshot,
                            topology,
                            cell,
                            existingOccupantEntityId))
                    {
                        continue;
                    }

                    if (subject == TileFeatureBlockerSubject.Box &&
                        activation.GameplayStateKind == BarricadeGameplayStateKind.ActiveSuppressedByUnit)
                    {
                        continue;
                    }

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
                                                 movementKind == TileFeatureMovementKind.FlipLanding ||
                                                 movementKind == TileFeatureMovementKind.ImpactFollowThrough,
                TileFeatureBlockerSubject.Unit => movementKind == TileFeatureMovementKind.GroundStep ||
                                                  movementKind == TileFeatureMovementKind.Free2DTopologyTransition ||
                                                  movementKind == TileFeatureMovementKind.UnitPlacement ||
                                                  movementKind == TileFeatureMovementKind.UnitSettlement,
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

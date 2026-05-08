using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Movement.Expansion
{
    internal static class TileFeatureBoxBlockerQuery
    {
        public static bool HasActiveBarricadeBlocker(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            SurfaceCell cell)
        {
            return TryGetActiveBarricadeBlocker(snapshot, definitions, cell, out _);
        }

        public static bool TryGetActiveBarricadeBlocker(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            SurfaceCell cell,
            out TileFeatureState barricade)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (definitions == null || definitions.Count == 0)
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

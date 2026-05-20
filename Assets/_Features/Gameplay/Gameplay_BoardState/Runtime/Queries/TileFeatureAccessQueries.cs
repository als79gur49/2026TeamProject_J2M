using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    public static class TileFeatureAccessQueries
    {
        public static bool IsActiveDestroyTile(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            SurfaceCell cell,
            CubeTopologyState evaluationTopology)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (definitions == null || definitions.Count == 0)
            {
                return false;
            }

            var tileFeaturesAtCell = new List<TileFeatureState>();
            snapshot.EnumerateTileFeaturesAt(cell, tileFeaturesAtCell);
            for (var i = 0; i < tileFeaturesAtCell.Count; i++)
            {
                var tileFeature = tileFeaturesAtCell[i];
                if (tileFeature.Kind != TileFeatureKind.Destroy ||
                    !TryFindDefinition(definitions, tileFeature.TileId, out var definition))
                {
                    continue;
                }

                if (TileFeatureActivationQueries.IsActive(tileFeature, definition, evaluationTopology))
                {
                    return true;
                }
            }

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

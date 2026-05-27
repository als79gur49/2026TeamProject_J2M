using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.BoardState
{
    public static class TileFeatureHazardQueries
    {
        public static TileApproachRisk EvaluateTileApproachRisk(
            WorldSnapshot snapshot,
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            in EntityState actor,
            SurfaceCell candidateCell)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!IsDestroyTileLethalForUnit(actor) ||
                IsActiveGlider(snapshot, actor.entityId))
            {
                return TileApproachRisk.Neutral;
            }

            var tileFeaturesAtCell = new List<TileFeatureState>();
            snapshot.EnumerateTileFeaturesAt(candidateCell, tileFeaturesAtCell);
            for (var i = 0; i < tileFeaturesAtCell.Count; i++)
            {
                var tileFeature = tileFeaturesAtCell[i];
                if (tileFeature.Kind == TileFeatureKind.Destroy &&
                    TryFindDefinition(definitions, tileFeature.TileId, out var definition) &&
                    TileFeatureActivationQueries.IsActive(tileFeature, definition, snapshot.Topology))
                {
                    return TileApproachRisk.LethalOnEnter;
                }
            }

            return TileApproachRisk.Neutral;
        }

        public static bool IsDestroyTileLethalForUnit(in EntityState actor)
        {
            return actor.type == EntityType.Unit &&
                   actor.unitMobilityKind != UnitMobilityKind.Air;
        }

        private static bool TryFindDefinition(
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            int tileId,
            out TileFeatureRuntimeDefinition definition)
        {
            if (definitions != null)
            {
                for (var i = 0; i < definitions.Count; i++)
                {
                    var candidate = definitions[i];
                    if (candidate.TileId == tileId)
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = default;
            return false;
        }

        private static bool IsActiveGlider(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetEnemyGlideState(entityId, out var glideState) &&
                   glideState.IsActive;
        }
    }
}

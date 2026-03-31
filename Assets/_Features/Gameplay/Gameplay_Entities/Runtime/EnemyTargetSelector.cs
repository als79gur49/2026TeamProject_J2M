using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    internal static class EnemyTargetSelector
    {
        public static bool TryFindNearestOpponent(
            WorldSnapshot snapshot,
            in EntityState source,
            int maxDistance,
            out EntityState target)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            target = default;

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            var bestDistance = int.MaxValue;
            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var candidate = orderedEntities[i];
                if (!IsValidTarget(snapshot, source, candidate))
                {
                    continue;
                }

                var distance = GetPlanarDistance(source.position, candidate.position);
                if (!distance.HasValue || distance.Value > maxDistance || distance.Value >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance.Value;
                target = candidate;
            }

            return bestDistance != int.MaxValue;
        }

        private static bool IsValidTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState candidate)
        {
            return candidate.entityId != source.entityId &&
                   candidate.type == EntityType.Unit &&
                   candidate.teamId != source.teamId &&
                   candidate.hp > 0 &&
                   snapshot.CanBeTargetedForNewSelection(candidate.entityId);
        }

        private static int? GetPlanarDistance(SurfaceCell source, SurfaceCell target)
        {
            if (source.face != target.face)
            {
                return null;
            }

            var delta = target - source;
            return Math.Abs(delta.x) + Math.Abs(delta.y);
        }
    }
}

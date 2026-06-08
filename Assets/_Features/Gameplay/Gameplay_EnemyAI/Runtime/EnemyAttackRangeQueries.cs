using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    internal static class EnemyAttackRangeQueries
    {
        public static bool IsTargetInRange(
            in EntityState source,
            in EntityState target,
            in AttackDecisionSettings settings)
        {
            settings.Validate(nameof(settings));

            var distance = GetPlanarDistance(source.position, target.position);
            return distance.HasValue && distance.Value <= settings.AttackRange;
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

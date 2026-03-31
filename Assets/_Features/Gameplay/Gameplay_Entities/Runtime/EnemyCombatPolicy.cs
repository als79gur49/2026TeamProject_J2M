using System;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    internal static class EnemyCombatPolicy
    {
        public static bool TryBuildAttackIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in EnemyAiConfig config,
            out RawAttackIntent intent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            intent = default;

            var distance = GetPlanarDistance(source.position, target.position);
            if (!distance.HasValue || distance.Value > config.AttackRange)
            {
                return false;
            }

            intent = new RawAttackIntent(
                source.entityId,
                config.AttackPriority,
                target.entityId);
            return true;
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

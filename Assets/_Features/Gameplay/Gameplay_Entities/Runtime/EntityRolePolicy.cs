using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    public static class EntityRolePolicy
    {
        public static bool IsEnemyUnit(in EntityState entity)
        {
            return IsEnemyUnit(entity.type, entity.unitRole);
        }

        public static bool IsEnemyUnit(EntityType entityType, UnitRole unitRole)
        {
            return entityType == EntityType.Unit &&
                   unitRole == UnitRole.Enemy;
        }

        public static bool IsPlayerUnit(in EntityState entity)
        {
            return IsPlayerUnit(entity.type, entity.unitRole);
        }

        public static bool IsPlayerUnit(EntityType entityType, UnitRole unitRole)
        {
            return entityType == EntityType.Unit &&
                   unitRole == UnitRole.Player;
        }
    }
}

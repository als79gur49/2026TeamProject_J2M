using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class EnemyEntityLogicFactory : IEntityLogicFactory
    {
        public bool CanCreate(in EntityState entity)
        {
            return entity.type == EntityType.Unit &&
                   entity.aiMode != EnemyAiMode.None;
        }

        public IEntityLogic Create(in EntityState entity)
        {
            return new EnemyLogic(entity.entityId);
        }
    }
}

using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Model.Actions
{
    public readonly struct SpawnAction
    {
        public SpawnAction(int spawnId, EntityState entity)
        {
            SpawnId = spawnId;
            Entity = entity;
        }

        public int SpawnId { get; }

        public EntityState Entity { get; }
    }
}

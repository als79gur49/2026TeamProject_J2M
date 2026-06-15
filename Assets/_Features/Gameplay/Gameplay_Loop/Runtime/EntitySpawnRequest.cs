using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.Loop
{
    internal enum EntitySpawnRequestKind
    {
        Summon = 0,
    }

    internal readonly struct EntitySpawnRequestSource
    {
        public EntitySpawnRequestSource(
            int sourceEntityId,
            int sourceEffectIndex,
            int triggerTick,
            SurfaceCell originCell)
        {
            SourceEntityId = sourceEntityId;
            SourceEffectIndex = sourceEffectIndex;
            TriggerTick = triggerTick;
            OriginCell = originCell;
        }

        public int SourceEntityId { get; }

        public int SourceEffectIndex { get; }

        public int TriggerTick { get; }

        public SurfaceCell OriginCell { get; }
    }

    internal readonly struct EntitySpawnRequest
    {
        public EntitySpawnRequest(
            EntitySpawnRequestKind kind,
            EntitySpawnRequestSource source,
            EntityState sourceEntity,
            int spawnIndex,
            int tickIndex,
            SummonMinionRuntime summon,
            EnemyUnitSpawnDefaultsRuntime spawnDefaults)
        {
            Kind = kind;
            Source = source;
            SourceEntity = sourceEntity;
            SpawnIndex = spawnIndex;
            TickIndex = tickIndex;
            Summon = summon;
            SpawnDefaults = spawnDefaults;
        }

        public EntitySpawnRequestKind Kind { get; }

        public EntitySpawnRequestSource Source { get; }

        public EntityState SourceEntity { get; }

        public int SpawnIndex { get; }

        public int TickIndex { get; }

        public SummonMinionRuntime Summon { get; }

        public EnemyUnitSpawnDefaultsRuntime SpawnDefaults { get; }
    }

    internal readonly struct EntitySpawnMaterializationResult
    {
        private EntitySpawnMaterializationResult(
            bool succeeded,
            EntityState spawnedEntity,
            SurfaceCell spawnCell)
        {
            Succeeded = succeeded;
            SpawnedEntity = spawnedEntity;
            SpawnCell = spawnCell;
        }

        public bool Succeeded { get; }

        public EntityState SpawnedEntity { get; }

        public SurfaceCell SpawnCell { get; }

        public static EntitySpawnMaterializationResult Success(EntityState spawnedEntity, SurfaceCell spawnCell)
        {
            return new EntitySpawnMaterializationResult(true, spawnedEntity, spawnCell);
        }

        public static EntitySpawnMaterializationResult Skipped()
        {
            return new EntitySpawnMaterializationResult(false, default, default);
        }
    }
}

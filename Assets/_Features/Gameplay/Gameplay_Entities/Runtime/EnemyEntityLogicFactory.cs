using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class EnemyEntityLogicFactory : IEntityLogicFactory
    {
        private readonly EnemyAiProfile _defaultProfile;
        private readonly IReadOnlyDictionary<int, EnemyAiProfile> _profilesByEntityId;

        public EnemyEntityLogicFactory()
            : this(EnemyAiProfile.CreateRuntimeDefault())
        {
        }

        public EnemyEntityLogicFactory(
            EnemyAiProfile defaultProfile,
            IReadOnlyDictionary<int, EnemyAiProfile> profilesByEntityId = null)
        {
            _defaultProfile = defaultProfile ?? throw new ArgumentNullException(nameof(defaultProfile));
            _profilesByEntityId = profilesByEntityId;
        }

        public bool CanCreate(in EntityState entity)
        {
            return entity.type == EntityType.Unit &&
                   entity.aiMode != EnemyAiMode.None;
        }

        public IEntityLogic Create(in EntityState entity)
        {
            return new EnemyLogic(entity.entityId, ResolveProfile(entity));
        }

        internal EnemyAiProfile ResolveProfile(in EntityState entity)
        {
            if (_profilesByEntityId != null &&
                _profilesByEntityId.TryGetValue(entity.entityId, out var overriddenProfile) &&
                overriddenProfile != null)
            {
                return overriddenProfile;
            }

            return _defaultProfile;
        }
    }
}

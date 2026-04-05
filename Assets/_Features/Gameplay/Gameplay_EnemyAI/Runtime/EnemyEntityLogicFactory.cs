using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class EnemyEntityLogicFactory : IEntityLogicFactory
    {
        private readonly EnemyAiRuntimeDefinition _defaultDefinition;
        private readonly IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> _definitionsByEntityId;

        public EnemyEntityLogicFactory()
            : this(EnemyAiRuntimeDefinition.CreateDefaultMelee())
        {
        }

        public EnemyEntityLogicFactory(
            EnemyAiRuntimeDefinition defaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId = null)
        {
            defaultDefinition.Validate(nameof(defaultDefinition));

            _defaultDefinition = defaultDefinition;
            _definitionsByEntityId = definitionsByEntityId;
        }

        public bool CanCreate(in EntityState entity)
        {
            return entity.type == EntityType.Unit &&
                   entity.aiMode != EnemyAiMode.None;
        }

        public IEntityLogic Create(in EntityState entity)
        {
            return new EnemyLogic(entity.entityId, ResolveDefinition(entity));
        }

        internal EnemyAiRuntimeDefinition ResolveDefinition(in EntityState entity)
        {
            if (_definitionsByEntityId != null &&
                _definitionsByEntityId.TryGetValue(entity.entityId, out var overriddenDefinition))
            {
                overriddenDefinition.Validate(nameof(overriddenDefinition));
                return overriddenDefinition;
            }

            return _defaultDefinition;
        }
    }
}

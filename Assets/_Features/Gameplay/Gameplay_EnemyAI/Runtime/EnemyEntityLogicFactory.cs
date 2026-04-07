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

    internal static class EnemyParticipationPolicy
    {
        public static bool TryGetEnemyLogicEntity(
            WorldSnapshot snapshot,
            int entityId,
            out EntityState entity)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!snapshot.TryGetEntity(entityId, out entity))
            {
                return false;
            }

            return IsEnemyLogicEntity(entity);
        }

        public static bool IsEnemyLogicEntity(in EntityState entity)
        {
            return entity.type == EntityType.Unit &&
                   entity.aiMode != EnemyAiMode.None;
        }

        public static bool CanParticipateOnCurrentTopology(
            WorldSnapshot snapshot,
            in EntityState entity)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return IsEnemyLogicEntity(entity) &&
                   entity.position.face == snapshot.Topology.BottomFace;
        }

        public static bool IsControllableParticipant(
            WorldSnapshot snapshot,
            in EntityState entity)
        {
            return CanParticipateOnCurrentTopology(snapshot, entity) &&
                   entity.hp > 0 &&
                   !entity.markedForDeath &&
                   entity.boardPresence == EntityBoardPresence.Occupying &&
                   entity.aiMode != EnemyAiMode.Dead;
        }
    }
}

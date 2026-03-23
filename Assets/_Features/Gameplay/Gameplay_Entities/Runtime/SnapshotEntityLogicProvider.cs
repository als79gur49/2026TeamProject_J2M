using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Phases;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class SnapshotEntityLogicProvider : ISnapshotEntityLogicProvider
    {
        private readonly IReadOnlyList<IEntityLogicFactory> _entityLogicFactories;

        public SnapshotEntityLogicProvider(IEnumerable<IEntityLogicFactory> entityLogicFactories)
        {
            if (entityLogicFactories == null)
            {
                throw new ArgumentNullException(nameof(entityLogicFactories));
            }

            var factories = new List<IEntityLogicFactory>();

            foreach (var factory in entityLogicFactories)
            {
                if (factory == null)
                {
                    throw new ArgumentException("Entity logic factory collections cannot contain null entries.", nameof(entityLogicFactories));
                }

                factories.Add(factory);
            }

            _entityLogicFactories = factories.AsReadOnly();
        }

        public IReadOnlyList<IEntityLogic> Build(
            WorldSnapshot snapshot,
            IReadOnlyList<IEntityLogic> staticEntityLogics)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (staticEntityLogics == null)
            {
                throw new ArgumentNullException(nameof(staticEntityLogics));
            }

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            var entityLogics = new List<IEntityLogic>(staticEntityLogics.Count + orderedEntities.Count);

            for (var i = 0; i < staticEntityLogics.Count; i++)
            {
                entityLogics.Add(staticEntityLogics[i]);
            }

            for (var entityIndex = 0; entityIndex < orderedEntities.Count; entityIndex++)
            {
                var entity = orderedEntities[entityIndex];

                for (var factoryIndex = 0; factoryIndex < _entityLogicFactories.Count; factoryIndex++)
                {
                    var factory = _entityLogicFactories[factoryIndex];
                    if (!factory.CanCreate(entity))
                    {
                        continue;
                    }

                    var candidate = factory.Create(entity);
                    if (candidate == null)
                    {
                        throw new InvalidOperationException("Entity logic factories must not return null.");
                    }

                    if (HasPhaseOwnershipConflict(entity.entityId, candidate, entityLogics))
                    {
                        continue;
                    }

                    entityLogics.Add(candidate);
                }
            }

            return entityLogics;
        }

        private static bool HasPhaseOwnershipConflict(
            int entityId,
            IEntityLogic candidate,
            IReadOnlyList<IEntityLogic> existingEntityLogics)
        {
            if (candidate is not IEntityLogicSourceBinding candidateBinding)
            {
                return false;
            }

            return HasPhaseOwnershipConflict(entityId, TickPhase.Movement, candidateBinding, existingEntityLogics)
                || HasPhaseOwnershipConflict(entityId, TickPhase.Attack, candidateBinding, existingEntityLogics);
        }

        private static bool HasPhaseOwnershipConflict(
            int entityId,
            TickPhase phase,
            IEntityLogicSourceBinding candidateBinding,
            IReadOnlyList<IEntityLogic> existingEntityLogics)
        {
            if (!candidateBinding.ControlsEntity(entityId, phase))
            {
                return false;
            }

            for (var i = 0; i < existingEntityLogics.Count; i++)
            {
                if (existingEntityLogics[i] is IEntityLogicSourceBinding existingBinding &&
                    existingBinding.ControlsEntity(entityId, phase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

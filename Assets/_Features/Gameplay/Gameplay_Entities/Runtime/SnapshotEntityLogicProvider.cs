using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

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

        public EntityLogicSet Build(
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
                AddStaticEntityLogic(staticEntityLogics[i], entityLogics);
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

                    if (HasPhaseOwnershipConflict(candidate, entityLogics))
                    {
                        continue;
                    }

                    entityLogics.Add(candidate);
                }
            }

            return BuildEntityLogicSet(entityLogics);
        }

        private static void AddStaticEntityLogic(
            IEntityLogic candidate,
            List<IEntityLogic> entityLogics)
        {
            if (candidate == null)
            {
                throw new InvalidOperationException("Static entity logic collections cannot contain null entries.");
            }

            if (HasPhaseOwnershipConflict(candidate, entityLogics))
            {
                throw new InvalidOperationException("Static entity logic configuration contains duplicate phase ownership.");
            }

            entityLogics.Add(candidate);
        }

        private static EntityLogicSet BuildEntityLogicSet(IReadOnlyList<IEntityLogic> entityLogics)
        {
            var aiStateLogics = new List<IEnemyAiStateLogic>(entityLogics.Count);
            var movementLogics = new List<IMovementEntityLogic>(entityLogics.Count);
            var attackLogics = new List<IAttackEntityLogic>(entityLogics.Count);

            for (var i = 0; i < entityLogics.Count; i++)
            {
                if (entityLogics[i] is IEnemyAiStateLogic aiStateLogic)
                {
                    aiStateLogics.Add(aiStateLogic);
                }

                if (entityLogics[i] is IMovementEntityLogic movementLogic)
                {
                    movementLogics.Add(movementLogic);
                }

                if (entityLogics[i] is IAttackEntityLogic attackLogic)
                {
                    attackLogics.Add(attackLogic);
                }
            }

            return new EntityLogicSet(
                aiStateLogics.AsReadOnly(),
                movementLogics.AsReadOnly(),
                attackLogics.AsReadOnly());
        }

        private static bool HasPhaseOwnershipConflict(
            IEntityLogic candidate,
            IReadOnlyList<IEntityLogic> existingEntityLogics)
        {
            if (candidate is not IEntityLogicSourceBinding candidateBinding)
            {
                return false;
            }

            return HasPhaseOwnershipConflict<IMovementEntityLogic>(candidate, candidateBinding, existingEntityLogics)
                || HasPhaseOwnershipConflict<IEnemyAiStateLogic>(candidate, candidateBinding, existingEntityLogics)
                || HasPhaseOwnershipConflict<IAttackEntityLogic>(candidate, candidateBinding, existingEntityLogics);
        }

        private static bool HasPhaseOwnershipConflict<TPhaseLogic>(
            IEntityLogic candidate,
            IEntityLogicSourceBinding candidateBinding,
            IReadOnlyList<IEntityLogic> existingEntityLogics)
            where TPhaseLogic : class, IEntityLogic
        {
            if (candidate is not TPhaseLogic)
            {
                return false;
            }

            for (var i = 0; i < existingEntityLogics.Count; i++)
            {
                if (existingEntityLogics[i] is TPhaseLogic &&
                    existingEntityLogics[i] is IEntityLogicSourceBinding existingBinding &&
                    existingBinding.ControlledEntityId == candidateBinding.ControlledEntityId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.PlayerControl;

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
                var creationContext = new EntityLogicCreationContext(snapshot, entity);

                for (var factoryIndex = 0; factoryIndex < _entityLogicFactories.Count; factoryIndex++)
                {
                    var factory = _entityLogicFactories[factoryIndex];
                    if (!factory.CanCreate(creationContext))
                    {
                        continue;
                    }

                    var candidate = factory.Create(creationContext);
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
            var preMovementStateLogics = new List<IPreMovementStateLogic>(entityLogics.Count);
            var aiStateLogics = new List<IEnemyAiStateLogic>(entityLogics.Count);
            var enemyActionStateLogics = new List<IEnemyActionStateLogic>(entityLogics.Count);
            var frontFaceSupportLogics = new List<IFrontFaceSupportLogic>(entityLogics.Count);
            var movementLogics = new List<IMovementEntityLogic>(entityLogics.Count);
            var attackLogics = new List<IAttackEntityLogic>(entityLogics.Count);
            var playerControlLogicSourceIds = new HashSet<int>();

            for (var i = 0; i < entityLogics.Count; i++)
            {
                if (entityLogics[i] is IPreMovementStateLogic preMovementStateLogic)
                {
                    preMovementStateLogics.Add(preMovementStateLogic);
                }

                if (entityLogics[i] is IEnemyAiStateLogic aiStateLogic)
                {
                    aiStateLogics.Add(aiStateLogic);
                }

                if (entityLogics[i] is IEnemyActionStateLogic enemyActionStateLogic)
                {
                    enemyActionStateLogics.Add(enemyActionStateLogic);
                }

                if (entityLogics[i] is IFrontFaceSupportLogic frontFaceSupportLogic)
                {
                    frontFaceSupportLogics.Add(frontFaceSupportLogic);
                }

                if (entityLogics[i] is IMovementEntityLogic movementLogic)
                {
                    movementLogics.Add(movementLogic);
                }

                if (entityLogics[i] is IAttackEntityLogic attackLogic)
                {
                    attackLogics.Add(attackLogic);
                }

                if (entityLogics[i] is PlayerLogic playerLogic &&
                    entityLogics[i] is IEntityLogicSourceBinding binding &&
                    playerControlLogicSourceIds.Add(binding.ControlledEntityId))
                {
                    preMovementStateLogics.Add(
                        new PlayerControlStateLogic(
                            binding.ControlledEntityId,
                            playerLogic.PushWindupTicks,
                            playerLogic.PushRecoveryTicks,
                            playerLogic.FlipWindupTicks,
                            playerLogic.FlipRecoveryTicks));
                }
            }

            return new EntityLogicSet(
                preMovementStateLogics.AsReadOnly(),
                aiStateLogics.AsReadOnly(),
                enemyActionStateLogics.AsReadOnly(),
                frontFaceSupportLogics.AsReadOnly(),
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
                || HasPhaseOwnershipConflict<IPreMovementStateLogic>(candidate, candidateBinding, existingEntityLogics)
                || HasPhaseOwnershipConflict<IEnemyAiStateLogic>(candidate, candidateBinding, existingEntityLogics)
                || HasPhaseOwnershipConflict<IEnemyActionStateLogic>(candidate, candidateBinding, existingEntityLogics)
                || HasPhaseOwnershipConflict<IFrontFaceSupportLogic>(candidate, candidateBinding, existingEntityLogics)
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

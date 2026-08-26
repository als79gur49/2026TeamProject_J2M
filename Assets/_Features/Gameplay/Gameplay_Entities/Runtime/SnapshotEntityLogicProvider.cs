using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class SnapshotEntityLogicProvider : ISnapshotEntityLogicProvider, IEnemyGlidePresentationSettingsResolver
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

            var diagnosticsEnabled = GameplayTickWorkloadDiagnostics.IsEnabled;
            var workloadMetrics = diagnosticsEnabled
                ? new EntityLogicBuildMetricsAccumulator(_entityLogicFactories.Count)
                : default;

            var entityLogics = new List<IEntityLogic>(staticEntityLogics.Count + orderedEntities.Count);
            var phaseOwnerIndex = new PhaseOwnerIndex();

            for (var i = 0; i < staticEntityLogics.Count; i++)
            {
                AddStaticEntityLogic(staticEntityLogics[i], entityLogics, phaseOwnerIndex);
            }

            for (var entityIndex = 0; entityIndex < orderedEntities.Count; entityIndex++)
            {
                var entity = orderedEntities[entityIndex];
                var creationContext = new EntityLogicCreationContext(snapshot, entity);

                if (diagnosticsEnabled)
                {
                    workloadMetrics.RecordEntityVisited(entity.type);
                }

                for (var factoryIndex = 0; factoryIndex < _entityLogicFactories.Count; factoryIndex++)
                {
                    var factory = _entityLogicFactories[factoryIndex];
                    if (diagnosticsEnabled)
                    {
                        workloadMetrics.RecordCanCreateProbe(entity.type);
                    }

                    if (!factory.CanCreate(creationContext))
                    {
                        continue;
                    }

                    var candidate = factory.Create(creationContext);
                    if (candidate == null)
                    {
                        throw new InvalidOperationException("Entity logic factories must not return null.");
                    }

                    if (diagnosticsEnabled)
                    {
                        workloadMetrics.RecordCreated(entity.type);
                    }

                    if (!TryAddDynamicEntityLogic(candidate, entityLogics, phaseOwnerIndex))
                    {
                        if (diagnosticsEnabled)
                        {
                            workloadMetrics.RecordConflictRejected(entity.type);
                        }

                        continue;
                    }

                    if (diagnosticsEnabled)
                    {
                        workloadMetrics.RecordAccepted(entity.type);
                    }
                }
            }

            if (diagnosticsEnabled)
            {
                var metrics = workloadMetrics.Build();
                GameplayTickWorkloadDiagnostics.RecordEntityLogicBuild(metrics);
            }

            return BuildEntityLogicSet(entityLogics);
        }

        public bool TryResolveEnemyGlidePresentationSettings(
            WorldSnapshot snapshot,
            in EntityState entity,
            out EnemyGlidePresentationSettings settings)
        {
            for (var i = 0; i < _entityLogicFactories.Count; i++)
            {
                if (_entityLogicFactories[i] is IEnemyGlidePresentationSettingsResolver resolver &&
                    resolver.TryResolveEnemyGlidePresentationSettings(snapshot, entity, out settings))
                {
                    return true;
                }
            }

            settings = EnemyGlidePresentationSettings.CreateDefault();
            return false;
        }

        private static void AddStaticEntityLogic(
            IEntityLogic candidate,
            List<IEntityLogic> entityLogics,
            PhaseOwnerIndex phaseOwnerIndex)
        {
            if (candidate == null)
            {
                throw new InvalidOperationException("Static entity logic collections cannot contain null entries.");
            }

            var ownership = GetOwnership(candidate);
            if (phaseOwnerIndex.HasConflict(ownership))
            {
                throw new InvalidOperationException("Static entity logic configuration contains duplicate phase ownership.");
            }

            entityLogics.Add(candidate);
            phaseOwnerIndex.Register(ownership);
        }

        private static bool TryAddDynamicEntityLogic(
            IEntityLogic candidate,
            List<IEntityLogic> entityLogics,
            PhaseOwnerIndex phaseOwnerIndex)
        {
            var ownership = GetOwnership(candidate);
            if (phaseOwnerIndex.HasConflict(ownership))
            {
                return false;
            }

            entityLogics.Add(candidate);
            phaseOwnerIndex.Register(ownership);
            return true;
        }

        private static EntityLogicSet BuildEntityLogicSet(IReadOnlyList<IEntityLogic> entityLogics)
        {
            var preMovementStateLogics = new List<IPreMovementStateLogic>(entityLogics.Count);
            var aiStateLogics = new List<IEnemyAiStateLogic>(entityLogics.Count);
            var enemyActionStateLogics = new List<IEnemyActionStateLogic>(entityLogics.Count);
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
                movementLogics.AsReadOnly(),
                attackLogics.AsReadOnly());
        }

        internal static bool HasPhaseOwnershipConflictSlowForTest(
            IEntityLogic candidate,
            IReadOnlyList<IEntityLogic> existingEntityLogics)
        {
            return HasPhaseOwnershipConflictSlow(candidate, existingEntityLogics);
        }

        internal static bool HasPhaseOwnershipConflictIndexedForTest(
            IEntityLogic candidate,
            IReadOnlyList<IEntityLogic> existingEntityLogics)
        {
            var phaseOwnerIndex = new PhaseOwnerIndex();
            for (var i = 0; i < existingEntityLogics.Count; i++)
            {
                phaseOwnerIndex.Register(GetOwnership(existingEntityLogics[i]));
            }

            return phaseOwnerIndex.HasConflict(GetOwnership(candidate));
        }

        private static EntityLogicOwnership GetOwnership(IEntityLogic logic)
        {
            var phaseMask = GetSupportedPhaseMask(logic);
            if (phaseMask == EntityLogicPhaseMask.None ||
                logic is not IEntityLogicSourceBinding binding)
            {
                return EntityLogicOwnership.None;
            }

            return new EntityLogicOwnership(true, binding.ControlledEntityId, phaseMask);
        }

        private static EntityLogicPhaseMask GetSupportedPhaseMask(IEntityLogic logic)
        {
            var phaseMask = EntityLogicPhaseMask.None;

            if (logic is IMovementEntityLogic)
            {
                phaseMask |= EntityLogicPhaseMask.Movement;
            }

            if (logic is IPreMovementStateLogic)
            {
                phaseMask |= EntityLogicPhaseMask.PreMovementState;
            }

            if (logic is IEnemyAiStateLogic)
            {
                phaseMask |= EntityLogicPhaseMask.EnemyAiState;
            }

            if (logic is IEnemyActionStateLogic)
            {
                phaseMask |= EntityLogicPhaseMask.EnemyActionState;
            }

            if (logic is IAttackEntityLogic)
            {
                phaseMask |= EntityLogicPhaseMask.Attack;
            }

            return phaseMask;
        }

        private static bool HasPhaseOwnershipConflictSlow(
            IEntityLogic candidate,
            IReadOnlyList<IEntityLogic> existingEntityLogics)
        {
            if (candidate is not IEntityLogicSourceBinding candidateBinding)
            {
                return false;
            }

            return HasPhaseOwnershipConflictSlow<IMovementEntityLogic>(candidate, candidateBinding, existingEntityLogics)
                || HasPhaseOwnershipConflictSlow<IPreMovementStateLogic>(candidate, candidateBinding, existingEntityLogics)
                || HasPhaseOwnershipConflictSlow<IEnemyAiStateLogic>(candidate, candidateBinding, existingEntityLogics)
                || HasPhaseOwnershipConflictSlow<IEnemyActionStateLogic>(candidate, candidateBinding, existingEntityLogics)
                || HasPhaseOwnershipConflictSlow<IAttackEntityLogic>(candidate, candidateBinding, existingEntityLogics);
        }

        private static bool HasPhaseOwnershipConflictSlow<TPhaseLogic>(
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

        [Flags]
        private enum EntityLogicPhaseMask
        {
            None = 0,
            Movement = 1 << 0,
            PreMovementState = 1 << 1,
            EnemyAiState = 1 << 2,
            EnemyActionState = 1 << 3,
            Attack = 1 << 4,
        }

        private readonly struct EntityLogicOwnership
        {
            public static readonly EntityLogicOwnership None = new(false, 0, EntityLogicPhaseMask.None);

            public EntityLogicOwnership(
                bool hasSourceBinding,
                int controlledEntityId,
                EntityLogicPhaseMask phaseMask)
            {
                HasSourceBinding = hasSourceBinding;
                ControlledEntityId = controlledEntityId;
                PhaseMask = phaseMask;
            }

            public bool HasSourceBinding { get; }

            public int ControlledEntityId { get; }

            public EntityLogicPhaseMask PhaseMask { get; }
        }

        private readonly struct PhaseOwnerKey : IEquatable<PhaseOwnerKey>
        {
            public PhaseOwnerKey(EntityLogicPhaseMask phase, int controlledEntityId)
            {
                Phase = phase;
                ControlledEntityId = controlledEntityId;
            }

            private EntityLogicPhaseMask Phase { get; }

            private int ControlledEntityId { get; }

            public bool Equals(PhaseOwnerKey other)
            {
                return Phase == other.Phase &&
                       ControlledEntityId == other.ControlledEntityId;
            }

            public override bool Equals(object obj)
            {
                return obj is PhaseOwnerKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Phase * 397) ^ ControlledEntityId;
                }
            }
        }

        private sealed class PhaseOwnerIndex
        {
            private readonly HashSet<PhaseOwnerKey> _owners = new();

            public bool HasConflict(EntityLogicOwnership ownership)
            {
                if (!ownership.HasSourceBinding ||
                    ownership.PhaseMask == EntityLogicPhaseMask.None)
                {
                    return false;
                }

                return HasConflict(ownership, EntityLogicPhaseMask.Movement) ||
                       HasConflict(ownership, EntityLogicPhaseMask.PreMovementState) ||
                       HasConflict(ownership, EntityLogicPhaseMask.EnemyAiState) ||
                       HasConflict(ownership, EntityLogicPhaseMask.EnemyActionState) ||
                       HasConflict(ownership, EntityLogicPhaseMask.Attack);
            }

            public void Register(EntityLogicOwnership ownership)
            {
                if (!ownership.HasSourceBinding ||
                    ownership.PhaseMask == EntityLogicPhaseMask.None)
                {
                    return;
                }

                Register(ownership, EntityLogicPhaseMask.Movement);
                Register(ownership, EntityLogicPhaseMask.PreMovementState);
                Register(ownership, EntityLogicPhaseMask.EnemyAiState);
                Register(ownership, EntityLogicPhaseMask.EnemyActionState);
                Register(ownership, EntityLogicPhaseMask.Attack);
            }

            private bool HasConflict(EntityLogicOwnership ownership, EntityLogicPhaseMask phase)
            {
                return (ownership.PhaseMask & phase) == phase &&
                       _owners.Contains(new PhaseOwnerKey(phase, ownership.ControlledEntityId));
            }

            private void Register(EntityLogicOwnership ownership, EntityLogicPhaseMask phase)
            {
                if ((ownership.PhaseMask & phase) == phase)
                {
                    _owners.Add(new PhaseOwnerKey(phase, ownership.ControlledEntityId));
                }
            }
        }
    }
}

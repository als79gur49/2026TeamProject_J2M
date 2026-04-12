using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Entities
{
    internal interface IEnemyJumpTimingBinding : IEntityLogicSourceBinding
    {
        bool TryGetJumpCooldownTicks(out int cooldownTicks);
    }

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
            return new EnemyCoreLogicAdapter(entity.entityId, ResolveDefinition(entity));
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

    internal sealed class EnemyCombatEntityLogicFactory : IEntityLogicFactory
    {
        private readonly EnemyEntityLogicFactory _enemyLogicFactory;

        public EnemyCombatEntityLogicFactory()
            : this(EnemyAiRuntimeDefinition.CreateDefaultMelee())
        {
        }

        public EnemyCombatEntityLogicFactory(
            EnemyAiRuntimeDefinition defaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId = null)
        {
            _enemyLogicFactory = new EnemyEntityLogicFactory(defaultDefinition, definitionsByEntityId);
        }

        public bool CanCreate(in EntityState entity)
        {
            if (!_enemyLogicFactory.CanCreate(entity))
            {
                return false;
            }

            var definition = _enemyLogicFactory.ResolveDefinition(entity);
            return definition.Capabilities.TryGetCombat(out _) ||
                   definition.Capabilities.TryGetPassiveContact(out _);
        }

        public IEntityLogic Create(in EntityState entity)
        {
            return new EnemyAttackLogicAdapter(entity.entityId, _enemyLogicFactory.ResolveDefinition(entity));
        }
    }

    internal sealed class EnemyCoreLogicAdapter : IEnemyAiStateLogic, IPreMovementStateLogic, IMovementEntityLogic, IEnemyJumpTimingBinding
    {
        private readonly EnemyLogic _logic;

        public EnemyCoreLogicAdapter(int entityId, in EnemyAiRuntimeDefinition definition)
        {
            _logic = new EnemyLogic(entityId, definition);
        }

        public int ControlledEntityId => _logic.ControlledEntityId;

        public bool TryGetJumpCooldownTicks(out int cooldownTicks)
        {
            return _logic.TryGetJumpCooldownTicks(out cooldownTicks);
        }

        public void CommitAiTransitions(
            WorldSnapshot snapshot,
            in TickInput input,
            EnemyAiTransitionStage stage,
            IEnemyAiCommitContext writeContext,
            List<string> transitions)
        {
            ((IEnemyAiStateLogic)_logic).CommitAiTransitions(snapshot, input, stage, writeContext, transitions);
        }

        public void CommitPreMovementState(
            WorldSnapshot snapshot,
            in TickInput input,
            IPreMovementStateCommitContext writeContext,
            List<string> updates,
            List<PlayerActionTransition> actionTransitions)
        {
            ((IPreMovementStateLogic)_logic).CommitPreMovementState(snapshot, input, writeContext, updates, actionTransitions);
        }

        public void CollectMovementIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawMovementIntent> buffer)
        {
            _logic.CollectMovementIntents(snapshot, input, buffer);
        }
    }

    internal sealed class EnemyAttackLogicAdapter : IAttackEntityLogic, IEntityLogicSourceBinding
    {
        private readonly EnemyLogic _logic;

        public EnemyAttackLogicAdapter(int entityId, in EnemyAiRuntimeDefinition definition)
        {
            _logic = new EnemyLogic(entityId, definition);
        }

        public int ControlledEntityId => _logic.ControlledEntityId;

        public void CollectAttackIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawAttackIntent> buffer)
        {
            _logic.CollectAttackIntents(snapshot, input, buffer);
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

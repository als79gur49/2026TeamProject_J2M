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

    internal sealed class EnemyEntityLogicFactory : IEntityLogicFactory, IEnemyGlidePresentationSettingsResolver
    {
        private readonly EnemyAiRuntimeDefinition _defaultDefinition;
        private readonly IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> _definitionsByEntityId;
        private readonly IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> _definitionsByArchetypeId;

        public EnemyEntityLogicFactory()
            : this(EnemyAiRuntimeDefinition.CreateDefaultMelee())
        {
        }

        public EnemyEntityLogicFactory(
            EnemyAiRuntimeDefinition defaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId = null,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId = null)
        {
            defaultDefinition.Validate(nameof(defaultDefinition));

            _defaultDefinition = defaultDefinition;
            _definitionsByEntityId = definitionsByEntityId;
            _definitionsByArchetypeId = definitionsByArchetypeId;
        }

        public bool CanCreate(in EntityLogicCreationContext context)
        {
            var entity = context.Entity;
            return entity.type == EntityType.Unit &&
                   entity.aiMode != EnemyAiMode.None;
        }

        public IEntityLogic Create(in EntityLogicCreationContext context)
        {
            var entity = context.Entity;
            return new EnemyCoreLogicAdapter(entity.entityId, ResolveDefinition(context.Snapshot, entity));
        }

        internal EnemyAiRuntimeDefinition ResolveDefinition(WorldSnapshot snapshot, in EntityState entity)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (_definitionsByEntityId != null &&
                _definitionsByEntityId.TryGetValue(entity.entityId, out var overriddenDefinition))
            {
                overriddenDefinition.Validate(nameof(overriddenDefinition));
                return overriddenDefinition;
            }

            if (snapshot.TryGetEnemyDefinitionBindingState(entity.entityId, out var binding))
            {
                if (_definitionsByArchetypeId != null &&
                    _definitionsByArchetypeId.TryGetValue(binding.ArchetypeId, out var archetypeDefinition))
                {
                    archetypeDefinition.Validate(nameof(archetypeDefinition));
                    return archetypeDefinition;
                }

                throw new InvalidOperationException(
                    $"Missing enemy AI archetype definition for binding '{binding.ArchetypeId}' on entity {entity.entityId}.");
            }

            return _defaultDefinition;
        }

        public bool TryResolveEnemyGlidePresentationSettings(
            WorldSnapshot snapshot,
            in EntityState entity,
            out EnemyGlidePresentationSettings settings)
        {
            var definition = ResolveDefinition(snapshot, entity);
            if (definition.Capabilities.TryGetMovementSkill(out var movementSkill) &&
                movementSkill.Kind == MovementSkillStrategyKind.GlideOverSolid)
            {
                settings = movementSkill.GlidePresentationSettings;
                return true;
            }

            settings = EnemyGlidePresentationSettings.CreateDefault();
            return false;
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
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId = null,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId = null)
        {
            _enemyLogicFactory = new EnemyEntityLogicFactory(defaultDefinition, definitionsByEntityId, definitionsByArchetypeId);
        }

        public bool CanCreate(in EntityLogicCreationContext context)
        {
            if (!_enemyLogicFactory.CanCreate(context))
            {
                return false;
            }

            var definition = _enemyLogicFactory.ResolveDefinition(context.Snapshot, context.Entity);
            return definition.Capabilities.TryGetCombat(out _) ||
                   definition.Capabilities.TryGetPassiveContact(out _);
        }

        public IEntityLogic Create(in EntityLogicCreationContext context)
        {
            var entity = context.Entity;
            return new EnemyAttackLogicAdapter(entity.entityId, _enemyLogicFactory.ResolveDefinition(context.Snapshot, entity));
        }
    }

    internal sealed class EnemyFrontFaceSupportEntityLogicFactory : IEntityLogicFactory
    {
        private readonly EnemyEntityLogicFactory _enemyLogicFactory;

        public EnemyFrontFaceSupportEntityLogicFactory()
            : this(EnemyAiRuntimeDefinition.CreateDefaultMelee())
        {
        }

        public EnemyFrontFaceSupportEntityLogicFactory(
            EnemyAiRuntimeDefinition defaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId = null,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId = null)
        {
            _enemyLogicFactory = new EnemyEntityLogicFactory(defaultDefinition, definitionsByEntityId, definitionsByArchetypeId);
        }

        public bool CanCreate(in EntityLogicCreationContext context)
        {
            if (!_enemyLogicFactory.CanCreate(context))
            {
                return false;
            }

            var definition = _enemyLogicFactory.ResolveDefinition(context.Snapshot, context.Entity);
            return definition.Capabilities.TryGetFrontFaceSupport(out _);
        }

        public IEntityLogic Create(in EntityLogicCreationContext context)
        {
            var entity = context.Entity;
            return new EnemyFrontFaceSupportLogicAdapter(entity.entityId, _enemyLogicFactory.ResolveDefinition(context.Snapshot, entity));
        }
    }

    internal sealed class EnemyCoreLogicAdapter : IEnemyAiStateLogic, IPreMovementStateLogic, IMovementEntityLogic, IEnemyJumpTimingBinding, ITileFeatureDefinitionContextReceiver
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

        public void BindTileFeatureDefinitions(IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            _logic.BindTileFeatureDefinitions(tileFeatureDefinitions);
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

    internal sealed class EnemyFrontFaceSupportLogicAdapter : IFrontFaceSupportLogic, IEntityLogicSourceBinding
    {
        private readonly int _entityId;
        private readonly EnemyFrontFaceSupportCapabilityRuntime _capability;

        public EnemyFrontFaceSupportLogicAdapter(int entityId, in EnemyAiRuntimeDefinition definition)
        {
            _entityId = entityId;
            definition.Capabilities.TryGetFrontFaceSupport(out _capability);
        }

        public int ControlledEntityId => _entityId;

        public void CollectFrontFaceSupportContributors(
            WorldSnapshot snapshot,
            in TickInput input,
            List<FrontFaceSupportContributor> buffer)
        {
            _ = input;

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (_capability == null ||
                !EnemyParticipationPolicy.TryGetEnemyLogicEntity(snapshot, _entityId, out var source) ||
                !EnemyFrontFaceSupportPolicy.IsActiveFrontFaceSupportSource(snapshot, source) ||
                !snapshot.TryGetEnemyFrontFaceSupportState(_entityId, out var supportState) ||
                !supportState.HasEffectCount(_capability.Effects.Count))
            {
                return;
            }

            for (var i = 0; i < _capability.Effects.Count; i++)
            {
                if (supportState.EffectStates[i].phase != EnemyFrontFaceSupportEffectPhase.Active)
                {
                    continue;
                }

                buffer.Add(new FrontFaceSupportContributor(_entityId, source.position, i, _capability.Effects[i]));
            }
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
                   !IsHardInvalidParticipant(entity);
        }

        public static bool IsHardInvalidParticipant(in EntityState entity)
        {
            return !IsEnemyLogicEntity(entity) ||
                   entity.hp <= 0 ||
                   entity.markedForDeath ||
                   entity.boardPresence != EntityBoardPresence.Occupying ||
                   entity.aiMode == EnemyAiMode.Dead;
        }
    }

    internal static class EnemyFrontFaceSupportPolicy
    {
        public static bool IsActiveFrontFaceSupportSource(
            WorldSnapshot snapshot,
            in EntityState entity)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return EnemyParticipationPolicy.IsEnemyLogicEntity(entity) &&
                   entity.position.face == snapshot.Topology.FrontFace &&
                   entity.hp > 0 &&
                   !entity.markedForDeath &&
                   entity.boardPresence == EntityBoardPresence.Occupying &&
                   entity.aiMode != EnemyAiMode.Dead;
        }
    }
}

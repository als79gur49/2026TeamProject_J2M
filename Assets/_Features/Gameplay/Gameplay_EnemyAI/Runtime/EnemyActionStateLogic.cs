using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class EnemyActionStateLogic : IEnemyActionStateLogic, IEntityLogicSourceBinding
    {
        private readonly int _entityId;
        private readonly DetectionSettings _detectionSettings;
        private readonly IDetectionStrategy _detectionStrategy;
        private readonly EnemyCombatCapabilityRuntime _combatCapability;

        public EnemyActionStateLogic(int entityId)
            : this(entityId, EnemyAiRuntimeDefinition.CreateDefaultMelee())
        {
        }

        public EnemyActionStateLogic(int entityId, EnemyAiProfile profile)
            : this(
                entityId,
                (profile ?? throw new ArgumentNullException(nameof(profile)))
                .CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond))
        {
        }

        public EnemyActionStateLogic(int entityId, in EnemyAiRuntimeDefinition aiDefinition)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Enemy action state logic requires a positive entity ID.");
            }

            aiDefinition.Validate(nameof(aiDefinition));

            _entityId = entityId;
            _detectionSettings = aiDefinition.DetectionSettings;
            _detectionStrategy = aiDefinition.DetectionStrategy;
            aiDefinition.Capabilities.TryGetCombat(out _combatCapability);
        }

        public int ControlledEntityId => _entityId;

        public void CommitEnemyActionState(
            WorldSnapshot snapshot,
            in TickInput input,
            EnemyActionStage stage,
            IEnemyActionCommitContext writeContext,
            List<EnemyActionTransition> transitions)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            if (!EnemyParticipationPolicy.TryGetEnemyLogicEntity(snapshot, _entityId, out var source))
            {
                return;
            }

            var hasPreviousAction = snapshot.TryGetEnemyActionState(_entityId, out var previousAction);
            if (_combatCapability == null)
            {
                var clearedAction = EnemyActionQueries.Clear(previousAction);
                if (ShouldWriteActionState(hasPreviousAction, previousAction, clearedAction))
                {
                    writeContext.SetEnemyActionState(_entityId, clearedAction);
                    transitions.Add(new EnemyActionTransition(_entityId, previousAction, clearedAction));
                }

                return;
            }

            if (!EnemyParticipationPolicy.CanParticipateOnCurrentTopology(snapshot, source))
            {
                var clearedAction = EnemyActionQueries.Clear(previousAction);
                if (ShouldWriteActionState(hasPreviousAction, previousAction, clearedAction))
                {
                    writeContext.SetEnemyActionState(_entityId, clearedAction);
                    transitions.Add(new EnemyActionTransition(_entityId, previousAction, clearedAction));
                }

                return;
            }

            var nextAction = stage switch
            {
                EnemyActionStage.BeforeAttackCollection => CommitBeforeAttackCollection(snapshot, source, previousAction, writeContext, input.TickIndex),
                EnemyActionStage.AfterAttack => CommitAfterAttack(source, previousAction, input.TickIndex),
                _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown enemy action stage."),
            };

            if (ShouldWriteActionState(hasPreviousAction, previousAction, nextAction))
            {
                writeContext.SetEnemyActionState(_entityId, nextAction);
            }

            transitions.Add(new EnemyActionTransition(_entityId, previousAction, nextAction));
        }

        private EnemyActionRuntimeState CommitBeforeAttackCollection(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyActionRuntimeState previousAction,
            IEnemyActionCommitContext writeContext,
            int tickIndex)
        {
            if (source.hp <= 0 ||
                source.markedForDeath ||
                source.aiMode == EnemyAiMode.Dead)
            {
                return EnemyActionQueries.Clear(previousAction);
            }

            if (source.aiMode != EnemyAiMode.Attack)
            {
                return EnemyActionQueries.Clear(previousAction);
            }

            if (previousAction.IsActive)
            {
                if (EnemyActionStateTargeting.TryResolveLockedTarget(
                        snapshot,
                        source,
                        previousAction,
                        _combatCapability.AttackDecisionStrategy,
                        _detectionSettings,
                        _combatCapability.AttackDecisionSettings,
                        out _))
                {
                    if (source.facing != previousAction.direction)
                    {
                        writeContext.SetFacing(_entityId, previousAction.direction);
                    }

                    return previousAction;
                }

                ApplyCancelFallback(snapshot, source, writeContext);
                return EnemyActionQueries.Clear(previousAction);
            }

            if (!EnemyActionStateTargeting.TryResolveStartAction(
                    snapshot,
                    source,
                    _detectionStrategy,
                    _combatCapability.AttackDecisionStrategy,
                    _detectionSettings,
                    _combatCapability.AttackDecisionSettings,
                    out var target,
                    out var direction))
            {
                ApplyCancelFallback(snapshot, source, writeContext);
                return EnemyActionQueries.Clear(previousAction);
            }

            if (!snapshot.CanStartAction(_entityId, tickIndex))
            {
                return previousAction;
            }

            var nextAction = EnemyActionQueries.StartAction(
                previousAction,
                EnemyActionKind.Melee,
                target.entityId,
                direction,
                tickIndex,
                _combatCapability.AttackTimingSettings.WindupTicks);
            writeContext.SetFacing(_entityId, direction);
            return nextAction;
        }

        private static EnemyActionRuntimeState CommitAfterAttack(
            in EntityState source,
            in EnemyActionRuntimeState previousAction,
            int tickIndex)
        {
            if (!previousAction.IsActive)
            {
                return previousAction;
            }

            if (source.hp <= 0 ||
                source.markedForDeath ||
                source.aiMode != EnemyAiMode.Attack)
            {
                return EnemyActionQueries.Clear(previousAction);
            }

            return EnemyActionQueries.CanExecute(previousAction, tickIndex)
                ? EnemyActionQueries.MarkExecutionAttempted(previousAction, tickIndex)
                : previousAction;
        }

        private void ApplyCancelFallback(
            WorldSnapshot snapshot,
            in EntityState source,
            IEnemyActionCommitContext writeContext)
        {
            var fallbackMode = EnemyActionStateTargeting.ResolveFallbackAiMode(
                snapshot,
                source,
                _detectionStrategy,
                _detectionSettings);

            if (fallbackMode != source.aiMode ||
                source.aiStateTimer != 0)
            {
                writeContext.ApplyEnemyAiState(_entityId, fallbackMode, 0);
            }
        }
        private static bool ShouldWriteActionState(
            bool hadPreviousAction,
            in EnemyActionRuntimeState previousAction,
            in EnemyActionRuntimeState nextAction)
        {
            return hadPreviousAction ||
                   nextAction.IsActive ||
                   nextAction.sequence != 0 ||
                   !AreEqual(previousAction, nextAction);
        }

        private static bool AreEqual(
            in EnemyActionRuntimeState left,
            in EnemyActionRuntimeState right)
        {
            return left.kind == right.kind &&
                   left.sequence == right.sequence &&
                   left.lockedTargetEntityId == right.lockedTargetEntityId &&
                   left.direction == right.direction &&
                   left.startTick == right.startTick &&
                   left.executeTick == right.executeTick &&
                   left.executionAttempted == right.executionAttempted;
        }
    }

    internal sealed class EnemyActionStateEntityLogicFactory : IEntityLogicFactory
    {
        private readonly EnemyEntityLogicFactory _enemyLogicFactory;

        public EnemyActionStateEntityLogicFactory()
            : this(EnemyAiRuntimeDefinition.CreateDefaultMelee())
        {
        }

        public EnemyActionStateEntityLogicFactory(
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
            return definition.Capabilities.TryGetCombat(out _);
        }

        public IEntityLogic Create(in EntityState entity)
        {
            return new EnemyActionStateLogic(entity.entityId, _enemyLogicFactory.ResolveDefinition(entity));
        }
    }

    internal static class EnemyActionStateTargeting
    {
        public static bool TryResolveStartAction(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            IAttackDecisionStrategy attackDecisionStrategy,
            in DetectionSettings detectionSettings,
            in AttackDecisionSettings attackDecisionSettings,
            out EntityState target,
            out Direction direction)
        {
            target = default;
            direction = source.facing;

            if (!detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out target) ||
                !attackDecisionStrategy.IsTargetInRange(source, target, attackDecisionSettings))
            {
                target = default;
                return false;
            }

            direction = ResolveFacing(source, target);
            return true;
        }

        public static bool TryResolveLockedTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyActionRuntimeState actionState,
            IAttackDecisionStrategy attackDecisionStrategy,
            in DetectionSettings detectionSettings,
            in AttackDecisionSettings attackDecisionSettings,
            out EntityState target)
        {
            target = default;

            if (!snapshot.TryGetEntity(actionState.lockedTargetEntityId, out target) ||
                !IsValidLockedTarget(snapshot, source, target, detectionSettings) ||
                !attackDecisionStrategy.IsTargetInRange(source, target, attackDecisionSettings))
            {
                target = default;
                return false;
            }

            return true;
        }

        public static EnemyAiMode ResolveFallbackAiMode(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            in DetectionSettings detectionSettings)
        {
            return detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out _)
                ? EnemyAiMode.Chase
                : EnemyAiMode.Patrol;
        }

        public static Direction ResolveFacing(
            in EntityState source,
            in EntityState target)
        {
            if (source.position.face != target.position.face)
            {
                return source.facing;
            }

            var planarDelta = target.position.PlanarPosition - source.position.PlanarPosition;
            var horizontalDistance = Mathf.Abs(planarDelta.x);
            var verticalDistance = Mathf.Abs(planarDelta.y);

            if (horizontalDistance >= verticalDistance &&
                planarDelta.x != 0)
            {
                return planarDelta.x > 0
                    ? Direction.Right
                    : Direction.Left;
            }

            if (planarDelta.y != 0)
            {
                return planarDelta.y > 0
                    ? Direction.Up
                    : Direction.Down;
            }

            // Overlap contact attacks keep the actor's committed facing.
            return source.facing;
        }

        private static bool IsValidLockedTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in DetectionSettings detectionSettings)
        {
            if (target.entityId == source.entityId ||
                target.type != EntityType.Unit ||
                target.teamId == source.teamId ||
                target.hp <= 0)
            {
                return false;
            }

            if (target.boardPresence != EntityBoardPresence.Occupying ||
                !snapshot.Topology.IsFaceActive(target.position.face))
            {
                return false;
            }

            return !target.markedForDeath || detectionSettings.CanTargetMarkedForDeath;
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;

namespace Game.Feature.Gameplay.Entities
{
    public interface IEnemyAiStateResolver
    {
        EnemyAiTransitionDecision Resolve(
            WorldSnapshot snapshot,
            in EntityState source,
            EnemyAiTransitionStage stage,
            IDetectionStrategy detectionStrategy,
            IAttackDecisionStrategy attackDecisionStrategy,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings,
            in AttackDecisionSettings attackDecisionSettings);
    }

    public sealed class EnemyLogic : IEnemyAiStateLogic, IMovementEntityLogic, IAttackEntityLogic, IEntityLogicSourceBinding
    {
        private readonly int _entityId;
        private readonly EnemyAiCommonSettings _commonSettings;
        private readonly PatrolSettings _patrolSettings;
        private readonly DetectionSettings _detectionSettings;
        private readonly ChaseSettings _chaseSettings;
        private readonly AttackDecisionSettings _attackDecisionSettings;
        private readonly IPatrolStrategy _patrolStrategy;
        private readonly IDetectionStrategy _detectionStrategy;
        private readonly IChaseStrategy _chaseStrategy;
        private readonly IAttackDecisionStrategy _attackDecisionStrategy;
        private readonly IEnemyAiStateResolver _stateResolver;

        public EnemyLogic(int entityId)
            : this(entityId, EnemyAiRuntimeDefinition.CreateDefaultMelee())
        {
        }

        public EnemyLogic(int entityId, EnemyAiProfile profile)
            : this(entityId, (profile ?? throw new ArgumentNullException(nameof(profile))).CreateRuntimeDefinition())
        {
        }

        [Obsolete("Use EnemyAiProfile or EnemyAiRuntimeDefinition instead.")]
        public EnemyLogic(int entityId, EnemyAiConfig config)
            : this(entityId, config.ToRuntimeDefinition())
        {
        }

        public EnemyLogic(int entityId, in EnemyAiRuntimeDefinition aiDefinition)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Enemy logic requires a positive entity ID.");
            }

            aiDefinition.Validate(nameof(aiDefinition));

            _entityId = entityId;
            _commonSettings = aiDefinition.CommonSettings;
            _patrolSettings = aiDefinition.PatrolSettings;
            _detectionSettings = aiDefinition.DetectionSettings;
            _chaseSettings = aiDefinition.ChaseSettings;
            _attackDecisionSettings = aiDefinition.AttackDecisionSettings;
            _patrolStrategy = aiDefinition.PatrolStrategy;
            _detectionStrategy = aiDefinition.DetectionStrategy;
            _chaseStrategy = aiDefinition.ChaseStrategy;
            _attackDecisionStrategy = aiDefinition.AttackDecisionStrategy;
            _stateResolver = aiDefinition.StateResolver;
        }

        public int ControlledEntityId => _entityId;

        void IEnemyAiStateLogic.CommitAiTransitions(
            WorldSnapshot snapshot,
            in TickInput input,
            EnemyAiTransitionStage stage,
            IEnemyAiCommitContext writeContext,
            List<string> transitions)
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

            if (!TryGetAiControlledEnemy(snapshot, out var source))
            {
                return;
            }

            var decision = _stateResolver.Resolve(
                snapshot,
                source,
                stage,
                _detectionStrategy,
                _attackDecisionStrategy,
                _commonSettings,
                _detectionSettings,
                _attackDecisionSettings);

            if (decision.Mode == source.aiMode && decision.Timer == source.aiStateTimer)
            {
                return;
            }

            writeContext.ApplyEnemyAiState(source.entityId, decision.Mode, decision.Timer);
            transitions.Add(
                $"EnemyAiTransition|Stage={stage}|E={source.entityId}|From={source.aiMode}|FromTimer={source.aiStateTimer}|To={decision.Mode}|ToTimer={decision.Timer}|Reason={decision.Reason}");
        }

        public void CollectMovementIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawMovementIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!TryGetControllableEnemy(snapshot, out var source))
            {
                return;
            }

            switch (source.aiMode)
            {
                case EnemyAiMode.Patrol:
                    if (_patrolStrategy.TryBuildMovementIntent(
                            snapshot,
                            source,
                            _commonSettings,
                            _patrolSettings,
                            out var patrolIntent))
                    {
                        buffer.Add(patrolIntent);
                    }

                    return;

                case EnemyAiMode.Chase:
                    if (!_detectionStrategy.TryFindTarget(snapshot, source, _detectionSettings, out var chaseTarget))
                    {
                        return;
                    }

                    if (_chaseStrategy.TryBuildMovementIntent(
                            snapshot,
                            source,
                            chaseTarget,
                            _commonSettings,
                            _chaseSettings,
                            out var chaseIntent))
                    {
                        buffer.Add(chaseIntent);
                    }

                    return;

                default:
                    return;
            }
        }

        public void CollectAttackIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawAttackIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!TryGetControllableEnemy(snapshot, out var source) ||
                source.aiMode != EnemyAiMode.Attack ||
                !_detectionStrategy.TryFindTarget(snapshot, source, _detectionSettings, out var target) ||
                !_attackDecisionStrategy.TryBuildAttackIntent(
                    snapshot,
                    source,
                    target,
                    _commonSettings,
                    _attackDecisionSettings,
                    out var attackIntent))
            {
                return;
            }

            buffer.Add(attackIntent);
        }

        private bool TryGetAiControlledEnemy(WorldSnapshot snapshot, out EntityState source)
        {
            if (!snapshot.TryGetEntity(_entityId, out source))
            {
                return false;
            }

            return source.type == EntityType.Unit &&
                   source.aiMode != EnemyAiMode.None;
        }

        private bool TryGetControllableEnemy(WorldSnapshot snapshot, out EntityState source)
        {
            if (!snapshot.TryGetEntity(_entityId, out source))
            {
                return false;
            }

            return source.type == EntityType.Unit &&
                   source.hp > 0 &&
                   !source.markedForDeath &&
                   source.boardPresence == EntityBoardPresence.Occupying &&
                   snapshot.Topology.IsFaceActive(source.position.face) &&
                   source.aiMode != EnemyAiMode.None &&
                   source.aiMode != EnemyAiMode.Dead;
        }
    }

    public sealed class DefaultEnemyAiStateResolver : IEnemyAiStateResolver
    {
        public static readonly DefaultEnemyAiStateResolver Instance = new();

        public EnemyAiTransitionDecision Resolve(
            WorldSnapshot snapshot,
            in EntityState source,
            EnemyAiTransitionStage stage,
            IDetectionStrategy detectionStrategy,
            IAttackDecisionStrategy attackDecisionStrategy,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings,
            in AttackDecisionSettings attackDecisionSettings)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (detectionStrategy == null)
            {
                throw new ArgumentNullException(nameof(detectionStrategy));
            }

            if (attackDecisionStrategy == null)
            {
                throw new ArgumentNullException(nameof(attackDecisionStrategy));
            }

            if (source.hp <= 0 || source.markedForDeath)
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Dead, 0, "Dead");
            }

            switch (stage)
            {
                case EnemyAiTransitionStage.BeforeMovement:
                    return ResolveBeforeMovement(snapshot, source, detectionStrategy, attackDecisionStrategy, commonSettings, detectionSettings, attackDecisionSettings);

                case EnemyAiTransitionStage.BeforeAttack:
                    return ResolveBeforeAttack(snapshot, source, detectionStrategy, attackDecisionStrategy, commonSettings, detectionSettings, attackDecisionSettings);

                case EnemyAiTransitionStage.AfterAttack:
                    return ResolveAfterAttack(source, commonSettings);

                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown enemy AI transition stage.");
            }
        }

        private static EnemyAiTransitionDecision ResolveBeforeMovement(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            IAttackDecisionStrategy attackDecisionStrategy,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings,
            in AttackDecisionSettings attackDecisionSettings)
        {
            switch (source.aiMode)
            {
                case EnemyAiMode.None:
                case EnemyAiMode.Dead:
                    return Keep(source, "Disabled");

                case EnemyAiMode.Patrol:
                case EnemyAiMode.Chase:
                case EnemyAiMode.Attack:
                    return TryResolveCombatReadiness(
                        snapshot,
                        source,
                        detectionStrategy,
                        attackDecisionStrategy,
                        detectionSettings,
                        attackDecisionSettings,
                        EnemyAiMode.Patrol);

                case EnemyAiMode.Recover:
                    if (source.aiStateTimer > 0)
                    {
                        return new EnemyAiTransitionDecision(
                            EnemyAiMode.Recover,
                            source.aiStateTimer - 1,
                            "RecoverTick");
                    }

                    if (detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out _))
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "RecoverComplete");
                    }

                    return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, "RecoverCompleteNoTarget");

                default:
                    return Keep(source, "UnhandledBeforeMovement");
            }
        }

        private static EnemyAiTransitionDecision ResolveBeforeAttack(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            IAttackDecisionStrategy attackDecisionStrategy,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings,
            in AttackDecisionSettings attackDecisionSettings)
        {
            switch (source.aiMode)
            {
                case EnemyAiMode.Chase:
                case EnemyAiMode.Attack:
                    return TryResolveCombatReadiness(
                        snapshot,
                        source,
                        detectionStrategy,
                        attackDecisionStrategy,
                        detectionSettings,
                        attackDecisionSettings,
                        EnemyAiMode.Patrol);

                default:
                    return Keep(source, "NoBeforeAttackTransition");
            }
        }

        private static EnemyAiTransitionDecision ResolveAfterAttack(
            in EntityState source,
            in EnemyAiCommonSettings commonSettings)
        {
            if (source.aiMode != EnemyAiMode.Attack)
            {
                return Keep(source, "NoAfterAttackTransition");
            }

            return new EnemyAiTransitionDecision(
                EnemyAiMode.Recover,
                commonSettings.RecoverTicks,
                "AttackCommitted");
        }

        private static EnemyAiTransitionDecision TryResolveCombatReadiness(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            IAttackDecisionStrategy attackDecisionStrategy,
            in DetectionSettings detectionSettings,
            in AttackDecisionSettings attackDecisionSettings,
            EnemyAiMode patrolFallback)
        {
            if (!detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out var target))
            {
                return new EnemyAiTransitionDecision(patrolFallback, 0, "NoTarget");
            }

            if (attackDecisionStrategy.IsTargetInRange(source, target, attackDecisionSettings))
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, "TargetInRange");
            }

            return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "TargetSensed");
        }

        private static EnemyAiTransitionDecision Keep(in EntityState source, string reason)
        {
            return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, reason);
        }
    }

    public readonly struct EnemyAiTransitionDecision
    {
        public EnemyAiTransitionDecision(EnemyAiMode mode, int timer, string reason)
        {
            Mode = mode;
            Timer = timer;
            Reason = reason ?? string.Empty;
        }

        public EnemyAiMode Mode { get; }

        public int Timer { get; }

        public string Reason { get; }
    }
}

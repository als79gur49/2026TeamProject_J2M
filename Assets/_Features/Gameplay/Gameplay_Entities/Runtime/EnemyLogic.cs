using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;

namespace Game.Feature.Gameplay.Entities
{
    public sealed class EnemyLogic : IEnemyAiStateLogic, IMovementEntityLogic, IAttackEntityLogic, IEntityLogicSourceBinding
    {
        private readonly int _entityId;
        private readonly EnemyAiConfig _config;

        public EnemyLogic(int entityId)
            : this(entityId, EnemyAiConfig.CreateDefaultMelee())
        {
        }

        public EnemyLogic(int entityId, EnemyAiConfig config)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Enemy logic requires a positive entity ID.");
            }

            if (config.SenseRange <= 0)
            {
                throw new ArgumentException("Enemy logic requires a config with a positive sense range.", nameof(config));
            }

            if (config.AttackRange <= 0)
            {
                throw new ArgumentException("Enemy logic requires a config with a positive attack range.", nameof(config));
            }

            if (config.RecoverTicks < 0)
            {
                throw new ArgumentException("Enemy logic requires a config with a non-negative recover tick count.", nameof(config));
            }

            _entityId = entityId;
            _config = config;
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

            var decision = EnemyAiStateResolver.Resolve(snapshot, source, _config, stage);
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
                    if (EnemyMovementPolicy.TryBuildPatrolMove(snapshot, source, _config, out var patrolIntent))
                    {
                        buffer.Add(patrolIntent);
                    }

                    return;

                case EnemyAiMode.Chase:
                    if (!EnemyTargetSelector.TryFindNearestOpponent(snapshot, source, _config.SenseRange, out var chaseTarget))
                    {
                        return;
                    }

                    if (EnemyMovementPolicy.TryBuildChaseMove(snapshot, source, chaseTarget, _config, out var chaseIntent))
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
                !EnemyTargetSelector.TryFindNearestOpponent(snapshot, source, _config.SenseRange, out var target) ||
                !EnemyCombatPolicy.TryBuildAttackIntent(snapshot, source, target, _config, out var attackIntent))
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

    internal static class EnemyAiStateResolver
    {
        public static EnemyAiTransitionDecision Resolve(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiConfig config,
            EnemyAiTransitionStage stage)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (source.hp <= 0 || source.markedForDeath)
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Dead, 0, "Dead");
            }

            switch (stage)
            {
                case EnemyAiTransitionStage.BeforeMovement:
                    return ResolveBeforeMovement(snapshot, source, config);

                case EnemyAiTransitionStage.BeforeAttack:
                    return ResolveBeforeAttack(snapshot, source, config);

                case EnemyAiTransitionStage.AfterAttack:
                    return ResolveAfterAttack(source, config);

                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown enemy AI transition stage.");
            }
        }

        private static EnemyAiTransitionDecision ResolveBeforeMovement(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiConfig config)
        {
            switch (source.aiMode)
            {
                case EnemyAiMode.None:
                case EnemyAiMode.Dead:
                    return Keep(source, "Disabled");

                case EnemyAiMode.Patrol:
                case EnemyAiMode.Chase:
                case EnemyAiMode.Attack:
                    return TryResolveCombatReadiness(snapshot, source, config, EnemyAiMode.Patrol);

                case EnemyAiMode.Recover:
                    if (source.aiStateTimer > 0)
                    {
                        return new EnemyAiTransitionDecision(
                            EnemyAiMode.Recover,
                            source.aiStateTimer - 1,
                            "RecoverTick");
                    }

                    if (EnemyTargetSelector.TryFindNearestOpponent(snapshot, source, config.SenseRange, out _))
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
            in EnemyAiConfig config)
        {
            switch (source.aiMode)
            {
                case EnemyAiMode.Chase:
                case EnemyAiMode.Attack:
                    return TryResolveCombatReadiness(snapshot, source, config, EnemyAiMode.Patrol);

                default:
                    return Keep(source, "NoBeforeAttackTransition");
            }
        }

        private static EnemyAiTransitionDecision ResolveAfterAttack(
            in EntityState source,
            in EnemyAiConfig config)
        {
            if (source.aiMode != EnemyAiMode.Attack)
            {
                return Keep(source, "NoAfterAttackTransition");
            }

            return new EnemyAiTransitionDecision(
                EnemyAiMode.Recover,
                config.RecoverTicks,
                "AttackCommitted");
        }

        private static EnemyAiTransitionDecision TryResolveCombatReadiness(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiConfig config,
            EnemyAiMode patrolFallback)
        {
            if (!EnemyTargetSelector.TryFindNearestOpponent(snapshot, source, config.SenseRange, out var target))
            {
                return new EnemyAiTransitionDecision(patrolFallback, 0, "NoTarget");
            }

            if (EnemyCombatPolicy.IsTargetInAttackRange(source, target, config))
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

    internal readonly struct EnemyAiTransitionDecision
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

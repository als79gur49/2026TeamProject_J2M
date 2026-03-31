using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;

namespace Game.Feature.Gameplay.Entities
{
    public sealed class EnemyLogic : IMovementEntityLogic, IAttackEntityLogic, IEntityLogicSourceBinding
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
}

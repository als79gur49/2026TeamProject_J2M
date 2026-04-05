using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct EnemyViewPresentationState
    {
        public EnemyViewPresentationState(
            int entityId,
            int tickIndex,
            EnemyAiMode aiMode,
            EnemyActionKind activeActionKind,
            bool isMoving,
            bool startedWindupThisTick,
            bool executedThisTick,
            bool startedRecoveryThisTick,
            bool tookDamage,
            bool didDie)
        {
            EntityId = entityId;
            TickIndex = tickIndex;
            AiMode = aiMode;
            ActiveActionKind = activeActionKind;
            IsMoving = isMoving;
            StartedWindupThisTick = startedWindupThisTick;
            ExecutedThisTick = executedThisTick;
            StartedRecoveryThisTick = startedRecoveryThisTick;
            TookDamage = tookDamage;
            DidDie = didDie;
        }

        public int EntityId { get; }

        public int TickIndex { get; }

        public EnemyAiMode AiMode { get; }

        public EnemyActionKind ActiveActionKind { get; }

        public bool IsMoving { get; }

        public bool StartedWindupThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool StartedRecoveryThisTick { get; }

        public bool DidAttack => ExecutedThisTick;

        public bool TookDamage { get; }

        public bool DidDie { get; }
    }

    public sealed class EnemyViewPresentationMapper
    {
        private readonly HashSet<int> _attackingEntityIds = new();
        private readonly HashSet<int> _candidateEntityIds = new();
        private readonly Dictionary<int, TickEnemyActionPresentationSignal> _enemyActionSignalsByEntityId = new();
        private readonly HashSet<int> _damagedEntityIds = new();
        private readonly Dictionary<int, EntityState> _finalEntitiesById = new();
        private readonly HashSet<int> _movingEntityIds = new();
        private readonly HashSet<int> _removedEntityIds = new();

        public void Build(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId,
            Dictionary<int, EnemyViewPresentationState> buffer)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (viewsByEntityId == null)
            {
                throw new ArgumentNullException(nameof(viewsByEntityId));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            _candidateEntityIds.Clear();
            _movingEntityIds.Clear();
            _attackingEntityIds.Clear();
            _enemyActionSignalsByEntityId.Clear();
            _damagedEntityIds.Clear();
            _removedEntityIds.Clear();
            _finalEntitiesById.Clear();

            CacheFinalEntities(result.FinalEntities);
            CollectMovementSignals(result.PresentationData);
            CollectAttackSignals(result.AttackPhaseResult);
            CollectEnemyActionSignals(result.PresentationData);
            CollectRemovalSignals(result.CleanupPhaseResult);

            foreach (var entityId in _candidateEntityIds)
            {
                var hasFinalEntity = _finalEntitiesById.TryGetValue(entityId, out var finalEntity);
                var hasEnemyDriver = HasEnemyDriver(viewsByEntityId, entityId);
                if (!hasEnemyDriver && (!hasFinalEntity || !ShouldMap(finalEntity)))
                {
                    continue;
                }

                var didDie = _removedEntityIds.Contains(entityId) ||
                             (hasFinalEntity && (finalEntity.aiMode == EnemyAiMode.Dead || finalEntity.markedForDeath));
                var aiMode = hasFinalEntity
                    ? finalEntity.aiMode
                    : EnemyAiMode.Dead;
                var activeActionKind = EnemyActionKind.None;
                var startedWindupThisTick = false;
                var executedThisTick = _attackingEntityIds.Contains(entityId);
                var startedRecoveryThisTick = false;

                if (_enemyActionSignalsByEntityId.TryGetValue(entityId, out var actionSignal))
                {
                    activeActionKind = actionSignal.ActiveActionKind;
                    startedWindupThisTick = actionSignal.StartedThisTick && !actionSignal.ExecutedThisTick;
                    executedThisTick |= actionSignal.ExecutedThisTick;
                    startedRecoveryThisTick = actionSignal.StartedRecoveryThisTick;
                }

                buffer[entityId] = new EnemyViewPresentationState(
                    entityId,
                    result.TickIndex,
                    aiMode,
                    activeActionKind,
                    _movingEntityIds.Contains(entityId),
                    startedWindupThisTick,
                    executedThisTick,
                    startedRecoveryThisTick,
                    _damagedEntityIds.Contains(entityId),
                    didDie);
            }
        }

        public bool TryMapInitial(in EntityState entity, out EnemyViewPresentationState state)
        {
            if (!ShouldMap(entity))
            {
                state = default;
                return false;
            }

            state = new EnemyViewPresentationState(
                entity.entityId,
                tickIndex: -1,
                entity.aiMode,
                EnemyActionKind.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                tookDamage: false,
                didDie: entity.aiMode == EnemyAiMode.Dead || entity.markedForDeath);
            return true;
        }

        private void CacheFinalEntities(IReadOnlyList<EntityState> finalEntities)
        {
            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                _finalEntitiesById[entity.entityId] = entity;

                if (ShouldMap(entity))
                {
                    _candidateEntityIds.Add(entity.entityId);
                }
            }
        }

        private void CollectMovementSignals(TickPresentationData presentationData)
        {
            for (var i = 0; i < presentationData.EntityMotions.Count; i++)
            {
                var entityId = presentationData.EntityMotions[i].EntityId;
                _candidateEntityIds.Add(entityId);
                _movingEntityIds.Add(entityId);
            }
        }

        private void CollectAttackSignals(AttackPhaseResult attackPhaseResult)
        {
            var selectedGroups = attackPhaseResult.SelectedGroups;
            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                _candidateEntityIds.Add(group.SourceId);
                _attackingEntityIds.Add(group.SourceId);

                for (var damageIndex = 0; damageIndex < group.Damages.Count; damageIndex++)
                {
                    var targetId = group.Damages[damageIndex].TargetId;
                    _candidateEntityIds.Add(targetId);
                    _damagedEntityIds.Add(targetId);
                }
            }
        }

        private void CollectEnemyActionSignals(TickPresentationData presentationData)
        {
            var enemyActionSignals = presentationData.EnemyActionSignals;
            for (var i = 0; i < enemyActionSignals.Count; i++)
            {
                var signal = enemyActionSignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _enemyActionSignalsByEntityId[signal.EntityId] = signal;
            }
        }

        private void CollectRemovalSignals(CleanupPhaseResult cleanupPhaseResult)
        {
            var removedEntityIds = cleanupPhaseResult.RemovedEntityIds;
            for (var i = 0; i < removedEntityIds.Count; i++)
            {
                var entityId = removedEntityIds[i];
                _candidateEntityIds.Add(entityId);
                _removedEntityIds.Add(entityId);
            }
        }

        private static bool HasEnemyDriver(IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId, int entityId)
        {
            return viewsByEntityId.TryGetValue(entityId, out var view) &&
                   view != null &&
                   view.TryGetComponent<EnemyAnimatorDriver>(out _);
        }

        private static bool ShouldMap(in EntityState entity)
        {
            return entity.type == EntityType.Unit &&
                   entity.aiMode != EnemyAiMode.None;
        }
    }
}

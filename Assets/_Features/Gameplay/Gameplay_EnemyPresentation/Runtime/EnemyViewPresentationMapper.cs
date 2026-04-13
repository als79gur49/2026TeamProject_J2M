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
            : this(
                entityId,
                tickIndex,
                aiMode,
                activeActionKind,
                EnemyJumpPhase.None,
                isMoving,
                startedWindupThisTick,
                executedThisTick,
                startedRecoveryThisTick,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                tookDamage,
                didDie)
        {
        }

        public EnemyViewPresentationState(
            int entityId,
            int tickIndex,
            EnemyAiMode aiMode,
            EnemyActionKind activeActionKind,
            EnemyJumpPhase jumpPhase,
            bool isMoving,
            bool startedWindupThisTick,
            bool executedThisTick,
            bool startedRecoveryThisTick,
            bool startedJumpWindupThisTick,
            bool startedJumpAirborneThisTick,
            bool landedFromJumpThisTick,
            bool retryingJumpAirborneThisTick,
            bool tookDamage,
            bool didDie)
        {
            EntityId = entityId;
            TickIndex = tickIndex;
            AiMode = aiMode;
            ActiveActionKind = activeActionKind;
            JumpPhase = jumpPhase;
            IsMoving = isMoving;
            StartedWindupThisTick = startedWindupThisTick;
            ExecutedThisTick = executedThisTick;
            StartedRecoveryThisTick = startedRecoveryThisTick;
            StartedJumpWindupThisTick = startedJumpWindupThisTick;
            StartedJumpAirborneThisTick = startedJumpAirborneThisTick;
            LandedFromJumpThisTick = landedFromJumpThisTick;
            RetryingJumpAirborneThisTick = retryingJumpAirborneThisTick;
            TookDamage = tookDamage;
            DidDie = didDie;
        }

        public int EntityId { get; }

        public int TickIndex { get; }

        public EnemyAiMode AiMode { get; }

        public EnemyActionKind ActiveActionKind { get; }

        public EnemyJumpPhase JumpPhase { get; }

        public bool IsMoving { get; }

        public bool StartedWindupThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool StartedRecoveryThisTick { get; }

        public bool StartedJumpWindupThisTick { get; }

        public bool StartedJumpAirborneThisTick { get; }

        public bool LandedFromJumpThisTick { get; }

        public bool RetryingJumpAirborneThisTick { get; }

        public bool DidAttack => ExecutedThisTick;

        public bool TookDamage { get; }

        public bool DidDie { get; }
    }

    public sealed class EnemyViewPresentationMapper
    {
        private readonly HashSet<int> _candidateEntityIds = new();
        private readonly Dictionary<int, TickEnemyActionPresentationSignal> _enemyActionSignalsByEntityId = new();
        private readonly Dictionary<int, TickEnemyDamagePresentationSignal> _enemyDamageSignalsByEntityId = new();
        private readonly Dictionary<int, TickEnemyJumpPresentationSignal> _enemyJumpSignalsByEntityId = new();
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
            _enemyActionSignalsByEntityId.Clear();
            _enemyDamageSignalsByEntityId.Clear();
            _enemyJumpSignalsByEntityId.Clear();
            _removedEntityIds.Clear();
            _finalEntitiesById.Clear();

            CacheFinalEntities(result.FinalEntities);
            CollectMovementSignals(result.PresentationData);
            CollectEnemyActionSignals(result.PresentationData);
            CollectEnemyDamageSignals(result.PresentationData);
            CollectEnemyJumpSignals(result.PresentationData);
            CollectRemovalSignals(result.PresentationData);

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
                var jumpPhase = EnemyJumpPhase.None;
                var startedWindupThisTick = false;
                var executedThisTick = false;
                var startedRecoveryThisTick = false;
                var startedJumpWindupThisTick = false;
                var startedJumpAirborneThisTick = false;
                var landedFromJumpThisTick = false;
                var retryingJumpAirborneThisTick = false;
                var tookDamageThisTick = false;

                if (_enemyActionSignalsByEntityId.TryGetValue(entityId, out var actionSignal))
                {
                    activeActionKind = actionSignal.ActiveActionKind;
                    startedWindupThisTick = actionSignal.StartedThisTick && !actionSignal.ExecutedThisTick;
                    executedThisTick = actionSignal.ExecutedThisTick;
                    startedRecoveryThisTick = actionSignal.StartedRecoveryThisTick;
                }

                if (_enemyDamageSignalsByEntityId.TryGetValue(entityId, out var damageSignal))
                {
                    tookDamageThisTick = damageSignal.TookDamageThisTick;
                }

                if (_enemyJumpSignalsByEntityId.TryGetValue(entityId, out var jumpSignal))
                {
                    jumpPhase = jumpSignal.Phase;
                    startedJumpWindupThisTick = jumpSignal.StartedWindupThisTick;
                    startedJumpAirborneThisTick = jumpSignal.StartedAirborneThisTick;
                    landedFromJumpThisTick = jumpSignal.LandedThisTick;
                    retryingJumpAirborneThisTick = jumpSignal.RetryThisTick;
                }

                buffer[entityId] = new EnemyViewPresentationState(
                    entityId,
                    result.TickIndex,
                    aiMode,
                    activeActionKind,
                    jumpPhase,
                    _movingEntityIds.Contains(entityId),
                    startedWindupThisTick,
                    executedThisTick,
                    startedRecoveryThisTick,
                    startedJumpWindupThisTick,
                    startedJumpAirborneThisTick,
                    landedFromJumpThisTick,
                    retryingJumpAirborneThisTick,
                    tookDamageThisTick,
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
                EnemyJumpPhase.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
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

        private void CollectEnemyDamageSignals(TickPresentationData presentationData)
        {
            var enemyDamageSignals = presentationData.EnemyDamageSignals;
            for (var i = 0; i < enemyDamageSignals.Count; i++)
            {
                var signal = enemyDamageSignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _enemyDamageSignalsByEntityId[signal.EntityId] = signal;
            }
        }

        private void CollectEnemyJumpSignals(TickPresentationData presentationData)
        {
            var enemyJumpSignals = presentationData.EnemyJumpSignals;
            for (var i = 0; i < enemyJumpSignals.Count; i++)
            {
                var signal = enemyJumpSignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _enemyJumpSignalsByEntityId[signal.EntityId] = signal;
            }
        }

        private void CollectRemovalSignals(TickPresentationData presentationData)
        {
            var entityExitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < entityExitSignals.Count; i++)
            {
                var entityId = entityExitSignals[i].ExitedEntityId;
                _candidateEntityIds.Add(entityId);
                _removedEntityIds.Add(entityId);
            }

            var visibilityChanges = presentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.ChangeKind != TickVisibilityChangeKind.Remove)
                {
                    continue;
                }

                var entityId = change.EntityId;
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
            return EntityRolePolicy.IsEnemyUnit(entity);
        }
    }
}

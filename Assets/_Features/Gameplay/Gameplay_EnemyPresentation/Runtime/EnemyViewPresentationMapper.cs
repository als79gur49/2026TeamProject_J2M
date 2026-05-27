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
                EnemyChargePhase.None,
                isMoving,
                startedWindupThisTick,
                executedThisTick,
                startedRecoveryThisTick,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: false,
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
            : this(
                entityId,
                tickIndex,
                aiMode,
                activeActionKind,
                jumpPhase,
                EnemyChargePhase.None,
                isMoving,
                startedWindupThisTick,
                executedThisTick,
                startedRecoveryThisTick,
                startedJumpWindupThisTick,
                startedJumpAirborneThisTick,
                landedFromJumpThisTick,
                retryingJumpAirborneThisTick,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: false,
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
            EnemyChargePhase chargePhase,
            bool isMoving,
            bool startedWindupThisTick,
            bool executedThisTick,
            bool startedRecoveryThisTick,
            bool startedJumpWindupThisTick,
            bool startedJumpAirborneThisTick,
            bool landedFromJumpThisTick,
            bool retryingJumpAirborneThisTick,
            bool startedChargeWindupThisTick,
            bool startedChargeActiveThisTick,
            bool startedChargeRecoverThisTick,
            bool tookDamage,
            bool didDie,
            TickEnemyJumpPresentationOutcome jumpOutcome = TickEnemyJumpPresentationOutcome.None,
            EnemyGlidePhase glidePhase = EnemyGlidePhase.Ready,
            bool startedGlideWindupThisTick = false,
            bool startedGlideActiveThisTick = false,
            bool startedGlideRecoverThisTick = false,
            EnemyUtilityPresentationKind utilityPresentationKind = EnemyUtilityPresentationKind.None,
            bool startedUtilityWindupThisTick = false,
            EnemyUtilityEffectPhase utilityPhase = EnemyUtilityEffectPhase.None,
            bool startedUtilityRecoverThisTick = false,
            int utilityEffectIndex = 0,
            int utilityActivationSequence = 0,
            bool utilityCanceledThisTick = false)
        {
            EntityId = entityId;
            TickIndex = tickIndex;
            AiMode = aiMode;
            ActiveActionKind = activeActionKind;
            JumpPhase = jumpPhase;
            JumpOutcome = jumpOutcome;
            ChargePhase = chargePhase;
            IsMoving = isMoving;
            StartedWindupThisTick = startedWindupThisTick;
            ExecutedThisTick = executedThisTick;
            StartedRecoveryThisTick = startedRecoveryThisTick;
            StartedJumpWindupThisTick = startedJumpWindupThisTick;
            StartedJumpAirborneThisTick = startedJumpAirborneThisTick;
            LandedFromJumpThisTick = landedFromJumpThisTick;
            RetryingJumpAirborneThisTick = retryingJumpAirborneThisTick;
            StartedChargeWindupThisTick = startedChargeWindupThisTick;
            StartedChargeActiveThisTick = startedChargeActiveThisTick;
            StartedChargeRecoverThisTick = startedChargeRecoverThisTick;
            GlidePhase = glidePhase;
            StartedGlideWindupThisTick = startedGlideWindupThisTick;
            StartedGlideActiveThisTick = startedGlideActiveThisTick;
            StartedGlideRecoverThisTick = startedGlideRecoverThisTick;
            UtilityPresentationKind = utilityPresentationKind;
            StartedUtilityWindupThisTick = startedUtilityWindupThisTick;
            UtilityPhase = utilityPhase;
            StartedUtilityRecoverThisTick = startedUtilityRecoverThisTick;
            UtilityEffectIndex = utilityEffectIndex;
            UtilityActivationSequence = utilityActivationSequence;
            UtilityCanceledThisTick = utilityCanceledThisTick;
            TookDamage = tookDamage;
            DidDie = didDie;
        }

        public int EntityId { get; }

        public int TickIndex { get; }

        public EnemyAiMode AiMode { get; }

        public EnemyActionKind ActiveActionKind { get; }

        public EnemyJumpPhase JumpPhase { get; }

        public TickEnemyJumpPresentationOutcome JumpOutcome { get; }

        public EnemyChargePhase ChargePhase { get; }

        public bool IsMoving { get; }

        public bool StartedWindupThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool StartedRecoveryThisTick { get; }

        public bool StartedJumpWindupThisTick { get; }

        public bool StartedJumpAirborneThisTick { get; }

        public bool LandedFromJumpThisTick { get; }

        public bool RetryingJumpAirborneThisTick { get; }

        public bool StartedChargeWindupThisTick { get; }

        public bool StartedChargeActiveThisTick { get; }

        public bool StartedChargeRecoverThisTick { get; }

        public EnemyGlidePhase GlidePhase { get; }

        public bool StartedGlideWindupThisTick { get; }

        public bool StartedGlideActiveThisTick { get; }

        public bool StartedGlideRecoverThisTick { get; }

        public EnemyUtilityPresentationKind UtilityPresentationKind { get; }

        public bool StartedUtilityWindupThisTick { get; }

        public EnemyUtilityEffectPhase UtilityPhase { get; }

        public bool StartedUtilityRecoverThisTick { get; }

        public int UtilityEffectIndex { get; }

        public int UtilityActivationSequence { get; }

        public bool UtilityCanceledThisTick { get; }

        public bool DidAttack => ExecutedThisTick;

        public bool TookDamage { get; }

        public bool DidDie { get; }

        public EnemyViewPresentationState WithDidDie(bool didDie)
        {
            return new EnemyViewPresentationState(
                EntityId,
                TickIndex,
                AiMode,
                ActiveActionKind,
                JumpPhase,
                ChargePhase,
                IsMoving,
                StartedWindupThisTick,
                ExecutedThisTick,
                StartedRecoveryThisTick,
                StartedJumpWindupThisTick,
                StartedJumpAirborneThisTick,
                LandedFromJumpThisTick,
                RetryingJumpAirborneThisTick,
                StartedChargeWindupThisTick,
                StartedChargeActiveThisTick,
                StartedChargeRecoverThisTick,
                TookDamage,
                didDie,
                JumpOutcome,
                GlidePhase,
                StartedGlideWindupThisTick,
                StartedGlideActiveThisTick,
                StartedGlideRecoverThisTick,
                UtilityPresentationKind,
                StartedUtilityWindupThisTick,
                UtilityPhase,
                StartedUtilityRecoverThisTick,
                UtilityEffectIndex,
                UtilityActivationSequence,
                UtilityCanceledThisTick);
        }

        public EnemyViewPresentationState WithJumpLandingCompletionHold()
        {
            return new EnemyViewPresentationState(
                EntityId,
                TickIndex,
                AiMode,
                ActiveActionKind,
                EnemyJumpPhase.Airborne,
                ChargePhase,
                IsMoving,
                StartedWindupThisTick,
                ExecutedThisTick,
                StartedRecoveryThisTick,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                StartedChargeWindupThisTick,
                StartedChargeActiveThisTick,
                StartedChargeRecoverThisTick,
                TookDamage,
                DidDie,
                JumpOutcome,
                GlidePhase,
                StartedGlideWindupThisTick,
                StartedGlideActiveThisTick,
                StartedGlideRecoverThisTick,
                UtilityPresentationKind,
                StartedUtilityWindupThisTick,
                UtilityPhase,
                StartedUtilityRecoverThisTick,
                UtilityEffectIndex,
                UtilityActivationSequence,
                UtilityCanceledThisTick);
        }

        public EnemyViewPresentationState WithJumpLandingCompletionSettled()
        {
            return new EnemyViewPresentationState(
                EntityId,
                TickIndex,
                AiMode,
                ActiveActionKind,
                EnemyJumpPhase.None,
                ChargePhase,
                IsMoving,
                StartedWindupThisTick,
                ExecutedThisTick,
                StartedRecoveryThisTick,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                StartedChargeWindupThisTick,
                StartedChargeActiveThisTick,
                StartedChargeRecoverThisTick,
                TookDamage,
                DidDie,
                JumpOutcome,
                GlidePhase,
                StartedGlideWindupThisTick,
                StartedGlideActiveThisTick,
                StartedGlideRecoverThisTick,
                UtilityPresentationKind,
                StartedUtilityWindupThisTick,
                UtilityPhase,
                StartedUtilityRecoverThisTick,
                UtilityEffectIndex,
                UtilityActivationSequence,
                UtilityCanceledThisTick);
        }
    }

    public sealed class EnemyViewPresentationMapper
    {
        private readonly HashSet<int> _candidateEntityIds = new();
        private readonly Dictionary<int, TickEnemyActionPresentationSignal> _enemyActionSignalsByEntityId = new();
        private readonly Dictionary<int, TickEnemyDamagePresentationSignal> _enemyDamageSignalsByEntityId = new();
        private readonly Dictionary<int, TickEnemyJumpPresentationSignal> _enemyJumpSignalsByEntityId = new();
        private readonly Dictionary<int, TickEnemyChargePresentationSignal> _enemyChargeSignalsByEntityId = new();
        private readonly Dictionary<int, TickEnemyGlidePresentationSignal> _enemyGlideSignalsByEntityId = new();
        private readonly Dictionary<int, TickEnemyUtilityPresentationSignal> _enemyUtilitySignalsByEntityId = new();
        private readonly Dictionary<int, TickEnemyUtilityPhasePresentationState> _enemyUtilityPhaseStatesByEntityId = new();
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
            _enemyChargeSignalsByEntityId.Clear();
            _enemyGlideSignalsByEntityId.Clear();
            _enemyUtilitySignalsByEntityId.Clear();
            _enemyUtilityPhaseStatesByEntityId.Clear();
            _removedEntityIds.Clear();
            _finalEntitiesById.Clear();

            CacheFinalEntities(result.FinalEntities);
            CollectMovementSignals(result.PresentationData);
            CollectEnemyActionSignals(result.PresentationData);
            CollectEnemyDamageSignals(result.PresentationData);
            CollectEnemyJumpSignals(result.PresentationData);
            CollectEnemyChargeSignals(result.PresentationData);
            CollectEnemyGlideSignals(result.PresentationData);
            CollectEnemyUtilitySignals(result.PresentationData);
            CollectEnemyUtilityPhaseStates(result.PresentationData);
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
                var jumpOutcome = TickEnemyJumpPresentationOutcome.None;
                var chargePhase = EnemyChargePhase.None;
                var startedChargeWindupThisTick = false;
                var startedChargeActiveThisTick = false;
                var startedChargeRecoverThisTick = false;
                var glidePhase = EnemyGlidePhase.Ready;
                var startedGlideWindupThisTick = false;
                var startedGlideActiveThisTick = false;
                var startedGlideRecoverThisTick = false;
                var utilityPresentationKind = EnemyUtilityPresentationKind.None;
                var startedUtilityWindupThisTick = false;
                var utilityPhase = EnemyUtilityEffectPhase.None;
                var startedUtilityRecoverThisTick = false;
                var utilityCanceledThisTick = false;
                var utilityEffectIndex = 0;
                var utilityActivationSequence = 0;
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
                    jumpOutcome = jumpSignal.Outcome;
                }

                if (_enemyChargeSignalsByEntityId.TryGetValue(entityId, out var chargeSignal))
                {
                    chargePhase = chargeSignal.Phase;
                    startedChargeWindupThisTick = chargeSignal.StartedWindupThisTick;
                    startedChargeActiveThisTick = chargeSignal.StartedActiveThisTick;
                    startedChargeRecoverThisTick = chargeSignal.StartedRecoverThisTick;
                    startedWindupThisTick |= chargeSignal.StartedWindupThisTick;
                    startedRecoveryThisTick |= chargeSignal.StartedRecoverThisTick;
                }

                if (_enemyGlideSignalsByEntityId.TryGetValue(entityId, out var glideSignal))
                {
                    glidePhase = glideSignal.Phase;
                    startedGlideWindupThisTick = glideSignal.Phase == EnemyGlidePhase.Windup &&
                                                glideSignal.PhaseElapsedTicks == 0;
                    startedGlideActiveThisTick = glideSignal.Phase == EnemyGlidePhase.Active &&
                                                glideSignal.PhaseElapsedTicks == 0;
                    startedGlideRecoverThisTick = glideSignal.Phase == EnemyGlidePhase.Recovery &&
                                                 glideSignal.PhaseElapsedTicks == 0;
                    startedWindupThisTick |= startedGlideWindupThisTick;
                    startedRecoveryThisTick |= startedGlideRecoverThisTick;
                }

                if (_enemyUtilitySignalsByEntityId.TryGetValue(entityId, out var utilitySignal))
                {
                    utilityPresentationKind = utilitySignal.Kind;
                    startedUtilityWindupThisTick = utilitySignal.Phase == EnemyUtilityPresentationPhase.WindupStarted;
                    startedUtilityRecoverThisTick = utilitySignal.Phase == EnemyUtilityPresentationPhase.RecoverStarted;
                    utilityCanceledThisTick = utilitySignal.Phase == EnemyUtilityPresentationPhase.Canceled;
                    startedRecoveryThisTick |= startedUtilityRecoverThisTick;
                    utilityEffectIndex = utilitySignal.EffectIndex;
                    utilityActivationSequence = utilitySignal.ActivationSequence;
                }

                if (_enemyUtilityPhaseStatesByEntityId.TryGetValue(entityId, out var utilityPhaseState))
                {
                    utilityPresentationKind = utilityPhaseState.Kind;
                    utilityPhase = utilityPhaseState.Phase;
                    utilityEffectIndex = utilityPhaseState.EffectIndex;
                    utilityActivationSequence = utilityPhaseState.ActivationSequence;
                }

                buffer[entityId] = new EnemyViewPresentationState(
                    entityId,
                    result.TickIndex,
                    aiMode,
                    activeActionKind,
                    jumpPhase,
                    chargePhase,
                    _movingEntityIds.Contains(entityId),
                    startedWindupThisTick,
                    executedThisTick,
                    startedRecoveryThisTick,
                    startedJumpWindupThisTick,
                    startedJumpAirborneThisTick,
                    landedFromJumpThisTick,
                    retryingJumpAirborneThisTick,
                    startedChargeWindupThisTick,
                    startedChargeActiveThisTick,
                    startedChargeRecoverThisTick,
                    tookDamageThisTick,
                    didDie,
                    jumpOutcome,
                    glidePhase,
                    startedGlideWindupThisTick,
                    startedGlideActiveThisTick,
                    startedGlideRecoverThisTick,
                    utilityPresentationKind,
                    startedUtilityWindupThisTick,
                    utilityPhase,
                    startedUtilityRecoverThisTick,
                    utilityEffectIndex,
                    utilityActivationSequence,
                    utilityCanceledThisTick);
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
                EnemyChargePhase.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: false,
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

        private void CollectEnemyChargeSignals(TickPresentationData presentationData)
        {
            var enemyChargeSignals = presentationData.EnemyChargeSignals;
            for (var i = 0; i < enemyChargeSignals.Count; i++)
            {
                var signal = enemyChargeSignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _enemyChargeSignalsByEntityId[signal.EntityId] = signal;
            }
        }

        private void CollectEnemyGlideSignals(TickPresentationData presentationData)
        {
            var enemyGlideSignals = presentationData.EnemyGlideSignals;
            for (var i = 0; i < enemyGlideSignals.Count; i++)
            {
                var signal = enemyGlideSignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _enemyGlideSignalsByEntityId[signal.EntityId] = signal;
            }
        }

        private void CollectEnemyUtilitySignals(TickPresentationData presentationData)
        {
            var enemyUtilitySignals = presentationData.EnemyUtilitySignals;
            for (var i = 0; i < enemyUtilitySignals.Count; i++)
            {
                var signal = enemyUtilitySignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _enemyUtilitySignalsByEntityId[signal.EntityId] = signal;
            }
        }

        private void CollectEnemyUtilityPhaseStates(TickPresentationData presentationData)
        {
            var enemyUtilityPhaseStates = presentationData.EnemyUtilityPhaseStates;
            for (var i = 0; i < enemyUtilityPhaseStates.Count; i++)
            {
                var state = enemyUtilityPhaseStates[i];
                _candidateEntityIds.Add(state.EntityId);
                if (!_enemyUtilityPhaseStatesByEntityId.TryGetValue(state.EntityId, out var current) ||
                    GetEnemyUtilityPhasePriority(state.Phase) > GetEnemyUtilityPhasePriority(current.Phase))
                {
                    _enemyUtilityPhaseStatesByEntityId[state.EntityId] = state;
                }
            }
        }

        private static int GetEnemyUtilityPhasePriority(EnemyUtilityEffectPhase phase)
        {
            switch (phase)
            {
                case EnemyUtilityEffectPhase.Recover:
                    return 3;

                case EnemyUtilityEffectPhase.Windup:
                    return 2;

                case EnemyUtilityEffectPhase.Active:
                    return 1;

                default:
                    return 0;
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

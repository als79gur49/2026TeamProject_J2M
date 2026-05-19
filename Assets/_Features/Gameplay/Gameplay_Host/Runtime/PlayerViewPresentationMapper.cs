using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Host
{
    public enum PlayerViewAnimationState
    {
        Idle = 0,
        WalkLoop = 1,
        Push = 2,
        Flip = 3,
        Death = 4,
    }

    public readonly struct PlayerViewPresentationState
    {
        public PlayerViewPresentationState(
            int entityId,
            int tickIndex,
            PlayerActionKind activeActionKind,
            int activeActionSequence,
            bool startedThisTick,
            bool executedThisTick,
            bool completedThisTick,
            bool canceledThisTick,
            bool isRecoveryPhase = false,
            bool didDie = false,
            bool didDieThisTick = false,
            bool tookDamageThisTick = false,
            int actionPlanId = 0,
            TickPlayerFlipOutcomeKind flipOutcome = TickPlayerFlipOutcomeKind.None,
            bool hasFlipImpactContactTiming = false,
            int flipTargetBoxEntityId = 0,
            int deathSourceEntityId = 0,
            bool resolvedDamageSourceAvailable = false,
            int damageAmountAtFatalHit = 0,
            DeathDirectionHintKind deathDirectionHintKind = DeathDirectionHintKind.Unknown,
            Direction deathFallbackFacing = Direction.None,
            bool hasActionAttempt = false,
            PlayerActionKind actionAttemptKind = PlayerActionKind.None,
            Direction actionAttemptDirection = Direction.None,
            PlayerActionAttemptFeedbackKind actionAttemptFeedbackKind = PlayerActionAttemptFeedbackKind.None)
            : this(
                entityId,
                tickIndex,
                activeActionKind,
                activeActionSequence,
                startedThisTick,
                executedThisTick,
                completedThisTick,
                canceledThisTick,
                shouldPlayWalkLoop: false,
                isRecoveryPhase: isRecoveryPhase,
                didDie: didDie,
                didDieThisTick: didDieThisTick,
                tookDamageThisTick: tookDamageThisTick,
                actionPlanId: actionPlanId,
                flipOutcome: flipOutcome,
                hasFlipImpactContactTiming: hasFlipImpactContactTiming,
                flipTargetBoxEntityId: flipTargetBoxEntityId,
                deathSourceEntityId: deathSourceEntityId,
                resolvedDamageSourceAvailable: resolvedDamageSourceAvailable,
                damageAmountAtFatalHit: damageAmountAtFatalHit,
                deathDirectionHintKind: deathDirectionHintKind,
                deathFallbackFacing: deathFallbackFacing,
                hasActionAttempt: hasActionAttempt,
                actionAttemptKind: actionAttemptKind,
                actionAttemptDirection: actionAttemptDirection,
                actionAttemptFeedbackKind: actionAttemptFeedbackKind)
        {
        }

        public PlayerViewPresentationState(
            int entityId,
            int tickIndex,
            PlayerActionKind activeActionKind,
            int activeActionSequence,
            bool startedThisTick,
            bool executedThisTick,
            bool completedThisTick,
            bool canceledThisTick,
            bool shouldPlayWalkLoop,
            bool isRecoveryPhase = false,
            bool didDie = false,
            bool didDieThisTick = false,
            bool tookDamageThisTick = false,
            int actionPlanId = 0,
            TickPlayerFlipOutcomeKind flipOutcome = TickPlayerFlipOutcomeKind.None,
            bool hasFlipImpactContactTiming = false,
            int flipTargetBoxEntityId = 0,
            int deathSourceEntityId = 0,
            bool resolvedDamageSourceAvailable = false,
            int damageAmountAtFatalHit = 0,
            DeathDirectionHintKind deathDirectionHintKind = DeathDirectionHintKind.Unknown,
            Direction deathFallbackFacing = Direction.None,
            bool hasActionAttempt = false,
            PlayerActionKind actionAttemptKind = PlayerActionKind.None,
            Direction actionAttemptDirection = Direction.None,
            PlayerActionAttemptFeedbackKind actionAttemptFeedbackKind = PlayerActionAttemptFeedbackKind.None)
        {
            EntityId = entityId;
            TickIndex = tickIndex;
            ActiveActionKind = activeActionKind;
            ActiveActionSequence = activeActionSequence;
            IsRecoveryPhase = isRecoveryPhase;
            StartedThisTick = startedThisTick;
            ExecutedThisTick = executedThisTick;
            CompletedThisTick = completedThisTick;
            CanceledThisTick = canceledThisTick;
            ShouldPlayWalkLoop = shouldPlayWalkLoop;
            DidDie = didDie;
            DidDieThisTick = didDieThisTick;
            TookDamageThisTick = tookDamageThisTick;
            ActionPlanId = actionPlanId;
            FlipOutcome = flipOutcome;
            HasFlipImpactContactTiming = hasFlipImpactContactTiming;
            FlipTargetBoxEntityId = flipTargetBoxEntityId;
            DeathSourceEntityId = deathSourceEntityId;
            ResolvedDamageSourceAvailable = resolvedDamageSourceAvailable;
            DamageAmountAtFatalHit = damageAmountAtFatalHit;
            DeathDirectionHintKind = deathDirectionHintKind;
            DeathFallbackFacing = deathFallbackFacing;
            HasActionAttempt = hasActionAttempt;
            ActionAttemptKind = actionAttemptKind;
            ActionAttemptDirection = actionAttemptDirection;
            ActionAttemptFeedbackKind = actionAttemptFeedbackKind;
        }

        public int EntityId { get; }

        public int TickIndex { get; }

        public PlayerActionKind ActiveActionKind { get; }

        public int ActiveActionSequence { get; }

        public bool IsRecoveryPhase { get; }

        public bool StartedThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool CompletedThisTick { get; }

        public bool CanceledThisTick { get; }

        public bool ShouldPlayWalkLoop { get; }

        public bool DidDie { get; }

        public bool DidDieThisTick { get; }

        public bool TookDamageThisTick { get; }

        public int ActionPlanId { get; }

        public TickPlayerFlipOutcomeKind FlipOutcome { get; }

        public bool HasFlipImpactContactTiming { get; }

        public int FlipTargetBoxEntityId { get; }

        public int DeathSourceEntityId { get; }

        public bool ResolvedDamageSourceAvailable { get; }

        public int DamageAmountAtFatalHit { get; }

        public DeathDirectionHintKind DeathDirectionHintKind { get; }

        public Direction DeathFallbackFacing { get; }

        public bool HasActionAttempt { get; }

        public PlayerActionKind ActionAttemptKind { get; }

        public Direction ActionAttemptDirection { get; }

        public PlayerActionAttemptFeedbackKind ActionAttemptFeedbackKind { get; }
    }

    public sealed class PlayerViewPresentationMapper
    {
        private readonly HashSet<int> _candidateEntityIds = new();
        private readonly Dictionary<int, EntityState> _finalEntitiesById = new();
        private readonly HashSet<int> _removedEntityIds = new();
        private readonly Dictionary<int, TickPlayerActionPresentationSignal> _signalsByEntityId = new();
        private readonly Dictionary<int, TickPlayerDamagePresentationSignal> _damageSignalsByEntityId = new();
        private readonly Dictionary<int, TickPlayerDeathPresentationSignal> _deathSignalsByEntityId = new();
        private readonly Dictionary<int, FlipImpactPresentationSignal> _flipImpactSignalsByActionPlanId = new();
        private readonly Dictionary<int, TickPlayerLocomotionPresentationSignal> _locomotionSignalsByEntityId = new();
        private readonly Dictionary<int, TickPlayerActionAttemptPresentationSignal> _attemptSignalsByEntityId = new();

        public void Build(
            TickResult result,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId,
            Dictionary<int, PlayerViewPresentationState> buffer)
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
            _finalEntitiesById.Clear();
            _removedEntityIds.Clear();
            _signalsByEntityId.Clear();
            _damageSignalsByEntityId.Clear();
            _deathSignalsByEntityId.Clear();
            _flipImpactSignalsByActionPlanId.Clear();
            _locomotionSignalsByEntityId.Clear();
            _attemptSignalsByEntityId.Clear();

            CacheFinalEntities(result.FinalEntities);
            CollectRemovalSignals(result.PresentationData);

            foreach (var pair in viewsByEntityId)
            {
                if (pair.Value != null &&
                    pair.Value.TryGetComponent<PlayerAnimatorDriver>(out _))
                {
                    _candidateEntityIds.Add(pair.Key);
                }
            }

            var playerActionSignals = result.PresentationData.PlayerActionSignals;
            for (var i = 0; i < playerActionSignals.Count; i++)
            {
                var signal = playerActionSignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _signalsByEntityId[signal.EntityId] = signal;
            }

            var playerLocomotionSignals = result.PresentationData.PlayerLocomotionSignals;
            for (var i = 0; i < playerLocomotionSignals.Count; i++)
            {
                var signal = playerLocomotionSignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _locomotionSignalsByEntityId[signal.EntityId] = signal;
            }

            var playerActionAttemptSignals = result.PresentationData.PlayerActionAttemptSignals;
            for (var i = 0; i < playerActionAttemptSignals.Count; i++)
            {
                var signal = playerActionAttemptSignals[i];
                if (!signal.EmitsVisualFeedback)
                {
                    continue;
                }

                _candidateEntityIds.Add(signal.EntityId);
                _attemptSignalsByEntityId[signal.EntityId] = signal;
            }

            var playerDamageSignals = result.PresentationData.PlayerDamageSignals;
            for (var i = 0; i < playerDamageSignals.Count; i++)
            {
                var signal = playerDamageSignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _damageSignalsByEntityId[signal.EntityId] = signal;
            }

            var playerDeathSignals = result.PresentationData.PlayerDeathSignals;
            for (var i = 0; i < playerDeathSignals.Count; i++)
            {
                var signal = playerDeathSignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _deathSignalsByEntityId[signal.EntityId] = signal;
            }

            var flipImpactSignals = result.PresentationData.FlipImpactSignals;
            for (var i = 0; i < flipImpactSignals.Count; i++)
            {
                var signal = flipImpactSignals[i];
                if (signal.SourceActionPlanId <= 0 ||
                    _flipImpactSignalsByActionPlanId.ContainsKey(signal.SourceActionPlanId))
                {
                    continue;
                }

                _flipImpactSignalsByActionPlanId[signal.SourceActionPlanId] = signal;
            }

            foreach (var entityId in _candidateEntityIds)
            {
                if (!HasPlayerDriver(viewsByEntityId, entityId))
                {
                    continue;
                }

                if (!_signalsByEntityId.TryGetValue(entityId, out var signal))
                {
                    signal = default;
                }

                var shouldPlayWalkLoop = _locomotionSignalsByEntityId.TryGetValue(entityId, out var locomotionSignal) &&
                                         locomotionSignal.ShouldPlayWalkLoop;
                var didDie = _removedEntityIds.Contains(entityId) ||
                             (_finalEntitiesById.TryGetValue(entityId, out var finalEntity) &&
                              (finalEntity.hp <= 0 || finalEntity.markedForDeath));
                var didDieThisTick = _deathSignalsByEntityId.TryGetValue(entityId, out var deathSignal) &&
                                     deathSignal.DidDieThisTick;
                var tookDamageThisTick = !didDie &&
                                         _damageSignalsByEntityId.TryGetValue(entityId, out var damageSignal) &&
                                         damageSignal.TookDamageThisTick;
                var hasActionAttempt = _attemptSignalsByEntityId.TryGetValue(entityId, out var attemptSignal);
                var flipOutcome = signal.FlipOutcome;
                var hasFlipImpactContactTiming = signal.HasFlipImpactContactTiming;
                var flipTargetBoxEntityId = signal.FlipTargetBoxEntityId;
                if (signal.ActiveActionKind == PlayerActionKind.Flip &&
                    signal.ActionPlanId > 0 &&
                    _flipImpactSignalsByActionPlanId.TryGetValue(signal.ActionPlanId, out var flipImpactSignal) &&
                    (flipImpactSignal.ActorEntityId <= 0 || flipImpactSignal.ActorEntityId == entityId) &&
                    (signal.FlipTargetBoxEntityId <= 0 || signal.FlipTargetBoxEntityId == flipImpactSignal.BoxEntityId))
                {
                    flipOutcome = flipImpactSignal.Disposition == FlipImpactPresentationDisposition.DestroySelf
                        ? TickPlayerFlipOutcomeKind.DestroySelf
                        : TickPlayerFlipOutcomeKind.Stay;
                    hasFlipImpactContactTiming = true;
                    flipTargetBoxEntityId = flipImpactSignal.BoxEntityId;
                }

                buffer[entityId] = new PlayerViewPresentationState(
                    entityId,
                    result.TickIndex,
                    signal.ActiveActionKind,
                    signal.ActiveActionSequence,
                    signal.StartedThisTick,
                    signal.ExecutedThisTick,
                    signal.CompletedThisTick,
                    signal.CanceledThisTick,
                    shouldPlayWalkLoop,
                    signal.IsRecoveryPhase,
                    didDie,
                    didDieThisTick,
                    tookDamageThisTick,
                    signal.ActionPlanId,
                    flipOutcome,
                    hasFlipImpactContactTiming,
                    flipTargetBoxEntityId,
                    deathSignal.SourceEntityId,
                    deathSignal.ResolvedDamageSourceAvailable,
                    deathSignal.DamageAmountAtFatalHit,
                    deathSignal.DeathDirectionHintKind,
                    deathSignal.FallbackFacing,
                    hasActionAttempt,
                    hasActionAttempt ? attemptSignal.ActionKind : PlayerActionKind.None,
                    hasActionAttempt ? attemptSignal.Direction : Direction.None,
                    hasActionAttempt ? attemptSignal.FeedbackKind : PlayerActionAttemptFeedbackKind.None);
            }
        }

        public static PlayerViewPresentationState CreateInitial(int entityId)
        {
            return new PlayerViewPresentationState(
                entityId,
                tickIndex: -1,
                PlayerActionKind.None,
                activeActionSequence: 0,
                startedThisTick: false,
                executedThisTick: false,
                completedThisTick: false,
                canceledThisTick: false,
                shouldPlayWalkLoop: false,
                isRecoveryPhase: false,
                didDie: false,
                didDieThisTick: false,
                tookDamageThisTick: false,
                actionPlanId: 0,
                flipOutcome: TickPlayerFlipOutcomeKind.None,
                hasFlipImpactContactTiming: false,
                flipTargetBoxEntityId: 0,
                deathSourceEntityId: 0,
                resolvedDamageSourceAvailable: false,
                damageAmountAtFatalHit: 0,
                deathDirectionHintKind: DeathDirectionHintKind.Unknown,
                deathFallbackFacing: Direction.None,
                hasActionAttempt: false,
                actionAttemptKind: PlayerActionKind.None,
                actionAttemptDirection: Direction.None,
                actionAttemptFeedbackKind: PlayerActionAttemptFeedbackKind.None);
        }

        private void CacheFinalEntities(IReadOnlyList<EntityState> finalEntities)
        {
            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                _finalEntitiesById[entity.entityId] = entity;
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

            var deathHoldSignals = presentationData.PlayerDeathHoldSignals;
            for (var i = 0; i < deathHoldSignals.Count; i++)
            {
                var entityId = deathHoldSignals[i].EntityId;
                _candidateEntityIds.Add(entityId);
                _removedEntityIds.Add(entityId);
            }
        }

        private static bool HasPlayerDriver(IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId, int entityId)
        {
            return viewsByEntityId.TryGetValue(entityId, out var view) &&
                   view != null &&
                   view.TryGetComponent<PlayerAnimatorDriver>(out _);
        }
    }
}

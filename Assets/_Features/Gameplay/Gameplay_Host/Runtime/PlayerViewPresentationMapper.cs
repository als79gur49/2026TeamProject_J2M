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
            bool tookDamageThisTick = false)
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
                tookDamageThisTick: tookDamageThisTick)
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
            bool tookDamageThisTick = false)
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
            TookDamageThisTick = tookDamageThisTick;
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

        public bool TookDamageThisTick { get; }
    }

    public sealed class PlayerViewPresentationMapper
    {
        private readonly HashSet<int> _candidateEntityIds = new();
        private readonly Dictionary<int, EntityState> _finalEntitiesById = new();
        private readonly HashSet<int> _removedEntityIds = new();
        private readonly Dictionary<int, TickPlayerActionPresentationSignal> _signalsByEntityId = new();
        private readonly Dictionary<int, TickPlayerDamagePresentationSignal> _damageSignalsByEntityId = new();
        private readonly Dictionary<int, TickPlayerLocomotionPresentationSignal> _locomotionSignalsByEntityId = new();

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
            _locomotionSignalsByEntityId.Clear();

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

            var playerDamageSignals = result.PresentationData.PlayerDamageSignals;
            for (var i = 0; i < playerDamageSignals.Count; i++)
            {
                var signal = playerDamageSignals[i];
                _candidateEntityIds.Add(signal.EntityId);
                _damageSignalsByEntityId[signal.EntityId] = signal;
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
                var tookDamageThisTick = !didDie &&
                                         _damageSignalsByEntityId.TryGetValue(entityId, out var damageSignal) &&
                                         damageSignal.TookDamageThisTick;

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
                    tookDamageThisTick);
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
                tookDamageThisTick: false);
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
        }

        private static bool HasPlayerDriver(IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId, int entityId)
        {
            return viewsByEntityId.TryGetValue(entityId, out var view) &&
                   view != null &&
                   view.TryGetComponent<PlayerAnimatorDriver>(out _);
        }
    }
}

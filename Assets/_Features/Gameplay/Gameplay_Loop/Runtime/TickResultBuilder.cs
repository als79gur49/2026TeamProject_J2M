using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class TickResultBuilder
    {
        public TickResultData Build(
            WorldSnapshot finalSnapshot,
            IReadOnlyList<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult)
        {
            if (finalSnapshot == null)
            {
                throw new ArgumentNullException(nameof(finalSnapshot));
            }

            if (pendingDelayedAttackEffects == null)
            {
                throw new ArgumentNullException(nameof(pendingDelayedAttackEffects));
            }

            if (movementPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(movementPhaseResult));
            }

            if (attackPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(attackPhaseResult));
            }

            if (cleanupPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(cleanupPhaseResult));
            }

            var finalEntities = new List<EntityState>();
            finalSnapshot.EnumerateEntitiesOrdered(finalEntities);

            var eventLog = new List<string>(
                movementPhaseResult.CommitEvents.Count +
                attackPhaseResult.EventLogEntries.Count +
                cleanupPhaseResult.RemovedEntityIds.Count +
                cleanupPhaseResult.TimerChanges.Count +
                cleanupPhaseResult.StateTransitions.Count);

            AddRange(eventLog, movementPhaseResult.CommitEvents);
            AddRange(eventLog, attackPhaseResult.EventLogEntries);

            for (var i = 0; i < cleanupPhaseResult.RemovedEntityIds.Count; i++)
            {
                eventLog.Add($"CleanupRemoved|E={cleanupPhaseResult.RemovedEntityIds[i]}");
            }

            AddRange(eventLog, cleanupPhaseResult.TimerChanges);
            AddRange(eventLog, cleanupPhaseResult.StateTransitions);

            return new TickResultData(finalEntities, pendingDelayedAttackEffects, eventLog);
        }

        private static void AddRange(List<string> destination, IReadOnlyList<string> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }
        }
    }

    internal sealed class TickResultData
    {
        private readonly ReadOnlyCollection<string> _eventLog;
        private readonly ReadOnlyCollection<EntityState> _finalEntities;
        private readonly ReadOnlyCollection<DelayedAttackEffectRecord> _pendingDelayedAttackEffects;

        public TickResultData(
            IEnumerable<EntityState> finalEntities,
            IEnumerable<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            IEnumerable<string> eventLog)
        {
            if (finalEntities == null)
            {
                throw new ArgumentNullException(nameof(finalEntities));
            }

            if (pendingDelayedAttackEffects == null)
            {
                throw new ArgumentNullException(nameof(pendingDelayedAttackEffects));
            }

            if (eventLog == null)
            {
                throw new ArgumentNullException(nameof(eventLog));
            }

            _finalEntities = new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities));
            _pendingDelayedAttackEffects = new ReadOnlyCollection<DelayedAttackEffectRecord>(new List<DelayedAttackEffectRecord>(pendingDelayedAttackEffects));
            _eventLog = new ReadOnlyCollection<string>(new List<string>(eventLog));
        }

        public IReadOnlyList<EntityState> FinalEntities => _finalEntities;

        public IReadOnlyList<DelayedAttackEffectRecord> PendingDelayedAttackEffects => _pendingDelayedAttackEffects;

        public IReadOnlyList<string> EventLog => _eventLog;
    }
}

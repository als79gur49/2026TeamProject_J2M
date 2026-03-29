using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Model.Groups;

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

            return new TickResultData(
                finalEntities,
                pendingDelayedAttackEffects,
                eventLog,
                BuildPresentationData(finalSnapshot, movementPhaseResult));
        }

        private static void AddRange(List<string> destination, IReadOnlyList<string> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }
        }

        private static TickPresentationData BuildPresentationData(
            WorldSnapshot finalSnapshot,
            MovementPhaseResult movementPhaseResult)
        {
            // Motion clips are derived from the committed final snapshot. Gameplay has already advanced to these cells.
            var entityMotions = new List<TickEntityMotion>();

            for (var groupIndex = 0; groupIndex < movementPhaseResult.SelectedGroups.Count; groupIndex++)
            {
                var group = movementPhaseResult.SelectedGroups[groupIndex];
                var motionKind = ResolveMotionKind(group.GroupKind);
                if (motionKind == TickEntityMotionKind.None)
                {
                    continue;
                }

                for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                {
                    var move = group.Moves[moveIndex];
                    if (!finalSnapshot.TryGetEntity(move.EntityId, out var entity) ||
                        entity.type != EntityType.Box ||
                        entity.boardPresence != EntityBoardPresence.Occupying)
                    {
                        continue;
                    }

                    entityMotions.Add(
                        new TickEntityMotion(
                            move.EntityId,
                            motionKind,
                            move.SourceCell,
                            move.DestinationCell));
                }
            }

            return entityMotions.Count == 0
                ? TickPresentationData.Empty
                : new TickPresentationData(entityMotions);
        }

        private static TickEntityMotionKind ResolveMotionKind(ActionGroupKind groupKind)
        {
            return groupKind switch
            {
                ActionGroupKind.Push => TickEntityMotionKind.Push,
                ActionGroupKind.Flip => TickEntityMotionKind.Flip,
                _ => TickEntityMotionKind.None,
            };
        }
    }

    internal sealed class TickResultData
    {
        private readonly ReadOnlyCollection<string> _eventLog;
        private readonly ReadOnlyCollection<EntityState> _finalEntities;
        private readonly ReadOnlyCollection<DelayedAttackEffectRecord> _pendingDelayedAttackEffects;
        private readonly TickPresentationData _presentationData;

        public TickResultData(
            IEnumerable<EntityState> finalEntities,
            IEnumerable<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            IEnumerable<string> eventLog)
            : this(
                finalEntities,
                pendingDelayedAttackEffects,
                eventLog,
                TickPresentationData.Empty)
        {
        }

        public TickResultData(
            IEnumerable<EntityState> finalEntities,
            IEnumerable<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            IEnumerable<string> eventLog,
            TickPresentationData presentationData)
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

            _presentationData = presentationData ?? throw new ArgumentNullException(nameof(presentationData));
            _finalEntities = new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities));
            _pendingDelayedAttackEffects = new ReadOnlyCollection<DelayedAttackEffectRecord>(new List<DelayedAttackEffectRecord>(pendingDelayedAttackEffects));
            _eventLog = new ReadOnlyCollection<string>(new List<string>(eventLog));
        }

        public IReadOnlyList<EntityState> FinalEntities => _finalEntities;

        public IReadOnlyList<DelayedAttackEffectRecord> PendingDelayedAttackEffects => _pendingDelayedAttackEffects;

        public IReadOnlyList<string> EventLog => _eventLog;

        public TickPresentationData PresentationData => _presentationData;
    }
}

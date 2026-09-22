using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class StateTransitionProcessor
    {
        public void Process(
            WorldSnapshot snapshot,
            IReadOnlyList<int> removedEntityIds,
            IReadOnlyList<int> timerExpiredTransitionCandidateIds,
            ICleanupCommitContext writeContext,
            List<string> stateTransitions)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (removedEntityIds == null)
            {
                throw new ArgumentNullException(nameof(removedEntityIds));
            }

            if (timerExpiredTransitionCandidateIds == null)
            {
                throw new ArgumentNullException(nameof(timerExpiredTransitionCandidateIds));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (stateTransitions == null)
            {
                throw new ArgumentNullException(nameof(stateTransitions));
            }

            stateTransitions.Clear();

            var immediateCandidateIds = snapshot.CleanupImmediateTransitionCandidateIds.Span;
            var immediateIndex = 0;
            var timerIndex = 0;
            var removedIndex = 0;
            while (immediateIndex < immediateCandidateIds.Length ||
                   timerIndex < timerExpiredTransitionCandidateIds.Count)
            {
                var hasImmediate = immediateIndex < immediateCandidateIds.Length;
                var hasTimerExpired = timerIndex < timerExpiredTransitionCandidateIds.Count;
                var takeImmediate = hasImmediate &&
                                    (!hasTimerExpired ||
                                     immediateCandidateIds[immediateIndex] <=
                                     timerExpiredTransitionCandidateIds[timerIndex]);
                var entityId = takeImmediate
                    ? immediateCandidateIds[immediateIndex]
                    : timerExpiredTransitionCandidateIds[timerIndex];
                var timerExpired = hasTimerExpired &&
                                   timerExpiredTransitionCandidateIds[timerIndex] == entityId;

                if (takeImmediate)
                {
                    immediateIndex++;
                }

                if (timerExpired)
                {
                    timerIndex++;
                }

                while (removedIndex < removedEntityIds.Count && removedEntityIds[removedIndex] < entityId)
                {
                    removedIndex++;
                }

                if ((removedIndex < removedEntityIds.Count && removedEntityIds[removedIndex] == entityId) ||
                    !snapshot.TryGetEntity(entityId, out var entity))
                {
                    continue;
                }

                var previousState = entity.state;
                entity.state = EntityPhaseState.Idle;
                if (timerExpired)
                {
                    entity.stateTimer = 0;
                }

                writeContext.ApplyStateChange(entity.entityId, entity.state, entity.stateTimer);
                stateTransitions.Add(
                    $"StateTransitioned|E={entity.entityId}|From={previousState}|To={entity.state}|Timer={entity.stateTimer}");
            }
        }
    }
}

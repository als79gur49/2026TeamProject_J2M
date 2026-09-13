using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class StateTimerProcessor
    {
        public void Process(
            WorldSnapshot snapshot,
            IReadOnlyList<int> removedEntityIds,
            ICleanupCommitContext writeContext,
            int tickIndex,
            List<string> timerChanges,
            List<int> transitionCandidateIds)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (removedEntityIds == null)
            {
                throw new ArgumentNullException(nameof(removedEntityIds));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (timerChanges == null)
            {
                throw new ArgumentNullException(nameof(timerChanges));
            }

            if (transitionCandidateIds == null)
            {
                throw new ArgumentNullException(nameof(transitionCandidateIds));
            }

            timerChanges.Clear();
            transitionCandidateIds.Clear();

            var timerCandidateIds = snapshot.CleanupTimerCandidateIds.Span;
            var removedIndex = 0;
            for (var i = 0; i < timerCandidateIds.Length; i++)
            {
                var entityId = timerCandidateIds[i];
                while (removedIndex < removedEntityIds.Count && removedEntityIds[removedIndex] < entityId)
                {
                    removedIndex++;
                }

                if ((removedIndex < removedEntityIds.Count && removedEntityIds[removedIndex] == entityId) ||
                    !snapshot.TryGetEntity(entityId, out var entity) ||
                    entity.spawnTick == tickIndex)
                {
                    continue;
                }

                var previousTimer = entity.stateTimer;
                entity.stateTimer = previousTimer - 1;

                writeContext.ApplyStateChange(entity.entityId, entity.state, entity.stateTimer);
                timerChanges.Add(
                    $"TimerTicked|E={entity.entityId}|State={entity.state}|From={previousTimer}|To={entity.stateTimer}");

                if (entity.stateTimer <= 0 && CleanupCandidateQueries.CanTransitionWhenTimerExpires(entity))
                {
                    transitionCandidateIds.Add(entityId);
                }
            }
        }
    }
}

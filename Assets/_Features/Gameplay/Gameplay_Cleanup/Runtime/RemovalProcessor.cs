using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class RemovalProcessor
    {
        public void Process(
            IReadOnlyList<EntityState> orderedEntities,
            ICleanupCommitContext writeContext,
            List<EntityState> survivingEntities,
            List<int> removedEntityIds,
            bool captureStructuralCounts,
            out CleanupStructuralScanCounts structuralCounts)
        {
            if (orderedEntities == null)
            {
                throw new ArgumentNullException(nameof(orderedEntities));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (survivingEntities == null)
            {
                throw new ArgumentNullException(nameof(survivingEntities));
            }

            if (removedEntityIds == null)
            {
                throw new ArgumentNullException(nameof(removedEntityIds));
            }

            survivingEntities.Clear();
            removedEntityIds.Clear();
            var removalCandidateCount = 0;
            var timerCandidateCount = 0;
            var immediateTransitionCandidateCount = 0;

            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var entity = orderedEntities[i];
                var shouldRemove = ShouldRemove(entity);
                if (captureStructuralCounts)
                {
                    if (shouldRemove)
                    {
                        removalCandidateCount++;
                    }

                    if (entity.stateTimer > 0)
                    {
                        timerCandidateCount++;
                    }

                    if (entity.stateTimer <= 0 &&
                        (entity.state == EntityPhaseState.Acting || entity.state == EntityPhaseState.Cooldown))
                    {
                        immediateTransitionCandidateCount++;
                    }
                }

                if (shouldRemove)
                {
                    removedEntityIds.Add(entity.entityId);
                    continue;
                }

                survivingEntities.Add(entity);
            }

            for (var i = 0; i < removedEntityIds.Count; i++)
            {
                writeContext.RemoveEntity(removedEntityIds[i]);
            }

            structuralCounts = new CleanupStructuralScanCounts(
                removalCandidateCount,
                timerCandidateCount,
                immediateTransitionCandidateCount);
        }

        private static bool ShouldRemove(EntityState entity)
        {
            return entity.hp <= 0 || entity.markedForDeath;
        }
    }
}

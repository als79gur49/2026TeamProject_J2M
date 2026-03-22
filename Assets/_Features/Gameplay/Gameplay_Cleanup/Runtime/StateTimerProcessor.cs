using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class StateTimerProcessor
    {
        public void Process(
            List<EntityState> survivingEntities,
            IWorldWriteContext writeContext,
            int tickIndex,
            List<string> timerChanges)
        {
            if (survivingEntities == null)
            {
                throw new ArgumentNullException(nameof(survivingEntities));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (timerChanges == null)
            {
                throw new ArgumentNullException(nameof(timerChanges));
            }

            timerChanges.Clear();

            for (var i = 0; i < survivingEntities.Count; i++)
            {
                var entity = survivingEntities[i];
                if (entity.spawnTick == tickIndex || entity.stateTimer <= 0)
                {
                    continue;
                }

                var previousTimer = entity.stateTimer;
                entity.stateTimer = previousTimer - 1;
                survivingEntities[i] = entity;

                writeContext.ApplyStateChange(entity.entityId, entity.state, entity.stateTimer);
                timerChanges.Add(
                    $"TimerTicked|E={entity.entityId}|State={entity.state}|From={previousTimer}|To={entity.stateTimer}");
            }
        }
    }
}

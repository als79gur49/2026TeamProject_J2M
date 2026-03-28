using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class StateTransitionProcessor
    {
        public void Process(
            List<EntityState> survivingEntities,
            IWorldWriteContext writeContext,
            List<string> stateTransitions)
        {
            if (survivingEntities == null)
            {
                throw new ArgumentNullException(nameof(survivingEntities));
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

            for (var i = 0; i < survivingEntities.Count; i++)
            {
                var entity = survivingEntities[i];
                if (!TryGetNextState(entity, out var nextState))
                {
                    continue;
                }

                var previousState = entity.state;
                entity.state = nextState;
                survivingEntities[i] = entity;

                writeContext.ApplyStateChange(entity.entityId, entity.state, entity.stateTimer);
                stateTransitions.Add(
                    $"StateTransitioned|E={entity.entityId}|From={previousState}|To={entity.state}|Timer={entity.stateTimer}");
            }
        }

        private static bool TryGetNextState(EntityState entity, out EntityPhaseState nextState)
        {
            nextState = entity.state;

            if (entity.stateTimer > 0)
            {
                return false;
            }

            switch (entity.state)
            {
                case EntityPhaseState.Acting:
                case EntityPhaseState.Cooldown:
                case EntityPhaseState.Sliding:
                    nextState = EntityPhaseState.Idle;
                    return true;

                default:
                    return false;
            }
        }
    }
}

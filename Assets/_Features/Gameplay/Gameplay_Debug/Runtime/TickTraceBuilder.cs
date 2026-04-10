using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Debug
{
    internal sealed class TickTraceBuilder
    {
        private readonly TickTraceFormatter _formatter = new();

        public TickTrace Build(
            int tickIndex,
            WorldSnapshot s0Snapshot,
            EnemyAiPhaseResult enemyAiPhaseResult,
            EnemyActionPhaseResult enemyActionPhaseResult,
            PreMovementStatePhaseResult preMovementStatePhaseResult,
            MovementPhaseResult movementPhaseResult,
            WorldSnapshot s1Snapshot,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            WorldSnapshot finalSnapshot,
            TickResultData tickResultData,
            string determinismHash)
        {
            return Build(
                tickIndex,
                s0Snapshot,
                enemyAiPhaseResult,
                enemyActionPhaseResult,
                preMovementStatePhaseResult,
                movementPhaseResult,
                s1Snapshot,
                attackPhaseResult,
                cleanupPhaseResult,
                RespawnPhaseResult.Empty,
                finalSnapshot,
                tickResultData,
                determinismHash);
        }

        public TickTrace Build(
            int tickIndex,
            WorldSnapshot s0Snapshot,
            EnemyAiPhaseResult enemyAiPhaseResult,
            EnemyActionPhaseResult enemyActionPhaseResult,
            PreMovementStatePhaseResult preMovementStatePhaseResult,
            MovementPhaseResult movementPhaseResult,
            WorldSnapshot s1Snapshot,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            RespawnPhaseResult respawnPhaseResult,
            WorldSnapshot finalSnapshot,
            TickResultData tickResultData,
            string determinismHash)
        {
            if (s0Snapshot == null)
            {
                throw new ArgumentNullException(nameof(s0Snapshot));
            }

            if (movementPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(movementPhaseResult));
            }

            if (preMovementStatePhaseResult == null)
            {
                throw new ArgumentNullException(nameof(preMovementStatePhaseResult));
            }

            if (enemyAiPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(enemyAiPhaseResult));
            }

            if (enemyActionPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(enemyActionPhaseResult));
            }

            if (s1Snapshot == null)
            {
                throw new ArgumentNullException(nameof(s1Snapshot));
            }

            if (attackPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(attackPhaseResult));
            }

            if (cleanupPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(cleanupPhaseResult));
            }

            if (respawnPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(respawnPhaseResult));
            }

            if (finalSnapshot == null)
            {
                throw new ArgumentNullException(nameof(finalSnapshot));
            }

            if (tickResultData == null)
            {
                throw new ArgumentNullException(nameof(tickResultData));
            }

            if (determinismHash == null)
            {
                throw new ArgumentNullException(nameof(determinismHash));
            }

            return new TickTrace(
                _formatter.Format(
                    tickIndex,
                    s0Snapshot,
                    enemyAiPhaseResult,
                    enemyActionPhaseResult,
                    preMovementStatePhaseResult,
                    movementPhaseResult,
                    s1Snapshot,
                    attackPhaseResult,
                    cleanupPhaseResult,
                    respawnPhaseResult,
                    finalSnapshot,
                    tickResultData,
                    determinismHash));
        }
    }
}

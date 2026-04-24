using System;
using System.Collections.Generic;
using System.Text;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class DeterminismHashBuilder
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public string Build(int tickIndex, WorldSnapshot finalSnapshot, TickResultData tickResultData)
        {
            if (finalSnapshot == null)
            {
                throw new ArgumentNullException(nameof(finalSnapshot));
            }

            if (tickResultData == null)
            {
                throw new ArgumentNullException(nameof(tickResultData));
            }

            var canonicalDump = BuildCanonicalDump(tickIndex, finalSnapshot, tickResultData);
            return ComputeFnv1A64(canonicalDump).ToString("X16");
        }

        private static string BuildCanonicalDump(
            int tickIndex,
            WorldSnapshot finalSnapshot,
            TickResultData tickResultData)
        {
            var builder = new StringBuilder(512);

            builder.Append("Tick=").Append(tickIndex).Append('\n');
            builder.Append("Topology").Append('\n');
            builder.Append(finalSnapshot.Topology.BottomFace).Append('|').Append(finalSnapshot.Topology.FrontFace).Append('\n');
            builder.Append("BoardBounds").Append('\n');
            AppendBoardBounds(builder, finalSnapshot.BoardBounds);

            builder.Append("Terrain").Append('\n');
            AppendTerrainLines(builder, GetOrderedTerrain(finalSnapshot));

            builder.Append("Entities").Append('\n');
            AppendEntityLines(builder, tickResultData.FinalEntities);

            builder.Append("PlayerControl").Append('\n');
            AppendPlayerControlLines(builder, GetOrderedPlayerControlStates(finalSnapshot));

            builder.Append("PlayerDamage").Append('\n');
            AppendPlayerDamageLines(builder, GetOrderedPlayerDamageStates(finalSnapshot));

            builder.Append("EnemyActions").Append('\n');
            AppendEnemyActionLines(builder, GetOrderedEnemyActionStates(finalSnapshot));

            builder.Append("EnemyPatrols").Append('\n');
            AppendEnemyPatrolLines(builder, GetOrderedEnemyPatrolStates(finalSnapshot));

            builder.Append("ExecutionLocks").Append('\n');
            AppendExecutionLockLines(builder, GetOrderedExecutionLockStates(finalSnapshot));

            builder.Append("EnemyJumps").Append('\n');
            AppendEnemyJumpLines(builder, GetOrderedEnemyJumpStates(finalSnapshot));

            builder.Append("EnemyUtilities").Append('\n');
            AppendEnemyUtilityLines(builder, GetOrderedEnemyUtilityStates(finalSnapshot));

            builder.Append("EnemyCharges").Append('\n');
            AppendEnemyChargeLines(builder, GetOrderedEnemyChargeStates(finalSnapshot));

            builder.Append("PhasedStates").Append('\n');
            AppendPhasedStateLines(builder, GetOrderedPhasedStates(finalSnapshot));

            builder.Append("SummonedEntities").Append('\n');
            AppendSummonedEntityLines(builder, GetOrderedSummonedEntityStates(finalSnapshot));

            builder.Append("SolidOccupancy").Append('\n');
            AppendOccupancyLines(builder, GetOrderedSolidOccupancy(finalSnapshot));

            builder.Append("StackedUnitOccupancy").Append('\n');
            AppendOccupancyLines(builder, GetOrderedUnitOccupancy(finalSnapshot));

            builder.Append("ProjectileOccupancy").Append('\n');
            AppendOccupancyLines(builder, GetOrderedProjectileOccupancy(finalSnapshot));

            builder.Append("MarkedForDeath").Append('\n');
            AppendMarkedForDeathLines(builder, tickResultData.FinalEntities);

            builder.Append("PendingDelayedAttackEffects").Append('\n');
            AppendPendingDelayedAttackEffectLines(builder, tickResultData.PendingDelayedAttackEffects);

            builder.Append("Objective").Append('\n');
            AppendObjectiveLines(builder, tickResultData.ObjectiveResult);

            builder.Append("EventLog").Append('\n');
            AppendStringLines(builder, tickResultData.EventLog);

            return builder.ToString();
        }

        private static void AppendEntityLines(StringBuilder builder, IReadOnlyList<EntityState> finalEntities)
        {
            if (finalEntities.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                builder
                    .Append(entity.entityId).Append('|')
                    .Append((int)entity.position.face).Append('|')
                    .Append(entity.position.x).Append('|')
                    .Append(entity.position.y).Append('|')
                    .Append(entity.hp).Append('|')
                    .Append(entity.maxHp).Append('|')
                    .Append(entity.teamId).Append('|')
                    .Append((int)entity.type).Append('|')
                    .Append((int)entity.state).Append('|')
                    .Append(entity.stateTimer).Append('|')
                    .Append((int)entity.facing).Append('|')
                    .Append((int)entity.boardPresence).Append('|')
                    .Append(entity.markedForDeath ? 1 : 0).Append('|')
                    .Append(entity.spawnTick).Append('|')
                    .Append((int)entity.boxCapabilities).Append('|')
                    .Append(entity.kineticInstigatorEntityId).Append('|')
                    .Append(entity.kineticInstigatorTeamId).Append('|')
                    .Append((int)entity.aiMode).Append('|')
                    .Append(entity.aiStateTimer).Append('|')
                    .Append(entity.enemyLocomotionCooldownTicks).Append('\n');
            }
        }

        private static List<SnapshotOccupancyEntry> GetOrderedUnitOccupancy(WorldSnapshot finalSnapshot)
        {
            var occupancyEntries = new List<SnapshotOccupancyEntry>();
            finalSnapshot.EnumerateUnitOccupancyOrdered(occupancyEntries);
            return occupancyEntries;
        }

        private static List<SnapshotOccupancyEntry> GetOrderedSolidOccupancy(WorldSnapshot finalSnapshot)
        {
            var occupancyEntries = new List<SnapshotOccupancyEntry>();
            finalSnapshot.EnumerateSolidOccupancyOrdered(occupancyEntries);
            return occupancyEntries;
        }

        private static List<EnemyJumpSnapshotEntry> GetOrderedEnemyJumpStates(WorldSnapshot finalSnapshot)
        {
            var enemyJumpEntries = new List<EnemyJumpSnapshotEntry>();
            finalSnapshot.EnumerateEnemyJumpStatesOrdered(enemyJumpEntries);
            return enemyJumpEntries;
        }

        private static List<EnemyPatrolSnapshotEntry> GetOrderedEnemyPatrolStates(WorldSnapshot finalSnapshot)
        {
            var enemyPatrolEntries = new List<EnemyPatrolSnapshotEntry>();
            finalSnapshot.EnumerateEnemyPatrolStatesOrdered(enemyPatrolEntries);
            return enemyPatrolEntries;
        }

        private static List<EnemyChargeSnapshotEntry> GetOrderedEnemyChargeStates(WorldSnapshot finalSnapshot)
        {
            var enemyChargeEntries = new List<EnemyChargeSnapshotEntry>();
            finalSnapshot.EnumerateEnemyChargeStatesOrdered(enemyChargeEntries);
            return enemyChargeEntries;
        }

        private static List<EnemyUtilitySnapshotEntry> GetOrderedEnemyUtilityStates(WorldSnapshot finalSnapshot)
        {
            var enemyUtilityEntries = new List<EnemyUtilitySnapshotEntry>();
            finalSnapshot.EnumerateEnemyUtilityStatesOrdered(enemyUtilityEntries);
            return enemyUtilityEntries;
        }

        private static List<EntityExecutionLockSnapshotEntry> GetOrderedExecutionLockStates(WorldSnapshot finalSnapshot)
        {
            var executionLockEntries = new List<EntityExecutionLockSnapshotEntry>();
            finalSnapshot.EnumerateEntityExecutionLockStatesOrdered(executionLockEntries);
            return executionLockEntries;
        }

        private static List<SummonedEntitySnapshotEntry> GetOrderedSummonedEntityStates(WorldSnapshot finalSnapshot)
        {
            var summonedEntries = new List<SummonedEntitySnapshotEntry>();
            finalSnapshot.EnumerateSummonedEntityStatesOrdered(summonedEntries);
            return summonedEntries;
        }

        private static List<PhasedSnapshotEntry> GetOrderedPhasedStates(WorldSnapshot finalSnapshot)
        {
            var phasedEntries = new List<PhasedSnapshotEntry>();
            finalSnapshot.EnumeratePhasedStatesOrdered(phasedEntries);
            return phasedEntries;
        }

        private static List<PlayerControlSnapshotEntry> GetOrderedPlayerControlStates(WorldSnapshot finalSnapshot)
        {
            var playerControlEntries = new List<PlayerControlSnapshotEntry>();
            finalSnapshot.EnumeratePlayerControlStatesOrdered(playerControlEntries);
            return playerControlEntries;
        }

        private static List<PlayerDamageSnapshotEntry> GetOrderedPlayerDamageStates(WorldSnapshot finalSnapshot)
        {
            var playerDamageEntries = new List<PlayerDamageSnapshotEntry>();
            finalSnapshot.EnumeratePlayerDamageStatesOrdered(playerDamageEntries);
            return playerDamageEntries;
        }

        private static List<EnemyActionSnapshotEntry> GetOrderedEnemyActionStates(WorldSnapshot finalSnapshot)
        {
            var enemyActionEntries = new List<EnemyActionSnapshotEntry>();
            finalSnapshot.EnumerateEnemyActionStatesOrdered(enemyActionEntries);
            return enemyActionEntries;
        }

        private static List<SnapshotOccupancyEntry> GetOrderedProjectileOccupancy(WorldSnapshot finalSnapshot)
        {
            var occupancyEntries = new List<SnapshotOccupancyEntry>();
            finalSnapshot.EnumerateProjectileOccupancyOrdered(occupancyEntries);
            return occupancyEntries;
        }

        private static List<TerrainCellState> GetOrderedTerrain(WorldSnapshot finalSnapshot)
        {
            var terrainEntries = new List<TerrainCellState>();
            finalSnapshot.EnumerateTerrainCellsOrdered(terrainEntries);
            return terrainEntries;
        }

        private static void AppendBoardBounds(StringBuilder builder, BoardBounds boardBounds)
        {
            if (!boardBounds.IsBounded)
            {
                builder.Append("Unbounded").Append('\n');
                return;
            }

            builder
                .Append(boardBounds.MinInclusive.x).Append('|')
                .Append(boardBounds.MinInclusive.y).Append('|')
                .Append(boardBounds.MaxInclusive.x).Append('|')
                .Append(boardBounds.MaxInclusive.y).Append('\n');
        }

        private static void AppendOccupancyLines(
            StringBuilder builder,
            IReadOnlyList<SnapshotOccupancyEntry> occupancyEntries)
        {
            if (occupancyEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < occupancyEntries.Count; i++)
            {
                var entry = occupancyEntries[i];
                builder
                    .Append((int)entry.Cell.face).Append('|')
                    .Append(entry.Cell.x).Append('|')
                    .Append(entry.Cell.y).Append('|')
                    .Append(entry.EntityId).Append('\n');
            }
        }

        private static void AppendTerrainLines(
            StringBuilder builder,
            IReadOnlyList<TerrainCellState> terrainEntries)
        {
            if (terrainEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < terrainEntries.Count; i++)
            {
                builder
                    .Append((int)terrainEntries[i].Cell.face).Append('|')
                    .Append(terrainEntries[i].Cell.x).Append('|')
                    .Append(terrainEntries[i].Cell.y).Append('|')
                    .Append((int)terrainEntries[i].Kind).Append('|')
                    .Append((int)terrainEntries[i].Flags).Append('\n');
            }
        }

        private static void AppendPhasedStateLines(
            StringBuilder builder,
            IReadOnlyList<PhasedSnapshotEntry> phasedEntries)
        {
            if (phasedEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < phasedEntries.Count; i++)
            {
                var entry = phasedEntries[i];
                builder
                    .Append(entry.EntityId).Append('|')
                    .Append((int)entry.State.ownerKind).Append('|')
                    .Append(entry.State.sequence).Append('|')
                    .Append(entry.State.enteredTick).Append('|')
                    .Append(entry.State.exitTickExclusive).Append('|')
                    .Append(entry.State.IsActive ? 1 : 0).Append('\n');
            }
        }

        private static void AppendEnemyUtilityLines(
            StringBuilder builder,
            IReadOnlyList<EnemyUtilitySnapshotEntry> enemyUtilityEntries)
        {
            if (enemyUtilityEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < enemyUtilityEntries.Count; i++)
            {
                var entry = enemyUtilityEntries[i];
                if (entry.State.EffectStates.Count == 0)
                {
                    builder.Append(entry.EntityId).Append("|<empty>").Append('\n');
                    continue;
                }

                for (var effectIndex = 0; effectIndex < entry.State.EffectStates.Count; effectIndex++)
                {
                    builder
                        .Append(entry.EntityId).Append('|')
                        .Append(effectIndex).Append('|')
                        .Append(entry.State.EffectStates[effectIndex].cooldownTicksRemaining).Append('\n');
                }
            }
        }

        private static void AppendSummonedEntityLines(
            StringBuilder builder,
            IReadOnlyList<SummonedEntitySnapshotEntry> summonedEntries)
        {
            if (summonedEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < summonedEntries.Count; i++)
            {
                builder
                    .Append(summonedEntries[i].EntityId).Append('|')
                    .Append(summonedEntries[i].State.SourceEntityId).Append('|')
                    .Append(summonedEntries[i].State.SourceEffectIndex).Append('\n');
            }
        }

        private static void AppendObjectiveLines(
            StringBuilder builder,
            StageObjectiveTickResult objectiveResult)
        {
            if (objectiveResult == null || !objectiveResult.HasObjective)
            {
                builder.Append("<none>").Append('\n');
                return;
            }

            builder
                .Append(objectiveResult.GoalReached ? 1 : 0).Append('|')
                .Append(objectiveResult.AllConditionsSatisfied ? 1 : 0).Append('|')
                .Append(objectiveResult.ClearedThisTick ? 1 : 0).Append('|')
                .Append(objectiveResult.IsCleared ? 1 : 0).Append('\n');

            var conditionStatuses = objectiveResult.ConditionStatuses;
            if (conditionStatuses.Count == 0)
            {
                builder.Append("<conditions-empty>").Append('\n');
                return;
            }

            for (var i = 0; i < conditionStatuses.Count; i++)
            {
                var status = conditionStatuses[i];
                builder
                    .Append(status.ConditionId).Append('|')
                    .Append(status.ConditionType).Append('|')
                    .Append(status.IsSatisfied ? 1 : 0).Append('|')
                    .Append(status.Details).Append('\n');
            }
        }

        private static void AppendPlayerControlLines(
            StringBuilder builder,
            IReadOnlyList<PlayerControlSnapshotEntry> playerControlEntries)
        {
            if (playerControlEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < playerControlEntries.Count; i++)
            {
                var entry = playerControlEntries[i];
                builder
                    .Append(entry.EntityId).Append('|')
                    .Append(entry.State.moveCooldownTicks).Append('|')
                    .Append(entry.State.nextMoveAllowedTick).Append('|')
                    .Append(entry.State.actionSequenceCounter).Append('|')
                    .Append((int)entry.State.activeAction.kind).Append('|')
                    .Append(entry.State.activeAction.sequence).Append('|')
                    .Append((int)entry.State.activeAction.direction).Append('|')
                    .Append(entry.State.activeAction.targetEntityId).Append('|')
                    .Append(entry.State.activeAction.startTick).Append('|')
                    .Append(entry.State.activeAction.executeTick).Append('|')
                    .Append(entry.State.activeAction.recoveryEndTick).Append('|')
                    .Append(entry.State.activeAction.executionAttempted ? 1 : 0).Append('\n');
            }
        }

        private static void AppendPlayerDamageLines(
            StringBuilder builder,
            IReadOnlyList<PlayerDamageSnapshotEntry> playerDamageEntries)
        {
            if (playerDamageEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < playerDamageEntries.Count; i++)
            {
                var entry = playerDamageEntries[i];
                builder
                    .Append(entry.EntityId).Append('|')
                    .Append(entry.State.nextDamageAllowedTick).Append('\n');
            }
        }

        private static void AppendEnemyActionLines(
            StringBuilder builder,
            IReadOnlyList<EnemyActionSnapshotEntry> enemyActionEntries)
        {
            if (enemyActionEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < enemyActionEntries.Count; i++)
            {
                var entry = enemyActionEntries[i];
                builder
                    .Append(entry.EntityId).Append('|')
                    .Append((int)entry.State.kind).Append('|')
                    .Append(entry.State.sequence).Append('|')
                    .Append(entry.State.lockedTargetEntityId).Append('|')
                    .Append((int)entry.State.direction).Append('|')
                    .Append(entry.State.startTick).Append('|')
                    .Append(entry.State.executeTick).Append('|')
                    .Append(entry.State.executionAttempted ? 1 : 0).Append('\n');
            }
        }

        private static void AppendEnemyJumpLines(
            StringBuilder builder,
            IReadOnlyList<EnemyJumpSnapshotEntry> enemyJumpEntries)
        {
            if (enemyJumpEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < enemyJumpEntries.Count; i++)
            {
                var entry = enemyJumpEntries[i];
                builder
                    .Append(entry.EntityId).Append('|')
                    .Append((int)entry.State.phase).Append('|')
                    .Append(entry.State.sequence).Append('|')
                    .Append((int)entry.State.sourceCell.face).Append('|')
                    .Append(entry.State.sourceCell.x).Append('|')
                    .Append(entry.State.sourceCell.y).Append('|')
                    .Append((int)entry.State.lockedTargetCell.face).Append('|')
                    .Append(entry.State.lockedTargetCell.x).Append('|')
                    .Append(entry.State.lockedTargetCell.y).Append('|')
                    .Append(entry.State.windupEndTick).Append('|')
                    .Append(entry.State.landingTick).Append('|')
                    .Append(entry.State.cooldownRemainingTicks).Append('|')
                    .Append(entry.State.retryCount).Append('\n');
            }
        }

        private static void AppendEnemyPatrolLines(
            StringBuilder builder,
            IReadOnlyList<EnemyPatrolSnapshotEntry> enemyPatrolEntries)
        {
            if (enemyPatrolEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < enemyPatrolEntries.Count; i++)
            {
                var entry = enemyPatrolEntries[i];
                builder
                    .Append(entry.EntityId).Append('|')
                    .Append(entry.State.sequence).Append('|')
                    .Append((int)entry.State.homeCell.face).Append('|')
                    .Append(entry.State.homeCell.x).Append('|')
                    .Append(entry.State.homeCell.y).Append('|')
                    .Append((int)entry.State.lastCommittedDirection).Append('\n');
            }
        }

        private static void AppendEnemyChargeLines(
            StringBuilder builder,
            IReadOnlyList<EnemyChargeSnapshotEntry> enemyChargeEntries)
        {
            if (enemyChargeEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < enemyChargeEntries.Count; i++)
            {
                var entry = enemyChargeEntries[i];
                builder
                    .Append(entry.EntityId).Append('|')
                    .Append((int)entry.State.phase).Append('|')
                    .Append(entry.State.sequence).Append('|')
                    .Append((int)entry.State.lockedDirection).Append('|')
                    .Append(entry.State.windupEndTick).Append('|')
                    .Append(entry.State.remainingActiveSteps).Append('|')
                    .Append(entry.State.recoverRemainingTicks).Append('\n');
            }
        }

        private static void AppendExecutionLockLines(
            StringBuilder builder,
            IReadOnlyList<EntityExecutionLockSnapshotEntry> executionLockEntries)
        {
            if (executionLockEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < executionLockEntries.Count; i++)
            {
                var entry = executionLockEntries[i];
                builder
                    .Append(entry.EntityId).Append('|')
                    .Append((int)entry.State.phase).Append('|')
                    .Append(entry.State.sequence).Append('|')
                    .Append(entry.State.unlockTickExclusive).Append('\n');
            }
        }

        private static void AppendMarkedForDeathLines(StringBuilder builder, IReadOnlyList<EntityState> finalEntities)
        {
            var hasMarkedEntity = false;

            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (!entity.markedForDeath)
                {
                    continue;
                }

                hasMarkedEntity = true;
                builder.Append(entity.entityId).Append('\n');
            }

            if (!hasMarkedEntity)
            {
                builder.Append("<empty>").Append('\n');
            }
        }

        private static void AppendPendingDelayedAttackEffectLines(
            StringBuilder builder,
            IReadOnlyList<DelayedAttackEffectRecord> pendingDelayedAttackEffects)
        {
            if (pendingDelayedAttackEffects.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < pendingDelayedAttackEffects.Count; i++)
            {
                var effect = pendingDelayedAttackEffects[i];
                builder
                    .Append(effect.SourceId).Append('|')
                    .Append(effect.TargetId).Append('|')
                    .Append(effect.Damage).Append('|')
                    .Append(effect.Priority).Append('|')
                    .Append(effect.TickGenerated).Append('|')
                    .Append(effect.ExecuteAtTick).Append('|')
                    .Append(effect.SourceActionPlanId).Append('|')
                    .Append(effect.EffectSequence).Append('\n');
            }
        }

        private static void AppendStringLines(StringBuilder builder, IReadOnlyList<string> values)
        {
            if (values.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                builder.Append(values[i]).Append('\n');
            }
        }

        private static ulong ComputeFnv1A64(string input)
        {
            var hash = FnvOffsetBasis;
            var bytes = Encoding.UTF8.GetBytes(input);

            for (var i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= FnvPrime;
            }

            return hash;
        }
    }
}

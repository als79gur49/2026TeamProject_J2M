using System;
using System.Collections.Generic;
using System.Text;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
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
            builder.Append("BoardBounds").Append('\n');
            AppendBoardBounds(builder, finalSnapshot.BoardBounds);

            builder.Append("Terrain").Append('\n');
            AppendTerrainLines(builder, GetOrderedTerrain(finalSnapshot));

            builder.Append("Entities").Append('\n');
            AppendEntityLines(builder, tickResultData.FinalEntities);

            builder.Append("UnitOccupancy").Append('\n');
            AppendOccupancyLines(builder, GetOrderedUnitOccupancy(finalSnapshot));

            builder.Append("ProjectileOccupancy").Append('\n');
            AppendOccupancyLines(builder, GetOrderedProjectileOccupancy(finalSnapshot));

            builder.Append("MarkedForDeath").Append('\n');
            AppendMarkedForDeathLines(builder, tickResultData.FinalEntities);

            builder.Append("PendingDelayedAttackEffects").Append('\n');
            AppendPendingDelayedAttackEffectLines(builder, tickResultData.PendingDelayedAttackEffects);

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
                    .Append(entity.position.x).Append('|')
                    .Append(entity.position.y).Append('|')
                    .Append(entity.hp).Append('|')
                    .Append(entity.maxHp).Append('|')
                    .Append(entity.teamId).Append('|')
                    .Append((int)entity.type).Append('|')
                    .Append((int)entity.state).Append('|')
                    .Append(entity.stateTimer).Append('|')
                    .Append((int)entity.facing).Append('|')
                    .Append(entity.markedForDeath ? 1 : 0).Append('|')
                    .Append(entity.spawnTick).Append('|')
                    .Append((int)entity.boxCapabilities).Append('\n');
            }
        }

        private static List<SnapshotOccupancyEntry> GetOrderedUnitOccupancy(WorldSnapshot finalSnapshot)
        {
            var occupancyEntries = new List<SnapshotOccupancyEntry>();
            finalSnapshot.EnumerateUnitOccupancyOrdered(occupancyEntries);
            return occupancyEntries;
        }

        private static List<SnapshotOccupancyEntry> GetOrderedProjectileOccupancy(WorldSnapshot finalSnapshot)
        {
            var occupancyEntries = new List<SnapshotOccupancyEntry>();
            finalSnapshot.EnumerateProjectileOccupancyOrdered(occupancyEntries);
            return occupancyEntries;
        }

        private static List<Vector2Int> GetOrderedTerrain(WorldSnapshot finalSnapshot)
        {
            var terrainEntries = new List<Vector2Int>();
            finalSnapshot.EnumerateTerrainBlockedCellsOrdered(terrainEntries);
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
                    .Append(entry.Cell.x).Append('|')
                    .Append(entry.Cell.y).Append('|')
                    .Append(entry.EntityId).Append('\n');
            }
        }

        private static void AppendTerrainLines(
            StringBuilder builder,
            IReadOnlyList<Vector2Int> terrainEntries)
        {
            if (terrainEntries.Count == 0)
            {
                builder.Append("<empty>").Append('\n');
                return;
            }

            for (var i = 0; i < terrainEntries.Count; i++)
            {
                builder
                    .Append(terrainEntries[i].x).Append('|')
                    .Append(terrainEntries[i].y).Append('\n');
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
                    .Append(effect.SourceActionGroupId).Append('|')
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

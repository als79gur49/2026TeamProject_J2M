using System.Collections.Generic;
using System.Text;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Debug
{
    internal sealed class TickTraceFormatter
    {
        public string Format(
            int tickIndex,
            WorldSnapshot s0Snapshot,
            EnemyAiPhaseResult enemyAiPhaseResult,
            PreMovementStatePhaseResult preMovementStatePhaseResult,
            MovementPhaseResult movementPhaseResult,
            WorldSnapshot s1Snapshot,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            WorldSnapshot finalSnapshot,
            TickResultData tickResultData,
            string determinismHash)
        {
            var builder = new StringBuilder(2048);
            builder.Append("Tick ").Append(tickIndex.ToString("D5")).Append('\n');

            AppendSnapshotSections(builder, "S0", s0Snapshot);
            AppendSection(builder, "EnemyAi.BeforeMovementTransitions", enemyAiPhaseResult.BeforeMovementTransitions, FormatString);
            AppendSection(builder, "PreMovement.PlayerControlUpdates", preMovementStatePhaseResult.Updates, FormatString);
            AppendSection(builder, "Movement.RawIntents", movementPhaseResult.RawIntents, FormatRawMovementIntent);
            AppendSection(builder, "Movement.SortedIntents", movementPhaseResult.SortedIntents, FormatMoveIntent);
            AppendSection(builder, "Movement.Candidates", movementPhaseResult.ExpandedCandidates, FormatActionGroup);
            AppendSection(builder, "Movement.RejectedReasons", movementPhaseResult.RejectedReasons, FormatString);
            AppendSection(builder, "Movement.SelectedGroups", movementPhaseResult.SelectedGroups, FormatActionGroup);
            AppendSection(builder, "Movement.CommitEvents", movementPhaseResult.CommitEvents, FormatString);
            AppendOccupancySection(builder, "Movement.OccupancyBefore", s0Snapshot);
            AppendOccupancySection(builder, "Movement.OccupancyAfter", s1Snapshot);

            AppendSnapshotSections(builder, "S1", s1Snapshot);
            AppendSection(builder, "EnemyAi.BeforeAttackTransitions", enemyAiPhaseResult.BeforeAttackTransitions, FormatString);
            AppendSection(builder, "Attack.RawIntents", attackPhaseResult.RawIntents, FormatRawAttackIntent);
            AppendSection(builder, "Attack.DrainedImpacts", attackPhaseResult.DrainedImpactReservations, FormatImpactReservation);
            AppendSection(builder, "Attack.DrainedDelayedEffects", attackPhaseResult.DrainedDelayedAttackEffects, FormatDelayedAttackEffectRecord);
            AppendSection(builder, "Attack.NormalizedInputs", attackPhaseResult.SortedInputs, FormatAttackIntent);
            AppendSection(builder, "Attack.Candidates", attackPhaseResult.ExpandedCandidates, FormatActionGroup);
            AppendSection(builder, "Attack.RejectedReasons", attackPhaseResult.RejectedReasons, FormatString);
            AppendSection(builder, "Attack.SelectedGroups", attackPhaseResult.SelectedGroups, FormatActionGroup);
            AppendSection(builder, "Attack.CommitEvents", attackPhaseResult.CommitEvents, FormatString);
            AppendSection(builder, "EnemyAi.AfterAttackTransitions", enemyAiPhaseResult.AfterAttackTransitions, FormatString);

            AppendSection(builder, "Cleanup.RemovedIds", cleanupPhaseResult.RemovedEntityIds, value => value.ToString());
            AppendSection(builder, "Cleanup.TimerChanges", cleanupPhaseResult.TimerChanges, FormatString);
            AppendSection(builder, "Cleanup.StateTransitions", cleanupPhaseResult.StateTransitions, FormatString);

            AppendSnapshotSections(builder, "Final", finalSnapshot);
            AppendSection(builder, "Final.PendingDelayedEffects", tickResultData.PendingDelayedAttackEffects, FormatDelayedAttackEffectRecord);
            AppendSection(builder, "TickResult.FinalEntities", tickResultData.FinalEntities, FormatEntityState);
            AppendSection(builder, "TickResult.EventLog", tickResultData.EventLog, FormatString);
            AppendSection(builder, "DeterminismHash", new[] { determinismHash }, FormatString);

            return builder.ToString();
        }

        private static void AppendSnapshotSections(StringBuilder builder, string label, WorldSnapshot snapshot)
        {
            AppendSection(builder, $"{label}.Topology", new[] { snapshot.Topology.ToString() }, FormatString);
            AppendSection(builder, $"{label}.BoardBounds", new[] { FormatBoardBounds(snapshot.BoardBounds) }, FormatString);
            AppendSection(builder, $"{label}.Terrain", GetTerrainEntries(snapshot), FormatString);
            AppendSection(builder, $"{label}.Entities", GetOrderedEntities(snapshot), FormatEntityState);
            AppendSection(builder, $"{label}.PlayerControl", GetPlayerControlEntries(snapshot), FormatString);
            AppendOccupancySection(builder, $"{label}.Occupancy", snapshot);
        }

        private static void AppendOccupancySection(StringBuilder builder, string title, WorldSnapshot snapshot)
        {
            AppendSection(builder, title, GetOccupancyEntries(snapshot), FormatString);
        }

        private static List<EntityState> GetOrderedEntities(WorldSnapshot snapshot)
        {
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            return entities;
        }

        private static List<string> GetOccupancyEntries(WorldSnapshot snapshot)
        {
            var occupancyLines = new List<string>();

            var unitEntries = new List<SnapshotOccupancyEntry>();
            snapshot.EnumerateUnitOccupancyOrdered(unitEntries);
            AddOccupancyLines(occupancyLines, "Unit", unitEntries);

            var projectileEntries = new List<SnapshotOccupancyEntry>();
            snapshot.EnumerateProjectileOccupancyOrdered(projectileEntries);
            AddOccupancyLines(occupancyLines, "Projectile", projectileEntries);

            return occupancyLines;
        }

        private static List<string> GetPlayerControlEntries(WorldSnapshot snapshot)
        {
            var entries = new List<PlayerControlSnapshotEntry>();
            var lines = new List<string>();
            snapshot.EnumeratePlayerControlStatesOrdered(entries);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                lines.Add(
                    $"E={entry.EntityId}|Cooldown={entry.State.moveCooldownTicks}|PushTicks={entry.State.pushContactTicks}|Target={entry.State.pushTargetEntityId}|Direction={entry.State.pushDirection}|Lock={entry.State.interactionLockTicks}");
            }

            return lines;
        }

        private static List<string> GetTerrainEntries(WorldSnapshot snapshot)
        {
            var terrainLines = new List<string>();
            var blockedCells = new List<Vector2Int>();
            snapshot.EnumerateTerrainBlockedCellsOrdered(blockedCells);

            for (var i = 0; i < blockedCells.Count; i++)
            {
                terrainLines.Add($"Cell=({blockedCells[i].x},{blockedCells[i].y})|BlocksUnit=1");
            }

            return terrainLines;
        }

        private static void AddOccupancyLines(
            List<string> buffer,
            string layerName,
            IReadOnlyList<SnapshotOccupancyEntry> entries)
        {
            if (entries.Count == 0)
            {
                buffer.Add($"{layerName}|<empty>");
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                buffer.Add($"{layerName}|Cell=({entry.Cell.x},{entry.Cell.y})|E={entry.EntityId}|Face={entry.Cell.face}");
            }
        }

        private static void AppendSection<T>(
            StringBuilder builder,
            string title,
            IReadOnlyList<T> values,
            System.Func<T, string> formatter)
        {
            builder.Append(title).Append('\n');

            if (values.Count == 0)
            {
                builder.Append("  <empty>").Append('\n');
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                builder.Append("  ").Append(formatter(values[i])).Append('\n');
            }
        }

        private static string FormatString(string value)
        {
            return value;
        }

        private static string FormatBoardBounds(BoardBounds boardBounds)
        {
            if (!boardBounds.IsBounded)
            {
                return "Unbounded";
            }

            return $"Min=({boardBounds.MinInclusive.x},{boardBounds.MinInclusive.y})|Max=({boardBounds.MaxInclusive.x},{boardBounds.MaxInclusive.y})";
        }

        private static string FormatRawMovementIntent(RawMovementIntent rawIntent)
        {
            return $"Source={rawIntent.SourceId}|Priority={rawIntent.Priority}|Destination=({rawIntent.Destination.x},{rawIntent.Destination.y})|Command={rawIntent.CommandKind}";
        }

        private static string FormatMoveIntent(MoveIntent intent)
        {
            return $"I={intent.IntentId}|Source={intent.SourceId}|Priority={intent.Priority}|Destination=({intent.Destination.x},{intent.Destination.y})|Command={intent.CommandKind}";
        }

        private static string FormatRawAttackIntent(RawAttackIntent rawIntent)
        {
            if (rawIntent.HasTargetCell)
            {
                return $"Source={rawIntent.SourceId}|Priority={rawIntent.Priority}|TargetCell=({rawIntent.TargetCell.x},{rawIntent.TargetCell.y})|Command={rawIntent.CommandKind}|LocalSequence={rawIntent.LocalSequence}";
            }

            return $"Source={rawIntent.SourceId}|Priority={rawIntent.Priority}|Target={rawIntent.TargetId}|Command={rawIntent.CommandKind}|LocalSequence={rawIntent.LocalSequence}";
        }

        private static string FormatAttackIntent(AttackIntent intent)
        {
            var builder = new StringBuilder();
            builder
                .Append("I=").Append(intent.IntentId)
                .Append("|Source=").Append(intent.SourceId)
                .Append("|Priority=").Append(intent.Priority)
                .Append("|Target=").Append(intent.TargetId)
                .Append("|Command=").Append(intent.CommandKind)
                .Append("|Kind=").Append(intent.InputKind)
                .Append("|LocalSequence=").Append(intent.LocalSequence);

            if (intent.HasTargetCell)
            {
                builder
                    .Append("|TargetCell=(").Append(intent.TargetCell.x).Append(',').Append(intent.TargetCell.y).Append(')');
            }

            if (intent.ImpactReservation.HasValue)
            {
                builder.Append('|').Append(FormatImpactReservation(intent.ImpactReservation.Value));
            }

            if (intent.DelayedAttackEffect.HasValue)
            {
                builder.Append('|').Append(FormatDelayedAttackEffectRecord(intent.DelayedAttackEffect.Value));
            }

            return builder.ToString();
        }

        private static string FormatImpactReservation(ImpactReservation reservation)
        {
            return
                $"Reservation|Source={reservation.SourceId}|Target={reservation.TargetId}|Position=({reservation.Position.x},{reservation.Position.y})|Damage={reservation.Damage}|Tick={reservation.TickGenerated}|Group={reservation.SourceActionGroupId}|Sequence={reservation.ReservationSequence}";
        }

        private static string FormatDelayedAttackEffectRecord(DelayedAttackEffectRecord effectRecord)
        {
            return
                $"DelayedAttack|Source={effectRecord.SourceId}|Target={effectRecord.TargetId}|Damage={effectRecord.Damage}|Priority={effectRecord.Priority}|GeneratedTick={effectRecord.TickGenerated}|ExecuteTick={effectRecord.ExecuteAtTick}|Group={effectRecord.SourceActionGroupId}|Sequence={effectRecord.EffectSequence}";
        }

        private static string FormatActionGroup(ActionGroup group)
        {
            var builder = new StringBuilder();
            builder
                .Append("G=").Append(group.GroupId)
                .Append("|I=").Append(group.IntentId)
                .Append("|Source=").Append(group.SourceId)
                .Append("|Priority=").Append(group.Priority)
                .Append("|Kind=").Append(group.GroupKind)
                .Append("|Moves=").Append(FormatMoves(group.Moves))
                .Append("|Damages=").Append(FormatDamages(group.Damages))
                .Append("|Spawns=").Append(FormatSpawns(group.Spawns))
                .Append("|Destroys=").Append(FormatDestroys(group.Destroys))
                .Append("|StateChanges=").Append(FormatStateChanges(group.StateChanges))
                .Append("|BoardPresenceChanges=").Append(FormatBoardPresenceChanges(group.BoardPresenceChanges))
                .Append("|TopologyChanges=").Append(FormatTopologyChanges(group.TopologyChanges))
                .Append("|DelayedAttacks=").Append(FormatDelayedAttacks(group.DelayedAttacks));
            return builder.ToString();
        }

        private static string FormatMoves(IReadOnlyList<MoveAction> moves)
        {
            if (moves.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");

            for (var i = 0; i < moves.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var move = moves[i];
                builder
                    .Append("E=").Append(move.EntityId)
                    .Append(':').Append(FormatCell(move.SourceCell))
                    .Append("->").Append(FormatCell(move.DestinationCell))
                    .Append(':').Append(move.Facing);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatDamages(IReadOnlyList<DamageAction> damages)
        {
            if (damages.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");

            for (var i = 0; i < damages.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var damage = damages[i];
                builder
                    .Append("Target=").Append(damage.TargetId)
                    .Append(":Amount=").Append(damage.Amount);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatSpawns(IReadOnlyList<SpawnAction> spawns)
        {
            if (spawns.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");

            for (var i = 0; i < spawns.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var spawn = spawns[i];
                builder
                    .Append("SpawnId=").Append(spawn.SpawnId)
                    .Append(":Entity=").Append(FormatEntityState(spawn.Entity));
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatDestroys(IReadOnlyList<DestroyAction> destroys)
        {
            if (destroys.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");

            for (var i = 0; i < destroys.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder
                    .Append("Target=").Append(destroys[i].TargetId)
                    .Append(":Condition=").Append(destroys[i].Condition);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatStateChanges(IReadOnlyList<StateChangeAction> stateChanges)
        {
            if (stateChanges.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");

            for (var i = 0; i < stateChanges.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var stateChange = stateChanges[i];
                builder
                    .Append("E=").Append(stateChange.EntityId)
                    .Append(":State=").Append(stateChange.State)
                    .Append(":Timer=").Append(stateChange.StateTimer);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatBoardPresenceChanges(IReadOnlyList<BoardPresenceChangeAction> boardPresenceChanges)
        {
            if (boardPresenceChanges.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");

            for (var i = 0; i < boardPresenceChanges.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var boardPresenceChange = boardPresenceChanges[i];
                builder
                    .Append("E=").Append(boardPresenceChange.EntityId)
                    .Append(":Presence=").Append(boardPresenceChange.BoardPresence);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatTopologyChanges(IReadOnlyList<TopologyChangeAction> topologyChanges)
        {
            if (topologyChanges.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");

            for (var i = 0; i < topologyChanges.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var topologyChange = topologyChanges[i];
                builder
                    .Append("Rotation=").Append(topologyChange.RotationKind)
                    .Append(":Bottom=").Append(topologyChange.UpdatedTopology.BottomFace)
                    .Append(":Front=").Append(topologyChange.UpdatedTopology.FrontFace);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatDelayedAttacks(IReadOnlyList<DelayedAttackAction> delayedAttacks)
        {
            if (delayedAttacks.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder("[");

            for (var i = 0; i < delayedAttacks.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var delayedAttack = delayedAttacks[i];
                builder
                    .Append("Target=").Append(delayedAttack.TargetId)
                    .Append(":Damage=").Append(delayedAttack.Damage);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatEntityState(EntityState entity)
        {
            return
                $"E={entity.entityId}|Pos=({entity.position.x},{entity.position.y})|Hp={entity.hp}/{entity.maxHp}|Team={entity.teamId}|Type={entity.type}|State={entity.state}|Timer={entity.stateTimer}|Facing={entity.facing}|Marked={entity.markedForDeath}|SpawnTick={entity.spawnTick}|BoxCapabilities={entity.boxCapabilities}|AiMode={entity.aiMode}|AiTimer={entity.aiStateTimer}|Face={entity.position.face}|Presence={entity.boardPresence}";
        }

        private static string FormatCell(SurfaceCell cell)
        {
            return cell.face == FaceId.Floor
                ? $"({cell.x},{cell.y})"
                : $"{cell.face}({cell.x},{cell.y})";
        }
    }
}

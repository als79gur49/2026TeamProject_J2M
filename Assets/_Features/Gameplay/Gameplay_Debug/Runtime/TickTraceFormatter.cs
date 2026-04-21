using System.Collections.Generic;
using System.Text;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
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
            var builder = new StringBuilder(2048);
            builder.Append("Tick ").Append(tickIndex.ToString("D5")).Append('\n');

            AppendSnapshotSections(builder, "S0", s0Snapshot);
            AppendSection(builder, "EnemyAi.BeforeMovementTransitions", enemyAiPhaseResult.BeforeMovementTransitions, FormatString);
            AppendSection(builder, "PreMovement.PlayerControlUpdates", preMovementStatePhaseResult.Updates, FormatString);
            AppendSection(builder, "PreMovement.PlayerActionTransitions", preMovementStatePhaseResult.PlayerActionTransitions, FormatPlayerActionTransition);
            AppendSection(builder, "Movement.RawIntents", movementPhaseResult.RawIntents, FormatRawMovementIntent);
            AppendSection(builder, "Movement.SortedIntents", movementPhaseResult.SortedIntents, FormatMoveIntent);
            AppendSection(builder, "Movement.RejectedReasons", movementPhaseResult.RejectedReasons, FormatString);
            AppendSection(builder, "Movement.Resolutions", movementPhaseResult.ResolutionRecords, FormatResolutionRecord);
            AppendSection(builder, "Movement.ResolvedOperations", movementPhaseResult.ResolvedOperations, FormatFinalizationOperation);
            AppendSection(builder, "Movement.CommitEvents", movementPhaseResult.CommitEvents, FormatString);
            AppendOccupancySection(builder, "Movement.OccupancyBefore", s0Snapshot);
            AppendOccupancySection(builder, "Movement.OccupancyAfter", s1Snapshot);

            AppendSnapshotSections(builder, "S1", s1Snapshot);
            AppendSection(builder, "EnemyAi.BeforeAttackTransitions", enemyAiPhaseResult.BeforeAttackTransitions, FormatString);
            AppendSection(builder, "EnemyAction.BeforeAttackCollectionTransitions", enemyActionPhaseResult.BeforeAttackCollectionTransitions, FormatEnemyActionTransition);
            AppendSection(builder, "Attack.RawIntents", attackPhaseResult.RawIntents, FormatRawAttackIntent);
            AppendSection(
                builder,
                "Attack.MovementReservationExport",
                new[]
                {
                    $"FreezeVersion={attackPhaseResult.FrozenMovementReservationExport.FreezeVersion}|ImpactCount={attackPhaseResult.FrozenMovementReservationExport.ImpactReservations.Count}",
                },
                FormatString);
            AppendSection(builder, "Attack.DrainedImpacts", attackPhaseResult.DrainedImpactReservations, FormatImpactReservation);
            AppendSection(builder, "Attack.DrainedDelayedEffects", attackPhaseResult.DrainedDelayedAttackEffects, FormatDelayedAttackEffectRecord);
            AppendSection(builder, "Attack.RejectedReasons", attackPhaseResult.RejectedReasons, FormatString);
            AppendSection(builder, "Attack.Resolutions", attackPhaseResult.ResolutionRecords, FormatResolutionRecord);
            AppendSection(builder, "Attack.ResolvedOperations", attackPhaseResult.ResolvedOperations, FormatFinalizationOperation);
            AppendSection(builder, "Attack.DamageResolutions", attackPhaseResult.DamageResolutions, FormatDamageResolutionRecord);
            AppendSection(builder, "Attack.QueuedDelayedEffects", attackPhaseResult.QueuedDelayedAttackEffects, FormatDelayedAttackEffectRecord);
            AppendSection(builder, "Attack.CommitEvents", attackPhaseResult.CommitEvents, FormatString);
            AppendSection(builder, "EnemyAction.AfterAttackTransitions", enemyActionPhaseResult.AfterAttackTransitions, FormatEnemyActionTransition);
            AppendSection(builder, "EnemyAi.AfterAttackTransitions", enemyAiPhaseResult.AfterAttackTransitions, FormatString);

            AppendSection(builder, "Cleanup.RemovedIds", cleanupPhaseResult.RemovedEntityIds, value => value.ToString());
            AppendSection(builder, "Cleanup.TimerChanges", cleanupPhaseResult.TimerChanges, FormatString);
            AppendSection(builder, "Cleanup.StateTransitions", cleanupPhaseResult.StateTransitions, FormatString);
            AppendSection(builder, "Respawn.Events", respawnPhaseResult.EventLogEntries, FormatString);
            AppendSection(
                builder,
                "Respawn.Entities",
                respawnPhaseResult.RespawnedEntities,
                entity => FormatEntityState(finalSnapshot, entity));

            AppendSnapshotSections(builder, "Final", finalSnapshot);
            AppendSection(builder, "Final.PendingDelayedEffects", tickResultData.PendingDelayedAttackEffects, FormatDelayedAttackEffectRecord);
            AppendSection(
                builder,
                "TickResult.FinalEntities",
                tickResultData.FinalEntities,
                entity => FormatEntityState(finalSnapshot, entity));
            AppendSection(builder, "TickResult.EventLog", tickResultData.EventLog, FormatString);
            AppendSection(builder, "DeterminismHash", new[] { determinismHash }, FormatString);

            return builder.ToString();
        }

        private static void AppendSnapshotSections(StringBuilder builder, string label, WorldSnapshot snapshot)
        {
            AppendSection(builder, $"{label}.Topology", new[] { snapshot.Topology.ToString() }, FormatString);
            AppendSection(builder, $"{label}.BoardBounds", new[] { FormatBoardBounds(snapshot.BoardBounds) }, FormatString);
            AppendSection(builder, $"{label}.Terrain", GetTerrainEntries(snapshot), FormatString);
            AppendSection(
                builder,
                $"{label}.Entities",
                GetOrderedEntities(snapshot),
                entity => FormatEntityState(snapshot, entity));
            AppendSection(builder, $"{label}.PlayerControl", GetPlayerControlEntries(snapshot), FormatString);
            AppendSection(builder, $"{label}.PlayerDamage", GetPlayerDamageEntries(snapshot), FormatString);
            AppendSection(builder, $"{label}.EnemyActions", GetEnemyActionEntries(snapshot), FormatString);
            AppendSection(builder, $"{label}.ExecutionLocks", GetExecutionLockEntries(snapshot), FormatString);
            AppendSection(builder, $"{label}.EnemyJumps", GetEnemyJumpEntries(snapshot), FormatString);
            AppendSection(builder, $"{label}.Phased", GetPhasedEntries(snapshot), FormatString);
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
            var occupancyEntries = new List<TraceOccupancyEntry>();

            var solidEntries = new List<SnapshotOccupancyEntry>();
            snapshot.EnumerateSolidOccupancyOrdered(solidEntries);
            AddOccupancyEntries(occupancyEntries, "Solid", solidEntries);

            var unitEntries = new List<SnapshotOccupancyEntry>();
            snapshot.EnumerateUnitOccupancyOrdered(unitEntries);
            AddOccupancyEntries(occupancyEntries, "Unit", unitEntries);

            var projectileEntries = new List<SnapshotOccupancyEntry>();
            snapshot.EnumerateProjectileOccupancyOrdered(projectileEntries);
            AddOccupancyEntries(occupancyEntries, "Projectile", projectileEntries);

            occupancyEntries.Sort(TraceOccupancyEntryComparer.Instance);

            var occupancyLines = new List<string>(occupancyEntries.Count);
            for (var i = 0; i < occupancyEntries.Count; i++)
            {
                var entry = occupancyEntries[i];
                occupancyLines.Add(
                    $"Layer={entry.LayerName}|Cell=({entry.Entry.Cell.x},{entry.Entry.Cell.y})|E={entry.Entry.EntityId}|Face={entry.Entry.Cell.face}");
            }

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
                    $"E={entry.EntityId}|Cooldown={entry.State.moveCooldownTicks}|NextMoveAllowed={entry.State.nextMoveAllowedTick}|Action={entry.State.activeAction.kind}|ActionSeq={entry.State.activeAction.sequence}|ActionDirection={entry.State.activeAction.direction}|ActionTarget={entry.State.activeAction.targetEntityId}|Start={entry.State.activeAction.startTick}|Execute={entry.State.activeAction.executeTick}|Recovery={entry.State.activeAction.recoveryEndTick}|Attempted={(entry.State.activeAction.executionAttempted ? 1 : 0)}");
            }

            return lines;
        }

        private static List<string> GetPlayerDamageEntries(WorldSnapshot snapshot)
        {
            var entries = new List<PlayerDamageSnapshotEntry>();
            var lines = new List<string>();
            snapshot.EnumeratePlayerDamageStatesOrdered(entries);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                lines.Add(
                    $"E={entry.EntityId}|NextDamageAllowed={entry.State.nextDamageAllowedTick}");
            }

            return lines;
        }

        private static List<string> GetEnemyActionEntries(WorldSnapshot snapshot)
        {
            var entries = new List<EnemyActionSnapshotEntry>();
            var lines = new List<string>();
            snapshot.EnumerateEnemyActionStatesOrdered(entries);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                lines.Add(
                    $"E={entry.EntityId}|Kind={entry.State.kind}|Seq={entry.State.sequence}|Target={entry.State.lockedTargetEntityId}|Direction={entry.State.direction}|Start={entry.State.startTick}|Execute={entry.State.executeTick}|Attempted={(entry.State.executionAttempted ? 1 : 0)}");
            }

            return lines;
        }

        private static List<string> GetEnemyJumpEntries(WorldSnapshot snapshot)
        {
            var entries = new List<EnemyJumpSnapshotEntry>();
            var lines = new List<string>();
            snapshot.EnumerateEnemyJumpStatesOrdered(entries);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                lines.Add(
                    $"E={entry.EntityId}|Phase={entry.State.phase}|Seq={entry.State.sequence}|Source={entry.State.sourceCell}|Locked={entry.State.lockedTargetCell}|WindupEnd={entry.State.windupEndTick}|Landing={entry.State.landingTick}|Cooldown={entry.State.cooldownRemainingTicks}|Retry={entry.State.retryCount}");
            }

            return lines;
        }

        private static List<string> GetExecutionLockEntries(WorldSnapshot snapshot)
        {
            var entries = new List<EntityExecutionLockSnapshotEntry>();
            var lines = new List<string>();
            snapshot.EnumerateEntityExecutionLockStatesOrdered(entries);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                lines.Add(
                    $"E={entry.EntityId}|Phase={entry.State.phase}|Sequence={entry.State.sequence}|UnlockTickExclusive={entry.State.unlockTickExclusive}");
            }

            return lines;
        }

        private static List<string> GetPhasedEntries(WorldSnapshot snapshot)
        {
            var entries = new List<PhasedSnapshotEntry>();
            var lines = new List<string>();
            snapshot.EnumeratePhasedStatesOrdered(entries);

            for (var i = 0; i < entries.Count; i++)
            {
                var metadataSuffix = BuildPhasedMetadataSuffix(entries[i].State.ownerKind);
                var entry = entries[i];
                lines.Add(
                    $"E={entry.EntityId}|Owner={entry.State.ownerKind}|Seq={entry.State.sequence}|Entered={entry.State.enteredTick}|ExitExclusive={entry.State.exitTickExclusive}|Active={(entry.State.IsActive ? 1 : 0)}{metadataSuffix}");
            }

            return lines;
        }

        private static List<string> GetTerrainEntries(WorldSnapshot snapshot)
        {
            var terrainLines = new List<string>();
            var terrainCells = new List<TerrainCellState>();
            snapshot.EnumerateTerrainCellsOrdered(terrainCells);

            for (var i = 0; i < terrainCells.Count; i++)
            {
                terrainLines.Add(
                    $"Cell={terrainCells[i].Cell}|Kind={terrainCells[i].Kind}|Flags={terrainCells[i].Flags}");
            }

            return terrainLines;
        }

        private static void AddOccupancyEntries(
            List<TraceOccupancyEntry> buffer,
            string layerName,
            IReadOnlyList<SnapshotOccupancyEntry> entries)
        {
            if (entries.Count == 0)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                buffer.Add(new TraceOccupancyEntry(layerName, entries[i]));
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

        private static string FormatPlayerActionTransition(PlayerActionTransition transition)
        {
            return $"E={transition.EntityId}|Prev={transition.PreviousKind}|Curr={transition.CurrentKind}|PrevSeq={transition.PreviousSequence}|CurrSeq={transition.CurrentSequence}|Started={transition.StartedThisTick}|Completed={transition.CompletedThisTick}|Canceled={transition.CanceledThisTick}";
        }

        private static string FormatEnemyActionTransition(EnemyActionTransition transition)
        {
            return $"E={transition.EntityId}|Prev={transition.PreviousKind}|Curr={transition.CurrentKind}|PrevSeq={transition.PreviousSequence}|CurrSeq={transition.CurrentSequence}|Started={transition.StartedThisTick}|Canceled={transition.CanceledThisTick}";
        }

        private static string FormatDamageResolutionRecord(DamageResolutionRecord record)
        {
#pragma warning disable CS0618
            return $"Plan={record.ActionPlanId}|Intent={record.IntentId}|Source={record.SourceId}|SourceKind={record.SourceKind}|Target={record.TargetId}|Amount={record.Amount}|Accepted={(record.Accepted ? 1 : 0)}|RejectReason={record.RejectReason}";
#pragma warning restore CS0618
        }

        private static string FormatResolutionRecord(ResolutionRecord record)
        {
            return $"Contest={record.ContestId}|Kind={record.Kind}|Accepted={(record.Accepted ? 1 : 0)}|Source={record.SourceId}|Priority={record.Priority}|Plan={record.ActionPlanId}|Affected={record.AffectedEntityId}|Local={record.LocalActionIndex}";
        }

        private static string FormatFinalizationOperation(FinalizationOperation operation)
        {
            var builder = new StringBuilder();
            builder
                .Append("Seq=").Append(operation.Sequence)
                .Append("|Bucket=").Append(operation.Bucket)
                .Append("|Kind=").Append(operation.Kind)
                .Append("|Origin=").Append(operation.Metadata.OriginPhase)
                .Append("|Semantic=").Append(operation.Metadata.SemanticKind)
                .Append("|MoveSemantic=").Append(operation.Metadata.MovementSemanticKind)
                .Append("|DamageSource=").Append(operation.Metadata.DamageSourceType)
                .Append("|Jump=").Append(operation.Metadata.JumpPresentationKind)
                .Append("|Source=").Append(operation.Metadata.SourceActorEntityId)
                .Append("|Plan=").Append(operation.Metadata.ActionPlanId)
                .Append("|Intent=").Append(operation.Metadata.IntentId)
                .Append("|Contest=").Append(operation.Metadata.ContestId)
                .Append("|Local=").Append(operation.Metadata.LocalActionIndex)
                .Append("|Priority=").Append(operation.Metadata.Priority)
                .Append("|Entity=").Append(operation.EntityId);

            switch (operation.Kind)
            {
                case FinalizationOperationKind.MoveEntity:
                    builder.Append("|Dest=").Append(FormatCell(operation.Destination));
                    break;

                case FinalizationOperationKind.ApplyStateChange:
                    builder.Append("|State=").Append(operation.PhaseState).Append("|Timer=").Append(operation.StateTimer);
                    break;

                case FinalizationOperationKind.SetFacing:
                    builder.Append("|Facing=").Append(operation.Facing);
                    break;

                case FinalizationOperationKind.SetBoardPresence:
                    builder.Append("|Presence=").Append(operation.BoardPresence);
                    break;

                case FinalizationOperationKind.SetPhasedState:
                    builder.Append("|Owner=").Append(operation.PhasedState.ownerKind)
                        .Append("|PhaseSeq=").Append(operation.PhasedState.sequence)
                        .Append("|Entered=").Append(operation.PhasedState.enteredTick)
                        .Append("|ExitExclusive=").Append(operation.PhasedState.exitTickExclusive)
                        .Append("|Active=").Append(operation.PhasedState.IsActive ? 1 : 0)
                        .Append(BuildPhasedMetadataSuffix(operation.PhasedState.ownerKind));
                    break;

                case FinalizationOperationKind.SetTopology:
                    builder.Append("|Rotation=").Append(operation.Metadata.RotationKind)
                        .Append("|Bottom=").Append(operation.Topology.BottomFace)
                        .Append("|Front=").Append(operation.Topology.FrontFace);
                    break;

                case FinalizationOperationKind.ApplyDamage:
                    builder.Append("|Amount=").Append(operation.Amount)
                        .Append("|SourceKind=").Append(operation.Metadata.AttackSourceKind);
                    break;

                case FinalizationOperationKind.MarkDestroy:
                    builder.Append("|ExitCause=").Append(operation.Metadata.ExitCauseHint);
                    break;

                case FinalizationOperationKind.SpawnEntity:
                    builder.Append("|SpawnE=").Append(operation.SpawnedEntity.entityId)
                        .Append("|Pos=").Append(FormatCell(operation.SpawnedEntity.position))
                        .Append("|Type=").Append(operation.SpawnedEntity.type);
                    break;

                case FinalizationOperationKind.EnqueueDelayedAttackEffect:
                    builder.Append("|DelayedSource=").Append(operation.DelayedAttackEffect.SourceId)
                        .Append("|DelayedTarget=").Append(operation.DelayedAttackEffect.TargetId)
                        .Append("|Damage=").Append(operation.DelayedAttackEffect.Damage)
                        .Append("|ExecuteTick=").Append(operation.DelayedAttackEffect.ExecuteAtTick)
                        .Append("|EffectSequence=").Append(operation.DelayedAttackEffect.EffectSequence);
                    break;
            }

            return builder.ToString();
        }

        private static string BuildPhasedMetadataSuffix(PhasedRuntimeStateOwnerKind ownerKind)
        {
            if (!PhasedSourceMetadataCatalog.TryGet(ownerKind, out var metadata))
            {
                return string.Empty;
            }

            var existingEnemyLock = metadata.TargetabilityMode == PhasedTargetabilityMode.FreshSelectionSuppressedWithCurrentEnemyLockRetention
                ? "Retained(StageScopedContract)"
                : "Deferred";
            return $"|Stage={metadata.EmittingStage}|Timing={metadata.TimingRow}|Targetability={metadata.TargetabilityMode}|Settle={metadata.RequestedTerminalSettleMode}(StageDefault)|ReservationRead={metadata.ReservationReadClass}|Earliest={metadata.EarliestObservableSnapshot}|OccupancyClaim=True(StageDefault)|FreshTarget=Suppressed(ConfirmedContract)|ExistingEnemyLock={existingEnemyLock}";
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
                return $"Source={rawIntent.SourceId}|Priority={rawIntent.Priority}|SourceKind={rawIntent.SourceKind}|TargetCell=({rawIntent.TargetCell.x},{rawIntent.TargetCell.y})|Command={rawIntent.CommandKind}|LocalSequence={rawIntent.LocalSequence}";
            }

            return $"Source={rawIntent.SourceId}|Priority={rawIntent.Priority}|SourceKind={rawIntent.SourceKind}|Target={rawIntent.TargetId}|Command={rawIntent.CommandKind}|LocalSequence={rawIntent.LocalSequence}";
        }

        private static string FormatImpactReservation(ImpactReservation reservation)
        {
            return
                $"Reservation|Source={reservation.SourceId}|Target={reservation.TargetId}|Position={FormatCell(reservation.ImpactCell)}|Damage={reservation.Damage}|Tick={reservation.TickGenerated}";
        }

        private static string FormatDelayedAttackEffectRecord(DelayedAttackEffectRecord effectRecord)
        {
            return
                $"DelayedAttack|Source={effectRecord.SourceId}|Target={effectRecord.TargetId}|Damage={effectRecord.Damage}|Priority={effectRecord.Priority}|GeneratedTick={effectRecord.TickGenerated}|ExecuteTick={effectRecord.ExecuteAtTick}|SourcePlan={effectRecord.SourceActionPlanId}|Sequence={effectRecord.EffectSequence}";
        }

        private readonly struct TraceOccupancyEntry
        {
            public TraceOccupancyEntry(string layerName, SnapshotOccupancyEntry entry)
            {
                LayerName = layerName;
                Entry = entry;
            }

            public string LayerName { get; }

            public SnapshotOccupancyEntry Entry { get; }
        }

        private sealed class TraceOccupancyEntryComparer : IComparer<TraceOccupancyEntry>
        {
            internal static readonly TraceOccupancyEntryComparer Instance = new();

            public int Compare(TraceOccupancyEntry left, TraceOccupancyEntry right)
            {
                var result = left.Entry.Cell.face.CompareTo(right.Entry.Cell.face);
                if (result != 0)
                {
                    return result;
                }

                result = left.Entry.Cell.x.CompareTo(right.Entry.Cell.x);
                if (result != 0)
                {
                    return result;
                }

                result = left.Entry.Cell.y.CompareTo(right.Entry.Cell.y);
                if (result != 0)
                {
                    return result;
                }

                result = left.Entry.EntityId.CompareTo(right.Entry.EntityId);
                if (result != 0)
                {
                    return result;
                }

                return string.CompareOrdinal(left.LayerName, right.LayerName);
            }
        }

        private static string FormatEntityState(WorldSnapshot snapshot, EntityState entity)
        {
            var spatialState = default(ResolvedSpatialState);
            var hasSpatialState = snapshot != null &&
                                  snapshot.TryGetResolvedSpatialState(entity.entityId, out spatialState);
            var spatialKind = hasSpatialState ? spatialState.Kind.ToString() : "Unknown";
            var spatialOccClaim = hasSpatialState ? (spatialState.ClaimsAuthoritativeOccupancy ? 1 : 0) : -1;
            var spatialGameplayVisible = hasSpatialState ? (spatialState.IsGameplayVisible ? 1 : 0) : -1;
            var spatialSource = hasSpatialState ? spatialState.Source.ToString() : "Unknown";
            return
                $"E={entity.entityId}|Pos=({entity.position.x},{entity.position.y})|Hp={entity.hp}/{entity.maxHp}|Team={entity.teamId}|Type={entity.type}|State={entity.state}|Timer={entity.stateTimer}|Facing={entity.facing}|Marked={entity.markedForDeath}|SpawnTick={entity.spawnTick}|BoxCapabilities={entity.boxCapabilities}|KineticInstigator={entity.kineticInstigatorEntityId}|KineticTeam={entity.kineticInstigatorTeamId}|AiMode={entity.aiMode}|AiTimer={entity.aiStateTimer}|LocomotionCooldown={entity.enemyLocomotionCooldownTicks}|Face={entity.position.face}|Presence={entity.boardPresence}|SpatialKind={spatialKind}|SpatialOccClaim={spatialOccClaim}|SpatialGameplayVisible={spatialGameplayVisible}|SpatialSource={spatialSource}";
        }

        private static string FormatCell(SurfaceCell cell)
        {
            return cell.face == FaceId.Floor
                ? $"({cell.x},{cell.y})"
                : $"{cell.face}({cell.x},{cell.y})";
        }
    }
}

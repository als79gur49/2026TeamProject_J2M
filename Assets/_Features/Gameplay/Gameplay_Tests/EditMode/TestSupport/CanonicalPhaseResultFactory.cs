using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Tests
{
    internal static class CanonicalPhaseResultFactory
    {
        public static MovementPhaseResult CreateMovementPhaseResult(
            IEnumerable<RawMovementIntent> rawIntents,
            IEnumerable<MoveIntent> sortedIntents,
            IEnumerable<ActionGroup> selectedGroups,
            IEnumerable<string> commitEvents,
            IEnumerable<string> rejectedReasons,
            Func<ActionGroup, ResolvedActionSemanticKind> semanticResolver = null,
            IEnumerable<ImpactDispositionResolutionRecord> impactDispositionRecords = null,
            IEnumerable<BoxSlideStopResult> boxSlideStops = null)
        {
            var groups = Materialize(selectedGroups);
            var resolutionRecords = new List<ResolutionRecord>(groups.Count);
            var operations = new List<FinalizationOperation>();
            var materializedImpactDispositionRecords = impactDispositionRecords != null
                ? new List<ImpactDispositionResolutionRecord>(impactDispositionRecords)
                : new List<ImpactDispositionResolutionRecord>();
            var sequence = 1L;

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                var semanticKind = semanticResolver?.Invoke(group) ?? ResolveMovementSemanticKind(group);
                var actionPlanId = ResolveActionPlanId(group, groupIndex);
                resolutionRecords.Add(
                    new ResolutionRecord(
                        contestId: actionPlanId,
                        kind: ContestKind.Space,
                        accepted: true,
                        sourceId: group.SourceId,
                        priority: group.Priority,
                        actionPlanId: actionPlanId,
                        affectedEntityId: group.SourceId,
                        localActionIndex: 0));

                for (var stateIndex = 0; stateIndex < group.StateChanges.Count; stateIndex++)
                {
                    var stateChange = group.StateChanges[stateIndex];
                    operations.Add(
                        FinalizationOperation.ApplyStateChange(
                            sequence++,
                            stateChange.EntityId,
                            stateChange.State,
                            stateChange.StateTimer,
                            CreateMovementMetadata(group, actionPlanId, semanticKind, stateIndex)));
                }

                for (var presenceIndex = 0; presenceIndex < group.BoardPresenceChanges.Count; presenceIndex++)
                {
                    var boardPresence = group.BoardPresenceChanges[presenceIndex];
                    operations.Add(
                        FinalizationOperation.SetBoardPresence(
                            sequence++,
                            boardPresence.EntityId,
                            boardPresence.BoardPresence,
                            CreateMovementMetadata(
                                group,
                                actionPlanId,
                                semanticKind,
                                presenceIndex,
                                exitCauseHint: ResolveMovementExitCause(group))));
                }

                for (var topologyIndex = 0; topologyIndex < group.TopologyChanges.Count; topologyIndex++)
                {
                    var topologyChange = group.TopologyChanges[topologyIndex];
                    operations.Add(
                        FinalizationOperation.SetTopology(
                            sequence++,
                            topologyChange.UpdatedTopology,
                            CreateMovementMetadata(
                                group,
                                actionPlanId,
                                semanticKind,
                                topologyIndex,
                                rotationKind: topologyChange.RotationKind)));
                }

                for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                {
                    var move = group.Moves[moveIndex];
                    operations.Add(
                        FinalizationOperation.MoveEntity(
                            sequence++,
                            move.EntityId,
                            move.Destination,
                            CreateMovementMetadata(group, actionPlanId, semanticKind, moveIndex)));
                }

                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    var destroy = group.Destroys[destroyIndex];
                    resolutionRecords.Add(
                        new ResolutionRecord(
                            contestId: actionPlanId,
                            kind: ContestKind.Destroy,
                            accepted: true,
                            sourceId: group.SourceId,
                            priority: group.Priority,
                            actionPlanId: actionPlanId,
                            affectedEntityId: destroy.TargetId,
                            localActionIndex: destroyIndex));
                    operations.Add(
                        FinalizationOperation.MarkDestroy(
                            sequence++,
                            destroy.TargetId,
                            CreateMovementMetadata(
                                group,
                                actionPlanId,
                                semanticKind,
                                destroyIndex,
                                exitCauseHint: ResolveMovementExitCause(group))));
                }
            }

            return new MovementPhaseResult(
                rawIntents,
                sortedIntents,
                resolutionRecords,
                materializedImpactDispositionRecords,
                operations,
                commitEvents,
                rejectedReasons,
                boxSlideStops: boxSlideStops);
        }

        public static MovementPhaseResult CreateMovementPhaseResult(params ActionGroup[] selectedGroups)
        {
            return CreateMovementPhaseResult(
                Array.Empty<RawMovementIntent>(),
                Array.Empty<MoveIntent>(),
                selectedGroups,
                BuildMovementCommitEvents(Materialize(selectedGroups)),
                Array.Empty<string>());
        }

        public static AttackPhaseResult CreateAttackPhaseResult(
            IEnumerable<RawAttackIntent> rawIntents,
            IEnumerable<ImpactReservation> drainedImpactReservations,
            IEnumerable<DelayedAttackEffectRecord> drainedDelayedAttackEffects,
            IEnumerable<DamageResolutionRecord> damageResolutions,
            IEnumerable<ActionGroup> selectedGroups,
            IEnumerable<string> commitEvents,
            IEnumerable<string> eventLogEntries,
            IEnumerable<string> rejectedReasons)
        {
            var groups = Materialize(selectedGroups);
            var resolutionRecords = new List<ResolutionRecord>();
            var operations = new List<FinalizationOperation>();
            var queuedDelayedAttackEffects = new List<DelayedAttackEffectRecord>();
            var sequence = 1L;

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                var actionPlanId = ResolveActionPlanId(group, groupIndex);
                resolutionRecords.Add(
                    new ResolutionRecord(
                        contestId: actionPlanId,
                        kind: ContestKind.Plan,
                        accepted: true,
                        sourceId: group.SourceId,
                        priority: group.Priority,
                        actionPlanId: actionPlanId,
                        affectedEntityId: group.SourceId,
                        localActionIndex: 0));

                for (var stateIndex = 0; stateIndex < group.StateChanges.Count; stateIndex++)
                {
                    var stateChange = group.StateChanges[stateIndex];
                    operations.Add(
                        FinalizationOperation.ApplyStateChange(
                            sequence++,
                            stateChange.EntityId,
                            stateChange.State,
                            stateChange.StateTimer,
                            CreateAttackMetadata(group, actionPlanId, stateIndex)));
                }

                for (var damageIndex = 0; damageIndex < group.Damages.Count; damageIndex++)
                {
                    var damage = group.Damages[damageIndex];
                    resolutionRecords.Add(
                        new ResolutionRecord(
                            contestId: actionPlanId,
                            kind: ContestKind.Damage,
                            accepted: true,
                            sourceId: group.SourceId,
                            priority: group.Priority,
                            actionPlanId: actionPlanId,
                            affectedEntityId: damage.TargetId,
                            localActionIndex: damageIndex));
                    operations.Add(
                        FinalizationOperation.ApplyDamage(
                            sequence++,
                            damage.TargetId,
                            damage.Amount,
                            CreateAttackMetadata(group, actionPlanId, damageIndex)));
                }

                for (var spawnIndex = 0; spawnIndex < group.Spawns.Count; spawnIndex++)
                {
                    var spawn = group.Spawns[spawnIndex];
                    operations.Add(
                        FinalizationOperation.SpawnEntity(
                            sequence++,
                            spawn.Entity,
                            CreateAttackMetadata(group, actionPlanId, spawnIndex)));
                }

                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    var destroy = group.Destroys[destroyIndex];
                    resolutionRecords.Add(
                        new ResolutionRecord(
                            contestId: actionPlanId,
                            kind: ContestKind.Destroy,
                            accepted: true,
                            sourceId: group.SourceId,
                            priority: group.Priority,
                            actionPlanId: actionPlanId,
                            affectedEntityId: destroy.TargetId,
                            localActionIndex: destroyIndex));
                    operations.Add(
                        FinalizationOperation.MarkDestroy(
                            sequence++,
                            destroy.TargetId,
                            CreateAttackMetadata(
                                group,
                                actionPlanId,
                                destroyIndex,
                                exitCauseHint: TickEntityExitCause.Killed)));
                }

                for (var delayedIndex = 0; delayedIndex < group.DelayedAttacks.Count; delayedIndex++)
                {
                    var delayedAttack = group.DelayedAttacks[delayedIndex];
                    var effectRecord = new DelayedAttackEffectRecord(
                        group.SourceId,
                        delayedAttack.TargetId,
                        delayedAttack.Damage,
                        group.Priority,
                        tickGenerated: 0,
                        executeAtTick: 1,
                        sourceActionPlanId: actionPlanId,
                        effectSequence: delayedIndex + 1);
                    queuedDelayedAttackEffects.Add(effectRecord);
                    operations.Add(
                        FinalizationOperation.EnqueueDelayedAttackEffect(
                            sequence++,
                            effectRecord,
                            CreateAttackMetadata(
                                group,
                                actionPlanId,
                                delayedIndex,
                                attackSourceKind: AttackSourceKind.DelayedEffect)));
                }
            }

            return new AttackPhaseResult(
                rawIntents,
                drainedImpactReservations,
                drainedDelayedAttackEffects,
                damageResolutions,
                resolutionRecords,
                operations,
                queuedDelayedAttackEffects,
                commitEvents,
                eventLogEntries,
                rejectedReasons);
        }

        public static AttackPhaseResult CreateAttackPhaseResult(params ActionGroup[] selectedGroups)
        {
            var groups = Materialize(selectedGroups);
            var commitEvents = BuildAttackCommitEvents(groups);
            return CreateAttackPhaseResult(
                Array.Empty<RawAttackIntent>(),
                Array.Empty<ImpactReservation>(),
                Array.Empty<DelayedAttackEffectRecord>(),
                BuildAcceptedDamageResolutions(groups),
                groups,
                commitEvents,
                commitEvents,
                Array.Empty<string>());
        }

        private static List<ActionGroup> Materialize(IEnumerable<ActionGroup> selectedGroups)
        {
            return selectedGroups == null ? new List<ActionGroup>() : new List<ActionGroup>(selectedGroups);
        }

        private static int ResolveActionPlanId(ActionGroup group, int index)
        {
            return group.GroupId > 0 ? group.GroupId : index + 1;
        }

        private static ResolvedActionSemanticKind ResolveMovementSemanticKind(ActionGroup group)
        {
            return group.GroupKind switch
            {
                ActionGroupKind.Move => ResolvedActionSemanticKind.Move,
                ActionGroupKind.Item => ResolvedActionSemanticKind.Item,
                ActionGroupKind.Push when HasMovedNonSourceEntity(group) => ResolvedActionSemanticKind.Slide,
                ActionGroupKind.Push => ResolvedActionSemanticKind.Push,
                ActionGroupKind.Flip => ResolvedActionSemanticKind.Flip,
                ActionGroupKind.BoxImpact => ResolvedActionSemanticKind.Impact,
                ActionGroupKind.ProjectileImpact => ResolvedActionSemanticKind.Impact,
                ActionGroupKind.Stop => ResolvedActionSemanticKind.Stop,
                _ => ResolvedActionSemanticKind.Move,
            };
        }

        private static bool HasMovedNonSourceEntity(ActionGroup group)
        {
            for (var i = 0; i < group.Moves.Count; i++)
            {
                if (group.Moves[i].EntityId != group.SourceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static TickEntityExitCause ResolveMovementExitCause(ActionGroup group)
        {
            return group.GroupKind switch
            {
                ActionGroupKind.Item => TickEntityExitCause.ItemConsume,
                ActionGroupKind.BoxImpact => TickEntityExitCause.DestroyedByImpact,
                ActionGroupKind.ProjectileImpact => TickEntityExitCause.DestroyedByImpact,
                _ => TickEntityExitCause.None,
            };
        }

        private static FinalizationOperationMetadata CreateMovementMetadata(
            ActionGroup group,
            int actionPlanId,
            ResolvedActionSemanticKind semanticKind,
            int localActionIndex,
            CubeRotationKind rotationKind = CubeRotationKind.None,
            TickEntityExitCause exitCauseHint = TickEntityExitCause.None)
        {
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                semanticKind,
                group.SourceId,
                actionPlanId,
                group.IntentId,
                contestId: actionPlanId,
                localActionIndex: localActionIndex,
                priority: group.Priority,
                rotationKind: rotationKind,
                exitCauseHint: exitCauseHint,
                movementSemanticKind: ResolveMovementSemanticKind(semanticKind),
                damageSourceType: DamageSourceType.None,
                movementExecutionBoundaryKind: ResolveMovementExecutionBoundaryKind(group),
                boundaryReason: ResolveMovementExecutionBoundaryReason(ResolveMovementExecutionBoundaryKind(group)));
        }

        private static MovementExecutionBoundaryKind ResolveMovementExecutionBoundaryKind(ActionGroup group)
        {
            if (group.TopologyChanges.Count > 0)
            {
                return MovementExecutionBoundaryKind.TopologyMaterialization;
            }

            if (group.GroupKind == ActionGroupKind.Push ||
                group.GroupKind == ActionGroupKind.Flip ||
                group.GroupKind == ActionGroupKind.Item ||
                group.GroupKind == ActionGroupKind.BoxImpact ||
                group.GroupKind == ActionGroupKind.Stop ||
                group.HasResolvedImpact ||
                group.HasDeferredImpact)
            {
                return MovementExecutionBoundaryKind.BoxActionMovement;
            }

            if (group.GroupKind == ActionGroupKind.Move)
            {
                return MovementExecutionBoundaryKind.LegacyFallback;
            }

            return MovementExecutionBoundaryKind.Unknown;
        }

        private static string ResolveMovementExecutionBoundaryReason(MovementExecutionBoundaryKind kind)
        {
            return kind switch
            {
                MovementExecutionBoundaryKind.TopologyMaterialization => "TopologyMaterialization",
                MovementExecutionBoundaryKind.BoxActionMovement => "BoxActionMovement",
                MovementExecutionBoundaryKind.LegacyFallback => "LegacyFallback",
                _ => string.Empty,
            };
        }

        private static FinalizationOperationMetadata CreateAttackMetadata(
            ActionGroup group,
            int actionPlanId,
            int localActionIndex,
            AttackSourceKind attackSourceKind = AttackSourceKind.Combat,
            TickEntityExitCause exitCauseHint = TickEntityExitCause.None)
        {
            var resolvedAttackSourceKind = attackSourceKind == AttackSourceKind.Combat ? group.AttackSourceKind : attackSourceKind;
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                ResolvedActionSemanticKind.Attack,
                group.SourceId,
                actionPlanId,
                group.IntentId,
                contestId: actionPlanId,
                localActionIndex: localActionIndex,
                priority: group.Priority,
                exitCauseHint: exitCauseHint,
                attackSourceKind: resolvedAttackSourceKind,
                movementSemanticKind: MovementSemanticKind.None,
                damageSourceType: ResolveDamageSourceType(resolvedAttackSourceKind));
        }

        private static MovementSemanticKind ResolveMovementSemanticKind(ResolvedActionSemanticKind semanticKind)
        {
            return semanticKind switch
            {
                ResolvedActionSemanticKind.Move => MovementSemanticKind.Move,
                ResolvedActionSemanticKind.Push => MovementSemanticKind.Push,
                ResolvedActionSemanticKind.Flip => MovementSemanticKind.Flip,
                ResolvedActionSemanticKind.Slide => MovementSemanticKind.Slide,
                ResolvedActionSemanticKind.Impact => MovementSemanticKind.Impact,
                ResolvedActionSemanticKind.JumpLanding => MovementSemanticKind.JumpLanding,
                ResolvedActionSemanticKind.ProjectileMove => MovementSemanticKind.ProjectileMove,
                ResolvedActionSemanticKind.Item => MovementSemanticKind.Item,
                ResolvedActionSemanticKind.Stop => MovementSemanticKind.Stop,
                _ => MovementSemanticKind.None,
            };
        }

        private static DamageSourceType ResolveDamageSourceType(AttackSourceKind attackSourceKind)
        {
            return attackSourceKind switch
            {
                AttackSourceKind.Combat => DamageSourceType.Attack,
                AttackSourceKind.DelayedEffect => DamageSourceType.Attack,
                AttackSourceKind.ForwardCellImpact => DamageSourceType.Attack,
                AttackSourceKind.ImpactReservation => DamageSourceType.Impact,
                AttackSourceKind.B1ScheduledContactDue => DamageSourceType.Impact,
                AttackSourceKind.PassiveContact => DamageSourceType.Environmental,
                _ => DamageSourceType.None,
            };
        }

        private static IReadOnlyList<string> BuildMovementCommitEvents(IReadOnlyList<ActionGroup> selectedGroups)
        {
            var commitEvents = new List<string>();
            var sequence = 1;
            for (var i = 0; i < selectedGroups.Count; i++)
            {
                var group = selectedGroups[i];
                if (!group.HasResolvedImpact)
                {
                    continue;
                }

                for (var targetIndex = 0; targetIndex < group.ImpactTargetIds.Count; targetIndex++)
                {
                    commitEvents.Add(
                        $"ImpactReservationCreated|G={ResolveActionPlanId(group, i)}|I={group.IntentId}|Source={group.ImpactSourceId}|Target={group.ImpactTargetIds[targetIndex]}|At=(0,0)|Damage=1|Sequence={sequence++}");
                }
            }

            return commitEvents;
        }

        private static IReadOnlyList<string> BuildAttackCommitEvents(IReadOnlyList<ActionGroup> selectedGroups)
        {
            var commitEvents = new List<string>();
            for (var i = 0; i < selectedGroups.Count; i++)
            {
                var group = selectedGroups[i];
                var actionPlanId = ResolveActionPlanId(group, i);
                for (var spawnIndex = 0; spawnIndex < group.Spawns.Count; spawnIndex++)
                {
                    var spawn = group.Spawns[spawnIndex];
                    commitEvents.Add(
                        $"SpawnCommitted|G={actionPlanId}|I={group.IntentId}|SpawnId={spawn.SpawnId}|E={spawn.Entity.entityId}|Pos=({spawn.Entity.position.x},{spawn.Entity.position.y})|Type={spawn.Entity.type}|SpawnTick={spawn.Entity.spawnTick}");
                }
            }

            return commitEvents;
        }

        private static IReadOnlyList<DamageResolutionRecord> BuildAcceptedDamageResolutions(IReadOnlyList<ActionGroup> selectedGroups)
        {
            var damageResolutions = new List<DamageResolutionRecord>();
            for (var i = 0; i < selectedGroups.Count; i++)
            {
                var group = selectedGroups[i];
                var actionPlanId = ResolveActionPlanId(group, i);
                for (var damageIndex = 0; damageIndex < group.Damages.Count; damageIndex++)
                {
                    var damage = group.Damages[damageIndex];
                    damageResolutions.Add(
                        new DamageResolutionRecord(
                            actionPlanId,
                            group.IntentId,
                            group.SourceId,
                            group.AttackSourceKind,
                            damage.TargetId,
                            damage.Amount,
                            accepted: true,
                            rejectReason: DamageRejectReason.None,
                            localActionIndex: damageIndex));
                }
            }

            return damageResolutions;
        }
    }
}

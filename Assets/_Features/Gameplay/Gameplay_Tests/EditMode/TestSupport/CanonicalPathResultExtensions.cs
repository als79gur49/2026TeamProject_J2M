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
    internal static class CanonicalPathResultExtensions
    {
        public static IReadOnlyList<CanonicalResolvedAction> ResolveAcceptedActions(this MovementPhaseResult result)
        {
            return ResolveAcceptedActions(
                result.ResolutionRecords,
                result.ResolvedOperations,
                result.CommitEvents,
                ContestKind.Space,
                isMovement: true);
        }

        public static IReadOnlyList<CanonicalResolvedAction> ResolveAcceptedActions(this AttackPhaseResult result)
        {
            return ResolveAcceptedActions(
                result.ResolutionRecords,
                result.ResolvedOperations,
                result.CommitEvents,
                ContestKind.Plan,
                isMovement: false);
        }

        private static IReadOnlyList<CanonicalResolvedAction> ResolveAcceptedActions(
            IReadOnlyList<ResolutionRecord> resolutionRecords,
            IReadOnlyList<FinalizationOperation> operations,
            IReadOnlyList<string> commitEvents,
            ContestKind acceptedContestKind,
            bool isMovement)
        {
            var actions = new List<CanonicalResolvedAction>();
            var indexedOperations = IndexOperationsByActionPlanId(operations);
            var indexedImpactMetadata = IndexImpactMetadata(commitEvents);
            var indexedSpawnIds = IndexSpawnIds(commitEvents);

            for (var i = 0; i < resolutionRecords.Count; i++)
            {
                var resolution = resolutionRecords[i];
                if (resolution.Kind != acceptedContestKind ||
                    !resolution.Accepted ||
                    resolution.LocalActionIndex != 0)
                {
                    continue;
                }

                indexedOperations.TryGetValue(resolution.ActionPlanId, out var actionOperations);
                indexedImpactMetadata.TryGetValue(resolution.ActionPlanId, out var impactMetadata);
                indexedSpawnIds.TryGetValue(resolution.ActionPlanId, out var spawnIds);
                actions.Add(
                    CanonicalResolvedAction.Create(
                        resolution,
                        actionOperations != null ? actionOperations : Array.Empty<FinalizationOperation>(),
                        impactMetadata,
                        spawnIds != null ? spawnIds : Array.Empty<int>(),
                        isMovement));
            }

            return actions;
        }

        private static Dictionary<int, List<FinalizationOperation>> IndexOperationsByActionPlanId(
            IReadOnlyList<FinalizationOperation> operations)
        {
            var indexed = new Dictionary<int, List<FinalizationOperation>>();
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (!indexed.TryGetValue(operation.Metadata.ActionPlanId, out var list))
                {
                    list = new List<FinalizationOperation>();
                    indexed[operation.Metadata.ActionPlanId] = list;
                }

                list.Add(operation);
            }

            return indexed;
        }

        private static Dictionary<int, CanonicalImpactMetadata> IndexImpactMetadata(IReadOnlyList<string> commitEvents)
        {
            var indexed = new Dictionary<int, CanonicalImpactMetadata>();
            for (var i = 0; i < commitEvents.Count; i++)
            {
                var commitEvent = commitEvents[i];
                if (!commitEvent.StartsWith("ImpactReservationCreated|G=", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = commitEvent.Split('|');
                var groupId = 0;
                var sourceId = 0;
                var targetId = 0;
                for (var partIndex = 0; partIndex < parts.Length; partIndex++)
                {
                    var part = parts[partIndex];
                    if (part.StartsWith("G=", StringComparison.Ordinal))
                    {
                        int.TryParse(part.AsSpan(2), out groupId);
                    }
                    else if (part.StartsWith("Source=", StringComparison.Ordinal))
                    {
                        int.TryParse(part.AsSpan("Source=".Length), out sourceId);
                    }
                    else if (part.StartsWith("Target=", StringComparison.Ordinal))
                    {
                        int.TryParse(part.AsSpan("Target=".Length), out targetId);
                    }
                }

                if (groupId > 0)
                {
                    indexed[groupId] = new CanonicalImpactMetadata(sourceId, targetId);
                }
            }

            return indexed;
        }

        private static Dictionary<int, List<int>> IndexSpawnIds(IReadOnlyList<string> commitEvents)
        {
            var indexed = new Dictionary<int, List<int>>();
            for (var i = 0; i < commitEvents.Count; i++)
            {
                var commitEvent = commitEvents[i];
                if (!commitEvent.StartsWith("SpawnCommitted|G=", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = commitEvent.Split('|');
                var groupId = 0;
                var spawnId = 0;
                for (var partIndex = 0; partIndex < parts.Length; partIndex++)
                {
                    var part = parts[partIndex];
                    if (part.StartsWith("G=", StringComparison.Ordinal))
                    {
                        int.TryParse(part.AsSpan(2), out groupId);
                    }
                    else if (part.StartsWith("SpawnId=", StringComparison.Ordinal))
                    {
                        int.TryParse(part.AsSpan("SpawnId=".Length), out spawnId);
                    }
                }

                if (groupId <= 0)
                {
                    continue;
                }

                if (!indexed.TryGetValue(groupId, out var spawnIds))
                {
                    spawnIds = new List<int>();
                    indexed[groupId] = spawnIds;
                }

                spawnIds.Add(spawnId);
            }

            return indexed;
        }

    }

    internal readonly struct CanonicalImpactMetadata
    {
        public CanonicalImpactMetadata(int sourceId, int targetId)
        {
            SourceId = sourceId;
            TargetId = targetId;
        }

        public int SourceId { get; }

        public int TargetId { get; }
    }

    internal sealed class CanonicalResolvedAction
    {
        private CanonicalResolvedAction(
            int groupId,
            int intentId,
            int sourceId,
            int priority,
            ActionGroupKind groupKind,
            IReadOnlyList<MoveAction> moves,
            IReadOnlyList<DamageAction> damages,
            IReadOnlyList<SpawnAction> spawns,
            IReadOnlyList<DestroyAction> destroys,
            IReadOnlyList<StateChangeAction> stateChanges,
            IReadOnlyList<TopologyChangeAction> topologyChanges,
            int impactSourceId,
            int impactTargetId,
            int projectileImpactTargetId)
        {
            GroupId = groupId;
            IntentId = intentId;
            SourceId = sourceId;
            Priority = priority;
            GroupKind = groupKind;
            Moves = moves;
            Damages = damages;
            Spawns = spawns;
            Destroys = destroys;
            StateChanges = stateChanges;
            TopologyChanges = topologyChanges;
            ImpactSourceId = impactSourceId;
            ImpactTargetId = impactTargetId;
            ProjectileImpactTargetId = projectileImpactTargetId;
        }

        public int GroupId { get; }

        public int IntentId { get; }

        public int SourceId { get; }

        public int Priority { get; }

        public ActionGroupKind GroupKind { get; }

        public IReadOnlyList<MoveAction> Moves { get; }

        public IReadOnlyList<DamageAction> Damages { get; }

        public IReadOnlyList<SpawnAction> Spawns { get; }

        public IReadOnlyList<DestroyAction> Destroys { get; }

        public IReadOnlyList<StateChangeAction> StateChanges { get; }

        public IReadOnlyList<TopologyChangeAction> TopologyChanges { get; }

        public int ImpactSourceId { get; }

        public int ImpactTargetId { get; }

        public int ProjectileImpactTargetId { get; }

        public static CanonicalResolvedAction Create(
            ResolutionRecord resolutionRecord,
            IReadOnlyList<FinalizationOperation> operations,
            object impactMetadataObject,
            IReadOnlyList<int> spawnIds,
            bool isMovement)
        {
            var moves = new List<MoveAction>();
            var damages = new List<DamageAction>();
            var spawns = new List<SpawnAction>();
            var destroys = new List<DestroyAction>();
            var stateChanges = new List<StateChangeAction>();
            var topologyChanges = new List<TopologyChangeAction>();
            var semanticKind = ResolvedActionSemanticKind.None;
            var intentId = 0;
            var priority = resolutionRecord.Priority;

            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                semanticKind = semanticKind == ResolvedActionSemanticKind.None
                    ? operation.Metadata.SemanticKind
                    : semanticKind;
                intentId = intentId == 0 ? operation.Metadata.IntentId : intentId;

                switch (operation.Kind)
                {
                    case FinalizationOperationKind.MoveEntity:
                        moves.Add(
                            new MoveAction(
                                operation.EntityId,
                                source: SurfaceCell.FromPlanar(new UnityEngine.Vector2Int(0, 0)),
                                destination: operation.Destination,
                                operation.Facing));
                        break;

                    case FinalizationOperationKind.ApplyDamage:
                        damages.Add(new DamageAction(operation.EntityId, operation.Amount));
                        break;

                    case FinalizationOperationKind.SpawnEntity:
                        var spawnId = i < spawnIds.Count ? spawnIds[i] : i + 1;
                        spawns.Add(new SpawnAction(spawnId, operation.SpawnedEntity));
                        break;

                    case FinalizationOperationKind.MarkDestroy:
                        destroys.Add(new DestroyAction(operation.EntityId, DestroyCondition.WhenHpDepleted));
                        break;

                    case FinalizationOperationKind.ApplyStateChange:
                        stateChanges.Add(new StateChangeAction(operation.EntityId, operation.PhaseState, operation.StateTimer));
                        break;

                    case FinalizationOperationKind.SetTopology:
                        topologyChanges.Add(new TopologyChangeAction(operation.Metadata.RotationKind, operation.Topology));
                        break;
                }
            }

            var impactSourceId = 0;
            var impactTargetId = 0;
            if (impactMetadataObject is CanonicalImpactMetadata impactMetadata)
            {
                impactSourceId = impactMetadata.SourceId;
                impactTargetId = impactMetadata.TargetId;
            }

            var groupKind = ResolveGroupKind(isMovement, semanticKind, resolutionRecord.SourceId, impactSourceId);
            var projectileImpactTargetId = groupKind == ActionGroupKind.ProjectileImpact
                ? impactTargetId
                : 0;

            return new CanonicalResolvedAction(
                resolutionRecord.ActionPlanId,
                intentId,
                resolutionRecord.SourceId,
                priority,
                groupKind,
                moves,
                damages,
                spawns,
                destroys,
                stateChanges,
                topologyChanges,
                impactSourceId,
                impactTargetId,
                projectileImpactTargetId);
        }

        private static ActionGroupKind ResolveGroupKind(
            bool isMovement,
            ResolvedActionSemanticKind semanticKind,
            int sourceId,
            int impactSourceId)
        {
            if (!isMovement)
            {
                return ActionGroupKind.Attack;
            }

            return semanticKind switch
            {
                ResolvedActionSemanticKind.Move => ActionGroupKind.Move,
                ResolvedActionSemanticKind.Item => ActionGroupKind.Item,
                ResolvedActionSemanticKind.Push => ActionGroupKind.Push,
                ResolvedActionSemanticKind.Slide => ActionGroupKind.Push,
                ResolvedActionSemanticKind.Flip => ActionGroupKind.Flip,
                ResolvedActionSemanticKind.ProjectileMove => ActionGroupKind.Move,
                ResolvedActionSemanticKind.Stop => ActionGroupKind.Stop,
                ResolvedActionSemanticKind.Impact when impactSourceId == sourceId => ActionGroupKind.ProjectileImpact,
                ResolvedActionSemanticKind.Impact => ActionGroupKind.BoxImpact,
                _ => ActionGroupKind.Move,
            };
        }
    }

    internal static class CanonicalPhaseResultFactory
    {
        public static MovementPhaseResult CreateMovementPhaseResult(
            IEnumerable<RawMovementIntent> rawIntents,
            IEnumerable<MoveIntent> sortedIntents,
            IEnumerable<ActionGroup> selectedGroups,
            IEnumerable<string> commitEvents,
            IEnumerable<string> rejectedReasons,
            Func<ActionGroup, ResolvedActionSemanticKind> semanticResolver = null)
        {
            var groups = Materialize(selectedGroups);
            var resolutionRecords = new List<ResolutionRecord>(groups.Count);
            var operations = new List<FinalizationOperation>();
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
                operations,
                commitEvents,
                rejectedReasons);
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
            IEnumerable<AttackIntent> sortedInputs,
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
                        sourceActionGroupId: actionPlanId,
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
                sortedInputs,
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
                Array.Empty<AttackIntent>(),
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
                damageSourceType: DamageSourceType.None);
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
                AttackSourceKind.ImpactReservation => DamageSourceType.Impact,
                AttackSourceKind.PassiveContact => DamageSourceType.Environmental,
                _ => DamageSourceType.None,
            };
        }

        private static IReadOnlyList<string> BuildMovementCommitEvents(IReadOnlyList<ActionGroup> selectedGroups)
        {
            var commitEvents = new List<string>();
            for (var i = 0; i < selectedGroups.Count; i++)
            {
                var group = selectedGroups[i];
                if (!group.HasResolvedImpact)
                {
                    continue;
                }

                commitEvents.Add(
                    $"ImpactReservationCreated|G={ResolveActionPlanId(group, i)}|I={group.IntentId}|Source={group.ImpactSourceId}|Target={group.ImpactTargetId}|At=(0,0)|Damage=1|Sequence={i + 1}");
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

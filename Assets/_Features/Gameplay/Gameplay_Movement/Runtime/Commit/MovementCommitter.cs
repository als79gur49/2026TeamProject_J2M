using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Commit
{
    internal readonly struct MovementFacingResolutionRecord
    {
        public MovementFacingResolutionRecord(int groupId, int entityId, Direction facing)
        {
            GroupId = groupId;
            EntityId = entityId;
            Facing = facing;
        }

        public int GroupId { get; }

        public int EntityId { get; }

        public Direction Facing { get; }
    }

    internal readonly struct MovementExecutionLockResolutionRecord
    {
        public MovementExecutionLockResolutionRecord(int groupId, int entityId, EntityExecutionLockState lockState)
        {
            GroupId = groupId;
            EntityId = entityId;
            LockState = lockState;
        }

        public int GroupId { get; }

        public int EntityId { get; }

        public EntityExecutionLockState LockState { get; }
    }

    internal readonly struct MovementEnemyLocomotionResolutionRecord
    {
        public MovementEnemyLocomotionResolutionRecord(int groupId, int entityId, int cooldownTicks)
        {
            GroupId = groupId;
            EntityId = entityId;
            CooldownTicks = cooldownTicks;
        }

        public int GroupId { get; }

        public int EntityId { get; }

        public int CooldownTicks { get; }
    }

    internal readonly struct MovementEnemyPatrolResolutionRecord
    {
        public MovementEnemyPatrolResolutionRecord(int groupId, int entityId, EnemyPatrolRuntimeState state)
        {
            GroupId = groupId;
            EntityId = entityId;
            State = state;
        }

        public int GroupId { get; }

        public int EntityId { get; }

        public EnemyPatrolRuntimeState State { get; }
    }

    internal readonly struct MovementPlayerControlResolutionRecord
    {
        public MovementPlayerControlResolutionRecord(int groupId, int entityId, PlayerControlState state)
        {
            GroupId = groupId;
            EntityId = entityId;
            State = state;
        }

        public int GroupId { get; }

        public int EntityId { get; }

        public PlayerControlState State { get; }
    }

    internal readonly struct MovementImpactSpaceResolutionRecord
    {
        public MovementImpactSpaceResolutionRecord(
            int groupId,
            int entityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            Direction facing,
            bool hasStateChange,
            EntityPhaseState state,
            int stateTimer,
            bool hasSourceFacing,
            int sourceFacingEntityId,
            Direction sourceFacing)
        {
            GroupId = groupId;
            EntityId = entityId;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            Facing = facing;
            HasStateChange = hasStateChange;
            State = state;
            StateTimer = stateTimer;
            HasSourceFacing = hasSourceFacing;
            SourceFacingEntityId = sourceFacingEntityId;
            SourceFacing = sourceFacing;
        }

        public int GroupId { get; }

        public int EntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public Direction Facing { get; }

        public bool HasStateChange { get; }

        public EntityPhaseState State { get; }

        public int StateTimer { get; }

        public bool HasSourceFacing { get; }

        public int SourceFacingEntityId { get; }

        public Direction SourceFacing { get; }
    }

    internal sealed class MovementCommitter
    {
        private const int ProjectileImpactDamageAmount = 1;
        private const int BoxImpactDamageAmount = 1;
        private readonly int _moveOccupancyTicks;
        private readonly int _playerMoveCooldownTicks;
        private readonly int _slidingStateTimerTicks;

        public MovementCommitter(
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming,
            GameplayTimingProfile timingProfile)
        {
            if (playerControlTiming.MoveCooldownTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerControlTiming),
                    "Player move cooldown ticks must be zero or greater.");
            }

            if (timingProfile == null)
            {
                throw new ArgumentNullException(nameof(timingProfile));
            }

            _playerMoveCooldownTicks = playerControlTiming.MoveCooldownTicks;
            _moveOccupancyTicks = timingProfile.MoveOccupancyTicks;
            _slidingStateTimerTicks = timingProfile.BoxSlideStepIntervalTicks;
        }

        public void Commit(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            int tickIndex,
            IMovementCommitContext writeContext,
            ImpactReservationBuffer transientBuffer,
            IReadOnlyList<ActionGroup> selectedGroups,
            List<string> commitEvents)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (transientBuffer == null)
            {
                throw new ArgumentNullException(nameof(transientBuffer));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            if (commitEvents == null)
            {
                throw new ArgumentNullException(nameof(commitEvents));
            }

            commitEvents.Clear();
            var impactReservations = ResolveImpactReservations(snapshot, tickIndex, selectedGroups);
            for (var i = 0; i < impactReservations.Count; i++)
            {
                transientBuffer.AddImpact(impactReservations[i]);
            }

            var destroyResolutions = ResolveDestroyResolutions(snapshot, selectedGroups);
            var facingResolutions = ResolveFacingResolutions(snapshot, sortedIntents, selectedGroups);
            var executionLockResolutions = ResolveExecutionLockResolutions(snapshot, tickIndex, selectedGroups);
            var enemyLocomotionResolutions = ResolveEnemyLocomotionResolutions(snapshot, sortedIntents, selectedGroups);
            var enemyPatrolResolutions = ResolveEnemyPatrolResolutions(snapshot, sortedIntents, selectedGroups);
            var playerControlResolutions = ResolvePlayerControlResolutions(snapshot, sortedIntents, tickIndex, selectedGroups);
            CommitResolved(
                writeContext,
                selectedGroups,
                impactReservations,
                Array.Empty<MovementImpactSpaceResolutionRecord>(),
                destroyResolutions,
                facingResolutions,
                executionLockResolutions,
                enemyLocomotionResolutions,
                enemyPatrolResolutions,
                playerControlResolutions,
                commitEvents);
        }

        internal void CommitResolved(
            IMovementCommitContext writeContext,
            IReadOnlyList<ActionGroup> selectedGroups,
            IReadOnlyList<ImpactReservation> impactReservations,
            IReadOnlyList<MovementImpactSpaceResolutionRecord> impactSpaceResolutions,
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            IReadOnlyList<MovementFacingResolutionRecord> facingResolutions,
            IReadOnlyList<MovementExecutionLockResolutionRecord> executionLockResolutions,
            IReadOnlyList<MovementEnemyLocomotionResolutionRecord> enemyLocomotionResolutions,
            IReadOnlyList<MovementEnemyPatrolResolutionRecord> enemyPatrolResolutions,
            IReadOnlyList<MovementPlayerControlResolutionRecord> playerControlResolutions,
            List<string> commitEvents)
        {
            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            if (impactReservations == null)
            {
                throw new ArgumentNullException(nameof(impactReservations));
            }

            if (impactSpaceResolutions == null)
            {
                throw new ArgumentNullException(nameof(impactSpaceResolutions));
            }

            if (destroyResolutions == null)
            {
                throw new ArgumentNullException(nameof(destroyResolutions));
            }

            if (facingResolutions == null)
            {
                throw new ArgumentNullException(nameof(facingResolutions));
            }

            if (executionLockResolutions == null)
            {
                throw new ArgumentNullException(nameof(executionLockResolutions));
            }

            if (enemyLocomotionResolutions == null)
            {
                throw new ArgumentNullException(nameof(enemyLocomotionResolutions));
            }

            if (enemyPatrolResolutions == null)
            {
                throw new ArgumentNullException(nameof(enemyPatrolResolutions));
            }

            if (playerControlResolutions == null)
            {
                throw new ArgumentNullException(nameof(playerControlResolutions));
            }

            if (commitEvents == null)
            {
                throw new ArgumentNullException(nameof(commitEvents));
            }

            commitEvents.Clear();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                var hasImpactReservation = false;
                for (var impactIndex = 0; impactIndex < impactReservations.Count; impactIndex++)
                {
                    var impactReservation = impactReservations[impactIndex];
                    if (impactReservation.SourceActionPlanId != group.GroupId)
                    {
                        continue;
                    }

                    hasImpactReservation = true;
                    commitEvents.Add(
                        $"ImpactReservationCreated|G={group.GroupId}|I={group.IntentId}|Source={impactReservation.SourceId}|Target={impactReservation.TargetId}|At={FormatCell(impactReservation.ImpactCell)}|Damage={impactReservation.Damage}|Sequence={impactReservation.LocalActionIndex}");
                }

                if (hasImpactReservation &&
                    group.GroupKind == ActionGroupKind.ProjectileImpact)
                {
                    continue;
                }

                var hasImpactSpaceResolution = TryFindImpactSpaceResolution(
                    impactSpaceResolutions,
                    group.GroupId,
                    out var impactSpaceResolution);

                if (hasImpactSpaceResolution &&
                    impactSpaceResolution.HasSourceFacing)
                {
                    writeContext.SetFacing(impactSpaceResolution.SourceFacingEntityId, impactSpaceResolution.SourceFacing);
                    commitEvents.Add(
                        $"FacingCommitted|G={group.GroupId}|I={group.IntentId}|E={impactSpaceResolution.SourceFacingEntityId}|Facing={impactSpaceResolution.SourceFacing}");
                }

                if (hasImpactSpaceResolution &&
                    impactSpaceResolution.HasStateChange)
                {
                    writeContext.ApplyStateChange(
                        impactSpaceResolution.EntityId,
                        impactSpaceResolution.State,
                        impactSpaceResolution.StateTimer);
                    commitEvents.Add(
                        $"StateChanged|G={group.GroupId}|I={group.IntentId}|E={impactSpaceResolution.EntityId}|State={impactSpaceResolution.State}|Timer={impactSpaceResolution.StateTimer}");
                }

                if (!hasImpactSpaceResolution)
                {
                    for (var stateChangeIndex = 0; stateChangeIndex < group.StateChanges.Count; stateChangeIndex++)
                    {
                        var stateChange = group.StateChanges[stateChangeIndex];
                        writeContext.ApplyStateChange(stateChange.EntityId, stateChange.State, stateChange.StateTimer);
                        commitEvents.Add(
                            $"StateChanged|G={group.GroupId}|I={group.IntentId}|E={stateChange.EntityId}|State={stateChange.State}|Timer={stateChange.StateTimer}");
                    }
                }

                for (var presenceIndex = 0; presenceIndex < group.BoardPresenceChanges.Count; presenceIndex++)
                {
                    var boardPresenceChange = group.BoardPresenceChanges[presenceIndex];
                    writeContext.SetBoardPresence(boardPresenceChange.EntityId, boardPresenceChange.BoardPresence);
                    commitEvents.Add(
                        $"BoardPresenceCommitted|G={group.GroupId}|I={group.IntentId}|E={boardPresenceChange.EntityId}|Presence={boardPresenceChange.BoardPresence}");
                }

                for (var topologyIndex = 0; topologyIndex < group.TopologyChanges.Count; topologyIndex++)
                {
                    var topologyChange = group.TopologyChanges[topologyIndex];
                    writeContext.SetTopology(topologyChange.UpdatedTopology);
                    commitEvents.Add(
                        $"TopologyCommitted|G={group.GroupId}|I={group.IntentId}|Rotation={topologyChange.RotationKind}|Bottom={topologyChange.UpdatedTopology.BottomFace}|Front={topologyChange.UpdatedTopology.FrontFace}");
                }

                if (TryFindFacingResolution(facingResolutions, group.GroupId, out var facingResolution))
                {
                    writeContext.SetFacing(facingResolution.EntityId, facingResolution.Facing);
                    commitEvents.Add(
                        $"FacingCommitted|G={group.GroupId}|I={group.IntentId}|E={facingResolution.EntityId}|Facing={facingResolution.Facing}");
                }

                if (hasImpactSpaceResolution)
                {
                    writeContext.MoveEntity(impactSpaceResolution.EntityId, impactSpaceResolution.DestinationCell);
                    writeContext.SetFacing(impactSpaceResolution.EntityId, impactSpaceResolution.Facing);
                    commitEvents.Add(
                        $"MoveCommitted|G={group.GroupId}|I={group.IntentId}|E={impactSpaceResolution.EntityId}|To={FormatCell(impactSpaceResolution.DestinationCell)}|Facing={impactSpaceResolution.Facing}");
                }
                else
                {
                    for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                    {
                        var move = group.Moves[moveIndex];
                        writeContext.MoveEntity(move.EntityId, move.DestinationCell);
                        writeContext.SetFacing(move.EntityId, move.Facing);
                        commitEvents.Add(
                            $"MoveCommitted|G={group.GroupId}|I={group.IntentId}|E={move.EntityId}|To={FormatCell(move.DestinationCell)}|Facing={move.Facing}");
                    }
                }

                if (group.BoxKineticTargetId > 0)
                {
                    writeContext.SetBoxKineticOwner(
                        group.BoxKineticTargetId,
                        group.BoxKineticInstigatorEntityId,
                        group.BoxKineticInstigatorTeamId);
                }

                if (hasImpactSpaceResolution)
                {
                    if (TryFindExecutionLockResolution(
                            executionLockResolutions,
                            group.GroupId,
                            impactSpaceResolution.EntityId,
                            out var impactExecutionLockResolution))
                    {
                        writeContext.SetEntityExecutionLockState(
                            impactExecutionLockResolution.EntityId,
                            impactExecutionLockResolution.LockState);
                    }
                }
                else
                {
                    for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                    {
                        var move = group.Moves[moveIndex];
                        if (!TryFindExecutionLockResolution(executionLockResolutions, group.GroupId, move.EntityId, out var executionLockResolution))
                        {
                            continue;
                        }

                        writeContext.SetEntityExecutionLockState(executionLockResolution.EntityId, executionLockResolution.LockState);
                    }
                }

                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    var destroy = group.Destroys[destroyIndex];
                    if (!TryFindDestroyResolution(destroyResolutions, group.GroupId, destroy.TargetId, destroyIndex, out var destroyResolution) ||
                        !destroyResolution.Accepted)
                    {
                        continue;
                    }

                    writeContext.MarkDestroy(destroyResolution.TargetId);
                    commitEvents.Add(
                        $"DestroyMarked|G={group.GroupId}|I={group.IntentId}|Target={destroyResolution.TargetId}|Condition={destroyResolution.Condition}");
                }

                if (TryFindEnemyLocomotionResolution(enemyLocomotionResolutions, group.GroupId, out var enemyLocomotionResolution))
                {
                    writeContext.SetEnemyLocomotionCooldown(enemyLocomotionResolution.EntityId, enemyLocomotionResolution.CooldownTicks);
                }

                if (TryFindEnemyPatrolResolution(enemyPatrolResolutions, group.GroupId, out var enemyPatrolResolution))
                {
                    writeContext.SetEnemyPatrolState(enemyPatrolResolution.EntityId, enemyPatrolResolution.State);
                    commitEvents.Add(
                        $"EnemyPatrolStateUpdated|G={group.GroupId}|I={group.IntentId}|E={enemyPatrolResolution.EntityId}|Label=CommittedMove|Seq={enemyPatrolResolution.State.sequence}|Home={enemyPatrolResolution.State.homeCell}|LastDirection={enemyPatrolResolution.State.lastCommittedDirection}");
                }

                if (TryFindPlayerControlResolution(playerControlResolutions, group.GroupId, out var playerControlResolution))
                {
                    writeContext.SetPlayerControlState(playerControlResolution.EntityId, playerControlResolution.State);
                }
            }
        }

        internal List<ImpactReservation> ResolveImpactReservations(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<ActionGroup> selectedGroups)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            var impactReservations = new List<ImpactReservation>();
            var reservationSequence = 1;

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                if (group.GroupKind != ActionGroupKind.ProjectileImpact &&
                    !group.HasResolvedImpact)
                {
                    continue;
                }

                if (TryCreateImpactReservations(snapshot, tickIndex, group, ref reservationSequence, impactReservations))
                {
                    continue;
                }
            }

            return impactReservations;
        }

        internal List<DestroyResolutionRecord> ResolveDestroyResolutions(
            WorldSnapshot snapshot,
            IReadOnlyList<ActionGroup> selectedGroups)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            var destroyResolutions = new List<DestroyResolutionRecord>();
            var destroyMarkedTargets = new HashSet<int>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
                {
                    var destroy = group.Destroys[destroyIndex];
                    var accepted = false;
                    var finalHp = 0;

                    if (!destroyMarkedTargets.Contains(destroy.TargetId) &&
                        snapshot.TryGetEntity(destroy.TargetId, out var target) &&
                        !target.markedForDeath)
                    {
                        finalHp = target.hp;
                        if (destroy.Condition != DestroyCondition.WhenHpDepleted || finalHp <= 0)
                        {
                            destroyMarkedTargets.Add(destroy.TargetId);
                            accepted = true;
                        }
                    }

                    destroyResolutions.Add(
                        new DestroyResolutionRecord(
                            group.GroupId,
                            group.IntentId,
                            group.SourceId,
                            destroy.TargetId,
                            destroy.Condition,
                            finalHp,
                            accepted,
                            destroyIndex));
                }
            }

            return destroyResolutions;
        }

        internal List<MovementFacingResolutionRecord> ResolveFacingResolutions(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            IReadOnlyList<ActionGroup> selectedGroups)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            var facingResolutions = new List<MovementFacingResolutionRecord>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                if (group.GroupKind != ActionGroupKind.Flip)
                {
                    continue;
                }

                facingResolutions.Add(
                    new MovementFacingResolutionRecord(
                        group.GroupId,
                        group.SourceId,
                        ResolveFlipSourceFacing(snapshot, sortedIntents, group)));
            }

            return facingResolutions;
        }

        internal List<MovementExecutionLockResolutionRecord> ResolveExecutionLockResolutions(
            WorldSnapshot snapshot,
            int tickIndex,
            IReadOnlyList<ActionGroup> selectedGroups)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            var executionLockResolutions = new List<MovementExecutionLockResolutionRecord>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                if (group.GroupKind != ActionGroupKind.Move &&
                    group.GroupKind != ActionGroupKind.Item)
                {
                    continue;
                }

                for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                {
                    var move = group.Moves[moveIndex];
                    if (!snapshot.TryGetEntity(move.EntityId, out var movedEntity) ||
                        movedEntity.type != EntityType.Unit)
                    {
                        continue;
                    }

                    snapshot.TryGetEntityExecutionLockState(move.EntityId, out var previousState);
                    var lockState = EntityExecutionLockQueries.StartMoveLock(previousState, tickIndex, _moveOccupancyTicks);
                    executionLockResolutions.Add(
                        new MovementExecutionLockResolutionRecord(
                            group.GroupId,
                            move.EntityId,
                            lockState));
                }
            }

            return executionLockResolutions;
        }

        internal List<MovementEnemyLocomotionResolutionRecord> ResolveEnemyLocomotionResolutions(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            IReadOnlyList<ActionGroup> selectedGroups)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            var locomotionResolutions = new List<MovementEnemyLocomotionResolutionRecord>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                if (!snapshot.TryGetEntity(group.SourceId, out var source) ||
                    source.type != EntityType.Unit ||
                    (source.aiMode != EnemyAiMode.Patrol &&
                     source.aiMode != EnemyAiMode.Chase &&
                     source.aiMode != EnemyAiMode.Charge))
                {
                    continue;
                }

                var intent = FindIntent(sortedIntents, group.IntentId);
                if (intent == null ||
                    intent.CommandKind != MovementCommandKind.Move)
                {
                    continue;
                }

                locomotionResolutions.Add(
                    new MovementEnemyLocomotionResolutionRecord(
                        group.GroupId,
                        group.SourceId,
                        intent.MoveCooldownTicks));
            }

            return locomotionResolutions;
        }

        internal List<MovementEnemyPatrolResolutionRecord> ResolveEnemyPatrolResolutions(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            IReadOnlyList<ActionGroup> selectedGroups)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            var patrolResolutions = new List<MovementEnemyPatrolResolutionRecord>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                if (!snapshot.TryGetEntity(group.SourceId, out var source) ||
                    source.type != EntityType.Unit ||
                    source.aiMode != EnemyAiMode.Patrol)
                {
                    continue;
                }

                var intent = FindIntent(sortedIntents, group.IntentId);
                if (intent == null ||
                    intent.CommandKind != MovementCommandKind.Move ||
                    !snapshot.TryGetEnemyPatrolState(group.SourceId, out var currentState) ||
                    !currentState.IsInitialized ||
                    !EnemyMovementStrategyShared.TryResolveDirection(
                        intent.Destination - source.position.PlanarPosition,
                        out var direction))
                {
                    continue;
                }

                patrolResolutions.Add(
                    new MovementEnemyPatrolResolutionRecord(
                        group.GroupId,
                        group.SourceId,
                        EnemyPatrolQueries.CommitMove(currentState, direction)));
            }

            return patrolResolutions;
        }

        internal List<MovementPlayerControlResolutionRecord> ResolvePlayerControlResolutions(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            int tickIndex,
            IReadOnlyList<ActionGroup> selectedGroups)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            var playerControlResolutions = new List<MovementPlayerControlResolutionRecord>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                if (!snapshot.TryGetPlayerControlState(group.SourceId, out var controlState))
                {
                    continue;
                }

                var intent = FindIntent(sortedIntents, group.IntentId);
                if (intent == null)
                {
                    continue;
                }

                if (!ShouldConsumePlayerMoveCooldown(intent, group))
                {
                    continue;
                }

                var updatedState = PlayerControlQueries.ConsumeMoveCooldown(
                    controlState,
                    _playerMoveCooldownTicks,
                    tickIndex);

                playerControlResolutions.Add(
                    new MovementPlayerControlResolutionRecord(
                        group.GroupId,
                        group.SourceId,
                        updatedState));
            }
            return playerControlResolutions;
        }

        private static bool ShouldConsumePlayerMoveCooldown(MoveIntent intent, ActionGroup group)
        {
            return intent != null &&
                   intent.CommandKind == MovementCommandKind.Move &&
                   !HasTopologyChangingMove(group);
        }

        private static bool HasTopologyChangingMove(ActionGroup group)
        {
            return group != null &&
                   group.GroupKind == ActionGroupKind.Move &&
                   group.TopologyChanges.Count > 0;
        }

        internal bool TryResolveImpactSpaceSuccess(
            WorldSnapshot snapshot,
            ActionGroup group,
            ImpactReservation impactReservation,
            out MovementImpactSpaceResolutionRecord resolution)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (group.GroupKind != ActionGroupKind.BoxImpact)
            {
                resolution = default;
                return false;
            }

            if (!snapshot.TryGetEntity(group.ImpactSourceId, out var impactSourceBox))
            {
                throw new InvalidOperationException(
                    $"Impact space resolution requires a valid impact source box. ImpactSource={group.ImpactSourceId}, Group={group.GroupId}, Intent={group.IntentId}");
            }

            if (!ImpactGeometryResolver.TryResolve(
                    snapshot.Topology,
                    snapshot.BoardBounds,
                    impactSourceBox.position,
                    impactReservation.ImpactCell,
                    out var geometry))
            {
                resolution = default;
                return false;
            }

            resolution = new MovementImpactSpaceResolutionRecord(
                group.GroupId,
                impactSourceBox.entityId,
                impactSourceBox.position,
                geometry.ImpactCell,
                geometry.MoveFacing,
                hasStateChange: !geometry.IsFlipImpact,
                state: EntityPhaseState.Sliding,
                stateTimer: !geometry.IsFlipImpact ? _slidingStateTimerTicks : 0,
                geometry.HasSourceFacing,
                sourceFacingEntityId: geometry.HasSourceFacing ? group.SourceId : 0,
                geometry.SourceFacing);
            return true;
        }

        internal static List<ImpactReservation> SortImpactReservations(IReadOnlyList<ImpactReservation> impactReservations)
        {
            if (impactReservations == null)
            {
                throw new ArgumentNullException(nameof(impactReservations));
            }

            var sortedReservations = new List<ImpactReservation>(impactReservations);
            sortedReservations.Sort(ImpactReservationComparer.Instance);
            return sortedReservations;
        }

        private static bool TryFindImpactReservation(
            IReadOnlyList<ImpactReservation> impactReservations,
            int groupId,
            out ImpactReservation impactReservation)
        {
            for (var i = 0; i < impactReservations.Count; i++)
            {
                if (impactReservations[i].SourceActionPlanId == groupId)
                {
                    impactReservation = impactReservations[i];
                    return true;
                }
            }

            impactReservation = default;
            return false;
        }

        private static bool TryFindFacingResolution(
            IReadOnlyList<MovementFacingResolutionRecord> facingResolutions,
            int groupId,
            out MovementFacingResolutionRecord facingResolution)
        {
            for (var i = 0; i < facingResolutions.Count; i++)
            {
                if (facingResolutions[i].GroupId == groupId)
                {
                    facingResolution = facingResolutions[i];
                    return true;
                }
            }

            facingResolution = default;
            return false;
        }

        private static bool TryFindExecutionLockResolution(
            IReadOnlyList<MovementExecutionLockResolutionRecord> executionLockResolutions,
            int groupId,
            int entityId,
            out MovementExecutionLockResolutionRecord executionLockResolution)
        {
            for (var i = 0; i < executionLockResolutions.Count; i++)
            {
                if (executionLockResolutions[i].GroupId == groupId &&
                    executionLockResolutions[i].EntityId == entityId)
                {
                    executionLockResolution = executionLockResolutions[i];
                    return true;
                }
            }

            executionLockResolution = default;
            return false;
        }

        private static bool TryFindDestroyResolution(
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            int actionPlanId,
            int targetId,
            int localActionIndex,
            out DestroyResolutionRecord destroyResolution)
        {
            for (var i = 0; i < destroyResolutions.Count; i++)
            {
                if (destroyResolutions[i].ActionPlanId == actionPlanId &&
                    destroyResolutions[i].TargetId == targetId &&
                    destroyResolutions[i].LocalActionIndex == localActionIndex)
                {
                    destroyResolution = destroyResolutions[i];
                    return true;
                }
            }

            destroyResolution = default;
            return false;
        }

        private static bool TryFindEnemyLocomotionResolution(
            IReadOnlyList<MovementEnemyLocomotionResolutionRecord> enemyLocomotionResolutions,
            int groupId,
            out MovementEnemyLocomotionResolutionRecord enemyLocomotionResolution)
        {
            for (var i = 0; i < enemyLocomotionResolutions.Count; i++)
            {
                if (enemyLocomotionResolutions[i].GroupId == groupId)
                {
                    enemyLocomotionResolution = enemyLocomotionResolutions[i];
                    return true;
                }
            }

            enemyLocomotionResolution = default;
            return false;
        }

        private static bool TryFindEnemyPatrolResolution(
            IReadOnlyList<MovementEnemyPatrolResolutionRecord> enemyPatrolResolutions,
            int groupId,
            out MovementEnemyPatrolResolutionRecord enemyPatrolResolution)
        {
            for (var i = 0; i < enemyPatrolResolutions.Count; i++)
            {
                if (enemyPatrolResolutions[i].GroupId == groupId)
                {
                    enemyPatrolResolution = enemyPatrolResolutions[i];
                    return true;
                }
            }

            enemyPatrolResolution = default;
            return false;
        }

        private static bool TryFindPlayerControlResolution(
            IReadOnlyList<MovementPlayerControlResolutionRecord> playerControlResolutions,
            int groupId,
            out MovementPlayerControlResolutionRecord playerControlResolution)
        {
            for (var i = 0; i < playerControlResolutions.Count; i++)
            {
                if (playerControlResolutions[i].GroupId == groupId)
                {
                    playerControlResolution = playerControlResolutions[i];
                    return true;
                }
            }

            playerControlResolution = default;
            return false;
        }

        private static bool TryFindImpactSpaceResolution(
            IReadOnlyList<MovementImpactSpaceResolutionRecord> impactSpaceResolutions,
            int groupId,
            out MovementImpactSpaceResolutionRecord impactSpaceResolution)
        {
            for (var i = 0; i < impactSpaceResolutions.Count; i++)
            {
                if (impactSpaceResolutions[i].GroupId == groupId)
                {
                    impactSpaceResolution = impactSpaceResolutions[i];
                    return true;
                }
            }

            impactSpaceResolution = default;
            return false;
        }

        private static bool TryCreateImpactReservations(
            WorldSnapshot snapshot,
            int tickIndex,
            ActionGroup group,
            ref int reservationSequence,
            List<ImpactReservation> impactReservations)
        {
            if (!snapshot.TryGetEntity(group.ImpactSourceId, out var source))
            {
                throw new InvalidOperationException(
                    $"Impact group references a missing source entity. ImpactSource={group.ImpactSourceId}, Intent={group.IntentId}, Group={group.GroupId}");
            }

            if (group.GroupKind == ActionGroupKind.ProjectileImpact &&
                source.type != EntityType.Projectile)
            {
                throw new InvalidOperationException(
                    $"Projectile impact group must reference a projectile source. Source={group.SourceId}, Type={source.type}, Intent={group.IntentId}");
            }

            if (!group.HasResolvedImpact)
            {
                throw new InvalidOperationException(
                    $"Impact group is missing its resolved target. Source={group.SourceId}, Intent={group.IntentId}, Group={group.GroupId}");
            }

            var addedAny = false;
            for (var i = 0; i < group.ImpactTargetIds.Count; i++)
            {
                var impactTargetId = group.ImpactTargetIds[i];
                if (!snapshot.TryGetEntity(impactTargetId, out var target))
                {
                    throw new InvalidOperationException(
                        $"Impact group target no longer exists in the authoritative snapshot. Source={group.SourceId}, Intent={group.IntentId}, Target={impactTargetId}");
                }

                if (!ImpactGeometryResolver.TryResolve(
                        snapshot.Topology,
                        snapshot.BoardBounds,
                        source.position,
                        target.position,
                        out _))
                {
                    continue;
                }

                impactReservations.Add(
                    new ImpactReservation(
                        source.entityId,
                        target.entityId,
                        target.position,
                        group.GroupKind == ActionGroupKind.ProjectileImpact
                            ? ProjectileImpactDamageAmount
                            : BoxImpactDamageAmount,
                        tickIndex,
                        group.GroupId,
                        reservationSequence++));
                addedAny = true;
            }

            return addedAny;
        }

        private static MoveIntent FindIntent(IReadOnlyList<MoveIntent> sortedIntents, int intentId)
        {
            for (var i = 0; i < sortedIntents.Count; i++)
            {
                if (sortedIntents[i].IntentId == intentId)
                {
                    return sortedIntents[i];
                }
            }

            return null;
        }

        private static Direction ResolveFlipSourceFacing(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            ActionGroup group)
        {
            if (!snapshot.TryGetEntity(group.SourceId, out var source))
            {
                throw new InvalidOperationException(
                    $"Flip group references a missing source entity. Source={group.SourceId}, Intent={group.IntentId}");
            }

            var intent = FindIntent(sortedIntents, group.IntentId);
            if (intent == null)
            {
                throw new InvalidOperationException(
                    $"Flip group is missing its movement intent. Source={group.SourceId}, Intent={group.IntentId}");
            }

            var delta = intent.Destination - source.position;
            var actionDirection = ResolveFlipActionDirection(delta, group);
            return DirectionUtility.Opposite(actionDirection);
        }

        private static Direction ResolveFlipActionDirection(Vector2Int delta, ActionGroup group)
        {
            if (delta.x == 0 && delta.y == 1)
            {
                return Direction.Up;
            }

            if (delta.x == 1 && delta.y == 0)
            {
                return Direction.Right;
            }

            if (delta.x == 0 && delta.y == -1)
            {
                return Direction.Down;
            }

            if (delta.x == -1 && delta.y == 0)
            {
                return Direction.Left;
            }

            throw new InvalidOperationException(
                $"Flip group requires an orthogonal adjacent direction. Source={group.SourceId}, Intent={group.IntentId}");
        }

        private static string FormatCell(SurfaceCell cell)
        {
            return cell.face == FaceId.Floor
                ? $"({cell.x},{cell.y})"
                : $"{cell.face}({cell.x},{cell.y})";
        }
    }
}

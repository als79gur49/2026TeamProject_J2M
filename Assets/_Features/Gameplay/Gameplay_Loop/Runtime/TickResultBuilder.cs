using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class TickResultBuilder
    {
        private readonly TickPresentationDataBuilder _presentationDataBuilder = new();

        public TickResultData Build(
            WorldSnapshot finalSnapshot,
            IReadOnlyList<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            in TickPresentationBuildContext presentationBuildContext)
        {
            if (finalSnapshot == null)
            {
                throw new ArgumentNullException(nameof(finalSnapshot));
            }

            if (pendingDelayedAttackEffects == null)
            {
                throw new ArgumentNullException(nameof(pendingDelayedAttackEffects));
            }

            if (movementPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(movementPhaseResult));
            }

            if (attackPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(attackPhaseResult));
            }

            if (cleanupPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(cleanupPhaseResult));
            }

            var finalEntities = new List<EntityState>();
            finalSnapshot.EnumerateEntitiesOrdered(finalEntities);

            var eventLog = new List<string>(
                movementPhaseResult.CommitEvents.Count +
                attackPhaseResult.EventLogEntries.Count +
                cleanupPhaseResult.RemovedEntityIds.Count +
                cleanupPhaseResult.TimerChanges.Count +
                cleanupPhaseResult.StateTransitions.Count);

            AddRange(eventLog, movementPhaseResult.CommitEvents);
            AddRange(eventLog, attackPhaseResult.EventLogEntries);

            for (var i = 0; i < cleanupPhaseResult.RemovedEntityIds.Count; i++)
            {
                eventLog.Add($"CleanupRemoved|E={cleanupPhaseResult.RemovedEntityIds[i]}");
            }

            AddRange(eventLog, cleanupPhaseResult.TimerChanges);
            AddRange(eventLog, cleanupPhaseResult.StateTransitions);

            return new TickResultData(
                finalEntities,
                pendingDelayedAttackEffects,
                eventLog,
                _presentationDataBuilder.Build(presentationBuildContext));
        }

        private static void AddRange(List<string> destination, IReadOnlyList<string> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }
        }
    }

    internal sealed class TickResultData
    {
        private readonly ReadOnlyCollection<string> _eventLog;
        private readonly ReadOnlyCollection<EntityState> _finalEntities;
        private readonly ReadOnlyCollection<DelayedAttackEffectRecord> _pendingDelayedAttackEffects;
        private readonly TickPresentationData _presentationData;

        public TickResultData(
            IEnumerable<EntityState> finalEntities,
            IEnumerable<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            IEnumerable<string> eventLog)
            : this(
                finalEntities,
                pendingDelayedAttackEffects,
                eventLog,
                TickPresentationData.Empty)
        {
        }

        public TickResultData(
            IEnumerable<EntityState> finalEntities,
            IEnumerable<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            IEnumerable<string> eventLog,
            TickPresentationData presentationData)
        {
            if (finalEntities == null)
            {
                throw new ArgumentNullException(nameof(finalEntities));
            }

            if (pendingDelayedAttackEffects == null)
            {
                throw new ArgumentNullException(nameof(pendingDelayedAttackEffects));
            }

            if (eventLog == null)
            {
                throw new ArgumentNullException(nameof(eventLog));
            }

            _presentationData = presentationData ?? throw new ArgumentNullException(nameof(presentationData));
            _finalEntities = new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities));
            _pendingDelayedAttackEffects = new ReadOnlyCollection<DelayedAttackEffectRecord>(new List<DelayedAttackEffectRecord>(pendingDelayedAttackEffects));
            _eventLog = new ReadOnlyCollection<string>(new List<string>(eventLog));
        }

        public IReadOnlyList<EntityState> FinalEntities => _finalEntities;

        public IReadOnlyList<DelayedAttackEffectRecord> PendingDelayedAttackEffects => _pendingDelayedAttackEffects;

        public IReadOnlyList<string> EventLog => _eventLog;

        public TickPresentationData PresentationData => _presentationData;
    }

    internal readonly struct TickPresentationBuildContext
    {
        public TickPresentationBuildContext(
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            WorldSnapshot finalAuthoritativeSnapshot,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            int currentTickIndex = 0,
            WorldSnapshot jumpBaselineSnapshot = null)
            : this(
                preMovementSnapshot,
                postMovementSnapshot,
                postAttackSnapshot,
                finalAuthoritativeSnapshot,
                new PreMovementStatePhaseResult(new List<string>(), new List<PlayerActionTransition>()),
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                currentTickIndex,
                jumpBaselineSnapshot)
        {
        }

        public TickPresentationBuildContext(
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            WorldSnapshot finalAuthoritativeSnapshot,
            PreMovementStatePhaseResult preMovementStatePhaseResult,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            int currentTickIndex = 0,
            WorldSnapshot jumpBaselineSnapshot = null)
        {
            PreMovementSnapshot = preMovementSnapshot ?? throw new ArgumentNullException(nameof(preMovementSnapshot));
            PostMovementSnapshot = postMovementSnapshot ?? throw new ArgumentNullException(nameof(postMovementSnapshot));
            PostAttackSnapshot = postAttackSnapshot ?? throw new ArgumentNullException(nameof(postAttackSnapshot));
            FinalAuthoritativeSnapshot = finalAuthoritativeSnapshot ?? throw new ArgumentNullException(nameof(finalAuthoritativeSnapshot));
            PreMovementStatePhaseResult = preMovementStatePhaseResult ?? throw new ArgumentNullException(nameof(preMovementStatePhaseResult));
            MovementPhaseResult = movementPhaseResult ?? throw new ArgumentNullException(nameof(movementPhaseResult));
            AttackPhaseResult = attackPhaseResult ?? throw new ArgumentNullException(nameof(attackPhaseResult));
            CleanupPhaseResult = cleanupPhaseResult ?? throw new ArgumentNullException(nameof(cleanupPhaseResult));
            CurrentTickIndex = currentTickIndex;
            JumpBaselineSnapshot = jumpBaselineSnapshot ?? PreMovementSnapshot;
        }

        public WorldSnapshot PreMovementSnapshot { get; }

        public WorldSnapshot PostMovementSnapshot { get; }

        public WorldSnapshot PostAttackSnapshot { get; }

        public WorldSnapshot FinalAuthoritativeSnapshot { get; }

        public PreMovementStatePhaseResult PreMovementStatePhaseResult { get; }

        public MovementPhaseResult MovementPhaseResult { get; }

        public AttackPhaseResult AttackPhaseResult { get; }

        public CleanupPhaseResult CleanupPhaseResult { get; }

        public int CurrentTickIndex { get; }

        public WorldSnapshot JumpBaselineSnapshot { get; }
    }

    internal sealed class TickPresentationDataBuilder
    {
        public TickPresentationData Build(in TickPresentationBuildContext context)
        {
            var entityMotions = new List<TickEntityMotion>();
            var entityExitSignals = new List<TickEntityExitPresentationSignal>();
            var enemyActionSignals = new List<TickEnemyActionPresentationSignal>();
            var enemyJumpSignals = new List<TickEnemyJumpPresentationSignal>();
            var playerActionSignals = new List<TickPlayerActionPresentationSignal>();
            var visibilityChanges = new List<TickVisibilityChange>();
            var transitionVisibilityChanges = new List<TickTransitionVisibilityChange>();
            var exitOwnedEntityIds = new HashSet<int>();

            BuildEntityExitPresentation(context, entityExitSignals, exitOwnedEntityIds);
            BuildMovementPresentation(context, entityMotions, visibilityChanges, exitOwnedEntityIds);
            BuildAttackPresentation(context, visibilityChanges);
            BuildCleanupPresentation(context, visibilityChanges, exitOwnedEntityIds);
            BuildPlayerPresentation(context, playerActionSignals);
            BuildEnemyPresentation(context, enemyActionSignals);
            BuildEnemyJumpPresentation(context, enemyJumpSignals);

            var topologyMotion = BuildTopologyMotion(context);
            BuildTransitionVisibilityPresentation(context, visibilityChanges, entityExitSignals, transitionVisibilityChanges);

            return entityMotions.Count == 0 &&
                   enemyActionSignals.Count == 0 &&
                   enemyJumpSignals.Count == 0 &&
                   entityExitSignals.Count == 0 &&
                   playerActionSignals.Count == 0 &&
                   visibilityChanges.Count == 0 &&
                   transitionVisibilityChanges.Count == 0 &&
                   !topologyMotion.HasValue
                ? TickPresentationData.Empty
                : new TickPresentationData(
                    entityMotions,
                    topologyMotion,
                    visibilityChanges,
                    transitionVisibilityChanges,
                    playerActionSignals,
                    enemyActionSignals,
                    enemyJumpSignals,
                    entityExitSignals);
        }

        private static void BuildMovementPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityMotion> entityMotions,
            List<TickVisibilityChange> visibilityChanges,
            ISet<int> exitOwnedEntityIds)
        {
            var selectedGroups = context.MovementPhaseResult.SelectedGroups;

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                AppendEntityMotions(context, group, entityMotions);
                AppendDetachVisibilityChanges(
                    context.PreMovementSnapshot,
                    group.BoardPresenceChanges,
                    visibilityChanges,
                    exitOwnedEntityIds);
            }
        }

        private static void BuildAttackPresentation(
            in TickPresentationBuildContext context,
            List<TickVisibilityChange> visibilityChanges)
        {
            var selectedGroups = context.AttackPhaseResult.SelectedGroups;

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var spawnIndex = 0; spawnIndex < group.Spawns.Count; spawnIndex++)
                {
                    var spawn = group.Spawns[spawnIndex];
                    if (!context.PostAttackSnapshot.TryGetEntity(spawn.Entity.entityId, out var spawnedEntity))
                    {
                        continue;
                    }

                    visibilityChanges.Add(
                        new TickVisibilityChange(
                            spawnedEntity.entityId,
                            TickVisibilityChangeKind.Spawn,
                            spawnedEntity.position,
                            context.PostAttackSnapshot.Topology,
                            spawnedEntity.facing));
                }
            }
        }

        private static void BuildEntityExitPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityExitPresentationSignal> entityExitSignals,
            ISet<int> exitOwnedEntityIds)
        {
            // Exit-owned removals bypass generic detach/remove visibility tracks. The
            // authoritative entity view disappears immediately; only transient echoes linger.
            var selectedGroups = context.MovementPhaseResult.SelectedGroups;
            var signaledEntityIds = new HashSet<int>();
            var removedEntityIds = new HashSet<int>(context.CleanupPhaseResult.RemovedEntityIds);

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                var destroyTargets = CollectDestroyTargets(group);
                for (var changeIndex = 0; changeIndex < group.BoardPresenceChanges.Count; changeIndex++)
                {
                    var boardPresenceChange = group.BoardPresenceChanges[changeIndex];
                    if (boardPresenceChange.BoardPresence != EntityBoardPresence.Detached ||
                        !removedEntityIds.Contains(boardPresenceChange.EntityId) ||
                        !signaledEntityIds.Add(boardPresenceChange.EntityId) ||
                        !context.PreMovementSnapshot.TryGetEntity(boardPresenceChange.EntityId, out var sourceEntity))
                    {
                        continue;
                    }

                    var exitCause = ResolveEntityExitCause(group, destroyTargets, sourceEntity);
                    if (exitCause == TickEntityExitCause.None)
                    {
                        continue;
                    }

                    entityExitSignals.Add(
                        new TickEntityExitPresentationSignal(
                            boardPresenceChange.EntityId,
                            exitCause,
                            sourceEntity.position,
                            context.PreMovementSnapshot.Topology,
                            sourceEntity.facing,
                            sourceEntity.type,
                            sourceActorEntityId: group.SourceId));
                    exitOwnedEntityIds.Add(boardPresenceChange.EntityId);
                }
            }
        }

        private static void BuildCleanupPresentation(
            in TickPresentationBuildContext context,
            List<TickVisibilityChange> visibilityChanges,
            ISet<int> exitOwnedEntityIds)
        {
            var removedEntityIds = context.CleanupPhaseResult.RemovedEntityIds;

            for (var i = 0; i < removedEntityIds.Count; i++)
            {
                var entityId = removedEntityIds[i];
                if (exitOwnedEntityIds.Contains(entityId))
                {
                    continue;
                }

                if (!context.PostAttackSnapshot.TryGetEntity(entityId, out var removedEntity))
                {
                    continue;
                }

                visibilityChanges.Add(
                    new TickVisibilityChange(
                        entityId,
                    TickVisibilityChangeKind.Remove,
                    removedEntity.position,
                    context.PostAttackSnapshot.Topology,
                    removedEntity.facing));
            }
        }

        private static void BuildPlayerPresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerActionPresentationSignal> playerActionSignals)
        {
            var candidateEntityIds = new List<int>();
            var seenEntityIds = new HashSet<int>();
            var transitionsByEntityId = new Dictionary<int, PlayerActionTransition>();
            var playerControlEntries = new List<PlayerControlSnapshotEntry>();
            context.FinalAuthoritativeSnapshot.EnumeratePlayerControlStatesOrdered(playerControlEntries);

            for (var i = 0; i < playerControlEntries.Count; i++)
            {
                var entry = playerControlEntries[i];
                if (!entry.State.activeAction.IsActive ||
                    !seenEntityIds.Add(entry.EntityId))
                {
                    continue;
                }

                candidateEntityIds.Add(entry.EntityId);
            }

            var actionTransitions = context.PreMovementStatePhaseResult.PlayerActionTransitions;
            for (var i = 0; i < actionTransitions.Count; i++)
            {
                var transition = actionTransitions[i];
                transitionsByEntityId[transition.EntityId] = transition;
                if (seenEntityIds.Add(transition.EntityId))
                {
                    candidateEntityIds.Add(transition.EntityId);
                }
            }

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                var activeActionKind = PlayerActionKind.None;
                var activeActionSequence = 0;
                var startedThisTick = false;
                var executedThisTick = false;
                var completedThisTick = false;
                var canceledThisTick = false;

                if (transitionsByEntityId.TryGetValue(entityId, out var transition))
                {
                    activeActionKind = transition.CurrentKind;
                    activeActionSequence = transition.CurrentSequence;
                    startedThisTick = transition.StartedThisTick;
                    completedThisTick = transition.CompletedThisTick;
                    canceledThisTick = transition.CanceledThisTick;
                }

                if (context.FinalAuthoritativeSnapshot.TryGetPlayerControlState(entityId, out var controlState))
                {
                    activeActionKind = controlState.activeAction.kind;
                    activeActionSequence = controlState.activeAction.sequence;
                    executedThisTick = controlState.activeAction.IsActive &&
                                       controlState.activeAction.executeTick == context.CurrentTickIndex;
                }

                playerActionSignals.Add(
                    new TickPlayerActionPresentationSignal(
                        entityId,
                        activeActionKind,
                        activeActionSequence,
                        startedThisTick,
                        completedThisTick,
                        canceledThisTick,
                        executedThisTick));
            }
        }

        private static void BuildEnemyPresentation(
            in TickPresentationBuildContext context,
            List<TickEnemyActionPresentationSignal> enemyActionSignals)
        {
            var candidateEntityIds = new List<int>();
            var seenEntityIds = new HashSet<int>();
            var executedEntityIds = new HashSet<int>();
            var preMovementEntries = new List<EnemyActionSnapshotEntry>();
            var postMovementEntries = new List<EnemyActionSnapshotEntry>();
            var finalEntries = new List<EnemyActionSnapshotEntry>();

            context.PreMovementSnapshot.EnumerateEnemyActionStatesOrdered(preMovementEntries);
            context.PostMovementSnapshot.EnumerateEnemyActionStatesOrdered(postMovementEntries);
            context.FinalAuthoritativeSnapshot.EnumerateEnemyActionStatesOrdered(finalEntries);

            CollectEnemyActionCandidateIds(preMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyActionCandidateIds(postMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyActionCandidateIds(finalEntries, seenEntityIds, candidateEntityIds);

            var selectedGroups = context.AttackPhaseResult.SelectedGroups;
            for (var i = 0; i < selectedGroups.Count; i++)
            {
                var sourceId = selectedGroups[i].SourceId;
                if (!IsEnemyUnit(context.PostMovementSnapshot, sourceId))
                {
                    continue;
                }

                executedEntityIds.Add(sourceId);
                if (seenEntityIds.Add(sourceId))
                {
                    candidateEntityIds.Add(sourceId);
                }
            }

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                context.PreMovementSnapshot.TryGetEnemyActionState(entityId, out var previousAction);
                context.PostMovementSnapshot.TryGetEnemyActionState(entityId, out var currentAction);

                var transition = new EnemyActionTransition(entityId, previousAction, currentAction);
                var activeActionKind = EnemyActionKind.None;
                var activeActionSequence = 0;

                if (context.FinalAuthoritativeSnapshot.TryGetEnemyActionState(entityId, out var finalAction))
                {
                    activeActionKind = finalAction.kind;
                    activeActionSequence = finalAction.sequence;
                }

                enemyActionSignals.Add(
                    new TickEnemyActionPresentationSignal(
                        entityId,
                        activeActionKind,
                        activeActionSequence,
                        transition.StartedThisTick,
                        transition.CanceledThisTick,
                        executedEntityIds.Contains(entityId),
                        DidStartEnemyRecovery(context.PostMovementSnapshot, context.PostAttackSnapshot, entityId)));
            }
        }

        private static void BuildEnemyJumpPresentation(
            in TickPresentationBuildContext context,
            List<TickEnemyJumpPresentationSignal> enemyJumpSignals)
        {
            var candidateEntityIds = new List<int>();
            var seenEntityIds = new HashSet<int>();
            var preMovementEntries = new List<EnemyJumpSnapshotEntry>();
            var postMovementEntries = new List<EnemyJumpSnapshotEntry>();
            var finalEntries = new List<EnemyJumpSnapshotEntry>();

            context.JumpBaselineSnapshot.EnumerateEnemyJumpStatesOrdered(preMovementEntries);
            context.PostMovementSnapshot.EnumerateEnemyJumpStatesOrdered(postMovementEntries);
            context.FinalAuthoritativeSnapshot.EnumerateEnemyJumpStatesOrdered(finalEntries);

            CollectEnemyJumpCandidateIds(preMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyJumpCandidateIds(postMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyJumpCandidateIds(finalEntries, seenEntityIds, candidateEntityIds);

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                var hasPreviousState = context.JumpBaselineSnapshot.TryGetEnemyJumpState(entityId, out var previousJumpState);
                var hasPostMovementState = context.PostMovementSnapshot.TryGetEnemyJumpState(entityId, out var postMovementJumpState);
                var hasFinalState = context.FinalAuthoritativeSnapshot.TryGetEnemyJumpState(entityId, out var finalJumpState);

                var resolvedState = ResolvePresentationJumpState(
                    hasPreviousState,
                    previousJumpState,
                    hasPostMovementState,
                    postMovementJumpState,
                    hasFinalState,
                    finalJumpState);
                if (!ShouldEmitJumpSignal(hasPreviousState, previousJumpState, hasPostMovementState, postMovementJumpState, hasFinalState, finalJumpState))
                {
                    continue;
                }

                var startedWindupThisTick = resolvedState.phase == EnemyJumpPhase.Windup &&
                                            (!hasPreviousState || previousJumpState.phase != EnemyJumpPhase.Windup);
                var retryThisTick = hasPreviousState &&
                                    previousJumpState.phase == EnemyJumpPhase.Airborne &&
                                    resolvedState.phase == EnemyJumpPhase.Airborne &&
                                    resolvedState.retryCount > previousJumpState.retryCount;
                var startedAirborneThisTick = resolvedState.phase == EnemyJumpPhase.Airborne &&
                                              (!hasPreviousState || previousJumpState.phase != EnemyJumpPhase.Airborne) &&
                                              !retryThisTick;
                var landedThisTick = hasPreviousState &&
                                     previousJumpState.phase == EnemyJumpPhase.Airborne &&
                                     resolvedState.phase != EnemyJumpPhase.Airborne &&
                                     context.FinalAuthoritativeSnapshot.TryGetEntity(entityId, out var landedEntity) &&
                                     landedEntity.boardPresence == EntityBoardPresence.Occupying;
                var facing = ResolveJumpPresentationFacing(context, entityId);
                var presentationTargetCell = ResolveJumpPresentationTargetCell(
                    context,
                    entityId,
                    resolvedState,
                    landedThisTick);
                var remainingAirborneTicks = resolvedState.phase == EnemyJumpPhase.Airborne
                    ? Math.Max(0, resolvedState.landingTick - context.CurrentTickIndex)
                    : 0;

                enemyJumpSignals.Add(
                    new TickEnemyJumpPresentationSignal(
                        entityId,
                        resolvedState.sequence,
                        resolvedState.phase,
                        startedWindupThisTick,
                        startedAirborneThisTick,
                        landedThisTick,
                        retryThisTick,
                        sourceCell: resolvedState.sourceCell,
                        lockedTargetCell: resolvedState.lockedTargetCell,
                        presentationTargetCell: presentationTargetCell,
                        facing: facing,
                        landingTick: resolvedState.landingTick,
                        remainingAirborneTicks: remainingAirborneTicks,
                        retryCount: resolvedState.retryCount));
            }
        }

        private static void CollectEnemyActionCandidateIds(
            List<EnemyActionSnapshotEntry> entries,
            HashSet<int> seenEntityIds,
            List<int> candidateEntityIds)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry.State.IsActive ||
                    !seenEntityIds.Add(entry.EntityId))
                {
                    continue;
                }

                candidateEntityIds.Add(entry.EntityId);
            }
        }

        private static void CollectEnemyJumpCandidateIds(
            List<EnemyJumpSnapshotEntry> entries,
            HashSet<int> seenEntityIds,
            List<int> candidateEntityIds)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.State.phase == EnemyJumpPhase.None ||
                    !seenEntityIds.Add(entry.EntityId))
                {
                    continue;
                }

                candidateEntityIds.Add(entry.EntityId);
            }
        }

        private static EnemyJumpRuntimeState ResolvePresentationJumpState(
            bool hasPreviousState,
            in EnemyJumpRuntimeState previousJumpState,
            bool hasPostMovementState,
            in EnemyJumpRuntimeState postMovementJumpState,
            bool hasFinalState,
            in EnemyJumpRuntimeState finalJumpState)
        {
            if (hasFinalState)
            {
                return finalJumpState;
            }

            if (hasPostMovementState)
            {
                return postMovementJumpState;
            }

            return hasPreviousState
                ? previousJumpState
                : default;
        }

        private static bool ShouldEmitJumpSignal(
            bool hasPreviousState,
            in EnemyJumpRuntimeState previousJumpState,
            bool hasPostMovementState,
            in EnemyJumpRuntimeState postMovementJumpState,
            bool hasFinalState,
            in EnemyJumpRuntimeState finalJumpState)
        {
            return (hasPreviousState && previousJumpState.phase != EnemyJumpPhase.None) ||
                   (hasPostMovementState && postMovementJumpState.phase != EnemyJumpPhase.None) ||
                   (hasFinalState && finalJumpState.phase != EnemyJumpPhase.None);
        }

        private static Direction ResolveJumpPresentationFacing(
            in TickPresentationBuildContext context,
            int entityId)
        {
            return TryResolveJumpPresentationEntity(context, entityId, out var entity)
                ? entity.facing
                : Direction.Up;
        }

        private static SurfaceCell ResolveJumpPresentationTargetCell(
            in TickPresentationBuildContext context,
            int entityId,
            in EnemyJumpRuntimeState jumpState,
            bool landedThisTick)
        {
            if (landedThisTick &&
                context.FinalAuthoritativeSnapshot.TryGetEntity(entityId, out var landedEntity))
            {
                return landedEntity.position;
            }

            if (TryResolveJumpPresentationEntity(context, entityId, out var entity) &&
                EnemyJumpQueries.TryResolveLandingCell(
                    context.FinalAuthoritativeSnapshot,
                    entity,
                    jumpState,
                    out var predictedLandingCell,
                    out _))
            {
                return predictedLandingCell;
            }

            return jumpState.lockedTargetCell;
        }

        private static bool TryResolveJumpPresentationEntity(
            in TickPresentationBuildContext context,
            int entityId,
            out EntityState entity)
        {
            return context.FinalAuthoritativeSnapshot.TryGetEntity(entityId, out entity) ||
                   context.PostMovementSnapshot.TryGetEntity(entityId, out entity) ||
                   context.JumpBaselineSnapshot.TryGetEntity(entityId, out entity) ||
                   context.PreMovementSnapshot.TryGetEntity(entityId, out entity);
        }

        private static bool DidStartEnemyRecovery(
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            int entityId)
        {
            return postMovementSnapshot.TryGetEntity(entityId, out var beforeAttackEntity) &&
                   beforeAttackEntity.type == EntityType.Unit &&
                   beforeAttackEntity.aiMode == EnemyAiMode.Attack &&
                   postAttackSnapshot.TryGetEntity(entityId, out var afterAttackEntity) &&
                   afterAttackEntity.aiMode == EnemyAiMode.Recover;
        }

        private static bool IsEnemyUnit(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetEntity(entityId, out var entity) &&
                   entity.type == EntityType.Unit &&
                   entity.aiMode != EnemyAiMode.None;
        }

        private static TickTopologyMotion? BuildTopologyMotion(in TickPresentationBuildContext context)
        {
            var sourceTopology = context.PreMovementSnapshot.Topology;
            var destinationTopology = context.PostMovementSnapshot.Topology;
            if (sourceTopology.Equals(destinationTopology))
            {
                return null;
            }

            return new TickTopologyMotion(
                sourceTopology,
                destinationTopology,
                ResolveRotationKind(context.MovementPhaseResult.SelectedGroups, sourceTopology, destinationTopology));
        }

        private static void BuildTransitionVisibilityPresentation(
            in TickPresentationBuildContext context,
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            IReadOnlyList<TickEntityExitPresentationSignal> entityExitSignals,
            List<TickTransitionVisibilityChange> transitionVisibilityChanges)
        {
            var sourceTopology = context.PreMovementSnapshot.Topology;
            var destinationTopology = context.FinalAuthoritativeSnapshot.Topology;
            if (sourceTopology.Equals(destinationTopology))
            {
                return;
            }

            var excludedEntityIds = CollectTransitionVisibilityExcludedEntityIds(
                context.MovementPhaseResult.SelectedGroups,
                visibilityChanges,
                entityExitSignals);
            var preMovementEntities = new List<EntityState>();
            context.PreMovementSnapshot.EnumerateEntitiesOrdered(preMovementEntities);

            for (var i = 0; i < preMovementEntities.Count; i++)
            {
                var entity = preMovementEntities[i];
                if (excludedEntityIds.Contains(entity.entityId) ||
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, sourceTopology))
                {
                    continue;
                }

                if (context.FinalAuthoritativeSnapshot.TryGetEntity(entity.entityId, out var destinationEntity) &&
                    GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(destinationEntity, destinationTopology))
                {
                    continue;
                }

                transitionVisibilityChanges.Add(
                    new TickTransitionVisibilityChange(
                        entity.entityId,
                        TickTransitionVisibilityMode.RetainUntilTransitionComplete,
                        entity.position,
                        sourceTopology,
                        entity.facing));
            }

            var finalEntities = new List<EntityState>();
            context.FinalAuthoritativeSnapshot.EnumerateEntitiesOrdered(finalEntities);

            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (excludedEntityIds.Contains(entity.entityId) ||
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(entity, destinationTopology))
                {
                    continue;
                }

                if (context.PreMovementSnapshot.TryGetEntity(entity.entityId, out var sourceEntity) &&
                    GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(sourceEntity, sourceTopology))
                {
                    continue;
                }

                transitionVisibilityChanges.Add(
                    new TickTransitionVisibilityChange(
                        entity.entityId,
                        TickTransitionVisibilityMode.ShowAtTransitionStart,
                        entity.position,
                        destinationTopology,
                        entity.facing));
            }
        }

        private static void AppendEntityMotions(
            in TickPresentationBuildContext context,
            ActionGroup group,
            List<TickEntityMotion> entityMotions)
        {
            var motionKind = ResolveMotionKind(group, context.PreMovementSnapshot, context.PostMovementSnapshot);
            if (motionKind == TickEntityMotionKind.None)
            {
                return;
            }

            for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
            {
                var move = group.Moves[moveIndex];
                if (!context.PreMovementSnapshot.TryGetEntity(move.EntityId, out var sourceEntity) ||
                    !context.PostMovementSnapshot.TryGetEntity(move.EntityId, out var destinationEntity) ||
                    destinationEntity.boardPresence != EntityBoardPresence.Occupying)
                {
                    continue;
                }

                entityMotions.Add(
                    new TickEntityMotion(
                        move.EntityId,
                        motionKind,
                        move.SourceCell,
                        move.DestinationCell,
                        context.PreMovementSnapshot.Topology,
                        context.PostMovementSnapshot.Topology,
                        sourceEntity.facing,
                        move.Facing));
            }
        }

        private static void AppendDetachVisibilityChanges(
            WorldSnapshot sourceSnapshot,
            IReadOnlyList<BoardPresenceChangeAction> boardPresenceChanges,
            List<TickVisibilityChange> visibilityChanges,
            ISet<int> excludedEntityIds)
        {
            for (var i = 0; i < boardPresenceChanges.Count; i++)
            {
                var boardPresenceChange = boardPresenceChanges[i];
                if (boardPresenceChange.BoardPresence != EntityBoardPresence.Detached ||
                    excludedEntityIds.Contains(boardPresenceChange.EntityId) ||
                    !sourceSnapshot.TryGetEntity(boardPresenceChange.EntityId, out var sourceEntity))
                {
                    continue;
                }

                visibilityChanges.Add(
                    new TickVisibilityChange(
                        boardPresenceChange.EntityId,
                        TickVisibilityChangeKind.Detach,
                        sourceEntity.position,
                        sourceSnapshot.Topology,
                        sourceEntity.facing));
            }
        }

        private static TickEntityMotionKind ResolveMotionKind(
            ActionGroup group,
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot)
        {
            return group.GroupKind switch
            {
                ActionGroupKind.Push => ResolvePushMotionKind(group, preMovementSnapshot, postMovementSnapshot),
                ActionGroupKind.Flip => TickEntityMotionKind.Flip,
                ActionGroupKind.Move => ResolveMoveMotionKind(group, preMovementSnapshot, postMovementSnapshot),
                ActionGroupKind.Item => ResolveMoveMotionKind(group, preMovementSnapshot, postMovementSnapshot),
                _ => TickEntityMotionKind.None,
            };
        }

        private static TickEntityMotionKind ResolvePushMotionKind(
            ActionGroup group,
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot)
        {
            for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
            {
                var entityId = group.Moves[moveIndex].EntityId;
                if (!TryResolveEntity(entityId, preMovementSnapshot, postMovementSnapshot, out var preEntity, out var postEntity))
                {
                    continue;
                }

                if (IsSlidingPushBox(preEntity) || IsSlidingPushBox(postEntity))
                {
                    return TickEntityMotionKind.BoxSlide;
                }
            }

            return TickEntityMotionKind.Push;
        }

        private static TickEntityMotionKind ResolveMoveMotionKind(
            ActionGroup group,
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot)
        {
            for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
            {
                var entityId = group.Moves[moveIndex].EntityId;
                if (TryResolveEntityType(entityId, preMovementSnapshot, postMovementSnapshot, out var entityType))
                {
                    return entityType == EntityType.Projectile
                        ? TickEntityMotionKind.ProjectileMove
                        : TickEntityMotionKind.Move;
                }
            }

            return TickEntityMotionKind.None;
        }

        private static HashSet<int> CollectTransitionVisibilityExcludedEntityIds(
            IReadOnlyList<ActionGroup> selectedGroups,
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            IReadOnlyList<TickEntityExitPresentationSignal> entityExitSignals)
        {
            var excludedEntityIds = new HashSet<int>();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                {
                    excludedEntityIds.Add(group.Moves[moveIndex].EntityId);
                }
            }

            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                excludedEntityIds.Add(visibilityChanges[i].EntityId);
            }

            for (var i = 0; i < entityExitSignals.Count; i++)
            {
                excludedEntityIds.Add(entityExitSignals[i].ExitedEntityId);
            }

            return excludedEntityIds;
        }

        private static HashSet<int> CollectDestroyTargets(ActionGroup group)
        {
            var destroyTargets = new HashSet<int>();
            for (var destroyIndex = 0; destroyIndex < group.Destroys.Count; destroyIndex++)
            {
                destroyTargets.Add(group.Destroys[destroyIndex].TargetId);
            }

            return destroyTargets;
        }

        private static TickEntityExitCause ResolveEntityExitCause(
            ActionGroup group,
            ISet<int> destroyTargets,
            EntityState sourceEntity)
        {
            if (group.GroupKind == ActionGroupKind.Item)
            {
                return TickEntityExitCause.ItemConsume;
            }

            if (sourceEntity.type == EntityType.Box &&
                destroyTargets.Contains(sourceEntity.entityId))
            {
                return TickEntityExitCause.BoxDestroy;
            }

            return TickEntityExitCause.None;
        }

        private static bool TryResolveEntityType(
            int entityId,
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot,
            out EntityType entityType)
        {
            if (preMovementSnapshot.TryGetEntity(entityId, out var preMovementEntity))
            {
                entityType = preMovementEntity.type;
                return true;
            }

            if (postMovementSnapshot.TryGetEntity(entityId, out var postMovementEntity))
            {
                entityType = postMovementEntity.type;
                return true;
            }

            entityType = default;
            return false;
        }

        private static bool TryResolveEntity(
            int entityId,
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot,
            out EntityState? preEntity,
            out EntityState? postEntity)
        {
            var hasPreEntity = preMovementSnapshot.TryGetEntity(entityId, out var resolvedPreEntity);
            var hasPostEntity = postMovementSnapshot.TryGetEntity(entityId, out var resolvedPostEntity);
            preEntity = hasPreEntity ? resolvedPreEntity : null;
            postEntity = hasPostEntity ? resolvedPostEntity : null;
            return hasPreEntity || hasPostEntity;
        }

        private static bool IsSlidingPushBox(EntityState? entity)
        {
            return entity.HasValue &&
                   entity.Value.type == EntityType.Box &&
                   entity.Value.state == EntityPhaseState.Sliding &&
                   (entity.Value.boxCapabilities & BoxCapabilities.Push) == BoxCapabilities.Push;
        }

        private static CubeRotationKind ResolveRotationKind(
            IReadOnlyList<ActionGroup> selectedGroups,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology)
        {
            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var topologyChanges = selectedGroups[groupIndex].TopologyChanges;
                for (var changeIndex = 0; changeIndex < topologyChanges.Count; changeIndex++)
                {
                    var topologyChange = topologyChanges[changeIndex];
                    if (!topologyChange.UpdatedTopology.Equals(destinationTopology))
                    {
                        continue;
                    }

                    return topologyChange.RotationKind;
                }
            }

            if (FaceIdUtility.GetNext(sourceTopology.BottomFace) == destinationTopology.BottomFace)
            {
                return CubeRotationKind.Forward;
            }

            if (FaceIdUtility.GetPrevious(sourceTopology.BottomFace) == destinationTopology.BottomFace)
            {
                return CubeRotationKind.Backward;
            }

            return CubeRotationKind.None;
        }
    }
}

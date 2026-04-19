using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Objectives;
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
            RespawnPhaseResult respawnPhaseResult,
            StageObjectiveTickResult objectiveResult,
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

            if (respawnPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(respawnPhaseResult));
            }

            if (objectiveResult == null)
            {
                throw new ArgumentNullException(nameof(objectiveResult));
            }

            var finalEntities = new List<EntityState>();
            finalSnapshot.EnumerateEntitiesOrdered(finalEntities);

            var eventLog = new List<string>(
                movementPhaseResult.CommitEvents.Count +
                attackPhaseResult.EventLogEntries.Count +
                cleanupPhaseResult.RemovedEntityIds.Count +
                cleanupPhaseResult.TimerChanges.Count +
                cleanupPhaseResult.StateTransitions.Count +
                respawnPhaseResult.EventLogEntries.Count);

            AddRange(eventLog, movementPhaseResult.CommitEvents);
            AddRange(eventLog, attackPhaseResult.EventLogEntries);

            for (var i = 0; i < cleanupPhaseResult.RemovedEntityIds.Count; i++)
            {
                eventLog.Add($"CleanupRemoved|E={cleanupPhaseResult.RemovedEntityIds[i]}");
            }

            AddRange(eventLog, cleanupPhaseResult.TimerChanges);
            AddRange(eventLog, cleanupPhaseResult.StateTransitions);
            AddRange(eventLog, respawnPhaseResult.EventLogEntries);

            return new TickResultData(
                finalEntities,
                pendingDelayedAttackEffects,
                eventLog,
                _presentationDataBuilder.Build(presentationBuildContext),
                objectiveResult);
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
        private readonly StageObjectiveTickResult _objectiveResult;

        public TickResultData(
            IEnumerable<EntityState> finalEntities,
            IEnumerable<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            IEnumerable<string> eventLog)
            : this(
                finalEntities,
                pendingDelayedAttackEffects,
                eventLog,
                TickPresentationData.Empty,
                StageObjectiveTickResult.NoObjective)
        {
        }

        public TickResultData(
            IEnumerable<EntityState> finalEntities,
            IEnumerable<DelayedAttackEffectRecord> pendingDelayedAttackEffects,
            IEnumerable<string> eventLog,
            TickPresentationData presentationData,
            StageObjectiveTickResult objectiveResult = null)
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
            _objectiveResult = objectiveResult ?? StageObjectiveTickResult.NoObjective;
            _finalEntities = new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities));
            _pendingDelayedAttackEffects = new ReadOnlyCollection<DelayedAttackEffectRecord>(new List<DelayedAttackEffectRecord>(pendingDelayedAttackEffects));
            _eventLog = new ReadOnlyCollection<string>(new List<string>(eventLog));
        }

        public IReadOnlyList<EntityState> FinalEntities => _finalEntities;

        public IReadOnlyList<DelayedAttackEffectRecord> PendingDelayedAttackEffects => _pendingDelayedAttackEffects;

        public IReadOnlyList<string> EventLog => _eventLog;

        public TickPresentationData PresentationData => _presentationData;

        public StageObjectiveTickResult ObjectiveResult => _objectiveResult;
    }

    internal sealed class RespawnPhaseResult
    {
        public static readonly RespawnPhaseResult Empty = new(
            Array.Empty<EntityState>(),
            Array.Empty<string>());

        private readonly ReadOnlyCollection<string> _eventLogEntries;
        private readonly ReadOnlyCollection<EntityState> _respawnedEntities;

        public RespawnPhaseResult(
            IEnumerable<EntityState> respawnedEntities,
            IEnumerable<string> eventLogEntries)
        {
            if (respawnedEntities == null)
            {
                throw new ArgumentNullException(nameof(respawnedEntities));
            }

            if (eventLogEntries == null)
            {
                throw new ArgumentNullException(nameof(eventLogEntries));
            }

            _respawnedEntities = new ReadOnlyCollection<EntityState>(new List<EntityState>(respawnedEntities));
            _eventLogEntries = new ReadOnlyCollection<string>(new List<string>(eventLogEntries));
        }

        public IReadOnlyList<EntityState> RespawnedEntities => _respawnedEntities;

        public IReadOnlyList<string> EventLogEntries => _eventLogEntries;
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
            WorldSnapshot jumpBaselineSnapshot = null,
            PlayerTickCommand playerCommand = default)
            : this(
                preMovementSnapshot,
                postMovementSnapshot,
                postAttackSnapshot,
                finalAuthoritativeSnapshot,
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                RespawnPhaseResult.Empty,
                currentTickIndex,
                jumpBaselineSnapshot,
                playerCommand)
        {
        }

        public TickPresentationBuildContext(
            WorldSnapshot preMovementSnapshot,
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            WorldSnapshot finalAuthoritativeSnapshot,
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            CleanupPhaseResult cleanupPhaseResult,
            RespawnPhaseResult respawnPhaseResult,
            int currentTickIndex = 0,
            WorldSnapshot jumpBaselineSnapshot = null,
            PlayerTickCommand playerCommand = default)
            : this(
                preMovementSnapshot,
                postMovementSnapshot,
                postAttackSnapshot,
                finalAuthoritativeSnapshot,
                new PreMovementStatePhaseResult(new List<string>(), new List<PlayerActionTransition>()),
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                respawnPhaseResult,
                currentTickIndex,
                jumpBaselineSnapshot,
                playerCommand)
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
            WorldSnapshot jumpBaselineSnapshot = null,
            PlayerTickCommand playerCommand = default)
            : this(
                preMovementSnapshot,
                postMovementSnapshot,
                postAttackSnapshot,
                finalAuthoritativeSnapshot,
                preMovementStatePhaseResult,
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult,
                RespawnPhaseResult.Empty,
                currentTickIndex,
                jumpBaselineSnapshot,
                playerCommand)
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
            RespawnPhaseResult respawnPhaseResult,
            int currentTickIndex = 0,
            WorldSnapshot jumpBaselineSnapshot = null,
            PlayerTickCommand playerCommand = default)
        {
            PreMovementSnapshot = preMovementSnapshot ?? throw new ArgumentNullException(nameof(preMovementSnapshot));
            PostMovementSnapshot = postMovementSnapshot ?? throw new ArgumentNullException(nameof(postMovementSnapshot));
            PostAttackSnapshot = postAttackSnapshot ?? throw new ArgumentNullException(nameof(postAttackSnapshot));
            FinalAuthoritativeSnapshot = finalAuthoritativeSnapshot ?? throw new ArgumentNullException(nameof(finalAuthoritativeSnapshot));
            PreMovementStatePhaseResult = preMovementStatePhaseResult ?? throw new ArgumentNullException(nameof(preMovementStatePhaseResult));
            MovementPhaseResult = movementPhaseResult ?? throw new ArgumentNullException(nameof(movementPhaseResult));
            AttackPhaseResult = attackPhaseResult ?? throw new ArgumentNullException(nameof(attackPhaseResult));
            CleanupPhaseResult = cleanupPhaseResult ?? throw new ArgumentNullException(nameof(cleanupPhaseResult));
            RespawnPhaseResult = respawnPhaseResult ?? throw new ArgumentNullException(nameof(respawnPhaseResult));
            CurrentTickIndex = currentTickIndex;
            JumpBaselineSnapshot = jumpBaselineSnapshot ?? PreMovementSnapshot;
            PlayerCommand = playerCommand;
        }

        public WorldSnapshot PreMovementSnapshot { get; }

        // Movement-visible surface after resolve. This may include accepted impact
        // follow-through vacates/moves, but it is not the authoritative HP/destroy truth.
        public WorldSnapshot PostMovementSnapshot { get; }

        // Attack-authoritative surface after damage/destroy has been resolved.
        public WorldSnapshot PostAttackSnapshot { get; }

        public WorldSnapshot FinalAuthoritativeSnapshot { get; }

        public PreMovementStatePhaseResult PreMovementStatePhaseResult { get; }

        public MovementPhaseResult MovementPhaseResult { get; }

        public AttackPhaseResult AttackPhaseResult { get; }

        public CleanupPhaseResult CleanupPhaseResult { get; }

        public RespawnPhaseResult RespawnPhaseResult { get; }

        public int CurrentTickIndex { get; }

        public WorldSnapshot JumpBaselineSnapshot { get; }

        public PlayerTickCommand PlayerCommand { get; }
    }

    internal sealed class TickPresentationDataBuilder
    {
        public TickPresentationData Build(in TickPresentationBuildContext context)
        {
            var entityMotions = new List<TickEntityMotion>();
            var entityExitSignals = new List<TickEntityExitPresentationSignal>();
            var impactTransientSignals = new List<TickImpactTransientPresentationSignal>();
            var enemyActionSignals = new List<TickEnemyActionPresentationSignal>();
            var enemyDamageSignals = new List<TickEnemyDamagePresentationSignal>();
            var enemyJumpSignals = new List<TickEnemyJumpPresentationSignal>();
            var playerActionSignals = new List<TickPlayerActionPresentationSignal>();
            var playerDamageSignals = new List<TickPlayerDamagePresentationSignal>();
            var playerLocomotionSignals = new List<TickPlayerLocomotionPresentationSignal>();
            var visibilityChanges = new List<TickVisibilityChange>();
            var transitionVisibilityChanges = new List<TickTransitionVisibilityChange>();
            var exitOwnedEntityIds = new HashSet<int>();

            BuildEntityExitPresentation(context, entityExitSignals, exitOwnedEntityIds);
            BuildImpactTransientPresentation(context, impactTransientSignals);
            BuildMovementPresentation(context, entityMotions, visibilityChanges, exitOwnedEntityIds);
            BuildAttackPresentation(context, visibilityChanges);
            BuildCleanupPresentation(context, visibilityChanges, exitOwnedEntityIds);
            BuildRespawnPresentation(context, visibilityChanges);
            BuildPlayerPresentation(context, playerActionSignals);
            BuildPlayerDamagePresentation(context, playerDamageSignals);
            BuildPlayerLocomotionPresentation(context, playerLocomotionSignals);
            BuildEnemyDamagePresentation(context, enemyDamageSignals);
            BuildEnemyPresentation(context, enemyActionSignals);
            BuildEnemyJumpPresentation(context, enemyJumpSignals);

            var topologyMotion = BuildTopologyMotion(context);
            BuildTransitionVisibilityPresentation(context, visibilityChanges, entityExitSignals, transitionVisibilityChanges);

            return entityMotions.Count == 0 &&
                   enemyActionSignals.Count == 0 &&
                   enemyDamageSignals.Count == 0 &&
                   enemyJumpSignals.Count == 0 &&
                   entityExitSignals.Count == 0 &&
                   impactTransientSignals.Count == 0 &&
                   playerActionSignals.Count == 0 &&
                   playerDamageSignals.Count == 0 &&
                   playerLocomotionSignals.Count == 0 &&
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
                    playerLocomotionSignals,
                    playerDamageSignals,
                    enemyDamageSignals,
                    enemyActionSignals,
                    enemyJumpSignals,
                    entityExitSignals,
                    impactTransientSignals);
        }

        private static void BuildMovementPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityMotion> entityMotions,
            List<TickVisibilityChange> visibilityChanges,
            ISet<int> exitOwnedEntityIds)
        {
            var operations = context.MovementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.MoveEntity)
                {
                    AppendEntityMotion(context, operation, entityMotions);
                    continue;
                }

                if (operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                    operation.BoardPresence == EntityBoardPresence.Detached &&
                    !exitOwnedEntityIds.Contains(operation.EntityId) &&
                    context.PreMovementSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity))
                {
                    visibilityChanges.Add(
                        new TickVisibilityChange(
                            operation.EntityId,
                            TickVisibilityChangeKind.Detach,
                            sourceEntity.position,
                            context.PreMovementSnapshot.Topology,
                            sourceEntity.facing));
                }
            }
        }

        private static void BuildImpactTransientPresentation(
            in TickPresentationBuildContext context,
            List<TickImpactTransientPresentationSignal> impactTransientSignals)
        {
            var removedEntityIds = new HashSet<int>(context.CleanupPhaseResult.RemovedEntityIds);
            var dispositionRecords = context.MovementPhaseResult.ImpactDispositionRecords;
            var signaledEntityIds = new HashSet<int>();

            for (var i = 0; i < dispositionRecords.Count; i++)
            {
                var record = dispositionRecords[i];
                if (record.PolicyKind != ImpactDispositionPolicyKind.Flip ||
                    record.DispositionKind != ImpactDispositionKind.DestroySelf ||
                    !removedEntityIds.Contains(record.ImpactSourceEntityId) ||
                    !signaledEntityIds.Add(record.ImpactSourceEntityId) ||
                    !context.PreMovementSnapshot.TryGetEntity(record.ImpactSourceEntityId, out var sourceEntity))
                {
                    continue;
                }

                impactTransientSignals.Add(
                    new TickImpactTransientPresentationSignal(
                        record.ImpactSourceEntityId,
                        sourceEntity.type,
                        sourceEntity.position,
                        record.ImpactCell,
                        context.PreMovementSnapshot.Topology,
                        sourceEntity.facing,
                        BuildStableImpactPresentationSeed(
                            context.CurrentTickIndex,
                            record.ImpactSourceEntityId,
                            record.ImpactTargetEntityId)));
            }
        }

        private static void BuildAttackPresentation(
            in TickPresentationBuildContext context,
            List<TickVisibilityChange> visibilityChanges)
        {
            var operations = context.AttackPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind != FinalizationOperationKind.SpawnEntity)
                {
                    continue;
                }

                var spawnedEntity = operation.SpawnedEntity;
                visibilityChanges.Add(
                    new TickVisibilityChange(
                        spawnedEntity.entityId,
                        TickVisibilityChangeKind.Spawn,
                        spawnedEntity.position,
                        context.PostAttackSnapshot.Topology,
                        spawnedEntity.facing));
            }
        }

        private static void BuildEntityExitPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityExitPresentationSignal> entityExitSignals,
            ISet<int> exitOwnedEntityIds)
        {
            // Exit-owned removals bypass generic detach/remove visibility tracks. The
            // authoritative entity view disappears immediately; only transient echoes linger.
            var signaledEntityIds = new HashSet<int>();
            BuildMovementOwnedExitPresentation(
                context,
                entityExitSignals,
                exitOwnedEntityIds,
                signaledEntityIds);
            BuildAttackOwnedExitPresentation(
                context,
                entityExitSignals,
                exitOwnedEntityIds,
                signaledEntityIds);
        }

        private static void BuildMovementOwnedExitPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityExitPresentationSignal> entityExitSignals,
            ISet<int> exitOwnedEntityIds,
            ISet<int> signaledEntityIds)
        {
            var removedEntityIds = new HashSet<int>(context.CleanupPhaseResult.RemovedEntityIds);
            var operations = context.MovementPhaseResult.ResolvedOperations;

            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Metadata.ExitCauseHint == TickEntityExitCause.None ||
                    !removedEntityIds.Contains(operation.EntityId) ||
                    !signaledEntityIds.Add(operation.EntityId) ||
                    !context.PreMovementSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity))
                {
                    continue;
                }

                var isExitSignal =
                    (operation.Kind == FinalizationOperationKind.SetBoardPresence &&
                     operation.BoardPresence == EntityBoardPresence.Detached) ||
                    operation.Kind == FinalizationOperationKind.MarkDestroy;
                if (!isExitSignal)
                {
                    signaledEntityIds.Remove(operation.EntityId);
                    continue;
                }

                var exitCause = operation.Metadata.ExitCauseHint;
                entityExitSignals.Add(
                    new TickEntityExitPresentationSignal(
                        operation.EntityId,
                        exitCause,
                        sourceEntity.position,
                        context.PreMovementSnapshot.Topology,
                        sourceEntity.facing,
                        sourceEntity.type,
                        sourceActorEntityId: operation.Metadata.SourceActorEntityId,
                        presentationSeed: BuildStablePresentationSeed(
                            context.CurrentTickIndex,
                            operation.EntityId,
                            operation.Metadata.SourceActorEntityId,
                            exitCause)));
                exitOwnedEntityIds.Add(operation.EntityId);
            }
        }

        private static void BuildAttackOwnedExitPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityExitPresentationSignal> entityExitSignals,
            ISet<int> exitOwnedEntityIds,
            ISet<int> signaledEntityIds)
        {
            var removedEntityIds = context.CleanupPhaseResult.RemovedEntityIds;
            for (var i = 0; i < removedEntityIds.Count; i++)
            {
                var entityId = removedEntityIds[i];
                if (exitOwnedEntityIds.Contains(entityId) ||
                    !signaledEntityIds.Add(entityId) ||
                    !context.PostAttackSnapshot.TryGetEntity(entityId, out var removedEntity) ||
                    !IsEnemyUnit(context.PostAttackSnapshot, entityId) ||
                    !TryFindAttackDestroyOperation(context.AttackPhaseResult.ResolvedOperations, entityId, out var destroyOperation))
                {
                    continue;
                }

                entityExitSignals.Add(
                    new TickEntityExitPresentationSignal(
                        entityId,
                        destroyOperation.Metadata.ExitCauseHint,
                        removedEntity.position,
                        context.PostAttackSnapshot.Topology,
                        removedEntity.facing,
                        removedEntity.type,
                        sourceActorEntityId: destroyOperation.Metadata.SourceActorEntityId,
                        presentationSeed: BuildStablePresentationSeed(
                            context.CurrentTickIndex,
                            entityId,
                            destroyOperation.Metadata.SourceActorEntityId,
                            destroyOperation.Metadata.ExitCauseHint)));
                exitOwnedEntityIds.Add(entityId);
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

        private static void BuildRespawnPresentation(
            in TickPresentationBuildContext context,
            List<TickVisibilityChange> visibilityChanges)
        {
            var respawnedEntities = context.RespawnPhaseResult.RespawnedEntities;

            for (var i = 0; i < respawnedEntities.Count; i++)
            {
                var entity = respawnedEntities[i];
                visibilityChanges.Add(
                    new TickVisibilityChange(
                        entity.entityId,
                        TickVisibilityChangeKind.Spawn,
                        entity.position,
                        context.FinalAuthoritativeSnapshot.Topology,
                        entity.facing));
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
                var targetEntityId = 0;
                var direction = Direction.None;

                if (transitionsByEntityId.TryGetValue(entityId, out var transition))
                {
                    activeActionKind = transition.CurrentKind;
                    activeActionSequence = transition.CurrentSequence;
                    if ((transition.CompletedThisTick || transition.CanceledThisTick) &&
                        transition.CurrentKind == PlayerActionKind.None)
                    {
                        activeActionKind = transition.PreviousKind;
                        activeActionSequence = transition.PreviousSequence;
                    }

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
                    targetEntityId = controlState.activeAction.targetEntityId;
                    direction = controlState.activeAction.direction;
                }

                var isRecoveryPhase =
                    activeActionKind != PlayerActionKind.None &&
                    context.FinalAuthoritativeSnapshot.TryGetPlayerControlState(entityId, out var finalControlState) &&
                    finalControlState.activeAction.IsActive &&
                    finalControlState.activeAction.executeTick <= context.CurrentTickIndex;
                var resolutionKind = ResolvePlayerActionResolutionKind(
                    context,
                    entityId,
                    activeActionKind,
                    executedThisTick);

                playerActionSignals.Add(
                    new TickPlayerActionPresentationSignal(
                        entityId,
                        activeActionKind,
                        activeActionSequence,
                        startedThisTick,
                        completedThisTick,
                        canceledThisTick,
                        executedThisTick,
                        isRecoveryPhase,
                        resolutionKind,
                        targetEntityId,
                        direction));
            }
        }

        private static void BuildPlayerLocomotionPresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals)
        {
            var playerControlEntries = new List<PlayerControlSnapshotEntry>();
            context.FinalAuthoritativeSnapshot.EnumeratePlayerControlStatesOrdered(playerControlEntries);

            for (var i = 0; i < playerControlEntries.Count; i++)
            {
                var entry = playerControlEntries[i];
                var moveMotionGeneratedThisTick = DidGeneratePlayerMoveMotionThisTick(context, entry.EntityId);
                var waitingForNextMoveCadence = ShouldWaitForNextMoveCadence(context, entry.State);
                var shouldPlayWalkLoop =
                    !entry.State.activeAction.IsActive &&
                    !context.PlayerCommand.FlipPressed &&
                    (moveMotionGeneratedThisTick || waitingForNextMoveCadence);

                playerLocomotionSignals.Add(
                    new TickPlayerLocomotionPresentationSignal(
                        entry.EntityId,
                        shouldPlayWalkLoop,
                        moveMotionGeneratedThisTick,
                        waitingForNextMoveCadence,
                        context.PlayerCommand.MoveDirection,
                        context.PlayerCommand.IsMoveBuffered));
            }
        }

        private static void BuildPlayerDamagePresentation(
            in TickPresentationBuildContext context,
            List<TickPlayerDamagePresentationSignal> playerDamageSignals)
        {
            var aggregatedDamageByPlayerId = new Dictionary<int, int>();

            for (var i = 0; i < context.AttackPhaseResult.DamageResolutions.Count; i++)
            {
                var resolution = context.AttackPhaseResult.DamageResolutions[i];
                if (!resolution.Accepted ||
                    !context.PostAttackSnapshot.TryGetEntity(resolution.TargetId, out var target) ||
                    !EntityRolePolicy.IsPlayerUnit(target))
                {
                    continue;
                }

                if (!aggregatedDamageByPlayerId.TryGetValue(resolution.TargetId, out var amount))
                {
                    amount = 0;
                }

                aggregatedDamageByPlayerId[resolution.TargetId] = amount + resolution.Amount;
            }

            foreach (var pair in aggregatedDamageByPlayerId)
            {
                playerDamageSignals.Add(
                    new TickPlayerDamagePresentationSignal(
                        pair.Key,
                        tookDamageThisTick: true,
                        pair.Value));
            }
        }

        private static TickPlayerActionResolutionKind ResolvePlayerActionResolutionKind(
            in TickPresentationBuildContext context,
            int entityId,
            PlayerActionKind actionKind,
            bool executedThisTick)
        {
            if (!executedThisTick ||
                actionKind == PlayerActionKind.None)
            {
                return TickPlayerActionResolutionKind.None;
            }

            if (DidResolvePlayerImpactThisTick(context.AttackPhaseResult, entityId))
            {
                return TickPlayerActionResolutionKind.Impact;
            }

            if (DidResolvePlayerActionMovementThisTick(context.MovementPhaseResult, entityId, actionKind))
            {
                return TickPlayerActionResolutionKind.Success;
            }

            return TickPlayerActionResolutionKind.Blocked;
        }

        private static bool DidResolvePlayerImpactThisTick(
            AttackPhaseResult attackPhaseResult,
            int entityId)
        {
            var reservations = attackPhaseResult.DrainedImpactReservations;
            for (var i = 0; i < reservations.Count; i++)
            {
                if (reservations[i].SourceId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DidResolvePlayerActionMovementThisTick(
            MovementPhaseResult movementPhaseResult,
            int entityId,
            PlayerActionKind actionKind)
        {
            var operations = movementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind != FinalizationOperationKind.MoveEntity ||
                    operation.Metadata.SourceActorEntityId != entityId)
                {
                    continue;
                }

                switch (actionKind)
                {
                    case PlayerActionKind.Push when operation.Metadata.SemanticKind == ResolvedActionSemanticKind.Push:
                    case PlayerActionKind.Flip when operation.Metadata.SemanticKind == ResolvedActionSemanticKind.Flip:
                        return true;
                }
            }

            return false;
        }

        private static void BuildEnemyPresentation(
            in TickPresentationBuildContext context,
            List<TickEnemyActionPresentationSignal> enemyActionSignals)
        {
            var candidateEntityIds = new List<int>();
            var seenEntityIds = new HashSet<int>();
            var executedEntityIds = new HashSet<int>();
            var preMovementEntries = new List<EnemyActionSnapshotEntry>();
            var postAttackEntries = new List<EnemyActionSnapshotEntry>();
            var finalEntries = new List<EnemyActionSnapshotEntry>();

            context.PreMovementSnapshot.EnumerateEnemyActionStatesOrdered(preMovementEntries);
            context.PostAttackSnapshot.EnumerateEnemyActionStatesOrdered(postAttackEntries);
            context.FinalAuthoritativeSnapshot.EnumerateEnemyActionStatesOrdered(finalEntries);

            CollectEnemyActionCandidateIds(preMovementEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyActionCandidateIds(postAttackEntries, seenEntityIds, candidateEntityIds);
            CollectEnemyActionCandidateIds(finalEntries, seenEntityIds, candidateEntityIds);

            for (var i = 0; i < context.AttackPhaseResult.ResolutionRecords.Count; i++)
            {
                var resolutionRecord = context.AttackPhaseResult.ResolutionRecords[i];
                if (resolutionRecord.Kind != ContestKind.Plan ||
                    !resolutionRecord.Accepted ||
                    !IsEnemyUnit(context.PostMovementSnapshot, resolutionRecord.SourceId))
                {
                    continue;
                }

                executedEntityIds.Add(resolutionRecord.SourceId);
                if (seenEntityIds.Add(resolutionRecord.SourceId))
                {
                    candidateEntityIds.Add(resolutionRecord.SourceId);
                }
            }

            for (var i = 0; i < candidateEntityIds.Count; i++)
            {
                var entityId = candidateEntityIds[i];
                context.PreMovementSnapshot.TryGetEnemyActionState(entityId, out var previousAction);
                context.PostAttackSnapshot.TryGetEnemyActionState(entityId, out var currentAction);

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

        private static void BuildEnemyDamagePresentation(
            in TickPresentationBuildContext context,
            List<TickEnemyDamagePresentationSignal> enemyDamageSignals)
        {
            var acceptedDamageByEntityId = new Dictionary<int, int>();
            var damageResolutions = context.AttackPhaseResult.DamageResolutions;

            for (var i = 0; i < damageResolutions.Count; i++)
            {
                var resolution = damageResolutions[i];
                if (!resolution.Accepted ||
                    !context.PostAttackSnapshot.TryGetEntity(resolution.TargetId, out var targetEntity) ||
                    !EntityRolePolicy.IsEnemyUnit(targetEntity))
                {
                    continue;
                }

                acceptedDamageByEntityId.TryGetValue(targetEntity.entityId, out var accumulatedDamage);
                acceptedDamageByEntityId[targetEntity.entityId] = accumulatedDamage + resolution.Amount;
            }

            foreach (var pair in acceptedDamageByEntityId)
            {
                enemyDamageSignals.Add(
                    new TickEnemyDamagePresentationSignal(
                        pair.Key,
                        tookDamageThisTick: true,
                        pair.Value));
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

                var startedWindupThisTick = false;
                var startedAirborneThisTick = false;
                var landedThisTick = false;
                var retryThisTick = false;
                var presentationTargetCell = resolvedState.lockedTargetCell;
                if (TryFindJumpPresentationOperation(context.MovementPhaseResult.ResolvedOperations, entityId, out var jumpOperation))
                {
                    startedWindupThisTick = jumpOperation.Metadata.JumpPresentationKind == JumpPresentationKind.WindupStart;
                    startedAirborneThisTick = jumpOperation.Metadata.JumpPresentationKind == JumpPresentationKind.AirborneStart;
                    landedThisTick = jumpOperation.Metadata.JumpPresentationKind == JumpPresentationKind.LandingSuccess;
                    retryThisTick = jumpOperation.Metadata.JumpPresentationKind == JumpPresentationKind.LandingRetry;
                    if (!jumpOperation.Metadata.PresentationTargetCell.Equals(default(SurfaceCell)))
                    {
                        presentationTargetCell = jumpOperation.Metadata.PresentationTargetCell;
                    }
                }

                var facing = ResolveJumpPresentationFacing(context, entityId);
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
                ResolveRotationKind(
                    context.MovementPhaseResult.ResolvedOperations,
                    sourceTopology,
                    destinationTopology));
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
                context.MovementPhaseResult.ResolvedOperations,
                visibilityChanges,
                entityExitSignals);
            var finalEntities = new List<EntityState>();
            context.FinalAuthoritativeSnapshot.EnumerateEntitiesOrdered(finalEntities);

            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (excludedEntityIds.Contains(entity.entityId) ||
                    !context.FinalAuthoritativeSnapshot.TryGetResolvedSpatialState(entity.entityId, out var destinationSpatialState) ||
                    !GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(destinationSpatialState))
                {
                    continue;
                }

                if (context.PreMovementSnapshot.TryGetEntity(entity.entityId, out var sourceEntity) &&
                    context.PreMovementSnapshot.TryGetResolvedSpatialState(sourceEntity.entityId, out var sourceSpatialState) &&
                    GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(sourceSpatialState))
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

        private static void AppendEntityMotion(
            in TickPresentationBuildContext context,
            FinalizationOperation operation,
            List<TickEntityMotion> entityMotions)
        {
            if (operation.Kind != FinalizationOperationKind.MoveEntity ||
                !TryResolveMotionKind(operation.Metadata.MovementSemanticKind, out var motionKind) ||
                !context.PreMovementSnapshot.TryGetEntity(operation.EntityId, out var sourceEntity) ||
                !context.PostMovementSnapshot.TryGetEntity(operation.EntityId, out var destinationEntity) ||
                destinationEntity.boardPresence != EntityBoardPresence.Occupying)
            {
                return;
            }

            entityMotions.Add(
                new TickEntityMotion(
                    operation.EntityId,
                    motionKind,
                    sourceEntity.position,
                    operation.Destination,
                    context.PreMovementSnapshot.Topology,
                    context.PostMovementSnapshot.Topology,
                    sourceEntity.facing,
                    destinationEntity.facing));
        }

        private static bool TryResolveMotionKind(
            MovementSemanticKind semanticKind,
            out TickEntityMotionKind motionKind)
        {
            motionKind = semanticKind switch
            {
                MovementSemanticKind.Move => TickEntityMotionKind.Move,
                MovementSemanticKind.Item => TickEntityMotionKind.Move,
                MovementSemanticKind.Push => TickEntityMotionKind.Push,
                MovementSemanticKind.Flip => TickEntityMotionKind.Flip,
                MovementSemanticKind.Slide => TickEntityMotionKind.BoxSlide,
                MovementSemanticKind.ProjectileMove => TickEntityMotionKind.ProjectileMove,
                _ => TickEntityMotionKind.None,
            };
            return motionKind != TickEntityMotionKind.None;
        }

        private static bool DidGeneratePlayerMoveMotionThisTick(
            in TickPresentationBuildContext context,
            int entityId)
        {
            var operations = context.MovementPhaseResult.ResolvedOperations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == entityId &&
                    TryResolveMotionKind(operation.Metadata.MovementSemanticKind, out var motionKind) &&
                    motionKind == TickEntityMotionKind.Move)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldWaitForNextMoveCadence(
            in TickPresentationBuildContext context,
            in PlayerControlState controlState)
        {
            return context.PlayerCommand.MoveDirection != Direction.None &&
                   !context.PlayerCommand.FlipPressed &&
                   !context.PlayerCommand.IsMoveBuffered &&
                   !controlState.activeAction.IsActive &&
                   PlayerControlQueries.IsMoveOnCooldown(controlState, context.CurrentTickIndex);
        }

        private static HashSet<int> CollectTransitionVisibilityExcludedEntityIds(
            IReadOnlyList<FinalizationOperation> movementOperations,
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            IReadOnlyList<TickEntityExitPresentationSignal> entityExitSignals)
        {
            var excludedEntityIds = new HashSet<int>();

            for (var i = 0; i < movementOperations.Count; i++)
            {
                if (movementOperations[i].Kind == FinalizationOperationKind.MoveEntity)
                {
                    excludedEntityIds.Add(movementOperations[i].EntityId);
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

        private static bool TryFindAttackDestroyOperation(
            IReadOnlyList<FinalizationOperation> operations,
            int targetEntityId,
            out FinalizationOperation destroyOperation)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.MarkDestroy &&
                    operation.EntityId == targetEntityId &&
                    operation.Metadata.ExitCauseHint != TickEntityExitCause.None)
                {
                    destroyOperation = operation;
                    return true;
                }
            }

            destroyOperation = default;
            return false;
        }

        private static bool TryFindJumpPresentationOperation(
            IReadOnlyList<FinalizationOperation> operations,
            int entityId,
            out FinalizationOperation jumpOperation)
        {
            for (var i = operations.Count - 1; i >= 0; i--)
            {
                var operation = operations[i];
                if (operation.Kind == FinalizationOperationKind.SetEnemyJumpState &&
                    operation.EntityId == entityId &&
                    operation.Metadata.JumpPresentationKind != JumpPresentationKind.None)
                {
                    jumpOperation = operation;
                    return true;
                }
            }

            jumpOperation = default;
            return false;
        }

        private static int BuildStablePresentationSeed(
            int currentTickIndex,
            int exitedEntityId,
            int sourceActorEntityId,
            TickEntityExitCause exitCause)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = MixStableSeed(hash, currentTickIndex);
                hash = MixStableSeed(hash, exitedEntityId);
                hash = MixStableSeed(hash, sourceActorEntityId);
                hash = MixStableSeed(hash, (int)exitCause);
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static int BuildStableImpactPresentationSeed(
            int currentTickIndex,
            int sourceEntityId,
            int targetEntityId)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = MixStableSeed(hash, currentTickIndex);
                hash = MixStableSeed(hash, sourceEntityId);
                hash = MixStableSeed(hash, targetEntityId);
                hash = MixStableSeed(hash, (int)TickEntityExitCause.DestroyedByImpact);
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static uint MixStableSeed(uint hash, int value)
        {
            unchecked
            {
                return (hash ^ (uint)value) * 16777619u;
            }
        }

        private static CubeRotationKind ResolveRotationKind(
            IReadOnlyList<FinalizationOperation> movementOperations,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology)
        {
            for (var i = 0; i < movementOperations.Count; i++)
            {
                var operation = movementOperations[i];
                if (operation.Kind == FinalizationOperationKind.SetTopology &&
                    operation.Topology.Equals(destinationTopology) &&
                    operation.Metadata.RotationKind != CubeRotationKind.None)
                {
                    return operation.Metadata.RotationKind;
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

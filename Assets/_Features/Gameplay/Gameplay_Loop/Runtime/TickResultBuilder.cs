using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
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
            CleanupPhaseResult cleanupPhaseResult)
            : this(
                preMovementSnapshot,
                postMovementSnapshot,
                postAttackSnapshot,
                finalAuthoritativeSnapshot,
                new PreMovementStatePhaseResult(new List<string>(), new List<PlayerActionTransition>()),
                movementPhaseResult,
                attackPhaseResult,
                cleanupPhaseResult)
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
            CleanupPhaseResult cleanupPhaseResult)
        {
            PreMovementSnapshot = preMovementSnapshot ?? throw new ArgumentNullException(nameof(preMovementSnapshot));
            PostMovementSnapshot = postMovementSnapshot ?? throw new ArgumentNullException(nameof(postMovementSnapshot));
            PostAttackSnapshot = postAttackSnapshot ?? throw new ArgumentNullException(nameof(postAttackSnapshot));
            FinalAuthoritativeSnapshot = finalAuthoritativeSnapshot ?? throw new ArgumentNullException(nameof(finalAuthoritativeSnapshot));
            PreMovementStatePhaseResult = preMovementStatePhaseResult ?? throw new ArgumentNullException(nameof(preMovementStatePhaseResult));
            MovementPhaseResult = movementPhaseResult ?? throw new ArgumentNullException(nameof(movementPhaseResult));
            AttackPhaseResult = attackPhaseResult ?? throw new ArgumentNullException(nameof(attackPhaseResult));
            CleanupPhaseResult = cleanupPhaseResult ?? throw new ArgumentNullException(nameof(cleanupPhaseResult));
        }

        public WorldSnapshot PreMovementSnapshot { get; }

        public WorldSnapshot PostMovementSnapshot { get; }

        public WorldSnapshot PostAttackSnapshot { get; }

        public WorldSnapshot FinalAuthoritativeSnapshot { get; }

        public PreMovementStatePhaseResult PreMovementStatePhaseResult { get; }

        public MovementPhaseResult MovementPhaseResult { get; }

        public AttackPhaseResult AttackPhaseResult { get; }

        public CleanupPhaseResult CleanupPhaseResult { get; }
    }

    internal sealed class TickPresentationDataBuilder
    {
        public TickPresentationData Build(in TickPresentationBuildContext context)
        {
            var entityMotions = new List<TickEntityMotion>();
            var playerActionSignals = new List<TickPlayerActionPresentationSignal>();
            var visibilityChanges = new List<TickVisibilityChange>();
            var transitionVisibilityChanges = new List<TickTransitionVisibilityChange>();

            BuildMovementPresentation(context, entityMotions, visibilityChanges);
            BuildAttackPresentation(context, visibilityChanges);
            BuildCleanupPresentation(context, visibilityChanges);
            BuildPlayerPresentation(context, playerActionSignals);

            var topologyMotion = BuildTopologyMotion(context);
            BuildTransitionVisibilityPresentation(context, visibilityChanges, transitionVisibilityChanges);

            return entityMotions.Count == 0 &&
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
                    playerActionSignals);
        }

        private static void BuildMovementPresentation(
            in TickPresentationBuildContext context,
            List<TickEntityMotion> entityMotions,
            List<TickVisibilityChange> visibilityChanges)
        {
            var selectedGroups = context.MovementPhaseResult.SelectedGroups;

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];
                AppendEntityMotions(context, group, entityMotions);
                AppendDetachVisibilityChanges(context.PreMovementSnapshot, group.BoardPresenceChanges, visibilityChanges);
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

        private static void BuildCleanupPresentation(
            in TickPresentationBuildContext context,
            List<TickVisibilityChange> visibilityChanges)
        {
            var removedEntityIds = context.CleanupPhaseResult.RemovedEntityIds;

            for (var i = 0; i < removedEntityIds.Count; i++)
            {
                var entityId = removedEntityIds[i];
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
                }

                playerActionSignals.Add(
                    new TickPlayerActionPresentationSignal(
                        entityId,
                        activeActionKind,
                        activeActionSequence,
                        startedThisTick,
                        completedThisTick,
                        canceledThisTick));
            }
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
                visibilityChanges);
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
            List<TickVisibilityChange> visibilityChanges)
        {
            for (var i = 0; i < boardPresenceChanges.Count; i++)
            {
                var boardPresenceChange = boardPresenceChanges[i];
                if (boardPresenceChange.BoardPresence != EntityBoardPresence.Detached ||
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
            IReadOnlyList<TickVisibilityChange> visibilityChanges)
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

            return excludedEntityIds;
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

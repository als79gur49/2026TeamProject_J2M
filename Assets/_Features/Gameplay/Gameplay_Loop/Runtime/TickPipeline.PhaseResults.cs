using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Sorting;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class EnemyAiPhaseResult
    {
        public EnemyAiPhaseResult(
            List<string> beforeMovementTransitions,
            List<string> beforeAttackTransitions,
            List<string> afterAttackTransitions)
        {
            BeforeMovementTransitions = beforeMovementTransitions ?? throw new ArgumentNullException(nameof(beforeMovementTransitions));
            BeforeAttackTransitions = beforeAttackTransitions ?? throw new ArgumentNullException(nameof(beforeAttackTransitions));
            AfterAttackTransitions = afterAttackTransitions ?? throw new ArgumentNullException(nameof(afterAttackTransitions));
        }

        public List<string> BeforeMovementTransitions { get; }

        public List<string> BeforeAttackTransitions { get; }

        public List<string> AfterAttackTransitions { get; }
    }

    internal sealed class EnemyActionPhaseResult
    {
        public EnemyActionPhaseResult(
            List<EnemyActionTransition> beforeAttackCollectionTransitions,
            List<EnemyActionTransition> afterAttackTransitions)
        {
            BeforeAttackCollectionTransitions = beforeAttackCollectionTransitions ?? throw new ArgumentNullException(nameof(beforeAttackCollectionTransitions));
            AfterAttackTransitions = afterAttackTransitions ?? throw new ArgumentNullException(nameof(afterAttackTransitions));
        }

        public List<EnemyActionTransition> BeforeAttackCollectionTransitions { get; }

        public List<EnemyActionTransition> AfterAttackTransitions { get; }
    }

    internal sealed class PreMovementStatePhaseResult
    {
        public PreMovementStatePhaseResult(List<string> updates)
            : this(
                updates,
                new List<PlayerActionTransition>(),
                new List<string>(),
                new List<EnemyUtilityTriggerIntent>(),
                new List<string>())
        {
        }

        public PreMovementStatePhaseResult(
            List<string> updates,
            List<PlayerActionTransition> playerActionTransitions,
            List<string> rejectedReasons = null,
            List<EnemyUtilityTriggerIntent> utilityTriggerIntents = null,
            List<string> eventLogEntries = null)
        {
            Updates = updates ?? throw new ArgumentNullException(nameof(updates));
            PlayerActionTransitions = playerActionTransitions ?? throw new ArgumentNullException(nameof(playerActionTransitions));
            RejectedReasons = rejectedReasons ?? new List<string>();
            UtilityTriggerIntents = utilityTriggerIntents ?? new List<EnemyUtilityTriggerIntent>();
            EventLogEntries = eventLogEntries ?? new List<string>();
        }

        public List<string> Updates { get; }

        public List<PlayerActionTransition> PlayerActionTransitions { get; }

        public List<string> RejectedReasons { get; }

        public List<EnemyUtilityTriggerIntent> UtilityTriggerIntents { get; }

        public List<string> EventLogEntries { get; }
    }

    internal readonly struct PlayerActionAttemptResolution
    {
        public PlayerActionAttemptResolution(
            int entityId,
            PlayerActionKind actionKind,
            Direction direction,
            PlayerActionAttemptFeedbackKind feedbackKind,
            bool consumesMovement,
            bool emitsFakePresentation,
            int targetEntityId = 0,
            bool hasTarget = false,
            bool emitsVisualFeedback = true)
        {
            EntityId = entityId;
            ActionKind = actionKind;
            Direction = direction;
            FeedbackKind = feedbackKind;
            ConsumesMovement = consumesMovement;
            EmitsFakePresentation = emitsFakePresentation;
            TargetEntityId = targetEntityId;
            HasTarget = hasTarget && targetEntityId > 0;
            EmitsVisualFeedback = emitsVisualFeedback;
        }

        public int EntityId { get; }

        public bool HasAttempt => ActionKind == PlayerActionKind.Push || ActionKind == PlayerActionKind.Flip;

        public PlayerActionKind ActionKind { get; }

        public Direction Direction { get; }

        public PlayerActionAttemptFeedbackKind FeedbackKind { get; }

        public int TargetEntityId { get; }

        public bool HasTarget { get; }

        public bool ConsumesMovement { get; }

        public bool EmitsFakePresentation { get; }

        public bool EmitsVisualFeedback { get; }
    }

    internal sealed class PlanPhaseResult
    {
        public PlanPhaseResult(
            List<RawMovementIntent> rawIntents,
            List<MoveIntent> sortedIntents,
            List<ActionGroup> expandedCandidates,
            Dictionary<int, MovementActionPlanPayload> movementActionPlanPayloads,
            List<int> orderedMovementActionPlanIds,
            List<string> rejectedReasons,
            List<Contest> spaceContests,
            List<Contest> jumpLandingSpaceContests,
            List<JumpLandingPlan> jumpLandingPlans,
            Dictionary<int, JumpLandingActionPlanPayload> jumpLandingActionPlanPayloads,
            List<int> orderedJumpLandingActionPlanIds,
            List<string> jumpLandingEvents,
            List<BarricadeBlockFact> barricadeBlockFacts,
            List<BoxSlideStopResult> boxSlideStops,
            List<TickPlayerTopologyTransitionBlockedSignal> playerTopologyTransitionBlockedSignals,
            List<string> movementDebugEvents,
            int nextContestId,
            EnemyAiPhaseResult enemyAiPhaseResult,
            PreMovementStatePhaseResult preMovementStatePhaseResult,
            WorldSnapshot postEnemyAiSnapshot,
            WorldSnapshot planSnapshot,
            WorldSnapshot topologyActivationPreviousSnapshot,
            FinalizationBatch planFinalizationBatch,
            IReadOnlyList<GravityFieldPresentationEvent> gravityFieldPresentationEvents = null,
            IReadOnlyList<GravityFieldLockedTargetFact> gravityFieldLockedTargetFacts = null,
            IReadOnlyList<PlayerActionAttemptResolution> playerActionAttemptResolutions = null,
            IReadOnlyList<EnemyGravityFieldAuraLockedTargetFact> enemyGravityFieldAuraLockedTargetFacts = null)
        {
            RawIntents = rawIntents ?? throw new ArgumentNullException(nameof(rawIntents));
            SortedIntents = sortedIntents ?? throw new ArgumentNullException(nameof(sortedIntents));
            ExpandedCandidates = expandedCandidates ?? throw new ArgumentNullException(nameof(expandedCandidates));
            MovementActionPlanPayloads = movementActionPlanPayloads ?? throw new ArgumentNullException(nameof(movementActionPlanPayloads));
            OrderedMovementActionPlanIds = orderedMovementActionPlanIds ?? throw new ArgumentNullException(nameof(orderedMovementActionPlanIds));
            RejectedReasons = rejectedReasons ?? throw new ArgumentNullException(nameof(rejectedReasons));
            SpaceContests = spaceContests ?? throw new ArgumentNullException(nameof(spaceContests));
            JumpLandingSpaceContests = jumpLandingSpaceContests ?? throw new ArgumentNullException(nameof(jumpLandingSpaceContests));
            JumpLandingPlans = jumpLandingPlans ?? throw new ArgumentNullException(nameof(jumpLandingPlans));
            JumpLandingActionPlanPayloads = jumpLandingActionPlanPayloads ?? throw new ArgumentNullException(nameof(jumpLandingActionPlanPayloads));
            OrderedJumpLandingActionPlanIds = orderedJumpLandingActionPlanIds ?? throw new ArgumentNullException(nameof(orderedJumpLandingActionPlanIds));
            JumpLandingEvents = jumpLandingEvents ?? throw new ArgumentNullException(nameof(jumpLandingEvents));
            BarricadeBlockFacts = barricadeBlockFacts ?? throw new ArgumentNullException(nameof(barricadeBlockFacts));
            BoxSlideStops = boxSlideStops ?? throw new ArgumentNullException(nameof(boxSlideStops));
            PlayerTopologyTransitionBlockedSignals = playerTopologyTransitionBlockedSignals ??
                throw new ArgumentNullException(nameof(playerTopologyTransitionBlockedSignals));
            MovementDebugEvents = movementDebugEvents ?? throw new ArgumentNullException(nameof(movementDebugEvents));
            NextContestId = nextContestId;
            EnemyAiPhaseResult = enemyAiPhaseResult ?? throw new ArgumentNullException(nameof(enemyAiPhaseResult));
            PreMovementStatePhaseResult = preMovementStatePhaseResult ?? throw new ArgumentNullException(nameof(preMovementStatePhaseResult));
            PostEnemyAiSnapshot = postEnemyAiSnapshot ?? throw new ArgumentNullException(nameof(postEnemyAiSnapshot));
            PlanSnapshot = planSnapshot ?? throw new ArgumentNullException(nameof(planSnapshot));
            TopologyActivationPreviousSnapshot = topologyActivationPreviousSnapshot ?? planSnapshot;
            PlanFinalizationBatch = planFinalizationBatch ?? throw new ArgumentNullException(nameof(planFinalizationBatch));
            GravityFieldPresentationEvents = gravityFieldPresentationEvents ?? Array.Empty<GravityFieldPresentationEvent>();
            GravityFieldLockedTargetFacts = gravityFieldLockedTargetFacts ?? Array.Empty<GravityFieldLockedTargetFact>();
            PlayerActionAttemptResolutions = playerActionAttemptResolutions ?? Array.Empty<PlayerActionAttemptResolution>();
            EnemyGravityFieldAuraLockedTargetFacts = enemyGravityFieldAuraLockedTargetFacts ??
                                                     Array.Empty<EnemyGravityFieldAuraLockedTargetFact>();
        }

        public List<RawMovementIntent> RawIntents { get; }

        public List<MoveIntent> SortedIntents { get; }

        public List<ActionGroup> ExpandedCandidates { get; }

        public Dictionary<int, MovementActionPlanPayload> MovementActionPlanPayloads { get; }

        public List<int> OrderedMovementActionPlanIds { get; }

        public List<string> RejectedReasons { get; }

        public List<Contest> SpaceContests { get; }

        public List<Contest> JumpLandingSpaceContests { get; }

        public List<JumpLandingPlan> JumpLandingPlans { get; }

        public Dictionary<int, JumpLandingActionPlanPayload> JumpLandingActionPlanPayloads { get; }

        public List<int> OrderedJumpLandingActionPlanIds { get; }

        public List<string> JumpLandingEvents { get; }

        public List<BarricadeBlockFact> BarricadeBlockFacts { get; }

        public List<BoxSlideStopResult> BoxSlideStops { get; }

        public List<TickPlayerTopologyTransitionBlockedSignal> PlayerTopologyTransitionBlockedSignals { get; }

        public List<string> MovementDebugEvents { get; }

        public int NextContestId { get; }

        public EnemyAiPhaseResult EnemyAiPhaseResult { get; }

        public PreMovementStatePhaseResult PreMovementStatePhaseResult { get; }

        public WorldSnapshot PostEnemyAiSnapshot { get; }

        public WorldSnapshot PlanSnapshot { get; }

        public WorldSnapshot TopologyActivationPreviousSnapshot { get; }

        public FinalizationBatch PlanFinalizationBatch { get; }

        public IReadOnlyList<GravityFieldPresentationEvent> GravityFieldPresentationEvents { get; }

        public IReadOnlyList<GravityFieldLockedTargetFact> GravityFieldLockedTargetFacts { get; }

        public IReadOnlyList<PlayerActionAttemptResolution> PlayerActionAttemptResolutions { get; }

        public IReadOnlyList<EnemyGravityFieldAuraLockedTargetFact> EnemyGravityFieldAuraLockedTargetFacts { get; }
    }

    internal sealed class ResolvePhaseResult
    {
        public ResolvePhaseResult(
            MovementPhaseResult movementPhaseResult,
            AttackPhaseResult attackPhaseResult,
            EnemyActionPhaseResult enemyActionPhaseResult,
            FinalizationBatch finalizationBatch,
            WorldSnapshot postMovementSnapshot,
            WorldSnapshot postAttackSnapshot,
            List<Contest> contests,
            List<ResolutionRecord> resolutionRecords,
            IReadOnlyList<TilePresentationEvent> tilePresentationEvents = null,
            IReadOnlyList<GravityFieldPresentationEvent> gravityFieldPresentationEvents = null,
            IReadOnlyList<GravityFieldLockedTargetFact> gravityFieldLockedTargetFacts = null,
            IReadOnlyList<EnemyGravityFieldAuraLockedTargetFact> enemyGravityFieldAuraLockedTargetFacts = null)
        {
            MovementPhaseResult = movementPhaseResult ?? throw new ArgumentNullException(nameof(movementPhaseResult));
            AttackPhaseResult = attackPhaseResult ?? throw new ArgumentNullException(nameof(attackPhaseResult));
            EnemyActionPhaseResult = enemyActionPhaseResult ?? throw new ArgumentNullException(nameof(enemyActionPhaseResult));
            FinalizationBatch = finalizationBatch ?? throw new ArgumentNullException(nameof(finalizationBatch));
            PostMovementSnapshot = postMovementSnapshot ?? throw new ArgumentNullException(nameof(postMovementSnapshot));
            PostAttackSnapshot = postAttackSnapshot ?? throw new ArgumentNullException(nameof(postAttackSnapshot));
            Contests = contests ?? throw new ArgumentNullException(nameof(contests));
            ResolutionRecords = resolutionRecords ?? throw new ArgumentNullException(nameof(resolutionRecords));
            TilePresentationEvents = tilePresentationEvents ?? Array.Empty<TilePresentationEvent>();
            GravityFieldPresentationEvents = gravityFieldPresentationEvents ?? Array.Empty<GravityFieldPresentationEvent>();
            GravityFieldLockedTargetFacts = gravityFieldLockedTargetFacts ?? Array.Empty<GravityFieldLockedTargetFact>();
            EnemyGravityFieldAuraLockedTargetFacts = enemyGravityFieldAuraLockedTargetFacts ??
                                                     Array.Empty<EnemyGravityFieldAuraLockedTargetFact>();
        }

        public MovementPhaseResult MovementPhaseResult { get; }

        public AttackPhaseResult AttackPhaseResult { get; }

        public EnemyActionPhaseResult EnemyActionPhaseResult { get; }

        public FinalizationBatch FinalizationBatch { get; }

        public WorldSnapshot PostMovementSnapshot { get; }

        public WorldSnapshot PostAttackSnapshot { get; }

        public List<Contest> Contests { get; }

        public List<ResolutionRecord> ResolutionRecords { get; }

        public IReadOnlyList<TilePresentationEvent> TilePresentationEvents { get; }

        public IReadOnlyList<GravityFieldPresentationEvent> GravityFieldPresentationEvents { get; }

        public IReadOnlyList<GravityFieldLockedTargetFact> GravityFieldLockedTargetFacts { get; }

        public IReadOnlyList<EnemyGravityFieldAuraLockedTargetFact> EnemyGravityFieldAuraLockedTargetFacts { get; }
    }

    internal sealed class AttackPlanBuildResult
    {
        public AttackPlanBuildResult(
            List<RawAttackIntent> rawAttackIntents,
            List<ActionGroup> expandedCandidates,
            Dictionary<int, AttackActionPlanPayload> actionPlanPayloads,
            List<int> orderedActionPlanIds,
            List<string> rejectedReasons,
            List<PendingCellImpactResolutionRecord> pendingCellImpactResolutions = null)
        {
            RawAttackIntents = rawAttackIntents ?? throw new ArgumentNullException(nameof(rawAttackIntents));
            ExpandedCandidates = expandedCandidates ?? throw new ArgumentNullException(nameof(expandedCandidates));
            ActionPlanPayloads = actionPlanPayloads ?? throw new ArgumentNullException(nameof(actionPlanPayloads));
            OrderedActionPlanIds = orderedActionPlanIds ?? throw new ArgumentNullException(nameof(orderedActionPlanIds));
            RejectedReasons = rejectedReasons ?? throw new ArgumentNullException(nameof(rejectedReasons));
            PendingCellImpactResolutions = pendingCellImpactResolutions ?? new List<PendingCellImpactResolutionRecord>();
        }

        public List<RawAttackIntent> RawAttackIntents { get; }

        public List<ActionGroup> ExpandedCandidates { get; }

        public Dictionary<int, AttackActionPlanPayload> ActionPlanPayloads { get; }

        public List<int> OrderedActionPlanIds { get; }

        public List<string> RejectedReasons { get; }

        public List<PendingCellImpactResolutionRecord> PendingCellImpactResolutions { get; }
    }

    internal sealed class EnemyUtilityResolveResult
    {
        public EnemyUtilityResolveResult(
            FinalizationBatch batch,
            List<string> eventLogEntries,
            IReadOnlyList<EnemyGravityFieldAuraLockedTargetFact> gravityFieldAuraLockedTargetFacts = null)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            EventLogEntries = eventLogEntries ?? throw new ArgumentNullException(nameof(eventLogEntries));
            GravityFieldAuraLockedTargetFacts = gravityFieldAuraLockedTargetFacts ??
                                                Array.Empty<EnemyGravityFieldAuraLockedTargetFact>();
        }

        public FinalizationBatch Batch { get; }

        public List<string> EventLogEntries { get; }

        public IReadOnlyList<EnemyGravityFieldAuraLockedTargetFact> GravityFieldAuraLockedTargetFacts { get; }
    }

    internal readonly struct EnemyGravityFieldAuraLockedTargetFact
    {
        public EnemyGravityFieldAuraLockedTargetFact(
            int sourceEntityId,
            int sourceEffectIndex,
            int activationSequence,
            int targetEntityId,
            SurfaceCell originCell,
            SurfaceCell targetCell)
        {
            SourceEntityId = sourceEntityId;
            SourceEffectIndex = sourceEffectIndex;
            ActivationSequence = activationSequence;
            TargetEntityId = targetEntityId;
            OriginCell = originCell;
            TargetCell = targetCell;
        }

        public int SourceEntityId { get; }

        public int SourceEffectIndex { get; }

        public int ActivationSequence { get; }

        public int TargetEntityId { get; }

        public SurfaceCell OriginCell { get; }

        public SurfaceCell TargetCell { get; }
    }

    internal static class EnemyUtilityResolver
    {
        private readonly struct SourceEffectKey : IEquatable<SourceEffectKey>
        {
            public SourceEffectKey(int sourceEntityId, int effectIndex)
            {
                SourceEntityId = sourceEntityId;
                EffectIndex = effectIndex;
            }

            public int SourceEntityId { get; }

            public int EffectIndex { get; }

            public bool Equals(SourceEffectKey other)
            {
                return SourceEntityId == other.SourceEntityId &&
                       EffectIndex == other.EffectIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is SourceEffectKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SourceEntityId * 397) ^ EffectIndex;
                }
            }
        }

        private enum SummonSkipReason
        {
            SourceInvalid = 0,
            MaxAliveReached = 1,
            NoCandidateCell = 2,
        }

        private enum BoxInteractionLockSkipReason
        {
            SourceInvalid = 0,
            NoTargetBoxes = 1,
        }

        public static EnemyUtilityResolveResult ResolvePreMovementProjectedEffects(
            WorldSnapshot projectedSnapshot,
            IReadOnlyList<EnemyUtilityTriggerIntent> triggerIntents,
            int tickIndex)
        {
            if (projectedSnapshot == null)
            {
                throw new ArgumentNullException(nameof(projectedSnapshot));
            }

            if (triggerIntents == null)
            {
                throw new ArgumentNullException(nameof(triggerIntents));
            }

            var batch = new FinalizationBatch();
            var eventLogEntries = new List<string>();
            var gravityFieldAuraLockedTargetFacts = new List<EnemyGravityFieldAuraLockedTargetFact>();
            var plannedStatesByBoxEntityId = new Dictionary<int, BoxInteractionLockState>();
            for (var intentIndex = 0; intentIndex < triggerIntents.Count; intentIndex++)
            {
                var triggerIntent = triggerIntents[intentIndex];
                if (triggerIntent.EffectKind == EnemyUtilityEffectKind.GravityFieldAura)
                {
                    ResolveGravityFieldAura(
                        projectedSnapshot,
                        triggerIntent,
                        tickIndex,
                        plannedStatesByBoxEntityId,
                        gravityFieldAuraLockedTargetFacts,
                        eventLogEntries);
                }
            }

            ResolveActiveEnemyGravityFieldAuraFields(
                projectedSnapshot,
                batch,
                tickIndex,
                plannedStatesByBoxEntityId,
                gravityFieldAuraLockedTargetFacts,
                eventLogEntries);

            if (plannedStatesByBoxEntityId.Count == 0)
            {
                return new EnemyUtilityResolveResult(batch, eventLogEntries, gravityFieldAuraLockedTargetFacts);
            }

            var orderedBoxEntityIds = new List<int>(plannedStatesByBoxEntityId.Keys);
            orderedBoxEntityIds.Sort();
            for (var boxIndex = 0; boxIndex < orderedBoxEntityIds.Count; boxIndex++)
            {
                var boxEntityId = orderedBoxEntityIds[boxIndex];
                var mergedState = plannedStatesByBoxEntityId[boxEntityId];
                if (projectedSnapshot.TryGetActiveBoxInteractionLockState(boxEntityId, tickIndex, out var existingState) &&
                    BoxInteractionLockMerge.AreEqual(existingState, mergedState))
                {
                    continue;
                }

                batch.SetBoxInteractionLockState(
                    boxEntityId,
                    mergedState,
                    new FinalizationOperationMetadata(
                        TickPhase.Plan,
                        ResolvedActionSemanticKind.None,
                        mergedState.SourceEntityId,
                        actionPlanId: 0));
                if (projectedSnapshot.TryGetEntity(boxEntityId, out var box))
                {
                    eventLogEntries.Add(
                        $"BoxInteractionLockApplied|Source={mergedState.SourceEntityId}|Effect={mergedState.SourceEffectIndex}|Reason={mergedState.SourceReason}|Box={boxEntityId}|Cell={box.position}|Expires={mergedState.ExpiresTickExclusive}|BlocksPush={(mergedState.BlocksPush ? 1 : 0)}|BlocksFlip={(mergedState.BlocksFlip ? 1 : 0)}|BlocksDestroy={(mergedState.BlocksDestroy ? 1 : 0)}");
                }
            }

            return new EnemyUtilityResolveResult(batch, eventLogEntries, gravityFieldAuraLockedTargetFacts);
        }

        public static EnemyUtilityResolveResult ResolvePostAttackEffects(
            WorldSnapshot postAttackSnapshot,
            IReadOnlyList<EnemyUtilityTriggerIntent> triggerIntents,
            int tickIndex,
            EntityIdAllocator entityIdAllocator,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            if (postAttackSnapshot == null)
            {
                throw new ArgumentNullException(nameof(postAttackSnapshot));
            }

            if (triggerIntents == null)
            {
                throw new ArgumentNullException(nameof(triggerIntents));
            }

            if (entityIdAllocator == null)
            {
                throw new ArgumentNullException(nameof(entityIdAllocator));
            }

            var batch = new FinalizationBatch();
            var eventLogEntries = new List<string>();
            if (triggerIntents.Count == 0)
            {
                return new EnemyUtilityResolveResult(batch, eventLogEntries);
            }

            var reservedSpawnCells = new HashSet<SurfaceCell>();
            var summonedEntries = new List<SummonedEntitySnapshotEntry>();
            postAttackSnapshot.EnumerateSummonedEntityStatesOrdered(summonedEntries);
            var plannedChildrenBySource = new Dictionary<SourceEffectKey, int>();

            for (var intentIndex = 0; intentIndex < triggerIntents.Count; intentIndex++)
            {
                var triggerIntent = triggerIntents[intentIndex];
                if (triggerIntent.EffectKind != EnemyUtilityEffectKind.SummonMinion)
                {
                    continue;
                }

                ResolveSummonMinion(
                    postAttackSnapshot,
                    triggerIntent,
                    tickIndex,
                    entityIdAllocator,
                    spawnDefaultsByArchetypeId,
                    summonedEntries,
                    plannedChildrenBySource,
                    reservedSpawnCells,
                    tileFeatureDefinitions,
                    batch,
                    eventLogEntries);
            }

            return new EnemyUtilityResolveResult(batch, eventLogEntries);
        }

        private static void ResolveGravityFieldAura(
            WorldSnapshot snapshot,
            in EnemyUtilityTriggerIntent triggerIntent,
            int tickIndex,
            IDictionary<int, BoxInteractionLockState> plannedStatesByBoxEntityId,
            List<EnemyGravityFieldAuraLockedTargetFact> lockedTargetFacts,
            List<string> eventLogEntries)
        {
            if (!TryGetValidSource(snapshot, triggerIntent.SourceEntityId, out var source))
            {
                AppendLockSkipEvent(eventLogEntries, triggerIntent, BoxInteractionLockSkipReason.SourceInvalid, tickIndex);
                return;
            }

            var targetEntityIds = new HashSet<int>();
            var targetCellsByEntityId = new Dictionary<int, SurfaceCell>();
            var targetCellOffsets = BuildSquareOffsets(triggerIntent.EffectRuntime.GravityFieldAura.Radius);
            for (var offsetIndex = 0; offsetIndex < targetCellOffsets.Count; offsetIndex++)
            {
                var candidateCell = triggerIntent.OriginCell + targetCellOffsets[offsetIndex];
                if (!snapshot.IsInsideBoard(candidateCell) ||
                    !TryResolveGravityFieldAuraTargetBox(snapshot, candidateCell, out var box))
                {
                    continue;
                }

                if (targetEntityIds.Add(box.entityId))
                {
                    targetCellsByEntityId.Add(box.entityId, box.position);
                }
            }

            if (targetEntityIds.Count == 0)
            {
                AppendLockSkipEvent(eventLogEntries, triggerIntent, BoxInteractionLockSkipReason.NoTargetBoxes, tickIndex);
                return;
            }

            var aura = triggerIntent.EffectRuntime.GravityFieldAura;
            var newState = new BoxInteractionLockState(
                triggerIntent.SourceEntityId,
                triggerIntent.EffectIndex,
                tickIndex + 1,
                aura.BlocksPush,
                aura.BlocksFlip,
                aura.BlocksDestroy,
                BoxInteractionLockSourceReason.EnemyGravityFieldAura);
            var orderedTargetEntityIds = new List<int>(targetEntityIds);
            orderedTargetEntityIds.Sort();

            for (var targetIndex = 0; targetIndex < orderedTargetEntityIds.Count; targetIndex++)
            {
                var boxEntityId = orderedTargetEntityIds[targetIndex];
                var hasExistingPlannedState = plannedStatesByBoxEntityId.TryGetValue(boxEntityId, out var plannedState);
                var hasExistingSnapshotState = snapshot.TryGetActiveBoxInteractionLockState(boxEntityId, tickIndex, out var existingState);
                var mergedState = !hasExistingPlannedState && !hasExistingSnapshotState
                    ? newState
                    : MergeBoxInteractionLockStates(
                        hasExistingPlannedState ? plannedState : existingState,
                        newState);
                plannedStatesByBoxEntityId[boxEntityId] = mergedState;
                lockedTargetFacts.Add(new EnemyGravityFieldAuraLockedTargetFact(
                    triggerIntent.SourceEntityId,
                    triggerIntent.EffectIndex,
                    activationSequence: 0,
                    boxEntityId,
                    triggerIntent.OriginCell,
                    targetCellsByEntityId.TryGetValue(boxEntityId, out var targetCell) ? targetCell : default));
            }
        }

        private static void ResolveActiveEnemyGravityFieldAuraFields(
            WorldSnapshot snapshot,
            FinalizationBatch batch,
            int tickIndex,
            IDictionary<int, BoxInteractionLockState> plannedStatesByBoxEntityId,
            List<EnemyGravityFieldAuraLockedTargetFact> lockedTargetFacts,
            List<string> eventLogEntries)
        {
            var fieldEntries = new List<EnemyGravityFieldAuraFieldSnapshotEntry>();
            snapshot.EnumerateEnemyGravityFieldAuraFieldStatesOrdered(fieldEntries);
            for (var fieldIndex = 0; fieldIndex < fieldEntries.Count; fieldIndex++)
            {
                var fieldEntry = fieldEntries[fieldIndex];
                if (!fieldEntry.State.IsActive(tickIndex))
                {
                    continue;
                }

                ResolveGravityFieldAuraField(
                    snapshot,
                    fieldEntry.FieldId,
                    fieldEntry.State,
                    tickIndex,
                    plannedStatesByBoxEntityId,
                    lockedTargetFacts,
                    eventLogEntries);
            }
        }

        private static void ResolveGravityFieldAuraField(
            WorldSnapshot snapshot,
            int fieldId,
            in EnemyGravityFieldAuraFieldState fieldState,
            int tickIndex,
            IDictionary<int, BoxInteractionLockState> plannedStatesByBoxEntityId,
            List<EnemyGravityFieldAuraLockedTargetFact> lockedTargetFacts,
            List<string> eventLogEntries)
        {
            var targetEntityIds = new HashSet<int>();
            var targetCellsByEntityId = new Dictionary<int, SurfaceCell>();
            var targetCellOffsets = BuildSquareOffsets(fieldState.Radius);
            for (var offsetIndex = 0; offsetIndex < targetCellOffsets.Count; offsetIndex++)
            {
                var candidateCell = fieldState.OriginCell + targetCellOffsets[offsetIndex];
                if (!snapshot.IsInsideBoard(candidateCell) ||
                    !TryResolveGravityFieldAuraTargetBox(snapshot, candidateCell, out var box))
                {
                    continue;
                }

                if (targetEntityIds.Add(box.entityId))
                {
                    targetCellsByEntityId.Add(box.entityId, box.position);
                }
            }

            if (targetEntityIds.Count == 0)
            {
                eventLogEntries.Add(
                    $"EnemyGravityFieldAuraFieldNoTargets|Field={fieldId}|Source={fieldState.SourceEntityId}|Effect={fieldState.SourceEffectIndex}|Tick={tickIndex}|Origin={fieldState.OriginCell}");
                return;
            }

            var newState = new BoxInteractionLockState(
                fieldState.SourceEntityId,
                fieldState.SourceEffectIndex,
                tickIndex + 1,
                fieldState.BlocksPush,
                fieldState.BlocksFlip,
                fieldState.BlocksDestroy,
                BoxInteractionLockSourceReason.EnemyGravityFieldAura);
            var orderedTargetEntityIds = new List<int>(targetEntityIds);
            orderedTargetEntityIds.Sort();

            for (var targetIndex = 0; targetIndex < orderedTargetEntityIds.Count; targetIndex++)
            {
                var boxEntityId = orderedTargetEntityIds[targetIndex];
                var hasExistingPlannedState = plannedStatesByBoxEntityId.TryGetValue(boxEntityId, out var plannedState);
                var hasExistingSnapshotState = snapshot.TryGetActiveBoxInteractionLockState(boxEntityId, tickIndex, out var existingState);
                var mergedState = !hasExistingPlannedState && !hasExistingSnapshotState
                    ? newState
                    : MergeBoxInteractionLockStates(
                        hasExistingPlannedState ? plannedState : existingState,
                        newState);
                plannedStatesByBoxEntityId[boxEntityId] = mergedState;
                lockedTargetFacts.Add(new EnemyGravityFieldAuraLockedTargetFact(
                    fieldState.SourceEntityId,
                    fieldState.SourceEffectIndex,
                    fieldState.ActivationSequence,
                    boxEntityId,
                    fieldState.OriginCell,
                    targetCellsByEntityId.TryGetValue(boxEntityId, out var targetCell) ? targetCell : default));
            }
        }

        private static void ResolveSummonMinion(
            WorldSnapshot snapshot,
            in EnemyUtilityTriggerIntent triggerIntent,
            int tickIndex,
            EntityIdAllocator entityIdAllocator,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId,
            IReadOnlyList<SummonedEntitySnapshotEntry> summonedEntries,
            IDictionary<SourceEffectKey, int> plannedChildrenBySource,
            ISet<SurfaceCell> reservedSpawnCells,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            FinalizationBatch batch,
            List<string> eventLogEntries)
        {
            if (!TryGetValidSource(snapshot, triggerIntent.SourceEntityId, out var source))
            {
                AppendSkipEvent(eventLogEntries, triggerIntent, tickIndex, spawnIndex: 0, SummonSkipReason.SourceInvalid);
                return;
            }

            var summonRuntime = triggerIntent.EffectRuntime.Summon;
            var spawnDefaults = EntitySpawnMaterializer.ResolveSummonSpawnDefaults(
                summonRuntime.SummonedArchetypeId,
                spawnDefaultsByArchetypeId);
            var sourceKey = new SourceEffectKey(triggerIntent.SourceEntityId, triggerIntent.EffectIndex);
            for (var spawnIndex = 0; spawnIndex < summonRuntime.SpawnCountPerTrigger; spawnIndex++)
            {
                var plannedChildren = plannedChildrenBySource.TryGetValue(sourceKey, out var currentPlannedChildren)
                    ? currentPlannedChildren
                    : 0;
                if (EnemyUtilitySummonPolicy.IsMaxAliveReached(
                        snapshot,
                        summonedEntries,
                        triggerIntent.SourceEntityId,
                        triggerIntent.EffectIndex,
                        summonRuntime,
                        plannedChildren))
                {
                    AppendSkipEvent(eventLogEntries, triggerIntent, tickIndex, spawnIndex, SummonSkipReason.MaxAliveReached);
                    continue;
                }

                var spawnRequest = new EntitySpawnRequest(
                    EntitySpawnRequestKind.Summon,
                    new EntitySpawnRequestSource(
                        triggerIntent.SourceEntityId,
                        triggerIntent.EffectIndex,
                        triggerIntent.TriggerTick,
                        source.position,
                        source.facing,
                        source.teamId),
                    spawnIndex,
                    tickIndex,
                    summonRuntime,
                    spawnDefaults);
                var spawnResult = EntitySpawnMaterializer.Materialize(
                        snapshot,
                        spawnRequest,
                        entityIdAllocator,
                        tileFeatureDefinitions,
                        reservedSpawnCells,
                        batch,
                        eventLogEntries);
                if (!spawnResult.Succeeded)
                {
                    continue;
                }

                plannedChildrenBySource[sourceKey] = plannedChildren + 1;
            }
        }

        private static bool TryGetValidSource(
            WorldSnapshot snapshot,
            int sourceEntityId,
            out EntityState source)
        {
            if (!snapshot.TryGetEntity(sourceEntityId, out source))
            {
                return false;
            }

            return EnemyParticipationPolicy.IsControllableParticipant(snapshot, source) &&
                   source.position.face == snapshot.Topology.BottomFace;
        }

        private static List<Vector2Int> BuildSquareOffsets(int radius)
        {
            var offsets = new List<Vector2Int>();
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    offsets.Add(new Vector2Int(dx, dy));
                }
            }

            offsets.Sort(CompareSquareOffsets);
            return offsets;
        }

        private static int CompareSquareOffsets(Vector2Int left, Vector2Int right)
        {
            var yComparison = right.y.CompareTo(left.y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return left.x.CompareTo(right.x);
        }

        private static bool TryResolveLockTargetBox(
            WorldSnapshot snapshot,
            SurfaceCell cell,
            out EntityState box)
        {
            if (!snapshot.TryGetSolidSemanticAt(cell, out var solidSemantic) ||
                solidSemantic.Kind != SolidKind.Box)
            {
                box = default;
                return false;
            }

            box = solidSemantic.Entity;
            return box.type == EntityType.Box &&
                   box.hp > 0 &&
                   !box.markedForDeath &&
                   box.boardPresence == EntityBoardPresence.Occupying;
        }

        private static bool TryResolveGravityFieldAuraTargetBox(
            WorldSnapshot snapshot,
            SurfaceCell cell,
            out EntityState box)
        {
            if (!TryResolveLockTargetBox(snapshot, cell, out box) ||
                box.state == EntityPhaseState.Sliding ||
                HasActivePhasedState(snapshot, box.entityId) ||
                !snapshot.TryGetResolvedSpatialState(box.entityId, out var spatialState))
            {
                return false;
            }

            return spatialState.Kind == SpatialState.Anchored &&
                   spatialState.ClaimsAuthoritativeOccupancy &&
                   spatialState.IsGameplayVisible;
        }

        private static bool HasActivePhasedState(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetPhasedState(entityId, out var phasedState) &&
                   phasedState.IsActive;
        }

        private static BoxInteractionLockState MergeBoxInteractionLockStates(
            in BoxInteractionLockState existingState,
            in BoxInteractionLockState newState)
        {
            return BoxInteractionLockMerge.Merge(existingState, newState);
        }

        private static bool AreBoxInteractionLockStatesEqual(
            in BoxInteractionLockState left,
            in BoxInteractionLockState right)
        {
            return BoxInteractionLockMerge.AreEqual(left, right);
        }

        private static void AppendSkipEvent(
            List<string> eventLogEntries,
            in EnemyUtilityTriggerIntent triggerIntent,
            int tickIndex,
            int spawnIndex,
            SummonSkipReason reason)
        {
            eventLogEntries.Add(
                $"SummonSkipped|Source={triggerIntent.SourceEntityId}|Effect={triggerIntent.EffectIndex}|SpawnIndex={spawnIndex}|Reason={reason}|Tick={tickIndex}");
        }

        private static void AppendLockSkipEvent(
            List<string> eventLogEntries,
            in EnemyUtilityTriggerIntent triggerIntent,
            BoxInteractionLockSkipReason reason,
            int tickIndex)
        {
            eventLogEntries.Add(
                $"BoxInteractionLockSkipped|Source={triggerIntent.SourceEntityId}|Effect={triggerIntent.EffectIndex}|Reason={reason}|Tick={tickIndex}");
        }
    }

}

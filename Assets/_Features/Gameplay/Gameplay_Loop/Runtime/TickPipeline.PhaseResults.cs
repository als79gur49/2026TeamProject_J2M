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
            bool hasTarget = false)
        {
            EntityId = entityId;
            ActionKind = actionKind;
            Direction = direction;
            FeedbackKind = feedbackKind;
            ConsumesMovement = consumesMovement;
            EmitsFakePresentation = emitsFakePresentation;
            TargetEntityId = targetEntityId;
            HasTarget = hasTarget && targetEntityId > 0;
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
            List<Contest> phaseRelocationSpaceContests,
            List<PhaseRelocationPlan> phaseRelocationPlans,
            Dictionary<int, PhaseRelocationActionPlanPayload> phaseRelocationActionPlanPayloads,
            List<int> orderedPhaseRelocationActionPlanIds,
            List<FrontFaceShieldSourcePresentationExport> frontFaceShieldSourceExports,
            List<FrontFaceShieldBlockPresentationExport> frontFaceShieldBlockExports,
            List<BarricadeBlockFact> barricadeBlockFacts,
            List<BoxSlideStopResult> boxSlideStops,
            List<string> movementDebugEvents,
            int nextContestId,
            EnemyAiPhaseResult enemyAiPhaseResult,
            PreMovementStatePhaseResult preMovementStatePhaseResult,
            WorldSnapshot postEnemyAiSnapshot,
            WorldSnapshot planSnapshot,
            FinalizationBatch planFinalizationBatch,
            IReadOnlyList<GravityFieldPresentationEvent> gravityFieldPresentationEvents = null,
            IReadOnlyList<GravityFieldLockedTargetFact> gravityFieldLockedTargetFacts = null,
            IReadOnlyList<PlayerActionAttemptResolution> playerActionAttemptResolutions = null)
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
            PhaseRelocationSpaceContests = phaseRelocationSpaceContests ?? throw new ArgumentNullException(nameof(phaseRelocationSpaceContests));
            PhaseRelocationPlans = phaseRelocationPlans ?? throw new ArgumentNullException(nameof(phaseRelocationPlans));
            PhaseRelocationActionPlanPayloads = phaseRelocationActionPlanPayloads ?? throw new ArgumentNullException(nameof(phaseRelocationActionPlanPayloads));
            OrderedPhaseRelocationActionPlanIds = orderedPhaseRelocationActionPlanIds ?? throw new ArgumentNullException(nameof(orderedPhaseRelocationActionPlanIds));
            FrontFaceShieldSourceExports = frontFaceShieldSourceExports ?? throw new ArgumentNullException(nameof(frontFaceShieldSourceExports));
            FrontFaceShieldBlockExports = frontFaceShieldBlockExports ?? throw new ArgumentNullException(nameof(frontFaceShieldBlockExports));
            BarricadeBlockFacts = barricadeBlockFacts ?? throw new ArgumentNullException(nameof(barricadeBlockFacts));
            BoxSlideStops = boxSlideStops ?? throw new ArgumentNullException(nameof(boxSlideStops));
            MovementDebugEvents = movementDebugEvents ?? throw new ArgumentNullException(nameof(movementDebugEvents));
            NextContestId = nextContestId;
            EnemyAiPhaseResult = enemyAiPhaseResult ?? throw new ArgumentNullException(nameof(enemyAiPhaseResult));
            PreMovementStatePhaseResult = preMovementStatePhaseResult ?? throw new ArgumentNullException(nameof(preMovementStatePhaseResult));
            PostEnemyAiSnapshot = postEnemyAiSnapshot ?? throw new ArgumentNullException(nameof(postEnemyAiSnapshot));
            PlanSnapshot = planSnapshot ?? throw new ArgumentNullException(nameof(planSnapshot));
            PlanFinalizationBatch = planFinalizationBatch ?? throw new ArgumentNullException(nameof(planFinalizationBatch));
            GravityFieldPresentationEvents = gravityFieldPresentationEvents ?? Array.Empty<GravityFieldPresentationEvent>();
            GravityFieldLockedTargetFacts = gravityFieldLockedTargetFacts ?? Array.Empty<GravityFieldLockedTargetFact>();
            PlayerActionAttemptResolutions = playerActionAttemptResolutions ?? Array.Empty<PlayerActionAttemptResolution>();
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

        public List<Contest> PhaseRelocationSpaceContests { get; }

        public List<PhaseRelocationPlan> PhaseRelocationPlans { get; }

        public Dictionary<int, PhaseRelocationActionPlanPayload> PhaseRelocationActionPlanPayloads { get; }

        public List<int> OrderedPhaseRelocationActionPlanIds { get; }

        public List<FrontFaceShieldSourcePresentationExport> FrontFaceShieldSourceExports { get; }

        public List<FrontFaceShieldBlockPresentationExport> FrontFaceShieldBlockExports { get; }

        public List<BarricadeBlockFact> BarricadeBlockFacts { get; }

        public List<BoxSlideStopResult> BoxSlideStops { get; }

        public List<string> MovementDebugEvents { get; }

        public int NextContestId { get; }

        public EnemyAiPhaseResult EnemyAiPhaseResult { get; }

        public PreMovementStatePhaseResult PreMovementStatePhaseResult { get; }

        public WorldSnapshot PostEnemyAiSnapshot { get; }

        public WorldSnapshot PlanSnapshot { get; }

        public FinalizationBatch PlanFinalizationBatch { get; }

        public IReadOnlyList<GravityFieldPresentationEvent> GravityFieldPresentationEvents { get; }

        public IReadOnlyList<GravityFieldLockedTargetFact> GravityFieldLockedTargetFacts { get; }

        public IReadOnlyList<PlayerActionAttemptResolution> PlayerActionAttemptResolutions { get; }
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
            IReadOnlyList<GravityFieldLockedTargetFact> gravityFieldLockedTargetFacts = null)
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
        public EnemyUtilityResolveResult(FinalizationBatch batch, List<string> eventLogEntries)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            EventLogEntries = eventLogEntries ?? throw new ArgumentNullException(nameof(eventLogEntries));
        }

        public FinalizationBatch Batch { get; }

        public List<string> EventLogEntries { get; }
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

        private enum LockNearbyBoxesSkipReason
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
            if (triggerIntents.Count == 0)
            {
                return new EnemyUtilityResolveResult(batch, eventLogEntries);
            }

            var plannedStatesByBoxEntityId = new Dictionary<int, BoxInteractionLockState>();
            for (var intentIndex = 0; intentIndex < triggerIntents.Count; intentIndex++)
            {
                var triggerIntent = triggerIntents[intentIndex];
                if (triggerIntent.EffectKind == EnemyUtilityEffectKind.LockNearbyBoxes)
                {
                    ResolveLockNearbyBoxes(
                        projectedSnapshot,
                        triggerIntent,
                        tickIndex,
                        plannedStatesByBoxEntityId,
                        eventLogEntries);
                    continue;
                }

                if (triggerIntent.EffectKind == EnemyUtilityEffectKind.GravityFieldAura)
                {
                    ResolveGravityFieldAura(
                        projectedSnapshot,
                        triggerIntent,
                        tickIndex,
                        plannedStatesByBoxEntityId,
                        eventLogEntries);
                }
            }

            if (plannedStatesByBoxEntityId.Count == 0)
            {
                return new EnemyUtilityResolveResult(batch, eventLogEntries);
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

            return new EnemyUtilityResolveResult(batch, eventLogEntries);
        }

        public static EnemyUtilityResolveResult ResolvePostAttackEffects(
            WorldSnapshot postAttackSnapshot,
            IReadOnlyList<EnemyUtilityTriggerIntent> triggerIntents,
            int tickIndex,
            EntityIdAllocator entityIdAllocator,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId)
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
                    batch,
                    eventLogEntries);
            }

            return new EnemyUtilityResolveResult(batch, eventLogEntries);
        }

        private static void ResolveLockNearbyBoxes(
            WorldSnapshot snapshot,
            in EnemyUtilityTriggerIntent triggerIntent,
            int tickIndex,
            IDictionary<int, BoxInteractionLockState> plannedStatesByBoxEntityId,
            List<string> eventLogEntries)
        {
            if (!TryGetValidSource(snapshot, triggerIntent.SourceEntityId, out var source))
            {
                AppendLockSkipEvent(eventLogEntries, triggerIntent, LockNearbyBoxesSkipReason.SourceInvalid, tickIndex);
                return;
            }

            var targetEntityIds = new HashSet<int>();
            var targetCellOffsets = BuildTargetOffsets(source.facing, triggerIntent.EffectRuntime.LockNearbyBoxes);
            for (var offsetIndex = 0; offsetIndex < targetCellOffsets.Count; offsetIndex++)
            {
                var candidateCell = source.position + targetCellOffsets[offsetIndex];
                if (!snapshot.IsInsideBoard(candidateCell) ||
                    !TryResolveLockTargetBox(snapshot, candidateCell, out var box))
                {
                    continue;
                }

                targetEntityIds.Add(box.entityId);
            }

            if (targetEntityIds.Count == 0)
            {
                AppendLockSkipEvent(eventLogEntries, triggerIntent, LockNearbyBoxesSkipReason.NoTargetBoxes, tickIndex);
                return;
            }

            var newExpiresTickExclusive = tickIndex + triggerIntent.EffectRuntime.LockNearbyBoxes.DurationTicks;
            var newState = new BoxInteractionLockState(
                triggerIntent.SourceEntityId,
                triggerIntent.EffectIndex,
                newExpiresTickExclusive,
                triggerIntent.EffectRuntime.LockNearbyBoxes.BlocksPush,
                triggerIntent.EffectRuntime.LockNearbyBoxes.BlocksFlip);
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
            }
        }

        private static void ResolveGravityFieldAura(
            WorldSnapshot snapshot,
            in EnemyUtilityTriggerIntent triggerIntent,
            int tickIndex,
            IDictionary<int, BoxInteractionLockState> plannedStatesByBoxEntityId,
            List<string> eventLogEntries)
        {
            if (!TryGetValidSource(snapshot, triggerIntent.SourceEntityId, out var source))
            {
                AppendLockSkipEvent(eventLogEntries, triggerIntent, LockNearbyBoxesSkipReason.SourceInvalid, tickIndex);
                return;
            }

            var targetEntityIds = new HashSet<int>();
            var targetCellOffsets = BuildSquareOffsets(triggerIntent.EffectRuntime.GravityFieldAura.Radius);
            for (var offsetIndex = 0; offsetIndex < targetCellOffsets.Count; offsetIndex++)
            {
                var candidateCell = triggerIntent.OriginCell + targetCellOffsets[offsetIndex];
                if (!snapshot.IsInsideBoard(candidateCell) ||
                    !TryResolveGravityFieldAuraTargetBox(snapshot, candidateCell, out var box))
                {
                    continue;
                }

                targetEntityIds.Add(box.entityId);
            }

            if (targetEntityIds.Count == 0)
            {
                AppendLockSkipEvent(eventLogEntries, triggerIntent, LockNearbyBoxesSkipReason.NoTargetBoxes, tickIndex);
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
            FinalizationBatch batch,
            List<string> eventLogEntries)
        {
            if (!TryGetValidSource(snapshot, triggerIntent.SourceEntityId, out var source))
            {
                AppendSkipEvent(eventLogEntries, triggerIntent, tickIndex, spawnIndex: 0, SummonSkipReason.SourceInvalid);
                return;
            }

            var summonRuntime = triggerIntent.EffectRuntime.Summon;
            var spawnDefaults = ResolveArchetypeSpawnDefaults(summonRuntime.SummonedArchetypeId, spawnDefaultsByArchetypeId);
            var minionHp = summonRuntime.OverrideHp
                ? summonRuntime.HpOverride
                : spawnDefaults.Hp;
            var initialAiMode = spawnDefaults.InitialAiMode;
            var enemyDefinitionBindingState = new EnemyDefinitionBindingState(summonRuntime.SummonedArchetypeId);
            var sourceKey = new SourceEffectKey(triggerIntent.SourceEntityId, triggerIntent.EffectIndex);
            for (var spawnIndex = 0; spawnIndex < summonRuntime.SpawnCountPerTrigger; spawnIndex++)
            {
                var aliveChildren = CountAliveChildren(
                    snapshot,
                    summonedEntries,
                    triggerIntent.SourceEntityId,
                    triggerIntent.EffectIndex);
                var plannedChildren = plannedChildrenBySource.TryGetValue(sourceKey, out var currentPlannedChildren)
                    ? currentPlannedChildren
                    : 0;
                if (aliveChildren + plannedChildren >= summonRuntime.MaxAliveChildren)
                {
                    AppendSkipEvent(eventLogEntries, triggerIntent, tickIndex, spawnIndex, SummonSkipReason.MaxAliveReached);
                    continue;
                }

                if (!TrySelectCandidateCell(
                        snapshot,
                        source,
                        summonRuntime,
                        reservedSpawnCells,
                        out var spawnCell))
                {
                    AppendSkipEvent(eventLogEntries, triggerIntent, tickIndex, spawnIndex, SummonSkipReason.NoCandidateCell);
                    continue;
                }

                var summonedEntityState = new SummonedEntityState(triggerIntent.SourceEntityId, triggerIntent.EffectIndex);
                var spawnedEntity = CreateSummonedMinionEntity(
                    entityIdAllocator.AllocateEntityId(),
                    source,
                    spawnCell,
                    minionHp,
                    initialAiMode,
                    tickIndex);
                batch.SpawnEntity(
                    spawnedEntity,
                    new FinalizationOperationMetadata(
                        TickPhase.Resolve,
                        ResolvedActionSemanticKind.None,
                        triggerIntent.SourceEntityId,
                        actionPlanId: 0,
                        movementExecutionBoundaryKind: MovementExecutionBoundaryKind.SpawnRespawnPlacement,
                        boundaryReason: "EnemyUtilitySummonPlacement"),
                    hasSummonedEntityState: true,
                    summonedEntityState: summonedEntityState,
                    hasEnemyDefinitionBindingState: true,
                    enemyDefinitionBindingState: enemyDefinitionBindingState);
                reservedSpawnCells.Add(spawnCell);
                plannedChildrenBySource[sourceKey] = plannedChildren + 1;
                AppendCommittedEvent(
                    eventLogEntries,
                    triggerIntent,
                    tickIndex,
                    spawnIndex,
                    spawnedEntity,
                    summonRuntime);
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

        private static int CountAliveChildren(
            WorldSnapshot snapshot,
            IReadOnlyList<SummonedEntitySnapshotEntry> summonedEntries,
            int sourceEntityId,
            int effectIndex)
        {
            var aliveCount = 0;
            for (var i = 0; i < summonedEntries.Count; i++)
            {
                var entry = summonedEntries[i];
                if (entry.State.SourceEntityId != sourceEntityId ||
                    entry.State.SourceEffectIndex != effectIndex ||
                    !snapshot.TryGetEntity(entry.EntityId, out var child) ||
                    child.hp <= 0 ||
                    child.markedForDeath ||
                    child.boardPresence != EntityBoardPresence.Occupying)
                {
                    continue;
                }

                aliveCount++;
            }

            return aliveCount;
        }

        private static bool TrySelectCandidateCell(
            WorldSnapshot snapshot,
            in EntityState source,
            in SummonMinionRuntime summonRuntime,
            ISet<SurfaceCell> reservedSpawnCells,
            out SurfaceCell spawnCell)
        {
            var candidateOffsets = BuildCandidateOffsets(source.facing);
            for (var i = 0; i < candidateOffsets.Count; i++)
            {
                var candidateCell = source.position + candidateOffsets[i];
                if (!snapshot.IsInsideBoard(candidateCell) ||
                    snapshot.IsTerrainBlockedForUnit(candidateCell))
                {
                    continue;
                }

                if (summonRuntime.RequireNoSolidAtSpawnCell &&
                    snapshot.TryGetSolidSemanticAt(candidateCell, out _))
                {
                    continue;
                }

                if (snapshot.TryGetPlacementBlocker(EntityType.Unit, candidateCell, ignoredEntityId: 0, out _))
                {
                    continue;
                }

                if (summonRuntime.RequireNoUnitAtSpawnCell &&
                    snapshot.HasAnyUnitAt(candidateCell))
                {
                    continue;
                }

                if (reservedSpawnCells.Contains(candidateCell))
                {
                    continue;
                }

                spawnCell = candidateCell;
                return true;
            }

            spawnCell = default;
            return false;
        }

        private static List<Vector2Int> BuildCandidateOffsets(Direction facing)
        {
            if (!EnemyMovementStrategyShared.TryResolveDelta(facing, out var forward) ||
                !EnemyMovementStrategyShared.TryResolveDelta(TurnRight(facing), out var right) ||
                !EnemyMovementStrategyShared.TryResolveDelta(TurnLeft(facing), out var left) ||
                !EnemyMovementStrategyShared.TryResolveDelta(TurnBack(facing), out var back))
            {
                return new List<Vector2Int>
                {
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 0),
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, -1),
                };
            }

            return new List<Vector2Int>
            {
                forward,
                right,
                left,
                back,
            };
        }

        private static List<Vector2Int> BuildTargetOffsets(
            Direction facing,
            in LockNearbyBoxesRuntime lockRuntime)
        {
            switch (lockRuntime.TargetPattern)
            {
                case BoxLockTargetPattern.OrthogonalAdjacent4:
                    return BuildCandidateOffsets(facing);

                case BoxLockTargetPattern.ManhattanRadius:
                    return BuildManhattanOffsets(lockRuntime.Radius, lockRuntime.IncludeSourceCell);

                default:
                    throw new ArgumentOutOfRangeException(nameof(lockRuntime), lockRuntime.TargetPattern, "Unsupported lock nearby boxes target pattern.");
            }
        }

        private static List<Vector2Int> BuildManhattanOffsets(int radius, bool includeSourceCell)
        {
            var offsets = new List<Vector2Int>();
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    var distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (distance > radius ||
                        (!includeSourceCell && dx == 0 && dy == 0))
                    {
                        continue;
                    }

                    offsets.Add(new Vector2Int(dx, dy));
                }
            }

            offsets.Sort(CompareManhattanOffsets);
            return offsets;
        }

        private static int CompareManhattanOffsets(Vector2Int left, Vector2Int right)
        {
            var distanceComparison = (Mathf.Abs(left.x) + Mathf.Abs(left.y)).CompareTo(Mathf.Abs(right.x) + Mathf.Abs(right.y));
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            var yComparison = right.y.CompareTo(left.y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return left.x.CompareTo(right.x);
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

        private static Direction TurnRight(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Right,
                Direction.Right => Direction.Down,
                Direction.Down => Direction.Left,
                Direction.Left => Direction.Up,
                _ => Direction.None,
            };
        }

        private static Direction TurnLeft(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Left,
                Direction.Left => Direction.Down,
                Direction.Down => Direction.Right,
                Direction.Right => Direction.Up,
                _ => Direction.None,
            };
        }

        private static Direction TurnBack(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.None,
            };
        }

        private static EnemyUnitSpawnDefaultsRuntime ResolveArchetypeSpawnDefaults(
            EnemyUnitArchetypeId archetypeId,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId)
        {
            archetypeId.Validate(nameof(archetypeId));
            if (spawnDefaultsByArchetypeId != null &&
                spawnDefaultsByArchetypeId.TryGetValue(archetypeId, out var spawnDefaults))
            {
                return spawnDefaults;
            }

            throw new InvalidOperationException(
                $"Missing enemy unit spawn defaults for archetype '{archetypeId}'.");
        }

        private static EntityState CreateSummonedMinionEntity(
            int entityId,
            in EntityState source,
            SurfaceCell spawnCell,
            int minionHp,
            EnemyAiMode initialAiMode,
            int tickIndex)
        {
            return new EntityState
            {
                entityId = entityId,
                position = spawnCell,
                hp = minionHp,
                maxHp = minionHp,
                teamId = source.teamId,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = source.facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = tickIndex,
                aiMode = initialAiMode,
                aiStateTimer = 0,
                enemyLocomotionCooldownTicks = 0,
                enemyAttackCooldownTicks = 0,
                enemyAttackCooldownTotalTicks = 0,
            };
        }

        private static void AppendCommittedEvent(
            List<string> eventLogEntries,
            in EnemyUtilityTriggerIntent triggerIntent,
            int tickIndex,
            int spawnIndex,
            in EntityState spawnedEntity,
            in SummonMinionRuntime summonRuntime)
        {
            eventLogEntries.Add(
                $"SummonCommitted|Source={triggerIntent.SourceEntityId}|Effect={triggerIntent.EffectIndex}|SpawnIndex={spawnIndex}|Spawned={spawnedEntity.entityId}|Pos=({spawnedEntity.position.x},{spawnedEntity.position.y})|Archetype={summonRuntime.SummonedArchetypeId}|Tick={tickIndex}");
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
            LockNearbyBoxesSkipReason reason,
            int tickIndex)
        {
            eventLogEntries.Add(
                $"BoxInteractionLockSkipped|Source={triggerIntent.SourceEntityId}|Effect={triggerIntent.EffectIndex}|Reason={reason}|Tick={tickIndex}");
        }
    }

}

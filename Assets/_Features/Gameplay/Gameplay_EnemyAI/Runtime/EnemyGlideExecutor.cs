using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal static class EnemyGlideExecutor
    {
        public static void Commit(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
            int entityId,
            EnemyGlideBehaviorRuntime glideBehavior,
            IDetectionStrategy detectionStrategy,
            IChaseStrategy chaseStrategy,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings,
            in ChaseSettings chaseSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            var hasPreviousState = snapshot.TryGetEnemyGlideState(entityId, out var previousState);
            var nextState = hasPreviousState ? previousState : default;
            var changed = false;

            if (source.hp <= 0 ||
                source.markedForDeath ||
                source.aiMode == EnemyAiMode.Dead ||
                source.boardPresence != EntityBoardPresence.Occupying)
            {
                if (hasPreviousState)
                {
                    nextState = EnemyGlideQueries.Clear();
                    changed = true;
                    AppendGlideUpdate(updates, entityId, "ClearDead", nextState);
                }

                if (changed)
                {
                    writeContext.SetEnemyGlideState(entityId, nextState);
                }

                return;
            }

            changed |= TryAdvanceGlideLifecycle(
                snapshot,
                in input,
                source,
                entityId,
                detectionStrategy,
                chaseStrategy,
                commonSettings,
                detectionSettings,
                chaseSettings,
                tileFeatureDefinitions,
                ref hasPreviousState,
                ref nextState,
                updates);

            if (nextState.Phase == EnemyGlidePhase.Ready &&
                (!nextState.InitialDelayInitialized || nextState.InitialDelayTicksRemaining > 0) &&
                glideBehavior.Timing.InitialDelayTicks > 0)
            {
                var delayedState = EnemyGlideQueries.TickInitialDelay(
                    nextState,
                    glideBehavior.Timing.InitialDelayTicks);
                if (!AreEqual(nextState, delayedState))
                {
                    nextState = delayedState;
                    hasPreviousState = true;
                    changed = true;
                    AppendGlideUpdate(
                        updates,
                        entityId,
                        nextState.InitialDelayTicksRemaining > 0 ? "InitialDelayTick" : "InitialDelayReady",
                        nextState);
                }
            }

            var canStartGlide = source.aiMode == EnemyAiMode.Chase &&
                                EnemyGlideQueries.CanStart(hasPreviousState, nextState, input.TickIndex);
            if (canStartGlide &&
                !HasUnsettledVoluntaryKinematicPose(snapshot, source.entityId) &&
                detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out var chaseTarget) &&
                TryResolveGlideStartLockedStep(snapshot, source, chaseTarget,
                    chaseStrategy, commonSettings, chaseSettings, tileFeatureDefinitions,
                    out var lockedStep))
            {
                nextState = EnemyGlideQueries.Start(
                    nextState,
                    input.TickIndex,
                    glideBehavior.Timing,
                    lockedStep,
                    chaseTarget.entityId);
                hasPreviousState = true;
                changed = true;
                AppendGlideUpdate(updates, entityId, "Start", nextState);
                changed |= TryAdvanceGlideLifecycle(
                    snapshot,
                    in input,
                    source,
                    entityId,
                    detectionStrategy,
                    chaseStrategy,
                    commonSettings,
                    detectionSettings,
                    chaseSettings,
                    tileFeatureDefinitions,
                    ref hasPreviousState,
                    ref nextState,
                    updates);
            }

            if (changed)
            {
                writeContext.SetEnemyGlideState(entityId, nextState);
            }
        }

        private static bool TryAdvanceGlideLifecycle(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
            int entityId,
            IDetectionStrategy detectionStrategy,
            IChaseStrategy chaseStrategy,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings,
            in ChaseSettings chaseSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            ref bool hasPreviousState,
            ref EnemyGlideRuntimeState nextState,
            List<string> updates)
        {
            var changed = false;
            for (var guard = 0; guard < 8; guard++)
            {
                switch (nextState.Phase)
                {
                    case EnemyGlidePhase.Windup:
                        if (input.TickIndex < nextState.WindupUntilTickExclusive)
                        {
                            return changed;
                        }

                        var activeLockedStep = default(Vector2Int?);
                        var activeLockedTargetEntityId = 0;
                        if (TryResolveGlideActiveStartTarget(
                                snapshot,
                                source,
                                detectionStrategy,
                                chaseStrategy,
                                commonSettings,
                                detectionSettings,
                                chaseSettings,
                                tileFeatureDefinitions,
                                out var activeTarget,
                                out var activeStep))
                        {
                            activeLockedStep = activeStep;
                            activeLockedTargetEntityId = activeTarget.entityId;
                        }

                        nextState = EnemyGlideQueries.BeginActive(
                            nextState,
                            input.TickIndex,
                            activeLockedStep,
                            activeLockedTargetEntityId);
                        hasPreviousState = true;
                        changed = true;
                        AppendGlideUpdate(updates, entityId, "EnterActive", nextState);
                        continue;

                    case EnemyGlidePhase.Active:
                        if (input.TickIndex < nextState.ActiveUntilTickExclusive &&
                            !nextState.WantsRecover)
                        {
                            return changed;
                        }

                        if (snapshot.TryGetSolidSemanticAt(source.position, out _))
                        {
                            if (!nextState.WantsRecover)
                            {
                                nextState = EnemyGlideQueries.MarkActiveWantsRecover(nextState);
                                hasPreviousState = true;
                                changed = true;
                                AppendGlideUpdate(updates, entityId, "WantsRecover", nextState);
                            }

                            return changed;
                        }

                        if (!nextState.WantsRecover)
                        {
                            nextState = EnemyGlideQueries.MarkActiveWantsRecover(nextState);
                            hasPreviousState = true;
                            changed = true;
                            AppendGlideUpdate(updates, entityId, "WantsRecover", nextState);
                        }

                        nextState = EnemyGlideQueries.BeginRecovery(nextState, input.TickIndex);
                        hasPreviousState = true;
                        changed = true;
                        AppendGlideUpdate(updates, entityId, "EnterRecovery", nextState);
                        continue;

                    case EnemyGlidePhase.Recovery:
                        if (input.TickIndex < nextState.RecoveryUntilTickExclusive)
                        {
                            return changed;
                        }

                        nextState = EnemyGlideQueries.EndRecoveryToCooldown(nextState, input.TickIndex);
                        hasPreviousState = true;
                        changed = true;
                        AppendGlideUpdate(updates, entityId, "EnterCooldown", nextState);
                        continue;

                    case EnemyGlidePhase.Cooldown:
                        if (input.TickIndex >= nextState.CooldownUntilTickExclusive &&
                            input.TickIndex > nextState.LastExitedTick &&
                            source.aiMode != EnemyAiMode.Chase)
                        {
                            nextState = EnemyGlideQueries.ClearRuntimeActivityPreservingInitialDelay(nextState);
                            hasPreviousState = nextState.HasAuthoritativeRecord;
                            changed = true;
                            AppendGlideUpdate(updates, entityId, "Ready", nextState);
                        }

                        return changed;

                    case EnemyGlidePhase.Ready:
                    default:
                        return changed;
                }
            }

            return changed;
        }

        public static bool ShouldSuppressMovement(
            WorldSnapshot snapshot,
            int entityId,
            EnemyGlideBehaviorRuntime glideBehavior)
        {
            if (glideBehavior == null ||
                !snapshot.TryGetEnemyGlideState(entityId, out var glideState))
            {
                return false;
            }

            return glideState.Phase == EnemyGlidePhase.Windup ||
                   glideState.Phase == EnemyGlidePhase.Recovery;
        }

        private static bool HasUnsettledVoluntaryKinematicPose(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetUnitKinematicPose(entityId, out var pose) &&
                   pose.HasAuthoritativeState &&
                   !pose.IsSettledAtAnchor &&
                   pose.Mode == MotionMode.Voluntary;
        }

        private static bool TryGetLockedGlideStep(
            in EnemyGlideRuntimeState state,
            out Vector2Int lockedStep)
        {
            lockedStep = new Vector2Int(state.LockedStepX, state.LockedStepY);
            return state.HasLockedStep &&
                   Math.Abs(lockedStep.x) + Math.Abs(lockedStep.y) == 1;
        }

        private static bool TryResolveGlideLockedStep(
            in EntityState source,
            Vector2Int destination,
            out Vector2Int lockedStep)
        {
            lockedStep = destination - source.position.PlanarPosition;
            return Math.Abs(lockedStep.x) + Math.Abs(lockedStep.y) == 1;
        }

        private static bool TryResolveGlideStartLockedStep(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            IChaseStrategy chaseStrategy,
            in EnemyAiCommonSettings commonSettings,
            in ChaseSettings chaseSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out Vector2Int lockedStep)
        {
            lockedStep = Vector2Int.zero;
            if (chaseStrategy.TryBuildMovementIntent(
                    snapshot,
                    source,
                    target,
                    commonSettings,
                    chaseSettings,
                    tileFeatureDefinitions,
                    out var chaseIntent) &&
                TryResolveGlideLockedStep(source, chaseIntent.Destination, out lockedStep))
            {
                return true;
            }

            return TryResolveGlideLockedStepTowardTarget(source, target, chaseSettings, out lockedStep);
        }

        private static bool TryResolveGlideActiveStartTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            IChaseStrategy chaseStrategy,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings,
            in ChaseSettings chaseSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out EntityState target,
            out Vector2Int lockedStep)
        {
            target = default;
            lockedStep = Vector2Int.zero;
            if (!detectionStrategy.TryFindTarget(
                    snapshot,
                    source,
                    detectionSettings,
                    out target,
                    new EnemyDetectionQueryOptions(LineOfSightSolidBlockerPolicy.IgnoreSolid)))
            {
                return false;
            }

            return TryResolveGlideStartLockedStep(snapshot, source, target,
                chaseStrategy, commonSettings, chaseSettings, tileFeatureDefinitions,
                out lockedStep);
        }

        private static bool TryResolveGlideLockedStepTowardTarget(
            in EntityState source,
            in EntityState target,
            in ChaseSettings settings,
            out Vector2Int lockedStep)
        {
            lockedStep = Vector2Int.zero;
            if (source.position.face != target.position.face)
            {
                return false;
            }

            var planarDelta = target.position - source.position;
            settings.Validate(nameof(settings));
            if (Math.Abs(planarDelta.x) + Math.Abs(planarDelta.y) <= settings.DesiredChaseDistance)
            {
                return false;
            }

            var horizontalStep = planarDelta.x == 0
                ? (Vector2Int?)null
                : new Vector2Int(Math.Sign(planarDelta.x), 0);
            var verticalStep = planarDelta.y == 0
                ? (Vector2Int?)null
                : new Vector2Int(0, Math.Sign(planarDelta.y));
            var tryHorizontalFirst = ShouldTryHorizontalGlideStepFirst(planarDelta, source.facing, settings.AxisPriority);
            var selected = tryHorizontalFirst
                ? horizontalStep ?? verticalStep
                : verticalStep ?? horizontalStep;
            if (!selected.HasValue)
            {
                return false;
            }

            lockedStep = selected.Value;
            return Math.Abs(lockedStep.x) + Math.Abs(lockedStep.y) == 1;
        }

        private static bool ShouldTryHorizontalGlideStepFirst(
            Vector2Int planarDelta,
            Direction facing,
            ChaseAxisPriorityMode axisPriority)
        {
            switch (axisPriority)
            {
                case ChaseAxisPriorityMode.HorizontalFirst:
                    return true;

                case ChaseAxisPriorityMode.VerticalFirst:
                    return false;

                case ChaseAxisPriorityMode.GreatestDistanceThenFacingTieBreak:
                default:
                    var absX = Math.Abs(planarDelta.x);
                    var absY = Math.Abs(planarDelta.y);
                    if (absX != absY)
                    {
                        return absX > absY;
                    }

                    return facing == Direction.Left ||
                           facing == Direction.Right ||
                           planarDelta.x != 0;
            }
        }

        private static void AppendGlideUpdate(
            List<string> updates,
            int entityId,
            string label,
            in EnemyGlideRuntimeState state)
        {
            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            updates.Add(
                $"EnemyGlideStateUpdated|E={entityId}|Label={label}|Phase={state.Phase}|Active={(state.IsActive ? 1 : 0)}|WantsRecover={(state.WantsRecover ? 1 : 0)}|Seq={state.Sequence}|WindupUntil={state.WindupUntilTickExclusive}|ActiveUntil={state.ActiveUntilTickExclusive}|RecoveryUntil={state.RecoveryUntilTickExclusive}|CooldownUntil={state.CooldownUntilTickExclusive}|Windup={state.WindupTicks}|Duration={state.DurationTicks}|Recovery={state.RecoveryTicks}|Cooldown={state.CooldownTicks}|GlideMoveTicks={state.GlideMoveTicks}|LastExited={state.LastExitedTick}|InitialDelayInitialized={(state.InitialDelayInitialized ? 1 : 0)}|InitialDelayRemaining={state.InitialDelayTicksRemaining}|LockedStep={FormatLockedGlideStep(state)}|LockedTarget={state.LockedTargetEntityId}");
        }

        private static bool AreEqual(
            in EnemyGlideRuntimeState left,
            in EnemyGlideRuntimeState right)
        {
            return left.Phase == right.Phase &&
                   left.IsActive == right.IsActive &&
                   left.Sequence == right.Sequence &&
                   left.WindupUntilTickExclusive == right.WindupUntilTickExclusive &&
                   left.ActiveUntilTickExclusive == right.ActiveUntilTickExclusive &&
                   left.RecoveryUntilTickExclusive == right.RecoveryUntilTickExclusive &&
                   left.CooldownUntilTickExclusive == right.CooldownUntilTickExclusive &&
                   left.WindupTicks == right.WindupTicks &&
                   left.DurationTicks == right.DurationTicks &&
                   left.RecoveryTicks == right.RecoveryTicks &&
                   left.CooldownTicks == right.CooldownTicks &&
                   left.GlideMoveTicks == right.GlideMoveTicks &&
                   left.LastExitedTick == right.LastExitedTick &&
                   left.WantsRecover == right.WantsRecover &&
                   left.InitialDelayInitialized == right.InitialDelayInitialized &&
                   left.InitialDelayTicksRemaining == right.InitialDelayTicksRemaining &&
                   left.HasLockedStep == right.HasLockedStep &&
                   left.LockedStepX == right.LockedStepX &&
                   left.LockedStepY == right.LockedStepY &&
                   left.LockedTargetEntityId == right.LockedTargetEntityId;
        }

        private static string FormatLockedGlideStep(in EnemyGlideRuntimeState state)
        {
            return TryGetLockedGlideStep(state, out var lockedStep)
                ? $"({lockedStep.x},{lockedStep.y})"
                : "None";
        }

    }
}

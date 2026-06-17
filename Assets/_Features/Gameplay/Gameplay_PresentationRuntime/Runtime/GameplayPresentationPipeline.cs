using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.PresentationRuntime
{
    public sealed class TickPresentationFactExtractor
    {
        public PresentationFactFrame Extract(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var presentationData = result.PresentationData ?? TickPresentationData.Empty;
            var facts = new List<PresentationFact>();
            var tickIndex = result.TickIndex;
            var topologyCount = 0;
            var combatCount = 0;
            var lifecycleCount = 0;
            var movementCount = 0;
            var tileCount = 0;
            var gravityCount = 0;
            var objectiveCount = 0;
            var stageCount = 0;
            var enemyPresentationCount = 0;
            var actionAudioCount = 0;

            for (var i = 0; i < presentationData.EntityMotions.Count; i++)
            {
                var motion = presentationData.EntityMotions[i];
                var motionPayload = TryCreateBoxMotionPayload(
                    presentationData,
                    result,
                    motion,
                    i,
                    out var payload)
                    ? payload
                    : default;
                facts.Add(new PresentationFact(
                    PresentationFactKind.Movement,
                    new PresentationSource(
                        tickIndex,
                        ResolveMotionSemanticSource(motionPayload, PresentationSemanticSource.EntityMotion),
                        motion.EntityId,
                        (int)motionPayload.ActionKind,
                        motionPayload.SourceSequenceId > 0 ? motionPayload.SourceSequenceId : i + 1),
                    PresentationTarget.Entity(motion.EntityId),
                    new PresentationFactPayload(
                        primaryValue: (int)motion.MotionKind,
                        primaryCell: motion.SourceCell,
                        secondaryCell: motion.DestinationCell,
                        hasPrimaryCell: true,
                        hasSecondaryCell: true),
                    motionPayload: motionPayload));
                movementCount++;
            }

            for (var i = 0; i < presentationData.FlipImpactSignals.Count; i++)
            {
                var signal = presentationData.FlipImpactSignals[i];
                if (signal.BoxEntityId <= 0)
                {
                    continue;
                }

                var payload = new PresentationMotionPayload(
                    PresentationMotionFactKind.BoxFlipImpact,
                    signal.BoxEntityId,
                    signal.SourceCell,
                    signal.ImpactCell,
                    signal.ActorEntityId,
                    PresentationMotionActionKind.Flip,
                    signal.ImpactFacing,
                    signal.SourceFacing,
                    signal.ImpactFacing,
                    sourceSequenceId: signal.SourceActionPlanId > 0 ? signal.SourceActionPlanId : i + 1,
                    sourceActionPlanId: signal.SourceActionPlanId,
                    impactTargetEntityId: signal.ImpactTargetEntityId,
                    flipDisposition: (int)signal.Disposition,
                    hasLandingCell: signal.HasLandingCell,
                    landingCell: signal.LandingCell,
                    topology: signal.Topology,
                    hasTopology: true);
                facts.Add(new PresentationFact(
                    PresentationFactKind.Movement,
                    new PresentationSource(
                        tickIndex,
                        PresentationSemanticSource.BoxFlipImpactMotion,
                        signal.BoxEntityId,
                        (int)PresentationMotionActionKind.Flip,
                        payload.SourceSequenceId),
                    PresentationTarget.Entity(signal.BoxEntityId),
                    new PresentationFactPayload(
                        primaryValue: (int)PresentationMotionFactKind.BoxFlipImpact,
                        secondaryValue: (int)signal.Disposition,
                        tertiaryValue: signal.ImpactTargetEntityId,
                        primaryCell: signal.SourceCell,
                        secondaryCell: signal.ImpactCell,
                        hasPrimaryCell: true,
                        hasSecondaryCell: true),
                    motionPayload: payload));
                movementCount++;
            }

            if (presentationData.TopologyMotion.HasValue)
            {
                var topologyMotion = presentationData.TopologyMotion.Value;
                facts.Add(new PresentationFact(
                    PresentationFactKind.Topology,
                    new PresentationSource(tickIndex, PresentationSemanticSource.TopologyMotion),
                    PresentationTarget.Topology(),
                    topologyPayload: new PresentationTopologyTransitionPayload(
                        topologyMotion.SourceTopology,
                        topologyMotion.DestinationTopology,
                        topologyMotion.RotationKind,
                        tickIndex)));
                topologyCount++;
            }

            for (var i = 0; i < presentationData.PlayerActionSignals.Count; i++)
            {
                var signal = presentationData.PlayerActionSignals[i];
                if (!signal.StartedThisTick &&
                    !signal.ExecutedThisTick &&
                    !signal.CompletedThisTick &&
                    !signal.CanceledThisTick)
                {
                    continue;
                }

                facts.Add(new PresentationFact(
                    PresentationFactKind.Action,
                    new PresentationSource(
                        tickIndex,
                        PresentationSemanticSource.PlayerAction,
                        signal.EntityId,
                        (int)signal.ActiveActionKind,
                        signal.ActiveActionSequence),
                    PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(
                        primaryValue: (int)signal.ResolutionKind,
                        secondaryValue: signal.TargetEntityId)));

                if (TryCreateActionAudioPayload(
                        signal,
                        tickIndex,
                        out var actionAudioPayload))
                {
                    facts.Add(new PresentationFact(
                        PresentationFactKind.ActionAudio,
                        new PresentationSource(
                            tickIndex,
                            PresentationSemanticSource.PlayerActionAudio,
                            signal.EntityId,
                            (int)actionAudioPayload.ActionKind,
                            actionAudioPayload.SourceSequenceId),
                        PresentationTarget.Entity(signal.EntityId),
                        new PresentationFactPayload(
                            primaryValue: (int)actionAudioPayload.Moment,
                            secondaryValue: actionAudioPayload.TargetEntityId,
                            tertiaryValue: (int)actionAudioPayload.OutcomeKind),
                        actionAudioPayload: actionAudioPayload));
                    actionAudioCount++;
                }
            }

            AddPlayerActionAnimationFacts(
                facts,
                tickIndex,
                presentationData.PlayerActionSignals);

            for (var i = 0; i < presentationData.PlayerActionAttemptSignals.Count; i++)
            {
                var signal = presentationData.PlayerActionAttemptSignals[i];
                facts.Add(new PresentationFact(
                    PresentationFactKind.Action,
                    new PresentationSource(
                        tickIndex,
                        PresentationSemanticSource.PlayerActionAttempt,
                        signal.EntityId,
                        (int)signal.ActionKind),
                    signal.HasTarget ? PresentationTarget.Entity(signal.TargetEntityId) : PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(
                        primaryValue: (int)signal.FeedbackKind,
                        secondaryValue: signal.EmitsVisualFeedback ? 1 : 0)));

                if (TryCreateActionAudioPayload(
                        signal,
                        tickIndex,
                        i + 1,
                        out var actionAudioPayload))
                {
                    facts.Add(new PresentationFact(
                        PresentationFactKind.ActionAudio,
                        new PresentationSource(
                            tickIndex,
                            PresentationSemanticSource.PlayerActionAttemptAudio,
                            signal.EntityId,
                            (int)actionAudioPayload.ActionKind,
                            actionAudioPayload.SourceSequenceId),
                        PresentationTarget.Entity(signal.EntityId),
                        new PresentationFactPayload(
                            primaryValue: (int)actionAudioPayload.Moment,
                            secondaryValue: actionAudioPayload.TargetEntityId,
                            tertiaryValue: actionAudioPayload.SourceFeedbackKind),
                        actionAudioPayload: actionAudioPayload));
                    actionAudioCount++;
                }
            }

            AddPlayerActionAttemptAnimationFacts(
                facts,
                tickIndex,
                presentationData.PlayerActionAttemptSignals);

            for (var i = 0; i < presentationData.PlayerDamageSignals.Count; i++)
            {
                var signal = presentationData.PlayerDamageSignals[i];
                if (!signal.TookDamageThisTick)
                {
                    continue;
                }

                facts.Add(new PresentationFact(
                    PresentationFactKind.Combat,
                    new PresentationSource(tickIndex, PresentationSemanticSource.PlayerDamage, signal.EntityId),
                    PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(primaryValue: signal.DamageAmount)));
                combatCount++;
            }

            for (var i = 0; i < presentationData.EnemyDamageSignals.Count; i++)
            {
                var signal = presentationData.EnemyDamageSignals[i];
                if (!signal.TookDamageThisTick)
                {
                    continue;
                }

                facts.Add(new PresentationFact(
                    PresentationFactKind.Combat,
                    new PresentationSource(tickIndex, PresentationSemanticSource.EnemyDamage, signal.EntityId),
                    PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(primaryValue: signal.DamageAmount)));
                combatCount++;
            }

            for (var i = 0; i < presentationData.PlayerDeathSignals.Count; i++)
            {
                var signal = presentationData.PlayerDeathSignals[i];
                if (!signal.DidDieThisTick)
                {
                    continue;
                }

                facts.Add(new PresentationFact(
                    PresentationFactKind.EntityLifecycle,
                    new PresentationSource(tickIndex, PresentationSemanticSource.PlayerDeath, signal.SourceEntityId),
                    PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(primaryValue: signal.DamageAmountAtFatalHit)));
                lifecycleCount++;
            }

            lifecycleCount += AddEntityLifecycleFacts(
                facts,
                tickIndex,
                presentationData.VisibilityChanges.Count,
                PresentationSemanticSource.EntityExit);
            lifecycleCount += AddEntityLifecycleFacts(
                facts,
                tickIndex,
                presentationData.TransitionVisibilityChanges.Count,
                PresentationSemanticSource.EntityExit);
            lifecycleCount += AddEntityLifecycleFacts(
                facts,
                tickIndex,
                presentationData.EntitySpawnSignals.Count,
                PresentationSemanticSource.EntitySpawn);
            lifecycleCount += AddEntityLifecycleFacts(
                facts,
                tickIndex,
                presentationData.EntityExitSignals.Count,
                PresentationSemanticSource.EntityExit);
            lifecycleCount += AddCoreSfxEntityExitFacts(
                facts,
                tickIndex,
                presentationData.EntityExitSignals);
            enemyPresentationCount += AddEnemyJumpPresentationFacts(
                facts,
                tickIndex,
                presentationData.EnemyJumpSignals);
            enemyPresentationCount += AddEnemyChargePresentationFacts(
                facts,
                tickIndex,
                presentationData.EnemyChargeSignals);
            enemyPresentationCount += AddEnemyDeathPresentationFacts(
                facts,
                tickIndex,
                presentationData.EntityExitSignals);

            for (var i = 0; i < presentationData.TileEvents.Count; i++)
            {
                var tileEvent = presentationData.TileEvents[i];
                facts.Add(new PresentationFact(
                    PresentationFactKind.Tile,
                    new PresentationSource(tickIndex, PresentationSemanticSource.TileEvent, tileEvent.SourceEntityId),
                    PresentationTarget.SurfaceCell(tileEvent.Cell),
                    new PresentationFactPayload(
                        primaryValue: (int)tileEvent.EventKind,
                        secondaryValue: tileEvent.TileId,
                        tertiaryValue: (int)tileEvent.TileFeatureKind,
                        primaryCell: tileEvent.Cell,
                        hasPrimaryCell: true)));
                tileCount++;
            }

            for (var i = 0; i < presentationData.GravityFieldEvents.Count; i++)
            {
                var gravityEvent = presentationData.GravityFieldEvents[i];
                facts.Add(new PresentationFact(
                    PresentationFactKind.Gravity,
                    new PresentationSource(tickIndex, PresentationSemanticSource.GravityField, gravityEvent.EmitterEntityId),
                    PresentationTarget.SurfaceCell(gravityEvent.Cell),
                    new PresentationFactPayload(
                        primaryValue: (int)gravityEvent.EventKind,
                        secondaryValue: gravityEvent.TargetEntityId,
                        primaryCell: gravityEvent.Cell,
                        hasPrimaryCell: true)));
                gravityCount++;
            }

            if (result.ObjectiveResult.HasObjective)
            {
                if (result.ObjectiveResult.ClearedThisTick ||
                    result.ObjectiveResult.GoalReached ||
                    result.ObjectiveResult.RequiredNonPrimaryConditionsSatisfiedThisTick)
                {
                    facts.Add(new PresentationFact(
                        PresentationFactKind.Objective,
                        new PresentationSource(tickIndex, PresentationSemanticSource.ObjectiveResult),
                        PresentationTarget.Global(),
                        new PresentationFactPayload(
                            primaryValue: result.ObjectiveResult.ClearedThisTick ? 1 : 0,
                            secondaryValue: result.ObjectiveResult.GoalReached ? 1 : 0,
                            tertiaryValue: result.ObjectiveResult.ConditionStatuses.Count)));
                    objectiveCount++;
                }

                if (result.ObjectiveResult.ClearedThisTick)
                {
                    facts.Add(new PresentationFact(
                        PresentationFactKind.Stage,
                        new PresentationSource(tickIndex, PresentationSemanticSource.StageOutcome),
                        PresentationTarget.Global(),
                        new PresentationFactPayload(primaryValue: 1)));
                    stageCount++;
                }
            }

            return new PresentationFactFrame(
                tickIndex,
                facts,
                new PresentationFactFrameDiagnostics(
                    facts.Count,
                    topologyCount,
                    combatCount,
                    lifecycleCount,
                    movementCount,
                    tileCount,
                    gravityCount,
                    objectiveCount,
                    stageCount,
                    enemyPresentationCount,
                    actionAudioCount));
        }

        private static bool TryCreateActionAudioPayload(
            in TickPlayerActionPresentationSignal signal,
            int tickIndex,
            out PresentationActionAudioPayload payload)
        {
            payload = default;
            if (!signal.StartedThisTick ||
                !TryResolveGameplayActionKind(signal.ActiveActionKind, out var actionKind))
            {
                return false;
            }

            payload = new PresentationActionAudioPayload(
                signal.EntityId,
                actionKind,
                moment: 0,
                tickIndex,
                sourceSequenceId: signal.ActiveActionSequence,
                sourceActionPlanId: signal.ActionPlanId,
                targetEntityId: signal.TargetEntityId,
                direction: signal.Direction,
                outcomeKind: PresentationActionAudioOutcomeKind.Started);
            return payload.IsValid;
        }

        private static bool TryCreateActionAudioPayload(
            in TickPlayerActionAttemptPresentationSignal signal,
            int tickIndex,
            int sourceSequenceId,
            out PresentationActionAudioPayload payload)
        {
            payload = default;
            if (!TryResolveGameplayActionKind(signal.ActionKind, out var actionKind) ||
                !TryResolveActionAudioMoment(signal.FeedbackKind, out var moment))
            {
                return false;
            }

            payload = new PresentationActionAudioPayload(
                signal.EntityId,
                actionKind,
                moment,
                tickIndex,
                sourceSequenceId: sourceSequenceId,
                targetEntityId: signal.HasTarget ? signal.TargetEntityId : 0,
                direction: signal.Direction,
                outcomeKind: PresentationActionAudioOutcomeKind.AttemptFeedback,
                sourceFeedbackKind: (int)signal.FeedbackKind);
            return payload.IsValid;
        }

        private static bool TryResolveGameplayActionKind(
            PlayerActionKind actionKind,
            out int resolved)
        {
            switch (actionKind)
            {
                case PlayerActionKind.Push:
                    resolved = 0;
                    return true;
                case PlayerActionKind.Flip:
                    resolved = 1;
                    return true;
                default:
                    resolved = default;
                    return false;
            }
        }

        private static bool TryResolveActionAudioMoment(
            PlayerActionAttemptFeedbackKind feedbackKind,
            out int moment)
        {
            switch (feedbackKind)
            {
                case PlayerActionAttemptFeedbackKind.AssistOutOfRange:
                    moment = 6;
                    return true;
                case PlayerActionAttemptFeedbackKind.NoTarget:
                    moment = 7;
                    return true;
                case PlayerActionAttemptFeedbackKind.Invalid:
                    moment = 8;
                    return true;
                default:
                    moment = default;
                    return false;
            }
        }

        private static int AddEntityLifecycleFacts(
            List<PresentationFact> facts,
            int tickIndex,
            int count,
            PresentationSemanticSource semanticSource)
        {
            for (var i = 0; i < count; i++)
            {
                facts.Add(new PresentationFact(
                    PresentationFactKind.EntityLifecycle,
                    new PresentationSource(tickIndex, semanticSource, sourceSequence: i + 1),
                    PresentationTarget.Global(),
                    new PresentationFactPayload(primaryValue: count, secondaryValue: i + 1)));
            }

            return count;
        }

        private static bool TryCreateBoxMotionPayload(
            TickPresentationData presentationData,
            TickResult result,
            in TickEntityMotion motion,
            int motionIndex,
            out PresentationMotionPayload payload)
        {
            payload = default;
            if (motion.EntityId <= 0)
            {
                return false;
            }

            if (motion.MotionKind == TickEntityMotionKind.BoxSlide)
            {
                TryResolveBoxSlideStartSignal(
                    presentationData.BoxSlideStartSignals,
                    motion,
                    out var startSignal);
                payload = new PresentationMotionPayload(
                    PresentationMotionFactKind.BoxSlide,
                    motion.EntityId,
                    motion.SourceCell,
                    motion.DestinationCell,
                    startSignal.ActorEntityId,
                    PresentationMotionActionKind.Push,
                    startSignal.BoxEntityId > 0 ? ResolveDirection(startSignal.SourceCell, startSignal.DestinationCell) : ResolveDirection(motion.SourceCell, motion.DestinationCell),
                    motion.SourceFacing ?? Direction.None,
                    motion.DestinationFacing ?? Direction.None,
                    sourceSequenceId: motionIndex + 1,
                    topology: startSignal.BoxEntityId > 0 ? startSignal.Topology : ResolveMotionTopology(motion, result),
                    hasTopology: true);
                return true;
            }

            if (motion.MotionKind == TickEntityMotionKind.Flip &&
                IsBoxEntity(result, motion.EntityId))
            {
                payload = new PresentationMotionPayload(
                    PresentationMotionFactKind.BoxFlip,
                    motion.EntityId,
                    motion.SourceCell,
                    motion.DestinationCell,
                    actorEntityId: ResolveFlipActorEntityId(presentationData, motion.EntityId),
                    PresentationMotionActionKind.Flip,
                    ResolveDirection(motion.SourceCell, motion.DestinationCell),
                    motion.SourceFacing ?? Direction.None,
                    motion.DestinationFacing ?? Direction.None,
                    sourceSequenceId: motionIndex + 1,
                    topology: ResolveMotionTopology(motion, result),
                    hasTopology: true);
                return true;
            }

            return false;
        }

        private static PresentationSemanticSource ResolveMotionSemanticSource(
            PresentationMotionPayload payload,
            PresentationSemanticSource fallback)
        {
            switch (payload.Kind)
            {
                case PresentationMotionFactKind.BoxSlide:
                    return PresentationSemanticSource.BoxSlideMotion;
                case PresentationMotionFactKind.BoxFlip:
                    return PresentationSemanticSource.BoxFlipMotion;
                case PresentationMotionFactKind.BoxFlipImpact:
                    return PresentationSemanticSource.BoxFlipImpactMotion;
                default:
                    return fallback;
            }
        }

        private static void AddPlayerActionAnimationFacts(
            List<PresentationFact> facts,
            int tickIndex,
            IReadOnlyList<TickPlayerActionPresentationSignal> signals)
        {
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!TryResolveAnimationActionKind(signal.ActiveActionKind, out var actionKind))
                {
                    continue;
                }

                if (signal.StartedThisTick)
                {
                    AddPlayerActionAnimationFact(
                        facts,
                        tickIndex,
                        signal,
                        actionKind,
                        PresentationAnimationPhaseKind.Windup,
                        PresentationAnimationOutcomeKind.Started,
                        i + 1);
                }

                if (signal.ExecutedThisTick)
                {
                    AddPlayerActionAnimationFact(
                        facts,
                        tickIndex,
                        signal,
                        actionKind,
                        PresentationAnimationPhaseKind.Execute,
                        ResolveAnimationOutcome(signal.ResolutionKind),
                        i + 1);
                    continue;
                }

                if (signal.IsRecoveryPhase)
                {
                    AddPlayerActionAnimationFact(
                        facts,
                        tickIndex,
                        signal,
                        actionKind,
                        PresentationAnimationPhaseKind.Recovery,
                        PresentationAnimationOutcomeKind.Recovery,
                        i + 1);
                }

                if (signal.CanceledThisTick)
                {
                    AddPlayerActionAnimationFact(
                        facts,
                        tickIndex,
                        signal,
                        actionKind,
                        PresentationAnimationPhaseKind.Failed,
                        PresentationAnimationOutcomeKind.Failed,
                        i + 1);
                }
            }
        }

        private static void AddPlayerActionAnimationFact(
            List<PresentationFact> facts,
            int tickIndex,
            in TickPlayerActionPresentationSignal signal,
            PresentationAnimationActionKind actionKind,
            PresentationAnimationPhaseKind phaseKind,
            PresentationAnimationOutcomeKind outcomeKind,
            int fallbackSequence)
        {
            var sequenceId = signal.ActiveActionSequence > 0
                ? signal.ActiveActionSequence
                : fallbackSequence;
            var payload = new PresentationAnimationPayload(
                PresentationAnimationFactKind.PlayerAction,
                signal.EntityId,
                actionKind,
                phaseKind,
                outcomeKind,
                tickIndex,
                sequenceId,
                signal.ActionPlanId,
                signal.TargetEntityId,
                signal.Direction);
            facts.Add(new PresentationFact(
                PresentationFactKind.Action,
                new PresentationSource(
                    tickIndex,
                    PresentationSemanticSource.PlayerAction,
                    signal.EntityId,
                    (int)actionKind,
                    sequenceId),
                PresentationTarget.Entity(signal.EntityId),
                new PresentationFactPayload(
                    primaryValue: (int)phaseKind,
                    secondaryValue: (int)outcomeKind,
                    tertiaryValue: signal.TargetEntityId),
                animationPayload: payload));
        }

        private static void AddPlayerActionAttemptAnimationFacts(
            List<PresentationFact> facts,
            int tickIndex,
            IReadOnlyList<TickPlayerActionAttemptPresentationSignal> signals)
        {
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!signal.EmitsVisualFeedback ||
                    !TryResolveAnimationActionKind(signal.ActionKind, out var actionKind))
                {
                    continue;
                }

                var sequenceId = i + 1;
                var payload = new PresentationAnimationPayload(
                    PresentationAnimationFactKind.PlayerAction,
                    signal.EntityId,
                    actionKind,
                    PresentationAnimationPhaseKind.Failed,
                    PresentationAnimationOutcomeKind.Failed,
                    tickIndex,
                    sequenceId,
                    sourceActionPlanId: 0,
                    targetEntityId: signal.TargetEntityId,
                    direction: signal.Direction);
                facts.Add(new PresentationFact(
                    PresentationFactKind.Action,
                    new PresentationSource(
                        tickIndex,
                        PresentationSemanticSource.PlayerActionAttempt,
                        signal.EntityId,
                        (int)actionKind,
                        sequenceId),
                    PresentationTarget.Entity(signal.EntityId),
                    new PresentationFactPayload(
                        primaryValue: (int)PresentationAnimationPhaseKind.Failed,
                        secondaryValue: (int)PresentationAnimationOutcomeKind.Failed,
                        tertiaryValue: (int)signal.FeedbackKind),
                    animationPayload: payload));
            }
        }

        private static bool TryResolveAnimationActionKind(
            PlayerActionKind actionKind,
            out PresentationAnimationActionKind animationActionKind)
        {
            switch (actionKind)
            {
                case PlayerActionKind.Push:
                    animationActionKind = PresentationAnimationActionKind.Push;
                    return true;
                case PlayerActionKind.Flip:
                    animationActionKind = PresentationAnimationActionKind.Flip;
                    return true;
                default:
                    animationActionKind = PresentationAnimationActionKind.None;
                    return false;
            }
        }

        private static PresentationAnimationOutcomeKind ResolveAnimationOutcome(
            TickPlayerActionResolutionKind resolutionKind)
        {
            switch (resolutionKind)
            {
                case TickPlayerActionResolutionKind.Blocked:
                    return PresentationAnimationOutcomeKind.Blocked;
                case TickPlayerActionResolutionKind.Impact:
                    return PresentationAnimationOutcomeKind.Impact;
                case TickPlayerActionResolutionKind.Success:
                case TickPlayerActionResolutionKind.None:
                default:
                    return PresentationAnimationOutcomeKind.Executed;
            }
        }

        private static bool TryResolveBoxSlideStartSignal(
            IReadOnlyList<BoxSlideStartPresentationSignal> signals,
            in TickEntityMotion motion,
            out BoxSlideStartPresentationSignal signal)
        {
            if (signals != null)
            {
                for (var i = 0; i < signals.Count; i++)
                {
                    var candidate = signals[i];
                    if (candidate.BoxEntityId == motion.EntityId &&
                        candidate.SourceCell.Equals(motion.SourceCell) &&
                        candidate.DestinationCell.Equals(motion.DestinationCell))
                    {
                        signal = candidate;
                        return true;
                    }
                }
            }

            signal = default;
            return false;
        }

        private static bool IsBoxEntity(TickResult result, int entityId)
        {
            var finalEntities = result.FinalEntities;
            for (var i = 0; i < finalEntities.Count; i++)
            {
                if (finalEntities[i].entityId == entityId)
                {
                    return finalEntities[i].type == EntityType.Box;
                }
            }

            return false;
        }

        private static int ResolveFlipActorEntityId(TickPresentationData presentationData, int boxEntityId)
        {
            var playerActionSignals = presentationData.PlayerActionSignals;
            for (var i = 0; i < playerActionSignals.Count; i++)
            {
                var signal = playerActionSignals[i];
                if (signal.ActiveActionKind == PlayerActionKind.Flip &&
                    signal.TargetEntityId == boxEntityId)
                {
                    return signal.EntityId;
                }
            }

            return 0;
        }

        private static CubeTopologyState ResolveMotionTopology(in TickEntityMotion motion, TickResult result)
        {
            if (motion.SourceTopology.HasValue)
            {
                return motion.SourceTopology.Value;
            }

            if (motion.DestinationTopology.HasValue)
            {
                return motion.DestinationTopology.Value;
            }

            return result.PresentationData.TopologyMotion.HasValue
                ? result.PresentationData.TopologyMotion.Value.SourceTopology
                : result.FinalTopology;
        }

        private static Direction ResolveDirection(SurfaceCell source, SurfaceCell destination)
        {
            if (source.face != destination.face)
            {
                return Direction.None;
            }

            var delta = destination - source;
            if (delta.x == 1 && delta.y == 0)
            {
                return Direction.Right;
            }

            if (delta.x == -1 && delta.y == 0)
            {
                return Direction.Left;
            }

            if (delta.x == 0 && delta.y == 1)
            {
                return Direction.Up;
            }

            if (delta.x == 0 && delta.y == -1)
            {
                return Direction.Down;
            }

            return Direction.None;
        }

        private static int AddCoreSfxEntityExitFacts(
            List<PresentationFact> facts,
            int tickIndex,
            IReadOnlyList<TickEntityExitPresentationSignal> signals)
        {
            if (signals == null || signals.Count == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.ExitedEntityId <= 0 ||
                    !IsCoreSfxExitCause(signal.ExitCause))
                {
                    continue;
                }

                facts.Add(new PresentationFact(
                    PresentationFactKind.EntityLifecycle,
                    new PresentationSource(
                        tickIndex,
                        PresentationSemanticSource.EntityExit,
                        signal.ExitedEntityId,
                        (int)signal.ExitCause,
                        signal.PresentationSeed),
                    PresentationTarget.Entity(signal.ExitedEntityId),
                    new PresentationFactPayload(
                        primaryValue: (int)signal.ExitCause,
                        secondaryValue: (int)signal.EntityType,
                        tertiaryValue: signal.SourceActorEntityId ?? 0,
                        primaryCell: signal.SourceCell,
                        hasPrimaryCell: true)));
                count++;
            }

            return count;
        }

        private static bool IsCoreSfxExitCause(TickEntityExitCause exitCause)
        {
            return exitCause == TickEntityExitCause.ItemConsume ||
                   exitCause == TickEntityExitCause.BoxDestroy ||
                   exitCause == TickEntityExitCause.DestroyedByImpact ||
                   exitCause == TickEntityExitCause.EnemyDeath ||
                   exitCause == TickEntityExitCause.Killed ||
                   exitCause == TickEntityExitCause.OutOfBounds;
        }

        private static int AddEnemyJumpPresentationFacts(
            List<PresentationFact> facts,
            int tickIndex,
            IReadOnlyList<TickEnemyJumpPresentationSignal> signals)
        {
            if (signals == null || signals.Count == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0)
                {
                    continue;
                }

                if (signal.StartedWindupThisTick)
                {
                    AddEnemyPresentationFact(
                        facts,
                        tickIndex,
                        PresentationSemanticSource.EnemyJump,
                        signal.EntityId,
                        PresentationEnemyPresentationKind.Jump,
                        PresentationEnemyPresentationPhase.Windup,
                        ResolveJumpOutcome(signal),
                        signal.Sequence > 0 ? signal.Sequence : i + 1,
                        signal.SourceCell,
                        signal.PresentationTargetCell,
                        hasSourceCell: true,
                        hasTargetCell: true,
                        signal.Facing);
                    count++;
                }

                if (signal.StartedAirborneThisTick || signal.RetryThisTick)
                {
                    AddEnemyPresentationFact(
                        facts,
                        tickIndex,
                        PresentationSemanticSource.EnemyJump,
                        signal.EntityId,
                        PresentationEnemyPresentationKind.Jump,
                        PresentationEnemyPresentationPhase.Airborne,
                        ResolveJumpOutcome(signal),
                        signal.Sequence > 0 ? signal.Sequence : i + 1,
                        signal.SourceCell,
                        signal.PresentationTargetCell,
                        hasSourceCell: true,
                        hasTargetCell: true,
                        signal.Facing);
                    count++;
                }

                if (signal.LandedThisTick)
                {
                    AddEnemyPresentationFact(
                        facts,
                        tickIndex,
                        PresentationSemanticSource.EnemyJump,
                        signal.EntityId,
                        PresentationEnemyPresentationKind.Jump,
                        PresentationEnemyPresentationPhase.Land,
                        ResolveJumpOutcome(signal),
                        signal.Sequence > 0 ? signal.Sequence : i + 1,
                        signal.SourceCell,
                        signal.PresentationTargetCell,
                        hasSourceCell: true,
                        hasTargetCell: true,
                        signal.Facing);
                    count++;
                }
            }

            return count;
        }

        private static int AddEnemyChargePresentationFacts(
            List<PresentationFact> facts,
            int tickIndex,
            IReadOnlyList<TickEnemyChargePresentationSignal> signals)
        {
            if (signals == null || signals.Count == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0)
                {
                    continue;
                }

                if (signal.StartedWindupThisTick)
                {
                    AddEnemyPresentationFact(
                        facts,
                        tickIndex,
                        PresentationSemanticSource.EnemyCharge,
                        signal.EntityId,
                        PresentationEnemyPresentationKind.Charge,
                        PresentationEnemyPresentationPhase.Windup,
                        PresentationEnemyPresentationOutcome.Started,
                        signal.Sequence > 0 ? signal.Sequence : i + 1,
                        direction: signal.LockedDirection);
                    count++;
                }

                if (signal.StartedActiveThisTick)
                {
                    AddEnemyPresentationFact(
                        facts,
                        tickIndex,
                        PresentationSemanticSource.EnemyCharge,
                        signal.EntityId,
                        PresentationEnemyPresentationKind.Charge,
                        PresentationEnemyPresentationPhase.Active,
                        PresentationEnemyPresentationOutcome.ActiveStarted,
                        signal.Sequence > 0 ? signal.Sequence : i + 1,
                        direction: signal.LockedDirection);
                    count++;
                }

                if (signal.StartedRecoverThisTick)
                {
                    AddEnemyPresentationFact(
                        facts,
                        tickIndex,
                        PresentationSemanticSource.EnemyCharge,
                        signal.EntityId,
                        PresentationEnemyPresentationKind.Charge,
                        PresentationEnemyPresentationPhase.Recover,
                        PresentationEnemyPresentationOutcome.Started,
                        signal.Sequence > 0 ? signal.Sequence : i + 1,
                        direction: signal.LockedDirection);
                    count++;
                }
            }

            return count;
        }

        private static int AddEnemyDeathPresentationFacts(
            List<PresentationFact> facts,
            int tickIndex,
            IReadOnlyList<TickEntityExitPresentationSignal> signals)
        {
            if (signals == null || signals.Count == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityType != EntityType.Unit ||
                    signal.ExitedEntityId <= 0 ||
                    (signal.ExitCause != TickEntityExitCause.EnemyDeath &&
                     signal.ExitCause != TickEntityExitCause.Killed))
                {
                    continue;
                }

                AddEnemyPresentationFact(
                    facts,
                    tickIndex,
                    PresentationSemanticSource.EntityExit,
                    signal.ExitedEntityId,
                    PresentationEnemyPresentationKind.Death,
                    PresentationEnemyPresentationPhase.Death,
                    PresentationEnemyPresentationOutcome.Death,
                    signal.PresentationSeed > 0 ? signal.PresentationSeed : i + 1,
                    signal.SourceCell,
                    signal.PresentationTargetCell,
                    hasSourceCell: true,
                    hasTargetCell: signal.HasPresentationTargetCell,
                    signal.Facing,
                    (int)signal.ExitCause,
                    (int)signal.Timing);
                count++;
            }

            return count;
        }

        private static void AddEnemyPresentationFact(
            List<PresentationFact> facts,
            int tickIndex,
            PresentationSemanticSource semanticSource,
            int entityId,
            PresentationEnemyPresentationKind kind,
            PresentationEnemyPresentationPhase phase,
            PresentationEnemyPresentationOutcome outcome,
            int sequenceId,
            SurfaceCell sourceCell = default,
            SurfaceCell targetCell = default,
            bool hasSourceCell = false,
            bool hasTargetCell = false,
            Direction direction = Direction.None,
            int sourceCause = 0,
            int timing = 0)
        {
            var enemyPayload = new PresentationEnemyPayload(
                kind,
                phase,
                entityId,
                tickIndex,
                sequenceId,
                outcome,
                sourceCell,
                targetCell,
                hasSourceCell,
                hasTargetCell,
                direction,
                sourceCause,
                timing);
            var animationPayload = new PresentationAnimationPayload(
                PresentationAnimationFactKind.EnemyPresentation,
                entityId,
                ResolveEnemyAnimationActionKind(kind),
                ResolveEnemyAnimationPhaseKind(phase),
                ResolveEnemyAnimationOutcomeKind(outcome),
                tickIndex,
                sequenceId,
                sourceActionPlanId: 0,
                targetEntityId: 0,
                direction);
            facts.Add(new PresentationFact(
                PresentationFactKind.EnemyPresentation,
                new PresentationSource(
                    tickIndex,
                    semanticSource,
                    entityId,
                    (int)kind,
                    sequenceId),
                PresentationTarget.Entity(entityId),
                new PresentationFactPayload(
                    primaryValue: (int)kind,
                    secondaryValue: (int)phase,
                    tertiaryValue: (int)outcome,
                    primaryCell: sourceCell,
                    secondaryCell: targetCell,
                    hasPrimaryCell: hasSourceCell,
                    hasSecondaryCell: hasTargetCell),
                animationPayload: animationPayload,
                enemyPayload: enemyPayload));
        }

        private static PresentationEnemyPresentationOutcome ResolveJumpOutcome(
            in TickEnemyJumpPresentationSignal signal)
        {
            switch (signal.Outcome)
            {
                case TickEnemyJumpPresentationOutcome.Landed:
                case TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded:
                    return PresentationEnemyPresentationOutcome.Landed;
                case TickEnemyJumpPresentationOutcome.Retried:
                    return PresentationEnemyPresentationOutcome.Retried;
                case TickEnemyJumpPresentationOutcome.AirborneStarted:
                    return PresentationEnemyPresentationOutcome.ActiveStarted;
                case TickEnemyJumpPresentationOutcome.WindupStarted:
                    return PresentationEnemyPresentationOutcome.Started;
                default:
                    if (signal.LandedThisTick)
                    {
                        return PresentationEnemyPresentationOutcome.Landed;
                    }

                    return signal.StartedAirborneThisTick || signal.RetryThisTick
                        ? PresentationEnemyPresentationOutcome.ActiveStarted
                        : PresentationEnemyPresentationOutcome.Started;
            }
        }

        private static PresentationAnimationActionKind ResolveEnemyAnimationActionKind(
            PresentationEnemyPresentationKind kind)
        {
            return kind switch
            {
                PresentationEnemyPresentationKind.Jump => PresentationAnimationActionKind.EnemyJump,
                PresentationEnemyPresentationKind.Charge => PresentationAnimationActionKind.EnemyCharge,
                PresentationEnemyPresentationKind.Death => PresentationAnimationActionKind.EnemyDeath,
                _ => PresentationAnimationActionKind.None,
            };
        }

        private static PresentationAnimationPhaseKind ResolveEnemyAnimationPhaseKind(
            PresentationEnemyPresentationPhase phase)
        {
            return phase switch
            {
                PresentationEnemyPresentationPhase.Windup => PresentationAnimationPhaseKind.Windup,
                PresentationEnemyPresentationPhase.Airborne => PresentationAnimationPhaseKind.Airborne,
                PresentationEnemyPresentationPhase.Land => PresentationAnimationPhaseKind.Land,
                PresentationEnemyPresentationPhase.Active => PresentationAnimationPhaseKind.Active,
                PresentationEnemyPresentationPhase.Recover => PresentationAnimationPhaseKind.Recovery,
                PresentationEnemyPresentationPhase.Death => PresentationAnimationPhaseKind.Death,
                _ => PresentationAnimationPhaseKind.None,
            };
        }

        private static PresentationAnimationOutcomeKind ResolveEnemyAnimationOutcomeKind(
            PresentationEnemyPresentationOutcome outcome)
        {
            return outcome switch
            {
                PresentationEnemyPresentationOutcome.Landed => PresentationAnimationOutcomeKind.Landed,
                PresentationEnemyPresentationOutcome.Retried => PresentationAnimationOutcomeKind.Retried,
                PresentationEnemyPresentationOutcome.ActiveStarted => PresentationAnimationOutcomeKind.Executed,
                PresentationEnemyPresentationOutcome.Death => PresentationAnimationOutcomeKind.Death,
                PresentationEnemyPresentationOutcome.Started => PresentationAnimationOutcomeKind.Started,
                _ => PresentationAnimationOutcomeKind.None,
            };
        }
    }

    public sealed class GameplayPresentationPipeline
    {
        private static readonly IReadOnlyList<IPresentationExecutor> EmptyExecutors =
            new ReadOnlyCollection<IPresentationExecutor>(new List<IPresentationExecutor>());

        private readonly TickPresentationFactExtractor _factExtractor;
        private readonly PresentationCuePlannerSet _cuePlannerSet;
        private readonly PresentationPlaybackPlanner _playbackPlanner;
        private readonly PresentationPlaybackScheduler _scheduler;
        private readonly IReadOnlyList<IPresentationExecutor> _executors;

        public GameplayPresentationPipeline(
            TickPresentationFactExtractor factExtractor,
            PresentationCuePlannerSet cuePlannerSet,
            PresentationPlaybackPlanner playbackPlanner,
            PresentationPlaybackScheduler scheduler,
            IReadOnlyList<IPresentationExecutor> executors = null)
        {
            _factExtractor = factExtractor ?? throw new ArgumentNullException(nameof(factExtractor));
            _cuePlannerSet = cuePlannerSet ?? throw new ArgumentNullException(nameof(cuePlannerSet));
            _playbackPlanner = playbackPlanner ?? throw new ArgumentNullException(nameof(playbackPlanner));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _executors = executors == null || executors.Count == 0
                ? EmptyExecutors
                : new ReadOnlyCollection<IPresentationExecutor>(new List<IPresentationExecutor>(executors));
        }

        public PresentationFactFrame LastFactFrame { get; private set; }

        public PresentationCueFrame LastCueFrame { get; private set; }

        public PresentationPlaybackPlan LastPlaybackPlan { get; private set; }

        public IReadOnlyList<IPresentationExecutor> Executors => _executors;

        public PresentationPlaybackDiagnostics CurrentDiagnostics => _scheduler.CurrentDiagnostics;

        public PresentationBlockingSnapshot BlockingSnapshot => _scheduler.BlockingSnapshot;

        public int NoOpSchedulerAcceptCount => _scheduler.CurrentDiagnostics.NoOpSchedulerAcceptCount;

        public bool HasBlockingPresentation => _scheduler.HasBlockingPresentation;

        public void ObserveTopologyActiveState(bool isActive, int tickIndex)
        {
            _scheduler.ObserveActiveBlockingState(
                PresentationBlockingSource.TopologyTransition,
                isActive,
                tickIndex);
        }

        public void Present(TickResult result)
        {
            var factFrame = _factExtractor.Extract(result);
            var cueFrame = _cuePlannerSet.Plan(factFrame);
            var playbackPlan = _playbackPlanner.Plan(cueFrame);

            LastFactFrame = factFrame;
            LastCueFrame = cueFrame;
            LastPlaybackPlan = playbackPlan;
            for (var i = 0; i < _executors.Count; i++)
            {
                _executors[i]?.Prepare(playbackPlan);
            }

            _scheduler.Accept(playbackPlan);
            for (var i = 0; i < _executors.Count; i++)
            {
                _executors[i]?.Play(playbackPlan);
            }
        }

        public void Update(float deltaTime)
        {
            _scheduler.Update(deltaTime);
            for (var i = 0; i < _executors.Count; i++)
            {
                _executors[i]?.Update(deltaTime);
            }
        }

        public void ResetSession()
        {
            LastFactFrame = null;
            LastCueFrame = null;
            LastPlaybackPlan = null;
            _scheduler.ResetSession();
            for (var i = 0; i < _executors.Count; i++)
            {
                _executors[i]?.ResetSession();
            }
        }

        public void HardCleanup()
        {
            LastFactFrame = null;
            LastCueFrame = null;
            LastPlaybackPlan = null;
            _scheduler.HardCleanup();
            for (var i = 0; i < _executors.Count; i++)
            {
                _executors[i]?.HardCleanup();
            }
        }
    }

    public static class GameplayPresentationPipelineInstaller
    {
        public static GameplayPresentationPipeline CreateDiagnosticsOnly()
        {
            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new TopologyCuePlanner(),
                    new MotionCuePlanner(),
                    new AnimationCuePlanner(),
                    new EnemyPresentationCuePlanner(),
                    new VfxCuePlanner(),
                    new SfxCuePlanner(),
                    new ActionAudioCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler());
        }
    }
}

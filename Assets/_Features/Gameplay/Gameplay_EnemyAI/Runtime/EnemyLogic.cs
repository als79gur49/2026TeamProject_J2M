using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    public interface IEnemyAiStateResolver
    {
        EnemyAiTransitionDecision Resolve(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            EnemyAiTransitionStage stage,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyAiCommonSettings commonSettings,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings);
    }

    public sealed class EnemyLogic : IEnemyAiStateLogic, IPreMovementStateLogic, IMovementEntityLogic, IMovementEntityDebugLogic, IAttackEntityLogic, IEntityLogicSourceBinding
    {
        private readonly struct GroundLocomotionResolution
        {
            public GroundLocomotionResolution(
                bool hasIntent,
                RawMovementIntent intent,
                int cooldownTicks,
                int ordinaryKinematicMoveTicks)
            {
                HasIntent = hasIntent;
                Intent = intent;
                CooldownTicks = cooldownTicks;
                OrdinaryKinematicMoveTicks = ordinaryKinematicMoveTicks;
            }

            public bool HasIntent { get; }

            public RawMovementIntent Intent { get; }

            public int CooldownTicks { get; }

            public int OrdinaryKinematicMoveTicks { get; }
        }

        private readonly int _entityId;
        private readonly EnemyAiCommonSettings _commonSettings;
        private readonly PatrolSettings _patrolSettings;
        private readonly DetectionSettings _detectionSettings;
        private readonly ChaseSettings _chaseSettings;
        private readonly EnemyLocomotionTimingSettings _locomotionTimingSettings;
        private readonly EnemyChargeTimingSettings _chargeTimingSettings;
        private readonly PatrolStrategyKind _patrolStrategyKind;
        private readonly IPatrolStrategy _patrolStrategy;
        private readonly IDetectionStrategy _detectionStrategy;
        private readonly IChaseStrategy _chaseStrategy;
        private readonly EnemyCombatCapabilityRuntime _combatCapability;
        private readonly EnemyMovementSkillCapabilityRuntime _movementSkillCapability;
        private readonly EnemyPassiveContactCapabilityRuntime _passiveContactCapability;
        private readonly EnemyUtilityCapabilityRuntime _utilityCapability;
        private readonly EnemyFrontFaceSupportCapabilityRuntime _frontFaceSupportCapability;
        private readonly IEnemyAiStateResolver _stateResolver;
        private readonly bool _usesChargeStateResolver;
        private readonly List<EntityState> _sharedCellUnits = new();

        public EnemyLogic(int entityId)
            : this(entityId, EnemyAiRuntimeDefinition.CreateDefaultMelee())
        {
        }

        public EnemyLogic(int entityId, EnemyAiProfile profile)
            : this(
                entityId,
                (profile ?? throw new ArgumentNullException(nameof(profile)))
                .CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond))
        {
        }

        public EnemyLogic(int entityId, in EnemyAiRuntimeDefinition aiDefinition)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Enemy logic requires a positive entity ID.");
            }

            aiDefinition.Validate(nameof(aiDefinition));

            _entityId = entityId;
            _commonSettings = aiDefinition.CommonSettings;
            _patrolSettings = aiDefinition.PatrolSettings;
            _detectionSettings = aiDefinition.DetectionSettings;
            _chaseSettings = aiDefinition.ChaseSettings;
            _locomotionTimingSettings = aiDefinition.LocomotionTimingSettings;
            _chargeTimingSettings = aiDefinition.ChargeTimingSettings;
            _patrolStrategyKind = aiDefinition.Brain.Patrol.Kind;
            _patrolStrategy = aiDefinition.PatrolStrategy;
            _detectionStrategy = aiDefinition.DetectionStrategy;
            _chaseStrategy = aiDefinition.ChaseStrategy;
            _stateResolver = aiDefinition.StateResolver;
            _usesChargeStateResolver = aiDefinition.Brain.StateResolver.Kind == EnemyAiStateResolverKind.Charge;
            aiDefinition.Capabilities.TryGetCombat(out _combatCapability);
            aiDefinition.Capabilities.TryGetMovementSkill(out _movementSkillCapability);
            aiDefinition.Capabilities.TryGetPassiveContact(out _passiveContactCapability);
            aiDefinition.Capabilities.TryGetUtility(out _utilityCapability);
            aiDefinition.Capabilities.TryGetFrontFaceSupport(out _frontFaceSupportCapability);
        }

        public int ControlledEntityId => _entityId;

        internal bool TryGetJumpCooldownTicks(out int cooldownTicks)
        {
            cooldownTicks = 0;
            if (!HasJumpMovementSkill())
            {
                return false;
            }

            cooldownTicks = _movementSkillCapability.JumpTimingSettings.CooldownTicks;
            return true;
        }

        void IEnemyAiStateLogic.CommitAiTransitions(
            WorldSnapshot snapshot,
            in TickInput input,
            EnemyAiTransitionStage stage,
            IEnemyAiCommitContext writeContext,
            List<string> transitions)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            if (!TryGetAiControlledEnemy(snapshot, out var source))
            {
                return;
            }

            var decision = _stateResolver.Resolve(
                snapshot,
                source,
                input.TickIndex,
                stage,
                _detectionStrategy,
                _combatCapability,
                _commonSettings,
                _chargeTimingSettings,
                _detectionSettings);

            if (ShouldDeferChargeStartForOrdinaryKinematic(snapshot, source, decision, out var deferredPose))
            {
                transitions.Add(
                    $"EnemyChargeStartDeferred|Stage={stage}|E={source.entityId}|Reason=KinematicNotSettled|Mode={deferredPose.Mode}|Anchor={deferredPose.AnchorCell}|Offset={deferredPose.LocalOffset}|Elapsed={deferredPose.State.elapsedTicks}|Total={deferredPose.State.totalTicks}");
                return;
            }

            var resolvedFacing = ResolvePatrolFacing(snapshot, source, input.TickIndex, stage, decision);
            TryCapturePatrolOriginBeforeLeavingPatrol(
                snapshot,
                source,
                input.TickIndex,
                stage,
                decision,
                writeContext,
                transitions);

            if (decision.Mode == source.aiMode && decision.Timer == source.aiStateTimer)
            {
                if (resolvedFacing.HasValue)
                {
                    writeContext.SetFacing(source.entityId, resolvedFacing.Value);
                }

                if (ShouldLogUnchangedChargeDecision(source, decision))
                {
                    transitions.Add(
                        $"EnemyAiTransition|Stage={stage}|E={source.entityId}|From={source.aiMode}|FromTimer={source.aiStateTimer}|To={decision.Mode}|ToTimer={decision.Timer}|Reason={decision.Reason}|Facing={(resolvedFacing.HasValue ? resolvedFacing.Value.ToString() : source.facing.ToString())}");
                }

                return;
            }

            writeContext.ApplyEnemyAiState(source.entityId, decision.Mode, decision.Timer);
            if (resolvedFacing.HasValue)
            {
                writeContext.SetFacing(source.entityId, resolvedFacing.Value);
            }
            transitions.Add(
                $"EnemyAiTransition|Stage={stage}|E={source.entityId}|From={source.aiMode}|FromTimer={source.aiStateTimer}|To={decision.Mode}|ToTimer={decision.Timer}|Reason={decision.Reason}|Facing={(resolvedFacing.HasValue ? resolvedFacing.Value.ToString() : source.facing.ToString())}");
        }

        void IPreMovementStateLogic.CommitPreMovementState(
            WorldSnapshot snapshot,
            in TickInput input,
            IPreMovementStateCommitContext writeContext,
            List<string> updates,
            List<PlayerActionTransition> actionTransitions)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            if (actionTransitions == null)
            {
                throw new ArgumentNullException(nameof(actionTransitions));
            }

            if (!EnemyParticipationPolicy.TryGetEnemyLogicEntity(snapshot, _entityId, out var source))
            {
                return;
            }

            if (_frontFaceSupportCapability != null)
            {
                CommitEnemyFrontFaceSupportState(snapshot, in input, source, writeContext, updates);
            }

            if (!EnemyParticipationPolicy.CanParticipateOnCurrentTopology(snapshot, source))
            {
                if (_utilityCapability != null)
                {
                    CommitEnemyUtilityState(snapshot, in input, source, writeContext, updates);
                }

                return;
            }

            TryInitializePatrolStateFromProposal(snapshot, source, input.TickIndex, writeContext, updates);

            var suppressMovementThisTick = false;
            if (HasJumpMovementSkill())
            {
                if (writeContext is not IEnemyJumpCommitContext jumpWriteContext)
                {
                    throw new ArgumentException("Pre-movement write context must expose enemy jump commit capabilities.", nameof(writeContext));
                }

                suppressMovementThisTick = CommitJumpState(snapshot, in input, source, writeContext, jumpWriteContext, updates);
            }

            if (_usesChargeStateResolver)
            {
                CommitChargeState(snapshot, in input, source, writeContext, updates);
            }

            if (HasGlideMovementSkill())
            {
                CommitGlideState(snapshot, in input, source, writeContext, updates);
            }

            if (HasPhaseMovementSkill())
            {
                CommitEnemyOwnedPhasedState(snapshot, in input, source, writeContext, updates);
            }

            if (_utilityCapability != null)
            {
                CommitEnemyUtilityState(snapshot, in input, source, writeContext, updates);
            }

            if (!TryGetControllableEnemy(snapshot, out source) ||
                source.enemyLocomotionCooldownTicks <= 0)
            {
                return;
            }

            var nextCooldown = source.enemyLocomotionCooldownTicks - 1;
            writeContext.SetEnemyLocomotionCooldown(_entityId, nextCooldown);
            updates.Add(
                $"EnemyLocomotionCooldownUpdated|E={_entityId}|From={source.enemyLocomotionCooldownTicks}|To={nextCooldown}");

            if (suppressMovementThisTick)
            {
                return;
            }
        }

        public void CollectMovementIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawMovementIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!TryGetControllableEnemy(snapshot, out var source))
            {
                return;
            }

            if (snapshot.TryGetUnitKinematicPose(source.entityId, out var pose) &&
                pose.HasAuthoritativeState &&
                !pose.IsSettledAtAnchor &&
                pose.Mode == MotionMode.Voluntary)
            {
                return;
            }

            if (HasGlideMovementSkill() &&
                snapshot.TryGetEnemyGlideState(_entityId, out var movementGlideState) &&
                movementGlideState.Phase == EnemyGlidePhase.LandingPending)
            {
                if (TryResolveLandingPendingEgressLocomotion(snapshot, source, out var egressLocomotion))
                {
                    buffer.Add(ApplyMovementTiming(
                        egressLocomotion.Intent,
                        egressLocomotion.CooldownTicks,
                        egressLocomotion.OrdinaryKinematicMoveTicks));
                }

                return;
            }

            if (ShouldSuppressMovementForJump(snapshot, input.TickIndex) ||
                ShouldSuppressMovementForGlide(snapshot) ||
                ShouldSuppressMovementForEnemyPhase(snapshot))
            {
                return;
            }

            var locomotion = ResolveBaselineGroundLocomotion(snapshot, source, input.TickIndex);
            if (locomotion.HasIntent)
            {
                buffer.Add(ApplyMovementTiming(
                    locomotion.Intent,
                    locomotion.CooldownTicks,
                    locomotion.OrdinaryKinematicMoveTicks));
            }
        }

        public void CollectMovementDebugEvents(
            WorldSnapshot snapshot,
            in TickInput input,
            IReadOnlyList<RawMovementIntent> rawIntents,
            List<string> debugEvents)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (rawIntents == null)
            {
                throw new ArgumentNullException(nameof(rawIntents));
            }

            if (debugEvents == null)
            {
                throw new ArgumentNullException(nameof(debugEvents));
            }

            if (!HasGlideMovementSkill() ||
                !TryGetControllableEnemy(snapshot, out var source) ||
                !snapshot.TryGetEnemyGlideState(_entityId, out var glideState))
            {
                return;
            }

            var result = "Skipped";
            var reason = ResolveGlideMoveIntentDebugReason(snapshot, source, glideState, input.TickIndex, rawIntents);
            if (reason == "IntentCreated")
            {
                result = "IntentCreated";
            }

            AppendGlideMoveIntentDebug(
                debugEvents,
                snapshot,
                input.TickIndex,
                source,
                glideState,
                result,
                reason,
                TryFindMoveIntent(rawIntents, source.entityId, out var intent) ? intent.Destination : default,
                hasDestination: reason == "IntentCreated");
        }

        public void CollectAttackIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawAttackIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!TryGetControllableEnemy(snapshot, out var source))
            {
                return;
            }

            if (ShouldSuppressAttackForEnemyPhase(snapshot, input.TickIndex))
            {
                return;
            }

            if (_combatCapability != null &&
                !ShouldSuppressCombatAttackForJump(snapshot) &&
                snapshot.TryGetEnemyActionState(_entityId, out var actionState) &&
                EnemyActionQueries.CanExecute(actionState, input.TickIndex) &&
                EnemyActionStateTargeting.TryResolveLockedTarget(
                    snapshot,
                    source,
                    actionState,
                    _combatCapability.AttackDecisionStrategy,
                    _detectionSettings,
                    _combatCapability.AttackDecisionSettings,
                    out var combatTarget) &&
                _combatCapability.AttackDecisionStrategy.TryBuildAttackIntent(
                    snapshot,
                    source,
                    combatTarget,
                    _commonSettings,
                    _combatCapability.AttackDecisionSettings,
                    out var combatIntent))
            {
                buffer.Add(new RawAttackIntent(
                    combatIntent.SourceId,
                    combatIntent.Priority,
                    combatIntent.TargetId,
                    AttackSourceKind.Combat,
                    localSequence: 0));
            }

            if (_passiveContactCapability != null &&
                CanCollectPassiveContact(snapshot) &&
                TryResolvePassiveContactTarget(snapshot, source, out var passiveContactTarget) &&
                _passiveContactCapability.AttackDecisionStrategy.TryBuildAttackIntent(
                    snapshot,
                    source,
                    passiveContactTarget,
                    _commonSettings,
                    _passiveContactCapability.AttackDecisionSettings,
                    out var passiveContactIntent))
            {
                buffer.Add(new RawAttackIntent(
                    passiveContactIntent.SourceId,
                    passiveContactIntent.Priority,
                    passiveContactIntent.TargetId,
                    AttackSourceKind.PassiveContact,
                    localSequence: 1));
            }
        }

        private bool TryGetAiControlledEnemy(WorldSnapshot snapshot, out EntityState source)
        {
            if (!EnemyParticipationPolicy.TryGetEnemyLogicEntity(snapshot, _entityId, out source))
            {
                return false;
            }

            return EnemyParticipationPolicy.CanParticipateOnCurrentTopology(snapshot, source);
        }

        private bool ShouldSuppressMovementForJump(WorldSnapshot snapshot, int tickIndex)
        {
            if (!TryGetJumpState(snapshot, out var jumpState))
            {
                return false;
            }

            return jumpState.phase == EnemyJumpPhase.Windup ||
                   jumpState.phase == EnemyJumpPhase.Airborne ||
                   (jumpState.phase == EnemyJumpPhase.Cooldown &&
                    jumpState.landingTick == tickIndex);
        }

        private bool ShouldSuppressCombatAttackForJump(WorldSnapshot snapshot)
        {
            if (!TryGetJumpState(snapshot, out var jumpState))
            {
                return false;
            }

            return jumpState.phase == EnemyJumpPhase.Windup ||
                   jumpState.phase == EnemyJumpPhase.Airborne;
        }

        private bool TryGetJumpState(WorldSnapshot snapshot, out EnemyJumpRuntimeState jumpState)
        {
            jumpState = default;
            return HasJumpMovementSkill() &&
                   snapshot.TryGetEnemyJumpState(_entityId, out jumpState);
        }

        private bool TryGetChargeState(WorldSnapshot snapshot, out EnemyChargeRuntimeState chargeState)
        {
            chargeState = default;
            return _usesChargeStateResolver &&
                   snapshot != null &&
                   TryGetChargeStateAuthoritative(snapshot, _entityId, out chargeState);
        }

        private static bool TryGetChargeStateAuthoritative(
            WorldSnapshot snapshot,
            int entityId,
            out EnemyChargeRuntimeState chargeState)
        {
            return snapshot.TryGetEnemyChargeState(entityId, out chargeState) &&
                   chargeState.phase != EnemyChargePhase.None;
        }

        private bool TryResolveActiveChargeDirection(
            WorldSnapshot snapshot,
            in EntityState source,
            out Direction direction)
        {
            direction = Direction.None;
            if (!_usesChargeStateResolver)
            {
                return false;
            }

            if (TryGetChargeState(snapshot, out var chargeState) &&
                chargeState.phase == EnemyChargePhase.Active &&
                chargeState.lockedDirection != Direction.None)
            {
                direction = chargeState.lockedDirection;
                return true;
            }

            return false;
        }

        private bool CanCollectPassiveContact(WorldSnapshot snapshot)
        {
            if (!_usesChargeStateResolver)
            {
                return true;
            }

            return TryGetChargeState(snapshot, out var chargeState) &&
                   chargeState.phase == EnemyChargePhase.Active;
        }

        private bool TryGetEnemyOwnedPhasedState(WorldSnapshot snapshot, out PhasedRuntimeState phasedState)
        {
            phasedState = default;
            return snapshot != null &&
                   snapshot.TryGetPhasedState(_entityId, out phasedState) &&
                   phasedState.IsActive &&
                   phasedState.ownerKind == PhasedRuntimeStateOwnerKind.EnemyPreMovement;
        }

        private bool HasJumpMovementSkill()
        {
            return _movementSkillCapability != null &&
                   _movementSkillCapability.Kind == MovementSkillStrategyKind.JumpToLockedTarget;
        }

        private bool HasPhaseMovementSkill()
        {
            return _movementSkillCapability != null &&
                   _movementSkillCapability.Kind == MovementSkillStrategyKind.PhaseThroughLockedTarget;
        }

        private bool HasGlideMovementSkill()
        {
            return _movementSkillCapability != null &&
                   _movementSkillCapability.Kind == MovementSkillStrategyKind.GlideOverSolid;
        }

        private void CommitGlideState(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            var hasPreviousState = snapshot.TryGetEnemyGlideState(_entityId, out var previousState);
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
                    AppendGlideUpdate(updates, _entityId, "ClearDead", nextState);
                    AppendGlideStateDebug(updates, snapshot, input.TickIndex, source, "ClearDead", nextState);
                }

                if (changed)
                {
                    writeContext.SetEnemyGlideState(_entityId, nextState);
                }

                return;
            }

            changed |= TryAdvanceGlideLifecycle(
                snapshot,
                in input,
                source,
                ref hasPreviousState,
                ref nextState,
                updates);

            if (source.aiMode == EnemyAiMode.Chase &&
                EnemyGlideQueries.CanStart(hasPreviousState, nextState, input.TickIndex))
            {
                nextState = EnemyGlideQueries.Start(
                    nextState,
                    input.TickIndex,
                    _movementSkillCapability.GlideTimingSettings);
                hasPreviousState = true;
                changed = true;
                AppendGlideUpdate(updates, _entityId, "Start", nextState);
                changed |= TryAdvanceGlideLifecycle(
                    snapshot,
                    in input,
                    source,
                    ref hasPreviousState,
                    ref nextState,
                    updates);
            }

            if (changed)
            {
                writeContext.SetEnemyGlideState(_entityId, nextState);
            }
        }

        private bool TryAdvanceGlideLifecycle(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
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

                        nextState = EnemyGlideQueries.BeginActive(nextState, input.TickIndex);
                        hasPreviousState = true;
                        changed = true;
                        AppendGlideUpdate(updates, _entityId, "EnterActive", nextState);
                        AppendGlideStateDebug(updates, snapshot, input.TickIndex, source, "EnterActive", nextState);
                        continue;

                    case EnemyGlidePhase.Active:
                        if (input.TickIndex < nextState.ActiveUntilTickExclusive)
                        {
                            return changed;
                        }

                        var sourcePositionIsSolid = snapshot.TryGetSolidSemanticAt(source.position, out _);
                        var hasSolidBoundTerminal = TryResolveSolidBoundGlideKinematicTerminal(
                            snapshot,
                            source.entityId,
                            nextState,
                            out var pendingCell);
                        if (sourcePositionIsSolid || hasSolidBoundTerminal)
                        {
                            nextState = EnemyGlideQueries.EndActiveToLandingPending(
                                nextState,
                                input.TickIndex,
                                sourcePositionIsSolid ? source.position : pendingCell);
                        }
                        else
                        {
                            nextState = EnemyGlideQueries.BeginRecovery(nextState, input.TickIndex);
                        }

                        hasPreviousState = true;
                        changed = true;
                        AppendGlideUpdate(
                            updates,
                            _entityId,
                            nextState.IsLandingPending ? "EnterLandingPending" : "EnterRecovery",
                            nextState);
                        AppendGlideStateDebug(
                            updates,
                            snapshot,
                            input.TickIndex,
                            source,
                            nextState.IsLandingPending ? "EnterLandingPending" : "EnterRecovery",
                            nextState);
                        continue;

                    case EnemyGlidePhase.LandingPending:
                        if (snapshot.TryGetSolidSemanticAt(source.position, out _) ||
                            HasUnsettledVoluntaryKinematicPose(snapshot, source.entityId))
                        {
                            return changed;
                        }

                        nextState = EnemyGlideQueries.BeginRecovery(nextState, input.TickIndex);
                        hasPreviousState = true;
                        changed = true;
                        AppendGlideUpdate(updates, _entityId, "EnterRecovery", nextState);
                        AppendGlideStateDebug(updates, snapshot, input.TickIndex, source, "EnterRecovery", nextState);
                        continue;

                    case EnemyGlidePhase.Recovery:
                        if (input.TickIndex < nextState.RecoveryUntilTickExclusive)
                        {
                            return changed;
                        }

                        nextState = EnemyGlideQueries.EndRecoveryToCooldown(nextState, input.TickIndex);
                        hasPreviousState = true;
                        changed = true;
                        AppendGlideUpdate(updates, _entityId, "EnterCooldown", nextState);
                        AppendGlideStateDebug(updates, snapshot, input.TickIndex, source, "EnterCooldown", nextState);
                        continue;

                    case EnemyGlidePhase.Cooldown:
                        if (input.TickIndex >= nextState.CooldownUntilTickExclusive &&
                            input.TickIndex > nextState.LastExitedTick &&
                            source.aiMode != EnemyAiMode.Chase)
                        {
                            nextState = EnemyGlideQueries.Clear();
                            hasPreviousState = false;
                            changed = true;
                            AppendGlideUpdate(updates, _entityId, "Ready", nextState);
                            AppendGlideStateDebug(updates, snapshot, input.TickIndex, source, "Ready", nextState);
                        }

                        return changed;

                    case EnemyGlidePhase.Ready:
                    default:
                        return changed;
                }
            }

            return changed;
        }

        private void CommitEnemyOwnedPhasedState(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            if (writeContext is not IPhasedStateCommitContext phasedWriteContext)
            {
                throw new InvalidOperationException("Pre-movement write contexts must support phased runtime writes.");
            }

            var hasCurrentPhasedState = snapshot.TryGetPhasedState(_entityId, out var currentPhasedState) &&
                                        currentPhasedState.IsActive;
            var ownsEnemyPreMovementPhase = hasCurrentPhasedState &&
                                            currentPhasedState.ownerKind == PhasedRuntimeStateOwnerKind.EnemyPreMovement;
            var shouldOwnEnemyPreMovementPhase = ShouldOwnEnemyPreMovementPhase(snapshot, source, input.TickIndex);

            if (shouldOwnEnemyPreMovementPhase)
            {
                if (hasCurrentPhasedState &&
                    !ownsEnemyPreMovementPhase)
                {
                    throw new InvalidOperationException(
                        $"Entity {_entityId} cannot enter enemy-owned phased state while owner {currentPhasedState.ownerKind} is still active.");
                }

                if (ownsEnemyPreMovementPhase)
                {
                    return;
                }

                phasedWriteContext.SetPhasedState(
                    _entityId,
                    PhasedRuntimeStateQueries.BeginEnemyPreMovement(default, input.TickIndex));
                updates.Add(
                    $"PhaseEnter|Entity={_entityId}|Tick={input.TickIndex}|Owner={PhasedRuntimeStateOwnerKind.EnemyPreMovement}|Rule={EnemyPhaseThroughLockedTargetQueries.RuleLabel}");
                return;
            }

            if (!ownsEnemyPreMovementPhase)
            {
                return;
            }

            phasedWriteContext.SetPhasedState(_entityId, PhasedRuntimeStateQueries.Clear());
            updates.Add(
                $"PhaseExit|Entity={_entityId}|Tick={input.TickIndex}|Owner={PhasedRuntimeStateOwnerKind.EnemyPreMovement}|Reason=LockedTargetCrossThroughWindowClosed");
        }

        private bool ShouldOwnEnemyPreMovementPhase(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex)
        {
            if (!HasPhaseMovementSkill() ||
                _combatCapability == null ||
                source.hp <= 0 ||
                source.markedForDeath ||
                source.aiMode == EnemyAiMode.Dead ||
                source.boardPresence != EntityBoardPresence.Occupying ||
                !snapshot.TryGetEnemyActionState(_entityId, out var actionState) ||
                !EnemyActionQueries.CanExecute(actionState, tickIndex))
            {
                return false;
            }

            return EnemyPhaseThroughLockedTargetQueries.TryResolveValidatorWindow(
                snapshot,
                source,
                actionState,
                _combatCapability.AttackDecisionStrategy,
                _detectionSettings,
                _combatCapability.AttackDecisionSettings,
                out _,
                out _);
        }

        private bool ShouldSuppressMovementForEnemyPhase(WorldSnapshot snapshot)
        {
            return HasPhaseMovementSkill() &&
                   TryGetEnemyOwnedPhasedState(snapshot, out _);
        }

        private bool ShouldSuppressMovementForGlide(WorldSnapshot snapshot)
        {
            if (!HasGlideMovementSkill() ||
                !snapshot.TryGetEnemyGlideState(_entityId, out var glideState))
            {
                return false;
            }

            return glideState.Phase == EnemyGlidePhase.Windup ||
                   glideState.Phase == EnemyGlidePhase.Recovery;
        }

        private bool TryResolveLandingPendingEgressLocomotion(
            WorldSnapshot snapshot,
            in EntityState source,
            out GroundLocomotionResolution locomotion)
        {
            locomotion = default;
            if (!HasGlideMovementSkill() ||
                source.aiMode != EnemyAiMode.Chase ||
                !snapshot.TryGetEnemyGlideState(_entityId, out var glideState) ||
                glideState.Phase != EnemyGlidePhase.LandingPending)
            {
                return false;
            }

            var sourceIsSolid = snapshot.TryGetSolidSemanticAt(source.position, out _);
            var pendingCellIsSolid = snapshot.TryGetSolidSemanticAt(glideState.LandingPendingCell, out _);
            if (!sourceIsSolid && !pendingCellIsSolid)
            {
                return false;
            }

            if (!_detectionStrategy.TryFindTarget(snapshot, source, _detectionSettings, out var chaseTarget))
            {
                return false;
            }

            if (!_chaseStrategy.TryBuildMovementIntent(
                    snapshot,
                    source,
                    chaseTarget,
                    _commonSettings,
                    _chaseSettings,
                    out var egressIntent) &&
                !TryBuildLandingPendingEgressIntent(snapshot, source, chaseTarget, out egressIntent))
            {
                return false;
            }

            var destination = new SurfaceCell(source.position.face, egressIntent.Destination.x, egressIntent.Destination.y);
            if (snapshot.TryGetSolidSemanticAt(destination, out _))
            {
                return false;
            }

            locomotion = new GroundLocomotionResolution(
                hasIntent: true,
                egressIntent,
                _locomotionTimingSettings.MoveCooldownTicks,
                _locomotionTimingSettings.OrdinaryKinematicMoveTicks);
            return true;
        }

        private bool TryBuildLandingPendingEgressIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            out RawMovementIntent intent)
        {
            intent = default;
            if (source.position.face != target.position.face)
            {
                return false;
            }

            var planarDelta = target.position - source.position;
            var horizontalStep = planarDelta.x == 0
                ? (Vector2Int?)null
                : new Vector2Int(Math.Sign(planarDelta.x), 0);
            var verticalStep = planarDelta.y == 0
                ? (Vector2Int?)null
                : new Vector2Int(0, Math.Sign(planarDelta.y));
            var tryHorizontalFirst = Math.Abs(planarDelta.x) >= Math.Abs(planarDelta.y);

            if (tryHorizontalFirst)
            {
                return TryBuildLandingPendingEgressIntentForStep(snapshot, source, horizontalStep, out intent) ||
                       TryBuildLandingPendingEgressIntentForStep(snapshot, source, verticalStep, out intent);
            }

            return TryBuildLandingPendingEgressIntentForStep(snapshot, source, verticalStep, out intent) ||
                   TryBuildLandingPendingEgressIntentForStep(snapshot, source, horizontalStep, out intent);
        }

        private bool TryBuildLandingPendingEgressIntentForStep(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int? step,
            out RawMovementIntent intent)
        {
            intent = default;
            if (!step.HasValue ||
                !snapshot.TryResolveUnitStep(
                    source.position,
                    step.Value,
                    out var destination,
                    out var rotationKind,
                    out _) ||
                rotationKind != CubeRotationKind.None ||
                destination.face != source.position.face ||
                snapshot.TryGetSolidSemanticAt(destination, out _) ||
                !IsLandingPendingEgressUnitDestinationAllowed(snapshot, source, destination))
            {
                return false;
            }

            intent = new RawMovementIntent(
                source.entityId,
                _commonSettings.MovementPriority,
                destination.PlanarPosition);
            return true;
        }

        private static bool IsLandingPendingEgressUnitDestinationAllowed(
            WorldSnapshot snapshot,
            in EntityState source,
            SurfaceCell destination)
        {
            var occupants = new List<EntityState>();
            snapshot.EnumerateUnitsAt(destination, occupants);
            for (var i = 0; i < occupants.Count; i++)
            {
                var occupant = occupants[i];
                if (occupant.entityId == source.entityId ||
                    occupant.boardPresence != EntityBoardPresence.Occupying ||
                    occupant.hp <= 0 ||
                    occupant.markedForDeath)
                {
                    continue;
                }

                if (occupant.teamId == source.teamId ||
                    !EntityRolePolicy.IsPlayerUnit(occupant))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryResolveSolidBoundGlideKinematicTerminal(
            WorldSnapshot snapshot,
            int entityId,
            in EnemyGlideRuntimeState glideState,
            out SurfaceCell terminalCell)
        {
            if (!TryResolveUnsettledGlideKinematicTerminal(snapshot, entityId, glideState, out terminalCell))
            {
                return false;
            }

            return snapshot.TryGetSolidSemanticAt(terminalCell, out _);
        }

        private static bool TryResolveUnsettledGlideKinematicTerminal(
            WorldSnapshot snapshot,
            int entityId,
            in EnemyGlideRuntimeState glideState,
            out SurfaceCell terminalCell)
        {
            terminalCell = default;
            if (!snapshot.TryGetUnitKinematicPose(entityId, out var pose) ||
                !pose.HasAuthoritativeState ||
                pose.IsSettledAtAnchor ||
                pose.Mode != MotionMode.Voluntary ||
                !TryResolveGlideStepDirection(pose.State, out var stepDirection) ||
                !IsGlideKinematicStartedDuringActiveWindow(pose.State, glideState))
            {
                return false;
            }

            terminalCell = pose.State.commitTick > 0 && pose.State.elapsedTicks >= pose.State.commitTick
                ? pose.AnchorCell
                : pose.AnchorCell + stepDirection;
            return true;
        }

        private static bool HasUnsettledVoluntaryKinematicPose(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetUnitKinematicPose(entityId, out var pose) &&
                   pose.HasAuthoritativeState &&
                   !pose.IsSettledAtAnchor &&
                   pose.Mode == MotionMode.Voluntary;
        }

        private static bool IsGlideKinematicStartedDuringActiveWindow(
            in UnitKinematicRuntimeState state,
            in EnemyGlideRuntimeState glideState)
        {
            if (glideState.ActiveUntilTickExclusive <= 0 ||
                glideState.DurationTicks <= 0)
            {
                return false;
            }

            var activeStartTick = glideState.ActiveUntilTickExclusive - glideState.DurationTicks;
            return state.startedTick >= activeStartTick &&
                   state.startedTick < glideState.ActiveUntilTickExclusive;
        }

        private static bool TryResolveGlideStepDirection(
            in UnitKinematicRuntimeState state,
            out Vector2Int stepDirection)
        {
            stepDirection = new Vector2Int(state.stepDirectionX, state.stepDirectionY);
            return Math.Abs(stepDirection.x) + Math.Abs(stepDirection.y) == 1;
        }

        private bool ShouldSuppressAttackForEnemyPhase(WorldSnapshot snapshot, int tickIndex)
        {
            if (!HasPhaseMovementSkill())
            {
                return false;
            }

            if (TryGetEnemyOwnedPhasedState(snapshot, out _))
            {
                return true;
            }

            // The phase-through validator stays relocation-only: the execute window never falls back into same-tick combat.
            return snapshot.TryGetEnemyActionState(_entityId, out var actionState) &&
                   actionState.IsActive &&
                   EnemyActionQueries.CanExecute(actionState, tickIndex);
        }

        private bool TryResolvePassiveContactTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            out EntityState target)
        {
            target = default;
            _sharedCellUnits.Clear();
            snapshot.EnumerateUnitsAt(source.position, _sharedCellUnits);

            for (var i = 0; i < _sharedCellUnits.Count; i++)
            {
                var candidate = _sharedCellUnits[i];
                if (candidate.entityId == source.entityId ||
                    candidate.teamId == source.teamId ||
                    !EntityRolePolicy.IsPlayerUnit(candidate) ||
                    candidate.hp <= 0 ||
                    candidate.markedForDeath)
                {
                    continue;
                }

                target = candidate;
                return true;
            }

            return false;
        }

        private bool TryGetControllableEnemy(WorldSnapshot snapshot, out EntityState source)
        {
            if (!EnemyParticipationPolicy.TryGetEnemyLogicEntity(snapshot, _entityId, out source))
            {
                return false;
            }

            return EnemyParticipationPolicy.IsControllableParticipant(snapshot, source);
        }

        private void CommitEnemyUtilityState(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            var isControllableParticipant = EnemyParticipationPolicy.IsControllableParticipant(snapshot, source);
            var hasCurrentState = snapshot.TryGetEnemyUtilityState(_entityId, out var currentState);
            var initializedState = false;
            if ((!hasCurrentState || !currentState.HasEffectCount(_utilityCapability.Effects.Count)) &&
                isControllableParticipant)
            {
                currentState = EnemyUtilityStateQueries.CreateInitialState(_utilityCapability);
                hasCurrentState = true;
                initializedState = true;
                updates.Add(
                    $"EnemyUtilityInitialized|E={_entityId}|EffectCount={currentState.EffectStates.Count}");
            }

            if (!hasCurrentState)
            {
                return;
            }

            if (!isControllableParticipant)
            {
                CancelEnemyUtilityWindups(currentState, writeContext, updates);
                return;
            }

            var nextEffectStates = new EnemyUtilityEffectState[currentState.EffectStates.Count];
            var hasAnyChange = false;

            for (var effectIndex = 0; effectIndex < currentState.EffectStates.Count; effectIndex++)
            {
                var previousEffectState = currentState.EffectStates[effectIndex];
                var nextEffectState = previousEffectState;
                var effectRuntime = _utilityCapability.Effects[effectIndex];
                var triggered = false;

                if (effectRuntime.Kind == EnemyUtilityEffectKind.SummonMinion)
                {
                    if (nextEffectState.phase == EnemyUtilityEffectPhase.Windup)
                    {
                        if (input.TickIndex >= nextEffectState.windupEndTick)
                        {
                            nextEffectState.phase = EnemyUtilityEffectPhase.None;
                            nextEffectState.windupStartTick = 0;
                            nextEffectState.windupEndTick = 0;
                            nextEffectState.cooldownTicksRemaining = effectRuntime.CooldownTicks;
                            triggered = true;
                            if (writeContext is IEnemyUtilityTriggerSink triggerSink)
                            {
                                triggerSink.EmitEnemyUtilityTriggerIntent(
                                    new EnemyUtilityTriggerIntent(
                                        _entityId,
                                        effectIndex,
                                        effectRuntime.Kind,
                                        input.TickIndex,
                                        effectRuntime));
                            }

                            updates.Add(
                                $"EnemyUtilityWindupCommitted|E={_entityId}|Effect={effectIndex}|Sequence={nextEffectState.activationSequence}|Tick={input.TickIndex}");
                        }
                    }
                    else
                    {
                        if (nextEffectState.cooldownTicksRemaining > 0)
                        {
                            nextEffectState.cooldownTicksRemaining = Mathf.Max(0, nextEffectState.cooldownTicksRemaining - 1);
                        }

                        if (nextEffectState.cooldownTicksRemaining == 0)
                        {
                            nextEffectState.phase = EnemyUtilityEffectPhase.Windup;
                            nextEffectState.windupStartTick = input.TickIndex;
                            nextEffectState.windupEndTick = input.TickIndex + effectRuntime.Summon.WindupTicks;
                            nextEffectState.activationSequence = Math.Max(0, nextEffectState.activationSequence) + 1;
                            updates.Add(
                                $"EnemyUtilityWindupStarted|E={_entityId}|Effect={effectIndex}|Sequence={nextEffectState.activationSequence}|Start={nextEffectState.windupStartTick}|End={nextEffectState.windupEndTick}");
                        }
                    }

                    nextEffectStates[effectIndex] = nextEffectState;
                    if (!AreEqual(previousEffectState, nextEffectState))
                    {
                        hasAnyChange = true;
                        updates.Add(
                            $"EnemyUtilityCooldownUpdated|E={_entityId}|Effect={effectIndex}|From={previousEffectState.cooldownTicksRemaining}|To={nextEffectState.cooldownTicksRemaining}|Triggered={(triggered ? 1 : 0)}");
                    }

                    continue;
                }

                if (nextEffectState.cooldownTicksRemaining > 0)
                {
                    nextEffectState.cooldownTicksRemaining = Mathf.Max(0, nextEffectState.cooldownTicksRemaining - 1);
                }

                triggered = nextEffectState.cooldownTicksRemaining == 0;
                if (triggered)
                {
                    nextEffectState.cooldownTicksRemaining = effectRuntime.CooldownTicks;
                    if (writeContext is IEnemyUtilityTriggerSink triggerSink)
                    {
                        triggerSink.EmitEnemyUtilityTriggerIntent(
                            new EnemyUtilityTriggerIntent(
                                _entityId,
                                effectIndex,
                                effectRuntime.Kind,
                                input.TickIndex,
                                effectRuntime));
                    }
                }

                nextEffectStates[effectIndex] = nextEffectState;
                if (!AreEqual(previousEffectState, nextEffectState))
                {
                    hasAnyChange = true;
                    updates.Add(
                        $"EnemyUtilityCooldownUpdated|E={_entityId}|Effect={effectIndex}|From={previousEffectState.cooldownTicksRemaining}|To={nextEffectState.cooldownTicksRemaining}|Triggered={(triggered ? 1 : 0)}");
                }
            }

            if (!initializedState &&
                !hasAnyChange)
            {
                return;
            }

            writeContext.SetEnemyUtilityState(_entityId, new EnemyUtilityRuntimeState(nextEffectStates));
        }

        private void CancelEnemyUtilityWindups(
            EnemyUtilityRuntimeState currentState,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            if (currentState == null)
            {
                return;
            }

            var nextEffectStates = new EnemyUtilityEffectState[currentState.EffectStates.Count];
            var hasAnyChange = false;
            for (var effectIndex = 0; effectIndex < currentState.EffectStates.Count; effectIndex++)
            {
                var previousEffectState = currentState.EffectStates[effectIndex];
                var nextEffectState = previousEffectState;
                if (nextEffectState.phase == EnemyUtilityEffectPhase.Windup)
                {
                    var cooldownTicks = effectIndex < _utilityCapability.Effects.Count
                        ? _utilityCapability.Effects[effectIndex].CooldownTicks
                        : nextEffectState.cooldownTicksRemaining;

                    nextEffectState.phase = EnemyUtilityEffectPhase.None;
                    nextEffectState.cooldownTicksRemaining = cooldownTicks;
                    nextEffectState.windupStartTick = 0;
                    nextEffectState.windupEndTick = 0;
                    hasAnyChange = true;
                    updates.Add(
                        $"EnemyUtilityWindupCanceled|E={_entityId}|Effect={effectIndex}|Sequence={nextEffectState.activationSequence}|Cooldown={nextEffectState.cooldownTicksRemaining}");
                }

                nextEffectStates[effectIndex] = nextEffectState;
            }

            if (hasAnyChange)
            {
                writeContext.SetEnemyUtilityState(_entityId, new EnemyUtilityRuntimeState(nextEffectStates));
            }
        }

        private void CommitEnemyFrontFaceSupportState(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            var isEligibleSource = EnemyFrontFaceSupportPolicy.IsActiveFrontFaceSupportSource(snapshot, source);
            var hasCurrentState = snapshot.TryGetEnemyFrontFaceSupportState(_entityId, out var currentState);
            var initializedState = false;
            if ((!hasCurrentState || !currentState.HasEffectCount(_frontFaceSupportCapability.Effects.Count)) &&
                isEligibleSource)
            {
                currentState = EnemyFrontFaceSupportStateQueries.CreateInitialState(_frontFaceSupportCapability);
                hasCurrentState = true;
                initializedState = true;
                updates.Add(
                    $"EnemyFrontFaceSupportInitialized|E={_entityId}|EffectCount={currentState.EffectStates.Count}");
            }

            if (!hasCurrentState)
            {
                return;
            }

            var nextEffectStates = new EnemyFrontFaceSupportEffectState[currentState.EffectStates.Count];
            var hasAnyChange = false;

            for (var effectIndex = 0; effectIndex < currentState.EffectStates.Count; effectIndex++)
            {
                var previousEffectState = currentState.EffectStates[effectIndex];
                var effectRuntime = _frontFaceSupportCapability.Effects[effectIndex];
                var nextEffectState = previousEffectState;

                if (!isEligibleSource)
                {
                    var canceledWindup = previousEffectState.phase == EnemyFrontFaceSupportEffectPhase.Windup;
                    var cooldownTicksRemaining = canceledWindup && effectRuntime.Kind == EnemyFrontFaceSupportEffectKind.BoxSlideShield
                        ? effectRuntime.BoxSlideShield.CooldownTicks
                        : Mathf.Max(0, previousEffectState.cooldownTicksRemaining - 1);

                    nextEffectState = EnemyFrontFaceSupportStateQueries.CreateInactiveEffectState(
                        effectRuntime,
                        previousEffectState.activationSequence);
                    nextEffectState.cooldownTicksRemaining = cooldownTicksRemaining;
                    if (!AreEqual(previousEffectState, nextEffectState))
                    {
                        updates.Add(
                            $"EnemyFrontFaceSupportCleared|E={_entityId}|Effect={effectIndex}|PreviousPhase={previousEffectState.phase}|Cooldown={nextEffectState.cooldownTicksRemaining}");
                    }
                }
                else if (effectRuntime.Kind == EnemyFrontFaceSupportEffectKind.BoxSlideShield)
                {
                    var shield = effectRuntime.BoxSlideShield;
                    nextEffectState.radius = shield.Radius;
                    nextEffectState.includeSourceCell = shield.IncludeSourceCell;
                    nextEffectState.targetPattern = shield.TargetPattern;

                    if (nextEffectState.phase == EnemyFrontFaceSupportEffectPhase.None)
                    {
                        if (nextEffectState.cooldownTicksRemaining > 0)
                        {
                            nextEffectState.cooldownTicksRemaining = Mathf.Max(0, nextEffectState.cooldownTicksRemaining - 1);
                        }

                        if (nextEffectState.cooldownTicksRemaining == 0)
                        {
                            nextEffectState.phase = EnemyFrontFaceSupportEffectPhase.Windup;
                            nextEffectState.windupStartTick = input.TickIndex;
                            nextEffectState.windupEndTick = input.TickIndex + shield.WindupTicks;
                            nextEffectState.activationSequence = Math.Max(0, nextEffectState.activationSequence) + 1;
                            updates.Add(
                                $"EnemyFrontFaceSupportWindupStarted|E={_entityId}|Effect={effectIndex}|Sequence={nextEffectState.activationSequence}|Start={nextEffectState.windupStartTick}|End={nextEffectState.windupEndTick}");
                        }
                    }
                    else if (nextEffectState.phase == EnemyFrontFaceSupportEffectPhase.Windup &&
                             input.TickIndex >= nextEffectState.windupEndTick)
                    {
                        nextEffectState.phase = EnemyFrontFaceSupportEffectPhase.Active;
                        nextEffectState.cooldownTicksRemaining = shield.CooldownTicks;
                        updates.Add(
                            $"EnemyFrontFaceSupportActivated|E={_entityId}|Effect={effectIndex}|Sequence={nextEffectState.activationSequence}|Tick={input.TickIndex}|Cooldown={nextEffectState.cooldownTicksRemaining}");
                    }
                    else if (nextEffectState.phase == EnemyFrontFaceSupportEffectPhase.Active &&
                             nextEffectState.cooldownTicksRemaining > 0)
                    {
                        nextEffectState.cooldownTicksRemaining = Mathf.Max(0, nextEffectState.cooldownTicksRemaining - 1);
                    }
                }

                nextEffectStates[effectIndex] = nextEffectState;
                if (!AreEqual(previousEffectState, nextEffectState))
                {
                    hasAnyChange = true;
                }
            }

            if (!initializedState &&
                !hasAnyChange)
            {
                return;
            }

            writeContext.SetEnemyFrontFaceSupportState(
                _entityId,
                new EnemyFrontFaceSupportRuntimeState(nextEffectStates));
        }

        private static bool AreEqual(EnemyUtilityEffectState left, EnemyUtilityEffectState right)
        {
            return left.cooldownTicksRemaining == right.cooldownTicksRemaining &&
                   left.phase == right.phase &&
                   left.windupStartTick == right.windupStartTick &&
                   left.windupEndTick == right.windupEndTick &&
                   left.activationSequence == right.activationSequence;
        }

        private static bool AreEqual(EnemyFrontFaceSupportEffectState left, EnemyFrontFaceSupportEffectState right)
        {
            return left.phase == right.phase &&
                   left.windupStartTick == right.windupStartTick &&
                   left.windupEndTick == right.windupEndTick &&
                   left.activationSequence == right.activationSequence &&
                   left.cooldownTicksRemaining == right.cooldownTicksRemaining &&
                   left.radius == right.radius &&
                   left.includeSourceCell == right.includeSourceCell &&
                   left.targetPattern == right.targetPattern;
        }

        private static RawMovementIntent ApplyMovementTiming(
            RawMovementIntent intent,
            int cooldownTicks,
            int ordinaryKinematicMoveTicks)
        {
            return new RawMovementIntent(
                intent.SourceId,
                intent.Priority,
                intent.Destination,
                intent.CommandKind,
                intent.LocalSequence,
                cooldownTicks,
                ordinaryKinematicMoveTicks);
        }

        private bool ShouldHoldWallFollowForSameCellPassiveContact(
            WorldSnapshot snapshot,
            in EntityState source)
        {
            return _patrolStrategy is WallFollowPatrolStrategy &&
                   _passiveContactCapability != null &&
                   TryResolvePassiveContactTarget(snapshot, source, out _);
        }

        private bool CommitJumpState(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
            IPreMovementStateCommitContext stateWriteContext,
            IEnemyJumpCommitContext jumpWriteContext,
            List<string> updates)
        {
            var hasPreviousState = snapshot.TryGetEnemyJumpState(_entityId, out var previousState);
            var nextState = previousState;
            var suppressMovementThisTick = false;

            if (source.hp <= 0 ||
                source.markedForDeath ||
                source.aiMode == EnemyAiMode.Dead)
            {
                if (ShouldWriteJumpState(hasPreviousState, previousState, EnemyJumpQueries.Clear(previousState)))
                {
                    nextState = EnemyJumpQueries.Clear(previousState);
                    stateWriteContext.SetEnemyJumpState(_entityId, nextState);
                    AppendJumpUpdate(updates, _entityId, "ClearDead", nextState);
                }

                return false;
            }

            if (!hasPreviousState || previousState.phase == EnemyJumpPhase.None)
            {
                if (TryResolveScheduledJumpStart(
                        snapshot,
                        source,
                        hasPreviousState ? previousState : default,
                        input.TickIndex,
                        out nextState))
                {
                    var jumpFacing = EnemyJumpQueries.ResolveJumpBasisFacing(
                        nextState.sourceCell,
                        nextState.lockedTargetCell,
                        source.facing,
                        _chaseSettings.AxisPriority);
                    stateWriteContext.SetFacing(_entityId, jumpFacing);
                    suppressMovementThisTick = true;
                    AppendJumpUpdate(updates, _entityId, "Start", nextState, $"Facing={jumpFacing}");
                }
            }

            if (nextState.phase == EnemyJumpPhase.Windup)
            {
                suppressMovementThisTick = true;

                if (input.TickIndex >= nextState.windupEndTick)
                {
                    jumpWriteContext.SetEnemyJumpBoardPresence(_entityId, EntityBoardPresence.Detached);
                    nextState = EnemyJumpQueries.BeginAirborne(nextState);
                    AppendJumpUpdate(updates, _entityId, "Takeoff", nextState);
                }
            }

            if (nextState.phase == EnemyJumpPhase.Airborne)
            {
                suppressMovementThisTick = true;
            }
            else if (nextState.phase == EnemyJumpPhase.Cooldown)
            {
                var cooledState = EnemyJumpQueries.TickCooldown(nextState);
                if (!AreEqual(nextState, cooledState))
                {
                    nextState = cooledState;
                    AppendJumpUpdate(
                        updates,
                        _entityId,
                        nextState.phase == EnemyJumpPhase.None ? "CooldownComplete" : "CooldownTick",
                        nextState);
                }

            }

            if (ShouldWriteJumpState(hasPreviousState, previousState, nextState))
            {
                stateWriteContext.SetEnemyJumpState(_entityId, nextState);
            }

            return suppressMovementThisTick;
        }

        private void CommitChargeState(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
            IPreMovementStateCommitContext stateWriteContext,
            List<string> updates)
        {
            var hasPreviousState = snapshot.TryGetEnemyChargeState(_entityId, out var previousState);
            var nextState = hasPreviousState ? previousState : default;

            if (source.hp <= 0 ||
                source.markedForDeath ||
                source.aiMode == EnemyAiMode.Dead ||
                source.boardPresence != EntityBoardPresence.Occupying)
            {
                if (hasPreviousState &&
                    previousState.phase != EnemyChargePhase.None &&
                    ShouldWriteChargeState(hasPreviousState, previousState, EnemyChargeQueries.Clear(previousState)))
                {
                    nextState = EnemyChargeQueries.Clear(previousState);
                    stateWriteContext.SetEnemyChargeState(_entityId, nextState);
                    AppendChargeUpdate(updates, _entityId, "ClearDead", nextState);
                }

                return;
            }

            switch (source.aiMode)
            {
                case EnemyAiMode.Charge:
                    if (!hasPreviousState ||
                        previousState.phase == EnemyChargePhase.None ||
                        previousState.phase == EnemyChargePhase.Recover)
                    {
                        if (TryBuildChargeStartState(snapshot, in input, source, hasPreviousState ? previousState : default, out nextState))
                        {
                            AppendChargeUpdate(updates, _entityId, "Start", nextState);
                        }
                        else if (hasPreviousState && previousState.phase != EnemyChargePhase.None)
                        {
                            nextState = EnemyChargeQueries.Clear(previousState);
                            AppendChargeUpdate(updates, _entityId, "ClearInvalidStart", nextState);
                        }

                        break;
                    }

                    if (previousState.phase == EnemyChargePhase.Windup)
                    {
                        if (input.TickIndex >= previousState.windupEndTick &&
                            EnemyChargeStrategyShared.CanAdvanceChargeStep(snapshot, source, previousState.lockedDirection))
                        {
                            nextState = EnemyChargeQueries.BeginActive(previousState);
                            AppendChargeUpdate(updates, _entityId, "BeginActive", nextState);
                        }

                        break;
                    }
                    break;

                case EnemyAiMode.Recover:
                    if (hasPreviousState &&
                        previousState.phase == EnemyChargePhase.Recover)
                    {
                        if (previousState.recoverRemainingTicks > 0)
                        {
                            nextState = EnemyChargeQueries.TickRecover(previousState);
                            AppendChargeUpdate(updates, _entityId, "TickRecover", nextState);
                        }
                    }
                    else if (hasPreviousState &&
                             previousState.phase != EnemyChargePhase.None)
                    {
                        nextState = EnemyChargeQueries.EnterRecover(previousState, _chargeTimingSettings.RecoverTicks);
                        AppendChargeUpdate(updates, _entityId, "EnterRecover", nextState);
                    }
                    break;

                default:
                    if (hasPreviousState &&
                        previousState.phase != EnemyChargePhase.None)
                    {
                        nextState = EnemyChargeQueries.Clear(previousState);
                        AppendChargeUpdate(updates, _entityId, "Clear", nextState);
                    }
                    break;
            }

            if (ShouldWriteChargeState(hasPreviousState, previousState, nextState))
            {
                stateWriteContext.SetEnemyChargeState(_entityId, nextState);
            }
        }

        private bool TryBuildChargeStartState(
            WorldSnapshot snapshot,
            in TickInput input,
            in EntityState source,
            in EnemyChargeRuntimeState previousState,
            out EnemyChargeRuntimeState nextState)
        {
            nextState = default;
            if (!_detectionStrategy.TryFindTarget(snapshot, source, _detectionSettings, out var target) ||
                !EnemyChargeStrategyShared.TryResolveChargeStart(snapshot, source, target, out var lockedDirection, out var reachableSteps))
            {
                return false;
            }

            nextState = EnemyChargeQueries.StartCharge(
                previousState,
                lockedDirection,
                input.TickIndex,
                _chargeTimingSettings,
                reachableSteps);
            return true;
        }

        private static bool CanMoveThisTick(in EntityState source)
        {
            return source.enemyLocomotionCooldownTicks <= 1;
        }

        private bool ShouldLogUnchangedChargeDecision(in EntityState source, in EnemyAiTransitionDecision decision)
        {
            return _usesChargeStateResolver &&
                   !string.IsNullOrEmpty(decision.Reason) &&
                   decision.Reason.StartsWith("Charge", StringComparison.Ordinal) &&
                   (source.aiMode == EnemyAiMode.Charge ||
                    source.aiMode == EnemyAiMode.Recover ||
                    decision.Mode == EnemyAiMode.Charge ||
                    decision.Mode == EnemyAiMode.Recover);
        }

        private static bool ShouldDeferChargeStartForOrdinaryKinematic(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiTransitionDecision decision,
            out UnitKinematicPose pose)
        {
            pose = default;
            return source.aiMode != EnemyAiMode.Charge &&
                   decision.Mode == EnemyAiMode.Charge &&
                   string.Equals(decision.Reason, "ChargeStart", StringComparison.Ordinal) &&
                   snapshot.TryGetUnitKinematicPose(source.entityId, out pose) &&
                   pose.HasAuthoritativeState &&
                   !pose.IsSettledAtAnchor &&
                   pose.Mode == MotionMode.Voluntary;
        }

        private static bool ShouldWriteChargeState(
            bool hadPreviousState,
            in EnemyChargeRuntimeState previousState,
            in EnemyChargeRuntimeState nextState)
        {
            return hadPreviousState ||
                   nextState.IsActive ||
                   nextState.sequence != 0 ||
                   !AreEqual(previousState, nextState);
        }

        private static bool AreEqual(
            in EnemyChargeRuntimeState left,
            in EnemyChargeRuntimeState right)
        {
            return left.phase == right.phase &&
                   left.sequence == right.sequence &&
                   left.lockedDirection == right.lockedDirection &&
                   left.windupEndTick == right.windupEndTick &&
                   left.remainingActiveSteps == right.remainingActiveSteps &&
                   left.recoverRemainingTicks == right.recoverRemainingTicks;
        }

        private static void AppendChargeUpdate(
            List<string> updates,
            int entityId,
            string label,
            in EnemyChargeRuntimeState state,
            string extra = null)
        {
            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            var builder = new System.Text.StringBuilder();
            builder
                .Append("EnemyChargeStateUpdated|E=").Append(entityId)
                .Append("|Label=").Append(label ?? string.Empty)
                .Append("|Phase=").Append(state.phase)
                .Append("|Seq=").Append(state.sequence)
                .Append("|Direction=").Append(state.lockedDirection)
                .Append("|WindupEnd=").Append(state.windupEndTick)
                .Append("|ActiveSteps=").Append(state.remainingActiveSteps)
                .Append("|RecoverTicks=").Append(state.recoverRemainingTicks);

            if (!string.IsNullOrEmpty(extra))
            {
                builder.Append('|').Append(extra);
            }

            updates.Add(builder.ToString());
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
                $"EnemyGlideStateUpdated|E={entityId}|Label={label}|Phase={state.Phase}|Active={(state.IsActive ? 1 : 0)}|LandingPending={(state.IsLandingPending ? 1 : 0)}|Seq={state.Sequence}|WindupUntil={state.WindupUntilTickExclusive}|ActiveUntil={state.ActiveUntilTickExclusive}|RecoveryUntil={state.RecoveryUntilTickExclusive}|CooldownUntil={state.CooldownUntilTickExclusive}|Windup={state.WindupTicks}|Duration={state.DurationTicks}|Recovery={state.RecoveryTicks}|Cooldown={state.CooldownTicks}|LastExited={state.LastExitedTick}|PendingCell={state.LandingPendingCell}");
        }

        private void AppendGlideStateDebug(
            List<string> updates,
            WorldSnapshot snapshot,
            int tickIndex,
            in EntityState source,
            string label,
            in EnemyGlideRuntimeState state)
        {
            var targetSummary = ResolveGlideDebugTargetSummary(snapshot, source);
            updates.Add(
                $"EnemyGlideStateDebug|Tick={tickIndex}|E={_entityId}|Label={label}|Phase={state.Phase}|AiMode={source.aiMode}|Pos={source.position}|Facing={source.facing}|LocomotionCooldown={source.enemyLocomotionCooldownTicks}|{targetSummary}");
        }

        private void AppendGlideMoveIntentDebug(
            List<string> debugEvents,
            WorldSnapshot snapshot,
            int tickIndex,
            in EntityState source,
            in EnemyGlideRuntimeState state,
            string result,
            string reason,
            Vector2Int destination,
            bool hasDestination)
        {
            var targetSummary = ResolveGlideDebugTargetSummary(snapshot, source);
            var destinationSuffix = hasDestination
                ? $"|Destination=({destination.x},{destination.y})"
                : string.Empty;
            debugEvents.Add(
                $"EnemyGlideMoveIntentDebug|Tick={tickIndex}|E={_entityId}|Phase={state.Phase}|AiMode={source.aiMode}|Pos={source.position}|Facing={source.facing}|LocomotionCooldown={source.enemyLocomotionCooldownTicks}|{targetSummary}|Result={result}|Reason={reason}{destinationSuffix}");
        }

        private string ResolveGlideMoveIntentDebugReason(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyGlideRuntimeState state,
            int tickIndex,
            IReadOnlyList<RawMovementIntent> rawIntents)
        {
            if (state.Phase != EnemyGlidePhase.Active)
            {
                return "NotActive";
            }

            if (snapshot.TryGetUnitKinematicPose(source.entityId, out var pose) &&
                pose.HasAuthoritativeState &&
                !pose.IsSettledAtAnchor &&
                pose.Mode == MotionMode.Voluntary)
            {
                return "PoseUnsettled";
            }

            if (ShouldSuppressMovementForJump(snapshot, tickIndex))
            {
                return "SuppressedByJump";
            }

            if (ShouldSuppressMovementForEnemyPhase(snapshot))
            {
                return "SuppressedByPhase";
            }

            if (source.enemyLocomotionCooldownTicks > 0)
            {
                return "CooldownBlocked";
            }

            if (source.aiMode != EnemyAiMode.Chase)
            {
                return "NotChase";
            }

            if (!_detectionStrategy.TryFindTarget(snapshot, source, _detectionSettings, out var chaseTarget))
            {
                return "NoTarget";
            }

            if (!_chaseStrategy.TryBuildMovementIntent(
                    snapshot,
                    source,
                    chaseTarget,
                    _commonSettings,
                    _chaseSettings,
                    out _))
            {
                return "ChaseIntentFailed";
            }

            return TryFindMoveIntent(rawIntents, source.entityId, out _)
                ? "IntentCreated"
                : "ChaseIntentFailed";
        }

        private string ResolveGlideDebugTargetSummary(WorldSnapshot snapshot, in EntityState source)
        {
            if (_detectionStrategy.TryFindTarget(snapshot, source, _detectionSettings, out var target))
            {
                return $"HasTarget=1|TargetId={target.entityId}|TargetCell={target.position}";
            }

            return "HasTarget=0|TargetId=-1|TargetCell=None";
        }

        private static bool TryFindMoveIntent(
            IReadOnlyList<RawMovementIntent> rawIntents,
            int entityId,
            out RawMovementIntent intent)
        {
            for (var i = 0; i < rawIntents.Count; i++)
            {
                if (rawIntents[i].SourceId == entityId &&
                    rawIntents[i].CommandKind == MovementCommandKind.Move)
                {
                    intent = rawIntents[i];
                    return true;
                }
            }

            intent = default;
            return false;
        }

        private GroundLocomotionResolution ResolveBaselineGroundLocomotion(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex)
        {
            if (source.enemyLocomotionCooldownTicks > 0)
            {
                return default;
            }

            switch (source.aiMode)
            {
                case EnemyAiMode.Patrol:
                    if (ShouldHoldWallFollowForSameCellPassiveContact(snapshot, source))
                    {
                        return default;
                    }

                    if (TryBuildPatrolDecisionProposal(snapshot, source, tickIndex, out var patrolProposal))
                    {
                        if (patrolProposal.HasDirection &&
                            EnemyMovementStrategyShared.ResolveDelta(patrolProposal.PlannedDirection) is { } patrolDelta &&
                            EnemyMovementStrategyShared.TryBuildMoveIntent(
                                snapshot,
                                source,
                                _commonSettings,
                                patrolDelta,
                                out var patrolIntent))
                        {
                            return new GroundLocomotionResolution(
                                hasIntent: true,
                                patrolIntent,
                                _locomotionTimingSettings.MoveCooldownTicks,
                                _locomotionTimingSettings.OrdinaryKinematicMoveTicks);
                        }

                        return default;
                    }

                    if (_patrolStrategy.TryBuildMovementIntent(
                            snapshot,
                            source,
                            _commonSettings,
                            _patrolSettings,
                            out var fallbackPatrolIntent))
                    {
                        return new GroundLocomotionResolution(
                            hasIntent: true,
                            fallbackPatrolIntent,
                            _locomotionTimingSettings.MoveCooldownTicks,
                            _locomotionTimingSettings.OrdinaryKinematicMoveTicks);
                    }

                    return default;

                case EnemyAiMode.Chase:
                    if (!_detectionStrategy.TryFindTarget(snapshot, source, _detectionSettings, out var chaseTarget))
                    {
                        return default;
                    }

                    if (_chaseStrategy.TryBuildMovementIntent(
                            snapshot,
                            source,
                            chaseTarget,
                            _commonSettings,
                            _chaseSettings,
                            out var chaseIntent))
                    {
                        return new GroundLocomotionResolution(
                            hasIntent: true,
                            chaseIntent,
                            _locomotionTimingSettings.MoveCooldownTicks,
                            _locomotionTimingSettings.OrdinaryKinematicMoveTicks);
                    }

                    return default;

                case EnemyAiMode.Charge:
                    if (TryResolveActiveChargeDirection(snapshot, source, out var chargeDirection) &&
                        EnemyMovementStrategyShared.ResolveDelta(chargeDirection) is { } chargeDelta &&
                        EnemyMovementStrategyShared.TryBuildChargeMoveIntentIgnoringUnits(
                            snapshot,
                            source,
                            _commonSettings,
                            chargeDelta,
                            out var chargeIntent))
                    {
                        return new GroundLocomotionResolution(
                            hasIntent: true,
                            chargeIntent,
                            _chargeTimingSettings.ActiveStepCooldownTicks,
                            ordinaryKinematicMoveTicks: 0);
                    }

                    return default;

                default:
                    return default;
            }
        }

        private bool TryResolveScheduledJumpStart(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyJumpRuntimeState previousState,
            int tickIndex,
            out EnemyJumpRuntimeState jumpState)
        {
            jumpState = default;
            if (source.boardPresence != EntityBoardPresence.Occupying ||
                previousState.phase != EnemyJumpPhase.None ||
                !TryFindSameFacePlayerTarget(snapshot, source, previousState, tickIndex, out var lockedTargetCell))
            {
                return false;
            }

            jumpState = EnemyJumpQueries.StartJump(
                previousState,
                source.position,
                lockedTargetCell,
                tickIndex,
                _movementSkillCapability.JumpTimingSettings);
            return true;
        }

        private bool TryFindSameFacePlayerTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyJumpRuntimeState previousState,
            int tickIndex,
            out SurfaceCell lockedTargetCell)
        {
            lockedTargetCell = default;

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            var bestDistance = int.MaxValue;
            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var candidate = orderedEntities[i];
                if (!IsValidSameFacePlayerTarget(snapshot, source, candidate))
                {
                    continue;
                }

                var distance = GetPlanarDistance(source.position, candidate.position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                var jumpState = EnemyJumpQueries.StartJump(
                    previousState,
                    source.position,
                    candidate.position,
                    tickIndex,
                    _movementSkillCapability.JumpTimingSettings);

                bestDistance = distance;
                lockedTargetCell = candidate.position;
            }

            return bestDistance != int.MaxValue;
        }

        private static bool IsValidSameFacePlayerTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState candidate)
        {
            return candidate.entityId != source.entityId &&
                   candidate.type == EntityType.Unit &&
                   candidate.hp > 0 &&
                   !candidate.markedForDeath &&
                   candidate.boardPresence == EntityBoardPresence.Occupying &&
                   candidate.position != source.position &&
                   candidate.position.face == source.position.face &&
                   snapshot.TryGetPlayerControlState(candidate.entityId, out _);
        }

        private static int GetPlanarDistance(SurfaceCell source, SurfaceCell target)
        {
            var sourcePlanar = source.PlanarPosition;
            var targetPlanar = target.PlanarPosition;
            return Math.Abs(targetPlanar.x - sourcePlanar.x) + Math.Abs(targetPlanar.y - sourcePlanar.y);
        }

        private static bool ShouldWriteJumpState(
            bool hadPreviousState,
            in EnemyJumpRuntimeState previousState,
            in EnemyJumpRuntimeState nextState)
        {
            return hadPreviousState ||
                   nextState.IsActive ||
                   nextState.sequence != 0 ||
                   !AreEqual(previousState, nextState);
        }

        private static bool AreEqual(
            in EnemyJumpRuntimeState left,
            in EnemyJumpRuntimeState right)
        {
            return left.phase == right.phase &&
                   left.sequence == right.sequence &&
                   left.sourceCell == right.sourceCell &&
                   left.lockedTargetCell == right.lockedTargetCell &&
                   left.windupEndTick == right.windupEndTick &&
                   left.landingTick == right.landingTick &&
                   left.cooldownRemainingTicks == right.cooldownRemainingTicks &&
                   left.retryCount == right.retryCount;
        }

        private static void AppendJumpUpdate(
            List<string> updates,
            int entityId,
            string label,
            in EnemyJumpRuntimeState state,
            string extra = null)
        {
            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            var builder = new System.Text.StringBuilder();
            builder
                .Append("EnemyJumpStateUpdated|E=").Append(entityId)
                .Append("|Label=").Append(label ?? string.Empty)
                .Append("|Phase=").Append(state.phase)
                .Append("|Seq=").Append(state.sequence)
                .Append("|Source=").Append(state.sourceCell)
                .Append("|Locked=").Append(state.lockedTargetCell)
                .Append("|WindupEnd=").Append(state.windupEndTick)
                .Append("|Landing=").Append(state.landingTick)
                .Append("|Cooldown=").Append(state.cooldownRemainingTicks)
                .Append("|Retry=").Append(state.retryCount);

            if (!string.IsNullOrEmpty(extra))
            {
                builder.Append('|').Append(extra);
            }

            updates.Add(builder.ToString());
        }

        private Direction? ResolvePatrolFacing(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            EnemyAiTransitionStage stage,
            in EnemyAiTransitionDecision decision)
        {
            if (decision.Facing.HasValue)
            {
                return decision.Facing.Value;
            }

            if (decision.Mode != EnemyAiMode.Patrol ||
                source.enemyLocomotionCooldownTicks > 0)
            {
                return null;
            }

            if (stage == EnemyAiTransitionStage.BeforeMovement &&
                TryBuildPatrolDecisionProposal(snapshot, source, tickIndex, out var patrolProposal))
            {
                return patrolProposal.HasDirection && patrolProposal.PlannedFacing != source.facing
                    ? patrolProposal.PlannedFacing
                    : (Direction?)null;
            }

            if (stage == EnemyAiTransitionStage.BeforeMovement &&
                _patrolStrategy is WallFollowPatrolStrategy &&
                _patrolStrategy.TryBuildMovementIntent(
                    snapshot,
                    source,
                    _commonSettings,
                    _patrolSettings,
                    out _))
            {
                return null;
            }

            if (stage == EnemyAiTransitionStage.BeforeMovement &&
                _patrolStrategy is IPatrolFacingStrategy patrolFacingStrategy &&
                patrolFacingStrategy.TryResolveFacing(snapshot, source, _patrolSettings, out var patrolFacing))
            {
                return patrolFacing;
            }

            if (stage != EnemyAiTransitionStage.BeforeAttack ||
                _patrolStrategy is not WallFollowPatrolStrategy ||
                EnemyMovementStrategyShared.TryChooseWallFollowDirection(snapshot, source, _patrolSettings, out _) ||
                !EnemyMovementStrategyShared.TryChooseWallFollowRotateOnlyFacing(
                    source.facing,
                    _patrolSettings.TurnPreference,
                    out var rotateOnlyFacing) ||
                rotateOnlyFacing == source.facing)
            {
                return null;
            }

            return rotateOnlyFacing;
        }

        private bool TryBuildPatrolDecisionProposal(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            out EnemyPatrolDecisionProposal proposal)
        {
            var patrolState = snapshot != null &&
                              snapshot.TryGetEnemyPatrolState(_entityId, out var storedState)
                ? storedState
                : default;
            return EnemyPatrolDecisionPlanner.TryBuildProposal(
                snapshot,
                source,
                tickIndex,
                _patrolStrategyKind,
                patrolState,
                _patrolSettings,
                out proposal);
        }

        private void TryCapturePatrolOriginBeforeLeavingPatrol(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            EnemyAiTransitionStage stage,
            in EnemyAiTransitionDecision decision,
            IEnemyAiCommitContext writeContext,
            List<string> transitions)
        {
            if (stage != EnemyAiTransitionStage.BeforeMovement ||
                source.aiMode != EnemyAiMode.Patrol ||
                decision.Mode == EnemyAiMode.Patrol ||
                _patrolStrategyKind != PatrolStrategyKind.RandomWalk ||
                writeContext is not IPreMovementStateCommitContext patrolWriteContext)
            {
                return;
            }

            var hadPreviousState = snapshot.TryGetEnemyPatrolState(_entityId, out var previousState);
            if (previousState.IsInitialized ||
                !TryBuildPatrolDecisionProposal(snapshot, source, tickIndex, out var proposal) ||
                !proposal.ShouldInitializeState)
            {
                return;
            }

            var nextState = EnemyPatrolQueries.Initialize(previousState, source.position);
            if (!ShouldWritePatrolState(hadPreviousState, previousState, nextState))
            {
                return;
            }

            patrolWriteContext.SetEnemyPatrolState(_entityId, nextState);
            AppendPatrolUpdate(transitions, _entityId, "Initialized", nextState, $"Mask={proposal.CandidateMask}");
        }

        private void TryInitializePatrolStateFromProposal(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            if (source.aiMode != EnemyAiMode.Patrol ||
                !TryBuildPatrolDecisionProposal(snapshot, source, tickIndex, out var proposal) ||
                !proposal.ShouldInitializeState)
            {
                return;
            }

            var hadPreviousState = snapshot.TryGetEnemyPatrolState(_entityId, out var previousState);
            var nextState = EnemyPatrolQueries.Initialize(previousState, source.position);
            if (!ShouldWritePatrolState(hadPreviousState, previousState, nextState))
            {
                return;
            }

            writeContext.SetEnemyPatrolState(_entityId, nextState);
            AppendPatrolUpdate(updates, _entityId, "Initialized", nextState, $"Mask={proposal.CandidateMask}");
        }

        private static bool ShouldWritePatrolState(
            bool hadPreviousState,
            in EnemyPatrolRuntimeState previousState,
            in EnemyPatrolRuntimeState nextState)
        {
            return hadPreviousState ||
                   nextState.IsInitialized ||
                   !AreEqual(previousState, nextState);
        }

        private static bool AreEqual(
            in EnemyPatrolRuntimeState left,
            in EnemyPatrolRuntimeState right)
        {
            return left.sequence == right.sequence &&
                   left.homeCell == right.homeCell &&
                   left.lastCommittedDirection == right.lastCommittedDirection;
        }

        private static void AppendPatrolUpdate(
            List<string> updates,
            int entityId,
            string label,
            in EnemyPatrolRuntimeState state,
            string extra = null)
        {
            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            var builder = new System.Text.StringBuilder();
            builder
                .Append("EnemyPatrolStateUpdated|E=").Append(entityId)
                .Append("|Label=").Append(label ?? string.Empty)
                .Append("|Seq=").Append(state.sequence)
                .Append("|Home=").Append(state.homeCell)
                .Append("|LastDirection=").Append(state.lastCommittedDirection);

            if (!string.IsNullOrEmpty(extra))
            {
                builder.Append('|').Append(extra);
            }

            updates.Add(builder.ToString());
        }
    }

    internal static class EnemyPhaseThroughLockedTargetQueries
    {
        // Baseline validator-only chooser. This current lock-based rule is not a reusable
        // template for generalized phase movement.
        public const string RuleLabel = "LockedTargetCrossThrough";

        public static bool TryResolveValidatorWindow(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyActionRuntimeState actionState,
            IAttackDecisionStrategy attackDecisionStrategy,
            in DetectionSettings detectionSettings,
            in AttackDecisionSettings attackDecisionSettings,
            out EntityState lockedTarget,
            out SurfaceCell terminalCell)
        {
            lockedTarget = default;
            terminalCell = default;

            return snapshot != null &&
                   EnemyActionStateTargeting.TryResolveLockedTarget(
                       snapshot,
                       source,
                       actionState,
                       attackDecisionStrategy,
                       detectionSettings,
                       attackDecisionSettings,
                       out lockedTarget) &&
                   TryResolveCurrentTerminalCell(source, lockedTarget, actionState.direction, out terminalCell);
        }

        public static bool TryResolveCurrentTerminalCell(
            in EntityState source,
            in EntityState lockedTarget,
            Direction direction,
            out SurfaceCell terminalCell)
        {
            // Keep the chooser local and deterministic: same-face, committed line, behind-target +1.
            terminalCell = default;
            if (source.position.face != lockedTarget.position.face ||
                !EnemyMovementStrategyShared.TryResolveDelta(direction, out var delta))
            {
                return false;
            }

            var expectedTargetPosition = source.position.PlanarPosition + delta;
            if (lockedTarget.position.PlanarPosition != expectedTargetPosition)
            {
                return false;
            }

            terminalCell = new SurfaceCell(
                source.position.face,
                lockedTarget.position.x + delta.x,
                lockedTarget.position.y + delta.y);
            return true;
        }
    }

    public sealed class DefaultEnemyAiStateResolver : IEnemyAiStateResolver
    {
        public static readonly DefaultEnemyAiStateResolver Instance = new();

        public EnemyAiTransitionDecision Resolve(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            EnemyAiTransitionStage stage,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyAiCommonSettings commonSettings,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (detectionStrategy == null)
            {
                throw new ArgumentNullException(nameof(detectionStrategy));
            }

            if (source.hp <= 0 || source.markedForDeath)
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Dead, 0, "Dead");
            }

            switch (stage)
            {
                case EnemyAiTransitionStage.BeforeMovement:
                    return ResolveBeforeMovement(snapshot, source, detectionStrategy, combatCapability, commonSettings, detectionSettings);

                case EnemyAiTransitionStage.BeforeAttack:
                    return ResolveBeforeAttack(snapshot, source, detectionStrategy, combatCapability, commonSettings, detectionSettings);

                case EnemyAiTransitionStage.AfterAttack:
                    return ResolveAfterAttack(snapshot, source, tickIndex, combatCapability, commonSettings);

                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown enemy AI transition stage.");
            }
        }

        private static EnemyAiTransitionDecision ResolveBeforeMovement(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings)
        {
            switch (source.aiMode)
            {
                case EnemyAiMode.None:
                case EnemyAiMode.Dead:
                    return Keep(source, "Disabled");

                case EnemyAiMode.Patrol:
                case EnemyAiMode.Chase:
                case EnemyAiMode.Attack:
                    return TryResolveCombatReadiness(
                        snapshot,
                        source,
                        detectionStrategy,
                        combatCapability,
                        detectionSettings,
                        EnemyAiMode.Patrol);

                case EnemyAiMode.Recover:
                    if (source.aiStateTimer > 0)
                    {
                        return new EnemyAiTransitionDecision(
                            EnemyAiMode.Recover,
                            source.aiStateTimer - 1,
                            "RecoverTick");
                    }

                    if (detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out _))
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "RecoverComplete");
                    }

                    return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, "RecoverCompleteNoTarget");

                default:
                    return Keep(source, "UnhandledBeforeMovement");
            }
        }

        private static EnemyAiTransitionDecision ResolveBeforeAttack(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings)
        {
            switch (source.aiMode)
            {
                case EnemyAiMode.Chase:
                case EnemyAiMode.Attack:
                    return TryResolveCombatReadiness(
                        snapshot,
                        source,
                        detectionStrategy,
                        combatCapability,
                        detectionSettings,
                        EnemyAiMode.Patrol);

                default:
                    return Keep(source, "NoBeforeAttackTransition");
            }
        }

        private static EnemyAiTransitionDecision ResolveAfterAttack(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyAiCommonSettings commonSettings)
        {
            var didCommit = DidCommitCombatAttackThisTick(snapshot, source.entityId, tickIndex);
            if (source.aiMode != EnemyAiMode.Attack ||
                !didCommit)
            {
                return Keep(source, "NoAfterAttackTransition");
            }

            if (UsesReceiverOwnedContactCadence(combatCapability))
            {
                return Keep(source, "ReceiverOwnedContactCadence");
            }

            return new EnemyAiTransitionDecision(
                EnemyAiMode.Recover,
                commonSettings.RecoverTicks,
                "AttackCommitted");
        }

        private static EnemyAiTransitionDecision TryResolveCombatReadiness(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in DetectionSettings detectionSettings,
            EnemyAiMode patrolFallback)
        {
            if (source.aiMode == EnemyAiMode.Attack &&
                snapshot.TryGetEnemyActionState(source.entityId, out var actionState) &&
                actionState.IsActive)
            {
                if (combatCapability != null &&
                    EnemyActionStateTargeting.TryResolveLockedTarget(
                        snapshot,
                        source,
                        actionState,
                        combatCapability.AttackDecisionStrategy,
                        detectionSettings,
                        combatCapability.AttackDecisionSettings,
                        out _))
                {
                    return new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, "LockedTargetInRange", actionState.direction);
                }

                return new EnemyAiTransitionDecision(
                    EnemyActionStateTargeting.ResolveFallbackAiMode(snapshot, source, detectionStrategy, detectionSettings),
                    0,
                    "LockedTargetLost");
            }

            if (!detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out var target))
            {
                return new EnemyAiTransitionDecision(patrolFallback, 0, "NoTarget");
            }

            if (combatCapability != null &&
                combatCapability.AttackDecisionStrategy.IsTargetInRange(source, target, combatCapability.AttackDecisionSettings))
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, "TargetInRange");
            }

            return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "TargetSensed");
        }

        private static EnemyAiTransitionDecision Keep(in EntityState source, string reason)
        {
            return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, reason);
        }

        private static bool UsesReceiverOwnedContactCadence(EnemyCombatCapabilityRuntime combatCapability)
        {
            return combatCapability != null &&
                   combatCapability.AttackDecisionStrategy is ContactSameCellAttackDecisionStrategy;
        }

        private static bool DidCommitCombatAttackThisTick(
            WorldSnapshot snapshot,
            int entityId,
            int tickIndex)
        {
            return snapshot.TryGetEnemyActionState(entityId, out var actionState) &&
                   actionState.IsActive &&
                   (actionState.executionAttempted || actionState.executeTick == tickIndex);
        }
    }

    public sealed class ChargingEnemyAiStateResolver : IEnemyAiStateResolver
    {
        public static readonly ChargingEnemyAiStateResolver Instance = new();

        public EnemyAiTransitionDecision Resolve(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            EnemyAiTransitionStage stage,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyAiCommonSettings commonSettings,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (detectionStrategy == null)
            {
                throw new ArgumentNullException(nameof(detectionStrategy));
            }

            if (source.hp <= 0 || source.markedForDeath)
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Dead, 0, "Dead");
            }

            return stage switch
            {
                EnemyAiTransitionStage.BeforeMovement => ResolveBeforeMovement(
                    snapshot,
                    source,
                    tickIndex,
                    detectionStrategy,
                    combatCapability,
                    chargeTimingSettings,
                    detectionSettings),
                EnemyAiTransitionStage.BeforeAttack => ResolveBeforeAttack(
                    snapshot,
                    source,
                    detectionStrategy,
                    combatCapability,
                    detectionSettings),
                EnemyAiTransitionStage.AfterAttack => ResolveAfterAttack(snapshot, source, tickIndex, combatCapability, commonSettings),
                _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown enemy AI transition stage."),
            };
        }

        private static EnemyAiTransitionDecision ResolveAfterAttack(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyAiCommonSettings commonSettings)
        {
            var didCommit = DidCommitCombatAttackThisTick(snapshot, source.entityId, tickIndex);
            if (source.aiMode != EnemyAiMode.Attack ||
                !didCommit)
            {
                return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, "NoAfterAttackTransition");
            }

            if (UsesReceiverOwnedContactCadence(combatCapability))
            {
                return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, "ReceiverOwnedContactCadence");
            }

            return new EnemyAiTransitionDecision(EnemyAiMode.Recover, commonSettings.RecoverTicks, "AttackCommitted");
        }

        private static bool UsesReceiverOwnedContactCadence(EnemyCombatCapabilityRuntime combatCapability)
        {
            return combatCapability != null &&
                   combatCapability.AttackDecisionStrategy is ContactSameCellAttackDecisionStrategy;
        }

        private static bool DidCommitCombatAttackThisTick(
            WorldSnapshot snapshot,
            int entityId,
            int tickIndex)
        {
            return snapshot.TryGetEnemyActionState(entityId, out var actionState) &&
                   actionState.IsActive &&
                   (actionState.executionAttempted || actionState.executeTick == tickIndex);
        }

        private static EnemyAiTransitionDecision ResolveBeforeMovement(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings)
        {
            switch (source.aiMode)
            {
                case EnemyAiMode.None:
                case EnemyAiMode.Dead:
                    return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, "Disabled");

                case EnemyAiMode.Patrol:
                    if (detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out _))
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "TargetSensed");
                    }

                    return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, "NoTarget");

                case EnemyAiMode.Chase:
                    return ResolveChase(snapshot, source, detectionStrategy, combatCapability, detectionSettings);

                case EnemyAiMode.Charge:
                    return ResolveChargeBeforeMovement(
                        snapshot,
                        source,
                        tickIndex,
                        detectionStrategy,
                        combatCapability,
                        chargeTimingSettings,
                        detectionSettings);

                case EnemyAiMode.Attack:
                    return ResolveAttackOrFallback(snapshot, source, detectionStrategy, combatCapability, detectionSettings);

                case EnemyAiMode.Recover:
                    if (IsChargeOwnedRecover(snapshot, source.entityId, out var chargeRecoverState))
                    {
                        if (chargeRecoverState.recoverRemainingTicks > 0)
                        {
                            return new EnemyAiTransitionDecision(EnemyAiMode.Recover, 0, "ChargeRecoverTick");
                        }

                        return ResolvePostCharge(snapshot, source, detectionStrategy, combatCapability, detectionSettings, "ChargeRecoverComplete");
                    }

                    var genericRecoverCountdown = GetGenericRecoverCountdown(snapshot, source);
                    if (genericRecoverCountdown > 0)
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Recover, genericRecoverCountdown - 1, "RecoverTick");
                    }

                    return detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out _)
                        ? new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "RecoverComplete")
                        : new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, "RecoverCompleteNoTarget");

                default:
                    return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, "UnhandledBeforeMovement");
            }
        }

        private static EnemyAiTransitionDecision ResolveBeforeAttack(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in DetectionSettings detectionSettings)
        {
            if (source.aiMode == EnemyAiMode.Charge)
            {
                if (TryGetChargeStateAuthoritative(snapshot, source.entityId, out var chargeState))
                {
                    return new EnemyAiTransitionDecision(
                        EnemyAiMode.Charge,
                        0,
                        chargeState.phase switch
                        {
                            EnemyChargePhase.Windup => "ChargeWindup",
                            EnemyChargePhase.Active => chargeState.remainingActiveSteps == 0 ? "ChargeFinalStep" : "ChargeInProgress",
                            EnemyChargePhase.Recover => "ChargeRecover",
                            _ => "ChargePendingStateStart",
                        });
                }

                return new EnemyAiTransitionDecision(
                    EnemyAiMode.Charge,
                    0,
                    "ChargePendingStateStart");
            }

            if (source.aiMode == EnemyAiMode.Chase || source.aiMode == EnemyAiMode.Attack)
            {
                return ResolveAttackOrFallback(snapshot, source, detectionStrategy, combatCapability, detectionSettings);
            }

            return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, "NoBeforeAttackTransition");
        }

        private static EnemyAiTransitionDecision ResolveChase(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in DetectionSettings detectionSettings)
        {
            if (!detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out var target))
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, "NoTarget");
            }

            if (combatCapability != null &&
                combatCapability.AttackDecisionStrategy.IsTargetInRange(source, target, combatCapability.AttackDecisionSettings))
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, "TargetInRange");
            }

            if (EnemyChargeStrategyShared.TryResolveChargeStart(snapshot, source, target, out var chargeFacing, out _))
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeStart", chargeFacing);
            }

            return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "TargetSensed");
        }

        private static EnemyAiTransitionDecision ResolveAttackOrFallback(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in DetectionSettings detectionSettings)
        {
            if (source.aiMode == EnemyAiMode.Attack &&
                snapshot.TryGetEnemyActionState(source.entityId, out var actionState) &&
                actionState.IsActive)
            {
                if (combatCapability != null &&
                    EnemyActionStateTargeting.TryResolveLockedTarget(
                        snapshot,
                        source,
                        actionState,
                        combatCapability.AttackDecisionStrategy,
                        detectionSettings,
                        combatCapability.AttackDecisionSettings,
                        out _))
                {
                    return new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, "LockedTargetInRange", actionState.direction);
                }

                return new EnemyAiTransitionDecision(
                    EnemyActionStateTargeting.ResolveFallbackAiMode(snapshot, source, detectionStrategy, detectionSettings),
                    0,
                    "LockedTargetLost");
            }

            if (!detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out var target))
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, "NoTarget");
            }

            return combatCapability != null &&
                   combatCapability.AttackDecisionStrategy.IsTargetInRange(source, target, combatCapability.AttackDecisionSettings)
                ? new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, "TargetInRange")
                : new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "TargetSensed");
        }

        private static EnemyAiTransitionDecision ResolvePostCharge(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in DetectionSettings detectionSettings,
            string reason)
        {
            if (!detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out var target))
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, reason);
            }

            return combatCapability != null &&
                   combatCapability.AttackDecisionStrategy.IsTargetInRange(source, target, combatCapability.AttackDecisionSettings)
                ? new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, reason)
                : new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, reason);
        }

        private static EnemyAiTransitionDecision ResolveChargeBeforeMovement(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings)
        {
            if (!snapshot.TryGetEnemyChargeState(source.entityId, out var chargeState) ||
                chargeState.phase == EnemyChargePhase.None)
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargePendingStateStart");
            }

            switch (chargeState.phase)
            {
                case EnemyChargePhase.Windup:
                    if (tickIndex < chargeState.windupEndTick)
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeWindup");
                    }

                    if (!EnemyChargeStrategyShared.CanAdvanceChargeStep(snapshot, source, chargeState.lockedDirection))
                    {
                        return ResolveChargeRecoveryOrImmediate(
                            snapshot,
                            source,
                            detectionStrategy,
                            combatCapability,
                            chargeTimingSettings,
                            detectionSettings,
                            "ChargeBlocked");
                    }

                    if (!CanMoveThisTick(source))
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeWindupCompleteWaitingForLocomotionCooldown");
                    }

                    return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeContinue");

                case EnemyChargePhase.Active:
                    if (snapshot.TryGetUnitKinematicPose(source.entityId, out var chargePose) &&
                        chargePose.HasAuthoritativeState &&
                        !chargePose.IsSettledAtAnchor &&
                        chargePose.Mode == MotionMode.Charge)
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeKinematicInProgress");
                    }

                    if (chargeState.remainingActiveSteps == 0)
                    {
                        if (source.enemyLocomotionCooldownTicks > 0)
                        {
                            return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeWaitingForLocomotionCooldown");
                        }

                        return ResolveChargeRecoveryOrImmediate(
                            snapshot,
                            source,
                            detectionStrategy,
                            combatCapability,
                            chargeTimingSettings,
                            detectionSettings,
                            "ChargeComplete");
                    }

                    if (!CanMoveThisTick(source))
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeWaitingForLocomotionCooldown");
                    }

                    if (!EnemyChargeStrategyShared.CanAdvanceChargeStep(snapshot, source, chargeState.lockedDirection))
                    {
                        return ResolveChargeRecoveryOrImmediate(
                            snapshot,
                            source,
                            detectionStrategy,
                            combatCapability,
                            chargeTimingSettings,
                            detectionSettings,
                            "ChargeBlocked");
                    }

                    return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeContinue");

                case EnemyChargePhase.Recover:
                    if (chargeState.recoverRemainingTicks > 0)
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Recover, 0, "ChargeRecoverTick");
                    }

                    return ResolvePostCharge(snapshot, source, detectionStrategy, combatCapability, detectionSettings, "ChargeRecoverComplete");

                default:
                    return new EnemyAiTransitionDecision(source.aiMode, 0, "UnhandledChargePhase");
            }
        }

        private static EnemyAiTransitionDecision ResolveChargeRecoveryOrImmediate(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings,
            string reason)
        {
            if (chargeTimingSettings.RecoverTicks == 0)
            {
                return ResolvePostCharge(snapshot, source, detectionStrategy, combatCapability, detectionSettings, reason);
            }

            return new EnemyAiTransitionDecision(
                EnemyAiMode.Recover,
                0,
                reason);
        }

        private static bool TryGetChargeStateAuthoritative(
            WorldSnapshot snapshot,
            int entityId,
            out EnemyChargeRuntimeState chargeState)
        {
            return snapshot.TryGetEnemyChargeState(entityId, out chargeState) &&
                   chargeState.phase != EnemyChargePhase.None;
        }

        private static bool IsChargeOwnedRecover(
            WorldSnapshot snapshot,
            int entityId,
            out EnemyChargeRuntimeState chargeState)
        {
            // Recover mode is shared by generic melee recover and charge-owned recover.
            // Charge-owned recover is authoritative only when a stored charge state is present
            // with phase Recover. Generic recover continues to use aiStateTimer until a future
            // dedicated EnemyRecoverRuntimeState exists.
            return TryGetChargeStateAuthoritative(snapshot, entityId, out chargeState) &&
                   chargeState.phase == EnemyChargePhase.Recover;
        }

        private static int GetGenericRecoverCountdown(WorldSnapshot snapshot, in EntityState source)
        {
            return source.aiMode == EnemyAiMode.Recover &&
                   !IsChargeOwnedRecover(snapshot, source.entityId, out _)
                ? Math.Max(0, source.aiStateTimer)
                : 0;
        }

        private static bool CanMoveThisTick(in EntityState source)
        {
            return source.enemyLocomotionCooldownTicks <= 1;
        }
    }

    public readonly struct EnemyAiTransitionDecision
    {
        public EnemyAiTransitionDecision(EnemyAiMode mode, int timer, string reason, Direction? facing = null)
        {
            Mode = mode;
            Timer = timer;
            Reason = reason ?? string.Empty;
            Facing = facing;
        }

        public EnemyAiMode Mode { get; }

        public int Timer { get; }

        public string Reason { get; }

        public Direction? Facing { get; }
    }
}

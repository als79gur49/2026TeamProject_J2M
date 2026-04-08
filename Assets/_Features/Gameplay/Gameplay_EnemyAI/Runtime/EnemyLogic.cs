using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
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
            EnemyAiTransitionStage stage,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings);
    }

    public sealed class EnemyLogic : IEnemyAiStateLogic, IPreMovementStateLogic, IMovementEntityLogic, IAttackEntityLogic, IEntityLogicSourceBinding
    {
        private readonly struct GroundLocomotionResolution
        {
            public GroundLocomotionResolution(bool hasIntent, RawMovementIntent intent, bool canAttemptJumpFallback)
            {
                HasIntent = hasIntent;
                Intent = intent;
                CanAttemptJumpFallback = canAttemptJumpFallback;
            }

            public bool HasIntent { get; }

            public RawMovementIntent Intent { get; }

            public bool CanAttemptJumpFallback { get; }
        }

        private readonly int _entityId;
        private readonly EnemyAiCommonSettings _commonSettings;
        private readonly PatrolSettings _patrolSettings;
        private readonly DetectionSettings _detectionSettings;
        private readonly ChaseSettings _chaseSettings;
        private readonly EnemyLocomotionTimingSettings _locomotionTimingSettings;
        private readonly IPatrolStrategy _patrolStrategy;
        private readonly IDetectionStrategy _detectionStrategy;
        private readonly IChaseStrategy _chaseStrategy;
        private readonly EnemyCombatCapabilityRuntime _combatCapability;
        private readonly EnemyMovementSkillCapabilityRuntime _movementSkillCapability;
        private readonly IEnemyAiStateResolver _stateResolver;

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

        [Obsolete("Use EnemyAiProfile or EnemyAiRuntimeDefinition instead.")]
        public EnemyLogic(int entityId, EnemyAiConfig config)
            : this(entityId, config.ToRuntimeDefinition())
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
            _patrolStrategy = aiDefinition.PatrolStrategy;
            _detectionStrategy = aiDefinition.DetectionStrategy;
            _chaseStrategy = aiDefinition.ChaseStrategy;
            _stateResolver = aiDefinition.StateResolver;
            aiDefinition.Capabilities.TryGetCombat(out _combatCapability);
            aiDefinition.Capabilities.TryGetMovementSkill(out _movementSkillCapability);
        }

        public int ControlledEntityId => _entityId;

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
                stage,
                _detectionStrategy,
                _combatCapability,
                _commonSettings,
                _detectionSettings);
            var resolvedFacing = ResolvePatrolFacing(snapshot, source, stage, decision);

            if (decision.Mode == source.aiMode && decision.Timer == source.aiStateTimer)
            {
                if (resolvedFacing.HasValue)
                {
                    writeContext.SetFacing(source.entityId, resolvedFacing.Value);
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

            if (!TryGetAiControlledEnemy(snapshot, out var source))
            {
                return;
            }

            var suppressMovementThisTick = false;
            if (HasJumpMovementSkill())
            {
                if (writeContext is not IEnemyJumpCommitContext jumpWriteContext)
                {
                    throw new ArgumentException("Pre-movement write context must expose enemy jump commit capabilities.", nameof(writeContext));
                }

                suppressMovementThisTick = CommitJumpState(snapshot, in input, source, writeContext, jumpWriteContext, updates);
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

            if (ShouldSuppressMovementForJump(snapshot, input.TickIndex))
            {
                return;
            }

            var locomotion = ResolveBaselineGroundLocomotion(snapshot, source);
            if (locomotion.HasIntent)
            {
                buffer.Add(ApplyLocomotionCooldown(locomotion.Intent));
            }
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

            if (ShouldSuppressAttackForJump(snapshot))
            {
                return;
            }

            if (_combatCapability == null)
            {
                return;
            }

            RawAttackIntent attackIntent;
            if (!TryGetControllableEnemy(snapshot, out var source) ||
                !snapshot.TryGetEnemyActionState(_entityId, out var actionState) ||
                !EnemyActionQueries.CanExecute(actionState, input.TickIndex) ||
                !EnemyActionStateTargeting.TryResolveLockedTarget(
                    snapshot,
                    source,
                    actionState,
                    _combatCapability.AttackDecisionStrategy,
                    _detectionSettings,
                    _combatCapability.AttackDecisionSettings,
                    out var target) ||
                !_combatCapability.AttackDecisionStrategy.TryBuildAttackIntent(
                    snapshot,
                    source,
                    target,
                    _commonSettings,
                    _combatCapability.AttackDecisionSettings,
                    out attackIntent))
            {
                return;
            }

            buffer.Add(attackIntent);
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

        private bool ShouldSuppressAttackForJump(WorldSnapshot snapshot)
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

        private bool HasJumpMovementSkill()
        {
            return _movementSkillCapability != null &&
                   _movementSkillCapability.Kind == MovementSkillStrategyKind.JumpToLockedTarget;
        }

        private bool TryGetControllableEnemy(WorldSnapshot snapshot, out EntityState source)
        {
            if (!EnemyParticipationPolicy.TryGetEnemyLogicEntity(snapshot, _entityId, out source))
            {
                return false;
            }

            return EnemyParticipationPolicy.IsControllableParticipant(snapshot, source);
        }

        private RawMovementIntent ApplyLocomotionCooldown(RawMovementIntent intent)
        {
            return new RawMovementIntent(
                intent.SourceId,
                intent.Priority,
                intent.Destination,
                intent.CommandKind,
                intent.LocalSequence,
                _locomotionTimingSettings.MoveCooldownTicks);
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
                var locomotion = ResolveBaselineGroundLocomotion(snapshot, source);
                if (TryResolveJumpStart(snapshot, source, locomotion, input.TickIndex, out nextState))
                {
                    suppressMovementThisTick = true;
                    AppendJumpUpdate(updates, _entityId, "Start", nextState);
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

                if (input.TickIndex >= nextState.landingTick)
                {
                    if (EnemyJumpQueries.TryResolveLandingCell(snapshot, source, nextState, out var landingCell, out var landingRule))
                    {
                        jumpWriteContext.MoveEnemyJumpEntity(_entityId, landingCell);
                        jumpWriteContext.SetEnemyJumpBoardPresence(_entityId, EntityBoardPresence.Occupying);
                        nextState = EnemyJumpQueries.EnterCooldown(nextState, _movementSkillCapability.JumpTimingSettings.CooldownTicks);
                        AppendJumpUpdate(
                            updates,
                            _entityId,
                            "Landing",
                            nextState,
                            $"Cell={landingCell}|Rule={landingRule}");
                    }
                    else
                    {
                        nextState = EnemyJumpQueries.ScheduleRetry(nextState, input.TickIndex + 1);
                        AppendJumpUpdate(updates, _entityId, "Retry", nextState, "Reason=NoLegalLandingCell");
                    }
                }
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

        private GroundLocomotionResolution ResolveBaselineGroundLocomotion(
            WorldSnapshot snapshot,
            in EntityState source)
        {
            if (source.enemyLocomotionCooldownTicks > 0)
            {
                return default;
            }

            switch (source.aiMode)
            {
                case EnemyAiMode.Patrol:
                    if (_patrolStrategy.TryBuildMovementIntent(
                            snapshot,
                            source,
                            _commonSettings,
                            _patrolSettings,
                            out var patrolIntent))
                    {
                        return new GroundLocomotionResolution(
                            hasIntent: true,
                            patrolIntent,
                            canAttemptJumpFallback: false);
                    }

                    return new GroundLocomotionResolution(
                        hasIntent: false,
                        default,
                        canAttemptJumpFallback: true);

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
                            canAttemptJumpFallback: false);
                    }

                    return new GroundLocomotionResolution(
                        hasIntent: false,
                        default,
                        canAttemptJumpFallback: true);

                case EnemyAiMode.Charge:
                    var chargeDelta = EnemyMovementStrategyShared.ResolveDelta(source.facing);
                    if (chargeDelta.HasValue &&
                        EnemyMovementStrategyShared.TryBuildMoveIntent(
                            snapshot,
                            source,
                            _commonSettings,
                            chargeDelta.Value,
                            out var chargeIntent))
                    {
                        return new GroundLocomotionResolution(
                            hasIntent: true,
                            chargeIntent,
                            canAttemptJumpFallback: false);
                    }

                    return default;

                default:
                    return default;
            }
        }

        private bool TryResolveJumpStart(
            WorldSnapshot snapshot,
            in EntityState source,
            in GroundLocomotionResolution locomotion,
            int tickIndex,
            out EnemyJumpRuntimeState jumpState)
        {
            jumpState = default;
            var hasExistingState = snapshot.TryGetEnemyJumpState(_entityId, out var existingState);

            if (locomotion.HasIntent ||
                !locomotion.CanAttemptJumpFallback ||
                source.boardPresence != EntityBoardPresence.Occupying ||
                (hasExistingState &&
                 existingState.phase == EnemyJumpPhase.Cooldown &&
                 existingState.cooldownRemainingTicks > 0))
            {
                return false;
            }

            switch (source.aiMode)
            {
                case EnemyAiMode.Patrol:
                    return TryResolvePatrolJumpStart(snapshot, source, hasExistingState ? existingState : default, tickIndex, out jumpState);

                case EnemyAiMode.Chase:
                    return TryResolveChaseJumpStart(snapshot, source, hasExistingState ? existingState : default, tickIndex, out jumpState);

                default:
                    return false;
            }
        }

        private bool TryResolvePatrolJumpStart(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyJumpRuntimeState previousState,
            int tickIndex,
            out EnemyJumpRuntimeState jumpState)
        {
            jumpState = default;

            var forwardDelta = EnemyMovementStrategyShared.ResolveDelta(source.facing);
            if (!forwardDelta.HasValue)
            {
                return false;
            }

            var lockedTargetCell = source.position + (forwardDelta.Value * 2);
            jumpState = EnemyJumpQueries.StartJump(
                previousState,
                source.position,
                lockedTargetCell,
                tickIndex,
                _movementSkillCapability.JumpTimingSettings);

            return TryResolveJumpLanding(snapshot, source, jumpState);
        }

        private bool TryResolveChaseJumpStart(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyJumpRuntimeState previousState,
            int tickIndex,
            out EnemyJumpRuntimeState jumpState)
        {
            jumpState = default;
            if (!_detectionStrategy.TryFindTarget(snapshot, source, _detectionSettings, out var target) ||
                target.position == source.position ||
                target.position.face != source.position.face)
            {
                return false;
            }

            jumpState = EnemyJumpQueries.StartJump(
                previousState,
                source.position,
                target.position,
                tickIndex,
                _movementSkillCapability.JumpTimingSettings);

            return TryResolveJumpLanding(snapshot, source, jumpState);
        }

        private static bool TryResolveJumpLanding(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyJumpRuntimeState jumpState)
        {
            return EnemyJumpQueries.TryResolveLandingCell(snapshot, source, jumpState, out var landingCell, out _) &&
                   landingCell != source.position;
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
            EnemyAiTransitionStage stage,
            in EnemyAiTransitionDecision decision)
        {
            if (decision.Facing.HasValue)
            {
                return decision.Facing.Value;
            }

            if (stage != EnemyAiTransitionStage.BeforeMovement ||
                decision.Mode != EnemyAiMode.Patrol ||
                source.enemyLocomotionCooldownTicks > 0 ||
                _patrolStrategy is not IPatrolFacingStrategy patrolFacingStrategy ||
                !patrolFacingStrategy.TryResolveFacing(snapshot, source, _patrolSettings, out var patrolFacing))
            {
                return null;
            }

            return patrolFacing;
        }
    }

    public sealed class DefaultEnemyAiStateResolver : IEnemyAiStateResolver
    {
        public static readonly DefaultEnemyAiStateResolver Instance = new();

        public EnemyAiTransitionDecision Resolve(
            WorldSnapshot snapshot,
            in EntityState source,
            EnemyAiTransitionStage stage,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyAiCommonSettings commonSettings,
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
                    return ResolveAfterAttack(snapshot, source, commonSettings);

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
            in EnemyAiCommonSettings commonSettings)
        {
            if (source.aiMode != EnemyAiMode.Attack)
            {
                return Keep(source, "NoAfterAttackTransition");
            }

            if (!snapshot.TryGetEnemyActionState(source.entityId, out var actionState) ||
                !actionState.IsActive ||
                !actionState.executionAttempted)
            {
                return Keep(source, "NoAfterAttackTransition");
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
    }

    public sealed class ChargingEnemyAiStateResolver : IEnemyAiStateResolver
    {
        public static readonly ChargingEnemyAiStateResolver Instance = new();

        public EnemyAiTransitionDecision Resolve(
            WorldSnapshot snapshot,
            in EntityState source,
            EnemyAiTransitionStage stage,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            in EnemyAiCommonSettings commonSettings,
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
                    detectionStrategy,
                    combatCapability,
                    commonSettings,
                    detectionSettings),
                EnemyAiTransitionStage.BeforeAttack => ResolveBeforeAttack(
                    snapshot,
                    source,
                    detectionStrategy,
                    combatCapability,
                    commonSettings,
                    detectionSettings),
                EnemyAiTransitionStage.AfterAttack => ResolveAfterAttack(snapshot, source, commonSettings),
                _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown enemy AI transition stage."),
            };
        }

        private static EnemyAiTransitionDecision ResolveAfterAttack(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyAiCommonSettings commonSettings)
        {
            if (source.aiMode != EnemyAiMode.Attack)
            {
                return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, "NoAfterAttackTransition");
            }

            if (!snapshot.TryGetEnemyActionState(source.entityId, out var actionState) ||
                !actionState.IsActive ||
                !actionState.executionAttempted)
            {
                return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, "NoAfterAttackTransition");
            }

            return new EnemyAiTransitionDecision(EnemyAiMode.Recover, commonSettings.RecoverTicks, "AttackCommitted");
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
                    if (source.enemyLocomotionCooldownTicks > 0)
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Charge, source.aiStateTimer, "ChargeWaitingForLocomotionCooldown");
                    }

                    if (!EnemyChargeStrategyShared.CanAdvanceChargeStep(snapshot, source))
                    {
                        return ResolvePostCharge(snapshot, source, detectionStrategy, combatCapability, detectionSettings, "ChargeBlocked");
                    }

                    return source.aiStateTimer > 0
                        ? new EnemyAiTransitionDecision(EnemyAiMode.Charge, source.aiStateTimer - 1, "ChargeContinue")
                        : new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeFinalStep");

                case EnemyAiMode.Attack:
                    return ResolveAttackOrFallback(snapshot, source, detectionStrategy, combatCapability, detectionSettings);

                case EnemyAiMode.Recover:
                    if (source.aiStateTimer > 0)
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Recover, source.aiStateTimer - 1, "RecoverTick");
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
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings)
        {
            if (source.aiMode == EnemyAiMode.Charge)
            {
                if (source.enemyLocomotionCooldownTicks > 0)
                {
                    return new EnemyAiTransitionDecision(EnemyAiMode.Charge, source.aiStateTimer, "ChargeWaitingForLocomotionCooldown");
                }

                return source.aiStateTimer == 0
                    ? ResolvePostCharge(snapshot, source, detectionStrategy, combatCapability, detectionSettings, "ChargeComplete")
                    : new EnemyAiTransitionDecision(EnemyAiMode.Charge, source.aiStateTimer, "ChargeInProgress");
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

            if (EnemyChargeStrategyShared.TryResolveChargeStart(snapshot, source, target, out var chargeFacing, out var reachableSteps))
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Charge, reachableSteps - 1, "ChargeStart", chargeFacing);
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

using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class EnemyActionStateLogic : IEnemyActionStateLogic, IEntityLogicSourceBinding, ITileFeatureDefinitionContextReceiver
    {
        private readonly int _entityId;
        private readonly EnemyAiCommonSettings _commonSettings;
        private readonly DetectionSettings _detectionSettings;
        private readonly IDetectionStrategy _detectionStrategy;
        private readonly EnemyCombatCapabilityRuntime _combatCapability;
        private IReadOnlyList<TileFeatureRuntimeDefinition> _tileFeatureDefinitions = Array.Empty<TileFeatureRuntimeDefinition>();

        public EnemyActionStateLogic(int entityId)
            : this(entityId, EnemyAiRuntimeDefinition.CreateDefaultMelee())
        {
        }

        public EnemyActionStateLogic(int entityId, EnemyAiProfile profile)
            : this(
                entityId,
                (profile ?? throw new ArgumentNullException(nameof(profile)))
                .CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond))
        {
        }

        public EnemyActionStateLogic(int entityId, in EnemyAiRuntimeDefinition aiDefinition)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Enemy action state logic requires a positive entity ID.");
            }

            aiDefinition.Validate(nameof(aiDefinition));

            _entityId = entityId;
            _commonSettings = aiDefinition.CommonSettings;
            _detectionSettings = aiDefinition.DetectionSettings;
            _detectionStrategy = aiDefinition.DetectionStrategy;
            aiDefinition.Capabilities.TryGetCombat(out _combatCapability);
        }

        public int ControlledEntityId => _entityId;

        public void BindTileFeatureDefinitions(IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            _tileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
        }

        public void CommitEnemyActionState(
            WorldSnapshot snapshot,
            in TickInput input,
            EnemyActionStage stage,
            IEnemyActionCommitContext writeContext,
            List<EnemyActionTransition> transitions)
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

            if (!EnemyParticipationPolicy.TryGetEnemyLogicEntity(snapshot, _entityId, out var source))
            {
                return;
            }

            var hasPreviousAction = snapshot.TryGetEnemyActionState(_entityId, out var previousAction);
            if (_combatCapability == null)
            {
                var clearedAction = ClearActionWithFinalizer(
                    snapshot,
                    previousAction,
                    writeContext,
                    input.TickIndex,
                    EntityLocomotionLeaseReleaseReason.NoCombatCapability);
                if (ShouldWriteActionState(hasPreviousAction, previousAction, clearedAction))
                {
                    writeContext.SetEnemyActionState(_entityId, clearedAction);
                    transitions.Add(new EnemyActionTransition(_entityId, previousAction, clearedAction));
                }

                return;
            }

            if (!EnemyParticipationPolicy.CanParticipateOnCurrentTopology(snapshot, source))
            {
                var clearedAction = ClearActionWithFinalizer(
                    snapshot,
                    previousAction,
                    writeContext,
                    input.TickIndex,
                    EntityLocomotionLeaseReleaseReason.TopologyNonParticipant);
                if (ShouldWriteActionState(hasPreviousAction, previousAction, clearedAction))
                {
                    writeContext.SetEnemyActionState(_entityId, clearedAction);
                    transitions.Add(new EnemyActionTransition(_entityId, previousAction, clearedAction));
                }

                return;
            }

            var nextAction = stage switch
            {
                EnemyActionStage.BeforeAttackCollection => CommitBeforeAttackCollection(snapshot, source, previousAction, writeContext, input.TickIndex),
                EnemyActionStage.AfterAttack => CommitAfterAttack(snapshot, source, previousAction, writeContext, input.TickIndex),
                _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown enemy action stage."),
            };

            if (ShouldWriteActionState(hasPreviousAction, previousAction, nextAction))
            {
                writeContext.SetEnemyActionState(_entityId, nextAction);
            }

            transitions.Add(new EnemyActionTransition(_entityId, previousAction, nextAction));
        }

        private EnemyActionRuntimeState CommitBeforeAttackCollection(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyActionRuntimeState previousAction,
            IEnemyActionCommitContext writeContext,
            int tickIndex)
        {
            var workingAction = previousAction;
            if (workingAction.IsActive &&
                workingAction.executionAttempted &&
                workingAction.executeTick < tickIndex)
            {
                workingAction = ClearActionWithFinalizer(
                    snapshot,
                    workingAction,
                    writeContext,
                    tickIndex,
                    EntityLocomotionLeaseReleaseReason.PostExecuteCleanup);
            }

            if (UsesReceiverOwnedContactCadence() &&
                workingAction.IsActive &&
                workingAction.executionAttempted)
            {
                workingAction = ClearActionWithFinalizer(
                    snapshot,
                    workingAction,
                    writeContext,
                    tickIndex,
                    EntityLocomotionLeaseReleaseReason.PostExecuteCleanup);
            }

            if (source.hp <= 0 ||
                source.markedForDeath ||
                source.boardPresence != EntityBoardPresence.Occupying ||
                source.aiMode == EnemyAiMode.Dead)
            {
                return ClearActionWithFinalizer(
                    snapshot,
                    workingAction,
                    writeContext,
                    tickIndex,
                    EntityLocomotionLeaseReleaseReason.SourceInactive);
            }

            if (source.aiMode != EnemyAiMode.Attack)
            {
                if (source.aiMode == EnemyAiMode.Recover &&
                    workingAction.IsActive &&
                    workingAction.executionAttempted)
                {
                    return workingAction;
                }

                return ClearActionWithFinalizer(
                    snapshot,
                    workingAction,
                    writeContext,
                    tickIndex,
                    EntityLocomotionLeaseReleaseReason.AiModeNonAttack);
            }

            if (workingAction.IsActive)
            {
                if (workingAction.kind == EnemyActionKind.ForwardCellProjectile)
                {
                    if (!workingAction.executionAttempted &&
                        workingAction.executeTick <= tickIndex)
                    {
                        var releasedAction = CommitForwardCellProjectileRelease(
                            snapshot,
                            source,
                            workingAction,
                            writeContext,
                            tickIndex);
                        ReleaseCombatLocomotionHoldIfNeeded(
                            snapshot,
                            releasedAction,
                            writeContext,
                            tickIndex,
                            EntityLocomotionLeaseReleaseReason.NormalComplete);
                        return releasedAction;
                    }

                    var authoritativeFacing = EnemyActionQueries.ResolveAuthoritativeFacing(workingAction);
                    if (source.facing != authoritativeFacing)
                    {
                        writeContext.AddPoseMutation(
                            CreateActionFacingMutation(
                                source,
                                authoritativeFacing,
                                PoseMutationSource.CombatActionHold,
                                tickIndex,
                                workingAction.sequence,
                                "ForwardCellProjectileHold"));
                    }

                    return workingAction;
                }

                if (EnemyActionStateTargeting.TryResolveLockedTarget(
                        snapshot,
                        source,
                        workingAction,
                        _combatCapability.AttackDecisionStrategy,
                        _detectionSettings,
                        _combatCapability.AttackDecisionSettings,
                        out _))
                {
                    if (source.facing != workingAction.direction)
                    {
                        writeContext.AddPoseMutation(
                            CreateActionFacingMutation(
                                source,
                                workingAction.direction,
                                PoseMutationSource.CombatActionHold,
                                tickIndex,
                                workingAction.sequence,
                                "CombatActionHold"));
                    }

                    return workingAction;
                }

                ApplyCancelFallback(snapshot, source, writeContext);
                return ClearActionWithFinalizer(
                    snapshot,
                    workingAction,
                    writeContext,
                    tickIndex,
                    EntityLocomotionLeaseReleaseReason.ActionLogicClear);
            }

            if (!EnemyActionStateTargeting.TryResolveStartAction(
                    snapshot,
                    source,
                    _detectionStrategy,
                    _combatCapability.AttackDecisionStrategy,
                    _detectionSettings,
                    _combatCapability.AttackDecisionSettings,
                    out var target,
                    out var direction))
            {
                ApplyCancelFallback(snapshot, source, writeContext);
                return ClearActionWithFinalizer(
                    snapshot,
                    previousAction,
                    writeContext,
                    tickIndex,
                    EntityLocomotionLeaseReleaseReason.ActionLogicClear);
            }

            if (!CanStartCombatActionThisTick(snapshot, source, tickIndex))
            {
                return workingAction;
            }

            var startQuery = QueryCombatWindupStart(
                snapshot,
                source,
                target,
                out var lockedTargetCell);
            if (!startQuery.CanStart)
            {
                if (startQuery.BlockReason != WindupMeleeStartBlockReason.OutsideSimulationStartRange)
                {
                    ApplyCancelFallback(snapshot, source, writeContext);
                }

                return ClearActionWithFinalizer(
                    snapshot,
                    previousAction,
                    writeContext,
                    tickIndex,
                    EntityLocomotionLeaseReleaseReason.ActionLogicClear);
            }

            var actionKind = _combatCapability.Kind == AttackDecisionStrategyKind.WindupForwardCellProjectile
                ? EnemyActionKind.ForwardCellProjectile
                : EnemyActionKind.Melee;
            var nextAction = EnemyActionQueries.StartAction(
                workingAction,
                actionKind,
                target.entityId,
                direction,
                tickIndex,
                _combatCapability.AttackTimingSettings.WindupTicks,
                startQuery.EnemyOrigin,
                actionKind == EnemyActionKind.ForwardCellProjectile ? lockedTargetCell : null);
            writeContext.AddPoseMutation(
                CreateActionFacingMutation(
                    source,
                    direction,
                    PoseMutationSource.CombatActionStart,
                    tickIndex,
                    nextAction.sequence,
                    "CombatActionStart"));
            HoldCombatLocomotionAtCurrentPose(snapshot, source, writeContext, tickIndex, nextAction.sequence);
            return nextAction;
        }

        private WindupMeleeStartQueryResult QueryCombatWindupStart(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            out SurfaceCell? lockedTargetCell)
        {
            lockedTargetCell = null;
            if (_combatCapability.Kind == AttackDecisionStrategyKind.WindupForwardCellProjectile)
            {
                var result = WindupMeleeCombatPoseQueries.QueryStartWindupForwardCellProjectile(
                    snapshot,
                    source,
                    target,
                    _combatCapability.AttackDecisionStrategy,
                    _combatCapability.AttackDecisionSettings,
                    _combatCapability.WindupForwardCellProjectileSettings,
                    _tileFeatureDefinitions,
                    out var targetCell);
                if (result.CanStart)
                {
                    lockedTargetCell = targetCell;
                }

                return result;
            }

            return WindupMeleeCombatPoseQueries.QueryStartWindupMeleeA(
                snapshot,
                source,
                target,
                _combatCapability.AttackDecisionStrategy,
                _combatCapability.AttackDecisionSettings,
                _combatCapability.WindupMeleeSettings);
        }

        private EnemyActionRuntimeState CommitForwardCellProjectileRelease(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyActionRuntimeState action,
            IEnemyActionCommitContext writeContext,
            int tickIndex)
        {
            var authoritativeFacing = EnemyActionQueries.ResolveAuthoritativeFacing(action);
            if (source.facing != authoritativeFacing)
            {
                writeContext.AddPoseMutation(
                    CreateActionFacingMutation(
                        source,
                        authoritativeFacing,
                        PoseMutationSource.CombatActionRelease,
                        tickIndex,
                        action.sequence,
                        "ForwardCellProjectileRelease"));
            }

            if (!action.hasLockedForwardCellImpact)
            {
                return EnemyActionQueries.MarkExecutionAttempted(action, tickIndex);
            }

            var settings = _combatCapability.WindupForwardCellProjectileSettings;
            var impactDelayTicks = ResolveForwardCellProjectileImpactDelayTicks(action, settings);
            var impact = new PendingCellImpact(
                AllocatePendingCellImpactId(_entityId, action.sequence),
                _entityId,
                _entityId,
                action.lockedAttackBaseCell,
                action.lockedTargetCell,
                snapshot.Topology,
                action.lockedAttackDirection,
                settings.Damage,
                tickIndex,
                tickIndex,
                tickIndex + impactDelayTicks);

            writeContext.AddPendingCellImpact(impact);
            var shotKey = ForwardCellProjectileDebugLog.BuildShotKey(
                impact.SourceEnemyId,
                impact.TargetCell,
                impact.ImpactTick,
                impact.ImpactId);
            ForwardCellProjectileDebugLog.MarkFired(
                shotKey,
                impact.SourceEnemyId,
                impact.TargetCell,
                impact.ImpactTick,
                impact.ImpactId);
            ForwardCellProjectileDebugLog.Log(
                "RELEASE_PENDING_CREATED",
                $"Tick={tickIndex} Shot={shotKey} Source={impact.SourceEnemyId} " +
                $"SourceCell=({ForwardCellProjectileDebugLog.FormatCell(impact.SourceCell)}) " +
                $"SourceFacing={source.facing} Dir={impact.Direction} " +
                $"TargetCell=({ForwardCellProjectileDebugLog.FormatCell(impact.TargetCell)}) " +
                $"ImpactTick={impact.ImpactTick} LaunchTopology={ForwardCellProjectileDebugLog.FormatTopology(impact.LaunchTopology)} " +
                $"CreatedPending=true PendingCountAfterAdd={snapshot.CountPendingCellImpactsForOwner(impact.OwnerId) + 1} " +
                $"Damage={impact.Damage}");
            if (settings.AttackCooldownTicks > 0)
            {
                writeContext.SetEnemyAttackCooldown(
                    _entityId,
                    settings.AttackCooldownTicks,
                    settings.AttackCooldownTicks);
            }

            writeContext.ApplyEnemyAiState(_entityId, EnemyAiMode.Recover, _commonSettings.RecoverTicks);
            return EnemyActionQueries.MarkExecutionAttempted(action, tickIndex);
        }

        private static int AllocatePendingCellImpactId(int ownerId, int actionSequence)
        {
            return checked((ownerId * 100000) + Math.Max(1, actionSequence));
        }

        private static int ResolveForwardCellProjectileImpactDelayTicks(
            in EnemyActionRuntimeState action,
            in WindupForwardCellProjectileSettings settings)
        {
            if (action.lockedAttackBaseCell.face != action.lockedTargetCell.face)
            {
                return settings.ImpactDelayTicks;
            }

            var distanceCells = Math.Abs(action.lockedTargetCell.x - action.lockedAttackBaseCell.x) +
                                Math.Abs(action.lockedTargetCell.y - action.lockedAttackBaseCell.y);
            return settings.ResolveImpactDelayTicks(distanceCells);
        }

        private EntityPoseMutationOperation CreateActionFacingMutation(
            in EntityState source,
            Direction facing,
            PoseMutationSource poseSource,
            int tickIndex,
            int actionSequenceId,
            string writer)
        {
            return new EntityPoseMutationOperation(
                new EntityPoseMutationRequest
                {
                    EntityId = _entityId,
                    Source = poseSource,
                    Kind = PoseMutationKind.FacingOnly,
                    FromCell = source.position,
                    ToCell = source.position,
                    PositionChanged = false,
                    FacingBefore = source.facing,
                    FacingAfter = facing,
                    MovementDirection = Direction.None,
                    MovementIntentExists = false,
                    MovementAccepted = false,
                    MovementSuppressed = false,
                    HasExplicitActionFacing = true,
                    HasExplicitSkillFacing = false,
                    HasExplicitRotateAction = false,
                    KinematicMutation = KinematicMutationKind.None,
                    TickIndex = tickIndex,
                    ActionSequenceId = actionSequenceId,
                    Writer = writer,
                    Reason = "ExplicitCombatActionFacing",
                });
        }

        private EntityPoseMutationOperation CreateActionKinematicMutation(
            in EntityState source,
            UnitKinematicRuntimeState state,
            PoseMutationSource poseSource,
            KinematicMutationKind mutationKind,
            int tickIndex,
            int actionSequenceId,
            string writer)
        {
            return new EntityPoseMutationOperation(
                new EntityPoseMutationRequest
                {
                    EntityId = _entityId,
                    Source = poseSource,
                    Kind = PoseMutationKind.KinematicOnly,
                    FromCell = source.position,
                    ToCell = source.position,
                    PositionChanged = false,
                    FacingBefore = source.facing,
                    FacingAfter = source.facing,
                    MovementDirection = Direction.None,
                    MovementIntentExists = false,
                    MovementAccepted = false,
                    MovementSuppressed = false,
                    HasExplicitActionFacing = false,
                    HasExplicitSkillFacing = false,
                    HasExplicitRotateAction = false,
                    KinematicMutation = mutationKind,
                    KinematicDirection = Direction.None,
                    HasKinematicDirection = false,
                    KinematicDirectionKind = KinematicDirectionKind.None,
                    KinematicFacingPolicy = KinematicFacingPolicy.PreserveFacing,
                    ShouldUpdateFacing = false,
                    TickIndex = tickIndex,
                    ActionSequenceId = actionSequenceId,
                    Writer = writer,
                    Reason = "CombatLocomotionKinematic",
                },
                state);
        }

        private EnemyActionRuntimeState CommitAfterAttack(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyActionRuntimeState previousAction,
            IEnemyActionCommitContext writeContext,
            int tickIndex)
        {
            if (!previousAction.IsActive)
            {
                return previousAction;
            }

            if (source.hp <= 0 ||
                source.markedForDeath)
            {
                return ClearActionWithFinalizer(
                    snapshot,
                    previousAction,
                    writeContext,
                    tickIndex,
                    EntityLocomotionLeaseReleaseReason.AfterAttackClear);
            }

            return EnemyActionQueries.CanExecute(previousAction, tickIndex)
                ? EnemyActionQueries.MarkExecutionAttempted(previousAction, tickIndex)
                : previousAction;
        }

        private void ApplyCancelFallback(
            WorldSnapshot snapshot,
            in EntityState source,
            IEnemyActionCommitContext writeContext)
        {
            var fallbackMode = EnemyActionStateTargeting.ResolveFallbackAiMode(
                snapshot,
                source,
                _detectionStrategy,
                _detectionSettings);

            if (fallbackMode != source.aiMode ||
                source.aiStateTimer != 0)
            {
                writeContext.ApplyEnemyAiState(_entityId, fallbackMode, 0);
            }
        }

        private void HoldCombatLocomotionAtCurrentPose(
            WorldSnapshot snapshot,
            in EntityState source,
            IEnemyActionCommitContext writeContext,
            int tickIndex,
            int actionSequenceId)
        {
            if (snapshot.TryGetUnitContinuousLocomotionPose(source.entityId, out var continuousPose) &&
                continuousPose.HasAuthoritativeState &&
                (!continuousPose.State.velocity.IsZero ||
                 continuousPose.Mode != ContinuousLocomotionMode.Idle))
            {
                writeContext.SetUnitContinuousLocomotionState(
                    source.entityId,
                    UnitContinuousLocomotionState.CreateIdleFreeze(continuousPose.State));
            }

            if (snapshot.TryGetUnitKinematicPose(source.entityId, out var kinematicPose) &&
                kinematicPose.HasAuthoritativeState &&
                kinematicPose.Mode == MotionMode.Voluntary)
            {
                writeContext.SetEntityLocomotionLeaseState(
                    source.entityId,
                    CreateCombatActionLease(
                        source.entityId,
                        actionSequenceId,
                        kinematicPose.State,
                        kinematicPose.AnchorCell,
                        tickIndex));
                writeContext.AddPoseMutation(
                    CreateActionKinematicMutation(
                        source,
                        UnitKinematicRuntimeState.CreateHeldFreeze(kinematicPose.State),
                        PoseMutationSource.KinematicHold,
                        KinematicMutationKind.Hold,
                        tickIndex,
                        actionSequenceId,
                        "CombatLocomotionHold"));
            }
        }

        private void ReleaseCombatLocomotionHoldIfNeeded(
            WorldSnapshot snapshot,
            in EnemyActionRuntimeState actionState,
            IEnemyActionCommitContext writeContext,
            int tickIndex,
            EntityLocomotionLeaseReleaseReason reason)
        {
            ReleaseCombatLocomotionLeaseIfNeeded(snapshot, actionState, writeContext, tickIndex, reason);
        }

        private EnemyActionRuntimeState ClearActionWithFinalizer(
            WorldSnapshot snapshot,
            in EnemyActionRuntimeState actionState,
            IEnemyActionCommitContext writeContext,
            int tickIndex,
            EntityLocomotionLeaseReleaseReason reason)
        {
            ReleaseCombatLocomotionLeaseIfNeeded(snapshot, actionState, writeContext, tickIndex, reason);
            return EnemyActionQueries.Clear(actionState);
        }

        private void ReleaseCombatLocomotionLeaseIfNeeded(
            WorldSnapshot snapshot,
            in EnemyActionRuntimeState actionState,
            IEnemyActionCommitContext writeContext,
            int tickIndex,
            EntityLocomotionLeaseReleaseReason reason)
        {
            if (!snapshot.TryGetEntityLocomotionLeaseState(_entityId, out var lease) ||
                !lease.IsActive ||
                lease.ownerKind != EntityLocomotionLeaseOwnerKind.CombatAction ||
                lease.ownerActionSequenceId != actionState.sequence)
            {
                return;
            }

            var hasHeldPose = snapshot.TryGetUnitKinematicPose(_entityId, out var pose) &&
                              pose.HasAuthoritativeState &&
                              pose.Mode == MotionMode.Held;
            var policy = ResolveLeaseReleasePolicy(reason, lease, hasHeldPose, pose, out var velocity);
            if (hasHeldPose)
            {
                var facing = snapshot.TryGetEntity(_entityId, out var entity)
                    ? entity.facing
                    : Direction.None;
                var releasedState = policy == EntityLocomotionLeaseReleasePolicy.ResumeCapturedVoluntary
                    ? UnitKinematicRuntimeState.CreateVoluntaryResumeFromHeld(pose.State, velocity)
                    : UnitKinematicRuntimeState.SettledZero;
                var source = policy == EntityLocomotionLeaseReleasePolicy.ResumeCapturedVoluntary
                    ? PoseMutationSource.KinematicRelease
                    : PoseMutationSource.KinematicSettle;
                var mutation = policy == EntityLocomotionLeaseReleasePolicy.ResumeCapturedVoluntary
                    ? KinematicMutationKind.ReleaseHold
                    : KinematicMutationKind.ForceSettledZero;
                writeContext.AddPoseMutation(
                    CreateActionKinematicMutation(
                        new EntityState { entityId = _entityId, position = pose.AnchorCell, facing = facing },
                        releasedState,
                        source,
                        mutation,
                        tickIndex,
                        actionState.sequence,
                        policy == EntityLocomotionLeaseReleasePolicy.ResumeCapturedVoluntary
                            ? "CombatLocomotionRelease"
                            : "CombatLocomotionForceSettled"));
            }

            var completedLease = lease;
            completedLease.stateKind = EntityLocomotionLeaseStateKind.Completed;
            completedLease.lastReleaseTick = tickIndex;
            completedLease.lastReleaseReason = reason;
            writeContext.SetEntityLocomotionLeaseState(_entityId, completedLease);
        }

        private static EntityLocomotionLeaseState CreateCombatActionLease(
            int entityId,
            int actionSequenceId,
            in UnitKinematicRuntimeState capturedKinematic,
            SurfaceCell anchorAtAcquire,
            int tickIndex)
        {
            return new EntityLocomotionLeaseState
            {
                leaseId = AllocateCombatActionLeaseId(entityId, actionSequenceId),
                entityId = entityId,
                ownerKind = EntityLocomotionLeaseOwnerKind.CombatAction,
                stateKind = EntityLocomotionLeaseStateKind.HeldByOwner,
                ownerActionSequenceId = actionSequenceId,
                capturedKinematic = capturedKinematic,
                anchorAtAcquire = anchorAtAcquire,
                acquiredTick = tickIndex,
                lastReleaseTick = 0,
                lastReleaseReason = EntityLocomotionLeaseReleaseReason.NormalComplete,
            };
        }

        private static int AllocateCombatActionLeaseId(int entityId, int actionSequenceId)
        {
            return checked((entityId * 100000) + Math.Max(1, actionSequenceId));
        }

        private static EntityLocomotionLeaseReleasePolicy ResolveLeaseReleasePolicy(
            EntityLocomotionLeaseReleaseReason reason,
            in EntityLocomotionLeaseState lease,
            bool hasHeldPose,
            in UnitKinematicPose pose,
            out KinematicVelocity2 velocity)
        {
            velocity = default;
            if (hasHeldPose &&
                TryResolveHeldResumeVelocity(lease.capturedKinematic, out velocity))
            {
                return EntityLocomotionLeaseReleasePolicy.ResumeCapturedVoluntary;
            }

            return reason == EntityLocomotionLeaseReleaseReason.TopologyNonParticipant
                ? EntityLocomotionLeaseReleasePolicy.ForceSettledAtCurrentAnchor
                : EntityLocomotionLeaseReleasePolicy.ClearStaleHold;
        }

        private static bool TryResolveHeldResumeVelocity(
            in UnitKinematicRuntimeState state,
            out KinematicVelocity2 velocity)
        {
            velocity = default;
            if (state.totalTicks <= 0 ||
                (state.stepDirectionX == 0 && state.stepDirectionY == 0))
            {
                return false;
            }

            velocity = new KinematicVelocity2(
                KinematicFixed.FromRaw(state.stepDirectionX * KinematicFixed.UnitsPerCell / state.totalTicks),
                KinematicFixed.FromRaw(state.stepDirectionY * KinematicFixed.UnitsPerCell / state.totalTicks));
            return true;
        }

        private bool UsesReceiverOwnedContactCadence()
        {
            return _combatCapability != null &&
                   _combatCapability.AttackDecisionStrategy is ContactSameCellAttackDecisionStrategy;
        }

        private bool CanStartCombatActionThisTick(WorldSnapshot snapshot, in EntityState source, int tickIndex)
        {
            if (_combatCapability != null &&
                _combatCapability.Kind == AttackDecisionStrategyKind.WindupForwardCellProjectile &&
                source.enemyAttackCooldownTicks > 0)
            {
                return false;
            }

            if (snapshot.CanStartAction(_entityId, tickIndex))
            {
                return true;
            }

            if (_combatCapability != null &&
                _combatCapability.AttackTimingSettings.WindupTicks > 0 &&
                source.aiMode != EnemyAiMode.Attack &&
                WindupMeleeCombatPoseQueries.IsMoveLockStartedThisTick(snapshot, _entityId, tickIndex))
            {
                return false;
            }

            if (CanStartExecutionLockedWindupCombat(snapshot, tickIndex))
            {
                return true;
            }

            if (_combatCapability == null ||
                _combatCapability.AttackTimingSettings.WindupTicks != 0 ||
                !snapshot.TryGetEntityExecutionLockState(_entityId, out var executionLockState))
            {
                return false;
            }

            return executionLockState.phase == EntityExecutionPhase.Move &&
                   EntityExecutionLockQueries.IsLocked(executionLockState, tickIndex);
        }

        private bool CanStartExecutionLockedWindupCombat(WorldSnapshot snapshot, int tickIndex)
        {
            if (_combatCapability == null ||
                _combatCapability.AttackTimingSettings.WindupTicks <= 0 ||
                UsesReceiverOwnedContactCadence() ||
                !snapshot.TryGetEntityExecutionLockState(_entityId, out var executionLockState))
            {
                return false;
            }

            // Preserve the existing short lock behavior while preventing long move-occupancy
            // presentation windows from starving enemy windup start during patrol/chase closure.
            return executionLockState.phase == EntityExecutionPhase.Move &&
                   EntityExecutionLockQueries.IsLocked(executionLockState, tickIndex) &&
                   executionLockState.unlockTickExclusive > tickIndex + 1;
        }

        private static bool ShouldWriteActionState(
            bool hadPreviousAction,
            in EnemyActionRuntimeState previousAction,
            in EnemyActionRuntimeState nextAction)
        {
            return hadPreviousAction ||
                   nextAction.IsActive ||
                   nextAction.sequence != 0 ||
                   !AreEqual(previousAction, nextAction);
        }

        private static bool AreEqual(
            in EnemyActionRuntimeState left,
            in EnemyActionRuntimeState right)
        {
            return left.kind == right.kind &&
                   left.sequence == right.sequence &&
                   left.lockedTargetEntityId == right.lockedTargetEntityId &&
                   left.direction == right.direction &&
                   left.startTick == right.startTick &&
                   left.executeTick == right.executeTick &&
                   left.executionAttempted == right.executionAttempted &&
                   left.hasLockedCombatAnchor == right.hasLockedCombatAnchor &&
                   left.lockedCombatAnchor.Equals(right.lockedCombatAnchor) &&
                   left.hasLockedForwardCellImpact == right.hasLockedForwardCellImpact &&
                   left.lockedAttackBaseCell.Equals(right.lockedAttackBaseCell) &&
                   left.lockedTargetCell.Equals(right.lockedTargetCell) &&
                   left.lockedAttackDirection == right.lockedAttackDirection;
        }
    }

    internal sealed class EnemyActionStateEntityLogicFactory : IEntityLogicFactory
    {
        private readonly EnemyEntityLogicFactory _enemyLogicFactory;

        public EnemyActionStateEntityLogicFactory()
            : this(EnemyAiRuntimeDefinition.CreateDefaultMelee())
        {
        }

        public EnemyActionStateEntityLogicFactory(
            EnemyAiRuntimeDefinition defaultDefinition,
            IReadOnlyDictionary<int, EnemyAiRuntimeDefinition> definitionsByEntityId = null,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId = null)
        {
            _enemyLogicFactory = new EnemyEntityLogicFactory(defaultDefinition, definitionsByEntityId, definitionsByArchetypeId);
        }

        public bool CanCreate(in EntityLogicCreationContext context)
        {
            if (!_enemyLogicFactory.CanCreate(context))
            {
                return false;
            }

            var definition = _enemyLogicFactory.ResolveDefinition(context.Snapshot, context.Entity);
            return definition.Capabilities.TryGetCombat(out _);
        }

        public IEntityLogic Create(in EntityLogicCreationContext context)
        {
            var entity = context.Entity;
            return new EnemyActionStateLogic(entity.entityId, _enemyLogicFactory.ResolveDefinition(context.Snapshot, entity));
        }
    }

    internal static class EnemyActionStateTargeting
    {
        public static bool TryResolveStartAction(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            IAttackDecisionStrategy attackDecisionStrategy,
            in DetectionSettings detectionSettings,
            in AttackDecisionSettings attackDecisionSettings,
            out EntityState target,
            out Direction direction)
        {
            target = default;
            direction = source.facing;

            if (!detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out target) ||
                !attackDecisionStrategy.IsTargetInRange(source, target, attackDecisionSettings))
            {
                target = default;
                return false;
            }

            direction = ResolveFacing(source, target);
            return true;
        }

        public static bool TryResolveLockedTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyActionRuntimeState actionState,
            IAttackDecisionStrategy attackDecisionStrategy,
            in DetectionSettings detectionSettings,
            in AttackDecisionSettings attackDecisionSettings,
            out EntityState target)
        {
            target = default;

            if (!snapshot.TryGetEntity(actionState.lockedTargetEntityId, out target) ||
                !IsValidLockedTargetForCurrentAction(snapshot, source, target, detectionSettings) ||
                !attackDecisionStrategy.IsTargetInRange(source, target, attackDecisionSettings))
            {
                target = default;
                return false;
            }

            return true;
        }

        public static EnemyAiMode ResolveFallbackAiMode(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            in DetectionSettings detectionSettings)
        {
            return detectionStrategy.TryFindTarget(snapshot, source, detectionSettings, out _)
                ? EnemyAiMode.Chase
                : EnemyAiMode.Patrol;
        }

        public static Direction ResolveFacing(
            in EntityState source,
            in EntityState target)
        {
            if (source.position.face != target.position.face)
            {
                return source.facing;
            }

            var planarDelta = target.position.PlanarPosition - source.position.PlanarPosition;
            var horizontalDistance = Mathf.Abs(planarDelta.x);
            var verticalDistance = Mathf.Abs(planarDelta.y);

            if (horizontalDistance >= verticalDistance &&
                planarDelta.x != 0)
            {
                return planarDelta.x > 0
                    ? Direction.Right
                    : Direction.Left;
            }

            if (planarDelta.y != 0)
            {
                return planarDelta.y > 0
                    ? Direction.Up
                    : Direction.Down;
            }

            // Overlap contact attacks keep the actor's committed facing.
            return source.facing;
        }

        public static bool IsLockedTargetValidForCurrentAction(
            WorldSnapshot snapshot,
            in EntityState source,
            in EnemyActionRuntimeState actionState,
            in DetectionSettings detectionSettings)
        {
            return snapshot.TryGetEntity(actionState.lockedTargetEntityId, out var target) &&
                   IsValidLockedTargetForCurrentAction(snapshot, source, target, detectionSettings);
        }

        private static bool IsValidLockedTargetForCurrentAction(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in DetectionSettings detectionSettings)
        {
            if (target.entityId == source.entityId ||
                target.type != EntityType.Unit ||
                target.teamId == source.teamId ||
                target.hp <= 0)
            {
                return false;
            }

            if (target.boardPresence != EntityBoardPresence.Occupying ||
                !snapshot.Topology.IsFaceActive(target.position.face))
            {
                return false;
            }

            if (!snapshot.TryGetResolvedSpatialState(target.entityId, out var spatialState) ||
                !ModifierQuery.ShouldParticipateInEnemyCurrentLockRetention(
                    spatialState,
                    new CurrentEnemyLockRetentionEvidence(source.entityId, target.entityId)))
            {
                return false;
            }

            return !target.markedForDeath || detectionSettings.CanTargetMarkedForDeath;
        }
    }
}

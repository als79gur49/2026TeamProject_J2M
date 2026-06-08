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
        private readonly List<EntityState> _sharedCellUnits = new();
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
                var clearedAction = EnemyActionQueries.Clear(previousAction);
                if (ShouldWriteActionState(hasPreviousAction, previousAction, clearedAction))
                {
                    writeContext.SetEnemyActionState(_entityId, clearedAction);
                    transitions.Add(new EnemyActionTransition(_entityId, previousAction, clearedAction));
                }

                return;
            }

            if (!EnemyParticipationPolicy.CanParticipateOnCurrentTopology(snapshot, source))
            {
                var clearedAction = EnemyActionQueries.Clear(previousAction);
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
                EnemyActionStage.AfterAttack => CommitAfterAttack(source, previousAction, input.TickIndex),
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
                ReleaseCombatLocomotionHoldIfNeeded(snapshot, workingAction, writeContext);
                workingAction = EnemyActionQueries.Clear(workingAction);
            }

            if (UsesReceiverOwnedContactCadence() &&
                workingAction.IsActive &&
                workingAction.executionAttempted)
            {
                ReleaseCombatLocomotionHoldIfNeeded(snapshot, workingAction, writeContext);
                workingAction = EnemyActionQueries.Clear(workingAction);
            }

            if (source.hp <= 0 ||
                source.markedForDeath ||
                source.boardPresence != EntityBoardPresence.Occupying ||
                source.aiMode == EnemyAiMode.Dead)
            {
                return EnemyActionQueries.Clear(workingAction);
            }

            if (source.aiMode != EnemyAiMode.Attack)
            {
                if (source.aiMode == EnemyAiMode.Recover &&
                    workingAction.IsActive &&
                    workingAction.executionAttempted)
                {
                    return workingAction;
                }

                ReleaseCombatLocomotionHoldIfNeeded(snapshot, workingAction, writeContext);
                return EnemyActionQueries.Clear(workingAction);
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
                        ReleaseCombatLocomotionHoldIfNeeded(snapshot, releasedAction, writeContext);
                        return releasedAction;
                    }

                    var authoritativeFacing = EnemyActionQueries.ResolveAuthoritativeFacing(workingAction);
                    if (source.facing != authoritativeFacing)
                    {
                        writeContext.SetFacing(_entityId, authoritativeFacing);
                    }

                    return workingAction;
                }

                if (EnemyActionStateTargeting.TryResolveLockedTarget(
                        snapshot,
                        source,
                        workingAction,
                        _combatCapability,
                        _detectionSettings,
                        out _))
                {
                    if (source.facing != workingAction.direction)
                    {
                        writeContext.SetFacing(_entityId, workingAction.direction);
                    }

                    return workingAction;
                }

                ApplyCancelFallback(snapshot, source, writeContext);
                ReleaseCombatLocomotionHoldIfNeeded(snapshot, workingAction, writeContext);
                return EnemyActionQueries.Clear(workingAction);
            }

            if (!EnemyActionStateTargeting.TryResolveStartAction(
                    snapshot,
                    source,
                    _detectionStrategy,
                    _combatCapability,
                    _detectionSettings,
                    out var target,
                    out var direction))
            {
                ApplyCancelFallback(snapshot, source, writeContext);
                return EnemyActionQueries.Clear(previousAction);
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

                return EnemyActionQueries.Clear(previousAction);
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
            writeContext.SetFacing(_entityId, direction);
            HoldCombatLocomotionAtCurrentPose(snapshot, source, writeContext);
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
                writeContext.SetFacing(_entityId, authoritativeFacing);
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
                tickIndex + impactDelayTicks,
                snapshot.TopologyRevision);

            writeContext.AddPendingCellImpact(impact);
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

        private static EnemyActionRuntimeState CommitAfterAttack(
            in EntityState source,
            in EnemyActionRuntimeState previousAction,
            int tickIndex)
        {
            if (!previousAction.IsActive)
            {
                return previousAction;
            }

            if (source.hp <= 0 ||
                source.markedForDeath)
            {
                return EnemyActionQueries.Clear(previousAction);
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
                _detectionSettings,
                _combatCapability,
                passiveContactCapability: null,
                _sharedCellUnits);

            if (fallbackMode != source.aiMode ||
                source.aiStateTimer != 0)
            {
                writeContext.ApplyEnemyAiState(_entityId, fallbackMode, 0);
            }
        }

        private void HoldCombatLocomotionAtCurrentPose(
            WorldSnapshot snapshot,
            in EntityState source,
            IEnemyActionCommitContext writeContext)
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
                writeContext.SetUnitKinematicState(
                    source.entityId,
                    UnitKinematicRuntimeState.CreateHeldFreeze(kinematicPose.State));
            }
        }

        private void ReleaseCombatLocomotionHoldIfNeeded(
            WorldSnapshot snapshot,
            in EnemyActionRuntimeState actionState,
            IEnemyActionCommitContext writeContext)
        {
            if (!actionState.hasLockedCombatAnchor ||
                !snapshot.TryGetUnitKinematicPose(_entityId, out var pose) ||
                !pose.HasAuthoritativeState ||
                pose.Mode != MotionMode.Held ||
                !TryResolveHeldResumeVelocity(pose.State, out var velocity))
            {
                return;
            }

            writeContext.SetUnitKinematicState(
                _entityId,
                UnitKinematicRuntimeState.CreateVoluntaryResumeFromHeld(pose.State, velocity));
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
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyAiRuntimeDefinition> definitionsByArchetypeId = null,
            bool hasDefaultDefinition = true)
        {
            _enemyLogicFactory = new EnemyEntityLogicFactory(defaultDefinition, definitionsByEntityId, definitionsByArchetypeId, hasDefaultDefinition);
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
            EnemyCombatCapabilityRuntime combatCapability,
            in DetectionSettings detectionSettings,
            out EntityState target,
            out Direction direction)
        {
            target = default;
            direction = source.facing;

            if (!EnemyTargetSelector.TryAcquireFreshTarget(
                    snapshot,
                    source,
                    detectionStrategy,
                    detectionSettings,
                    out target,
                    out _) ||
                !EnemyTargetEligibilityPolicy.EvaluateCombatActionValidate(snapshot, source, target, combatCapability).Eligible)
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
            EnemyCombatCapabilityRuntime combatCapability,
            in DetectionSettings detectionSettings,
            out EntityState target)
        {
            target = default;

            if (!snapshot.TryGetEntity(actionState.lockedTargetEntityId, out target) ||
                !IsValidLockedTargetForCurrentAction(snapshot, source, target, detectionSettings) ||
                !EnemyTargetEligibilityPolicy.EvaluateCombatActionValidate(snapshot, source, target, combatCapability).Eligible)
            {
                target = default;
                return false;
            }

            return true;
        }

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

            if (!EnemyTargetSelector.TryAcquireFreshTarget(
                    snapshot,
                    source,
                    detectionStrategy,
                    detectionSettings,
                    out target,
                    out _) ||
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
            in DetectionSettings detectionSettings,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            List<EntityState> sharedCellUnits)
        {
            if (EnemyTargetSelector.TryAcquireFreshTarget(
                    snapshot,
                    source,
                    detectionStrategy,
                    detectionSettings,
                    out _,
                    out _))
            {
                return EnemyAiMode.Chase;
            }

            if (sharedCellUnits != null &&
                EnemyTargetSelector.TryFindLocalEngagementTarget(
                    snapshot,
                    source,
                    combatCapability,
                    passiveContactCapability,
                    sharedCellUnits,
                    out _,
                    out _))
            {
                return EnemyAiMode.Chase;
            }

            return EnemyAiMode.Patrol;
        }

        public static EnemyAiMode ResolveFallbackAiMode(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            in DetectionSettings detectionSettings)
        {
            return EnemyTargetSelector.TryAcquireFreshTarget(
                    snapshot,
                    source,
                    detectionStrategy,
                    detectionSettings,
                    out _,
                    out _)
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

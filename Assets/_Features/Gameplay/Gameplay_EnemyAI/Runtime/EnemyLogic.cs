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
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability,
            in EnemyAiCommonSettings commonSettings,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions);
    }

    public sealed class EnemyLogic : IEnemyAiStateLogic, IPreMovementStateLogic, IMovementEntityLogic, IMovementEntityDebugLogic, IAttackEntityLogic, IEntityLogicSourceBinding, ITileFeatureDefinitionContextReceiver
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
        private readonly IEnemyAiStateResolver _stateResolver;
        private readonly bool _usesChargeStateResolver;
        private readonly List<EntityState> _sharedCellUnits = new();
        private IReadOnlyList<TileFeatureRuntimeDefinition> _tileFeatureDefinitions = Array.Empty<TileFeatureRuntimeDefinition>();
        private int _pendingChaseBlockedReactionDecisionTick = -1;
        private Direction _pendingChaseBlockedDirectionToAvoid = Direction.None;

        public EnemyLogic(int entityId)
        {
            throw new InvalidOperationException(
                "Enemy AI runtime definition must be explicit. Use an EnemyAiProfile or EnemyAiRuntimeDefinition constructor.");
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
        }

        public int ControlledEntityId => _entityId;

        public void BindTileFeatureDefinitions(IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            _tileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>();
        }

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
                _passiveContactCapability,
                _movementSkillCapability,
                _commonSettings,
                _chargeTimingSettings,
                _detectionSettings,
                _tileFeatureDefinitions);

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

                if (ShouldLogUnchangedDecision(source, decision))
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
                if (_utilityCapability != null &&
                    snapshot.TryGetEnemyUtilityState(_entityId, out var currentUtilityState))
                {
                    CancelEnemyUtilityWindups(currentUtilityState, writeContext, updates);
                }

                return;
            }

            if (!EnemyParticipationPolicy.CanParticipateOnCurrentTopology(snapshot, source))
            {
                if (HasJumpMovementSkill())
                {
                    CommitJumpTopologySuspend(snapshot, input.TickIndex, writeContext, updates);
                }

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

            if (_utilityCapability != null)
            {
                CommitEnemyUtilityState(snapshot, in input, source, writeContext, updates);
            }

            if (!TryGetControllableEnemy(snapshot, out source))
            {
                return;
            }

            var consumedPendingBlockedReaction = TryPreparePendingEnemyBlockedReaction(
                snapshot,
                source,
                input.TickIndex,
                writeContext,
                updates);

            if (source.enemyAttackCooldownTicks > 0)
            {
                var nextAttackCooldown = source.enemyAttackCooldownTicks - 1;
                writeContext.SetEnemyAttackCooldown(
                    _entityId,
                    nextAttackCooldown,
                    source.enemyAttackCooldownTotalTicks);
                updates.Add(
                    $"EnemyAttackCooldownUpdated|E={_entityId}|From={source.enemyAttackCooldownTicks}|To={nextAttackCooldown}");
            }

            if (source.enemyLocomotionCooldownTicks > 0)
            {
                if (consumedPendingBlockedReaction)
                {
                    updates.Add(
                        $"EnemyLocomotionCooldownClearedByBlockedReaction|E={_entityId}|From={source.enemyLocomotionCooldownTicks}|To=0");
                }
                else
                {
                    var nextCooldown = source.enemyLocomotionCooldownTicks - 1;
                    writeContext.SetEnemyLocomotionCooldown(_entityId, nextCooldown);
                    updates.Add(
                        $"EnemyLocomotionCooldownUpdated|E={_entityId}|From={source.enemyLocomotionCooldownTicks}|To={nextCooldown}");
                }
            }

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

            if (ShouldSuppressAutonomousMovementAndFacing(snapshot, source, input.TickIndex))
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

            if (debugEvents == null)
            {
                throw new ArgumentNullException(nameof(debugEvents));
            }

            if (!TryGetControllableEnemy(snapshot, out var source) ||
                HasRawMovementIntentForSource(rawIntents, source.entityId) ||
                !ShouldSuppressOrdinaryMovementForLocalEngagement(snapshot, source))
            {
                return;
            }

            debugEvents.Add(
                $"OrdinaryMovementSuppressedByLocalEngagement|E={source.entityId}|Mode={source.aiMode}|Reason=OrdinaryMovementSuppressedByLocalEngagement");
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

            if (_passiveContactCapability != null &&
                CanCollectPassiveContact(snapshot) &&
                TryResolvePassiveContactTarget(snapshot, source, out var passiveContactTarget) &&
                _passiveContactCapability.AttackDecisionStrategy.IsTargetInRange(
                    source,
                    passiveContactTarget,
                    _passiveContactCapability.AttackDecisionSettings))
            {
                buffer.Add(new RawAttackIntent(
                    source.entityId,
                    _commonSettings.AttackPriority,
                    passiveContactTarget.entityId,
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

        private bool TryPreparePendingEnemyBlockedReaction(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            IPreMovementStateCommitContext writeContext,
            List<string> updates)
        {
            if (!snapshot.TryGetPendingEnemyBlockedReaction(_entityId, out var reaction))
            {
                return false;
            }

            if (reaction.IsExpiredBeforeDecision(tickIndex))
            {
                writeContext.ClearPendingEnemyBlockedReaction(_entityId);
                updates.Add(
                    $"PendingEnemyBlockedReactionCleared|E={_entityId}|Reason=Expired|Created={reaction.CreatedTick}|Expire={reaction.ExpireTick}|Tick={tickIndex}");
                return false;
            }

            if (reaction.Kind != EnemyBlockedReactionKind.KinematicContinuationTargetBlocked ||
                reaction.EnemyEntityId != _entityId)
            {
                writeContext.ClearPendingEnemyBlockedReaction(_entityId);
                updates.Add(
                    $"PendingEnemyBlockedReactionCleared|E={_entityId}|Reason=InvalidReaction|Kind={reaction.Kind}|ReactionEntity={reaction.EnemyEntityId}");
                return false;
            }

            if (reaction.ModeAtBlock != source.aiMode ||
                (source.aiMode != EnemyAiMode.Chase &&
                 source.aiMode != EnemyAiMode.Patrol))
            {
                writeContext.ClearPendingEnemyBlockedReaction(_entityId);
                updates.Add(
                    $"PendingEnemyBlockedReactionCleared|E={_entityId}|Reason=ModeMismatch|CurrentMode={source.aiMode}|BlockMode={reaction.ModeAtBlock}");
                return false;
            }

            if (source.position != reaction.SourceCell)
            {
                writeContext.ClearPendingEnemyBlockedReaction(_entityId);
                updates.Add(
                    $"PendingEnemyBlockedReactionCleared|E={_entityId}|Reason=SourceMismatch|Current={source.position}|ReactionSource={reaction.SourceCell}");
                return false;
            }

            if (!DirectionUtility.IsCardinal(reaction.BlockedDirection) ||
                !IsSettledForBlockedReactionDecision(snapshot, source))
            {
                return false;
            }

            if (snapshot.TryGetEnemyActionState(_entityId, out var actionState) &&
                actionState.IsActive)
            {
                writeContext.ClearPendingEnemyBlockedReaction(_entityId);
                updates.Add(
                    $"PendingEnemyBlockedReactionCleared|E={_entityId}|Reason=ActionActive|Kind={actionState.kind}");
                return false;
            }

            if (ShouldSuppressAutonomousMovementAndFacing(snapshot, source, tickIndex))
            {
                return false;
            }

            if (source.aiMode == EnemyAiMode.Chase)
            {
                _pendingChaseBlockedReactionDecisionTick = tickIndex;
                _pendingChaseBlockedDirectionToAvoid = reaction.BlockedDirection;
            }
            else if (EnemyPatrolDecisionPlanner.TryResolveBlockedReactionFacingOverride(
                         _patrolStrategyKind,
                         reaction,
                         out var patrolFacing) &&
                     patrolFacing != source.facing)
            {
                writeContext.SetFacing(_entityId, patrolFacing);
                updates.Add(
                    $"PendingEnemyBlockedReactionPatrolFacing|E={_entityId}|From={source.facing}|To={patrolFacing}|Direction={reaction.BlockedDirection}");
            }

            writeContext.SetEnemyLocomotionCooldown(_entityId, 0);
            writeContext.ClearPendingEnemyBlockedReaction(_entityId);
            updates.Add(
                $"PendingEnemyBlockedReactionConsumed|E={_entityId}|Mode={source.aiMode}|Direction={reaction.BlockedDirection}|Source={reaction.SourceCell}|BlockedTarget={reaction.BlockedTargetCell}|Tick={tickIndex}");
            return true;
        }

        private static bool IsSettledForBlockedReactionDecision(WorldSnapshot snapshot, in EntityState source)
        {
            return !snapshot.TryGetUnitKinematicPose(source.entityId, out var pose) ||
                   !pose.HasAuthoritativeState ||
                   pose.IsSettledAtAnchor;
        }

        private bool TryGetChaseBlockedDirectionToAvoid(int tickIndex, out Direction direction)
        {
            if (_pendingChaseBlockedReactionDecisionTick == tickIndex &&
                DirectionUtility.IsCardinal(_pendingChaseBlockedDirectionToAvoid))
            {
                direction = _pendingChaseBlockedDirectionToAvoid;
                return true;
            }

            direction = Direction.None;
            return false;
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

        private bool ShouldSuppressAutonomousMovementAndFacing(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex)
        {
            return ShouldSuppressMovementForJump(snapshot, tickIndex) ||
                   ShouldSuppressMovementForActivePhasedState(snapshot) ||
                   ShouldSuppressMovementForGlide(snapshot) ||
                   ShouldSuppressMovementForUtility(snapshot, source, tickIndex) ||
                   ShouldSuppressMovementForImminentUtilityWindup(snapshot, source, tickIndex) ||
                   ShouldSuppressMovementForCharge(snapshot);
        }

        private bool ShouldSuppressMovementForActivePhasedState(WorldSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.TryGetPhasedState(_entityId, out var phasedState) &&
                   phasedState.IsActive;
        }

        private bool ShouldSuppressMovementForUtility(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex)
        {
            if (_utilityCapability == null ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, source) ||
                !snapshot.TryGetEnemyUtilityState(_entityId, out var utilityState))
            {
                return false;
            }

            if (!utilityState.HasEffectCount(_utilityCapability.Effects.Count))
            {
                return false;
            }

            for (var effectIndex = 0; effectIndex < _utilityCapability.Effects.Count; effectIndex++)
            {
                var effectState = utilityState.EffectStates[effectIndex];
                var effectRuntime = _utilityCapability.Effects[effectIndex];
                if (effectState.effectKind != effectRuntime.Kind ||
                    !IsUtilityMovementSuppressionWindowActive(effectRuntime, effectState, tickIndex))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool ShouldSuppressMovementForImminentUtilityWindup(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex)
        {
            if (_utilityCapability == null ||
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, source))
            {
                return false;
            }

            var utilityState = GetUtilityStateForStartPrediction(snapshot);
            List<SummonedEntitySnapshotEntry> summonedEntries = null;
            var hasEnumeratedSummonedEntries = false;
            for (var effectIndex = 0; effectIndex < _utilityCapability.Effects.Count; effectIndex++)
            {
                var effectRuntime = _utilityCapability.Effects[effectIndex];
                var effectState = GetEffectStateForStartPrediction(utilityState, effectIndex, effectRuntime);

                if (!CanStartDelayedUtilityWindupThisTick(effectRuntime, effectState))
                {
                    continue;
                }

                if (effectRuntime.Kind == EnemyUtilityEffectKind.SummonMinion)
                {
                    summonedEntries ??= new List<SummonedEntitySnapshotEntry>();
                    if (!hasEnumeratedSummonedEntries)
                    {
                        snapshot.EnumerateSummonedEntityStatesOrdered(summonedEntries);
                        hasEnumeratedSummonedEntries = true;
                    }

                    if (EnemyUtilitySummonPolicy.IsMaxAliveReached(
                            snapshot,
                            summonedEntries,
                            source.entityId,
                            effectIndex,
                            effectRuntime.Summon))
                    {
                        continue;
                    }
                }

                return true;
            }

            return false;
        }

        private EnemyUtilityRuntimeState GetUtilityStateForStartPrediction(WorldSnapshot snapshot)
        {
            return snapshot.TryGetEnemyUtilityState(_entityId, out var utilityState) &&
                   utilityState.HasEffectCount(_utilityCapability.Effects.Count)
                ? utilityState
                : EnemyUtilityStateQueries.CreateInitialState(_utilityCapability);
        }

        private static EnemyUtilityEffectState GetEffectStateForStartPrediction(
            EnemyUtilityRuntimeState utilityState,
            int effectIndex,
            EnemyUtilityEffectRuntime effectRuntime)
        {
            var effectState = utilityState.EffectStates[effectIndex];
            return effectState.effectKind == effectRuntime.Kind
                ? effectState
                : CreateInitialUtilityEffectState(effectRuntime);
        }

        private static EnemyUtilityEffectState CreateInitialUtilityEffectState(EnemyUtilityEffectRuntime effectRuntime)
        {
            return new EnemyUtilityEffectState
            {
                effectKind = effectRuntime.Kind,
                cooldownTicksRemaining = effectRuntime.InitialDelayTicks,
            };
        }

        private static bool CanStartDelayedUtilityWindupThisTick(
            EnemyUtilityEffectRuntime effectRuntime,
            in EnemyUtilityEffectState effectState)
        {
            if (!UsesDelayedUtilityWindup(effectRuntime) ||
                !SuppressesMovementDuringWindup(effectRuntime) ||
                effectState.effectKind != effectRuntime.Kind ||
                effectState.phase != EnemyUtilityEffectPhase.None ||
                effectState.movementSuppressionUntilTickInclusive > 0)
            {
                return false;
            }

            var nextCooldownTicks = effectState.cooldownTicksRemaining > 0
                ? Mathf.Max(0, effectState.cooldownTicksRemaining - 1)
                : 0;
            return nextCooldownTicks == 0;
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

        private bool HasJumpMovementSkill()
        {
            return _movementSkillCapability != null &&
                   _movementSkillCapability.Kind == MovementSkillStrategyKind.JumpToLockedTarget;
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

            if (nextState.Phase == EnemyGlidePhase.Ready &&
                (!nextState.InitialDelayInitialized || nextState.InitialDelayTicksRemaining > 0) &&
                _movementSkillCapability.GlideTimingSettings.InitialDelayTicks > 0)
            {
                var delayedState = EnemyGlideQueries.TickInitialDelay(
                    nextState,
                    _movementSkillCapability.GlideTimingSettings.InitialDelayTicks);
                if (!AreEqual(nextState, delayedState))
                {
                    nextState = delayedState;
                    hasPreviousState = true;
                    changed = true;
                    AppendGlideUpdate(
                        updates,
                        _entityId,
                        nextState.InitialDelayTicksRemaining > 0 ? "InitialDelayTick" : "InitialDelayReady",
                        nextState);
                }
            }

            var canStartGlide = source.aiMode == EnemyAiMode.Chase &&
                                EnemyGlideQueries.CanStart(hasPreviousState, nextState, input.TickIndex);
            if (canStartGlide &&
                !HasUnsettledVoluntaryKinematicPose(snapshot, source.entityId) &&
                _detectionStrategy.TryFindTarget(snapshot, source, _detectionSettings, out var chaseTarget) &&
                TryResolveGlideStartLockedStep(snapshot, source, chaseTarget, out var lockedStep))
            {
                nextState = EnemyGlideQueries.Start(
                    nextState,
                    input.TickIndex,
                    _movementSkillCapability.GlideTimingSettings,
                    lockedStep,
                    chaseTarget.entityId);
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

                        var activeLockedStep = default(Vector2Int?);
                        var activeLockedTargetEntityId = 0;
                        if (TryResolveGlideActiveStartTarget(
                                snapshot,
                                source,
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
                        AppendGlideUpdate(updates, _entityId, "EnterActive", nextState);
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
                                AppendGlideUpdate(updates, _entityId, "WantsRecover", nextState);
                            }

                            return changed;
                        }

                        if (!nextState.WantsRecover)
                        {
                            nextState = EnemyGlideQueries.MarkActiveWantsRecover(nextState);
                            hasPreviousState = true;
                            changed = true;
                            AppendGlideUpdate(updates, _entityId, "WantsRecover", nextState);
                        }

                        nextState = EnemyGlideQueries.BeginRecovery(nextState, input.TickIndex);
                        hasPreviousState = true;
                        changed = true;
                        AppendGlideUpdate(updates, _entityId, "EnterRecovery", nextState);
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
                        continue;

                    case EnemyGlidePhase.Cooldown:
                        if (input.TickIndex >= nextState.CooldownUntilTickExclusive &&
                            input.TickIndex > nextState.LastExitedTick &&
                            source.aiMode != EnemyAiMode.Chase)
                        {
                            nextState = EnemyGlideQueries.ClearRuntimeActivityPreservingInitialDelay(nextState);
                            hasPreviousState = nextState.HasAuthoritativeRecord;
                            changed = true;
                            AppendGlideUpdate(updates, _entityId, "Ready", nextState);
                        }

                        return changed;

                    case EnemyGlidePhase.Ready:
                    default:
                        return changed;
                }
            }

            return changed;
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

        private bool ShouldSuppressMovementForCharge(WorldSnapshot snapshot)
        {
            return TryGetChargeState(snapshot, out var chargeState) &&
                   (chargeState.phase == EnemyChargePhase.Windup ||
                    chargeState.phase == EnemyChargePhase.Recover);
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

        private bool TryResolveGlideStartLockedStep(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            out Vector2Int lockedStep)
        {
            lockedStep = Vector2Int.zero;
            if (_chaseStrategy.TryBuildMovementIntent(
                    snapshot,
                    source,
                    target,
                    _commonSettings,
                    _chaseSettings,
                    _tileFeatureDefinitions,
                    out var chaseIntent) &&
                TryResolveGlideLockedStep(source, chaseIntent.Destination, out lockedStep))
            {
                return true;
            }

            return TryResolveGlideLockedStepTowardTarget(source, target, _chaseSettings, out lockedStep);
        }

        private bool TryResolveGlideActiveStartTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            out EntityState target,
            out Vector2Int lockedStep)
        {
            target = default;
            lockedStep = Vector2Int.zero;
            if (!_detectionStrategy.TryFindTarget(
                    snapshot,
                    source,
                    _detectionSettings,
                    out target,
                    new EnemyDetectionQueryOptions(LineOfSightSolidBlockerPolicy.IgnoreSolid)))
            {
                return false;
            }

            return TryResolveGlideStartLockedStep(snapshot, source, target, out lockedStep);
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

        private bool TryResolvePassiveContactTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            out EntityState target)
        {
            target = default;
            return EnemyLocalContactPolicy.TryFindPassiveContactCandidate(
                snapshot,
                source,
                _passiveContactCapability,
                _sharedCellUnits,
                out target,
                out _);
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
            var canParticipateOnCurrentTopology = EnemyParticipationPolicy.CanParticipateOnCurrentTopology(snapshot, source);
            var isHardInvalidParticipant = EnemyParticipationPolicy.IsHardInvalidParticipant(source);
            var isControllableParticipant = canParticipateOnCurrentTopology && !isHardInvalidParticipant;
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

            if (!canParticipateOnCurrentTopology &&
                !isHardInvalidParticipant)
            {
                SuspendEnemyUtilityForTopologyParticipationLoss(currentState, writeContext, updates);
                return;
            }

            if (isHardInvalidParticipant)
            {
                CancelEnemyUtilityWindups(currentState, writeContext, updates);
                return;
            }

            var nextEffectStates = new EnemyUtilityEffectState[currentState.EffectStates.Count];
            var hasAnyChange = false;
            List<SummonedEntitySnapshotEntry> summonedEntries = null;
            var hasEnumeratedSummonedEntries = false;

            for (var effectIndex = 0; effectIndex < currentState.EffectStates.Count; effectIndex++)
            {
                var previousEffectState = currentState.EffectStates[effectIndex];
                var nextEffectState = previousEffectState;
                var effectRuntime = _utilityCapability.Effects[effectIndex];
                var triggered = false;
                nextEffectState.effectKind = effectRuntime.Kind;
                if (nextEffectState.movementSuppressionUntilTickInclusive > 0 &&
                    input.TickIndex > nextEffectState.movementSuppressionUntilTickInclusive)
                {
                    nextEffectState.movementSuppressionUntilTickInclusive = 0;
                }

                if (nextEffectState.phase == EnemyUtilityEffectPhase.Recover)
                {
                    AdvanceUtilityRecover(effectRuntime, input.TickIndex, ref nextEffectState);
                    nextEffectStates[effectIndex] = nextEffectState;
                    if (!AreEqual(previousEffectState, nextEffectState))
                    {
                        hasAnyChange = true;
                        updates.Add(
                            $"EnemyUtilityRecoverUpdated|E={_entityId}|Effect={effectIndex}|Phase={nextEffectState.phase}|RecoverStart={nextEffectState.recoverStartTick}|RecoverEnd={nextEffectState.recoverEndTickExclusive}|Cooldown={nextEffectState.cooldownTicksRemaining}");
                    }

                    continue;
                }

                if (effectRuntime.Kind == EnemyUtilityEffectKind.GravityFieldAura)
                {
                    if (nextEffectState.phase == EnemyUtilityEffectPhase.Windup)
                    {
                        if (input.TickIndex >= nextEffectState.windupEndTick)
                        {
                            triggered = true;
                            var fieldStartTick = input.TickIndex;
                            var fieldEndTickExclusive = fieldStartTick + effectRuntime.GravityFieldAura.FieldDurationTicks;
                            var fieldOriginCell = source.position;
                            var fieldId = EnemyGravityFieldAuraFieldIds.Compute(
                                _entityId,
                                effectIndex,
                                nextEffectState.activationSequence);
                            writeContext.SetEnemyGravityFieldAuraFieldState(
                                fieldId,
                                new EnemyGravityFieldAuraFieldState(
                                    _entityId,
                                    effectIndex,
                                    nextEffectState.activationSequence,
                                    fieldOriginCell,
                                    effectRuntime.GravityFieldAura.Radius,
                                    fieldStartTick,
                                    fieldEndTickExclusive,
                                    effectRuntime.GravityFieldAura.BlocksPush,
                                    effectRuntime.GravityFieldAura.BlocksFlip,
                                    effectRuntime.GravityFieldAura.BlocksDestroy));
                            nextEffectState.windupStartTick = 0;
                            nextEffectState.windupEndTick = 0;
                            nextEffectState.activeStartTick = 0;
                            nextEffectState.activeEndTickExclusive = 0;
                            nextEffectState.activeOriginCell = default;
                            EnterUtilityRecoverOrClear(
                                effectRuntime,
                                input.TickIndex,
                                ref nextEffectState,
                                startCooldownAfterRecover: true);

                            EmitEnemyUtilityTriggerIntent(
                                writeContext,
                                effectIndex,
                                effectRuntime,
                                input.TickIndex,
                                fieldOriginCell);
                            updates.Add(
                                $"EnemyUtilityAttackStarted|E={_entityId}|Effect={effectIndex}|Sequence={nextEffectState.activationSequence}|Field={fieldId}|Start={fieldStartTick}|End={fieldEndTickExclusive}|Origin={fieldOriginCell}|RecoverEnd={nextEffectState.recoverEndTickExclusive}");
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
                            nextEffectState.windupEndTick = input.TickIndex + effectRuntime.GravityFieldAura.WindupTicks;
                            nextEffectState.recoverStartTick = 0;
                            nextEffectState.recoverEndTickExclusive = 0;
                            nextEffectState.activeStartTick = 0;
                            nextEffectState.activeEndTickExclusive = 0;
                            nextEffectState.activeOriginCell = default;
                            nextEffectState.activationSequence = Math.Max(0, nextEffectState.activationSequence) + 1;
                            if (effectRuntime.GravityFieldAura.SuppressMovementDuringWindup)
                            {
                                nextEffectState.movementSuppressionUntilTickInclusive = nextEffectState.windupEndTick;
                            }

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

                if (effectRuntime.Kind == EnemyUtilityEffectKind.SummonMinion)
                {
                    if (nextEffectState.phase == EnemyUtilityEffectPhase.Windup)
                    {
                        if (input.TickIndex >= nextEffectState.windupEndTick)
                        {
                            triggered = true;
                            EmitEnemyUtilityTriggerIntent(writeContext, effectIndex, effectRuntime, input.TickIndex);

                            EnterUtilityRecoverOrClear(effectRuntime, input.TickIndex, ref nextEffectState);
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
                            summonedEntries ??= new List<SummonedEntitySnapshotEntry>();
                            if (!hasEnumeratedSummonedEntries)
                            {
                                snapshot.EnumerateSummonedEntityStatesOrdered(summonedEntries);
                                hasEnumeratedSummonedEntries = true;
                            }

                            if (!EnemyUtilitySummonPolicy.IsMaxAliveReached(
                                    snapshot,
                                    summonedEntries,
                                    source.entityId,
                                    effectIndex,
                                    effectRuntime.Summon))
                            {
                                nextEffectState.phase = EnemyUtilityEffectPhase.Windup;
                                nextEffectState.windupStartTick = input.TickIndex;
                                nextEffectState.windupEndTick = input.TickIndex + effectRuntime.Summon.WindupTicks;
                                nextEffectState.recoverStartTick = 0;
                                nextEffectState.recoverEndTickExclusive = 0;
                                nextEffectState.activationSequence = Math.Max(0, nextEffectState.activationSequence) + 1;
                                if (effectRuntime.Summon.SuppressMovementDuringWindup)
                                {
                                    nextEffectState.movementSuppressionUntilTickInclusive = nextEffectState.windupEndTick;
                                }

                                updates.Add(
                                    $"EnemyUtilityWindupStarted|E={_entityId}|Effect={effectIndex}|Sequence={nextEffectState.activationSequence}|Start={nextEffectState.windupStartTick}|End={nextEffectState.windupEndTick}");
                            }
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
                    EmitEnemyUtilityTriggerIntent(writeContext, effectIndex, effectRuntime, input.TickIndex);

                    EnterUtilityRecoverOrClear(effectRuntime, input.TickIndex, ref nextEffectState);
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

        private void SuspendEnemyUtilityForTopologyParticipationLoss(
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
                var nextEffectState = currentState.EffectStates[effectIndex];
                if (effectIndex < _utilityCapability.Effects.Count)
                {
                    nextEffectState.effectKind = _utilityCapability.Effects[effectIndex].Kind;
                }

                if (ShiftEnemyUtilitySuspendedWindow(ref nextEffectState))
                {
                    hasAnyChange = true;
                    updates.Add(
                        $"EnemyUtilityTopologySuspended|E={_entityId}|Effect={effectIndex}|Phase={nextEffectState.phase}|Sequence={nextEffectState.activationSequence}|WindupEnd={nextEffectState.windupEndTick}|ActiveEnd={nextEffectState.activeEndTickExclusive}|RecoverEnd={nextEffectState.recoverEndTickExclusive}|Cooldown={nextEffectState.cooldownTicksRemaining}");
                }

                nextEffectStates[effectIndex] = nextEffectState;
            }

            if (hasAnyChange)
            {
                writeContext.SetEnemyUtilityState(_entityId, new EnemyUtilityRuntimeState(nextEffectStates));
            }
        }

        private static bool ShiftEnemyUtilitySuspendedWindow(ref EnemyUtilityEffectState state)
        {
            var shifted = false;
            switch (state.phase)
            {
                case EnemyUtilityEffectPhase.Windup:
                    if (state.windupEndTick > 0)
                    {
                        state.windupEndTick++;
                        shifted = true;
                    }

                    break;

                case EnemyUtilityEffectPhase.Active:
                    if (state.activeEndTickExclusive > 0)
                    {
                        state.activeEndTickExclusive++;
                        shifted = true;
                    }

                    break;

                case EnemyUtilityEffectPhase.Recover:
                    if (state.recoverEndTickExclusive > 0)
                    {
                        state.recoverEndTickExclusive++;
                        shifted = true;
                    }

                    break;
            }

            if (state.movementSuppressionUntilTickInclusive > 0)
            {
                state.movementSuppressionUntilTickInclusive++;
                shifted = true;
            }

            return shifted;
        }

        private void EmitEnemyUtilityTriggerIntent(
            IPreMovementStateCommitContext writeContext,
            int effectIndex,
            EnemyUtilityEffectRuntime effectRuntime,
            int tickIndex,
            SurfaceCell originCell = default)
        {
            if (writeContext is not IEnemyUtilityTriggerSink triggerSink)
            {
                return;
            }

            triggerSink.EmitEnemyUtilityTriggerIntent(
                new EnemyUtilityTriggerIntent(
                    _entityId,
                    effectIndex,
                    effectRuntime.Kind,
                    tickIndex,
                    effectRuntime,
                    originCell));
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
                if (nextEffectState.phase == EnemyUtilityEffectPhase.Windup ||
                    nextEffectState.phase == EnemyUtilityEffectPhase.Active ||
                    nextEffectState.phase == EnemyUtilityEffectPhase.Recover ||
                    nextEffectState.movementSuppressionUntilTickInclusive != 0)
                {
                    var cooldownTicks = effectIndex < _utilityCapability.Effects.Count
                        ? _utilityCapability.Effects[effectIndex].CooldownTicks
                        : nextEffectState.cooldownTicksRemaining;

                    nextEffectState.phase = EnemyUtilityEffectPhase.None;
                    nextEffectState.cooldownTicksRemaining = cooldownTicks;
                    nextEffectState.windupStartTick = 0;
                    nextEffectState.windupEndTick = 0;
                    nextEffectState.activeStartTick = 0;
                    nextEffectState.activeEndTickExclusive = 0;
                    nextEffectState.activeOriginCell = default;
                    nextEffectState.recoverStartTick = 0;
                    nextEffectState.recoverEndTickExclusive = 0;
                    nextEffectState.movementSuppressionUntilTickInclusive = 0;
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

        private static bool AreEqual(EnemyUtilityEffectState left, EnemyUtilityEffectState right)
        {
            return left.effectKind == right.effectKind &&
                   left.cooldownTicksRemaining == right.cooldownTicksRemaining &&
                   left.phase == right.phase &&
                   left.windupStartTick == right.windupStartTick &&
                   left.windupEndTick == right.windupEndTick &&
                   left.activeStartTick == right.activeStartTick &&
                   left.activeEndTickExclusive == right.activeEndTickExclusive &&
                   left.activeOriginCell.Equals(right.activeOriginCell) &&
                   left.recoverStartTick == right.recoverStartTick &&
                   left.recoverEndTickExclusive == right.recoverEndTickExclusive &&
                   left.activationSequence == right.activationSequence &&
                   left.movementSuppressionUntilTickInclusive == right.movementSuppressionUntilTickInclusive;
        }

        private static void EnterUtilityRecoverOrClear(
            EnemyUtilityEffectRuntime effectRuntime,
            int tickIndex,
            ref EnemyUtilityEffectState state,
            bool startCooldownAfterRecover = false)
        {
            state.windupStartTick = 0;
            state.windupEndTick = 0;
            state.activeStartTick = 0;
            state.activeEndTickExclusive = 0;
            state.activeOriginCell = default;

            var recoveryTicks = GetUtilityRecoveryTicks(effectRuntime);
            if (recoveryTicks <= 0)
            {
                state.phase = EnemyUtilityEffectPhase.None;
                state.recoverStartTick = 0;
                state.recoverEndTickExclusive = 0;
                state.cooldownTicksRemaining = effectRuntime.CooldownTicks;
                return;
            }

            state.phase = EnemyUtilityEffectPhase.Recover;
            state.recoverStartTick = tickIndex;
            state.recoverEndTickExclusive = tickIndex + recoveryTicks;
            state.cooldownTicksRemaining = startCooldownAfterRecover
                ? 0
                : effectRuntime.CooldownTicks;
            if (SuppressesMovementDuringRecover(effectRuntime))
            {
                state.movementSuppressionUntilTickInclusive = Mathf.Max(
                    state.movementSuppressionUntilTickInclusive,
                    state.recoverEndTickExclusive - 1);
            }
        }

        private static void AdvanceUtilityRecover(
            EnemyUtilityEffectRuntime effectRuntime,
            int tickIndex,
            ref EnemyUtilityEffectState state)
        {
            if (tickIndex >= state.recoverEndTickExclusive)
            {
                state.phase = EnemyUtilityEffectPhase.None;
                state.recoverStartTick = 0;
                state.recoverEndTickExclusive = 0;
                if (effectRuntime.Kind == EnemyUtilityEffectKind.GravityFieldAura &&
                    state.cooldownTicksRemaining == 0)
                {
                    state.cooldownTicksRemaining = effectRuntime.CooldownTicks;
                }

                return;
            }

            if (state.cooldownTicksRemaining > 0)
            {
                state.cooldownTicksRemaining = Mathf.Max(0, state.cooldownTicksRemaining - 1);
            }
        }

        private static int GetUtilityRecoveryTicks(EnemyUtilityEffectRuntime effectRuntime)
        {
            return effectRuntime.Kind switch
            {
                EnemyUtilityEffectKind.SummonMinion => effectRuntime.Summon.RecoveryTicks,
                EnemyUtilityEffectKind.GravityFieldAura => effectRuntime.GravityFieldAura.RecoveryTicks,
                _ => 0,
            };
        }

        private static bool IsUtilityMovementSuppressionWindowActive(
            EnemyUtilityEffectRuntime effectRuntime,
            in EnemyUtilityEffectState state,
            int tickIndex)
        {
            if (state.movementSuppressionUntilTickInclusive <= 0 ||
                tickIndex > state.movementSuppressionUntilTickInclusive)
            {
                return false;
            }

            return state.phase switch
            {
                EnemyUtilityEffectPhase.Windup => SuppressesMovementDuringWindup(effectRuntime),
                EnemyUtilityEffectPhase.Active => false,
                EnemyUtilityEffectPhase.Recover => SuppressesMovementDuringRecover(effectRuntime) ||
                                                   IsWindupSuppressionWindowRemainder(effectRuntime, state, tickIndex),
                EnemyUtilityEffectPhase.None => IsWindupSuppressionWindowRemainder(effectRuntime, state, tickIndex),
                _ => false,
            };
        }

        private static bool IsWindupSuppressionWindowRemainder(
            EnemyUtilityEffectRuntime effectRuntime,
            in EnemyUtilityEffectState state,
            int tickIndex)
        {
            return SuppressesMovementDuringWindup(effectRuntime) &&
                   tickIndex == state.movementSuppressionUntilTickInclusive;
        }

        private static bool SuppressesMovementDuringWindup(EnemyUtilityEffectRuntime effectRuntime)
        {
            return effectRuntime.Kind switch
            {
                EnemyUtilityEffectKind.SummonMinion => effectRuntime.Summon.SuppressMovementDuringWindup,
                EnemyUtilityEffectKind.GravityFieldAura => effectRuntime.GravityFieldAura.SuppressMovementDuringWindup,
                _ => false,
            };
        }

        private static bool SuppressesMovementDuringRecover(EnemyUtilityEffectRuntime effectRuntime)
        {
            return effectRuntime.Kind switch
            {
                EnemyUtilityEffectKind.SummonMinion => effectRuntime.Summon.SuppressMovementDuringRecover,
                EnemyUtilityEffectKind.GravityFieldAura => effectRuntime.GravityFieldAura.SuppressMovementDuringRecover,
                _ => false,
            };
        }

        private static bool UsesDelayedUtilityWindup(EnemyUtilityEffectRuntime effectRuntime)
        {
            return effectRuntime.Kind switch
            {
                EnemyUtilityEffectKind.SummonMinion => true,
                EnemyUtilityEffectKind.GravityFieldAura => true,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(effectRuntime.Kind),
                    effectRuntime.Kind,
                    "Unhandled enemy utility effect kind."),
            };
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

        private bool ShouldSuppressOrdinaryMovementForLocalEngagement(
            WorldSnapshot snapshot,
            in EntityState source)
        {
            if (source.aiMode != EnemyAiMode.Patrol &&
                source.aiMode != EnemyAiMode.Chase)
            {
                return false;
            }

            return EnemyTargetSelector.TryFindLocalEngagementTarget(
                snapshot,
                source,
                _combatCapability,
                _passiveContactCapability,
                _sharedCellUnits,
                out _,
                out _);
        }

        private static bool HasRawMovementIntentForSource(
            IReadOnlyList<RawMovementIntent> rawIntents,
            int sourceEntityId)
        {
            if (rawIntents == null)
            {
                return false;
            }

            for (var i = 0; i < rawIntents.Count; i++)
            {
                if (rawIntents[i].SourceId == sourceEntityId)
                {
                    return true;
                }
            }

            return false;
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
                if ((!nextState.initialDelayInitialized || nextState.initialDelayTicksRemaining > 0) &&
                    _movementSkillCapability.JumpTimingSettings.InitialDelayTicks > 0)
                {
                    nextState = EnemyJumpQueries.TickInitialDelay(
                        nextState,
                        _movementSkillCapability.JumpTimingSettings.InitialDelayTicks);
                    AppendJumpUpdate(
                        updates,
                        _entityId,
                        nextState.initialDelayTicksRemaining > 0 ? "InitialDelayTick" : "InitialDelayReady",
                        nextState);
                }

                if (TryResolveScheduledJumpStart(
                        snapshot,
                        source,
                        nextState,
                        input.TickIndex,
                        out var startedState))
                {
                    nextState = startedState;
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
                nextState = EnemyJumpQueries.ResumeTopologyParticipation(nextState);

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
                nextState = EnemyJumpQueries.ResumeTopologyParticipation(nextState);
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

        private void CommitJumpTopologySuspend(
            WorldSnapshot snapshot,
            int tickIndex,
            IPreMovementStateCommitContext stateWriteContext,
            List<string> updates)
        {
            if (!snapshot.TryGetEnemyJumpState(_entityId, out var previousState) ||
                (previousState.phase != EnemyJumpPhase.Windup &&
                 previousState.phase != EnemyJumpPhase.Airborne))
            {
                return;
            }

            var nextState = EnemyJumpQueries.SuspendTopologyParticipation(previousState, tickIndex);
            if (!AreEqual(previousState, nextState))
            {
                stateWriteContext.SetEnemyJumpState(_entityId, nextState);
                AppendJumpUpdate(updates, _entityId, "TopologySuspend", nextState);
            }
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
                            EnemyChargeStrategyShared.CanAdvanceChargeStep(
                                snapshot,
                                source,
                                previousState.lockedDirection,
                                _tileFeatureDefinitions))
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
                !EnemyChargeStrategyShared.TryResolveChargeStart(
                    snapshot,
                    source,
                    target,
                    _tileFeatureDefinitions,
                    out var lockedDirection,
                    out var reachableSteps))
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

        private bool ShouldLogUnchangedDecision(in EntityState source, in EnemyAiTransitionDecision decision)
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
                    if (ShouldSuppressOrdinaryMovementForLocalEngagement(snapshot, source))
                    {
                        return default;
                    }

                    if (ShouldHoldWallFollowForSameCellPassiveContact(snapshot, source))
                    {
                        return default;
                    }

                    if (TryBuildPatrolMovementIntentOwned(snapshot, source, tickIndex, out var patrolIntent))
                    {
                        return CreateGroundLocomotionResolution(snapshot, source, patrolIntent);
                    }

                    return default;

                case EnemyAiMode.Chase:
                    if (ShouldSuppressOrdinaryMovementForLocalEngagement(snapshot, source))
                    {
                        return default;
                    }

                    var excludedChaseDirection = TryGetChaseBlockedDirectionToAvoid(tickIndex, out var blockedDirectionToAvoid)
                        ? blockedDirectionToAvoid
                        : (Direction?)null;
                    if (!_detectionStrategy.TryFindTarget(
                            snapshot,
                            source,
                            _detectionSettings,
                            out var chaseTarget,
                            EnemyDetectionQueryOptionResolver.Resolve(snapshot, source, _movementSkillCapability)))
                    {
                        if (IsActiveGlide(snapshot, source) &&
                            TryBuildPatrolMovementIntentOwned(snapshot, source, tickIndex, out var glideFallbackIntent))
                        {
                            return CreateGroundLocomotionResolution(snapshot, source, glideFallbackIntent);
                        }

                        return default;
                    }

                    if (ShouldHoldWindupProjectileMovementForAttackerTransition(snapshot, source, chaseTarget))
                    {
                        return default;
                    }

                    if (_chaseStrategy.TryBuildMovementIntent(
                            snapshot,
                            source,
                            chaseTarget,
                            _commonSettings,
                            _chaseSettings,
                            _tileFeatureDefinitions,
                            out var chaseIntent,
                            excludedChaseDirection))
                    {
                        return CreateGroundLocomotionResolution(snapshot, source, chaseIntent);
                    }

                    if (TryBuildWindupProjectileSimulationApproachIntent(
                            snapshot,
                            source,
                            chaseTarget,
                            excludedChaseDirection,
                            out var windupApproachIntent))
                    {
                        return CreateGroundLocomotionResolution(snapshot, source, windupApproachIntent);
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
                            _tileFeatureDefinitions,
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

        private bool TryBuildPatrolMovementIntentOwned(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            out RawMovementIntent intent)
        {
            intent = default;
            if (TryBuildPatrolDecisionProposal(snapshot, source, tickIndex, out var patrolProposal))
            {
                var patrolDelta = EnemyMovementStrategyShared.ResolveDelta(patrolProposal.PlannedDirection);
                if (!patrolProposal.HasDirection ||
                    !patrolDelta.HasValue)
                {
                    return false;
                }

                return EnemyMovementStrategyShared.TryBuildMoveIntent(
                    snapshot,
                    source,
                    _commonSettings,
                    patrolDelta.Value,
                    _tileFeatureDefinitions,
                    out intent);
            }

            if (_patrolStrategy is RandomWalkPatrolStrategy)
            {
                return false;
            }

            return _patrolStrategy.TryBuildMovementIntent(
                snapshot,
                source,
                _commonSettings,
                _patrolSettings,
                _tileFeatureDefinitions,
                out intent);
        }

        private bool TryBuildWindupProjectileSimulationApproachIntent(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            Direction? excludedDirection,
            out RawMovementIntent intent)
        {
            intent = default;
            if (_combatCapability == null)
            {
                return false;
            }

            var startQuery = QueryCombatWindupStart(snapshot, source, target, _combatCapability, _tileFeatureDefinitions);
            if (!startQuery.ShouldApproach)
            {
                return false;
            }

            var approachSettings = new ChaseSettings(
                _chaseSettings.AxisPriority,
                _chaseSettings.TrySecondaryAxisWhenBlocked,
                desiredChaseDistance: 0);
            return _chaseStrategy.TryBuildMovementIntent(
                snapshot,
                source,
                target,
                _commonSettings,
                approachSettings,
                _tileFeatureDefinitions,
                out intent,
                excludedDirection);
        }

        private GroundLocomotionResolution CreateGroundLocomotionResolution(
            WorldSnapshot snapshot,
            in EntityState source,
            RawMovementIntent intent)
        {
            if (IsActiveGlide(snapshot, source) &&
                snapshot.TryGetEnemyGlideState(source.entityId, out var glideState))
            {
                return new GroundLocomotionResolution(
                    hasIntent: true,
                    intent,
                    cooldownTicks: 0,
                    ordinaryKinematicMoveTicks: Math.Max(1, glideState.GlideMoveTicks));
            }

            return new GroundLocomotionResolution(
                hasIntent: true,
                intent,
                _locomotionTimingSettings.MoveCooldownTicks,
                _locomotionTimingSettings.OrdinaryKinematicMoveTicks);
        }

        private static bool IsActiveGlide(WorldSnapshot snapshot, in EntityState source)
        {
            return snapshot.TryGetEnemyGlideState(source.entityId, out var glideState) &&
                   glideState.Phase == EnemyGlidePhase.Active;
        }

        private bool ShouldHoldWindupProjectileMovementForAttackerTransition(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target)
        {
            if (_combatCapability == null)
            {
                return false;
            }

            var startQuery = QueryCombatWindupStart(snapshot, source, target, _combatCapability, _tileFeatureDefinitions);
            return startQuery.BlockReason == CombatWindupStartBlockReason.SevereTransition &&
                   CombatWindupPoseQueries.IsInSevereCombatOriginTransition(snapshot, source);
        }

        private static CombatWindupStartQueryResult QueryCombatWindupStart(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            EnemyCombatCapabilityRuntime combatCapability,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            if (combatCapability.Kind == AttackDecisionStrategyKind.WindupForwardCellProjectile)
            {
                return CombatWindupPoseQueries.QueryStartWindupForwardCellProjectile(
                    snapshot,
                    source,
                    target,
                    combatCapability.AttackDecisionStrategy,
                    combatCapability.AttackDecisionSettings,
                    combatCapability.WindupForwardCellProjectileSettings,
                    tileFeatureDefinitions,
                    out _);
            }

            return CombatWindupStartQueryResult.Block(CombatWindupStartBlockReason.TargetInvalid);
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
                previousState.initialDelayTicksRemaining > 0 ||
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
                   left.retryCount == right.retryCount &&
                   left.topologySuspendLastTick == right.topologySuspendLastTick &&
                   left.initialDelayInitialized == right.initialDelayInitialized &&
                   left.initialDelayTicksRemaining == right.initialDelayTicksRemaining;
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
                .Append("|Retry=").Append(state.retryCount)
                .Append("|TopologySuspendLast=").Append(state.topologySuspendLastTick)
                .Append("|InitialDelayInitialized=").Append(state.initialDelayInitialized ? 1 : 0)
                .Append("|InitialDelayRemaining=").Append(state.initialDelayTicksRemaining);

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

            if (ShouldSuppressAutonomousMovementAndFacing(snapshot, source, tickIndex))
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
                    _tileFeatureDefinitions,
                    out _))
            {
                return null;
            }

            if (stage == EnemyAiTransitionStage.BeforeMovement &&
                _patrolStrategy is WallFollowPatrolStrategy)
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
                _patrolStrategy is not WallFollowPatrolStrategy)
            {
                return null;
            }

            var wallFollowOutcome = EnemyMovementStrategyShared.ChooseWallFollowDirection(
                snapshot,
                source,
                _patrolSettings,
                _tileFeatureDefinitions,
                out _);
            if (wallFollowOutcome == EnemyMovementStrategyShared.WallFollowHandRuleOutcome.BuiltDirection ||
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
                _tileFeatureDefinitions,
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
                writeContext is not IPreMovementStateCommitContext patrolWriteContext)
            {
                return;
            }

            var hadPreviousState = snapshot.TryGetEnemyPatrolState(_entityId, out var previousState);
            if (previousState.IsInitialized ||
                !TryBuildPatrolDecisionProposal(snapshot, source, tickIndex, out var proposal) ||
                !proposal.ShouldCaptureOriginBeforeLeavingPatrol ||
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
            if (source.aiMode != EnemyAiMode.Patrol)
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

    internal static class EnemyDetectionQueryOptionResolver
    {
        public static EnemyDetectionQueryOptions Resolve(
            WorldSnapshot snapshot,
            in EntityState source,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (movementSkillCapability == null ||
                movementSkillCapability.Kind != MovementSkillStrategyKind.GlideOverSolid ||
                !snapshot.TryGetEnemyGlideState(source.entityId, out var glideState) ||
                glideState.Phase != EnemyGlidePhase.Active)
            {
                return EnemyDetectionQueryOptions.Default;
            }

            return new EnemyDetectionQueryOptions(LineOfSightSolidBlockerPolicy.IgnoreSolid);
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
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability,
            in EnemyAiCommonSettings commonSettings,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
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
                    return ResolveBeforeMovement(snapshot, source, tickIndex, detectionStrategy, combatCapability, passiveContactCapability, movementSkillCapability, commonSettings, detectionSettings, tileFeatureDefinitions);

                case EnemyAiTransitionStage.BeforeAttack:
                    return ResolveBeforeAttack(snapshot, source, tickIndex, detectionStrategy, combatCapability, passiveContactCapability, movementSkillCapability, commonSettings, detectionSettings, tileFeatureDefinitions);

                case EnemyAiTransitionStage.AfterAttack:
                    return ResolveAfterAttack(snapshot, source, tickIndex, combatCapability, commonSettings);

                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown enemy AI transition stage.");
            }
        }

        private static EnemyAiTransitionDecision ResolveBeforeMovement(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
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
                        tickIndex,
                        detectionStrategy,
                        combatCapability,
                        passiveContactCapability,
                        movementSkillCapability,
                        detectionSettings,
                        tileFeatureDefinitions,
                        EnemyAiMode.Patrol);

                case EnemyAiMode.Recover:
                    if (source.aiStateTimer > 0)
                    {
                        return new EnemyAiTransitionDecision(
                            EnemyAiMode.Recover,
                            source.aiStateTimer - 1,
                            "RecoverTick");
                    }

                    if (detectionStrategy.TryFindTarget(
                            snapshot,
                            source,
                            detectionSettings,
                            out _,
                            EnemyDetectionQueryOptionResolver.Resolve(snapshot, source, movementSkillCapability)))
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
            int tickIndex,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability,
            in EnemyAiCommonSettings commonSettings,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            switch (source.aiMode)
            {
                case EnemyAiMode.Chase:
                case EnemyAiMode.Attack:
                    return TryResolveCombatReadiness(
                        snapshot,
                        source,
                        tickIndex,
                        detectionStrategy,
                        combatCapability,
                        passiveContactCapability,
                        movementSkillCapability,
                        detectionSettings,
                        tileFeatureDefinitions,
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
            int tickIndex,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            EnemyAiMode patrolFallback)
        {
            if (source.aiMode == EnemyAiMode.Attack &&
                snapshot.TryGetEnemyActionState(source.entityId, out var actionState) &&
                actionState.IsActive)
            {
                if (actionState.kind == EnemyActionKind.ForwardCellProjectile &&
                    actionState.hasLockedForwardCellImpact)
                {
                    return new EnemyAiTransitionDecision(
                        EnemyAiMode.Attack,
                        0,
                        "LockedForwardCellImpact",
                        EnemyActionQueries.ResolveAuthoritativeFacing(actionState));
                }

                if (combatCapability != null &&
                    EnemyActionStateTargeting.TryResolveLockedTarget(
                        snapshot,
                        source,
                        actionState,
                        combatCapability,
                        detectionSettings,
                        out var retainedLockedTarget))
                {
                    var retainReason = EnemyTargetEligibilityPolicy
                        .EvaluateFreshAcquire(snapshot, source, retainedLockedTarget, detectionSettings)
                        .RejectReason == EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState
                        ? "LockedTargetRetainedDespiteFreshSuppression"
                        : "LockedTargetInRange";
                    return new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, retainReason, actionState.direction);
                }

                if (TryResolveLocalEngagementHold(
                        snapshot,
                        source,
                        combatCapability,
                        passiveContactCapability,
                        out _,
                        out _))
                {
                    return new EnemyAiTransitionDecision(
                        EnemyAiMode.Chase,
                        0,
                        "PatrolFallbackDeniedByLocalEngagement");
                }

                return new EnemyAiTransitionDecision(
                    EnemyActionStateTargeting.ResolveFallbackAiMode(
                        snapshot,
                        source,
                        detectionStrategy,
                        detectionSettings,
                        combatCapability,
                        passiveContactCapability,
                        new List<EntityState>()),
                    0,
                    "LockedTargetLost");
            }

            if (!EnemyTargetSelector.TryAcquireFreshTarget(
                    snapshot,
                    source,
                    detectionStrategy,
                    detectionSettings,
                    out var target,
                    out var freshAcquireResult,
                    EnemyDetectionQueryOptionResolver.Resolve(snapshot, source, movementSkillCapability)))
            {
                if (TryResolveLocalEngagementHold(
                        snapshot,
                        source,
                        combatCapability,
                        passiveContactCapability,
                        out _,
                        out var localHoldResult))
                {
                    return new EnemyAiTransitionDecision(
                        source.aiMode == EnemyAiMode.Patrol ? EnemyAiMode.Chase : source.aiMode,
                        0,
                        BuildNoTargetHoldReason(freshAcquireResult, localHoldResult));
                }

                return new EnemyAiTransitionDecision(patrolFallback, 0, BuildNoTargetReason(freshAcquireResult));
            }

            if (combatCapability != null &&
                combatCapability.AttackDecisionStrategy.IsTargetInRange(source, target, combatCapability.AttackDecisionSettings))
            {
                var startQuery = CombatWindupPoseQueries.QueryShortRangeWindupStart(
                    snapshot,
                    source,
                    target,
                    combatCapability,
                    tileFeatureDefinitions,
                    out _);
                if (startQuery.CanStart &&
                    source.position.Equals(target.position) &&
                    CombatWindupPoseQueries.IsMoveLockStartedThisTick(snapshot, source.entityId, tickIndex))
                {
                    return new EnemyAiTransitionDecision(
                        EnemyAiMode.Chase,
                        0,
                        "TargetInRangeButWindupApproachInProgress");
                }

                return startQuery.CanStart
                    ? new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, "TargetInRange")
                    : new EnemyAiTransitionDecision(
                        EnemyAiMode.Chase,
                        0,
                        startQuery.ShouldApproach
                            ? "TargetInRangeButSimulationStartRangeOutside"
                            : "TargetInRangeButCombatPoseNotReady");
            }

            return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "TargetSensed");
        }

        private static bool TryResolveLocalEngagementHold(
            WorldSnapshot snapshot,
            in EntityState source,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            out EntityState target,
            out EnemyTargetEligibilityResult result)
        {
            return EnemyTargetSelector.TryFindLocalEngagementTarget(
                snapshot,
                source,
                combatCapability,
                passiveContactCapability,
                new List<EntityState>(),
                out target,
                out result);
        }

        private static string BuildNoTargetReason(in EnemyTargetEligibilityResult freshAcquireResult)
        {
            return freshAcquireResult.RejectReason == EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState
                ? "FreshTargetSuppressedBySpatialState"
                : "NoTarget";
        }

        private static string BuildNoTargetHoldReason(
            in EnemyTargetEligibilityResult freshAcquireResult,
            in EnemyTargetEligibilityResult localHoldResult)
        {
            if (localHoldResult.AcceptReason == EnemyTargetEligibilityAcceptReason.SameCellLocalEngagement)
            {
                return freshAcquireResult.RejectReason == EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState
                    ? "LocalEngagementHeldSameCell"
                    : "PatrolFallbackDeniedByLocalEngagement";
            }

            return "PatrolFallbackDeniedByLocalEngagement";
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
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability,
            in EnemyAiCommonSettings commonSettings,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
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
                    passiveContactCapability,
                    movementSkillCapability,
                    chargeTimingSettings,
                    detectionSettings,
                    tileFeatureDefinitions),
                EnemyAiTransitionStage.BeforeAttack => ResolveBeforeAttack(
                    snapshot,
                    source,
                    tickIndex,
                    detectionStrategy,
                    combatCapability,
                    passiveContactCapability,
                    movementSkillCapability,
                    detectionSettings,
                    tileFeatureDefinitions),
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
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            switch (source.aiMode)
            {
                case EnemyAiMode.None:
                case EnemyAiMode.Dead:
                    return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, "Disabled");

                case EnemyAiMode.Patrol:
                    if (EnemyTargetSelector.TryAcquireFreshTarget(
                            snapshot,
                            source,
                            detectionStrategy,
                            detectionSettings,
                            out _,
                            out var patrolFreshResult,
                            EnemyDetectionQueryOptionResolver.Resolve(snapshot, source, movementSkillCapability)))
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "TargetSensed");
                    }

                    if (TryResolveLocalEngagementHold(
                            snapshot,
                            source,
                            combatCapability,
                            passiveContactCapability,
                            out _,
                            out var patrolLocalHoldResult))
                    {
                        return new EnemyAiTransitionDecision(
                            EnemyAiMode.Chase,
                            0,
                            BuildNoTargetHoldReason(patrolFreshResult, patrolLocalHoldResult));
                    }

                    return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, BuildNoTargetReason(patrolFreshResult));

                case EnemyAiMode.Chase:
                    return ResolveChase(snapshot, source, tickIndex, detectionStrategy, combatCapability, passiveContactCapability, movementSkillCapability, detectionSettings, tileFeatureDefinitions);

                case EnemyAiMode.Charge:
                    return ResolveChargeBeforeMovement(
                        snapshot,
                        source,
                        tickIndex,
                        detectionStrategy,
                        combatCapability,
                        passiveContactCapability,
                        chargeTimingSettings,
                        detectionSettings,
                        tileFeatureDefinitions);

                case EnemyAiMode.Attack:
                    return ResolveAttackOrFallback(snapshot, source, tickIndex, detectionStrategy, combatCapability, passiveContactCapability, movementSkillCapability, detectionSettings, tileFeatureDefinitions);

                case EnemyAiMode.Recover:
                    if (IsChargeOwnedRecover(snapshot, source.entityId, out var chargeRecoverState))
                    {
                        if (chargeRecoverState.recoverRemainingTicks > 0)
                        {
                            return new EnemyAiTransitionDecision(EnemyAiMode.Recover, 0, "ChargeRecoverTick");
                        }

                        return ResolvePostCharge(snapshot, source, detectionStrategy, combatCapability, passiveContactCapability, detectionSettings, "ChargeRecoverComplete");
                    }

                    var genericRecoverCountdown = GetGenericRecoverCountdown(snapshot, source);
                    if (genericRecoverCountdown > 0)
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Recover, genericRecoverCountdown - 1, "RecoverTick");
                    }

                    if (EnemyTargetSelector.TryAcquireFreshTarget(
                            snapshot,
                            source,
                            detectionStrategy,
                            detectionSettings,
                            out _,
                            out var recoverFreshResult,
                            EnemyDetectionQueryOptionResolver.Resolve(snapshot, source, movementSkillCapability)))
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "RecoverComplete");
                    }

                    if (TryResolveLocalEngagementHold(
                            snapshot,
                            source,
                            combatCapability,
                            passiveContactCapability,
                            out _,
                            out var recoverLocalHoldResult))
                    {
                        return new EnemyAiTransitionDecision(
                            EnemyAiMode.Chase,
                            0,
                            BuildNoTargetHoldReason(recoverFreshResult, recoverLocalHoldResult));
                    }

                    return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, "RecoverCompleteNoTarget");

                default:
                    return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, "UnhandledBeforeMovement");
            }
        }

        private static EnemyAiTransitionDecision ResolveBeforeAttack(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
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
                return ResolveAttackOrFallback(snapshot, source, tickIndex, detectionStrategy, combatCapability, passiveContactCapability, movementSkillCapability, detectionSettings, tileFeatureDefinitions);
            }

            return new EnemyAiTransitionDecision(source.aiMode, source.aiStateTimer, "NoBeforeAttackTransition");
        }

        private static EnemyAiTransitionDecision ResolveChase(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            if (!EnemyTargetSelector.TryAcquireFreshTarget(
                    snapshot,
                    source,
                    detectionStrategy,
                    detectionSettings,
                    out var target,
                    out var freshAcquireResult,
                    EnemyDetectionQueryOptionResolver.Resolve(snapshot, source, movementSkillCapability)))
            {
                if (TryResolveLocalEngagementHold(
                        snapshot,
                        source,
                        combatCapability,
                        passiveContactCapability,
                        out _,
                        out var localHoldResult))
                {
                    return new EnemyAiTransitionDecision(
                        EnemyAiMode.Chase,
                        0,
                        BuildNoTargetHoldReason(freshAcquireResult, localHoldResult));
                }

                return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, BuildNoTargetReason(freshAcquireResult));
            }

            if (combatCapability != null &&
                combatCapability.AttackDecisionStrategy.IsTargetInRange(source, target, combatCapability.AttackDecisionSettings))
            {
                var startQuery = CombatWindupPoseQueries.QueryShortRangeWindupStart(
                    snapshot,
                    source,
                    target,
                    combatCapability,
                    tileFeatureDefinitions,
                    out _);
                if (startQuery.CanStart &&
                    source.position.Equals(target.position) &&
                    CombatWindupPoseQueries.IsMoveLockStartedThisTick(snapshot, source.entityId, tickIndex))
                {
                    return new EnemyAiTransitionDecision(
                        EnemyAiMode.Chase,
                        0,
                        "TargetInRangeButWindupApproachInProgress");
                }

                return startQuery.CanStart
                    ? new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, "TargetInRange")
                    : new EnemyAiTransitionDecision(
                        EnemyAiMode.Chase,
                        0,
                        startQuery.ShouldApproach
                            ? "TargetInRangeButSimulationStartRangeOutside"
                            : "TargetInRangeButCombatPoseNotReady");
            }

            if (EnemyChargeStrategyShared.TryResolveChargeStart(
                    snapshot,
                    source,
                    target,
                    tileFeatureDefinitions,
                    out var chargeFacing,
                    out _))
            {
                return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeStart", chargeFacing);
            }

            return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "TargetSensed");
        }

        private static EnemyAiTransitionDecision ResolveAttackOrFallback(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            EnemyMovementSkillCapabilityRuntime movementSkillCapability,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            if (source.aiMode == EnemyAiMode.Attack &&
                snapshot.TryGetEnemyActionState(source.entityId, out var actionState) &&
                actionState.IsActive)
            {
                if (actionState.kind == EnemyActionKind.ForwardCellProjectile &&
                    actionState.hasLockedForwardCellImpact)
                {
                    return new EnemyAiTransitionDecision(
                        EnemyAiMode.Attack,
                        0,
                        "LockedForwardCellImpact",
                        EnemyActionQueries.ResolveAuthoritativeFacing(actionState));
                }

                if (combatCapability != null &&
                    EnemyActionStateTargeting.TryResolveLockedTarget(
                        snapshot,
                        source,
                        actionState,
                        combatCapability,
                        detectionSettings,
                        out var retainedLockedTarget))
                {
                    var retainReason = EnemyTargetEligibilityPolicy
                        .EvaluateFreshAcquire(snapshot, source, retainedLockedTarget, detectionSettings)
                        .RejectReason == EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState
                        ? "LockedTargetRetainedDespiteFreshSuppression"
                        : "LockedTargetInRange";
                    return new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, retainReason, actionState.direction);
                }

                if (TryResolveLocalEngagementHold(
                        snapshot,
                        source,
                        combatCapability,
                        passiveContactCapability,
                        out _,
                        out _))
                {
                    return new EnemyAiTransitionDecision(
                        EnemyAiMode.Chase,
                        0,
                        "PatrolFallbackDeniedByLocalEngagement");
                }

                return new EnemyAiTransitionDecision(
                    EnemyActionStateTargeting.ResolveFallbackAiMode(
                        snapshot,
                        source,
                        detectionStrategy,
                        detectionSettings,
                        combatCapability,
                        passiveContactCapability,
                        new List<EntityState>()),
                    0,
                    "LockedTargetLost");
            }

            if (!EnemyTargetSelector.TryAcquireFreshTarget(
                    snapshot,
                    source,
                    detectionStrategy,
                    detectionSettings,
                    out var target,
                    out var attackFreshResult,
                    EnemyDetectionQueryOptionResolver.Resolve(snapshot, source, movementSkillCapability)))
            {
                if (TryResolveLocalEngagementHold(
                        snapshot,
                        source,
                        combatCapability,
                        passiveContactCapability,
                        out _,
                        out var attackLocalHoldResult))
                {
                    return new EnemyAiTransitionDecision(
                        EnemyAiMode.Chase,
                        0,
                        BuildNoTargetHoldReason(attackFreshResult, attackLocalHoldResult));
                }

                return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, BuildNoTargetReason(attackFreshResult));
            }

            if (combatCapability != null &&
                combatCapability.AttackDecisionStrategy.IsTargetInRange(source, target, combatCapability.AttackDecisionSettings))
            {
                var startQuery = CombatWindupPoseQueries.QueryShortRangeWindupStart(
                    snapshot,
                    source,
                    target,
                    combatCapability,
                    tileFeatureDefinitions,
                    out _);
                if (startQuery.CanStart &&
                    source.position.Equals(target.position) &&
                    CombatWindupPoseQueries.IsMoveLockStartedThisTick(snapshot, source.entityId, tickIndex))
                {
                    return new EnemyAiTransitionDecision(
                        EnemyAiMode.Chase,
                        0,
                        "TargetInRangeButWindupApproachInProgress");
                }

                return startQuery.CanStart
                    ? new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, "TargetInRange")
                    : new EnemyAiTransitionDecision(
                        EnemyAiMode.Chase,
                        0,
                        startQuery.ShouldApproach
                            ? "TargetInRangeButSimulationStartRangeOutside"
                            : "TargetInRangeButCombatPoseNotReady");
            }

            return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "TargetSensed");
        }

        private static EnemyAiTransitionDecision ResolvePostCharge(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            in DetectionSettings detectionSettings,
            string reason)
        {
            if (!EnemyTargetSelector.TryAcquireFreshTarget(
                    snapshot,
                    source,
                    detectionStrategy,
                    detectionSettings,
                    out var target,
                    out var freshResult))
            {
                if (TryResolveLocalEngagementHold(
                        snapshot,
                        source,
                        combatCapability,
                        passiveContactCapability,
                        out _,
                        out _))
                {
                    return new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, "PatrolFallbackDeniedByLocalEngagement");
                }

                return new EnemyAiTransitionDecision(EnemyAiMode.Patrol, 0, reason);
            }

            return combatCapability != null &&
                   combatCapability.AttackDecisionStrategy.IsTargetInRange(source, target, combatCapability.AttackDecisionSettings)
                ? new EnemyAiTransitionDecision(EnemyAiMode.Attack, 0, reason)
                : new EnemyAiTransitionDecision(EnemyAiMode.Chase, 0, reason);
        }

        private static bool TryResolveLocalEngagementHold(
            WorldSnapshot snapshot,
            in EntityState source,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            out EntityState target,
            out EnemyTargetEligibilityResult result)
        {
            return EnemyTargetSelector.TryFindLocalEngagementTarget(
                snapshot,
                source,
                combatCapability,
                passiveContactCapability,
                new List<EntityState>(),
                out target,
                out result);
        }

        private static string BuildNoTargetReason(in EnemyTargetEligibilityResult freshAcquireResult)
        {
            return freshAcquireResult.RejectReason == EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState
                ? "FreshTargetSuppressedBySpatialState"
                : "NoTarget";
        }

        private static string BuildNoTargetHoldReason(
            in EnemyTargetEligibilityResult freshAcquireResult,
            in EnemyTargetEligibilityResult localHoldResult)
        {
            if (localHoldResult.AcceptReason == EnemyTargetEligibilityAcceptReason.SameCellLocalEngagement)
            {
                return freshAcquireResult.RejectReason == EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState
                    ? "LocalEngagementHeldSameCell"
                    : "PatrolFallbackDeniedByLocalEngagement";
            }

            return "PatrolFallbackDeniedByLocalEngagement";
        }

        private static EnemyAiTransitionDecision ResolveChargeBeforeMovement(
            WorldSnapshot snapshot,
            in EntityState source,
            int tickIndex,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
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

                    if (!EnemyChargeStrategyShared.CanAdvanceChargeStep(
                            snapshot,
                            source,
                            chargeState.lockedDirection,
                            tileFeatureDefinitions))
                    {
                        return ResolveChargeRecoveryOrImmediate(
                            snapshot,
                            source,
                            detectionStrategy,
                            combatCapability,
                            passiveContactCapability,
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
                            passiveContactCapability,
                            chargeTimingSettings,
                            detectionSettings,
                            "ChargeComplete");
                    }

                    if (!CanMoveThisTick(source))
                    {
                        return new EnemyAiTransitionDecision(EnemyAiMode.Charge, 0, "ChargeWaitingForLocomotionCooldown");
                    }

                    if (!EnemyChargeStrategyShared.CanAdvanceChargeStep(
                            snapshot,
                            source,
                            chargeState.lockedDirection,
                            tileFeatureDefinitions))
                    {
                        return ResolveChargeRecoveryOrImmediate(
                            snapshot,
                            source,
                            detectionStrategy,
                            combatCapability,
                            passiveContactCapability,
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

                    return ResolvePostCharge(snapshot, source, detectionStrategy, combatCapability, passiveContactCapability, detectionSettings, "ChargeRecoverComplete");

                default:
                    return new EnemyAiTransitionDecision(source.aiMode, 0, "UnhandledChargePhase");
            }
        }

        private static EnemyAiTransitionDecision ResolveChargeRecoveryOrImmediate(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            in EnemyChargeTimingSettings chargeTimingSettings,
            in DetectionSettings detectionSettings,
            string reason)
        {
            if (chargeTimingSettings.RecoverTicks == 0)
            {
                return ResolvePostCharge(snapshot, source, detectionStrategy, combatCapability, passiveContactCapability, detectionSettings, reason);
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

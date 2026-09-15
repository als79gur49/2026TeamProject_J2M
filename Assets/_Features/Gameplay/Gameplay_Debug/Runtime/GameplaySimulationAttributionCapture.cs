#if VECTORQUAKE_CAPTURE_BUILD
using System;

namespace Game.Feature.Gameplay.Loop
{
    public readonly struct GameplaySimulationAttributionDetail
    {
        public GameplaySimulationAttributionDetail(
            int tickIndex,
            long bootstrapTicks,
            long planTicks,
            long resolveTicks,
            long finalizeAndSnapshotTicks,
            long cleanupAndSnapshotTicks,
            long respawnAndFinalSnapshotTicks,
            long resultMaterializationTicks,
            long planEnemyAiAndProjectionTicks,
            long planKinematicAndGravityProjectionTicks,
            long planPreMovementStateAndUtilityProjectionTicks,
            long planJumpLandingAndPlayerActionAttemptsTicks,
            long planMovementIntentCollectionAndPartitionTicks,
            long planLocomotionProjectionTicks,
            long planMovementExpansionTicks,
            long planPayloadOrderingAndResultTicks,
            long resolveMovementPlanningAndMaterializationTicks,
            long resolveInitialProjectionAndBeforeAttackStateTicks,
            long resolvePreliminaryAttackAndImpactDispositionTicks,
            long resolveMovementRematerializationAndJumpLandingTicks,
            long resolveTileEffectsAndProjectionTicks,
            long resolveFinalAttackAndMaterializationTicks,
            long resolvePostAttackStateAndUtilityTicks,
            long resolveResultMaterializationTicks,
            long resolveInitialProjectionSetupAndBatchApplyTicks,
            long resolveInitialPostMovementSnapshotTicks,
            long resolveBeforeAttackAiTransitionTicks,
            long resolveBeforeAttackEnemyActionTicks,
            long resolveInitialPostMovementSnapshotBaseImportTicks,
            long resolveInitialPostMovementSnapshotOverlayApplyTicks,
            long resolveInitialPostMovementSnapshotMaterializationTicks,
            long planPreMovementSetupTicks,
            long planPreMovementLogicTicks,
            long planPreMovementBookkeepingTicks,
            long planPreMovementProjectionApplyTicks,
            long planPreMovementUtilityInputSnapshotTicks,
            long planPreMovementUtilityResolveTicks,
            long planPreMovementUtilityProjectionApplyTicks,
            long resolveBeforeAttackAiSetupTicks,
            long resolveBeforeAttackAiLogicTicks,
            long resolveBeforeAttackAiProjectionApplyTicks)
        {
            TickIndex = tickIndex;
            BootstrapTicks = bootstrapTicks;
            PlanTicks = planTicks;
            ResolveTicks = resolveTicks;
            FinalizeAndSnapshotTicks = finalizeAndSnapshotTicks;
            CleanupAndSnapshotTicks = cleanupAndSnapshotTicks;
            RespawnAndFinalSnapshotTicks = respawnAndFinalSnapshotTicks;
            ResultMaterializationTicks = resultMaterializationTicks;
            PlanEnemyAiAndProjectionTicks = planEnemyAiAndProjectionTicks;
            PlanKinematicAndGravityProjectionTicks = planKinematicAndGravityProjectionTicks;
            PlanPreMovementStateAndUtilityProjectionTicks = planPreMovementStateAndUtilityProjectionTicks;
            PlanJumpLandingAndPlayerActionAttemptsTicks = planJumpLandingAndPlayerActionAttemptsTicks;
            PlanMovementIntentCollectionAndPartitionTicks = planMovementIntentCollectionAndPartitionTicks;
            PlanLocomotionProjectionTicks = planLocomotionProjectionTicks;
            PlanMovementExpansionTicks = planMovementExpansionTicks;
            PlanPayloadOrderingAndResultTicks = planPayloadOrderingAndResultTicks;
            ResolveMovementPlanningAndMaterializationTicks = resolveMovementPlanningAndMaterializationTicks;
            ResolveInitialProjectionAndBeforeAttackStateTicks = resolveInitialProjectionAndBeforeAttackStateTicks;
            ResolvePreliminaryAttackAndImpactDispositionTicks = resolvePreliminaryAttackAndImpactDispositionTicks;
            ResolveMovementRematerializationAndJumpLandingTicks = resolveMovementRematerializationAndJumpLandingTicks;
            ResolveTileEffectsAndProjectionTicks = resolveTileEffectsAndProjectionTicks;
            ResolveFinalAttackAndMaterializationTicks = resolveFinalAttackAndMaterializationTicks;
            ResolvePostAttackStateAndUtilityTicks = resolvePostAttackStateAndUtilityTicks;
            ResolveResultMaterializationTicks = resolveResultMaterializationTicks;
            ResolveInitialProjectionSetupAndBatchApplyTicks = resolveInitialProjectionSetupAndBatchApplyTicks;
            ResolveInitialPostMovementSnapshotTicks = resolveInitialPostMovementSnapshotTicks;
            ResolveBeforeAttackAiTransitionTicks = resolveBeforeAttackAiTransitionTicks;
            ResolveBeforeAttackEnemyActionTicks = resolveBeforeAttackEnemyActionTicks;
            ResolveInitialPostMovementSnapshotBaseImportTicks = resolveInitialPostMovementSnapshotBaseImportTicks;
            ResolveInitialPostMovementSnapshotOverlayApplyTicks = resolveInitialPostMovementSnapshotOverlayApplyTicks;
            ResolveInitialPostMovementSnapshotMaterializationTicks = resolveInitialPostMovementSnapshotMaterializationTicks;
            PlanPreMovementSetupTicks = planPreMovementSetupTicks;
            PlanPreMovementLogicTicks = planPreMovementLogicTicks;
            PlanPreMovementBookkeepingTicks = planPreMovementBookkeepingTicks;
            PlanPreMovementProjectionApplyTicks = planPreMovementProjectionApplyTicks;
            PlanPreMovementUtilityInputSnapshotTicks = planPreMovementUtilityInputSnapshotTicks;
            PlanPreMovementUtilityResolveTicks = planPreMovementUtilityResolveTicks;
            PlanPreMovementUtilityProjectionApplyTicks = planPreMovementUtilityProjectionApplyTicks;
            ResolveBeforeAttackAiSetupTicks = resolveBeforeAttackAiSetupTicks;
            ResolveBeforeAttackAiLogicTicks = resolveBeforeAttackAiLogicTicks;
            ResolveBeforeAttackAiProjectionApplyTicks = resolveBeforeAttackAiProjectionApplyTicks;
        }

        public int TickIndex { get; }
        public long BootstrapTicks { get; }
        public long PlanTicks { get; }
        public long ResolveTicks { get; }
        public long FinalizeAndSnapshotTicks { get; }
        public long CleanupAndSnapshotTicks { get; }
        public long RespawnAndFinalSnapshotTicks { get; }
        public long ResultMaterializationTicks { get; }
        public long PlanEnemyAiAndProjectionTicks { get; }
        public long PlanKinematicAndGravityProjectionTicks { get; }
        public long PlanPreMovementStateAndUtilityProjectionTicks { get; }
        public long PlanJumpLandingAndPlayerActionAttemptsTicks { get; }
        public long PlanMovementIntentCollectionAndPartitionTicks { get; }
        public long PlanLocomotionProjectionTicks { get; }
        public long PlanMovementExpansionTicks { get; }
        public long PlanPayloadOrderingAndResultTicks { get; }
        public long ResolveMovementPlanningAndMaterializationTicks { get; }
        public long ResolveInitialProjectionAndBeforeAttackStateTicks { get; }
        public long ResolvePreliminaryAttackAndImpactDispositionTicks { get; }
        public long ResolveMovementRematerializationAndJumpLandingTicks { get; }
        public long ResolveTileEffectsAndProjectionTicks { get; }
        public long ResolveFinalAttackAndMaterializationTicks { get; }
        public long ResolvePostAttackStateAndUtilityTicks { get; }
        public long ResolveResultMaterializationTicks { get; }
        public long ResolveInitialProjectionSetupAndBatchApplyTicks { get; }
        public long ResolveInitialPostMovementSnapshotTicks { get; }
        public long ResolveBeforeAttackAiTransitionTicks { get; }
        public long ResolveBeforeAttackEnemyActionTicks { get; }
        public long ResolveInitialPostMovementSnapshotBaseImportTicks { get; }
        public long ResolveInitialPostMovementSnapshotOverlayApplyTicks { get; }
        public long ResolveInitialPostMovementSnapshotMaterializationTicks { get; }
        public long PlanPreMovementSetupTicks { get; }
        public long PlanPreMovementLogicTicks { get; }
        public long PlanPreMovementBookkeepingTicks { get; }
        public long PlanPreMovementProjectionApplyTicks { get; }
        public long PlanPreMovementUtilityInputSnapshotTicks { get; }
        public long PlanPreMovementUtilityResolveTicks { get; }
        public long PlanPreMovementUtilityProjectionApplyTicks { get; }
        public long ResolveBeforeAttackAiSetupTicks { get; }
        public long ResolveBeforeAttackAiLogicTicks { get; }
        public long ResolveBeforeAttackAiProjectionApplyTicks { get; }
    }

    public static class GameplaySimulationAttributionCapture
    {
        private static bool _isActive;
        private static bool _hasPlanDetail;
        private static bool _hasResolveDetail;
        private static bool _hasResolveInitialPostMovementSnapshotDetail;
        private static bool _hasCompletedDetail;
        private static int _planTickIndex;
        private static int _resolveTickIndex;
        private static long[] _planTicks;
        private static long[] _resolveTicks;
        private static GameplaySimulationAttributionDetail _completedDetail;

        public static bool IsActive => _isActive;

        public static void BeginSession()
        {
            if (_isActive)
            {
                throw new InvalidOperationException("Simulation attribution capture is already active.");
            }

            _isActive = true;
            _hasPlanDetail = false;
            _hasResolveDetail = false;
            _hasResolveInitialPostMovementSnapshotDetail = false;
            _hasCompletedDetail = false;
            if (_planTicks == null || _planTicks.Length != 15)
            {
                _planTicks = new long[15];
            }
            if (_resolveTicks == null || _resolveTicks.Length != 18)
            {
                _resolveTicks = new long[18];
            }
            Array.Clear(_planTicks, 0, _planTicks.Length);
            Array.Clear(_resolveTicks, 0, _resolveTicks.Length);
        }

        public static void CancelSession()
        {
            _isActive = false;
            _hasPlanDetail = false;
            _hasResolveDetail = false;
            _hasResolveInitialPostMovementSnapshotDetail = false;
            _hasCompletedDetail = false;
        }

        public static void RecordPlan(
            int tickIndex,
            long enemyAiAndProjectionTicks,
            long kinematicAndGravityProjectionTicks,
            long preMovementStateAndUtilityProjectionTicks,
            long jumpLandingAndPlayerActionAttemptsTicks,
            long movementIntentCollectionAndPartitionTicks,
            long locomotionProjectionTicks,
            long movementExpansionTicks,
            long payloadOrderingAndResultTicks,
            long preMovementSetupTicks,
            long preMovementLogicTicks,
            long preMovementBookkeepingTicks,
            long preMovementProjectionApplyTicks,
            long preMovementUtilityInputSnapshotTicks,
            long preMovementUtilityResolveTicks,
            long preMovementUtilityProjectionApplyTicks)
        {
            if (!_isActive)
            {
                return;
            }

            if (_hasPlanDetail || _hasCompletedDetail)
            {
                throw new InvalidOperationException("Simulation attribution detail was not consumed in Tick order.");
            }

            _planTickIndex = tickIndex;
            _planTicks[0] = enemyAiAndProjectionTicks;
            _planTicks[1] = kinematicAndGravityProjectionTicks;
            _planTicks[2] = preMovementStateAndUtilityProjectionTicks;
            _planTicks[3] = jumpLandingAndPlayerActionAttemptsTicks;
            _planTicks[4] = movementIntentCollectionAndPartitionTicks;
            _planTicks[5] = locomotionProjectionTicks;
            _planTicks[6] = movementExpansionTicks;
            _planTicks[7] = payloadOrderingAndResultTicks;
            _planTicks[8] = preMovementSetupTicks;
            _planTicks[9] = preMovementLogicTicks;
            _planTicks[10] = preMovementBookkeepingTicks;
            _planTicks[11] = preMovementProjectionApplyTicks;
            _planTicks[12] = preMovementUtilityInputSnapshotTicks;
            _planTicks[13] = preMovementUtilityResolveTicks;
            _planTicks[14] = preMovementUtilityProjectionApplyTicks;
            _hasPlanDetail = true;
        }

        public static void RecordResolve(
            int tickIndex,
            long movementPlanningAndMaterializationTicks,
            long initialProjectionAndBeforeAttackStateTicks,
            long preliminaryAttackAndImpactDispositionTicks,
            long movementRematerializationAndJumpLandingTicks,
            long tileEffectsAndProjectionTicks,
            long finalAttackAndMaterializationTicks,
            long postAttackStateAndUtilityTicks,
            long resultMaterializationTicks,
            long initialProjectionSetupAndBatchApplyTicks,
            long initialPostMovementSnapshotTicks,
            long beforeAttackAiTransitionTicks,
            long beforeAttackEnemyActionTicks,
            long beforeAttackAiSetupTicks,
            long beforeAttackAiLogicTicks,
            long beforeAttackAiProjectionApplyTicks)
        {
            if (!_isActive)
            {
                return;
            }

            if (!_hasPlanDetail || _planTickIndex != tickIndex ||
                !_hasResolveInitialPostMovementSnapshotDetail ||
                _hasResolveDetail || _hasCompletedDetail)
            {
                throw new InvalidOperationException("Simulation attribution Resolve detail is out of order.");
            }

            _resolveTickIndex = tickIndex;
            _resolveTicks[0] = movementPlanningAndMaterializationTicks;
            _resolveTicks[1] = initialProjectionAndBeforeAttackStateTicks;
            _resolveTicks[2] = preliminaryAttackAndImpactDispositionTicks;
            _resolveTicks[3] = movementRematerializationAndJumpLandingTicks;
            _resolveTicks[4] = tileEffectsAndProjectionTicks;
            _resolveTicks[5] = finalAttackAndMaterializationTicks;
            _resolveTicks[6] = postAttackStateAndUtilityTicks;
            _resolveTicks[7] = resultMaterializationTicks;
            _resolveTicks[8] = initialProjectionSetupAndBatchApplyTicks;
            _resolveTicks[9] = initialPostMovementSnapshotTicks;
            _resolveTicks[10] = beforeAttackAiTransitionTicks;
            _resolveTicks[11] = beforeAttackEnemyActionTicks;
            _resolveTicks[15] = beforeAttackAiSetupTicks;
            _resolveTicks[16] = beforeAttackAiLogicTicks;
            _resolveTicks[17] = beforeAttackAiProjectionApplyTicks;
            _hasResolveDetail = true;
        }

        public static void RecordResolveInitialPostMovementSnapshot(
            long baseImportTicks,
            long overlayApplyTicks,
            long materializationTicks)
        {
            if (!_isActive)
            {
                return;
            }

            if (_hasResolveInitialPostMovementSnapshotDetail || _hasResolveDetail || _hasCompletedDetail)
            {
                throw new InvalidOperationException(
                    "Resolve initial post-movement snapshot attribution must be recorded exactly once per Tick.");
            }

            _resolveTicks[12] = baseImportTicks;
            _resolveTicks[13] = overlayApplyTicks;
            _resolveTicks[14] = materializationTicks;
            _hasResolveInitialPostMovementSnapshotDetail = true;
        }

        public static void RecordCompleted(
            int tickIndex,
            long bootstrapTicks,
            long planTicks,
            long resolveTicks,
            long finalizeAndSnapshotTicks,
            long cleanupAndSnapshotTicks,
            long respawnAndFinalSnapshotTicks,
            long resultMaterializationTicks)
        {
            if (!_isActive)
            {
                return;
            }

            if (!_hasPlanDetail || _planTickIndex != tickIndex ||
                !_hasResolveDetail || _resolveTickIndex != tickIndex ||
                _hasCompletedDetail)
            {
                throw new InvalidOperationException("Simulation attribution detail is missing or mismatched.");
            }

            _completedDetail = new GameplaySimulationAttributionDetail(
                tickIndex,
                bootstrapTicks,
                planTicks,
                resolveTicks,
                finalizeAndSnapshotTicks,
                cleanupAndSnapshotTicks,
                respawnAndFinalSnapshotTicks,
                resultMaterializationTicks,
                _planTicks[0],
                _planTicks[1],
                _planTicks[2],
                _planTicks[3],
                _planTicks[4],
                _planTicks[5],
                _planTicks[6],
                _planTicks[7],
                _resolveTicks[0],
                _resolveTicks[1],
                _resolveTicks[2],
                _resolveTicks[3],
                _resolveTicks[4],
                _resolveTicks[5],
                _resolveTicks[6],
                _resolveTicks[7],
                _resolveTicks[8],
                _resolveTicks[9],
                _resolveTicks[10],
                _resolveTicks[11],
                _resolveTicks[12],
                _resolveTicks[13],
                _resolveTicks[14],
                _planTicks[8],
                _planTicks[9],
                _planTicks[10],
                _planTicks[11],
                _planTicks[12],
                _planTicks[13],
                _planTicks[14],
                _resolveTicks[15],
                _resolveTicks[16],
                _resolveTicks[17]);
            _hasPlanDetail = false;
            _hasResolveDetail = false;
            _hasResolveInitialPostMovementSnapshotDetail = false;
            _hasCompletedDetail = true;
        }

        public static GameplaySimulationAttributionDetail Consume(int tickIndex)
        {
            if (!_isActive || !_hasCompletedDetail || _completedDetail.TickIndex != tickIndex)
            {
                throw new InvalidOperationException("Simulation attribution detail is missing or out of order.");
            }

            var result = _completedDetail;
            _hasCompletedDetail = false;
            return result;
        }
    }
}
#endif

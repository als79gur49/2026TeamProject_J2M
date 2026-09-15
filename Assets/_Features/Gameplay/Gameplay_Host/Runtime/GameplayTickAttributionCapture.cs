#if VECTORQUAKE_CAPTURE_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayPresentationAttributionDetail
    {
        public GameplayPresentationAttributionDetail(
            int tickIndex,
            long coordinatorTicks,
            long cameraAndStateNotificationTicks,
            long preCommitPlanningTicks,
            long committedFrameAndStateTicks,
            long motionAnimationVfxTicks,
            long audioTicks,
            long applyCleanupUpdateTicks)
        {
            TickIndex = tickIndex;
            CoordinatorTicks = coordinatorTicks;
            CameraAndStateNotificationTicks = cameraAndStateNotificationTicks;
            PreCommitPlanningTicks = preCommitPlanningTicks;
            CommittedFrameAndStateTicks = committedFrameAndStateTicks;
            MotionAnimationVfxTicks = motionAnimationVfxTicks;
            AudioTicks = audioTicks;
            ApplyCleanupUpdateTicks = applyCleanupUpdateTicks;
        }

        public int TickIndex { get; }
        public long CoordinatorTicks { get; }
        public long CameraAndStateNotificationTicks { get; }
        public long PreCommitPlanningTicks { get; }
        public long CommittedFrameAndStateTicks { get; }
        public long MotionAnimationVfxTicks { get; }
        public long AudioTicks { get; }
        public long ApplyCleanupUpdateTicks { get; }
    }

    public readonly struct GameplayTickAttributionSample
    {
        public GameplayTickAttributionSample(
            int tickIndex,
            int threadId,
            long outerTicks,
            long inputPreparationTicks,
            long simulationTicks,
            long hostPostProcessTicks,
            long presentationTicks,
            long callbackTicks,
            GameplaySimulationAttributionDetail simulationDetail,
            GameplayPresentationAttributionDetail presentationDetail)
        {
            TickIndex = tickIndex;
            ThreadId = threadId;
            OuterTicks = outerTicks;
            InputPreparationTicks = inputPreparationTicks;
            SimulationTicks = simulationTicks;
            HostPostProcessTicks = hostPostProcessTicks;
            PresentationTicks = presentationTicks;
            CallbackTicks = callbackTicks;
            SimulationDetail = simulationDetail;
            PresentationDetail = presentationDetail;
        }

        public int TickIndex { get; }
        public int ThreadId { get; }
        public long OuterTicks { get; }
        public long InputPreparationTicks { get; }
        public long SimulationTicks { get; }
        public long HostPostProcessTicks { get; }
        public long PresentationTicks { get; }
        public long CallbackTicks { get; }
        public GameplaySimulationAttributionDetail SimulationDetail { get; }
        public GameplayPresentationAttributionDetail PresentationDetail { get; }
    }

    public static class GameplayTickAttributionCapture
    {
        private static readonly List<GameplayTickAttributionSample> Samples = new();
        private static bool _isActive;
        private static bool _hasPresentationCoordinatorDetail;
        private static bool _hasPresentationDetail;
        private static int _presentationTickIndex;
        private static long[] _presentationCoordinatorTicks;
        private static GameplayPresentationAttributionDetail _presentationDetail;

        public static bool IsActive => _isActive;

        public static void BeginSession(int expectedSampleCount)
        {
            if (_isActive)
            {
                throw new InvalidOperationException("Gameplay Tick attribution capture is already active.");
            }

            GameplaySimulationAttributionCapture.BeginSession();
            Samples.Clear();
            if (Samples.Capacity < expectedSampleCount)
            {
                Samples.Capacity = expectedSampleCount;
            }

            _hasPresentationCoordinatorDetail = false;
            _hasPresentationDetail = false;
            _presentationCoordinatorTicks ??= new long[5];
            Array.Clear(_presentationCoordinatorTicks, 0, _presentationCoordinatorTicks.Length);
            _isActive = true;
        }

        public static IReadOnlyList<GameplayTickAttributionSample> CompleteSession()
        {
            if (!_isActive)
            {
                throw new InvalidOperationException("Gameplay Tick attribution capture is not active.");
            }

            if (_hasPresentationCoordinatorDetail || _hasPresentationDetail)
            {
                throw new InvalidOperationException("Presentation attribution detail was not consumed.");
            }

            _isActive = false;
            GameplaySimulationAttributionCapture.CancelSession();
            return Samples.ToArray();
        }

        public static void CancelSession()
        {
            _isActive = false;
            _hasPresentationCoordinatorDetail = false;
            _hasPresentationDetail = false;
            GameplaySimulationAttributionCapture.CancelSession();
            Samples.Clear();
        }

        public static void RecordPresentationCoordinator(
            int tickIndex,
            long preCommitPlanningTicks,
            long committedFrameAndStateTicks,
            long motionAnimationVfxTicks,
            long audioTicks,
            long applyCleanupUpdateTicks)
        {
            if (!_isActive)
            {
                return;
            }

            if (_hasPresentationCoordinatorDetail || _hasPresentationDetail)
            {
                throw new InvalidOperationException("Presentation coordinator detail was not consumed in Tick order.");
            }

            _presentationTickIndex = tickIndex;
            _presentationCoordinatorTicks[0] = preCommitPlanningTicks;
            _presentationCoordinatorTicks[1] = committedFrameAndStateTicks;
            _presentationCoordinatorTicks[2] = motionAnimationVfxTicks;
            _presentationCoordinatorTicks[3] = audioTicks;
            _presentationCoordinatorTicks[4] = applyCleanupUpdateTicks;
            _hasPresentationCoordinatorDetail = true;
        }

        public static void RecordPresentation(
            int tickIndex,
            long coordinatorTicks,
            long cameraAndStateNotificationTicks)
        {
            if (!_isActive)
            {
                return;
            }

            if (!_hasPresentationCoordinatorDetail || _presentationTickIndex != tickIndex ||
                _hasPresentationDetail)
            {
                throw new InvalidOperationException("Presentation coordinator detail is missing or mismatched.");
            }

            _presentationDetail = new GameplayPresentationAttributionDetail(
                tickIndex,
                coordinatorTicks,
                cameraAndStateNotificationTicks,
                _presentationCoordinatorTicks[0],
                _presentationCoordinatorTicks[1],
                _presentationCoordinatorTicks[2],
                _presentationCoordinatorTicks[3],
                _presentationCoordinatorTicks[4]);
            _hasPresentationCoordinatorDetail = false;
            _hasPresentationDetail = true;
        }

        public static void Record(
            int tickIndex,
            long outerTicks,
            long inputPreparationTicks,
            long simulationTicks,
            long hostPostProcessTicks,
            long presentationTicks,
            long callbackTicks)
        {
            if (!_isActive)
            {
                return;
            }

            if (!_hasPresentationDetail || _presentationDetail.TickIndex != tickIndex)
            {
                throw new InvalidOperationException("Presentation attribution detail is missing or out of order.");
            }

            var simulationDetail = GameplaySimulationAttributionCapture.Consume(tickIndex);
            var presentationDetail = _presentationDetail;
            _hasPresentationDetail = false;

            var simulationChildren =
                simulationDetail.BootstrapTicks +
                simulationDetail.PlanTicks +
                simulationDetail.ResolveTicks +
                simulationDetail.FinalizeAndSnapshotTicks +
                simulationDetail.CleanupAndSnapshotTicks +
                simulationDetail.RespawnAndFinalSnapshotTicks +
                simulationDetail.ResultMaterializationTicks;
            var planChildren =
                simulationDetail.PlanEnemyAiAndProjectionTicks +
                simulationDetail.PlanKinematicAndGravityProjectionTicks +
                simulationDetail.PlanPreMovementStateAndUtilityProjectionTicks +
                simulationDetail.PlanJumpLandingAndPlayerActionAttemptsTicks +
                simulationDetail.PlanMovementIntentCollectionAndPartitionTicks +
                simulationDetail.PlanLocomotionProjectionTicks +
                simulationDetail.PlanMovementExpansionTicks +
                simulationDetail.PlanPayloadOrderingAndResultTicks;
            var resolveChildren =
                simulationDetail.ResolveMovementPlanningAndMaterializationTicks +
                simulationDetail.ResolveInitialProjectionAndBeforeAttackStateTicks +
                simulationDetail.ResolvePreliminaryAttackAndImpactDispositionTicks +
                simulationDetail.ResolveMovementRematerializationAndJumpLandingTicks +
                simulationDetail.ResolveTileEffectsAndProjectionTicks +
                simulationDetail.ResolveFinalAttackAndMaterializationTicks +
                simulationDetail.ResolvePostAttackStateAndUtilityTicks +
                simulationDetail.ResolveResultMaterializationTicks;
            var resolveInitialProjectionChildren =
                simulationDetail.ResolveInitialProjectionSetupAndBatchApplyTicks +
                simulationDetail.ResolveInitialPostMovementSnapshotTicks +
                simulationDetail.ResolveBeforeAttackAiTransitionTicks +
                simulationDetail.ResolveBeforeAttackEnemyActionTicks;
            var resolveInitialPostMovementSnapshotChildren =
                simulationDetail.ResolveInitialPostMovementSnapshotBaseImportTicks +
                simulationDetail.ResolveInitialPostMovementSnapshotOverlayApplyTicks +
                simulationDetail.ResolveInitialPostMovementSnapshotMaterializationTicks;
            var presentationChildren =
                presentationDetail.CoordinatorTicks +
                presentationDetail.CameraAndStateNotificationTicks;
            var coordinatorChildren =
                presentationDetail.PreCommitPlanningTicks +
                presentationDetail.CommittedFrameAndStateTicks +
                presentationDetail.MotionAnimationVfxTicks +
                presentationDetail.AudioTicks +
                presentationDetail.ApplyCleanupUpdateTicks;
            if (simulationChildren > simulationTicks ||
                planChildren > simulationDetail.PlanTicks ||
                resolveChildren > simulationDetail.ResolveTicks ||
                resolveInitialProjectionChildren > simulationDetail.ResolveInitialProjectionAndBeforeAttackStateTicks ||
                resolveInitialPostMovementSnapshotChildren > simulationDetail.ResolveInitialPostMovementSnapshotTicks ||
                presentationChildren > presentationTicks ||
                coordinatorChildren > presentationDetail.CoordinatorTicks)
            {
                throw new InvalidOperationException("Tick attribution child timing exceeds its parent boundary.");
            }

            Samples.Add(
                new GameplayTickAttributionSample(
                    tickIndex,
                    Thread.CurrentThread.ManagedThreadId,
                    outerTicks,
                    inputPreparationTicks,
                    simulationTicks,
                    hostPostProcessTicks,
                    presentationTicks,
                    callbackTicks,
                    simulationDetail,
                    presentationDetail));
        }
    }
}
#endif

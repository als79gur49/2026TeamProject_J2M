using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;
using UnityEngine.Profiling;

namespace Game.Feature.Gameplay.Loop
{
    [Flags]
    internal enum CleanupCaptureMode
    {
        None = 0,
        Timing = 1,
        Structural = 2,
        Reference = 4,
    }

    internal enum CleanupCaptureStrategy
    {
        AFullScanNoCandidates = 0,
        BCandidateMaintenanceFullScan = 1,
        CCandidateMaintenanceIndexed = 2,
    }

    internal static class CleanupCaptureStrategySelector
    {
        internal static bool TryResolveS3A(
            string requested,
            out CleanupCaptureStrategy strategy,
            out string rejectionReason)
        {
            strategy = CleanupCaptureStrategy.AFullScanNoCandidates;
            rejectionReason = string.Empty;
            if (string.Equals(requested, "A", StringComparison.Ordinal))
            {
                return true;
            }

            rejectionReason = string.Equals(requested, "B", StringComparison.Ordinal) ||
                              string.Equals(requested, "C", StringComparison.Ordinal)
                ? $"Cleanup strategy {requested} is unavailable during S3-A."
                : $"Unknown Cleanup strategy '{requested ?? string.Empty}'.";
            return false;
        }
    }

    internal readonly struct CleanupSlice3Counts
    {
        internal CleanupSlice3Counts(
            int fullScanInvocationCount,
            int fullScanEntityVisitCount,
            int survivorCopyCount,
            int removalCandidateCount,
            int timerCandidateCount,
            int immediateTransitionCandidateCount,
            int removalProcessedCount,
            int timerProcessedCount,
            int transitionProcessedCount,
            int zeroCandidateOpportunityCount,
            int referenceOracleInvocationCount,
            int indexedInvocationCount,
            int hiddenFallbackCount,
            int invariantMismatchCount,
            int cleanupProcessorTimingSampleCount,
            long cleanupProcessorElapsedTicks,
            int runCleanupPhaseTimingSampleCount,
            long runCleanupPhaseElapsedTicks)
        {
            FullScanInvocationCount = fullScanInvocationCount;
            FullScanEntityVisitCount = fullScanEntityVisitCount;
            SurvivorCopyCount = survivorCopyCount;
            RemovalCandidateCount = removalCandidateCount;
            TimerCandidateCount = timerCandidateCount;
            ImmediateTransitionCandidateCount = immediateTransitionCandidateCount;
            RemovalProcessedCount = removalProcessedCount;
            TimerProcessedCount = timerProcessedCount;
            TransitionProcessedCount = transitionProcessedCount;
            ZeroCandidateOpportunityCount = zeroCandidateOpportunityCount;
            ReferenceOracleInvocationCount = referenceOracleInvocationCount;
            IndexedInvocationCount = indexedInvocationCount;
            HiddenFallbackCount = hiddenFallbackCount;
            InvariantMismatchCount = invariantMismatchCount;
            CleanupProcessorTimingSampleCount = cleanupProcessorTimingSampleCount;
            CleanupProcessorElapsedTicks = cleanupProcessorElapsedTicks;
            RunCleanupPhaseTimingSampleCount = runCleanupPhaseTimingSampleCount;
            RunCleanupPhaseElapsedTicks = runCleanupPhaseElapsedTicks;
        }

        public int FullScanInvocationCount { get; }
        public int FullScanEntityVisitCount { get; }
        public int SurvivorCopyCount { get; }
        public int RemovalCandidateCount { get; }
        public int TimerCandidateCount { get; }
        public int ImmediateTransitionCandidateCount { get; }
        public int RemovalProcessedCount { get; }
        public int TimerProcessedCount { get; }
        public int TransitionProcessedCount { get; }
        public int ZeroCandidateOpportunityCount { get; }
        public int ReferenceOracleInvocationCount { get; }
        public int IndexedInvocationCount { get; }
        public int HiddenFallbackCount { get; }
        public int InvariantMismatchCount { get; }
        public int CandidateMembershipCheckCount => 0;
        public int CandidateMembershipAddCount => 0;
        public int CandidateMembershipRemoveCount => 0;
        public int SnapshotCandidateArrayCount => 0;
        public int SnapshotCandidateCarriedItemCount => 0;
        public int FastImportCandidateItemCount => 0;
        public int FastImportSeparatePredicateRebuildEntityVisitCount => 0;
        public int CleanupProcessorTimingSampleCount { get; }
        public long CleanupProcessorElapsedTicks { get; }
        public int RunCleanupPhaseTimingSampleCount { get; }
        public long RunCleanupPhaseElapsedTicks { get; }
    }

    internal readonly struct CleanupStructuralScanCounts
    {
        internal CleanupStructuralScanCounts(
            int removalCandidateCount,
            int timerCandidateCount,
            int immediateTransitionCandidateCount)
        {
            RemovalCandidateCount = removalCandidateCount;
            TimerCandidateCount = timerCandidateCount;
            ImmediateTransitionCandidateCount = immediateTransitionCandidateCount;
        }

        internal int RemovalCandidateCount { get; }
        internal int TimerCandidateCount { get; }
        internal int ImmediateTransitionCandidateCount { get; }
    }

    internal static class CleanupSlice3Diagnostics
    {
        [ThreadStatic]
        private static Capture _current;

        internal static bool IsEnabled => _current != null;

        internal static bool ShouldCaptureStructural =>
            _current != null && (_current.Mode & CleanupCaptureMode.Structural) != 0;

        internal static bool ShouldCaptureTiming =>
            _current != null && (_current.Mode & CleanupCaptureMode.Timing) != 0;

        internal static bool ShouldCompareReferenceOracle => _current != null && _current.CompareReferenceOracle;

        internal static long StopwatchFrequency => Stopwatch.Frequency;

        internal static long MeasureDisabledNoOpAllocatedBytes(int iterations)
        {
            if (_current != null)
            {
                throw new InvalidOperationException("Disabled diagnostics allocation can only be measured without an active capture.");
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < Math.Max(0, iterations); index++)
            {
                BeginTiming();
                RecordFullScan(0, 0, 0, 0, 0, 0, 0, 0);
                RecordReferenceOracleInvocation();
                RecordInvariantMismatch();
                RecordCleanupProcessorTiming(0L);
                RecordRunCleanupPhaseTiming(0L);
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        internal static CleanupSlice3Scope BeginCapture(bool compareReferenceOracle = false)
        {
            return BeginCapture(
                CleanupCaptureMode.Timing |
                CleanupCaptureMode.Structural |
                (compareReferenceOracle ? CleanupCaptureMode.Reference : CleanupCaptureMode.None));
        }

        internal static CleanupSlice3Scope BeginCapture(CleanupCaptureMode mode)
        {
            var previous = _current;
            var capture = new Capture(mode);
            _current = capture;
            return new CleanupSlice3Scope(previous, capture);
        }

        internal static long BeginTiming()
        {
            return ShouldCaptureTiming ? Stopwatch.GetTimestamp() : 0L;
        }

        internal static void RecordFullScan(
            int entityVisits,
            int survivorCopies,
            int removalCandidates,
            int timerCandidates,
            int immediateTransitionCandidates,
            int removalProcessed,
            int timerProcessed,
            int transitionProcessed)
        {
            if (ShouldCaptureStructural)
            {
                _current.RecordFullScan(
                    entityVisits,
                    survivorCopies,
                    removalCandidates,
                    timerCandidates,
                    immediateTransitionCandidates,
                    removalProcessed,
                    timerProcessed,
                    transitionProcessed);
            }
        }

        internal static void RecordReferenceOracleInvocation()
        {
            _current?.RecordReferenceOracleInvocation();
        }

        internal static void RecordInvariantMismatch()
        {
            _current?.RecordInvariantMismatch();
        }

        internal static void RecordCleanupProcessorTiming(long startedAt)
        {
            if (ShouldCaptureTiming)
            {
                _current.RecordCleanupProcessorTiming(Stopwatch.GetTimestamp() - startedAt);
            }
        }

        internal static void RecordRunCleanupPhaseTiming(long startedAt)
        {
            if (ShouldCaptureTiming)
            {
                _current.RecordRunCleanupPhaseTiming(Stopwatch.GetTimestamp() - startedAt);
            }
        }

        internal sealed class Capture
        {
            private int _fullScanInvocationCount;
            private int _fullScanEntityVisitCount;
            private int _survivorCopyCount;
            private int _removalCandidateCount;
            private int _timerCandidateCount;
            private int _immediateTransitionCandidateCount;
            private int _removalProcessedCount;
            private int _timerProcessedCount;
            private int _transitionProcessedCount;
            private int _zeroCandidateOpportunityCount;
            private int _referenceOracleInvocationCount;
            private int _invariantMismatchCount;
            private int _cleanupProcessorTimingSampleCount;
            private long _cleanupProcessorElapsedTicks;
            private int _runCleanupPhaseTimingSampleCount;
            private long _runCleanupPhaseElapsedTicks;

            internal Capture(CleanupCaptureMode mode)
            {
                Mode = mode;
            }

            internal CleanupCaptureMode Mode { get; }

            internal bool CompareReferenceOracle => (Mode & CleanupCaptureMode.Reference) != 0;

            internal CleanupSlice3Counts Counts => new CleanupSlice3Counts(
                _fullScanInvocationCount,
                _fullScanEntityVisitCount,
                _survivorCopyCount,
                _removalCandidateCount,
                _timerCandidateCount,
                _immediateTransitionCandidateCount,
                _removalProcessedCount,
                _timerProcessedCount,
                _transitionProcessedCount,
                _zeroCandidateOpportunityCount,
                _referenceOracleInvocationCount,
                indexedInvocationCount: 0,
                hiddenFallbackCount: 0,
                _invariantMismatchCount,
                _cleanupProcessorTimingSampleCount,
                _cleanupProcessorElapsedTicks,
                _runCleanupPhaseTimingSampleCount,
                _runCleanupPhaseElapsedTicks);

            internal void RecordFullScan(
                int entityVisits,
                int survivorCopies,
                int removalCandidates,
                int timerCandidates,
                int immediateTransitionCandidates,
                int removalProcessed,
                int timerProcessed,
                int transitionProcessed)
            {
                _fullScanInvocationCount++;
                _fullScanEntityVisitCount += entityVisits;
                _survivorCopyCount += survivorCopies;
                _removalCandidateCount += removalCandidates;
                _timerCandidateCount += timerCandidates;
                _immediateTransitionCandidateCount += immediateTransitionCandidates;
                _removalProcessedCount += removalProcessed;
                _timerProcessedCount += timerProcessed;
                _transitionProcessedCount += transitionProcessed;
                if (removalCandidates == 0 && timerCandidates == 0 && immediateTransitionCandidates == 0)
                {
                    _zeroCandidateOpportunityCount++;
                }
            }

            internal void RecordReferenceOracleInvocation()
            {
                _referenceOracleInvocationCount++;
            }

            internal void RecordInvariantMismatch()
            {
                _invariantMismatchCount++;
            }

            internal void RecordCleanupProcessorTiming(long elapsedTicks)
            {
                _cleanupProcessorTimingSampleCount++;
                _cleanupProcessorElapsedTicks += elapsedTicks;
            }

            internal void RecordRunCleanupPhaseTiming(long elapsedTicks)
            {
                _runCleanupPhaseTimingSampleCount++;
                _runCleanupPhaseElapsedTicks += elapsedTicks;
            }
        }

        internal readonly struct CleanupSlice3Scope : IDisposable
        {
            private readonly Capture _previous;
            private readonly Capture _capture;

            internal CleanupSlice3Scope(Capture previous, Capture capture)
            {
                _previous = previous;
                _capture = capture;
            }

            public CleanupSlice3Counts Counts => _capture?.Counts ?? default;

            public void Dispose()
            {
                if (_current == _capture)
                {
                    _current = _previous;
                }
            }
        }
    }

    internal enum CleanupCommitOperationKind
    {
        RemoveEntity = 0,
        ApplyStateChange = 1,
        RemoveBoxInteractionLockState = 2,
        RemoveEnemyGravityFieldAuraFieldState = 3,
        ClearPendingEnemyBlockedReaction = 4,
    }

    internal readonly struct CleanupCommitOperation
    {
        internal CleanupCommitOperation(
            CleanupCommitOperationKind kind,
            int entityId,
            EntityPhaseState state = default,
            int stateTimer = 0)
        {
            Kind = kind;
            EntityId = entityId;
            State = state;
            StateTimer = stateTimer;
        }

        internal CleanupCommitOperationKind Kind { get; }
        internal int EntityId { get; }
        internal EntityPhaseState State { get; }
        internal int StateTimer { get; }
    }

    internal sealed class CleanupReferenceOperationPlan
    {
        internal CleanupReferenceOperationPlan(
            CleanupPhaseResult result,
            IReadOnlyList<CleanupCommitOperation> operations)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        internal CleanupPhaseResult Result { get; }
        internal IReadOnlyList<CleanupCommitOperation> Operations { get; }
    }

    internal sealed class CleanupRecordingCommitContext : ICleanupCommitContext
    {
        private readonly ICleanupCommitContext _inner;
        private readonly List<CleanupCommitOperation> _operations = new();

        internal CleanupRecordingCommitContext(ICleanupCommitContext inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal IReadOnlyList<CleanupCommitOperation> Operations => _operations;

        public void ApplyStateChange(int entityId, EntityPhaseState state, int stateTimer)
        {
            _operations.Add(
                new CleanupCommitOperation(
                    CleanupCommitOperationKind.ApplyStateChange,
                    entityId,
                    state,
                    stateTimer));
            _inner.ApplyStateChange(entityId, state, stateTimer);
        }

        public void RemoveEntity(int entityId)
        {
            _operations.Add(new CleanupCommitOperation(CleanupCommitOperationKind.RemoveEntity, entityId));
            _inner.RemoveEntity(entityId);
        }

        public void RemoveBoxInteractionLockState(int entityId)
        {
            _operations.Add(
                new CleanupCommitOperation(
                    CleanupCommitOperationKind.RemoveBoxInteractionLockState,
                    entityId));
            _inner.RemoveBoxInteractionLockState(entityId);
        }

        public void RemoveEnemyGravityFieldAuraFieldState(int fieldId)
        {
            _operations.Add(
                new CleanupCommitOperation(
                    CleanupCommitOperationKind.RemoveEnemyGravityFieldAuraFieldState,
                    fieldId));
            _inner.RemoveEnemyGravityFieldAuraFieldState(fieldId);
        }

        public void ClearPendingEnemyBlockedReaction(int entityId)
        {
            _operations.Add(
                new CleanupCommitOperation(
                    CleanupCommitOperationKind.ClearPendingEnemyBlockedReaction,
                    entityId));
            _inner.ClearPendingEnemyBlockedReaction(entityId);
        }
    }

    internal static class CleanupReferenceOracle
    {
        internal static CleanupReferenceOperationPlan BuildOperationPlan(
            WorldSnapshot snapshot,
            int tickIndex)
        {
            return BuildOperationPlanCore(snapshot, tickIndex);
        }

        internal static CleanupPhaseResult BuildPlan(WorldSnapshot snapshot, int tickIndex)
        {
            return BuildOperationPlanCore(snapshot, tickIndex).Result;
        }

        private static CleanupReferenceOperationPlan BuildOperationPlanCore(
            WorldSnapshot snapshot,
            int tickIndex)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var ordered = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(ordered);
            var survivors = new List<EntityState>(ordered.Count);
            var removedIds = new List<int>();
            var operations = new List<CleanupCommitOperation>();
            for (var index = 0; index < ordered.Count; index++)
            {
                var entity = ordered[index];
                if (entity.hp <= 0 || entity.markedForDeath)
                {
                    removedIds.Add(entity.entityId);
                    operations.Add(
                        new CleanupCommitOperation(
                            CleanupCommitOperationKind.RemoveEntity,
                            entity.entityId));
                }
                else
                {
                    survivors.Add(entity);
                }
            }

            var removalEvents = new List<string>();
            var kinematicPoses = new List<RemovedUnitKinematicPoseRecord>();
            var continuousPoses = new List<RemovedUnitContinuousLocomotionPoseRecord>();
            for (var index = 0; index < removedIds.Count; index++)
            {
                var entityId = removedIds[index];
                if (snapshot.TryGetUnitKinematicPose(entityId, out var kinematicPose) &&
                    kinematicPose.HasAuthoritativeState)
                {
                    kinematicPoses.Add(new RemovedUnitKinematicPoseRecord(entityId, kinematicPose));
                    removalEvents.Add(
                        $"KinematicPoseRemoved|E={entityId}|Anchor={kinematicPose.AnchorCell}|Offset={kinematicPose.LocalOffset}|Mode={kinematicPose.Mode}");
                }

                if (snapshot.TryGetUnitContinuousLocomotionPose(entityId, out var continuousPose) &&
                    continuousPose.HasAuthoritativeState)
                {
                    continuousPoses.Add(new RemovedUnitContinuousLocomotionPoseRecord(entityId, continuousPose));
                    removalEvents.Add(
                        $"ContinuousLocomotionPoseRemoved|E={entityId}|Anchor={continuousPose.AnchorCell}|Offset={continuousPose.LocalOffset}|Mode={continuousPose.Mode}");
                }
            }

            var timerChanges = new List<string>();
            for (var index = 0; index < survivors.Count; index++)
            {
                var entity = survivors[index];
                if (entity.spawnTick == tickIndex || entity.stateTimer <= 0)
                {
                    continue;
                }

                var previousTimer = entity.stateTimer;
                entity.stateTimer = previousTimer - 1;
                survivors[index] = entity;
                operations.Add(
                    new CleanupCommitOperation(
                        CleanupCommitOperationKind.ApplyStateChange,
                        entity.entityId,
                        entity.state,
                        entity.stateTimer));
                timerChanges.Add(
                    $"TimerTicked|E={entity.entityId}|State={entity.state}|From={previousTimer}|To={entity.stateTimer}");
            }

            var stateTransitions = new List<string>();
            for (var index = 0; index < survivors.Count; index++)
            {
                var entity = survivors[index];
                if (entity.stateTimer > 0 ||
                    (entity.state != EntityPhaseState.Acting && entity.state != EntityPhaseState.Cooldown))
                {
                    continue;
                }

                var previousState = entity.state;
                entity.state = EntityPhaseState.Idle;
                survivors[index] = entity;
                operations.Add(
                    new CleanupCommitOperation(
                        CleanupCommitOperationKind.ApplyStateChange,
                        entity.entityId,
                        entity.state,
                        entity.stateTimer));
                stateTransitions.Add(
                    $"StateTransitioned|E={entity.entityId}|From={previousState}|To={entity.state}|Timer={entity.stateTimer}");
            }

            return new CleanupReferenceOperationPlan(
                new CleanupPhaseResult(
                    removedIds,
                    timerChanges,
                    stateTransitions,
                    removalEvents,
                    kinematicPoses,
                    continuousPoses),
                operations);
        }

        internal static bool Matches(CleanupPhaseResult expected, CleanupPhaseResult actual)
        {
            return SequenceEqual(expected.RemovedEntityIds, actual.RemovedEntityIds) &&
                   SequenceEqual(expected.TimerChanges, actual.TimerChanges) &&
                   SequenceEqual(expected.StateTransitions, actual.StateTransitions) &&
                   SequenceEqual(expected.EventLogEntries, actual.EventLogEntries) &&
                   SequenceEqual(expected.RemovedUnitKinematicPoses, actual.RemovedUnitKinematicPoses) &&
                   SequenceEqual(
                       expected.RemovedUnitContinuousLocomotionPoses,
                       actual.RemovedUnitContinuousLocomotionPoses);
        }

        internal static bool MatchesOperations(
            IReadOnlyList<CleanupCommitOperation> expected,
            IReadOnlyList<CleanupCommitOperation> actual)
        {
            if (expected.Count != actual.Count)
            {
                return false;
            }

            for (var index = 0; index < expected.Count; index++)
            {
                var left = expected[index];
                var right = actual[index];
                if (left.Kind != right.Kind ||
                    left.EntityId != right.EntityId ||
                    left.State != right.State ||
                    left.StateTimer != right.StateTimer)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SequenceEqual<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            var comparer = EqualityComparer<T>.Default;
            for (var index = 0; index < left.Count; index++)
            {
                if (!comparer.Equals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal readonly struct CleanupSlice3WorkloadObservation
    {
        internal CleanupSlice3WorkloadObservation(int mutationCount, in CleanupSlice3Counts counts)
        {
            MutationCount = mutationCount;
            Counts = counts;
        }

        internal int MutationCount { get; }

        internal CleanupSlice3Counts Counts { get; }
    }

    internal enum CleanupSlice3ScheduledOperationKind
    {
        RespawnIfMissing = 0,
        Damage = 1,
        ApplyStateChange = 2,
    }

    internal readonly struct CleanupSlice3ScheduledOperation
    {
        private CleanupSlice3ScheduledOperation(
            CleanupSlice3ScheduledOperationKind kind,
            int entityId,
            int amount,
            EntityPhaseState state,
            int stateTimer)
        {
            Kind = kind;
            EntityId = entityId;
            Amount = amount;
            State = state;
            StateTimer = stateTimer;
        }

        internal CleanupSlice3ScheduledOperationKind Kind { get; }

        internal int EntityId { get; }

        internal int Amount { get; }

        internal EntityPhaseState State { get; }

        internal int StateTimer { get; }

        internal static CleanupSlice3ScheduledOperation RespawnIfMissing(int entityId)
        {
            return new CleanupSlice3ScheduledOperation(
                CleanupSlice3ScheduledOperationKind.RespawnIfMissing,
                entityId,
                amount: 0,
                state: EntityPhaseState.Idle,
                stateTimer: 0);
        }

        internal static CleanupSlice3ScheduledOperation Damage(int entityId, int amount)
        {
            return new CleanupSlice3ScheduledOperation(
                CleanupSlice3ScheduledOperationKind.Damage,
                entityId,
                amount,
                state: EntityPhaseState.Idle,
                stateTimer: 0);
        }

        internal static CleanupSlice3ScheduledOperation ApplyStateChange(
            int entityId,
            EntityPhaseState state,
            int stateTimer)
        {
            return new CleanupSlice3ScheduledOperation(
                CleanupSlice3ScheduledOperationKind.ApplyStateChange,
                entityId,
                amount: 0,
                state: state,
                stateTimer: stateTimer);
        }
    }

    internal sealed class CleanupSlice3SyntheticWorkload
    {
        private const int WorkloadEntityCount = 256;
        private const int BoardWidth = 16;
        private const int StressRemovalCount = 16;
        private const int StressTimerCount = 32;
        private const int StressImmediateTransitionCount = 32;

        private readonly bool _isStress;
        private readonly EntityState[] _initialEntities;
        private readonly CleanupSlice3ScheduledOperation[] _schedule;
        private readonly WorldState _world;
        private readonly TickPipeline _pipeline;

        private CleanupSlice3SyntheticWorkload(string workloadId, int seed, bool isStress)
        {
            WorkloadId = workloadId;
            Seed = seed;
            _isStress = isStress;
            _schedule = BuildSchedule(seed, isStress);
            ScheduleHash = ComputeSha256(BuildScheduleCanonical(seed, _schedule));
            _initialEntities = CreateInitialEntities(seed);
            InitialWorldFingerprint = ComputeInitialWorldFingerprint(_initialEntities);
            _world = GameplayCompositionRoot.CreateWorldState(
                _initialEntities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(BoardWidth - 1, BoardWidth - 1)),
                new CubeTopologyState(FaceId.Floor));
            _pipeline = GameplayCompositionRoot.CreateTickPipeline(_world);
        }

        internal string WorkloadId { get; }

        internal int Seed { get; }

        internal string ScheduleHash { get; }

        internal string InitialWorldFingerprint { get; }

        internal int EntityCount => WorkloadEntityCount;

        internal int WallCount => WorkloadEntityCount;

        internal int ScheduledOperationCount => _schedule.Length;

        internal static CleanupSlice3SyntheticWorkload CreateTarget()
        {
            return new CleanupSlice3SyntheticWorkload(
                "cleanup-s3-target-wall-empty-v2",
                31001,
                isStress: false);
        }

        internal static CleanupSlice3SyntheticWorkload CreateStress()
        {
            return new CleanupSlice3SyntheticWorkload(
                "cleanup-s3-stress-dense-v2",
                31002,
                isStress: true);
        }

        internal CleanupSlice3WorkloadObservation RunTick(int tickIndex, bool compareReferenceOracle = false)
        {
            return RunTick(
                tickIndex,
                CleanupCaptureMode.Timing |
                CleanupCaptureMode.Structural |
                (compareReferenceOracle ? CleanupCaptureMode.Reference : CleanupCaptureMode.None));
        }

        internal CleanupSlice3WorkloadObservation RunTick(int tickIndex, CleanupCaptureMode captureMode)
        {
            var mutationCount = _isStress ? ApplyStressSchedule(tickIndex) : 0;
            CleanupSlice3Counts counts;
            using (var capture = CleanupSlice3Diagnostics.BeginCapture(captureMode))
            {
                _pipeline.RunTick(new TickInput(tickIndex));
                counts = capture.Counts;
            }

            return new CleanupSlice3WorkloadObservation(mutationCount, counts);
        }

        internal int RunTickCaptureOff(int tickIndex)
        {
            var mutationCount = _isStress ? ApplyStressSchedule(tickIndex) : 0;
            _pipeline.RunTick(new TickInput(tickIndex));
            return mutationCount;
        }

        private int ApplyStressSchedule(int tickIndex)
        {
            var mutationCount = 0;
            var snapshot = _world.CreateSnapshot();
            var writeContext = _world.CreateWriteContext();
            var attackContext = (IAttackCommitContext)writeContext;
            for (var index = 0; index < _schedule.Length; index++)
            {
                var operation = _schedule[index];
                switch (operation.Kind)
                {
                    case CleanupSlice3ScheduledOperationKind.RespawnIfMissing:
                        if (!snapshot.TryGetEntity(operation.EntityId, out _))
                        {
                            var entity = _initialEntities[operation.EntityId - 1];
                            entity.spawnTick = tickIndex;
                            writeContext.SpawnEntity(entity);
                            mutationCount++;
                        }

                        break;
                    case CleanupSlice3ScheduledOperationKind.Damage:
                        writeContext.ApplyDamage(operation.EntityId, operation.Amount);
                        mutationCount++;
                        break;
                    case CleanupSlice3ScheduledOperationKind.ApplyStateChange:
                        attackContext.ApplyStateChange(
                            operation.EntityId,
                            operation.State,
                            operation.StateTimer);
                        mutationCount++;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported Cleanup Slice 3 scheduled operation: {operation.Kind}");
                }
            }

            return mutationCount;
        }

        private static CleanupSlice3ScheduledOperation[] BuildSchedule(int seed, bool isStress)
        {
            if (!isStress)
            {
                return Array.Empty<CleanupSlice3ScheduledOperation>();
            }

            var operations = new CleanupSlice3ScheduledOperation[
                (StressRemovalCount * 2) + StressTimerCount + StressImmediateTransitionCount];
            var cursor = 0;
            for (var index = 0; index < StressRemovalCount; index++)
            {
                var entityId = ScheduledEntityId(seed, index);
                operations[cursor++] = CleanupSlice3ScheduledOperation.RespawnIfMissing(entityId);
                operations[cursor++] = CleanupSlice3ScheduledOperation.Damage(entityId, amount: 1);
            }

            var timerEndExclusive = StressRemovalCount + StressTimerCount;
            for (var index = StressRemovalCount; index < timerEndExclusive; index++)
            {
                operations[cursor++] = CleanupSlice3ScheduledOperation.ApplyStateChange(
                    ScheduledEntityId(seed, index),
                    EntityPhaseState.Acting,
                    stateTimer: 2);
            }

            var immediateEndExclusive = timerEndExclusive + StressImmediateTransitionCount;
            for (var index = timerEndExclusive; index < immediateEndExclusive; index++)
            {
                operations[cursor++] = CleanupSlice3ScheduledOperation.ApplyStateChange(
                    ScheduledEntityId(seed, index),
                    EntityPhaseState.Cooldown,
                    stateTimer: 0);
            }

            return operations;
        }

        private static EntityState[] CreateInitialEntities(int seed)
        {
            var entities = new EntityState[WorkloadEntityCount];
            for (var index = 0; index < entities.Length; index++)
            {
                entities[index] = new EntityState
                {
                    entityId = index + 1,
                    position = new SurfaceCell(
                        FaceId.Floor,
                        ((index + seed) % WorkloadEntityCount) % BoardWidth,
                        ((index + seed) % WorkloadEntityCount) / BoardWidth),
                    hp = 1,
                    maxHp = 1,
                    type = EntityType.None,
                    state = EntityPhaseState.Idle,
                    stateTimer = 0,
                    facing = Direction.Right,
                    boardPresence = EntityBoardPresence.Occupying,
                };
            }

            return entities;
        }

        private static string ComputeInitialWorldFingerprint(IReadOnlyList<EntityState> entities)
        {
            var canonical = new StringBuilder(entities.Count * 40);
            canonical.Append("board=Floor:0,0..15,15|entities=");
            for (var index = 0; index < entities.Count; index++)
            {
                var entity = entities[index];
                canonical.Append(entity.entityId).Append(':')
                    .Append((int)entity.type).Append(':')
                    .Append((int)entity.position.face).Append(':')
                    .Append(entity.position.x).Append(':')
                    .Append(entity.position.y).Append(':')
                    .Append(entity.hp).Append(':')
                    .Append(entity.maxHp).Append(':')
                    .Append(entity.teamId).Append(':')
                    .Append((int)entity.state).Append(':')
                    .Append(entity.stateTimer).Append(':')
                    .Append((int)entity.facing).Append(':')
                    .Append((int)entity.boardPresence).Append(':')
                    .Append(entity.markedForDeath ? 1 : 0).Append(':')
                    .Append(entity.spawnTick).Append(':')
                    .Append((int)entity.unitRole).Append(':')
                    .Append((int)entity.unitMobilityKind).Append(':')
                    .Append((int)entity.boxCapabilities).Append(':')
                    .Append((int)entity.boxArchetype).Append(':')
                    .Append((int)entity.gravityFieldPhase).Append(':')
                    .Append(entity.gravityFieldTimerTicks).Append(':')
                    .Append(entity.kineticInstigatorEntityId).Append(':')
                    .Append(entity.kineticInstigatorTeamId).Append(':')
                    .Append((int)entity.aiMode).Append(':')
                    .Append(entity.aiStateTimer).Append(':')
                    .Append(entity.enemyLocomotionCooldownTicks).Append(':')
                    .Append(entity.enemyAttackCooldownTicks).Append(':')
                    .Append(entity.enemyAttackCooldownTotalTicks).Append(';');
            }

            return ComputeSha256(canonical.ToString());
        }

        private static string BuildScheduleCanonical(
            int seed,
            IReadOnlyList<CleanupSlice3ScheduledOperation> schedule)
        {
            var canonical = new StringBuilder();
            canonical.Append("v2|seed=").Append(seed).Append("|operations=");
            if (schedule.Count == 0)
            {
                return canonical.Append("none").ToString();
            }

            for (var index = 0; index < schedule.Count; index++)
            {
                var operation = schedule[index];
                switch (operation.Kind)
                {
                    case CleanupSlice3ScheduledOperationKind.RespawnIfMissing:
                        canonical.Append("respawn-if-missing:")
                            .Append(operation.EntityId)
                            .Append(';');
                        break;
                    case CleanupSlice3ScheduledOperationKind.Damage:
                        canonical.Append("damage:")
                            .Append(operation.EntityId)
                            .Append(':')
                            .Append(operation.Amount)
                            .Append(';');
                        break;
                    case CleanupSlice3ScheduledOperationKind.ApplyStateChange:
                        canonical.Append("state:")
                            .Append(operation.EntityId)
                            .Append(':')
                            .Append(operation.State)
                            .Append(':')
                            .Append(operation.StateTimer)
                            .Append(';');
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported Cleanup Slice 3 scheduled operation: {operation.Kind}");
                }
            }

            return canonical.ToString();
        }

        private static int ScheduledEntityId(int seed, int scheduleIndex)
        {
            return ((scheduleIndex + seed) % WorkloadEntityCount) + 1;
        }

        private static string ComputeSha256(string value)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                var result = new StringBuilder(bytes.Length * 2);
                for (var index = 0; index < bytes.Length; index++)
                {
                    result.Append(bytes[index].ToString("x2"));
                }

                return result.ToString();
            }
        }
    }

#if VECTORQUAKE_CAPTURE_BUILD
    public static class CleanupSlice3PlayerCalibration
    {
        public static string CaptureJson(
            string requestedStrategy,
            int warmupTicks,
            int sampleTicks,
            int repetitions)
        {
            if (!CleanupCaptureStrategySelector.TryResolveS3A(
                    requestedStrategy,
                    out var strategy,
                    out var rejectionReason))
            {
                throw new InvalidOperationException(rejectionReason);
            }

            if (strategy != CleanupCaptureStrategy.AFullScanNoCandidates)
            {
                throw new InvalidOperationException("S3-A calibration resolved an unavailable Cleanup strategy.");
            }

            warmupTicks = Math.Max(1, warmupTicks);
            sampleTicks = Math.Max(1, sampleTicks);
            repetitions = Math.Max(1, repetitions);
            var disabledNoOpAllocatedBytes = CleanupSlice3Diagnostics.MeasureDisabledNoOpAllocatedBytes(10000);
            var targetJson = CaptureWorkload(
                CleanupSlice3SyntheticWorkload.CreateTarget,
                warmupTicks,
                sampleTicks,
                repetitions,
                expectedMutationCount: 0,
                expectedRemovalCandidates: 0,
                expectedTimerCandidates: 0,
                expectedImmediateCandidates: 0);
            var stressJson = CaptureWorkload(
                CleanupSlice3SyntheticWorkload.CreateStress,
                warmupTicks,
                sampleTicks,
                repetitions,
                expectedMutationCount: 96,
                expectedRemovalCandidates: 16,
                expectedTimerCandidates: 32,
                expectedImmediateCandidates: 32);

            return "{" +
                   "\"schemaVersion\":1" +
                   ",\"stage\":\"S3-A\"" +
                   ",\"strategy\":\"A\"" +
                   ",\"repetitions\":" + repetitions +
                   ",\"warmupTicksPerRepetition\":" + warmupTicks +
                   ",\"sampleTicksPerRepetition\":" + sampleTicks +
                   ",\"allocationSignal\":\"per-tick current-thread allocated-byte delta appended by Player probe\"" +
                   ",\"diagnosticsOffNoOpAllocatedBytes\":" + disabledNoOpAllocatedBytes +
                   ",\"workloads\":[" + targetJson + "," + stressJson + "]" +
                   "}";
        }

        public static CleanupSlice3AllocationSession CreateAllocationSession(
            string requestedStrategy,
            string workloadId)
        {
            if (!CleanupCaptureStrategySelector.TryResolveS3A(
                    requestedStrategy,
                    out _,
                    out var rejectionReason))
            {
                throw new InvalidOperationException(rejectionReason);
            }

            if (string.Equals(
                    workloadId,
                    "cleanup-s3-target-wall-empty-v2",
                    StringComparison.Ordinal))
            {
                return new CleanupSlice3AllocationSession(CleanupSlice3SyntheticWorkload.CreateTarget());
            }

            if (string.Equals(
                    workloadId,
                    "cleanup-s3-stress-dense-v2",
                    StringComparison.Ordinal))
            {
                return new CleanupSlice3AllocationSession(CleanupSlice3SyntheticWorkload.CreateStress());
            }

            throw new InvalidOperationException($"Unknown Cleanup Slice 3 workload '{workloadId ?? string.Empty}'.");
        }

        private static string CaptureWorkload(
            Func<CleanupSlice3SyntheticWorkload> createWorkload,
            int warmupTicks,
            int sampleTicks,
            int repetitions,
            int expectedMutationCount,
            int expectedRemovalCandidates,
            int expectedTimerCandidates,
            int expectedImmediateCandidates)
        {
            var identity = createWorkload();
            var oracleWorkload = createWorkload();
            for (var tick = 0; tick < warmupTicks; tick++)
            {
                oracleWorkload.RunTick(1000 + tick);
            }

            var oracleObservation = oracleWorkload.RunTick(
                1000 + warmupTicks,
                compareReferenceOracle: true);
            if (oracleObservation.Counts.InvariantMismatchCount != 0)
            {
                throw new InvalidOperationException(
                    $"Reference oracle mismatch for workload {identity.WorkloadId}.");
            }

            var runJson = CaptureRunsRoundRobin(
                createWorkload,
                warmupTicks,
                sampleTicks,
                repetitions,
                expectedMutationCount,
                expectedRemovalCandidates,
                expectedTimerCandidates,
                expectedImmediateCandidates);

            return "{" +
                   "\"workloadId\":\"" + EscapeJson(identity.WorkloadId) + "\"" +
                   ",\"seed\":" + identity.Seed +
                   ",\"scheduleHash\":\"" + identity.ScheduleHash + "\"" +
                   ",\"initialWorldFingerprint\":\"" + identity.InitialWorldFingerprint + "\"" +
                   ",\"entityCount\":" + identity.EntityCount +
                   ",\"wallCount\":" + identity.WallCount +
                   ",\"expectedPerTick\":{" +
                   "\"mutations\":" + expectedMutationCount +
                   ",\"removalCandidates\":" + expectedRemovalCandidates +
                   ",\"timerCandidates\":" + expectedTimerCandidates +
                   ",\"immediateTransitionCandidates\":" + expectedImmediateCandidates +
                   "}" +
                   ",\"oracleParityVerified\":true" +
                   ",\"runs\":[" + runJson + "]" +
                   "}";
        }

        private static string CaptureRunsRoundRobin(
            Func<CleanupSlice3SyntheticWorkload> createWorkload,
            int warmupTicks,
            int sampleTicks,
            int repetitions,
            int expectedMutationCount,
            int expectedRemovalCandidates,
            int expectedTimerCandidates,
            int expectedImmediateCandidates)
        {
            var captures = new TimingRunCapture[repetitions];
            for (var repetition = 0; repetition < repetitions; repetition++)
            {
                captures[repetition] = new TimingRunCapture(
                    createWorkload(),
                    createWorkload(),
                    createWorkload(),
                    repetition + 1,
                    sampleTicks,
                    expectedMutationCount,
                    expectedRemovalCandidates,
                    expectedTimerCandidates,
                    expectedImmediateCandidates);
            }

            for (var tick = 0; tick < warmupTicks; tick++)
            {
                for (var offset = 0; offset < repetitions; offset++)
                {
                    captures[(tick + offset) % repetitions].Warm(2000 + tick);
                }
            }

            for (var tick = 0; tick < sampleTicks; tick++)
            {
                for (var offset = 0; offset < repetitions; offset++)
                {
                    captures[(tick + offset) % repetitions].Measure(3000 + tick);
                }
            }

            var json = new StringBuilder();
            for (var repetition = 0; repetition < repetitions; repetition++)
            {
                if (repetition > 0)
                {
                    json.Append(',');
                }

                json.Append(captures[repetition].ToJson());
            }

            return json.ToString();
        }

        private sealed class TimingRunCapture
        {
            private readonly CleanupSlice3SyntheticWorkload _structural;
            private readonly CleanupSlice3SyntheticWorkload _timed;
            private readonly CleanupSlice3SyntheticWorkload _captureOff;
            private readonly int _repetition;
            private readonly int _sampleTicks;
            private readonly int _expectedMutationCount;
            private readonly int _expectedRemovalCandidates;
            private readonly int _expectedTimerCandidates;
            private readonly int _expectedImmediateCandidates;
            private readonly List<double> _wholeTickMilliseconds;
            private readonly List<double> _cleanupProcessorMilliseconds;
            private readonly List<double> _runCleanupPhaseMilliseconds;
            private readonly List<double> _captureOffWholeTickMilliseconds;
            private readonly CounterTotals _totals = new CounterTotals();
            private int _sequence;

            internal TimingRunCapture(
                CleanupSlice3SyntheticWorkload structural,
                CleanupSlice3SyntheticWorkload timed,
                CleanupSlice3SyntheticWorkload captureOff,
                int repetition,
                int sampleTicks,
                int expectedMutationCount,
                int expectedRemovalCandidates,
                int expectedTimerCandidates,
                int expectedImmediateCandidates)
            {
                _structural = structural;
                _timed = timed;
                _captureOff = captureOff;
                _repetition = repetition;
                _sampleTicks = sampleTicks;
                _expectedMutationCount = expectedMutationCount;
                _expectedRemovalCandidates = expectedRemovalCandidates;
                _expectedTimerCandidates = expectedTimerCandidates;
                _expectedImmediateCandidates = expectedImmediateCandidates;
                _wholeTickMilliseconds = new List<double>(sampleTicks);
                _cleanupProcessorMilliseconds = new List<double>(sampleTicks);
                _runCleanupPhaseMilliseconds = new List<double>(sampleTicks);
                _captureOffWholeTickMilliseconds = new List<double>(sampleTicks);
            }

            internal void Warm(int tickIndex)
            {
                if ((_sequence++ & 1) == 0)
                {
                    _structural.RunTick(tickIndex, CleanupCaptureMode.Structural);
                    _timed.RunTick(tickIndex, CleanupCaptureMode.Timing);
                    _captureOff.RunTickCaptureOff(tickIndex);
                }
                else
                {
                    _captureOff.RunTickCaptureOff(tickIndex);
                    _timed.RunTick(tickIndex, CleanupCaptureMode.Timing);
                    _structural.RunTick(tickIndex, CleanupCaptureMode.Structural);
                }
            }

            internal void Measure(int tickIndex)
            {
                if ((_sequence++ & 1) == 0)
                {
                    MeasureStructural(tickIndex);
                    MeasureCaptured(tickIndex);
                    MeasureOff(tickIndex);
                }
                else
                {
                    MeasureOff(tickIndex);
                    MeasureCaptured(tickIndex);
                    MeasureStructural(tickIndex);
                }
            }

            private void MeasureStructural(int tickIndex)
            {
                var observation = _structural.RunTick(tickIndex, CleanupCaptureMode.Structural);
                _totals.Add(observation);
            }

            private void MeasureCaptured(int tickIndex)
            {
                var startedAt = Stopwatch.GetTimestamp();
                var observation = _timed.RunTick(tickIndex, CleanupCaptureMode.Timing);
                var finishedAt = Stopwatch.GetTimestamp();
                _wholeTickMilliseconds.Add(ToMilliseconds(finishedAt - startedAt));
                _cleanupProcessorMilliseconds.Add(
                    ToMilliseconds(observation.Counts.CleanupProcessorElapsedTicks));
                _runCleanupPhaseMilliseconds.Add(
                    ToMilliseconds(observation.Counts.RunCleanupPhaseElapsedTicks));
            }

            private void MeasureOff(int tickIndex)
            {
                var startedAt = Stopwatch.GetTimestamp();
                _captureOff.RunTickCaptureOff(tickIndex);
                _captureOffWholeTickMilliseconds.Add(
                    ToMilliseconds(Stopwatch.GetTimestamp() - startedAt));
            }

            internal string ToJson()
            {
                return "{" +
                       "\"repetition\":" + _repetition +
                       ",\"executedTicks\":" + _sampleTicks +
                       ",\"mutationCount\":" + _totals.MutationCount +
                       ",\"expectedMutationCount\":" + (_expectedMutationCount * _sampleTicks) +
                       ",\"removalCandidateCount\":" + _totals.RemovalCandidateCount +
                       ",\"expectedRemovalCandidateCount\":" + (_expectedRemovalCandidates * _sampleTicks) +
                       ",\"timerCandidateCount\":" + _totals.TimerCandidateCount +
                       ",\"expectedTimerCandidateCount\":" + (_expectedTimerCandidates * _sampleTicks) +
                       ",\"immediateTransitionCandidateCount\":" + _totals.ImmediateTransitionCandidateCount +
                       ",\"expectedImmediateTransitionCandidateCount\":" + (_expectedImmediateCandidates * _sampleTicks) +
                       ",\"fullScanInvocationCount\":" + _totals.FullScanInvocationCount +
                       ",\"fullScanEntityVisitCount\":" + _totals.FullScanEntityVisitCount +
                       ",\"survivorCopyCount\":" + _totals.SurvivorCopyCount +
                       ",\"removalProcessedCount\":" + _totals.RemovalProcessedCount +
                       ",\"timerProcessedCount\":" + _totals.TimerProcessedCount +
                       ",\"transitionProcessedCount\":" + _totals.TransitionProcessedCount +
                       ",\"zeroCandidateOpportunityCount\":" + _totals.ZeroCandidateOpportunityCount +
                       ",\"referenceOracleInvocationCount\":" + _totals.ReferenceOracleInvocationCount +
                       ",\"indexedInvocationCount\":" + _totals.IndexedInvocationCount +
                       ",\"hiddenFallbackCount\":" + _totals.HiddenFallbackCount +
                       ",\"invariantMismatchCount\":" + _totals.InvariantMismatchCount +
                       ",\"candidateMembershipCheckCount\":" + _totals.CandidateMembershipCheckCount +
                       ",\"candidateMembershipAddCount\":" + _totals.CandidateMembershipAddCount +
                       ",\"candidateMembershipRemoveCount\":" + _totals.CandidateMembershipRemoveCount +
                       ",\"snapshotCandidateArrayCount\":" + _totals.SnapshotCandidateArrayCount +
                       ",\"snapshotCandidateCarriedItemCount\":" + _totals.SnapshotCandidateCarriedItemCount +
                       ",\"fastImportCandidateItemCount\":" + _totals.FastImportCandidateItemCount +
                       ",\"fastImportSeparatePredicateRebuildEntityVisitCount\":" + _totals.FastImportSeparatePredicateRebuildEntityVisitCount +
                       ",\"validCleanupProcessorSamples\":" + _cleanupProcessorMilliseconds.Count +
                       ",\"validRunCleanupPhaseSamples\":" + _runCleanupPhaseMilliseconds.Count +
                       ",\"wholeTickMilliseconds\":" + MetricSummary.ToJson(_wholeTickMilliseconds) +
                       ",\"cleanupProcessorMilliseconds\":" + MetricSummary.ToJson(_cleanupProcessorMilliseconds) +
                       ",\"runCleanupPhaseMilliseconds\":" + MetricSummary.ToJson(_runCleanupPhaseMilliseconds) +
                       ",\"captureOffWholeTickMilliseconds\":" + MetricSummary.ToJson(_captureOffWholeTickMilliseconds) +
                       "}";
            }
        }

        private static string CaptureRun(
            CleanupSlice3SyntheticWorkload workload,
            int warmupTicks,
            int sampleTicks,
            int repetition,
            int expectedMutationCount,
            int expectedRemovalCandidates,
            int expectedTimerCandidates,
            int expectedImmediateCandidates)
        {
            var captureOffWorkload = workload.WorkloadId.Contains("target")
                ? CleanupSlice3SyntheticWorkload.CreateTarget()
                : CleanupSlice3SyntheticWorkload.CreateStress();
            for (var tick = 0; tick < warmupTicks; tick++)
            {
                if ((tick & 1) == 0)
                {
                    workload.RunTick(2000 + tick);
                    captureOffWorkload.RunTickCaptureOff(2000 + tick);
                }
                else
                {
                    captureOffWorkload.RunTickCaptureOff(2000 + tick);
                    workload.RunTick(2000 + tick);
                }
            }

            var wholeTickMilliseconds = new List<double>(sampleTicks);
            var cleanupProcessorMilliseconds = new List<double>(sampleTicks);
            var runCleanupPhaseMilliseconds = new List<double>(sampleTicks);
            var allocationBlocks = new List<long>(sampleTicks);
            var captureOffWholeTickMilliseconds = new List<double>(sampleTicks);
            var captureOffAllocationBlocks = new List<long>(sampleTicks);
            var totals = new CounterTotals();
            var allocationRecorder = Recorder.Get("GC.Alloc");
            allocationRecorder.FilterToCurrentThread();
            allocationRecorder.enabled = false;
            for (var tick = 0; tick < sampleTicks; tick++)
            {
                if ((tick & 1) == 0)
                {
                    MeasureCapturedTick(
                        workload,
                        3000 + tick,
                        allocationRecorder,
                        wholeTickMilliseconds,
                        cleanupProcessorMilliseconds,
                        runCleanupPhaseMilliseconds,
                        allocationBlocks,
                        totals);
                    MeasureCaptureOffTick(
                        captureOffWorkload,
                        3000 + tick,
                        allocationRecorder,
                        captureOffWholeTickMilliseconds,
                        captureOffAllocationBlocks);
                }
                else
                {
                    MeasureCaptureOffTick(
                        captureOffWorkload,
                        3000 + tick,
                        allocationRecorder,
                        captureOffWholeTickMilliseconds,
                        captureOffAllocationBlocks);
                    MeasureCapturedTick(
                        workload,
                        3000 + tick,
                        allocationRecorder,
                        wholeTickMilliseconds,
                        cleanupProcessorMilliseconds,
                        runCleanupPhaseMilliseconds,
                        allocationBlocks,
                        totals);
                }
            }
            allocationRecorder.enabled = false;

            return "{" +
                   "\"repetition\":" + repetition +
                   ",\"executedTicks\":" + sampleTicks +
                   ",\"mutationCount\":" + totals.MutationCount +
                   ",\"expectedMutationCount\":" + (expectedMutationCount * sampleTicks) +
                   ",\"removalCandidateCount\":" + totals.RemovalCandidateCount +
                   ",\"expectedRemovalCandidateCount\":" + (expectedRemovalCandidates * sampleTicks) +
                   ",\"timerCandidateCount\":" + totals.TimerCandidateCount +
                   ",\"expectedTimerCandidateCount\":" + (expectedTimerCandidates * sampleTicks) +
                   ",\"immediateTransitionCandidateCount\":" + totals.ImmediateTransitionCandidateCount +
                   ",\"expectedImmediateTransitionCandidateCount\":" + (expectedImmediateCandidates * sampleTicks) +
                   ",\"fullScanInvocationCount\":" + totals.FullScanInvocationCount +
                   ",\"fullScanEntityVisitCount\":" + totals.FullScanEntityVisitCount +
                   ",\"survivorCopyCount\":" + totals.SurvivorCopyCount +
                   ",\"removalProcessedCount\":" + totals.RemovalProcessedCount +
                   ",\"timerProcessedCount\":" + totals.TimerProcessedCount +
                   ",\"transitionProcessedCount\":" + totals.TransitionProcessedCount +
                   ",\"zeroCandidateOpportunityCount\":" + totals.ZeroCandidateOpportunityCount +
                   ",\"referenceOracleInvocationCount\":" + totals.ReferenceOracleInvocationCount +
                   ",\"indexedInvocationCount\":" + totals.IndexedInvocationCount +
                   ",\"hiddenFallbackCount\":" + totals.HiddenFallbackCount +
                   ",\"invariantMismatchCount\":" + totals.InvariantMismatchCount +
                   ",\"candidateMembershipCheckCount\":" + totals.CandidateMembershipCheckCount +
                   ",\"candidateMembershipAddCount\":" + totals.CandidateMembershipAddCount +
                   ",\"candidateMembershipRemoveCount\":" + totals.CandidateMembershipRemoveCount +
                   ",\"snapshotCandidateArrayCount\":" + totals.SnapshotCandidateArrayCount +
                   ",\"snapshotCandidateCarriedItemCount\":" + totals.SnapshotCandidateCarriedItemCount +
                   ",\"fastImportCandidateItemCount\":" + totals.FastImportCandidateItemCount +
                   ",\"fastImportSeparatePredicateRebuildEntityVisitCount\":" + totals.FastImportSeparatePredicateRebuildEntityVisitCount +
                   ",\"validCleanupProcessorSamples\":" + totals.CleanupProcessorTimingSampleCount +
                   ",\"validRunCleanupPhaseSamples\":" + totals.RunCleanupPhaseTimingSampleCount +
                   ",\"validAllocationSamples\":" + allocationBlocks.Count +
                   ",\"wholeTickMilliseconds\":" + MetricSummary.ToJson(wholeTickMilliseconds) +
                   ",\"cleanupProcessorMilliseconds\":" + MetricSummary.ToJson(cleanupProcessorMilliseconds) +
                   ",\"runCleanupPhaseMilliseconds\":" + MetricSummary.ToJson(runCleanupPhaseMilliseconds) +
                   ",\"gcAllocationBlocksPerTick\":" + LongMetricSummary.ToJson(allocationBlocks) +
                   ",\"captureOffWholeTickMilliseconds\":" + MetricSummary.ToJson(captureOffWholeTickMilliseconds) +
                   ",\"captureOffGcAllocationBlocksPerTick\":" + LongMetricSummary.ToJson(captureOffAllocationBlocks) +
                   "}";
        }

        private static void MeasureCapturedTick(
            CleanupSlice3SyntheticWorkload workload,
            int tickIndex,
            Recorder allocationRecorder,
            ICollection<double> wholeTickMilliseconds,
            ICollection<double> cleanupProcessorMilliseconds,
            ICollection<double> runCleanupPhaseMilliseconds,
            ICollection<long> allocationBlocks,
            CounterTotals totals)
        {
            allocationRecorder.enabled = false;
            allocationRecorder.enabled = true;
            var startedAt = Stopwatch.GetTimestamp();
            var observation = workload.RunTick(tickIndex);
            var finishedAt = Stopwatch.GetTimestamp();
            allocationRecorder.enabled = false;
            wholeTickMilliseconds.Add(ToMilliseconds(finishedAt - startedAt));
            cleanupProcessorMilliseconds.Add(
                ToMilliseconds(observation.Counts.CleanupProcessorElapsedTicks));
            runCleanupPhaseMilliseconds.Add(
                ToMilliseconds(observation.Counts.RunCleanupPhaseElapsedTicks));
            allocationBlocks.Add(allocationRecorder.sampleBlockCount);
            totals.Add(observation);
        }

        private static void MeasureCaptureOffTick(
            CleanupSlice3SyntheticWorkload workload,
            int tickIndex,
            Recorder allocationRecorder,
            ICollection<double> wholeTickMilliseconds,
            ICollection<long> allocationBlocks)
        {
            allocationRecorder.enabled = false;
            allocationRecorder.enabled = true;
            var startedAt = Stopwatch.GetTimestamp();
            workload.RunTickCaptureOff(tickIndex);
            var finishedAt = Stopwatch.GetTimestamp();
            allocationRecorder.enabled = false;
            wholeTickMilliseconds.Add(ToMilliseconds(finishedAt - startedAt));
            allocationBlocks.Add(allocationRecorder.sampleBlockCount);
        }

        private static int MeasureDisabledNoOpAllocationBlocks(int iterations)
        {
            var recorder = Recorder.Get("GC.Alloc");
            recorder.FilterToCurrentThread();
            recorder.enabled = false;
            recorder.enabled = true;
            for (var index = 0; index < iterations; index++)
            {
                CleanupSlice3Diagnostics.BeginTiming();
                CleanupSlice3Diagnostics.RecordFullScan(0, 0, 0, 0, 0, 0, 0, 0);
                CleanupSlice3Diagnostics.RecordReferenceOracleInvocation();
                CleanupSlice3Diagnostics.RecordInvariantMismatch();
                CleanupSlice3Diagnostics.RecordCleanupProcessorTiming(0L);
                CleanupSlice3Diagnostics.RecordRunCleanupPhaseTiming(0L);
            }

            recorder.enabled = false;
            return recorder.sampleBlockCount;
        }

        private static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private sealed class CounterTotals
        {
            internal int MutationCount;
            internal int FullScanInvocationCount;
            internal int FullScanEntityVisitCount;
            internal int SurvivorCopyCount;
            internal int RemovalCandidateCount;
            internal int TimerCandidateCount;
            internal int ImmediateTransitionCandidateCount;
            internal int RemovalProcessedCount;
            internal int TimerProcessedCount;
            internal int TransitionProcessedCount;
            internal int ZeroCandidateOpportunityCount;
            internal int ReferenceOracleInvocationCount;
            internal int IndexedInvocationCount;
            internal int HiddenFallbackCount;
            internal int InvariantMismatchCount;
            internal int CandidateMembershipCheckCount;
            internal int CandidateMembershipAddCount;
            internal int CandidateMembershipRemoveCount;
            internal int SnapshotCandidateArrayCount;
            internal int SnapshotCandidateCarriedItemCount;
            internal int FastImportCandidateItemCount;
            internal int FastImportSeparatePredicateRebuildEntityVisitCount;
            internal int CleanupProcessorTimingSampleCount;
            internal int RunCleanupPhaseTimingSampleCount;

            internal void Add(in CleanupSlice3WorkloadObservation observation)
            {
                var counts = observation.Counts;
                MutationCount += observation.MutationCount;
                FullScanInvocationCount += counts.FullScanInvocationCount;
                FullScanEntityVisitCount += counts.FullScanEntityVisitCount;
                SurvivorCopyCount += counts.SurvivorCopyCount;
                RemovalCandidateCount += counts.RemovalCandidateCount;
                TimerCandidateCount += counts.TimerCandidateCount;
                ImmediateTransitionCandidateCount += counts.ImmediateTransitionCandidateCount;
                RemovalProcessedCount += counts.RemovalProcessedCount;
                TimerProcessedCount += counts.TimerProcessedCount;
                TransitionProcessedCount += counts.TransitionProcessedCount;
                ZeroCandidateOpportunityCount += counts.ZeroCandidateOpportunityCount;
                ReferenceOracleInvocationCount += counts.ReferenceOracleInvocationCount;
                IndexedInvocationCount += counts.IndexedInvocationCount;
                HiddenFallbackCount += counts.HiddenFallbackCount;
                InvariantMismatchCount += counts.InvariantMismatchCount;
                CandidateMembershipCheckCount += counts.CandidateMembershipCheckCount;
                CandidateMembershipAddCount += counts.CandidateMembershipAddCount;
                CandidateMembershipRemoveCount += counts.CandidateMembershipRemoveCount;
                SnapshotCandidateArrayCount += counts.SnapshotCandidateArrayCount;
                SnapshotCandidateCarriedItemCount += counts.SnapshotCandidateCarriedItemCount;
                FastImportCandidateItemCount += counts.FastImportCandidateItemCount;
                FastImportSeparatePredicateRebuildEntityVisitCount += counts.FastImportSeparatePredicateRebuildEntityVisitCount;
                CleanupProcessorTimingSampleCount += counts.CleanupProcessorTimingSampleCount;
                RunCleanupPhaseTimingSampleCount += counts.RunCleanupPhaseTimingSampleCount;
            }
        }

        private static class MetricSummary
        {
            internal static string ToJson(List<double> values)
            {
                values.Sort();
                return "{" +
                       "\"count\":" + values.Count +
                       ",\"median\":" + Number(Sample(values, 0.50d)) +
                       ",\"p95\":" + Number(Sample(values, 0.95d)) +
                       ",\"p99\":" + Number(Sample(values, 0.99d)) +
                       ",\"maximum\":" + Number(values.Count == 0 ? 0d : values[values.Count - 1]) +
                       "}";
            }

            private static double Sample(IReadOnlyList<double> values, double percentile)
            {
                if (values.Count == 0)
                {
                    return 0d;
                }

                var position = percentile * (values.Count - 1);
                var lower = (int)Math.Floor(position);
                var upper = (int)Math.Ceiling(position);
                return values[lower] + ((values[upper] - values[lower]) * (position - lower));
            }

            private static string Number(double value)
            {
                return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static class LongMetricSummary
        {
            internal static string ToJson(List<long> values)
            {
                values.Sort();
                return "{" +
                       "\"count\":" + values.Count +
                       ",\"median\":" + Sample(values, 0.50d) +
                       ",\"p95\":" + Sample(values, 0.95d) +
                       ",\"p99\":" + Sample(values, 0.99d) +
                       ",\"maximum\":" + (values.Count == 0 ? 0L : values[values.Count - 1]) +
                       "}";
            }

            private static long Sample(IReadOnlyList<long> values, double percentile)
            {
                if (values.Count == 0)
                {
                    return 0L;
                }

                var index = (int)Math.Ceiling(percentile * values.Count) - 1;
                return values[Math.Max(0, Math.Min(values.Count - 1, index))];
            }
        }
    }

    public sealed class CleanupSlice3AllocationSession
    {
        private readonly CleanupSlice3SyntheticWorkload _workload;

        internal CleanupSlice3AllocationSession(CleanupSlice3SyntheticWorkload workload)
        {
            _workload = workload ?? throw new ArgumentNullException(nameof(workload));
        }

        public string WorkloadId => _workload.WorkloadId;

        public string ScheduleHash => _workload.ScheduleHash;

        public string InitialWorldFingerprint => _workload.InitialWorldFingerprint;

        public void RunTick(int tickIndex, bool captureDiagnostics)
        {
            if (captureDiagnostics)
            {
                _workload.RunTick(tickIndex);
            }
            else
            {
                _workload.RunTickCaptureOff(tickIndex);
            }
        }
    }
#endif
}

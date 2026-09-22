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

        internal static void RecordIndexed(
            int removalCandidates,
            int timerCandidates,
            int transitionCandidates,
            int removalProcessed,
            int timerProcessed,
            int transitionProcessed)
        {
            if (ShouldCaptureStructural)
            {
                _current.RecordIndexed(
                    removalCandidates,
                    timerCandidates,
                    transitionCandidates,
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
            private int _indexedInvocationCount;
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
                _indexedInvocationCount,
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

            internal void RecordIndexed(
                int removalCandidates,
                int timerCandidates,
                int transitionCandidates,
                int removalProcessed,
                int timerProcessed,
                int transitionProcessed)
            {
                _indexedInvocationCount++;
                _removalCandidateCount += removalCandidates;
                _timerCandidateCount += timerCandidates;
                _immediateTransitionCandidateCount += transitionCandidates;
                _removalProcessedCount += removalProcessed;
                _timerProcessedCount += timerProcessed;
                _transitionProcessedCount += transitionProcessed;
                if (removalCandidates == 0 && timerCandidates == 0 && transitionCandidates == 0)
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
            return CleanupSlice3PlayerCalibrationCore.Capture(
                requestedStrategy,
                warmupTicks,
                sampleTicks,
                repetitions).Json;
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
                return new CleanupSlice3AllocationSession(
                    CleanupSlice3PlayerCalibrationCore.CreateWorkload(workloadId));
            }

            if (string.Equals(
                    workloadId,
                    "cleanup-s3-stress-dense-v2",
                    StringComparison.Ordinal))
            {
                return new CleanupSlice3AllocationSession(
                    CleanupSlice3PlayerCalibrationCore.CreateWorkload(workloadId));
            }

            throw new InvalidOperationException($"Unknown Cleanup Slice 3 workload '{workloadId ?? string.Empty}'.");
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

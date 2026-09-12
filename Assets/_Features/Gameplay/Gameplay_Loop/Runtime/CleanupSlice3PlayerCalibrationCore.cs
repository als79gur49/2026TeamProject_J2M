using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Game.Feature.Gameplay.Loop
{
    internal static class CleanupSlice3PlayerCalibrationCore
    {
        internal static CleanupSlice3PlayerCalibrationResult Capture(
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
            var disabledNoOpAllocatedBytes =
                CleanupSlice3Diagnostics.MeasureDisabledNoOpAllocatedBytes(10000);
            var phaseTracker = new GlobalPhaseTracker();
            var target = new WorkloadCapture(
                CleanupSlice3SyntheticWorkload.CreateTarget,
                sampleTicks,
                repetitions,
                expectedMutationCount: 0,
                expectedRemovalCandidates: 0,
                expectedTimerCandidates: 0,
                expectedImmediateCandidates: 0);
            target.CaptureTimingAndOff(warmupTicks, sampleTicks, phaseTracker, workloadOrdinal: 0);
            var stress = new WorkloadCapture(
                CleanupSlice3SyntheticWorkload.CreateStress,
                sampleTicks,
                repetitions,
                expectedMutationCount: 96,
                expectedRemovalCandidates: 16,
                expectedTimerCandidates: 32,
                expectedImmediateCandidates: 32);
            stress.CaptureTimingAndOff(warmupTicks, sampleTicks, phaseTracker, workloadOrdinal: 1);
            target.CaptureReference(warmupTicks, sampleTicks, phaseTracker, workloadOrdinal: 0);
            stress.CaptureReference(warmupTicks, sampleTicks, phaseTracker, workloadOrdinal: 1);
            phaseTracker.Complete();
            if (!phaseTracker.IsVerified)
            {
                throw new InvalidOperationException(
                    "Cleanup Slice 3 calibration global phase order is invalid.");
            }

            var json = "{" +
                       "\"schemaVersion\":1" +
                       ",\"stage\":\"S3-A\"" +
                       ",\"strategy\":\"A\"" +
                       ",\"repetitions\":" + repetitions +
                       ",\"warmupTicksPerRepetition\":" + warmupTicks +
                       ",\"sampleTicksPerRepetition\":" + sampleTicks +
                       ",\"allocationSignal\":\"per-tick current-thread allocated-byte delta appended by Player probe\"" +
                       ",\"diagnosticsOffNoOpAllocatedBytes\":" + disabledNoOpAllocatedBytes +
                       ",\"workloads\":[" + target.ToJson() + "," + stress.ToJson() + "]" +
                       "}";

            return new CleanupSlice3PlayerCalibrationResult(
                json,
                new[] { target.Result, stress.Result },
                phaseTracker.IsVerified);
        }

        internal static CleanupSlice3PlayerDiagnosticsProjection Project(
            in CleanupSlice3Counts counts)
        {
            return new CleanupSlice3PlayerDiagnosticsProjection(
                counts.RemovalCandidateCount,
                counts.TimerCandidateCount,
                counts.ImmediateTransitionCandidateCount,
                counts.RemovalProcessedCount,
                counts.TimerProcessedCount,
                counts.TransitionProcessedCount);
        }

        internal static bool VerifyWholeCleanupParity(TickResult captureOff, TickResult captured)
        {
            if (captureOff == null || captured == null)
            {
                return false;
            }

            return captureOff.EventLog.SequenceEqual(captured.EventLog) &&
                   captureOff.FinalEntities.SequenceEqual(captured.FinalEntities) &&
                   captureOff.PhaseTrace.SequenceEqual(captured.PhaseTrace) &&
                   string.Equals(captureOff.DeterminismHash, captured.DeterminismHash, StringComparison.Ordinal) &&
                   string.Equals(captureOff.Trace.Text, captured.Trace.Text, StringComparison.Ordinal);
        }

        internal static CleanupSlice3SyntheticWorkload CreateWorkload(string workloadId)
        {
            if (string.Equals(workloadId, "cleanup-s3-target-wall-empty-v2", StringComparison.Ordinal))
            {
                return CleanupSlice3SyntheticWorkload.CreateTarget();
            }

            if (string.Equals(workloadId, "cleanup-s3-stress-dense-v2", StringComparison.Ordinal))
            {
                return CleanupSlice3SyntheticWorkload.CreateStress();
            }

            throw new InvalidOperationException($"Unknown Cleanup Slice 3 workload '{workloadId ?? string.Empty}'.");
        }

        private sealed class WorkloadCapture
        {
            private readonly Func<CleanupSlice3SyntheticWorkload> _createWorkload;
            private readonly string _workloadId;
            private readonly int _seed;
            private readonly string _scheduleHash;
            private readonly string _initialWorldFingerprint;
            private readonly int _entityCount;
            private readonly int _wallCount;
            private readonly int _expectedMutationCount;
            private readonly int _expectedRemovalCandidates;
            private readonly int _expectedTimerCandidates;
            private readonly int _expectedImmediateCandidates;
            private readonly RunCapture[] _captures;

            internal WorkloadCapture(
                Func<CleanupSlice3SyntheticWorkload> createWorkload,
                int sampleTicks,
                int repetitions,
                int expectedMutationCount,
                int expectedRemovalCandidates,
                int expectedTimerCandidates,
                int expectedImmediateCandidates)
            {
                _createWorkload = createWorkload;
                var identity = createWorkload();
                _workloadId = identity.WorkloadId;
                _seed = identity.Seed;
                _scheduleHash = identity.ScheduleHash;
                _initialWorldFingerprint = identity.InitialWorldFingerprint;
                _entityCount = identity.EntityCount;
                _wallCount = identity.WallCount;
                _expectedMutationCount = expectedMutationCount;
                _expectedRemovalCandidates = expectedRemovalCandidates;
                _expectedTimerCandidates = expectedTimerCandidates;
                _expectedImmediateCandidates = expectedImmediateCandidates;
                _captures = new RunCapture[repetitions];
                for (var repetition = 0; repetition < repetitions; repetition++)
                {
                    _captures[repetition] = new RunCapture(
                        createWorkload(),
                        createWorkload(),
                        repetition + 1,
                        sampleTicks,
                        expectedMutationCount,
                        expectedRemovalCandidates,
                        expectedTimerCandidates,
                        expectedImmediateCandidates);
                }
            }

            internal CleanupSlice3PlayerCalibrationWorkload Result
            {
                get
                {
                    var summaries = new CleanupSlice3PlayerCalibrationRun[_captures.Length];
                    var parity = true;
                    for (var repetition = 0; repetition < _captures.Length; repetition++)
                    {
                        summaries[repetition] = _captures[repetition].Summary;
                        parity &= _captures[repetition].OracleParityVerified;
                    }

                    return new CleanupSlice3PlayerCalibrationWorkload(parity, summaries);
                }
            }

            internal void CaptureTimingAndOff(
                int warmupTicks,
                int sampleTicks,
                GlobalPhaseTracker phaseTracker,
                int workloadOrdinal)
            {
                for (var tick = 0; tick < warmupTicks; tick++)
                {
                    for (var repetition = 0; repetition < _captures.Length; repetition++)
                    {
                        _captures[repetition].WarmTimingAndCaptureOff(2000 + tick);
                    }
                }

                for (var tick = 0; tick < sampleTicks; tick++)
                {
                    for (var repetition = 0; repetition < _captures.Length; repetition++)
                    {
                        _captures[repetition].MeasureTiming(3000 + tick);
                    }
                }
                var timingOrdinal = phaseTracker.MarkTimingComplete(workloadOrdinal);

                for (var tick = 0; tick < sampleTicks; tick++)
                {
                    for (var repetition = 0; repetition < _captures.Length; repetition++)
                    {
                        _captures[repetition].MeasureCaptureOff(3000 + tick);
                    }
                }
                var captureOffOrdinal = phaseTracker.MarkCaptureOffComplete(workloadOrdinal);
                foreach (var capture in _captures)
                {
                    capture.CompleteTimingAndOff(timingOrdinal, captureOffOrdinal);
                }
            }

            internal void CaptureReference(
                int warmupTicks,
                int sampleTicks,
                GlobalPhaseTracker phaseTracker,
                int workloadOrdinal)
            {
                var referenceOrdinal = phaseTracker.MarkReferenceStarted(workloadOrdinal);
                foreach (var capture in _captures)
                {
                    capture.CreateReference(_createWorkload, referenceOrdinal);
                }

                for (var tick = 0; tick < warmupTicks; tick++)
                {
                    for (var repetition = 0; repetition < _captures.Length; repetition++)
                    {
                        _captures[repetition].WarmReference(2000 + tick);
                    }
                }

                for (var tick = 0; tick < sampleTicks; tick++)
                {
                    for (var repetition = 0; repetition < _captures.Length; repetition++)
                    {
                        _captures[repetition].MeasureReference(3000 + tick);
                    }
                }

                foreach (var capture in _captures)
                {
                    capture.ReleaseReference();
                }
            }

            internal string ToJson()
            {
                var runJson = new StringBuilder();
                var parity = true;
                for (var repetition = 0; repetition < _captures.Length; repetition++)
                {
                    if (repetition > 0)
                    {
                        runJson.Append(',');
                    }

                    parity &= _captures[repetition].OracleParityVerified;
                    runJson.Append(_captures[repetition].ToJson());
                }

                return "{" +
                       "\"workloadId\":\"" + EscapeJson(_workloadId) + "\"" +
                       ",\"seed\":" + _seed +
                       ",\"scheduleHash\":\"" + _scheduleHash + "\"" +
                       ",\"initialWorldFingerprint\":\"" + _initialWorldFingerprint + "\"" +
                       ",\"entityCount\":" + _entityCount +
                       ",\"wallCount\":" + _wallCount +
                       ",\"expectedPerTick\":{" +
                       "\"mutations\":" + _expectedMutationCount +
                       ",\"removalCandidates\":" + _expectedRemovalCandidates +
                       ",\"timerCandidates\":" + _expectedTimerCandidates +
                       ",\"immediateTransitionCandidates\":" + _expectedImmediateCandidates +
                       "}" +
                       ",\"oracleParityVerified\":" + (parity ? "true" : "false") +
                       ",\"runs\":[" + runJson + "]" +
                       "}";
            }
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
        }

        private sealed class RunCapture
        {
            private CleanupSlice3SyntheticWorkload _timed;
            private CleanupSlice3SyntheticWorkload _captureOff;
            private CleanupSlice3SyntheticWorkload _reference;
            private readonly string _workloadId;
            private readonly int _seed;
            private readonly string _scheduleHash;
            private readonly string _initialWorldFingerprint;
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
            private bool _identityBindingVerified;
            private int _timingReferenceInvocationCount;
            private int _captureOffReferenceInvocationCount;
            private int _warmupReferenceInvocationCount;
            private int _timingPhaseOrdinal;
            private int _captureOffPhaseOrdinal;
            private int _referencePhaseOrdinal;

            internal RunCapture(
                CleanupSlice3SyntheticWorkload timed,
                CleanupSlice3SyntheticWorkload captureOff,
                int repetition,
                int sampleTicks,
                int expectedMutationCount,
                int expectedRemovalCandidates,
                int expectedTimerCandidates,
                int expectedImmediateCandidates)
            {
                _timed = timed;
                _captureOff = captureOff;
                _workloadId = timed.WorkloadId;
                _seed = timed.Seed;
                _scheduleHash = timed.ScheduleHash;
                _initialWorldFingerprint = timed.InitialWorldFingerprint;
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
                _identityBindingVerified = SameIdentity(timed, captureOff);
            }

            internal bool OracleParityVerified =>
                _identityBindingVerified &&
                _timingReferenceInvocationCount == 0 &&
                _captureOffReferenceInvocationCount == 0 &&
                _warmupReferenceInvocationCount == 0 &&
                _totals.ReferenceOracleInvocationCount == _sampleTicks &&
                _totals.InvariantMismatchCount == 0;

            internal CleanupSlice3PlayerCalibrationRun Summary =>
                new CleanupSlice3PlayerCalibrationRun(
                    _repetition,
                    _sampleTicks,
                    _timingReferenceInvocationCount,
                    _captureOffReferenceInvocationCount,
                    _warmupReferenceInvocationCount,
                    _totals.ReferenceOracleInvocationCount,
                    _totals.InvariantMismatchCount,
                    referenceUsesStructuralAndReference: true,
                    phaseOrder: PhaseOrder,
                    identityBindingVerified: _identityBindingVerified);

            private string PhaseOrder =>
                _timingPhaseOrdinal > 0 &&
                _captureOffPhaseOrdinal > _timingPhaseOrdinal &&
                _referencePhaseOrdinal > _captureOffPhaseOrdinal
                    ? "Timing,CaptureOff,Reference"
                    : "Invalid";

            internal void WarmTimingAndCaptureOff(int tickIndex)
            {
                _timed.RunTick(tickIndex, CleanupCaptureMode.Timing);
                _captureOff.RunTickCaptureOff(tickIndex);
            }

            internal void MeasureTiming(int tickIndex)
            {
                var startedAt = Stopwatch.GetTimestamp();
                var observation = _timed.RunTick(tickIndex, CleanupCaptureMode.Timing);
                _wholeTickMilliseconds.Add(ToMilliseconds(Stopwatch.GetTimestamp() - startedAt));
                _cleanupProcessorMilliseconds.Add(
                    ToMilliseconds(observation.Counts.CleanupProcessorElapsedTicks));
                _runCleanupPhaseMilliseconds.Add(
                    ToMilliseconds(observation.Counts.RunCleanupPhaseElapsedTicks));
                _timingReferenceInvocationCount += observation.Counts.ReferenceOracleInvocationCount;
            }

            internal void MeasureCaptureOff(int tickIndex)
            {
                var startedAt = Stopwatch.GetTimestamp();
                _captureOff.RunTickCaptureOff(tickIndex);
                _captureOffWholeTickMilliseconds.Add(ToMilliseconds(Stopwatch.GetTimestamp() - startedAt));
            }

            internal void CompleteTimingAndOff(int timingOrdinal, int captureOffOrdinal)
            {
                _timingPhaseOrdinal = timingOrdinal;
                _captureOffPhaseOrdinal = captureOffOrdinal;
                _timed = null;
                _captureOff = null;
            }

            internal void CreateReference(
                Func<CleanupSlice3SyntheticWorkload> createWorkload,
                int referenceOrdinal)
            {
                _reference = createWorkload();
                _referencePhaseOrdinal = referenceOrdinal;
                _identityBindingVerified &= SameIdentity(
                    _workloadId,
                    _seed,
                    _scheduleHash,
                    _initialWorldFingerprint,
                    _reference);
            }

            internal void WarmReference(int tickIndex)
            {
                var observation = _reference.RunTick(tickIndex, CleanupCaptureMode.Structural);
                _warmupReferenceInvocationCount += observation.Counts.ReferenceOracleInvocationCount;
            }

            internal void MeasureReference(int tickIndex)
            {
                _totals.Add(_reference.RunTick(
                    tickIndex,
                       CleanupCaptureMode.Structural | CleanupCaptureMode.Reference));
            }

            internal void ReleaseReference()
            {
                _reference = null;
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

            private static bool SameIdentity(
                CleanupSlice3SyntheticWorkload left,
                CleanupSlice3SyntheticWorkload right)
            {
                return string.Equals(left.WorkloadId, right.WorkloadId, StringComparison.Ordinal) &&
                       left.Seed == right.Seed &&
                       string.Equals(left.ScheduleHash, right.ScheduleHash, StringComparison.Ordinal) &&
                       string.Equals(
                           left.InitialWorldFingerprint,
                           right.InitialWorldFingerprint,
                           StringComparison.Ordinal);
            }

            private static bool SameIdentity(
                string workloadId,
                int seed,
                string scheduleHash,
                string initialWorldFingerprint,
                CleanupSlice3SyntheticWorkload right)
            {
                return string.Equals(workloadId, right.WorkloadId, StringComparison.Ordinal) &&
                       seed == right.Seed &&
                       string.Equals(scheduleHash, right.ScheduleHash, StringComparison.Ordinal) &&
                       string.Equals(
                           initialWorldFingerprint,
                           right.InitialWorldFingerprint,
                           StringComparison.Ordinal);
            }
        }

        private sealed class GlobalPhaseTracker
        {
            private int _step;
            private int _lastTimingOffOrdinal;
            private int _firstReferenceOrdinal;
            private bool _valid = true;

            internal bool IsVerified =>
                _valid &&
                _step == 6 &&
                _lastTimingOffOrdinal > 0 &&
                _firstReferenceOrdinal > _lastTimingOffOrdinal;

            internal int MarkTimingComplete(int workloadOrdinal)
            {
                return Mark((workloadOrdinal * 2) + 1, reference: false);
            }

            internal int MarkCaptureOffComplete(int workloadOrdinal)
            {
                return Mark((workloadOrdinal * 2) + 2, reference: false);
            }

            internal int MarkReferenceStarted(int workloadOrdinal)
            {
                return Mark(workloadOrdinal + 5, reference: true);
            }

            internal void Complete()
            {
                if (_step != 6)
                {
                    _valid = false;
                }
            }

            private int Mark(int expectedStep, bool reference)
            {
                var ordinal = ++_step;
                if (ordinal != expectedStep)
                {
                    _valid = false;
                }

                if (reference)
                {
                    if (_firstReferenceOrdinal == 0)
                    {
                        _firstReferenceOrdinal = ordinal;
                    }
                }
                else
                {
                    _lastTimingOffOrdinal = ordinal;
                }

                return ordinal;
            }
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
                FastImportSeparatePredicateRebuildEntityVisitCount +=
                    counts.FastImportSeparatePredicateRebuildEntityVisitCount;
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
    }

    internal sealed class CleanupSlice3PlayerCalibrationResult
    {
        internal CleanupSlice3PlayerCalibrationResult(
            string json,
            IReadOnlyList<CleanupSlice3PlayerCalibrationWorkload> workloads,
            bool globalPhaseOrderVerified)
        {
            Json = json;
            Workloads = workloads;
            GlobalPhaseOrderVerified = globalPhaseOrderVerified;
        }

        internal string Json { get; }
        internal IReadOnlyList<CleanupSlice3PlayerCalibrationWorkload> Workloads { get; }
        internal bool GlobalPhaseOrderVerified { get; }
    }

    internal sealed class CleanupSlice3PlayerCalibrationWorkload
    {
        internal CleanupSlice3PlayerCalibrationWorkload(
            bool oracleParityVerified,
            IReadOnlyList<CleanupSlice3PlayerCalibrationRun> runs)
        {
            OracleParityVerified = oracleParityVerified;
            Runs = runs;
        }

        internal bool OracleParityVerified { get; }
        internal IReadOnlyList<CleanupSlice3PlayerCalibrationRun> Runs { get; }
    }

    internal readonly struct CleanupSlice3PlayerCalibrationRun
    {
        internal CleanupSlice3PlayerCalibrationRun(
            int repetition,
            int executedTicks,
            int timingReferenceInvocationCount,
            int captureOffReferenceInvocationCount,
            int warmupReferenceInvocationCount,
            int referenceOracleInvocationCount,
            int invariantMismatchCount,
            bool referenceUsesStructuralAndReference,
            string phaseOrder,
            bool identityBindingVerified)
        {
            Repetition = repetition;
            ExecutedTicks = executedTicks;
            TimingReferenceInvocationCount = timingReferenceInvocationCount;
            CaptureOffReferenceInvocationCount = captureOffReferenceInvocationCount;
            WarmupReferenceInvocationCount = warmupReferenceInvocationCount;
            ReferenceOracleInvocationCount = referenceOracleInvocationCount;
            InvariantMismatchCount = invariantMismatchCount;
            ReferenceUsesStructuralAndReference = referenceUsesStructuralAndReference;
            PhaseOrder = phaseOrder;
            IdentityBindingVerified = identityBindingVerified;
        }

        internal int Repetition { get; }
        internal int ExecutedTicks { get; }
        internal int TimingReferenceInvocationCount { get; }
        internal int CaptureOffReferenceInvocationCount { get; }
        internal int WarmupReferenceInvocationCount { get; }
        internal int ReferenceOracleInvocationCount { get; }
        internal int InvariantMismatchCount { get; }
        internal bool ReferenceUsesStructuralAndReference { get; }
        internal string PhaseOrder { get; }
        internal bool IdentityBindingVerified { get; }
    }

    internal readonly struct CleanupSlice3PlayerDiagnosticsProjection
    {
        internal CleanupSlice3PlayerDiagnosticsProjection(
            int rawRemovalMatchCount,
            int rawTimerMatchCount,
            int rawImmediateTransitionMatchCount,
            int removalProcessedCount,
            int timerProcessedCount,
            int transitionProcessedCount)
        {
            RawRemovalMatchCount = rawRemovalMatchCount;
            RawTimerMatchCount = rawTimerMatchCount;
            RawImmediateTransitionMatchCount = rawImmediateTransitionMatchCount;
            RemovalProcessedCount = removalProcessedCount;
            TimerProcessedCount = timerProcessedCount;
            TransitionProcessedCount = transitionProcessedCount;
        }

        internal int RawRemovalMatchCount { get; }
        internal int RawTimerMatchCount { get; }
        internal int RawImmediateTransitionMatchCount { get; }
        internal int RemovalProcessedCount { get; }
        internal int TimerProcessedCount { get; }
        internal int TransitionProcessedCount { get; }
    }
}

using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Unity.Profiling;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class CleanupProcessor
    {
        private static readonly ProfilerMarker ProcessMarker = new("Gameplay.CleanupProcessor");

        private readonly RemovalProcessor _removalProcessor = new();
        private readonly StateTimerProcessor _stateTimerProcessor = new();
        private readonly StateTransitionProcessor _stateTransitionProcessor = new();

        public CleanupPhaseResult Process(
            WorldSnapshot snapshot,
            ICleanupCommitContext writeContext,
            int tickIndex)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            CleanupReferenceOperationPlan referencePlan = null;
            CleanupRecordingCommitContext recordingContext = null;
            if (CleanupSlice3Diagnostics.ShouldCompareReferenceOracle)
            {
                CleanupSlice3Diagnostics.RecordReferenceOracleInvocation();
                referencePlan = CleanupReferenceOracle.BuildOperationPlan(snapshot, tickIndex);
                recordingContext = new CleanupRecordingCommitContext(writeContext);
                writeContext = recordingContext;
            }

            var timingStartedAt = CleanupSlice3Diagnostics.BeginTiming();
            using var markerScope = ProcessMarker.Auto();
            try
            {
                var removedEntityIds = new List<int>();
                _removalProcessor.Process(snapshot.CleanupRemovalCandidateIds.Span, writeContext, removedEntityIds);

                var removedUnitKinematicPoses = new List<RemovedUnitKinematicPoseRecord>();
                var removedUnitContinuousLocomotionPoses = new List<RemovedUnitContinuousLocomotionPoseRecord>();
                var removalEventLogEntries = new List<string>();
                for (var i = 0; i < removedEntityIds.Count; i++)
                {
                    var removedEntityId = removedEntityIds[i];
                    if (snapshot.TryGetUnitKinematicPose(removedEntityId, out var pose) &&
                        pose.HasAuthoritativeState)
                    {
                        removedUnitKinematicPoses.Add(new RemovedUnitKinematicPoseRecord(removedEntityId, pose));
                        removalEventLogEntries.Add(
                            $"KinematicPoseRemoved|E={removedEntityId}|Anchor={pose.AnchorCell}|Offset={pose.LocalOffset}|Mode={pose.Mode}");
                    }

                    if (snapshot.TryGetUnitContinuousLocomotionPose(removedEntityId, out var continuousPose) &&
                        continuousPose.HasAuthoritativeState)
                    {
                        removedUnitContinuousLocomotionPoses.Add(
                            new RemovedUnitContinuousLocomotionPoseRecord(removedEntityId, continuousPose));
                        removalEventLogEntries.Add(
                            $"ContinuousLocomotionPoseRemoved|E={removedEntityId}|Anchor={continuousPose.AnchorCell}|Offset={continuousPose.LocalOffset}|Mode={continuousPose.Mode}");
                    }
                }

                var timerChanges = new List<string>();
                var timerExpiredTransitionCandidateIds = new List<int>();
                _stateTimerProcessor.Process(
                    snapshot,
                    removedEntityIds,
                    writeContext,
                    tickIndex,
                    timerChanges,
                    timerExpiredTransitionCandidateIds);

                var stateTransitions = new List<string>();
                _stateTransitionProcessor.Process(
                    snapshot,
                    removedEntityIds,
                    timerExpiredTransitionCandidateIds,
                    writeContext,
                    stateTransitions);

                var result = new CleanupPhaseResult(
                    removedEntityIds,
                    timerChanges,
                    stateTransitions,
                    removalEventLogEntries,
                    removedUnitKinematicPoses,
                    removedUnitContinuousLocomotionPoses);

                CleanupSlice3Diagnostics.RecordIndexed(
                    snapshot.CleanupRemovalCandidateIds.Length,
                    snapshot.CleanupTimerCandidateIds.Length,
                    snapshot.CleanupImmediateTransitionCandidateIds.Length,
                    removedEntityIds.Count,
                    timerChanges.Count,
                    stateTransitions.Count);

                if (referencePlan != null &&
                    (!CleanupReferenceOracle.Matches(referencePlan.Result, result) ||
                     !CleanupReferenceOracle.MatchesOperations(referencePlan.Operations, recordingContext.Operations)))
                {
                    CleanupSlice3Diagnostics.RecordInvariantMismatch();
                    throw new InvalidOperationException(
                        "Cleanup full-scan result diverged from the independent S3-A reference operation plan.");
                }

                return result;
            }
            finally
            {
                CleanupSlice3Diagnostics.RecordCleanupProcessorTiming(timingStartedAt);
            }
        }
    }
}

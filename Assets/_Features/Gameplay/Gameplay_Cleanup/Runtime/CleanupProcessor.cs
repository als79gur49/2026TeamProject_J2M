using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class CleanupProcessor
    {
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

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            var survivingEntities = new List<EntityState>(orderedEntities.Count);
            var removedEntityIds = new List<int>();
            _removalProcessor.Process(orderedEntities, writeContext, survivingEntities, removedEntityIds);

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
            _stateTimerProcessor.Process(survivingEntities, writeContext, tickIndex, timerChanges);

            var stateTransitions = new List<string>();
            _stateTransitionProcessor.Process(survivingEntities, writeContext, stateTransitions);

            return new CleanupPhaseResult(
                removedEntityIds,
                timerChanges,
                stateTransitions,
                removalEventLogEntries,
                removedUnitKinematicPoses,
                removedUnitContinuousLocomotionPoses);
        }

        public CleanupPhaseResult ProcessRemovalsOnly(
            WorldSnapshot snapshot,
            ICleanupCommitContext writeContext,
            IReadOnlyCollection<int> candidateEntityIds = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);
            if (candidateEntityIds != null)
            {
                for (var i = orderedEntities.Count - 1; i >= 0; i--)
                {
                    if (!Contains(candidateEntityIds, orderedEntities[i].entityId))
                    {
                        orderedEntities.RemoveAt(i);
                    }
                }
            }

            var survivingEntities = new List<EntityState>(orderedEntities.Count);
            var removedEntityIds = new List<int>();
            _removalProcessor.Process(orderedEntities, writeContext, survivingEntities, removedEntityIds);

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

            return new CleanupPhaseResult(
                removedEntityIds,
                Array.Empty<string>(),
                Array.Empty<string>(),
                removalEventLogEntries,
                removedUnitKinematicPoses,
                removedUnitContinuousLocomotionPoses);
        }

        private static bool Contains(IReadOnlyCollection<int> values, int value)
        {
            foreach (var candidate in values)
            {
                if (candidate == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

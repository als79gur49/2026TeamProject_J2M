using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    internal readonly struct RemovedUnitKinematicPoseRecord
    {
        public RemovedUnitKinematicPoseRecord(int entityId, UnitKinematicPose pose)
        {
            EntityId = entityId;
            Pose = pose;
        }

        public int EntityId { get; }

        public UnitKinematicPose Pose { get; }
    }

    internal readonly struct RemovedUnitContinuousLocomotionPoseRecord
    {
        public RemovedUnitContinuousLocomotionPoseRecord(int entityId, UnitContinuousLocomotionPose pose)
        {
            EntityId = entityId;
            Pose = pose;
        }

        public int EntityId { get; }

        public UnitContinuousLocomotionPose Pose { get; }
    }

    internal sealed class CleanupPhaseResult
    {
        public static readonly CleanupPhaseResult Empty = new(
            Array.Empty<int>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<RemovedUnitKinematicPoseRecord>(),
            Array.Empty<RemovedUnitContinuousLocomotionPoseRecord>());

        private readonly ReadOnlyCollection<string> _eventLogEntries;
        private readonly ReadOnlyCollection<int> _removedEntityIds;
        private readonly ReadOnlyCollection<RemovedUnitKinematicPoseRecord> _removedUnitKinematicPoses;
        private readonly ReadOnlyCollection<RemovedUnitContinuousLocomotionPoseRecord> _removedUnitContinuousLocomotionPoses;
        private readonly ReadOnlyCollection<string> _stateTransitions;
        private readonly ReadOnlyCollection<string> _timerChanges;

        public CleanupPhaseResult(
            IEnumerable<int> removedEntityIds,
            IEnumerable<string> timerChanges,
            IEnumerable<string> stateTransitions,
            IEnumerable<string> eventLogEntries = null,
            IEnumerable<RemovedUnitKinematicPoseRecord> removedUnitKinematicPoses = null,
            IEnumerable<RemovedUnitContinuousLocomotionPoseRecord> removedUnitContinuousLocomotionPoses = null)
        {
            if (removedEntityIds == null)
            {
                throw new ArgumentNullException(nameof(removedEntityIds));
            }

            if (timerChanges == null)
            {
                throw new ArgumentNullException(nameof(timerChanges));
            }

            if (stateTransitions == null)
            {
                throw new ArgumentNullException(nameof(stateTransitions));
            }

            _removedEntityIds = new ReadOnlyCollection<int>(new List<int>(removedEntityIds));
            _timerChanges = new ReadOnlyCollection<string>(new List<string>(timerChanges));
            _stateTransitions = new ReadOnlyCollection<string>(new List<string>(stateTransitions));
            _eventLogEntries = new ReadOnlyCollection<string>(new List<string>(eventLogEntries ?? Array.Empty<string>()));
            _removedUnitKinematicPoses = new ReadOnlyCollection<RemovedUnitKinematicPoseRecord>(
                new List<RemovedUnitKinematicPoseRecord>(
                    removedUnitKinematicPoses ?? Array.Empty<RemovedUnitKinematicPoseRecord>()));
            _removedUnitContinuousLocomotionPoses = new ReadOnlyCollection<RemovedUnitContinuousLocomotionPoseRecord>(
                new List<RemovedUnitContinuousLocomotionPoseRecord>(
                    removedUnitContinuousLocomotionPoses ?? Array.Empty<RemovedUnitContinuousLocomotionPoseRecord>()));
        }

        public IReadOnlyList<int> RemovedEntityIds => _removedEntityIds;

        public IReadOnlyList<string> TimerChanges => _timerChanges;

        public IReadOnlyList<string> StateTransitions => _stateTransitions;

        public IReadOnlyList<string> EventLogEntries => _eventLogEntries;

        public IReadOnlyList<RemovedUnitKinematicPoseRecord> RemovedUnitKinematicPoses => _removedUnitKinematicPoses;

        public IReadOnlyList<RemovedUnitContinuousLocomotionPoseRecord> RemovedUnitContinuousLocomotionPoses => _removedUnitContinuousLocomotionPoses;
    }
}

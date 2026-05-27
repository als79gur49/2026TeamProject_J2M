using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.EnemyAudio
{
    public sealed class EnemyAudioRequestPlanner
    {
        public IReadOnlyList<EnemyAudioRequest> BuildRequests(
            TickResult result,
            GameplayTimingProfile timingProfile = null)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            timingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            var enemyEntityIds = BuildEnemyEntityIdSet(result.FinalEntities);
            var motionFactEntityIds = BuildMotionFactEntityIdSet(result.PresentationData);
            var requests = new List<EnemyAudioRequest>();
            BuildMoveRequests(result.PresentationData, result.TickIndex, enemyEntityIds, requests);
            BuildActionRequests(result.PresentationData, requests);
            BuildUtilityRequests(result.PresentationData, requests);
            BuildSummonRequests(result.PresentationData, requests);
            BuildJumpRequests(result.PresentationData, requests);
            BuildGlideRequests(result.PresentationData, requests);
            BuildChargeRequests(result.PresentationData, requests);
            BuildProjectileImpactRequests(result.PresentationData, requests);
            BuildDeathRequests(result.PresentationData, timingProfile, requests);
            BuildStationaryActiveRequests(result.FinalEntities, motionFactEntityIds, requests);
            return requests;
        }

        private static HashSet<int> BuildEnemyEntityIdSet(IReadOnlyList<EntityState> finalEntities)
        {
            var entityIds = new HashSet<int>();
            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (EntityRolePolicy.IsEnemyUnit(entity))
                {
                    entityIds.Add(entity.entityId);
                }
            }

            return entityIds;
        }

        private static HashSet<int> BuildMotionFactEntityIdSet(TickPresentationData presentationData)
        {
            var entityIds = new HashSet<int>();
            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                if (motions[i].EntityId > 0)
                {
                    entityIds.Add(motions[i].EntityId);
                }
            }

            var kinematicTracks = presentationData.KinematicMotionTracks;
            for (var i = 0; i < kinematicTracks.Count; i++)
            {
                if (kinematicTracks[i].EntityId > 0)
                {
                    entityIds.Add(kinematicTracks[i].EntityId);
                }
            }

            return entityIds;
        }

        private static void BuildMoveRequests(
            TickPresentationData presentationData,
            int tickIndex,
            ISet<int> enemyEntityIds,
            ICollection<EnemyAudioRequest> requests)
        {
            var plannedMoveEntityIds = new HashSet<int>();
            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (motion.MotionKind != TickEntityMotionKind.Move ||
                    !enemyEntityIds.Contains(motion.EntityId) ||
                    !plannedMoveEntityIds.Add(motion.EntityId))
                {
                    continue;
                }

                AddRequest(motion.EntityId, EnemyAudioCue.Move, requests);
            }

            var kinematicTracks = presentationData.KinematicMotionTracks;
            for (var i = 0; i < kinematicTracks.Count; i++)
            {
                var track = kinematicTracks[i];
                if (!IsEnemyLocomotionMoveTrack(track, tickIndex, enemyEntityIds) ||
                    !plannedMoveEntityIds.Add(track.EntityId))
                {
                    continue;
                }

                AddRequest(track.EntityId, EnemyAudioCue.Move, requests);
            }
        }

        private static bool IsEnemyLocomotionMoveTrack(
            in TickKinematicMotionTrack track,
            int tickIndex,
            ISet<int> enemyEntityIds)
        {
            return track.EntityId > 0 &&
                   enemyEntityIds.Contains(track.EntityId) &&
                   track.EntityType == EntityType.Unit &&
                   track.TerminalKind == TickKinematicMotionTerminalKind.None &&
                   track.MotionMode == MotionMode.Voluntary &&
                   track.ForcedMotionOp == ForcedMotionOp.None &&
                   HasActualKinematicMovement(track) &&
                   IsKinematicMoveStartTick(track, tickIndex);
        }

        private static bool HasActualKinematicMovement(in TickKinematicMotionTrack track)
        {
            return !track.SourceAnchorCell.Equals(track.DestinationAnchorCell) ||
                   !track.SourceLocalOffset.Equals(track.DestinationLocalOffset);
        }

        private static bool IsKinematicMoveStartTick(in TickKinematicMotionTrack track, int tickIndex)
        {
            return track.StartedTick <= 0 ||
                   track.StartedTick == tickIndex;
        }

        private static void BuildActionRequests(
            TickPresentationData presentationData,
            ICollection<EnemyAudioRequest> requests)
        {
            var signals = presentationData.EnemyActionSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                AddRequestIf(signal.EntityId, EnemyAudioCue.Windup, signal.StartedThisTick, requests);
                AddEnemyActionExecutionRequest(signal, requests);
                AddRequestIf(signal.EntityId, EnemyAudioCue.Recover, signal.StartedRecoveryThisTick, requests);
            }
        }

        private static void AddEnemyActionExecutionRequest(
            in TickEnemyActionPresentationSignal signal,
            ICollection<EnemyAudioRequest> requests)
        {
            if (!signal.ExecutedThisTick)
            {
                return;
            }

            if (signal.PresentationOutcome == EnemyActionPresentationOutcome.RejectedByReceiverCooldown)
            {
                return;
            }

            if (signal.PresentationSource == EnemyActionPresentationSource.PassiveContact)
            {
                if (signal.PresentationOutcome == EnemyActionPresentationOutcome.RejectedByPlayerInvincible)
                {
                    return;
                }

                AddRequest(signal.EntityId, EnemyAudioCue.PassiveContact, requests);
                return;
            }

            AddRequest(signal.EntityId, EnemyAudioCue.Active, requests);
        }

        private static void BuildUtilityRequests(
            TickPresentationData presentationData,
            ICollection<EnemyAudioRequest> requests)
        {
            var signals = presentationData.EnemyUtilitySignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                AddRequestIf(
                    signal.EntityId,
                    EnemyAudioCue.Windup,
                    signal.Phase == EnemyUtilityPresentationPhase.WindupStarted &&
                    (signal.Kind == EnemyUtilityPresentationKind.LockNearbyBoxes ||
                     signal.Kind == EnemyUtilityPresentationKind.GravityFieldAura),
                    requests);
                AddRequestIf(
                    signal.EntityId,
                    EnemyAudioCue.Recover,
                    (signal.Kind == EnemyUtilityPresentationKind.LockNearbyBoxes ||
                     signal.Kind == EnemyUtilityPresentationKind.GravityFieldAura) &&
                    signal.Phase == EnemyUtilityPresentationPhase.RecoverStarted,
                    requests);
                AddRequestIf(
                    signal.EntityId,
                    EnemyAudioCue.Active,
                    signal.Kind == EnemyUtilityPresentationKind.GravityFieldAura &&
                    (signal.Phase == EnemyUtilityPresentationPhase.ActiveStarted ||
                     signal.Phase == EnemyUtilityPresentationPhase.AttackStarted),
                    requests);
            }
        }

        private static void BuildSummonRequests(
            TickPresentationData presentationData,
            ICollection<EnemyAudioRequest> requests)
        {
            var signals = presentationData.SummonWindupWarnings;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                AddRequestIf(
                    signal.SourceEntityId,
                    EnemyAudioCue.Windup,
                    signal.SourceEntityId > 0 &&
                    signal.TickIndex == signal.WindupStartTick,
                    requests);
            }

            var visibilityChanges = presentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var visibilityChange = visibilityChanges[i];
                if (visibilityChange.ChangeKind != TickVisibilityChangeKind.Spawn ||
                    !TryResolveSummonSourceEntityId(
                        presentationData,
                        visibilityChange.EntityId,
                        out var sourceEntityId))
                {
                    continue;
                }

                AddRequest(sourceEntityId, EnemyAudioCue.Active, requests);
            }
        }

        private static bool TryResolveSummonSourceEntityId(
            TickPresentationData presentationData,
            int summonedEntityId,
            out int sourceEntityId)
        {
            var bindings = presentationData.SummonedEnemyPresentationBindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.EntityId == summonedEntityId &&
                    binding.SourceEntityId > 0)
                {
                    sourceEntityId = binding.SourceEntityId;
                    return true;
                }
            }

            sourceEntityId = 0;
            return false;
        }

        private static void BuildJumpRequests(
            TickPresentationData presentationData,
            ICollection<EnemyAudioRequest> requests)
        {
            var signals = presentationData.EnemyJumpSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                AddRequestIf(signal.EntityId, EnemyAudioCue.Landing, signal.LandedThisTick, requests);
            }
        }

        private static void BuildGlideRequests(
            TickPresentationData presentationData,
            ICollection<EnemyAudioRequest> requests)
        {
            var signals = presentationData.EnemyGlideSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.PhaseElapsedTicks != 0)
                {
                    continue;
                }

                switch (signal.Phase)
                {
                    case EnemyGlidePhase.Windup:
                        AddRequest(signal.EntityId, EnemyAudioCue.Windup, requests);
                        break;
                    case EnemyGlidePhase.Active:
                        AddRequest(signal.EntityId, EnemyAudioCue.Active, requests);
                        break;
                    case EnemyGlidePhase.Recovery:
                        AddRequest(signal.EntityId, EnemyAudioCue.Recover, requests);
                        break;
                }
            }
        }

        private static void BuildChargeRequests(
            TickPresentationData presentationData,
            ICollection<EnemyAudioRequest> requests)
        {
            var signals = presentationData.EnemyChargeSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                AddRequestIf(signal.EntityId, EnemyAudioCue.Active, signal.StartedActiveThisTick, requests);
            }
        }

        private static void BuildProjectileImpactRequests(
            TickPresentationData presentationData,
            ICollection<EnemyAudioRequest> requests)
        {
            var signals = presentationData.ForwardCellImpactSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                AddRequestIf(
                    signal.SourceEnemyId,
                    EnemyAudioCue.ProjectileImpact,
                    signal.SourceEnemyId > 0,
                    requests);
            }
        }

        private static void BuildDeathRequests(
            TickPresentationData presentationData,
            GameplayTimingProfile timingProfile,
            ICollection<EnemyAudioRequest> requests)
        {
            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.ExitCause != TickEntityExitCause.EnemyDeath &&
                    signal.ExitCause != TickEntityExitCause.Killed)
                {
                    continue;
                }

                var delaySeconds = signal.Timing == EntityExitPresentationTiming.AtContactTime
                    ? timingProfile.FlipMotionDurationSeconds * signal.VisualContactNormalizedTime
                    : 0f;
                AddRequest(signal.ExitedEntityId, EnemyAudioCue.Death, requests, delaySeconds);
            }
        }

        private static void BuildStationaryActiveRequests(
            IReadOnlyList<EntityState> finalEntities,
            ISet<int> motionFactEntityIds,
            ICollection<EnemyAudioRequest> requests)
        {
            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (!EntityRolePolicy.IsEnemyUnit(entity) ||
                    entity.entityId <= 0 ||
                    motionFactEntityIds.Contains(entity.entityId) ||
                    HasRequestForOwner(requests, entity.entityId))
                {
                    continue;
                }

                AddRequest(entity.entityId, EnemyAudioCue.StationaryActive, requests);
            }
        }

        private static bool HasRequestForOwner(IEnumerable<EnemyAudioRequest> requests, int ownerEntityId)
        {
            foreach (var request in requests)
            {
                if (request.OwnerEntityId == ownerEntityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddRequestIf(
            int ownerEntityId,
            EnemyAudioCue cue,
            bool shouldEmit,
            ICollection<EnemyAudioRequest> requests)
        {
            if (!shouldEmit)
            {
                return;
            }

            AddRequest(ownerEntityId, cue, requests);
        }

        private static void AddRequest(
            int ownerEntityId,
            EnemyAudioCue cue,
            ICollection<EnemyAudioRequest> requests,
            float delaySeconds = 0f)
        {
            requests.Add(new EnemyAudioRequest(
                ownerEntityId,
                cue,
                new AudioPlaybackContext(
                    ownerEntityId: ownerEntityId,
                    debugTag: EnemyAudioCueCatalog.Format(cue)),
                delaySeconds));
        }
    }
}

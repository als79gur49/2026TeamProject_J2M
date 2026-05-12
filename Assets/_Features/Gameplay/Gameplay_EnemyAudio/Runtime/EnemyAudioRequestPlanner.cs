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
        public IReadOnlyList<EnemyAudioRequest> BuildRequests(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var enemyEntityIds = BuildEnemyEntityIdSet(result.FinalEntities);
            var requests = new List<EnemyAudioRequest>();
            BuildMoveRequests(result.PresentationData, enemyEntityIds, requests);
            BuildActionRequests(result.PresentationData, requests);
            BuildUtilityRequests(result.PresentationData, requests);
            BuildSummonRequests(result.PresentationData, requests);
            BuildJumpRequests(result.PresentationData, requests);
            BuildDeathRequests(result.PresentationData, requests);
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

        private static void BuildMoveRequests(
            TickPresentationData presentationData,
            ISet<int> enemyEntityIds,
            ICollection<EnemyAudioRequest> requests)
        {
            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (motion.MotionKind != TickEntityMotionKind.Move ||
                    !enemyEntityIds.Contains(motion.EntityId))
                {
                    continue;
                }

                AddRequest(motion.EntityId, EnemyAudioCue.Move, requests);
            }
        }

        private static void BuildActionRequests(
            TickPresentationData presentationData,
            ICollection<EnemyAudioRequest> requests)
        {
            var signals = presentationData.EnemyActionSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                AddRequestIf(signal.EntityId, EnemyAudioCue.Act, signal.StartedThisTick, requests);
                AddRequestIf(signal.EntityId, EnemyAudioCue.Plasma, signal.ExecutedThisTick, requests);
            }
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
                    EnemyAudioCue.Act,
                    signal.Phase == EnemyUtilityPresentationPhase.WindupStarted,
                    requests);
                AddRequestIf(
                    signal.EntityId,
                    EnemyAudioCue.GravityField,
                    signal.Kind == EnemyUtilityPresentationKind.LockNearbyBoxes &&
                    signal.Phase == EnemyUtilityPresentationPhase.RecoverStarted,
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
                AddRequest(signals[i].SourceEntityId, EnemyAudioCue.Act, requests);
            }
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

        private static void BuildDeathRequests(
            TickPresentationData presentationData,
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

                AddRequest(signal.ExitedEntityId, EnemyAudioCue.Death, requests);
            }
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
            ICollection<EnemyAudioRequest> requests)
        {
            requests.Add(new EnemyAudioRequest(
                ownerEntityId,
                cue,
                new AudioPlaybackContext(
                    ownerEntityId: ownerEntityId,
                    debugTag: EnemyAudioCueCatalog.Format(cue))));
        }
    }
}

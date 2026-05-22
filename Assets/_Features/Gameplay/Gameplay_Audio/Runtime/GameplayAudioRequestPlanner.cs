using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Audio
{
    public sealed class GameplayAudioRequestPlanner
    {
        public IReadOnlyList<GameplayAudioSemanticId> RequiredSemantics => GameplayAudioSemanticCatalog.RequiredOneShotV1;

        public IReadOnlyList<GameplayAudioRequest> BuildRequests(
            TickResult result,
            GameplayTimingProfile timingProfile = null)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            timingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            var requests = new List<GameplayAudioRequest>();
            BuildDamageRequests(result.PresentationData, requests);
            BuildExitRequests(result.PresentationData, timingProfile, requests);
            return requests;
        }

        private static void BuildDamageRequests(
            TickPresentationData presentationData,
            ICollection<GameplayAudioRequest> requests)
        {
            var playerDamageRequestEntityIds = new HashSet<int>();
            var playerDamageSignals = presentationData.PlayerDamageSignals;
            for (var i = 0; i < playerDamageSignals.Count; i++)
            {
                var signal = playerDamageSignals[i];
                if (!signal.TookDamageThisTick)
                {
                    continue;
                }

                requests.Add(CreateRequest(GameplayAudioSemanticId.PlayerDamage, signal.EntityId));
                playerDamageRequestEntityIds.Add(signal.EntityId);
            }

            var playerDeathSignals = presentationData.PlayerDeathSignals;
            for (var i = 0; i < playerDeathSignals.Count; i++)
            {
                var signal = playerDeathSignals[i];
                if (!signal.DidDieThisTick ||
                    !playerDamageRequestEntityIds.Add(signal.EntityId))
                {
                    continue;
                }

                requests.Add(CreateRequest(GameplayAudioSemanticId.PlayerDamage, signal.EntityId));
            }

            var enemyDamageSignals = presentationData.EnemyDamageSignals;
            for (var i = 0; i < enemyDamageSignals.Count; i++)
            {
                var signal = enemyDamageSignals[i];
                if (!signal.TookDamageThisTick)
                {
                    continue;
                }

                requests.Add(CreateRequest(GameplayAudioSemanticId.EnemyDamage, signal.EntityId));
            }
        }

        private static void BuildExitRequests(
            TickPresentationData presentationData,
            GameplayTimingProfile timingProfile,
            ICollection<GameplayAudioRequest> requests)
        {
            // v1 gameplay audio intentionally excludes locomotion, windup/recovery, UI, and BGM.
            // This planner remains a pure translation from TickResult public presentation facts
            // into gameplay-origin one-shot SFX requests only.
            var exitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < exitSignals.Count; i++)
            {
                var signal = exitSignals[i];
                if (!GameplayAudioSemanticCatalog.TryResolveExitSemantic(signal.ExitCause, out var semanticId))
                {
                    continue;
                }

                var delaySeconds = ResolveEntityExitAudioDelaySeconds(
                    presentationData,
                    signal,
                    semanticId,
                    timingProfile);
                requests.Add(CreateRequest(semanticId, signal.ExitedEntityId, delaySeconds));
            }
        }

        private static float ResolveEntityExitAudioDelaySeconds(
            TickPresentationData presentationData,
            in TickEntityExitPresentationSignal signal,
            GameplayAudioSemanticId semanticId,
            GameplayTimingProfile timingProfile)
        {
            if (semanticId == GameplayAudioSemanticId.EntityExitBoxDestroy &&
                signal.Timing == EntityExitPresentationTiming.AfterEntityMotion)
            {
                return ResolveAfterEntityMotionDelaySeconds(
                    presentationData,
                    signal.ExitedEntityId,
                    timingProfile);
            }

            if (signal.Timing == EntityExitPresentationTiming.AtContactTime)
            {
                return timingProfile.FlipMotionDurationSeconds * signal.VisualContactNormalizedTime;
            }

            return 0f;
        }

        private static float ResolveAfterEntityMotionDelaySeconds(
            TickPresentationData presentationData,
            int entityId,
            GameplayTimingProfile timingProfile)
        {
            var kinematicTracks = presentationData.KinematicMotionTracks;
            for (var i = 0; i < kinematicTracks.Count; i++)
            {
                var track = kinematicTracks[i];
                if (track.EntityId == entityId &&
                    track.TotalTicks > 0)
                {
                    return track.TotalTicks / (float)timingProfile.SimulationTicksPerSecond;
                }
            }

            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (motion.EntityId != entityId)
                {
                    continue;
                }

                return ResolveLegacyMotionDurationSeconds(motion.MotionKind, timingProfile);
            }

            return 0f;
        }

        private static float ResolveLegacyMotionDurationSeconds(
            TickEntityMotionKind motionKind,
            GameplayTimingProfile timingProfile)
        {
            switch (motionKind)
            {
                case TickEntityMotionKind.Move:
                    return timingProfile.MoveMotionDurationSeconds;
                case TickEntityMotionKind.Push:
                    return timingProfile.PushMotionDurationSeconds;
                case TickEntityMotionKind.Flip:
                    return timingProfile.FlipMotionDurationSeconds;
                case TickEntityMotionKind.ProjectileMove:
                    return timingProfile.ProjectileStepIntervalSeconds;
                case TickEntityMotionKind.BoxSlide:
                    return timingProfile.BoxSlideStepIntervalSeconds;
                case TickEntityMotionKind.None:
                default:
                    return 0f;
            }
        }

        private static GameplayAudioRequest CreateRequest(
            GameplayAudioSemanticId semanticId,
            int ownerEntityId,
            float delaySeconds = 0f)
        {
            return new GameplayAudioRequest(
                semanticId,
                ownerEntityId,
                new AudioPlaybackContext(
                    ownerEntityId: ownerEntityId,
                    debugTag: GameplayAudioSemanticCatalog.Format(semanticId)),
                delaySeconds);
        }
    }
}

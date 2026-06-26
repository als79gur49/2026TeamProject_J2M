using System;
using System.Collections.Generic;
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
            var requests = new List<EnemyAudioRequest>();
            var semanticEvents = EnemyAudioSemanticProjector.Project(result);
            for (var i = 0; i < semanticEvents.Count; i++)
            {
                AddRequest(semanticEvents[i], timingProfile, requests);
            }

            return requests;
        }

        private static void AddRequest(
            in EnemyAudioSemanticEvent semanticEvent,
            GameplayTimingProfile timingProfile,
            ICollection<EnemyAudioRequest> requests)
        {
            if (!TryResolveCue(semanticEvent.Cue, out var cue))
            {
                return;
            }

            var delaySeconds = ResolveDelaySeconds(semanticEvent, timingProfile);
            var identity = semanticEvent.ImpactId > 0 ||
                           semanticEvent.ImpactTick > 0 ||
                           semanticEvent.PresentationKey > 0
                ? new EnemyAudioRequestIdentity(
                    true,
                    semanticEvent.OwnerEntityId,
                    semanticEvent.TargetCell,
                    semanticEvent.ImpactTick,
                    semanticEvent.ImpactId,
                    semanticEvent.PresentationKey)
                : default;
            requests.Add(new EnemyAudioRequest(
                semanticEvent.OwnerEntityId,
                cue,
                new AudioPlaybackContext(
                    ownerEntityId: semanticEvent.OwnerEntityId,
                    debugTag: EnemyAudioCueCatalog.Format(cue)),
                delaySeconds,
                identity,
                semanticEvent));
        }

        private static float ResolveDelaySeconds(
            in EnemyAudioSemanticEvent semanticEvent,
            GameplayTimingProfile timingProfile)
        {
            if (semanticEvent.Timing == (int)EntityExitPresentationTiming.AtContactTime)
            {
                return (timingProfile ?? GameplayTimingProfile.CreateDefault()).FlipMotionDurationSeconds *
                       semanticEvent.VisualContactNormalizedTime;
            }

            return 0f;
        }

        private static bool TryResolveCue(EnemyAudioSemanticCue semanticCue, out EnemyAudioCue cue)
        {
            switch (semanticCue)
            {
                case EnemyAudioSemanticCue.Move:
                    cue = EnemyAudioCue.Move;
                    return true;
                case EnemyAudioSemanticCue.Death:
                    cue = EnemyAudioCue.Death;
                    return true;
                case EnemyAudioSemanticCue.Windup:
                    cue = EnemyAudioCue.Windup;
                    return true;
                case EnemyAudioSemanticCue.Landing:
                    cue = EnemyAudioCue.Landing;
                    return true;
                case EnemyAudioSemanticCue.Active:
                    cue = EnemyAudioCue.Active;
                    return true;
                case EnemyAudioSemanticCue.Recover:
                    cue = EnemyAudioCue.Recover;
                    return true;
                case EnemyAudioSemanticCue.ForwardCellImpact:
                    cue = EnemyAudioCue.ForwardCellImpact;
                    return true;
                case EnemyAudioSemanticCue.ChargeActiveLoop:
                    cue = EnemyAudioCue.ChargeActiveLoop;
                    return true;
                case EnemyAudioSemanticCue.StationaryActive:
                    cue = EnemyAudioCue.StationaryActive;
                    return true;
                case EnemyAudioSemanticCue.PassiveContact:
                    cue = EnemyAudioCue.PassiveContact;
                    return true;
                default:
                    cue = EnemyAudioCue.None;
                    return false;
            }
        }

    }
}

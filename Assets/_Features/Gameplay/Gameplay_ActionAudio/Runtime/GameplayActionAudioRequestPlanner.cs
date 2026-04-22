using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.ActionAudio
{
    public sealed class GameplayActionAudioRequestPlanner
    {
        public IReadOnlyList<GameplayActionAudioRequest> BuildRequests(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var requests = new List<GameplayActionAudioRequest>();
            var signals = result.PresentationData.PlayerActionSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (!TryResolveActionKind(signal.ActiveActionKind, out var action))
                {
                    continue;
                }

                AppendRequestsForSignal(signal, action, requests);
            }

            return requests;
        }

        private static void AppendRequestsForSignal(
            in TickPlayerActionPresentationSignal signal,
            GameplayActionKind action,
            ICollection<GameplayActionAudioRequest> requests)
        {
            AppendIf(signal.EntityId, action, GameplayActionAudioMoment.Windup, signal.StartedThisTick, requests);
            AppendIf(signal.EntityId, action, GameplayActionAudioMoment.Execute, signal.ExecutedThisTick, requests);
            AppendIf(signal.EntityId, action, GameplayActionAudioMoment.Contact, ShouldEmitContact(signal, action), requests);
            AppendIf(
                signal.EntityId,
                action,
                GameplayActionAudioMoment.ImpactEnemy,
                signal.ExecutedThisTick && signal.ResolutionKind == TickPlayerActionResolutionKind.Impact,
                requests);
            AppendIf(
                signal.EntityId,
                action,
                GameplayActionAudioMoment.Blocked,
                signal.ExecutedThisTick && signal.ResolutionKind == TickPlayerActionResolutionKind.Blocked,
                requests);
            AppendIf(
                signal.EntityId,
                action,
                GameplayActionAudioMoment.Recovery,
                signal.ExecutedThisTick && signal.IsRecoveryPhase,
                requests);
        }

        private static void AppendIf(
            int ownerEntityId,
            GameplayActionKind action,
            GameplayActionAudioMoment moment,
            bool shouldEmit,
            ICollection<GameplayActionAudioRequest> requests)
        {
            if (!shouldEmit)
            {
                return;
            }

            requests.Add(new GameplayActionAudioRequest(
                ownerEntityId,
                action,
                moment,
                new AudioPlaybackContext(
                    ownerEntityId: ownerEntityId,
                    debugTag: GameplayActionAudioDebugTag.Format(action, moment))));
        }

        private static bool ShouldEmitContact(
            in TickPlayerActionPresentationSignal signal,
            GameplayActionKind action)
        {
            return action switch
            {
                GameplayActionKind.Push => signal.ExecutedThisTick &&
                                           signal.ResolutionKind != TickPlayerActionResolutionKind.Blocked,
                GameplayActionKind.Flip => signal.HasFlipImpactContactTiming,
                _ => false,
            };
        }

        private static bool TryResolveActionKind(PlayerActionKind actionKind, out GameplayActionKind resolved)
        {
            switch (actionKind)
            {
                case PlayerActionKind.Push:
                    resolved = GameplayActionKind.Push;
                    return true;

                case PlayerActionKind.Flip:
                    resolved = GameplayActionKind.Flip;
                    return true;

                default:
                    resolved = default;
                    return false;
            }
        }
    }
}

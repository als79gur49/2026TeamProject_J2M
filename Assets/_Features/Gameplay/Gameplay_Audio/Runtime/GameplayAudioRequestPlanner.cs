using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Audio
{
    public sealed class GameplayAudioRequestPlanner
    {
        public IReadOnlyList<GameplayAudioSemanticId> RequiredSemantics => GameplayAudioSemanticCatalog.RequiredOneShotV1;

        public IReadOnlyList<GameplayAudioRequest> BuildRequests(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var requests = new List<GameplayAudioRequest>();
            BuildDamageRequests(result.PresentationData, requests);
            BuildExitRequests(result.PresentationData, requests);
            return requests;
        }

        private static void BuildDamageRequests(
            TickPresentationData presentationData,
            ICollection<GameplayAudioRequest> requests)
        {
            var playerDamageSignals = presentationData.PlayerDamageSignals;
            for (var i = 0; i < playerDamageSignals.Count; i++)
            {
                var signal = playerDamageSignals[i];
                if (!signal.TookDamageThisTick)
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

                requests.Add(CreateRequest(semanticId, signal.ExitedEntityId));
            }
        }

        private static GameplayAudioRequest CreateRequest(
            GameplayAudioSemanticId semanticId,
            int ownerEntityId)
        {
            return new GameplayAudioRequest(
                semanticId,
                ownerEntityId,
                new AudioPlaybackContext(
                    ownerEntityId: ownerEntityId,
                    debugTag: GameplayAudioSemanticCatalog.Format(semanticId)));
        }
    }
}

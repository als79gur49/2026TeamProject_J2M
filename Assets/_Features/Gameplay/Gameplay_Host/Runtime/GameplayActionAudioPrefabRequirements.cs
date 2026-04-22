using System;
using Game.Feature.Gameplay.ActionAudio;

namespace Game.Feature.Gameplay.Host
{
    internal static class GameplayActionAudioPrefabRequirements
    {
        public static GameplayActionAudioAuthoring GetOptionalValidatedAuthoring(
            GameplayEntityView entityView,
            string ownerDescription)
        {
            if (entityView == null)
            {
                throw new ArgumentNullException(nameof(entityView));
            }

            return GameplayActionAudioAuthoring.GetOptionalValidatedAuthoring(entityView);
        }
    }
}

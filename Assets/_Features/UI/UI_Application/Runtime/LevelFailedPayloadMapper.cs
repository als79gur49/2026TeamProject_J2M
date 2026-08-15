using System;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public static class LevelFailedPayloadMapper
    {
        public static LevelFailedScreenPayload Map(GameplayLevelFailedReadModel readModel)
        {
            if (readModel == null)
            {
                throw new ArgumentNullException(nameof(readModel));
            }

            return new LevelFailedScreenPayload(
                readModel.RestartLevelRequest,
                readModel.TerminalToken);
        }
    }
}

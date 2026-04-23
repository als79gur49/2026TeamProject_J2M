using System;

namespace Game.Feature.UI.Flow
{
    internal interface IUiFlowAudioIntentBoundary
    {
        bool ExecuteOpenForwardBoundary(Func<bool> action);
    }
}

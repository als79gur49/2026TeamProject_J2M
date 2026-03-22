using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class CleanupProcessor
    {
        public void Process(IWorldWriteContext writeContext, int tickIndex)
        {
            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }
        }
    }
}

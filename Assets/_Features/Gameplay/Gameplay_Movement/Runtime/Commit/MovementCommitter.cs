using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Movement.Commit
{
    internal sealed class MovementCommitter
    {
        public void Commit(IWorldWriteContext writeContext, PhaseTransientBuffer transientBuffer)
        {
            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (transientBuffer == null)
            {
                throw new ArgumentNullException(nameof(transientBuffer));
            }
        }
    }
}

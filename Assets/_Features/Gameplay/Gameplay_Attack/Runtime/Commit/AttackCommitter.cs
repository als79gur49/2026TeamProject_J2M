using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Attack.Commit
{
    internal sealed class AttackCommitter
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

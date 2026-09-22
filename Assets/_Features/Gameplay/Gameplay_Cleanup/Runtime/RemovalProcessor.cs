using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Cleanup
{
    internal sealed class RemovalProcessor
    {
        public void Process(
            ReadOnlySpan<int> removalCandidateIds,
            ICleanupCommitContext writeContext,
            List<int> removedEntityIds)
        {
            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (removedEntityIds == null)
            {
                throw new ArgumentNullException(nameof(removedEntityIds));
            }

            removedEntityIds.Clear();
            for (var i = 0; i < removalCandidateIds.Length; i++)
            {
                removedEntityIds.Add(removalCandidateIds[i]);
            }

            for (var i = 0; i < removedEntityIds.Count; i++)
            {
                writeContext.RemoveEntity(removedEntityIds[i]);
            }
        }
    }
}

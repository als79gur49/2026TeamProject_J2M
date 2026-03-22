using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;

namespace Game.Feature.Gameplay.Movement.Commit
{
    internal sealed class MovementCommitter
    {
        public void Commit(
            IWorldWriteContext writeContext,
            PhaseTransientBuffer transientBuffer,
            IReadOnlyList<ActionGroup> selectedGroups,
            List<string> commitEvents)
        {
            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            if (transientBuffer == null)
            {
                throw new ArgumentNullException(nameof(transientBuffer));
            }

            if (selectedGroups == null)
            {
                throw new ArgumentNullException(nameof(selectedGroups));
            }

            if (commitEvents == null)
            {
                throw new ArgumentNullException(nameof(commitEvents));
            }

            commitEvents.Clear();

            for (var groupIndex = 0; groupIndex < selectedGroups.Count; groupIndex++)
            {
                var group = selectedGroups[groupIndex];

                for (var moveIndex = 0; moveIndex < group.Moves.Count; moveIndex++)
                {
                    var move = group.Moves[moveIndex];
                    writeContext.SetFacing(move.EntityId, move.Facing);
                    writeContext.MoveEntity(move.EntityId, move.Destination);
                    commitEvents.Add(
                        $"MoveCommitted|G={group.GroupId}|I={group.IntentId}|E={move.EntityId}|To=({move.Destination.x},{move.Destination.y})|Facing={move.Facing}");
                }
            }
        }
    }
}

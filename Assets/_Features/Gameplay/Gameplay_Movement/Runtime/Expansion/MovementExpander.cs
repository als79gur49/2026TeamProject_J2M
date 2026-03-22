using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Movement.Actions;
using Game.Feature.Gameplay.Movement.Groups;
using Game.Feature.Gameplay.Movement.Intents;
using UnityEngine;

namespace Game.Feature.Gameplay.Movement.Expansion
{
    internal sealed class MovementExpander
    {
        public void Expand(
            WorldSnapshot snapshot,
            IReadOnlyList<MoveIntent> sortedIntents,
            List<ActionGroup> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (sortedIntents == null)
            {
                throw new ArgumentNullException(nameof(sortedIntents));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();

            for (var i = 0; i < sortedIntents.Count; i++)
            {
                var intent = sortedIntents[i];

                if (!snapshot.TryGetEntity(intent.SourceId, out var entity))
                {
                    continue;
                }

                ValidateSingleStepMove(entity.position, intent.Destination, intent.SourceId);

                if (snapshot.IsBlockedForUnit(intent.Destination))
                {
                    continue;
                }

                var actionGroup = new ActionGroup(
                    intent.IntentId,
                    intent.SourceId,
                    intent.Priority,
                    ActionGroupKind.Move);
                actionGroup.Moves.Add(
                    new MoveAction(
                        intent.SourceId,
                        intent.Destination,
                        ResolveFacing(entity.position, intent.Destination)));
                buffer.Add(actionGroup);
            }
        }

        private static Direction ResolveFacing(Vector2Int source, Vector2Int destination)
        {
            var delta = destination - source;

            if (delta == Vector2Int.up)
            {
                return Direction.Up;
            }

            if (delta == Vector2Int.right)
            {
                return Direction.Right;
            }

            if (delta == Vector2Int.down)
            {
                return Direction.Down;
            }

            if (delta == Vector2Int.left)
            {
                return Direction.Left;
            }

            throw new InvalidOperationException("Stage2 movement only supports orthogonal single-cell moves.");
        }

        private static void ValidateSingleStepMove(Vector2Int source, Vector2Int destination, int sourceId)
        {
            var delta = destination - source;
            if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1)
            {
                throw new InvalidOperationException(
                    $"Entity {sourceId} emitted an invalid Stage2 MoveIntent. Only orthogonal single-cell moves are allowed.");
            }
        }
    }
}

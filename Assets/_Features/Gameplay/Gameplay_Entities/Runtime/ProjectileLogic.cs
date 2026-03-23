using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    internal sealed class ProjectileLogic : IEntityLogic
    {
        private const int DefaultMovementPriority = 0;

        private readonly int _sourceId;

        public ProjectileLogic(int sourceId)
        {
            if (sourceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceId), "Projectile logic requires a positive source ID.");
            }

            _sourceId = sourceId;
        }

        public void CollectMovementIntents(
            WorldSnapshot snapshot,
            in TickInput input,
            List<RawMovementIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!snapshot.TryGetEntity(_sourceId, out var source))
            {
                return;
            }

            if (source.type != EntityType.Projectile || source.hp <= 0 || source.markedForDeath)
            {
                return;
            }

            var delta = ResolveDelta(source.facing);
            if (!delta.HasValue)
            {
                return;
            }

            buffer.Add(
                new RawMovementIntent(
                    source.entityId,
                    DefaultMovementPriority,
                    source.position + delta.Value));
        }

        public void CollectAttackIntents(WorldSnapshot snapshot, List<RawAttackIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
        }

        private static Vector2Int? ResolveDelta(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Vector2Int.up;

                case Direction.Right:
                    return Vector2Int.right;

                case Direction.Down:
                    return Vector2Int.down;

                case Direction.Left:
                    return Vector2Int.left;

                default:
                    return null;
            }
        }
    }
}

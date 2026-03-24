using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    public sealed class PlayerLogic : IEntityLogic, IEntityLogicSourceBinding
    {
        private const int DefaultMovementPriority = 100;

        private readonly int _entityId;

        public PlayerLogic(int entityId)
        {
            if (entityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(entityId), "Player logic requires a positive entity ID.");
            }

            _entityId = entityId;
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

            if (!snapshot.TryGetEntity(_entityId, out var entity))
            {
                return;
            }

            if (entity.hp <= 0 || entity.markedForDeath)
            {
                return;
            }

            var commandKind = ResolveCommandKind(input.PlayerCommand.PrimaryKind);
            if (!commandKind.HasValue)
            {
                return;
            }

            if (!TryResolveDelta(input.PlayerCommand.Direction, out var delta))
            {
                return;
            }

            buffer.Add(
                new RawMovementIntent(
                    entity.entityId,
                    DefaultMovementPriority,
                    entity.position + delta,
                    commandKind.Value));
        }

        public void CollectAttackIntents(
            WorldSnapshot snapshot,
            List<RawAttackIntent> buffer)
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

        public bool ControlsEntity(int entityId, TickPhase phase)
        {
            return phase == TickPhase.Movement && entityId == _entityId;
        }

        private static MovementCommandKind? ResolveCommandKind(PlayerPrimaryCommandKind primaryKind)
        {
            switch (primaryKind)
            {
                case PlayerPrimaryCommandKind.Move:
                    return MovementCommandKind.Move;

                case PlayerPrimaryCommandKind.InteractSlide:
                    return MovementCommandKind.InteractSlide;

                case PlayerPrimaryCommandKind.InteractFlip:
                    return MovementCommandKind.InteractFlip;

                default:
                    return null;
            }
        }

        private static bool TryResolveDelta(Direction direction, out Vector2Int delta)
        {
            switch (direction)
            {
                case Direction.Up:
                    delta = Vector2Int.up;
                    return true;

                case Direction.Right:
                    delta = Vector2Int.right;
                    return true;

                case Direction.Down:
                    delta = Vector2Int.down;
                    return true;

                case Direction.Left:
                    delta = Vector2Int.left;
                    return true;

                default:
                    delta = Vector2Int.zero;
                    return false;
            }
        }
    }
}

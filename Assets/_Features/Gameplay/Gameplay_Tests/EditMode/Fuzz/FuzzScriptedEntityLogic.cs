using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Tests.Replay;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Fuzz
{
    internal sealed class FuzzScriptedEntityLogic : IEntityLogic, IReplayTickAwareEntityLogic, IEntityLogicSourceBinding
    {
        private readonly FuzzEntityScript _script;
        private int _currentTickIndex;

        public FuzzScriptedEntityLogic(FuzzEntityScript script)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
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

            if (!snapshot.TryGetEntity(_script.EntityId, out var source) || source.hp <= 0 || source.markedForDeath)
            {
                return;
            }

            if (!TryGetMovementCommand(input.TickIndex, out var movementCommand))
            {
                return;
            }

            buffer.Add(
                new RawMovementIntent(
                    _script.EntityId,
                    movementCommand.Priority,
                    source.position + ResolveDelta(movementCommand.Direction)));
        }

        public void CollectAttackIntents(WorldSnapshot snapshot, in TickInput input, List<RawAttackIntent> buffer)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (_currentTickIndex <= 0)
            {
                return;
            }

            if (!snapshot.TryGetEntity(_script.EntityId, out var source) || source.hp <= 0 || source.markedForDeath)
            {
                return;
            }

            if (!TryGetAttackCommand(_currentTickIndex, out var attackCommand))
            {
                return;
            }

            buffer.Add(new RawAttackIntent(_script.EntityId, attackCommand.Priority, attackCommand.TargetId));
        }

        public void SetReplayTickIndex(int tickIndex)
        {
            _currentTickIndex = tickIndex;
        }

        public bool ControlsEntity(int entityId, TickPhase phase)
        {
            return phase == TickPhase.Movement &&
                _script.MovementCommands.Count > 0 &&
                _script.EntityId == entityId;
        }

        private bool TryGetMovementCommand(int tickIndex, out FuzzMovementCommand command)
        {
            return TryGetMovementCommand(_script.MovementCommands, tickIndex, out command);
        }

        private bool TryGetAttackCommand(int tickIndex, out FuzzAttackCommand command)
        {
            for (var i = 0; i < _script.AttackCommands.Count; i++)
            {
                var candidate = _script.AttackCommands[i];
                if (candidate.TickIndex == tickIndex)
                {
                    command = candidate;
                    return true;
                }

                if (candidate.TickIndex > tickIndex)
                {
                    break;
                }
            }

            command = default;
            return false;
        }

        private static bool TryGetMovementCommand(
            IReadOnlyList<FuzzMovementCommand> movementCommands,
            int tickIndex,
            out FuzzMovementCommand command)
        {
            for (var i = 0; i < movementCommands.Count; i++)
            {
                var candidate = movementCommands[i];
                if (candidate.TickIndex == tickIndex)
                {
                    command = candidate;
                    return true;
                }

                if (candidate.TickIndex > tickIndex)
                {
                    break;
                }
            }

            command = default;
            return false;
        }

        private static Vector2Int ResolveDelta(Direction direction)
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
                    throw new InvalidOperationException("Fuzz movement commands require an orthogonal direction.");
            }
        }
    }
}

using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.PlayerControl
{
    public struct PlayerControlState
    {
        public int moveCooldownTicks;
        public int pushContactTicks;
        public int pushTargetEntityId;
        public Direction pushDirection;
    }

    internal readonly struct PlayerControlSnapshotEntry
    {
        public PlayerControlSnapshotEntry(int entityId, PlayerControlState state)
        {
            EntityId = entityId;
            State = state;
        }

        public int EntityId { get; }

        public PlayerControlState State { get; }
    }

    internal readonly struct PlayerPushContact
    {
        public PlayerPushContact(int targetEntityId, Direction direction)
        {
            TargetEntityId = targetEntityId;
            Direction = direction;
        }

        public int TargetEntityId { get; }

        public Direction Direction { get; }
    }

    internal static class PlayerControlQueries
    {
        public static PlayerControlState ResetContact(in PlayerControlState state)
        {
            var updatedState = state;
            updatedState.pushContactTicks = 0;
            updatedState.pushTargetEntityId = 0;
            updatedState.pushDirection = Direction.None;
            return updatedState;
        }

        public static PlayerControlState ConsumeMoveCooldown(
            in PlayerControlState state,
            int moveCooldownTicks)
        {
            var updatedState = ResetContact(state);
            updatedState.moveCooldownTicks = Mathf.Max(0, moveCooldownTicks);
            return updatedState;
        }

        public static bool TryResolvePushContact(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection,
            out PlayerPushContact contact)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryResolveDelta(inputDirection, out var delta))
            {
                contact = default;
                return false;
            }

            if (!TryResolveTraversalStep(snapshot, player, delta, out var targetCell, out var movementTopology))
            {
                contact = default;
                return false;
            }

            if (!snapshot.TryGetUnitAt(movementTopology, targetCell, out var target) ||
                target.type != EntityType.Box ||
                !HasBoxCapability(target, BoxCapabilities.Push) ||
                !snapshot.Topology.IsFaceActive(target.position.face))
            {
                contact = default;
                return false;
            }

            if (snapshot.TryResolveNextSurfaceBoxSlideStep(
                    snapshot.Topology,
                    target.position,
                    delta,
                    out _,
                    out _) ||
                HasBoxCapability(target, BoxCapabilities.Destroy))
            {
                contact = new PlayerPushContact(target.entityId, inputDirection);
                return true;
            }

            contact = default;
            return false;
        }

        private static bool TryResolveTraversalStep(
            WorldSnapshot snapshot,
            in EntityState source,
            Vector2Int delta,
            out SurfaceCell destinationCell,
            out CubeTopologyState movementTopology)
        {
            if (!snapshot.Topology.IsFaceActive(source.position.face))
            {
                destinationCell = default;
                movementTopology = snapshot.Topology;
                return false;
            }

            if (!snapshot.TryResolvePlayerStep(
                    source.position,
                    delta,
                    out destinationCell,
                    out var rotationKind,
                    out var updatedTopology))
            {
                destinationCell = source.position + delta;
                movementTopology = snapshot.Topology;
                return false;
            }

            movementTopology = rotationKind == CubeRotationKind.None
                ? snapshot.Topology
                : updatedTopology;
            return true;
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

        private static bool HasBoxCapability(EntityState entity, BoxCapabilities capability)
        {
            return entity.type == EntityType.Box && (entity.boxCapabilities & capability) == capability;
        }
    }
}

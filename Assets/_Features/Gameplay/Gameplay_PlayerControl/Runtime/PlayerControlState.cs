using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.PlayerControl
{
    public enum PlayerActionKind
    {
        None = 0,
        Push = 1,
        Flip = 2,
    }

    public struct PlayerActionRuntimeState
    {
        public PlayerActionKind kind;
        public int sequence;
        public Direction direction;
        public int targetEntityId;
        public int startTick;
        public int executeTick;
        public int recoveryEndTick;
        public bool executionAttempted;

        public bool IsActive => kind != PlayerActionKind.None;
    }

    public struct PlayerControlState
    {
        public int moveCooldownTicks;
        public int nextMoveAllowedTick;
        public int pushContactTicks;
        public int pushTargetEntityId;
        public Direction pushDirection;
        public int actionSequenceCounter;
        public PlayerActionRuntimeState activeAction;
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

    internal readonly struct PlayerActionTarget
    {
        public PlayerActionTarget(int targetEntityId, Direction direction)
        {
            TargetEntityId = targetEntityId;
            Direction = direction;
        }

        public int TargetEntityId { get; }

        public Direction Direction { get; }
    }

    public readonly struct PlayerActionTransition
    {
        public PlayerActionTransition(
            int entityId,
            in PlayerActionRuntimeState previousAction,
            in PlayerActionRuntimeState currentAction)
        {
            EntityId = entityId;
            PreviousKind = previousAction.kind;
            CurrentKind = currentAction.kind;
            PreviousSequence = previousAction.sequence;
            CurrentSequence = currentAction.sequence;
            StartedThisTick = currentAction.kind != PlayerActionKind.None &&
                              (previousAction.kind != currentAction.kind ||
                               previousAction.sequence != currentAction.sequence);
            CompletedThisTick = previousAction.kind != PlayerActionKind.None &&
                                currentAction.kind == PlayerActionKind.None &&
                                previousAction.executionAttempted;
            CanceledThisTick = previousAction.kind != PlayerActionKind.None &&
                               currentAction.kind == PlayerActionKind.None &&
                               !previousAction.executionAttempted;
        }

        public int EntityId { get; }

        public PlayerActionKind PreviousKind { get; }

        public PlayerActionKind CurrentKind { get; }

        public int PreviousSequence { get; }

        public int CurrentSequence { get; }

        public bool StartedThisTick { get; }

        public bool CompletedThisTick { get; }

        public bool CanceledThisTick { get; }
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

        public static PlayerControlState StartAction(
            in PlayerControlState state,
            PlayerActionKind kind,
            Direction direction,
            int targetEntityId,
            int startTick,
            int windupTicks,
            int recoveryTicks)
        {
            if (windupTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windupTicks), "Action wind-up ticks must be zero or greater.");
            }

            if (recoveryTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(recoveryTicks), "Action recovery ticks must be zero or greater.");
            }

            var updatedState = ResetContact(state);
            updatedState.actionSequenceCounter = Mathf.Max(1, updatedState.actionSequenceCounter + 1);
            updatedState.activeAction = new PlayerActionRuntimeState
            {
                kind = kind,
                sequence = updatedState.actionSequenceCounter,
                direction = direction,
                targetEntityId = targetEntityId,
                startTick = startTick,
                executeTick = startTick + windupTicks,
                recoveryEndTick = startTick + windupTicks + recoveryTicks,
                executionAttempted = windupTicks == 0,
            };

            return updatedState;
        }

        public static PlayerControlState AdvanceActiveAction(
            in PlayerControlState state,
            int tickIndex)
        {
            if (!state.activeAction.IsActive)
            {
                return state;
            }

            var updatedState = ResetContact(state);

            if (tickIndex > updatedState.activeAction.recoveryEndTick)
            {
                updatedState.activeAction = default;
                return updatedState;
            }

            if (!updatedState.activeAction.executionAttempted &&
                tickIndex >= updatedState.activeAction.executeTick)
            {
                updatedState.activeAction.executionAttempted = true;
            }

            return updatedState;
        }

        public static PlayerControlState ConsumeMoveCooldown(
            in PlayerControlState state,
            int moveCooldownTicks,
            int tickIndex)
        {
            var updatedState = ResetContact(state);
            updatedState.moveCooldownTicks = Mathf.Max(0, moveCooldownTicks);
            updatedState.nextMoveAllowedTick = updatedState.moveCooldownTicks > 0
                ? tickIndex + updatedState.moveCooldownTicks + 1
                : 0;
            return updatedState;
        }

        public static bool IsMoveOnCooldown(
            in PlayerControlState state,
            int tickIndex)
        {
            return state.moveCooldownTicks > 0 ||
                   (state.nextMoveAllowedTick > 0 && tickIndex < state.nextMoveAllowedTick);
        }

        public static bool TryResolvePushContact(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection,
            out PlayerActionTarget contact)
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

            if (!TryResolvePushBoxContact(snapshot, movementTopology, targetCell, out var target))
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
                contact = new PlayerActionTarget(target.entityId, inputDirection);
                return true;
            }

            if (!snapshot.TryResolveNextSurfaceBoxSlideStep(
                    snapshot.Topology,
                    target.position,
                    delta,
                    out _,
                    out var stopper) &&
                snapshot.TryPickHostileUnitImpactTargetAt(stopper.Cell, player.teamId, out _))
            {
                contact = new PlayerActionTarget(target.entityId, inputDirection);
                return true;
            }

            contact = default;
            return false;
        }

        public static bool ShouldPreserveBufferedPushContact(
            WorldSnapshot snapshot,
            in EntityState player,
            in PlayerControlState state,
            Direction inputDirection)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (state.pushContactTicks <= 0 ||
                state.pushTargetEntityId <= 0 ||
                state.pushDirection != inputDirection)
            {
                return false;
            }

            return TryResolvePushContact(snapshot, player, inputDirection, out var contact) &&
                   contact.TargetEntityId == state.pushTargetEntityId &&
                   contact.Direction == state.pushDirection;
        }

        public static bool TryResolveFlipTarget(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection,
            out PlayerActionTarget target)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryResolveDelta(inputDirection, out var delta) ||
                !snapshot.TryResolveLocalFlipCells(player.position, delta, out var targetCell, out var landingCell))
            {
                target = default;
                return false;
            }

            if (!TryResolveFlippableBoxTarget(snapshot, targetCell, landingCell, out var entity))
            {
                target = default;
                return false;
            }

            target = new PlayerActionTarget(entity.entityId, inputDirection);
            return true;
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

        private static bool TryResolvePushBoxContact(
            WorldSnapshot snapshot,
            CubeTopologyState movementTopology,
            SurfaceCell targetCell,
            out EntityState target)
        {
            if (!snapshot.TryGetBoxAt(movementTopology, targetCell, out target))
            {
                return false;
            }

            return HasBoxCapability(target, BoxCapabilities.Push) &&
                   snapshot.Topology.IsFaceActive(target.position.face);
        }

        private static bool TryResolveFlippableBoxTarget(
            WorldSnapshot snapshot,
            SurfaceCell targetCell,
            SurfaceCell landingCell,
            out EntityState entity)
        {
            if (!snapshot.TryGetBoxAt(targetCell, out entity) ||
                !HasBoxCapability(entity, BoxCapabilities.Flip))
            {
                return false;
            }

            return true;
        }

        public static bool CanPendingActionStillExecute(
            WorldSnapshot snapshot,
            in EntityState player,
            in PlayerActionRuntimeState action)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!action.IsActive ||
                !TryResolveDelta(action.direction, out var delta))
            {
                return false;
            }

            switch (action.kind)
            {
                case PlayerActionKind.Push:
                    return CanPendingPushStillExecute(snapshot, player, action.targetEntityId, delta);

                case PlayerActionKind.Flip:
                    return CanPendingFlipStillExecute(snapshot, player, action.targetEntityId, delta);

                default:
                    return false;
            }
        }

        private static bool CanPendingPushStillExecute(
            WorldSnapshot snapshot,
            in EntityState player,
            int targetEntityId,
            Vector2Int delta)
        {
            if (!TryResolveTraversalStep(snapshot, player, delta, out var targetCell, out var movementTopology) ||
                !snapshot.TryGetBoxAt(movementTopology, targetCell, out var target))
            {
                return false;
            }

            return target.entityId == targetEntityId &&
                   HasBoxCapability(target, BoxCapabilities.Push);
        }

        private static bool CanPendingFlipStillExecute(
            WorldSnapshot snapshot,
            in EntityState player,
            int targetEntityId,
            Vector2Int delta)
        {
            if (!snapshot.TryResolveLocalFlipCells(player.position, delta, out var targetCell, out _) ||
                !snapshot.TryGetBoxAt(targetCell, out var target))
            {
                return false;
            }

            return target.entityId == targetEntityId &&
                   HasBoxCapability(target, BoxCapabilities.Flip);
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

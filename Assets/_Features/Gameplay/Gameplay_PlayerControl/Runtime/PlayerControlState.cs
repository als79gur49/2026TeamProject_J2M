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

    public enum PlayerQueuedFree2DActionKind
    {
        None = 0,
        Push = 1,
        Flip = 2,
    }

    public struct PlayerQueuedFree2DActionState
    {
        public PlayerQueuedFree2DActionKind kind;
        public Direction direction;
        public int requestedTick;

        public bool IsQueued => kind != PlayerQueuedFree2DActionKind.None;
    }

    public struct PlayerControlState
    {
        public int moveCooldownTicks;
        public int nextMoveAllowedTick;
        public int actionSequenceCounter;
        public PlayerActionRuntimeState activeAction;
        public Direction queuedKinematicTurnDirection;
        public PlayerQueuedFree2DActionState queuedFree2DAction;
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

            var updatedState = state;
            updatedState.actionSequenceCounter = Mathf.Max(1, updatedState.actionSequenceCounter + 1);
            updatedState.queuedFree2DAction = default;
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

            var updatedState = state;

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
            var updatedState = state;
            updatedState.moveCooldownTicks = Mathf.Max(0, moveCooldownTicks);
            updatedState.nextMoveAllowedTick = updatedState.moveCooldownTicks > 0
                ? tickIndex + updatedState.moveCooldownTicks + 1
                : 0;
            return updatedState;
        }

        public static PlayerControlState QueueKinematicTurn(
            in PlayerControlState state,
            Direction direction)
        {
            var updatedState = state;
            updatedState.queuedKinematicTurnDirection = IsCardinalDirection(direction)
                ? direction
                : Direction.None;
            return updatedState;
        }

        public static PlayerControlState ClearQueuedKinematicTurn(in PlayerControlState state)
        {
            var updatedState = state;
            updatedState.queuedKinematicTurnDirection = Direction.None;
            return updatedState;
        }

        public static PlayerControlState QueueFree2DAction(
            in PlayerControlState state,
            PlayerQueuedFree2DActionKind kind,
            Direction direction,
            int requestedTick)
        {
            var updatedState = state;
            updatedState.queuedFree2DAction = IsQueueableFree2DAction(kind, direction)
                ? new PlayerQueuedFree2DActionState
                {
                    kind = kind,
                    direction = direction,
                    requestedTick = Math.Max(0, requestedTick),
                }
                : default;
            return updatedState;
        }

        public static PlayerControlState ClearQueuedFree2DAction(in PlayerControlState state)
        {
            var updatedState = state;
            updatedState.queuedFree2DAction = default;
            return updatedState;
        }

        public static bool IsMoveOnCooldown(
            in PlayerControlState state,
            int tickIndex)
        {
            return state.moveCooldownTicks > 0 ||
                   (state.nextMoveAllowedTick > 0 && tickIndex < state.nextMoveAllowedTick);
        }

        public static bool HasQueuedKinematicTurn(in PlayerControlState state)
        {
            return IsCardinalDirection(state.queuedKinematicTurnDirection);
        }

        public static bool HasQueuedFree2DAction(in PlayerControlState state)
        {
            return IsQueueableFree2DAction(state.queuedFree2DAction.kind, state.queuedFree2DAction.direction);
        }

        public static PlayerQueuedFree2DActionKind ToQueuedFree2DActionKind(PlayerActionKind actionKind)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => PlayerQueuedFree2DActionKind.Push,
                PlayerActionKind.Flip => PlayerQueuedFree2DActionKind.Flip,
                _ => PlayerQueuedFree2DActionKind.None,
            };
        }

        private static bool IsCardinalDirection(Direction direction)
        {
            return direction == Direction.Up ||
                   direction == Direction.Right ||
                   direction == Direction.Down ||
                   direction == Direction.Left;
        }

        private static bool IsQueueableFree2DAction(PlayerQueuedFree2DActionKind kind, Direction direction)
        {
            return (kind == PlayerQueuedFree2DActionKind.Push ||
                    kind == PlayerQueuedFree2DActionKind.Flip) &&
                   IsCardinalDirection(direction);
        }

        public static bool TryResolvePushContact(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection,
            out PlayerActionTarget contact)
        {
            return TryResolvePushContact(snapshot, player, inputDirection, tickIndex: 0, checkLocks: false, out contact);
        }

        public static bool TryResolvePushContact(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection,
            int tickIndex,
            out PlayerActionTarget contact)
        {
            return TryResolvePushContact(snapshot, player, inputDirection, tickIndex, checkLocks: true, out contact);
        }

        private static bool TryResolvePushContact(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection,
            int tickIndex,
            bool checkLocks,
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

            if (!UnitSpatialQuery.TryResolveSettledProbeCell(snapshot, player.entityId, inputDirection, out _))
            {
                contact = default;
                return false;
            }

            return TryResolvePushContactAtAnchor(
                snapshot,
                player,
                player.position,
                inputDirection,
                delta,
                tickIndex,
                checkLocks,
                out contact);
        }

        public static bool HasFree2DActionAssistCandidate(
            WorldSnapshot snapshot,
            in EntityState player,
            SurfaceCell currentAnchor,
            PlayerQueuedFree2DActionKind actionKind,
            Direction actionDirection)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryResolveDelta(actionDirection, out var delta))
            {
                return false;
            }

            switch (actionKind)
            {
                case PlayerQueuedFree2DActionKind.Push:
                    return TryResolvePushContactAtAnchor(
                        snapshot,
                        player,
                        currentAnchor,
                        actionDirection,
                        delta,
                        tickIndex: 0,
                        checkLocks: false,
                        out _);

                case PlayerQueuedFree2DActionKind.Flip:
                    return TryResolveFlipTargetAtAnchor(
                        snapshot,
                        player,
                        currentAnchor,
                        actionDirection,
                        delta,
                        tickIndex: 0,
                        checkLocks: false,
                        out _);

                default:
                    return false;
            }
        }

        public static bool TryResolveAdjacentPushTarget(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection,
            out PlayerActionTarget target)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryResolveDelta(inputDirection, out var delta))
            {
                target = default;
                return false;
            }

            if (!UnitSpatialQuery.TryResolveSettledProbeCell(snapshot, player.entityId, inputDirection, out _))
            {
                target = default;
                return false;
            }

            if (!TryResolveTraversalStep(snapshot, player, delta, out var targetCell, out var movementTopology))
            {
                target = default;
                return false;
            }

            if (!TryResolvePushBoxContact(snapshot, movementTopology, targetCell, out var entity))
            {
                target = default;
                return false;
            }

            target = new PlayerActionTarget(entity.entityId, inputDirection);
            return true;
        }

        public static bool ShouldSuppressOrdinaryMoveForPushTarget(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryResolveDelta(inputDirection, out var delta))
            {
                return false;
            }

            var targetCell = player.position + delta;
            if (!snapshot.TryGetSolidSemanticAt(targetCell, out var targetSemantic) ||
                targetSemantic.Kind != SolidKind.Box)
            {
                return false;
            }

            var target = targetSemantic.Entity;
            return HasBoxCapability(target, BoxCapabilities.Push) &&
                   !HasBoxCapability(target, BoxCapabilities.Item);
        }

        public static bool TryResolveFlipTarget(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection,
            out PlayerActionTarget target)
        {
            return TryResolveFlipTarget(snapshot, player, inputDirection, tickIndex: 0, checkLocks: false, out target);
        }

        public static bool TryResolveFlipTarget(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection,
            int tickIndex,
            out PlayerActionTarget target)
        {
            return TryResolveFlipTarget(snapshot, player, inputDirection, tickIndex, checkLocks: true, out target);
        }

        private static bool TryResolveFlipTarget(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction inputDirection,
            int tickIndex,
            bool checkLocks,
            out PlayerActionTarget target)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryResolveDelta(inputDirection, out var delta) ||
                !UnitSpatialQuery.TryResolveSettledProbeCell(snapshot, player.entityId, inputDirection, out _))
            {
                target = default;
                return false;
            }

            return TryResolveFlipTargetAtAnchor(
                snapshot,
                player,
                player.position,
                inputDirection,
                delta,
                tickIndex,
                checkLocks,
                out target);
        }

        private static bool TryResolvePushContactAtAnchor(
            WorldSnapshot snapshot,
            in EntityState player,
            SurfaceCell currentAnchor,
            Direction inputDirection,
            Vector2Int delta,
            int tickIndex,
            bool checkLocks,
            out PlayerActionTarget contact)
        {
            var anchoredPlayer = player;
            anchoredPlayer.position = currentAnchor;
            if (!TryResolveTraversalStep(snapshot, anchoredPlayer, delta, out var targetCell, out var movementTopology))
            {
                contact = default;
                return false;
            }

            if (!TryResolvePushBoxContact(snapshot, movementTopology, targetCell, out var target))
            {
                contact = default;
                return false;
            }

            if (checkLocks &&
                TryGetActiveBoxInteractionLock(snapshot, target.entityId, tickIndex, blocksPush: true, out _))
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
                snapshot.TryPickHostileUnitImpactTargetAtForBoxSlide(stopper.Cell, player.teamId, out _))
            {
                contact = new PlayerActionTarget(target.entityId, inputDirection);
                return true;
            }

            contact = default;
            return false;
        }

        private static bool TryResolveFlipTargetAtAnchor(
            WorldSnapshot snapshot,
            in EntityState player,
            SurfaceCell currentAnchor,
            Direction inputDirection,
            Vector2Int delta,
            int tickIndex,
            bool checkLocks,
            out PlayerActionTarget target)
        {
            var anchoredPlayer = player;
            anchoredPlayer.position = currentAnchor;
            if (!snapshot.TryResolveLocalFlipCells(anchoredPlayer.position, delta, out var targetCell, out var landingCell) ||
                !TryResolveFlippableBoxTarget(snapshot, targetCell, landingCell, out var entity))
            {
                target = default;
                return false;
            }

            if (checkLocks &&
                TryGetActiveBoxInteractionLock(snapshot, entity.entityId, tickIndex, blocksPush: false, out _))
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
            if (!snapshot.TryGetSolidSemanticAt(movementTopology, targetCell, out var targetSemantic) ||
                targetSemantic.Kind != SolidKind.Box)
            {
                target = default;
                return false;
            }

            target = targetSemantic.Entity;
            return HasBoxCapability(target, BoxCapabilities.Push) &&
                   snapshot.Topology.IsFaceActive(target.position.face);
        }

        private static bool TryResolveFlippableBoxTarget(
            WorldSnapshot snapshot,
            SurfaceCell targetCell,
            SurfaceCell landingCell,
            out EntityState entity)
        {
            if (!snapshot.TryGetSolidSemanticAt(targetCell, out var targetSemantic) ||
                targetSemantic.Kind != SolidKind.Box ||
                !HasBoxCapability(targetSemantic.Entity, BoxCapabilities.Flip))
            {
                entity = default;
                return false;
            }

            entity = targetSemantic.Entity;
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
                !TryResolveDelta(action.direction, out var delta) ||
                !UnitSpatialQuery.IsSettledAtAnchor(snapshot, player.entityId))
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
                !snapshot.TryGetSolidSemanticAt(movementTopology, targetCell, out var targetSemantic) ||
                targetSemantic.Kind != SolidKind.Box)
            {
                return false;
            }

            var target = targetSemantic.Entity;
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
                !snapshot.TryGetSolidSemanticAt(targetCell, out var targetSemantic) ||
                targetSemantic.Kind != SolidKind.Box)
            {
                return false;
            }

            var target = targetSemantic.Entity;
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

        private static bool TryGetActiveBoxInteractionLock(
            WorldSnapshot snapshot,
            int boxEntityId,
            int tickIndex,
            bool blocksPush,
            out BoxInteractionLockState lockState)
        {
            if (!snapshot.TryGetActiveBoxInteractionLockState(boxEntityId, tickIndex, out lockState))
            {
                return false;
            }

            return blocksPush
                ? lockState.BlocksPush
                : lockState.BlocksFlip;
        }
    }

    public static class PlayerActionPreviewQueries
    {
        public static bool HasExplicitPushCandidate(
            WorldSnapshot snapshot,
            in EntityState player,
            Direction direction)
        {
            return snapshot != null &&
                   PlayerControlQueries.TryResolvePushContact(snapshot, player, direction, out _);
        }
    }
}

using System;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}

namespace Game.Feature.Gameplay.BoardState
{
    public enum PoseMutationSource
    {
        None = 0,
        MovementProbe = 1,
        MovementIntent = 2,
        MovementCommit = 3,
        CombatActionStart = 4,
        CombatActionHold = 5,
        CombatActionRelease = 6,
        MovementSkillStart = 7,
        MovementSkillActive = 8,
        ExplicitRotateAction = 9,
        KinematicHold = 10,
        KinematicRelease = 11,
        KinematicSettle = 12,
        SpawnOrRespawn = 13,
        Cleanup = 14,
        PresentationOnly = 15,
        KinematicLocomotion = 16,
        KinematicMovementSkill = 17,
    }

    public enum PoseMutationKind
    {
        None = 0,
        PositionOnly = 1,
        FacingOnly = 2,
        PositionAndFacing = 3,
        KinematicOnly = 4,
        PositionFacingAndKinematic = 5,
        KinematicAndFacing = 6,
    }

    public enum KinematicDirectionKind
    {
        None = 0,
        AnchorDelta = 1,
        LocalOffsetDelta = 2,
        Velocity = 3,
        ExplicitSkillDirection = 4,
        ExplicitActionDirection = 5,
    }

    public enum KinematicFacingPolicy
    {
        PreserveFacing = 0,
        MatchKinematicDirection = 1,
        ExplicitFacing = 2,
    }

    public enum KinematicMutationKind
    {
        None = 0,
        Hold = 1,
        ReleaseHold = 2,
        ClearStaleHold = 3,
        Settle = 4,
        ResumeVoluntary = 5,
        ForceSettledZero = 6,
        InterruptFreeze = 7,
    }

    public readonly struct EntityPoseMutationRequest
    {
        public int EntityId { get; init; }
        public PoseMutationSource Source { get; init; }
        public PoseMutationKind Kind { get; init; }
        public SurfaceCell FromCell { get; init; }
        public SurfaceCell ToCell { get; init; }
        public bool PositionChanged { get; init; }
        public Direction FacingBefore { get; init; }
        public Direction FacingAfter { get; init; }
        public Direction MovementDirection { get; init; }
        public bool MovementIntentExists { get; init; }
        public bool MovementAccepted { get; init; }
        public bool MovementSuppressed { get; init; }
        public bool HasExplicitActionFacing { get; init; }
        public bool HasExplicitSkillFacing { get; init; }
        public bool HasExplicitRotateAction { get; init; }
        public KinematicMutationKind KinematicMutation { get; init; }
        public Direction KinematicDirection { get; init; }
        public bool HasKinematicDirection { get; init; }
        public KinematicDirectionKind KinematicDirectionKind { get; init; }
        public KinematicFacingPolicy KinematicFacingPolicy { get; init; }
        public bool ShouldUpdateFacing { get; init; }
        public int TickIndex { get; init; }
        public int OperationId { get; init; }
        public int MovementIntentId { get; init; }
        public int MovementResolutionId { get; init; }
        public int ActionSequenceId { get; init; }
        public string Writer { get; init; }
        public string Reason { get; init; }
        public bool UsesSyntheticOperationId { get; init; }
    }

    public readonly struct EntityPoseMutationDecision
    {
        public EntityPoseMutationDecision(
            bool allowed,
            string rejectReason,
            bool appliesPosition,
            bool appliesFacing,
            bool appliesKinematic)
        {
            Allowed = allowed;
            RejectReason = rejectReason ?? string.Empty;
            AppliesPosition = appliesPosition;
            AppliesFacing = appliesFacing;
            AppliesKinematic = appliesKinematic;
        }

        public bool Allowed { get; init; }
        public string RejectReason { get; init; }
        public bool AppliesPosition { get; init; }
        public bool AppliesFacing { get; init; }
        public bool AppliesKinematic { get; init; }
    }

    public readonly struct EntityPoseMutationOperation
    {
        public EntityPoseMutationOperation(
            EntityPoseMutationRequest request,
            UnitKinematicRuntimeState unitKinematicState = default)
        {
            Request = request;
            UnitKinematicState = unitKinematicState;
        }

        public EntityPoseMutationRequest Request { get; init; }
        public UnitKinematicRuntimeState UnitKinematicState { get; init; }
    }

    public static class EntityPoseMutationAuthority
    {
        public static EntityPoseMutationDecision Decide(in EntityPoseMutationRequest request)
        {
            switch (request.Source)
            {
                case PoseMutationSource.MovementProbe:
                case PoseMutationSource.MovementIntent:
                case PoseMutationSource.PresentationOnly:
                    return Reject("NonAuthoritativePoseMutationSource");

                case PoseMutationSource.MovementCommit:
                    if (!request.MovementAccepted)
                    {
                        return Reject("MovementCommitRequiresAcceptedMovement");
                    }

                    if (request.MovementSuppressed)
                    {
                        return Reject("MovementCommitSuppressed");
                    }

                    if (!request.PositionChanged)
                    {
                        return Reject("MovementCommitWithoutPositionChangeCannotMutateFacing");
                    }

                    if (request.Kind != PoseMutationKind.PositionAndFacing)
                    {
                        return Reject("PositionChangingMovementMustCommitPositionAndFacing");
                    }

                    if (request.FacingAfter != request.MovementDirection)
                    {
                        return Reject("CommittedMovementFacingMustMatchMovementDirection");
                    }

                    return AllowPositionAndFacing();

                case PoseMutationSource.CombatActionStart:
                case PoseMutationSource.CombatActionHold:
                case PoseMutationSource.CombatActionRelease:
                    return request.HasExplicitActionFacing
                        ? AllowFacingOnly()
                        : Reject("MissingExplicitActionFacing");

                case PoseMutationSource.MovementSkillStart:
                case PoseMutationSource.MovementSkillActive:
                    return request.HasExplicitSkillFacing
                        ? AllowFacingOnly()
                        : Reject("MissingExplicitSkillFacing");

                case PoseMutationSource.ExplicitRotateAction:
                    return request.HasExplicitRotateAction
                        ? AllowFacingOnly()
                        : Reject("MissingExplicitRotateAction");

                case PoseMutationSource.KinematicHold:
                case PoseMutationSource.KinematicRelease:
                case PoseMutationSource.KinematicSettle:
                    if (request.Kind != PoseMutationKind.KinematicOnly)
                    {
                        return Reject("KinematicCleanupMustUseKinematicOnly");
                    }

                    if (request.ShouldUpdateFacing || request.KinematicFacingPolicy != KinematicFacingPolicy.PreserveFacing)
                    {
                        return Reject("KinematicCleanupMustPreserveFacing");
                    }

                    return AllowKinematicOnly();

                case PoseMutationSource.KinematicLocomotion:
                case PoseMutationSource.KinematicMovementSkill:
                    if (request.Kind != PoseMutationKind.KinematicAndFacing)
                    {
                        return Reject("KinematicLocomotionRequiresKinematicAndFacing");
                    }

                    if (!request.HasKinematicDirection ||
                        !DirectionUtility.IsCardinal(request.KinematicDirection))
                    {
                        return Reject("MissingKinematicDirection");
                    }

                    if (!request.ShouldUpdateFacing)
                    {
                        return Reject("KinematicLocomotionRequiresFacingUpdate");
                    }

                    if (request.FacingAfter != request.KinematicDirection)
                    {
                        return Reject("KinematicFacingMustMatchDirection");
                    }

                    if (request.KinematicFacingPolicy != KinematicFacingPolicy.MatchKinematicDirection &&
                        request.KinematicFacingPolicy != KinematicFacingPolicy.ExplicitFacing)
                    {
                        return Reject("KinematicLocomotionRequiresFacingPolicy");
                    }

                    return AllowKinematicAndFacing();

                case PoseMutationSource.SpawnOrRespawn:
                case PoseMutationSource.Cleanup:
                    return AllowLifecycle(request);

                case PoseMutationSource.None:
                default:
                    return Reject("UnknownPoseMutationSource");
            }
        }

        private static EntityPoseMutationDecision AllowLifecycle(in EntityPoseMutationRequest request)
        {
            return request.Kind switch
            {
                PoseMutationKind.PositionOnly => new EntityPoseMutationDecision(true, string.Empty, true, false, false),
                PoseMutationKind.FacingOnly => AllowFacingOnly(),
                PoseMutationKind.PositionAndFacing => AllowPositionAndFacing(),
                PoseMutationKind.KinematicOnly => AllowKinematicOnly(),
                PoseMutationKind.PositionFacingAndKinematic => new EntityPoseMutationDecision(true, string.Empty, true, true, true),
                PoseMutationKind.KinematicAndFacing => AllowKinematicAndFacing(),
                _ => Reject("LifecyclePoseMutationRequiresMutationKind"),
            };
        }

        private static EntityPoseMutationDecision AllowPositionAndFacing()
        {
            return new EntityPoseMutationDecision(true, string.Empty, true, true, false);
        }

        private static EntityPoseMutationDecision AllowFacingOnly()
        {
            return new EntityPoseMutationDecision(true, string.Empty, false, true, false);
        }

        private static EntityPoseMutationDecision AllowKinematicOnly()
        {
            return new EntityPoseMutationDecision(true, string.Empty, false, false, true);
        }

        private static EntityPoseMutationDecision AllowKinematicAndFacing()
        {
            return new EntityPoseMutationDecision(true, string.Empty, false, true, true);
        }

        private static EntityPoseMutationDecision Reject(string reason)
        {
            return new EntityPoseMutationDecision(false, reason, false, false, false);
        }
    }

    public static class KinematicDirectionResolver
    {
        public static bool TryResolveKinematicDirection(
            SurfaceCell before,
            SurfaceCell after,
            CubeTopologyState topology,
            BoardBounds boardBounds,
            out Direction direction)
        {
            if (TryResolveSameFaceDirection(before, after, out direction))
            {
                return true;
            }

            foreach (var candidate in CardinalDirections)
            {
                if (!SurfaceTraversalQueries.TryResolveUnitStep(
                        topology,
                        boardBounds,
                        before,
                        candidate,
                        out var destination,
                        out _,
                        out _) ||
                    !destination.Equals(after))
                {
                    continue;
                }

                direction = candidate;
                return true;
            }

            direction = Direction.None;
            return false;
        }

        private static bool TryResolveSameFaceDirection(
            SurfaceCell before,
            SurfaceCell after,
            out Direction direction)
        {
            direction = Direction.None;
            if (before.face != after.face)
            {
                return false;
            }

            var deltaX = after.x - before.x;
            var deltaY = after.y - before.y;
            if (deltaX == 1 && deltaY == 0)
            {
                direction = Direction.Right;
                return true;
            }

            if (deltaX == -1 && deltaY == 0)
            {
                direction = Direction.Left;
                return true;
            }

            if (deltaX == 0 && deltaY == 1)
            {
                direction = Direction.Up;
                return true;
            }

            if (deltaX == 0 && deltaY == -1)
            {
                direction = Direction.Down;
                return true;
            }

            return false;
        }

        private static readonly Direction[] CardinalDirections =
        {
            Direction.Up,
            Direction.Right,
            Direction.Down,
            Direction.Left,
        };
    }
}

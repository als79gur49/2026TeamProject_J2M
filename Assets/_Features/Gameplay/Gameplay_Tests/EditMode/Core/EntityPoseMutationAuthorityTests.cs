using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class EntityPoseMutationAuthorityTests
    {
        [Test]
        [Category("Core")]
        public void MovementCommit_PositionAndFacingCommitAtomically()
        {
            var request = CreateMovementCommitRequest(
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0),
                Direction.Right,
                Direction.Right);

            var decision = EntityPoseMutationAuthority.Decide(request);

            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.AppliesPosition, Is.True);
            Assert.That(decision.AppliesFacing, Is.True);
            Assert.That(decision.AppliesKinematic, Is.False);
        }

        [Test]
        [Category("Core")]
        public void MovementCommit_FacingMustMatchMovementDirection()
        {
            var request = CreateMovementCommitRequest(
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0),
                Direction.Right,
                Direction.Left);

            var decision = EntityPoseMutationAuthority.Decide(request);

            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.RejectReason, Is.EqualTo("CommittedMovementFacingMustMatchMovementDirection"));
        }

        [Test]
        [Category("Core")]
        public void MovementCommit_NoPositionChange_BlocksFacing()
        {
            var cell = new SurfaceCell(FaceId.Floor, 0, 0);
            var request = CreateMovementCommitRequest(cell, cell, Direction.Right, Direction.Right);

            var decision = EntityPoseMutationAuthority.Decide(request);

            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.RejectReason, Is.EqualTo("MovementCommitWithoutPositionChangeCannotMutateFacing"));
        }

        [Test]
        [Category("Core")]
        public void MovementProbe_CannotMutatePose()
        {
            var request = CreateMovementCommitRequest(
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0),
                Direction.Right,
                Direction.Right,
                PoseMutationSource.MovementProbe);

            var decision = EntityPoseMutationAuthority.Decide(request);

            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.RejectReason, Is.EqualTo("NonAuthoritativePoseMutationSource"));
        }

        [Test]
        [Category("Core")]
        public void CombatActionStart_AllowsFacingOnly_WithExplicitMetadata()
        {
            var request = new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = PoseMutationSource.CombatActionStart,
                Kind = PoseMutationKind.FacingOnly,
                HasExplicitActionFacing = true,
                FacingAfter = Direction.Right,
                Writer = "CombatActionStart",
            };

            var decision = EntityPoseMutationAuthority.Decide(request);

            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.AppliesFacing, Is.True);
            Assert.That(decision.AppliesPosition, Is.False);
        }

        [Test]
        [Category("Core")]
        public void ExplicitRotate_RequiresExplicitRotateMetadata()
        {
            var request = new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = PoseMutationSource.ExplicitRotateAction,
                Kind = PoseMutationKind.FacingOnly,
                HasExplicitRotateAction = false,
                FacingAfter = Direction.Right,
                Writer = "MovementFacingResolution",
            };

            var decision = EntityPoseMutationAuthority.Decide(request);

            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.RejectReason, Is.EqualTo("MissingExplicitRotateAction"));
        }

        [Test]
        [Category("Core")]
        public void KinematicRelease_UsesKinematicOnlyMutation()
        {
            var request = new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = PoseMutationSource.KinematicRelease,
                Kind = PoseMutationKind.KinematicOnly,
                FacingBefore = Direction.Left,
                FacingAfter = Direction.Left,
                KinematicMutation = KinematicMutationKind.ReleaseHold,
                KinematicFacingPolicy = KinematicFacingPolicy.PreserveFacing,
                Writer = "CombatLocomotionRelease",
            };

            var decision = EntityPoseMutationAuthority.Decide(request);

            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.AppliesKinematic, Is.True);
            Assert.That(decision.AppliesPosition, Is.False);
            Assert.That(decision.AppliesFacing, Is.False);
        }

        [Test]
        [Category("Core")]
        public void KinematicLocomotion_AnchorDelta_FacingMatchesDirection()
        {
            var request = CreateKinematicLocomotionRequest(Direction.Right, Direction.Right);

            var decision = EntityPoseMutationAuthority.Decide(request);

            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.AppliesKinematic, Is.True);
            Assert.That(decision.AppliesFacing, Is.True);
            Assert.That(decision.AppliesPosition, Is.False);
        }

        [Test]
        [Category("Core")]
        public void KinematicLocomotion_RejectsFacingDirectionMismatch()
        {
            var request = CreateKinematicLocomotionRequest(Direction.Right, Direction.Left);

            var decision = EntityPoseMutationAuthority.Decide(request);

            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.RejectReason, Is.EqualTo("KinematicFacingMustMatchDirection"));
        }

        [Test]
        [Category("Core")]
        public void KinematicSettle_KinematicOnly_DoesNotUpdateFacing()
        {
            var request = new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = PoseMutationSource.KinematicSettle,
                Kind = PoseMutationKind.KinematicOnly,
                FromCell = new SurfaceCell(FaceId.Floor, 0, 0),
                ToCell = new SurfaceCell(FaceId.Floor, 0, 0),
                FacingBefore = Direction.Up,
                FacingAfter = Direction.Up,
                KinematicMutation = KinematicMutationKind.Settle,
                KinematicFacingPolicy = KinematicFacingPolicy.PreserveFacing,
                ShouldUpdateFacing = false,
                Writer = "KinematicSettle",
            };

            var decision = EntityPoseMutationAuthority.Decide(request);

            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.AppliesKinematic, Is.True);
            Assert.That(decision.AppliesFacing, Is.False);
        }

        [Test]
        [Category("Core")]
        public void MovementCommit_PositionAndFacingCommit_CreatesPresentationRecord()
        {
            var request = CreateMovementCommitRequest(
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0),
                Direction.Right,
                Direction.Right,
                operationId: 137,
                movementResolutionId: 137);
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(request));

            Assert.That(batch.MovementPresentationRecords.Count, Is.EqualTo(1));
            var record = batch.MovementPresentationRecords[0];
            Assert.That(record.EntityId, Is.EqualTo(request.EntityId));
            Assert.That(record.OperationId, Is.EqualTo(request.OperationId));
            Assert.That(record.MovementResolutionId, Is.EqualTo(request.MovementResolutionId));
            Assert.That(record.FromCell, Is.EqualTo(request.FromCell));
            Assert.That(record.ToCell, Is.EqualTo(request.ToCell));
            Assert.That(record.MovementDirection, Is.EqualTo(Direction.Right));
            Assert.That(batch.MovementPresentationDiagnostics[0], Does.Contain("Created=1"));
        }

        [Test]
        [Category("Core")]
        public void MovementRejected_DoesNotCreateMotionTrack()
        {
            var request = CreateMovementCommitRequest(
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0),
                Direction.Right,
                Direction.Right,
                movementAccepted: false);
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(request));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
            Assert.That(batch.MovementPresentationDiagnostics[0], Does.Contain("Created=0"));
            Assert.That(batch.MovementPresentationDiagnostics[0], Does.Contain("MovementCommitRequiresAcceptedMovement"));
        }

        [Test]
        [Category("Core")]
        public void MovementSuppressed_DoesNotCreateMotionTrack()
        {
            var request = CreateMovementCommitRequest(
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0),
                Direction.Right,
                Direction.Right,
                movementSuppressed: true);
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(request));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
            Assert.That(batch.MovementPresentationDiagnostics[0], Does.Contain("Created=0"));
            Assert.That(batch.MovementPresentationDiagnostics[0], Does.Contain("MovementCommitSuppressed"));
        }

        [Test]
        [Category("Core")]
        public void PoseMutation_FacingOnly_DoesNotCreateMotionTrack()
        {
            var cell = new SurfaceCell(FaceId.Floor, 0, 0);
            var request = new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = PoseMutationSource.MovementCommit,
                Kind = PoseMutationKind.FacingOnly,
                FromCell = cell,
                ToCell = cell,
                PositionChanged = false,
                FacingAfter = Direction.Right,
                MovementDirection = Direction.None,
                MovementAccepted = true,
                MovementSuppressed = false,
                Writer = "MovementFacingResolution",
            };
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(request));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PoseMutation_KinematicOnly_DoesNotCreateMotionTrack()
        {
            var request = new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = PoseMutationSource.KinematicRelease,
                Kind = PoseMutationKind.KinematicOnly,
                KinematicMutation = KinematicMutationKind.ReleaseHold,
                Writer = "CombatLocomotionRelease",
            };
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(request));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ExplicitActionFacing_DoesNotCreateMovementMotionTrack()
        {
            var request = new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = PoseMutationSource.CombatActionStart,
                Kind = PoseMutationKind.FacingOnly,
                HasExplicitActionFacing = true,
                FacingAfter = Direction.Right,
                Writer = "CombatActionStart",
            };
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(request));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ExplicitRotate_DoesNotCreateMovementMotionTrack()
        {
            var request = new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = PoseMutationSource.ExplicitRotateAction,
                Kind = PoseMutationKind.FacingOnly,
                HasExplicitRotateAction = true,
                FacingAfter = Direction.Right,
                Writer = "ExplicitRotateAction",
            };
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(request));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
        }

        private static EntityPoseMutationRequest CreateMovementCommitRequest(
            SurfaceCell from,
            SurfaceCell to,
            Direction movementDirection,
            Direction facingAfter,
            PoseMutationSource source = PoseMutationSource.MovementCommit,
            int operationId = 0,
            int movementResolutionId = 0,
            bool movementAccepted = true,
            bool movementSuppressed = false)
        {
            return new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = source,
                Kind = PoseMutationKind.PositionAndFacing,
                FromCell = from,
                ToCell = to,
                PositionChanged = !from.Equals(to),
                FacingBefore = Direction.Left,
                FacingAfter = facingAfter,
                MovementDirection = movementDirection,
                MovementIntentExists = true,
                MovementAccepted = movementAccepted,
                MovementSuppressed = movementSuppressed,
                TickIndex = 1,
                OperationId = operationId,
                MovementIntentId = 17,
                MovementResolutionId = movementResolutionId,
                Writer = "MovementCommit",
            };
        }

        private static EntityPoseMutationRequest CreateKinematicLocomotionRequest(
            Direction kinematicDirection,
            Direction facingAfter)
        {
            return new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = PoseMutationSource.KinematicLocomotion,
                Kind = PoseMutationKind.KinematicAndFacing,
                FromCell = new SurfaceCell(FaceId.Floor, 0, 0),
                ToCell = new SurfaceCell(FaceId.Floor, 1, 0),
                PositionChanged = true,
                FacingBefore = Direction.Left,
                FacingAfter = facingAfter,
                MovementDirection = kinematicDirection,
                MovementIntentExists = true,
                MovementAccepted = true,
                MovementSuppressed = false,
                KinematicMutation = KinematicMutationKind.ResumeVoluntary,
                KinematicDirection = kinematicDirection,
                HasKinematicDirection = true,
                KinematicDirectionKind = KinematicDirectionKind.AnchorDelta,
                KinematicFacingPolicy = KinematicFacingPolicy.MatchKinematicDirection,
                ShouldUpdateFacing = true,
                TickIndex = 1,
                OperationId = 241,
                MovementIntentId = 17,
                MovementResolutionId = 137,
                Writer = "KinematicMotionOutcome",
            };
        }
    }
}

using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class PoseMutationPresentationCarrierTests
    {
        [Test]
        [Category("Core")]
        public void MovementPresentationRecord_DiscreteMovementOnly()
        {
            var request = CreateMovementCommitRequest(
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0),
                Direction.Right,
                Direction.Right);
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(request));

            Assert.That(batch.MovementPresentationRecords, Has.Count.EqualTo(1));
            Assert.That(batch.MovementPresentationRecords[0].Source, Is.EqualTo("MovementCommit"));
            Assert.That(batch.MovementPresentationRecords[0].PositionChanged, Is.True);
            Assert.That(batch.MovementPresentationRecords[0].WasAccepted, Is.True);
            Assert.That(batch.MovementPresentationDiagnostics[0], Does.Contain("[MovementPresentationRecord]"));
            Assert.That(batch.MovementPresentationDiagnostics[0], Does.Contain("Created=1"));
            Assert.That(batch.MovementPresentationDiagnostics[0], Does.Contain("Reason=Created"));
        }

        [Test]
        [Category("Core")]
        public void MovementPresentationRecord_NotCreatedForKinematicOnly()
        {
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(CreateKinematicCleanupRequest(PoseMutationSource.KinematicHold)));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
            Assert.That(batch.MovementPresentationDiagnostics, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void MovementPresentationRecord_NotCreatedForKinematicSettle()
        {
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(CreateKinematicCleanupRequest(PoseMutationSource.KinematicSettle)));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
            Assert.That(batch.MovementPresentationDiagnostics, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void MovementPresentationRecord_NotCreatedForKinematicRelease()
        {
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(CreateKinematicCleanupRequest(PoseMutationSource.KinematicRelease)));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
            Assert.That(batch.MovementPresentationDiagnostics, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void MovementPresentationRecord_NotCreatedForExplicitActionFacing()
        {
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(CreateFacingOnlyRequest(
                PoseMutationSource.CombatActionStart,
                hasExplicitActionFacing: true)));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
            Assert.That(batch.MovementPresentationDiagnostics, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void MovementPresentationRecord_NotCreatedForExplicitRotate()
        {
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(CreateFacingOnlyRequest(
                PoseMutationSource.ExplicitRotateAction,
                hasExplicitRotateAction: true)));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
            Assert.That(batch.MovementPresentationDiagnostics, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void MovementPresentationRecord_NotCreatedForSpawnOrRespawn()
        {
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(CreateLifecycleRequest(PoseMutationSource.SpawnOrRespawn)));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
            Assert.That(batch.MovementPresentationDiagnostics, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void MovementPresentationRecord_NotCreatedForCleanupRelocation()
        {
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(CreateLifecycleRequest(PoseMutationSource.Cleanup)));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
            Assert.That(batch.MovementPresentationDiagnostics, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void MovementCommit_NoOp_DoesNotCreateMovementPresentationRecord()
        {
            var cell = new SurfaceCell(FaceId.Floor, 0, 0);
            var batch = new FinalizationBatch();

            batch.AddPoseMutation(new EntityPoseMutationOperation(CreateMovementCommitRequest(
                cell,
                cell,
                Direction.Right,
                Direction.Right)));

            Assert.That(batch.MovementPresentationRecords, Is.Empty);
            Assert.That(batch.MovementPresentationDiagnostics[0], Does.Contain("Created=0"));
            Assert.That(batch.MovementPresentationDiagnostics[0], Does.Contain("MovementCommitWithoutPositionChangeCannotMutateFacing"));
        }

        [Test]
        [Category("Core")]
        public void MovementPresentationRecord_LogOnlyForDiscreteMovementCommit()
        {
            var actionBatch = new FinalizationBatch();
            actionBatch.AddPoseMutation(new EntityPoseMutationOperation(CreateFacingOnlyRequest(
                PoseMutationSource.CombatActionStart,
                hasExplicitActionFacing: true)));

            var movementBatch = new FinalizationBatch();
            movementBatch.AddPoseMutation(new EntityPoseMutationOperation(CreateMovementCommitRequest(
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0),
                Direction.Right,
                Direction.Right)));

            Assert.That(actionBatch.MovementPresentationDiagnostics, Is.Empty);
            Assert.That(movementBatch.MovementPresentationDiagnostics, Has.Count.EqualTo(1));
            Assert.That(movementBatch.MovementPresentationDiagnostics[0], Does.Contain("[MovementPresentationRecord]"));
        }

        private static EntityPoseMutationRequest CreateMovementCommitRequest(
            SurfaceCell from,
            SurfaceCell to,
            Direction movementDirection,
            Direction facingAfter)
        {
            return new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = PoseMutationSource.MovementCommit,
                Kind = PoseMutationKind.PositionAndFacing,
                FromCell = from,
                ToCell = to,
                PositionChanged = !from.Equals(to),
                FacingBefore = Direction.Left,
                FacingAfter = facingAfter,
                MovementDirection = movementDirection,
                MovementIntentExists = true,
                MovementAccepted = true,
                MovementSuppressed = false,
                TickIndex = 1,
                OperationId = 137,
                MovementIntentId = 17,
                MovementResolutionId = 137,
                Writer = "MovementCommit",
            };
        }

        private static EntityPoseMutationRequest CreateKinematicCleanupRequest(PoseMutationSource source)
        {
            return new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = source,
                Kind = PoseMutationKind.KinematicOnly,
                FromCell = new SurfaceCell(FaceId.Floor, 0, 0),
                ToCell = new SurfaceCell(FaceId.Floor, 0, 0),
                FacingBefore = Direction.Left,
                FacingAfter = Direction.Left,
                KinematicMutation = source == PoseMutationSource.KinematicSettle
                    ? KinematicMutationKind.Settle
                    : KinematicMutationKind.ReleaseHold,
                KinematicFacingPolicy = KinematicFacingPolicy.PreserveFacing,
                ShouldUpdateFacing = false,
                Writer = source.ToString(),
            };
        }

        private static EntityPoseMutationRequest CreateFacingOnlyRequest(
            PoseMutationSource source,
            bool hasExplicitActionFacing = false,
            bool hasExplicitRotateAction = false)
        {
            return new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = source,
                Kind = PoseMutationKind.FacingOnly,
                FacingBefore = Direction.Left,
                FacingAfter = Direction.Right,
                HasExplicitActionFacing = hasExplicitActionFacing,
                HasExplicitRotateAction = hasExplicitRotateAction,
                Writer = source.ToString(),
            };
        }

        private static EntityPoseMutationRequest CreateLifecycleRequest(PoseMutationSource source)
        {
            return new EntityPoseMutationRequest
            {
                EntityId = 40,
                Source = source,
                Kind = PoseMutationKind.PositionAndFacing,
                FromCell = new SurfaceCell(FaceId.Floor, 0, 0),
                ToCell = new SurfaceCell(FaceId.Floor, 1, 0),
                PositionChanged = true,
                FacingBefore = Direction.Left,
                FacingAfter = Direction.Right,
                Writer = source.ToString(),
            };
        }
    }
}

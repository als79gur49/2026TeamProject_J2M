using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class BoxActionPresentationCarrierCoreTests
    {
        [Test]
        [Category("Core")]
        public void PushAction_CreatesBoxSlidePresentationRecord()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var operationId = 137;
            var intentId = 17;
            var resolutionId = 23;

            var sourceEntity = CreateBox(30, sourceCell, Direction.Right, BoxCapabilities.Push);
            var destinationEntity = CreateBox(30, destinationCell, Direction.Right, BoxCapabilities.Push);
            destinationEntity.state = EntityPhaseState.Sliding;
            destinationEntity.stateTimer = 7;
            destinationEntity.kineticInstigatorEntityId = 10;
            destinationEntity.kineticInstigatorTeamId = 1;

            var presentationData = BuildBoxActionPresentationData(
                sourceEntity,
                destinationEntity,
                CreateMovementPresentationRecord(
                    30,
                    sourceCell,
                    destinationCell,
                    Direction.Right,
                    operationId,
                    intentId,
                    resolutionId),
                CreateMovementCommitPoseOperation(
                    30,
                    sourceCell,
                    destinationCell,
                    Direction.Right,
                    operationId,
                    intentId,
                    resolutionId,
                    CreateMetadata(
                        ResolvedActionSemanticKind.Slide,
                        MovementSemanticKind.Slide,
                        actorEntityId: 10,
                        actionPlanId: 100,
                        intentId,
                        resolutionId)));

            var record = presentationData.PushSlidePresentationRecords.Single();
            Assert.That(record.BoxEntityId, Is.EqualTo(30));
            Assert.That(record.ActorEntityId, Is.EqualTo(10));
            Assert.That(record.OperationId, Is.EqualTo(operationId));
            Assert.That(record.MovementIntentId, Is.EqualTo(intentId));
            Assert.That(record.MovementResolutionId, Is.EqualTo(resolutionId));
            Assert.That(record.FromCell, Is.EqualTo(sourceCell));
            Assert.That(record.ToCell, Is.EqualTo(destinationCell));
            Assert.That(record.Direction, Is.EqualTo(Direction.Right));
            Assert.That(record.KineticInstigatorEntityId, Is.EqualTo(10));
            Assert.That(record.KineticInstigatorTeamId, Is.EqualTo(1));
            Assert.That(record.StateTimerTicks, Is.EqualTo(7));
            Assert.That(record.IsInitialPush, Is.True);
            Assert.That(record.IsAutoSlide, Is.False);
            Assert.That(presentationData.BoxSlideStartSignals.Single().BoxEntityId, Is.EqualTo(30));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Kind: TickEntityMotionKind.BoxSlide, OperationId: operationId),
                },
                presentationData.EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.OperationId))
                    .ToArray());
            Assert.That(
                presentationData.EntityMotions.Any(motion => motion.MotionKind == TickEntityMotionKind.Move),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void FlipAction_CreatesBoxFlipPresentationRecord()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var operationId = 211;
            var intentId = 19;
            var resolutionId = 29;

            var sourceEntity = CreateBox(30, sourceCell, Direction.Left, BoxCapabilities.Flip);
            var destinationEntity = CreateBox(30, landingCell, Direction.Right, BoxCapabilities.Flip);

            var presentationData = BuildBoxActionPresentationData(
                sourceEntity,
                destinationEntity,
                CreateMovementPresentationRecord(
                    30,
                    sourceCell,
                    landingCell,
                    Direction.Right,
                    operationId,
                    intentId,
                    resolutionId),
                CreateMovementCommitPoseOperation(
                    30,
                    sourceCell,
                    landingCell,
                    Direction.Right,
                    operationId,
                    intentId,
                    resolutionId,
                    CreateMetadata(
                        ResolvedActionSemanticKind.Flip,
                        MovementSemanticKind.Flip,
                        actorEntityId: 10,
                        actionPlanId: 101,
                        intentId,
                        resolutionId)));

            var record = presentationData.BoxFlipPresentationRecords.Single();
            Assert.That(record.BoxEntityId, Is.EqualTo(30));
            Assert.That(record.ActorEntityId, Is.EqualTo(10));
            Assert.That(record.OperationId, Is.EqualTo(operationId));
            Assert.That(record.MovementIntentId, Is.EqualTo(intentId));
            Assert.That(record.MovementResolutionId, Is.EqualTo(resolutionId));
            Assert.That(record.TargetBoxCell, Is.EqualTo(sourceCell));
            Assert.That(record.LandingCell, Is.EqualTo(landingCell));
            Assert.That(record.FlipDirection, Is.EqualTo(Direction.Right));
            Assert.That(record.FacingBefore, Is.EqualTo(Direction.Left));
            Assert.That(record.FacingAfter, Is.EqualTo(Direction.Right));
            Assert.That(record.Disposition, Is.EqualTo(BoxFlipDisposition.Landing));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Kind: TickEntityMotionKind.Flip, OperationId: operationId),
                },
                presentationData.EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.OperationId))
                    .ToArray());
            Assert.That(presentationData.FlipFloorImpactSignals.Single().BoxEntityId, Is.EqualTo(30));
            Assert.That(
                presentationData.EntityMotions.Any(motion => motion.MotionKind == TickEntityMotionKind.Move),
                Is.False);
        }

        private static TickPresentationData BuildBoxActionPresentationData(
            EntityState sourceEntity,
            EntityState destinationEntity,
            MovementPresentationRecord movementRecord,
            FinalizationOperation operation)
        {
            var preMovementSnapshot = CreateWorldState(new[] { sourceEntity }).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(new[] { destinationEntity }).CreateSnapshot();

            return new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    CreateMovementPhaseResultWithPresentationRecordsAndOperations(
                        new[] { movementRecord },
                        new[] { operation }),
                    AttackPhaseResult.Empty,
                    CleanupPhaseResult.Empty,
                    currentTickIndex: movementRecord.TickIndex));
        }

        private static MovementPhaseResult CreateMovementPhaseResultWithPresentationRecordsAndOperations(
            IEnumerable<MovementPresentationRecord> records,
            IEnumerable<FinalizationOperation> operations)
        {
            return new MovementPhaseResult(
                Array.Empty<RawMovementIntent>(),
                Array.Empty<MoveIntent>(),
                Array.Empty<ResolutionRecord>(),
                Array.Empty<ImpactDispositionResolutionRecord>(),
                operations ?? Array.Empty<FinalizationOperation>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                movementPresentationRecords: records ?? Array.Empty<MovementPresentationRecord>());
        }

        private static MovementPresentationRecord CreateMovementPresentationRecord(
            int entityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            Direction direction,
            int operationId,
            int intentId,
            int resolutionId)
        {
            return new MovementPresentationRecord(
                entityId,
                tickIndex: 1,
                operationId,
                intentId,
                resolutionId,
                sourceCell,
                destinationCell,
                direction,
                positionChanged: true,
                wasAccepted: true,
                source: "MovementCommit",
                usesSyntheticOperationId: false);
        }

        private static FinalizationOperation CreateMovementCommitPoseOperation(
            int entityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            Direction direction,
            int operationId,
            int intentId,
            int resolutionId,
            FinalizationOperationMetadata metadata)
        {
            return FinalizationOperation.PoseMutation(
                1,
                new EntityPoseMutationOperation(
                    new EntityPoseMutationRequest
                    {
                        EntityId = entityId,
                        Source = PoseMutationSource.MovementCommit,
                        Kind = PoseMutationKind.PositionAndFacing,
                        FromCell = sourceCell,
                        ToCell = destinationCell,
                        PositionChanged = true,
                        FacingBefore = Direction.None,
                        FacingAfter = direction,
                        MovementDirection = direction,
                        MovementIntentExists = true,
                        MovementAccepted = true,
                        MovementSuppressed = false,
                        TickIndex = 1,
                        OperationId = operationId,
                        MovementIntentId = intentId,
                        MovementResolutionId = resolutionId,
                        Writer = "MovementCommit",
                        Reason = "BoxActionMovement",
                    }),
                metadata);
        }

        private static FinalizationOperationMetadata CreateMetadata(
            ResolvedActionSemanticKind actionSemantic,
            MovementSemanticKind movementSemantic,
            int actorEntityId,
            int actionPlanId,
            int intentId,
            int resolutionId)
        {
            return new FinalizationOperationMetadata(
                TickPhase.Resolve,
                actionSemantic,
                sourceActorEntityId: actorEntityId,
                actionPlanId: actionPlanId,
                intentId: intentId,
                contestId: resolutionId,
                movementSemanticKind: movementSemantic,
                movementExecutionBoundaryKind: MovementExecutionBoundaryKind.BoxActionMovement,
                boundaryReason: "BoxActionMovement");
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(4, 4)),
                GameplayTerrainData.Empty);
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            Direction facing,
            BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Box,
                boxCapabilities = capabilities,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}

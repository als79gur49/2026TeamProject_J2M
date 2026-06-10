using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class ReservationReadModelContractTests
    {
        [Test]
        [Category("Core")]
        public void MovementReservationBook_Freeze_SortsImpactReservations_AndExposesReadOnlyStatuses()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var jumpLandingCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var reservationBook = new MovementReservationBook();

            reservationBook.ReserveImpactPayload(
                CreateImpactReservationPayload(
                    sourceEntityId: 20,
                    attackSourceEntityId: 10,
                    sourceCell,
                    targetEntityId: 30,
                    destinationCell),
                actionPlanId: 101);
            reservationBook.ReserveJumpLanding(
                entityId: 40,
                jumpLandingCell,
                blocksUnitSharedSettlement: true);

            var frozenExport = reservationBook.Freeze(new[]
            {
                new ImpactReservation(sourceId: 30, targetId: 31, impactCell: destinationCell, damage: 1, tickGenerated: 4),
                new ImpactReservation(sourceId: 10, targetId: 11, impactCell: jumpLandingCell, damage: 1, tickGenerated: 4),
            });

            Assert.That(frozenExport.FreezeVersion, Is.EqualTo(1));
            Assert.That(frozenExport.ImpactReservations, Has.Count.EqualTo(2));
            Assert.That(frozenExport.ImpactReservations[0].SourceId, Is.EqualTo(10));
            Assert.That(frozenExport.ImpactReservations[1].SourceId, Is.EqualTo(30));
            Assert.That(frozenExport.GetCellStatus(destinationCell), Is.EqualTo(ReservationStatus.Reserved));
            Assert.That(frozenExport.GetCellStatus(jumpLandingCell), Is.EqualTo(ReservationStatus.Reserved));
            Assert.That(frozenExport.GetEdgeStatus(sourceCell, destinationCell), Is.EqualTo(ReservationStatus.Reserved));
            Assert.That(frozenExport.GetEntityStatus(20), Is.EqualTo(ReservationStatus.Reserved));
            Assert.That(frozenExport.GetEntityStatus(40), Is.EqualTo(ReservationStatus.Reserved));
        }

        [Test]
        [Category("Core")]
        public void MovementReservationBook_CellReservationInfo_DistinguishesUnitSharedSettlementCompatibility()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreateUnit(entityId: 10, position: sourceCell),
            });
            var reservationBook = new MovementReservationBook();

            Assert.That(
                reservationBook.TryAcceptPayload(
                    worldState.CreateSnapshot(),
                    CreateMovePayload(
                        actionPlanId: 1,
                        sourceActorEntityId: 10,
                        movedEntityId: 10,
                        sourceCell,
                        destinationCell),
                    out _),
                Is.True);

            var unitMoveInfo = reservationBook.GetCellReservationInfo(destinationCell);
            Assert.That(unitMoveInfo.Status, Is.EqualTo(ReservationStatus.Conflicted));
            Assert.That(unitMoveInfo.ReservedEntityId, Is.EqualTo(10));
            Assert.That(unitMoveInfo.ReservedEntityType, Is.EqualTo(EntityType.Unit));
            Assert.That(unitMoveInfo.IsUnitSharedSettlementCompatible, Is.True);

            var jumpReservationBook = new MovementReservationBook();
            jumpReservationBook.ReserveJumpLanding(
                entityId: 40,
                destinationCell: destinationCell,
                blocksUnitSharedSettlement: false);

            var jumpInfo = jumpReservationBook.GetCellReservationInfo(destinationCell);
            Assert.That(jumpInfo.Status, Is.EqualTo(ReservationStatus.Conflicted));
            Assert.That(jumpInfo.ReservedEntityType, Is.EqualTo(EntityType.Unit));
            Assert.That(jumpInfo.IsUnitSharedSettlementCompatible, Is.True);

            var blockingJumpReservationBook = new MovementReservationBook();
            blockingJumpReservationBook.ReserveJumpLanding(
                entityId: 40,
                destinationCell: destinationCell,
                blocksUnitSharedSettlement: true);

            var blockingJumpInfo = blockingJumpReservationBook.GetCellReservationInfo(destinationCell);
            Assert.That(blockingJumpInfo.Status, Is.EqualTo(ReservationStatus.Conflicted));
            Assert.That(blockingJumpInfo.ReservedEntityType, Is.EqualTo(EntityType.Unit));
            Assert.That(blockingJumpInfo.IsUnitSharedSettlementCompatible, Is.False);

            var impactReservationBook = new MovementReservationBook();
            impactReservationBook.ReserveImpactPayload(
                CreateImpactReservationPayload(
                    sourceEntityId: 20,
                    attackSourceEntityId: 10,
                    sourceCell,
                    targetEntityId: 30,
                    destinationCell),
                actionPlanId: 101);

            var impactInfo = impactReservationBook.GetCellReservationInfo(destinationCell);
            Assert.That(impactInfo.Status, Is.EqualTo(ReservationStatus.Conflicted));
            Assert.That(impactInfo.ReservedEntityType, Is.EqualTo(EntityType.Unit));
            Assert.That(impactInfo.IsUnitSharedSettlementCompatible, Is.False);

            var boxSourceCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var boxDestinationCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var boxWorldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreateUnit(entityId: 10, position: sourceCell),
                CreateBox(entityId: 20, position: boxSourceCell),
            });
            var boxReservationBook = new MovementReservationBook();

            Assert.That(
                boxReservationBook.TryAcceptPayload(
                    boxWorldState.CreateSnapshot(),
                    CreateMovePayload(
                        actionPlanId: 2,
                        sourceActorEntityId: 10,
                        movedEntityId: 20,
                        boxSourceCell,
                        boxDestinationCell),
                    out _),
                Is.True);

            var boxMoveInfo = boxReservationBook.GetCellReservationInfo(boxDestinationCell);
            Assert.That(boxMoveInfo.Status, Is.EqualTo(ReservationStatus.Conflicted));
            Assert.That(boxMoveInfo.ReservedEntityId, Is.EqualTo(20));
            Assert.That(boxMoveInfo.ReservedEntityType, Is.EqualTo(EntityType.Box));
            Assert.That(boxMoveInfo.IsUnitSharedSettlementCompatible, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void MovementReservationBook_Freeze_RejectsPostFreezeMutation()
        {
            var reservationBook = new MovementReservationBook();
            reservationBook.Freeze(Array.Empty<ImpactReservation>());

            var exception = Assert.Throws<InvalidOperationException>(
                () => reservationBook.ReserveJumpLanding(
                    entityId: 10,
                    destinationCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    blocksUnitSharedSettlement: true));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("frozen", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void AttackInputNormalizer_Normalize_WithFrozenMovementReservationExport_ConsumesExportedImpactsOnly()
        {
            var normalizer = new AttackInputNormalizer();
            var buffer = new List<AttackIntent>();
            var frozenExport = new FrozenMovementReservationExport(
                freezeVersion: 3,
                impactReservations: new[]
                {
                    new ImpactReservation(
                        sourceId: 50,
                        targetId: 60,
                        impactCell: new SurfaceCell(FaceId.Floor, 3, 0),
                        damage: 2,
                        tickGenerated: 7),
                },
                reservedCells: new HashSet<SurfaceCell>(),
                reservedEdges: new Dictionary<UndirectedEdgeKey, EdgeReservation>(),
                reservedEntities: new HashSet<int>());

            normalizer.Normalize(
                Array.Empty<RawAttackIntent>(),
                frozenExport,
                Array.Empty<DelayedAttackEffectRecord>(),
                buffer);

            Assert.That(buffer, Has.Count.EqualTo(1));
            Assert.That(buffer[0].InputKind, Is.EqualTo(AttackInputKind.ImpactReservation));
            Assert.That(buffer[0].ImpactReservation.HasValue, Is.True);
            Assert.That(buffer[0].ImpactReservation.Value.SourceId, Is.EqualTo(50));
            Assert.That(buffer[0].ImpactReservation.Value.TargetId, Is.EqualTo(60));
        }

        private static MovementImpactReservationPayload CreateImpactReservationPayload(
            int sourceEntityId,
            int attackSourceEntityId,
            SurfaceCell sourceCell,
            int targetEntityId,
            SurfaceCell destinationCell)
        {
            return new MovementImpactReservationPayload(
                sourceEntityId,
                attackSourceEntityId,
                sourceCell,
                targetEntityId,
                destinationCell,
                damageAmount: 1,
                sequence: 0,
                contingentDestinationCell: destinationCell,
                contingentSourceCell: sourceCell,
                contingentFacing: Direction.Right,
                hasContingentStateChange: true,
                contingentState: EntityPhaseState.Sliding,
                contingentStateTimer: 1,
                hasSourceFacing: false,
                sourceFacingEntityId: 0,
                sourceFacing: Direction.None,
                dispositionPolicyKind: ImpactDispositionPolicyKind.PushLike,
                contingentSemanticKind: ResolvedActionSemanticKind.Push);
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                type = EntityType.Unit,
                position = position,
                hp = 3,
                maxHp = 3,
                boardPresence = EntityBoardPresence.Occupying,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                type = EntityType.Box,
                position = position,
                hp = 1,
                maxHp = 1,
                boardPresence = EntityBoardPresence.Occupying,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boxCapabilities = BoxCapabilities.Push,
            };
        }

        private static MovementActionPlanPayload CreateMovePayload(
            int actionPlanId,
            int sourceActorEntityId,
            int movedEntityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell)
        {
            return new MovementActionPlanPayload(
                actionPlanId,
                actionPlanId,
                sourceActorEntityId,
                0,
                ResolvedActionSemanticKind.Move,
                MovementCandidateKind.Move,
                sourceCell,
                destinationCell,
                true,
                new MovementEdge(sourceCell, destinationCell),
                new[] { movedEntityId },
                MovementReservationKind.Edge,
                MovementBlockingType.NonBlocking,
                Array.Empty<StateChangeWritePayload>(),
                new[] { new MoveWritePayload(movedEntityId, sourceCell, destinationCell, Direction.Right) },
                Array.Empty<BoardPresenceWritePayload>(),
                Array.Empty<FacingWritePayload>(),
                Array.Empty<BoxKineticOwnerWritePayload>(),
                Array.Empty<TopologyWritePayload>(),
                Array.Empty<ExecutionLockWritePayload>(),
                Array.Empty<EnemyLocomotionWritePayload>(),
                Array.Empty<EnemyPatrolWritePayload>(),
                Array.Empty<EnemyChargeWritePayload>(),
                Array.Empty<PlayerControlWritePayload>(),
                Array.Empty<DestroyWritePayload>(),
                false,
                default,
                false,
                default);
        }
    }
}

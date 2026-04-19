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
        [Category("Extended")]
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
            reservationBook.ReserveJumpLanding(entityId: 40, jumpLandingCell);

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
        [Category("Extended")]
        public void MovementReservationBook_RuntimeCellRead_UsesConflictedStatusForPreSettleConsumer()
        {
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var reservationBook = new MovementReservationBook();

            reservationBook.ReservePhaseRelocation(entityId: 40, destinationCell);

            Assert.That(reservationBook.GetCellStatus(destinationCell), Is.EqualTo(ReservationStatus.Conflicted));
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
                    destinationCell: new SurfaceCell(FaceId.Floor, 1, 0)));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("frozen", exception.Message);
        }

        [Test]
        [Category("Extended")]
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
    }
}

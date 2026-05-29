using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineImpactAcceptanceTests
    {
        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_AcceptedDestroyFromActorSourceAndSoleTargetOccupant_ReturnsAllowed()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateSnapshot(new[]
            {
                CreateBox(20, sourceCell),
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
            });

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    snapshot,
                    new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: true) },
                    CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell)).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_RemainingStackedOccupant_ReturnsBlocked()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateSnapshot(new[]
            {
                CreateBox(20, sourceCell),
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
                CreateUnit(40, destinationCell, hp: 3, teamId: 1),
            });

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    snapshot,
                    new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: true) },
                    CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell)).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_DestroyRejected_ReturnsBlocked()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateSnapshot(new[]
            {
                CreateBox(20, sourceCell),
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
            });

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    snapshot,
                    new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: false) },
                    CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell)).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_MissingSourceOrTarget_ReturnsBlocked()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateSnapshot(new[]
            {
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
            });

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    snapshot,
                    new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: true) },
                    CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell)).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_UnrelatedLethalOccupantStillBlocks_ReturnsBlocked()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateSnapshot(new[]
            {
                CreateBox(20, sourceCell),
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
                CreateUnit(40, destinationCell, hp: 0, teamId: 1, markedForDeath: true),
            });

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    snapshot,
                    new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: true) },
                    CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell)).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        private static WorldSnapshot CreateSnapshot(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities).CreateSnapshot();
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
                contingentStateTimer: 12,
                hasSourceFacing: false,
                sourceFacingEntityId: 0,
                sourceFacing: Direction.None,
                dispositionPolicyKind: ImpactDispositionPolicyKind.PushLike,
                contingentSemanticKind: ResolvedActionSemanticKind.Push);
        }

        private static DestroyResolutionRecord CreateDestroyResolution(
            int sourceId,
            int targetId,
            bool accepted)
        {
            return new DestroyResolutionRecord(
                actionPlanId: 1,
                intentId: 1,
                sourceId,
                targetId,
                DestroyCondition.WhenHpDepleted,
                finalHp: accepted ? 0 : 1,
                accepted,
                localActionIndex: 0);
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                facing = Direction.Right,
                boxCapabilities = BoxCapabilities.Push,
            };
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int hp,
            int teamId,
            bool markedForDeath = false)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp > 0 ? hp : 1,
                teamId = teamId,
                type = EntityType.Unit,
                facing = Direction.Right,
                markedForDeath = markedForDeath,
            };
        }
    }
}

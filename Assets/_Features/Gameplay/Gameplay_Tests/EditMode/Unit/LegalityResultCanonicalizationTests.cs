using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class LegalityResultCanonicalizationTests
    {
        [Test]
        [Category("Extended")]
        public void RuntimePlacementValidityPolicy_EvaluateGameplayPlacement_InactiveFaceTerrainBlockedAuthoritativeOnly_PreservesCanonicalFields()
        {
            var blockedCell = new SurfaceCell(FaceId.Front, 1, 0);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new List<EntityState>(),
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                    new GameplayTerrainData(new[] { blockedCell.PlanarPosition }),
                    new CubeTopologyState(FaceId.Back))
                .CreateSnapshot();

            var gameplayLegality = RuntimePlacementValidityPolicy.EvaluateGameplayPlacement(
                snapshot,
                EntityType.Unit,
                blockedCell,
                ignoredEntityId: 0);
            var authoritativeLegality = RuntimePlacementValidityPolicy.EvaluateAuthoritativePlacement(
                snapshot,
                EntityType.Unit,
                blockedCell,
                ignoredEntityId: 0);

            Assert.That(gameplayLegality.Domain, Is.EqualTo(LegalityDomain.Placement));
            Assert.That(gameplayLegality.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(gameplayLegality.Cell, Is.EqualTo(blockedCell));
            Assert.That(gameplayLegality.Topology, Is.EqualTo(snapshot.Topology));
            Assert.That(gameplayLegality.Reservation, Is.EqualTo(ReservationStatus.None));
            Assert.That(gameplayLegality.TransitionRequirement.Kind, Is.EqualTo(TransitionRequirementKind.None));
            Assert.That(gameplayLegality.Blockers, Is.Empty);
            Assert.That(authoritativeLegality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(authoritativeLegality.Blockers.Count, Is.EqualTo(1));
            Assert.That(authoritativeLegality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Terrain));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeTraversalLegalityPolicy_EvaluateDestination_WithRotation_ExportsTopologyUpdateRequirement()
        {
            var destinationCell = new SurfaceCell(FaceId.Front, 0, 0);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()).CreateSnapshot();
            var updatedTopology = new CubeTopologyState(FaceId.Back);

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                destinationCell,
                ignoredEntityId: 0,
                evaluatedTopology: updatedTopology,
                rotationKind: CubeRotationKind.Forward,
                updatedTopology: updatedTopology);

            Assert.That(legality.Domain, Is.EqualTo(LegalityDomain.Traversal));
            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(legality.Cell, Is.EqualTo(destinationCell));
            Assert.That(legality.Topology, Is.EqualTo(updatedTopology));
            Assert.That(legality.Blockers, Is.Empty);
            Assert.That(legality.TransitionRequirement.Kind, Is.EqualTo(TransitionRequirementKind.TopologyUpdate));
            Assert.That(legality.TransitionRequirement.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(legality.TransitionRequirement.UpdatedTopology, Is.EqualTo(updatedTopology));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_ConflictedReservation_ReturnsReservationBlocker()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreateBox(20, sourceCell),
                    CreateUnit(30, destinationCell),
                })
                .CreateSnapshot();

            var legality = RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                snapshot,
                new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: true) },
                CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell),
                ReservationStatus.Conflicted);

            Assert.That(legality.Domain, Is.EqualTo(LegalityDomain.Settlement));
            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Reservation, Is.EqualTo(ReservationStatus.Conflicted));
            Assert.That(legality.TransitionRequirement.Kind, Is.EqualTo(TransitionRequirementKind.None));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Reservation));
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
                contingentSemanticKind: ResolvedActionSemanticKind.Push);
        }

        private static DestroyResolutionRecord CreateDestroyResolution(
            int sourceId,
            int targetId,
            bool accepted)
        {
            return new DestroyResolutionRecord(
                groupId: 1,
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

        private static EntityState CreateUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Unit,
                facing = Direction.Right,
            };
        }
    }
}

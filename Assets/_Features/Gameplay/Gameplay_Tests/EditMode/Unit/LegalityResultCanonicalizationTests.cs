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
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()).CreateSnapshot();
            var updatedTopology = new CubeTopologyState(FaceId.Back);
            var destinationCell = new SurfaceCell(updatedTopology.BottomFace, 0, 0);

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
        public void RuntimeTraversalLegalityPolicy_EvaluateDestination_TopologyTransitionTerrainBlocked_PreservesRequirementAndBlocker()
        {
            var originCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Back, 0, 1);
            var unit = CreateUnit(10, originCell);
            var updatedTopology = new CubeTopologyState(FaceId.Back);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[] { unit },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    new GameplayTerrainData(new[]
                    {
                        new TerrainCellState(
                            destinationCell,
                            TerrainKind.Generic,
                            TerrainFlags.BlocksGroundTraversal),
                    }),
                    new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                destinationCell,
                ignoredEntityId: unit.entityId,
                evaluatedTopology: updatedTopology,
                rotationKind: CubeRotationKind.Backward,
                updatedTopology: updatedTopology);

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.TransitionRequirement.Kind, Is.EqualTo(TransitionRequirementKind.TopologyUpdate));
            Assert.That(legality.TransitionRequirement.RotationKind, Is.EqualTo(CubeRotationKind.Backward));
            Assert.That(legality.TransitionRequirement.UpdatedTopology, Is.EqualTo(updatedTopology));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Terrain));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeTraversalLegalityPolicy_EvaluateDestination_UnitGroundTraversal_BlockedByActiveBarricade()
        {
            var originCell = new SurfaceCell(FaceId.Front, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Front, 1, 0);
            var unit = CreateUnit(10, originCell);
            var barricade = CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade);
            var definitions = new[]
            {
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
            };
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[] { unit },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { barricade })
                .CreateSnapshot();

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                destinationCell,
                ignoredEntityId: unit.entityId,
                evaluatedTopology: snapshot.Topology,
                rotationKind: CubeRotationKind.None,
                updatedTopology: snapshot.Topology,
                tileFeatureDefinitions: definitions);

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.TileFeature));
            Assert.That(legality.Blockers[0].TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(legality.Blockers[0].TileId, Is.EqualTo(100));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeTraversalLegalityPolicy_EvaluateDestination_InactiveBarricade_AllowsUnitTraversal()
        {
            var originCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var unit = CreateUnit(10, originCell);
            var barricade = CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade);
            var definitions = new[]
            {
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
            };
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[] { unit },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { barricade })
                .CreateSnapshot();

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                destinationCell,
                ignoredEntityId: unit.entityId,
                evaluatedTopology: snapshot.Topology,
                rotationKind: CubeRotationKind.None,
                updatedTopology: snapshot.Topology,
                tileFeatureDefinitions: definitions);

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(legality.Blockers, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void RuntimeTraversalLegalityPolicy_EvaluateDestination_FrontFaceUnit_BlockedAtPolicyLevel()
        {
            var originCell = new SurfaceCell(FaceId.Front, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Front, 2, 0);
            var unit = CreateUnit(10, originCell);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[] { unit },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(3, 2)),
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade) })
                .CreateSnapshot();

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                new TraverseContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, unit),
                    originCell,
                    destinationCell,
                    snapshot.Topology,
                    TransitionRequirement.None,
                    tileFeatureDefinitions: new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly) }));

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.TileFeature));
        }

        [Test]
        [Category("Extended")]
        public void BarricadeOverlay_DoesNotBecomeOccupancy_OrInvalidateExistingUnitOccupancy()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 0);
            var unit = CreateUnit(10, cell);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[] { unit },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { CreateTileFeature(100, cell, TileFeatureKind.Barricade) })
                .CreateSnapshot();
            var units = new List<EntityState>();

            snapshot.EnumerateUnitsAt(cell, units);

            Assert.That(units.Count, Is.EqualTo(1));
            Assert.That(units[0].entityId, Is.EqualTo(unit.entityId));
            Assert.That(snapshot.TryGetSolidSemanticAt(cell, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void BarricadeActivation_BlocksNewEntrantWhileExistingOccupantCanRemain()
        {
            var cell = new SurfaceCell(FaceId.Front, 1, 0);
            var existingUnit = CreateUnit(10, cell);
            var entrant = CreateUnit(20, new SurfaceCell(FaceId.Front, 0, 0));
            var definitions = new[]
            {
                CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
            };
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[] { existingUnit, entrant },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { CreateTileFeature(100, cell, TileFeatureKind.Barricade) })
                .CreateSnapshot();

            var existingBlocked = TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                snapshot,
                definitions,
                cell,
                TileFeatureBlockerSubject.Unit,
                TileFeatureMovementKind.UnitSettlement,
                out _,
                snapshot.Topology,
                existingUnit.entityId);
            var entrantBlocked = TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                snapshot,
                definitions,
                cell,
                TileFeatureBlockerSubject.Unit,
                TileFeatureMovementKind.UnitSettlement,
                out var blocker,
                snapshot.Topology,
                entrant.entityId);

            Assert.That(existingBlocked, Is.False);
            Assert.That(entrantBlocked, Is.True);
            Assert.That(blocker.TileId, Is.EqualTo(100));
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

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_ActiveBarricade_ReturnsTileFeatureBlocker()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Front, 2, 0);
            var box = CreateBox(20, sourceCell);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        box,
                        CreateUnit(30, destinationCell),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(3, 2)),
                    GameplayTerrainData.Empty,
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade) })
                .CreateSnapshot();

            var legality = RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, box),
                    destinationCell,
                    snapshot.Topology,
                    SpatialState.Anchored,
                    tileFeatureDefinitions: new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly) }),
                new ImpactFollowThroughEvidence(
                    attackSourceId: 20,
                    targetId: 30,
                    destroyResolutions: new[] { CreateDestroyResolution(sourceId: 20, targetId: 30, accepted: true) }));

            Assert.That(legality.Domain, Is.EqualTo(LegalityDomain.Settlement));
            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.TileFeature));
            Assert.That(legality.Blockers[0].TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(legality.Blockers[0].TileId, Is.EqualTo(100));
        }

        [Test]
        [Category("Extended")]
        public void LegalityDiagnosticsFormatter_FormatStableSummary_ExportsStableFieldsOnly()
        {
            var legality = LegalityResult.Blocked(
                LegalityDomain.Traversal,
                new SurfaceCell(FaceId.Floor, 1, 0),
                new CubeTopologyState(FaceId.Floor),
                RuntimeLegalityBlockerFactory.CreateTerrain(TerrainFlags.BlocksGroundTraversal),
                ReservationStatus.None,
                TransitionRequirement.None);

            var formatted = LegalityDiagnosticsFormatter.FormatStableSummary(legality);

            Assert.That(formatted, Does.Contain("LegalityDomain=Traversal"));
            Assert.That(formatted, Does.Contain("LegalityVerdict=Blocked"));
            Assert.That(formatted, Does.Contain("ReservationStatus=None"));
            Assert.That(formatted, Does.Contain("LegalityBlockerKinds=Terrain"));
            Assert.That(formatted, Does.Not.Contain("CapabilityFlags"));
            Assert.That(formatted, Does.Not.Contain("ModifierFlags"));
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

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateDefinition(
            int tileId,
            TileFeatureActivationRule activationRule)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: 0,
                presentationKey: string.Empty);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class LegalityResultCanonicalizationTests
    {
        [Test]
        [Category("Extended")]
        public void RuntimePlacementValidityPolicy_EvaluateGameplayPlacement_InactiveFaceInBoundsCell_PreservesCanonicalFields()
        {
            var destinationCell = new SurfaceCell(FaceId.Front, 1, 0);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new List<EntityState>(),
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                    new CubeTopologyState(FaceId.Back))
                .CreateSnapshot();

            var gameplayLegality = RuntimePlacementValidityPolicy.EvaluateGameplayPlacement(
                snapshot,
                EntityType.Unit,
                destinationCell,
                ignoredEntityId: 0);
            var authoritativeLegality = RuntimePlacementValidityPolicy.EvaluateAuthoritativePlacement(
                snapshot,
                EntityType.Unit,
                destinationCell,
                ignoredEntityId: 0);

            Assert.That(gameplayLegality.Domain, Is.EqualTo(LegalityDomain.Placement));
            Assert.That(gameplayLegality.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(gameplayLegality.Cell, Is.EqualTo(destinationCell));
            Assert.That(gameplayLegality.Topology, Is.EqualTo(snapshot.Topology));
            Assert.That(gameplayLegality.Reservation, Is.EqualTo(ReservationStatus.None));
            Assert.That(gameplayLegality.TransitionRequirement.Kind, Is.EqualTo(TransitionRequirementKind.None));
            Assert.That(gameplayLegality.Blockers, Is.Empty);
            Assert.That(authoritativeLegality.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(authoritativeLegality.Blockers, Is.Empty);
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
                updatedTopology: updatedTopology,
                tileFeatureEvidence: TileFeatureTraversalEvidence.Empty);

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
        public void RuntimeTraversalLegalityPolicy_EvaluateDestination_TopologyBlockers_PreserveRequirementAndTileFeaturePrecedence()
        {
            var originCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Back, 0, 1);
            var unit = CreateUnit(10, originCell);
            var updatedTopology = new CubeTopologyState(FaceId.Back);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[] { unit, CreateBox(20, destinationCell) },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshot,
                EntityType.Unit,
                destinationCell,
                ignoredEntityId: unit.entityId,
                evaluatedTopology: updatedTopology,
                rotationKind: CubeRotationKind.Backward,
                updatedTopology: updatedTopology,
                tileFeatureEvidence: TileFeatureTraversalEvidence.Empty);

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.TransitionRequirement.Kind, Is.EqualTo(TransitionRequirementKind.TopologyUpdate));
            Assert.That(legality.TransitionRequirement.RotationKind, Is.EqualTo(CubeRotationKind.Backward));
            Assert.That(legality.TransitionRequirement.UpdatedTopology, Is.EqualTo(updatedTopology));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Solid));

            var unitWithTileFeature = CreateUnit(10, originCell);
            var snapshotWithTileFeature = GameplayWorldStateTestFactory.CreateBounded(
                    new[] { unitWithTileFeature, CreateBox(20, destinationCell) },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade) })
                .CreateSnapshot();

            var tileFeatureLegality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                snapshotWithTileFeature,
                EntityType.Unit,
                destinationCell,
                ignoredEntityId: unitWithTileFeature.entityId,
                evaluatedTopology: updatedTopology,
                rotationKind: CubeRotationKind.Backward,
                updatedTopology: updatedTopology,
                tileFeatureEvidence: new TileFeatureTraversalEvidence(
                    new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly) }));

            Assert.That(tileFeatureLegality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(tileFeatureLegality.Blockers.Count, Is.EqualTo(1));
            Assert.That(tileFeatureLegality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.TileFeature));
            Assert.That(tileFeatureLegality.Blockers[0].TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(tileFeatureLegality.Blockers[0].TileId, Is.EqualTo(100));
            Assert.That(
                tileFeatureLegality.Blockers.Any(blocker => blocker.Kind == LegalityBlockerKind.Solid),
                Is.False);
            Assert.That(
                tileFeatureLegality.TransitionRequirement.Kind,
                Is.EqualTo(TransitionRequirementKind.TopologyUpdate));
            Assert.That(
                tileFeatureLegality.TransitionRequirement.RotationKind,
                Is.EqualTo(CubeRotationKind.Backward));
            Assert.That(tileFeatureLegality.TransitionRequirement.UpdatedTopology, Is.EqualTo(updatedTopology));
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
                tileFeatureEvidence: new TileFeatureTraversalEvidence(definitions));

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
                tileFeatureEvidence: new TileFeatureTraversalEvidence(definitions));

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
                    TransitionRequirement.None),
                new TileFeatureTraversalEvidence(
                    new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly) }));

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.TileFeature));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeTraversalLegalityPolicy_DefaultTileFeatureEvidence_FailsFastAtPolicyEntrypoints()
        {
            var originCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var unit = CreateUnit(10, originCell);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(new[] { unit }).CreateSnapshot();
            var context = new TraverseContext(
                snapshot,
                StateQuery.BuildActorRef(snapshot, unit),
                originCell,
                destinationCell,
                snapshot.Topology,
                TransitionRequirement.None);

            Assert.Throws<InvalidOperationException>(
                () => RuntimeTraversalLegalityPolicy.EvaluateDestination(context, default));
            Assert.Throws<InvalidOperationException>(
                () => RuntimeTraversalLegalityPolicy.EvaluateTopologyTransitionTileFeatureGuard(
                    context,
                    default));
            Assert.Throws<InvalidOperationException>(
                () => RuntimeTraversalLegalityPolicy.EvaluateChargeSolidOnlyStopCell(
                    snapshot,
                    destinationCell,
                    default));
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
                TileFeatureSettlementEvidence.Empty,
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
        public void RuntimeSettlementLegalityPolicy_ImpactAfterSuppressedBarricadeEnemyDeath_ReturnsBarricadeReassertCrush()
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
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade) })
                .CreateSnapshot();

            var evaluation = RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThroughDetailed(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, box),
                    destinationCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new ImpactFollowThroughEvidence(
                    attackSourceId: 20,
                    targetId: 30,
                    destroyResolutions: new[] { CreateDestroyResolution(sourceId: 20, targetId: 30, accepted: true) }),
                new TileFeatureSettlementEvidence(
                    new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly) }));
            var legality = evaluation.LegalityResult;

            Assert.That(evaluation.OutcomeKind, Is.EqualTo(ImpactFollowThroughSettlementOutcomeKind.BarricadeReassertCrush));
            Assert.That(evaluation.Barricade.TileId, Is.EqualTo(100));
            Assert.That(evaluation.Barricade.Cell, Is.EqualTo(destinationCell));
            Assert.That(legality.Domain, Is.EqualTo(LegalityDomain.Settlement));
            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.TileFeature));
            Assert.That(legality.Blockers[0].TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(legality.Blockers[0].TileId, Is.EqualTo(100));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_JumpCrush_ActiveBarricadePrecedesJumpCrushableBox()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Front, 1, 0);
            var jumper = CreateUnit(10, sourceCell);
            jumper.boardPresence = EntityBoardPresence.Detached;
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        jumper,
                        CreateBox(20, destinationCell, BoxCapabilities.JumpCrushable),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade) })
                .CreateSnapshot();

            var evaluation = RuntimeSettlementLegalityPolicy.EvaluateJumpCrushLandingCell(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, jumper),
                    destinationCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new JumpLandingEvidence(snapshot, destinationCell),
                new TileFeatureSettlementEvidence(
                    new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly) }));

            Assert.That(evaluation.LegalityResult.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(evaluation.LegalityResult.Blockers.Count, Is.EqualTo(1));
            Assert.That(evaluation.LegalityResult.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.TileFeature));
            Assert.That(evaluation.LegalityResult.Blockers[0].TileFeatureKind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(evaluation.LegalityResult.Blockers[0].TileId, Is.EqualTo(100));
            Assert.That(evaluation.CrushedBoxEntityId, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_ImpactReservationConflict_PrecedesBarricadeReassertCrush()
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
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade) })
                .CreateSnapshot();

            var evaluation = RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThroughDetailed(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, box),
                    destinationCell,
                    snapshot.Topology,
                    SpatialState.Anchored,
                    ReservationStatus.Conflicted),
                new ImpactFollowThroughEvidence(
                    attackSourceId: 20,
                    targetId: 30,
                    destroyResolutions: new[] { CreateDestroyResolution(sourceId: 20, targetId: 30, accepted: true) }),
                new TileFeatureSettlementEvidence(
                    new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly) }));

            Assert.That(evaluation.OutcomeKind, Is.EqualTo(ImpactFollowThroughSettlementOutcomeKind.Ordinary));
            Assert.That(evaluation.LegalityResult.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(evaluation.LegalityResult.Reservation, Is.EqualTo(ReservationStatus.Conflicted));
            Assert.That(evaluation.LegalityResult.Blockers.Count, Is.EqualTo(1));
            Assert.That(evaluation.LegalityResult.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Reservation));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_LandingReservations_PrecedeActiveBarricadeAndJumpCrushableBox()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Front, 1, 0);
            var unit = CreateUnit(10, sourceCell);
            var snapshot = GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        unit,
                        CreateBox(20, destinationCell, BoxCapabilities.JumpCrushable),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade) })
                .CreateSnapshot();
            var context = new SettlementContext(
                snapshot,
                StateQuery.BuildActorRef(snapshot, unit),
                destinationCell,
                snapshot.Topology,
                SpatialState.Anchored,
                ReservationStatus.Conflicted);
            var tileFeatureEvidence = new TileFeatureSettlementEvidence(
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly) });

            var landing = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(context, tileFeatureEvidence);
            var jumpLanding = RuntimeSettlementLegalityPolicy.EvaluateJumpLandingCell(
                context,
                new JumpLandingEvidence(snapshot, destinationCell),
                tileFeatureEvidence);
            var jumpCrushLanding = RuntimeSettlementLegalityPolicy.EvaluateJumpCrushLandingCell(
                context,
                new JumpLandingEvidence(snapshot, destinationCell),
                tileFeatureEvidence);

            AssertReservationBlocker(landing);
            AssertReservationBlocker(jumpLanding);
            AssertReservationBlocker(jumpCrushLanding.LegalityResult);
            Assert.That(
                jumpCrushLanding.LegalityResult.Blockers.Any(
                    blocker => blocker.Kind == LegalityBlockerKind.TileFeature),
                Is.False);
            Assert.That(jumpCrushLanding.CrushedBoxEntityId, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_ImpactAfterSuppressedBarricadeEnemySurvives_ReturnsOrdinaryImpactStop()
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
                    new CubeTopologyState(FaceId.Floor),
                    GameplayTimingProfile.CreateDefault(),
                    new[] { CreateTileFeature(100, destinationCell, TileFeatureKind.Barricade) })
                .CreateSnapshot();

            var evaluation = RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThroughDetailed(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, box),
                    destinationCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new ImpactFollowThroughEvidence(
                    attackSourceId: 20,
                    targetId: 30,
                    destroyResolutions: new[] { CreateDestroyResolution(sourceId: 20, targetId: 30, accepted: false) }),
                new TileFeatureSettlementEvidence(
                    new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly) }));

            Assert.That(evaluation.OutcomeKind, Is.EqualTo(ImpactFollowThroughSettlementOutcomeKind.Ordinary));
            Assert.That(evaluation.LegalityResult.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(evaluation.LegalityResult.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Reservation));
        }

        [Test]
        [Category("Extended")]
        public void LegalityDiagnosticsFormatter_FormatStableSummary_ExportsStableFieldsOnly()
        {
            var blockerEntity = CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0));
            var legality = LegalityResult.Blocked(
                LegalityDomain.Traversal,
                new SurfaceCell(FaceId.Floor, 1, 0),
                new CubeTopologyState(FaceId.Floor),
                RuntimeLegalityBlockerFactory.Create(
                    new Dictionary<int, EntityState> { [20] = blockerEntity },
                    SlideStopper.CreateEntity(blockerEntity)),
                ReservationStatus.None,
                TransitionRequirement.None);

            var formatted = LegalityDiagnosticsFormatter.FormatStableSummary(legality);

            Assert.That(formatted, Does.Contain("LegalityDomain=Traversal"));
            Assert.That(formatted, Does.Contain("LegalityVerdict=Blocked"));
            Assert.That(formatted, Does.Contain("ReservationStatus=None"));
            Assert.That(formatted, Does.Contain("LegalityBlockerKinds=Solid"));
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
                new BoxImpactParticipants(attackSourceEntityId, sourceEntityId, new[] { targetEntityId }),
                new ImpactTravelGeometry(sourceCell, destinationCell, destinationCell, Direction.Right),
                new ImpactAttackHandoff(attackSourceEntityId, damageAmount: 1, sequence: 0),
                new ImpactSourceDispositionPayload(
                    ImpactDispositionPolicyKind.PushLike,
                    hasImpactSourcePoseCommit: true,
                    new ImpactSourcePoseCommit(sourceEntityId, Direction.Right),
                    hasStateChange: true,
                    state: EntityPhaseState.Sliding,
                    stateTimer: 12,
                    semanticKind: ResolvedActionSemanticKind.Push));
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

        private static void AssertReservationBlocker(LegalityResult legality)
        {
            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Reservation, Is.EqualTo(ReservationStatus.Conflicted));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Reservation));
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities = BoxCapabilities.Push)
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
                boxCapabilities = capabilities,
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
                boundEntityId: 0);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class ModifierCapabilityGeneralizationTests
    {
        [Test]
        [Category("Core")]
        public void RuntimeTraversalLegalityPolicy_EvaluateDestination_LivePhasedActor_IgnoresUnitBlocker()
        {
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                    CreateUnit(20, destinationCell, teamId: 2),
                });
            WritePhasedState(worldState, 10, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                new TraverseContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, source),
                    originCell: source.position,
                    candidateCell: destinationCell,
                    evaluationTopology: snapshot.Topology,
                    TransitionRequirement.None));

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(legality.Blockers, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void RuntimeTraversalLegalityPolicy_EvaluateDestination_LivePhasedActor_IgnoresSolidBlocker()
        {
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                    CreateBox(30, destinationCell),
                });
            WritePhasedState(worldState, 10, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                new TraverseContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, source),
                    originCell: source.position,
                    candidateCell: destinationCell,
                    evaluationTopology: snapshot.Topology,
                    TransitionRequirement.None));

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(legality.Blockers, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityQueryPolicy_LivePhasedState_SuppressesTargetSelection_ButNotGameplayQueries()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2),
                });
            WritePhasedState(worldState, 20, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetResolvedSpatialState(20, out var phased), Is.True);

            Assert.That(SpatialStateSemantics.ParticipatesInTargetSelection(phased), Is.False);
            Assert.That(GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(phased), Is.True);
            Assert.That(GameplayEntityQueryPolicy.ShouldParticipateInTargetSelection(phased), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_LivePhasedCarrier_SuppressesFreshTargetSelection_AndImpactPick()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                    CreateUnit(20, targetCell, teamId: 2),
                });
            WritePhasedState(worldState, 20, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            Assert.That(snapshot.CanBeTargetedForNewSelection(20), Is.False);
            Assert.That(snapshot.TryPickImpactTargetAt(targetCell, sourceTeamId: source.teamId, out _), Is.False);
            Assert.That(
                NearestOpponentDetectionStrategy.Instance.TryFindTarget(
                    snapshot,
                    source,
                    DetectionSettings.CreateDefaultMelee(),
                    out _),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyActionStateTargeting_TryResolveStartAction_PrefersUnphasedFreshSelection_And_DoesNotReuseCurrentEnemyLockRetention()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 2),
                    CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 1),
                    CreateUnit(30, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 1),
                });
            WritePhasedState(worldState, 20, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            Assert.That(
                EnemyActionStateTargeting.TryResolveStartAction(
                    snapshot,
                    source,
                    NearestOpponentDetectionStrategy.Instance,
                    MeleeAttackDecisionStrategy.Instance,
                    DetectionSettings.CreateDefaultMelee(),
                    AttackDecisionSettings.CreateDefaultMelee(),
                    out var target,
                    out var direction),
                Is.True);
            Assert.That(target.entityId, Is.EqualTo(30));
            Assert.That(direction, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void EnemyActionStateTargeting_CurrentEnemyLockPath_RetainsLockedTarget_WhileFreshSelectionStaysSuppressed()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 2),
                    CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 1),
                });
            WritePhasedState(worldState, 20, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);
            Assert.That(snapshot.CanBeTargetedForNewSelection(20), Is.False);

            var actionState = EnemyActionQueries.StartAction(
                default,
                EnemyActionKind.Melee,
                lockedTargetEntityId: 20,
                direction: Direction.Right,
                startTick: 1,
                windupTicks: 0);

            Assert.That(
                EnemyActionStateTargeting.TryResolveLockedTarget(
                    snapshot,
                    source,
                    actionState,
                    MeleeAttackDecisionStrategy.Instance,
                    DetectionSettings.CreateDefaultMelee(),
                    AttackDecisionSettings.CreateDefaultMelee(),
                    out var lockedTarget),
                Is.True);
            Assert.That(lockedTarget.entityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Extended")]
        public void PhasedVisiblePlayerIsNotFreshAcquireButIsLocalEngagement()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, sharedCell, teamId: 2, aiMode: EnemyAiMode.Chase),
                    CreateUnit(20, sharedCell, teamId: 1),
                });
            WritePhasedState(worldState, 20, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);
            Assert.That(snapshot.TryGetEntity(20, out var target), Is.True);

            var fresh = EnemyTargetEligibilityPolicy.EvaluateFreshAcquire(
                snapshot,
                source,
                target,
                DetectionSettings.CreateDefaultMelee());
            var localHold = EnemyTargetEligibilityPolicy.EvaluateLocalEngagementHold(
                snapshot,
                source,
                target,
                CreateDefaultCombatCapability(),
                CreatePassiveContactCapability());
            var passiveCandidate = EnemyTargetEligibilityPolicy.EvaluatePassiveContactCandidate(
                snapshot,
                source,
                target,
                CreatePassiveContactCapability());

            Assert.That(fresh.Eligible, Is.False);
            Assert.That(fresh.RejectReason, Is.EqualTo(EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState));
            Assert.That(localHold.Eligible, Is.True);
            Assert.That(localHold.AcceptReason, Is.EqualTo(EnemyTargetEligibilityAcceptReason.SameCellLocalEngagement));
            Assert.That(passiveCandidate.Eligible, Is.True);
            Assert.That(passiveCandidate.AcceptReason, Is.EqualTo(EnemyTargetEligibilityAcceptReason.PassiveContactCandidate));
        }

        [Test]
        [Category("Extended")]
        public void LockedTargetRetentionDoesNotUseFreshAcquireEligibility()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, sharedCell, teamId: 2, aiMode: EnemyAiMode.Attack),
                    CreateUnit(20, sharedCell, teamId: 1),
                });
            WritePhasedState(worldState, 20, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);
            Assert.That(snapshot.TryGetEntity(20, out var target), Is.True);

            var fresh = EnemyTargetEligibilityPolicy.EvaluateFreshAcquire(
                snapshot,
                source,
                target,
                DetectionSettings.CreateDefaultMelee());
            var retained = EnemyTargetEligibilityPolicy.EvaluateRetainLockedTarget(
                snapshot,
                source,
                target);

            Assert.That(fresh.Eligible, Is.False);
            Assert.That(fresh.RejectReason, Is.EqualTo(EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState));
            Assert.That(retained.Eligible, Is.True);
            Assert.That(retained.AcceptReason, Is.EqualTo(EnemyTargetEligibilityAcceptReason.LockedTargetRetained));
        }

        [Test]
        [Category("Extended")]
        public void FreshTargetSuppressionStillWorksForNonContactPhasedPlayer()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 2, aiMode: EnemyAiMode.Chase),
                    CreateUnit(20, new SurfaceCell(FaceId.Floor, 2, 0), teamId: 1),
                });
            WritePhasedState(worldState, 20, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);
            Assert.That(snapshot.TryGetEntity(20, out var target), Is.True);

            var fresh = EnemyTargetEligibilityPolicy.EvaluateFreshAcquire(
                snapshot,
                source,
                target,
                DetectionSettings.CreateDefaultMelee());
            var localHold = EnemyTargetEligibilityPolicy.EvaluateLocalEngagementHold(
                snapshot,
                source,
                target,
                CreateDefaultCombatCapability(),
                passiveContactCapability: null);

            Assert.That(fresh.Eligible, Is.False);
            Assert.That(fresh.RejectReason, Is.EqualTo(EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState));
            Assert.That(localHold.Eligible, Is.False);
            Assert.That(localHold.RejectReason, Is.EqualTo(EnemyTargetEligibilityRejectReason.TargetNotSameCell));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateLandingPlacement_PhasedReservationUsesSingleTerminalCellFact()
        {
            var terminalCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                });
            WritePhasedState(worldState, 10, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            var allowed = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, source),
                    terminalCell,
                    snapshot.Topology,
                    SpatialState.Phased,
                    ReservationStatus.None));
            var blocked = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, source),
                    terminalCell,
                    snapshot.Topology,
                    SpatialState.Phased,
                    ReservationStatus.Conflicted));

            Assert.That(allowed.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(blocked.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(blocked.Reservation, Is.EqualTo(ReservationStatus.Conflicted));
            Assert.That(blocked.Blockers.Count, Is.EqualTo(1));
            Assert.That(blocked.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Reservation));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateLandingPlacement_RequestedPhasedState_UsesCurrentAnchoredLikeDefault()
        {
            var terminalCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                    CreateUnit(20, terminalCell, teamId: 2),
                });
            WritePhasedState(worldState, 10, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            var legality = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, source),
                    terminalCell,
                    snapshot.Topology,
                    SpatialState.Phased));

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(legality.Blockers.Any(blocker => blocker.Kind == LegalityBlockerKind.Unit), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateLandingPlacement_RequestedPhasedState_SolidDestinationStillBlocksUnitActor()
        {
            var terminalCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                    CreateBox(30, terminalCell),
                });
            WritePhasedState(worldState, 10, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            var legality = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, source),
                    terminalCell,
                    snapshot.Topology,
                    SpatialState.Phased));

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Solid));
        }

        [TestCase("Box")]
        [TestCase("Solid")]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateLandingPlacement_NonUnitActorDestinationWithUnitStack_RemainsBlockedByUnit(string actorKind)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var terminalCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var actor = actorKind == "Box"
                ? CreateBox(10, sourceCell)
                : CreateWall(10, sourceCell);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    actor,
                    CreateUnit(20, terminalCell, teamId: 2),
                });
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            var legality = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, source),
                    terminalCell,
                    snapshot.Topology,
                    SpatialState.Anchored));

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Unit));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateLandingPlacement_UnitOnlyDestinationWithReservationConflict_BlocksByReservation()
        {
            var terminalCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                    CreateUnit(20, terminalCell, teamId: 2),
                });
            WritePhasedState(worldState, 10, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            var legality = RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, source),
                    terminalCell,
                    snapshot.Topology,
                    SpatialState.Phased,
                    ReservationStatus.Conflicted));

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Reservation));
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_PreMovementPhasedEnter_IsVisibleToMovementCollection_SameTick()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PhasedMovementProbeLogic(10, new Vector2Int(1, 0)),
                });

            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(10, out _), Is.False);

            var result = pipeline.RunTick(new TickInput(1));
            var finalEntity = result.FinalEntities.Single(entity => entity.entityId == 10);
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(finalSnapshot.TryGetPhasedState(10, out var phasedState), Is.True);
            Assert.That(phasedState.ownerKind, Is.EqualTo(PhasedRuntimeStateOwnerKind.MovementPreMovement));
            Assert.That(finalSnapshot.TryGetResolvedSpatialState(10, out var finalSpatial), Is.True);
            Assert.That(finalSpatial.Kind, Is.EqualTo(SpatialState.Phased));
            Assert.That(result.Trace.Text, Does.Contain("PreMovement.PlayerControlUpdates"));
            Assert.That(result.Trace.Text, Does.Contain("PhaseEnter|Entity=10|Tick=1"));
            Assert.That(result.Trace.Text, Does.Contain("Kind=SetPhasedState"));
            Assert.That(result.Trace.Text, Does.Contain("Origin=Plan"));
            Assert.That(result.Trace.Text, Does.Contain("Owner=MovementPreMovement"));
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_PlayerFlipWindupLiveOwner_IsInvisibleToSiblingPreMovementLogic_ButAuthoritativeAfterTick()
        {
            var observer = new SnapshotPhaseObserverLogic(observedEntityId: 10);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Flip),
                });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(
                        entityId: 10,
                        pushWindupTicks: 1,
                        pushRecoveryTicks: 0,
                        flipWindupTicks: 2,
                        flipRecoveryTicks: 0),
                    observer,
                });

            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(10, out _), Is.False);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(observer.SawPhasedStateDuringCommit, Is.False);
            Assert.That(finalSnapshot.TryGetPhasedState(10, out var phasedState), Is.True);
            Assert.That(phasedState.ownerKind, Is.EqualTo(PhasedRuntimeStateOwnerKind.MovementPreMovement));
            Assert.That(phasedState.enteredTick, Is.EqualTo(1));
            Assert.That(finalSnapshot.TryGetResolvedSpatialState(10, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Phased));
            Assert.That(finalSnapshot.CanBeTargetedForNewSelection(10), Is.False);
            Assert.That(result.Trace.Text, Does.Contain("PhaseEnter|Entity=10|Tick=1|Owner=MovementPreMovement|Rule=PlayerFlipWindup"));
            Assert.That(result.Trace.Text, Does.Contain("Kind=SetPhasedState"));
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_PlayerFlipWindupLiveOwner_SustainsAcrossWindup_AndClearsOnExecuteTick()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Flip),
                });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(
                        entityId: 10,
                        pushWindupTicks: 1,
                        pushRecoveryTicks: 0,
                        flipWindupTicks: 2,
                        flipRecoveryTicks: 0),
                });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var afterEnter = worldState.CreateSnapshot();
            Assert.That(afterEnter.TryGetPhasedState(10, out var enteredState), Is.True);
            Assert.That(enteredState.sequence, Is.EqualTo(1));
            Assert.That(enteredState.enteredTick, Is.EqualTo(1));

            var sustainResult = pipeline.RunTick(new TickInput(2));
            var afterSustain = worldState.CreateSnapshot();
            Assert.That(afterSustain.TryGetPhasedState(10, out var sustainedState), Is.True);
            Assert.That(sustainedState.sequence, Is.EqualTo(1));
            Assert.That(sustainedState.enteredTick, Is.EqualTo(1));
            Assert.That(sustainResult.Trace.Text, Does.Not.Contain("PhaseEnter|Entity=10"));
            Assert.That(sustainResult.Trace.Text, Does.Not.Contain("PhaseExit|Entity=10"));
            Assert.That(sustainResult.Trace.Text, Does.Not.Contain("Kind=SetPhasedState"));

            var clearResult = pipeline.RunTick(new TickInput(3));
            var afterClear = worldState.CreateSnapshot();
            Assert.That(afterClear.TryGetPhasedState(10, out _), Is.False);
            Assert.That(clearResult.Trace.Text, Does.Contain("PhaseExit|Entity=10|Tick=3|Owner=MovementPreMovement|Reason=PlayerFlipWindupEnded"));
            Assert.That(clearResult.Trace.Text, Does.Contain("Kind=SetPhasedState"));
            Assert.That(clearResult.Trace.Text, Does.Contain("Active=0"));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeTraversalLegalityPolicy_EvaluateDestination_SystemValidationPhasedActor_IgnoresUnitBlocker()
        {
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                    CreateUnit(20, destinationCell, teamId: 2),
                });
            WritePhasedState(worldState, 10, PhasedRuntimeStateQueries.BeginSystemPreMovementValidation(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            var legality = RuntimeTraversalLegalityPolicy.EvaluateDestination(
                new TraverseContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, source),
                    originCell: source.position,
                    candidateCell: destinationCell,
                    evaluationTopology: snapshot.Topology,
                    TransitionRequirement.None));

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(legality.Blockers, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_SystemValidationPhasedCarrier_SuppressesFreshTargetSelection_WithoutEnemyLockRetention()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                    CreateUnit(20, targetCell, teamId: 2),
                });
            WritePhasedState(worldState, 20, PhasedRuntimeStateQueries.BeginSystemPreMovementValidation(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var source), Is.True);

            Assert.That(snapshot.CanBeTargetedForNewSelection(20), Is.False);
            Assert.That(snapshot.TryPickImpactTargetAt(targetCell, sourceTeamId: 1, out _), Is.False);
            Assert.That(
                EnemyActionStateTargeting.TryResolveStartAction(
                    snapshot,
                    source,
                    NearestOpponentDetectionStrategy.Instance,
                    MeleeAttackDecisionStrategy.Instance,
                    DetectionSettings.CreateDefaultMelee(),
                    AttackDecisionSettings.CreateDefaultMelee(),
                    out var target,
                    out _),
                Is.False);
            Assert.That(target, Is.EqualTo(default(EntityState)));
        }

        [Test]
        [Category("Extended")]
        public void SystemPreMovementValidationSource_SustainsAcrossWindow_And_ClearsWhenWindowCloses()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                });
            var logic = CreateSystemPreMovementValidationLogic(entityId: 10, enterTick: 1, exitTickExclusive: 3);

            var enterUpdates = new List<string>();
            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1),
                worldState.CreateWriteContext(),
                enterUpdates,
                new List<PlayerActionTransition>());

            Assert.That(
                enterUpdates,
                Is.EqualTo(new[]
                {
                    "PhaseEnter|Entity=10|Tick=1|Owner=SystemPreMovementValidation|Rule=SystemPreMovementValidationWindow",
                }));
            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(10, out var enteredState), Is.True);
            Assert.That(enteredState.sequence, Is.EqualTo(1));

            var sustainUpdates = new List<string>();
            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2),
                worldState.CreateWriteContext(),
                sustainUpdates,
                new List<PlayerActionTransition>());
            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(10, out var sustainedState), Is.True);
            Assert.That(sustainedState.sequence, Is.EqualTo(1));
            Assert.That(sustainUpdates, Is.Empty);

            var clearUpdates = new List<string>();
            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(3),
                worldState.CreateWriteContext(),
                clearUpdates,
                new List<PlayerActionTransition>());
            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(10, out _), Is.False);
            Assert.That(
                clearUpdates,
                Is.EqualTo(new[]
                {
                    "PhaseExit|Entity=10|Tick=3|Owner=SystemPreMovementValidation|Reason=ValidationWindowClosed",
                }));
        }

        [Test]
        [Category("Extended")]
        public void SystemPreMovementValidationSource_ForeignOwnerConflict_Throws()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                });
            WritePhasedState(worldState, 10, PhasedRuntimeStateQueries.ForceDebug(default, tickIndex: 0));
            var logic = CreateSystemPreMovementValidationLogic(entityId: 10, enterTick: 1, exitTickExclusive: 3);

            var exception = Assert.Throws<InvalidOperationException>(
                () => logic.CommitPreMovementState(
                    worldState.CreateSnapshot(),
                    new TickInput(1),
                    worldState.CreateWriteContext(),
                    new List<string>(),
                    new List<PlayerActionTransition>()));
            Assert.That(exception?.Message, Does.Contain("cannot enter system validation phased state while owner DebugForced is still active"));
        }

        [Test]
        [Category("Extended")]
        public void SystemPreMovementValidationSource_ForcedCancel_WhenEntityHpDropsToZero()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1),
                });
            var logic = CreateSystemPreMovementValidationLogic(entityId: 10, enterTick: 1, exitTickExclusive: 3);

            var enterUpdates = new List<string>();
            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(1),
                worldState.CreateWriteContext(),
                enterUpdates,
                new List<PlayerActionTransition>());
            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(10, out _), Is.True);
            Assert.That(
                enterUpdates,
                Is.EqualTo(new[]
                {
                    "PhaseEnter|Entity=10|Tick=1|Owner=SystemPreMovementValidation|Rule=SystemPreMovementValidationWindow",
                }));

            worldState.CreateWriteContext().ApplyDamage(10, 3);
            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(10, out _), Is.False);

            var postDamageUpdates = new List<string>();
            logic.CommitPreMovementState(
                worldState.CreateSnapshot(),
                new TickInput(2),
                worldState.CreateWriteContext(),
                postDamageUpdates,
                new List<PlayerActionTransition>());
            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(10, out _), Is.False);
            Assert.That(postDamageUpdates, Is.Empty);
        }

        private static void WritePhasedState(WorldState worldState, int entityId, PhasedRuntimeState state)
        {
            var writeContext = worldState.CreateWriteContext();
            ((IPhasedStateCommitContext)writeContext).SetPhasedState(entityId, state);
        }

        private static IPreMovementStateLogic CreateSystemPreMovementValidationLogic(
            int entityId,
            int enterTick,
            int exitTickExclusive)
        {
            var type = typeof(WorldSnapshot).Assembly.GetType(
                "Game.Feature.Gameplay.Entities.SystemPreMovementValidationLogic",
                throwOnError: true);
            var instance = Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { entityId, enterTick, exitTickExclusive },
                culture: null);
            if (instance is not IPreMovementStateLogic logic)
            {
                throw new InvalidOperationException("Failed to construct SystemPreMovementValidationLogic as an IPreMovementStateLogic.");
            }

            return logic;
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int teamId,
            EnemyAiMode aiMode = EnemyAiMode.None)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
            };
        }

        private static EnemyCombatCapabilityRuntime CreateDefaultCombatCapability()
        {
            return new EnemyCombatCapabilityRuntime(
                AttackDecisionStrategyKind.Melee,
                AttackDecisionSettings.CreateDefaultMelee(),
                EnemyAttackTimingSettings.CreateDefaultMelee(),
                MeleeAttackDecisionStrategy.Instance);
        }

        private static EnemyPassiveContactCapabilityRuntime CreatePassiveContactCapability()
        {
            return new EnemyPassiveContactCapabilityRuntime(
                AttackDecisionStrategyKind.ContactSameCell,
                AttackDecisionSettings.CreateDefaultMelee(),
                ContactSameCellAttackDecisionStrategy.Instance);
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
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boxCapabilities = capabilities,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private sealed class SnapshotPhaseObserverLogic : IPreMovementStateLogic
        {
            private readonly int _observedEntityId;

            public SnapshotPhaseObserverLogic(int observedEntityId)
            {
                _observedEntityId = observedEntityId;
            }

            public bool SawPhasedStateDuringCommit { get; private set; }

            public void CommitPreMovementState(
                WorldSnapshot snapshot,
                in TickInput input,
                IPreMovementStateCommitContext writeContext,
                List<string> updates,
                List<PlayerActionTransition> actionTransitions)
            {
                SawPhasedStateDuringCommit = snapshot.TryGetPhasedState(_observedEntityId, out _);
                updates.Add(
                    $"ObservedPhaseDuringPreMovement|Entity={_observedEntityId}|Tick={input.TickIndex}|Visible={(SawPhasedStateDuringCommit ? 1 : 0)}");
            }
        }

        private sealed class PhasedMovementProbeLogic : IPreMovementStateLogic, IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly Vector2Int _destination;
            private readonly int _entityId;

            public PhasedMovementProbeLogic(int entityId, Vector2Int destination)
            {
                _entityId = entityId;
                _destination = destination;
            }

            public int ControlledEntityId => _entityId;

            public void CommitPreMovementState(
                WorldSnapshot snapshot,
                in TickInput input,
                IPreMovementStateCommitContext writeContext,
                List<string> updates,
                List<PlayerActionTransition> actionTransitions)
            {
                var phasedWriteContext = (IPhasedStateCommitContext)writeContext;
                var previousState = snapshot.TryGetPhasedState(_entityId, out var currentState)
                    ? currentState
                    : default;
                phasedWriteContext.SetPhasedState(
                    _entityId,
                    PhasedRuntimeStateQueries.BeginMovementPreMovement(previousState, input.TickIndex));
                updates.Add($"PhaseEnter|Entity={_entityId}|Tick={input.TickIndex}");
            }

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (snapshot.TryGetResolvedSpatialState(_entityId, out var spatialState) &&
                    spatialState.Kind == SpatialState.Phased)
                {
                    buffer.Add(new RawMovementIntent(_entityId, priority: 100, destination: _destination));
                }
            }
        }

    }
}

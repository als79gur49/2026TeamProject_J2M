using System.Collections.Generic;
using System.Linq;
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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

            Assert.That(legality.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(legality.Blockers.Count, Is.EqualTo(1));
            Assert.That(legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Unit));
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
                        pushContactThresholdTicks: GameplayTimingProfile.DefaultPlayerPushContactThresholdTicks,
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
                        pushContactThresholdTicks: GameplayTimingProfile.DefaultPlayerPushContactThresholdTicks,
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

        private static void WritePhasedState(WorldState worldState, int entityId, PhasedRuntimeState state)
        {
            var writeContext = worldState.CreateWriteContext();
            ((IPhasedStateCommitContext)writeContext).SetPhasedState(entityId, state);
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position, int teamId)
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
            };
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

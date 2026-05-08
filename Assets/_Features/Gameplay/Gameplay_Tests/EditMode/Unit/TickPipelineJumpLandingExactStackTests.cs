using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineJumpLandingExactStackTests
    {
        [Test]
        [Category("Core")]
        public void BoxCapabilities_FlagValues_AreStable()
        {
            Assert.That((int)BoxCapabilities.Push, Is.EqualTo(1));
            Assert.That((int)BoxCapabilities.Flip, Is.EqualTo(2));
            Assert.That((int)BoxCapabilities.Item, Is.EqualTo(4));
            Assert.That((int)BoxCapabilities.Destroy, Is.EqualTo(8));
            Assert.That((int)BoxCapabilities.JumpCrushable, Is.EqualTo(16));
        }

        [Test]
        [Category("Extended")]
        public void EvaluateJumpLandingCell_LockedTargetWithUnitStack_Allows()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, targetCell, teamId: 1),
                CreateUnit(20, targetCell, teamId: 2),
                CreateUnit(40, sourceCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
            });
            var snapshot = worldState.CreateSnapshot();

            var result = RuntimeSettlementLegalityPolicy.EvaluateJumpLandingCell(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, 40, EntityType.Unit),
                    targetCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new JumpLandingEvidence(snapshot, targetCell));

            Assert.That(result.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
        }

        [Test]
        [Category("Core")]
        public void EvaluateJumpCrushLandingCell_JumpCrushableBoxOnLockedTarget_AllowsAndReportsBox()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(40, sourceCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
                CreateBox(50, targetCell, BoxCapabilities.JumpCrushable),
            });
            var snapshot = worldState.CreateSnapshot();

            var evaluation = RuntimeSettlementLegalityPolicy.EvaluateJumpCrushLandingCell(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, 40, EntityType.Unit),
                    targetCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new JumpLandingEvidence(snapshot, targetCell));

            Assert.That(evaluation.LegalityResult.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(evaluation.CrushedBoxEntityId, Is.EqualTo(50));
        }

        [Test]
        [Category("Core")]
        public void ResolveJumpLanding_CrushBoxAndLand_UsesCurrentResolveSnapshotBoxId()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var stalePlanBoxCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(40, sourceCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
                CreateBox(50, stalePlanBoxCell, BoxCapabilities.JumpCrushable),
                CreateBox(51, targetCell, BoxCapabilities.JumpCrushable),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);
            var snapshot = worldState.CreateSnapshot();
            var actionPlanId = 7;
            var contestId = 3;
            var successState = new EnemyJumpRuntimeState
            {
                phase = EnemyJumpPhase.Cooldown,
                sequence = 1,
                sourceCell = sourceCell,
                lockedTargetCell = targetCell,
                windupEndTick = 1,
                landingTick = 2,
                cooldownRemainingTicks = 1,
            };
            var retryState = successState;
            retryState.phase = EnemyJumpPhase.Airborne;
            retryState.retryCount = 1;
            var payloads = new Dictionary<int, JumpLandingActionPlanPayload>
            {
                {
                    actionPlanId,
                    new JumpLandingActionPlanPayload(
                        actionPlanId,
                        sourceActorEntityId: 40,
                        priority: 0,
                        JumpLandingKind.CrushBoxAndLand,
                        targetCell,
                        contestedTargetEntityId: 0,
                        landingRule: "TargetCrushBox",
                        successState,
                        retryState)
                },
            };
            var contests = new[]
            {
                new Contest(
                    contestId,
                    ContestKind.Space,
                    actionPlanId,
                    sourceId: 40,
                    priority: 0,
                    affectedEntityId: 50,
                    affectedCell: targetCell,
                    hasAffectedCell: true,
                    localActionIndex: 0),
            };
            var movementRecords = new List<ResolutionRecord>();
            var movementCommitEvents = new List<string>();

            var batch = ResolveJumpLandingSpaceContestsCanonical(
                pipeline,
                snapshot,
                snapshot,
                new[] { actionPlanId },
                payloads,
                contests,
                new MovementReservationBook(),
                movementRecords,
                movementCommitEvents);
            var operations = batch.Operations.ToArray();
            var projectedWorld = new ProjectedWorld(snapshot);
            projectedWorld.ApplyBatch(batch);
            var resolvedSnapshot = projectedWorld.CreateSnapshot();

            Assert.That(operations.Length, Is.EqualTo(5));
            Assert.That(operations[0].Kind, Is.EqualTo(FinalizationOperationKind.SetBoardPresence));
            Assert.That(operations[0].EntityId, Is.EqualTo(51));
            Assert.That(operations[0].BoardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(operations[1].Kind, Is.EqualTo(FinalizationOperationKind.MarkDestroy));
            Assert.That(operations[1].EntityId, Is.EqualTo(51));
            Assert.That(operations[2].Kind, Is.EqualTo(FinalizationOperationKind.MoveEntity));
            Assert.That(operations[2].EntityId, Is.EqualTo(40));
            Assert.That(operations[2].Destination, Is.EqualTo(targetCell));
            Assert.That(operations[3].Kind, Is.EqualTo(FinalizationOperationKind.SetBoardPresence));
            Assert.That(operations[3].EntityId, Is.EqualTo(40));
            Assert.That(operations[3].BoardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(operations[4].Kind, Is.EqualTo(FinalizationOperationKind.SetEnemyJumpState));
            Assert.That(operations[4].EntityId, Is.EqualTo(40));
            Assert.That(operations.Any(operation => operation.EntityId == 50), Is.False);

            Assert.That(resolvedSnapshot.TryGetEntity(51, out var currentBox), Is.True);
            Assert.That(currentBox.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(currentBox.markedForDeath, Is.True);
            Assert.That(resolvedSnapshot.TryGetBoxAt(targetCell, out _), Is.False);
            Assert.That(resolvedSnapshot.TryGetEntity(50, out var staleBox), Is.True);
            Assert.That(staleBox.markedForDeath, Is.False);
            Assert.That(staleBox.position, Is.EqualTo(stalePlanBoxCell));
            Assert.That(resolvedSnapshot.TryGetEntity(40, out var jumper), Is.True);
            Assert.That(jumper.position, Is.EqualTo(targetCell));
            Assert.That(jumper.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(movementRecords.Single().Accepted, Is.True);
            Assert.That(movementCommitEvents.Single(), Does.Contain("Target=51"));
        }

        [Test]
        [Category("Extended")]
        [TestCase((int)BoxCapabilities.None)]
        [TestCase((int)BoxCapabilities.Destroy)]
        [TestCase((int)BoxCapabilities.Item)]
        public void EvaluateJumpCrushLandingCell_NonJumpCrushableBoxOnLockedTarget_Blocks(int boxCapabilityValue)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(40, sourceCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
                CreateBox(50, targetCell, (BoxCapabilities)boxCapabilityValue),
            });
            var snapshot = worldState.CreateSnapshot();

            var evaluation = RuntimeSettlementLegalityPolicy.EvaluateJumpCrushLandingCell(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, 40, EntityType.Unit),
                    targetCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new JumpLandingEvidence(snapshot, targetCell));

            Assert.That(evaluation.LegalityResult.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(evaluation.CrushedBoxEntityId, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void EvaluateJumpLandingCell_NonLockedTargetWithUnitStack_Blocks()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, targetCell, teamId: 1),
                CreateUnit(40, sourceCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
            });
            var snapshot = worldState.CreateSnapshot();

            var result = RuntimeSettlementLegalityPolicy.EvaluateJumpLandingCell(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, 40, EntityType.Unit),
                    targetCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new JumpLandingEvidence(snapshot, lockedTargetCell));

            Assert.That(result.Verdict, Is.EqualTo(LegalityVerdict.Blocked));
        }

        [Test]
        [Category("Extended")]
        public void EvaluateJumpLandingCell_LockedTargetWithoutPlayerControl_Allows()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, targetCell, teamId: 1),
                CreateUnit(40, sourceCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
            });
            var snapshot = worldState.CreateSnapshot();

            var result = RuntimeSettlementLegalityPolicy.EvaluateJumpLandingCell(
                new SettlementContext(
                    snapshot,
                    StateQuery.BuildActorRef(snapshot, 40, EntityType.Unit),
                    targetCell,
                    snapshot.Topology,
                    SpatialState.Anchored),
                new JumpLandingEvidence(snapshot, targetCell));

            Assert.That(result.Verdict, Is.EqualTo(LegalityVerdict.Allowed));
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static FinalizationBatch ResolveJumpLandingSpaceContestsCanonical(
            TickPipeline pipeline,
            WorldSnapshot movementSnapshot,
            WorldSnapshot damageProjectionSnapshot,
            IReadOnlyList<int> orderedJumpLandingActionPlanIds,
            IReadOnlyDictionary<int, JumpLandingActionPlanPayload> jumpLandingActionPlanPayloads,
            IReadOnlyList<Contest> jumpLandingSpaceContests,
            MovementReservationBook reservationBook,
            List<ResolutionRecord> movementResolutionRecords,
            List<string> movementCommitEvents)
        {
            var method = typeof(TickPipeline).GetMethod(
                "ResolveJumpLandingSpaceContestsCanonical",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            try
            {
                return (FinalizationBatch)method.Invoke(
                    pipeline,
                    new object[]
                    {
                        movementSnapshot,
                        damageProjectionSnapshot,
                        orderedJumpLandingActionPlanIds,
                        jumpLandingActionPlanPayloads,
                        jumpLandingSpaceContests,
                        reservationBook,
                        movementResolutionRecords,
                        movementCommitEvents,
                    });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int teamId,
            int hp = 3,
            bool markedForDeath = false,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp > 0 ? hp : 1,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = Direction.Right,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
                spawnTick = 0,
                aiMode = EnemyAiMode.None,
                aiStateTimer = 0,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities)
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
                stateTimer = 0,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                boxCapabilities = capabilities,
                aiMode = EnemyAiMode.None,
                aiStateTimer = 0,
            };
        }
    }
}

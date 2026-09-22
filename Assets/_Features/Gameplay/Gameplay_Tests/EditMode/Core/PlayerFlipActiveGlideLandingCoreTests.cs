using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class PlayerFlipActiveGlideLandingCoreTests
    {
        private static readonly SurfaceCell PlayerCell = new(FaceId.Floor, 2, 1);
        private static readonly SurfaceCell TargetCell = new(FaceId.Floor, 1, 1);
        private static readonly SurfaceCell LandingCell = new(FaceId.Floor, 3, 1);

        [Test]
        [Category("Core")]
        public void ActiveGlideEnemyOnFlipLandingCell_StartPendingAndExecute_UseBoxFlipPolicy()
        {
            var worldState = CreateStandardWorld(new[] { CreateActiveGlideEnemy(5, LandingCell) });
            SetActiveGlide(worldState, 5);
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);

            Assert.That(
                PlayerControlQueries.TryResolveFlipTarget(
                    snapshot,
                    player,
                    Direction.Left,
                    tickIndex: 1,
                    out var target),
                Is.True);
            Assert.That(target.TargetEntityId, Is.EqualTo(20));
            Assert.That(
                PlayerControlQueries.TryResolveBlockedFlipLandingTarget(
                    snapshot,
                    player,
                    player.position,
                    Direction.Left,
                    tickIndex: 1,
                    out _),
                Is.False);

            var pipeline = CreatePlayerPipeline(worldState);
            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var pendingSnapshot = worldState.CreateSnapshot();
            Assert.That(pendingSnapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(controlState.activeAction.executionAttempted, Is.False);
            Assert.That(
                PlayerControlQueries.CanPendingActionStillExecute(
                    pendingSnapshot,
                    pendingSnapshot.TryGetEntity(10, out var pendingPlayer) ? pendingPlayer : default,
                    controlState.activeAction,
                    tickIndex: 2),
                Is.True);
            Assert.That(startResult.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
            Assert.That(startResult.PresentationData.PlayerActionAttemptSignals, Is.Empty);

            var executeResult = pipeline.RunTick(new TickInput(2));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(
                executeResult.MovementPhaseResult.RawIntents,
                Has.Exactly(1).Matches<RawMovementIntent>(
                    intent => intent.SourceId == 10 && intent.CommandKind == MovementCommandKind.Flip));
            Assert.That(executeResult.MovementPhaseResult.RejectedReasons, Has.None.Contains("FlipLandingBlocked"));
            Assert.That(executeResult.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(executeResult.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(LandingCell, out var landedSolid), Is.True);
            Assert.That(landedSolid.Kind, Is.EqualTo(SolidKind.Box));
            Assert.That(landedSolid.Entity.entityId, Is.EqualTo(20));
            Assert.That(finalSnapshot.TryGetEntity(5, out var activeGlider), Is.True);
            Assert.That(activeGlider.markedForDeath, Is.False);
            Assert.That(finalSnapshot.TryGetActiveEnemyGlideState(5, out _), Is.True);
        }

        [Test]
        [Category("Core")]
        public void PendingFlip_ActiveGlideEnemyRemainingOnLandingCell_DoesNotCancelBeforeExecute()
        {
            var worldState = CreateStandardWorld(new[] { CreateActiveGlideEnemy(5, LandingCell) });
            SetActiveGlide(worldState, 5);
            var pipeline = CreatePlayerPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            worldState.CreateWriteContext().MoveEntity(5, LandingCell);
            var pendingSnapshot = worldState.CreateSnapshot();
            Assert.That(pendingSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(pendingSnapshot.TryGetEntity(5, out var pendingGlider), Is.True);
            Assert.That(pendingGlider.position, Is.EqualTo(LandingCell));
            Assert.That(pendingSnapshot.TryGetPlayerControlState(10, out var pendingState), Is.True);
            Assert.That(
                PlayerControlQueries.CanPendingActionStillExecute(
                    pendingSnapshot,
                    player,
                    pendingState.activeAction,
                    tickIndex: 2),
                Is.True);

            var executeResult = pipeline.RunTick(new TickInput(2));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(executeResult.MovementPhaseResult.RejectedReasons, Has.None.Contains("FlipLandingBlocked"));
            Assert.That(executeResult.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(LandingCell, out var landedSolid), Is.True);
            Assert.That(landedSolid.Entity.entityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void PendingFlip_NormalHostileUnitEnteringLandingCell_StaysAlignedWithBoxImpactExecutePolicy()
        {
            var worldState = CreateStandardWorld();
            var pipeline = CreatePlayerPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            worldState.CreateWriteContext().SpawnEntity(CreateEnemy(5, LandingCell, hp: 3));
            var pendingSnapshot = worldState.CreateSnapshot();
            Assert.That(pendingSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(pendingSnapshot.TryGetPlayerControlState(10, out var pendingState), Is.True);
            Assert.That(
                PlayerControlQueries.CanPendingActionStillExecute(
                    pendingSnapshot,
                    player,
                    pendingState.activeAction,
                    tickIndex: 2),
                Is.True);

            var executeResult = pipeline.RunTick(new TickInput(2));

            Assert.That(executeResult.MovementPhaseResult.RejectedReasons, Has.None.Contains("Source=10"));
            Assert.That(executeResult.MovementPhaseResult.CommitEvents, Has.None.Contains("MoveCommitted").And.Contains("E=20"));
            Assert.That(executeResult.AttackPhaseResult.DrainedImpactReservations, Has.Count.EqualTo(1));
            Assert.That(executeResult.PresentationData.FlipImpactSignals, Has.Count.EqualTo(1));
            Assert.That(executeResult.PresentationData.FlipImpactSignals[0].ImpactTargetEntityId, Is.EqualTo(5));
        }

        [Test]
        [Category("Core")]
        public void PendingFlip_LethalFollowThrough_FinalPlayerFacing_IsResultFacing()
        {
            var worldState = CreateStandardWorld();
            var pipeline = CreatePlayerPipeline(worldState);

            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            worldState.CreateWriteContext().SpawnEntity(CreateEnemy(5, LandingCell, hp: 1));
            var executeResult = pipeline.RunTick(new TickInput(2));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(startResult.PresentationData.PlayerFlipResultTurnSignals, Has.Count.EqualTo(1));
            Assert.That(startResult.PresentationData.PlayerFlipResultTurnSignals[0].ContactFacing, Is.EqualTo(Direction.Left));
            Assert.That(startResult.PresentationData.PlayerFlipResultTurnSignals[0].ResultFacing, Is.EqualTo(Direction.Right));
            Assert.That(executeResult.Trace.Text, Does.Contain("FlipResultFacingCommitted"));
            Assert.That(finalSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.facing, Is.EqualTo(Direction.Right));
            Assert.That(executeResult.FinalEntities.Single(entity => entity.entityId == 10).facing, Is.EqualTo(Direction.Right));
            Assert.That(finalSnapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(LandingCell));
            Assert.That(box.facing, Is.EqualTo(Direction.Right));
            Assert.That(finalSnapshot.TryGetEntity(5, out _), Is.False);
            Assert.That(executeResult.MovementPhaseResult.ImpactDispositionRecords.Single().DispositionKind, Is.EqualTo(ImpactDispositionKind.FollowThrough));
        }

        [Test]
        [Category("Core")]
        public void PendingFlip_LethalFollowThrough_DoesNotWriteActorContactFacing()
        {
            var worldState = CreateStandardWorld();
            var pipeline = CreatePlayerPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            worldState.CreateWriteContext().SpawnEntity(CreateEnemy(5, LandingCell, hp: 1));
            var executeResult = pipeline.RunTick(new TickInput(2));

            Assert.That(
                executeResult.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetFacing &&
                    operation.EntityId == 10 &&
                    operation.Facing == Direction.Left),
                Is.False);
            Assert.That(
                executeResult.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == 20 &&
                    operation.Destination == LandingCell),
                Is.True);
            Assert.That(
                executeResult.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetFacing &&
                    operation.EntityId == 20 &&
                    operation.Facing == Direction.Right),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void FlipLandingWall_RejectsAtStartAsBoxFlipHardBlocker()
        {
            AssertStartRejectedByLandingBlocker(
                CreateStandardWorld(new[] { CreateWall(30, LandingCell) }),
                expectBlockedAttempt: true);
        }

        [Test]
        [Category("Core")]
        public void FlipLandingBox_RejectsAtStartAsBoxFlipHardBlocker()
        {
            AssertStartRejectedByLandingBlocker(
                CreateStandardWorld(new[] { CreateBox(30, LandingCell, BoxCapabilities.Flip) }),
                expectBlockedAttempt: true);
        }

        [Test]
        [Category("Core")]
        public void FlipLandingBoardEdge_RejectsAtLocalGeometryBeforeLandingPolicy()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, playerCell),
                    CreateBox(20, targetCell, BoxCapabilities.Flip),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)));

            AssertStartRejectedByLandingBlocker(worldState, playerCell, Direction.Right, expectBlockedAttempt: false);
        }

        [Test]
        [Category("Core")]
        public void FlipLockedTarget_RejectsAtStartWithoutUsingLandingPolicyAsBypass()
        {
            var worldState = CreateStandardWorld();
            worldState.CreateWriteContext().SetBoxInteractionLockState(
                20,
                new BoxInteractionLockState(
                    sourceEntityId: 99,
                    sourceEffectIndex: 0,
                    expiresTickExclusive: 5,
                    blocksPush: false,
                    blocksFlip: true));

            AssertStartRejectedByLandingBlocker(worldState, expectBlockedAttempt: false);
        }

        [Test]
        [Category("Core")]
        public void FlipLandingAllyUnit_RejectsAtStartAndDoesNotBroadenBeyondExecutePolicy()
        {
            AssertStartRejectedByLandingBlocker(
                CreateStandardWorld(new[] { CreateUnit(30, LandingCell, teamId: 1, UnitRole.Player, EnemyAiMode.None) }),
                expectBlockedAttempt: true);
        }

        [Test]
        [Category("Core")]
        public void FlipLandingPhasedButNotActiveGlideEnemy_RejectsAtStartAndDoesNotTreatAsGlideException()
        {
            var worldState = CreateStandardWorld(new[] { CreateEnemy(5, LandingCell) });
            worldState.CreateWriteContext().SetPhasedState(
                5,
                PhasedRuntimeStateQueries.ForceDebug(default, tickIndex: 1));

            AssertStartRejectedByLandingBlocker(worldState, expectBlockedAttempt: true);
        }

        [Test]
        [Category("Core")]
        public void FlipLandingAirborneEnemyDetachedFromBoard_UsesCurrentEmptyLandingContract()
        {
            var airborne = CreateEnemy(5, LandingCell);
            airborne.boardPresence = EntityBoardPresence.Detached;
            var worldState = CreateStandardWorld(new[] { airborne });
            worldState.CreateWriteContext().SetEnemyJumpState(
                5,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = LandingCell,
                    lockedTargetCell = LandingCell,
                    windupEndTick = 0,
                    landingTick = 10,
                });
            var pipeline = CreatePlayerPipeline(worldState);

            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var executeResult = pipeline.RunTick(new TickInput(2));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(startResult.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
            Assert.That(executeResult.MovementPhaseResult.RejectedReasons, Has.None.Contains("Source=10"));
            Assert.That(executeResult.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(LandingCell, out var landedSolid), Is.True);
            Assert.That(landedSolid.Entity.entityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void FlipOnInactiveFace_RejectsBeforeLandingPolicyByExistingTopologyGate()
        {
            var playerCell = new SurfaceCell(FaceId.Back, 2, 1);
            var targetCell = new SurfaceCell(FaceId.Back, 1, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, playerCell),
                    CreateBox(20, targetCell, BoxCapabilities.Flip),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(5, 5)));

            AssertStartRejectedByLandingBlocker(worldState, playerCell, Direction.Left, expectBlockedAttempt: false);
        }

        private static void AssertStartRejectedByLandingBlocker(
            WorldState worldState,
            bool expectBlockedAttempt)
        {
            AssertStartRejectedByLandingBlocker(worldState, PlayerCell, Direction.Left, expectBlockedAttempt);
        }

        private static void AssertStartRejectedByLandingBlocker(
            WorldState worldState,
            SurfaceCell playerCell,
            Direction direction,
            bool expectBlockedAttempt)
        {
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);

            Assert.That(
                PlayerControlQueries.TryResolveFlipTarget(
                    snapshot,
                    player,
                    direction,
                    tickIndex: 1,
                    out _),
                Is.False);
            Assert.That(
                PlayerControlQueries.TryResolveBlockedFlipLandingTarget(
                    snapshot,
                    player,
                    playerCell,
                    direction,
                    tickIndex: 1,
                    out _),
                Is.EqualTo(expectBlockedAttempt));

            var result = CreatePlayerPipeline(worldState).RunTick(new TickInput(1, PlayerTickCommand.Flip(direction)));
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.MovementPhaseResult.RawIntents, Is.Empty);
        }

        private static WorldState CreateStandardWorld(
            IEnumerable<EntityState> landingEntities = null)
        {
            var entities = new List<EntityState>
            {
                CreatePlayer(10, PlayerCell),
                CreateBox(20, TargetCell, BoxCapabilities.Flip),
            };
            if (landingEntities != null)
            {
                entities.AddRange(landingEntities);
            }

            return CreateWorldState(
                entities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(5, 5)));
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> entities,
            BoardBounds boardBounds)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
        }

        private static TickPipeline CreatePlayerPipeline(WorldState worldState)
        {
            return new GameplayBootstrapper(GameplayEntityLogicProviderFactory.CreateDefault(CreateNonAttackingRuntime()))
                .CreateTickPipeline(
                    worldState,
                    new IEntityLogic[]
                    {
                        new PlayerLogic(10),
                    });
        }

        private static EnemyAiRuntimeDefinition CreateNonAttackingRuntime()
        {
            return new EnemyAiRuntimeDefinition(
                EnemyAiCommonSettings.CreateStandard(),
                PatrolSettings.CreateDefault(),
                DetectionSettings.CreateStandardEnemyDetection(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateAdjacentRange(),
                EnemyAttackTimingSettings.CreateImmediate(),
                EnemyLocomotionTimingSettings.CreateImmediate(),
                ForwardPatrolStrategy.Instance,
                NoDetectionStrategy.Instance,
                AxisPriorityChaseStrategy.Instance,
                NoAttackDecisionStrategy.Instance,
                DefaultEnemyAiStateResolver.Instance);
        }

        private static void SetActiveGlide(WorldState worldState, int entityId)
        {
            worldState.CreateWriteContext().SetEnemyGlideState(
                entityId,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Active,
                    sequence: 1,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: 8,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 0,
                    windupTicks: 0,
                    durationTicks: 8,
                    recoveryTicks: 0,
                    cooldownTicks: 0,
                    lastExitedTick: 0));
        }

        private static EntityState CreatePlayer(int entityId, SurfaceCell position)
        {
            return CreateUnit(entityId, position, teamId: 1, UnitRole.Player, EnemyAiMode.None);
        }

        private static EntityState CreateActiveGlideEnemy(int entityId, SurfaceCell position)
        {
            return CreateEnemy(entityId, position);
        }

        private static EntityState CreateEnemy(int entityId, SurfaceCell position, int hp = 3)
        {
            return CreateUnit(entityId, position, teamId: 2, UnitRole.Enemy, EnemyAiMode.None, hp);
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int teamId,
            UnitRole role,
            EnemyAiMode aiMode,
            int hp = 3)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = role,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                boxCapabilities = capabilities,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
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
                type = EntityType.Wall,
                state = EntityPhaseState.Idle,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}

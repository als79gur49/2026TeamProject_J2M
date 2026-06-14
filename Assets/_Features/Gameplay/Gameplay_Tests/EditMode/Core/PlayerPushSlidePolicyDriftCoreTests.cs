using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class PlayerPushSlidePolicyDriftCoreTests
    {
        private static readonly SurfaceCell PlayerCell = new(FaceId.Floor, 2, 1);
        private static readonly SurfaceCell BoxCell = new(FaceId.Floor, 3, 1);
        private static readonly SurfaceCell StopperCell = new(FaceId.Floor, 4, 1);

        [Test]
        [Category("Core")]
        public void PushEmptyFirstStep_StartPendingAndExecute_UseBoxSlidePolicy()
        {
            var worldState = CreateStandardWorld();
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);

            Assert.That(PlayerControlQueries.TryResolvePushContact(snapshot, player, Direction.Right, tickIndex: 1, out var target), Is.True);
            Assert.That(target.TargetEntityId, Is.EqualTo(20));

            var pipeline = CreatePlayerPipeline(worldState);
            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var pendingSnapshot = worldState.CreateSnapshot();
            Assert.That(pendingSnapshot.TryGetEntity(10, out var pendingPlayer), Is.True);
            Assert.That(pendingSnapshot.TryGetPlayerControlState(10, out var pendingState), Is.True);
            Assert.That(PlayerControlQueries.CanPendingActionStillExecute(pendingSnapshot, pendingPlayer, pendingState.activeAction, tickIndex: 2), Is.True);

            var executeResult = pipeline.RunTick(new TickInput(2));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(startResult.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
            Assert.That(startResult.PresentationData.PlayerActionAttemptSignals, Is.Empty);
            Assert.That(executeResult.MovementPhaseResult.RejectedReasons, Has.None.Contains("Source=10"));
            Assert.That(executeResult.PresentationData.BoxSlideStartSignals, Has.Count.EqualTo(1));
            Assert.That(finalSnapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(StopperCell));
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Sliding));
        }

        [Test]
        [Category("Core")]
        public void PushHostileStopper_StartPendingAndExecute_UseBoxSlideImpactPolicy()
        {
            var worldState = CreateStandardWorld(new[] { CreateEnemy(30, StopperCell, hp: 3) });
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);

            Assert.That(PlayerControlQueries.TryResolvePushContact(snapshot, player, Direction.Right, tickIndex: 1, out var target), Is.True);
            Assert.That(target.TargetEntityId, Is.EqualTo(20));

            var pipeline = CreatePlayerPipeline(worldState);
            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var pendingSnapshot = worldState.CreateSnapshot();
            Assert.That(pendingSnapshot.TryGetEntity(10, out var pendingPlayer), Is.True);
            Assert.That(pendingSnapshot.TryGetPlayerControlState(10, out var pendingState), Is.True);
            Assert.That(PlayerControlQueries.CanPendingActionStillExecute(pendingSnapshot, pendingPlayer, pendingState.activeAction, tickIndex: 2), Is.True);

            var executeResult = pipeline.RunTick(new TickInput(2));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(startResult.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
            Assert.That(executeResult.MovementPhaseResult.CommitEvents, Has.Some.Contains("ImpactReservationCreated").And.Contains("Source=20").And.Contains("Target=30"));
            Assert.That(executeResult.AttackPhaseResult.DrainedImpactReservations, Has.Count.EqualTo(1));
            Assert.That(executeResult.MovementPhaseResult.CommitEvents, Has.None.Contains("MoveCommitted").And.Contains("E=20"));
            Assert.That(finalSnapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(BoxCell));
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(finalSnapshot.TryGetEntity(30, out var enemy), Is.True);
            Assert.That(enemy.hp, Is.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void PushAllyStopper_RejectsAtStartInsteadOfStartingDeadPush()
        {
            var worldState = CreateStandardWorld(new[] { CreatePlayerUnit(30, StopperCell) });
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);

            Assert.That(PlayerControlQueries.TryResolvePushContact(snapshot, player, Direction.Right, tickIndex: 1, out _), Is.False);

            var result = CreatePlayerPipeline(worldState).RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(finalSnapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
        }

        [Test]
        [Category("Core")]
        public void PendingPush_AllyStopperEnteringFirstStep_CancelsBeforeExecute()
        {
            var worldState = CreateStandardWorld();
            var pipeline = CreatePlayerPipeline(worldState);

            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            worldState.CreateWriteContext().SpawnEntity(CreatePlayerUnit(30, StopperCell));
            var pendingSnapshot = worldState.CreateSnapshot();
            Assert.That(pendingSnapshot.TryGetEntity(10, out var pendingPlayer), Is.True);
            Assert.That(pendingSnapshot.TryGetPlayerControlState(10, out var pendingState), Is.True);
            Assert.That(PlayerControlQueries.CanPendingActionStillExecute(pendingSnapshot, pendingPlayer, pendingState.activeAction, tickIndex: 2), Is.False);

            var cancelResult = pipeline.RunTick(new TickInput(2));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(startResult.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(cancelResult.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(cancelResult.PresentationData.PlayerActionSignals.Single().CanceledThisTick, Is.True);
            Assert.That(finalSnapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(BoxCell));
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Idle));
        }

        [Test]
        [Category("Core")]
        public void PushActiveGlideStopper_StartPendingAndExecute_SkipActiveGlideLikeBoxSlidePolicy()
        {
            var worldState = CreateStandardWorld(new[] { CreateEnemy(30, StopperCell, hp: 3) });
            SetActiveGlide(worldState, 30);
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);

            Assert.That(PlayerControlQueries.TryResolvePushContact(snapshot, player, Direction.Right, tickIndex: 1, out _), Is.True);

            var pipeline = CreatePlayerPipeline(worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var executeResult = pipeline.RunTick(new TickInput(2));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(executeResult.MovementPhaseResult.RejectedReasons, Has.None.Contains("Source=10"));
            Assert.That(executeResult.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(finalSnapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(StopperCell));
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(finalSnapshot.TryGetEntity(30, out var glider), Is.True);
            Assert.That(glider.markedForDeath, Is.False);
            Assert.That(finalSnapshot.TryGetActiveEnemyGlideState(30, out _), Is.True);
        }

        [Test]
        [Category("Core")]
        public void PushDestroyFirstStepBlocked_StartAndExecute_UseDestroyFallback()
        {
            var worldState = CreateStandardWorld(
                stopperEntities: new[] { CreateWall(90, StopperCell) },
                boxCapabilities: BoxCapabilities.Push | BoxCapabilities.Destroy);
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);

            Assert.That(PlayerControlQueries.TryResolvePushContact(snapshot, player, Direction.Right, tickIndex: 1, out _), Is.True);

            var pipeline = CreatePlayerPipeline(worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var executeResult = pipeline.RunTick(new TickInput(2));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(executeResult.MovementPhaseResult.CommitEvents, Has.Some.Contains("DestroyMarked").And.Contains("Target=20"));
            Assert.That(executeResult.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
        }

        private static WorldState CreateStandardWorld(
            IEnumerable<EntityState> stopperEntities = null,
            BoxCapabilities boxCapabilities = BoxCapabilities.Push)
        {
            var entities = new List<EntityState>
            {
                CreatePlayerUnit(10, PlayerCell),
                CreateBox(20, BoxCell, boxCapabilities),
            };
            if (stopperEntities != null)
            {
                entities.AddRange(stopperEntities);
            }

            return GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(5, 5)),
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

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position)
        {
            return CreateUnit(entityId, position, teamId: 1, UnitRole.Player, EnemyAiMode.None);
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
                aiMode = aiMode,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
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
                facing = Direction.Right,
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
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}

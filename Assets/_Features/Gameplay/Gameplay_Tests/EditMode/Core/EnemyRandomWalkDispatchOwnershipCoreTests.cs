using System;
using System.Collections.Generic;
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
    public sealed class EnemyRandomWalkDispatchOwnershipCoreTests
    {
        [Test]
        [Category("Core")]
        public void RandomWalkPatrolStrategy_DirectDispatchGuard_RemainsOwnedByEnemyLogic()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateEnemy(40, new SurfaceCell(FaceId.Floor, 2, 2), EnemyAiMode.Patrol),
            });
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                RandomWalkPatrolStrategy.Instance.TryBuildMovementIntent(
                    snapshot,
                    enemy,
                    EnemyAiCommonSettings.CreateDefaultMelee(),
                    PatrolSettings.CreateDefaultRandomWalk(),
                    Array.Empty<TileFeatureRuntimeDefinition>(),
                    out _));

            Assert.That(
                exception.Message,
                Is.EqualTo("RandomWalk patrol dispatch is owned by EnemyLogic in the Phase 1 bounded rollout."));
        }

        [Test]
        [Category("Core")]
        public void EnemyLogic_RandomWalkPatrol_CollectsMovementThroughOwnedDispatch()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateEnemy(40, new SurfaceCell(FaceId.Floor, 2, 2), EnemyAiMode.Patrol),
            });
            var logic = new EnemyLogic(40, CreateRandomWalkRuntime());
            var rawIntents = new List<RawMovementIntent>();

            Assert.DoesNotThrow(() => logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(1), rawIntents));

            Assert.That(
                rawIntents,
                Has.Exactly(1).Matches<RawMovementIntent>(
                    intent => intent.SourceId == 40 && intent.CommandKind == MovementCommandKind.Move));
        }

        [Test]
        [Category("Core")]
        public void EnemyLogic_PhasedRandomWalkPatrol_SuppressesBaselineGroundLocomotion()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateEnemy(40, new SurfaceCell(FaceId.Floor, 2, 2), EnemyAiMode.Patrol),
            });
            worldState.CreateWriteContext().SetPhasedState(
                40,
                PhasedRuntimeStateQueries.ForceDebug(default, tickIndex: 1));
            var logic = new EnemyLogic(40, CreateRandomWalkRuntime());
            var rawIntents = new List<RawMovementIntent>();

            Assert.DoesNotThrow(() => logic.CollectMovementIntents(worldState.CreateSnapshot(), new TickInput(2), rawIntents));

            Assert.That(rawIntents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void NebulousPhasedSameCellFlip_WithEnemyLogic_DoesNotDispatchRandomWalkStrategyDirectly()
        {
            var playerCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, playerCell),
                CreateEnemy(5, playerCell, EnemyAiMode.Patrol),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 1), BoxCapabilities.Flip),
            });
            worldState.CreateWriteContext().SetPhasedState(
                5,
                PhasedRuntimeStateQueries.ForceDebug(default, tickIndex: 1));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                    new EnemyLogic(5, CreateRandomWalkRuntime()),
                });

            TickResult executeResult = null;
            Assert.DoesNotThrow(() => pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left))));
            Assert.DoesNotThrow(() => executeResult = pipeline.RunTick(new TickInput(2)));

            Assert.That(
                executeResult.MovementPhaseResult.RawIntents,
                Has.Exactly(1).Matches<RawMovementIntent>(
                    intent => intent.SourceId == 10 && intent.CommandKind == MovementCommandKind.Flip));
            Assert.That(
                executeResult.MovementPhaseResult.RawIntents,
                Has.None.Matches<RawMovementIntent>(
                    intent => intent.SourceId == 5 && intent.CommandKind == MovementCommandKind.Move));
        }

        [Test]
        [Category("Core")]
        public void EnemyPatrolRuntime_KindStrategyMismatch_FailsBeforeRuntimeTick()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new EnemyPatrolRuntime(
                    PatrolStrategyKind.Forward,
                    PatrolSettings.CreateDefaultRandomWalk(),
                    RandomWalkPatrolStrategy.Instance));

            Assert.That(exception.Message, Does.Contain("must match strategy implementation"));
        }

        private static EnemyAiRuntimeDefinition CreateRandomWalkRuntime()
        {
            return new EnemyAiRuntimeDefinition(
                EnemyAiCommonSettings.CreateDefaultMelee(),
                PatrolSettings.CreateDefaultRandomWalk(),
                DetectionSettings.CreateDefaultMelee(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateDefaultMelee(),
                EnemyAttackTimingSettings.CreateDefaultMelee(),
                EnemyLocomotionTimingSettings.CreateDefaultMelee(),
                RandomWalkPatrolStrategy.Instance,
                NoDetectionStrategy.Instance,
                AxisPriorityChaseStrategy.Instance,
                NoAttackDecisionStrategy.Instance,
                DefaultEnemyAiStateResolver.Instance);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(5, 5)),
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
        }

        private static EntityState CreatePlayer(int entityId, SurfaceCell position)
        {
            return CreateUnit(entityId, position, teamId: 1, UnitRole.Player, EnemyAiMode.None);
        }

        private static EntityState CreateEnemy(int entityId, SurfaceCell position, EnemyAiMode aiMode)
        {
            return CreateUnit(entityId, position, teamId: 2, UnitRole.Enemy, aiMode);
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int teamId,
            UnitRole role,
            EnemyAiMode aiMode)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
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
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}

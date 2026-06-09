using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class EnemyKinematicContinuationBlockedReactionCoreTests
    {
        [Test]
        [Category("Core")]
        public void EnemyKinematicContinuationBlocked_Chase_SourceSettlesAndRecordsPendingReaction()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var blockedCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 13, 4)),
                CreateEnemy(61, sourceCell),
                CreateBox(201, blockedCell),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());

            var result = CreatePipeline(worldState).RunTick(new TickInput(2));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(sourceCell));
            Assert.That(snapshot.TryGetPrimaryUnitAt(blockedCell, out var targetUnit), Is.False);
            Assert.That(targetUnit.entityId, Is.Not.EqualTo(61));
            Assert.That(snapshot.TryGetUnitKinematicPose(61, out var pose), Is.True);
            Assert.That(pose.IsSettledAtAnchor, Is.True);
            Assert.That(pose.State.localOffset.X.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.velocity.X.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.velocity.Y.RawValue, Is.EqualTo(0));
            AssertNoMovementIntentFor(result, 61);
            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("EnemyKinematicContinuationBlocked", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("KinematicAnchorCommitted", StringComparison.Ordinal) &&
                    entry.Contains("To=Floor(12,4)", StringComparison.Ordinal)),
                Is.False);
            Assert.That(snapshot.TryGetPendingEnemyBlockedReaction(61, out var reaction), Is.True);
            Assert.That(reaction.SourceCell, Is.EqualTo(sourceCell));
            Assert.That(reaction.BlockedTargetCell, Is.EqualTo(blockedCell));
            Assert.That(reaction.BlockedDirection, Is.EqualTo(Direction.Right));
            Assert.That(reaction.BlockerKind, Is.EqualTo(LegalityBlockerKind.Solid));
            Assert.That(reaction.BlockerSolidKind, Is.EqualTo(SolidKind.Box));
            Assert.That(reaction.BlockerEntityId, Is.EqualTo(201));
        }

        [Test]
        [Category("Core")]
        public void EnemyKinematicContinuationBlocked_Patrol_SourceSettlesAndRecordsPendingReaction()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var blockedCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 14, 4)),
                CreateEnemy(61, sourceCell, aiMode: EnemyAiMode.Patrol),
                CreateBox(201, blockedCell),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());

            var result = CreatePipeline(worldState, CreateForwardPatrolRuntime()).RunTick(new TickInput(2));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(sourceCell));
            Assert.That(snapshot.TryGetPrimaryUnitAt(blockedCell, out var targetUnit), Is.False);
            Assert.That(targetUnit.entityId, Is.Not.EqualTo(61));
            Assert.That(snapshot.TryGetUnitKinematicPose(61, out var pose), Is.True);
            Assert.That(pose.IsSettledAtAnchor, Is.True);
            Assert.That(pose.State.localOffset.X.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.velocity.X.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.velocity.Y.RawValue, Is.EqualTo(0));
            AssertNoMovementIntentFor(result, 61);
            Assert.That(snapshot.TryGetPendingEnemyBlockedReaction(61, out var reaction), Is.True);
            Assert.That(reaction.Kind, Is.EqualTo(EnemyBlockedReactionKind.KinematicContinuationTargetBlocked));
            Assert.That(reaction.ModeAtBlock, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(reaction.SourceCell, Is.EqualTo(sourceCell));
            Assert.That(reaction.BlockedTargetCell, Is.EqualTo(blockedCell));
            Assert.That(reaction.BlockedDirection, Is.EqualTo(Direction.Right));
            Assert.That(reaction.BlockerKind, Is.EqualTo(LegalityBlockerKind.Solid));
            Assert.That(reaction.BlockerSolidKind, Is.EqualTo(SolidKind.Box));
            Assert.That(reaction.BlockerEntityType, Is.EqualTo(EntityType.Box));
            Assert.That(reaction.BlockerEntityId, Is.EqualTo(201));
        }

        [Test]
        [Category("Core")]
        public void EnemyKinematicContinuationBlocked_Patrol_DoesNotCreateSameTickFallback()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var blockedCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var reverseCell = new SurfaceCell(FaceId.Floor, 10, 4);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 14, 4)),
                    CreateEnemy(61, sourceCell, aiMode: EnemyAiMode.Patrol),
                    CreateBox(201, blockedCell),
                },
                new BoardBounds(new Vector2Int(10, 4), new Vector2Int(14, 4)));
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());

            var result = CreatePipeline(worldState, CreateForwardPatrolRuntime()).RunTick(new TickInput(2));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(sourceCell));
            Assert.That(snapshot.TryGetPendingEnemyBlockedReaction(61, out _), Is.True);
            Assert.That(
                result.MovementPhaseResult.RawIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.Destination == reverseCell.PlanarPosition),
                Is.False);
            Assert.That(
                result.MovementPhaseResult.SortedIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.Destination == reverseCell.PlanarPosition),
                Is.False);
            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("MoveCommitted", StringComparison.Ordinal) &&
                    entry.Contains("E=61", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyPatrolBlockedReaction_Forward_ReversesNextTick()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var blockedCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var reverseCell = new SurfaceCell(FaceId.Floor, 10, 4);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 14, 4)),
                    CreateEnemy(61, sourceCell, aiMode: EnemyAiMode.Patrol, enemyLocomotionCooldownTicks: 3),
                    CreateBox(201, blockedCell),
                },
                new BoardBounds(new Vector2Int(10, 4), new Vector2Int(14, 4)));
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());
            var pipeline = CreatePipeline(worldState, CreateForwardPatrolRuntime());

            pipeline.RunTick(new TickInput(2));
            var reactionTick = pipeline.RunTick(new TickInput(3));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(sourceCell));
            Assert.That(enemy.enemyLocomotionCooldownTicks, Is.EqualTo(0));
            Assert.That(enemy.facing, Is.EqualTo(Direction.Left));
            Assert.That(
                reactionTick.MovementPhaseResult.RawIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.Destination == reverseCell.PlanarPosition),
                Is.True);
            Assert.That(
                reactionTick.MovementPhaseResult.SortedIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.Destination == reverseCell.PlanarPosition),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyPatrolBlockedReaction_RandomWalk_DoesNotForceReverseFacing()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var blockedCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 14, 4)),
                    CreateEnemy(61, sourceCell, aiMode: EnemyAiMode.Patrol, enemyLocomotionCooldownTicks: 3),
                    CreateBox(201, blockedCell),
                },
                new BoardBounds(new Vector2Int(10, 3), new Vector2Int(14, 5)));
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());
            var pipeline = CreatePipeline(worldState, CreateRandomWalkPatrolRuntime());

            pipeline.RunTick(new TickInput(2));
            var reactionTick = pipeline.RunTick(new TickInput(3));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.enemyLocomotionCooldownTicks, Is.EqualTo(0));
            Assert.That(
                reactionTick.EventLog.Any(entry =>
                    entry.Contains("PendingEnemyBlockedReactionPatrolFacing", StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                reactionTick.MovementPhaseResult.RawIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.CommandKind == MovementCommandKind.Move),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void EnemyKinematicContinuationBlocked_Patrol_SolidWallRecordsPendingReaction()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var blockedCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 14, 4)),
                CreateEnemy(61, sourceCell, aiMode: EnemyAiMode.Patrol),
                CreateWall(301, blockedCell),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());

            CreatePipeline(worldState, CreateForwardPatrolRuntime()).RunTick(new TickInput(2));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPendingEnemyBlockedReaction(61, out var reaction), Is.True);
            Assert.That(reaction.ModeAtBlock, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(reaction.BlockerKind, Is.EqualTo(LegalityBlockerKind.Solid));
            Assert.That(reaction.BlockerSolidKind, Is.EqualTo(SolidKind.Wall));
            Assert.That(reaction.BlockerEntityType, Is.EqualTo(EntityType.None));
            Assert.That(reaction.BlockerEntityId, Is.EqualTo(301));
        }

        [Test]
        [Category("Core")]
        public void EnemyKinematicContinuationBlocked_Patrol_UnitBlockerDoesNotRecordPendingReaction()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var blockedCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(70, blockedCell),
                CreateEnemy(61, sourceCell, aiMode: EnemyAiMode.Patrol),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());

            var result = CreatePipeline(worldState, CreateForwardPatrolRuntime()).RunTick(new TickInput(2));

            Assert.That(worldState.CreateSnapshot().TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("PendingEnemyBlockedReactionSet", StringComparison.Ordinal) &&
                    entry.Contains("E=61", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void EnemyChaseBlockedReaction_ReevaluatesNextTick_ExcludesBlockedDirection()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var blockedCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 13, 5)),
                    CreateEnemy(61, sourceCell, enemyLocomotionCooldownTicks: 3),
                    CreateBox(201, blockedCell),
                },
                new BoardBounds(new Vector2Int(10, 3), new Vector2Int(14, 5)));
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(2));
            var reevaluateResult = pipeline.RunTick(new TickInput(3));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(sourceCell));
            Assert.That(enemy.enemyLocomotionCooldownTicks, Is.EqualTo(0));
            Assert.That(
                reevaluateResult.MovementPhaseResult.RawIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.Destination == new Vector2Int(12, 4)),
                Is.False);
            Assert.That(
                reevaluateResult.MovementPhaseResult.SortedIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.Destination == new Vector2Int(12, 4)),
                Is.False);
            Assert.That(
                reevaluateResult.MovementPhaseResult.RawIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.Destination == new Vector2Int(11, 5)),
                Is.True);
            Assert.That(
                reevaluateResult.MovementPhaseResult.SortedIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.Destination == new Vector2Int(11, 5)),
                Is.True);
            Assert.That(snapshot.TryGetUnitKinematicPose(61, out var pose), Is.True);
            Assert.That(pose.State.velocity.X.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.velocity.Y.RawValue, Is.GreaterThan(0));
        }

        private static TickPipeline CreatePipeline(WorldState worldState)
        {
            return CreatePipeline(worldState, CreateChaseRuntime());
        }

        private static TickPipeline CreatePipeline(WorldState worldState, EnemyAiRuntimeDefinition enemyAiRuntime)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new GameplayBootstrapper(GameplayEntityLogicProviderFactory.CreateDefault(enemyAiRuntime)).CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    timingProfile.SimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
        }

        private static EnemyAiRuntimeDefinition CreateChaseRuntime()
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
                NearestOpponentDetectionStrategy.Instance,
                AxisPriorityChaseStrategy.Instance,
                NoAttackDecisionStrategy.Instance,
                DefaultEnemyAiStateResolver.Instance);
        }

        private static EnemyAiRuntimeDefinition CreateForwardPatrolRuntime()
        {
            return CreatePatrolRuntime(PatrolSettings.CreateDefault(), ForwardPatrolStrategy.Instance);
        }

        private static EnemyAiRuntimeDefinition CreateRandomWalkPatrolRuntime()
        {
            return CreatePatrolRuntime(PatrolSettings.CreateDefaultRandomWalk(), RandomWalkPatrolStrategy.Instance);
        }

        private static EnemyAiRuntimeDefinition CreatePatrolRuntime(
            PatrolSettings patrolSettings,
            IPatrolStrategy patrolStrategy)
        {
            return new EnemyAiRuntimeDefinition(
                EnemyAiCommonSettings.CreateStandard(),
                patrolSettings,
                DetectionSettings.CreateStandardEnemyDetection(),
                ChaseSettings.CreateDefault(),
                AttackDecisionSettings.CreateAdjacentRange(),
                EnemyAttackTimingSettings.CreateImmediate(),
                EnemyLocomotionTimingSettings.CreateImmediate(),
                patrolStrategy,
                NoDetectionStrategy.Instance,
                AxisPriorityChaseStrategy.Instance,
                NoAttackDecisionStrategy.Instance,
                DefaultEnemyAiStateResolver.Instance);
        }

        private static void AssertNoMovementIntentFor(TickResult result, int entityId)
        {
            Assert.That(
                result.MovementPhaseResult.RawIntents.Any(intent =>
                    intent.SourceId == entityId &&
                    intent.CommandKind == MovementCommandKind.Move),
                Is.False);
            Assert.That(
                result.MovementPhaseResult.SortedIntents.Any(intent =>
                    intent.SourceId == entityId &&
                    intent.CommandKind == MovementCommandKind.Move),
                Is.False);
        }

        private static WorldState CreateWorldState(EntityState[] entities)
        {
            return CreateWorldState(
                entities,
                new BoardBounds(new Vector2Int(10, 4), new Vector2Int(14, 4)));
        }

        private static WorldState CreateWorldState(EntityState[] entities, BoardBounds boardBounds)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                boardBounds,
                GameplayTerrainData.Empty);
        }

        private static EntityState CreatePlayer(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateEnemy(
            int entityId,
            SurfaceCell position,
            int enemyLocomotionCooldownTicks = 0,
            EnemyAiMode aiMode = EnemyAiMode.Chase,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                aiMode = aiMode,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                enemyLocomotionCooldownTicks = enemyLocomotionCooldownTicks,
            };
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
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static UnitKinematicRuntimeState CreateCommitTickKinematicContinuationState()
        {
            return new UnitKinematicRuntimeState
            {
                localOffset = new KinematicOffset2(KinematicFixed.FromRaw(1024), KinematicFixed.Zero),
                velocity = new KinematicVelocity2(KinematicFixed.FromRaw(1024), KinematicFixed.Zero),
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = 3072,
                remainingTicks = 3,
                speedScalePermille = 1000,
                sequenceId = 1,
                elapsedTicks = 1,
                totalTicks = 4,
                commitTick = 2,
                startedTick = 1,
                stepDirectionX = 1,
                stepDirectionY = 0,
            }.NormalizedForStorage();
        }

    }
}

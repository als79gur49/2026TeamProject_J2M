using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class EnemyKinematicContinuationScenarioTests
    {
        [Test]
        [Category("Core")]
        public void MovementStage_EnemyKinematicContinuation_DoesNotMaterializeAnchorCommitIntoExistingSolidBox()
        {
            var blockedAnchorCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 13, 4)),
                CreateEnemy(61, new SurfaceCell(FaceId.Floor, 11, 4)),
                CreateBox(201, blockedAnchorCell),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());
            writeContext.SetEnemyJumpState(61, CreateCooldownJumpState());
            var pipeline = CreatePipeline(worldState);

            TickResult result = null;
            Assert.DoesNotThrow(() => result = pipeline.RunTick(new TickInput(2)));
            Assert.That(result, Is.Not.Null);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.Not.EqualTo(blockedAnchorCell));
            Assert.That(snapshot.TryGetEntity(201, out var box), Is.True);
            Assert.That(box.type, Is.EqualTo(EntityType.Box));
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(box.position, Is.EqualTo(blockedAnchorCell));

            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("KinematicAnchorCommitted", StringComparison.Ordinal) &&
                    entry.Contains("E=61", StringComparison.Ordinal) &&
                    entry.Contains("To=Floor(12,4)", StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("MoveCommitted", StringComparison.Ordinal) &&
                    entry.Contains("E=61", StringComparison.Ordinal) &&
                    entry.Contains("To=Floor(12,4)", StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                HasClosedBlockedKinematicContinuation(result),
                Is.True,
                "The continuation should be rejected, blocked, held, or cancelled instead of materializing into the existing solid box.");
            Assert.That(
                snapshot.TryGetPendingEnemyBlockedReaction(61, out var reaction),
                Is.True);
            Assert.That(reaction.Kind, Is.EqualTo(EnemyBlockedReactionKind.KinematicContinuationTargetBlocked));
            Assert.That(reaction.ModeAtBlock, Is.EqualTo(EnemyAiMode.Chase));
            Assert.That(reaction.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 11, 4)));
            Assert.That(reaction.BlockedTargetCell, Is.EqualTo(blockedAnchorCell));
            Assert.That(reaction.BlockedDirection, Is.EqualTo(Direction.Right));
            Assert.That(reaction.BlockerKind, Is.EqualTo(LegalityBlockerKind.Solid));
            Assert.That(reaction.BlockerSolidKind, Is.EqualTo(SolidKind.Box));
            Assert.That(reaction.BlockerEntityType, Is.EqualTo(EntityType.Box));
            Assert.That(reaction.BlockerEntityId, Is.EqualTo(201));
            Assert.That(reaction.CreatedTick, Is.EqualTo(2));
            Assert.That(reaction.ExpireTick, Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void MovementStage_EnemyChaseBlockedReaction_ReevaluatesNextTickWithoutRetryingBlockedDirection()
        {
            var blockedAnchorCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var verticalCandidate = new SurfaceCell(FaceId.Floor, 11, 5);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 13, 5)),
                    CreateEnemy(61, sourceCell, enemyLocomotionCooldownTicks: 3),
                    CreateBox(201, blockedAnchorCell),
                },
                new BoardBounds(new Vector2Int(10, 3), new Vector2Int(14, 5)));
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());
            writeContext.SetEnemyJumpState(61, CreateCooldownJumpState());
            var pipeline = CreatePipeline(worldState);

            var blockedResult = pipeline.RunTick(new TickInput(2));
            Assert.That(
                blockedResult.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("PendingEnemyBlockedReactionSet", StringComparison.Ordinal) &&
                    entry.Contains("E=61", StringComparison.Ordinal)),
                Is.True);
            Assert.That(worldState.CreateSnapshot().TryGetPendingEnemyBlockedReaction(61, out _), Is.True);

            var reevaluateResult = pipeline.RunTick(new TickInput(3));
            var reevaluateSnapshot = worldState.CreateSnapshot();

            Assert.That(reevaluateSnapshot.TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
            Assert.That(
                reevaluateResult.Trace.Text,
                Does.Contain("PendingEnemyBlockedReactionConsumed|E=61|Direction=Right"));
            Assert.That(
                reevaluateResult.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("KinematicAnchorCommitted", StringComparison.Ordinal) &&
                    entry.Contains("To=Floor(12,4)", StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                reevaluateResult.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("MoveCommitted", StringComparison.Ordinal) &&
                    entry.Contains("To=Floor(12,4)", StringComparison.Ordinal)),
                Is.False);
            Assert.That(reevaluateSnapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(sourceCell));
            Assert.That(reevaluateSnapshot.TryGetUnitKinematicPose(61, out var pose), Is.True);
            Assert.That(pose.IsSettledAtAnchor, Is.False);
            Assert.That(pose.State.velocity.X.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.velocity.Y.RawValue, Is.GreaterThan(0));
            Assert.That(pose.AnchorCell, Is.EqualTo(sourceCell));
            Assert.That(verticalCandidate, Is.EqualTo(new SurfaceCell(FaceId.Floor, sourceCell.x, sourceCell.y + 1)));
        }

        [Test]
        [Category("Core")]
        public void MovementStage_EnemyChaseBlockedReaction_NoCandidateStaysWithoutReverseFallback()
        {
            var blockedAnchorCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 13, 4)),
                CreateEnemy(61, sourceCell, enemyLocomotionCooldownTicks: 3),
                CreateBox(201, blockedAnchorCell),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());
            writeContext.SetEnemyJumpState(61, CreateCooldownJumpState());
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(2));
            var reevaluateResult = pipeline.RunTick(new TickInput(3));
            var reevaluateSnapshot = worldState.CreateSnapshot();

            Assert.That(reevaluateSnapshot.TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
            Assert.That(reevaluateSnapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(sourceCell));
            Assert.That(enemy.enemyLocomotionCooldownTicks, Is.EqualTo(0));
            Assert.That(reevaluateSnapshot.TryGetUnitKinematicPose(61, out var pose), Is.False);
            Assert.That(
                reevaluateResult.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("KinematicPoseCommitted", StringComparison.Ordinal) &&
                    entry.Contains("E=61", StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                reevaluateResult.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("MoveCommitted", StringComparison.Ordinal) &&
                    entry.Contains("E=61", StringComparison.Ordinal) &&
                    entry.Contains("To=Floor(10,4)", StringComparison.Ordinal)),
                Is.False);
        }

        private static bool HasClosedBlockedKinematicContinuation(TickResult result)
        {
            return result.MovementPhaseResult.RejectedReasons.Any(reason =>
                       (reason.Contains("E=61", StringComparison.Ordinal) ||
                        reason.Contains("Source=61", StringComparison.Ordinal)) &&
                       (reason.Contains("Kinematic", StringComparison.Ordinal) ||
                        reason.Contains("Blocked", StringComparison.Ordinal) ||
                        reason.Contains("Rejected", StringComparison.Ordinal) ||
                        reason.Contains("Traversal", StringComparison.Ordinal))) ||
                   result.MovementPhaseResult.CommitEvents.Any(entry =>
                       entry.Contains("KinematicPoseCommitted", StringComparison.Ordinal) &&
                       entry.Contains("E=61", StringComparison.Ordinal) &&
                       (entry.Contains("Blocked=1", StringComparison.Ordinal) ||
                        entry.Contains("RejectedBy=", StringComparison.Ordinal) &&
                        !entry.Contains("RejectedBy=None", StringComparison.Ordinal) ||
                        entry.Contains("Mode=Held", StringComparison.Ordinal) ||
                        entry.Contains("Mode=Interrupted", StringComparison.Ordinal)));
        }

        private static TickPipeline CreatePipeline(WorldState worldState)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    timingProfile.SimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
        }

        private static WorldState CreateWorldState(EntityState[] entities)
        {
            return CreateWorldState(
                entities,
                new BoardBounds(new Vector2Int(10, 4), new Vector2Int(14, 4)));
        }

        private static WorldState CreateWorldState(EntityState[] entities, BoardBounds boardBounds)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
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
            int enemyLocomotionCooldownTicks = 0)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                aiMode = EnemyAiMode.Chase,
                facing = Direction.Right,
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

        private static EnemyJumpRuntimeState CreateCooldownJumpState()
        {
            return new EnemyJumpRuntimeState
            {
                phase = EnemyJumpPhase.Cooldown,
                sequence = 1,
                sourceCell = new SurfaceCell(FaceId.Floor, 11, 4),
                lockedTargetCell = new SurfaceCell(FaceId.Floor, 13, 4),
                windupEndTick = 0,
                landingTick = 0,
                cooldownRemainingTicks = 2,
            };
        }
    }
}

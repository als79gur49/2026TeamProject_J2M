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
        [Category("Extended")]
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
            return GameplayWorldStateTestFactory.CreateBounded(
                entities,
                new BoardBounds(new Vector2Int(10, 4), new Vector2Int(14, 4)),
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

        private static EntityState CreateEnemy(int entityId, SurfaceCell position)
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

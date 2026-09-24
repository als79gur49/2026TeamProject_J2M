using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

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
            writeContext.SetEnemyJumpState(61, CreatePostLandingCooldownJumpState());
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
            Assert.That(snapshot.TryGetPrimaryUnitAt(blockedAnchorCell, out var targetUnit), Is.False);
            Assert.That(targetUnit.entityId, Is.Not.EqualTo(61));
            Assert.That(snapshot.TryGetUnitKinematicPose(61, out var pose), Is.True);
            Assert.That(pose.AnchorCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 11, 4)));
            Assert.That(pose.IsSettledAtAnchor, Is.True);
            Assert.That(pose.State.localOffset.X.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.velocity.X.RawValue, Is.EqualTo(0));
            Assert.That(pose.State.velocity.Y.RawValue, Is.EqualTo(0));
            AssertNoMovementIntentFor(result, 61);

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
        public void BlockedEnemyKinematicContinuation_SameTickBoxPushIntoSourceAnchor_StopsAtOccupiedCell()
        {
            AssertBlockedEnemyKinematicContinuationStopsBoxPushIntoSourceAnchor(pushTick: 2);
        }

        [Test]
        [Category("Core")]
        public void BlockedEnemyKinematicContinuation_NextTickBoxPushIntoSourceAnchor_StopsAtOccupiedCell()
        {
            AssertBlockedEnemyKinematicContinuationStopsBoxPushIntoSourceAnchor(pushTick: 3);
        }

        private static void AssertBlockedEnemyKinematicContinuationStopsBoxPushIntoSourceAnchor(int pushTick)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var blockedTargetCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var approachingBoxCell = new SurfaceCell(FaceId.Floor, 10, 4);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 9, 4)),
                    CreateEnemy(61, sourceCell, enemyLocomotionCooldownTicks: 3),
                    CreateBox(201, blockedTargetCell),
                    CreateBox(202, approachingBoxCell, BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(9, 4), new Vector2Int(13, 4)));
            worldState.CreateWriteContext().SetUnitKinematicState(
                61,
                CreateCommitTickKinematicContinuationState());
            var pipeline = CreatePipeline(
                worldState,
                GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled,
                new IEntityLogic[] { new RightPushPlayerLogic(10) });

            var blockedTick = pipeline.RunTick(new TickInput(
                2,
                pushTick == 2 ? PlayerTickCommand.Move(Direction.Right) : default));
            Assert.That(
                blockedTick.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("EnemyKinematicContinuationBlocked", StringComparison.Ordinal) &&
                    reason.Contains("E=61", StringComparison.Ordinal)),
                Is.True);

            var pushResult = pushTick == 2
                ? blockedTick
                : pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(sourceCell));
            Assert.That(enemy.hp, Is.GreaterThan(0));
            Assert.That(snapshot.TryGetUnitKinematicPose(61, out var pose), Is.True);
            Assert.That(pose.AnchorCell, Is.EqualTo(sourceCell));
            Assert.That(pose.IsSettledAtAnchor, Is.True);
            Assert.That(snapshot.TryGetEntity(201, out var targetBox), Is.True);
            Assert.That(targetBox.position, Is.EqualTo(blockedTargetCell));
            Assert.That(snapshot.TryGetEntity(202, out var approachingBox), Is.True);
            Assert.That(approachingBox.position, Is.EqualTo(approachingBoxCell));
            Assert.That(snapshot.TryGetPrimaryUnitAt(sourceCell, out var sourceUnit), Is.True);
            Assert.That(sourceUnit.entityId, Is.EqualTo(61));
            Assert.That(snapshot.TryGetSolidOccupantAt(sourceCell, out _), Is.False);
            Assert.That(
                pushResult.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("ImpactReservationCreated", StringComparison.Ordinal) &&
                    entry.Contains("Source=202", StringComparison.Ordinal) &&
                    entry.Contains("Target=61", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                pushResult.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("MoveCommitted", StringComparison.Ordinal) &&
                    entry.Contains("E=202", StringComparison.Ordinal) &&
                    entry.Contains("To=Floor(11,4)", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void StageBackedWallIdentityCutover_PendingBlockedReaction_ChangesOnlyWallType()
        {
            var first = RunStageBackedWallBlockedContinuation();
            var second = RunStageBackedWallBlockedContinuation();

            Assert.That(first.DeterminismHash, Is.Not.Empty);
            Assert.That(first.RecomputedHash, Is.EqualTo(first.DeterminismHash));
            Assert.That(second.RecomputedHash, Is.EqualTo(second.DeterminismHash));
            Assert.That(second.DeterminismHash, Is.EqualTo(first.DeterminismHash));
            Assert.That(second.Trace, Is.EqualTo(first.Trace));
            Assert.That(second.ReactionRecord, Is.EqualTo(first.ReactionRecord));
            Assert.That(second.EventLogRecord, Is.EqualTo(first.EventLogRecord));
            Assert.That(second.SolidProjection, Is.EqualTo(first.SolidProjection));
        }

        [Test]
        [Category("Core")]
        public void WallIdentityDeltaComparator_PendingReaction_RejectsWrongIdKeyAndPayloadMutation()
        {
            WallIdentityDeltaComparatorTestHarness.AssertRuntimePositive(
                WallIdentityDeltaSource.PendingReaction,
                301,
                () => CapturePendingRuntimePositive("PendingReaction"));
        }

        [Test]
        [Category("Core")]
        public void WallIdentityDeltaComparator_PendingEventLog_RejectsWrongIdKeyAndPayloadMutation()
        {
            WallIdentityDeltaComparatorTestHarness.AssertRuntimePositive(
                WallIdentityDeltaSource.PendingEventLog,
                301,
                () => CapturePendingRuntimePositive("PendingEventLog"));
        }

        private string CapturePendingRuntimePositive(string source)
        {
            var capture = RunStageBackedWallBlockedContinuation();
            if (string.Equals(source, "PendingReaction", StringComparison.Ordinal))
            {
                return capture.ReactionRecord;
            }

            if (string.Equals(source, "PendingEventLog", StringComparison.Ordinal))
            {
                return capture.EventLogRecord;
            }

            throw new ArgumentOutOfRangeException(nameof(source), source, null);
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
            writeContext.SetEnemyJumpState(61, CreatePostLandingCooldownJumpState());
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
                reevaluateResult.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("KinematicAnchorCommitted", StringComparison.Ordinal) &&
                    entry.Contains("To=Floor(12,4)", StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                reevaluateResult.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("MoveCommitted", StringComparison.Ordinal) &&
                    entry.Contains("To=Floor(12,4)", StringComparison.Ordinal)),
                Is.False);
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
            Assert.That(reevaluateSnapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(sourceCell));
            Assert.That(enemy.enemyLocomotionCooldownTicks, Is.EqualTo(0));
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
            writeContext.SetEnemyJumpState(61, CreatePostLandingCooldownJumpState());
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(2));
            var reevaluateResult = pipeline.RunTick(new TickInput(3));
            var reevaluateSnapshot = worldState.CreateSnapshot();

            Assert.That(reevaluateSnapshot.TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
            Assert.That(reevaluateSnapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(sourceCell));
            Assert.That(enemy.enemyLocomotionCooldownTicks, Is.EqualTo(0));
            Assert.That(enemy.facing, Is.EqualTo(Direction.Right));
            Assert.That(reevaluateSnapshot.TryGetEnemyPatrolState(61, out _), Is.False);
            if (reevaluateSnapshot.TryGetUnitKinematicPose(61, out var pose))
            {
                Assert.That(pose.AnchorCell, Is.EqualTo(sourceCell));
                Assert.That(pose.IsSettledAtAnchor, Is.True);
                Assert.That(pose.LocalOffset.X.RawValue, Is.EqualTo(0));
                Assert.That(pose.LocalOffset.Y.RawValue, Is.EqualTo(0));
                Assert.That(pose.State.velocity.X.RawValue, Is.EqualTo(0));
                Assert.That(pose.State.velocity.Y.RawValue, Is.EqualTo(0));
            }

            AssertNoMovementIntentFor(reevaluateResult, 61);
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

        [Test]
        [Category("Core")]
        public void MovementStage_EnemyChaseBlockedReaction_NonSolidUnitOccupancy_DoesNotRecordPendingReactionOrForceReverse()
        {
            var blockedAnchorCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(70, blockedAnchorCell),
                CreateEnemy(61, new SurfaceCell(FaceId.Floor, 11, 4)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());
            var pipeline = CreatePipeline(worldState);

            var result = pipeline.RunTick(new TickInput(2));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("PendingEnemyBlockedReactionSet", StringComparison.Ordinal) &&
                    entry.Contains("E=61", StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                result.MovementPhaseResult.RawIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.Destination == new Vector2Int(10, 4)),
                Is.False);
            Assert.That(
                result.MovementPhaseResult.SortedIntents.Any(intent =>
                    intent.SourceId == 61 &&
                    intent.Destination == new Vector2Int(10, 4)),
                Is.False);
            Assert.That(snapshot.TryGetEntity(61, out var enemy), Is.True);
            Assert.That(enemy.position, Is.Not.EqualTo(new SurfaceCell(FaceId.Floor, 10, 4)));
        }

        [Test]
        [Category("Core")]
        public void MovementStage_EnemyChaseBlockedReaction_AttackOpportunityTakesPriorityOverMovementRetry()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(windupTicks: 1, recoverTicks: 1);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 10, 4)),
                CreateEnemy(61, sourceCell, enemyLocomotionCooldownTicks: 3),
                CreateBox(201, new SurfaceCell(FaceId.Floor, 12, 4)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());
            try
            {
                var pipeline = CreatePipeline(worldState, profile);

                pipeline.RunTick(new TickInput(2));
                var attackTick = pipeline.RunTick(new TickInput(3));
                var snapshot = worldState.CreateSnapshot();

                Assert.That(snapshot.TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
                Assert.That(snapshot.TryGetEntity(61, out var enemy), Is.True);
                Assert.That(enemy.position, Is.EqualTo(sourceCell));
                AssertNoMovementIntentFor(attackTick, 61);
                Assert.That(
                    attackTick.Trace.Text.Contains("To=Attack", StringComparison.Ordinal) ||
                    attackTick.AttackPhaseResult.RawIntents.Any(intent => intent.SourceId == 61) ||
                    snapshot.TryGetEnemyActionState(61, out var actionState) && actionState.IsActive,
                    Is.True);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_PendingEnemyBlockedReactionFields_AffectCanonicalHash()
        {
            var baselineHash = BuildPendingReactionHash(null);
            var baseReaction = CreatePendingReaction(
                sourceCell: new SurfaceCell(FaceId.Floor, 11, 4),
                blockedTargetCell: new SurfaceCell(FaceId.Floor, 12, 4),
                blockedDirection: Direction.Right,
                blockerEntityId: 201);
            var sourceChanged = CreatePendingReaction(
                sourceCell: new SurfaceCell(FaceId.Floor, 10, 4),
                blockedTargetCell: new SurfaceCell(FaceId.Floor, 12, 4),
                blockedDirection: Direction.Right,
                blockerEntityId: 201);
            var targetChanged = CreatePendingReaction(
                sourceCell: new SurfaceCell(FaceId.Floor, 11, 4),
                blockedTargetCell: new SurfaceCell(FaceId.Floor, 13, 4),
                blockedDirection: Direction.Right,
                blockerEntityId: 201);
            var directionChanged = CreatePendingReaction(
                sourceCell: new SurfaceCell(FaceId.Floor, 11, 4),
                blockedTargetCell: new SurfaceCell(FaceId.Floor, 12, 4),
                blockedDirection: Direction.Up,
                blockerEntityId: 201);
            var blockerChanged = CreatePendingReaction(
                sourceCell: new SurfaceCell(FaceId.Floor, 11, 4),
                blockedTargetCell: new SurfaceCell(FaceId.Floor, 12, 4),
                blockedDirection: Direction.Right,
                blockerEntityId: 202);

            var baseHash = BuildPendingReactionHash(baseReaction);

            Assert.That(baseHash, Is.Not.EqualTo(baselineHash));
            Assert.That(BuildPendingReactionHash(sourceChanged), Is.Not.EqualTo(baseHash));
            Assert.That(BuildPendingReactionHash(targetChanged), Is.Not.EqualTo(baseHash));
            Assert.That(BuildPendingReactionHash(directionChanged), Is.Not.EqualTo(baseHash));
            Assert.That(BuildPendingReactionHash(blockerChanged), Is.Not.EqualTo(baseHash));
        }

        [TestCase(EnemyJumpPhase.Windup)]
        [TestCase(EnemyJumpPhase.Airborne)]
        [Category("Core")]
        public void MovementStage_EnemyChaseBlockedReaction_JumpMovementSkillActive_DoesNotRecordPendingReaction(EnemyJumpPhase phase)
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 13, 4)),
                CreateEnemy(
                    61,
                    new SurfaceCell(FaceId.Floor, 11, 4),
                    boardPresence: phase == EnemyJumpPhase.Airborne
                        ? EntityBoardPresence.Detached
                        : EntityBoardPresence.Occupying),
                CreateBox(201, new SurfaceCell(FaceId.Floor, 12, 4)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());
            writeContext.SetEnemyJumpState(61, CreateJumpState(phase, landingTick: 4));

            var result = CreatePipeline(worldState).RunTick(new TickInput(2));

            Assert.That(worldState.CreateSnapshot().TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("PendingEnemyBlockedReactionSet", StringComparison.Ordinal) &&
                    entry.Contains("E=61", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void MovementStage_EnemyChaseBlockedReaction_JumpCooldownLandingTick_DoesNotRecordPendingReaction()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 13, 4)),
                CreateEnemy(61, new SurfaceCell(FaceId.Floor, 11, 4)),
                CreateBox(201, new SurfaceCell(FaceId.Floor, 12, 4)),
            });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetUnitKinematicState(61, CreateCommitTickKinematicContinuationState());
            writeContext.SetEnemyJumpState(61, CreateJumpState(EnemyJumpPhase.Cooldown, landingTick: 2));

            var result = CreatePipeline(worldState).RunTick(new TickInput(2));

            Assert.That(worldState.CreateSnapshot().TryGetPendingEnemyBlockedReaction(61, out _), Is.False);
            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(entry =>
                    entry.Contains("PendingEnemyBlockedReactionSet", StringComparison.Ordinal) &&
                    entry.Contains("E=61", StringComparison.Ordinal)),
                Is.False);
        }

        private static StageBackedWallKinematicCapture RunStageBackedWallBlockedContinuation()
        {
            const int playerEntityId = 10;
            const int enemyEntityId = 61;
            const int wallEntityId = 301;
            var sourceCell = new SurfaceCell(FaceId.Floor, 11, 4);
            var blockedCell = new SurfaceCell(FaceId.Floor, 12, 4);
            var playerCell = new SurfaceCell(FaceId.Floor, 14, 4);
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking();

            try
            {
                return StageAuthoredWallTestFactory.RunInHost(
                    wallEntityId,
                    blockedCell,
                    playerEntityId,
                    playerCell,
                    profile,
                    true,
                    (host, authoredWall) =>
                    {
                        host.TickRunner.RunTick(new TickInput(1));

                        var writeContext = host.WorldState.CreateWriteContext();
                        writeContext.SpawnEntity(CreateEnemy(enemyEntityId, sourceCell));
                        writeContext.SetUnitKinematicState(
                            enemyEntityId,
                            CreateCommitTickKinematicContinuationState());
                        writeContext.SetEnemyJumpState(
                            enemyEntityId,
                            CreatePostLandingCooldownJumpState());

                        var result = host.TickRunner.RunTick(new TickInput(2));
                        var snapshot = host.WorldState.CreateSnapshot();

                        Assert.That(
                            snapshot.TryGetSolidOccupantAt(blockedCell, out var solidOccupant),
                            Is.True);
                        Assert.That(solidOccupant.entityId, Is.EqualTo(authoredWall.entityId));
                        Assert.That(solidOccupant.type, Is.EqualTo(EntityType.Wall));
                        Assert.That(
                            snapshot.TryGetSolidSemanticAt(blockedCell, out var solidSemantic),
                            Is.True);
                        Assert.That(solidSemantic.Entity.entityId, Is.EqualTo(authoredWall.entityId));
                        Assert.That(solidSemantic.Kind, Is.EqualTo(SolidKind.Wall));
                        Assert.That(snapshot.IsWallAt(blockedCell), Is.True);
                        Assert.That(snapshot.IsBoxAt(blockedCell), Is.False);

                        Assert.That(
                            snapshot.TryGetPendingEnemyBlockedReaction(enemyEntityId, out var reaction),
                            Is.True);
                        Assert.That(reaction.BlockerEntityId, Is.EqualTo(authoredWall.entityId));
                        Assert.That(reaction.BlockerKind, Is.EqualTo(LegalityBlockerKind.Solid));
                        Assert.That(reaction.BlockerSolidKind, Is.EqualTo(SolidKind.Wall));
                        Assert.That(reaction.BlockerEntityType, Is.EqualTo(EntityType.Wall));

                        var eventLogRecord = result.EventLog.Single(entry =>
                            entry.StartsWith("PendingEnemyBlockedReactionSet|", StringComparison.Ordinal) &&
                            entry.Contains($"BlockerEntityId={authoredWall.entityId}", StringComparison.Ordinal));
                        Assert.That(eventLogRecord, Does.Contain($"BlockerEntityType={EntityType.Wall}"));
                        Assert.That(
                            result.EventLog.Count(entry =>
                                entry.StartsWith("PendingEnemyBlockedReactionSet|", StringComparison.Ordinal) &&
                                entry.Contains($"BlockerEntityId={authoredWall.entityId}", StringComparison.Ordinal)),
                            Is.EqualTo(1));

                        var reactionRecord =
                            $"PendingReaction|EnemyEntityId={reaction.EnemyEntityId}|Kind={reaction.Kind}|ModeAtBlock={reaction.ModeAtBlock}|SourceCell={reaction.SourceCell}|BlockedTargetCell={reaction.BlockedTargetCell}|BlockedDirection={reaction.BlockedDirection}|BlockerKind={reaction.BlockerKind}|BlockerSolidKind={reaction.BlockerSolidKind}|BlockerEntityType={reaction.BlockerEntityType}|BlockerEntityId={reaction.BlockerEntityId}|CreatedTick={reaction.CreatedTick}|ExpireTick={reaction.ExpireTick}";
                        var tickResultData = new TickResultData(
                            result.FinalEntities,
                            Array.Empty<DelayedAttackEffectRecord>(),
                            result.EventLog,
                            result.PresentationData,
                            result.ObjectiveResult);
                        var recomputedHash = new DeterminismHashBuilder().Build(
                            result.TickIndex,
                            snapshot,
                            tickResultData);
                        var solidProjection =
                            $"Solid|Cell={blockedCell}|E={solidSemantic.Entity.entityId}|Type={solidSemantic.Entity.type}|Kind={solidSemantic.Kind}";

                        Assert.That(recomputedHash, Is.EqualTo(result.DeterminismHash));
                        TestContext.Out.WriteLine(
                            $"STAGE_BACKED_WALL_E2E|Hash={result.DeterminismHash}|Reaction={reactionRecord}|Event={eventLogRecord}|Projection={solidProjection}");

                        return new StageBackedWallKinematicCapture(
                            reactionRecord,
                            eventLogRecord,
                            result.DeterminismHash,
                            recomputedHash,
                            result.Trace.Text,
                            solidProjection);
                    });
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
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
            return CreatePipeline(worldState, GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            IEntityLogic[] entityLogics = null)
        {
            var profile = EnemyAiProfileTestFactory.CreateNonAttacking();
            var timingProfile = GameplayTimingProfile.CreateDefault();
            try
            {
                return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                    worldState,
                    entityLogics ?? Array.Empty<IEntityLogic>(),
                    timingProfile,
                    PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                        timingProfile.SimulationTicksPerSecond,
                        timingProfile.RepeatedMoveIntervalSeconds),
                    runtimeFeatureFlags: runtimeFeatureFlags);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static TickPipeline CreatePipeline(WorldState worldState, EnemyAiProfile profile)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    timingProfile.SimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.EnemySameFaceContinuousLocomotionEnabled);
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

        private static string BuildPendingReactionHash(PendingEnemyBlockedReaction? reaction)
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 13, 4)),
                CreateEnemy(61, new SurfaceCell(FaceId.Floor, 11, 4)),
                CreateBox(201, new SurfaceCell(FaceId.Floor, 12, 4)),
                CreateBox(202, new SurfaceCell(FaceId.Floor, 14, 4)),
            });
            if (reaction.HasValue)
            {
                worldState.CreateWriteContext().SetPendingEnemyBlockedReaction(61, reaction.Value);
            }

            var snapshot = worldState.CreateSnapshot();
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            var tickResultData = new TickResultData(
                entities,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>());
            return new DeterminismHashBuilder().Build(7, snapshot, tickResultData);
        }

        private static PendingEnemyBlockedReaction CreatePendingReaction(
            SurfaceCell sourceCell,
            SurfaceCell blockedTargetCell,
            Direction blockedDirection,
            int blockerEntityId)
        {
            return new PendingEnemyBlockedReaction(
                61,
                EnemyBlockedReactionKind.KinematicContinuationTargetBlocked,
                EnemyAiMode.Chase,
                sourceCell,
                blockedTargetCell,
                blockedDirection,
                LegalityBlockerKind.Solid,
                SolidKind.Box,
                EntityType.Box,
                blockerEntityId,
                createdTick: 6,
                expireTick: 7);
        }

        private readonly struct StageBackedWallKinematicCapture
        {
            public StageBackedWallKinematicCapture(
                string reactionRecord,
                string eventLogRecord,
                string determinismHash,
                string recomputedHash,
                string trace,
                string solidProjection)
            {
                ReactionRecord = reactionRecord;
                EventLogRecord = eventLogRecord;
                DeterminismHash = determinismHash;
                RecomputedHash = recomputedHash;
                Trace = trace;
                SolidProjection = solidProjection;
            }

            public string ReactionRecord { get; }

            public string EventLogRecord { get; }

            public string DeterminismHash { get; }

            public string RecomputedHash { get; }

            public string Trace { get; }

            public string SolidProjection { get; }
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
                boardBounds);
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
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
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
                boardPresence = boardPresence,
                enemyLocomotionCooldownTicks = enemyLocomotionCooldownTicks,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities boxCapabilities = BoxCapabilities.None)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                boxCapabilities = boxCapabilities,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private sealed class RightPushPlayerLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            public RightPushPlayerLogic(int entityId)
            {
                ControlledEntityId = entityId;
            }

            public int ControlledEntityId { get; }

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (input.PlayerCommand.MoveDirection != Direction.Right ||
                    !snapshot.TryGetEntity(ControlledEntityId, out var player))
                {
                    return;
                }

                buffer.Add(new RawMovementIntent(
                    ControlledEntityId,
                    priority: 100,
                    player.position.PlanarPosition + Vector2Int.right,
                    MovementCommandKind.Push));
            }
        }

        private static UnitKinematicRuntimeState CreateCommitTickKinematicContinuationState()
        {
            return new UnitKinematicRuntimeState
            {
                localOffset = new SimulationOffset2(SimulationFixed.FromRaw(1024), SimulationFixed.Zero),
                velocity = new SimulationVelocity2(SimulationFixed.FromRaw(1024), SimulationFixed.Zero),
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

        private static EnemyJumpRuntimeState CreatePostLandingCooldownJumpState()
        {
            return CreateJumpState(EnemyJumpPhase.Cooldown, landingTick: 1);
        }

        private static EnemyJumpRuntimeState CreateJumpState(EnemyJumpPhase phase, int landingTick)
        {
            return new EnemyJumpRuntimeState
            {
                phase = phase,
                sequence = 1,
                sourceCell = new SurfaceCell(FaceId.Floor, 11, 4),
                lockedTargetCell = new SurfaceCell(FaceId.Floor, 13, 4),
                windupEndTick = 0,
                landingTick = landingTick,
                cooldownRemainingTicks = phase == EnemyJumpPhase.Cooldown ? 4 : 0,
            };
        }
    }
}

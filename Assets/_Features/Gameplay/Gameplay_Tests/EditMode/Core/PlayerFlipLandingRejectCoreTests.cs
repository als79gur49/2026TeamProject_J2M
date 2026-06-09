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
    public sealed class PlayerFlipLandingRejectCoreTests
    {
        [Test]
        [Category("Core")]
        public void PlayerControlQueries_FlipLandingSolid_RejectsTargetAndClassifiesBlockedAttempt()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 2, 0), BoxCapabilities.Flip),
                CreateWall(30, new SurfaceCell(FaceId.Floor, 0, 0)),
            });
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);

            var canStartFlip = PlayerControlQueries.TryResolveFlipTarget(
                snapshot,
                player,
                Direction.Right,
                tickIndex: 1,
                out _);
            var blockedAttempt = PlayerControlQueries.TryResolveBlockedFlipLandingTarget(
                snapshot,
                player,
                player.position,
                Direction.Right,
                tickIndex: 1,
                out var target);

            Assert.That(canStartFlip, Is.False);
            Assert.That(blockedAttempt, Is.True);
            Assert.That(target.TargetEntityId, Is.EqualTo(20));
            Assert.That(target.Direction, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAttemptSignal_VisualFeedbackDefaultsOn_AndCanBeSuppressed()
        {
            var defaultSignal = new TickPlayerActionAttemptPresentationSignal(
                10,
                PlayerActionKind.Flip,
                Direction.Right,
                PlayerActionAttemptFeedbackKind.NoTarget);
            var audioOnlySignal = new TickPlayerActionAttemptPresentationSignal(
                10,
                PlayerActionKind.Flip,
                Direction.Right,
                PlayerActionAttemptFeedbackKind.Invalid,
                targetEntityId: 20,
                hasTarget: true,
                emitsVisualFeedback: false);

            Assert.That(defaultSignal.EmitsVisualFeedback, Is.True);
            Assert.That(audioOnlySignal.EmitsVisualFeedback, Is.False);
            Assert.That(audioOnlySignal.HasTarget, Is.True);
            Assert.That(audioOnlySignal.TargetEntityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void PlayerControlQueries_PushLockedTarget_ClassifiesBlockedInteraction()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 2, 0), BoxCapabilities.Push),
            });
            worldState.CreateWriteContext().SetBoxInteractionLockState(
                20,
                new BoxInteractionLockState(
                    30,
                    sourceEffectIndex: 0,
                    expiresTickExclusive: 8,
                    blocksPush: true,
                    blocksFlip: false));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);

            var canStartPush = PlayerControlQueries.TryResolvePushContact(
                snapshot,
                player,
                Direction.Right,
                tickIndex: 1,
                out _);
            var lockedAttempt = PlayerControlQueries.TryResolveBoxInteractionLockedTarget(
                snapshot,
                player,
                player.position,
                PlayerQueuedFree2DActionKind.Push,
                Direction.Right,
                tickIndex: 1,
                out var target);

            Assert.That(canStartPush, Is.False);
            Assert.That(lockedAttempt, Is.True);
            Assert.That(target.TargetEntityId, Is.EqualTo(20));
            Assert.That(target.Direction, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Core")]
        public void PlayerInteractionLockedPush_RejectsBeforeActionStart_AndEmitsAudioOnlyAttempt()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 2, 0), BoxCapabilities.Push),
            });
            worldState.CreateWriteContext().SetBoxInteractionLockState(
                20,
                new BoxInteractionLockState(
                    30,
                    sourceEffectIndex: 0,
                    expiresTickExclusive: 8,
                    blocksPush: true,
                    blocksFlip: true));

            var result = GameplayCompositionRoot.CreateTickPipeline(
                    worldState,
                    new IEntityLogic[]
                    {
                        new PlayerLogic(10),
                    })
                .RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var attempts = result.PresentationData.PlayerActionAttemptSignals;

            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
            Assert.That(attempts, Has.Count.EqualTo(1));
            Assert.That(attempts[0].ActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(attempts[0].FeedbackKind, Is.EqualTo(PlayerActionAttemptFeedbackKind.Invalid));
            Assert.That(attempts[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(attempts[0].HasTarget, Is.True);
            Assert.That(attempts[0].EmitsVisualFeedback, Is.False);
        }

        [Test]
        [Category("Core")]
        public void PlayerInteractionLockedFlip_RejectsBeforeActionStart_AndEmitsAudioOnlyAttempt()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 2, 0), BoxCapabilities.Flip),
            });
            worldState.CreateWriteContext().SetBoxInteractionLockState(
                20,
                new BoxInteractionLockState(
                    30,
                    sourceEffectIndex: 0,
                    expiresTickExclusive: 8,
                    blocksPush: true,
                    blocksFlip: true));

            var result = GameplayCompositionRoot.CreateTickPipeline(
                    worldState,
                    new IEntityLogic[]
                    {
                        new PlayerLogic(10),
                    })
                .RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var attempts = result.PresentationData.PlayerActionAttemptSignals;

            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
            Assert.That(result.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(attempts, Has.Count.EqualTo(1));
            Assert.That(attempts[0].ActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(attempts[0].FeedbackKind, Is.EqualTo(PlayerActionAttemptFeedbackKind.Invalid));
            Assert.That(attempts[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(attempts[0].HasTarget, Is.True);
            Assert.That(attempts[0].EmitsVisualFeedback, Is.False);
        }

        [Test]
        [Category("Core")]
        public void NebulousActiveSameCell_LeftRightFlipBoxes_RejectsAsBlockedAttemptWithoutStartingFlip()
        {
            // Same-cell Nebulous coverage protects primary-unit/actor identity semantics.
            // Landing-cell active glide BoxFlip policy is covered in PlayerFlipActiveGlideLandingCoreTests.
            var playerCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, playerCell),
                CreateNebulous(5, playerCell),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 1), BoxCapabilities.Flip),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 3, 1), BoxCapabilities.Flip),
            });
            worldState.CreateWriteContext().SetPhasedState(
                5,
                PhasedRuntimeStateQueries.ForceDebug(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPrimaryUnitAt(playerCell, out var primary), Is.True);
            Assert.That(primary.entityId, Is.EqualTo(5));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(PlayerControlQueries.TryResolveFlipTarget(snapshot, player, Direction.Left, tickIndex: 1, out _), Is.False);
            Assert.That(
                PlayerControlQueries.TryResolveBlockedFlipLandingTarget(
                    snapshot,
                    player,
                    player.position,
                    Direction.Left,
                    tickIndex: 1,
                    out var blockedTarget),
                Is.True);
            Assert.That(blockedTarget.TargetEntityId, Is.EqualTo(20));

            var result = GameplayCompositionRoot.CreateTickPipeline(
                    worldState,
                    new IEntityLogic[]
                    {
                        new PlayerLogic(10),
                    })
                .RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));

            Assert.That(result.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(result.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Has.None.Contains("Source=5"));
            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.FlipImpactSignals, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].EntityId, Is.EqualTo(10));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].TargetEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].FeedbackKind, Is.EqualTo(PlayerActionAttemptFeedbackKind.Invalid));
        }

        [Test]
        [Category("Core")]
        public void NebulousActiveSameCell_OneSidedFlipBox_ExecutesWithPlayerActorAndIgnoresPrimaryUnit()
        {
            // Same-cell Nebulous coverage protects primary-unit/actor identity semantics.
            // Landing-cell active glide BoxFlip policy is covered in PlayerFlipActiveGlideLandingCoreTests.
            var playerCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var landingCell = new SurfaceCell(FaceId.Floor, 3, 1);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, playerCell),
                CreateUnit(5, playerCell),
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
            });
            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var executeResult = pipeline.RunTick(new TickInput(2));
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(startResult.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
            Assert.That(startResult.PresentationData.PlayerActionSignals[0].EntityId, Is.EqualTo(10));
            Assert.That(executeResult.MovementPhaseResult.RawIntents, Has.Count.EqualTo(1));
            Assert.That(executeResult.MovementPhaseResult.RawIntents[0].SourceId, Is.EqualTo(10));
            Assert.That(executeResult.MovementPhaseResult.SortedIntents, Has.Count.EqualTo(1));
            Assert.That(executeResult.MovementPhaseResult.SortedIntents[0].SourceId, Is.EqualTo(10));
            Assert.That(executeResult.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(executeResult.MovementPhaseResult.CommitEvents, Has.Some.Contains("MoveCommitted").And.Contains("E=20"));
            Assert.That(executeResult.MovementPhaseResult.CommitEvents, Has.None.Contains("Source=5"));
            Assert.That(executeResult.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(landingCell, out var landedBox), Is.True);
            Assert.That(landedBox.Entity.entityId, Is.EqualTo(20));
            Assert.That(finalSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(playerCell));
            Assert.That(finalSnapshot.TryGetEntity(5, out var nebulous), Is.True);
            Assert.That(nebulous.position, Is.EqualTo(playerCell));
        }

        [Test]
        [Category("Core")]
        public void NebulousEnemyActiveSameCell_OneSidedFlipBox_PlayerFlipSourceDoesNotResumeEnemyGroundLocomotion()
        {
            // Same-cell Nebulous coverage protects movement intent source separation and phased baseline suppression.
            // Landing-cell active glide BoxFlip policy is covered in PlayerFlipActiveGlideLandingCoreTests.
            var playerCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, playerCell),
                CreateNebulous(5, playerCell),
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
                });
            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var executeResult = pipeline.RunTick(new TickInput(2));

            Assert.That(startResult.PresentationData.PlayerActionSignals, Has.Count.EqualTo(1));
            Assert.That(startResult.PresentationData.PlayerActionSignals[0].EntityId, Is.EqualTo(10));
            Assert.That(
                executeResult.MovementPhaseResult.RawIntents,
                Has.Exactly(1).Matches<RawMovementIntent>(
                    intent => intent.SourceId == 10 && intent.CommandKind == MovementCommandKind.Flip));
            Assert.That(
                executeResult.MovementPhaseResult.RawIntents,
                Has.None.Matches<RawMovementIntent>(
                    intent => intent.SourceId == 5 && intent.CommandKind == MovementCommandKind.Move));
            Assert.That(executeResult.MovementPhaseResult.RejectedReasons, Has.None.Contains("Source=10"));
        }

        [Test]
        [Category("Core")]
        public void NebulousActiveSameCell_SpatialSemantics_RemainQueryVisibleButNotImpactSelectable()
        {
            // Same-cell Nebulous coverage protects spatial query visibility vs impact targetability.
            // Landing-cell active glide BoxFlip policy is covered in PlayerFlipActiveGlideLandingCoreTests.
            var playerCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, playerCell),
                CreateNebulous(5, playerCell),
            });
            worldState.CreateWriteContext().SetPhasedState(
                5,
                PhasedRuntimeStateQueries.ForceDebug(default, tickIndex: 1));
            var snapshot = worldState.CreateSnapshot();
            var units = new List<EntityState>();
            var impactTargets = new List<EntityState>();

            snapshot.EnumerateUnitsAt(playerCell, units);
            snapshot.EnumerateUnitImpactTargetsAt(playerCell, impactTargets);

            Assert.That(units.ConvertAll(unit => unit.entityId), Is.EqualTo(new[] { 5, 10 }));
            Assert.That(impactTargets.ConvertAll(unit => unit.entityId), Is.EqualTo(new[] { 10 }));
            Assert.That(snapshot.TryGetResolvedSpatialState(5, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Phased));
            Assert.That(GameplayEntityQueryPolicy.ShouldParticipateInGameplayQueries(spatialState), Is.True);
            Assert.That(ModifierQuery.ShouldParticipateInTraversalBlocking(spatialState), Is.True);
            Assert.That(ModifierQuery.ShouldParticipateInSettlementBlocking(spatialState), Is.True);
            Assert.That(GameplayEntityQueryPolicy.ShouldParticipateInTargetSelection(spatialState), Is.False);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(5, 5)),
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position, int teamId = 1)
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
            };
        }

        private static EntityState CreatePlayer(int entityId, SurfaceCell position)
        {
            var entity = CreateUnit(entityId, position, teamId: 1);
            entity.unitRole = UnitRole.Player;
            return entity;
        }

        private static EntityState CreateNebulous(int entityId, SurfaceCell position)
        {
            var entity = CreateUnit(entityId, position, teamId: 2);
            entity.unitRole = UnitRole.Enemy;
            entity.aiMode = EnemyAiMode.None;
            return entity;
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
            };
        }
    }
}

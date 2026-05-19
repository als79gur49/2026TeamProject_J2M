using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
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

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)),
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
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

using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class PlayerContinuousLocomotionScenarioTests
    {
        [Test]
        [Category("Extended")]
        public void Player_Free2D_StartRight_FromCenter()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.GreaterThan(0));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Moving));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track =>
                track.EntityId == 10 &&
                track.DestinationAnchorCell == new SurfaceCell(FaceId.Floor, 0, 0) &&
                track.DestinationLocalOffset.X.RawValue == state.localOffset.X.RawValue), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_ReleaseInput_HoldsCurrentLocalPoint()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var before = worldState.CreateSnapshot();
            Assert.That(before.TryGetUnitContinuousLocomotionState(10, out var moving), Is.True);

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            var after = worldState.CreateSnapshot();

            Assert.That(after.TryGetUnitContinuousLocomotionState(10, out var idle), Is.True);
            Assert.That(idle.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(idle.velocity, Is.EqualTo(KinematicVelocity2.Zero));
            Assert.That(idle.localOffset, Is.EqualTo(moving.localOffset));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RightOffset_ThenUpInput_MovesImmediatelyUp()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var rightSnapshot = worldState.CreateSnapshot();
            Assert.That(rightSnapshot.TryGetUnitContinuousLocomotionState(10, out var rightState), Is.True);

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var upSnapshot = worldState.CreateSnapshot();

            Assert.That(upSnapshot.TryGetUnitContinuousLocomotionState(10, out var upState), Is.True);
            Assert.That(upState.localOffset.X.RawValue, Is.EqualTo(rightState.localOffset.X.RawValue));
            Assert.That(upState.localOffset.Y.RawValue, Is.GreaterThan(0));
            Assert.That(upState.lastMoveDirection, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RightOffset_ThenLeftInput_MovesBackImmediately()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var rightSnapshot = worldState.CreateSnapshot();
            Assert.That(rightSnapshot.TryGetUnitContinuousLocomotionState(10, out var rightState), Is.True);

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Left)));
            var leftSnapshot = worldState.CreateSnapshot();

            Assert.That(leftSnapshot.TryGetUnitContinuousLocomotionState(10, out var leftState), Is.True);
            Assert.That(leftState.localOffset.X.RawValue, Is.LessThan(rightState.localOffset.X.RawValue));
            Assert.That(leftState.lastMoveDirection, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_CrossHalfBoundary_NormalizesAnchor()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MinLocalOffset));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_ApproachBox_ClampsAtBoundary()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_UnitOverlap_DoesNotBlock()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2));
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_LocalNonZero_PushFlipRejected()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_FlagOff_ExistingKinematicBaseline()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerStoppableKinematicLocomotionEnabled);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.True);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
        }

        private static TickPipeline CreatePipeline(WorldState worldState)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);
        }

        private static IEntityLogic[] CreatePlayerLogics()
        {
            return new IEntityLogic[]
            {
                new PlayerLogic(10),
                new PlayerControlStateLogic(10),
            };
        }

        private static WorldState CreateWorldState(params EntityState[] entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities);
        }

        private static EntityState CreatePlayer(int entityId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position, int teamId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                facing = Direction.Left,
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
                type = EntityType.Box,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
